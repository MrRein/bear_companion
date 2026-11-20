using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using static Mecho.Ui;

namespace Mecho;

/// <summary>
/// Пътят на проекта (като „trophy road“): горе – рангът на Мечо и голямата медена лента на
/// етапа; отдолу – хоризонтален път с всички 15 етапа. Над всяко кръгче е картата с
/// наградата: взетите – със зелена отметка, сегашната – светеща, следващите две – видими,
/// по-нататъшните – заключени. Мечо стои на пътя точно докъдето сте стигнали; на всеки
/// три етапа има значка – нов ранг. Мине ли етап – прозорецът се отваря сам и празнува.
/// </summary>
internal sealed class ProjectWindow : FloatWindow
{
    private static ProjectWindow? _open;
    private readonly StackPanel _content = new();
    private readonly StackPanel _headTitle;
    private int _celebrate = -1;
    private string _celebrateText = "";
    private string _key = "";

    public static ProjectWindow? Current => _open;

    // Размерите на пътя.
    private static double Gap => 140 * U;      // между два етапа
    private static double Pad => 76 * U;       // в началото и в края
    private static double CardW => 112 * U;
    private static double CardH => 104 * U;

    /// <summary>Самопроверката снима веднага: без анимации (всичко е на мястото си).</summary>
    private static bool Instant => SelfTest.Running;
    private static double TrackY => 142 * U;   // средата на пътя
    private static double RoadH => 236 * U;

    /// <summary>Отваря (или затваря, ако е отворен) прозореца с прогреса.</summary>
    public static void Toggle(PetWindow pet, Window? near = null)
    {
        if (_open != null)
        {
            _open.Close();
            return;
        }
        Open(pet, near);
    }

    public static ProjectWindow Open(PetWindow pet, Window? near = null, int celebrate = -1, string reward = "")
    {
        if (_open != null)
        {
            _open._celebrate = celebrate;
            _open._celebrateText = reward;
            _open.Rebuild();
            return _open;
        }
        var w = new ProjectWindow(pet) { _celebrate = celebrate, _celebrateText = reward };
        if (celebrate >= 0) w.ShowActivated = false;   // отваря се сам – не краде фокуса от работата
        w.Rebuild();
        w.Place(near ?? pet.MenuWin);
        w.Show();
        return w;
    }

    private ProjectWindow(PetWindow pet) : base(pet, "project", 560 * U)
    {
        _open = this;
        Title = "Пътят на проекта";
        Body.Content = _content;
        _headTitle = new StackPanel();
        Head.Children.Add(_headTitle);
        Closed += (_, _) => { if (_open == this) _open = null; pet.CareChanged -= OnCare; };
        pet.CareChanged += OnCare;
    }

    /// <summary>Точките/етапът се смениха: наново (празникът не се повтаря).</summary>
    private void OnCare(object? sender, EventArgs e)
    {
        string key = $"{Pet.Save.ProjectPaid}|{(int)Pet.Save.ProjectPoints}|{Pet.GameName}";
        if (key == _key) return;
        _celebrate = -1;
        Rebuild();
    }

    private static Brush Soft(Brush tint, double mix) => MenuWindow.Soft(tint, mix);

    /// <summary>Строи всичко наново: рангът, следващата награда (голяма) и пътят.</summary>
    public void Rebuild()
    {
        var pet = Pet;
        var s = pet.Save;
        var now = pet.ProjectNow;
        bool done = pet.ProjectDone;
        _key = $"{s.ProjectPaid}|{(int)s.ProjectPoints}|{pet.GameName}";
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

        if (_celebrate >= 0) _content.Children.Add(Banner());
        _content.Children.Add(done ? DoneBox() : NextReward(now.Stage, now.Info, now.Have, now.Need));
        _content.Children.Add(Path(s.ProjectPaid));
    }

    /// <summary>
    /// Следващата награда – голяма, най-отгоре (като следващия сандък): картинката, името,
    /// на кой етап е и лентата докъде сте до нея.
    /// </summary>
    private Border NextReward(int stage, PetWindow.ProjectStage info, double have, int need)
    {
        string plan = Pet.ProjectRewardPlan(stage);
        bool nuts = plan.StartsWith("nuts:");
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96 * U) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        // Картинката на наградата в светещо „гнездо“.
        FrameworkElement art = Pet.ShopArt(plan) is { } img ? Pixel(img, 4) : Icon(nuts ? "nut3" : PetWindow.ProjectRewardIcon(plan), 4);
        art.HorizontalAlignment = HorizontalAlignment.Center;
        art.VerticalAlignment = VerticalAlignment.Center;
        var slot = new Border
        {
            Width = 84 * U, Height = 84 * U, CornerRadius = new CornerRadius(10 * U),
            Background = Cream, BorderBrush = Honey, BorderThickness = new Thickness(Math.Max(3, P * 1.5)),
            Child = art, VerticalAlignment = VerticalAlignment.Center,
        };
        if (Honey is SolidColorBrush hb)
            slot.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = hb.Color, BlurRadius = 16, ShadowDepth = 0, Opacity = 0.8 };
        grid.Children.Add(slot);
        var right = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12 * U, 0, 0, 0) };
        right.Children.Add(new TextBlock { Text = "СЛЕДВАЩА НАГРАДА", Foreground = Muted, FontWeight = FontWeights.Bold });
        right.Children.Add(new TextBlock
        {
            Text = nuts ? "+" + plan[5..] + " лешника" : Pet.ProjectRewardLabel(plan),
            FontWeight = FontWeights.Bold, FontSize = Ui.Title, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2 * U, 0, 6 * U),
        });
        var bar = Bar(have / Math.Max(1, need), 10 * P);
        right.Children.Add(bar);
        var under = new DockPanel { Margin = new Thickness(0, 4 * U, 0, 0) };
        var pts = new TextBlock { Text = $"{(int)have} / {need}", FontWeight = FontWeights.Bold };
        DockPanel.SetDock(pts, Dock.Right);
        under.Children.Add(pts);
        under.Children.Add(new TextBlock { Text = $"Етап {stage + 1}: {info.Name}", Foreground = Muted, TextTrimming = TextTrimming.CharacterEllipsis });
        right.Children.Add(under);
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);
        return new Border
        {
            Background = Soft(Honey, 0.25),
            BorderBrush = Honey,
            BorderThickness = new Thickness(Math.Max(2, P)),
            CornerRadius = new CornerRadius(8 * U),
            Padding = new Thickness(10 * U),
            Margin = new Thickness(0, 0, 0, 10 * U),
            Child = grid,
        };
    }

    /// <summary>
    /// Пътят: 15 кръгчета на една линия (без превъртане). Минатите – медени с отметка;
    /// Мечо стои на сегашния; над следващите два – малка иконка на наградата; по-нататък
    /// – само кръгче. Звезда – нов ранг; накрая – трофей.
    /// </summary>
    private FrameworkElement Path(int paid)
    {
        int n = PetWindow.ProjectStages.Length;
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 2 * U) };
        for (int i = 0; i < n; i++) grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34 * U) });   // иконки / Мечо
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30 * U) });   // кръгчетата
        double edge = Math.Max(1, P / 2);

        // Линията под кръгчетата: изминатото – мед.
        var line = new Grid { Height = 4 * P, VerticalAlignment = VerticalAlignment.Center };
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0, Math.Min(paid, n - 1)), GridUnitType.Star) });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0, n - 1 - Math.Min(paid, n - 1)), GridUnitType.Star) });
        var doneLine = new Border { Background = Honey };
        var restLine = new Border { Background = Soft(WoodLight, 0.6) };
        Grid.SetColumn(restLine, 1);
        line.Children.Add(doneLine);
        line.Children.Add(restLine);
        var lineHost = new Border { Child = line };
        Grid.SetRow(lineHost, 1);
        Grid.SetColumnSpan(lineHost, n);
        lineHost.SizeChanged += (_, _) => lineHost.Padding = new Thickness(lineHost.ActualWidth / n / 2, 0, lineHost.ActualWidth / n / 2, 0);
        grid.Children.Add(lineHost);

        for (int i = 0; i < n; i++)
        {
            bool isDone = i < paid, isCur = i == paid;
            bool rankUp = i % 3 == 2, last = i == n - 1;
            string tip = $"{i + 1}. {PetWindow.ProjectStages[i].Name}" + (Pet.ProjectRewardShown(i) ? " – " + Pet.ProjectRewardLabel(Pet.ProjectRewardPlan(i)) : "");

            // Кръгчето (звезда при нов ранг, трофей накрая).
            double d = isCur ? 26 * U : rankUp || last ? 24 * U : 18 * U;
            var node = new Border
            {
                Width = d, Height = d, CornerRadius = new CornerRadius(d / 2),
                Background = isDone || isCur ? Honey : Cream,
                BorderBrush = Ink, BorderThickness = new Thickness(edge),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                ToolTip = Tip(tip + (rankUp && !last ? " · нов ранг: " + PetWindow.ProjectRank(i + 1) : "")),
            };
            if (isDone && !rankUp && !last) node.Child = new Viewbox { Child = Icon("check"), Margin = new Thickness(3 * U) };
            else if (rankUp || last) node.Child = new Viewbox { Child = Icon("star"), Margin = new Thickness(3 * U), Opacity = isDone ? 1 : 0.45 };
            Grid.SetRow(node, 1);
            Grid.SetColumn(node, i);
            grid.Children.Add(node);

            // Над него: Мечо (сега), иконката на наградата (следващите два), нищо – по-нататък.
            FrameworkElement? top = null;
            if (isCur) top = Icon("bear", 2);
            else if (!isDone && Pet.ProjectRewardShown(i))
            {
                string plan = Pet.ProjectRewardPlan(i);
                top = Pet.ShopArt(plan) is { } img ? Pixel(img, 2) : Icon(plan.StartsWith("nuts:") ? "nut2" : PetWindow.ProjectRewardIcon(plan));
            }
            if (top != null)
            {
                top.HorizontalAlignment = HorizontalAlignment.Center;
                top.VerticalAlignment = VerticalAlignment.Bottom;
                top.ToolTip = Tip(isCur ? "Тук сме: " + PetWindow.ProjectStages[i].Name : tip);
                Grid.SetColumn(top, i);
                grid.Children.Add(top);
            }
            if (i == _celebrate) Pop(node, 0.4);
        }

        var box = new StackPanel();
        box.Children.Add(new TextBlock { Text = "Пътят", FontWeight = FontWeights.Bold, Foreground = Muted, Margin = new Thickness(0, 0, 0, 2 * U) });
        box.Children.Add(grid);
        // Кратко: какво прави Мечо сега и как се върви напред.
        box.Children.Add(new TextBlock
        {
            Text = Pet.ProjectDone ? "" : "Мечо сега " + Pet.ProjectNow.Info.Doing + ". Вашата работа го движи: минута фокус = 1 точка, задача = 10/20/40.",
            Foreground = Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6 * U, 0, 0),
        });
        return box;
    }

    private Border Banner()
    {
        var b = new StackPanel();
        var row = new DockPanel();
        var star = Icon("star", 2);
        star.Margin = new Thickness(0, 0, 8 * U, 0);
        star.VerticalAlignment = VerticalAlignment.Top;
        DockPanel.SetDock(star, Dock.Left);
        row.Children.Add(star);
        row.Children.Add(new TextBlock
        {
            Text = $"Етап {_celebrate + 1} е готов: {PetWindow.ProjectStages[_celebrate].Name}!",
            FontWeight = FontWeights.Bold, FontSize = Ui.Title, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap,
        });
        b.Children.Add(row);
        if (_celebrateText.Length > 0)
            b.Children.Add(new TextBlock { Text = "Награда: " + _celebrateText.Replace("🌰", "лешника"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4 * U, 0, 0) });
        // Нов ранг: само ако с този етап се смени.
        if (PetWindow.ProjectRankIndex(_celebrate + 1) != PetWindow.ProjectRankIndex(_celebrate))
            b.Children.Add(new TextBlock { Text = "Нов ранг: " + PetWindow.ProjectRank(_celebrate + 1) + "!", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 4 * U, 0, 0) });
        var banner = new Border
        {
            Background = Soft(Good, 0.3),
            BorderBrush = Good,
            BorderThickness = new Thickness(Math.Max(2, P)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10 * U, 7 * U, 10 * U, 8 * U),
            Margin = new Thickness(0, 0, 0, 8 * U),
            Child = b,
        };
        Pop(banner, 0.85);
        return banner;
    }

    private Border DoneBox() => new()
    {
        Background = Soft(Good, 0.3),
        BorderBrush = Good,
        BorderThickness = new Thickness(Math.Max(2, P)),
        CornerRadius = new CornerRadius(6),
        Padding = new Thickness(10 * U, 8 * U, 10 * U, 9 * U),
        Margin = new Thickness(0, 0, 0, 8 * U),
        Child = new TextBlock { Text = "Играта излезе! Мечо вече е Издател – благодарение на твоята работа.", FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap },
    };

    /// <summary>Медена лента с отблясък, която се пълни пред очите ти (веднъж).</summary>
    private Grid Bar(double frac, double height)
    {
        frac = Math.Clamp(frac, 0, 1);
        double edge = Math.Max(1, P / 2);
        var track = new Grid { Height = height };
        track.Children.Add(new Border { Background = Empty, BorderBrush = Ink, BorderThickness = new Thickness(edge), CornerRadius = new CornerRadius(height / 2) });
        var fill = new Border { Background = Honey, CornerRadius = new CornerRadius(height / 2), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(edge), Width = 0 };
        fill.Child = new Border { Background = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), Height = Math.Max(2, height / 4), VerticalAlignment = VerticalAlignment.Top, CornerRadius = new CornerRadius(P), Margin = new Thickness(height / 3, P, height / 3, 0) };
        track.Children.Add(fill);
        track.SizeChanged += (_, e) =>
        {
            if (!e.WidthChanged) return;
            double to = Math.Max(0, (track.ActualWidth - 2 * edge) * frac);
            fill.Width = to;   // и без анимация (снимка, слаб компютър) е на мястото си
            if (Instant) return;
            fill.BeginAnimation(WidthProperty, new DoubleAnimation(_celebrate >= 0 ? 0 : to * 0.3, to, TimeSpan.FromMilliseconds(_celebrate >= 0 ? 1100 : 800))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
        };
        return track;
    }

    /// <summary>Значката на ранга: медно кръгче със звезда и номера на ранга.</summary>
    private static FrameworkElement RankBadge(int rank, double size)
    {
        var g = new Grid { Width = size, Height = size };
        g.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(size / 2), Background = Honey, BorderBrush = Ink, BorderThickness = new Thickness(Math.Max(1, P / 2)),
        });
        g.Children.Add(new Viewbox { Child = Icon("star"), Margin = new Thickness(size * 0.15), Opacity = 0.35 });
        g.Children.Add(new TextBlock
        {
            Text = (rank + 1).ToString(), FontWeight = FontWeights.Bold, FontSize = size > 30 * U ? Ui.Title : Ui.Body,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        });
        return g;
    }

    /// <summary>Подскача веднъж (празник): малко → голямо → нормално.</summary>
    private static void Pop(FrameworkElement e, double from)
    {
        if (Instant) return;
        var t = new ScaleTransform(from, from);
        e.RenderTransformOrigin = new Point(0.5, 0.5);
        e.RenderTransform = t;
        var a = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(700), FillBehavior = FillBehavior.Stop };
        a.KeyFrames.Add(new EasingDoubleKeyFrame(from, KeyTime.FromPercent(0)));
        a.KeyFrames.Add(new EasingDoubleKeyFrame(1.2, KeyTime.FromPercent(0.55)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        a.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromPercent(1)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } });
        t.ScaleX = t.ScaleY = 1;   // след анимацията (FillBehavior.Stop) – нормален размер
        t.BeginAnimation(ScaleTransform.ScaleXProperty, a);
        t.BeginAnimation(ScaleTransform.ScaleYProperty, a);
    }
}
