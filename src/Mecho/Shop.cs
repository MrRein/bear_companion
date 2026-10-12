using System;
using System.Collections.Generic;
using System.Linq;

namespace Mecho;

/// <summary>
/// Офертата на деня: най-горе в магазина има 3 неща (дрехи и аксесоари, тапети,
/// скинове за дивана и леглото), които се сменят всеки ден в 00:00. Купените не
/// излизат. Дрехите и тапетите идват едва след като е купен гардеробът; скиновете –
/// само ако има диван или легло.
/// </summary>
public static class DailyOffer
{
    public const int Count = 3;

    private static readonly Random Rng = new();

    /// <summary>Купен ли е гардеробът (иначе няма дрехи и тапети).</summary>
    public static bool HasWardrobe(SaveData s) => Kitchen.Owns(s, Kitchen.Wardrobe);

    /// <summary>Всичко, което може да излезе (още не е купено): ключ и тежест.</summary>
    private static IEnumerable<(string Key, double Weight)> Pool(SaveData s)
    {
        if (HasWardrobe(s))
        {
            foreach (var g in Clothes.All.Where(g => !g.Hidden && !Clothes.Owns(s, g))) yield return ("g:" + g.Id, 3);
            foreach (var b in Backdrops.All.Where(b => b.Price > 0 && !Backdrops.Owns(s, b))) yield return ("bg:" + b.Id, 1);
        }
        foreach (var item in new[] { Kitchen.Couch, Kitchen.Bed })
        {
            if (!Kitchen.Owns(s, item)) continue;
            foreach (var k in Furniture.SkinsFor(item).Where(k => k.Price > 0 && !Furniture.Owns(s, k))) yield return ($"skin:{item}:{k.Id}", 1);
        }
    }

    /// <summary>
    /// Днешните неща. Нов ден (или force) – нови 3. Ако днес са излезли по-малко
    /// (например гардеробът тъкмо е купен), се допълват до 3.
    /// </summary>
    public static IReadOnlyList<string> Today(SaveData s, bool force = false)
    {
        string today = DateTime.Today.ToString("yyyy-MM-dd");
        if (force || s.OfferDay != today)
        {
            s.OfferDay = today;
            s.OfferItems.Clear();
        }
        if (s.OfferItems.Count < Count)
        {
            var pool = Pool(s).Where(p => !s.OfferItems.Contains(p.Key)).ToList();
            while (s.OfferItems.Count < Count && pool.Count > 0)
            {
                double roll = Rng.NextDouble() * pool.Sum(p => p.Weight);
                int i = 0;
                for (; i < pool.Count - 1; i++)
                {
                    if (roll < pool[i].Weight) break;
                    roll -= pool[i].Weight;
                }
                s.OfferItems.Add(pool[i].Key);
                pool.RemoveAt(i);
            }
        }
        return s.OfferItems;
    }

    /// <summary>Колко остава до новата оферта (00:00).</summary>
    public static TimeSpan Left => DateTime.Today.AddDays(1) - DateTime.Now;

    public static object? Resolve(string key)
    {
        var parts = key.Split(':');
        return parts[0] switch
        {
            "g" => Clothes.Find(parts[1]),
            "bg" => Backdrops.All.FirstOrDefault(b => b.Id == parts[1]),
            "skin" when parts.Length == 3 => Furniture.SkinsFor(parts[1]).FirstOrDefault(k => k.Id == parts[2]),
            _ => null,
        };
    }
}
