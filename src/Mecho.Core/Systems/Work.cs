using System;

namespace Mecho.Core;

// ─────────────────────────────────────────────────────────────────────────────
// РАБОТА: мечо-доро (работа → почивка, по няколко подред), таймер и плащането.
//
// Модулът пази времето и САМ засича края (веднъж в секунда или при CheckWork):
//   работа свърши → почивка (PomodoroFocusDone);
//   почивка свърши → следващото от поредицата или край (PomodoroBreakDone);
//   таймерът свърши → WorkTimerFinished.
// Мечокът (приложението) само слуша сигналите и реагира: казва нещо, пуска звук,
// ляга си или сяда пак на бюрото. Затова краят идва и докато прозорецът е зает.
//
//     bus.Send(new StartPomodoro(25, 5, NewSeries: true));
//     bus.Send(new StartWorkTimer(15, "скица", WorkTimerKinds.Work));
//     bus.Send(new PayWork(25));          // 1 лешник на 10 мин, остатъкът се пази
//     bus.Subscribe<PomodoroFocusDone>(this, s => …);
// ─────────────────────────────────────────────────────────────────────────────

public enum WorkPhase
{
    Off,
    Focus,
    Break,
}

/// <summary>
/// Мечо-дорото. Mode – видът („25“, „52“, „90“), FocusMinutes/BreakMinutes – дължините му;
/// Series – колко подред, SeriesLeft – още колко след текущото; Today/Day/Total – броячи.
/// </summary>
public sealed record PomodoroState(WorkPhase Phase, DateTime EndsAt, string Mode, int FocusMinutes, int BreakMinutes,
    int Series, int SeriesLeft, int Today, string Day, int Total) : IStateSlice
{
    public static PomodoroState Default { get; } = new(WorkPhase.Off, DateTime.MinValue, "25", 25, 5, 1, 0, 0, "", 0);

    /// <summary>Кое поред е текущото (1-во от 4 …).</summary>
    public int Number => Series - SeriesLeft;
}

public static class WorkTimerKinds
{
    public const string Work = "work", Rest = "rest", Other = "other";
}

/// <summary>Таймерът. EndsAt == MinValue – не тече. Kind: work / rest / other.</summary>
public sealed record WorkTimerState(DateTime EndsAt, double Minutes, string Label, string Kind) : IStateSlice
{
    public static WorkTimerState None { get; } = new(DateTime.MinValue, 0, "", WorkTimerKinds.Work);

    public bool Running => EndsAt != DateTime.MinValue;

    public bool IsWork => Kind == WorkTimerKinds.Work;
}

/// <summary>Минути работа, още неизплатени в лешници (под 10).</summary>
public sealed record WorkBankState(double Minutes) : IStateSlice
{
    public static WorkBankState Empty { get; } = new(0);
}

// ── Команди ──
/// <summary>Почва работа. NewSeries – поредицата почва отначало (иначе е следващото от нея).</summary>
public sealed record StartPomodoro(int FocusMinutes, int BreakMinutes, bool NewSeries) : IGameCommand;
public sealed record StopPomodoro : IGameCommand;
/// <summary>Вид и дължини (само докато не тече).</summary>
public sealed record SetPomodoroMode(string Mode, int FocusMinutes, int BreakMinutes) : IGameCommand;
/// <summary>Колко подред, 1–4 (само докато не тече).</summary>
public sealed record SetPomodoroSeries(int Count) : IGameCommand;
/// <summary>Пряко записване (стари записи, Developer, самопроверка).</summary>
public sealed record SetPomodoro(PomodoroState Value) : IGameCommand;
public sealed record StartWorkTimer(double Minutes, string Label, string Kind) : IGameCommand;
public sealed record StopWorkTimer : IGameCommand;
public sealed record SetWorkTimer(WorkTimerState Value) : IGameCommand;
/// <summary>Провери сега дали нещо е свършило (иначе – до секунда).</summary>
public sealed record CheckWork : IGameCommand;
/// <summary>Плаща толкова минути работа: по 1 лешник на 10 минути, остатъкът чака.</summary>
public sealed record PayWork(double Minutes) : IGameCommand;
public sealed record SetWorkBank(double Minutes) : IGameCommand;

// ── Сигнали ──
public sealed record PomodoroStarted(int Number, int Series) : ISignal;
public sealed record PomodoroFocusDone(int Minutes, int Today) : ISignal;
/// <summary>Почивката свърши. NextFocus – почна следващото от поредицата.</summary>
public sealed record PomodoroBreakDone(bool NextFocus) : ISignal;
public sealed record PomodoroStopped(bool WasFocus) : ISignal;
public sealed record WorkTimerStarted(double Minutes, string Kind) : ISignal;
public sealed record WorkTimerFinished(double Minutes, string Label, string Kind) : ISignal;
public sealed record WorkTimerStopped(double MinutesDone, string Kind) : ISignal;
public sealed record WorkPaid(double Minutes, int Nuts) : ISignal;

/// <summary>Модулът „работа“ (Id: work, зависи от economy – плаща в лешници).</summary>
public sealed class WorkModule : GameModule
{
    public const string ModuleId = "work";
    public const string PomodoroKey = "work.pomodoro";
    public const string TimerKey = "work.timer";
    public const string BankKey = "work.bank";

    /// <summary>Колко минути работа носят един лешник.</summary>
    public const double MinutesPerNut = 10;

    public override string Id => ModuleId;

    public override System.Collections.Generic.IReadOnlyCollection<string> DependsOn { get; } = new[] { EconomyModule.ModuleId };

    /// <summary>Колко лешника носят толкова минути при такъв остатък (без да плаща).</summary>
    public static int NutsFor(double bank, double minutes) =>
        minutes <= 0 ? 0 : (int)Math.Floor((Math.Max(0, bank) + minutes) / MinutesPerNut + 1e-9);

    protected override void OnRegister()
    {
        Store.Register(this, PomodoroKey, PomodoroState.Default);
        Store.Register(this, TimerKey, WorkTimerState.None);
        Store.Register(this, BankKey, WorkBankState.Empty);

        Handle<StartPomodoro>(c =>
        {
            if (c.FocusMinutes <= 0 || c.BreakMinutes < 0) return CommandResult.Rejected("невалидна дължина");
            var p = RolledOver(State<PomodoroState>());
            int left = c.NewSeries ? Math.Max(0, p.Series - 1) : p.SeriesLeft;
            p = p with { Phase = WorkPhase.Focus, EndsAt = Clock.Now.AddMinutes(c.FocusMinutes), FocusMinutes = c.FocusMinutes, BreakMinutes = c.BreakMinutes, SeriesLeft = left };
            Store.Set(p);
            Emit(new PomodoroStarted(p.Number, p.Series));
            return CommandResult.Done;
        });
        Handle<StopPomodoro>(_ =>
        {
            var p = State<PomodoroState>();
            if (p.Phase == WorkPhase.Off) return CommandResult.Done;
            Store.Set(p with { Phase = WorkPhase.Off, SeriesLeft = 0 });
            Emit(new PomodoroStopped(p.Phase == WorkPhase.Focus));
            return CommandResult.Done;
        });
        Handle<SetPomodoroMode>(c =>
        {
            var p = State<PomodoroState>();
            if (p.Phase != WorkPhase.Off) return CommandResult.Rejected("тече – не се сменя");
            if (string.IsNullOrWhiteSpace(c.Mode) || c.FocusMinutes <= 0 || c.BreakMinutes < 0) return CommandResult.Rejected("невалиден вид");
            Store.Set(p with { Mode = c.Mode, FocusMinutes = c.FocusMinutes, BreakMinutes = c.BreakMinutes });
            return CommandResult.Done;
        });
        Handle<SetPomodoroSeries>(c =>
        {
            var p = State<PomodoroState>();
            if (p.Phase != WorkPhase.Off) return CommandResult.Rejected("тече – не се сменя");
            Store.Set(p with { Series = Math.Clamp(c.Count, 1, 4) });
            return CommandResult.Done;
        });
        Handle<SetPomodoro>(c =>
        {
            ArgumentNullException.ThrowIfNull(c.Value);
            var v = c.Value;
            Store.Set(v with { Mode = v.Mode ?? "25", Day = v.Day ?? "", Series = Math.Clamp(v.Series, 1, 4), SeriesLeft = Math.Clamp(v.SeriesLeft, 0, 3), Today = Math.Max(0, v.Today), Total = Math.Max(0, v.Total) });
            return CommandResult.Done;
        });

        Handle<StartWorkTimer>(c =>
        {
            if (c.Minutes <= 0 || double.IsNaN(c.Minutes) || double.IsInfinity(c.Minutes)) return CommandResult.Rejected("невалидни минути");
            string kind = c.Kind is WorkTimerKinds.Work or WorkTimerKinds.Rest or WorkTimerKinds.Other ? c.Kind : WorkTimerKinds.Other;
            Store.Set(new WorkTimerState(Clock.Now.AddMinutes(c.Minutes), c.Minutes, (c.Label ?? "").Trim(), kind));
            Emit(new WorkTimerStarted(c.Minutes, kind));
            return CommandResult.Done;
        });
        Handle<StopWorkTimer>(_ =>
        {
            var t = State<WorkTimerState>();
            if (!t.Running) return CommandResult.Done;
            double done = Math.Clamp(t.Minutes - (t.EndsAt - Clock.Now).TotalMinutes, 0, t.Minutes);
            Store.Set(t with { EndsAt = DateTime.MinValue });
            Emit(new WorkTimerStopped(done, t.Kind));
            return CommandResult.Done;
        });
        Handle<SetWorkTimer>(c =>
        {
            ArgumentNullException.ThrowIfNull(c.Value);
            Store.Set(c.Value with { Label = c.Value.Label ?? "", Kind = c.Value.Kind ?? WorkTimerKinds.Work });
            return CommandResult.Done;
        });

        Handle<CheckWork>(_ =>
        {
            Check();
            return CommandResult.Done;
        });
        Handle<PayWork>(c =>
        {
            if (double.IsNaN(c.Minutes) || double.IsInfinity(c.Minutes)) return CommandResult.Rejected("невалидни минути");
            if (c.Minutes <= 0) return CommandResult.Done;
            double bank = State<WorkBankState>().Minutes + c.Minutes;
            int nuts = (int)Math.Floor(bank / MinutesPerNut + 1e-9);
            Store.Set(new WorkBankState(Math.Max(0, bank - nuts * MinutesPerNut)));
            if (nuts > 0) Send(new EarnNuts(nuts, "работа"));
            Emit(new WorkPaid(c.Minutes, nuts));
            return CommandResult.Done;
        });
        Handle<SetWorkBank>(c =>
        {
            Store.Set(new WorkBankState(double.IsFinite(c.Minutes) ? Math.Max(0, c.Minutes) : 0));
            return CommandResult.Done;
        });

        On<DayStarted>(_ => Store.Set(RolledOver(State<PomodoroState>())));
        Every("check", 1, 1, Check);
    }

    /// <summary>Засича края на работата, почивката и таймера.</summary>
    internal void Check()
    {
        var now = Clock.Now;
        var p = State<PomodoroState>();
        if (p.Phase == WorkPhase.Focus && now >= p.EndsAt)
        {
            p = RolledOver(p);
            p = p with { Phase = WorkPhase.Break, EndsAt = now.AddMinutes(p.BreakMinutes), Today = p.Today + 1, Total = p.Total + 1 };
            Store.Set(p);
            Emit(new PomodoroFocusDone(p.FocusMinutes, p.Today));
        }
        else if (p.Phase == WorkPhase.Break && now >= p.EndsAt)
        {
            bool next = p.SeriesLeft > 0;
            Store.Set(next
                ? p with { Phase = WorkPhase.Focus, EndsAt = now.AddMinutes(p.FocusMinutes), SeriesLeft = p.SeriesLeft - 1 }
                : p with { Phase = WorkPhase.Off });
            Emit(new PomodoroBreakDone(next));
        }

        var t = State<WorkTimerState>();
        if (t.Running && now >= t.EndsAt)
        {
            Store.Set(t with { EndsAt = DateTime.MinValue });
            Emit(new WorkTimerFinished(t.Minutes, t.Label, t.Kind));
        }
    }

    /// <summary>Нов ден: броячът „днес“ почва от нула.</summary>
    private PomodoroState RolledOver(PomodoroState p)
    {
        string today = Clock.DayKey();
        return p.Day == today ? p : p with { Day = today, Today = 0 };
    }
}
