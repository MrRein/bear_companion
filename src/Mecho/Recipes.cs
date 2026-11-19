using System;
using System.Linq;

namespace Mecho;

/// <summary>
/// Рецептите се откриват бавно: от страници – винаги по 5. Коя рецепта ще се отключи
/// е тайна: решава се, когато събереш петата (случайна от ненаучените за уредите, които имаш). Страниците идват от мечо-дорото (1 на 5), от трудна задача и понякога
/// от дневния подарък. В „Храна“ се виждат само научените рецепти и една лента
/// с прогреса към следващата.
/// </summary>
public static class Recipes
{
    private static readonly Random Rng = new();

    /// <summary>Колко страници иска рецептата: винаги 5.</summary>
    public const int Pages = 5;

    public static int PagesFor(Food f) => Pages;

    public static string Difficulty(int pages) => pages switch { >= 7 => "сложна", >= 5 => "средна", _ => "лесна" };

    /// <summary>
    /// Рецептите, които още не знае и се учат от страници. Боровинките са без
    /// рецепта, а всеки уред идва със своята първа рецепта (Kitchen.DefaultRecipe).
    /// </summary>
    /// Учат се само рецепти за уредите, които вече има (иначе няма с какво да се сготвят).
    private static Food[] Unknown(SaveData s) => Kitchen.Foods.Where(f => f.Cost > 0 && Kitchen.Regular(f) && !Kitchen.IsDefaultRecipe(f) && !Kitchen.HasRecipe(f, s) && Kitchen.HasTool(f, s)).ToArray();

    /// <summary>Страници се събират чак след първия купен кухненски уред.</summary>
    public static bool Started(SaveData s) => Kitchen.Upgrades.Any(u => u.Shelf == Kitchen.KitchenShelf && Kitchen.Owns(s, u.Id));

    /// <summary>Следващата рецепта (избира я, ако още няма). null = знае всички (или още няма уред).</summary>
    public static Food? Next(SaveData s)
    {
        if (!Started(s)) return null;
        var current = Kitchen.Foods.FirstOrDefault(f => f.Id == s.NextRecipe);
        if (current != null && !Kitchen.HasRecipe(current, s)) return current;
        var unknown = Unknown(s);
        if (unknown.Length == 0)
        {
            s.NextRecipe = null;
            return null;
        }
        var pick = unknown[Rng.Next(unknown.Length)];
        s.NextRecipe = pick.Id;
        s.RecipePages = 0;
        return pick;
    }

    /// <summary>Добавя страница. Връща научената рецепта, ако тази страница я довърши.</summary>
    public static Food? AddPage(SaveData s)
    {
        var next = Next(s);
        if (next == null) return null;
        s.RecipePages++;
        if (s.RecipePages < Pages) return null;
        // Коя рецепта – решава се чак сега: случайна от ненаучените за уредите, които има.
        var unknown = Unknown(s);
        if (unknown.Length > 0) next = unknown[Rng.Next(unknown.Length)];
        s.Owned.Add(Kitchen.RecipeId(next));
        s.NextRecipe = null;
        s.RecipePages = 0;
        Next(s);
        return next;
    }
}
