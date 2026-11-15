using System;
using System.Linq;
using Mecho.Core;

namespace Mecho;

// Задачите по дни: „Работа“ в менюто (WorkView), местене от ден в ден, етикети, важни, време,
// и пренасянето на недовършените за днес.
public sealed partial class PetWindow
{
    /// <summary>Днешният ден за задачите (по часовника на ядрото – Developer може да го мести).</summary>
    public DateTime TaskToday => CoreEngine.Clock.Today();

    public TasksState TaskState => CoreEngine.Store.Get<TasksState>();

    /// <summary>„Работа“ (в менюто): отворена ли е сега.</summary>
    public bool PlannerIsOpen => _menu is { IsOpen: true } m && m.IsWorkPage;

    /// <summary>Отваря „Работа“ на „Днес“.</summary>
    public void OpenPlanner() => OpenPlanner("");

    /// <summary>
    /// Отваря „Работа“ (менюто става голямо): "" – Днес, "plan" – План за деня, "shutdown" – Край
    /// на деня, "week" – Седмица, "review" – Преглед на седмицата.
    /// </summary>
    public void OpenPlanner(string side) => OpenMenuPage(side switch
    {
        "plan" => MenuWindow.PagePlan,
        "shutdown" => MenuWindow.PageShutdown,
        "week" => MenuWindow.PageWeek,
        "review" => MenuWindow.PageReview,
        _ => MenuWindow.PageToday,
    });

    public void ClosePlanner()
    {
        if (PlannerIsOpen) _menu!.Hide();
    }

    /// <summary>Самопроверката: „Работа“ (в менюто).</summary>
    internal WorkView? Work => _menu?.Work;

    /// <summary>Мащабът е сменен („Още“ → Размер): и таблата.</summary>
    public void ZoomChanged()
    {
        foreach (var p in _pins.Values) p.ApplyZoom();
    }

    /// <summary>Мести задачата в ден (null – „Някога“), преди before (или накрая на деня).</summary>
    public void MoveTask(TaskItem task, DateTime? day, Guid? before = null)
    {
        if (!CoreEngine.Bus.Send(new MoveTask(task.Id, day, before)).Ok) return;
        NotifyCare();
        Persist();
    }

    /// <summary>Маркира: етикет ("" – без), важна, време (0 – не е казано), големина.</summary>
    public void MarkTask(TaskItem task, string? tag = null, bool? star = null, int? minutes = null, int? size = null)
    {
        if (!CoreEngine.Bus.Send(new MarkTask(task.Id, tag, star, minutes, size)).Ok) return;
        if (star == true) Emote("sparkle", 1);
        NotifyCare();
        Persist();
    }

    // ── Етикетите (свои имена и цветове) ──

    /// <summary>Нов етикет; връща ключа му (null – празно име или са твърде много).</summary>
    public string? AddTaskTag(string name, System.Windows.Media.Color color)
    {
        var key = TaskTags.Add(name, color);
        if (key != null) TagsChanged();
        return key;
    }

    public void RenameTaskTag(string key, string name)
    {
        TaskTags.Rename(key, name);
        TagsChanged();
    }

    public void RecolorTaskTag(string key, System.Windows.Media.Color color)
    {
        TaskTags.Recolor(key, color);
        TagsChanged();
    }

    /// <summary>Маха етикета; задачите с него остават без етикет.</summary>
    public void DeleteTaskTag(string key)
    {
        foreach (var t in TaskState.Open.Where(t => t.Tag == key).ToList())
            CoreEngine.Bus.Send(new MarkTask(t.Id, "", null, null, null));
        TaskTags.Remove(key);
        TagsChanged();
    }

    private void TagsChanged()
    {
        NotifyCare();
        _menu?.Work.Refresh(true);
        Persist();
    }

    /// <summary>Праща команда за задачите; ако мине – опреснява и записва.</summary>
    private bool TaskCommand(IGameCommand command)
    {
        if (!CoreEngine.Bus.Send(command).Ok) return false;
        NotifyCare();
        Persist();
        return true;
    }

    public void AddTaskStep(TaskItem task, string title) => TaskCommand(new AddTaskStep(task.Id, title));

    public void SetTaskStepDone(TaskItem task, Guid step, bool done)
    {
        if (TaskCommand(new SetTaskStepDone(task.Id, step, done)) && done) Emote("sparkle", 1);
    }

    public void RenameTaskStep(TaskItem task, Guid step, string title) => TaskCommand(new RenameTaskStep(task.Id, step, title));

    public void DeleteTaskStep(TaskItem task, Guid step) => TaskCommand(new DeleteTaskStep(task.Id, step));

    public void DuplicateTask(TaskItem task) => TaskCommand(new DuplicateTask(task.Id));

    /// <summary>Връща задачата, както е била (за „Отмени“).</summary>
    public void PutTask(TaskItem task, int index) => TaskCommand(new PutTask(task, index));

    /// <summary>Всички несвършени от деня → в друг ден (null – „Някога“).</summary>
    public void MoveUnfinished(DateTime from, DateTime? to) => TaskCommand(new MoveUnfinished(from, to));

    /// <summary>Работата (мечо-доро или работен таймер) свърши, а имаше задача – минутите отиват към нея.</summary>
    private void AddFocusTaskTime(double minutes)
    {
        if (FocusTask is { } t && minutes >= 1) CoreEngine.Bus.Send(new AddTaskTime(t.Id, (int)Math.Round(minutes)));
    }

    /// <summary>„План за деня“ е готов: мечокът се радва (веднъж на ден).</summary>
    public void PlannedToday()
    {
        string key = TaskToday.ToString("yyyy-MM-dd");
        bool first = _save.PlannedDay != key;
        _save.PlannedDay = key;
        if (first)
        {
            var (_, all, minutes) = TodayLoad();
            Say(R.Text("tasks.planned", _save.OwnerName, ("брой", all.ToString())) + (minutes > 0 ? $" (~{TaskCards.Duration(minutes)})" : ""), 5);
            React("happy");
        }
        Persist();
    }

    /// <summary>„Край на деня“: записва как е минал и мечокът казва нещо хубаво.</summary>
    public void ShutdownToday(string note)
    {
        string key = TaskToday.ToString("yyyy-MM-dd");
        note = note.Trim();
        if (note.Length > 0) _save.DayNotes[key] = note.Length > 2000 ? note[..2000] : note;
        else _save.DayNotes.Remove(key);
        bool first = _save.ShutdownDay != key;
        _save.ShutdownDay = key;
        if (first)
        {
            var (done, _, _) = TodayLoad();
            Say(R.Text(done > 0 ? "tasks.shutdown" : "tasks.shutdown.none", _save.OwnerName, ("брой", done.ToString())), 6);
            React(done > 0 ? "dance" : "happy", done > 0 ? 2 : 0);
        }
        NotifyCare();
        Persist();
    }

    // ── Прегледът на седмицата ──

    public const int MaxWeekGoals = 5;

    private static string WeekKey(DateTime monday) => monday.ToString("yyyy-MM-dd");

    /// <summary>Прегледът на седмицата, която почва в този понеделник (празен, ако го няма).</summary>
    public WeekReview WeekReviewFor(DateTime monday) =>
        _save.WeekReviews.TryGetValue(WeekKey(monday), out var r) ? r : new WeekReview();

    private WeekReview EnsureWeekReview(DateTime monday)
    {
        string key = WeekKey(monday);
        if (!_save.WeekReviews.TryGetValue(key, out var r)) _save.WeekReviews[key] = r = new WeekReview();
        return r;
    }

    public void SetWeekThoughts(DateTime monday, string good, string better)
    {
        var r = EnsureWeekReview(monday);
        r.Good = Clip(good, 2000);
        r.Better = Clip(better, 2000);
        Persist();
    }

    private static string Clip(string text, int max)
    {
        text = (text ?? "").Trim();
        return text.Length > max ? text[..max] : text;
    }

    public bool AddWeekGoal(DateTime monday, string text)
    {
        text = Clip(text, 120);
        var r = EnsureWeekReview(monday);
        if (text.Length == 0 || r.Goals.Count >= MaxWeekGoals) return false;
        r.Goals.Add(new WeekGoal { Text = text });
        NotifyCare();
        Persist();
        return true;
    }

    public void SetWeekGoalDone(DateTime monday, Guid id, bool done)
    {
        if (EnsureWeekReview(monday).Goals.FirstOrDefault(g => g.Id == id) is not { } g || g.Done == done) return;
        g.Done = done;
        if (done) Emote("sparkle", 1);
        NotifyCare();
        Persist();
    }

    public void DeleteWeekGoal(DateTime monday, Guid id)
    {
        EnsureWeekReview(monday).Goals.RemoveAll(g => g.Id == id);
        NotifyCare();
        Persist();
    }

    /// <summary>Прегледът е направен: мечокът казва нещо хубаво (веднъж на седмица).</summary>
    public void FinishWeekReview(DateTime monday)
    {
        var r = EnsureWeekReview(monday);
        bool first = !r.Done;
        r.Done = true;
        if (first)
        {
            int done = Enumerable.Range(0, 7).SelectMany(i => TaskState.OnDay(monday.AddDays(i))).Count(t => t.Done);
            Say(R.Text(done > 0 ? "week.reviewed" : "week.reviewed.none", _save.OwnerName, ("брой", done.ToString())), 6);
            React(done > 0 ? "dance" : "happy", done > 0 ? 2 : 0);
        }
        NotifyCare();
        Persist();
    }

    /// <summary>Вика се веднъж от конструктора (преди ядрото да тръгне – пренасянето при пускане също се чува).</summary>
    private void WirePlanner()
    {
        CoreEngine.Bus.Subscribe<TasksRolledOver>(this, s =>
        {
            NotifyCare();
            // След малко (при пускане – след поздрава); заспал – не го буди.
            Later(9, () =>
            {
                if (IsAsleep || OnWalk || SelfTest.Running) return;
                Say(R.Text("tasks.rolled", _save.OwnerName, ("брой", s.Count.ToString())), 5);
            });
        });
        TaskCards.DragEnded += () => NotifyCare();
    }


    /// <summary>Менюто е отворено или току-що се затвори (цъкането другаде го затваря, преди копчето да разбере).</summary>
    public bool MenuOpenOrJustClosed => MenuIsOpen || Now - _menuClosedAt < 0.3;

    /// <summary>Копчето с мечока в таблото: отваря менюто, а ако е било отворено – затваря го.</summary>
    public void ToggleMenuFrom(int tab, bool wasOpen)
    {
        if (wasOpen)
        {
            if (MenuIsOpen) _menu!.Hide();
            return;
        }
        OpenMenu(tab);
    }

    /// <summary>„Редактирай…“ от таблото: „Работа“, с подробностите на задачата.</summary>
    public void EditTaskInMenu(TaskItem task)
    {
        OpenMenuPage(task.Day == TaskToday ? MenuWindow.PageToday : MenuWindow.PageWeek);
        _menu!.Work.SelectTask(task.Id);
    }

    /// <summary>Колко задачи са за днес и колко са свършени, и колко време са (за обобщенията).</summary>
    public (int Done, int All, int Minutes) TodayLoad()
    {
        var today = TaskState.OnDay(TaskToday).ToList();
        return (today.Count(t => t.Done), today.Count, today.Where(t => !t.Done).Sum(t => t.Minutes));
    }
}
