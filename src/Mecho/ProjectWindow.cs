using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using static Mecho.Ui;

namespace Mecho;

/// <summary>
/// Целият проект: голяма лента от 15 звена (етапите), всяко с името си отдолу и наградата си
/// отгоре – в отделно квадратче. Изминатото е зелено, а Мечо стои на лентата точно докъдето
/// сте стигнали; при отваряне лентата се допълва пред очите ти. Наградите: взетите – цветни
/// с отметка; минат етап с невзета награда – свети (цъкаш и я взимаш); следващите – сиви
/// (виждаш какво идва); далечните – „?“. Цъкнеш ли извън прозореца – затваря се.
/// </summary>
internal sealed class ProjectWindow : FloatWindow
{
    private static ProjectWindow? _open;
    private readonly StackPanel _content = new();
    private readonly StackPanel _headTitle = new();
    private int _pop = -1;          // кое да подскочи (току-що минато или взето)
    private string _key = "";

    public static ProjectWindow? Current => _open;

    private static bool Instant => SelfTest.Running;

    private static double SegW => 132 * U;      // едно звено
    private static double Gap => 8 * U;         // между звената
    private static double BoxSize => 76 * U;    // квадратчето с наградата
    private static double BarH => 30 * U;

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

    private ProjectWindow(PetWindow pet) : base(pet, "project", 720 * U)
    {
        _open = this;
        Title = "Проектът на Мечо";
        Body.Content = _content;
        Head.Children.Add(_headTitle);
        // Като другите прозорчета: цъкнеш ли извън него – затваря се.
        Deactivated += (_, _) => Dispatcher.BeginInvoke(() =>
        {
            if (!IsLoaded || IsActive || SelfTest.Running) return;
            Close();
        }, System.Windows.Threading.DispatcherPriority.Input);
        // Отворен сам (празник, без фокус): мине ли мишката отгоре – става активен, за да се затваря като другите.
        MouseEnter += (_, _) => { if (!IsActive) Activate(); };
        Closed += (_, _) =>
        {
            if (_open == this) _open = null;
            pet.CareChanged -= OnCare;
        };
        pet.CareChanged += OnCare;
    }

    private string Key => $"{Pet.Save.ProjectPaid}|{(int)(Pet.ProjectProgress * 40)}|{Pet.GameName}|{Pet.ProjectClaimed}";

    private void OnCare(object? sender, EventArgs e)
    {
        if (Key == _key) return;
        _pop = -1;
        Rebuild();
    }

    private static Brush Soft(Brush tint, double mix) => MenuWindow.Soft(tint, mix);

    public void Rebuild()
    {
        var pet = Pet;
        _key = Key;
        _content.Children.Clear();

        // Горе: „Настолна игра“ с иконка (и името ѝ), под него – на кой етап сте.
        _headTitle.Children.Clear();
        var title = new StackPanel { Orientation = Orientation.Horizontal };
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
        // „Етап Механиките 2/15“ и под него – с малки наклонени букви какво прави мечокът.
        var now = pet.ProjectNow;
        var stageLine = new TextBlock { Margin = new Thickness(0, 4 * U, 0, 0) };
        if (pet.ProjectDone) stageLine.Inlines.Add(new System.Windows.Documents.Run("Играта излезе!") { FontWeight = FontWeights.Bold });
        else
        {
            stageLine.Inlines.Add(new System.Windows.Documents.Run("Етап ") { Foreground = Muted });
            stageLine.Inlines.Add(new System.Windows.Documents.Run(now.Info.Name) { FontWeight = FontWeights.Bold });
            stageLine.Inlines.Add(new System.Windows.Documents.Run($"  {now.Stage + 1}/{PetWindow.ProjectStages.Length}") { Foreground = Muted });
        }
        _headTitle.Children.Add(stageLine);
        if (!pet.ProjectDone && now.Info.Desc.Length > 0)
            _headTitle.Children.Add(new TextBlock { Text = now.Info.Desc, FontStyle = FontStyles.Italic, Foreground = Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 1 * U, 0, 0) });

        _content.Children.Add(Track());
        // Пунктир, а под него – трите подетапа на сегашния етап.
        if (!pet.ProjectDone)
        {
            _content.Children.Add(new Border
            {
                Height = P, Background = UiSkin.Line("dash") ?? WoodLight, Margin = new Thickness(6 * U, 8 * U, 6 * U, 10 * U),
            });
            _content.Children.Add(Steps());
        }
    }

    /// <summary>Сива версия на картинка (наградата, която още не е твоя) – със същата прозрачност.</summary>
    private static BitmapSource Gray(BitmapSource src)
    {
        var b = src.Format == PixelFormats.Bgra32 ? src : new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
        int w = b.PixelWidth, h = b.PixelHeight, stride = w * 4;
        var px = new byte[stride * h];
        b.CopyPixels(px, stride, 0);
        for (int i = 0; i < px.Length; i += 4)
        {
            byte g = (byte)Math.Min(255, (px[i] * 0.11 + px[i + 1] * 0.59 + px[i + 2] * 0.3) * 0.7 + 70);
            px[i] = px[i + 1] = px[i + 2] = g;
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

    /// <summary>Картинката на наградата (предметът от магазина, лешниците, дрехата…).</summary>
    private BitmapSource? RewardArt(string plan) =>
        Pet.ShopArt(plan) ?? LoadIcon(plan.StartsWith("nuts:") ? "nut3" : PetWindow.ProjectRewardIcon(plan));

    /// <summary>
    /// Лентата: една дълга линия през всички етапи; на нея за всеки етап – квадратче (звено),
    /// от което излиза линийка нагоре до наградата му. Изминатото по линията е зелено, а
    /// Мечо стои точно докъдето сте. Под квадратчетата – имената.
    /// </summary>
    private FrameworkElement Track()
    {
        var pet = Pet;
        int paid = pet.Save.ProjectPaid, n = PetWindow.ProjectStages.Length;
        int cur = pet.ProjectDone ? -1 : paid;
        double frac = pet.ProjectProgress;
        double colW = SegW + Gap, edge = Math.Max(1, P / 2);
        double boxTop = 10 * U, lineY = boxTop + BoxSize + 34 * U, nameY = lineY + 22 * U;
        double width = colW * n + 24 * U, height = nameY + 2.8 * Ui.Body + 6 * U;
        double Xc(int i) => 12 * U + i * colW + colW / 2;            // средата на етап i
        double startX = Xc(0) - colW / 2 + 6 * U, endX = Xc(n - 1) + colW / 2 - 6 * U;
        double lineH = 8 * P;
        var canvas = new Canvas { Width = width, Height = height };

        // Докъде сме: от квадратчето на предишния етап към това на сегашния.
        double reached = pet.ProjectDone ? endX : (cur == 0 ? startX : Xc(cur - 1)) + ((cur == 0 ? Xc(0) - startX : colW)) * frac;

        // ── Линията: празна, а изминатото – зелено (с отблясък) ──
        var line = new Border
        {
            Width = endX - startX, Height = lineH, CornerRadius = new CornerRadius(lineH / 2),
            Background = Empty, BorderBrush = Ink, BorderThickness = new Thickness(edge),
        };
        Canvas.SetLeft(line, startX);
        Canvas.SetTop(line, lineY - lineH / 2);
        canvas.Children.Add(line);
        double greenW = Math.Max(0, reached - startX - edge);
        if (greenW > 0)
        {
            var green = new Border
            {
                Width = greenW, Height = lineH - 2 * edge, CornerRadius = new CornerRadius(lineH / 2), Background = Good,
                Child = new Border { Background = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), Height = Math.Max(2, lineH / 4), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(lineH / 2, P / 2, lineH / 2, 0), CornerRadius = new CornerRadius(P) },
            };
            Canvas.SetLeft(green, startX + edge);
            Canvas.SetTop(green, lineY - lineH / 2 + edge);
            canvas.Children.Add(green);
            if (!Instant)
                green.Loaded += (_, _) => green.BeginAnimation(WidthProperty, new DoubleAnimation(Math.Max(0, greenW - colW * 0.6), greenW, TimeSpan.FromMilliseconds(1000))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
        }

        for (int i = 0; i < n; i++)
        {
            int stage = i;
            bool done = i < paid, isCur = i == cur;
            bool claim = pet.CanClaimProject(i), taken = done && !claim && i < pet.ProjectClaimed;
            bool shown = pet.ProjectRewardShown(i);
            string plan = shown || done ? pet.ProjectRewardPlan(i) : "";
            string name = PetWindow.ProjectStages[i].Name;
            double cx = Xc(i);

            // ── Линийката от звеното до наградата ──
            var stem = new Border { Width = Math.Max(2, P), Height = lineY - (boxTop + BoxSize), Background = done ? Good : isCur ? Honey : WoodLight };
            Canvas.SetLeft(stem, cx - stem.Width / 2);
            Canvas.SetTop(stem, boxTop + BoxSize);
            canvas.Children.Add(stem);

            // ── Звеното: квадратче на линията ──
            double sq = isCur || claim ? 26 * U : 22 * U;
            var link = new Border
            {
                Width = sq, Height = sq, CornerRadius = new CornerRadius(4 * U),
                Background = claim ? Honey : done ? Good : Cream,
                BorderBrush = Ink, BorderThickness = new Thickness(isCur || claim ? Math.Max(2, P) : edge),
                ToolTip = Tip(name),
                Child = done && !claim ? new Viewbox { Child = Icon("check"), Margin = new Thickness(4 * U) } : null,
            };
            Canvas.SetLeft(link, cx - sq / 2);
            Canvas.SetTop(link, lineY - sq / 2);
            Panel.SetZIndex(link, 3);
            canvas.Children.Add(link);

            // ── Наградата в своето квадратче ──
            var art = (shown || done) && RewardArt(plan) is { } src ? Art(src, BoxSize * 0.62, gray: !done) : null;
            var inside = new Grid();
            if (art != null)
            {
                art.HorizontalAlignment = HorizontalAlignment.Center;
                art.VerticalAlignment = VerticalAlignment.Center;
                inside.Children.Add(art);
            }
            else
            {
                var q = Icon("question", 3);
                q.HorizontalAlignment = HorizontalAlignment.Center;
                q.VerticalAlignment = VerticalAlignment.Center;
                q.Opacity = 0.5;
                inside.Children.Add(q);
            }
            if (taken)
                inside.Children.Add(new Border
                {
                    Width = 22 * U, Height = 22 * U, CornerRadius = new CornerRadius(11 * U), Background = Good, BorderBrush = Ink, BorderThickness = new Thickness(edge),
                    HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -9 * U, -9 * U, 0),
                    Child = new Viewbox { Child = Icon("check"), Margin = new Thickness(4 * U) },
                });
            if (claim)
            {
                var spark = Icon("emote_sparkle", 2);
                spark.HorizontalAlignment = HorizontalAlignment.Right;
                spark.VerticalAlignment = VerticalAlignment.Top;
                spark.Margin = new Thickness(0, -14 * U, -12 * U, 0);
                inside.Children.Add(spark);
            }
            var box = new Border
            {
                Width = BoxSize, Height = BoxSize, CornerRadius = new CornerRadius(8 * U),
                Background = claim ? Soft(Honey, 0.55) : taken ? Soft(Good, 0.25) : art != null ? Cream : Soft(WoodLight, 0.25),
                BorderBrush = claim ? Ink : taken ? Good : art != null ? (isCur ? Honey : Ink) : WoodLight,
                BorderThickness = new Thickness(claim || isCur ? Math.Max(3, P * 1.5) : Math.Max(2, P)),
                Child = inside,
                ToolTip = Tip(claim ? "Цъкни, за да вземеш: " + pet.ProjectRewardLabel(plan)
                    : taken ? pet.ProjectRewardLabel(plan) + " – твое"
                    : art == null ? "Изненада – ще се види, като наближите"
                    : (isCur ? "Наградата за този етап: " : "Награда: ") + pet.ProjectRewardLabel(plan)),
            };
            if (claim && Honey is SolidColorBrush hb)
                box.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = hb.Color, BlurRadius = 18, ShadowDepth = 0, Opacity = 0.9 };
            if (claim)
            {
                box.Cursor = link.Cursor = System.Windows.Input.Cursors.Hand;
                TaskCards.Clickable(box, () => Claim(stage));
                TaskCards.Clickable(link, () => Claim(stage));
            }
            Canvas.SetLeft(box, cx - BoxSize / 2);
            Canvas.SetTop(box, boxTop);
            canvas.Children.Add(box);

            // ── Името под звеното ──
            var label = new TextBlock
            {
                Text = name, Width = SegW, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap,
                MaxHeight = 2.8 * Ui.Body, TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = done || isCur ? Ink : Muted, FontWeight = isCur ? FontWeights.Bold : FontWeights.Normal,
            };
            Canvas.SetLeft(label, cx - SegW / 2);
            Canvas.SetTop(label, nameY);
            canvas.Children.Add(label);

            if (i == _pop) { Pop(box, 0.5); Pop(link, 0.5); }
        }

        // ── Мечо – на линията, точно докъдето сте ──
        if (!pet.ProjectDone)
        {
            var bear = Icon("small_bear", 2);
            bear.ToolTip = Tip("Мечо е тук");
            bear.HorizontalAlignment = HorizontalAlignment.Center;
            bear.VerticalAlignment = VerticalAlignment.Bottom;
            var cell = new Grid { Width = 40 * U, Height = 30 * U };
            cell.Children.Add(bear);
            Canvas.SetLeft(cell, reached - 20 * U);
            Canvas.SetTop(cell, lineY - lineH / 2 - 30 * U + 3 * U);
            Panel.SetZIndex(cell, 4);
            canvas.Children.Add(cell);
            if (!Instant && frac > 0)
                cell.Loaded += (_, _) => cell.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(reached - 20 * U - colW * 0.6 * frac, reached - 20 * U, TimeSpan.FromMilliseconds(1000))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
        }

        int focus = pet.ProjectHasClaim ? pet.ProjectClaimed : Math.Max(0, Math.Min(paid, n - 1));
        return Scroller(canvas, Xc(focus));
    }

    /// <summary>Трите подетапа на сегашния етап – отмятат се, докато работите (под пунктир).</summary>
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
            var mark = new Border
            {
                Width = 18 * U, Height = 18 * U, CornerRadius = new CornerRadius(3 * U),
                Background = ok ? Good : Cream, BorderBrush = Ink, BorderThickness = new Thickness(Math.Max(1, P / 2)),
                Child = ok ? new Viewbox { Child = Icon("check"), Margin = new Thickness(3 * U) } : null,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8 * U, 0),
            };
            row.Children.Add(mark);
            row.Children.Add(new TextBlock
            {
                Text = st.Steps[k], VerticalAlignment = VerticalAlignment.Center,
                Foreground = ok ? Muted : Ink, TextDecorations = ok ? TextDecorations.Strikethrough : null,
                FontWeight = !ok && k == doneSteps ? FontWeights.Bold : FontWeights.Normal,   // това, по което работи сега
            });
            box.Children.Add(row);
        }
        return box;
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

    /// <summary>Цъкна наградата: взима я, квадратчето подскача.</summary>
    private void Claim(int stage)
    {
        Pet.ClaimProjectReward(stage);
        _pop = stage;
        Rebuild();
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
