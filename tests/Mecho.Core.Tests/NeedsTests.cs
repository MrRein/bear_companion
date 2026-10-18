using Mecho.Core;
using Xunit;

namespace Mecho.Core.Tests;

public class NeedsTests
{
    private static (GameEngine E, ManualClock Clock, List<ISignal> Signals) Started()
    {
        var (e, clock, _) = Kit.Engine();
        e.Modules.Add(new NeedsModule());
        e.Modules.Add(new BearStatusModule());
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
    public void Hunger_drops_over_time_slower_when_asleep()
    {
        var (e, clock, _) = Started();
        e.Bus.Send(new SetNeeds(100, null, "тест"));
        RunFor(e, clock, 3600);
        double awake = 100 - e.Store.Get<NeedsState>().Fullness;
        Assert.InRange(awake, NeedsModule.HungerAwake - 0.5, NeedsModule.HungerAwake + 0.5);

        e.Bus.Send(new SetNeeds(100, null, "тест"));
        e.Bus.Send(new ReportBearStatus(true, false, false, "sleep"));
        RunFor(e, clock, 3600);
        double asleep = 100 - e.Store.Get<NeedsState>().Fullness;
        Assert.InRange(asleep, NeedsModule.HungerAsleep - 0.5, NeedsModule.HungerAsleep + 0.5);
    }

    [Fact]
    public void Events_modify_hunger_rate()
    {
        var (e, clock, _) = Started();
        e.Bus.Send(new SetNeeds(100, null, "тест"));
        e.Modifiers.Set("sick_day", Mods.HungerRate, 0.5);
        RunFor(e, clock, 3600);
        Assert.InRange(100 - e.Store.Get<NeedsState>().Fullness, NeedsModule.HungerAwake / 2 - 0.5, NeedsModule.HungerAwake / 2 + 0.5);
    }

    [Fact]
    public void Adjust_and_set_clamp_and_reject_garbage()
    {
        var (e, _, _) = Started();
        e.Bus.Send(new SetNeeds(50, 50, "тест"));
        e.Bus.Send(new AdjustNeeds(80, -80, "тест"));
        Assert.Equal(new NeedsState(100, 0), e.Store.Get<NeedsState>());
        Assert.False(e.Bus.Send(new AdjustNeeds(double.NaN, 0, "x")).Ok);
        Assert.False(e.Bus.Send(new SetNeeds(double.PositiveInfinity, null, "x")).Ok);
    }

    [Fact]
    public void NeedsChanged_is_coarse_StateChanged_is_exact()
    {
        var (e, _, signals) = Started();
        signals.Clear();
        e.Bus.Send(new SetNeeds(null, 89.6, "тест"));   // 0.4 – без грубия сигнал
        e.Bus.Send(new SetNeeds(null, 88.9, "тест"));   // 1.1 от обявеното – сигнал
        Assert.Single(signals.OfType<NeedsChanged>());
        Assert.Equal(2, signals.OfType<StateChanged<NeedsState>>().Count());
    }

    [Fact]
    public void Bear_status_signals_sleep_and_activity()
    {
        var (e, _, signals) = Started();
        e.Bus.Send(new ReportBearStatus(false, false, false, "reading"));
        e.Bus.Send(new ReportBearStatus(true, false, false, "sleep"));
        e.Bus.Send(new ReportBearStatus(true, false, false, "sleep"));   // същото – нищо
        Assert.Equal(new[] { true }, signals.OfType<SleepChanged>().Select(s => s.Asleep));
        Assert.Equal(new[] { "reading", "sleep" }, signals.OfType<ActivityChanged>().Select(a => a.New));
    }
}
