using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Mecho;

/// <summary>
/// Общият вид като в игра: шрифт Pixeloid, дървена рамка, хартия на точици,
/// „дебели“ бутони с ръб отдолу, еднакви пикселни иконки 12×12.
/// </summary>
public static class Ui
{
    /// <summary>Pixeloid Sans (OFL, GGBotNet), с резервен шрифт за емоджитата в репликите.</summary>
    public static readonly FontFamily GameFont =
        new(new Uri("pack://application:,,,/"), "./fonts/#Pixeloid Sans, Segoe UI Emoji, Segoe UI");

    /// <summary>Pixeloid Mono: за часовниците, за да не подскачат цифрите.</summary>
    public static readonly FontFamily MonoFont =
        new(new Uri("pack://application:,,,/"), "./fonts/#Pixeloid Mono, Consolas");

    // Pixeloid е нарисуван на мрежа от 9 пиксела: рязък е при 9, 18, 27… физически пиксела.
    // K е колко физически пиксела е една „точка“ от шрифта и от иконките.
    private static double _dpi = 1;
    private static int _k = 2;

    /// <summary>Обикновен текст (около 14 DIP, но на цели точки на шрифта).</summary>
    public static double Body => 9.0 * _k / _dpi;

    /// <summary>Заглавия.</summary>
    public static double Title => 9.0 * (_k + 1) / _dpi;

    /// <summary>Големи цифри (часовник).</summary>
    public static double Huge => 9.0 * (_k * 2 + 1) / _dpi;

    /// <summary>Вика се веднъж с мащаба на екрана (1 = 100%, 1.25 = 125%…).</summary>
    public static void Init(double dpi)
    {
        _dpi = dpi <= 0 ? 1 : dpi;
        _k = Math.Max(1, (int)Math.Round(14 * _dpi / 9));
    }

    private static SolidColorBrush Rgb(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    public static readonly Brush Ink = Rgb(59, 36, 20);
    public static readonly Brush Wood = Rgb(139, 90, 43);
    public static readonly Brush WoodDark = Rgb(98, 62, 32);
    public static readonly Brush WoodLight = Rgb(181, 125, 70);
    public static readonly Brush Paper = Rgb(255, 244, 220);
    public static readonly Brush PaperDark = Rgb(240, 221, 186);
    public static readonly Brush Tile = Rgb(251, 233, 200);
    public static readonly Brush TileArt = Rgb(236, 208, 160);
    public static readonly Brush Hover = Rgb(255, 226, 154);
    public static readonly Brush Honey = Rgb(255, 209, 102);
    public static readonly Brush Cream = Rgb(255, 248, 230);
    public static readonly Brush Muted = Rgb(125, 98, 72);
    public static readonly Brush Good = Rgb(120, 180, 80);
    public static readonly Brush Okay = Rgb(240, 170, 60);
    public static readonly Brush Bad = Rgb(220, 90, 80);
    public static readonly Brush Empty = Rgb(214, 190, 150);
    public static readonly Brush Sky = Rgb(170, 210, 235);
    public static readonly Brush Grass = Rgb(120, 170, 80);

    /// <summary>Хартия с едва видими точици, за да не е „суха“.</summary>
    public static readonly Brush PaperTexture = MakePaper();

    /// <summary>Дърво с ивици (дъски) за рамката.</summary>
    public static readonly Brush WoodTexture = MakeWood();

    private static Brush Tiled(DrawingGroup group, double w, double h)
    {
        var brush = new DrawingBrush(group)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, w, h),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None,
        };
        RenderOptions.SetEdgeMode(brush, EdgeMode.Aliased);
        brush.Freeze();
        return brush;
    }

    private static GeometryDrawing Box(Brush b, double x, double y, double w, double h) =>
        new(b, null, new RectangleGeometry(new Rect(x, y, w, h)));

    private static Brush MakePaper()
    {
        var g = new DrawingGroup();
        g.Children.Add(Box(Paper, 0, 0, 10, 10));
        g.Children.Add(Box(PaperDark, 0, 0, 1, 1));
        g.Children.Add(Box(PaperDark, 5, 5, 1, 1));
        return Tiled(g, 10, 10);
    }

    private static Brush MakeWood()
    {
        var g = new DrawingGroup();
        g.Children.Add(Box(Wood, 0, 0, 60, 14));
        g.Children.Add(Box(WoodDark, 0, 13, 60, 1));
        g.Children.Add(Box(WoodLight, 6, 4, 14, 1));
        g.Children.Add(Box(WoodDark, 32, 8, 16, 1));
        g.Children.Add(Box(WoodDark, 59, 0, 1, 13));
        return Tiled(g, 60, 14);
    }

    // ───────────────────────── Иконки ─────────────────────────

    private static readonly Dictionary<string, BitmapSource?> IconCache = new();

    private static BitmapSource? LoadIcon(string name)
    {
        if (IconCache.TryGetValue(name, out var cached)) return cached;
        BitmapSource? image = null;
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "assets", "ui", name + ".png");
            if (File.Exists(path))
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(path);
                bmp.EndInit();
                bmp.Freeze();
                image = bmp;
            }
        }
        catch (Exception)
        {
        }
        IconCache[name] = image;
        return image;
    }

    /// <summary>
    /// Иконка 12×12, увеличена на цели физически пиксели. Всички иконки до текст са
    /// еднакво големи (колкото е висок текстът); size 2 е за табовете.
    /// </summary>
    public static FrameworkElement Icon(string name, int size = 1)
    {
        double dip = 12.0 * (_k + size - 1) / _dpi;
        var source = LoadIcon(name);
        if (source == null) return new Border { Width = dip, Height = dip };
        var image = new Image { Source = source, Width = dip, Height = dip, SnapsToDevicePixels = true, Stretch = Stretch.Fill };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
        return image;
    }

    /// <summary>Иконка и текст на един ред, подравнени по средата.</summary>
    public static StackPanel IconText(string? icon, string text, bool bold = false, double? size = null)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        if (icon != null)
        {
            var i = Icon(icon);
            i.VerticalAlignment = VerticalAlignment.Center;
            i.Margin = new Thickness(0, 0, text.Length > 0 ? 6 : 0, 0);
            row.Children.Add(i);
        }
        if (text.Length > 0)
        {
            row.Children.Add(new TextBlock
            {
                Text = text,
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
                FontSize = size ?? Body,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }
        return row;
    }

    // ───────────────────────── Бутони ─────────────────────────

    /// <summary>Дебел бутон с ръб отдолу (по желание с иконка); при натискане „хлътва“.</summary>
    public static Button Button(string text, Action onClick, bool primary = false, string? icon = null)
    {
        var b = new Button
        {
            Content = IconText(icon, text, bold: true),
            Padding = new Thickness(10, 5, 10, 6),
            Margin = new Thickness(0, 0, 6, 6),
            Background = primary ? Honey : PaperDark,
            Foreground = Ink,
            BorderBrush = Ink,
            BorderThickness = new Thickness(2, 2, 2, 4),
            Cursor = Cursors.Hand,
            Template = ChunkyTemplate(),
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        b.Click += (_, _) => onClick();
        return b;
    }

    /// <summary>Сменя текста (и иконката) на бутон.</summary>
    public static void SetButton(Button button, string text, string? icon = null) =>
        button.Content = IconText(icon, text, bold: true);

    /// <summary>Малко копче (размер на задача, ✕ и др.).</summary>
    public static Border Chip(string text, Action onClick)
    {
        var chip = new Border
        {
            Background = PaperDark,
            BorderBrush = Ink,
            BorderThickness = new Thickness(2, 2, 2, 3),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(8, 3, 8, 4),
            Cursor = Cursors.Hand,
            Child = new TextBlock { Text = text, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center },
        };
        // Натискането не стига до родителя (например до дръжката за местене на панела).
        chip.MouseLeftButtonDown += (_, e) => e.Handled = true;
        chip.MouseLeftButtonUp += (_, e) =>
        {
            onClick();
            e.Handled = true;
        };
        return chip;
    }

    private static ControlTemplate ChunkyTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border), "bd");
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        border.SetBinding(Border.BackgroundProperty, new Binding(nameof(Control.Background)) { RelativeSource = RelativeSource.TemplatedParent });
        border.SetBinding(Border.BorderBrushProperty, new Binding(nameof(Control.BorderBrush)) { RelativeSource = RelativeSource.TemplatedParent });
        border.SetBinding(Border.BorderThicknessProperty, new Binding(nameof(Control.BorderThickness)) { RelativeSource = RelativeSource.TemplatedParent });
        border.SetBinding(Border.PaddingProperty, new Binding(nameof(Control.Padding)) { RelativeSource = RelativeSource.TemplatedParent });

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetBinding(FrameworkElement.HorizontalAlignmentProperty, new Binding(nameof(Control.HorizontalContentAlignment)) { RelativeSource = RelativeSource.TemplatedParent });
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);

        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, Hover, "bd"));
        template.Triggers.Add(hover);
        var pressed = new Trigger { Property = System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty, Value = true };
        pressed.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(2, 4, 2, 2), "bd"));
        template.Triggers.Add(pressed);
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.5));
        template.Triggers.Add(disabled);
        return template;
    }

    // ───────────────────────── Рамки, ленти, подсказки ─────────────────────────

    /// <summary>Рамката на прозорец: дърво отвън, хартия отвътре.</summary>
    public static Border Frame(UIElement child, Thickness padding) => new()
    {
        Background = WoodTexture,
        BorderBrush = Ink,
        BorderThickness = new Thickness(3),
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(5),
        Child = new Border
        {
            Background = PaperTexture,
            BorderBrush = Ink,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(5),
            Padding = padding,
            Child = child,
        },
    };

    /// <summary>Заглавна лента (като панделка) за раздел.</summary>
    public static Border Ribbon(string text, string? icon = null) => new()
    {
        Background = Honey,
        BorderBrush = Ink,
        BorderThickness = new Thickness(2, 2, 2, 3),
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(8, 3, 10, 4),
        Margin = new Thickness(0, 12, 0, 8),
        HorizontalAlignment = HorizontalAlignment.Left,
        Child = IconText(icon, text, bold: true),
    };

    /// <summary>Табелка с мед (брой лешници и др.).</summary>
    public static Border Pill(UIElement content) => new()
    {
        Background = Honey,
        BorderBrush = Ink,
        BorderThickness = new Thickness(2, 2, 2, 3),
        CornerRadius = new CornerRadius(10),
        Padding = new Thickness(8, 2, 10, 3),
        Child = content,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>Подсказка (при посочване) в стила на играта.</summary>
    public static ToolTip Tip(string text) => new()
    {
        Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, MaxWidth = 280, FontSize = Body },
        FontFamily = GameFont,
        Background = Cream,
        Foreground = Ink,
        BorderBrush = Ink,
        BorderThickness = new Thickness(2),
        Padding = new Thickness(8, 5, 8, 6),
    };

    /// <summary>Колко физически пиксела на точка за картинките (храна, портрет): big = 2 → „нормално“.</summary>
    public static int ArtScale(int big) => Math.Max(1, _k * big / 2);

    /// <summary>Пикселна картинка, увеличена без размазване на цели физически пиксели.</summary>
    public static Image Pixel(BitmapSource source, int pixelsPerDot)
    {
        var image = new Image
        {
            Source = source,
            Width = source.PixelWidth * pixelsPerDot / _dpi,
            Height = source.PixelHeight * pixelsPerDot / _dpi,
            SnapsToDevicePixels = true,
            Stretch = Stretch.Fill,
        };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
        return image;
    }
}
