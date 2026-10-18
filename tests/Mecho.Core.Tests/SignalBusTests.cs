using Mecho.Core;
using Xunit;

namespace Mecho.Core.Tests;

public class SignalBusTests
{
    private static SignalBus Bus(out MemoryLog log)
    {
        log = new MemoryLog();
        return new SignalBus(log);
    }

    [Fact]
    public void Publish_reaches_all_listeners_in_order()
    {
        var bus = Bus(out _);
        var seen = new List<string>();
        bus.Subscribe<Ping>(this, p => seen.Add("b" + p.N), order: 10);
        bus.Subscribe<Ping>(this, p => seen.Add("a" + p.N), order: 0);
        bus.Publish(new Ping(1));
        Assert.Equal(new[] { "a1", "b1" }, seen);
    }

    [Fact]
    public void Nested_publish_runs_after_current_signal()
    {
        var bus = Bus(out _);
        var seen = new List<string>();
        bus.Subscribe<Ping>(this, p => { seen.Add("ping1"); bus.Publish(new Pong(p.N)); });
        bus.Subscribe<Ping>(this, _ => seen.Add("ping2"));
        bus.Subscribe<Pong>(this, _ => seen.Add("pong"));
        bus.Publish(new Ping(1));
        Assert.Equal(new[] { "ping1", "ping2", "pong" }, seen);
    }

    [Fact]
    public void Failing_listener_does_not_stop_others_and_is_reported()
    {
        var bus = Bus(out var log);
        object? faulted = null;
        var bad = new object();
        bus.HandlerFaulted += (o, _) => faulted = o;
        bool good = false;
        bus.Subscribe<Ping>(bad, _ => throw new InvalidOperationException("boom"));
        bus.Subscribe<Ping>(this, _ => good = true);
        bus.Publish(new Ping(1));
        Assert.True(good);
        Assert.Same(bad, faulted);
        Assert.Equal(1, log.Errors);
    }

    [Fact]
    public void Null_arguments_throw_immediately()
    {
        var bus = Bus(out _);
        Assert.Throws<ArgumentNullException>(() => bus.Publish<Ping>(null!));
        Assert.Throws<ArgumentNullException>(() => bus.Subscribe<Ping>(null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() => bus.Subscribe<Ping>(this, null!));
        Assert.Throws<ArgumentNullException>(() => bus.Send<DoThing>(null!));
    }

    [Fact]
    public void Infinite_signal_loop_is_cut()
    {
        var bus = Bus(out var log);
        bus.MaxPerDrain = 100;
        bus.Subscribe<Ping>(this, p => bus.Publish(new Ping(p.N + 1)));
        bus.Publish(new Ping(0));
        Assert.True(log.Errors >= 1);
        Assert.True(bus.PublishedCount <= 101);
    }

    [Fact]
    public void Unsubscribe_owner_removes_everything()
    {
        var bus = Bus(out _);
        var owner = new object();
        int n = 0;
        bus.Subscribe<Ping>(owner, _ => n++);
        bus.Handle<DoThing>(owner, _ => CommandResult.Done);
        bus.Unsubscribe(owner);
        bus.Publish(new Ping(1));
        Assert.Equal(0, n);
        Assert.False(bus.CanHandle<DoThing>());
    }

    [Fact]
    public void Listener_removed_during_dispatch_is_not_called()
    {
        var bus = Bus(out _);
        Subscription? second = null;
        bool called = false;
        bus.Subscribe<Ping>(this, _ => second!.Dispose());
        second = bus.Subscribe<Ping>(this, _ => called = true);
        bus.Publish(new Ping(1));
        Assert.False(called);
    }

    [Fact]
    public void Commands_have_exactly_one_handler()
    {
        var bus = Bus(out _);
        bus.Handle<DoThing>(this, c => c.What == "ok" ? CommandResult.Done : CommandResult.Rejected("не"));
        Assert.Throws<InvalidOperationException>(() => bus.Handle<DoThing>(new object(), _ => CommandResult.Done));
        Assert.True(bus.Send(new DoThing("ok")).Ok);
        Assert.Equal("не", bus.Send(new DoThing("x")).Reason);
    }

    [Fact]
    public void Command_without_handler_is_rejected_not_thrown()
    {
        var bus = Bus(out var log);
        var r = bus.Send(new DoThing("x"));
        Assert.False(r.Ok);
        Assert.Equal(1, log.Warnings);
    }

    [Fact]
    public void Failing_command_is_rejected_and_reported()
    {
        var bus = Bus(out _);
        object? faulted = null;
        bus.HandlerFaulted += (o, _) => faulted = o;
        bus.Handle<DoThing>(this, _ => throw new Exception("boom"));
        Assert.False(bus.Send(new DoThing("x")).Ok);
        Assert.Same(this, faulted);
    }

    [Fact]
    public void Recent_keeps_readable_names()
    {
        var bus = Bus(out _);
        bus.Publish(new StateChanged<Counter>(new Counter(1), new Counter(2)));
        Assert.Equal("StateChanged<Counter>", bus.Recent[^1].Name);
    }

    [Fact]
    public void Publish_from_other_thread_without_marshal_throws()
    {
        var bus = Bus(out _);
        var ex = Task.Run(() => Record.Exception(() => bus.Publish(new Ping(1)))).Result;
        Assert.IsType<InvalidOperationException>(ex);
    }

    [Fact]
    public void Publish_from_other_thread_is_marshalled()
    {
        var queued = new List<Action>();
        var bus = new SignalBus(new MemoryLog(), a => { lock (queued) queued.Add(a); });
        int n = 0;
        bus.Subscribe<Ping>(this, _ => n++);
        Task.Run(() => bus.Publish(new Ping(1))).Wait();
        Assert.Equal(0, n);
        foreach (var a in queued) a();   // „UI нишката“ изпълнява
        Assert.Equal(1, n);
    }
}
