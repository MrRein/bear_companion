using Mecho.Core;
using Xunit;

namespace Mecho.Core.Tests;

public class InteractionsTests
{
    private static (GameEngine E, ManualClock Clock) Started(DateTime? start = null)
    {
        var (e, clock, _) = Kit.Engine(start);
        e.Modules.Add(new InteractionsModule());
        e.Start();
        return (e, clock);
    }

    private static bool Try(GameEngine e, string item, string action, bool urgent = false) => e.Bus.Send(new TryInteract(item, action, urgent)).Ok;

    [Fact]
    public void Not_too_often_overall_per_item_and_per_action()
    {
        var (e, clock) = Started();
        Assert.True(Try(e, "heater", "love"));
        Assert.False(Try(e, "lamp", "love"));                // твърде скоро след друго
        clock.Advance(InteractionsModule.MinGap.TotalSeconds + 1);
        Assert.True(Try(e, "lamp", "love"));
        clock.Advance(InteractionsModule.MinGap.TotalSeconds + 1);
        Assert.False(Try(e, "heater", "warm"));              // печицата – скоро беше
        clock.Advance(InteractionsModule.ItemGap.TotalSeconds);
        Assert.False(Try(e, "heater", "love"));              // същото действие – след часове
        Assert.True(Try(e, "heater", "warm"));
    }

    [Fact]
    public void Urgent_needs_only_a_short_gap()
    {
        var (e, clock) = Started();
        Assert.True(Try(e, "heater", "love"));
        Assert.False(Try(e, "heater", "turn_on", urgent: true));
        clock.Advance(InteractionsModule.MinUrgentGap.TotalSeconds + 1);
        Assert.True(Try(e, "heater", "turn_on", urgent: true));
    }

    [Fact]
    public void At_most_a_dozen_a_day_then_resets_tomorrow()
    {
        var (e, clock) = Started(new DateTime(2026, 10, 18, 6, 0, 0));
        int done = 0;
        for (int i = 0; i < 40; i++)
        {
            if (Try(e, "item" + i, "a")) done++;
            clock.Advance(InteractionsModule.MinGap.TotalSeconds + 1);
        }
        Assert.Equal(InteractionsModule.MaxPerDay, done);
        clock.Advance(TimeSpan.FromHours(20).TotalSeconds);
        Assert.True(Try(e, "new", "a"));
    }
}
