using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Mecho.Core;

/// <summary>
/// Един „слой“ от състоянието на играта (нужди, портфейл, растение…). Пише се като
/// неизменим record – промяна = нов запис (store.Update(s => s with { … })), така
/// старото и новото се виждат в StateChanged и никой не мени данните „под масата“.
/// <code>public sealed record PlantState(int Stage, bool Placed, double X) : IStateSlice;</code>
/// Списъци вътре – IReadOnlyList / ImmutableList (не List, който може да се мени тайно).
/// </summary>
public interface IStateSlice
{
}

/// <summary>Слой от състоянието се смени (Old → New). Слушай го, за да пречертаеш/реагираш.</summary>
public sealed record StateChanged<T>(T Old, T New) : ISignal where T : class, IStateSlice;

/// <summary>Слой, който никой не е регистрирал (вероятно модулът му не е пуснат).</summary>
public sealed class StateNotRegisteredException : InvalidOperationException
{
    public StateNotRegisteredException(Type type)
        : base($"Слоят {type.Name} не е регистриран. Регистрира го модулът, който го притежава (ctx.Store.Register в Register).") { }
}

/// <summary>
/// ЕДИНСТВЕНИЯТ източник на истината за данните на играта. Всеки слой има точно
/// един собственик (модулът, който го регистрира); другите го четат (Get) или
/// слушат промените му (StateChanged&lt;T&gt;). Всяка промяна:
///   1) заменя слоя с нов запис (неизменим),
///   2) пуска StateChanged&lt;T&gt;(старо, ново) по шината,
///   3) маркира състоянието за запис (IsDirty).
/// Еднаква стойност (record равенство) не пуска сигнал.
///
/// Запис: Export() → JSON { "schema": 1, "slices": { ключ: { "v": версия, "data": … } } }.
/// Import() чете обратно; по-стара версия на слой минава през migrate; непознати
/// слоеве (от по-нова версия на програмата) се пазят и записват обратно непокътнати.
///
/// Пример (в модул):
/// <code>
/// ctx.Store.Register("plant", new PlantState(0, false, double.NaN), version: 1);
/// var p = ctx.Store.Get&lt;PlantState&gt;();
/// ctx.Store.Update&lt;PlantState&gt;(p => p with { Stage = p.Stage + 1 });
/// </code>
/// </summary>
public sealed class GameStore
{
    public const int Schema = 1;

    private sealed class Entry
    {
        public required string Key { get; init; }
        public required Type Type { get; init; }
        public required int Version { get; init; }
        public required object Value { get; set; }
        public required object Owner { get; init; }
        public Func<JsonNode, int, JsonNode>? Migrate { get; init; }
        public required Action<object, object> Announce { get; init; }
    }

    private readonly SignalBus _bus;
    private readonly ICoreLog _log;
    private readonly Dictionary<Type, Entry> _byType = new();
    private readonly Dictionary<string, Entry> _byKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, JsonNode> _pending = new(StringComparer.Ordinal);   // прочетени, но още нерегистрирани
    private readonly Dictionary<string, JsonNode> _unknown = new(StringComparer.Ordinal);   // от по-нова версия – пазят се

    internal static readonly JsonSerializerOptions Json = new()
    {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new JsonStringEnumConverter() },
    };

    public GameStore(SignalBus bus, ICoreLog log)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>Има промени след последния запис.</summary>
    public bool IsDirty { get; private set; }

    /// <summary>Ключовете на регистрираните слоеве.</summary>
    public IEnumerable<string> Keys => _byKey.Keys;

    /// <summary>
    /// Регистрира слой (вика се от модула-собственик в Register). key – уникално име в
    /// записа (малки латински букви, точки: „plant“, „needs“); version – вдига се,
    /// когато формата на слоя се смени, и тогава migrate(старJSON, стараВерсия) го преправя.
    /// Ако вече има прочетени данни за ключа (Import преди регистрацията) – взимат се те.
    /// </summary>
    public void Register<T>(object owner, string key, T initial, int version = 1, Func<JsonNode, int, JsonNode>? migrate = null)
        where T : class, IStateSlice
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(initial);
        if (string.IsNullOrWhiteSpace(key) || key.Trim() != key)
            throw new ArgumentException("Ключът на слоя е празен или с интервали.", nameof(key));
        if (version < 1) throw new ArgumentOutOfRangeException(nameof(version), "Версията почва от 1.");
        if (_byType.ContainsKey(typeof(T)))
            throw new InvalidOperationException($"Слоят {typeof(T).Name} вече е регистриран.");
        if (_byKey.TryGetValue(key, out var clash))
            throw new InvalidOperationException($"Ключът „{key}“ вече е на слоя {clash.Type.Name}.");

        var entry = new Entry
        {
            Key = key,
            Type = typeof(T),
            Version = version,
            Value = initial,
            Owner = owner,
            Migrate = migrate,
            Announce = (o, n) => _bus.Publish(new StateChanged<T>((T)o, (T)n)),
        };
        _byType[typeof(T)] = entry;
        _byKey[key] = entry;
        if (_pending.Remove(key, out var saved)) Load(entry, saved);
    }

    /// <summary>Регистриран ли е слоят.</summary>
    public bool Has<T>() where T : class, IStateSlice => _byType.ContainsKey(typeof(T));

    /// <summary>Текущият слой. Нерегистриран → StateNotRegisteredException (ясна грешка, не null).</summary>
    public T Get<T>() where T : class, IStateSlice =>
        _byType.TryGetValue(typeof(T), out var e) ? (T)e.Value : throw new StateNotRegisteredException(typeof(T));

    /// <summary>Слоят, ако го има (за модули с незадължителна зависимост).</summary>
    public bool TryGet<T>(out T? value) where T : class, IStateSlice
    {
        if (_byType.TryGetValue(typeof(T), out var e))
        {
            value = (T)e.Value;
            return true;
        }
        value = null;
        return false;
    }

    /// <summary>Заменя слоя. Същата стойност – нищо. Иначе: StateChanged&lt;T&gt; и IsDirty.</summary>
    public T Set<T>(T next) where T : class, IStateSlice
    {
        ArgumentNullException.ThrowIfNull(next);
        if (!_byType.TryGetValue(typeof(T), out var e)) throw new StateNotRegisteredException(typeof(T));
        var old = (T)e.Value;
        if (Equals(old, next)) return old;
        e.Value = next;
        IsDirty = true;
        e.Announce(old, next);
        return next;
    }

    /// <summary>Промяна чрез функция от старото (store.Update&lt;Wallet&gt;(w => w with { Nuts = w.Nuts + 5 })).</summary>
    public T Update<T>(Func<T, T> change) where T : class, IStateSlice
    {
        ArgumentNullException.ThrowIfNull(change);
        var next = change(Get<T>()) ?? throw new InvalidOperationException($"Промяната на {typeof(T).Name} върна null.");
        return Set(next);
    }

    /// <summary>Записът е направен.</summary>
    public void MarkSaved() => IsDirty = false;

    /// <summary>Маха слоевете на собственика (модулът е изключен); данните им остават за запис.</summary>
    public void Release(object owner)
    {
        var mine = new List<Entry>();
        foreach (var e in _byKey.Values)
            if (ReferenceEquals(e.Owner, owner)) mine.Add(e);
        foreach (var e in mine)
        {
            _byKey.Remove(e.Key);
            _byType.Remove(e.Type);
            // Данните не се губят: при запис излизат както са, при нова регистрация се връщат.
            _pending[e.Key] = Serialize(e);
        }
    }

    // ───────────── Запис ─────────────

    /// <summary>Цялото състояние като JSON (за файла със записа).</summary>
    public JsonObject Export()
    {
        var slices = new JsonObject();
        foreach (var (key, node) in _unknown) slices[key] = node.DeepClone();
        foreach (var (key, node) in _pending) slices[key] = node.DeepClone();
        // Слой от по-нова версия на програмата остава какъвто е бил (иначе тази, по-стара
        // версия би го презаписала с по-малко данни).
        foreach (var e in _byKey.Values)
            if (!_unknown.ContainsKey(e.Key)) slices[e.Key] = Serialize(e);
        return new JsonObject { ["schema"] = Schema, ["slices"] = slices };
    }

    /// <summary>
    /// Чете записа. Слой, който не може да се прочете, остава с началната си стойност
    /// (и грешка в лога) – един повреден слой не губи останалите.
    /// </summary>
    public void Import(JsonObject root)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (root["slices"] is not JsonObject slices)
        {
            _log.Warn("Записът на състоянието е без „slices“ – почва се отначало.");
            return;
        }
        foreach (var (key, node) in slices)
        {
            if (node == null) continue;
            if (_byKey.TryGetValue(key, out var e)) Load(e, node.DeepClone());
            else _pending[key] = node.DeepClone();   // модулът ще се регистрира по-късно (или е от по-нова версия)
        }
        IsDirty = false;
    }

    /// <summary>Непознати слоеве, които никой модул не е взел (за диагностика).</summary>
    public IEnumerable<string> UnclaimedKeys => _pending.Keys;

    private JsonNode Serialize(Entry e) => new JsonObject
    {
        ["v"] = e.Version,
        ["data"] = JsonSerializer.SerializeToNode(e.Value, e.Type, Json),
    };

    private void Load(Entry e, JsonNode saved)
    {
        try
        {
            int version = saved["v"]?.GetValue<int>() ?? 1;
            var data = saved["data"] ?? throw new JsonException("няма „data“");
            if (version > e.Version)
            {
                // Записано от по-нова версия на програмата: не го пипаме (иначе ще загубим нещо).
                _log.Warn($"Слоят „{e.Key}“ е от по-нова версия ({version} > {e.Version}) – оставен непокътнат.");
                _unknown[e.Key] = saved;
                return;
            }
            if (version < e.Version)
            {
                if (e.Migrate == null) throw new JsonException($"версия {version} → {e.Version} без migrate");
                data = e.Migrate(data, version) ?? throw new JsonException("migrate върна null");
            }
            var value = data.Deserialize(e.Type, Json) ?? throw new JsonException("празни данни");
            e.Value = value;
        }
        catch (Exception ex)
        {
            _log.Error($"Слоят „{e.Key}“ не се прочете – остава началната стойност", ex);
            _unknown.Remove(e.Key);
        }
    }
}
