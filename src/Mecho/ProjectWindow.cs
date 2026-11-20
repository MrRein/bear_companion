using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using static Mecho.Ui;

namespace Mecho;

/// <summary>
/// Прогресът на проекта на Мечо (настолната игра): сегашният етап с голяма медена лента,
/// а под него – пътеката на всички 15 етапа с наградите. Наградите на минатите, сегашния и
/// следващите два се виждат; по-нататъшните са „?“. Мине ли етап – прозорецът се отваря
/// сам и празнува: кръгчето подскача, наградата светва, лентата се пълни отначало.
/// </summary>
internal sealed class ProjectWindow : FloatWindow
{
    private static ProjectWindow? _open;
    private readonly StackPanel _content = new();
    private int _celebrate = -1;
    private string _celebrateText = "";

    public static ProjectWindow? Current => _open;

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

    private ProjectWindow(PetWindow pet) : base(pet, "project", 470 * U)
    {
        _open = this;
        Title = "Проектът на Мечо";
        Body.Content = _content;
        var head = new StackPanel();
        head.Children.Add(new TextBlock { Text = "Проектът на Мечо", FontWeight = FontWeights.Bold, FontSize = Ui.Title });
        Head.Children.Add(head);
        _headTitle = head;
        Closed += (_, _) => { if (_open == this) _open = null; pet.CareChanged -= OnCare; };
        pet.CareChanged += OnCare;
    }

    private readonly StackPanel _headTitle;
    private string _key = "";

    /// <summary>Точките/етапът се смениха: наново (празникът не се повтаря).</summary>
    private void OnCare(object? sender, EventArgs e)
    {
        string key = $"{Pet.Save.ProjectPaid}|{(int)Pet.Save.ProjectPoints}|{Pet.GameName}";
        if (key == _key) return;
        _celebrate = -1;
        Rebuild();
    }

    /// <summary>Строи всичко наново (точките или етапът се смениха).</summary>
    public void Rebuild()
    {
        var pet = Pet;
        var s = pet.Save;
        _key = $"{s.ProjectPaid}|{(int)s.ProjectPoints}|{pet.GameName}";
        var now = pet.ProjectNow;
        bool done = pet.ProjectDone;
        _content.Children.Clear();

        // Името на играта (или „Настолната игра“) – под заглавието.
        if (_headTitle.Children.Count > 1) _headTitle.Children.RemoveAt(1);
        _headTitle.Children.Add(new TextBlock
        {
            Text = pet.GameName.Length > 0 ? "Настолната игра „" + pet.GameName + "“" : "Настолна игра (още без име)",
            Foreground = Muted,
        });

        // ── Празникът: мина етап ──
        if (_celebrate >= 0)
        {
            var banner = new Border
            {
                Background = Soft(Good, 0.3),
                BorderBrush = Good,
                BorderThickness = new Thickness(Math.Max(2, P)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10 * U, 7 * U, 10 * U, 8 * U),
                Margin = new Thickness(0, 0, 0, 10 * U),
            };
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
            banner.Child = b;
            _content.Children.Add(banner);
            Pop(banner, 0.85);
        }

        // ── Сегашният етап: голямата медена лента ──
        if (!done)
        {
            var cur = new StackPanel();
            cur.Children.Add(new TextBlock
            {
                Text = $"Етап {now.Stage + 1} от {PetWindow.ProjectStages.Length}: {now.Info.Name}",
                FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap,
            });
            cur.Children.Add(new TextBlock { Text = "Мечо сега " + now.Info.Doing + ".", Foreground = Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2 * U, 0, 6 * U) });
            var track = new Grid { Height = 9 * P, ClipToBounds = true };
            var bg = new Border { Background = Empty, BorderBrush = Ink, BorderThickness = new Thickness(Math.Max(1, P / 2)), CornerRadius = new CornerRadius(3 * P) };
            var fill = new Border { Background = Honey, CornerRadius = new CornerRadius(3 * P), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(Math.Max(1, P / 2)) };
            var shine = new Border { Background = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)), Height = 2 * P, VerticalAlignment = VerticalAlignment.Top, CornerRadius = new CornerRadius(P), Margin = new Thickness(2 * P, P, 2 * P, 0) };
            fill.Child = shine;
            track.Children.Add(bg);
            track.Children.Add(fill);
            var label = new TextBlock { Text = $"{(int)now.Have} / {now.Need}", FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            track.Children.Add(label);
            double frac = now.Need > 0 ? Math.Clamp(now.Have / now.Need, 0, 1) : 0;
            // Пълни се пред очите ти (веднъж, като се отвори).
            track.SizeChanged += (_, e) =>
            {
                if (!e.WidthChanged) return;
                double to = Math.Max(0, (track.ActualWidth - 2 * Math.Max(1, P / 2)) * frac);
                fill.BeginAnimation(WidthProperty, new DoubleAnimation(_celebrate >= 0 ? 0 : to * 0.4, to, TimeSpan.FromMilliseconds(_celebrate >= 0 ? 1100 : 700))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            };
            cur.Children.Add(track);
            cur.Children.Add(new TextBlock
            {
                Text = "Твоята работа го движи: 1 точка на минута мечо-доро или таймер; задача +10 / +20 / +40.",
                Foreground = Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6 * U, 0, 0),
            });
            _content.Children.Add(new Border
            {
                Background = Soft(Honey, 0.3),
                BorderBrush = Honey,
                BorderThickness = new Thickness(Math.Max(2, P)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10 * U, 8 * U, 10 * U, 9 * U),
                Margin = new Thickness(0, 0, 0, 10 * U),
                Child = cur,
            });
        }
        else
        {
            _content.Children.Add(new TextBlock
            {
                Text = "Играта излезе! Мечо вече е Издател. Благодарение на твоята работа.",
                FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10 * U),
            });
        }

        // ── Пътеката: 15 етапа, кръгчета, свързани с линия; наградите вдясно ──
        var path = new StackPanel();
        FrameworkElement? curRow = null;
        for (int i = 0; i < PetWindow.ProjectStages.Length; i++)
        {
            var row = StageRow(i, s.ProjectPaid, i == PetWindow.ProjectStages.Length - 1);
            if (i == Math.Min(s.ProjectPaid, PetWindow.ProjectStages.Length - 1)) curRow = row;
            path.Children.Add(row);
        }
        var scroll = new ScrollViewer
        {
            Content = path,
            MaxHeight = 330 * U,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        // Сегашният етап – в средата на пътеката.
        scroll.Loaded += (_, _) =>
        {
            if (curRow == null) return;
            var at = curRow.TransformToAncestor(path).Transform(new Point(0, 0));
            scroll.ScrollToVerticalOffset(Math.Max(0, at.Y - scroll.ViewportHeight / 2 + curRow.ActualHeight / 2));
        };
        _content.Children.Add(scroll);
    }

    /// <summary>Един етап от пътеката: кръгче (✓ / номер), името, наградата.</summary>
    private FrameworkElement StageRow(int i, int paid, bool last)
    {
        bool isDone = i < paid, isCur = i == paid;
        var st = PetWindow.ProjectStages[i];
        var grid = new Grid { MinHeight = 30 * U };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34 * U) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Линията между кръгчетата (минатото – зелено).
        var line = new Rectangle
        {
            Width = 2 * P,
            Fill = isDone ? Good : WoodLight,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = last ? VerticalAlignment.Top : VerticalAlignment.Stretch,
            Height = last ? 15 * U : double.NaN,
        };
        if (i == 0) { line.VerticalAlignment = VerticalAlignment.Bottom; line.Height = 15 * U; }
        grid.Children.Add(line);

        double d = isCur ? 22 * U : 18 * U;
        var node = new Border
        {
            Width = d, Height = d, CornerRadius = new CornerRadius(d / 2),
            Background = isDone ? Good : isCur ? Honey : Cream,
            BorderBrush = Ink, BorderThickness = new Thickness(Math.Max(1, P / 2)),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            RenderTransformOrigin = new Point(0.5, 0.5),
        };
        if (isDone) node.Child = new Viewbox { Child = Icon("check"), Margin = new Thickness(3 * U) };
        else node.Child = new TextBlock
        {
            Text = (i + 1).ToString(), FontWeight = FontWeights.Bold, Foreground = isCur ? Ink : Muted,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        };
        grid.Children.Add(node);

        var name = new TextBlock
        {
            Text = st.Name,
            FontWeight = isCur ? FontWeights.Bold : FontWeights.Normal,
            Foreground = isDone || isCur ? Ink : Muted,
            VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(6 * U, 0, 8 * U, 0),
        };
        Grid.SetColumn(name, 1);
        grid.Children.Add(name);

        var reward = RewardChip(i, isDone);
        Grid.SetColumn(reward, 2);
        grid.Children.Add(reward);

        if (i == _celebrate)
        {
            Pop(node, 0.5);
            Pop(reward, 0.6);
        }
        return grid;
    }

    /// <summary>Наградата: минатите – какво получи; следващите три – какво ще е; по-нататък – „?“.</summary>
    private FrameworkElement RewardChip(int i, bool isDone)
    {
        var s = Pet.Save;
        bool shown = Pet.ProjectRewardShown(i);
        string plan = shown ? Pet.ProjectRewardPlan(i) : "";
        string text = isDone && i < s.ProjectGot.Count && s.ProjectGot[i].Length > 0 ? s.ProjectGot[i]
            : shown ? Pet.ProjectRewardLabel(plan) : "?";
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var ic = Icon(shown ? PetWindow.ProjectRewardIcon(plan) : "question");
        ic.Margin = new Thickness(0, 0, 5 * U, 0);
        ic.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(ic);
        row.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, Foreground = isDone ? Muted : Ink, MaxWidth = 170 * U, TextTrimming = TextTrimming.CharacterEllipsis });
        return new Border
        {
            Background = isDone ? Brushes.Transparent : shown ? Cream : Soft(WoodLight, 0.2),
            BorderBrush = isDone ? Brushes.Transparent : shown ? Honey : WoodLight,
            BorderThickness = new Thickness(Math.Max(1, P / 2)),
            CornerRadius = new CornerRadius(8 * U),
            Padding = new Thickness(6 * U, 2 * U, 8 * U, 3 * U),
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = isDone ? 0.75 : 1,
            Child = row,
            ToolTip = Tip(isDone ? "Получено" : shown ? "Наградата за този етап" : "Изненада – ще се види, като наближиш"),
        };
    }

    private static Brush Soft(Brush tint, double mix) => MenuWindow.Soft(tint, mix);

    /// <summary>Подскача веднъж (празник): голямо → нормално.</summary>
    private static void Pop(FrameworkElement e, double from)
    {
        var t = new ScaleTransform(from, from);
        e.RenderTransformOrigin = new Point(0.5, 0.5);
        e.RenderTransform = t;
        var a = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(650) };
        a.KeyFrames.Add(new EasingDoubleKeyFrame(from, KeyTime.FromPercent(0)));
        a.KeyFrames.Add(new EasingDoubleKeyFrame(1.18, KeyTime.FromPercent(0.55)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        a.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromPercent(1)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } });
        t.BeginAnimation(ScaleTransform.ScaleXProperty, a);
        t.BeginAnimation(ScaleTransform.ScaleYProperty, a);
    }
}
