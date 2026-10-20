using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Mecho;

/// <summary>
/// Къде се облича дрехата. Layer е името на слоя в assets/bear (idle_shirt.png…).
/// Ако има Sprite, дрехата е рисунка assets/clothes/&lt;Sprite&gt;_&lt;Id&gt;.png, която се
/// слага на точката от слоя Layer (шапка, очила, шал); иначе слоят е сив шаблон.
/// </summary>
public sealed record Slot(string Id, string Name, string Icon, string Layer, string? Sprite = null, Anchor Anchor = Anchor.Bottom);

/// <summary>Коя точка на рисунката сяда на точката от слоя: долу (шапка), среда (очила), горе (шал).</summary>
public enum Anchor { Bottom, Center, Top }

public enum Pattern { None, Stripes, Dots, Checks }

/// <summary>Пижама: сет от горнище и долнище (скрити дрехи), купува се цяла.</summary>
public sealed record PajamaSet(string Id, string Name, int Price, string Top, string Pants)
{
    /// <summary>Как се пази в купените дрехи (OwnedClothes).</summary>
    public string Key => "pajama_" + Id;
}

/// <summary>
/// Дреха. Шапката е рисунка assets/clothes/hat_&lt;Id&gt;.png, която сяда на точката
/// от &lt;anim&gt;_head.png. Другите дрехи оцветяват сивия шаблон &lt;anim&gt;_&lt;слот&gt;.png
/// с Main (и Alt за шарката). Ако има готова цветна лента &lt;anim&gt;_&lt;слот&gt;_&lt;Id&gt;.png,
/// се ползва тя: така може да се нарисува и уникална дреха.
/// </summary>
public sealed record Garment(string Id, string Name, string SlotId, int Price, Color Main, Color Alt, Pattern Pattern = Pattern.None)
{
    /// <summary>Не се продава и не стои в гардероба (пижамата).</summary>
    public bool Hidden { get; init; }

    /// <summary>Блуза с дълъг ръкав: оцветява и ръкавите (иначе – къс ръкав, ръцете се виждат).</summary>
    public bool LongSleeves { get; init; }

    /// <summary>Пухкава плетка (пуловери, плетени ръкавички): мек контур и плетка по плата.</summary>
    public bool Fluffy { get; init; }

    /// <summary>Картинката в гардероба: assets/clothes/icons/&lt;вид&gt;.png.</summary>
    public string Kind => SlotId switch
    {
        "shirt" => Fluffy ? "sweater" : LongSleeves ? "blouse" : "tee",
        _ => SlotId,
    };

    public Slot Slot => Clothes.SlotById(SlotId);
}

public static class Clothes
{
    public static readonly Slot Hat = new("hat", "Шапки", "hat", "head", "hat", Anchor.Bottom);
    public static readonly Slot Glasses = new("glasses", "Очила", "glasses", "face", "glasses", Anchor.Center);
    public static readonly Slot Neck = new("neck", "Аксесоари", "scarf", "neck", "neck", Anchor.Top);

    /// <summary>Напитката в лапата, докато пие (не е дреха; рисунка drink_&lt;id&gt;.png на точката _cup).</summary>
    public static readonly Slot Cup = new("cup", "Напитка", "tea", "cup", "drink", Anchor.Bottom);
    /// <summary>Предметът в лапата (четка, лейка) в doing*: рисунка tool_&lt;id&gt;.png, дръжката в средата, на точката _tool.</summary>
    public static readonly Slot Tool = new("tool", "Предмет", "pencil", "tool", "tool", Anchor.Center);
    public static readonly Slot Shirt = new("shirt", "Блузи", "shirt", "shirt");
    public static readonly Slot Pants = new("pants", "Панталони", "pants", "pants");
    public static readonly Slot Shoes = new("shoes", "Обувки", "shoe", "shoes");
    public static readonly Slot Gloves = new("gloves", "Ръкавици", "glove", "gloves");

    /// <summary>В този ред се рисуват (шапката е най-отгоре).</summary>
    public static readonly Slot[] Slots = { Shoes, Pants, Shirt, Gloves, Neck, Glasses, Hat, Cup, Tool };

    /// <summary>В този ред са в гардероба.</summary>
    public static readonly Slot[] Shelves = { Hat, Glasses, Neck, Shirt, Pants, Shoes, Gloves };

    public static Slot SlotById(string id) => Slots.First(s => s.Id == id);

    private static Color C(byte r, byte g, byte b) => Color.FromRgb(r, g, b);

    /// <summary>Всички дрехи: вградените тук и новите от assets/clothes/clothes.json.</summary>
    public static readonly Garment[] All = WithCatalog(new Garment[]
    {
        new("beanie", "Шапка с помпон", "hat", 14, default, default),
        new("beret", "Барета на художник", "hat", 25, default, default),
        new("cap", "Каскет", "hat", 20, default, default),
        new("party", "Парти шапка", "hat", 20, default, default),
        new("chef", "Готварска шапка", "hat", 35, default, default),
        new("crown", "Корона на дизайнера", "hat", 105, default, default),

        // Тениски (къс ръкав).
        new("tee_red", "Червена тениска", "shirt", 10, C(214, 74, 70), C(214, 74, 70)),
        new("tee_white", "Бяла тениска", "shirt", 12, C(245, 245, 240), C(245, 245, 240)),
        new("tee_stripes", "Тениска на райета", "shirt", 20, C(240, 170, 60), C(250, 240, 220), Pattern.Stripes),
        new("tee_pink", "Розова тениска", "shirt", 14, C(240, 150, 170), C(240, 150, 170)),
        new("tee_green_dots", "Зелена тениска на точки", "shirt", 18, C(110, 170, 90), C(250, 240, 200), Pattern.Dots),

        // Блузи (дълъг тънък ръкав).
        new("sailor", "Моряшка блуза", "shirt", 25, C(245, 245, 250), C(60, 90, 170), Pattern.Stripes) { LongSleeves = true },
        new("hoodie", "Зелен суитшърт", "shirt", 30, C(110, 170, 90), C(110, 170, 90)) { LongSleeves = true },
        new("designer", "Лилава блуза на дизайнер", "shirt", 50, C(150, 100, 200), C(255, 209, 102), Pattern.Stripes) { LongSleeves = true },
        new("sweatshirt_gray", "Сив суитшърт", "shirt", 35, C(150, 150, 160), C(150, 150, 160)) { LongSleeves = true },
        new("plaid_shirt", "Риза каре", "shirt", 35, C(70, 110, 180), C(230, 230, 240), Pattern.Checks) { LongSleeves = true },

        // Пуловери (пухкави, дълъг ръкав).
        new("dots", "Жълт пуловер на точки", "shirt", 40, C(250, 200, 80), C(240, 120, 60), Pattern.Dots) { LongSleeves = true, Fluffy = true },
        new("sweater_red", "Червен пуловер", "shirt", 35, C(196, 60, 56), C(196, 60, 56)) { LongSleeves = true, Fluffy = true },
        new("cardigan", "Каре жилетка", "shirt", 45, C(120, 90, 160), C(240, 200, 120), Pattern.Checks) { LongSleeves = true, Fluffy = true },
        new("sweater_cream", "Кремав пухкав пуловер", "shirt", 45, C(245, 232, 205), C(245, 232, 205)) { LongSleeves = true, Fluffy = true },
        new("sweater_honey", "Меден пуловер", "shirt", 45, C(232, 170, 70), C(232, 170, 70)) { LongSleeves = true, Fluffy = true },
        new("sweater_winter", "Зимен пуловер на райета", "shirt", 55, C(200, 55, 60), C(250, 250, 250), Pattern.Stripes) { LongSleeves = true, Fluffy = true },
        new("sweater_lavender", "Лавандулов пуловер", "shirt", 50, C(180, 160, 220), C(180, 160, 220)) { LongSleeves = true, Fluffy = true },
        new("sweater_mint", "Ментов пуловер на точки", "shirt", 50, C(150, 215, 190), C(250, 250, 250), Pattern.Dots) { LongSleeves = true, Fluffy = true },

        new("jeans", "Дънки", "pants", 20, C(70, 100, 170), C(70, 100, 170)),
        new("jeans_blue", "Сини дънки", "pants", 20, C(95, 145, 220), C(95, 145, 220)),
        new("brown", "Кафяви панталонки", "pants", 14, C(120, 80, 50), C(120, 80, 50)),
        new("plaid", "Карирани панталони", "pants", 30, C(200, 70, 70), C(60, 50, 60), Pattern.Checks),
        new("pants_black", "Черни панталони", "pants", 20, C(60, 55, 60), C(60, 55, 60)),
        new("pants_pink_dots", "Розови на точки", "pants", 25, C(240, 160, 180), C(255, 255, 255), Pattern.Dots),
        new("pants_cargo", "Зелени карго", "pants", 25, C(110, 130, 80), C(110, 130, 80)),

        new("sneakers", "Червени гуменки", "shoes", 20, C(220, 60, 60), C(220, 60, 60)),
        new("boots", "Жълти ботуши", "shoes", 25, C(250, 200, 60), C(250, 200, 60)),
        new("slippers", "Розови пантофи", "shoes", 20, C(245, 150, 170), C(255, 255, 255), Pattern.Dots),
        new("sneakers_blue", "Сини гуменки", "shoes", 20, C(70, 110, 200), C(70, 110, 200)),
        new("sneakers_white", "Бели маратонки", "shoes", 25, C(240, 240, 240), C(240, 240, 240)),
        new("shoes_brown", "Кафяви обувки", "shoes", 25, C(110, 70, 40), C(110, 70, 40)),

        new("white", "Бели ръкавички", "gloves", 14, C(250, 250, 250), C(250, 250, 250)),
        new("winter", "Зимни на райета", "gloves", 20, C(90, 140, 210), C(250, 250, 250), Pattern.Stripes) { Fluffy = true },
        new("garden", "Градинарски", "gloves", 20, C(120, 180, 80), C(120, 180, 80)),
        new("gloves_red", "Червени ръкавички", "gloves", 15, C(210, 60, 60), C(210, 60, 60)),
        new("mittens", "Пухкави палчики", "gloves", 25, C(245, 225, 200), C(245, 225, 200)) { Fluffy = true },
        new("gloves_pink_dots", "Розови на точки", "gloves", 20, C(240, 150, 170), C(255, 255, 255), Pattern.Dots),

        // Аксесоари (рисунки на точка): очила при очите, шал/папийонка под брадичката.
        new("round", "Кръгли очила", "glasses", 20, default, default),
        new("sun", "Слънчеви очила", "glasses", 25, default, default),
        new("heart", "Очила сърчица", "glasses", 30, default, default),
        new("artist", "Очила на художник", "glasses", 40, default, default),
        new("gold", "Жълти очила", "glasses", 30, default, default),
        new("scarf_red", "Червен шал", "neck", 20, default, default),
        new("scarf_stripes", "Шал на райета", "neck", 25, default, default),
        new("bowtie", "Папийонка", "neck", 20, default, default),
        new("nut_necklace", "Колие с лешник", "neck", 45, default, default),

        // Пижамите: облича се сама вечер (виж PetWindow.Furniture.cs). Купуват се като сет
        // (PajamaSets), не поотделно, затова горнището и долнището са скрити.
        new("pj_top", "Пижама", "shirt", 0, C(150, 190, 235), C(250, 250, 255), Pattern.Dots) { Hidden = true, LongSleeves = true },
        new("pj_pants", "Пижама", "pants", 0, C(150, 190, 235), C(250, 250, 255), Pattern.Stripes) { Hidden = true },
        new("pj_pink_top", "Пижама", "shirt", 0, C(245, 170, 190), C(255, 255, 255), Pattern.Dots) { Hidden = true, LongSleeves = true },
        new("pj_pink_pants", "Пижама", "pants", 0, C(245, 170, 190), C(245, 170, 190)) { Hidden = true },
        new("pj_stars_top", "Пижама", "shirt", 0, C(50, 60, 120), C(255, 220, 100), Pattern.Dots) { Hidden = true, LongSleeves = true },
        new("pj_stars_pants", "Пижама", "pants", 0, C(50, 60, 120), C(255, 220, 100), Pattern.Dots) { Hidden = true },
        new("pj_honey_top", "Пижама", "shirt", 0, C(250, 205, 90), C(240, 150, 60), Pattern.Checks) { Hidden = true, LongSleeves = true },
        new("pj_honey_pants", "Пижама", "pants", 0, C(250, 205, 90), C(240, 150, 60), Pattern.Checks) { Hidden = true },
        new("pj_mint_top", "Пижама", "shirt", 0, C(150, 215, 190), C(255, 255, 255), Pattern.Stripes) { Hidden = true, LongSleeves = true },
        new("pj_mint_pants", "Пижама", "pants", 0, C(150, 215, 190), C(255, 255, 255), Pattern.Stripes) { Hidden = true },
        new("pj_cloud_top", "Пижама", "shirt", 0, C(245, 240, 230), C(245, 240, 230)) { Hidden = true, LongSleeves = true, Fluffy = true },
        new("pj_cloud_pants", "Пижама", "pants", 0, C(245, 240, 230), C(245, 240, 230)) { Hidden = true },
        new("pj_forest_top", "Пижама", "shirt", 0, C(100, 160, 90), C(60, 100, 55), Pattern.Checks) { Hidden = true, LongSleeves = true },
        new("pj_forest_pants", "Пижама", "pants", 0, C(70, 120, 65), C(70, 120, 65)) { Hidden = true },
        new("nightcap", "Нощна шапка", "hat", 0, default, default) { Hidden = true },

        // Напитките в лапата (виж PetWindow.Drinking.cs).
        new("tea", "Чай", "cup", 0, default, default) { Hidden = true },
        new("coffee", "Кафе", "cup", 0, default, default) { Hidden = true },
        new("juice_orange", "Портокалов сок", "cup", 0, default, default) { Hidden = true },
        new("juice_berry", "Боровинков сок", "cup", 0, default, default) { Hidden = true },
        new("paintbrush_red", "Червена четка", "tool", 0, default, default) { Hidden = true },
        new("paintbrush_blue", "Синя четка", "tool", 0, default, default) { Hidden = true },
        new("wateringcan", "Лейка", "tool", 0, default, default) { Hidden = true },
        new("shovel", "Лопатка", "tool", 0, default, default) { Hidden = true },
        new("scissors", "Ножица", "tool", 0, default, default) { Hidden = true },
        new("knitting", "Куки с прежда", "tool", 0, default, default) { Hidden = true },
    });

    /// <summary>
    /// Нови дрехи без програмиране: assets/clothes/clothes.json (редактира се и от
    /// GitHub). Дреха със същото id заменя вградената. Шапка, очила и шал искат и
    /// рисунка (hat_&lt;id&gt;.png и т.н.); без нея не се показват.
    /// </summary>
    private static Garment[] WithCatalog(Garment[] builtIn)
    {
        var all = builtIn.ToList();
        try
        {
            string path = System.IO.Path.Combine(AppContext.BaseDirectory, "assets", "clothes", "clothes.json");
            if (!System.IO.File.Exists(path)) return builtIn;
            using var doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(path),
                new System.Text.Json.JsonDocumentOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip, AllowTrailingCommas = true });
            foreach (var e in doc.RootElement.GetProperty("garments").EnumerateArray())
            {
                string Str(string name, string fallback = "") => e.TryGetProperty(name, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString()! : fallback;
                string id = Str("id"), slot = Str("slot");
                if (id.Length == 0 || !Slots.Any(x => x.Id == slot)) continue;
                var s = SlotById(slot);
                if (s.Sprite != null && !System.IO.File.Exists(System.IO.Path.Combine(AppContext.BaseDirectory, "assets", "clothes", $"{s.Sprite}_{id}.png"))) continue;
                int price = e.TryGetProperty("price", out var p) && p.TryGetInt32(out int n) ? n : 10;
                var main = Hex(Str("color", "#d64a46"));
                var alt = Hex(Str("color2", Str("color", "#d64a46")));
                var pattern = Str("pattern").ToLowerInvariant() switch
                {
                    "stripes" or "райета" => Pattern.Stripes,
                    "dots" or "точки" => Pattern.Dots,
                    "checks" or "каре" => Pattern.Checks,
                    _ => Pattern.None,
                };
                all.RemoveAll(g => g.Id == id);
                all.Add(new Garment(id, Str("name", id), slot, price, main, alt, pattern)
                {
                    LongSleeves = Str("sleeves").ToLowerInvariant() is "long" or "дълъг",
                    Fluffy = e.TryGetProperty("fluffy", out var fl) && fl.ValueKind == System.Text.Json.JsonValueKind.True,
                });
            }
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);   // счупен файл: остават вградените
        }
        return all.ToArray();
    }

    private static Color Hex(string hex)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex.StartsWith('#') ? hex : "#" + hex); }
        catch { return C(214, 74, 70); }
    }

    /// <summary>Пижамите (сетове): горнище и долнище, с нощната шапка и пантофите. Първата е безплатна.</summary>
    public static readonly PajamaSet[] PajamaSets =
    {
        new("classic", "Синя пижама", 0, "pj_top", "pj_pants"),
        new("pink", "Розова на точки", 20, "pj_pink_top", "pj_pink_pants"),
        new("stars", "Звездна пижама", 25, "pj_stars_top", "pj_stars_pants"),
        new("honey", "Медено каре", 25, "pj_honey_top", "pj_honey_pants"),
        new("mint", "Ментова на райе", 20, "pj_mint_top", "pj_mint_pants"),
        new("cloud", "Пухкава облачна", 35, "pj_cloud_top", "pj_cloud_pants"),
        new("forest", "Горска каре", 25, "pj_forest_top", "pj_forest_pants"),
    };

    public static PajamaSet PajamaById(string? id) => PajamaSets.FirstOrDefault(p => p.Id == id) ?? PajamaSets[0];

    public static bool Owns(SaveData save, PajamaSet p) => p.Price == 0 || save.OwnedClothes.Contains(p.Key);

    /// <summary>Избраната пижама, ако е купена (иначе синята).</summary>
    public static PajamaSet CurrentPajama(SaveData save) => PajamaById(save.Pajama) is var p && Owns(save, p) ? p : PajamaSets[0];

    /// <summary>Пижамата като дрехи (слот → дреха), които сменят облеклото вечер.</summary>
    public static Dictionary<string, string> PajamaOutfit(PajamaSet p) => new()
    {
        ["shirt"] = p.Top, ["pants"] = p.Pants, ["hat"] = "nightcap", ["shoes"] = "slippers",
    };

    public static Garment? Find(string? id) => id == null ? null : All.FirstOrDefault(g => g.Id == id) ?? GuitarTool(id);

    /// <summary>Китарата в лапите: guitar, guitar_electric… (рисунка tool_&lt;id&gt;.png, по една на скин).</summary>
    private static readonly Dictionary<string, Garment> Guitars = new();

    private static Garment? GuitarTool(string id)
    {
        if (!id.StartsWith("guitar")) return null;
        lock (Guitars)
        {
            if (!Guitars.TryGetValue(id, out var g)) Guitars[id] = g = new Garment(id, "Китара", "tool", 0, default, default) { Hidden = true };
            return g;
        }
    }

    public static bool Owns(SaveData save, Garment g) => save.OwnedClothes.Contains(g.Id);

    public static Garment? Worn(SaveData save, Slot slot) =>
        save.Outfit.TryGetValue(slot.Id, out var id) ? Find(id) : null;

    public static bool IsWorn(SaveData save, Garment g) => Worn(save, g.Slot) == g;
}

/// <summary>
/// Облича кадрите: върху мечока слага оцветените шаблони и шапката.
/// Кадърът е по-висок с HatRoom пиксела отгоре, за да има място за шапката.
/// Готовите кадри се пазят, докато не се смени облеклото.
/// </summary>
public sealed class Dresser
{
    public const int HatRoom = 12;

    /// <summary>Анимации, в които лапите държат нещо пред гърдите: без шал/папийонка/колие.</summary>
    /// <summary>Анимации с предмет в лапата (точката _tool): doing*, knit*, guitar*.</summary>
    public static bool HoldsTool(string anim) => anim.StartsWith("doing") || anim.StartsWith("knit") || anim.StartsWith("guitar");

    public static readonly HashSet<string> HandsBusy = new()
    {
        "knit", "knit_tired", "guitar", "guitar_tired",
        "read", "read_sleep", "game", "game_tired", "write", "write_tired", "write_sit", "write_sit_tired", "dice_sit_boardgame", "dice_sit_boardgame_tired", "focus", "focus_tired",
        "focus_write", "doing", "doing_tired", "doing_sit", "doing_sit_tired", "eat", "eat_hold",
        "sit_drinking", "sit_drinking_sip", "sit_drinking_blow", "cook", "cook_tired", "dice", "hold", "hold_walk",
    };

    // Готовите кадри (мечокът с дрехите) – по облекло. Пазим няколко облекла: иначе
    // всяка смяна (чашата в лапата при пиене, пижамата…) изтриваше всичко и
    // следващите кадри се рисуваха наново точно когато трябва да се покажат
    // (накъсване/премигване).
    private readonly Dictionary<string, Dictionary<(string, int), BitmapSource>> _caches = new();
    private readonly List<string> _recent = new();
    private Dictionary<(string, int), BitmapSource> _cache = new();
    private SpriteLibrary? _lib;
    private string _key = "";

    public BitmapSource Frame(SpriteLibrary lib, string anim, int index, IReadOnlyDictionary<string, string> outfit)
    {
        // Ключът за облеклото се прави наново само ако нещо се е сменило (иначе
        // всеки кадър правеше нов низ с LINQ).
        string key = SameOutfit(outfit) ? _lastKey : MakeKey(outfit);
        if (!ReferenceEquals(lib, _lib))
        {
            _previews.Clear();
            _caches.Clear();
            _recent.Clear();
            _lib = lib;
            _key = "";
        }
        if (key != _key)
        {
            if (!_caches.TryGetValue(key, out var c))
            {
                c = new Dictionary<(string, int), BitmapSource>();
                _caches[key] = c;
            }
            _cache = c;
            _key = key;
            _recent.Remove(key);
            _recent.Add(key);
            while (_recent.Count > 6)
            {
                _caches.Remove(_recent[0]);
                _recent.RemoveAt(0);
            }
        }
        if (_cache.TryGetValue((anim, index), out var done)) return done;
        done = Dress(lib, anim, index, outfit, Book);
        if (_cache.Count > 400) _cache.Clear();
        _cache[(anim, index)] = done;
        return done;
    }

    private string?[] _lastValues = Array.Empty<string?>();
    private string? _lastBook;
    private string _lastKey = "";

    private bool SameOutfit(IReadOnlyDictionary<string, string> outfit)
    {
        var slots = Clothes.Slots;
        if (_lastValues.Length != slots.Length || _lastBook != Book) return false;
        for (int i = 0; i < slots.Length; i++)
        {
            outfit.TryGetValue(slots[i].Id, out var id);
            if (id != _lastValues[i]) return false;
        }
        return true;
    }

    private string MakeKey(IReadOnlyDictionary<string, string> outfit)
    {
        var slots = Clothes.Slots;
        _lastValues = new string?[slots.Length];
        for (int i = 0; i < slots.Length; i++)
        {
            outfit.TryGetValue(slots[i].Id, out var id);
            _lastValues[i] = id;
        }
        _lastBook = Book;
        _lastKey = string.Join(",", _lastValues.Select(v => v ?? "")) + "|" + Book;
        return _lastKey;
    }

    private readonly Dictionary<string, BitmapSource?> _previews = new();

    /// <summary>
    /// Само дрехата, без мечока (за гардероба): шапката е нейната рисунка, а
    /// другите са оцветеният шаблон от първия кадър на „стои“, изрязан плътно.
    /// </summary>
    public BitmapSource? Preview(SpriteLibrary lib, Garment g)
    {
        if (!ReferenceEquals(lib, _lib))
        {
            _caches.Clear();
            _recent.Clear();
            _cache = new Dictionary<(string, int), BitmapSource>();
            _key = "";
            _previews.Clear();
            _lib = lib;
        }
        if (_previews.TryGetValue(g.Id, out var done)) return done;
        done = MakePreview(lib, g);
        _previews[g.Id] = done;
        return done;
    }

    private static BitmapSource? MakePreview(SpriteLibrary lib, Garment g)
    {
        var slot = g.Slot;
        if (slot.Sprite != null) return lib.Clothes(slot.Sprite + "_" + g.Id);
        // Своя картинка за гардероба: assets/clothes/icons/<вид>.png, оцветена като дрехата.
        if (lib.Clothes("icons/" + g.Kind) is { } icon)
        {
            int iw = icon.PixelWidth, ih = icon.PixelHeight;
            var ipx = new uint[iw * (ih + HatRoom)];
            Paint(ipx, iw, Pixels(icon), iw, ih, g);
            var iconFull = BitmapSource.Create(iw, ih + HatRoom, 96, 96, PixelFormats.Bgra32, null, ipx, iw * 4);
            var iconCrop = new CroppedBitmap(iconFull, new System.Windows.Int32Rect(0, HatRoom, iw, ih));
            iconCrop.Freeze();
            return iconCrop;
        }
        var own = Pick(lib.Layer("idle", slot.Layer + "_" + g.Id), 0);
        var mask = own ?? Pick(lib.Layer("idle", slot.Layer), 0);
        if (mask == null) return null;
        int w = mask.PixelWidth, h = mask.PixelHeight;
        var px = new uint[w * (h + HatRoom)];
        if (own != null) Blit(px, w, h + HatRoom, Pixels(own), w, h, 0, HatRoom);
        else Paint(px, w, Pixels(mask), w, h, g);

        // Плътно изрязване около дрехата.
        int minX = w, minY = h + HatRoom, maxX = -1, maxY = -1;
        for (int i = 0; i < px.Length; i++)
        {
            if (px[i] >> 24 == 0) continue;
            int x = i % w, y = i / w;
            minX = Math.Min(minX, x);
            maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y);
            maxY = Math.Max(maxY, y);
        }
        if (maxX < 0) return null;
        var full = BitmapSource.Create(w, h + HatRoom, 96, 96, PixelFormats.Bgra32, null, px, w * 4);
        var crop = new CroppedBitmap(full, new System.Windows.Int32Rect(minX, minY, maxX - minX + 1, maxY - minY + 1));
        crop.Freeze();
        return crop;
    }

    /// <summary>
    /// Книгата, която чете (book_1 … book_6 в assets/clothes): рисува се най-отгоре,
    /// над шала и дрехите, където я има в кадъра (точката &lt;anim&gt;_book.png).
    /// </summary>
    public string? Book { get; set; }

    /// <summary>Слоевете на ръцете и краката (от картата на дрехите), които са над аксесоарите.</summary>
    private static readonly string[] LimbLayers = { "sleeves", "gloves", "shoes", "holes" };

    private static BitmapSource Dress(SpriteLibrary lib, string anim, int index, IReadOnlyDictionary<string, string> outfit, string? book)
    {
        var frames = lib.Get(anim).Frames;
        var baseFrame = frames[index % frames.Length];
        int w = baseFrame.PixelWidth, h = baseFrame.PixelHeight, H = h + HatRoom;
        var px = new uint[w * H];
        Blit(px, w, H, Pixels(baseFrame), w, h, 0, HatRoom);
        uint[]? under = null;
        bool frontDone = false;

        // Ръцете и краката – над шала, очилата и шапката: това, което картата на дрехите
        // води за ръкав (тъмночервено), ръкавица (зелено) и обувка (жълто) – с дреха или
        // без (само с шал лапата пак е пред него) – и „дупките“ (<anim>_holes.png): неоцветеното
        // или в непознат цвят под врата – книга, лъжица… Сивото „отпред“ не изрязва.
        void RestoreFront()
        {
            if (frontDone) return;
            frontDone = true;
            if (under == null) return;
            foreach (var layer in LimbLayers)
            {
                if (Pick(lib.Layer(anim, layer), index) is not { } limb) continue;
                var limbPx = Pixels(limb);
                int lw = limb.PixelWidth;
                for (int i = 0; i < limbPx.Length; i++)
                {
                    if (limbPx[i] >> 24 == 0) continue;
                    int x = i % lw, y = i / lw;
                    if (x < w && y + HatRoom < H) px[(y + HatRoom) * w + x] = under[(y + HatRoom) * w + x];
                }
            }
        }

        foreach (var slot in Clothes.Slots)
        {
            if (!outfit.TryGetValue(slot.Id, out var id) || Clothes.Find(id) is not { } g || g.SlotId != slot.Id) continue;
            // Лапите държат нещо пред гърдите (книжка, джойстик, молив, чаша, четка,
            // лейка…): шалът, папийонката и колието не се рисуват – иначе са върху предмета.
            if (slot == Clothes.Neck && (HandsBusy.Contains(anim) || lib.Layer(anim, "book") != null)) continue;
            // Чашата е най-отгоре: първо ръцете отпред се връщат над аксесоарите.
            // Предметът в лапите (четка, лейка, китара) също: иначе лапата отпред го покрива.
            if (slot == Clothes.Cup || slot == Clothes.Tool) RestoreFront();
            if (slot.Sprite != null)
            {
                // Рисунка на точка: шапка (долният ѝ ред на точката), очила (средата),
                // шал (горният ред). Без точка в тази анимация (сън) не се слага.
                var art = lib.Clothes(slot.Sprite + "_" + g.Id);
                var point = Pick(lib.Layer(anim, slot.Layer), index);
                if (art == null || point == null) continue;
                var anchor = FirstPixel(Pixels(point), point.PixelWidth, point.PixelHeight);
                if (anchor == null) continue;
                var (ax, ay) = anchor.Value;
                int top = slot.Anchor switch
                {
                    Anchor.Bottom => ay - art.PixelHeight + 1,
                    Anchor.Center => ay - art.PixelHeight / 2,
                    _ => ay,
                };
                // Шал, очила и шапка са под ръцете: лапа пред тялото (слоят
                // <anim>_front.png) остава отгоре. Снимката е от преди първия аксесоар.
                if (slot != Clothes.Cup && slot != Clothes.Tool && !frontDone) under ??= (uint[])px.Clone();
                Blit(px, w, H, Pixels(art), art.PixelWidth, art.PixelHeight, ax - art.PixelWidth / 2, top + HatRoom);
                continue;
            }

            // Нарисувана дреха (цветна) или сив шаблон, който оцветяваме.
            var own = Pick(lib.Layer(anim, slot.Layer + "_" + g.Id), index);
            if (own != null)
            {
                Blit(px, w, H, Pixels(own), own.PixelWidth, own.PixelHeight, 0, HatRoom);
                continue;
            }
            var mask = Pick(lib.Layer(anim, slot.Layer), index);
            if (mask == null) continue;
            var maskPx = Pixels(mask);
            // Дълъг ръкав: и ръкавите (тъмночервеното от картата) са в цвета на блузата.
            if (g.LongSleeves && Pick(lib.Layer(anim, "sleeves"), index) is { } sleeves)
            {
                var sleevePx = Pixels(sleeves);
                for (int i = 0; i < Math.Min(maskPx.Length, sleevePx.Length); i++)
                    if (sleevePx[i] >> 24 != 0) maskPx[i] = sleevePx[i];
            }
            var (refX, refY) = Pick(lib.Layer(anim, "neck"), index) is { } neck && FirstPoint(neck) is var (nx, ny) ? (nx, ny) : (0, 0);
            Paint(px, w, maskPx, mask.PixelWidth, mask.PixelHeight, g, refX, refY);
        }

        RestoreFront();

        if (book != null && lib.Clothes(book) is { } bookArt && Pick(lib.Layer(anim, "book"), index) is { } bookPoint
            && FirstPixel(Pixels(bookPoint), bookPoint.PixelWidth, bookPoint.PixelHeight) is var (bx, by))
            Blit(px, w, H, Pixels(bookArt), bookArt.PixelWidth, bookArt.PixelHeight, bx, by + HatRoom);

        var bmp = BitmapSource.Create(w, H, 96, 96, PixelFormats.Bgra32, null, px, w * 4);
        bmp.Freeze();
        return bmp;
    }

    private static BitmapSource? Pick(BitmapSource[]? strip, int index) =>
        strip == null || strip.Length == 0 ? null : strip[index % strip.Length];

    /// <summary>Сивият шаблон става дреха: под 80 контур, до 135 сянка, после цветът, над 200 отблясък.</summary>
    /// <summary>
    /// Оцветява сивия шаблон. Шарката се брои от (refX, refY) – точката на врата
    /// в кадъра: върви с тялото, но не подскача, когато ръката се вдигне или
    /// гърдите мръднат (преди се броеше от най-горния пиксел на дрехата).
    /// </summary>
    private static void Paint(uint[] px, int w, uint[] mask, int mw, int mh, Garment g, int refX = 0, int refY = 0)
    {
        int top = refY;

        for (int y = 0; y < mh; y++)
        {
            for (int x = 0; x < Math.Min(mw, w); x++)
            {
                uint m = mask[y * mw + x];
                if (m >> 24 == 0) continue;
                int gray = (int)((m >> 16) & 0xFF);
                int ry = y - top + 64, rx = x - refX + 64;   // +64: винаги положително
                bool alt = g.Pattern switch
                {
                    Pattern.Stripes => ry / 2 % 2 == 1,
                    Pattern.Dots => ry % 3 == 1 && rx % 3 == 1,
                    Pattern.Checks => (rx / 2 + ry / 2) % 2 == 1,
                    _ => false,
                };
                var c = Shade(g, alt ? g.Alt : g.Main, gray, rx, ry);
                px[(y + HatRoom) * w + x] = 0xFF000000u | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
            }
        }
    }

    /// <summary>Цветът на пиксел от шаблона: контур, сянка, плат, отблясък; пухкавото – с плетка.</summary>
    private static Color Shade(Garment g, Color c, int gray, int rx, int ry)
    {
        var dark = Color.FromRgb(40, 25, 15);
        if (g.Fluffy)
        {
            // Пухкаво: по-мек контур, а платът е на бримки (светли и сенчести точици).
            if (gray < 80) return Mix(c, dark, (rx + ry) % 2 == 0 ? 0.45 : 0.3);
            if (gray < 135) return Mix(c, dark, 0.2);
            if (gray > 200) return Mix(c, Colors.White, 0.35);
            int knit = (rx + 2 * ry) % 4;
            return knit == 0 ? Mix(c, Colors.White, 0.18) : knit == 2 ? Mix(c, dark, 0.1) : c;
        }
        if (gray < 80) return Mix(c, dark, 0.6);          // контур
        if (gray < 135) return Mix(c, dark, 0.25);        // сянка
        if (gray > 200) return Mix(c, Colors.White, 0.3); // отблясък
        return c;
    }

    private static Color Mix(Color a, Color b, double t) => Color.FromRgb(
        (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

    /// <summary>Първият непрозрачен пиксел на точков слой (шапка, чаша, храна).</summary>
    public static (int X, int Y)? FirstPoint(BitmapSource layer) =>
        FirstPixel(Pixels(layer), layer.PixelWidth, layer.PixelHeight);

    private static (int, int)? FirstPixel(uint[] px, int w, int h)
    {
        for (int i = 0; i < w * h; i++)
            if (px[i] >> 24 != 0) return (i % w, i / w);
        return null;
    }

    /// <summary>Слага рисунката върху кадъра (прозрачното не се рисува).</summary>
    private static void Blit(uint[] dst, int dw, int dh, uint[] src, int sw, int sh, int left, int top)
    {
        for (int y = 0; y < sh; y++)
        {
            int dy = y + top;
            if (dy < 0 || dy >= dh) continue;
            for (int x = 0; x < sw; x++)
            {
                int dx = x + left;
                if (dx < 0 || dx >= dw) continue;
                uint s = src[y * sw + x];
                uint a = s >> 24;
                if (a == 0) continue;
                if (a == 255)
                {
                    dst[dy * dw + dx] = s;
                    continue;
                }
                uint d = dst[dy * dw + dx];
                uint da = d >> 24;
                uint oa = a + da * (255 - a) / 255;
                if (oa == 0) continue;
                uint Ch(int shift) =>
                    (((s >> shift) & 0xFF) * a + ((d >> shift) & 0xFF) * da * (255 - a) / 255) / oa;
                dst[dy * dw + dx] = (oa << 24) | (Ch(16) << 16) | (Ch(8) << 8) | Ch(0);
            }
        }
    }

    private static uint[] Pixels(BitmapSource src)
    {
        var bgra = src.Format == PixelFormats.Bgra32 ? src : new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
        var px = new uint[bgra.PixelWidth * bgra.PixelHeight];
        bgra.CopyPixels(px, bgra.PixelWidth * 4, 0);
        return px;
    }
}
