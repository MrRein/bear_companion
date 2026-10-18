using System;
using System.Collections.Generic;

namespace Mecho.Core;

// ─────────────────────────────────────────────────────────────────────────────
// ПОДАРЪКЪТ НА ДЕНЯ и РАСТЕНИЕТО.
//
// GiftsModule – всяка сутрин решава дали днес ще има подарък и кога (шанс ×
// Mods.GiftChance – празник го вдига). Мечокът тръгва за него, когато е свободен
// (приложението), и казва GiftPresented / GiftOpened.
//
// GardenModule – саксията: фаза 0–9. Грижата (лопатка, ножица, лейка) трупа минути:
// на 8 минути – фаза нагоре, но от грижа най-много 2 на ден. Без грижа – една фаза на
// 5 дни (само ако саксията е купена). Пуска PlantGrew.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Day – за кой ден е решено; At – кога тръгва (MinValue – днес няма/вече е); Pending – чака да се отвори.</summary>
public sealed record GiftState(string Day, DateTime At, bool Pending, string Color) : IStateSlice
{
    public static GiftState None { get; } = new("", DateTime.MinValue, false, "");

    public bool Planned => At != DateTime.MinValue;
}

/// <summary>Реши за днес наново (Developer: „нов ден за подаръка“).</summary>
public sealed record PlanGiftToday(bool Force = false) : IGameCommand;
/// <summary>Върна се с подаръка – чака Тут да го отвори.</summary>
public sealed record GiftPresented(string Color) : IGameCommand;
public sealed record GiftOpened : IGameCommand;
public sealed record SetGift(GiftState Value) : IGameCommand;

public sealed record GiftPlanned(DateTime? At) : ISignal;

/// <summary>Модулът „подаръкът на деня“ (Id: gifts).</summary>
public sealed class GiftsModule : GameModule
{
    public const string ModuleId = "gifts";
    public const string Key = "gifts";

    /// <summary>В 7 от 10 дни има подарък (× Mods.GiftChance, най-много 1).</summary>
    public const double BaseChance = 0.7;

    public override string Id => ModuleId;

    protected override void OnRegister()
    {
        Store.Register(this, Key, GiftState.None);
        Handle<PlanGiftToday>(c =>
        {
            Plan(c.Force);
            return CommandResult.Done;
        });
        Handle<GiftPresented>(c =>
        {
            Store.Update<GiftState>(s => s with { Pending = true, At = DateTime.MinValue, Color = c.Color ?? "" });
            return CommandResult.Done;
        });
        Handle<GiftOpened>(_ =>
        {
            Store.Update<GiftState>(s => s with { Pending = false, At = DateTime.MinValue });
            return CommandResult.Done;
        });
        Handle<SetGift>(c =>
        {
            ArgumentNullException.ThrowIfNull(c.Value);
            Store.Set(c.Value with { Day = c.Value.Day ?? "", Color = c.Value.Color ?? "" });
            return CommandResult.Done;
        });
        On<DayStarted>(_ => Plan(false));
    }

    public override void Start() => Plan(false);

    private void Plan(bool force)
    {
        var s = State<GiftState>();
        string today = Clock.DayKey();
        if (!force && s.Day == today) return;
        double chance = Math.Min(1, BaseChance * Modifiers.Get(Mods.GiftChance));
        DateTime at = Random.Chance(chance) ? Clock.Now.AddMinutes(20 + Random.Next(160)) : DateTime.MinValue;
        Store.Set(s with { Day = today, At = at });
        Emit(new GiftPlanned(at == DateTime.MinValue ? null : at));
    }
}

/// <summary>Растението в саксията. Stage 0–9; CareMinutes – грижа от последната фаза.</summary>
public sealed record PlantState(int Stage, double CareMinutes, DateTime LastGrow, string GrowDay, int GrowToday) : IStateSlice
{
    public static PlantState Seed { get; } = new(0, 0, DateTime.MinValue, "", 0);
}

/// <summary>Грижа толкова минути (докато мечокът се грижи).</summary>
public sealed record CareForPlant(double Minutes) : IGameCommand;
/// <summary>Фаза нагоре веднага (Developer). FromCare – брои се към „2 на ден“.</summary>
public sealed record GrowPlant(bool FromCare) : IGameCommand;
public sealed record SetPlant(PlantState Value) : IGameCommand;
/// <summary>Провери сега дали е пораснало само с времето (става и само на 10 минути).</summary>
public sealed record CheckPlantGrowth : IGameCommand;

public sealed record PlantGrew(int Stage, bool Bloomed, bool FromCare) : ISignal;

/// <summary>Модулът „растение“ (Id: garden, зависи от economy – расте само купена саксия).</summary>
public sealed class GardenModule : GameModule
{
    public const string ModuleId = "garden";
    public const string Key = "garden.plant";
    public const int Stages = 10;
    public const double MinutesPerStage = 8;
    public const int MaxCareGrowthPerDay = 2;
    public static readonly TimeSpan GrowsAloneEvery = TimeSpan.FromDays(5);

    private readonly string _potItem;

    /// <param name="potItem">Id на саксията в инвентара (приложението: Kitchen.PlantPot).</param>
    public GardenModule(string potItem = "plant") => _potItem = potItem;

    public override string Id => ModuleId;

    public override IReadOnlyCollection<string> DependsOn { get; } = new[] { EconomyModule.ModuleId };

    protected override void OnRegister()
    {
        Store.Register(this, Key, PlantState.Seed);
        Handle<CareForPlant>(c =>
        {
            if (!(c.Minutes > 0) || double.IsInfinity(c.Minutes)) return CommandResult.Rejected("невалидни минути");
            var p = State<PlantState>();
            p = p with { CareMinutes = p.CareMinutes + c.Minutes };
            Store.Set(p);
            if (p.CareMinutes >= MinutesPerStage) Grow(fromCare: true);
            return CommandResult.Done;
        });
        Handle<GrowPlant>(c =>
        {
            Grow(c.FromCare);
            return CommandResult.Done;
        });
        Handle<SetPlant>(c =>
        {
            ArgumentNullException.ThrowIfNull(c.Value);
            Store.Set(c.Value with { Stage = Math.Clamp(c.Value.Stage, 0, Stages - 1), GrowDay = c.Value.GrowDay ?? "" });
            return CommandResult.Done;
        });
        Handle<CheckPlantGrowth>(_ =>
        {
            GrowByTime();
            return CommandResult.Done;
        });
        On<DayStarted>(_ => GrowByTime());
        Every("by-time", 600, 600, GrowByTime);
    }

    public override void Start() => GrowByTime();

    private void Grow(bool fromCare)
    {
        var p = State<PlantState>();
        string today = Clock.DayKey();
        if (p.GrowDay != today) p = p with { GrowDay = today, GrowToday = 0 };
        if (fromCare)
        {
            p = p with { CareMinutes = 0 };
            if (p.GrowToday >= MaxCareGrowthPerDay)
            {
                Store.Set(p);
                return;
            }
            p = p with { GrowToday = p.GrowToday + 1 };
        }
        p = p with { LastGrow = Clock.Now };
        if (p.Stage >= Stages - 1)
        {
            Store.Set(p);
            return;
        }
        p = p with { Stage = p.Stage + 1 };
        Store.Set(p);
        Emit(new PlantGrew(p.Stage, p.Stage == Stages - 1, fromCare));
    }

    /// <summary>Без грижи расте много бавно: една фаза на 5 дни.</summary>
    internal void GrowByTime()
    {
        if (!Store.TryGet<InventoryState>(out var inv) || !inv!.Has(_potItem)) return;
        var p = State<PlantState>();
        if (p.LastGrow == DateTime.MinValue)
        {
            Store.Set(p with { LastGrow = Clock.Now });
            return;
        }
        int grown = 0;
        while (Clock.Now - p.LastGrow > GrowsAloneEvery && p.Stage < Stages - 1)
        {
            p = p with { Stage = p.Stage + 1, LastGrow = p.LastGrow + GrowsAloneEvery };
            grown++;
        }
        if (grown == 0) return;
        Store.Set(p);
        Emit(new PlantGrew(p.Stage, p.Stage == Stages - 1, false));
    }
}
