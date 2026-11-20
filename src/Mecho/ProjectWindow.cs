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
    private static double Gap => 118 * U;      // между два етапа
    private static double Pad => 70 * U;       // в началото и в края
    private static double CardW => 92 * U;
    private static double CardH => 96 * U;
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

    private ProjectWindow(PetWindow pet) : base(pet, "project", 640 * U)
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

    /// <summary>Строи всичко наново.</summary>
    public void Rebuild()
    {
        var pet = Pet;
        var s = pet.Save;
        var now = pet.ProjectNow;
        bool done = pet.ProjectDone;
        _key = $"{s.ProjectPaid}|{(int)s.ProjectPoints}|{pet.GameName}";
        _content.Children.Clear();

        // ── Горе: рангът (значка с номер) и името на играта ──
        _headTitle.Children.Clear();
        var rankRow = new DockPanel();
        var badge = RankBadge(PetWindow.ProjectRankIndex(s.ProjectPaid), 40 * U);
        badge.Margin = new Thickness(0, 0, 10 * U, 0);
        DockPanel.SetDock(badge, Dock.Left);
        rankRow.Children.Add(badge);
        var names = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        names.Children.Add(new TextBlock { Text = "Ранг: " + PetWindow.ProjectRank(s.ProjectPaid), FontWeight = FontWeights.Bold, FontSize = Ui.Title });
        names.Children.Add(new TextBlock
        {
            Text = pet.GameName.Length > 0 ? "Настолната игра „" + pet.GameName + "“" : "Настолната игра на Мечо",
            Foreground = Muted,
        });
        rankRow.Children.Add(names);
        _headTitle.Children.Add(rankRow);

        // ── Празникът: мина етап ──
        if (_celebrate >= 0) _content.Children.Add(Banner());

        // ── Сегашният етап: голямата медена лента ──
        _content.Children.Add(done ? DoneBox() : CurrentBox(now.Stage, now.Info, now.Have, now.Need));

        // ── Пътят ──
        _content.Children.Add(Road(s.ProjectPaid, done ? 1 : now.Need > 0 ? now.Have / now.Need : 0));
        _content.Children.Add(new TextBlock
        {
            Text = "Работата ви движи напред: 1 точка на минута мечо-доро или таймер · задача +10 / +20 / +40",
            Foreground = Muted, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 6 * U, 0, 0),
        });
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

    private Border CurrentBox(int stage, PetWindow.ProjectStage info, double have, int need)
    {
        var cur = new StackPanel();
        var top = new DockPanel();
        var pts = new TextBlock { Text = $"{(int)have} / {need}", FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(8 * U, 0, 0, 0) };
        DockPanel.SetDock(pts, Dock.Right);
        top.Children.Add(pts);
        top.Children.Add(new TextBlock { Text = $"Етап {stage + 1}: {info.Name}", FontWeight = FontWeights.Bold, FontSize = Ui.Title, TextWrapping = TextWrapping.Wrap });
        cur.Children.Add(top);
        cur.Children.Add(new TextBlock { Text = "Мечо сега " + info.Doing + ".", Foreground = Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2 * U, 0, 6 * U) });
        cur.Children.Add(Bar(have / Math.Max(1, need), 10 * P));
        return new Border
        {
            Background = Soft(Honey, 0.3),
            BorderBrush = Honey,
            BorderThickness = new Thickness(Math.Max(2, P)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10 * U, 8 * U, 10 * U, 9 * U),
            Margin = new Thickness(0, 0, 0, 8 * U),
            Child = cur,
        };
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
            fill.BeginAnimation(WidthProperty, new DoubleAnimation(_celebrate >= 0 ? 0 : to * 0.3, to, TimeSpan.FromMilliseconds(_celebrate >= 0 ? 1100 : 800))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
        };
        return track;
    }

    /// <summary>Хоризонталният път: пътят, кръгчетата, картите с награди, значките на ранговете и Мечо.</summary>
    private FrameworkElement Road(int paid, double frac)
    {
        int n = PetWindow.ProjectStages.Length;
        double width = Pad * 2 + Gap * (n - 1);
        var canvas = new Canvas { Width = width, Height = RoadH };
        double X(int i) => Pad + Gap * i;
        double roadH = 6 * P;
        double edge = Math.Max(1, P / 2);

        // Пътят: тъмен ръб, светла пътека; изминатото – мед.
        var road = new Border
        {
            Width = width - Pad * 2 + 30 * U, Height = roadH,
            Background = Soft(WoodLight, 0.6), BorderBrush = Ink, BorderThickness = new Thickness(edge),
            CornerRadius = new CornerRadius(roadH / 2),
        };
        Canvas.SetLeft(road, Pad - 15 * U);
        Canvas.SetTop(road, TrackY - roadH / 2);
        canvas.Children.Add(road);
        // Докъде сте: от предишното кръгче към сегашното – колкото е лентата на етапа.
        double reached = paid >= n ? X(n - 1) + 15 * U : paid == 0 ? X(0) - 15 * U + (X(0) - X(0)) : X(paid - 1) + (X(paid) - X(paid - 1)) * frac;
        if (paid == 0) reached = X(0) - 15 * U + 15 * U * frac;
        double w0 = Math.Max(0, reached - (Pad - 15 * U) - edge);
        if (w0 > 0)
        {
            var done = new Border { Height = roadH - 2 * edge, Background = Honey, CornerRadius = new CornerRadius(roadH / 2), Width = w0 };
            Canvas.SetLeft(done, Pad - 15 * U + edge);
            Canvas.SetTop(done, TrackY - roadH / 2 + edge);
            canvas.Children.Add(done);
            canvas.Loaded += (_, _) => done.BeginAnimation(WidthProperty, new DoubleAnimation(_celebrate >= 0 ? Math.Max(0, w0 - Gap) : w0 * 0.7, w0, TimeSpan.FromMilliseconds(900))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
        }

        for (int i = 0; i < n; i++)
        {
            bool isDone = i < paid, isCur = i == paid;
            double x = X(i);

            // Значка на всеки три етапа – нов ранг (на пътя, между кръгчетата).
            if (i % 3 == 2 && i < n - 1)
            {
                var flag = RankBadge(PetWindow.ProjectRankIndex(i + 1), 24 * U);
                flag.ToolTip = Tip("Ранг " + (PetWindow.ProjectRankIndex(i + 1) + 1) + ": " + PetWindow.ProjectRank(i + 1));
                flag.Opacity = isDone ? 1 : 0.5;
                Canvas.SetLeft(flag, x + Gap / 2 - 12 * U);
                Canvas.SetTop(flag, TrackY - 12 * U);
                canvas.Children.Add(flag);
            }

            // Кръгчето на пътя.
            double d = isCur ? 28 * U : 22 * U;
            var node = new Border
            {
                Width = d, Height = d, CornerRadius = new CornerRadius(d / 2),
                Background = isDone ? Good : isCur ? Honey : Cream,
                BorderBrush = Ink, BorderThickness = new Thickness(edge),
                Child = new TextBlock
                {
                    Text = (i + 1).ToString(), FontWeight = FontWeights.Bold, Foreground = isDone || isCur ? Ink : Muted,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                },
            };
            Canvas.SetLeft(node, x - d / 2);
            Canvas.SetTop(node, TrackY - d / 2);
            canvas.Children.Add(node);

            // Името на етапа – под пътя.
            var name = new TextBlock
            {
                Text = PetWindow.ProjectStages[i].Name, Width = Gap - 10 * U, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap,
                FontWeight = isCur ? FontWeights.Bold : FontWeights.Normal, Foreground = isDone || isCur ? Ink : Muted,
            };
            Canvas.SetLeft(name, x - (Gap - 10 * U) / 2);
            Canvas.SetTop(name, TrackY + 18 * U);
            canvas.Children.Add(name);

            // Картата с наградата – над пътя, с краче до него.
            var stem = new Rectangle { Width = Math.Max(2, P), Height = 22 * U - d / 2 + 2, Fill = isDone ? Good : isCur ? Honey : WoodLight };
            Canvas.SetLeft(stem, x - stem.Width / 2);
            Canvas.SetTop(stem, TrackY - 22 * U);
            canvas.Children.Add(stem);
            var card = RewardCard(i, isDone, isCur);
            Canvas.SetLeft(card, x - CardW / 2);
            Canvas.SetTop(card, TrackY - 22 * U - CardH);
            canvas.Children.Add(card);

            if (i == _celebrate)
            {
                Pop(card, 0.6);
                Pop(node, 0.5);
            }
        }

        // Мечо – под пътя, докъдето сте стигнали („Тук сме!“).
        var bear = Icon("bear", 2);
        bear.ToolTip = Tip("Тук сме!");
        canvas.Children.Add(bear);
        bear.Loaded += (_, _) =>
        {
            Canvas.SetLeft(bear, reached - bear.ActualWidth / 2);
            Canvas.SetTop(bear, RoadH - bear.ActualHeight - 4 * U);
        };

        var scroll = new ScrollViewer
        {
            Content = canvas,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Height = RoadH + 20 * U,
        };
        // Колелцето на мишката мести пътя наляво/надясно.
        scroll.PreviewMouseWheel += (_, e) =>
        {
            scroll.ScrollToHorizontalOffset(scroll.HorizontalOffset - e.Delta * 0.6);
            e.Handled = true;
        };
        // Сегашният етап – в средата.
        scroll.Loaded += (_, _) => scroll.ScrollToHorizontalOffset(Math.Max(0, X(Math.Min(paid, n - 1)) - scroll.ViewportWidth / 2));
        return new Border
        {
            Background = Soft(WoodLight, 0.15),
            BorderBrush = WoodLight,
            BorderThickness = new Thickness(edge),
            CornerRadius = new CornerRadius(6),
            Child = scroll,
        };
    }

    /// <summary>
    /// Картата с наградата: взетата – зелена с отметка; сегашната – светеща медена;
    /// следващите две – видими; по-нататък – заключена.
    /// </summary>
    private FrameworkElement RewardCard(int i, bool isDone, bool isCur)
    {
        var s = Pet.Save;
        bool shown = Pet.ProjectRewardShown(i);
        string plan = shown ? Pet.ProjectRewardPlan(i) : "";
        string text = isDone && i < s.ProjectGot.Count && s.ProjectGot[i].Length > 0 ? s.ProjectGot[i]
            : shown ? Pet.ProjectRewardLabel(plan) : "Заключено";
        var body = new Grid();
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        var ic = Icon(shown ? PetWindow.ProjectRewardIcon(plan) : "lock", 3);
        ic.HorizontalAlignment = HorizontalAlignment.Center;
        ic.Opacity = shown ? 1 : 0.55;
        stack.Children.Add(ic);
        stack.Children.Add(new TextBlock
        {
            Text = text, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxHeight = 3 * Ui.Body * 1.35,
            TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(2 * U, 4 * U, 2 * U, 0),
            Foreground = shown ? Ink : Muted, FontWeight = isCur ? FontWeights.Bold : FontWeights.Normal,
        });
        body.Children.Add(stack);
        if (isDone)
        {
            body.Children.Add(new Border
            {
                Width = 20 * U, Height = 20 * U, CornerRadius = new CornerRadius(10 * U), Background = Good, BorderBrush = Ink,
                BorderThickness = new Thickness(Math.Max(1, P / 2)), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -10 * U, -10 * U, 0), Child = new Viewbox { Child = Icon("check"), Margin = new Thickness(3 * U) },
            });
        }
        var card = new Border
        {
            Width = CardW, Height = CardH,
            Background = isDone ? Soft(Good, 0.25) : isCur ? Soft(Honey, 0.45) : shown ? Cream : Soft(WoodDark, 0.22),
            BorderBrush = isDone ? Good : isCur || shown ? Honey : WoodDark,
            BorderThickness = new Thickness(isCur ? Math.Max(3, P * 1.5) : Math.Max(2, P)),
            CornerRadius = new CornerRadius(8 * U),
            Padding = new Thickness(4 * U),
            Child = body,
            ToolTip = Tip(isDone ? "Взето: " + text : isCur ? "Следващата награда: " + text : shown ? "Наградата за етап " + (i + 1) + ": " + text : "Заключено – ще се види, като наближите"),
        };
        // Сегашната свети (мек ореол, без безкрайна анимация).
        if (isCur && Honey is SolidColorBrush hb)
            card.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = hb.Color, BlurRadius = 18, ShadowDepth = 0, Opacity = 0.9 };
        return card;
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
