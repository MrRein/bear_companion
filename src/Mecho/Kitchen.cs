using System.Linq;

namespace Mecho;

/// <summary>Храна, която мечокът обича. Нова храна се отключва със свършени задачи.</summary>
/// <summary>
/// Храна. Count е на колко парчета се появява из екрана (всяко засища с Fullness / Count);
/// рисунката е assets/props/food_{Id}.png.
/// </summary>
public sealed record Food(string Id, string Name, string Icon, double Fullness, int Count, int UnlockTasks, string Unlock, string[] Lines);

public static class Kitchen
{
    public static readonly Food[] Foods =
    {
        new("hazelnuts", "Лешници", "🌰", 15, 5, 0, "",
            new[] { "Крак! Мммм.", "Крак-крак!", "Още един! Крак!", "Ще пазя черупката за прототипа." }),
        new("popcorn", "Пуканки", "🍿", 25, 1, 5, "тенджерка",
            new[] { "Пук! Пук! Хванах една с уста!", "Пуканки като за игрална вечер!", "Хрус-хрус." }),
        new("meatballs", "Кюфтенца", "🍖", 40, 1, 15, "тиган",
            new[] { "Ох, горещи са! *духа*", "Кюфтенца! Най-хубавият ден!", "Цвър-цвър… мммм." }),
        new("potatoes", "Мечешки картофки", "🥔", 50, 1, 30, "фурна",
            new[] { "Мечешки картофки от фурната! Ухааа.", "Хрупкави отвън, меки отвътре. Перфектни.", "Ще ги направя пак утре. И вдругиден." }),
    };

    public static bool IsUnlocked(Food food, SaveData save) => save.TasksDoneTotal >= food.UnlockTasks;

    /// <summary>Храната, която току-що е отключена при този брой задачи (ако има).</summary>
    public static Food? UnlockedAt(int tasksDone) => Foods.FirstOrDefault(f => f.UnlockTasks > 0 && f.UnlockTasks == tasksDone);
}
