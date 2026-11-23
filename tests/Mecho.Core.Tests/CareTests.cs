using System.Collections.Immutable;
using Mecho.Core;
using Xunit;

namespace Mecho.Core.Tests;

// Фаза 3: Мечо забелязва теб.
public class CareTests
{
    private static (GameEngine E, ManualClock Clock, List<string> Thoughts, CareModule Care) Started(CareContext? ctx = null, int hour = 14)
    {
        var (e, clock, _) = Kit.Engine(new DateTime(2026, 10, 18, hour, 0, 0));
        e.Modules.Add(new EconomyModule());
        e.Modules.Add(new BearStatusModule());
        e.Modules.Add(new TasksModule());
        e.Modules.Add(new ThoughtsModule());
        var care = new CareModule();
        e.Modules.Add(care);
        var thoughts = new List<string>();
        e.Start();
        care.Context = () => (ctx ?? Calm(clock)) with { Now = clock.Now };
        return (e, clock, thoughts, care);
    }

    private static CareContext Calm(ManualClock c) => new(c.Now, false, 0, 2, 1, 0, 0);

    private static List<string> Pending(GameEngine e) => e.Store.Get<TalkState>().Pending.Select(t => t.Key).ToList();

    [Fact]
    public void Calm_day_nothing_to_say()
    {
        var (e, _, _, _) = Started();
        e.Bus.Send(new CheckCare());
        Assert.Empty(Pending(e));
    }

    [Fact]
    public void Two_hours_without_a_break_worries_him_once_in_a_while()
    {
        var (e, clock, _, _) = Started(new CareContext(DateTime.Now, true, 130, 4, 0, 0, 0));
        e.Bus.Send(new CheckCare());
        var t = e.Store.Get<TalkState>().Pending.Single();
        Assert.Equal("care.overwork", t.Key);
        Assert.Equal("2", t.Fact("часа"));
    }

    [Fact]
    public void Late_work_and_bad_day()
    {
        var (e, _, _, _) = Started(new CareContext(DateTime.Now, true, 30, 1, 0, 0, 0), hour: 23);
        e.Bus.Send(new CheckCare());
        Assert.Contains("care.late", Pending(e));
        var (e2, _, _, _) = Started(new CareContext(DateTime.Now, false, 0, 0, 0, 0, 0), hour: 19);
        e2.Bus.Send(new CheckCare());
        Assert.Contains("care.badday", Pending(e2));
    }

    [Fact]
    public void Streak_and_comeback_after_first_pomodoro()
    {
        var (e, _, _, _) = Started(new CareContext(DateTime.Now, false, 0, 1, 0, 4, 0));
        e.Bus.Send(new CheckCare());
        Assert.Contains("care.streak", Pending(e));
        var (e2, _, _, _) = Started(new CareContext(DateTime.Now, false, 0, 1, 0, 0, 5));
        e2.Bus.Send(new CheckCare());
        Assert.Contains("care.comeback", Pending(e2));
    }

    [Fact]
    public void Task_rolled_four_days_is_noticed()
    {
        var (e, clock, _, _) = Started();
        var task = new TaskEntry(Guid.NewGuid(), "Данъците", "", 2, false, null, false, Day: clock.Today(), Rolled: 5);
        e.Bus.Send(new SetTasks(TasksState.Empty with { Open = ImmutableList.Create(task) }));
        e.Bus.Send(new CheckCare());
        var t = e.Store.Get<TalkState>().Pending.Single(x => x.Key == "care.procrastinate");
        Assert.Equal("Данъците", t.Fact("задача"));
        Assert.Equal(task.Id.ToString(), t.Fact("id"));
    }

    [Fact]
    public void Big_task_done_is_celebrated_with_its_name()
    {
        var (e, _, _, _) = Started();
        e.Bus.Send(new AddTask("Голямото писмо", 3));
        var id = e.Store.Get<TasksState>().Open.Single().Id;
        e.Bus.Send(new SetTaskDone(id, true));
        var t = e.Store.Get<TalkState>().Pending.Single(x => x.Key == "care.bigtask");
        Assert.Equal("Голямото писмо", t.Fact("задача"));
    }

    [Fact]
    public void New_observers_plug_in()
    {
        var (e, _, _, care) = Started();
        care.Add(new Always());
        e.Bus.Send(new CheckCare());
        Assert.Contains("care.test", Pending(e));
    }

    private sealed class Always : IObserver
    {
        public CareIdea? Look(CareContext c, TasksState t) => new("care.test", 3, 1);
    }
}
