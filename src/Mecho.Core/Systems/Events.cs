using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Mecho.Core;

// ─────────────────────────────────────────────────────────────────────────────
// СЪБИТИЯ: от малка пеперуда до Коледа.
//
// Събитието е ДАННИ (GameEventDef): кога може да стане, колко трае и какви
// модификатори слага. EventDirector само решава кога почва и кога свършва; докато
// трае, модификаторите му са в ModifierStack под източник "event.<id>". Модулите
// не знаят за събитията – питат модификаторите (Needs: Mods.EnergyDrain, магазинът:
// Mods.ShopExtras, гардеробът: Mods.WantedOutfit…). Приложението слуша
// GameEventStarted/Ended за репликите и картинките.
//
// Ново събитие = един запис в GameEvents.All (или director.Define(...) от плъгин).
//
//   Day    – цял ден (пижамен ден, болен ден, Коледа); свършва в полунощ или по-рано
//            с EndGameEvent (взе лекарството).
//   Moment – няколко минути (пеперуда, дъга); случва се само през деня.
// ─────────────────────────────────────────────────────────────────────────────

public enum GameEventKind
{
    Day,
    Moment,
}

/// <summary>
/// Описание на събитие. On – дали датата е подходяща (Коледа: 20–26 декември);
/// Fixed – става винаги, щом датата е подходяща; иначе DailyChance (Day) или
/// HourlyChance (Moment). CooldownDays – колко дни поне между две.
/// Apply – слага модификаторите (източникът е готов: "event.&lt;id&gt;").
/// </summary>
public sealed class GameEventDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public GameEventKind Kind { get; init; } = GameEventKind.Day;
    public Func<DateTime, bool> On { get; init; } = _ => true;
    public bool Fixed { get; init; }
    public double DailyChance { get; init; }
    public double HourlyChance { get; init; }
    public TimeSpan Duration { get; init; } = TimeSpan.FromMinutes(2);
    public int CooldownDays { get; init; }
    public Action<ModifierStack, string>? Apply { get; init; }

    /// <summary>Източникът на модификаторите в ModifierStack.</summary>
    public string Source => "event." + Id;
}

/// <summary>Един активен: кога почна и кога свършва.</summary>
public sealed record ActiveEvent(string Id, DateTime StartedAt, DateTime EndsAt);

/// <summary>Active – активните; LastDay – кога е било всяко за последно; RolledDay – за кой ден е хвърлено „ще има ли“.</summary>
public sealed record EventsState(ImmutableSortedDictionary<string, ActiveEvent> Active, ImmutableSortedDictionary<string, string> LastDay, string RolledDay) : IStateSlice
{
    public static EventsState Empty { get; } = new(ImmutableSortedDictionary<string, ActiveEvent>.Empty, ImmutableSortedDictionary<string, string>.Empty, "");

    public bool IsActive(string id) => Active.ContainsKey(id);
}

/// <summary>Пусни го сега (Developer, тестове). Force – дори ако датата не е подходяща.</summary>
public sealed record StartGameEvent(string Id, bool Force = false) : IGameCommand;
/// <summary>Спри го (Reason: „лекарство“, „developer“…).</summary>
public sealed record EndGameEvent(string Id, string Reason) : IGameCommand;

/// <summary>Каталогът със събитията.</summary>
public static class GameEvents
{
    public const string Christmas = "christmas", PajamaDay = "pajama_day", SickDay = "sick_day", Butterfly = "butterfly", Rainbow = "rainbow";

    /// <summary>Тагове в Mods.ShopExtras (продукти само докато трае събитието).</summary>
    public const string Medicine = "medicine";

    public static IReadOnlyList<GameEventDef> All { get; } = new[]
    {
        new GameEventDef
        {
            Id = Christmas, Name = "Коледа", Fixed = true,
            On = d => d.Month == 12 && d.Day >= 20 && d.Day <= 26,
            Apply = (m, s) =>
            {
                m.Set(s, Mods.GiftChance, 2);                    // всеки ден подарък
                m.Add(s, Mods.Decor, "tree", "snow");
                m.Add(s, Mods.LinePacks, "event.christmas");
                m.Add(s, Mods.PreferredHobbies, "reading", "boardplay", "tea");
            },
        },
        new GameEventDef
        {
            Id = PajamaDay, Name = "Пижамен ден", DailyChance = 0.05, CooldownDays = 10,
            Apply = (m, s) =>
            {
                m.Add(s, Mods.WantedOutfit, WardrobeModule.PajamaTag);
                m.Add(s, Mods.PreferredHobbies, "reading", "gaming", "boardplay");
                m.Add(s, Mods.LinePacks, "event.pajama_day");
            },
        },
        new GameEventDef
        {
            Id = SickDay, Name = "Болен ден", DailyChance = 0.03, CooldownDays = 14,
            Apply = (m, s) =>
            {
                m.Set(s, Mods.EnergyDrain, 1.5);
                m.Set(s, Mods.HungerRate, 0.8);
                m.Set(s, Mods.Appetite, 0.7);
                m.Add(s, Mods.ShopExtras, Medicine);             // лекарството се продава само днес
                m.Add(s, Mods.BlockedHobbies, "yoga", "music");
                m.Add(s, Mods.PreferredHobbies, "reading", "tea");
                m.Add(s, Mods.LinePacks, "event.sick_day");
            },
        },
        new GameEventDef
        {
            Id = Butterfly, Name = "Пеперуда", Kind = GameEventKind.Moment, HourlyChance = 0.12,
            On = d => d.Hour >= 9 && d.Hour < 19 && d.Month is >= 4 and <= 10,
            Duration = TimeSpan.FromSeconds(40),
        },
        new GameEventDef
        {
            Id = Rainbow, Name = "Дъга", Kind = GameEventKind.Moment, HourlyChance = 0.02,
            On = d => d.Hour >= 10 && d.Hour < 18,
            Duration = TimeSpan.FromMinutes(3),
            Apply = (m, s) => m.Add(s, Mods.LinePacks, "event.rainbow"),
        },
    };
}

/// <summary>Режисьорът на събитията (Id: events). Не зависи от никого – само слага модификатори.</summary>
public sealed class EventDirector : GameModule
{
    public const string ModuleId = "events";
    public const string Key = "events";

    private readonly Dictionary<string, GameEventDef> _defs = new(StringComparer.Ordinal);

    /// <param name="automatic">false – събития само с StartGameEvent (самопроверката: нищо да не почне само насред проверка).</param>
    public EventDirector(IEnumerable<GameEventDef>? defs = null, bool automatic = true)
    {
        foreach (var d in defs ?? GameEvents.All) Define(d);
        Automatic = automatic;
    }

    /// <summary>Почват ли сами (по дата и шанс).</summary>
    public bool Automatic { get; set; }

    public override string Id => ModuleId;

    public IReadOnlyCollection<GameEventDef> Definitions => _defs.Values;

    /// <summary>Добавя (или подменя) събитие – плъгин, сезонно съдържание.</summary>
    public void Define(GameEventDef def)
    {
        ArgumentNullException.ThrowIfNull(def);
        if (string.IsNullOrWhiteSpace(def.Id)) throw new ArgumentException("Събитието иска Id.");
        _defs[def.Id] = def;
    }

    protected override void OnRegister()
    {
        Store.Register(this, Key, EventsState.Empty);
        Handle<StartGameEvent>(c =>
        {
            if (!_defs.TryGetValue(c.Id ?? "", out var def)) return CommandResult.Rejected("няма такова събитие");
            if (State<EventsState>().IsActive(def.Id)) return CommandResult.Done;
            if (!c.Force && !def.On(Clock.Now)) return CommandResult.Rejected("не е за днес");
            Begin(def);
            return CommandResult.Done;
        });
        Handle<EndGameEvent>(c =>
        {
            if (!State<EventsState>().IsActive(c.Id ?? "")) return CommandResult.Rejected("не е активно");
            Finish(c.Id!, string.IsNullOrWhiteSpace(c.Reason) ? "спряно" : c.Reason);
            return CommandResult.Done;
        });
        On<DayStarted>(_ =>
        {
            Expire();
            RollDay();
        });
        Every("tick", 60, 60, () =>
        {
            Expire();
            RollMoments(1.0 / 60);
        });
    }

    public override void Start()
    {
        // След рестарт: активните пак слагат модификаторите си (те не се пазят в записа).
        foreach (var a in State<EventsState>().Active.Values)
            if (_defs.TryGetValue(a.Id, out var def)) Apply(def);
        Expire();
        RollDay();
    }

    public override void Shutdown()
    {
        foreach (var def in _defs.Values) Modifiers.Clear(def.Source);
    }

    private void Begin(GameEventDef def)
    {
        var now = Clock.Now;
        var ends = def.Kind == GameEventKind.Day ? now.Date.AddDays(1) : now + def.Duration;
        Store.Update<EventsState>(s => s with
        {
            Active = s.Active.SetItem(def.Id, new ActiveEvent(def.Id, now, ends)),
            LastDay = s.LastDay.SetItem(def.Id, Clock.DayKey()),
        });
        Apply(def);
        Log.Info($"Събитие: {def.Name} почна (до {ends:HH:mm}).");
        Emit(new GameEventStarted(def.Id));
    }

    private void Apply(GameEventDef def)
    {
        Modifiers.Clear(def.Source);
        def.Apply?.Invoke(Modifiers, def.Source);
    }

    private void Finish(string id, string reason)
    {
        Store.Update<EventsState>(s => s with { Active = s.Active.Remove(id) });
        Modifiers.Clear("event." + id);
        Emit(new GameEventEnded(id, reason));
    }

    private void Expire()
    {
        var now = Clock.Now;
        foreach (var a in State<EventsState>().Active.Values.ToList())
            if (now >= a.EndsAt || !_defs.ContainsKey(a.Id)) Finish(a.Id, "свърши");
    }

    /// <summary>Нов ден: фиксираните (Коледа) – винаги; от случайните – най-много едно.</summary>
    private void RollDay()
    {
        if (!Automatic) return;
        var s = State<EventsState>();
        string today = Clock.DayKey();
        if (s.RolledDay == today) return;
        Store.Set(s with { RolledDay = today });
        var now = Clock.Now;
        var days = _defs.Values.Where(d => d.Kind == GameEventKind.Day && !State<EventsState>().IsActive(d.Id) && d.On(now)).ToList();
        foreach (var d in days.Where(d => d.Fixed)) Begin(d);
        foreach (var d in days.Where(d => !d.Fixed).OrderBy(d => d.Id, StringComparer.Ordinal))
        {
            if (OnCooldown(d, now)) continue;
            if (!Random.Chance(d.DailyChance)) continue;
            Begin(d);
            break;
        }
    }

    private void RollMoments(double hours)
    {
        if (!Automatic) return;
        var now = Clock.Now;
        foreach (var d in _defs.Values.Where(d => d.Kind == GameEventKind.Moment).OrderBy(d => d.Id, StringComparer.Ordinal))
        {
            if (State<EventsState>().IsActive(d.Id) || !d.On(now) || OnCooldown(d, now)) continue;
            if (Random.Chance(d.HourlyChance * hours)) Begin(d);
        }
    }

    private bool OnCooldown(GameEventDef d, DateTime now) =>
        d.CooldownDays > 0 && State<EventsState>().LastDay.TryGetValue(d.Id, out var last)
        && DateTime.TryParse(last, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var day)
        && (now.Date - day.Date).TotalDays < d.CooldownDays;
}
