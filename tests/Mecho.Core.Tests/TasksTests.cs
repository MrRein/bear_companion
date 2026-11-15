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
    public void Edit_keeps_the_task_and_its_place()
    {
        var (e, _, _) = Started();
        e.Bus.Send(new AddTask("a", 1));
        e.Bus.Send(new AddTask("b", 1));
        var first = e.Store.Get<TasksState>().Open[0];
        Assert.True(e.Bus.Send(new EditTask(first.Id, "  нова  ", 9, " описание ")).Ok);
        var edited = e.Store.Get<TasksState>().Open[0];
        Assert.Equal(first.Id, edited.Id);
        Assert.Equal("нова", edited.Title);
        Assert.Equal(3, edited.Size);
        Assert.Equal("описание", edited.Description);
        Assert.False(e.Bus.Send(new EditTask(first.Id, "   ", 1)).Ok);
        Assert.False(e.Bus.Send(new EditTask(Guid.NewGuid(), "x", 1)).Ok);
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

    [Fact]
    public void New_task_is_for_today_or_a_day_or_someday()
    {
        var (e, clock, _) = Started(new DateTime(2026, 11, 16, 10, 0, 0));
        e.Bus.Send(new AddTask("днес", 1));
        Assert.Equal(new DateTime(2026, 11, 16), Last(e).Day);
        e.Bus.Send(new AddTask("сряда", 1, Day: new DateTime(2026, 11, 18, 15, 0, 0), Tag: " work ", Star: true, Minutes: 9999));
        var t = Last(e);
        Assert.Equal((new DateTime(2026, 11, 18), "work", true, TasksModule.MaxMinutes), (t.Day, t.Tag, t.Star, t.Minutes));
        e.Bus.Send(new AddTask("някога", 1, Someday: true));
        Assert.Null(Last(e).Day);
        var s = e.Store.Get<TasksState>();
        Assert.Equal("някога", Assert.Single(s.Someday).Title);
        Assert.Equal("сряда", Assert.Single(s.OnDay(new DateTime(2026, 11, 18))).Title);
    }

    [Fact]
    public void Move_puts_the_task_in_the_day_and_place()
    {
        var (e, _, _) = Started(new DateTime(2026, 11, 16, 10, 0, 0));
        var mon = new DateTime(2026, 11, 16);
        var tue = mon.AddDays(1);
        foreach (var name in new[] { "a", "b", "c" }) e.Bus.Send(new AddTask(name, 1));
        e.Bus.Send(new AddTask("x", 1, Day: tue));
        var s = e.Store.Get<TasksState>();
        Guid a = s.Open[0].Id, b = s.Open[1].Id, c = s.Open[2].Id, x = s.Open[3].Id;

        e.Bus.Send(new MoveTask(c, mon, Before: a));                 // нагоре в деня
        Assert.Equal("c a b", Titles(e, mon));
        e.Bus.Send(new MoveTask(a, tue));                            // накрая на вторник
        Assert.Equal("c b", Titles(e, mon));
        Assert.Equal("x a", Titles(e, tue));
        e.Bus.Send(new MoveTask(b, tue, Before: x));
        Assert.Equal("b x a", Titles(e, tue));
        e.Bus.Send(new MoveTask(x, null));                           // „Някога“
        Assert.Equal("x", string.Join(" ", e.Store.Get<TasksState>().Someday.Select(t => t.Title)));
        Assert.False(e.Bus.Send(new MoveTask(Guid.NewGuid(), mon)).Ok);
    }

    private static string Titles(GameEngine e, DateTime day) => string.Join(" ", e.Store.Get<TasksState>().OnDay(day).Select(t => t.Title));

    [Fact]
    public void Mark_changes_only_what_is_given_and_size_only_while_open()
    {
        var (e, _, _) = Started();
        e.Bus.Send(new AddTask("a", 1, Tag: "home"));
        var id = Last(e).Id;
        e.Bus.Send(new MarkTask(id, Star: true));
        Assert.Equal(("home", true, 0, 1), (Last(e).Tag, Last(e).Star, Last(e).Minutes, Last(e).Size));
        e.Bus.Send(new MarkTask(id, Tag: "", Minutes: 45, Size: 3));
        Assert.Equal(("", true, 45, 3), (Last(e).Tag, Last(e).Star, Last(e).Minutes, Last(e).Size));
        e.Bus.Send(new SetTaskDone(id, true));
        e.Bus.Send(new MarkTask(id, Size: 1));
        Assert.Equal(3, Last(e).Size);
    }

    [Fact]
    public void Unfinished_tasks_roll_over_to_today()
    {
        var (e, clock, signals) = Started(new DateTime(2026, 11, 16, 23, 59, 0));
        e.Bus.Send(new AddTask("недовършена", 1));
        e.Bus.Send(new AddTask("свършена", 1));
        e.Bus.Send(new SetTaskDone(Last(e).Id, true));
        e.Bus.Send(new AddTask("за петък", 1, Day: new DateTime(2026, 11, 20)));
        e.Bus.Send(new AddTask("някога", 1, Someday: true));
        for (int i = 0; i < 120; i++)
        {
            clock.Advance(1);
            e.Update(1);
        }
        var s = e.Store.Get<TasksState>();
        var rolled = s.Open.Single(t => t.Title == "недовършена");
        Assert.Equal((new DateTime(2026, 11, 17), 1), (rolled.Day, rolled.Rolled));
        Assert.Equal(new DateTime(2026, 11, 20), s.Open.Single(t => t.Title == "за петък").Day);
        Assert.Null(s.Open.Single(t => t.Title == "някога").Day);
        Assert.Contains(new TasksRolledOver(1), signals);
        // Вчерашната свършена – в историята, но се вижда във вчерашния ден.
        Assert.Equal("свършена", Titles(e, new DateTime(2026, 11, 16)));
    }

    [Fact]
    public void Old_saves_keep_open_tasks_for_today()
    {
        // Запис от версия 1 (без ден): несвършената е за днес, свършената – за деня, в който е свършена.
        string v1 = """
        {"schema":1,"slices":{"tasks":{"v":1,"data":{"Open":[
          {"Id":"11111111-1111-1111-1111-111111111111","Title":"стара","Description":"","Size":2,"Done":false,"DoneAt":null,"PageRolled":false},
          {"Id":"22222222-2222-2222-2222-222222222222","Title":"свършена","Description":"","Size":1,"Done":true,"DoneAt":"2026-11-16T09:30:00","PageRolled":false}
        ],"History":[],"DoneTotal":1,"Focus":null}}}}
        """;
        var (e, _, _) = Kit.Engine(new DateTime(2026, 11, 16, 12, 0, 0));
        e.Store.Import(System.Text.Json.Nodes.JsonNode.Parse(v1)!.AsObject());
        e.Modules.Add(new EconomyModule());
        e.Modules.Add(new TasksModule());
        e.Start();
        var s = e.Store.Get<TasksState>();
        Assert.Equal(new DateTime(2026, 11, 16), s.Open[0].Day);
        Assert.Equal(new DateTime(2026, 11, 16), s.Open[1].Day);
        Assert.Equal(("", false, 0, 0), (s.Open[0].Tag, s.Open[0].Star, s.Open[0].Minutes, s.Open[0].Rolled));
        Assert.Empty(s.Someday);
    }
}
