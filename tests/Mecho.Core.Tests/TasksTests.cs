using Mecho.Core;
using Xunit;

namespace Mecho.Core.Tests;

public class TasksTests
{
    private static (GameEngine E, ManualClock Clock, List<ISignal> Signals) Started(DateTime? start = null)
    {
        var (e, clock, _) = Kit.Engine(start);
        e.Modules.Add(new EconomyModule());
        e.Modules.Add(new TasksModule());
        var signals = new List<ISignal>();
        e.Bus.SubscribeAll(new object(), signals.Add);
        e.Start();
        return (e, clock, signals);
    }

    private static TaskEntry Last(GameEngine e) => e.Store.Get<TasksState>().Open[^1];

    [Fact]
    public void Done_pays_reward_and_undo_takes_it_back()
    {
        var (e, _, signals) = Started();
        Assert.False(e.Bus.Send(new AddTask("  ", 1)).Ok);
        e.Bus.Send(new AddTask(" Скица ", 3, " две версии "));
        var t = Last(e);
        Assert.Equal(("Скица", "две версии", 3), (t.Title, t.Description, t.Size));

        e.Bus.Send(new SetTaskDone(t.Id, true));
        Assert.Equal(5, e.Store.Get<WalletState>().Nuts);
        Assert.Equal(1, e.Store.Get<TasksState>().DoneTotal);
        Assert.Contains(new TaskCompleted(t.Id, 3, 5, true), signals);

        e.Bus.Send(new SetTaskDone(t.Id, false));
        Assert.Equal(0, e.Store.Get<WalletState>().Nuts);
        Assert.Equal(0, e.Store.Get<TasksState>().DoneTotal);

        e.Bus.Send(new SetTaskDone(t.Id, true));    // втори път – без нов шанс за страница
        Assert.Contains(new TaskCompleted(t.Id, 3, 5, false), signals);
    }

    [Fact]
    public void Undo_never_goes_below_zero_nuts()
    {
        var (e, _, _) = Started();
        e.Bus.Send(new AddTask("a", 2));
        var t = Last(e);
        e.Bus.Send(new SetTaskDone(t.Id, true));
        e.Bus.Send(new SetNuts(1, "тест"));
        e.Bus.Send(new SetTaskDone(t.Id, false));
        Assert.Equal(0, e.Store.Get<WalletState>().Nuts);
    }

    [Fact]
    public void Focus_task_clears_when_done_or_deleted()
    {
        var (e, _, _) = Started();
        e.Bus.Send(new AddTask("a", 1));
        var t = Last(e);
        Assert.False(e.Bus.Send(new SetFocusTask(Guid.NewGuid())).Ok);
        e.Bus.Send(new SetFocusTask(t.Id));
        Assert.Equal(t, e.Store.Get<TasksState>().FocusTask);
        e.Bus.Send(new SetTaskDone(t.Id, true));
        Assert.Null(e.Store.Get<TasksState>().FocusTask);
        e.Bus.Send(new DeleteTask(t.Id));
        Assert.Empty(e.Store.Get<TasksState>().Open);
        Assert.Null(e.Store.Get<TasksState>().Focus);
    }

    [Fact]
    public void Done_tasks_move_to_history_at_midnight()
    {
        var (e, clock, _) = Started(new DateTime(2026, 10, 18, 23, 59, 0));
        e.Bus.Send(new AddTask("вчерашна", 1));
        e.Bus.Send(new SetTaskDone(Last(e).Id, true));
        e.Bus.Send(new AddTask("за утре", 1));
        for (int i = 0; i < 120; i++)
        {
            clock.Advance(1);
            e.Update(1);
        }
        var s = e.Store.Get<TasksState>();
        Assert.Equal("за утре", Assert.Single(s.Open).Title);
        Assert.Equal("вчерашна", Assert.Single(s.History).Title);
    }

    [Fact]
    public void Tasks_survive_save_and_load()
    {
        var (e, _, _) = Started();
        e.Bus.Send(new AddTask("a", 2, "d"));
        e.Bus.Send(new SetFocusTask(Last(e).Id));
        var json = e.Store.Export();
        var (e2, _, _) = Kit.Engine();
        e2.Store.Import(json);
        e2.Modules.Add(new EconomyModule());
        e2.Modules.Add(new TasksModule());
        e2.Start();
        var a = e.Store.Get<TasksState>();
        var b = e2.Store.Get<TasksState>();
        Assert.Equal(a.Open, b.Open);
        Assert.Equal(a.Focus, b.Focus);
    }
}
