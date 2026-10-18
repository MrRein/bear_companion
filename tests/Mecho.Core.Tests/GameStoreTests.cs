using System.Text.Json.Nodes;
using Mecho.Core;
using Xunit;

namespace Mecho.Core.Tests;

public class GameStoreTests
{
    [Fact]
    public void Set_publishes_change_only_when_value_differs()
    {
        var (e, _, _) = Kit.Engine();
        var owner = new object();
        e.Store.Register(owner, "counter", new Counter(0));
        var changes = new List<(int, int)>();
        e.Bus.Subscribe<StateChanged<Counter>>(this, c => changes.Add((c.Old.Value, c.New.Value)));
        e.Store.Update<Counter>(c => c with { Value = 1 });
        e.Store.Update<Counter>(c => c with { Value = 1 });   // същото – без сигнал
        e.Store.Set(new Counter(5));
        Assert.Equal(new[] { (0, 1), (1, 5) }, changes);
        Assert.True(e.Store.IsDirty);
    }

    [Fact]
    public void Unregistered_slice_gives_clear_error()
    {
        var (e, _, _) = Kit.Engine();
        Assert.Throws<StateNotRegisteredException>(() => e.Store.Get<Counter>());
        Assert.Throws<StateNotRegisteredException>(() => e.Store.Set(new Counter(1)));
        Assert.False(e.Store.TryGet<Counter>(out var v));
        Assert.Null(v);
    }

    [Fact]
    public void Duplicate_registration_and_bad_input_throw()
    {
        var (e, _, _) = Kit.Engine();
        e.Store.Register(this, "counter", new Counter(0));
        Assert.Throws<InvalidOperationException>(() => e.Store.Register(this, "counter2", new Counter(0)));
        Assert.Throws<InvalidOperationException>(() => e.Store.Register(this, "counter", new Bag(Array.Empty<string>())));
        Assert.Throws<ArgumentException>(() => e.Store.Register(this, " ", new Bag(Array.Empty<string>())));
        Assert.Throws<ArgumentNullException>(() => e.Store.Set<Counter>(null!));
        Assert.Throws<InvalidOperationException>(() => e.Store.Update<Counter>(_ => null!));
    }

    [Fact]
    public void Export_import_roundtrip()
    {
        var (a, _, _) = Kit.Engine();
        a.Store.Register(this, "counter", new Counter(0));
        a.Store.Register(this, "bag", new Bag(Array.Empty<string>()));
        a.Store.Set(new Counter(7));
        a.Store.Set(new Bag(new[] { "radio", "plant" }));
        var json = a.Store.Export();

        var (b, _, _) = Kit.Engine();
        b.Store.Import(JsonNode.Parse(json.ToJsonString())!.AsObject());   // записът може да е прочетен преди регистрацията
        b.Store.Register(this, "counter", new Counter(0));
        b.Store.Register(this, "bag", new Bag(Array.Empty<string>()));
        Assert.Equal(7, b.Store.Get<Counter>().Value);
        Assert.Equal(new[] { "radio", "plant" }, b.Store.Get<Bag>().Items);
    }

    [Fact]
    public void Old_version_is_migrated()
    {
        var (e, _, _) = Kit.Engine();
        var saved = JsonNode.Parse("""{"schema":1,"slices":{"counter":{"v":1,"data":{"Count":3}}}}""")!.AsObject();
        e.Store.Import(saved);
        e.Store.Register(this, "counter", new Counter(0), version: 2, migrate: (data, from) =>
            from == 1 ? new JsonObject { ["Value"] = data["Count"]!.GetValue<int>() * 10 } : data);
        Assert.Equal(30, e.Store.Get<Counter>().Value);
    }

    [Fact]
    public void Broken_slice_keeps_default_and_others_load()
    {
        var (e, _, log) = Kit.Engine();
        var saved = JsonNode.Parse("""{"schema":1,"slices":{"counter":{"v":1,"data":"не е обект"},"bag":{"v":1,"data":{"Items":["a"]}}}}""")!.AsObject();
        e.Store.Import(saved);
        e.Store.Register(this, "counter", new Counter(4));
        e.Store.Register(this, "bag", new Bag(Array.Empty<string>()));
        Assert.Equal(4, e.Store.Get<Counter>().Value);
        Assert.Equal(new[] { "a" }, e.Store.Get<Bag>().Items);
        Assert.True(log.Errors >= 1);
    }

    [Fact]
    public void Newer_version_and_unknown_slices_survive_a_save()
    {
        var (e, _, _) = Kit.Engine();
        var saved = JsonNode.Parse("""{"schema":1,"slices":{"counter":{"v":9,"data":{"Value":1,"New":true}},"future":{"v":1,"data":{"x":1}}}}""")!.AsObject();
        e.Store.Import(saved);
        e.Store.Register(this, "counter", new Counter(0));
        var json = e.Store.Export();
        Assert.Equal(9, json["slices"]!["counter"]!["v"]!.GetValue<int>());
        Assert.NotNull(json["slices"]!["future"]);
    }

    [Fact]
    public void Released_slice_data_is_kept()
    {
        var (e, _, _) = Kit.Engine();
        var owner = new object();
        e.Store.Register(owner, "counter", new Counter(0));
        e.Store.Set(new Counter(3));
        e.Store.Release(owner);
        Assert.False(e.Store.Has<Counter>());
        e.Store.Register(owner, "counter", new Counter(0));
        Assert.Equal(3, e.Store.Get<Counter>().Value);
    }
}
