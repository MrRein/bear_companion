using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    /// <summary>Мащабът на екрана (1 = 100%).</summary>
    public static double Dpi => _dpi;
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

    /// <summary>
    /// Цвят от скина на менюто: четката НЕ е замразена, а сменя цвета си (Apply),
    /// така всичко нарисувано с нея се оцветява наново без да се прави отначало.
    /// Затова в шаблони и стилове се слага през Binding (иначе WPF я замразява).
    /// </summary>
    private static SolidColorBrush Live(Color c) => new(c);

    private static readonly UiTheme Start = UiThemes.All[0];
    public static readonly Brush Ink = Live(Start.Ink);
    public static readonly Brush Wood = Live(Start.Wood);
    public static readonly Brush WoodDark = Live(Start.WoodDark);
    public static readonly Brush WoodLight = Live(Start.WoodLight);
    public static readonly Brush Paper = Live(Start.Paper);
    public static readonly Brush PaperDark = Live(Start.PaperDark);
    public static readonly Brush Tile = Live(Start.Tile);
    public static readonly Brush TileArt = Live(Start.TileArt);
    public static readonly Brush Hover = Live(Start.Hover);
    public static readonly Brush Honey = Live(Start.Honey);
    public static readonly Brush Cream = Live(Start.Cream);
    public static readonly Brush Muted = Live(Start.Muted);
    public static readonly Brush Empty = Live(Start.Empty);

    /// <summary>Слага скина на менюто (всички отворени прозорци се оцветяват веднага).</summary>
    public static void Apply(UiTheme t)
    {
        void Set(Brush b, Color c) => ((SolidColorBrush)b).Color = c;
        Set(Ink, t.Ink);
        Set(Wood, t.Wood);
        Set(WoodDark, t.WoodDark);
        Set(WoodLight, t.WoodLight);
        Set(Paper, t.Paper);
        Set(PaperDark, t.PaperDark);
        Set(Tile, t.Tile);
        Set(TileArt, t.TileArt);
        Set(Hover, t.Hover);
        Set(Honey, t.Honey);
        Set(Cream, t.Cream);
        Set(Muted, t.Muted);
        Set(Empty, t.Empty);
    }

    public static readonly Brush Good = Rgb(120, 180, 80);
    public static readonly Brush Okay = Rgb(240, 170, 60);
    public static readonly Brush Bad = Rgb(220, 90, 80);
    public static readonly Brush Sky = Rgb(170, 210, 235);
    public static readonly Brush Grass = Rgb(120, 170, 80);

    /// <summary>Хартия с едва видими точици, за да не е „суха“.</summary>
    /// <summary>
    /// Плъзгачът (scroll bar) в стила на менюто: дървен улей и меден палец, без
    /// стрелки. Слага се като стил по подразбиране в ресурсите на прозореца.
    /// </summary>
    public static Style ScrollBarStyle()
    {
        const string xaml = @"
<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
       xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
       xmlns:m='clr-namespace:Mecho;assembly=Mecho' TargetType='ScrollBar'>
  <Setter Property='Width' Value='16'/>
  <Setter Property='MinWidth' Value='16'/>
  <Setter Property='Margin' Value='6,0,0,0'/>
  <Setter Property='Template'>
    <Setter.Value>
      <ControlTemplate TargetType='ScrollBar'>
        <Border Background='{Binding Source={x:Static m:Ui.PaperDark}}' BorderBrush='{Binding Source={x:Static m:Ui.Ink}}' BorderThickness='2' CornerRadius='5' Padding='1'>
          <Track x:Name='PART_Track' IsDirectionReversed='True'>
            <Track.DecreaseRepeatButton>
              <RepeatButton Command='ScrollBar.PageUpCommand' Focusable='False' Opacity='0'/>
            </Track.DecreaseRepeatButton>
            <Track.IncreaseRepeatButton>
              <RepeatButton Command='ScrollBar.PageDownCommand' Focusable='False' Opacity='0'/>
            </Track.IncreaseRepeatButton>
            <Track.Thumb>
              <Thumb Cursor='Hand'>
                <Thumb.Template>
                  <ControlTemplate TargetType='Thumb'>
                    <Border x:Name='T' Background='{Binding Source={x:Static m:Ui.Honey}}' BorderBrush='{Binding Source={x:Static m:Ui.Ink}}' BorderThickness='2,2,2,3' CornerRadius='3' MinHeight='24'/>
                    <ControlTemplate.Triggers>
                      <Trigger Property='IsMouseOver' Value='True'>
                        <Setter TargetName='T' Property='Background' Value='{Binding Source={x:Static m:Ui.Hover}}'/>
                      </Trigger>
                      <Trigger Property='IsDragging' Value='True'>
                        <Setter TargetName='T' Property='Background' Value='#F0AA3C'/>
                      </Trigger>
                    </ControlTemplate.Triggers>
                  </ControlTemplate>
                </Thumb.Template>
              </Thumb>
            </Track.Thumb>
          </Track>
        </Border>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>";
        return (Style)System.Windows.Markup.XamlReader.Parse(xaml);
    }

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
        // Не се замразява: цветовете идват от скина на менюто и се сменят.
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

    internal static BitmapSource? LoadIcon(string name)
    {
        if (IconCache.TryGetValue(name, out var cached)) return cached;
        BitmapSource? image = null;
        try
        {
            // Иконките са в assets/ui; храната и предметите от магазина – в assets/props.
            string path = Path.Combine(AppContext.BaseDirectory, "assets", "ui", name + ".png");
            if (!File.Exists(path)) path = Path.Combine(AppContext.BaseDirectory, "assets", "props", name + ".png");
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

    // ───────────────────────── Емоджи → пикселни иконки ─────────────────────────

    /// <summary>Кое емоджи (или знак) с коя пикселна иконка се показва.</summary>
    private static readonly Dictionary<string, string> EmojiIcons = new()
    {
        ["🌰"] = "nut", ["⏰"] = "clock", ["🍯"] = "honey", ["☕"] = "tea", ["⚡"] = "energy",
        ["■"] = "stop", ["▶"] = "play", ["▲"] = "arrow_up", ["▼"] = "arrow_down",
        // Стрелките в изреченията („Десен бутон → „Обнови““).
        ["📍"] = "unpin", ["➕"] = "plus", ["→"] = "arrow_right", ["↑"] = "arrow_up", ["↓"] = "arrow_down", ["✓"] = "check", ["✔"] = "check", ["✕"] = "close", ["✗"] = "close",
        ["🫐"] = "food_berries", ["🍿"] = "food_popcorn", ["🍖"] = "food_meatballs", ["🥔"] = "food_potatoes", ["🍵"] = "food_tea",
        ["🍲"] = "shop_pot", ["🫖"] = "shop_kettle", ["🍳"] = "shop_pan", ["🔥"] = "shop_oven", ["🎧"] = "shop_headphones",
        ["🛏"] = "shop_bed", ["🧊"] = "shop_fridge", ["📖"] = "shop_cookbook", ["🌱"] = "shop_plant", ["🧘"] = "shop_yoga_mat",
        ["📻"] = "shop_radio", ["🎲"] = "dice", ["🎨"] = "shop_easel", ["🎮"] = "shop_computer", ["🤸"] = "shop_trampoline",
        ["💡"] = "pencil", ["📝"] = "notes", ["📋"] = "tasks", ["🛋"] = "couch", ["🎁"] = "gift", ["🚶"] = "walk", ["🔒"] = "lock",
        ["💤"] = "moon", ["🌙"] = "moon", ["☀"] = "sun", ["❤"] = "heart", ["♪"] = "music",
        ["🌡"] = "shop_heater", ["🗄"] = "shop_desk", ["🐦"] = "shop_feeder", ["🫓"] = "food_sandwich", ["🫕"] = "food_roast_soup", ["🍅"] = "food_pressed_caprese", ["🥬"] = "food_pressed_veggie",
        ["🍗"] = "food_pressed_chicken", ["🥯"] = "food_pressed_egg", ["🍄"] = "food_pressed_mushroom", ["🥓"] = "food_pressed_sandwich", ["📌"] = "pin", ["➖"] = "minimize", ["⧉"] = "copy", ["★"] = "star", ["🔄"] = "reroll", ["👁"] = "hide", ["🔊"] = "sound", ["☰"] = "menu", ["🐻"] = "bear", ["🧶"] = "shop_yarn_basket", ["🎸"] = "shop_guitar", ["🪔"] = "shop_lamp",
        ["🥄"] = "shop_mixer", ["🧇"] = "shop_sandwich", ["🍨"] = "shop_icecream", ["♨"] = "shop_grill",
        ["🍋"] = "food_lemonade", ["🥣"] = "food_soup", ["🌽"] = "food_corn", ["🍫"] = "food_cocoa", ["🌼"] = "food_linden_tea",
        ["🥚"] = "food_eggs", ["🥞"] = "food_pancakes", ["🫘"] = "food_espresso", ["🥛"] = "food_latte", ["🟤"] = "food_mocha",
        ["🫧"] = "food_cappuccino", ["🧋"] = "food_iced_coffee", ["🍪"] = "food_cookies", ["🧁"] = "food_muffins", ["🍕"] = "food_pizza",
        ["🥤"] = "food_smoothie", ["🍞"] = "food_toast", ["🥪"] = "food_sandwich_ham", ["🍌"] = "food_sandwich_honey",
        ["🟨"] = "food_waffle_honey", ["🟫"] = "food_waffle_choco", ["🍧"] = "food_waffle_icecream", ["🍦"] = "food_icecream_honey",
        ["🟣"] = "food_icecream_berry", ["🍢"] = "food_skewers", ["🫑"] = "food_veggies", ["🥖"] = "food_grilled_bread",
        // Диалозите и разговорите (razgovori.txt): всяко емоджи – с най-близката пикселна иконка.
        ["⏳"] = "hourglass", ["⏱"] = "clock", ["📅"] = "clock", ["😴"] = "emote_zzz", ["🥱"] = "emote_zzz", ["🛌"] = "bed",
        ["🦉"] = "moon", ["🌑"] = "moon", ["🌕"] = "moon", ["🌌"] = "moon", ["🌠"] = "star", ["🤔"] = "emote_question",
        ["❓"] = "emote_question", ["🤷"] = "emote_question", ["🤫"] = "quiet", ["🙊"] = "quiet", ["🤐"] = "quiet", ["🙉"] = "quiet",
        ["😊"] = "emote_happy", ["😄"] = "emote_happy", ["🙂"] = "emote_happy", ["😂"] = "emote_happy", ["😅"] = "emote_sweat", ["😉"] = "emote_happy",
        ["🥰"] = "heart", ["😌"] = "emote_happy", ["🤗"] = "heart", ["😋"] = "food", ["😐"] = "emote_sweat", ["😶"] = "emote_sweat",
        ["😒"] = "emote_sweat", ["😳"] = "emote_sweat", ["🧱"] = "emote_sweat", ["👀"] = "emote_sweat", ["🤯"] = "emote_exclaim", ["😮"] = "emote_exclaim",
        ["😲"] = "emote_exclaim", ["😱"] = "emote_exclaim", ["😤"] = "emote_sad", ["😣"] = "emote_sad", ["😔"] = "emote_sad", ["😩"] = "emote_sad",
        ["😟"] = "emote_sad", ["🥀"] = "emote_sad", ["🤧"] = "emote_cold", ["❄"] = "w_snow", ["💛"] = "heart", ["💖"] = "heart",
        ["💙"] = "heart", ["💚"] = "heart", ["🩶"] = "heart", ["✅"] = "check", ["👍"] = "check", ["🙆"] = "check",
        ["🙋"] = "check", ["👏"] = "star", ["🙅"] = "close", ["🙈"] = "hide", ["🙏"] = "check", ["✏"] = "pencil",
        ["🖊"] = "pencil", ["🖍"] = "pencil", ["🖌"] = "shop_easel", ["📄"] = "notes", ["📒"] = "notes", ["📧"] = "notes",
        ["💪"] = "tasks", ["🚀"] = "star", ["✨"] = "emote_sparkle", ["🌟"] = "star", ["🎞"] = "picture", ["🎬"] = "picture",
        ["🖼"] = "picture", ["🌄"] = "picture", ["📚"] = "book", ["📱"] = "game", ["💻"] = "shop_computer", ["📺"] = "game",
        ["🃏"] = "board", ["🧩"] = "board", ["🎵"] = "music", ["🎤"] = "music", ["🎹"] = "music", ["🎙"] = "music",
        ["🌲"] = "plant", ["🌳"] = "plant", ["🌿"] = "plant", ["🪴"] = "plant", ["🌵"] = "plant", ["🌸"] = "plant",
        ["🌻"] = "shop_feeder", ["💧"] = "plant", ["☁"] = "w_cloud", ["🌧"] = "w_rain", ["☔"] = "w_rain", ["⛈"] = "w_storm",
        ["🌤"] = "w_partly", ["🌪"] = "w_wind", ["🌞"] = "sun", ["🌅"] = "sun", ["🌇"] = "sun", ["🌴"] = "sun",
        ["🏖"] = "sun", ["🏡"] = "couch", ["🏠"] = "couch", ["🍽"] = "food", ["🍜"] = "food", ["🍝"] = "food",
        ["🥗"] = "food", ["🍰"] = "food", ["🥐"] = "food", ["🍎"] = "food", ["🥜"] = "nut", ["🧀"] = "food",
        ["🍓"] = "food_berries", ["🍭"] = "food", ["🧣"] = "scarf", ["👕"] = "shirt", ["👖"] = "pajama", ["🧸"] = "bear",
        ["🎈"] = "gift", ["🎄"] = "gift", ["🎂"] = "gift", ["🎆"] = "star", ["🏃"] = "walk",
    };

    /// <summary>
    /// Пише текст в TextBlock, като сменя познатите емоджи с пикселни иконки
    /// (останалите емоджи остават като букви).
    /// </summary>
    /// <summary>
    /// Като SetRichText, но в ред (StackPanel): иконките са центрирани спрямо буквите
    /// (в TextBlock с моно шрифта стояха забити горе). style – шрифт/цвят на буквите.
    /// </summary>
    /// <param name="nudge">С колко пиксела на иконката да я свали надолу (табелката с таймера: 1).</param>
    public static void SetIconLine(StackPanel row, string text, Action<TextBlock> style, int nudge = 0)
    {
        row.Children.Clear();
        var plain = new System.Text.StringBuilder();
        void Flush()
        {
            if (plain.Length == 0) return;
            var t = new TextBlock { Text = plain.ToString(), VerticalAlignment = VerticalAlignment.Center };
            style(t);
            row.Children.Add(t);
            plain.Clear();
        }
        var e = System.Globalization.StringInfo.GetTextElementEnumerator(text);
        while (e.MoveNext())
        {
            string el = e.GetTextElement().Replace("\uFE0F", "");
            if (IconFor(el) is { } icon)
            {
                Flush();
                var image = Icon(icon);
                image.VerticalAlignment = VerticalAlignment.Center;
                image.Margin = new Thickness(1, 0, 3, 0);
                row.Children.Add(image);
            }
            else if (!IsEmoji(el)) plain.Append(el);
        }
        Flush();
        AlignIconsToDigits(row, nudge);
    }

    /// <summary>Отместването на иконката (отгоре), за да е средата ѝ на средата на цифрите.</summary>
    internal static double LastIconOffset { get; private set; }

    /// <summary>
    /// Иконките и буквите в реда: средата на иконката – точно на средата на цифрите
    /// (по очертанията на шрифта; редът на шрифта има празно място отгоре, затова
    /// „центрирано“ по реда изглеждаше забито нагоре).
    /// </summary>
    private static void AlignIconsToDigits(StackPanel row, int nudge = 0)
    {
        var text = row.Children.OfType<TextBlock>().FirstOrDefault();
        if (text == null) return;
        double icon = row.Children.OfType<FrameworkElement>().Where(c => c is not TextBlock).Select(c => c.Height).DefaultIfEmpty(0).Max();
        if (double.IsNaN(icon) || icon <= 0) return;
        var (top, bottom, height) = DigitInk(text);
        if (height <= 0) return;
        // Средата на иконката – на средата на изрисуваните цифри (мерено по пикселите).
        double center = (top + bottom) / 2;
        double textTop = Math.Max(0, icon / 2 - center);
        double iconTop = Math.Round(textTop + center - icon / 2) + nudge * (double)_k / _dpi;
        LastIconOffset = iconTop - textTop;
        foreach (FrameworkElement c in row.Children)
        {
            c.VerticalAlignment = VerticalAlignment.Top;
            var m = c.Margin;
            c.Margin = new Thickness(m.Left, c is TextBlock ? textTop : iconTop, m.Right, 0);
        }
    }

    private static readonly Dictionary<string, (double Top, double Bottom, double Height)> DigitInkCache = new();

    /// <summary>
    /// Къде са изрисувани цифрите в реда на TextBlock-а (горе/долу, в DIP): рисува „0“
    /// със същия шрифт и търси първия и последния ред с пиксели. Пази се за шрифт и размер.
    /// </summary>
    private static (double Top, double Bottom, double Height) DigitInk(TextBlock like)
    {
        string key = $"{like.FontFamily}|{like.FontSize}|{like.FontWeight}";
        if (DigitInkCache.TryGetValue(key, out var known)) return known;
        var probe = new TextBlock
        {
            Text = "0",
            FontFamily = like.FontFamily,
            FontSize = like.FontSize,
            FontWeight = like.FontWeight,
            Foreground = Brushes.Black,
        };
        TextOptions.SetTextRenderingMode(probe, TextRenderingMode.Aliased);
        TextOptions.SetTextFormattingMode(probe, TextFormattingMode.Display);
        probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = probe.DesiredSize;
        probe.Arrange(new Rect(size));
        int w = Math.Max(1, (int)Math.Ceiling(size.Width)), h = Math.Max(1, (int)Math.Ceiling(size.Height));
        var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(probe);
        var px = new byte[w * h * 4];
        bmp.CopyPixels(px, w * 4, 0);
        int first = -1, last = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (px[(y * w + x) * 4 + 3] > 40)
                {
                    if (first < 0) first = y;
                    last = y;
                    break;
                }
        var result = first < 0 ? (0.0, 0.0, 0.0) : (first, last + 1.0, (double)h);
        DigitInkCache[key] = result;
        return result;
    }

    public static void SetRichText(TextBlock block, string text)
    {
        block.Inlines.Clear();
        var plain = new System.Text.StringBuilder();
        bool skipSpace = false;
        // *действие* (напр. „*прозява се*“) е със сиви букви, без звездичките.
        bool action = false;
        void Flush()
        {
            if (plain.Length == 0) return;
            var run = new System.Windows.Documents.Run(plain.ToString());
            if (action) run.Foreground = ActionInk;
            block.Inlines.Add(run);
            plain.Clear();
        }
        var e = System.Globalization.StringInfo.GetTextElementEnumerator(text);
        while (e.MoveNext())
        {
            string el = e.GetTextElement().Replace("\uFE0F", "");
            if (el == "*")
            {
                Flush();
                action = !action;
                continue;
            }
            if (IconFor(el) is { } icon)
            {
                Flush();
                var image = Icon(icon);
                image.Margin = new Thickness(1, 0, 1, 0);
                block.Inlines.Add(new System.Windows.Documents.InlineUIContainer(image)
                {
                    BaselineAlignment = BaselineAlignment.Center,
                });
            }
            else if (IsEmoji(el))
            {
                // Непознато емоджи: не го показваме (в пикселния шрифт стои неравно).
                if (plain.Length == 0 && block.Inlines.Count == 0) skipSpace = true;
            }
            else if (skipSpace && el == " ") skipSpace = false;
            else
            {
                skipSpace = false;
                plain.Append(el);
            }
        }
        Flush();
    }

    /// <summary>Цветът на *действията* в балончетата.</summary>
    public static readonly Brush ActionInk = Frozen(Color.FromRgb(0x8E, 0x8E, 0x8E));

    private static Brush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    /// <summary>Пикселната иконка за емоджи (или null); сложните („🧑‍🎨“) – по първото.</summary>
    internal static string? IconFor(string el)
    {
        if (EmojiIcons.TryGetValue(el, out var icon) && LoadIcon(icon) != null) return icon;
        int zwj = el.IndexOf('\u200D');
        if (zwj > 0 && EmojiIcons.TryGetValue(el[..zwj], out icon) && LoadIcon(icon) != null) return icon;
        return null;
    }

    // Емоджи без иконка не се показва (в пикселния шрифт стои неравно). Знаците за смятане
    // (−, ≠) и чертите (─) са си текст. „0️⃣“ започва с обикновена цифра – познава се по \u20E3.
    private static bool IsEmoji(string el) =>
        char.IsSurrogate(el[0]) || el.Contains('\u20E3')
        || el[0] is >= '\u2190' and <= '\u21FF' or >= '\u2300' and <= '\u23FF' or >= '\u25A0' and <= '\u27BF' or >= '\u2B00' and <= '\u2BFF';

    /// <summary>TextBlock с текст, в който емоджитата са пикселни иконки.</summary>
    public static TextBlock RichText(string text, double? size = null, bool bold = false)
    {
        var block = new TextBlock
        {
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
            FontSize = size ?? Body,
            VerticalAlignment = VerticalAlignment.Center,
        };
        SetRichText(block, text);
        return block;
    }

    /// <summary>Иконка и текст на един ред, подравнени по средата.</summary>
    private static readonly Dictionary<(BitmapSource, Color, Color), BitmapSource> ThemedCache = new();

    /// <summary>
    /// Рисунка (балонче) в цветовете на менюто: тъмният контур (59,36,20) става цветът на
    /// буквите, а бялото – на хартията. Сменя се, когато се смени скинът на менюто.
    /// </summary>
    public static BitmapSource Themed(BitmapSource src)
    {
        var ink = ((SolidColorBrush)Ink).Color;
        var paper = ((SolidColorBrush)Paper).Color;
        if (ThemedCache.TryGetValue((src, ink, paper), out var done)) return done;
        var conv = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
        int w = conv.PixelWidth, h = conv.PixelHeight, stride = w * 4;
        var px = new byte[h * stride];
        conv.CopyPixels(px, stride, 0);
        for (int i = 0; i < px.Length; i += 4)
        {
            if (px[i + 3] == 0) continue;
            byte b = px[i], g = px[i + 1], r = px[i + 2];
            if (r == 59 && g == 36 && b == 20) { px[i] = ink.B; px[i + 1] = ink.G; px[i + 2] = ink.R; }
            else if (r == 255 && g == 255 && b == 255) { px[i] = paper.B; px[i + 1] = paper.G; px[i + 2] = paper.R; }
        }
        var result = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, px, stride);
        result.Freeze();
        ThemedCache[(src, ink, paper)] = result;
        return result;
    }

    /// <summary>„+3 🌰“: числото, после лешникът.</summary>
    public static StackPanel RewardText(int nuts, bool bold = false)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(RichText($"+{nuts}", null, bold));
        var i = Icon("nut");
        i.VerticalAlignment = VerticalAlignment.Center;
        i.Margin = new Thickness(4, 0, 0, 0);
        row.Children.Add(i);
        return row;
    }

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
        if (text.Length > 0) row.Children.Add(RichText(text, size, bold));
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
    public static void SetButton(Button button, string text, string? icon = null)
    {
        // Менюто се опреснява всяка секунда: сменяме съдържанието само ако нещо е различно.
        string key = icon + "\u0001" + text;
        if (ButtonText.TryGetValue(button, out var old) && old.Value == key) return;
        ButtonText.AddOrUpdate(button, new System.Runtime.CompilerServices.StrongBox<string>(key));
        button.Content = IconText(icon, text, bold: true);
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Button, System.Runtime.CompilerServices.StrongBox<string>> ButtonText = new();

    // Три вида неща, навсякъде еднакво:
    // * натиска се – изпъкнало: дебел ръб отдолу (Raised), свети при посочване;
    // * избрано (включено) – медено и натиснато навътре: дебел ръб отгоре (Sunken);
    // * не се натиска – заглавие (само текст с черта отдолу) или плоска рамка (Flat).
    public static readonly Thickness Raised = new(2, 2, 2, 4);
    public static readonly Thickness Sunken = new(2, 4, 2, 2);
    public static readonly Thickness Flat = new(2);

    /// <summary>Избрано или не (копче или бутон-превключвател).</summary>
    public static void SetOn(FrameworkElement element, bool on)
    {
        element.Tag = on;
        switch (element)
        {
            case Border b:
                b.Background = on ? Honey : PaperDark;
                b.BorderThickness = on ? Sunken : Raised;
                break;
            case Control c:
                c.Background = on ? Honey : PaperDark;
                c.BorderThickness = on ? Sunken : Raised;
                break;
        }
    }

    private static bool IsOn(FrameworkElement e) => e.Tag is true;

    /// <summary>Съкратено до толкова знака (на един ред), с „…“ накрая.</summary>
    public static string Shorten(string text, int max)
    {
        string t = text.Replace('\n', ' ').Trim();
        return t.Length > max ? t[..max].TrimEnd() + "…" : t;
    }

    /// <summary>Малко копче (размер на задача, ✕ и др.).</summary>
    public static Border Chip(string text, Action onClick)
    {
        var chip = new Border
        {
            Background = PaperDark,
            BorderBrush = Ink,
            BorderThickness = Raised,
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(8, 3, 8, 4),
            Cursor = Cursors.Hand,
            Child = ChipContent(text),
        };
        chip.MouseEnter += (_, _) => { if (!IsOn(chip)) chip.Background = Hover; };
        chip.MouseLeave += (_, _) => { if (!IsOn(chip)) chip.Background = PaperDark; };
        // Натискането не стига до родителя (например до дръжката за местене на панела).
        chip.MouseLeftButtonDown += (_, e) => e.Handled = true;
        chip.MouseLeftButtonUp += (_, e) =>
        {
            onClick();
            e.Handled = true;
        };
        return chip;
    }

    private static TextBlock ChipContent(string text)
    {
        var t = RichText(text, bold: true);
        t.HorizontalAlignment = HorizontalAlignment.Center;
        return t;
    }

    /// <summary>Сменя текста на копче.</summary>
    public static void SetChip(Border chip, string text) => SetRichText((TextBlock)chip.Child, text);

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
        // През Binding: иначе шаблонът би замразил четката и скинът не би я сменил.
        hover.Setters.Add(new Setter(Border.BackgroundProperty, new Binding { Source = Hover }, "bd"));
        template.Triggers.Add(hover);
        var pressed = new Trigger { Property = System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty, Value = true };
        pressed.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(2, 4, 2, 2), "bd"));
        template.Triggers.Add(pressed);
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.5));
        disabled.Setters.Add(new Setter(Border.BorderThicknessProperty, Flat, "bd"));      // не изпъква: не се натиска
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
    /// Не е бутон: само текст с черта отдолу през цялата ширина.
    public static Border Ribbon(string text, string? icon = null) => new()
    {
        Background = Brushes.Transparent,
        BorderBrush = WoodLight,
        BorderThickness = new Thickness(0, 0, 0, 2),
        Padding = new Thickness(2, 0, 2, 3),
        Margin = new Thickness(0, 12, 0, 8),
        HorizontalAlignment = HorizontalAlignment.Stretch,
        Child = IconText(icon, text, bold: true),
    };

    /// <summary>Табелка с мед (брой лешници и др.).</summary>
    public static Border Pill(UIElement content) => new()
    {
        Background = Honey,
        BorderBrush = Ink,
        BorderThickness = Flat,
        CornerRadius = new CornerRadius(10),
        Padding = new Thickness(8, 2, 10, 3),
        Child = content,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>Подсказка (при посочване) в стила на играта.</summary>
    public static ToolTip Tip(string text) => new()
    {
        Content = Wrapped(text, 280),
        FontFamily = GameFont,
        Background = Cream,
        Foreground = Ink,
        BorderBrush = Ink,
        BorderThickness = new Thickness(2),
        Padding = new Thickness(8, 5, 8, 6),
    };

    private static TextBlock Wrapped(string text, double maxWidth)
    {
        var t = RichText(text);
        t.TextWrapping = TextWrapping.Wrap;
        t.MaxWidth = maxWidth;
        return t;
    }

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
