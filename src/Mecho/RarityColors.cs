using System.Windows.Media;

namespace Mecho;

/// <summary>Цветовете на рядкостта (като сандъчетата: дърво, сребро, злато, лилаво).</summary>
public static class RarityColors
{
    public static Color Of(Rarity r) => r switch
    {
        Rarity.Uncommon => System.Windows.Media.Color.FromRgb(96, 128, 168),    // сребърно-синьо
        Rarity.Rare => System.Windows.Media.Color.FromRgb(196, 140, 24),        // злато
        Rarity.Legendary => System.Windows.Media.Color.FromRgb(140, 70, 180),   // лилаво
        _ => System.Windows.Media.Color.FromRgb(139, 100, 62),                  // дърво
    };

    public static SolidColorBrush Brush(Rarity r)
    {
        var b = new SolidColorBrush(Of(r));
        b.Freeze();
        return b;
    }

}
