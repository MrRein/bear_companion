using System;
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
/// Пътешествието на проекта: малка рисувана карта. Лъкатушеща пътечка от точки минава
/// през поляна с цветя; на всеки етап – камъче, а над него наградата (самият предмет,
/// купчинка лешници или подаръче). Изминатото е медено, а истинското мече върви по
/// пътечката докъдето сте стигнали. Минат етап – подаръчето блести: цъкаш и го отваряш.
/// Далечните награди са „?“ кутийки. Без рамки, таблички и числа – само картата.
/// </summary>
internal sealed class ProjectWindow : FloatWindow
{
    private static ProjectWindow? _open;
    private readonly StackPanel _content = new();
    private readonly StackPanel _headTitle = new();
    private readonly DispatcherTimer _walk = new() { Interval = TimeSpan.FromMilliseconds(110) };
    private int _pop = -1;          // кое да подскочи (току-що минато или взето)
    private string _key = "";

    public static ProjectWindow? Current => _open;

    private static double Step => 118 * U;      // между два етапа
    private static double SceneH => 214 * U;
    private static double GroundY => 132 * U;    // средата на пътечката
    private static double Wave => 16 * U;        // колко лъкатуши
    private static bool Instant => SelfTest.Running;

    public static void Toggle(PetWindow pet, Window? near = null)
    {
        if (_open != null)
        {
            _open.Close();
            return;
        }
        Open(pet, near);
    }

    /// <summary>Отваря картата; celebrate – етапът, който току-що мина (подаръчето му подскача).</summary>
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

    private ProjectWindow(PetWindow pet) : base(pet, "project", 640 * U)
    {
        _open = this;
        Title = "Пътешествието на проекта";
        Body.Content = _content;
        Head.Children.Add(_headTitle);
        Closed += (_, _) =>
        {
            if (_open == this) _open = null;
            pet.CareChanged -= OnCare;
            _walk.Stop();
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
        var s = pet.Save;
        _key = Key;
        _walk.Stop();
        _content.Children.Clear();

        // Горе: името на играта – голямо; рангът – медно хапче до него.
        _headTitle.Children.Clear();
        _headTitle.Children.Add(new TextBlock
        {
            Text = pet.GameName.Length > 0 ? "„" + pet.GameName + "“" : "Настолната игра на Мечо",
            FontWeight = FontWeights.Bold, FontSize = Ui.Title,
        });
        var rank = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3 * U, 0, 0) };
        var pill = Pill(new TextBlock { Text = PetWindow.ProjectRank(s.ProjectPaid), FontWeight = FontWeights.Bold });
        pill.ToolTip = Tip("Рангът на Мечо – нов на всеки три етапа");
        rank.Children.Add(pill);
        rank.Children.Add(new TextBlock
        {
            Text = pet.ProjectDone ? "Играта излезе!" : $"етап {pet.ProjectNow.Stage + 1} от {PetWindow.ProjectStages.Length}",
            Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8 * U, 0, 0, 0),
        });
        _headTitle.Children.Add(rank);

        _content.Children.Add(Scene());
    }

    /// <summary>Точката на пътечката за етап i (-1 – стартът): лъкатуши нагоре-надолу.</summary>
    private static Point At(int i) => new(60 * U + Step * (i + 1), GroundY + Wave * Math.Sin((i + 1) * 1.15));

    /// <summary>Точката по пътечката между два етапа (t – 0…1).</summary>
    private static Point Between(int from, double t)
    {
        var a = At(from);
        var b = At(from + 1);
        return new Point(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
    }

    private FrameworkElement Scene()
    {
        var pet = Pet;
        int paid = pet.Save.ProjectPaid, n = PetWindow.ProjectStages.Length;
        double frac = pet.ProjectProgress;
        double width = At(n - 1).X + 70 * U;
        var canvas = new Canvas { Width = width, Height = SceneH, ClipToBounds = true };

        // Небе и поляна.
        canvas.Children.Add(new Rectangle
        {
            Width = width, Height = SceneH,
            Fill = new LinearGradientBrush(Color.FromRgb(232, 242, 238), Color.FromRgb(246, 238, 214), 90),
        });
        var grass = new Rectangle { Width = width, Height = SceneH - (GroundY - 34 * U), Fill = new SolidColorBrush(Color.FromRgb(196, 222, 160)) };
        Canvas.SetTop(grass, GroundY - 34 * U);
        canvas.Children.Add(grass);
        var grassEdge = new Rectangle { Width = width, Height = 2 * P, Fill = new SolidColorBrush(Color.FromRgb(160, 196, 120)) };
        Canvas.SetTop(grassEdge, GroundY - 34 * U);
        canvas.Children.Add(grassEdge);

        // Цветя по поляната (едни и същи всеки път).
        var rng = new Random(7);
        string[] flowers = { "flower_4_pink", "flower_4_yellow", "flower_4_white", "flower_4_purple", "flower_4_red", "flower_4_blue" };
        for (double fx = 20 * U; fx < width - 20 * U; fx += 38 * U + rng.Next(40) * U)
        {
            var f = Pixel(pet.PropImage(flowers[rng.Next(flowers.Length)]), 2);
            f.Opacity = 0.85;
            double fy = rng.Next(2) == 0 ? SceneH - 34 * U - rng.Next(8) * U : GroundY - 30 * U + rng.Next(6) * U;
            Canvas.SetLeft(f, fx);
            Canvas.SetTop(f, fy);
            canvas.Children.Add(f);
        }

        // Докъде сме: етап (откъдето) и колко от пътя до следващия.
        int fromStage = pet.ProjectDone ? n - 2 : paid - 1;
        double t = pet.ProjectDone ? 1 : frac;

        // Пътечката: точки; изминатото – мед.
        for (int i = -1; i < n - 1; i++)
        {
            var a = At(i);
            var b = At(i + 1);
            double len = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
            int dots = (int)(len / (10 * U));
            for (int k = 1; k < dots; k++)
            {
                double tt = (double)k / dots;
                bool walked = i < fromStage || i == fromStage && tt <= t;
                double sz = walked ? 4 * P / 2 + 2 : 3 * P / 2 + 1;
                var dot = new Rectangle { Width = sz, Height = sz, Fill = walked ? Honey : new SolidColorBrush(Color.FromRgb(150, 120, 90)), Opacity = walked ? 1 : 0.45 };
                Canvas.SetLeft(dot, a.X + (b.X - a.X) * tt - sz / 2);
                Canvas.SetTop(dot, a.Y + (b.Y - a.Y) * tt - sz / 2);
                canvas.Children.Add(dot);
            }
        }

        // Стартът: малко флагче.
        var start = Icon("flag");
        Canvas.SetLeft(start, At(-1).X - 8 * U);
        Canvas.SetTop(start, At(-1).Y - 22 * U);
        canvas.Children.Add(start);

        for (int i = 0; i < n; i++)
            DrawStage(canvas, i, paid);

        // Мечето – върви по пътечката докъдето сме.
        if (!pet.ProjectDone) DrawBear(canvas, fromStage, t);

        // Мести се с колелцето или с влачене (без плъзгач).
        var scroll = new ScrollViewer
        {
            Content = canvas,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Height = SceneH,
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
        scroll.MouseLeftButtonDown += (_, e) => e.Handled = true;
        scroll.PreviewMouseLeftButtonUp += (_, e) =>
        {
            if (scroll.IsMouseCaptured) { scroll.ReleaseMouseCapture(); e.Handled = true; }
            grab = null;
        };
        int focus = pet.ProjectHasClaim ? pet.ProjectClaimed : Math.Min(paid, n - 1);
        scroll.Loaded += (_, _) => scroll.ScrollToHorizontalOffset(Math.Max(0, At(focus).X - scroll.ViewportWidth * 0.4));

        return new Border
        {
            CornerRadius = new CornerRadius(6),
            BorderBrush = WoodLight,
            BorderThickness = Flat,
            Child = new Border { CornerRadius = new CornerRadius(6), ClipToBounds = true, Child = scroll },
        };
    }

    /// <summary>Един етап: камъче на пътечката, наградата над него, името под него.</summary>
    private void DrawStage(Canvas canvas, int i, int paid)
    {
        var pet = Pet;
        var p = At(i);
        bool done = i < paid, cur = i == paid && !pet.ProjectDone;
        bool claim = pet.CanClaimProject(i), taken = done && !claim && i < pet.ProjectClaimed;
        bool rank = i % 3 == 2;
        string name = PetWindow.ProjectStages[i].Name;
        int stage = i;

        // Камъчето.
        double sw = (cur || claim ? 30 : 24) * U, sh = sw * 0.45;
        var stone = new Ellipse
        {
            Width = sw, Height = sh,
            Fill = done ? Honey : cur ? Cream : new SolidColorBrush(Color.FromRgb(214, 200, 176)),
            Stroke = Ink, StrokeThickness = Math.Max(1, P / 2),
            ToolTip = Tip($"{i + 1}. {name}" + (rank ? " · нов ранг: " + PetWindow.ProjectRank(i + 1) : "")),
        };
        Canvas.SetLeft(stone, p.X - sw / 2);
        Canvas.SetTop(stone, p.Y - sh / 2);
        canvas.Children.Add(stone);

        // Наградата – стои на камъчето.
        bool shown = pet.ProjectRewardShown(i);
        string plan = shown ? pet.ProjectRewardPlan(i) : "";
        BitmapSource? art = null;
        FrameworkElement thing;
        if (claim) art = pet.PropImage("gift_green");
        else if (taken) art = pet.PropImage("gift_open");
        else if (!shown) art = pet.PropImage("mystery");
        else art = pet.ShopArt(plan);
        if (art != null) thing = Pixel(art, cur || claim ? 3 : 2);
        else
        {
            thing = Icon(plan.StartsWith("nuts:") ? "nut3" : PetWindow.ProjectRewardIcon(plan), cur ? 3 : 2);
        }
        thing.Opacity = taken ? 0.45 : !shown && !claim ? 0.7 : 1;
        thing.ToolTip = Tip(claim ? "Цъкни, за да отвориш: " + pet.ProjectRewardLabel(plan)
            : taken ? pet.ProjectRewardLabel(plan) + " – взето"
            : !shown ? "Изненада – ще се види, като наближите"
            : (cur ? "Следващата награда: " : "") + pet.ProjectRewardLabel(plan));
        var holder = new Grid { Width = Step, Height = 70 * U };
        thing.HorizontalAlignment = HorizontalAlignment.Center;
        thing.VerticalAlignment = VerticalAlignment.Bottom;
        holder.Children.Add(thing);
        // Блести, ако чака да го вземеш; звезда – нов ранг.
        if (claim)
        {
            var spark = Icon("emote_sparkle", 2);
            spark.HorizontalAlignment = HorizontalAlignment.Center;
            spark.VerticalAlignment = VerticalAlignment.Top;
            spark.Margin = new Thickness(26 * U, 0, 0, 0);
            holder.Children.Add(spark);
            holder.Cursor = System.Windows.Input.Cursors.Hand;
            holder.Background = Brushes.Transparent;
            TaskCards.Clickable(holder, () => Claim(stage));
        }
        Canvas.SetLeft(holder, p.X - Step / 2);
        Canvas.SetTop(holder, p.Y - sh / 2 - 70 * U + 2 * U);
        canvas.Children.Add(holder);

        // Името – под камъчето, дребно; сегашното – по-тъмно.
        var label = new TextBlock
        {
            Text = name, Width = Step - 6 * U, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap,
            MaxHeight = 2.7 * Ui.Body, TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = cur ? Ink : Muted, FontWeight = cur ? FontWeights.Bold : FontWeights.Normal,
        };
        Canvas.SetLeft(label, p.X - (Step - 6 * U) / 2);
        Canvas.SetTop(label, p.Y + sh / 2 + 6 * U);
        canvas.Children.Add(label);
        if (rank)
        {
            var star = new Viewbox { Width = 14 * U, Height = 14 * U, Child = Icon("star"), Opacity = done ? 1 : 0.55 };
            Canvas.SetLeft(star, p.X + sw / 2 - 2 * U);
            Canvas.SetTop(star, p.Y - sh / 2 - 8 * U);
            canvas.Children.Add(star);
        }

        if (i == _pop) Pop(holder, 0.4);
    }

    /// <summary>Мечето на пътечката: като се отвори картата – изминава последното парче и спира.</summary>
    private void DrawBear(Canvas canvas, int fromStage, double t)
    {
        var pet = Pet;
        var walk = pet.BearFrames("walk");
        var idle = pet.BearFrames("idle");
        if (idle.Length == 0) return;
        var img = Pixel(idle[0], 2);
        img.ToolTip = Tip("Мечо е тук – работата ви го бута напред");
        canvas.Children.Add(img);
        void PlaceAt(double tt)
        {
            var p = fromStage < -1 ? At(-1) : Between(fromStage, Math.Clamp(tt, 0, 1));
            Canvas.SetLeft(img, p.X - img.Width / 2);
            Canvas.SetTop(img, p.Y - img.Height + 4 * U);
        }
        if (Instant || walk.Length == 0)
        {
            PlaceAt(t);
            return;
        }
        // Изминава последното парче (до една трета от пътя между етапите) с ходене.
        double from = Math.Max(0, t - 0.35);
        int frame = 0;
        var started = DateTime.Now;
        PlaceAt(from);
        img.Source = walk[0];
        EventHandler? tick = null;
        tick = (_, _) =>
        {
            double k = Math.Min(1, (DateTime.Now - started).TotalSeconds / 1.2);
            PlaceAt(from + (t - from) * (1 - Math.Pow(1 - k, 2)));
            frame++;
            img.Source = k < 1 ? walk[frame % walk.Length] : idle[0];
            if (k >= 1)
            {
                _walk.Stop();
                _walk.Tick -= tick;
            }
        };
        _walk.Tick += tick;
        _walk.Start();
    }

    /// <summary>Цъкна подаръчето: взима наградата, то подскача.</summary>
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
        e.RenderTransformOrigin = new Point(0.5, 1);
        e.RenderTransform = tr;
        var a = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(700), FillBehavior = FillBehavior.Stop };
        a.KeyFrames.Add(new EasingDoubleKeyFrame(from, KeyTime.FromPercent(0)));
        a.KeyFrames.Add(new EasingDoubleKeyFrame(1.25, KeyTime.FromPercent(0.55)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        a.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromPercent(1)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } });
        tr.BeginAnimation(ScaleTransform.ScaleXProperty, a);
        tr.BeginAnimation(ScaleTransform.ScaleYProperty, a);
    }
}
