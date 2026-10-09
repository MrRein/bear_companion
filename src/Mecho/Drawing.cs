using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Mecho;

/// <summary>
/// Нарисуваното от Тут на един екран: решетка от пикселни квадратчета (колкото
/// един пиксел от мечока). Мечокът стъпва по тях като по платформи. Показва се
/// в прозрачен прозорец, през който кликовете минават.
/// </summary>
public sealed class DrawingCanvas
{
    public static readonly Color[] Palette =
    {
        Colors.Transparent,
        Color.FromRgb(139, 90, 43),   // 1 кафяво (по подразбиране)
        Color.FromRgb(59, 36, 20),    // 2 тъмно
        Color.FromRgb(255, 209, 102), // 3 мед
        Color.FromRgb(220, 90, 80),   // 4 червено
        Color.FromRgb(240, 160, 60),  // 5 оранжево
        Color.FromRgb(120, 180, 80),  // 6 зелено
        Color.FromRgb(90, 140, 210),  // 7 синьо
        Color.FromRgb(170, 120, 210), // 8 лилаво
        Color.FromRgb(240, 140, 150), // 9 розово
        Color.FromRgb(255, 248, 230), // 10 бяло
        Color.FromRgb(150, 150, 160), // 11 сиво
    };

    public Rect Area { get; }
    public double Cell { get; }
    public int W { get; }
    public int H { get; }
    public byte[] Cells { get; }
    public WriteableBitmap Bitmap { get; }
    public bool IsEmpty => _filled == 0;

    private int _filled; // колко квадратчета са нарисувани (за да не ги броим всеки кадър)

    private DrawingLayer? _layer;

    public DrawingCanvas(Rect area, double cell, byte[]? cells = null)
    {
        Area = area;
        Cell = cell;
        W = Math.Max(1, (int)Math.Ceiling(area.Width / cell));
        H = Math.Max(1, (int)Math.Ceiling(area.Height / cell));
        Cells = cells != null && cells.Length == W * H ? cells : new byte[W * H];
        _filled = Cells.Count(c => c != 0);
        Bitmap = new WriteableBitmap(W, H, 96, 96, PixelFormats.Pbgra32, null);
        Redraw(0, 0, W, H);
    }

    public bool Solid(int cx, int cy) => cx >= 0 && cy >= 0 && cx < W && cy < H && Cells[cy * W + cx] != 0;

    public int Col(double x) => (int)Math.Floor((x - Area.Left) / Cell);
    public int Row(double y) => (int)Math.Floor((y - Area.Top) / Cell);
    public double RowTop(int row) => Area.Top + row * Cell;
    public double ColLeft(int col) => Area.Left + col * Cell;

    /// <summary>Рисува (color > 0) или трие (color = 0) квадрат size×size около клетката.</summary>
    public void Stamp(int cx, int cy, int size, byte color)
    {
        int x0 = Math.Max(0, cx - size / 2), y0 = Math.Max(0, cy - size / 2);
        int x1 = Math.Min(W, x0 + size), y1 = Math.Min(H, y0 + size);
        if (x1 <= x0 || y1 <= y0) return;
        for (int y = y0; y < y1; y++)
        for (int x = x0; x < x1; x++)
        {
            ref byte cell = ref Cells[y * W + x];
            if (cell == 0 && color != 0) _filled++;
            else if (cell != 0 && color == 0) _filled--;
            cell = color;
        }
        Redraw(x0, y0, x1 - x0, y1 - y0);
    }

    /// <summary>Пребоядисва цялата рисунка в един цвят.</summary>
    public void Recolor(byte color)
    {
        for (int i = 0; i < Cells.Length; i++)
            if (Cells[i] != 0) Cells[i] = color;
        Redraw(0, 0, W, H);
    }

    public void Clear()
    {
        Array.Clear(Cells);
        _filled = 0;
        Redraw(0, 0, W, H);
    }

    private void Redraw(int x0, int y0, int w, int h)
    {
        var pixels = new int[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            var c = Palette[Math.Min(Palette.Length - 1, (int)Cells[(y0 + y) * W + x0 + x])];
            pixels[y * w + x] = c.A == 0 ? 0 : (255 << 24) | (c.R << 16) | (c.G << 8) | c.B;
        }
        Bitmap.WritePixels(new Int32Rect(x0, y0, w, h), pixels, w * 4, 0);
    }

    /// <summary>Показва рисунката (ако има какво) в прозрачния слой на екрана.</summary>
    public void ShowLayer(bool show)
    {
        if (show && !IsEmpty)
        {
            _layer ??= new DrawingLayer(this);
            if (!_layer.IsVisible) _layer.Show();
        }
        else _layer?.Hide();
    }

    // ───────────────────────── Запис ─────────────────────────

    public string Key => $"{Area.Left:0}_{Area.Top:0}_{Area.Width:0}_{Area.Height:0}_{Cell:0.##}".Replace(',', '.');

    public void Save(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, $"drawing_{Key}.bin");
            if (IsEmpty)
            {
                if (File.Exists(path)) File.Delete(path);
                return;
            }
            using var file = File.Create(path);
            using var zip = new DeflateStream(file, CompressionLevel.Optimal);
            zip.Write(Cells, 0, Cells.Length);
        }
        catch (Exception)
        {
        }
    }

    public static byte[]? LoadCells(string folder, string key, int length)
    {
        try
        {
            string path = Path.Combine(folder, $"drawing_{key}.bin");
            if (!File.Exists(path)) return null;
            using var file = File.OpenRead(path);
            using var zip = new DeflateStream(file, CompressionMode.Decompress);
            var cells = new byte[length];
            int read = 0;
            while (read < length)
            {
                int n = zip.Read(cells, read, length - read);
                if (n == 0) break;
                read += n;
            }
            return read == length ? cells : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}

/// <summary>Прозрачен прозорец върху целия екран, който само показва рисунката (кликовете минават през него).</summary>
public sealed class DrawingLayer : Window
{
    public DrawingLayer(DrawingCanvas canvas)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = canvas.Area.Left;
        Top = canvas.Area.Top;
        Width = canvas.W * canvas.Cell;
        Height = canvas.H * canvas.Cell;
        IsHitTestVisible = false;

        var image = new Image { Source = canvas.Bitmap, Stretch = Stretch.Fill, Width = Width, Height = Height };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
        Content = image;

        SourceInitialized += (_, _) => NativeMethods.MakeClickThrough(new WindowInteropHelper(this).Handle);
    }
}

/// <summary>Всички рисунки (по една за екран) и въпросът „къде е земята под мечока“.</summary>
public sealed class DrawingStore
{
    private readonly List<DrawingCanvas> _canvases = new();

    public static string Folder => SaveData.Folder;

    /// <summary>Рисунката за този екран (създава празна, ако няма).</summary>
    public DrawingCanvas For(Rect area, double cell)
    {
        var c = _canvases.FirstOrDefault(x => x.Area == area && Math.Abs(x.Cell - cell) < 0.01);
        if (c != null) return c;
        var empty = new DrawingCanvas(area, cell);
        var cells = DrawingCanvas.LoadCells(Folder, empty.Key, empty.W * empty.H);
        c = cells != null ? new DrawingCanvas(area, cell, cells) : empty;
        _canvases.Add(c);
        return c;
    }

    /// <summary>Само ако вече има рисунка за този екран.</summary>
    public DrawingCanvas? Existing(Rect area) => _canvases.FirstOrDefault(x => x.Area == area && !x.IsEmpty);

    public void ShowAll()
    {
        foreach (var c in _canvases) c.ShowLayer(true);
    }

    /// <summary>
    /// Къде е земята под мечока: най-горната нарисувана повърхност под стъпалата му
    /// (в ширина halfSpan около x), или долният край на екрана.
    /// </summary>
    public double GroundAt(Rect area, double x, double halfSpan, double feetY)
    {
        var c = Existing(area);
        if (c == null) return area.Bottom;
        int c0 = c.Col(x - halfSpan), c1 = c.Col(x + halfSpan);
        int start = Math.Max(0, c.Row(feetY - c.Cell * 1.5));
        for (int row = start; row < c.H; row++)
        {
            double top = c.RowTop(row);
            if (top >= area.Bottom) break;
            for (int col = c0; col <= c1; col++)
                if (c.Solid(col, row)) return Math.Min(top, area.Bottom);
        }
        return area.Bottom;
    }

    /// <summary>Докъде стига платформата, на която е стъпил (по хоризонтала).</summary>
    public (double Left, double Right)? PlatformSpan(Rect area, double x, double feetY)
    {
        var c = Existing(area);
        if (c == null) return null;
        int row = c.Row(feetY + c.Cell * 0.5);
        int col = c.Col(x);
        if (!c.Solid(col, row))
        {
            // Може да стъпва само с единия крак: търси наблизо.
            int found = -1;
            for (int d = 1; d < 12 && found < 0; d++)
            {
                if (c.Solid(col - d, row)) found = col - d;
                else if (c.Solid(col + d, row)) found = col + d;
            }
            if (found < 0) return null;
            col = found;
        }
        int l = col, r = col;
        while (c.Solid(l - 1, row)) l--;
        while (c.Solid(r + 1, row)) r++;
        return (c.ColLeft(l), c.ColLeft(r + 1));
    }

    /// <summary>Повърхност над мечока, на която може да скочи (или null).</summary>
    public Point? FindLedge(Rect area, double x, double feetY, double reachX, double minUp, double maxUp, Random rng)
    {
        var c = Existing(area);
        if (c == null) return null;
        for (int attempt = 0; attempt < 300; attempt++)
        {
            double tx = x + (rng.NextDouble() * 2 - 1) * reachX;
            if (tx < area.Left + 30 || tx > area.Right - 30) continue;
            int col = c.Col(tx);
            int rowTop = c.Row(feetY - maxUp), rowBottom = c.Row(feetY - minUp);
            for (int row = Math.Max(1, rowTop); row <= rowBottom && row < c.H; row++)
            {
                // Повърхност: плътно квадратче с 3 празни над него.
                if (c.Solid(col, row) && !c.Solid(col, row - 1) && !c.Solid(col, row - 2) && !c.Solid(col, row - 3))
                    return new Point(c.ColLeft(col) + c.Cell / 2, c.RowTop(row));
            }
        }
        return null;
    }
}
