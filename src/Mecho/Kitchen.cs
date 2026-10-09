using System.Linq;

namespace Mecho;

/// <summary>Храна, която мечокът обича. Нова храна се отключва със свършени задачи.</summary>
public sealed record Food(string Id, string Name, string Icon, double Fullness, int UnlockTasks, string Unlock, string[] Lines);

public static class Kitchen
{
    public static readonly Food[] Foods =
    {
        new("hazelnuts", "Лешници", "🌰", 15, 0, "",
            new[] { "Крак! Крак! Мммм.", "Лешници! Най-хубавите пионки… и закуска.", "Ще пазя черупките за прототипа." }),
        new("popcorn", "Пуканки", "🍿", 25, 5, "тенджерка",
            new[] { "Пук! Пук! Хванах една с уста!", "Пуканки като за игрална вечер!", "Хрус-хрус." }),
        new("meatballs", "Кюфтенца", "🍖", 40, 15, "тиган",
            new[] { "Ох, горещи са! *духа*", "Кюфтенца! Най-хубавият ден!", "Цвър-цвър… мммм." }),
        new("potatoes", "Мечешки картофки", "🥔", 50, 30, "фурна",
            new[] { "Мечешки картофки от фурната! Ухааа.", "Хрупкави отвън, меки отвътре. Перфектни.", "Ще ги направя пак утре. И вдругиден." }),
    };

    public static bool IsUnlocked(Food food, SaveData save) => save.TasksDoneTotal >= food.UnlockTasks;

    /// <summary>Храната, която току-що е отключена при този брой задачи (ако има).</summary>
    public static Food? UnlockedAt(int tasksDone) => Foods.FirstOrDefault(f => f.UnlockTasks > 0 && f.UnlockTasks == tasksDone);
}
