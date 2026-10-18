using System;

namespace Mecho.Core;

/// <summary>
/// Сглобката на ядрото – създава се веднъж при пускане на програмата:
/// <code>
/// var engine = new GameEngine(log: myLog, marshal: a => dispatcher.BeginInvoke(a));
/// engine.Modules.Add(new GiftsModule());        // всички механики
/// engine.Modules.Add(new PajamaDayModule());
/// engine.Start();                               // регистрира и пуска модулите
/// // всеки цикъл: engine.Update(dt);   при изход: engine.Shutdown();
/// </code>
/// Изгледът (прозорците) НЕ е модул: той чете Store, слуша сигналите и праща команди.
/// </summary>
public sealed class GameEngine
{
    private string? _lastDay;

    public GameEngine(IClock? clock = null, IRandom? random = null, ICoreLog? log = null, Action<Action>? marshal = null)
    {
        Clock = clock ?? new SystemClock();
        Random = random ?? new SystemRandom();
        Log = log ?? new MemoryLog();
        Bus = new SignalBus(Log, marshal);
        Store = new GameStore(Bus, Log);
        Scheduler = new Scheduler(Clock, Random, Log);
        Modifiers = new ModifierStack(Bus);
        Modules = new ModuleHost(this);
    }

    public IClock Clock { get; }
    public IRandom Random { get; }
    public ICoreLog Log { get; }
    public SignalBus Bus { get; }
    public GameStore Store { get; }
    public Scheduler Scheduler { get; }
    public ModifierStack Modifiers { get; }
    public ModuleHost Modules { get; }

    public bool Started { get; private set; }

    /// <summary>Пуска модулите и обявява деня (DayStarted).</summary>
    public void Start()
    {
        if (Started) return;
        Started = true;
        Modules.Start();
        CheckDay();
    }

    /// <summary>
    /// Един цикъл: нов ден? → DayStarted; планираните задачи; Update на модулите.
    /// Не хвърля: грешките на модулите се хващат от ModuleHost.
    /// </summary>
    public void Update(double dt)
    {
        if (!Started) return;
        if (double.IsNaN(dt) || dt < 0) dt = 0;
        CheckDay();
        Scheduler.Update();
        Modules.Update(dt);
    }

    public void Shutdown()
    {
        if (!Started) return;
        Modules.Shutdown();
        Started = false;
    }

    private void CheckDay()
    {
        string today = Clock.DayKey();
        if (today == _lastDay) return;
        DateTime? previous = _lastDay == null ? null : DateTime.Parse(_lastDay, System.Globalization.CultureInfo.InvariantCulture);
        _lastDay = today;
        Bus.Publish(new DayStarted(Clock.Today(), previous));
    }
}
