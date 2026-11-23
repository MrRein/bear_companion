using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using static Mecho.Ui;

namespace Mecho;

/// <summary>
/// Целият проект: дълга лента през 15-те етапа; на нея за всеки етап – звено (квадратче), от
/// което линийка води до наградата му – сандъче. Състоянията на сандъчето:
///  1. отворено – зелено квадратче, отворено сандъче; звеното – зелено с отметка;
///  2. отключено, неотворено – затворено, свети в жълто и от време на време потрепва;
///     звеното – зелено с отметка; цъкаш – отваря се прозорче с това, което получаваш;
///  3. етапът, към който вървиш – затворено сандъче в цвят, с медна рамка;
///  4. по-нататък – сив „?“.
/// Сандъчетата: обикновено (1–2 неща), ценно (3), много ценно (4), а финалът – специално и голямо.
/// Изминатото е зелено; посочиш ли лентата – балонче с Мечо над края на зеленото.
/// </summary>
internal sealed class ProjectWindow : FloatWindow
{
    private static ProjectWindow? _open;
    private readonly StackPanel _content = new();
    private readonly StackPanel _headTitle = new();
    private readonly Grid _overlay = new() { Visibility = Visibility.Collapsed };
    private readonly DispatcherTimer _wiggle = new();
    private readonly List<FrameworkElement> _claimBoxes = new();
    private int _pop = -1;          // кое да подскочи (току-що минато или взето)
    private string _key = "";

    public static ProjectWindow? Current => _open;

    private static bool Instant => SelfTest.Running;

    private static double SegW => 176 * U;      // едно звено
    private static double Gap => 8 * U;         // между звената
    private static double BoxSize => 76 * U;    // квадратчето с наградата
    private static double FinalBox => 104 * U;  // финалът – по-голямо

    public static void Toggle(PetWindow pet, Window? near = null)
    {
        if (_open != null)
        {
            _open.Close();
            return;
        }
        Open(pet, near);
    }

    /// <summary>Отваря проекта; celebrate – етапът, който току-що мина (подскача).</summary>
    public static ProjectWindow Open(PetWindow pet, Window? near = null, int celebrate = -1, string reward = "")
    {
        if (_open != null)
        {
            _open._pop = celebrate;
            _open.Rebuild();
            return _open;
        }
        var w = new ProjectWindow(pet) { _pop = celebrate };
        if (celebrate >= 0) w.ShowActivated = false;   // отваря се сам – не краде фокуса от работата
        w.Rebuild();
        w.Place(near ?? pet.MenuWin);
        w.Show();
        return w;
    }

    private ProjectWindow(PetWindow pet) : base(pet, "project", 820 * U)
    {
        _open = this;
        Title = "Проектът на Мечо";
        Body.Content = _content;
        // Прозорчето „Получи:“ – върху целия прозорец (и рамката), не само върху съдържанието.
        if (Content is UIElement frame)
        {
            Content = null;
            var whole = new Grid();
            whole.Children.Add(frame);
            whole.Children.Add(_overlay);
            Content = whole;
        }
        Head.Children.Add(_headTitle);
        // Като другите прозорчета: цъкнеш ли извън него – затваря се.
        Deactivated += (_, _) => Dispatcher.BeginInvoke(() =>
        {
            if (!IsLoaded || IsActive || SelfTest.Running) return;
            Close();
        }, DispatcherPriority.Input);
        // Отворен сам (празник, без фокус): мине ли мишката отгоре – става активен, за да се затваря като другите.
        MouseEnter += (_, _) => { if (!IsActive) Activate(); };
        // Сандъчето, което чака да го отвориш, потрепва от време на време.
        _wiggle.Tick += (_, _) =>
        {
            _wiggle.Interval = TimeSpan.FromSeconds(3 + Random.Shared.NextDouble() * 3);
            if (_claimBoxes.Count > 0) Wiggle(_claimBoxes[Random.Shared.Next(_claimBoxes.Count)]);
        };
        Closed += (_, _) =>
        {
            if (_open == this) _open = null;
            pet.CareChanged -= OnCare;
            _wiggle.Stop();
        };
        pet.CareChanged += OnCare;
    }

    private string Key => $"{Pet.Save.ProjectPaid}|{(int)(Pet.ProjectProgress * 40)}|{Pet.GameName}|{Pet.ProjectClaimed}|{Pet.PendingQuestion?.Key}";

    private void OnCare(object? sender, EventArgs e)
    {
        if (Key == _key) return;
        _pop = -1;
        Rebuild();
    }

    private static Brush Soft(Brush tint, double mix) => MenuWindow.Soft(tint, mix);

    /// <summary>Докъде е показана зелената лента (−1 – още не е; анимира се само растежът).</summary>
    private double _shownGreen = -1;

    public void Rebuild()
    {
        var pet = Pet;
        _key = Key;
        _content.Children.Clear();
        _claimBoxes.Clear();
        _wiggle.Stop();

        // Горе (в средата): „Настолна игра“ със зарчето и името ѝ; под него – моливче и етапът.
        _headTitle.Children.Clear();
        var title = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        var ic = Icon("dice", 2);
        ic.VerticalAlignment = VerticalAlignment.Center;
        ic.Margin = new Thickness(0, 0, 8 * U, 0);
        title.Children.Add(ic);
        title.Children.Add(new TextBlock
        {
            Text = "Настолна игра" + (pet.GameName.Length > 0 ? " „" + pet.GameName + "“" : ""),
            FontWeight = FontWeights.Bold, FontSize = Ui.Title, VerticalAlignment = VerticalAlignment.Center,
        });
        _headTitle.Children.Add(title);
        var now = pet.ProjectNow;
        var stageRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6 * U, 0, 0) };
        var pencil = Icon("pencil");
        pencil.VerticalAlignment = VerticalAlignment.Center;
        pencil.Margin = new Thickness(0, 0, 6 * U, 0);
        stageRow.Children.Add(pencil);
        var stageLine = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        if (pet.ProjectDone) stageLine.Inlines.Add(new System.Windows.Documents.Run("Играта излезе!") { FontWeight = FontWeights.Bold });
        else
        {
            stageLine.Inlines.Add(new System.Windows.Documents.Run("Етап ") { Foreground = Muted });
            stageLine.Inlines.Add(new System.Windows.Documents.Run(now.Info.Name) { FontWeight = FontWeights.Bold });
        }
        stageRow.Children.Add(stageLine);
        _headTitle.Children.Add(stageRow);
        if (!pet.ProjectDone && now.Info.Desc.Length > 0)
            _headTitle.Children.Add(new TextBlock { Text = now.Info.Desc, FontStyle = FontStyles.Italic, Foreground = Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 1 * U, 0, 0) });

        _content.Children.Add(Track());
        // Проектът чака: въпрос от Мечо (отговаряш тук) или неотворено сандъче.
        if (pet.PendingQuestion is { } q) _content.Children.Add(QuestionBox(q));
        else if (pet.ProjectHasClaim)
            _content.Children.Add(new TextBlock
            {
                Text = "Отвори светещото сандъче, за да продължи проектът.", FontWeight = FontWeights.Bold, Foreground = Ink,
                HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8 * U, 0, 0),
            });
        // Пунктир, а под него – трите подетапа на сегашния етап.
        if (!pet.ProjectDone)
        {
            _content.Children.Add(new Border
            {
                Height = P, Background = UiSkin.Line("dash") ?? WoodLight, Margin = new Thickness(6 * U, 8 * U, 6 * U, 10 * U),
            });
            _content.Children.Add(Steps());
        }
        if (_claimBoxes.Count > 0 && !Instant)
        {
            _wiggle.Interval = TimeSpan.FromSeconds(1.5);
            _wiggle.Start();
        }
    }

    /// <summary>
    /// Рамка „от пиксели“: ръбовете са плътни ленти, а ъглите – изрязани (стъпало от един
    /// пиксел), като в пиксел игрите; вътре – запълване и съдържание.
    /// </summary>
    private static Grid PixelFrame(double w, double h, Brush fill, Brush border, double t, UIElement? child)
    {
        var g = new Grid { Width = w, Height = h, Background = Brushes.Transparent };
        g.Children.Add(new Rectangle { Fill = fill, Margin = new Thickness(t) });
        g.Children.Add(new Rectangle { Fill = border, Height = t, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(t, 0, t, 0) });
        g.Children.Add(new Rectangle { Fill = border, Height = t, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(t, 0, t, 0) });
        g.Children.Add(new Rectangle { Fill = border, Width = t, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, t, 0, t) });
        g.Children.Add(new Rectangle { Fill = border, Width = t, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, t, 0, t) });
        foreach (var (ha, va) in new[] { (HorizontalAlignment.Left, VerticalAlignment.Top), (HorizontalAlignment.Right, VerticalAlignment.Top), (HorizontalAlignment.Left, VerticalAlignment.Bottom), (HorizontalAlignment.Right, VerticalAlignment.Bottom) })
            g.Children.Add(new Rectangle { Fill = border, Width = t, Height = t, HorizontalAlignment = ha, VerticalAlignment = va, Margin = new Thickness(t) });
        if (child != null)
        {
            var host = new Grid { Margin = new Thickness(t * 1.5) };
            host.Children.Add(child);
            g.Children.Add(host);
        }
        return g;
    }

    /// <summary>
    /// Мечо пита нещо за проекта (тема, име…): опциите и поле за свое. Докато не отговориш,
    /// проектът чака.
    /// </summary>
    private FrameworkElement QuestionBox(PetWindow.ProjectQuestion q)
    {
        var p = new StackPanel();
        var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6 * U) };
        var ic = Icon("small_bear", 2);
        ic.VerticalAlignment = VerticalAlignment.Center;
        ic.Margin = new Thickness(0, 0, 8 * U, 0);
        head.Children.Add(ic);
        head.Children.Add(new TextBlock { Text = q.Question, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 640 * U });
        p.Children.Add(head);
        var opts = new WrapPanel();
        foreach (var o in Pet.QuestionOptions(q))
        {
            string v = o;
            var chip = Chip(v, () => Pet.AnswerProject(q, v));
            chip.Margin = new Thickness(0, 0, 6, 4);
            opts.Children.Add(chip);
        }
        p.Children.Add(opts);
        var own = new DockPanel { Margin = new Thickness(0, 4 * U, 0, 0) };
        var box = new TextBox { MinWidth = 220 * U, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(4) };
        var go = Button(q.Key == "name" ? "Кръсти я" : "Това", () => Pet.AnswerProject(q, box.Text), primary: true);
        DockPanel.SetDock(go, Dock.Right);
        go.Margin = new Thickness(6 * U, 0, 0, 0);
        own.Children.Add(go);
        own.Children.Add(box);
        box.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) Pet.AnswerProject(q, box.Text); };
        p.Children.Add(new TextBlock { Text = q.FreePrompt, Foreground = Muted, Margin = new Thickness(0, 4 * U, 0, 2 * U) });
        p.Children.Add(own);
        var frame = PixelFrameAuto(p);
        frame.Margin = new Thickness(0, 10 * U, 0, 0);
        return frame;
    }

    /// <summary>Пикселна рамка около съдържание (с размера на съдържанието).</summary>
    private static Grid PixelFrameAuto(UIElement child)
    {
        var g = PixelFrame(0, 0, Soft(Honey, 0.25), Ink, Math.Max(2, P), null);
        g.Width = g.Height = double.NaN;
        g.Margin = new Thickness(0, 0, 0, 8 * U);
        g.Children.Add(new Border { Padding = new Thickness(14 * U, 10 * U, 14 * U, 12 * U), Child = child });
        return g;
    }

    /// <summary>Почти сиво (с лек оттенък) – за нещо, което още не е твое.</summary>
    private static BitmapSource Gray(BitmapSource src)
    {
        var b = src.Format == PixelFormats.Bgra32 ? src : new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
        int w = b.PixelWidth, h = b.PixelHeight, stride = w * 4;
        var px = new byte[stride * h];
        b.CopyPixels(px, stride, 0);
        for (int i = 0; i < px.Length; i += 4)
        {
            double g = (px[i] * 0.11 + px[i + 1] * 0.59 + px[i + 2] * 0.3) * 0.7 + 70;
            px[i] = (byte)Math.Min(255, g * 0.75 + px[i] * 0.25);
            px[i + 1] = (byte)Math.Min(255, g * 0.75 + px[i + 1] * 0.25);
            px[i + 2] = (byte)Math.Min(255, g * 0.75 + px[i + 2] * 0.25);
        }
        var outBmp = BitmapSource.Create(w, h, b.DpiX, b.DpiY, PixelFormats.Bgra32, null, px, stride);
        outBmp.Freeze();
        return outBmp;
    }

    private static Image Art(BitmapSource src, double size, bool gray)
    {
        var img = new Image { Source = gray ? Gray(src) : src, Width = size, Height = size, Stretch = Stretch.Uniform, SnapsToDevicePixels = true };
        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.NearestNeighbor);
        return img;
    }

    /// <summary>Сандъчето според това колко е пълно: обикновено (1–2), ценно (3), много ценно (4+); финалът – специално.</summary>
    private static string ChestArt(char tier) => tier switch { 'X' => "chest_legend", 'G' => "chest_gold", 'S' => "chest_silver", _ => "chest" };

    private static string ChestName(char tier) => tier switch { 'X' => "Специалното сандъче", 'G' => "Много ценно сандъче", 'S' => "Ценно сандъче", _ => "Сандъче" };

    /// <summary>Подсказката на сандъчето: заглавие и по ред за всяко нещо вътре (с иконка).</summary>
    private ToolTip RewardTip(string title, string plan)
    {
        var tip = Tip("");
        tip.Content = ItemsList(title, plan, 1);
        return tip;
    }

    private StackPanel ItemsList(string title, string plan, int size)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 4 * size) });
        foreach (var item in PetWindow.RewardItems(plan))
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1 * size, 0, 1 * size) };
            var ic = Icon(PetWindow.ItemIcon(item), size);
            ic.VerticalAlignment = VerticalAlignment.Center;
            ic.Margin = new Thickness(0, 0, 6 * size, 0);
            row.Children.Add(ic);
            row.Children.Add(new TextBlock { Text = Pet.ItemLabel(item), VerticalAlignment = VerticalAlignment.Center, FontWeight = size > 1 ? FontWeights.Bold : FontWeights.Normal });
            stack.Children.Add(row);
        }
        return stack;
    }

    /// <summary>
    /// Лентата: една дълга линия през всички етапи; на нея за всеки етап – звено, от което
    /// линийка води до сандъчето му. Изминатото е зелено, с „крайче“ в края.
    /// </summary>
    private FrameworkElement Track()
    {
        var pet = Pet;
        int paid = pet.Save.ProjectPaid, n = pet.ProjectStages.Length;
        int cur = pet.ProjectDone ? -1 : paid;
        double frac = pet.ProjectProgress;
        double colW = SegW + Gap, px = Math.Max(2, P);
        double boxBottom = 10 * U + FinalBox, lineY = boxBottom + 34 * U, nameY = lineY + 30 * U;
        double width = colW * n + 24 * U, height = nameY + 2.8 * Ui.Body + 6 * U;
        double Xc(int i) => 12 * U + i * colW + colW / 2;
        double startX = Xc(0) - colW / 2 + 6 * U, endX = Xc(n - 1) + colW / 2 - 6 * U;
        double lineH = 34 * U;
        var canvas = new Canvas { Width = width, Height = height };

        double reached = pet.ProjectDone ? Xc(n - 1) : (cur == 0 ? startX : Xc(cur - 1)) + ((cur == 0 ? Xc(0) - startX : colW)) * frac;

        // ── Линията: празна; изминатото – зелено, с готино крайче ──
        var line = PixelFrame(endX - startX, lineH, Empty, Ink, px, null);
        Canvas.SetLeft(line, startX);
        Canvas.SetTop(line, lineY - lineH / 2);
        canvas.Children.Add(line);
        double greenW = Math.Max(0, reached - startX - px);
        if (greenW > 0)
        {
            double gh = lineH - 2 * px;
            var green = new Grid { Width = greenW, Height = gh, ClipToBounds = true };
            green.Children.Add(new Rectangle { Fill = Good });
            green.Children.Add(new Rectangle { Fill = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)), Height = px, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, px, 0, 0) });
            green.Children.Add(new Rectangle { Fill = new SolidColorBrush(Color.FromArgb(50, 0, 0, 0)), Height = px, VerticalAlignment = VerticalAlignment.Bottom });
            // Крайчето: по-светла ивица, а ъглите – на стъпала (като заоблено, но от пиксели).
            if (!pet.ProjectDone)
            {
                green.Children.Add(new Rectangle { Fill = new SolidColorBrush(Color.FromArgb(110, 255, 255, 255)), Width = px, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, px * 2, px, px * 2) });
                green.Children.Add(new Rectangle { Fill = Empty, Width = px, Height = px, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top });
                green.Children.Add(new Rectangle { Fill = Empty, Width = px, Height = px, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom });
                green.Children.Add(new Rectangle { Fill = new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)), Width = px, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, px, 0, px) });
            }
            Canvas.SetLeft(green, startX + px);
            Canvas.SetTop(green, lineY - lineH / 2 + px);
            canvas.Children.Add(green);
            // Анимира се само при отваряне и когато лентата наистина е пораснала (не при всяко прерисуване –
            // напр. след име или отговор).
            double from = _shownGreen < 0 ? Math.Max(0, greenW - colW * 0.6) : _shownGreen;
            if (!Instant && greenW > from + 0.5)
                green.Loaded += (_, _) => green.BeginAnimation(WidthProperty, new DoubleAnimation(from, greenW, TimeSpan.FromMilliseconds(1000))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
            _shownGreen = greenW;
        }

        for (int i = 0; i < n; i++)
        {
            int stage = i;
            bool final = i == n - 1;
            bool done = i < paid, isCur = i == cur;
            bool claim = pet.CanClaimProject(i), taken = done && pet.IsProjectOpened(i);
            bool revealed = done || isCur;      // нататък – „?“
            // Наградата се решава чак когато се види (иначе всички 16 се теглеха при първото
            // отваряне – напр. още без гардероб, и после никъде нямаше дрехи).
            string plan = revealed || claim ? pet.ProjectRewardPlan(i) : "";
            string gotPlan = taken && i < pet.Save.ProjectGot.Count && pet.Save.ProjectGot[i].Length > 0 ? pet.Save.ProjectGot[i] : plan;
            string name = pet.ProjectStages[i].Name;
            double cx = Xc(i), size = final ? FinalBox : BoxSize, boxTop = boxBottom - size;

            // ── Линийката от звеното до сандъчето ──
            var stem = new Border { Width = Math.Max(2, P), Height = lineY - boxBottom, Background = done ? Good : isCur ? Honey : WoodLight };
            Canvas.SetLeft(stem, cx - stem.Width / 2);
            Canvas.SetTop(stem, boxBottom);
            canvas.Children.Add(stem);

            // ── Звеното: минатото (отворено или не) – зелено с отметка ──
            double sq = isCur || claim ? 44 * U : 40 * U;
            var link = PixelFrame(sq, sq, done ? Good : Cream, Ink, isCur || claim ? px * 1.5 : px,
                done ? new Viewbox { Child = Icon("check"), Margin = new Thickness(5 * U) } : null);
            link.ToolTip = Tip(name);
            Canvas.SetLeft(link, cx - sq / 2);
            Canvas.SetTop(link, lineY - sq / 2);
            Panel.SetZIndex(link, 3);
            canvas.Children.Add(link);

            // ── Сандъчето ──
            var inside = new Grid();
            FrameworkElement art;
            if (!revealed)
            {
                art = Icon("question", 3);
                art.Opacity = 0.45;
            }
            else
            {
                string chest = ChestArt(pet.ProjectTier(i));
                art = Art(pet.PropImage(taken ? chest + "_open" : chest), size * 0.64, gray: false);
            }
            art.HorizontalAlignment = HorizontalAlignment.Center;
            art.VerticalAlignment = VerticalAlignment.Center;
            inside.Children.Add(art);
            var box = PixelFrame(size, size,
                taken ? Soft(Good, 0.3) : claim ? Soft(Honey, 0.55) : isCur ? Cream : Soft(WoodLight, 0.3),
                taken ? Good : claim ? Ink : isCur ? Honey : WoodLight,
                claim || isCur || final ? px * 1.5 : px, inside);
            if (claim && Honey is SolidColorBrush hb)
                box.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = hb.Color, BlurRadius = 22, ShadowDepth = 0, Opacity = 1 };
            box.ToolTip = !revealed ? Tip(final ? "Специалното сандъче – най-пълното, чака те в края" : "Изненада – ще се види, като стигнете до него")
                : RewardTip(ChestName(pet.ProjectTier(i)) + (taken ? " – взе:" : claim ? " – цъкни, за да го отвориш:" : ":"), gotPlan);
            ToolTipService.SetInitialShowDelay(box, 150);
            if (claim)
            {
                box.Cursor = link.Cursor = System.Windows.Input.Cursors.Hand;
                TaskCards.Clickable(box, () => Claim(stage));
                TaskCards.Clickable(link, () => Claim(stage));
                _claimBoxes.Add(box);
            }
            Canvas.SetLeft(box, cx - size / 2);
            Canvas.SetTop(box, boxTop);
            canvas.Children.Add(box);

            // ── Името под звеното ──
            var label = new TextBlock
            {
                Text = name, Width = SegW, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap,
                MaxHeight = 2.8 * Ui.Body, TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = done || isCur ? Ink : Muted, FontWeight = isCur || final ? FontWeights.Bold : FontWeights.Normal,
            };
            Canvas.SetLeft(label, cx - SegW / 2);
            Canvas.SetTop(label, nameY);
            canvas.Children.Add(label);

            if (i == _pop) { Pop(box, 0.5); Pop(link, 0.5); }
        }

        // ── Мечо: посочиш ли лентата – бяло балонче с него над края на зеленото ──
        bool between = !pet.ProjectDone && frac > 0.02 && frac < 0.98;
        if (between)
        {
            double bw = 52 * U, bh = 46 * U;
            var bubble = PixelFrame(bw, bh, Brushes.White, Ink, px, null);
            var bear = Icon("small_bear", 3);
            bear.HorizontalAlignment = HorizontalAlignment.Center;
            bear.VerticalAlignment = VerticalAlignment.Center;
            // Мечето в иконката е малко вляво и надолу – изместено, за да е точно в средата.
            bear.RenderTransform = new TranslateTransform(bw * 0.02, -bh * 0.035);
            bubble.Children.Add(bear);
            var tail = new Polygon
            {
                Points = new PointCollection { new Point(0, 0), new Point(12 * U, 0), new Point(6 * U, 8 * U) },
                Fill = Brushes.White, Stroke = Ink, StrokeThickness = px,
            };
            var pop = new Canvas { Width = bw, Height = bh + 8 * U, IsHitTestVisible = false, Visibility = Instant ? Visibility.Visible : Visibility.Hidden };
            pop.Children.Add(bubble);
            Canvas.SetLeft(tail, bw / 2 - 6 * U);
            Canvas.SetTop(tail, bh - px);
            pop.Children.Add(tail);
            Canvas.SetLeft(pop, reached - bw / 2);
            Canvas.SetTop(pop, lineY - lineH / 2 - bh - 10 * U);
            Panel.SetZIndex(pop, 6);
            canvas.Children.Add(pop);
            var hover = new Rectangle { Width = endX - startX, Height = lineH + 8 * U, Fill = Brushes.Transparent };
            Canvas.SetLeft(hover, startX);
            Canvas.SetTop(hover, lineY - lineH / 2 - 4 * U);
            Panel.SetZIndex(hover, 2);
            // Появява се плавно, след като задържиш мишката малко на лентата.
            var delay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
            delay.Tick += (_, _) =>
            {
                delay.Stop();
                pop.Opacity = 0;
                pop.Visibility = Visibility.Visible;
                pop.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
            };
            hover.MouseEnter += (_, _) => delay.Start();
            hover.MouseLeave += (_, _) =>
            {
                delay.Stop();
                pop.BeginAnimation(OpacityProperty, null);
                pop.Visibility = Visibility.Hidden;
            };
            canvas.Children.Add(hover);
        }

        int focus = pet.ProjectHasClaim ? pet.ProjectFirstClaim : Math.Max(0, Math.Min(paid, n - 1));
        return Scroller(canvas, Xc(focus));
    }

    /// <summary>Трите подетапа на сегашния етап – отмятат се, докато работите (не се задраскват).</summary>
    private FrameworkElement Steps()
    {
        var pet = Pet;
        var box = new StackPanel { Margin = new Thickness(4 * U, 0, 4 * U, 2 * U) };
        if (pet.ProjectDone) return box;
        var st = pet.ProjectNow.Info;
        int doneSteps = pet.ProjectStepsDone;
        for (int k = 0; k < st.Steps.Length; k++)
        {
            bool ok = k < doneSteps;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5 * U) };
            var mark = PixelFrame(18 * U, 18 * U, ok ? Good : Cream, Ink, Math.Max(1, P / 2), ok ? new Viewbox { Child = Icon("check"), Margin = new Thickness(2 * U) } : null);
            mark.VerticalAlignment = VerticalAlignment.Center;
            mark.Margin = new Thickness(0, 0, 8 * U, 0);
            row.Children.Add(mark);
            row.Children.Add(new TextBlock
            {
                Text = st.Steps[k], VerticalAlignment = VerticalAlignment.Center,
                FontWeight = !ok && k == doneSteps ? FontWeights.Bold : FontWeights.Normal,   // това, по което работи сега
            });
            box.Children.Add(row);
        }
        return box;
    }

    /// <summary>Отвори сандъчето: взима наградата и показва прозорче с това, което получи.</summary>
    private void Claim(int stage)
    {
        Pet.ClaimProjectReward(stage);
        _pop = stage;
        Rebuild();
        var s = Pet.Save;
        string got = stage < s.ProjectGot.Count ? s.ProjectGot[stage] : "";
        if (got.Length > 0) ShowOpened(stage, got);
    }

    /// <summary>
    /// Прозорчето „Получи:“ върху целия прозорец: отвореното сандъче, а под него нещата –
    /// всяко в тъмно слотче, голямо (дрехата – истинската, с цветовете ѝ), с името отдолу.
    /// </summary>
    private void ShowOpened(int stage, string got)
    {
        var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        var chest = Art(Pet.PropImage(ChestArt(Pet.ProjectTier(stage)) + "_open"), 72 * U, gray: false);
        chest.HorizontalAlignment = HorizontalAlignment.Center;
        panel.Children.Add(chest);
        panel.Children.Add(new TextBlock { Text = "Получи:", FontWeight = FontWeights.Bold, FontSize = Ui.Title, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6 * U, 0, 8 * U) });
        var slots = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = 640 * U };
        double px = Math.Max(2, P);
        foreach (var item in PetWindow.RewardItems(got))
        {
            var cell = new StackPanel { Width = 150 * U, Margin = new Thickness(4 * U, 0, 4 * U, 8 * U) };
            var art = ItemArt(item, 54 * U);
            art.HorizontalAlignment = HorizontalAlignment.Center;
            art.VerticalAlignment = VerticalAlignment.Center;
            var rarity = Loot.RarityOf(item.TrimStart('?'));
            var slot = PixelFrame(74 * U, 74 * U, Soft(Ink, 0.22), Ink, px, art);
            slot.HorizontalAlignment = HorizontalAlignment.Center;
            cell.Children.Add(slot);
            cell.Children.Add(new TextBlock { Text = Pet.ItemLabel(item), TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 4 * U, 0, 0) });
            if (rarity is { } r)
                cell.Children.Add(RarityColors.Tag(r));
            slots.Children.Add(cell);
        }
        panel.Children.Add(slots);
        var ok = Button("Супер!", () => _overlay.Visibility = Visibility.Collapsed, primary: true);
        ok.HorizontalAlignment = HorizontalAlignment.Center;
        ok.Margin = new Thickness(0, 6 * U, 0, 0);
        panel.Children.Add(ok);
        var card = new Grid { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var frame = PixelFrame(0, 0, Cream, Ink, px * 1.5, null);
        frame.Width = frame.Height = double.NaN;
        card.Children.Add(frame);
        card.Children.Add(new Border { Padding = new Thickness(24 * U, 14 * U, 24 * U, 14 * U), Child = panel });
        _overlay.Children.Clear();
        _overlay.Children.Add(new Rectangle { Fill = new SolidColorBrush(Color.FromArgb(150, 30, 18, 10)) });
        _overlay.Children.Add(card);
        _overlay.Visibility = Visibility.Visible;
        Pop(card, 0.6);
    }

    /// <summary>Голямата картинка на едно получено нещо (дрехата – самата дреха).</summary>
    private FrameworkElement ItemArt(string item, double size)
    {
        string kind = item.Split(':')[0], id = item.Contains(':') ? item[(item.IndexOf(':') + 1)..] : "";
        BitmapSource? src = kind switch
        {
            "box" => Pet.PropImage(PetWindow.GameBox),
            _ => Pet.LootArt(item),
        };
        src ??= LoadIcon(PetWindow.ItemIcon(item));
        return src != null ? Art(src, size, gray: false) : Icon("gift", 3);
    }

    /// <summary>Самопроверката: прозорчето „Получи:“ за снимка.</summary>
    internal void TestOpened(int stage, string got) => ShowOpened(stage, got);

    /// <summary>Потрепва веднъж (сандъчето, което чака да го отвориш).</summary>
    private static void Wiggle(FrameworkElement e)
    {
        var r = new RotateTransform(0);
        e.RenderTransformOrigin = new Point(0.5, 0.9);
        e.RenderTransform = r;
        var a = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(520), FillBehavior = FillBehavior.Stop };
        foreach (var (deg, t) in new[] { (0.0, 0.0), (-7.0, 0.15), (6.0, 0.35), (-4.0, 0.55), (2.0, 0.75), (0.0, 1.0) })
            a.KeyFrames.Add(new LinearDoubleKeyFrame(deg, KeyTime.FromPercent(t)));
        r.BeginAnimation(RotateTransform.AngleProperty, a);
    }

    /// <summary>
    /// Превъртане, което не личи като „плъзгач“: краищата меко избледняват само откъдето
    /// има още; малки стрелки (без рамка) – само тогава; колелцето и влаченето също местят.
    /// center – коя точка да е в средата при отваряне (сегашният етап).
    /// </summary>
    private static FrameworkElement Scroller(FrameworkElement content, double center)
    {
        var scroll = new ScrollViewer
        {
            Content = content,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        // Избледняване в краищата (маска): 0 – изцяло видимо.
        var leftStop = new GradientStop(Colors.Black, 0);
        var leftIn = new GradientStop(Colors.Black, 0.08);
        var rightIn = new GradientStop(Colors.Black, 0.92);
        var rightStop = new GradientStop(Colors.Black, 1);
        scroll.OpacityMask = new LinearGradientBrush(new GradientStopCollection { leftStop, leftIn, rightIn, rightStop }, new Point(0, 0), new Point(1, 0));

        FrameworkElement Arrow(string icon, int dir)
        {
            var a = new Border
            {
                Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.Hand, Padding = new Thickness(4 * U),
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = dir < 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right,
                Child = Icon(icon), Opacity = 0.7, ToolTip = Tip(dir < 0 ? "Назад" : "Напред"),
            };
            TaskCards.Clickable(a, () =>
            {
                double to = scroll.HorizontalOffset + dir * scroll.ViewportWidth * 0.6;
                scroll.ScrollToHorizontalOffset(Math.Clamp(to, 0, scroll.ScrollableWidth));
            });
            return a;
        }
        var left = Arrow("nav_prev", -1);
        var right = Arrow("nav_next", 1);
        void Update()
        {
            bool more0 = scroll.HorizontalOffset > 1, more1 = scroll.HorizontalOffset < scroll.ScrollableWidth - 1;
            leftStop.Color = more0 ? Colors.Transparent : Colors.Black;
            rightStop.Color = more1 ? Colors.Transparent : Colors.Black;
            left.Visibility = more0 ? Visibility.Visible : Visibility.Hidden;
            right.Visibility = more1 ? Visibility.Visible : Visibility.Hidden;
        }
        scroll.ScrollChanged += (_, _) => Update();
        scroll.Loaded += (_, _) =>
        {
            scroll.ScrollToHorizontalOffset(Math.Clamp(center - scroll.ViewportWidth / 2, 0, Math.Max(0, scroll.ScrollableWidth)));
            Update();
        };
        scroll.PreviewMouseWheel += (_, e) =>
        {
            scroll.ScrollToHorizontalOffset(scroll.HorizontalOffset - e.Delta * 0.6);
            e.Handled = true;
        };
        Point? grab = null;
        double grabOffset = 0;
        scroll.PreviewMouseLeftButtonDown += (_, e) => { grab = e.GetPosition(scroll); grabOffset = scroll.HorizontalOffset; };
        scroll.PreviewMouseMove += (_, e) =>
        {
            if (grab is not { } g || e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) { grab = null; return; }
            double dx = e.GetPosition(scroll).X - g.X;
            if (Math.Abs(dx) < 4 && !scroll.IsMouseCaptured) return;
            if (!scroll.IsMouseCaptured) scroll.CaptureMouse();
            scroll.ScrollToHorizontalOffset(grabOffset - dx);
            e.Handled = true;
        };
        scroll.MouseLeftButtonDown += (_, e) => e.Handled = true;   // влаченето не мести прозореца
        scroll.PreviewMouseLeftButtonUp += (_, e) =>
        {
            if (scroll.IsMouseCaptured) { scroll.ReleaseMouseCapture(); e.Handled = true; }
            grab = null;
        };
        var host = new Grid();
        host.Children.Add(scroll);
        host.Children.Add(left);
        host.Children.Add(right);
        return host;
    }

    /// <summary>Подскача веднъж: малко → голямо → нормално.</summary>
    private static void Pop(FrameworkElement e, double from)
    {
        if (Instant) return;
        var tr = new ScaleTransform(1, 1);
        e.RenderTransformOrigin = new Point(0.5, 0.5);
        e.RenderTransform = tr;
        var a = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(700), FillBehavior = FillBehavior.Stop };
        a.KeyFrames.Add(new EasingDoubleKeyFrame(from, KeyTime.FromPercent(0)));
        a.KeyFrames.Add(new EasingDoubleKeyFrame(1.2, KeyTime.FromPercent(0.55)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        a.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromPercent(1)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } });
        tr.BeginAnimation(ScaleTransform.ScaleXProperty, a);
        tr.BeginAnimation(ScaleTransform.ScaleYProperty, a);
    }
}

/// <summary>
/// Завършен проект (от „Завършени проекти“ в менюто): какво направиха Мечо и Тут заедно –
/// видът, името, какво реши Тут (тема, герой…), кога е завършен.
/// </summary>
internal sealed class CompletedProjectWindow : FloatWindow
{
    public static void Show(PetWindow pet, FameEntry entry, Window? near = null)
    {
        var w = new CompletedProjectWindow(pet, entry);
        w.Place(near ?? pet.MenuWin);
        w.Show();
        w.Activate();
    }

    private CompletedProjectWindow(PetWindow pet, FameEntry f) : base(pet, "completed", 420 * U)
    {
        Title = f.Kind;
        var def = PetWindow.ProjectDefs.FirstOrDefault(d => d.Id == f.Type);
        var head = new StackPanel { Orientation = Orientation.Horizontal };
        var ic = Icon(def?.Icon ?? "star", 2);
        ic.VerticalAlignment = VerticalAlignment.Center;
        ic.Margin = new Thickness(0, 0, 8 * U, 0);
        head.Children.Add(ic);
        head.Children.Add(new TextBlock { Text = f.Kind, FontWeight = FontWeights.Bold, FontSize = Ui.Title, VerticalAlignment = VerticalAlignment.Center });
        Head.Children.Add(head);

        var p = new StackPanel();
        if (f.Name.Length > 0)
            p.Children.Add(new TextBlock { Text = "„" + f.Name + "“", FontWeight = FontWeights.Bold, FontSize = Ui.Title, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 8 * U) });
        if (def?.FinalItem is { Length: > 0 } item)
        {
            var img = Ui.Pixel(pet.PropImage(item), 5);
            img.HorizontalAlignment = HorizontalAlignment.Center;
            img.Margin = new Thickness(0, 0, 0, 10 * U);
            p.Children.Add(img);
        }
        var facts = f.Facts.Count > 0 ? f.Facts : new[] { ("Тема", f.Theme), ("Играчите", f.Goal), ("Главен герой", f.Hero) }
            .Where(x => x.Item2.Length > 0).Select(x => x.Item1 + ": " + x.Item2).ToList();
        foreach (var fact in facts)
        {
            int c = fact.IndexOf(':');
            var t = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2 * U, 0, 0) };
            t.Inlines.Add(new System.Windows.Documents.Run((c > 0 ? fact[..(c + 1)] : "") + " ") { Foreground = Muted });
            t.Inlines.Add(new System.Windows.Documents.Run(c > 0 ? fact[(c + 1)..].Trim() : fact) { FontWeight = FontWeights.Bold });
            p.Children.Add(t);
        }
        p.Children.Add(new TextBlock
        {
            Text = $"Направихте го заедно – Мечо и {pet.Save.OwnerName}, {f.Done}.",
            FontStyle = FontStyles.Italic, Foreground = Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10 * U, 0, 0),
        });
        Body.Content = p;
        Deactivated += (_, _) => Dispatcher.BeginInvoke(() =>
        {
            if (!IsLoaded || IsActive || SelfTest.Running) return;
            Close();
        }, DispatcherPriority.Input);
    }
}
