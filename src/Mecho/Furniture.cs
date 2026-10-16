using System.Collections.Generic;
using System.Linq;

namespace Mecho;

/// <summary>
/// Друг вид на предмет (диван, легло, печица, уред, китара, лампа): същото нещо,
/// но друга версия (камина вместо печица, мока кана вместо кафе машина…).
/// Класическият е безплатен; другите се купуват в офертата на деня (само щом
/// предметът е купен) и се сменят в гардероба („Мебели и уреди“).
/// </summary>
public sealed record Skin(string Item, string Id, string Name, int Price)
{
    /// <summary>Рисунката (без кадъра): couch_velvet, cook_pot_copper, heater_fireplace…</summary>
    public string Prop => Furniture.Art(Item, Id);
}

public static class Furniture
{
    public const string Classic = "classic";

    /// <summary>Рисунката на предмета: assets/props/&lt;Base&gt;[_&lt;скин&gt;][кадър].png.</summary>
    public static string Base(string item) => item switch
    {
        Kitchen.Couch or Kitchen.Bed or Kitchen.Heater or Kitchen.Lamp or Kitchen.Desk or Kitchen.Feeder or Kitchen.Radio => item,
        Kitchen.Guitar => "guitar",
        _ => "cook_" + item,
    };

    public static string Art(string item, string id, string frame = "") =>
        item == Kitchen.PlantPot ? $"plant_{(id == Classic ? "sunflower" : id)}_9"   // растението: голямото, за плочката
        : Base(item) + (id == Classic ? "" : "_" + id) + frame;

    public static readonly Skin[] CouchSkins =
    {
        new(Kitchen.Couch, Classic, "Червен диван", 0),
        new(Kitchen.Couch, "velvet", "Кадифен диван с гърбица", 20),
        new(Kitchen.Couch, "forest", "Горски с възглавнички", 20),
        new(Kitchen.Couch, "dots", "Розово канапенце", 25),
        new(Kitchen.Couch, "mint", "Дървена пейка на райе", 25),
        new(Kitchen.Couch, "honey", "Пухкав меден диван", 25),
        new(Kitchen.Couch, "leather", "Кожен честърфийлд", 35),
    };

    public static readonly Skin[] BedSkins =
    {
        new(Kitchen.Bed, Classic, "Синьо одеяло", 0),
        new(Kitchen.Bed, "pink", "Легло с балдахин", 20),
        new(Kitchen.Bed, "forest", "Легло от трупчета", 20),
        new(Kitchen.Bed, "honey", "Ниско футонче", 25),
        new(Kitchen.Bed, "stars", "Метално легло със звезди", 25),
    };

    private static Skin[] Of(string item, params (string Id, string Name, int Price)[] list) =>
        list.Select(x => new Skin(item, x.Id, x.Name, x.Price)).ToArray();

    /// <summary>Всички предмети със скинове (първият е класическият, безплатен).</summary>
    public static readonly Dictionary<string, Skin[]> ByItem = new()
    {
        [Kitchen.Couch] = CouchSkins,
        [Kitchen.Bed] = BedSkins,
        [Kitchen.Heater] = Of(Kitchen.Heater, (Classic, "Чугунена печица", 0), ("fireplace", "Камина", 35),
            ("tiles", "Кахлена печка", 30), ("retro", "Ретро радиатор", 25), ("cozy", "Печка с котлонче", 30)),
        [Kitchen.Lamp] = Of(Kitchen.Lamp, (Classic, "Лампион", 0), ("mushroom", "Лампа гъбка", 20),
            ("lantern", "Фенер", 20), ("moon", "Лампа луна", 25), ("lava", "Лава лампа", 30), ("candle", "Свещник", 15)),
        [Kitchen.Guitar] = Of(Kitchen.Guitar, (Classic, "Акустична китара", 0), ("electric", "Червена електрическа", 35),
            ("ukulele", "Укулеле", 20), ("blue", "Синя китара", 25), ("star", "Китара звезда", 30)),
        [Kitchen.PlantPot] = Of(Kitchen.PlantPot, (Classic, "Слънчоглед", 0), ("monstera", "Монстера", 20), ("cactus", "Кактус", 20)),
        [Kitchen.Desk] = Of(Kitchen.Desk, (Classic, "Бюро със столче", 0), ("oak", "Дъбово бюро с пънче", 20), ("white", "Бяло бюро с кръгло столче", 20),
            ("pink", "Розово бюро с възглавничка", 25), ("drawers", "Бюро с чекмеджета и зелено столче", 30)),
        [Kitchen.Radio] = Of(Kitchen.Radio, (Classic, "Радио", 0), ("gramophone", "Грамофон", 35), ("boombox", "Касетофон", 25),
            ("retro", "Старо лампово радио", 25), ("jukebox", "Джубокс", 40)),
        [Kitchen.Feeder] = Of(Kitchen.Feeder, (Classic, "Дървена хранилка", 0), ("tube", "Хранилка тръба", 15), ("cottage", "Хранилка къщичка", 20),
            ("round", "Кръгла хранилка", 15), ("birdhouse", "Къщичка за птици", 20)),
        [Kitchen.Pot] = Of(Kitchen.Pot, (Classic, "Тенджера", 0), ("enamel", "Емайлирана на точки", 15), ("copper", "Медна тенджера", 20)),
        [Kitchen.Pan] = Of(Kitchen.Pan, (Classic, "Тиган", 0), ("cast_iron", "Чугунен тиган", 15), ("pink", "Розов тиган", 15)),
        [Kitchen.Oven] = Of(Kitchen.Oven, (Classic, "Фурна", 0), ("retro", "Ретро фурна", 25), ("wood", "Дървена пещ", 30)),
        [Kitchen.CoffeeMachine] = Of(Kitchen.CoffeeMachine, (Classic, "Кафе машина", 0), ("moka", "Мока кана", 20), ("retro", "Ретро кафемашина", 25)),
        [Kitchen.Kettle] = Of(Kitchen.Kettle, (Classic, "Чайник", 0), ("whistle", "Свирещ чайник", 15), ("glass", "Стъклен чайник", 20)),
        [Kitchen.Mixer] = Of(Kitchen.Mixer, (Classic, "Миксер", 0), ("mint", "Ментов миксер", 15), ("hand", "Ръчна бъркалка", 15)),
        [Kitchen.SandwichMaker] = Of(Kitchen.SandwichMaker, (Classic, "Сандвичарник", 0), ("red", "Червен сандвичарник", 15), ("waffle", "Гофретник", 20)),
        [Kitchen.IceCream] = Of(Kitchen.IceCream, (Classic, "Машинка за сладолед", 0), ("cart", "Количка за сладолед", 25), ("pink", "Розова машинка", 15)),
        [Kitchen.Grill] = Of(Kitchen.Grill, (Classic, "Скара", 0), ("kettle", "Кръгло барбекю", 20), ("camp", "Огнище", 25)),
    };

    /// <summary>Столчето към бюрото: всеки скин на бюрото идва със свое столче.</summary>
    public static string StoolArt(SaveData save) => Current(save, Kitchen.Desk).Id switch
    {
        "oak" => "stool_log",
        "white" => "stool_round",
        "pink" => "stool_cushion",
        "drawers" => "stool_green",
        _ => "stool",
    };

    public static Skin[] SkinsFor(string item) => ByItem.TryGetValue(item, out var list) ? list : System.Array.Empty<Skin>();

    public static bool Owns(SaveData save, Skin s) => s.Price == 0 || save.Owned.Contains($"skin_{s.Item}_{s.Id}");

    /// <summary>Сложеният скин: избраният, ако е купен; иначе първият (той е винаги наличен).</summary>
    public static Skin Current(SaveData save, string item)
    {
        var all = SkinsFor(item);
        if (all.Length == 0) return new Skin(item, Classic, item, 0);
        string id = save.Skins.TryGetValue(item, out var chosen) ? chosen
            : item == Kitchen.Bed ? save.BedSkin : item == Kitchen.Couch ? save.CouchSkin : Classic;   // стари записи
        return all.FirstOrDefault(s => s.Id == id && Owns(save, s)) ?? all[0];
    }

    /// <summary>Рисунката на предмета със сложения скин (и кадъра: "_b", "_open"…).</summary>
    public static string CurrentArt(SaveData save, string item, string frame = "") => Art(item, Current(save, item).Id, frame);
}
