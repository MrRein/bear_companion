using Mecho.Core;
using Xunit;

namespace Mecho.Core.Tests;

public class EventsTests
{
    private static (GameEngine E, ManualClock Clock, List<ISignal> Signals) Started(DateTime start, IEnumerable<GameEventDef>? defs = null)
    {
        var (e, clock, _) = Kit.Engine(start);
        e.Modules.Add(new EventDirector(defs));
        var signals = new List<ISignal>();
        e.Bus.SubscribeAll(new object(), signals.Add);
        e.Start();
        return (e, clock, signals);
    }

    [Fact]
    public void Christmas_starts_on_its_dates_and_sets_modifiers_until_midnight()
    {
        var (e, clock, signals) = Started(new DateTime(2026, 12, 24, 9, 0, 0));
        Assert.True(e.Store.Get<EventsState>().IsActive(GameEvents.Christmas));
        Assert.Contains(new GameEventStarted(GameEvents.Christmas), signals);
        Assert.Contains("tree", e.Modifiers.Get(Mods.Decor));
        Assert.Equal(2, e.Modifiers.Get(Mods.GiftChance));
    }

    [Fact]
    public void Not_christmas_in_october()
    {
        var (e, _, _) = Started(new DateTime(2026, 10, 18, 9, 0, 0), new[] { GameEvents.All.First(d => d.Id == GameEvents.Christmas) });
        Assert.False(e.Store.Get<EventsState>().IsActive(GameEvents.Christmas));
        Assert.False(e.Bus.Send(new StartGameEvent(GameEvents.Christmas)).Ok);
        Assert.True(e.Bus.Send(new StartGameEvent(GameEvents.Christmas, Force: true)).Ok);
    }

    [Fact]
    public void Sick_day_sells_medicine_and_ends_when_treated()
    {
        var (e, _, signals) = Started(new DateTime(2026, 10, 18, 9, 0, 0), new[] { GameEvents.All.First(d => d.Id == GameEvents.SickDay) });
        e.Bus.Send(new StartGameEvent(GameEvents.SickDay));
        Assert.Contains(GameEvents.Medicine, e.Modifiers.Get(Mods.ShopExtras));
        Assert.Equal(1.5, e.Modifiers.Get(Mods.EnergyDrain));
        e.Bus.Send(new EndGameEvent(GameEvents.SickDay, "лекарство"));
        Assert.DoesNotContain(GameEvents.Medicine, e.Modifiers.Get(Mods.ShopExtras));
        Assert.Equal(1, e.Modifiers.Get(Mods.EnergyDrain));
        Assert.Contains(new GameEventEnded(GameEvents.SickDay, "лекарство"), signals);
    }

    [Fact]
    public void Day_events_end_at_midnight_and_have_cooldown()
    {
        var def = new GameEventDef { Id = "test_day", Name = "тест", DailyChance = 1, CooldownDays = 3, Apply = (m, s) => m.Add(s, Mods.LinePacks, "x") };
        var (e, clock, signals) = Started(new DateTime(2026, 10, 18, 23, 59, 0), new[] { def });
        Assert.True(e.Store.Get<EventsState>().IsActive("test_day"));
        for (int i = 0; i < 120; i++)
        {
            clock.Advance(1);
            e.Update(1);
        }
        Assert.Contains(signals, s => s is GameEventEnded { EventId: "test_day" });
        Assert.False(e.Store.Get<EventsState>().IsActive("test_day"));   // утре – още „почива“
        Assert.Empty(e.Modifiers.Get(Mods.LinePacks));
    }

    [Fact]
    public void Moments_happen_in_their_hours_and_end_after_duration()
    {
        var def = new GameEventDef { Id = "blink", Name = "миг", Kind = GameEventKind.Moment, HourlyChance = 60, Duration = TimeSpan.FromMinutes(2), On = d => d.Hour >= 9 };
        var (e, clock, signals) = Started(new DateTime(2026, 10, 18, 10, 0, 0), new[] { def });
        for (int i = 0; i < 61; i++)
        {
            clock.Advance(1);
            e.Update(1);
        }
        Assert.Contains(new GameEventStarted("blink"), signals);
        for (int i = 0; i < 200; i++)
        {
            clock.Advance(1);
            e.Update(1);
        }
        Assert.Contains(signals, s => s is GameEventEnded { EventId: "blink" });
    }

    [Fact]
    public void Active_event_reapplies_modifiers_after_restart()
    {
        var (e, _, _) = Started(new DateTime(2026, 10, 18, 9, 0, 0));
        e.Bus.Send(new StartGameEvent(GameEvents.PajamaDay));
        var json = e.Store.Export();
        var (e2, _, _) = Kit.Engine(new DateTime(2026, 10, 18, 15, 0, 0));
        e2.Store.Import(json);
        e2.Modules.Add(new EventDirector());
        e2.Start();
        Assert.True(WardrobeModule.WantsPajamas(e2.Modifiers));
    }
}
