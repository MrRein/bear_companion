using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Mecho;

/// <summary>Една анимация: кадри от лента в PNG файл.</summary>
public sealed class SpriteAnimation
{
    public required BitmapSource[] Frames { get; init; }
    public double Fps { get; init; } = 4;
    public bool Loop { get; init; } = true;

    public double Duration => Frames.Length / Fps;

    public BitmapSource FrameAt(double time)
    {
        int i = (int)(time * Fps);
        i = Loop ? i % Frames.Length : Math.Min(i, Frames.Length - 1);
        return Frames[i];
    }
}

/// <summary>
/// Чете анимациите от assets/bear/anim.json и PNG лентите до него.
/// Ако нещо липсва, рисува цветен квадрат с името на анимацията.
/// </summary>
public sealed class SpriteLibrary
{
    private sealed class Manifest
    {
        public int FrameWidth { get; set; } = 32;
        public int FrameHeight { get; set; } = 32;
        public int Scale { get; set; } = 3;
        public Dictionary<string, Entry> Animations { get; set; } = new();
    }

    private sealed class Entry
    {
        public string? File { get; set; }
        public double Fps { get; set; } = 4;
        public bool Loop { get; set; } = true;
    }

    private readonly Dictionary<string, SpriteAnimation> _anims = new();

    public int FrameWidth { get; private set; } = 32;
    public int FrameHeight { get; private set; } = 32;
    public int Scale { get; private set; } = 3;

    public static string Folder => Path.Combine(AppContext.BaseDirectory, "assets", "bear");

    public static SpriteLibrary Load()
    {
        var lib = new SpriteLibrary();
        var manifest = new Manifest();
        try
        {
            string path = Path.Combine(Folder, "anim.json");
            if (File.Exists(path))
            {
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(path), opts) ?? manifest;
            }
        }
        catch (Exception)
        {
            // Счупен anim.json: ползваме placeholder-и.
        }

        lib.FrameWidth = Math.Max(1, manifest.FrameWidth);
        lib.FrameHeight = Math.Max(1, manifest.FrameHeight);
        lib.Scale = Math.Max(1, manifest.Scale);

        foreach (var (name, entry) in manifest.Animations)
        {
            var frames = lib.LoadStrip(entry.File ?? name + ".png");
            if (frames != null)
                lib._anims[name] = new SpriteAnimation { Frames = frames, Fps = Math.Max(0.1, entry.Fps), Loop = entry.Loop };
        }
        return lib;
    }

    public SpriteAnimation Get(string name)
    {
        if (_anims.TryGetValue(name, out var a)) return a;
        a = new SpriteAnimation { Frames = new[] { Placeholder(name) }, Fps = 1, Loop = true };
        _anims[name] = a;
        return a;
    }

    private BitmapSource[]? LoadStrip(string file)
    {
        try
        {
            string path = Path.Combine(Folder, file);
            if (!File.Exists(path)) return null;

            // OnLoad: файлът не остава заключен и може да се смени, докато мечокът работи.
            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            img.UriSource = new Uri(path);
            img.EndInit();
            img.Freeze();

            int count = Math.Max(1, img.PixelWidth / FrameWidth);
            int height = Math.Min(FrameHeight, img.PixelHeight);
            var frames = new BitmapSource[count];
            for (int i = 0; i < count; i++)
            {
                var frame = new CroppedBitmap(img, new Int32Rect(i * FrameWidth, 0, FrameWidth, height));
                frame.Freeze();
                frames[i] = frame;
            }
            return frames;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private BitmapSource Placeholder(string name)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(139, 90, 43)), new Pen(Brushes.Black, 1),
                new Rect(0.5, 0.5, FrameWidth - 1, FrameHeight - 1));
            var text = new FormattedText(name.Length > 4 ? name[..4] : name, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, new Typeface("Consolas"), 8, Brushes.White, 1.0);
            dc.DrawText(text, new Point(2, FrameHeight / 2.0 - 5));
        }
        var bmp = new RenderTargetBitmap(FrameWidth, FrameHeight, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        bmp.Freeze();
        return bmp;
    }
}
