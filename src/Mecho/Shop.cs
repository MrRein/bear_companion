using System;
using System.Collections.Generic;
using System.Linq;

namespace Mecho;

/// <summary>
/// Офертата на деня: най-горе в магазина има 3 неща (дрехи и аксесоари, пижами,
/// тапети, скинове за мебелите и уредите), които се сменят всеки ден в 00:00.
/// Купените не излизат. Всичко идва едва след като е купен гардеробът; скинът –
/// само ако предметът му е купен.
/// </summary>
public static class DailyOffer
{
    public const int Count = 3;

    private static readonly Random Rng = new();

    /// <summary>Купен ли е гардеробът (иначе няма дрехи и тапети).</summary>
    public static bool HasWardrobe(SaveData s) => Kitchen.Owns(s, Kitchen.Wardrobe);

    /// <summary>
    /// Всичко, което може да излезе (още не е купено), по групи: всеки вид дреха
    /// (шапки, очила…), тапетите, скиновете за дивана и тези за леглото.
    /// </summary>
    private static Dictionary<string, List<string>> Pool(SaveData s)
    {
        var groups = new Dictionary<string, List<string>>();
        void Add(string group, string key)
        {
            if (!groups.TryGetValue(group, out var list)) groups[group] = list = new List<string>();
            list.Add(key);
        }
        if (HasWardrobe(s))
        {
            foreach (var g in Clothes.All.Where(g => !g.Hidden && !Clothes.Owns(s, g))) Add("g:" + g.SlotId, "g:" + g.Id);
            foreach (var b in Backdrops.All.Where(b => b.Price > 0 && !Backdrops.Owns(s, b))) Add("bg", "bg:" + b.Id);
            foreach (var p in Clothes.PajamaSets.Where(p => !Clothes.Owns(s, p))) Add("pj", "pj:" + p.Id);
            foreach (var t in UiThemes.All.Where(t => !UiThemes.Owns(s, t))) Add("ui", "ui:" + t.Id);
        }
        foreach (var item in Furniture.ByItem.Keys)
        {
            if (!Kitchen.Owns(s, item)) continue;
            foreach (var k in Furniture.SkinsFor(item).Where(k => k.Price > 0 && !Furniture.Owns(s, k))) Add("skin:" + item, $"skin:{item}:{k.Id}");
        }
        return groups;
    }

    /// <summary>Групата на нещо от офертата (от един вид има най-много едно).</summary>
    internal static string GroupOf(string key)
    {
        var parts = key.Split(':');
        return parts[0] switch
        {
            "g" => "g:" + (Clothes.Find(parts[1])?.SlotId ?? ""),
            "skin" => "skin:" + parts[1],
            _ => parts[0],
        };
    }

    /// <summary>
    /// Днешните неща. Нов ден (или force) – нови 3, всяко от различен вид (не три
    /// дивана или три шапки), случайни. Ако днес са излезли по-малко (например
    /// гардеробът тъкмо е купен), се допълват.
    /// </summary>
    public static IReadOnlyList<string> Today(SaveData s, bool force = false)
    {
        // Без гардероб няма оферта изобщо (и скиновете чакат гардероба).
        if (!HasWardrobe(s))
        {
            s.OfferItems.Clear();
            return s.OfferItems;
        }
        string today = DateTime.Today.ToString("yyyy-MM-dd");
        // Отпреди разнообразието: три неща от един вид (три дивана) – наново.
        bool same = s.OfferItems.Select(GroupOf).Distinct().Count() < s.OfferItems.Count;
        if (force || same || s.OfferDay != today)
        {
            s.OfferDay = today;
            s.OfferItems.Clear();
        }
        // Купено или вече невалидно (без гардероб и т.н.) от днешните не се пипа;
        // попълва се с групи, които още ги няма.
        var used = s.OfferItems.Select(GroupOf).ToHashSet();
        var groups = Pool(s).Where(g => !used.Contains(g.Key)).ToList();
        while (s.OfferItems.Count < Count && groups.Count > 0)
        {
            // Дрехите са по-често (много видове), но тапет, пижама и скин също имат шанс.
            // Скиновете са на много предмети: общо да не изместват дрехите.
            int skinGroups = Math.Max(1, groups.Count(g => g.Key.StartsWith("skin:")));
            double Weight(string group) => group.StartsWith("g:") ? 1 : group.StartsWith("skin:") ? Math.Min(1.5, 4.0 / skinGroups) : 1.5;
            double roll = Rng.NextDouble() * groups.Sum(g => Weight(g.Key));
            int i = 0;
            for (; i < groups.Count - 1; i++)
            {
                if (roll < Weight(groups[i].Key)) break;
                roll -= Weight(groups[i].Key);
            }
            var items = groups[i].Value;
            s.OfferItems.Add(items[Rng.Next(items.Count)]);
            groups.RemoveAt(i);
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
            "pj" => Clothes.PajamaSets.FirstOrDefault(p => p.Id == parts[1]),
            "ui" => UiThemes.All.FirstOrDefault(t => t.Id == parts[1]),
            "skin" when parts.Length == 3 => Furniture.SkinsFor(parts[1]).FirstOrDefault(k => k.Id == parts[2]),
            _ => null,
        };
    }
}
