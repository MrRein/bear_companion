using System.Linq;

namespace Mecho;

/// <summary>Фон за портрета на мечока в менюто (assets/backgrounds/bg_&lt;Id&gt;.png, 50 x 50).</summary>
public sealed record Backdrop(string Id, string Name, int Price);

public static class Backdrops
{
    public const string Default = "meadow";

    public static readonly Backdrop[] All =
    {
        new(Default, "Ливада", 0),
        new("sunset", "Залез", 8),
        new("night", "Звездна нощ", 8),
        new("beach", "Плаж", 10),
        new("autumn", "Есенна гора", 10),
        new("winter", "Зимна приказка", 12),
        new("candy", "Сладкарница", 12),
        new("studio", "Ателие", 15),
        new("library", "Библиотека", 15),
        new("underwater", "Под водата", 18),
        new("space", "Космос", 20),
    };

    public static Backdrop Find(string? id) => All.FirstOrDefault(b => b.Id == id) ?? All[0];

    public static bool Owns(SaveData save, Backdrop b) => b.Price == 0 || save.Owned.Contains("bg_" + b.Id);
}
