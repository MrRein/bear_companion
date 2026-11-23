using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Mecho;

/// <summary>
/// Как се показва рядкостта: звездичка (бяла, синя, жълта, лилава – assets/ui/rarity_*.png)
/// в ъгъла на картинката, с името при посочване. В подаръка и сандъчето – звезда и дума.
/// Безценната звезда леко пулсира.
/// </summary>
public static class RarityColors
{
    /// <summary>Тъмният тон на цвета (за текст – чете се на светъл фон).</summary>
    public static Color Of(Rarity r) => r switch
    {
        Rarity.Uncommon => Color.FromRgb(70, 115, 165),    // синьо
        Rarity.Rare => Color.FromRgb(176, 130, 30),        // жълто/злато
        Rarity.Legendary => Color.FromRgb(129, 81, 169),   // лилаво
        _ => Color.FromRgb(120, 120, 120),                 // бяло → сив текст
    };

    public static SolidColorBrush Brush(Rarity r)
    {
        var b = new SolidColorBrush(Of(r));
        b.Freeze();
        return b;
    }

    public static string IconName(Rarity r) => r switch
    {
        Rarity.Uncommon => "rarity_uncommon",
        Rarity.Rare => "rarity_rare",
        Rarity.Legendary => "rarity_legendary",
        _ => "rarity_common",
    };

    /// <summary>Звездичката (с името при посочване); безценната – пулсира.</summary>
    public static FrameworkElement Star(Rarity r, int size = 2)
    {
        var star = Ui.Icon(IconName(r), size);
        star.ToolTip = Ui.Tip(Rarities.Name(r));
        if (r == Rarity.Legendary && !SelfTest.Running)
        {
            star.RenderTransformOrigin = new Point(0.5, 0.5);
            var scale = new ScaleTransform(1, 1);
            star.RenderTransform = scale;
            var pulse = new DoubleAnimation(1, 1.18, TimeSpan.FromMilliseconds(900))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, pulse);
        }
        return star;
    }

    /// <summary>Звезда и дума („★ Страхотно“) – за подаръка и сандъчето.</summary>
    public static FrameworkElement Tag(Rarity r)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 3, 0, 0) };
        var star = Star(r, 2);
        star.VerticalAlignment = VerticalAlignment.Center;
        star.Margin = new Thickness(0, 0, 4, 0);
        row.Children.Add(star);
        row.Children.Add(new TextBlock { Text = Rarities.Name(r), Foreground = Brush(r), FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
        return row;
    }
}
