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
    public static readonly Tone Empty = new(C(190, 181, 165), C(174, 158, 132), C(142, 130, 110));
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
    public static BitmapSource Big(int halves, int cells, Tone fill) => Render(halves, cells, fill, small: false);

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
        var px = new Color?[w, h];
        // Рамка и прегради.
        for (int x = 0; x < w; x++) { px[x, 0] = Outline; px[x, h - 1] = Outline; }
        int cx = 0;
        for (int i = 0; i <= cells; i++)
        {
            for (int y = 0; y < h; y++) px[cx, y] = Outline;
            if (i < cells) cx += widths[i] + 1;
        }
        // Звената.
        cx = 1;
        for (int i = 0; i < cells; i++)
        {
            int cw = widths[i];
            // Колко колони от звеното са пълни.
            int full = halves >= (i + 1) * 2 ? cw : halves == i * 2 + 1 ? (small ? cw : cw / 2) : 0;
            for (int dx = 0; dx < cw; dx++)
            {
                var t = dx < full ? fill : Empty;
                for (int dy = 0; dy < h - 2; dy++)
                    px[cx + dx, 1 + dy] = dy == 0 ? t.Light : small || dy < h - 3 ? t.Mid : t.Dark;
            }
            cx += cw + 1;
        }
        // Заоблени ъгли: ъгловите пиксели – прозрачни (малката на храната – само отдясно).
        px[w - 1, 0] = px[w - 1, h - 1] = null;
        if (!small) px[0, 0] = px[0, h - 1] = null;

        var bytes = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (px[x, y] is not { } c) continue;
                int o = (y * w + x) * 4;
                bytes[o] = c.B; bytes[o + 1] = c.G; bytes[o + 2] = c.R; bytes[o + 3] = 255;
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
