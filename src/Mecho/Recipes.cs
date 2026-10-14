using System;
using System.Linq;

namespace Mecho;

/// <summary>
/// Рецептите се откриват бавно: от страници. Следващата рецепта е тайна (случайна
/// от ненаучените); знае се само колко е трудна: лесна 3 страници, средна 5,
/// сложна 7. Страниците идват от мечо-дорото (1 на 5), от трудна задача и понякога
/// от дневния подарък. В „Храна“ се виждат само научените рецепти и една лента
/// с прогреса към следващата.
/// </summary>
public static class Recipes
{
    private static readonly Random Rng = new();

    /// <summary>Колко страници иска рецептата.</summary>
    public static int PagesFor(Food f) => f.Id switch
    {
        "potatoes" or "pizza" => 7,
        "coffee" or "meatballs" or "latte" or "mocha" or "skewers" => 5,
        _ when f.Prep != null => 5,     // в два уреда – средна
        _ => 3,
    };

    public static string Difficulty(int pages) => pages switch { >= 7 => "сложна", >= 5 => "средна", _ => "лесна" };

    /// <summary>
    /// Рецептите, които още не знае и се учат от страници. Боровинките са без
    /// рецепта, а всеки уред идва със своята първа рецепта (Kitchen.DefaultRecipe).
    /// </summary>
    /// Учат се само рецепти за уредите, които вече има (иначе няма с какво да се сготвят).
    private static Food[] Unknown(SaveData s) => Kitchen.Foods.Where(f => f.Cost > 0 && !Kitchen.IsDefaultRecipe(f) && !Kitchen.HasRecipe(f, s) && Kitchen.HasTool(f, s)).ToArray();

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
        if (s.RecipePages < PagesFor(next)) return null;
        s.Owned.Add(Kitchen.RecipeId(next));
        s.NextRecipe = null;
        s.RecipePages = 0;
        Next(s);
        return next;
    }
}
