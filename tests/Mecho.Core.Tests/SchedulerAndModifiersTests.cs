using Mecho.Core;
using Xunit;

namespace Mecho.Core.Tests;

public class SchedulerAndModifiersTests
{
    [Fact]
    public void After_runs_once_at_the_right_time()
    {
        var (e, clock, _) = Kit.Engine();
        int n = 0;
        e.Scheduler.After(this, "once", 10, () => n++);
        clock.Advance(9);
        e.Scheduler.Update();
        Assert.Equal(0, n);
        clock.Advance(2);
        e.Scheduler.Update();
        clock.Advance(20);
        e.Scheduler.Update();
        Assert.Equal(1, n);
    }

    [Fact]
    public void Every_repeats_and_respects_when()
    {
        var (e, clock, _) = Kit.Engine();
        int n = 0;
        bool allowed = false;
        e.Scheduler.Every(this, "tick", 5, 5, () => n++, when: () => allowed);
        for (int i = 0; i < 4; i++) { clock.Advance(5); e.Scheduler.Update(); }
        Assert.Equal(0, n);
        allowed = true;
        for (int i = 0; i < 4; i++) { clock.Advance(5); e.Scheduler.Update(); }
        Assert.Equal(4, n);
    }

    [Fact]
    public void Same_name_replaces_and_failing_job_does_not_stop_others()
    {
        var (e, clock, log) = Kit.Engine();
        int a = 0, b = 0;
        e.Scheduler.After(this, "job", 1, () => a += 100);
        e.Scheduler.After(this, "job", 1, () => a++);        // замества
        e.Scheduler.After(this, "bad", 1, () => throw new Exception("boom"));
        e.Scheduler.After(this, "other", 1, () => b++);
        clock.Advance(2);
        e.Scheduler.Update();
        Assert.Equal(1, a);
        Assert.Equal(1, b);
        Assert.Equal(1, log.Errors);
        Assert.Throws<ArgumentOutOfRangeException>(() => e.Scheduler.Every(this, "x", 0, 1, () => { }));
    }

    [Fact]
    public void Day_change_publishes_DayStarted()
    {
        var (e, clock, _) = Kit.Engine(new DateTime(2026, 12, 24, 23, 59, 0));
        var days = new List<DateTime>();
        e.Bus.Subscribe<DayStarted>(this, d => days.Add(d.Day));
        e.Start();
        clock.Advance(120);
        e.Update(0.1);
        e.Update(0.1);
        Assert.Equal(new[] { new DateTime(2026, 12, 24), new DateTime(2026, 12, 25) }, days);
    }

    [Fact]
    public void Modifiers_combine_per_kind_and_clear_by_source()
    {
        var (e, _, _) = Kit.Engine();
        var m = e.Modifiers;
        int changes = 0;
        e.Bus.Subscribe<ModifiersChanged>(this, _ => changes++);
        m.Set("sick_day", Mods.EnergyDrain, 1.5);
        m.Set("evening", Mods.EnergyDrain, 2);
        m.Add("sick_day", Mods.ShopExtras, "medicine", "honey_tea");
        m.Add("christmas", Mods.ShopExtras, "gingerbread");
        m.Set("sick_day", Mods.NoWork, true);
        Assert.Equal(3, m.Get(Mods.EnergyDrain), 5);
        Assert.Equal(new[] { "gingerbread", "honey_tea", "medicine" }, m.Get(Mods.ShopExtras).OrderBy(x => x));
        Assert.True(m.Get(Mods.NoWork));
        m.Clear("sick_day");
        Assert.Equal(2, m.Get(Mods.EnergyDrain), 5);
        Assert.Equal(new[] { "gingerbread" }, m.Get(Mods.ShopExtras));
        Assert.False(m.Get(Mods.NoWork));
        Assert.True(changes >= 5);
        Assert.Throws<ArgumentOutOfRangeException>(() => m.Set("x", Mods.EnergyDrain, -1));
        Assert.Throws<ArgumentException>(() => m.Set(" ", Mods.EnergyDrain, 1));
    }
}
