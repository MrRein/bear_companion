using System;
using System.Collections.Generic;
using System.Threading;

namespace Mecho.Core;

/// <summary>
/// Сигнал: нещо, което ВЕЧЕ се е случило („ситостта се смени“, „купен е предмет“).
/// Пише се като неизменим record:
/// <code>public sealed record ItemAcquired(string ItemId, string Source) : ISignal;</code>
/// Може да има колкото искаш слушатели (или нито един).
/// </summary>
public interface ISignal
{
}

/// <summary>
/// Команда: намерение („сготви супа“, „сложи радиото“). Има точно един обработчик,
/// който може да я откаже (CommandResult.Rejected). Пише се като record:
/// <code>public sealed record Cook(string FoodId) : IGameCommand;</code>
/// (Не се казва ICommand, за да не се бърка с WPF.)
/// </summary>
public interface IGameCommand
{
}

/// <summary>Какво стана с командата: изпълнена или отказана (и защо – за репликата/лога).</summary>
public readonly record struct CommandResult(bool Ok, string? Reason)
{
    public static CommandResult Done => new(true, null);

    public static CommandResult Rejected(string reason) => new(false, reason);

    public override string ToString() => Ok ? "ok" : $"отказана: {Reason}";
}

/// <summary>Абонамент: Dispose() го маха. (Модулите обикновено не го пазят – ModuleHost маха всичко на модула наведнъж.)</summary>
public sealed class Subscription : IDisposable
{
    private Action? _remove;

    internal Subscription(Action remove) => _remove = remove;

    public void Dispose()
    {
        _remove?.Invoke();
        _remove = null;
    }
}

/// <summary>Един изпратен сигнал (за Developer панела и тестовете).</summary>
public readonly record struct SignalRecord(DateTime At, string Name, ISignal Signal);

/// <summary>
/// Шината: единственият начин модулите да говорят помежду си. Модул А не знае за
/// модул Б – А пуска сигнал (Publish) или праща команда (Send), Б е абониран.
///
/// Правила (строги, за да е предвидимо):
///  • Всичко е на една нишка (UI). Сигнал от друга нишка се препраща през marshal
///    (ако няма такъв – грешка, а не тиха гонка).
///  • Publish по време на друг Publish НЕ се изпълнява веднага, а след текущия
///    (опашка) – няма рекурсия и редът е ясен: първо всички слушатели на А, после Б.
///  • Грешка в един слушател се записва в лога, вдига HandlerFaulted (ModuleHost
///    брои грешките на модула) и не спира останалите слушатели.
///  • Безкраен цикъл от сигнали (А пуска Б, Б пуска А…) се спира след MaxPerDrain.
///  • null сигнал/команда/обработчик/собственик → ArgumentNullException веднага.
///  • Втори обработчик за една и съща команда → InvalidOperationException
///    (два модула не бива да „притежават“ едно действие).
///
/// Пример (в модул – виж GameModule за по-кратките On/Emit/Handle):
/// <code>
/// bus.Subscribe&lt;NeedsChanged&gt;(owner: this, s => { if (s.Fullness &lt; 20) ... });
/// bus.Publish(new NeedsChanged(oldF, newF, oldE, newE));
/// bus.Handle&lt;Cook&gt;(owner: this, c => kitchen.CanCook(c.FoodId) ? Start(c) : CommandResult.Rejected("няма уред"));
/// var result = bus.Send(new Cook("soup"));
/// </code>
/// </summary>
public sealed class SignalBus
{
    private sealed class Listener
    {
        public required Type Type { get; init; }
        public required object Owner { get; init; }
        public required Action<ISignal> Invoke { get; init; }
        public int Order { get; init; }
        public long Seq { get; init; }
        public bool Removed { get; set; }
    }

    private sealed record CommandHandler(object Owner, Func<IGameCommand, CommandResult> Run);

    private readonly Dictionary<Type, List<Listener>> _listeners = new();
    private readonly List<Listener> _all = new();
    private readonly Dictionary<Type, CommandHandler> _commands = new();
    private readonly Queue<ISignal> _queue = new();
    private readonly Queue<SignalRecord> _recent = new();
    private readonly ICoreLog _log;
    private readonly Action<Action>? _marshal;
    private readonly int _threadId = Environment.CurrentManagedThreadId;
    private bool _draining;
    private long _seq;

    /// <param name="log">Къде да пише грешките.</param>
    /// <param name="marshal">Как да се върне на нишката на шината (в приложението: Dispatcher.BeginInvoke).</param>
    public SignalBus(ICoreLog log, Action<Action>? marshal = null)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _marshal = marshal;
    }

    /// <summary>Най-много толкова сигнала в едно изпразване на опашката (предпазва от безкраен цикъл).</summary>
    public int MaxPerDrain { get; set; } = 10_000;

    /// <summary>Колко последни сигнала да помни (Developer → „Сигнали на живо“).</summary>
    public int RecentCapacity { get; set; } = 200;

    /// <summary>Слушател или обработчик на команда хвърли грешка: (собственик, грешка).</summary>
    public event Action<object, Exception>? HandlerFaulted;

    /// <summary>Последните изпратени сигнали (най-новият е последен).</summary>
    public IReadOnlyList<SignalRecord> Recent => _recent.ToArray();

    /// <summary>Колко сигнала са изпратени общо (тестове/диагностика).</summary>
    public long PublishedCount { get; private set; }

    // ───────────── Сигнали ─────────────

    /// <summary>
    /// Абонира owner за сигнал от тип T (точно този тип). order: по-малкото е по-рано
    /// (при равни – по реда на абониране).
    /// </summary>
    public Subscription Subscribe<T>(object owner, Action<T> handler, int order = 0) where T : class, ISignal
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(handler);
        CheckThread();
        var l = new Listener { Type = typeof(T), Owner = owner, Order = order, Seq = ++_seq, Invoke = s => handler((T)s) };
        if (!_listeners.TryGetValue(typeof(T), out var list)) _listeners[typeof(T)] = list = new List<Listener>();
        list.Add(l);
        list.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : a.Seq.CompareTo(b.Seq));
        return new Subscription(() => Remove(l));
    }

    /// <summary>Слуша ВСИЧКИ сигнали (само за диагностика – лог, Developer панел, тестове).</summary>
    public Subscription SubscribeAll(object owner, Action<ISignal> handler)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(handler);
        CheckThread();
        var l = new Listener { Type = typeof(ISignal), Owner = owner, Seq = ++_seq, Invoke = handler };
        _all.Add(l);
        return new Subscription(() => Remove(l));
    }

    /// <summary>Пуска сигнал до всички абонати (след текущия сигнал, ако в момента се разпраща друг).</summary>
    public void Publish<T>(T signal) where T : class, ISignal
    {
        ArgumentNullException.ThrowIfNull(signal);
        if (Environment.CurrentManagedThreadId != _threadId)
        {
            if (_marshal == null) throw new InvalidOperationException($"Сигнал {typeof(T).Name} от друга нишка, а шината няма marshal.");
            _marshal(() => Publish(signal));
            return;
        }
        _queue.Enqueue(signal);
        if (_draining) return;
        Drain();
    }

    private void Drain()
    {
        _draining = true;
        int count = 0;
        try
        {
            while (_queue.Count > 0)
            {
                if (++count > MaxPerDrain)
                {
                    _log.Error($"Шината: над {MaxPerDrain} сигнала наведнъж – вероятно безкраен цикъл. Изпуснати: {_queue.Count}.");
                    _queue.Clear();
                    break;
                }
                var signal = _queue.Dequeue();
                PublishedCount++;
                Remember(signal);
                if (_listeners.TryGetValue(signal.GetType(), out var list)) Dispatch(list.ToArray(), signal);
                if (_all.Count > 0) Dispatch(_all.ToArray(), signal);
            }
        }
        finally
        {
            _draining = false;
        }
    }

    private void Dispatch(Listener[] listeners, ISignal signal)
    {
        foreach (var l in listeners)
        {
            if (l.Removed) continue;   // отписан от предишен слушател на същия сигнал
            try
            {
                l.Invoke(signal);
            }
            catch (Exception ex)
            {
                _log.Error($"Слушател на {signal.GetType().Name} ({OwnerName(l.Owner)}) гръмна", ex);
                RaiseFault(l.Owner, ex);
            }
        }
    }

    private void Remember(ISignal signal)
    {
        if (RecentCapacity <= 0) return;
        _recent.Enqueue(new SignalRecord(DateTime.Now, NameOf(signal.GetType()), signal));
        while (_recent.Count > RecentCapacity) _recent.Dequeue();
    }

    // ───────────── Команди ─────────────

    /// <summary>
    /// owner става единственият обработчик на команда T. Втори опит (от друг или
    /// същия собственик) хвърля – така два модула не могат да си „откраднат“ действие.
    /// </summary>
    public Subscription Handle<T>(object owner, Func<T, CommandResult> handler) where T : class, IGameCommand
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(handler);
        CheckThread();
        if (_commands.TryGetValue(typeof(T), out var existing))
            throw new InvalidOperationException($"Команда {typeof(T).Name} вече се обработва от {OwnerName(existing.Owner)}.");
        var h = new CommandHandler(owner, c => handler((T)c));
        _commands[typeof(T)] = h;
        return new Subscription(() =>
        {
            if (_commands.TryGetValue(typeof(T), out var cur) && ReferenceEquals(cur, h)) _commands.Remove(typeof(T));
        });
    }

    /// <summary>Има ли кой да изпълни командата T.</summary>
    public bool CanHandle<T>() where T : class, IGameCommand => _commands.ContainsKey(typeof(T));

    /// <summary>
    /// Изпълнява командата веднага и връща резултата. Няма обработчик → Rejected
    /// (и предупреждение в лога), а не изключение: липсващ/изключен модул не бива
    /// да чупи бутона, който го вика.
    /// </summary>
    public CommandResult Send<T>(T command) where T : class, IGameCommand
    {
        ArgumentNullException.ThrowIfNull(command);
        CheckThread();
        if (!_commands.TryGetValue(command.GetType(), out var h))
        {
            _log.Warn($"Команда {command.GetType().Name} няма обработчик.");
            return CommandResult.Rejected("няма кой да я изпълни");
        }
        try
        {
            return h.Run(command);
        }
        catch (Exception ex)
        {
            _log.Error($"Команда {command.GetType().Name} ({OwnerName(h.Owner)}) гръмна", ex);
            RaiseFault(h.Owner, ex);
            return CommandResult.Rejected("грешка");
        }
    }

    // ───────────── Отписване ─────────────

    /// <summary>Маха всички абонаменти и команди на собственика (модул се изключва).</summary>
    public void Unsubscribe(object owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        foreach (var list in _listeners.Values)
            foreach (var l in list)
                if (ReferenceEquals(l.Owner, owner)) l.Removed = true;
        foreach (var list in _listeners.Values) list.RemoveAll(l => l.Removed);
        foreach (var l in _all)
            if (ReferenceEquals(l.Owner, owner)) l.Removed = true;
        _all.RemoveAll(l => l.Removed);
        var mine = new List<Type>();
        foreach (var (type, h) in _commands)
            if (ReferenceEquals(h.Owner, owner)) mine.Add(type);
        foreach (var t in mine) _commands.Remove(t);
    }

    /// <summary>Колко слушатели има сигнал T (тестове/диагностика).</summary>
    public int ListenerCount<T>() where T : class, ISignal =>
        _listeners.TryGetValue(typeof(T), out var list) ? list.Count : 0;

    private void Remove(Listener l)
    {
        l.Removed = true;
        if (_listeners.TryGetValue(l.Type, out var list)) list.Remove(l);
        _all.Remove(l);
    }

    private void RaiseFault(object owner, Exception ex)
    {
        try { HandlerFaulted?.Invoke(owner, ex); }
        catch (Exception inner) { _log.Error("HandlerFaulted гръмна", inner); }
    }

    private void CheckThread()
    {
        if (Environment.CurrentManagedThreadId != _threadId)
            throw new InvalidOperationException("Шината се ползва само от своята нишка (абонирай се/прати команда от UI нишката).");
    }

    internal static string OwnerName(object owner) => owner is IGameModule m ? m.Id : owner.GetType().Name;

    /// <summary>Четимо име на тип (StateChanged&lt;NeedsState&gt;, а не StateChanged`1).</summary>
    public static string NameOf(Type t)
    {
        if (!t.IsGenericType) return t.Name;
        string name = t.Name[..t.Name.IndexOf('`')];
        return $"{name}<{string.Join(",", Array.ConvertAll(t.GetGenericArguments(), NameOf))}>";
    }
}
