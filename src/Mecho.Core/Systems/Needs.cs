using System;

namespace Mecho.Core;

// ─────────────────────────────────────────────────────────────────────────────
// МЕЧОКЪТ И НУЖДИТЕ.
//
// BearStatusModule – какво прави мечокът сега (спи, работи, навън е). Тялото на
// мечока (прозорецът) го съобщава с ReportBearStatus; модулите го четат от
// BearStatusState, вместо да питат прозореца. Пуска SleepChanged и ActivityChanged.
//
// NeedsModule – ситост и енергия (0–100). Единственият, който ги пази:
//   • гладът спада сам тук (на секунда), по-бавно в съня, × Mods.HungerRate (събития);
//   • храна, сън, дрямка и т.н. ги менят с команди:
//       bus.Send(new AdjustNeeds(+30, 0, "супа"));
//       bus.Send(new SetNeeds(null, 100, "сутрин"));
//   • пуска NeedsChanged, когато ситостта или енергията се сменят с поне 1 точка
//     (за изгледи и реплики – не всеки кадър); StateChanged<NeedsState> – при всяка промяна.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Какво прави мечокът сега. Activity – кратко име („reading“, „cooking“, „walk“…).</summary>
public sealed record BearStatusState(bool Asleep, bool Working, bool Outside, string Activity) : IStateSlice
{
    public static BearStatusState Idle { get; } = new(false, false, false, "idle");
}

public sealed record ReportBearStatus(bool Asleep, bool Working, bool Outside, string Activity) : IGameCommand;

public sealed record ActivityChanged(string Old, string New) : ISignal;

/// <summary>Модулът „мечокът“: какво прави сега (Id: bear).</summary>
public sealed class BearStatusModule : GameModule
{
    public const string ModuleId = "bear";
    public const string Key = "bear.status";

    public override string Id => ModuleId;

    protected override void OnRegister()
    {
        Store.Register(this, Key, BearStatusState.Idle);
        Handle<ReportBearStatus>(c =>
        {
            var old = State<BearStatusState>();
            var next = new BearStatusState(c.Asleep, c.Working, c.Outside, string.IsNullOrWhiteSpace(c.Activity) ? "idle" : c.Activity);
            if (old == next) return CommandResult.Done;
            Store.Set(next);
            if (old.Asleep != next.Asleep) Emit(new SleepChanged(next.Asleep));
            if (old.Activity != next.Activity) Emit(new ActivityChanged(old.Activity, next.Activity));
            return CommandResult.Done;
        });
    }
}

/// <summary>Ситост и енергия (0–100).</summary>
public sealed record NeedsState(double Fullness, double Energy) : IStateSlice
{
    public static NeedsState Default { get; } = new(80, 90);
}

/// <summary>Промяна с толкова точки (може отрицателна). Резултатът се държи в 0–100.</summary>
public sealed record AdjustNeeds(double Fullness, double Energy, string Reason) : IGameCommand;

/// <summary>Точна стойност (null – без промяна).</summary>
public sealed record SetNeeds(double? Fullness, double? Energy, string Reason) : IGameCommand;

/// <summary>Модулът „нужди“ (Id: needs, зависи от bear).</summary>
public sealed class NeedsModule : GameModule
{
    public const string ModuleId = "needs";
    public const string Key = "needs";

    /// <summary>Колко точки ситост на час се губят буден/заспал (сит → гладен за ~3,5 ч).</summary>
    public const double HungerAwake = 22, HungerAsleep = 4;

    private double _lastDecay = double.NaN;
    private NeedsState _announced = NeedsState.Default;

    public override string Id => ModuleId;

    public override System.Collections.Generic.IReadOnlyCollection<string> DependsOn { get; } = new[] { BearStatusModule.ModuleId };

    protected override void OnRegister()
    {
        Store.Register(this, Key, NeedsState.Default);
        Handle<AdjustNeeds>(c =>
        {
            if (!Finite(c.Fullness) || !Finite(c.Energy)) return CommandResult.Rejected("невалидно число");
            var n = State<NeedsState>();
            Apply(new NeedsState(Clamp(n.Fullness + c.Fullness), Clamp(n.Energy + c.Energy)));
            return CommandResult.Done;
        });
        Handle<SetNeeds>(c =>
        {
            if ((c.Fullness is double f && !Finite(f)) || (c.Energy is double e && !Finite(e))) return CommandResult.Rejected("невалидно число");
            var n = State<NeedsState>();
            Apply(new NeedsState(Clamp(c.Fullness ?? n.Fullness), Clamp(c.Energy ?? n.Energy)));
            return CommandResult.Done;
        });
        // Гладът спада сам веднъж в секунда (по истинското изминало време).
        Every("hunger", 1, 1, Decay);
    }

    public override void Start()
    {
        _announced = State<NeedsState>();
        _lastDecay = Clock.Mono;
    }

    internal void Decay()
    {
        double now = Clock.Mono;
        double hours = double.IsNaN(_lastDecay) ? 0 : Math.Clamp(now - _lastDecay, 0, 60) / 3600;
        _lastDecay = now;
        if (hours <= 0) return;
        bool asleep = Store.TryGet<BearStatusState>(out var bear) && bear!.Asleep;
        double rate = (asleep ? HungerAsleep : HungerAwake) * Modifiers.Get(Mods.HungerRate);
        var n = State<NeedsState>();
        Apply(n with { Fullness = Clamp(n.Fullness - rate * hours) });
    }

    private void Apply(NeedsState next)
    {
        Store.Set(next);
        // Грубият сигнал – само при промяна с поне 1 точка от последно обявеното.
        if (Math.Abs(next.Fullness - _announced.Fullness) >= 1 || Math.Abs(next.Energy - _announced.Energy) >= 1)
        {
            var old = _announced;
            _announced = next;
            Emit(new NeedsChanged(old.Fullness, next.Fullness, old.Energy, next.Energy));
        }
    }

    private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

    private static double Clamp(double v) => Math.Clamp(v, 0, 100);
}
