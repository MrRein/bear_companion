using Mecho.Core;
using Xunit;

namespace Mecho.Core.Tests;

public class OpinionTests
{
    private static (GameEngine E, ManualClock Clock, OpinionModule O) Started(int hour = 13)
    {
        var (e, clock, _) = Kit.Engine(new DateTime(2026, 10, 18, hour, 0, 0));
        var o = new OpinionModule { WhimCandidates = () => new[] { "mushrooms" } };
        o.Settings = o.Settings with { WhimChance = 0 };
        e.Modules.Add(o);
        e.Start();
        return (e, clock, o);
    }

    private static OpinionInput Food(ManualClock c, string id, double fullness = 60, double energy = 70, bool wished = false,
        bool today = false, bool yesterday = false, params string[] tags) =>
        new("cook", id, tags.ToHashSet(), c.Now, fullness, energy, wished, today, yesterday, ObjectName: id);

    [Fact]
    public void Normal_food_is_fine_and_wished_food_is_loved()
    {
        var (_, c, o) = Started();
        Assert.Equal(Stance.Neutral, o.Evaluate(Food(c, "pizza")).Stance);
        var loved = o.Evaluate(Food(c, "pizza", wished: true));
        Assert.Equal(Stance.Loves, loved.Stance);
        Assert.Equal("opinion.loves", loved.Reason!.Key);
    }

    [Fact]
    public void Ate_yesterday_grumbles_ate_today_grumbles_more()
    {
        var (_, c, o) = Started();
        var y = o.Evaluate(Food(c, "pizza", yesterday: true));
        Assert.Equal(Stance.Grumbles, y.Stance);
        Assert.Equal("opinion.ate.yesterday", y.Reason!.Key);
        Assert.Equal("pizza", y.Reason.Facts["храна"]);
        Assert.Equal("opinion.ate.today", o.Evaluate(Food(c, "pizza", today: true)).Reason!.Key);
    }

    [Fact]
    public void Heavy_breakfast_is_refused_once_a_day_never_twice_in_a_row()
    {
        var (e, c, o) = Started(hour: 8);
        var r = o.Evaluate(Food(c, "soup", tags: "heavy"));
        Assert.Equal(Stance.Refuses, r.Stance);
        Assert.Equal("opinion.breakfast.heavy", r.Reason!.Key);
        e.Bus.Send(new OpinionShown(Stance.Refuses));
        // Втори път днес – само мрънка.
        Assert.Equal(Stance.Grumbles, o.Evaluate(Food(c, "soup", tags: "heavy")).Stance);
        // Утре сутрин – пак може, но не веднага след отказ.
        c.Now = c.Now.AddDays(1);
        Assert.Equal(Stance.Grumbles, o.Evaluate(Food(c, "soup", tags: "heavy")).Stance);
        e.Bus.Send(new OpinionShown(Stance.Neutral));
        Assert.Equal(Stance.Refuses, o.Evaluate(Food(c, "soup", tags: "heavy")).Stance);
    }

    [Fact]
    public void Starving_bear_eats_anything()
    {
        var (_, c, o) = Started(hour: 8);
        Assert.NotEqual(Stance.Refuses, o.Evaluate(Food(c, "soup", fullness: 10, tags: "heavy")).Stance);
        Assert.Equal(Stance.Neutral, o.Evaluate(Food(c, "pizza", fullness: 10, yesterday: true)).Stance);
    }

    [Fact]
    public void Whim_and_weather_and_tiredness()
    {
        var (e, c, o) = Started();
        e.Bus.Send(new SetWhim("mushrooms"));
        Assert.Equal("opinion.whim", o.Evaluate(Food(c, "mushrooms")).Reason!.Key);
        Assert.Equal(Stance.Neutral, o.Evaluate(Food(c, "pizza")).Stance);
        Assert.Equal("opinion.tired.cook", o.Evaluate(Food(c, "pizza", energy: 10, tags: "complex")).Reason!.Key);
        var hot = new OpinionInput("wear", "sweater", new HashSet<string> { "warm" }, c.Now, 60, 70, false, false, false, Temperature: 30, ObjectName: "Пуловер");
        var r = o.Evaluate(hot);
        Assert.Equal(Stance.Grumbles, r.Stance);
        Assert.Equal("opinion.hot.warm", r.Reason!.Key);
        Assert.Equal(Stance.Neutral, o.Evaluate(hot with { Temperature = 10 }).Stance);
    }

    [Fact]
    public void New_reasons_plug_in_as_classes()
    {
        var (_, c, o) = Started();
        o.Add(new Rainy());
        Assert.Equal("opinion.rain", o.Evaluate(new OpinionInput("walk", "park", new HashSet<string>(), c.Now, 60, 70, false, false, false)).Reason!.Key);
    }

    private sealed class Rainy : IOpinionFactor
    {
        public OpinionReason? Evaluate(OpinionInput i, OpinionModule o) => i.Verb == "walk" ? new("opinion.rain", -1.5, Facts.Empty) : null;
    }
}
