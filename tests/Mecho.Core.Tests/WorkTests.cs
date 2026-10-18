using Mecho.Core;
using Xunit;

namespace Mecho.Core.Tests;

public class WorkTests
{
    private static (GameEngine E, ManualClock Clock, List<ISignal> Signals) Started()
    {
        var (e, clock, _) = Kit.Engine();
        e.Modules.Add(new EconomyModule());
        e.Modules.Add(new WorkModule());
        var signals = new List<ISignal>();
        e.Bus.SubscribeAll(new object(), signals.Add);
        e.Start();
        return (e, clock, signals);
    }

    private static void RunFor(GameEngine e, ManualClock clock, double seconds)
    {
        for (double t = 0; t < seconds; t += 1)
        {
            clock.Advance(1);
            e.Update(1);
        }
    }

    [Fact]
    public void Series_runs_focus_break_focus_break_then_stops_on_its_own()
    {
        var (e, clock, signals) = Started();
        e.Bus.Send(new SetPomodoroSeries(2));
        e.Bus.Send(new StartPomodoro(1, 1, NewSeries: true));
        Assert.Equal(1, e.Store.Get<PomodoroState>().Number);

        RunFor(e, clock, 61);
        Assert.Equal(WorkPhase.Break, e.Store.Get<PomodoroState>().Phase);
        Assert.Contains(new PomodoroFocusDone(1, 1), signals);

        RunFor(e, clock, 61);
        var p = e.Store.Get<PomodoroState>();
        Assert.Equal(WorkPhase.Focus, p.Phase);
        Assert.Equal(2, p.Number);
        Assert.Contains(new PomodoroBreakDone(true), signals);

        RunFor(e, clock, 122);
        p = e.Store.Get<PomodoroState>();
        Assert.Equal(WorkPhase.Off, p.Phase);
        Assert.Equal(2, p.Today);
        Assert.Equal(2, p.Total);
        Assert.Contains(new PomodoroBreakDone(false), signals);
    }

    [Fact]
    public void Mode_and_series_cannot_change_while_running_and_stop_resets_series()
    {
        var (e, _, signals) = Started();
        e.Bus.Send(new SetPomodoroSeries(3));
        e.Bus.Send(new StartPomodoro(25, 5, true));
        Assert.False(e.Bus.Send(new SetPomodoroMode("90", 90, 20)).Ok);
        Assert.False(e.Bus.Send(new SetPomodoroSeries(1)).Ok);
        e.Bus.Send(new StopPomodoro());
        var p = e.Store.Get<PomodoroState>();
        Assert.Equal(WorkPhase.Off, p.Phase);
        Assert.Equal(0, p.SeriesLeft);
        Assert.Contains(new PomodoroStopped(true), signals);
        Assert.True(e.Bus.Send(new SetPomodoroMode("90", 90, 20)).Ok);
        Assert.False(e.Bus.Send(new StartPomodoro(0, 5, true)).Ok);
    }

    [Fact]
    public void Today_counter_rolls_over_at_midnight()
    {
        var (e, clock, _) = Kit.Engine(new DateTime(2026, 10, 18, 23, 50, 0));
        e.Modules.Add(new EconomyModule());
        e.Modules.Add(new WorkModule());
        e.Start();
        e.Bus.Send(new SetPomodoro(PomodoroState.Default with { Today = 5, Day = "2026-10-18", Total = 9 }));
        e.Bus.Send(new StartPomodoro(25, 5, true));
        RunFor(e, clock, 25 * 60 + 1);
        var p = e.Store.Get<PomodoroState>();
        Assert.Equal("2026-10-19", p.Day);
        Assert.Equal(1, p.Today);
        Assert.Equal(10, p.Total);
    }

    [Fact]
    public void Timer_finishes_and_stop_reports_minutes_done()
    {
        var (e, clock, signals) = Started();
        e.Bus.Send(new StartWorkTimer(2, " скица ", WorkTimerKinds.Rest));
        Assert.Equal("скица", e.Store.Get<WorkTimerState>().Label);
        RunFor(e, clock, 121);
        Assert.False(e.Store.Get<WorkTimerState>().Running);
        Assert.Contains(new WorkTimerFinished(2, "скица", WorkTimerKinds.Rest), signals);

        e.Bus.Send(new StartWorkTimer(10, "", "garbage"));
        Assert.Equal(WorkTimerKinds.Other, e.Store.Get<WorkTimerState>().Kind);
        clock.Advance(4 * 60);
        e.Bus.Send(new StopWorkTimer());
        var stopped = signals.OfType<WorkTimerStopped>().Single();
        Assert.InRange(stopped.MinutesDone, 3.99, 4.01);
        Assert.False(e.Bus.Send(new StartWorkTimer(0, "", WorkTimerKinds.Work)).Ok);
    }

    [Fact]
    public void Pay_gives_a_nut_per_ten_minutes_and_keeps_the_rest()
    {
        var (e, _, _) = Started();
        Assert.Equal(2, WorkModule.NutsFor(0, 25));
        e.Bus.Send(new PayWork(25));
        Assert.Equal(2, e.Store.Get<WalletState>().Nuts);
        Assert.Equal(5, e.Store.Get<WorkBankState>().Minutes, 6);
        Assert.Equal(1, WorkModule.NutsFor(5, 5));
        e.Bus.Send(new PayWork(5));
        Assert.Equal(3, e.Store.Get<WalletState>().Nuts);
        Assert.Equal(0, e.Store.Get<WorkBankState>().Minutes, 6);
        Assert.False(e.Bus.Send(new PayWork(double.NaN)).Ok);
    }
}
