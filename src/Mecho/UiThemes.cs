using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Mecho;

/// <summary>
/// Скин на менюто и бутоните: цветовете на рамката (дървото), хартията, бутоните
/// и избраното. Класическият (дъб и мед) е безплатен; другите се купуват в
/// офертата на деня и се сменят в гардероба → „Меню“.
/// </summary>
public sealed record UiTheme(
    string Id, string Name, int Price,
    Color Ink, Color Wood, Color WoodDark, Color WoodLight,
    Color Paper, Color PaperDark, Color Tile, Color TileArt,
    Color Hover, Color Honey, Color Cream, Color Muted, Color Empty)
{
    /// <summary>Как се пази в купените неща (Owned).</summary>
    public string Key => "ui_" + Id;
}

public static class UiThemes
{
    private static Color C(byte r, byte g, byte b) => Color.FromRgb(r, g, b);

    public static readonly UiTheme[] All =
    {
        new("classic", "Дъб и мед", 0,
            C(59, 36, 20), C(139, 90, 43), C(98, 62, 32), C(181, 125, 70),
            C(255, 244, 220), C(240, 221, 186), C(251, 233, 200), C(236, 208, 160),
            C(255, 226, 154), C(255, 209, 102), C(255, 248, 230), C(125, 98, 72), C(214, 190, 150)),
        new("birch", "Бреза", 30,
            C(52, 48, 42), C(222, 218, 205), C(62, 58, 52), C(246, 244, 236),
            C(251, 251, 245), C(233, 233, 220), C(245, 244, 235), C(224, 222, 205),
            C(232, 240, 196), C(186, 210, 112), C(253, 253, 248), C(118, 112, 98), C(212, 210, 192)),
        new("grass", "Трева", 30,
            C(38, 58, 28), C(96, 150, 62), C(62, 106, 40), C(142, 192, 92),
            C(241, 250, 226), C(216, 236, 190), C(233, 246, 216), C(202, 226, 172),
            C(250, 240, 150), C(250, 214, 88), C(248, 252, 240), C(92, 114, 74), C(190, 212, 164)),
        new("stone", "Камък", 30,
            C(44, 44, 50), C(140, 140, 146), C(94, 94, 100), C(182, 182, 188),
            C(239, 237, 231), C(216, 213, 206), C(231, 229, 223), C(201, 199, 191),
            C(204, 218, 232), C(132, 176, 216), C(247, 246, 241), C(104, 104, 110), C(190, 188, 182)),
        new("sakura", "Черешов цвят", 35,
            C(70, 34, 40), C(122, 70, 60), C(86, 46, 40), C(168, 106, 92),
            C(255, 241, 244), C(246, 216, 223), C(252, 233, 237), C(241, 202, 211),
            C(255, 216, 226), C(240, 142, 172), C(255, 248, 250), C(140, 94, 100), C(226, 190, 198)),
        new("night", "Нощно небе", 40,
            C(30, 32, 56), C(56, 62, 98), C(36, 40, 68), C(88, 96, 140),
            C(226, 229, 246), C(206, 210, 234), C(219, 222, 243), C(191, 196, 229),
            C(250, 232, 164), C(250, 210, 100), C(241, 243, 252), C(90, 96, 132), C(182, 187, 216)),
    };

    public static UiTheme Find(string? id) => All.FirstOrDefault(t => t.Id == id) ?? All[0];

    public static bool Owns(SaveData save, UiTheme t) => t.Price == 0 || save.Owned.Contains(t.Key);

    /// <summary>Сложеният скин, ако е купен (иначе класическият).</summary>
    public static UiTheme Current(SaveData save) => Find(save.UiTheme) is var t && Owns(save, t) ? t : All[0];

    /// <summary>Малка картинка на скина: дървена рамка, хартия и меден бутон (за плочката).</summary>
    public static BitmapSource Preview(UiTheme t)
    {
        const int w = 30, h = 22;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            void Box(Color c, double x, double y, double bw, double bh) => dc.DrawRectangle(new SolidColorBrush(c), null, new Rect(x, y, bw, bh));
            Box(t.Ink, 0, 0, w, h);
            Box(t.Wood, 1, 1, w - 2, h - 2);
            Box(t.WoodLight, 3, 2, 8, 1);
            Box(t.WoodDark, 16, 3, 9, 1);
            Box(t.Ink, 3, 4, w - 6, h - 7);
            Box(t.Paper, 4, 5, w - 8, h - 9);
            Box(t.PaperDark, 5, 6, 1, 1);
            Box(t.PaperDark, 10, 10, 1, 1);
            Box(t.Ink, 7, 8, 16, 7);           // бутонът
            Box(t.Honey, 8, 9, 14, 4);
            Box(t.Muted, 10, 11, 10, 1);
        }
        var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        bmp.Freeze();
        return bmp;
    }
}
