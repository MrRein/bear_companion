using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Mecho.Core;

// ─────────────────────────────────────────────────────────────────────────────
// ХРОНИКАТА: паметта на мечока – какво сме преживели заедно.
//
//     bus.Send(new Remember("project.done", Facts.Of(("проект", "Горски тайни")), Importance: 8));
//     var j = store.Get<JournalState>();
//     j.LastOf("project.done")?.Fact("проект");     // „Горски тайни“
//     j.DaysTogether(clock.Today());                // 42
//     j.OriginOf("cloth:scarf_red")                 // „от сандъчето на „Горски тайни““
//
// Моментите: вид (ключ), кога, факти (име → стойност) и важност 1–10. Маловажните
// се пазят 90 дни, важните (5+) – завинаги (най-много MaxMoments).
// Произходът на нещата: всяко ItemAcquired записва източника; приложението може да
// го уточни със SetOrigin („от сандъчето на …“).
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Един спомен. Facts – подробности („проект“ → „Горски тайни“).</summary>
public sealed record Moment(string Kind, DateTime When, ImmutableDictionary<string, string> Facts, int Importance)
{
    public string Fact(string name) => Facts.TryGetValue(name, out var v) ? v : "";
}

/// <summary>Помощник за факти: Facts.Of(("проект", "Горски тайни"), ("тема", "Космос")).</summary>
public static class Facts
{
    public static ImmutableDictionary<string, string> Empty { get; } = ImmutableDictionary<string, string>.Empty;

    public static ImmutableDictionary<string, string> Of(params (string Name, string Value)[] facts) =>
        facts.Aggregate(Empty, (d, f) => d.SetItem(f.Name, f.Value ?? ""));
}

/// <summary>Произходът на предмет: откъде (текст) и кога.</summary>
public sealed record Origin(string From, DateTime When);

public sealed record JournalState(ImmutableList<Moment> Moments, ImmutableDictionary<string, Origin> Origins, DateTime FirstDay,
    ImmutableDictionary<string, int>? Counters = null) : IStateSlice
{
    /// <summary>Брояч за цялото време (мечо-дора, проекти…) – не изветрява.</summary>
    public int Counter(string name) => Counters != null && Counters.TryGetValue(name, out var n) ? n : 0;

    public static JournalState Empty { get; } = new(ImmutableList<Moment>.Empty, ImmutableDictionary<string, Origin>.Empty, DateTime.MinValue);

    public Moment? LastOf(string kind) => Moments.LastOrDefault(m => m.Kind == kind);

    public int Count(string kind) => Moments.Count(m => m.Kind == kind);

    public IEnumerable<Moment> Since(DateTime from) => Moments.Where(m => m.When >= from);

    public IEnumerable<Moment> OfKind(string kind) => Moments.Where(m => m.Kind == kind);

    public Origin? OriginOf(string item) => Origins.TryGetValue(item, out var o) ? o : null;

    /// <summary>Колко дни сме заедно (първият ден е 1).</summary>
    public int DaysTogether(DateTime today) => FirstDay == DateTime.MinValue ? 1 : Math.Max(1, (int)(today.Date - FirstDay.Date).TotalDays + 1);
}

// ── Команди ──
/// <summary>Запомни момент. When – по подразбиране сега.</summary>
public sealed record Remember(string Kind, ImmutableDictionary<string, string>? Facts = null, int Importance = 3, DateTime? When = null) : IGameCommand;
/// <summary>Уточни откъде е предмет (Force – и ако вече има записан).</summary>
public sealed record SetOrigin(string ItemId, string From, bool Force = true) : IGameCommand;
/// <summary>Брои нещо за цялото време (Tally("pomodoro")). Пуска TallyReached на кръгли числа.</summary>
public sealed record Tally(string Name, int By = 1) : IGameCommand;
/// <summary>Първият ден заедно (стари записи: от най-стария ден).</summary>
public sealed record SetFirstDay(DateTime Day) : IGameCommand;

// ── Сигнали ──
public sealed record MomentRemembered(Moment Moment) : ISignal;
/// <summary>Броячът стигна кръгло число (Milestones).</summary>
public sealed record TallyReached(string Name, int Value) : ISignal;

/// <summary>Модулът „хроника“ (Id: journal).</summary>
public sealed class JournalModule : GameModule
{
    public const string ModuleId = "journal";
    public const string Key = "journal";
    public const int MaxMoments = 600;
    public const int KeepImportant = 5;
    public static readonly TimeSpan KeepMinor = TimeSpan.FromDays(90);

    /// <summary>Кръглите числа за TallyReached.</summary>
    public static readonly int[] Milestones = { 1, 10, 50, 100, 250, 500, 1000, 2000, 5000 };

    public override string Id => ModuleId;

    protected override void OnRegister()
    {
        Store.Register(this, Key, JournalState.Empty);
        Handle<Remember>(c =>
        {
            if (string.IsNullOrWhiteSpace(c.Kind)) return CommandResult.Rejected("празен вид");
            var m = new Moment(c.Kind, c.When ?? Clock.Now, c.Facts ?? Facts.Empty, Math.Clamp(c.Importance, 1, 10));
            Store.Update<JournalState>(s => s with { Moments = Prune(s.Moments.Add(m)) });
            Emit(new MomentRemembered(m));
            return CommandResult.Done;
        });
        Handle<SetOrigin>(c =>
        {
            if (string.IsNullOrWhiteSpace(c.ItemId)) return CommandResult.Rejected("празен предмет");
            var s = State<JournalState>();
            if (!c.Force && s.Origins.ContainsKey(c.ItemId)) return CommandResult.Done;
            Store.Set(s with { Origins = s.Origins.SetItem(c.ItemId, new Origin(c.From ?? "", Clock.Now)) });
            return CommandResult.Done;
        });
        Handle<Tally>(c =>
        {
            if (string.IsNullOrWhiteSpace(c.Name) || c.By <= 0) return CommandResult.Rejected("невалиден брояч");
            var s = State<JournalState>();
            int old = s.Counter(c.Name), now = old + c.By;
            Store.Set(s with { Counters = (s.Counters ?? ImmutableDictionary<string, int>.Empty).SetItem(c.Name, now) });
            foreach (int m in Milestones)
                if (old < m && now >= m) Emit(new TallyReached(c.Name, m));
            return CommandResult.Done;
        });
        Handle<SetFirstDay>(c =>
        {
            var s = State<JournalState>();
            if (s.FirstDay == DateTime.MinValue || c.Day.Date < s.FirstDay) Store.Set(s with { FirstDay = c.Day.Date });
            return CommandResult.Done;
        });
        // Всичко получено – откъде е (ако още не е уточнено).
        On<ItemAcquired>(a => Send(new SetOrigin(a.ItemId, a.Source, Force: false)));
    }

    public override void Start()
    {
        if (State<JournalState>().FirstDay == DateTime.MinValue) Send(new SetFirstDay(Clock.Today()));
    }

    private ImmutableList<Moment> Prune(ImmutableList<Moment> list)
    {
        var cutoff = Clock.Now - KeepMinor;
        if (list.Any(m => m.Importance < KeepImportant && m.When < cutoff))
            list = list.RemoveAll(m => m.Importance < KeepImportant && m.When < cutoff);
        if (list.Count > MaxMoments)
        {
            // Първо маха най-старите маловажни, после – най-старите изобщо.
            var minor = list.Where(m => m.Importance < KeepImportant).Take(list.Count - MaxMoments).ToHashSet();
            list = list.RemoveAll(minor.Contains);
            if (list.Count > MaxMoments) list = list.RemoveRange(0, list.Count - MaxMoments);
        }
        return list;
    }
}
