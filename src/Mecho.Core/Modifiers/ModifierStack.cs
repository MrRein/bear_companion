using System;
using System.Collections.Generic;
using System.Linq;

namespace Mecho.Core;

/// <summary>Име на модификатор. Подтиповете казват как се събират стойностите от няколко източника.</summary>
public abstract class ModifierKey
{
    protected ModifierKey(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Модификаторът иска име.", nameof(name));
        Name = name;
    }

    public string Name { get; }

    public override string ToString() => Name;
}

/// <summary>Множител: стойностите се умножават (болен ×1.5 и вечер ×2 = ×3). Без източници – 1.</summary>
public sealed class Multiplier : ModifierKey
{
    public Multiplier(string name) : base(name) { }
}

/// <summary>Флаг: вдигнат е, ако поне един източник го вдига. Без източници – false.</summary>
public sealed class Flag : ModifierKey
{
    public Flag(string name) : base(name) { }
}

/// <summary>Набор от етикети: обединение от всички източници (забранени занимания, продукти само днес…).</summary>
public sealed class TagSet : ModifierKey
{
    public TagSet(string name) : base(name) { }
}

/// <summary>
/// Известните модификатори – тук, за да ги виждат и събитията, и модулите. Модулите
/// НЕ проверяват „болен ли е“, а питат модификатора (Needs: Get(Mods.EnergyDrain)),
/// затова ново събитие не пипа кода на модулите. Нов модификатор = един ред тук и
/// модулът, който го чете.
/// </summary>
public static class Mods
{
    public static readonly Multiplier EnergyDrain = new("needs.energy_drain");
    public static readonly Multiplier HungerRate = new("needs.hunger_rate");
    public static readonly Multiplier Appetite = new("needs.appetite");
    public static readonly Multiplier GiftChance = new("gifts.chance");
    public static readonly TagSet ShopExtras = new("shop.extras");             // продукти само докато трае източникът
    public static readonly TagSet BlockedHobbies = new("hobbies.blocked");
    public static readonly TagSet PreferredHobbies = new("hobbies.preferred");
    public static readonly TagSet WantedOutfit = new("outfit.wanted");         // „пижамен ден“: pajama
    public static readonly TagSet LinePacks = new("lines.packs");              // допълнителни реплики: event.sick…
    public static readonly TagSet Decor = new("view.decor");                   // елха, сняг…
    public static readonly Flag NoWork = new("work.none");
}

/// <summary>
/// Сборът от модификатори от всички източници (активни събития, дрехи, време…).
/// Всеки източник (например събитието „sick_day“) слага своите стойности и ги маха
/// наведнъж с Clear(източник). Всяка промяна пуска ModifiersChanged.
///
/// Пример:
/// <code>
/// mods.Set("sick_day", Mods.EnergyDrain, 1.5);
/// mods.Add("sick_day", Mods.ShopExtras, "medicine", "honey_tea");
/// double drain = mods.Get(Mods.EnergyDrain);          // 1.5 (× всички други)
/// bool canBuy = mods.Get(Mods.ShopExtras).Contains("medicine");
/// mods.Clear("sick_day");                              // събитието свърши
/// </code>
/// </summary>
public sealed class ModifierStack
{
    private readonly Dictionary<string, Dictionary<ModifierKey, object>> _bySource = new(StringComparer.Ordinal);
    private readonly SignalBus? _bus;

    public ModifierStack(SignalBus? bus = null) => _bus = bus;

    public void Set(string source, Multiplier key, double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), $"Множителят {key} трябва да е ≥ 0.");
        Put(source, key, value);
    }

    public void Set(string source, Flag key, bool on) => Put(source, key, on);

    public void Add(string source, TagSet key, params string[] tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        var set = Source(source).TryGetValue(key, out var old) ? new HashSet<string>((HashSet<string>)old, StringComparer.Ordinal) : new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in tags)
            if (!string.IsNullOrWhiteSpace(t)) set.Add(t);
        Put(source, key, set);
    }

    /// <summary>Маха всичко от източника.</summary>
    public void Clear(string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        if (_bySource.Remove(source)) Changed(source);
    }

    /// <summary>Произведението от всички източници (1, ако няма).</summary>
    public double Get(Multiplier key)
    {
        double v = 1;
        foreach (var s in _bySource.Values)
            if (s.TryGetValue(key, out var x)) v *= (double)x;
        return v;
    }

    public bool Get(Flag key) => _bySource.Values.Any(s => s.TryGetValue(key, out var x) && (bool)x);

    public IReadOnlySet<string> Get(TagSet key)
    {
        var all = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in _bySource.Values)
            if (s.TryGetValue(key, out var x)) all.UnionWith((HashSet<string>)x);
        return all;
    }

    /// <summary>Кои източници има (Developer: „какво влияе сега“).</summary>
    public IEnumerable<string> Sources => _bySource.Keys;

    /// <summary>Описание за Developer: източник → модификатори.</summary>
    public string Describe() => _bySource.Count == 0 ? "(няма)" : string.Join("\n", _bySource.Select(s =>
        $"{s.Key}: " + string.Join(", ", s.Value.Select(kv => kv.Value is HashSet<string> h ? $"{kv.Key}=[{string.Join(",", h)}]" : $"{kv.Key}={kv.Value}"))));

    private Dictionary<ModifierKey, object> Source(string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        if (!_bySource.TryGetValue(source, out var d)) _bySource[source] = d = new Dictionary<ModifierKey, object>();
        return d;
    }

    private void Put(string source, ModifierKey key, object value)
    {
        ArgumentNullException.ThrowIfNull(key);
        Source(source)[key] = value;
        Changed(source);
    }

    private void Changed(string source) => _bus?.Publish(new ModifiersChanged(source));
}
