using System;

namespace Mecho;

/// <summary>
/// Рядкостта на нещата за събиране (дрехи, пижами, тапети, менюта, облици):
/// Обикновено · Хубаво · Страхотно · Безценно. Записана е в loot_list (колона „Рядкост“);
/// от нея идват цената в магазина и колко често излиза (оферта, подарък, цел, сандъчета).
/// Цветовете са като на сандъчетата: дърво, сребро, злато, лилаво.
/// </summary>
public enum Rarity { Common, Uncommon, Rare, Legendary }

public static class Rarities
{
    public static readonly Rarity[] All = { Rarity.Common, Rarity.Uncommon, Rarity.Rare, Rarity.Legendary };

    public static string Name(Rarity r) => r switch
    {
        Rarity.Uncommon => "Хубаво",
        Rarity.Rare => "Страхотно",
        Rarity.Legendary => "Безценно",
        _ => "Обикновено",
    };

    public static Rarity? Parse(string s) => s.Trim().ToLowerInvariant() switch
    {
        "обикновено" or "обикновен" or "common" => Rarity.Common,
        "хубаво" or "uncommon" => Rarity.Uncommon,
        "страхотно" or "rare" => Rarity.Rare,
        "безценно" or "legendary" => Rarity.Legendary,
        _ => null,
    };

    /// <summary>Ценовата група на предмета по кода му (дрехи, пижами, тапети, менюта, облици).</summary>
    public static string? PriceGroup(string code) => code.Split(':')[0] switch
    {
        "cloth" => "дрехи",
        "pajama" => "пижами",
        "bg" => "тапети",
        "theme" => "менюта",
        "skin" => "облици",
        _ => null,
    };

    /// <summary>Цените по подразбиране (ако ги няма в loot_list → „Настройки“).</summary>
    public static int DefaultPrice(string group, Rarity r)
    {
        int[] p = group is "дрехи" or "пижами" ? new[] { 20, 30, 50, 70 } : new[] { 35, 45, 65, 90 };
        return p[(int)r];
    }
}
