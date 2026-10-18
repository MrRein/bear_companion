using System;
using System.Collections.Generic;
using System.Linq;

namespace Mecho.Core;

/// <summary>Къде е модулът в живота си.</summary>
public enum ModuleStatus
{
    Added,       // добавен, още не е пуснат
    Running,     // работи
    Skipped,     // не е пуснат: липсва зависимост / кръгова зависимост / грешка в Register
    Faulted,     // спрян: твърде много грешки
    Stopped,     // изключен (Shutdown)
}

/// <summary>Състоянието на един модул (за Developer и тестовете).</summary>
public sealed record ModuleInfo(string Id, ModuleStatus Status, string? Reason, int Faults);

/// <summary>
/// Пуска модулите в правилния ред (зависимостите първи), вика Update и ги пази един
/// от друг:
///  • липсваща или кръгова зависимост → модулът не се пуска (Skipped), другите да;
///  • грешка в Register/Start → модулът се изключва чисто (абонаменти, задачи, слоеве);
///  • FaultLimit грешки за FaultWindow секунди (в Update, в слушател, в задача,
///    в команда) → модулът се изключва (Faulted) и се пуска ModuleFaulted;
///  • модул, добавен след Start, се пуска веднага (ако зависимостите му работят).
/// </summary>
public sealed class ModuleHost
{
    private sealed class Slot
    {
        public required IGameModule Module { get; init; }
        public ModuleStatus Status { get; set; } = ModuleStatus.Added;
        public string? Reason { get; set; }
        public readonly Queue<double> FaultTimes = new();
        public int Faults { get; set; }
    }

    private readonly GameEngine _engine;
    private readonly List<Slot> _slots = new();
    private bool _started;

    internal ModuleHost(GameEngine engine)
    {
        _engine = engine;
        engine.Bus.HandlerFaulted += (owner, _) => Fault(owner, "грешка в слушател/команда");
        engine.Scheduler.JobFaulted += (owner, _) => Fault(owner, "грешка в планирана задача");
    }

    /// <summary>Толкова грешки…</summary>
    public int FaultLimit { get; set; } = 5;

    /// <summary>…за толкова секунди изключват модула.</summary>
    public double FaultWindow { get; set; } = 60;

    /// <summary>Всички модули и състоянието им.</summary>
    public IReadOnlyList<ModuleInfo> Modules => _slots.Select(s => new ModuleInfo(s.Module.Id, s.Status, s.Reason, s.Faults)).ToList();

    public ModuleStatus StatusOf(string id) => Find(id)?.Status ?? throw new KeyNotFoundException($"Няма модул „{id}“.");

    public bool IsRunning(string id) => Find(id)?.Status == ModuleStatus.Running;

    /// <summary>Добавя модул. Две с едно и също Id → InvalidOperationException.</summary>
    public void Add(IGameModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        if (string.IsNullOrWhiteSpace(module.Id)) throw new ArgumentException("Модулът е без Id.", nameof(module));
        if (Find(module.Id) != null) throw new InvalidOperationException($"Модул „{module.Id}“ вече е добавен.");
        var slot = new Slot { Module = module };
        _slots.Add(slot);
        if (_started)
        {
            if (TryRegister(slot)) TryStart(slot);
        }
    }

    /// <summary>Пуска всички добавени модули (по зависимостите).</summary>
    public void Start()
    {
        if (_started) return;
        _started = true;
        foreach (var slot in Order()) TryRegister(slot);
        foreach (var slot in _slots.Where(s => s.Status == ModuleStatus.Running).ToList()) TryStart(slot);
    }

    /// <summary>Всеки цикъл: Update на работещите модули.</summary>
    public void Update(double dt)
    {
        foreach (var slot in _slots)
        {
            if (slot.Status != ModuleStatus.Running) continue;
            try
            {
                slot.Module.Update(dt);
            }
            catch (Exception ex)
            {
                _engine.Log.Error($"Модул „{slot.Module.Id}“ гръмна в Update", ex);
                Fault(slot.Module, "грешка в Update");
            }
        }
    }

    /// <summary>Изключва всички (обратно на реда на пускане).</summary>
    public void Shutdown()
    {
        for (int i = _slots.Count - 1; i >= 0; i--)
            if (_slots[i].Status == ModuleStatus.Running) Stop(_slots[i], ModuleStatus.Stopped, "изход");
    }

    /// <summary>Изключва един модул (Developer, или когато мечокът го иска).</summary>
    public void Disable(string id, string reason)
    {
        if (Find(id) is { Status: ModuleStatus.Running } slot) Stop(slot, ModuleStatus.Stopped, reason);
    }

    // ───────────── Вътрешни ─────────────

    private Slot? Find(string id) => _slots.FirstOrDefault(s => s.Module.Id == id);

    /// <summary>Подрежда по зависимостите; липсващите/кръговите стават Skipped.</summary>
    private List<Slot> Order()
    {
        var result = new List<Slot>();
        var state = new Dictionary<Slot, int>();   // 1 – обхожда се, 2 – готов
        bool Visit(Slot s, Stack<string> path)
        {
            if (state.TryGetValue(s, out int st)) return st == 2 && s.Status != ModuleStatus.Skipped;
            state[s] = 1;
            path.Push(s.Module.Id);
            foreach (var dep in s.Module.DependsOn ?? Array.Empty<string>())
            {
                var d = Find(dep);
                if (d == null)
                {
                    Skip(s, $"липсва зависимост „{dep}“");
                    break;
                }
                if (state.TryGetValue(d, out int ds) && ds == 1)
                {
                    Skip(s, $"кръгова зависимост: {string.Join(" → ", path.Reverse())} → {dep}");
                    break;
                }
                if (!Visit(d, path))
                {
                    Skip(s, $"зависимостта „{dep}“ не е пусната");
                    break;
                }
            }
            path.Pop();
            state[s] = 2;
            if (s.Status != ModuleStatus.Skipped) result.Add(s);
            return s.Status != ModuleStatus.Skipped;
        }
        foreach (var s in _slots) Visit(s, new Stack<string>());
        return result;
    }

    private void Skip(Slot s, string reason)
    {
        if (s.Status == ModuleStatus.Skipped) return;
        s.Status = ModuleStatus.Skipped;
        s.Reason = reason;
        _engine.Log.Warn($"Модул „{s.Module.Id}“ не е пуснат: {reason}.");
    }

    private bool TryRegister(Slot slot)
    {
        // Зависимостите (при късно добавяне) трябва да работят.
        foreach (var dep in slot.Module.DependsOn ?? Array.Empty<string>())
            if (!IsRunning(dep))
            {
                Skip(slot, $"зависимостта „{dep}“ не работи");
                return false;
            }
        try
        {
            slot.Status = ModuleStatus.Running;
            slot.Module.Register(new ModuleContext(slot.Module, _engine));
            return true;
        }
        catch (Exception ex)
        {
            _engine.Log.Error($"Модул „{slot.Module.Id}“ гръмна в Register", ex);
            Cleanup(slot.Module);
            slot.Status = ModuleStatus.Skipped;
            slot.Reason = "грешка в Register: " + ex.Message;
            return false;
        }
    }

    private void TryStart(Slot slot)
    {
        if (slot.Status != ModuleStatus.Running) return;
        try
        {
            slot.Module.Start();
        }
        catch (Exception ex)
        {
            _engine.Log.Error($"Модул „{slot.Module.Id}“ гръмна в Start", ex);
            Stop(slot, ModuleStatus.Faulted, "грешка в Start: " + ex.Message);
        }
    }

    private void Fault(object owner, string what)
    {
        var slot = _slots.FirstOrDefault(s => ReferenceEquals(s.Module, owner));
        if (slot == null || slot.Status != ModuleStatus.Running) return;
        double now = _engine.Clock.Mono;
        slot.Faults++;
        slot.FaultTimes.Enqueue(now);
        while (slot.FaultTimes.Count > 0 && now - slot.FaultTimes.Peek() > FaultWindow) slot.FaultTimes.Dequeue();
        if (slot.FaultTimes.Count >= FaultLimit)
        {
            string reason = $"{slot.FaultTimes.Count} грешки за {FaultWindow:0} с ({what})";
            Stop(slot, ModuleStatus.Faulted, reason);
            _engine.Bus.Publish(new ModuleFaulted(slot.Module.Id, reason));
        }
    }

    private void Stop(Slot slot, ModuleStatus status, string reason)
    {
        slot.Status = status;
        slot.Reason = reason;
        try
        {
            slot.Module.Shutdown();
        }
        catch (Exception ex)
        {
            _engine.Log.Error($"Модул „{slot.Module.Id}“ гръмна в Shutdown", ex);
        }
        Cleanup(slot.Module);
        if (status == ModuleStatus.Faulted) _engine.Log.Error($"Модул „{slot.Module.Id}“ е спрян: {reason}.");
    }

    private void Cleanup(IGameModule module)
    {
        _engine.Bus.Unsubscribe(module);
        _engine.Scheduler.CancelAll(module);
        _engine.Store.Release(module);
    }
}
