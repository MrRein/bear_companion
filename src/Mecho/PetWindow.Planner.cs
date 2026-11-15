using System;
using System.Linq;
using Mecho.Core;

namespace Mecho;

// Задачите по дни: седмицата (PlannerWindow), местене от ден в ден, етикети, важни, време,
// и пренасянето на недовършените за днес.
public sealed partial class PetWindow
{
    private PlannerWindow? _planner;

    /// <summary>Днешният ден за задачите (по часовника на ядрото – Developer може да го мести).</summary>
    public DateTime TaskToday => CoreEngine.Clock.Today();

    public TasksState TaskState => CoreEngine.Store.Get<TasksState>();

    public bool PlannerIsOpen => _planner != null;

    /// <summary>Отваря седмицата (или я вдига отпред, ако е отворена).</summary>
    public void OpenPlanner() => OpenPlanner("");

    /// <summary>Отваря задачите; side: "plan" – „План за деня“, "shutdown" – „Край на деня“.</summary>
    public void OpenPlanner(string side)
    {
        if (_menu is { IsOpen: true }) _menu.Hide();
        if (_planner == null)
        {
            _planner = new PlannerWindow(this);
            _planner.Closed += (_, _) =>
            {
                _planner = null;
                _save.Planner.Open = false;
                Persist();
            };
            _save.Planner.Open = true;
            _planner.Show();
        }
        else
        {
            if (_planner.WindowState == System.Windows.WindowState.Minimized) _planner.WindowState = System.Windows.WindowState.Normal;
            _planner.Activate();
        }
        if (side.Length > 0) _planner.OpenSide(side);
        Persist();
    }

    public void ClosePlanner() => _planner?.Close();

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

    /// <summary>Повтаря се: TaskRepeat.Daily, Weekdays, Weekly, Monthly ("" – не).</summary>
    public void SetTaskRepeat(TaskItem task, string repeat) => TaskCommand(new SetTaskRepeat(task.Id, repeat));

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

    /// <summary>При пускане: седмицата беше отворена – пак се показва.</summary>
    private void RestorePlanner()
    {
        if (_save.Planner.Open && !SelfTest.Running) OpenPlanner();
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

    /// <summary>„Редактирай…“ от таблото: менюто, направо на задачата.</summary>
    public void EditTaskInMenu(TaskItem task)
    {
        OpenMenu(MenuWindow.TasksTab);
        _menu!.ShowNewTask(true, task);
    }

    /// <summary>Колко задачи са за днес и колко са свършени, и колко време са (за обобщенията).</summary>
    public (int Done, int All, int Minutes) TodayLoad()
    {
        var today = TaskState.OnDay(TaskToday).ToList();
        return (today.Count(t => t.Done), today.Count, today.Where(t => !t.Done).Sum(t => t.Minutes));
    }
}
