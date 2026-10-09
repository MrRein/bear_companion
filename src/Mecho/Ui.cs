using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace Mecho;

/// <summary>Общият вид на панела и разговора: цветове и бутони.</summary>
public static class Ui
{
    public static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(59, 36, 20));
    public static readonly Brush Paper = new SolidColorBrush(Color.FromRgb(255, 248, 230));
    public static readonly Brush PaperDark = new SolidColorBrush(Color.FromRgb(240, 224, 196));
    public static readonly Brush Hover = new SolidColorBrush(Color.FromRgb(255, 233, 170));
    public static readonly Brush Honey = new SolidColorBrush(Color.FromRgb(255, 214, 102));
    public static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(140, 115, 90));
    public static readonly Brush Good = new SolidColorBrush(Color.FromRgb(120, 180, 80));
    public static readonly Brush Okay = new SolidColorBrush(Color.FromRgb(240, 170, 60));
    public static readonly Brush Bad = new SolidColorBrush(Color.FromRgb(220, 90, 80));

    public static Button Button(string text, Action onClick)
    {
        var b = new Button
        {
            Content = text,
            Padding = new Thickness(8, 3, 8, 4),
            Margin = new Thickness(0, 0, 4, 4),
            Background = PaperDark,
            Foreground = Ink,
            BorderBrush = Ink,
            BorderThickness = new Thickness(2),
            FontWeight = FontWeights.SemiBold,
            Cursor = Cursors.Hand,
            Template = FlatTemplate(),
        };
        b.Click += (_, _) => onClick();
        return b;
    }

    /// <summary>Малък „бутон“ от Border (за табове, размери и ×).</summary>
    public static Border Chip(string text, Action onClick)
    {
        var chip = new Border
        {
            Background = PaperDark,
            BorderBrush = Ink,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 2, 6, 3),
            Cursor = Cursors.Hand,
            Child = new TextBlock { Text = text, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center },
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

    /// <summary>Плосък бутон с рамка, който светва при посочване.</summary>
    public static ControlTemplate FlatTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border), "bd");
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
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
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.5));
        template.Triggers.Add(disabled);
        return template;
    }

    /// <summary>Рамката на прозорец (хартия с кафява рамка).</summary>
    public static Border Frame(UIElement child, Thickness padding) => new()
    {
        Background = Paper,
        BorderBrush = Ink,
        BorderThickness = new Thickness(3),
        CornerRadius = new CornerRadius(6),
        Padding = padding,
        Child = child,
    };
}
