using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Mecho.Core;

// ─────────────────────────────────────────────────────────────────────────────
// ЗАДАЧИТЕ: списъкът за днес, историята на свършените и наградите.
//
//     bus.Send(new AddTask("Скица за корицата", 3, "две версии"));
//     bus.Send(new SetTaskDone(id, true));    // +награда (1/3/5 лешника), брояч, TaskCompleted
//     bus.Send(new SetTaskDone(id, false));   // отмяна: наградата се връща
//     bus.Send(new SetFocusTask(id));         // по нея е мечо-дорото / работният таймер
//
// Правила тук: голяма задача дава шанс за страница само при първото „свършено“
// (TaskCompleted.FirstBigCompletion); свършените от минали дни отиват в историята
// сами в полунощ (DayStarted) – най-новите отпред, до 2000.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Една задача. Size: 1 – малка, 2 – средна, 3 – голяма. PageRolled – вече е дала шанс за страница.</summary>
public sealed record TaskEntry(Guid Id, string Title, string Description, int Size, bool Done, DateTime? DoneAt, bool PageRolled)
{
    public int Reward => Size switch { 3 => 5, 2 => 3, _ => 1 };
}

/// <summary>Open – днешните (и несвършените), History – свършените от минали дни, Focus – текущата.</summary>
public sealed record TasksState(ImmutableList<TaskEntry> Open, ImmutableList<TaskEntry> History, int DoneTotal, Guid? Focus) : IStateSlice
{
    public static TasksState Empty { get; } = new(ImmutableList<TaskEntry>.Empty, ImmutableList<TaskEntry>.Empty, 0, null);

    public TaskEntry? Find(Guid id) => Open.FirstOrDefault(t => t.Id == id);

    /// <summary>Текущата задача, ако още не е свършена.</summary>
    public TaskEntry? FocusTask => Focus is { } id && Find(id) is { Done: false } t ? t : null;
}

// ── Команди ──
public sealed record AddTask(string Title, int Size, string? Description = null) : IGameCommand;
public sealed record SetTaskDone(Guid Id, bool Done) : IGameCommand;
public sealed record DeleteTask(Guid Id) : IGameCommand;
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

/// <summary>Модулът „задачи“ (Id: tasks, зависи от economy – наградите са в лешници).</summary>
public sealed class TasksModule : GameModule
{
    public const string ModuleId = "tasks";
    public const string Key = "tasks";
    public const int MaxHistory = 2000;
    public const int MaxTitle = 200;

    public override string Id => ModuleId;

    public override IReadOnlyCollection<string> DependsOn { get; } = new[] { EconomyModule.ModuleId };

    protected override void OnRegister()
    {
        Store.Register(this, Key, TasksState.Empty);

        Handle<AddTask>(c =>
        {
            string title = (c.Title ?? "").Trim();
            if (title.Length == 0) return CommandResult.Rejected("задачата няма име");
            if (title.Length > MaxTitle) title = title[..MaxTitle];
            var t = new TaskEntry(Guid.NewGuid(), title, (c.Description ?? "").Trim(), Math.Clamp(c.Size, 1, 3), false, null, false);
            Store.Update<TasksState>(s => s with { Open = s.Open.Add(t) });
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

        On<DayStarted>(_ => Archive());
    }

    public override void Start() => Archive();

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
