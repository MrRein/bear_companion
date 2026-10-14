using System.Linq;

namespace Mecho;

/// <summary>
/// Скин за мебел (диван или легло): друга рисунка assets/props/&lt;предмет&gt;_&lt;Id&gt;.png.
/// Класическият е безплатен; другите се купуват в магазина и се
/// сменят в гардероба.
/// </summary>
public sealed record Skin(string Item, string Id, string Name, int Price)
{
    public string Prop => Id == Furniture.Classic ? Item : Item + "_" + Id;
}

public static class Furniture
{
    public const string Classic = "classic";

    public static readonly Skin[] CouchSkins =
    {
        new(Kitchen.Couch, Classic, "Червен диван", 0),
        new(Kitchen.Couch, "velvet", "Син кадифен", 20),
        new(Kitchen.Couch, "forest", "Горско зелен", 20),
        new(Kitchen.Couch, "dots", "Розов на точки", 25),
        new(Kitchen.Couch, "mint", "Мента на райе", 25),
        new(Kitchen.Couch, "honey", "Меден", 25),
        new(Kitchen.Couch, "leather", "Кожен капитоне", 35),
    };

    public static readonly Skin[] BedSkins =
    {
        new(Kitchen.Bed, Classic, "Синьо одеяло", 0),
        new(Kitchen.Bed, "pink", "Розово", 20),
        new(Kitchen.Bed, "forest", "Горско каре", 20),
        new(Kitchen.Bed, "honey", "Медено каре", 25),
        new(Kitchen.Bed, "stars", "Звездна нощ", 25),
    };

    public static Skin[] SkinsFor(string item) => item == Kitchen.Bed ? BedSkins : CouchSkins;

    public static bool Owns(SaveData save, Skin s) => s.Price == 0 || save.Owned.Contains($"skin_{s.Item}_{s.Id}");

    /// <summary>Сложеният скин: избраният, ако е купен; иначе първият (той е винаги наличен).</summary>
    public static Skin Current(SaveData save, string item)
    {
        string id = item == Kitchen.Bed ? save.BedSkin : save.CouchSkin;
        return SkinsFor(item).FirstOrDefault(s => s.Id == id && Owns(save, s)) ?? SkinsFor(item)[0];
    }
}
