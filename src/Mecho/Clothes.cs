using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Mecho;

/// <summary>Къде се облича дрехата. Name е и името на слоя в assets/bear (idle_shirt.png…).</summary>
public sealed record Slot(string Id, string Name, string Icon, string Layer);

public enum Pattern { None, Stripes, Dots, Checks }

/// <summary>
/// Дреха. Шапката е рисунка assets/clothes/hat_&lt;Id&gt;.png, която сяда на точката
/// от &lt;anim&gt;_head.png. Другите дрехи оцветяват сивия шаблон &lt;anim&gt;_&lt;слот&gt;.png
/// с Main (и Alt за шарката). Ако има готова цветна лента &lt;anim&gt;_&lt;слот&gt;_&lt;Id&gt;.png,
/// се ползва тя: така може да се нарисува и уникална дреха.
/// </summary>
public sealed record Garment(string Id, string Name, string SlotId, int Price, Color Main, Color Alt, Pattern Pattern = Pattern.None)
{
    public Slot Slot => Clothes.SlotById(SlotId);
}

public static class Clothes
{
    public static readonly Slot Hat = new("hat", "Шапки", "hat", "head");
    public static readonly Slot Shirt = new("shirt", "Блузи", "shirt", "shirt");
    public static readonly Slot Pants = new("pants", "Панталони", "pants", "pants");
    public static readonly Slot Shoes = new("shoes", "Обувки", "shoe", "shoes");
    public static readonly Slot Gloves = new("gloves", "Ръкавици", "glove", "gloves");

    /// <summary>В този ред се рисуват (шапката е най-отгоре).</summary>
    public static readonly Slot[] Slots = { Shoes, Pants, Shirt, Gloves, Hat };

    /// <summary>В този ред са в гардероба.</summary>
    public static readonly Slot[] Shelves = { Hat, Shirt, Pants, Shoes, Gloves };

    public static Slot SlotById(string id) => Slots.First(s => s.Id == id);

    private static Color C(byte r, byte g, byte b) => Color.FromRgb(r, g, b);

    public static readonly Garment[] All =
    {
        new("beanie", "Шапка с помпон", "hat", 8, default, default),
        new("beret", "Барета на художник", "hat", 15, default, default),
        new("cap", "Каскет", "hat", 10, default, default),
        new("party", "Парти шапка", "hat", 12, default, default),
        new("chef", "Готварска шапка", "hat", 20, default, default),
        new("crown", "Корона на дизайнера", "hat", 60, default, default),

        new("tee_red", "Червена тениска", "shirt", 6, C(214, 74, 70), C(214, 74, 70)),
        new("sailor", "Моряшка блуза", "shirt", 14, C(245, 245, 250), C(60, 90, 170), Pattern.Stripes),
        new("hoodie", "Зелен суитшърт", "shirt", 18, C(110, 170, 90), C(110, 170, 90)),
        new("dots", "Жълт пуловер на точки", "shirt", 22, C(250, 200, 80), C(240, 120, 60), Pattern.Dots),
        new("designer", "Лилава блуза на дизайнер", "shirt", 30, C(150, 100, 200), C(255, 209, 102), Pattern.Stripes),

        new("jeans", "Дънки", "pants", 10, C(70, 100, 170), C(70, 100, 170)),
        new("brown", "Кафяви панталонки", "pants", 8, C(120, 80, 50), C(120, 80, 50)),
        new("plaid", "Карирани панталони", "pants", 18, C(200, 70, 70), C(60, 50, 60), Pattern.Checks),

        new("sneakers", "Червени гуменки", "shoes", 10, C(220, 60, 60), C(220, 60, 60)),
        new("boots", "Жълти ботуши", "shoes", 14, C(250, 200, 60), C(250, 200, 60)),
        new("slippers", "Розови пантофи", "shoes", 12, C(245, 150, 170), C(255, 255, 255), Pattern.Dots),

        new("white", "Бели ръкавички", "gloves", 8, C(250, 250, 250), C(250, 250, 250)),
        new("winter", "Зимни на райета", "gloves", 12, C(90, 140, 210), C(250, 250, 250), Pattern.Stripes),
        new("garden", "Градинарски", "gloves", 10, C(120, 180, 80), C(120, 180, 80)),
    };

    public static Garment? Find(string? id) => id == null ? null : All.FirstOrDefault(g => g.Id == id);

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

    private readonly Dictionary<(string, int), BitmapSource> _cache = new();
    private SpriteLibrary? _lib;
    private string _key = "";

    public BitmapSource Frame(SpriteLibrary lib, string anim, int index, IReadOnlyDictionary<string, string> outfit)
    {
        string key = string.Join(",", Clothes.Slots.Select(s => outfit.TryGetValue(s.Id, out var id) ? id : ""));
        if (!ReferenceEquals(lib, _lib) || key != _key)
        {
            _cache.Clear();
            _lib = lib;
            _key = key;
        }
        if (_cache.TryGetValue((anim, index), out var done)) return done;
        done = Dress(lib, anim, index, outfit);
        if (_cache.Count > 400) _cache.Clear();
        _cache[(anim, index)] = done;
        return done;
    }

    /// <summary>Една дреха върху мечока (за плочките в гардероба).</summary>
    public static BitmapSource Preview(SpriteLibrary lib, IReadOnlyDictionary<string, string> outfit) =>
        Dress(lib, "idle", 0, outfit);

    private static BitmapSource Dress(SpriteLibrary lib, string anim, int index, IReadOnlyDictionary<string, string> outfit)
    {
        var frames = lib.Get(anim).Frames;
        var baseFrame = frames[index % frames.Length];
        int w = baseFrame.PixelWidth, h = baseFrame.PixelHeight, H = h + HatRoom;
        var px = new uint[w * H];
        Blit(px, w, H, Pixels(baseFrame), w, h, 0, HatRoom);

        foreach (var slot in Clothes.Slots)
        {
            if (!outfit.TryGetValue(slot.Id, out var id) || Clothes.Find(id) is not { } g || g.SlotId != slot.Id) continue;
            if (slot == Clothes.Hat)
            {
                var hat = lib.Clothes("hat_" + g.Id);
                var head = Pick(lib.Layer(anim, slot.Layer), index);
                if (hat == null || head == null) continue;
                var anchor = FirstPixel(Pixels(head), head.PixelWidth, head.PixelHeight);
                if (anchor == null) continue;
                var (ax, ay) = anchor.Value;
                // Средата на долния ред на шапката сяда на точката.
                Blit(px, w, H, Pixels(hat), hat.PixelWidth, hat.PixelHeight,
                    ax - hat.PixelWidth / 2, ay + HatRoom - hat.PixelHeight + 1);
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
            Paint(px, w, Pixels(mask), mask.PixelWidth, mask.PixelHeight, g);
        }

        var bmp = BitmapSource.Create(w, H, 96, 96, PixelFormats.Bgra32, null, px, w * 4);
        bmp.Freeze();
        return bmp;
    }

    private static BitmapSource? Pick(BitmapSource[]? strip, int index) =>
        strip == null || strip.Length == 0 ? null : strip[index % strip.Length];

    /// <summary>Сивият шаблон става дреха: тъмното е контур, средното е цветът, светлото е отблясък.</summary>
    private static void Paint(uint[] px, int w, uint[] mask, int mw, int mh, Garment g)
    {
        int top = int.MaxValue;
        for (int i = 0; i < mask.Length && top == int.MaxValue; i++)
            if (mask[i] >> 24 != 0) top = i / mw;
        if (top == int.MaxValue) return;

        for (int y = 0; y < mh; y++)
        {
            for (int x = 0; x < Math.Min(mw, w); x++)
            {
                uint m = mask[y * mw + x];
                if (m >> 24 == 0) continue;
                int gray = (int)((m >> 16) & 0xFF);
                int ry = y - top;   // шарката върви с дрехата, а не с кадъра
                bool alt = g.Pattern switch
                {
                    Pattern.Stripes => ry / 2 % 2 == 1,
                    Pattern.Dots => ry % 3 == 1 && x % 3 == 1,
                    Pattern.Checks => (x / 2 + ry / 2) % 2 == 1,
                    _ => false,
                };
                var c = alt ? g.Alt : g.Main;
                if (gray < 100) c = Mix(c, Color.FromRgb(40, 25, 15), 0.6);
                else if (gray > 200) c = Mix(c, Colors.White, 0.3);
                px[(y + HatRoom) * w + x] = 0xFF000000u | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
            }
        }
    }

    private static Color Mix(Color a, Color b, double t) => Color.FromRgb(
        (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

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
