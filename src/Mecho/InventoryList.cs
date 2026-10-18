using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mecho.Core;

namespace Mecho;

/// <summary>
/// Списъкът с притежаваното, както го вижда старият код (Add, Remove, Contains…),
/// но истината е в ядрото (InventoryState): четенето е оттам, промените са команди
/// (GrantItem / RevokeItem / SetInventory) – значи пускат ItemAcquired/ItemRevoked и
/// всички изгледи се обновяват. Преди свързването с ядрото (зареждане на стар запис,
/// самопроверката) работи върху старото поле в SaveData.
/// Нов код: ctx.Store.Get&lt;InventoryState&gt;().Has(id) и ctx.Bus.Send(new GrantItem(…)).
/// </summary>
public sealed class InventoryList : IList<string>, IReadOnlyCollection<string>
{
    private readonly SaveData _save;
    private readonly bool _clothes;

    internal InventoryList(SaveData save, bool clothes)
    {
        _save = save;
        _clothes = clothes;
    }

    private IReadOnlyList<string> Current =>
        _save.InventoryNow is { } inv ? (_clothes ? inv.Clothes : inv.Items) : _save.LegacyList(_clothes);

    public int Count => Current.Count;

    public bool IsReadOnly => false;

    public string this[int index]
    {
        get => Current[index];
        set => throw new NotSupportedException("Притежаваното не се подменя по индекс – махни и добави.");
    }

    public bool Contains(string item) =>
        item != null && (_save.InventoryNow is { } inv ? (_clothes ? inv.HasClothes(item) : inv.Has(item)) : _save.LegacyList(_clothes).Contains(item));

    public void Add(string item)
    {
        if (string.IsNullOrWhiteSpace(item)) return;
        if (_save.BusNow is { } bus) bus.Send(new GrantItem(item, "игра", _clothes));
        else if (!_save.LegacyList(_clothes).Contains(item)) _save.LegacyList(_clothes).Add(item);
    }

    public void AddRange(IEnumerable<string> items)
    {
        foreach (var i in items) Add(i);
    }

    public bool Remove(string item)
    {
        if (item == null) return false;
        if (_save.BusNow is { } bus) return bus.Send(new RevokeItem(item, _clothes)).Ok;
        return _save.LegacyList(_clothes).Remove(item);
    }

    public int RemoveAll(Predicate<string> match)
    {
        var gone = Current.Where(x => match(x)).ToList();
        foreach (var x in gone) Remove(x);
        return gone.Count;
    }

    public void Clear()
    {
        if (_save.BusNow is { } bus)
        {
            var inv = _save.InventoryNow!;
            bus.Send(_clothes ? new SetInventory(inv.Items, Array.Empty<string>(), "изчистено") : new SetInventory(Array.Empty<string>(), inv.Clothes, "изчистено"));
        }
        else _save.LegacyList(_clothes).Clear();
    }

    public int IndexOf(string item)
    {
        var c = Current;
        for (int i = 0; i < c.Count; i++)
            if (c[i] == item) return i;
        return -1;
    }

    public void Insert(int index, string item) => Add(item);

    public void RemoveAt(int index) => Remove(Current[index]);

    public void CopyTo(string[] array, int arrayIndex)
    {
        foreach (var x in Current) array[arrayIndex++] = x;
    }

    /// <summary>Снимка (промяна по време на обхождане не чупи обхождането).</summary>
    public IEnumerator<string> GetEnumerator() => Current.ToList().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
