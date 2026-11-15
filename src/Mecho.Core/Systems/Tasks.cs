using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mecho.Core;

// ─────────────────────────────────────────────────────────────────────────────
// ЗАДАЧИТЕ: списъкът за днес, историята на свършените и наградите.
//
//     bus.Send(new AddTask("Скица за корицата", 3, "две версии"));
//     bus.Send(new SetTaskDone(id, true));    // +награда (1/3/5 лешника), брояч, TaskCompleted
//     bus.Send(new SetTaskDone(id, false));   // отмяна: наградата се връща
//     bus.Send(new SetFocusTask(id));         // по нея е мечо-дорото / работният таймер
//
//     bus.Send(new MoveTask(id, monday, before));   // в друг ден (null – „Някога“), преди дадена задача
//     bus.Send(new MarkTask(id, Tag: "work", Star: true, Minutes: 30));
//
// Правила тук: голяма задача дава шанс за страница само при първото „свършено“
// (TaskCompleted.FirstBigCompletion); свършените от минали дни отиват в историята
// сами в полунощ (DayStarted) – най-новите отпред, до 2000. Несвършените от минал ден
// се пренасят за днес (Rolled + 1, TasksRolledOver). Редът в Open е редът в деня.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Една задача. Size: 1 – малка, 2 – средна, 3 – голяма. PageRolled – вече е дала шанс за страница.
/// Day – денят, за който е (само датата; null – „Някога“), Tag – етикетът (ключ, "" – без),
/// Star – важна, Minutes – колко време ще отнеме (0 – не е казано), Rolled – колко пъти е
/// пренасяна от ден за ден.
/// Steps – стъпките (подзадачите), Repeat – повтаря се (TaskRepeat), Series – общото на
/// повторенията, Spent – колко минути е работено по нея (мечо-доро, таймер), Created – кога е написана.
/// </summary>
public sealed record TaskEntry(Guid Id, string Title, string Description, int Size, bool Done, DateTime? DoneAt, bool PageRolled,
    DateTime? Day = null, string Tag = "", bool Star = false, int Minutes = 0, int Rolled = 0,
    ImmutableList<TaskStep>? Steps = null, string Repeat = "", Guid? Series = null, int Spent = 0, DateTime? Created = null)
{
    public int Reward => Size switch { 3 => 5, 2 => 3, _ => 1 };

    /// <summary>Стъпките (никога null).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public ImmutableList<TaskStep> StepList => Steps ?? ImmutableList<TaskStep>.Empty;

    // Стъпките се сравняват по съдържание (иначе една и съща задача след запис и четене е „различна“).
    public bool Equals(TaskEntry? o) =>
        o is not null && Id == o.Id && Title == o.Title && Description == o.Description && Size == o.Size && Done == o.Done
        && DoneAt == o.DoneAt && PageRolled == o.PageRolled && Day == o.Day && Tag == o.Tag && Star == o.Star && Minutes == o.Minutes
        && Rolled == o.Rolled && StepList.SequenceEqual(o.StepList) && Repeat == o.Repeat && Series == o.Series && Spent == o.Spent
        && Created == o.Created;

    public override int GetHashCode() => HashCode.Combine(Id, Title, Done, Day, Tag, Star, StepList.Count, Spent);

    /// <summary>Кратък подпис на всичко видимо (за „сменило ли се е нещо“ в прозорците).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string Signature =>
        $"{Id}|{Title}|{Description}|{Size}|{Done}|{Day:yyyyMMdd}|{Tag}|{Star}|{Minutes}|{Rolled}|{Repeat}|{Spent}|" +
        string.Join(",", StepList.Select(x => (x.Done ? "+" : "-") + x.Title));
}

/// <summary>Стъпка от задача (подзадача).</summary>
public sealed record TaskStep(Guid Id, string Title, bool Done);

/// <summary>Как се повтаря задача: "" – не, всеки ден, делнични, всяка седмица, всеки месец.</summary>
public static class TaskRepeat
{
    public const string None = "", Daily = "daily", Weekdays = "weekdays", Weekly = "weekly", Monthly = "monthly";

    public static readonly string[] All = { None, Daily, Weekdays, Weekly, Monthly };

    public static bool IsValid(string? r) => r != null && All.Contains(r);

    /// <summary>Пада ли се на този ден (anchor – първият ден на поредицата).</summary>
    public static bool Due(string repeat, DateTime anchor, DateTime day) => repeat switch
    {
        Daily => true,
        Weekdays => day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday),
        Weekly => day.DayOfWeek == anchor.DayOfWeek,
        Monthly => day.Day == Math.Min(anchor.Day, DateTime.DaysInMonth(day.Year, day.Month)),
        _ => false,
    };
}

/// <summary>Open – днешните (и несвършените), History – свършените от минали дни, Focus – текущата.</summary>
public sealed record TasksState(ImmutableList<TaskEntry> Open, ImmutableList<TaskEntry> History, int DoneTotal, Guid? Focus) : IStateSlice
{
    public static TasksState Empty { get; } = new(ImmutableList<TaskEntry>.Empty, ImmutableList<TaskEntry>.Empty, 0, null);

    public TaskEntry? Find(Guid id) => Open.FirstOrDefault(t => t.Id == id);

    /// <summary>Задачите за деня (в реда им), заедно със свършените, които вече са в историята.</summary>
    public IEnumerable<TaskEntry> OnDay(DateTime day)
    {
        day = day.Date;
        return Open.Where(t => t.Day == day).Concat(History.Where(t => (t.Day ?? t.DoneAt?.Date) == day).Reverse());
    }

    /// <summary>„Някога“: несвършените без ден.</summary>
    public IEnumerable<TaskEntry> Someday => Open.Where(t => t.Day == null && !t.Done);

    /// <summary>Текущата задача, ако още не е свършена.</summary>
    public TaskEntry? FocusTask => Focus is { } id && Find(id) is { Done: false } t ? t : null;
}

// ── Команди ──
/// <summary>Нова задача. Без Day – за днес; Someday – за „Някога“ (без ден).</summary>
public sealed record AddTask(string Title, int Size, string? Description = null, DateTime? Day = null, bool Someday = false,
    string? Tag = null, bool Star = false, int Minutes = 0, Guid? Before = null, string? Repeat = null) : IGameCommand;
/// <summary>Нова стъпка (подзадача) накрая.</summary>
public sealed record AddTaskStep(Guid TaskId, string Title) : IGameCommand;
public sealed record SetTaskStepDone(Guid TaskId, Guid StepId, bool Done) : IGameCommand;
public sealed record RenameTaskStep(Guid TaskId, Guid StepId, string Title) : IGameCommand;
public sealed record DeleteTaskStep(Guid TaskId, Guid StepId) : IGameCommand;
/// <summary>Повтаря се (TaskRepeat; "" – спира). Задача без ден става за днес.</summary>
public sealed record SetTaskRepeat(Guid Id, string Repeat) : IGameCommand;
/// <summary>Още минути работа по задачата (мечо-доро или таймер по нея).</summary>
public sealed record AddTaskTime(Guid Id, int Minutes) : IGameCommand;
/// <summary>Копие (несвършено, без повторение) точно след нея.</summary>
public sealed record DuplicateTask(Guid Id) : IGameCommand;
/// <summary>Слага задачата обратно, както е била (за „Отмени“): на мястото index, или я сменя, ако я има.</summary>
public sealed record PutTask(TaskEntry Task, int Index) : IGameCommand;
/// <summary>Всички несвършени от деня From → в To (null – „Някога“).</summary>
public sealed record MoveUnfinished(DateTime From, DateTime? To) : IGameCommand;
/// <summary>Мести задачата в ден (null – „Някога“) – преди Before или накрая на деня.</summary>
public sealed record MoveTask(Guid Id, DateTime? Day, Guid? Before = null) : IGameCommand;
/// <summary>Маркира задачата: етикет, важна, време, големина (null – без промяна).</summary>
public sealed record MarkTask(Guid Id, string? Tag = null, bool? Star = null, int? Minutes = null, int? Size = null) : IGameCommand;
/// <summary>Несвършените от минали дни → за днес (става и само в полунощ и при пускане).</summary>
public sealed record RollOverTasks : IGameCommand;
public sealed record SetTaskDone(Guid Id, bool Done) : IGameCommand;
public sealed record DeleteTask(Guid Id) : IGameCommand;
/// <summary>Поправя име, описание и трудност на несвършена задача (същата задача, същото място).</summary>
public sealed record EditTask(Guid Id, string Title, int Size, string? Description = null) : IGameCommand;
public sealed record SetFocusTask(Guid? Id) : IGameCommand;
/// <summary>Свършените преди днес → в историята (става и само в полунощ).</summary>
public sealed record ArchiveDoneTasks : IGameCommand;
/// <summary>Пряко записване (стари записи, Developer: „изчисти“).</summary>
public sealed record SetTasks(TasksState Value) : IGameCommand;

// ── Сигнали ──
public sealed record TaskAdded(Guid Id, string Title) : ISignal;
/// <summary>FirstBigCompletion – голяма задача, свършена за пръв път (шанс за страница).</summary>
public sealed record TaskCompleted(Guid Id, int Size, int Reward, bool FirstBigCompletion) : ISignal;
public sealed record TaskReopened(Guid Id, int Reward) : ISignal;
public sealed record TaskDeleted(Guid Id) : ISignal;
/// <summary>Толкова несвършени задачи от минали дни се преместиха за днес.</summary>
public sealed record TasksRolledOver(int Count) : ISignal;

/// <summary>Модулът „задачи“ (Id: tasks, зависи от economy – наградите са в лешници).</summary>
public sealed class TasksModule : GameModule
{
    public const string ModuleId = "tasks";
    public const string Key = "tasks";
    public const int MaxHistory = 2000;
    public const int MaxTitle = 200;
    public const int MaxMinutes = 12 * 60;
    public const int MaxTag = 40;

    /// <summary>Версия 2: задачите имат ден (старите несвършени стават за днес, а не „Някога“).</summary>
    public const int Version = 2;

    public override string Id => ModuleId;

    public override IReadOnlyCollection<string> DependsOn { get; } = new[] { EconomyModule.ModuleId };

    protected override void OnRegister()
    {
        Store.Register(this, Key, TasksState.Empty, Version, Migrate);

        Handle<AddTask>(c =>
        {
            string title = (c.Title ?? "").Trim();
            if (title.Length == 0) return CommandResult.Rejected("задачата няма име");
            if (title.Length > MaxTitle) title = title[..MaxTitle];
            DateTime? day = c.Someday ? null : (c.Day ?? Clock.Today()).Date;
            string repeat = TaskRepeat.IsValid(c.Repeat) && day != null ? c.Repeat! : TaskRepeat.None;
            var id = Guid.NewGuid();
            var t = new TaskEntry(id, title, (c.Description ?? "").Trim(), Math.Clamp(c.Size, 1, 3), false, null, false,
                day, CleanTag(c.Tag), c.Star, Math.Clamp(c.Minutes, 0, MaxMinutes),
                Repeat: repeat, Series: repeat.Length > 0 ? id : null, Created: Clock.Now);
            Store.Update<TasksState>(s =>
            {
                int at = c.Before is { } b ? s.Open.FindIndex(x => x.Id == b) : -1;
                return s with { Open = at < 0 ? s.Open.Add(t) : s.Open.Insert(at, t) };
            });
            Emit(new TaskAdded(t.Id, t.Title));
            return CommandResult.Done;
        });
        Handle<SetTaskDone>(c =>
        {
            var s = State<TasksState>();
            if (s.Find(c.Id) is not { } t) return CommandResult.Rejected("няма такава задача");
            if (t.Done == c.Done) return CommandResult.Done;
            if (c.Done)
            {
                bool firstBig = t.Size >= 3 && !t.PageRolled;
                var done = t with { Done = true, DoneAt = Clock.Now, PageRolled = t.PageRolled || t.Size >= 3 };
                Store.Set(s with { Open = s.Open.Replace(t, done), DoneTotal = s.DoneTotal + 1 });
                Send(new EarnNuts(t.Reward, "задача"));
                Emit(new TaskCompleted(t.Id, t.Size, t.Reward, firstBig));
            }
            else
            {
                Store.Set(s with { Open = s.Open.Replace(t, t with { Done = false, DoneAt = null }), DoneTotal = Math.Max(0, s.DoneTotal - 1) });
                // Отмяна: наградата се връща (колкото има – не под нулата).
                int back = Math.Min(t.Reward, Store.TryGet<WalletState>(out var w) ? w!.Nuts : 0);
                if (back > 0) Send(new SpendNuts(back, "отменена задача"));
                Emit(new TaskReopened(t.Id, t.Reward));
            }
            return CommandResult.Done;
        });
        Handle<EditTask>(c =>
        {
            var s = State<TasksState>();
            if (s.Find(c.Id) is not { } t) return CommandResult.Rejected("няма такава задача");
            if (t.Done) return CommandResult.Rejected("свършена е");
            string title = c.Title.Trim();
            if (title.Length == 0) return CommandResult.Rejected("празно име");
            if (title.Length > MaxTitle) title = title[..MaxTitle];
            var edited = t with { Title = title, Size = Math.Clamp(c.Size, 1, 3), Description = (c.Description ?? "").Trim() };
            Store.Set(s with { Open = s.Open.Replace(t, edited) });
            return CommandResult.Done;
        });
        Handle<DeleteTask>(c =>
        {
            var s = State<TasksState>();
            if (s.Find(c.Id) is not { } t) return CommandResult.Rejected("няма такава задача");
            Store.Set(s with { Open = s.Open.Remove(t), Focus = s.Focus == c.Id ? null : s.Focus });
            Emit(new TaskDeleted(c.Id));
            return CommandResult.Done;
        });
        Handle<SetFocusTask>(c =>
        {
            var s = State<TasksState>();
            if (c.Id is { } id && s.Find(id) == null) return CommandResult.Rejected("няма такава задача");
            if (s.Focus != c.Id) Store.Set(s with { Focus = c.Id });
            return CommandResult.Done;
        });
        Handle<MoveTask>(c =>
        {
            var s = State<TasksState>();
            if (s.Find(c.Id) is not { } t) return CommandResult.Rejected("няма такава задача");
            if (c.Before == c.Id) return CommandResult.Done;
            var moved = t with { Day = c.Day?.Date };
            var list = s.Open.Remove(t);
            int at = c.Before is { } b ? list.FindIndex(x => x.Id == b) : -1;
            if (at < 0)
            {
                // Накрая на деня: след последната задача от него (или накрая на списъка).
                int last = list.FindLastIndex(x => x.Day == moved.Day);
                at = last < 0 ? list.Count : last + 1;
            }
            Store.Set(s with { Open = list.Insert(at, moved) });
            return CommandResult.Done;
        });
        Handle<MarkTask>(c =>
        {
            var s = State<TasksState>();
            if (s.Find(c.Id) is not { } t) return CommandResult.Rejected("няма такава задача");
            var marked = t with
            {
                Tag = c.Tag != null ? CleanTag(c.Tag) : t.Tag,
                Star = c.Star ?? t.Star,
                Minutes = c.Minutes is { } m ? Math.Clamp(m, 0, MaxMinutes) : t.Minutes,
                // Големината (а с нея и наградата) – само докато не е свършена.
                Size = c.Size is { } z && !t.Done ? Math.Clamp(z, 1, 3) : t.Size,
            };
            if (marked != t) Store.Set(s with { Open = s.Open.Replace(t, marked) });
            return CommandResult.Done;
        });
        Handle<AddTaskStep>(c =>
        {
            string title = (c.Title ?? "").Trim();
            if (title.Length == 0) return CommandResult.Rejected("стъпката няма име");
            if (title.Length > MaxTitle) title = title[..MaxTitle];
            return Change(c.TaskId, t => t.StepList.Count >= MaxSteps ? null : t with { Steps = t.StepList.Add(new TaskStep(Guid.NewGuid(), title, false)) });
        });
        Handle<SetTaskStepDone>(c => ChangeStep(c.TaskId, c.StepId, x => x with { Done = c.Done }));
        Handle<RenameTaskStep>(c =>
        {
            string title = (c.Title ?? "").Trim();
            if (title.Length == 0) return CommandResult.Rejected("стъпката няма име");
            return ChangeStep(c.TaskId, c.StepId, x => x with { Title = title.Length > MaxTitle ? title[..MaxTitle] : title });
        });
        Handle<DeleteTaskStep>(c => Change(c.TaskId, t =>
            t.StepList.FirstOrDefault(x => x.Id == c.StepId) is { } step ? t with { Steps = t.StepList.Remove(step) } : null));
        Handle<SetTaskRepeat>(c =>
        {
            if (!TaskRepeat.IsValid(c.Repeat)) return CommandResult.Rejected("непознато повторение");
            return Change(c.Id, t => t with
            {
                Repeat = c.Repeat,
                Series = t.Series ?? (c.Repeat.Length > 0 ? t.Id : null),
                Day = c.Repeat.Length > 0 ? t.Day ?? Clock.Today() : t.Day,
            });
        });
        Handle<AddTaskTime>(c =>
        {
            if (c.Minutes <= 0) return CommandResult.Rejected("няма минути");
            return Change(c.Id, t => t with { Spent = Math.Min(t.Spent + c.Minutes, 100 * 60) });
        });
        Handle<DuplicateTask>(c =>
        {
            var s = State<TasksState>();
            if (s.Find(c.Id) is not { } t) return CommandResult.Rejected("няма такава задача");
            var copy = t with
            {
                Id = Guid.NewGuid(), Done = false, DoneAt = null, PageRolled = false, Rolled = 0, Spent = 0,
                Repeat = TaskRepeat.None, Series = null, Created = Clock.Now,
                Steps = t.StepList.Count == 0 ? null : t.StepList.Select(x => x with { Id = Guid.NewGuid(), Done = false }).ToImmutableList(),
                Title = t.Title.Length + 7 <= MaxTitle ? t.Title + " (копие)" : t.Title,
            };
            Store.Set(s with { Open = s.Open.Insert(s.Open.IndexOf(t) + 1, copy) });
            Emit(new TaskAdded(copy.Id, copy.Title));
            return CommandResult.Done;
        });
        Handle<PutTask>(c =>
        {
            ArgumentNullException.ThrowIfNull(c.Task);
            var s = State<TasksState>();
            var list = s.Find(c.Task.Id) is { } old ? s.Open.Remove(old) : s.Open;
            Store.Set(s with { Open = list.Insert(Math.Clamp(c.Index, 0, list.Count), c.Task) });
            return CommandResult.Done;
        });
        Handle<MoveUnfinished>(c =>
        {
            var s = State<TasksState>();
            var from = c.From.Date;
            var moving = s.Open.Where(t => !t.Done && t.Day == from).ToList();
            if (moving.Count == 0) return CommandResult.Done;
            var to = c.To?.Date;
            // Накрая на новия ден, в реда, в който са били.
            var list = s.Open.RemoveRange(moving);
            int last = list.FindLastIndex(x => x.Day == to);
            int at = last < 0 ? list.Count : last + 1;
            Store.Set(s with { Open = list.InsertRange(at, moving.Select(t => t with { Day = to })) });
            return CommandResult.Done;
        });
        Handle<RollOverTasks>(_ =>
        {
            RollOver();
            return CommandResult.Done;
        });
        Handle<ArchiveDoneTasks>(_ =>
        {
            Archive();
            return CommandResult.Done;
        });
        Handle<SetTasks>(c =>
        {
            ArgumentNullException.ThrowIfNull(c.Value);
            var v = c.Value;
            Store.Set(new TasksState(v.Open ?? ImmutableList<TaskEntry>.Empty, (v.History ?? ImmutableList<TaskEntry>.Empty).Take(MaxHistory).ToImmutableList(),
                Math.Max(0, v.DoneTotal), v.Focus));
            return CommandResult.Done;
        });

        On<DayStarted>(_ => NewDay());
    }

    public override void Start() => NewDay();

    private void NewDay()
    {
        Archive();
        Repeats();
        RollOver();
    }

    public const int MaxSteps = 50;

    /// <summary>Сменя задачата (change връща null – нищо не се сменя).</summary>
    private CommandResult Change(Guid id, Func<TaskEntry, TaskEntry?> change)
    {
        var s = State<TasksState>();
        if (s.Find(id) is not { } t) return CommandResult.Rejected("няма такава задача");
        if (change(t) is { } next && next != t) Store.Set(s with { Open = s.Open.Replace(t, next) });
        return CommandResult.Done;
    }

    private CommandResult ChangeStep(Guid taskId, Guid stepId, Func<TaskStep, TaskStep> change) => Change(taskId, t =>
        t.StepList.FirstOrDefault(x => x.Id == stepId) is { } step ? t with { Steps = t.StepList.Replace(step, change(step)) } : null);

    /// <summary>
    /// Повтарящите се: ако днес им е ден и още няма задача за днес – нова (несвършена, със
    /// стъпките отначало). Старите несвършени от същата поредица се махат – днешната е вместо тях.
    /// </summary>
    private void Repeats()
    {
        var s = State<TasksState>();
        var today = Clock.Today();
        var open = s.Open;
        bool changed = false;
        foreach (var series in s.Open.Concat(s.History).Where(t => t.Series != null).GroupBy(t => t.Series))
        {
            var all = series.ToList();
            var latest = all.OrderBy(t => t.Day ?? t.DoneAt?.Date ?? DateTime.MinValue).Last();
            if (latest.Repeat.Length == 0 || latest.Day is not { } last || last >= today) continue;
            var anchor = all.Min(t => t.Day ?? last);
            if (!TaskRepeat.Due(latest.Repeat, anchor, today)) continue;
            var next = latest with
            {
                Id = Guid.NewGuid(), Day = today, Done = false, DoneAt = null, PageRolled = false, Rolled = 0, Spent = 0, Created = Clock.Now,
                Steps = latest.Steps?.Select(x => x with { Done = false }).ToImmutableList(),
            };
            var stale = open.Where(t => t.Series == series.Key && !t.Done && t.Day < today).ToList();
            int at = stale.Count > 0 ? open.IndexOf(stale[0]) : open.Count;
            open = open.RemoveRange(stale);
            open = open.Insert(Math.Min(at, open.Count), next);
            changed = true;
        }
        if (changed) Store.Set(s with { Open = open });
    }

    private static string CleanTag(string? tag)
    {
        string t = (tag ?? "").Trim();
        return t.Length > MaxTag ? t[..MaxTag] : t;
    }

    /// <summary>Несвършените от минал ден – за днес, на същото място в реда.</summary>
    private void RollOver()
    {
        var s = State<TasksState>();
        var today = Clock.Today();
        int count = 0;
        var open = s.Open.Select(t =>
        {
            if (t.Done || t.Day is not { } d || d >= today) return t;
            count++;
            return t with { Day = today, Rolled = t.Rolled + 1 };
        }).ToImmutableList();
        if (count == 0) return;
        Store.Set(s with { Open = open });
        Emit(new TasksRolledOver(count));
    }

    /// <summary>
    /// Версия 1 → 2: задачите още нямаха ден. Несвършените стават за днес (иначе изведнъж
    /// всичко щеше да е в „Някога“), свършените – за деня, в който са свършени.
    /// </summary>
    private JsonNode Migrate(JsonNode data, int version)
    {
        if (version < 2)
        {
            string today = Clock.Today().ToString("yyyy-MM-ddT00:00:00");
            foreach (var name in new[] { "Open", "History" })
            {
                if (data[name] is not JsonArray list) continue;
                foreach (var item in list.OfType<JsonObject>())
                {
                    if (item["Day"] != null) continue;
                    string? doneAt = item["DoneAt"]?.GetValue<string>();
                    bool done = item["Done"]?.GetValue<bool>() ?? false;
                    item["Day"] = done && doneAt is { Length: >= 10 } ? doneAt[..10] + "T00:00:00" : today;
                }
            }
        }
        return data;
    }

    private void Archive()
    {
        var s = State<TasksState>();
        var today = Clock.Today();
        var old = s.Open.Where(t => t.Done && t.DoneAt?.Date < today).ToList();
        if (old.Count == 0) return;
        var history = old.OrderByDescending(t => t.DoneAt).Concat(s.History).Take(MaxHistory).ToImmutableList();
        Store.Set(s with { Open = s.Open.RemoveRange(old), History = history });
    }
}
