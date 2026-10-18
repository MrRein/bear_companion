using System;
using System.Collections.Generic;

namespace Mecho.Core;

/// <summary>
/// Модул = една механика (подаръци, растение, готвене, пижамен ден…). Включва се в
/// ядрото с engine.Modules.Add(new XModule()) и говори със света САМО през ctx:
/// шината (сигнали/команди), хранилището (своя слой + четене на чужди), планировчика,
/// модификаторите. Модулите не се реферират помежду си – затова нов модул не може да
/// счупи стар, а изключен модул (грешки, липсваща зависимост) не спира другите.
///
/// Жизнен цикъл (вика го ModuleHost):
///   Register(ctx) – регистрира слоя си в хранилището, абонаменти, команди, задачи.
///                   Тук НЕ се пращат команди към други модули (те може още да не са готови).
///   Start()       – всички са регистрирани: може да се говори с другите.
///   Update(dt)    – всеки цикъл (dt – секунди). Повечето модули не го ползват (планировчик).
///   Shutdown()    – изход/изключване. Абонаменти и задачи се махат автоматично.
/// </summary>
public interface IGameModule
{
    /// <summary>Уникално име: „gifts“, „garden“, „event.sick_day“.</summary>
    string Id { get; }

    /// <summary>Id-та на модули, без които този не може (пуска се след тях; липсва ли някой – не се пуска).</summary>
    IReadOnlyCollection<string> DependsOn { get; }

    void Register(ModuleContext ctx);

    void Start();

    void Update(double dt);

    void Shutdown();
}

/// <summary>Всичко, което модулът получава от ядрото. Собственик на абонаментите/задачите е модулът.</summary>
public sealed class ModuleContext
{
    internal ModuleContext(IGameModule owner, GameEngine engine)
    {
        Owner = owner;
        Bus = engine.Bus;
        Store = engine.Store;
        Scheduler = engine.Scheduler;
        Modifiers = engine.Modifiers;
        Clock = engine.Clock;
        Random = engine.Random;
        Log = engine.Log;
    }

    public IGameModule Owner { get; }
    public SignalBus Bus { get; }
    public GameStore Store { get; }
    public Scheduler Scheduler { get; }
    public ModifierStack Modifiers { get; }
    public IClock Clock { get; }
    public IRandom Random { get; }
    public ICoreLog Log { get; }
}

/// <summary>
/// Основа за модули с кратки помощници (On, Emit, Handle, Send, State, Every, After).
/// Нов модул – минимум:
/// <code>
/// public sealed class PajamaDayModule : GameModule
/// {
///     public override string Id => "event.pajama_day";
///     protected override void OnRegister()
///     {
///         On&lt;DayStarted&gt;(d => { if (Random.Chance(0.1)) Begin(); });
///     }
/// }
/// // и при старта: engine.Modules.Add(new PajamaDayModule());
/// </code>
/// </summary>
public abstract class GameModule : IGameModule
{
    private ModuleContext? _ctx;

    public abstract string Id { get; }

    public virtual IReadOnlyCollection<string> DependsOn => Array.Empty<string>();

    /// <summary>Контекстът (достъпен след Register; преди това – ясна грешка).</summary>
    protected ModuleContext Ctx => _ctx ?? throw new InvalidOperationException($"Модулът {Id} още не е регистриран.");

    protected SignalBus Bus => Ctx.Bus;
    protected GameStore Store => Ctx.Store;
    protected IClock Clock => Ctx.Clock;
    protected IRandom Random => Ctx.Random;
    protected ICoreLog Log => Ctx.Log;
    protected ModifierStack Modifiers => Ctx.Modifiers;

    public void Register(ModuleContext ctx)
    {
        _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        OnRegister();
    }

    public virtual void Start() { }

    public virtual void Update(double dt) { }

    public virtual void Shutdown() { }

    /// <summary>Абонаменти, слоеве, команди, задачи.</summary>
    protected abstract void OnRegister();

    protected Subscription On<T>(Action<T> handler, int order = 0) where T : class, ISignal => Bus.Subscribe(this, handler, order);

    protected void Emit<T>(T signal) where T : class, ISignal => Bus.Publish(signal);

    protected Subscription Handle<T>(Func<T, CommandResult> handler) where T : class, IGameCommand => Bus.Handle(this, handler);

    protected CommandResult Send<T>(T command) where T : class, IGameCommand => Bus.Send(command);

    protected T State<T>() where T : class, IStateSlice => Store.Get<T>();

    protected void Every(string name, double minSeconds, double maxSeconds, Action action, Func<bool>? when = null) =>
        Ctx.Scheduler.Every(this, name, minSeconds, maxSeconds, action, when);

    protected void After(string name, double seconds, Action action) => Ctx.Scheduler.After(this, name, seconds, action);
}
