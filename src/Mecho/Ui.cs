using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Mecho;

/// <summary>
/// Общият вид като в игра: пикселен шрифт, дървена рамка, хартия, „дебели“
/// бутони с ръб отдолу, плочки с картинки.
/// </summary>
public static class Ui
{
    /// <summary>
    /// Pixeled (избран от нас). Знаците, които го няма в него (–, „, ·, %), идват от
    /// Pixelify Sans, а емоджитата от Segoe UI Emoji.
    /// </summary>
    public static readonly FontFamily GameFont =
        new(new Uri("pack://application:,,,/"), "./fonts/#Pixeled, ./fonts/#Pixelify Sans, Segoe UI Emoji, Segoe UI");

    private static SolidColorBrush Rgb(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    public static readonly Brush Ink = Rgb(59, 36, 20);          // контури и текст
    public static readonly Brush Wood = Rgb(139, 90, 43);        // рамка и заглавие
    public static readonly Brush WoodDark = Rgb(98, 62, 32);
    public static readonly Brush WoodLight = Rgb(181, 125, 70);
    public static readonly Brush Paper = Rgb(255, 244, 220);     // фон на съдържанието
    public static readonly Brush PaperDark = Rgb(240, 221, 186);
    public static readonly Brush Tile = Rgb(251, 233, 200);
    public static readonly Brush TileArt = Rgb(236, 208, 160);   // фон зад картинката в плочка
    public static readonly Brush Hover = Rgb(255, 226, 154);
    public static readonly Brush Honey = Rgb(255, 209, 102);
    public static readonly Brush Cream = Rgb(255, 248, 230);
    public static readonly Brush Muted = Rgb(140, 115, 90);
    public static readonly Brush Good = Rgb(120, 180, 80);
    public static readonly Brush Okay = Rgb(240, 170, 60);
    public static readonly Brush Bad = Rgb(220, 90, 80);
    public static readonly Brush Empty = Rgb(214, 190, 150);

    /// <summary>Дебел бутон с ръб отдолу; при натискане „хлътва“.</summary>
    public static Button Button(string text, Action onClick, bool primary = false)
    {
        var b = new Button
        {
            Content = text,
            Padding = new Thickness(10, 3, 10, 4),
            Margin = new Thickness(0, 0, 4, 4),
            Background = primary ? Honey : PaperDark,
            Foreground = Ink,
            BorderBrush = Ink,
            BorderThickness = new Thickness(2, 2, 2, 4),
            FontWeight = FontWeights.Normal,
            Cursor = Cursors.Hand,
            Template = ChunkyTemplate(),
        };
        b.Click += (_, _) => onClick();
        return b;
    }

    /// <summary>Малък „бутон“ от Border (за размери, ✕ и др.).</summary>
    public static Border Chip(string text, Action onClick)
    {
        var chip = new Border
        {
            Background = PaperDark,
            BorderBrush = Ink,
            BorderThickness = new Thickness(2, 2, 2, 3),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(7, 2, 7, 3),
            Cursor = Cursors.Hand,
            Child = new TextBlock { Text = text, FontWeight = FontWeights.Normal, HorizontalAlignment = HorizontalAlignment.Center },
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
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.45));
        template.Triggers.Add(disabled);
        return template;
    }

    /// <summary>Рамката на прозорец: тъмно дърво отвън, хартия отвътре.</summary>
    public static Border Frame(UIElement child, Thickness padding) => new()
    {
        Background = Wood,
        BorderBrush = Ink,
        BorderThickness = new Thickness(3),
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(5),
        Child = new Border
        {
            Background = Paper,
            BorderBrush = WoodDark,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(5),
            Padding = padding,
            Child = child,
        },
    };

    /// <summary>Пикселна картинка, увеличена без размазване.</summary>
    public static Image Pixel(BitmapSource source, double scale)
    {
        var image = new Image
        {
            Source = source,
            Width = source.PixelWidth * scale,
            Height = source.PixelHeight * scale,
            SnapsToDevicePixels = true,
        };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
        return image;
    }

    /// <summary>Табелка с мед: брой лешници, цена и др.</summary>
    public static Border Pill(TextBlock text) => new()
    {
        Background = Honey,
        BorderBrush = Ink,
        BorderThickness = new Thickness(2, 2, 2, 3),
        CornerRadius = new CornerRadius(10),
        Padding = new Thickness(8, 1, 8, 2),
        Child = text,
        VerticalAlignment = VerticalAlignment.Center,
    };
}
