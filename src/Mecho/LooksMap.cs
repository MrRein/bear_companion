using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Mecho.Core;

namespace Mecho;

/// <summary>
/// Облеклото (слот → дреха) или скиновете (предмет → скин), както ги вижда старият
/// код (outfit[slot] = id, Remove, Clear, TryGetValue…). Истината е в ядрото
/// (LooksState): четенето е оттам, промените са команди (Wear, TakeOff, UseSkin…) –
/// значи пускат OutfitChanged/SkinChanged. Преди свързването (стар запис, отделен
/// SaveData в самопроверката) работи върху старото поле.
/// </summary>
public sealed class LooksMap : IDictionary<string, string>, IReadOnlyDictionary<string, string>
{
    private readonly SaveData _save;
    private readonly bool _skins;

    internal LooksMap(SaveData save, bool skins)
    {
        _save = save;
        _skins = skins;
    }

    private IReadOnlyDictionary<string, string> Current =>
        _save.LooksNow is { } l ? (_skins ? l.Skins : l.Outfit) : _save.LegacyMap(_skins);

    public string this[string key]
    {
        get => Current[key];
        set
        {
            if (_save.BusNow is { } bus)
            {
                if (_skins) bus.Send(new UseSkin(key, value));
                else bus.Send(new Wear(key, value));
            }
            else _save.LegacyMap(_skins)[key] = value;
        }
    }

    public bool Remove(string key)
    {
        if (_save.BusNow is { } bus)
        {
            if (!ContainsKey(key)) return false;
            return (_skins ? bus.Send(new UseSkin(key, null)) : bus.Send(new TakeOff(key))).Ok;
        }
        return _save.LegacyMap(_skins).Remove(key);
    }

    public void Clear()
    {
        if (_save.BusNow is { } bus)
        {
            if (Count == 0) return;
            if (_skins) bus.Send(new SetSkins(new Dictionary<string, string>()));
            else bus.Send(new TakeOffAll());
        }
        else _save.LegacyMap(_skins).Clear();
    }

    public void Add(string key, string value)
    {
        if (ContainsKey(key)) throw new ArgumentException($"{key} вече има стойност.");
        this[key] = value;
    }

    public bool ContainsKey(string key) => key != null && Current.ContainsKey(key);

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out string value) => Current.TryGetValue(key, out value);

    public int Count => Current.Count;

    public bool IsReadOnly => false;

    public ICollection<string> Keys => Current.Keys.ToList();

    public ICollection<string> Values => Current.Values.ToList();

    IEnumerable<string> IReadOnlyDictionary<string, string>.Keys => Current.Keys;

    IEnumerable<string> IReadOnlyDictionary<string, string>.Values => Current.Values;

    public void Add(KeyValuePair<string, string> item) => Add(item.Key, item.Value);

    public bool Contains(KeyValuePair<string, string> item) => TryGetValue(item.Key, out var v) && v == item.Value;

    public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex)
    {
        foreach (var kv in Current) array[arrayIndex++] = kv;
    }

    public bool Remove(KeyValuePair<string, string> item) => Contains(item) && Remove(item.Key);

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => Current.ToList().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
