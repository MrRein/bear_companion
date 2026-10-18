using Mecho.Core;
using Xunit;

namespace Mecho.Core.Tests;

public class LifeTests
{
    private static (GameEngine E, ManualClock Clock, List<ISignal> Signals) Started(DateTime? start = null, bool pot = true)
    {
        var (e, clock, _) = Kit.Engine(start);
        e.Modules.Add(new EconomyModule());
        e.Modules.Add(new GiftsModule());
        e.Modules.Add(new GardenModule("plant"));
        var signals = new List<ISignal>();
        e.Bus.SubscribeAll(new object(), signals.Add);
        e.Start();
        if (pot) e.Bus.Send(new GrantItem("plant", "тест"));
        return (e, clock, signals);
    }

    [Fact]
    public void Gift_is_planned_once_a_day_and_holiday_makes_it_certain()
    {
        var (e, _, _) = Started();
        var g = e.Store.Get<GiftState>();
        Assert.Equal("2026-10-18", g.Day);
        e.Modifiers.Set("event.christmas", Mods.GiftChance, 10);
        e.Bus.Send(new PlanGiftToday());
        Assert.Equal(g, e.Store.Get<GiftState>());          // вече е решено за днес
        e.Bus.Send(new PlanGiftToday(Force: true));
        Assert.True(e.Store.Get<GiftState>().Planned);
        e.Bus.Send(new GiftPresented("pink"));
        var p = e.Store.Get<GiftState>();
        Assert.True(p.Pending);
        Assert.False(p.Planned);
        e.Bus.Send(new GiftOpened());
        Assert.False(e.Store.Get<GiftState>().Pending);
    }

    [Fact]
    public void Care_grows_the_plant_at_most_twice_a_day()
    {
        var (e, _, signals) = Started();
        for (int i = 0; i < 5; i++) e.Bus.Send(new CareForPlant(GardenModule.MinutesPerStage));
        Assert.Equal(2, e.Store.Get<PlantState>().Stage);
        Assert.Equal(2, signals.OfType<PlantGrew>().Count());
    }

    [Fact]
    public void Plant_grows_alone_every_five_days_only_if_the_pot_is_owned()
    {
        var (e, clock, _) = Started();
        e.Bus.Send(new SetPlant(PlantState.Seed with { LastGrow = clock.Now.AddDays(-11) }));
        e.Bus.Send(new GrowPlant(false));      // фаза 1 веднага, LastGrow = сега
        Assert.Equal(1, e.Store.Get<PlantState>().Stage);

        var (e2, clock2, _) = Started(pot: false);
        e2.Bus.Send(new SetPlant(PlantState.Seed with { LastGrow = clock2.Now.AddDays(-11) }));
        clock2.Advance(601);
        e2.Update(601);
        Assert.Equal(0, e2.Store.Get<PlantState>().Stage);
        e2.Bus.Send(new GrantItem("plant", "тест"));
        clock2.Advance(601);
        e2.Update(601);
        Assert.Equal(2, e2.Store.Get<PlantState>().Stage);
    }

    [Fact]
    public void Plant_stops_at_full_bloom()
    {
        var (e, _, signals) = Started();
        e.Bus.Send(new SetPlant(PlantState.Seed with { Stage = GardenModule.Stages - 2 }));
        e.Bus.Send(new GrowPlant(false));
        e.Bus.Send(new GrowPlant(false));
        Assert.Equal(GardenModule.Stages - 1, e.Store.Get<PlantState>().Stage);
        Assert.Single(signals.OfType<PlantGrew>(), s => s.Bloomed);
    }
}
