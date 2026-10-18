using Mecho.Core;
using Xunit;

namespace Mecho.Core.Tests;

public class ModuleHostTests
{
    private sealed class Probe : GameModule
    {
        private readonly string _id;
        private readonly string[] _deps;
        public readonly List<string> Calls = new();
        public Action<Probe>? OnReg;
        public bool ThrowInUpdate;

        public Probe(string id, params string[] deps)
        {
            _id = id;
            _deps = deps;
        }

        public override string Id => _id;
        public override IReadOnlyCollection<string> DependsOn => _deps;

        protected override void OnRegister()
        {
            Calls.Add("register");
            OnReg?.Invoke(this);
        }

        public override void Start() => Calls.Add("start");

        public override void Update(double dt)
        {
            if (ThrowInUpdate) throw new Exception("boom");
            Calls.Add("update");
        }

        public override void Shutdown() => Calls.Add("shutdown");

        public void Listen() => On<Ping>(_ => Calls.Add("ping"));
        public void Job() => Every("job", 1, 1, () => Calls.Add("job"));
    }

    [Fact]
    public void Dependencies_start_first()
    {
        var (e, _, _) = Kit.Engine();
        var order = new List<string>();
        var a = new Probe("a", "b") { OnReg = _ => order.Add("a") };
        var b = new Probe("b") { OnReg = _ => order.Add("b") };
        e.Modules.Add(a);
        e.Modules.Add(b);
        e.Start();
        Assert.Equal(new[] { "b", "a" }, order);
        Assert.Equal(ModuleStatus.Running, e.Modules.StatusOf("a"));
    }

    [Fact]
    public void Missing_or_circular_dependency_skips_only_that_module()
    {
        var (e, _, _) = Kit.Engine();
        e.Modules.Add(new Probe("lonely", "ghost"));
        e.Modules.Add(new Probe("x", "y"));
        e.Modules.Add(new Probe("y", "x"));
        e.Modules.Add(new Probe("fine"));
        e.Start();
        Assert.Equal(ModuleStatus.Skipped, e.Modules.StatusOf("lonely"));
        Assert.Equal(ModuleStatus.Skipped, e.Modules.StatusOf("x"));
        Assert.Equal(ModuleStatus.Running, e.Modules.StatusOf("fine"));
    }

    [Fact]
    public void Duplicate_id_throws()
    {
        var (e, _, _) = Kit.Engine();
        e.Modules.Add(new Probe("a"));
        Assert.Throws<InvalidOperationException>(() => e.Modules.Add(new Probe("a")));
    }

    [Fact]
    public void Faulty_module_is_disabled_and_cleaned_up()
    {
        var (e, _, _) = Kit.Engine();
        var bad = new Probe("bad");
        bad.OnReg = p => p.Listen();
        e.Modules.Add(bad);
        e.Modules.Add(new Probe("good"));
        string? faulted = null;
        e.Bus.Subscribe<ModuleFaulted>(this, f => faulted = f.ModuleId);
        e.Start();
        bad.ThrowInUpdate = true;
        for (int i = 0; i < 10; i++) e.Update(0.1);
        Assert.Equal(ModuleStatus.Faulted, e.Modules.StatusOf("bad"));
        Assert.Equal("bad", faulted);
        Assert.Equal(ModuleStatus.Running, e.Modules.StatusOf("good"));
        Assert.Equal(0, e.Bus.ListenerCount<Ping>());   // абонаментите му са махнати
    }

    [Fact]
    public void Faults_in_listeners_count_too()
    {
        var (e, _, _) = Kit.Engine();
        var bad = new Probe("bad");
        bad.OnReg = p => p.Ctx2().Bus.Subscribe<Ping>(p, _ => throw new Exception("boom"));
        e.Modules.Add(bad);
        e.Start();
        for (int i = 0; i < 5; i++) e.Bus.Publish(new Ping(i));
        Assert.Equal(ModuleStatus.Faulted, e.Modules.StatusOf("bad"));
    }

    [Fact]
    public void Register_exception_skips_module_cleanly()
    {
        var (e, _, _) = Kit.Engine();
        var bad = new Probe("bad") { OnReg = p => { p.Listen(); throw new Exception("boom"); } };
        e.Modules.Add(bad);
        e.Start();
        Assert.Equal(ModuleStatus.Skipped, e.Modules.StatusOf("bad"));
        Assert.Equal(0, e.Bus.ListenerCount<Ping>());
    }

    [Fact]
    public void Late_added_module_starts_immediately()
    {
        var (e, _, _) = Kit.Engine();
        e.Start();
        var late = new Probe("late");
        e.Modules.Add(late);
        Assert.Equal(new[] { "register", "start" }, late.Calls);
    }

    [Fact]
    public void Shutdown_cancels_jobs_and_subscriptions()
    {
        var (e, clock, _) = Kit.Engine();
        var m = new Probe("m");
        m.OnReg = p => { p.Listen(); p.Job(); };
        e.Modules.Add(m);
        e.Start();
        e.Modules.Disable("m", "тест");
        clock.Advance(5);
        e.Update(0.1);
        e.Bus.Publish(new Ping(1));
        Assert.DoesNotContain("job", m.Calls);
        Assert.DoesNotContain("ping", m.Calls);
        Assert.Contains("shutdown", m.Calls);
    }
}

internal static class ProbeExt
{
    // Достъп до Ctx от теста (защитено в GameModule).
    public static ModuleContext Ctx2(this GameModule m) =>
        (ModuleContext)typeof(GameModule).GetProperty("Ctx", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(m)!;
}
