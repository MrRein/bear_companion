using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Mecho;

/// <summary>
/// Пикселните ленти (ситост, енергия) и малките ленти на храната – рисувани пиксел по пиксел
/// като рисунката на Тут: обща черна рамка със заоблени ъгли, звената делят по една черта,
/// всяко звено има светъл горен ред, среден и тъмен долен; половинка – запълнена отляво.
///   Голямо звено – 4×4 вътре; малко (на храната) – 2×2, половинката е 1 пиксел широка.
/// Цветът на пълните звена – по нивото (1 – червено … 10 – зелено); празните – сиво-бежови.
/// </summary>
public static class PixelBar
{
    public readonly record struct Tone(Color Light, Color Mid, Color Dark);

    private static Color C(byte r, byte g, byte b) => Color.FromRgb(r, g, b);

    public static readonly Color Outline = C(13, 18, 28);
    public static readonly Tone Empty = new(C(230, 216, 192), C(214, 190, 150), C(174, 156, 126));   // като старите празни звена
    public static readonly Tone Good = new(C(178, 215, 155), C(126, 188, 89), C(103, 154, 77));
    public static readonly Tone Okay = new(C(246, 210, 144), C(240, 180, 70), C(195, 148, 62));
    public static readonly Tone Honey = new(C(255, 227, 163), C(255, 209, 102), C(207, 171, 87));
    public static readonly Tone Bad = new(C(234, 156, 150), C(220, 90, 80), C(179, 76, 70));

    /// <summary>Цветът на ниво 1–10: червено → жълто → зелено.</summary>
    public static Tone Level(int level)
    {
        double t = (Math.Clamp(level, 1, 10) - 1) / 9.0;
        var (a, b, k) = t < 0.5 ? (Bad, Okay, t * 2) : (Okay, Good, (t - 0.5) * 2);
        Color L(Color x, Color y) => Color.FromRgb((byte)Math.Round(x.R + (y.R - x.R) * k), (byte)Math.Round(x.G + (y.G - x.G) * k), (byte)Math.Round(x.B + (y.B - x.B) * k));
        return new Tone(L(a.Light, b.Light), L(a.Mid, b.Mid), L(a.Dark, b.Dark));
    }

    private static readonly Dictionary<string, BitmapSource> Cache = new();

    /// <summary>
    /// Голяма лента: cells звена, halves – колко половинки са пълни (0…2·cells). Пълните – в тона fill,
    /// останалите – празни.
    /// </summary>
    public static BitmapSource Big(int halves, int cells, Tone fill) => RenderFramed(halves, cells, fill.Mid);

    /// <summary>Кафявата рамка и преградите (стилът от рисунката на Тут).</summary>
    public static readonly Color Wood = C(110, 45, 2);

    /// <summary>Тонът на звено от основен цвят (както на рисунката: светла ивица, основа, сянка, по-светъл ръб отдолу).</summary>
    private static (Color Base, Color Hi, Color Shade, Color Rim) Shades(Color c) =>
        (c, Hsl(c, 14, 0.22, -0.09), Hsl(c, -9, -0.07, 0), Hsl(c, -7, 0.02, 0));

    /// <summary>Отмества цвета в HSL: оттенък (градуси), светлина и наситеност (−1…1).</summary>
    private static Color Hsl(Color c, double dh, double dl, double ds)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), l = (max + min) / 2, h = 0, sat = 0;
        if (max != min)
        {
            double d = max - min;
            sat = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            h = max == r ? (g - b) / d + (g < b ? 6 : 0) : max == g ? (b - r) / d + 2 : (r - g) / d + 4;
            h /= 6;
        }
        h = ((h + dh / 360) % 1 + 1) % 1;
        l = Math.Clamp(l + dl, 0, 1);
        sat = Math.Clamp(sat + ds, 0, 1);
        double Hue(double p, double q, double t)
        {
            t = (t % 1 + 1) % 1;
            return t < 1 / 6.0 ? p + (q - p) * 6 * t : t < 0.5 ? q : t < 2 / 3.0 ? p + (q - p) * (2 / 3.0 - t) * 6 : p;
        }
        double q2 = l < 0.5 ? l * (1 + sat) : l + sat - l * sat, p2 = 2 * l - q2;
        return sat == 0 ? Color.FromRgb((byte)(l * 255), (byte)(l * 255), (byte)(l * 255))
            : Color.FromRgb((byte)Math.Round(Hue(p2, q2, h + 1 / 3.0) * 255), (byte)Math.Round(Hue(p2, q2, h) * 255), (byte)Math.Round(Hue(p2, q2, h - 1 / 3.0) * 255));
    }

    /// <summary>
    /// Голямата лента в рамка: кафява рамка и прегради, звено 4×7 вътре – ред 1 основа, ред 2 светла
    /// ивица, редове 3–4 основа, 5–6 сянка, 7 по-светъл ръб. Празните – бежови в същия ред.
    /// Половинка – лявата половина на звеното.
    /// </summary>
    private static BitmapSource RenderFramed(int halves, int cells, Color baseColor)
    {
        cells = Math.Max(1, cells);
        halves = Math.Clamp(halves, 0, cells * 2);
        string key = $"framed|{halves}|{cells}|{baseColor}";
        if (Cache.TryGetValue(key, out var hit)) return hit;
        const int inner = 4, rows = 7;
        int w = 1 + cells * (inner + 1), h = rows + 2;
        var full = Shades(baseColor);
        var empty = (Base: C(214, 190, 150), Hi: C(232, 218, 194), Shade: C(194, 168, 128), Rim: C(205, 182, 142));
        var bytes = new byte[w * h * 4];
        void Put(int x, int y, Color c) { int o = (y * w + x) * 4; bytes[o] = c.B; bytes[o + 1] = c.G; bytes[o + 2] = c.R; bytes[o + 3] = 255; }
        for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) Put(x, y, Wood);
        for (int i = 0; i < cells; i++)
        {
            int filledCols = halves >= (i + 1) * 2 ? inner : halves == i * 2 + 1 ? inner / 2 : 0;
            for (int dx = 0; dx < inner; dx++)
            {
                var t = dx < filledCols ? full : empty;
                for (int dy = 0; dy < rows; dy++)
                    Put(1 + i * (inner + 1) + dx, 1 + dy, dy == 1 ? t.Hi : dy <= 3 ? t.Base : dy <= 5 ? t.Shade : t.Rim);
            }
        }
        var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, bytes, w * 4);
        bmp.Freeze();
        return Cache[key] = bmp;
    }


    /// <summary>Малка лента (на храната): само пълните звена; половинката – тясно звено (1 пиксел).</summary>
    public static BitmapSource Small(int halves, Tone fill) => Render(halves, (halves + 1) / 2, fill, small: true);

    private static BitmapSource Render(int halves, int cells, Tone fill, bool small)
    {
        cells = Math.Max(1, cells);
        halves = Math.Clamp(halves, 0, cells * 2);
        string key = $"{small}|{halves}|{cells}|{fill.Light}{fill.Mid}{fill.Dark}";
        if (Cache.TryGetValue(key, out var hit)) return hit;

        int inner = small ? 2 : 4, h = small ? 4 : 6;
        // Ширини на звената (малката половинка – 1 пиксел).
        var widths = new int[cells];
        for (int i = 0; i < cells; i++) widths[i] = small && halves == i * 2 + 1 ? 1 : inner;
        int w = 1;
        foreach (int cw in widths) w += cw + 1;
        // Голямата има отдолу мека сянка (ред с полупрозрачно тъмно).
        int shadow = small ? 0 : 1;
        var px = new Color?[w, h + shadow];
        // Рамка и прегради.
        for (int x = 0; x < w; x++) { px[x, 0] = Outline; px[x, h - 1] = Outline; }
        int cx = 0;
        for (int i = 0; i <= cells; i++)
        {
            for (int y = 0; y < h; y++) px[cx, y] = Outline;
            if (i < cells) cx += widths[i] + 1;
        }
        // Бледият тон (започнато звено – половинка на голямата лента).
        static Color Mix(Color a, Color b, double k) => Color.FromRgb((byte)(a.R + (b.R - a.R) * k), (byte)(a.G + (b.G - a.G) * k), (byte)(a.B + (b.B - a.B) * k));
        var pale = new Tone(Mix(fill.Light, Empty.Light, 0.5), Mix(fill.Mid, Empty.Mid, 0.5), Mix(fill.Dark, Empty.Dark, 0.5));
        var shine = Mix(fill.Light, Colors.White, 0.6);
        // Звената.
        cx = 1;
        int rows = h - 2;
        for (int i = 0; i < cells; i++)
        {
            int cw = widths[i];
            bool whole = halves >= (i + 1) * 2, half = halves == i * 2 + 1;
            for (int dx = 0; dx < cw; dx++)
                for (int dy = 0; dy < rows; dy++)
                {
                    Color c;
                    if (whole || (half && small))
                    {
                        c = dy == 0 ? fill.Light : small || dy < rows - 1 ? fill.Mid : fill.Dark;
                        if (!small && dy == 0 && dx == 0) c = shine;   // отблясък в горния ляв ъгъл
                    }
                    else if (half)
                        c = dy == 0 ? pale.Light : dy < rows - 1 ? pale.Mid : pale.Dark;
                    else
                        // Празно – „хлътнало“: тъмно горе, светло долу.
                        c = dy == 0 ? Empty.Dark : dy < rows - 1 ? Empty.Mid : Empty.Light;
                    px[cx + dx, 1 + dy] = c;
                }
            cx += cw + 1;
        }
        // Заоблени ъгли: ъгловите пиксели – прозрачни (малката на храната – само отдясно).
        px[w - 1, 0] = px[w - 1, h - 1] = null;
        if (!small) px[0, 0] = px[0, h - 1] = null;
        var shadowColor = Color.FromArgb(70, 40, 25, 15);
        if (shadow > 0) for (int x = 2; x < w - 1; x++) px[x, h] = shadowColor;
        h += shadow;

        var bytes = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (px[x, y] is not { } c) continue;
                int o = (y * w + x) * 4;
                bytes[o] = c.B; bytes[o + 1] = c.G; bytes[o + 2] = c.R; bytes[o + 3] = c.A;
            }
        var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, bytes, w * 4);
        bmp.Freeze();
        return Cache[key] = bmp;
    }

    /// <summary>Мъничък пикселен „+“ (зелен) или „−“ (червен).</summary>
    public static BitmapSource Sign(bool minus)
    {
        string key = "sign|" + minus;
        if (Cache.TryGetValue(key, out var hit)) return hit;
        // Мъничък: 3×3 „+“ и 3×1 „−“, без контур (тъмният тон – да се вижда).
        string[] rows = minus
            ? new[] { "ggg" }
            : new[] { ".g.", "ggg", ".g." };
        var tone = minus ? Bad.Dark : Good.Dark;
        int w = rows[0].Length, h = rows.Length;
        var bytes = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                char ch = rows[y][x];
                if (ch == '.') continue;
                var c = ch == '#' ? Outline : tone;
                int o = (y * w + x) * 4;
                bytes[o] = c.B; bytes[o + 1] = c.G; bytes[o + 2] = c.R; bytes[o + 3] = 255;
            }
        var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, bytes, w * 4);
        bmp.Freeze();
        return Cache[key] = bmp;
    }
}
