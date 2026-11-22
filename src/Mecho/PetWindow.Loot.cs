using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Imaging;

namespace Mecho;

// Наградите от master loot_list (assets/loot_list.xlsx): кое нещо може ли да се падне сега,
// даването му и как изглежда. Кодовете: nuts, page, coupon:pct25, cloth:id, pajama:id,
// bg:id, theme:id, up:id, skin:предмет:id.
public sealed partial class PetWindow
{
    /// <summary>Всички предмети в играта като кодове за loot_list (за самопроверката).</summary>
    public static IEnumerable<string> AllLootCodes()
    {
        yield return "nuts";
        yield return "page";
        foreach (var c in CouponKinds) yield return "coupon:" + c.Code;
        foreach (var g in Clothes.All.Where(g => !g.Hidden && g.Price > 0)) yield return "cloth:" + g.Id;
        foreach (var p in Clothes.PajamaSets.Where(p => p.Price > 0)) yield return "pajama:" + p.Id;
        foreach (var b in Backdrops.All.Where(b => b.Price > 0)) yield return "bg:" + b.Id;
        foreach (var t in UiThemes.All.Where(t => t.Price > 0)) yield return "theme:" + t.Id;
        foreach (var u in Kitchen.Upgrades) yield return "up:" + u.Id;
        foreach (var (item, skins) in Furniture.ByItem)
            foreach (var k in skins.Where(k => k.Price > 0)) yield return $"skin:{item}:{k.Id}";
    }

    private static (string Kind, string Id) SplitCode(string code)
    {
        int i = code.IndexOf(':');
        return i < 0 ? (code, "") : (code[..i], code[(i + 1)..]);
    }

    /// <summary>Може ли това да се падне сега (не го имаш, има гардероб за дрехите…).</summary>
    public bool LootAvailable(string code)
    {
        var (kind, id) = SplitCode(code);
        bool wardrobe = DailyOffer.HasWardrobe(_save);
        switch (kind)
        {
            case "nuts": return true;
            case "page": return Recipes.Next(_save) != null;
            case "coupon": return CouponKinds.Any(c => c.Code == id);
            case "cloth": return wardrobe && Clothes.All.FirstOrDefault(g => g.Id == id) is { } g && !Clothes.Owns(_save, g);
            case "pajama": return wardrobe && Clothes.PajamaSets.FirstOrDefault(p => p.Id == id) is { } p && !Clothes.Owns(_save, p);
            case "bg": return wardrobe && Backdrops.All.FirstOrDefault(b => b.Id == id) is { } b && !Backdrops.Owns(_save, b);
            case "theme": return wardrobe && UiThemes.All.FirstOrDefault(t => t.Id == id) is { } t && !UiThemes.Owns(_save, t);
            case "up": return Kitchen.Find(id) != null && !OwnsOrOrdered(id);
            case "skin":
            {
                var (item, sid) = SplitCode(id);
                return OwnsOrOrdered(item) && Furniture.SkinsFor(item).FirstOrDefault(k => k.Id == sid) is { } k && !Furniture.Owns(_save, k);
            }
            default: return false;
        }
    }

    /// <summary>Тегли от източника (Loot.Mission, Loot.Gift, сандъчетата); except – вече избраните.</summary>
    private string? RollLoot(string source, Func<string, bool>? also = null) =>
        Loot.Roll(source, _rng, c => LootAvailable(c) && (also?.Invoke(c) ?? true));

    /// <summary>
    /// Дава нещото (без лешниците – те са с количество). Връща името му или null, ако не може.
    /// </summary>
    private string? GrantLoot(string code)
    {
        if (!LootAvailable(code)) return null;
        var (kind, id) = SplitCode(code);
        switch (kind)
        {
            case "page":
                FindRecipePage();
                return LastLearned != null ? "нова рецепта: " + LastLearned.Name : "страница от рецепта";
            case "coupon":
                _save.CouponCodes.Add(id);
                return "купон „" + CouponOf(id).Tip + "“";
            case "cloth":
                _save.OwnedClothes.Add(id);
                return Clothes.All.First(g => g.Id == id).Name;
            case "pajama":
            {
                var p = Clothes.PajamaSets.First(x => x.Id == id);
                _save.OwnedClothes.Add(p.Key);
                return p.Name;
            }
            case "bg":
                _save.Owned.Add("bg_" + id);
                return "тапет „" + Backdrops.Find(id).Name + "“";
            case "theme":
            {
                var t = UiThemes.All.First(x => x.Id == id);
                _save.Owned.Add(t.Key);
                return "меню „" + t.Name + "“";
            }
            case "up":
            {
                var u = Kitchen.Find(id)!;
                GiftUpgrade(u);
                return u.Name + " (идва с доставка)";
            }
            case "skin":
            {
                var (item, sid) = SplitCode(id);
                var k = Furniture.SkinsFor(item).First(x => x.Id == sid);
                _save.Owned.Add($"skin_{item}_{sid}");
                return k.Name;
            }
            default: return null;
        }
    }

    /// <summary>Името на предмет по код (от каталога; ако го няма – от loot_list).</summary>
    public static string LootName(string code)
    {
        var (kind, id) = SplitCode(code);
        return kind switch
        {
            "nuts" => "Лешници",
            "page" => "Страница от рецепта",
            "coupon" => "Купон: " + CouponOf(id).Tip,
            "cloth" => Clothes.All.FirstOrDefault(g => g.Id == id)?.Name ?? "Дреха",
            "pajama" => Clothes.PajamaSets.FirstOrDefault(p => p.Id == id)?.Name ?? "Пижама",
            "bg" => "Тапет „" + Backdrops.Find(id).Name + "“",
            "theme" => "Меню „" + (UiThemes.All.FirstOrDefault(t => t.Id == id)?.Name ?? id) + "“",
            "up" => Kitchen.Find(id)?.Name ?? "Мебел",
            "skin" => SplitCode(id) is var (item, sid) && Furniture.SkinsFor(item).FirstOrDefault(k => k.Id == sid) is { } k ? k.Name : "Облик",
            _ => Loot.Find(code)?.Name ?? code,
        };
    }

    /// <summary>Картинката на предмета (дреха, тапет, мебел…), ако я има.</summary>
    public BitmapSource? LootArt(string code)
    {
        var (kind, id) = SplitCode(code);
        try
        {
            switch (kind)
            {
                case "cloth" when Clothes.All.FirstOrDefault(g => g.Id == id) is { } g: return GarmentPreview(g);
                case "pajama" when Clothes.PajamaSets.FirstOrDefault(p => p.Id == id) is { } p
                                   && Clothes.All.FirstOrDefault(g => g.Id == p.Top) is { } top: return GarmentPreview(top);
                case "bg": return BackdropImage(Backdrops.Find(id));
                case "theme" when UiThemes.All.FirstOrDefault(t => t.Id == id) is { } t: return UiThemes.Preview(t);
                case "up": return _lib.HasProp("shop_" + id) ? PropImage("shop_" + id) : null;
                case "skin":
                {
                    var (item, sid) = SplitCode(id);
                    string art = Furniture.Art(item, sid);
                    return _lib.HasProp(art) ? PropImage(art) : null;
                }
            }
        }
        catch { }
        return null;
    }

    /// <summary>Иконка по вида на предмета.</summary>
    public static string LootIcon(string code)
    {
        var (kind, id) = SplitCode(code);
        return kind switch
        {
            "nuts" => "nut3",
            "page" => "book",
            "coupon" => CouponOf(id).Icon,
            "cloth" => Clothes.All.FirstOrDefault(g => g.Id == id) is { } g ? g.Slot.Icon : "shirt",
            "pajama" => "pajama",
            "bg" => "picture",
            "theme" => "menu",
            "up" => "couch",
            "skin" => "couch",
            _ => "gift",
        };
    }

    /// <summary>Лешниците от loot_list: „Настройки“ → ключ.мин / ключ.макс (по-често ниско).</summary>
    private int LootNuts(string key, int min, int max) =>
        LowRoll(Loot.SettingInt(key + " мин", min), Math.Max(Loot.SettingInt(key + " мин", min), Loot.SettingInt(key + " макс", max)));
}
