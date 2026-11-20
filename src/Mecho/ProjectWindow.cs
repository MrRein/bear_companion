using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using static Mecho.Ui;

namespace Mecho;

/// <summary>
/// Пътят на проекта (като „trophy road“). Главното е пътят: дебела пътека с кръгчетата на
/// 15-те етапа, изминатото е мед, Мечо стои на сегашния. Над всяко кръгче – наградата му
/// (минатите и следващите две се виждат, по-нататък – катинар). Мине ли етап, наградата
/// светва зелено с „Вземи!“ – цъкаш и я получаваш. Под пътя – един ред: следващата награда
/// и колко остава до нея. На всеки три етапа – звезда (нов ранг).
/// </summary>
internal sealed class ProjectWindow : FloatWindow
{
    private static ProjectWindow? _open;
    private readonly StackPanel _content = new();
    private readonly StackPanel _headTitle;
    private int _pop = -1;          // кое кръгче да подскочи (току-що минато или взето)
    private string _key = "";

    public static ProjectWindow? Current => _open;

    private static double Gap => 140 * U;       // между два етапа
    private static double Pad => 50 * U;       // в началото и в края
    private static double RoadY => 130 * U;    // средата на пътя
    private static double RoadHeight => 214 * U;

    /// <summary>Самопроверката снима веднага: без анимации.</summary>
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

    /// <summary>Отваря пътя; celebrate – етапът, който току-що мина (кръгчето му подскача).</summary>
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

    private ProjectWindow(PetWindow pet) : base(pet, "project", 700 * U)
    {
        _open = this;
        Title = "Пътят на проекта";
        Body.Content = _content;
        _headTitle = new StackPanel();
        Head.Children.Add(_headTitle);
        Closed += (_, _) => { if (_open == this) _open = null; pet.CareChanged -= OnCare; };
        pet.CareChanged += OnCare;
    }

    private string Key => $"{Pet.Save.ProjectPaid}|{(int)(Pet.ProjectProgress * 50)}|{Pet.GameName}|{Pet.ProjectClaimed}";

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
        _content.Children.Clear();

        // ── Горе: рангът и името на играта ──
        _headTitle.Children.Clear();
        var rankRow = new DockPanel();
        var badge = RankBadge(PetWindow.ProjectRankIndex(s.ProjectPaid), 36 * U);
        badge.Margin = new Thickness(0, 0, 10 * U, 0);
        DockPanel.SetDock(badge, Dock.Left);
        rankRow.Children.Add(badge);
        var names = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        names.Children.Add(new TextBlock { Text = PetWindow.ProjectRank(s.ProjectPaid), FontWeight = FontWeights.Bold, FontSize = Ui.Title });
        names.Children.Add(new TextBlock
        {
            Text = pet.GameName.Length > 0 ? "Настолната игра „" + pet.GameName + "“" : "Настолната игра на Мечо",
            Foreground = Muted,
        });
        rankRow.Children.Add(names);
        _headTitle.Children.Add(rankRow);

        // Карта с тъмен контур, като в „Работа“ (без излишно заглавие – прозорецът е пътят).
        _content.Children.Add(Road());
        // Отделна лента няма: медта по пътя до Мечо е напредъкът към наградата пред него.
        if (pet.ProjectDone) _content.Children.Add(NextLine());
    }

    /// <summary>
    /// Пътят в три реда: горе – наградите; в средата – пътеката с кръгчетата, а Мечо стои
    /// точно в края на медта (той е „плъзгачът“ на напредъка); долу – имената (един ред).
    /// Стрелки ‹ › отстрани местят пътя; колелцето и влаченето – също.
    /// </summary>
    private FrameworkElement Road()
    {
        var pet = Pet;
        int paid = pet.Save.ProjectPaid, n = PetWindow.ProjectStages.Length;
        double frac = pet.ProjectProgress;
        double tile = 52 * U, tileTop = 8 * U;
        double roadY = tileTop + tile + 44 * U;   // средата на пътеката
        double nameY = roadY + 24 * U;
        double height = nameY + 2.7 * Ui.Body + 6 * U;
        double width = Pad * 2 + Gap * n;
        double X(int i) => Pad + Gap * (i + 1);    // в началото – стартът
        double edge = Math.Max(1, P / 2);
        double roadH = 10 * P;
        var canvas = new Canvas { Width = width, Height = height };

        // Пътеката и медта (до Мечо).
        var road = new Border
        {
            Width = X(n - 1) - Pad + roadH, Height = roadH, CornerRadius = new CornerRadius(roadH / 2),
            Background = Soft(WoodLight, 0.55), BorderBrush = Ink, BorderThickness = new Thickness(edge),
        };
        Canvas.SetLeft(road, Pad - roadH / 2);
        Canvas.SetTop(road, roadY - roadH / 2);
        canvas.Children.Add(road);
        double reached = pet.ProjectDone ? X(n - 1) : (paid == 0 ? Pad : X(paid - 1)) + Gap * frac;
        double w0 = Math.Max(0, reached - (Pad - roadH / 2) - edge);
        if (w0 > 0)
        {
            var honey = new Border { Width = w0, Height = roadH - 2 * edge, Background = Honey, CornerRadius = new CornerRadius(roadH / 2) };
            honey.Child = new Border { Background = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), Height = Math.Max(2, roadH / 4), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(roadH / 2, P, roadH / 2, 0), CornerRadius = new CornerRadius(P) };
            Canvas.SetLeft(honey, Pad - roadH / 2 + edge);
            Canvas.SetTop(honey, roadY - roadH / 2 + edge);
            canvas.Children.Add(honey);
            if (!Instant)
                honey.Loaded += (_, _) => honey.BeginAnimation(WidthProperty, new DoubleAnimation(Math.Max(0, w0 - Gap * 0.5), w0, TimeSpan.FromMilliseconds(900))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
        }

        for (int i = 0; i < n; i++)
        {
            int stage = i;
            bool done = i < paid, cur = i == paid && !pet.ProjectDone;
            bool claim = pet.CanClaimProject(i), waiting = done && i >= pet.ProjectClaimed;
            bool rank = i % 3 == 2;
            string name = PetWindow.ProjectStages[i].Name;
            double x = X(i);

            // Кръгчето: минато – мед с отметка; чака да вземеш – зелено с подарък; сега – с рамка; после – номер.
            double d = cur || claim ? 34 * U : 28 * U;
            var node = new Border
            {
                Width = d, Height = d, CornerRadius = new CornerRadius(d / 2),
                Background = claim || waiting ? Good : done ? Honey : Cream,
                BorderBrush = Ink, BorderThickness = new Thickness(cur || claim ? Math.Max(2, P) : edge),
                ToolTip = Tip($"{i + 1}. {name}" + (rank ? " · нов ранг: " + PetWindow.ProjectRank(i + 1) : "")),
                Child = claim || waiting ? new Viewbox { Child = Icon("gift"), Margin = new Thickness(5 * U) }
                    : done ? new Viewbox { Child = Icon("check"), Margin = new Thickness(6 * U) }
                    : new TextBlock { Text = (i + 1).ToString(), FontWeight = FontWeights.Bold, Foreground = cur ? Ink : Muted, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            };
            Canvas.SetLeft(node, x - d / 2);
            Canvas.SetTop(node, roadY - d / 2);
            canvas.Children.Add(node);
            if (claim)
            {
                node.Cursor = System.Windows.Input.Cursors.Hand;
                TaskCards.Clickable(node, () => Claim(stage));
            }

            // Наградата горе (минатите, сегашната и следващите две; по-нататък – катинар).
            bool shown = pet.ProjectRewardShown(i);
            string plan = shown ? pet.ProjectRewardPlan(i) : "";
            FrameworkElement ic = !shown ? Icon("lock") : pet.ShopArt(plan) is { } img ? Pixel(img, 2) : Icon(plan.StartsWith("nuts:") ? "nut2" : PetWindow.ProjectRewardIcon(plan), 2);
            ic.HorizontalAlignment = HorizontalAlignment.Center;
            ic.VerticalAlignment = VerticalAlignment.Center;
            var top = new Border
            {
                Width = tile, Height = tile, CornerRadius = new CornerRadius(4), Child = ic,
                Background = claim ? Good : cur ? Honey : !shown || done ? Brushes.Transparent : Cream,
                BorderBrush = claim || cur || shown && !done ? Ink : Brushes.Transparent,
                BorderThickness = new Thickness(claim || cur ? Math.Max(2, P) : edge),
                Opacity = !shown ? 0.35 : done && !claim ? 0.4 : 1,
                ToolTip = Tip(claim ? "Цъкни, за да вземеш: " + pet.ProjectRewardLabel(plan) : !shown ? "Ще се види, като наближите" : pet.ProjectRewardLabel(plan) + (done && !waiting ? " – взето" : "")),
            };
            if (claim)
            {
                top.Cursor = System.Windows.Input.Cursors.Hand;
                TaskCards.Clickable(top, () => Claim(stage));
            }
            Canvas.SetLeft(top, x - tile / 2);
            Canvas.SetTop(top, tileTop);
            canvas.Children.Add(top);
            // Звезда в ъгъла на наградата: с този етап идва нов ранг.
            if (rank)
            {
                var star = new Viewbox { Width = 16 * U, Height = 16 * U, Child = Icon("star"), Opacity = done ? 1 : 0.6, ToolTip = Tip("Нов ранг: " + PetWindow.ProjectRank(i + 1)) };
                Canvas.SetLeft(star, x + tile / 2 - 10 * U);
                Canvas.SetTop(star, tileTop - 6 * U);
                canvas.Children.Add(star);
            }

            // Името – дребно, до два реда (цялото – като посочиш).
            var label = new TextBlock
            {
                Text = name, Width = Gap - 8 * U, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap,
                MaxHeight = 2.7 * Ui.Body, TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = cur ? Ink : Muted, FontWeight = cur ? FontWeights.Bold : FontWeights.Normal, ToolTip = Tip(name),
            };
            Canvas.SetLeft(label, x - (Gap - 8 * U) / 2);
            Canvas.SetTop(label, nameY);
            canvas.Children.Add(label);

            if (i == _pop) { Pop(node, 0.4); Pop(top, 0.5); }
        }

        // Мечо – стъпил на пътеката, точно в края на медта.
        if (!pet.ProjectDone)
        {
            var bear = Icon("bear", 2);
            bear.ToolTip = Tip("Мечо е тук – работата ви го бута напред");
            var cell = new Grid { Width = Gap, Height = 34 * U };
            bear.HorizontalAlignment = HorizontalAlignment.Center;
            bear.VerticalAlignment = VerticalAlignment.Bottom;
            cell.Children.Add(bear);
            Canvas.SetLeft(cell, reached - Gap / 2);
            Canvas.SetTop(cell, roadY - roadH / 2 - 34 * U + 3 * U);
            canvas.Children.Add(cell);
        }

        var scroll = new ScrollViewer
        {
            Content = canvas,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Height = height,
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
        scroll.MouseLeftButtonDown += (_, e) => e.Handled = true;   // влаченето по пътя не мести прозореца
        scroll.PreviewMouseLeftButtonUp += (_, e) =>
        {
            if (scroll.IsMouseCaptured) { scroll.ReleaseMouseCapture(); e.Handled = true; }
            grab = null;
        };
        // Наградата за вземане или Мечо – малко вляво от средата (да се вижда какво предстои).
        int focus = pet.ProjectHasClaim ? pet.ProjectClaimed : Math.Min(paid, n - 1);
        scroll.Loaded += (_, _) => scroll.ScrollToHorizontalOffset(Math.Max(0, X(focus) - scroll.ViewportWidth * 0.35));

        // Стрелките ‹ › отстрани (по два етапа).
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Border Arrow(string icon, int dir)
        {
            var a = Chip("", () => scroll.ScrollToHorizontalOffset(scroll.HorizontalOffset + dir * Gap * 2));
            a.Child = Icon(icon);
            a.VerticalAlignment = VerticalAlignment.Center;
            a.Margin = new Thickness(dir < 0 ? 4 * U : 0, 0, dir > 0 ? 4 * U : 0, 0);
            return a;
        }
        var left = Arrow("nav_prev", -1);
        var right = Arrow("nav_next", 1);
        Grid.SetColumn(scroll, 1);
        Grid.SetColumn(right, 2);
        grid.Children.Add(left);
        grid.Children.Add(scroll);
        grid.Children.Add(right);
        return new Border
        {
            Background = PaperDark,
            BorderBrush = Ink,
            BorderThickness = Flat,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(0, 4 * U, 0, 4 * U),
            Child = grid,
        };
    }

    /// <summary>Един ред под пътя: следващата награда (малко) и колко остава до нея.</summary>
    private FrameworkElement NextLine()
    {
        var pet = Pet;
        if (pet.ProjectDone)
            return new TextBlock { Text = "Играта излезе! Мечо вече е Издател.", FontWeight = FontWeights.Bold, Margin = new Thickness(2 * U, 8 * U, 0, 0) };
        var now = pet.ProjectNow;
        string plan = pet.ProjectRewardPlan(now.Stage);
        // Карта като в „Работа“: наградата вляво, медените клетки докъде сме (без числа).
        var row = new DockPanel();
        FrameworkElement ic = pet.ShopArt(plan) is { } img ? Pixel(img, 2) : Icon(plan.StartsWith("nuts:") ? "nut2" : PetWindow.ProjectRewardIcon(plan), 2);
        ic.VerticalAlignment = VerticalAlignment.Center;
        ic.Margin = new Thickness(0, 0, 10 * U, 0);
        DockPanel.SetDock(ic, Dock.Left);
        row.Children.Add(ic);
        const int cells = 12;
        int filled = (int)Math.Floor(pet.ProjectProgress * cells);
        var track = new UniformGrid { Rows = 1, Columns = cells, VerticalAlignment = VerticalAlignment.Center, Height = 5 * P };
        for (int i = 0; i < cells; i++)
            track.Children.Add(new Border
            {
                Margin = new Thickness(0, 0, i < cells - 1 ? -P : 0, 0),
                BorderBrush = Ink, BorderThickness = Flat,
                Background = i < filled ? Honey : Empty,
            }.Skinned(i < filled ? "cell_honey" : "cell_empty"));
        row.Children.Add(track);
        return new Border
        {
            Background = PaperDark,
            BorderBrush = Ink,
            BorderThickness = Flat,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10 * U, 8 * U, 12 * U, 8 * U),
            Child = row,
            ToolTip = Tip("Следваща награда: " + (plan.StartsWith("nuts:") ? plan[5..] + " лешника" : pet.ProjectRewardLabel(plan))),
        };
    }

    /// <summary>Цъкна наградата: взима я, кръгчето подскача.</summary>
    private void Claim(int stage)
    {
        Pet.ClaimProjectReward(stage);
        _pop = stage;
        Rebuild();
    }

    /// <summary>Медена лента, която се пълни (веднъж, като се покаже).</summary>
    private static Grid Bar(double frac, double height)
    {
        frac = Math.Clamp(frac, 0, 1);
        double edge = Math.Max(1, P / 2);
        var track = new Grid { Height = height };
        track.Children.Add(new Border { Background = Empty, BorderBrush = Ink, BorderThickness = new Thickness(edge), CornerRadius = new CornerRadius(height / 2) });
        var fill = new Border { Background = Honey, CornerRadius = new CornerRadius(height / 2), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(edge), Width = 0 };
        track.Children.Add(fill);
        track.SizeChanged += (_, e) =>
        {
            if (!e.WidthChanged) return;
            double to = Math.Max(0, (track.ActualWidth - 2 * edge) * frac);
            fill.Width = to;
            if (Instant) return;
            fill.BeginAnimation(WidthProperty, new DoubleAnimation(to * 0.3, to, TimeSpan.FromMilliseconds(800))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
        };
        return track;
    }

    /// <summary>Значката на ранга: медно кръгче с номера.</summary>
    private static FrameworkElement RankBadge(int rank, double size)
    {
        var g = new Grid { Width = size, Height = size };
        g.Children.Add(new Border { CornerRadius = new CornerRadius(size / 2), Background = Honey, BorderBrush = Ink, BorderThickness = new Thickness(Math.Max(1, P / 2)) });
        g.Children.Add(new Viewbox { Child = Icon("star"), Margin = new Thickness(size * 0.15), Opacity = 0.35 });
        g.Children.Add(new TextBlock
        {
            Text = (rank + 1).ToString(), FontWeight = FontWeights.Bold, FontSize = Ui.Title,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        });
        return g;
    }

    /// <summary>Подскача веднъж: малко → голямо → нормално.</summary>
    private static void Pop(FrameworkElement e, double from)
    {
        if (Instant) return;
        var t = new ScaleTransform(1, 1);
        e.RenderTransformOrigin = new Point(0.5, 0.5);
        e.RenderTransform = t;
        var a = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(700), FillBehavior = FillBehavior.Stop };
        a.KeyFrames.Add(new EasingDoubleKeyFrame(from, KeyTime.FromPercent(0)));
        a.KeyFrames.Add(new EasingDoubleKeyFrame(1.25, KeyTime.FromPercent(0.55)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        a.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromPercent(1)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } });
        t.BeginAnimation(ScaleTransform.ScaleXProperty, a);
        t.BeginAnimation(ScaleTransform.ScaleYProperty, a);
    }
}
