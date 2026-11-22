using System.Linq;

namespace Mecho;

/// <summary>Фон за портрета на мечока в менюто (assets/backgrounds/bg_&lt;Id&gt;.png, 50 x 50).</summary>
public sealed record Backdrop(string Id, string Name, int BasePrice)
{
    /// <summary>Цената – по рядкостта от loot_list.</summary>
    public int Price => Loot.PriceFor("bg:" + Id, BasePrice);
}

public static class Backdrops
{
    public const string Default = "meadow";

    public static readonly Backdrop[] All =
    {
        new(Default, "Ливада", 0),
        new("sunset", "Залез", 20),
        new("night", "Звездна нощ", 20),
        new("beach", "Плаж", 20),
        new("autumn", "Есенна гора", 20),
        new("winter", "Зимна приказка", 20),
        new("candy", "Сладкарница", 20),
        new("studio", "Ателие", 20),
        new("library", "Библиотека", 20),
        new("underwater", "Под водата", 20),
        new("space", "Космос", 20),
    };

    public static Backdrop Find(string? id) => All.FirstOrDefault(b => b.Id == id) ?? All[0];

    public static bool Owns(SaveData save, Backdrop b) => b.Price == 0 || save.Owned.Contains("bg_" + b.Id);
}
