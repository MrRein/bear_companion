using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Mecho;

/// <summary>
/// Менюто от спрайтове (assets/ui/skin/skin.png – всички в един файл): рамката, хартията,
/// табовете, бутоните, деленцата, плочките… са картинки, които Тут може да прерисува. Къде е
/// всяка в skin.png казва skin.json → rect. Всяка се реже на 9 части (skin.json → slice): ъглите стоят както са, ръбовете и средата се повтарят – пикселите остават цели и
/// колкото на храната (Ui.P). pad е откъде започва съдържанието.
///
/// Слага се с Skin.Set(рамка, "button"); състоянието (посочено, натиснато…) – с Skin.SetState
/// („button“ + „_hover“ → button_hover; ако го няма – button). Спрайтовете са в
/// цветовете на класическия скин; другите скинове ги преоцветяват (Recolor).
/// </summary>
public static class UiSkin
{
    private sealed record Def(Int32Rect Rect, int L, int T, int R, int B, int PL, int PT, int PR, int PB);

    public static readonly DependencyProperty SpriteProperty = DependencyProperty.RegisterAttached(
        "Sprite", typeof(string), typeof(UiSkin), new PropertyMetadata(null, (d, _) => Changed(d)));

    public static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State", typeof(string), typeof(UiSkin), new PropertyMetadata(null, (d, _) => Changed(d)));

    public static void Set(DependencyObject d, string? sprite) => d.SetValue(SpriteProperty, sprite);
    public static string? Get(DependencyObject d) => (string?)d.GetValue(SpriteProperty);
    public static void SetState(DependencyObject d, string? state) => d.SetValue(StateProperty, state);
    public static string? GetState(DependencyObject d) => (string?)d.GetValue(StateProperty);

    private static Dictionary<string, Def>? _defs;
    private static readonly Dictionary<string, BitmapSource?> Images = new();
    private static readonly Dictionary<(string, int, int, int, int), ImageBrush> Parts = new();
    private static readonly ConditionalWeakTable<Border, StrongBox<Thickness>> BasePadding = new();
    private static readonly List<WeakReference<Border>> Skinned = new();
    private static UiTheme _theme = UiThemes.All[0];

    private static string Dir => Path.Combine(AppContext.BaseDirectory, "assets", "ui", "skin");

    private static Dictionary<string, Def> Defs
    {
        get
        {
            if (_defs != null) return _defs;
            _defs = new Dictionary<string, Def>();
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Dir, "skin.json")));
                foreach (var p in doc.RootElement.EnumerateObject())
                {
                    int[] Ints(string key) => p.Value.GetProperty(key).EnumerateArray().Select(e => e.GetInt32()).ToArray();
                    var r = Ints("rect");
                    var s = Ints("slice");
                    var pad = Ints("pad");
                    _defs[p.Name] = new Def(new Int32Rect(r[0], r[1], r[2], r[3]), s[0], s[1], s[2], s[3], pad[0], pad[1], pad[2], pad[3]);
                }
            }
            catch (Exception)
            {
                // Без спрайтове менюто си остава с нарисуваните от кода рамки.
            }
            return _defs;
        }
    }

    /// <summary>Има ли такъв спрайт (или по-общ: „button_honey_hover“ → „button_honey“ → „button“).</summary>
    private static string? Resolve(string name)
    {
        for (string n = name; ; )
        {
            if (Defs.ContainsKey(n)) return n;
            int cut = n.LastIndexOf('_');
            if (cut <= 0) return null;
            n = n[..cut];
        }
    }

    /// <summary>Има ли такъв спрайт.</summary>
    public static bool Has(string name) => Defs.ContainsKey(name);

    /// <summary>Пунктир, повторен по хоризонтала (чертата под заглавията).</summary>
    public static Brush? Line(string name)
    {
        var img = Image(name);
        if (img == null) return null;
        double p = Ui.P;
        var brush = new ImageBrush(img)
        {
            TileMode = TileMode.Tile,
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, img.PixelWidth * p, img.PixelHeight * p),
            Stretch = Stretch.Fill,
        };
        RenderOptions.SetBitmapScalingMode(brush, BitmapScalingMode.NearestNeighbor);
        brush.Freeze();
        return brush;
    }

    /// <summary>Вика се, когато се смени скинът на менюто: преоцветява всичко.</summary>
    public static void UseTheme(UiTheme theme)
    {
        _theme = theme;
        _atlas = null;
        _atlasLoaded = false;
        Images.Clear();
        Parts.Clear();
        foreach (var w in Skinned.ToArray())
        {
            if (w.TryGetTarget(out var b)) Render(b);
            else Skinned.Remove(w);
        }
    }

    private static void Changed(DependencyObject d)
    {
        if (d is not Border b) return;
        if (!BasePadding.TryGetValue(b, out _))
        {
            BasePadding.Add(b, new StrongBox<Thickness>(b.Padding));
            b.SizeChanged += (_, _) => Render(b);
            Skinned.Add(new WeakReference<Border>(b));
            RenderOptions.SetBitmapScalingMode(b, BitmapScalingMode.NearestNeighbor);
        }
        Render(b);
    }

    private static void Render(Border b)
    {
        string? sprite = Get(b);
        if (sprite == null) return;
        string? state = GetState(b);
        string? name = Resolve(state == null ? sprite : sprite + "_" + state);
        if (name == null || Image(name) is not { } img) return;
        var def = Defs[name];
        double p = Ui.P;
        b.BorderBrush = null;
        b.BorderThickness = new Thickness(0);
        b.CornerRadius = new CornerRadius(0);
        var pad = BasePadding.TryGetValue(b, out var box) ? box.Value : new Thickness();
        b.Padding = new Thickness(pad.Left + def.PL * p, pad.Top + def.PT * p, pad.Right + def.PR * p, pad.Bottom + def.PB * p);
        double w = b.ActualWidth, h = b.ActualHeight;
        if (w <= 0 || h <= 0) return;
        b.Background = NineSlice(name, img, def, w, h, p);
    }

    private static Brush NineSlice(string name, BitmapSource img, Def d, double w, double h, double p)
    {
        int iw = img.PixelWidth, ih = img.PixelHeight;
        int[] sx = { 0, d.L, iw - d.R, iw };
        int[] sy = { 0, d.T, ih - d.B, ih };
        // Ъглите – в цели пиксели; ако нещото е по-малко от ръбовете, средата е 0.
        double l = Math.Min(d.L * p, w / 2), r = Math.Min(d.R * p, w / 2);
        double t = Math.Min(d.T * p, h / 2), bt = Math.Min(d.B * p, h / 2);
        double[] tx = { 0, l, w - r, w };
        double[] ty = { 0, t, h - bt, h };
        var g = new DrawingGroup();
        RenderOptions.SetBitmapScalingMode(g, BitmapScalingMode.NearestNeighbor);
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                int pw = sx[col + 1] - sx[col], ph = sy[row + 1] - sy[row];
                double rw = tx[col + 1] - tx[col], rh = ty[row + 1] - ty[row];
                if (pw <= 0 || ph <= 0 || rw <= 0 || rh <= 0) continue;
                var part = Part(name, img, sx[col], sy[row], pw, ph);
                // Плочката започва от началото на своята част (ъгълът отдолу/вдясно – от края).
                double ox = col == 2 ? tx[3] - pw * p : tx[col];
                double oy = row == 2 ? ty[3] - ph * p : ty[row];
                var tile = part.Clone();
                tile.Viewport = new Rect(ox, oy, pw * p, ph * p);
                tile.Freeze();
                g.Children.Add(new GeometryDrawing(tile, null, new RectangleGeometry(new Rect(tx[col], ty[row], rw, rh))));
            }
        }
        g.Freeze();
        var brush = new DrawingBrush(g)
        {
            Stretch = Stretch.None,
            AlignmentX = AlignmentX.Left,
            AlignmentY = AlignmentY.Top,
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, w, h),
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, w, h),
        };
        RenderOptions.SetBitmapScalingMode(brush, BitmapScalingMode.NearestNeighbor);
        brush.Freeze();
        return brush;
    }

    private static ImageBrush Part(string name, BitmapSource img, int x, int y, int w, int h)
    {
        var key = (name, x, y, w, h);
        if (Parts.TryGetValue(key, out var brush)) return brush;
        var crop = new CroppedBitmap(img, new Int32Rect(x, y, w, h));
        crop.Freeze();
        brush = new ImageBrush(crop)
        {
            TileMode = TileMode.Tile,
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.Fill,
        };
        RenderOptions.SetBitmapScalingMode(brush, BitmapScalingMode.NearestNeighbor);
        Parts[key] = brush;
        return brush;
    }

    private static BitmapSource? _atlas;
    private static bool _atlasLoaded;

    /// <summary>Целият skin.png (в цветовете на сложения скин на менюто).</summary>
    private static BitmapSource? Atlas()
    {
        if (_atlasLoaded) return _atlas;
        _atlasLoaded = true;
        try
        {
            string path = Path.Combine(Dir, "skin.png");
            if (File.Exists(path))
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(path);
                bmp.EndInit();
                bmp.Freeze();
                _atlas = _theme.Id == UiThemes.All[0].Id ? bmp : Recolor(bmp, UiThemes.All[0], _theme);
            }
        }
        catch (Exception)
        {
            _atlas = null;
        }
        return _atlas;
    }

    /// <summary>Един спрайт – изрязан от skin.png по skin.json → rect.</summary>
    private static BitmapSource? Image(string name)
    {
        if (Images.TryGetValue(name, out var cached)) return cached;
        BitmapSource? image = null;
        try
        {
            if (Defs.TryGetValue(name, out var def) && Atlas() is { } atlas
                && def.Rect.X + def.Rect.Width <= atlas.PixelWidth && def.Rect.Y + def.Rect.Height <= atlas.PixelHeight)
            {
                var crop = new CroppedBitmap(atlas, def.Rect);
                crop.Freeze();
                image = crop;
            }
        }
        catch (Exception)
        {
        }
        Images[name] = image;
        return image;
    }

    /// <summary>
    /// Спрайт от класическия скин в цветовете на друг: всеки пиксел се отнася към най-близкия
    /// цвят от класическия скин и се мести със същата разлика спрямо цвета на новия скин
    /// (така светлото и тъмното на релефа се запазват).
    /// </summary>
    private static BitmapSource Recolor(BitmapSource src, UiTheme from, UiTheme to)
    {
        Color[] a = Keys(from), b = Keys(to);
        var conv = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
        int w = conv.PixelWidth, h = conv.PixelHeight, stride = w * 4;
        var px = new byte[h * stride];
        conv.CopyPixels(px, stride, 0);
        for (int i = 0; i < px.Length; i += 4)
        {
            if (px[i + 3] == 0) continue;
            int r = px[i + 2], g = px[i + 1], bl = px[i];
            int best = 0, bestD = int.MaxValue;
            for (int k = 0; k < a.Length; k++)
            {
                int dr = r - a[k].R, dg = g - a[k].G, db = bl - a[k].B;
                int dist = dr * dr + dg * dg + db * db;
                if (dist < bestD) { bestD = dist; best = k; }
            }
            px[i + 2] = Clamp(b[best].R + r - a[best].R);
            px[i + 1] = Clamp(b[best].G + g - a[best].G);
            px[i] = Clamp(b[best].B + bl - a[best].B);
        }
        var result = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, px, stride);
        result.Freeze();
        return result;
    }

    private static byte Clamp(int v) => (byte)Math.Clamp(v, 0, 255);

    private static Color[] Keys(UiTheme t) =>
        new[] { t.Ink, t.Wood, t.WoodDark, t.WoodLight, t.Paper, t.PaperDark, t.Tile, t.TileArt, t.Hover, t.Honey, t.Cream, t.Muted, t.Empty };
}
