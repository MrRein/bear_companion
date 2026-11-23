using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Mecho.Core;

// ─────────────────────────────────────────────────────────────────────────────
// РЕЖИСЬОРЪТ НА МИСЛИТЕ: кое да каже мечокът сам и кога.
//
// Функциите не говорят направо – предлагат мисъл:
//
//     bus.Send(new Think("memory.yesterday.work", Importance: 5, Facts.Of(("часа", "4"))));
//
// Мисълта се казва (сигнал SpeakNow), ако важността ѝ е над прага. Прагът е
// „приказливостта“: Base + Pressure (+ BusyBonus, ако спи/работи). Всяко казано нещо
// вдига Pressure, тишината го сваля (наполовина за HalfLifeMinutes), внимание от
// Тут (цъкане, хранене) – също. Така в пъстър ден говори повече, а в тих – по-малко,
// без твърд лимит. Мисъл, която не е казана, изветрява след ExpireMinutes.
// Важност ≥ AlwaysAt минава и над прага (не и докато работи/спи – освен 10).
// ─────────────────────────────────────────────────────────────────────────────

public sealed record Thought(string Key, int Importance, ImmutableDictionary<string, string> Facts, DateTime Created, DateTime Expires)
{
    public string Fact(string name) => Facts.TryGetValue(name, out var v) ? v : "";
}

/// <summary>Числата на приказливостта (приложението може да ги смени с ConfigureTalk).</summary>
public sealed record TalkSettings(
    double Base = 3,
    double RaisePerLine = 3,
    double HalfLifeMinutes = 45,
    double BusyBonus = 4,
    double AttentionDrop = 2,
    int AlwaysAt = 9,
    double MinGapSeconds = 90,
    double ExpireMinutes = 60);

public sealed record TalkState(double Pressure, DateTime LastSpoke, DateTime LastDecay, ImmutableList<Thought> Pending, ImmutableDictionary<string, DateTime> LastByKey) : IStateSlice
{
    public static TalkState Quiet { get; } = new(0, DateTime.MinValue, DateTime.MinValue, ImmutableList<Thought>.Empty, ImmutableDictionary<string, DateTime>.Empty);
}

// ── Команди ──
/// <summary>
/// Предложи мисъл. Importance 1–10. CooldownHours – същият ключ не се казва пак преди
/// толкова часа. ExpireMinutes – колко да чака удобен момент (по подразбиране от настройките).
/// </summary>
public sealed record Think(string Key, int Importance, ImmutableDictionary<string, string>? Facts = null, double CooldownHours = 0, double? ExpireMinutes = null) : IGameCommand;
/// <summary>Тут обърна внимание (цъкна, нахрани, облече) – по-приказлив е.</summary>
public sealed record UserAttention : IGameCommand;
/// <summary>Мечокът каза нещо извън режисьора (отговор, награда) – брои се към приказливостта.</summary>
public sealed record SpokeAside(double Weight = 1) : IGameCommand;
public sealed record ConfigureTalk(TalkSettings Settings) : IGameCommand;
public sealed record ClearThoughts : IGameCommand;

// ── Сигнали ──
/// <summary>Кажи това сега (приложението избира репликата по Key и попълва Facts).</summary>
public sealed record SpeakNow(Thought Thought) : ISignal;
/// <summary>Мисълта изветря, без да е казана.</summary>
public sealed record ThoughtFaded(Thought Thought) : ISignal;

/// <summary>Модулът „мисли“ (Id: thoughts).</summary>
public sealed class ThoughtsModule : GameModule
{
    public const string ModuleId = "thoughts";
    public const string Key = "thoughts";

    public TalkSettings Settings { get; private set; } = new();

    /// <summary>Има ли какво да каже за този ключ (приложението: има ли реплики/показвач). null – всичко.</summary>
    public Func<string, bool>? CanSay { get; set; }

    public override string Id => ModuleId;

    protected override void OnRegister()
    {
        Store.Register(this, Key, TalkState.Quiet);
        Handle<Think>(c =>
        {
            if (string.IsNullOrWhiteSpace(c.Key)) return CommandResult.Rejected("празен ключ");
            if (CanSay != null && !CanSay(c.Key)) return CommandResult.Rejected("няма какво да каже");
            var s = State<TalkState>();
            var now = Clock.Now;
            if (c.CooldownHours > 0 && s.LastByKey.TryGetValue(c.Key, out var last) && now - last < TimeSpan.FromHours(c.CooldownHours))
                return CommandResult.Rejected("казано скоро");
            var t = new Thought(c.Key, Math.Clamp(c.Importance, 1, 10), c.Facts ?? Facts.Empty, now,
                now.AddMinutes(c.ExpireMinutes ?? Settings.ExpireMinutes));
            // Същият ключ – новата мисъл заменя старата.
            Store.Set(s with { Pending = s.Pending.RemoveAll(p => p.Key == c.Key).Add(t) });
            return CommandResult.Done;
        });
        Handle<UserAttention>(_ =>
        {
            Decay();
            Store.Update<TalkState>(s => s with { Pressure = Math.Max(-Settings.AttentionDrop, s.Pressure - Settings.AttentionDrop) });
            return CommandResult.Done;
        });
        Handle<SpokeAside>(c =>
        {
            Decay();
            Store.Update<TalkState>(s => s with { Pressure = s.Pressure + Settings.RaisePerLine * Math.Max(0, c.Weight) / 2, LastSpoke = Clock.Now });
            return CommandResult.Done;
        });
        Handle<ConfigureTalk>(c =>
        {
            Settings = c.Settings ?? new TalkSettings();
            return CommandResult.Done;
        });
        Handle<ClearThoughts>(_ =>
        {
            Store.Update<TalkState>(s => s with { Pending = ImmutableList<Thought>.Empty });
            return CommandResult.Done;
        });
        Every("talk", 2, 2, Tick);
    }

    /// <summary>Прагът сега (за Dev прозореца и тестовете).</summary>
    public double Threshold
    {
        get
        {
            Decay();
            return Settings.Base + State<TalkState>().Pressure + (Busy ? Settings.BusyBonus : 0);
        }
    }

    private bool Busy => Store.TryGet<BearStatusState>(out var b) && b != null && (b.Asleep || b.Working);

    /// <summary>Тишината сваля натиска (експоненциално, наполовина за HalfLifeMinutes).</summary>
    private void Decay()
    {
        var s = State<TalkState>();
        var now = Clock.Now;
        if (s.LastDecay == DateTime.MinValue || now <= s.LastDecay)
        {
            if (s.LastDecay == DateTime.MinValue) Store.Set(s with { LastDecay = now });
            return;
        }
        double minutes = (now - s.LastDecay).TotalMinutes;
        double p = s.Pressure > 0 ? s.Pressure * Math.Pow(0.5, minutes / Math.Max(1, Settings.HalfLifeMinutes)) : Math.Min(0, s.Pressure + minutes / 30);
        Store.Set(s with { Pressure = p, LastDecay = now });
    }

    /// <summary>Решава дали да каже нещо сега (вика се на 2 секунди; и ръчно от тестовете).</summary>
    public void Tick()
    {
        Decay();
        var s = State<TalkState>();
        var now = Clock.Now;
        var faded = s.Pending.Where(t => t.Expires <= now).ToList();
        if (faded.Count > 0)
        {
            s = s with { Pending = s.Pending.RemoveAll(t => t.Expires <= now) };
            Store.Set(s);
            foreach (var t in faded) Emit(new ThoughtFaded(t));
        }
        if (s.Pending.IsEmpty) return;
        bool busy = Busy;
        double threshold = Settings.Base + s.Pressure + (busy ? Settings.BusyBonus : 0);
        bool gapOk = s.LastSpoke == DateTime.MinValue || (now - s.LastSpoke).TotalSeconds >= Settings.MinGapSeconds;
        var pick = s.Pending
            .Where(t => t.Importance >= 10 || (gapOk && (t.Importance > threshold || (!busy && t.Importance >= Settings.AlwaysAt))))
            .OrderByDescending(t => t.Importance).ThenBy(t => t.Created)
            .FirstOrDefault();
        if (pick == null) return;
        Store.Set(s with
        {
            Pending = s.Pending.Remove(pick),
            Pressure = Math.Max(0, s.Pressure) + Settings.RaisePerLine,
            LastSpoke = now,
            LastByKey = s.LastByKey.SetItem(pick.Key, now),
        });
        Emit(new SpeakNow(pick));
    }
}
