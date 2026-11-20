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
/// Напредъкът на проекта – една лента, която се вижда цялата наведнъж: 15 части (етапите),
/// на групи по три (между групите – звездичка: нов ранг). Минатите са медени с отметка,
/// сегашната се пълни с мед, докато работите, следващите са празни. Над всяка част е
/// наградата ѝ (далечните – бледо „?“); минат етап с невзета награда е зелен с подаръче –
/// цъкаш и го взимаш. Под сегашната част – Мечо и името на етапа. Без числа и обяснения.
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

    public static void Toggle(PetWindow pet, Window? near = null)
    {
        if (_open != null)
        {
            _open.Close();
            return;
        }
        Open(pet, near);
    }

    /// <summary>Отваря напредъка; celebrate – етапът, който току-що мина (подскача).</summary>
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

    private ProjectWindow(PetWindow pet) : base(pet, "project", 660 * U)
    {
        _open = this;
        Title = "Проектът на Мечо";
        Body.Content = _content;
        Head.Children.Add(_headTitle);
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

    public void Rebuild()
    {
        var pet = Pet;
        var s = pet.Save;
        _key = Key;
        _content.Children.Clear();

        // Горе: името на играта и рангът (медно хапче) с етапа.
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

        _content.Children.Add(Track());
    }

    /// <summary>Лентата: награди (горе), 15-те части (в средата), Мечо и името (долу).</summary>
    private FrameworkElement Track()
    {
        var pet = Pet;
        int paid = pet.Save.ProjectPaid, n = PetWindow.ProjectStages.Length;
        int cur = pet.ProjectDone ? -1 : paid;
        double frac = pet.ProjectProgress;
        double segH = 28 * U, gap = 4 * U, groupGap = 14 * U;
        double edge = Math.Max(1, P / 2);

        // Еднаква ширина на частите – лентата може да е дълга; превърта се (виж Scroller).
        double segW = 58 * U;
        var grid = new Grid { Margin = new Thickness(14 * U, 6 * U, 14 * U, 0), HorizontalAlignment = HorizontalAlignment.Left };
        for (int i = 0; i < n; i++)
        {
            double extra = (i % 3 == 0 && i > 0 ? groupGap / 2 : gap / 2) + (i % 3 == 2 && i < n - 1 ? groupGap / 2 : gap / 2);
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(segW + extra) });
        }
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(50 * U) });   // наградите
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(segH) });     // лентата
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });          // Мечо и името

        for (int i = 0; i < n; i++)
        {
            int stage = i;
            bool done = i < paid, isCur = i == cur;
            bool claim = pet.CanClaimProject(i), taken = done && !claim && i < pet.ProjectClaimed;
            bool groupEnd = i % 3 == 2 && i < n - 1;
            string name = PetWindow.ProjectStages[i].Name;
            var margin = new Thickness(i % 3 == 0 && i > 0 ? groupGap / 2 : gap / 2, 0, groupEnd ? groupGap / 2 : gap / 2, 0);

            // ── Частта от лентата ──
            var seg = new Grid { Margin = margin, ToolTip = Tip($"{i + 1}. {name}") };
            var bg = new Border
            {
                CornerRadius = new CornerRadius(5 * U),
                Background = claim ? Good : done ? Honey : Empty,
                BorderBrush = Ink, BorderThickness = new Thickness(isCur ? Math.Max(2, P) : edge),
            };
            seg.Children.Add(bg);
            if (isCur)
            {
                // Сегашната се пълни с мед (докъдето сте), с отблясък.
                var fill = new Border { CornerRadius = new CornerRadius(4 * U), Background = Honey, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(Math.Max(2, P)), Width = 0 };
                fill.Child = new Border { Background = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)), Height = Math.Max(2, segH / 5), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(2 * U, 2 * U, 2 * U, 0), CornerRadius = new CornerRadius(P) };
                seg.Children.Add(fill);
                seg.SizeChanged += (_, e) =>
                {
                    if (!e.WidthChanged) return;
                    double to = Math.Max(0, (seg.ActualWidth - 2 * Math.Max(2, P)) * frac);
                    fill.Width = to;
                    if (!Instant)
                        fill.BeginAnimation(WidthProperty, new DoubleAnimation(0, to, TimeSpan.FromMilliseconds(800))
                        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
                };
            }
            if (claim)
                seg.Children.Add(new Viewbox { Child = Icon("gift"), Margin = new Thickness(3 * U) });
            else if (done)
                seg.Children.Add(new Viewbox { Child = Icon("check"), Margin = new Thickness(5 * U), Opacity = 0.8 });
            Grid.SetRow(seg, 1);
            Grid.SetColumn(seg, i);
            grid.Children.Add(seg);

            // ── Наградата над нея ──
            bool shown = pet.ProjectRewardShown(i);
            string plan = shown ? pet.ProjectRewardPlan(i) : "";
            FrameworkElement thing;
            if (claim) thing = Pixel(pet.PropImage("gift_green"), 2);
            else if (taken) thing = Pixel(pet.PropImage("gift_open"), 1);
            else if (!shown) thing = Icon("question");
            else if (pet.ShopArt(plan) is { } art) thing = Pixel(art, 2);
            else thing = Icon(plan.StartsWith("nuts:") ? (isCur ? "nut3" : "nut2") : PetWindow.ProjectRewardIcon(plan), 2);
            thing.Opacity = taken ? 0.4 : !shown ? 0.3 : 1;
            thing.HorizontalAlignment = HorizontalAlignment.Center;
            thing.VerticalAlignment = VerticalAlignment.Bottom;
            var holder = new Grid
            {
                Background = Brushes.Transparent,
                Margin = new Thickness(0, 0, 0, 4 * U),
                ToolTip = Tip(claim ? "Цъкни, за да вземеш: " + pet.ProjectRewardLabel(plan)
                    : taken ? pet.ProjectRewardLabel(plan) + " – взето"
                    : !shown ? "Изненада – ще се види, като наближите"
                    : (isCur ? "Следващата награда: " : "") + pet.ProjectRewardLabel(plan)),
            };
            holder.Children.Add(thing);
            if (claim)
            {
                var spark = Icon("emote_sparkle");
                spark.HorizontalAlignment = HorizontalAlignment.Right;
                spark.VerticalAlignment = VerticalAlignment.Top;
                holder.Children.Add(spark);
            }
            Grid.SetRow(holder, 0);
            Grid.SetColumn(holder, i);
            grid.Children.Add(holder);

            if (claim)
            {
                seg.Cursor = holder.Cursor = System.Windows.Input.Cursors.Hand;
                TaskCards.Clickable(seg, () => Claim(stage));
                TaskCards.Clickable(holder, () => Claim(stage));
            }

            // Звездичка в пролуката между групите – там идва нов ранг.
            if (groupEnd)
            {
                var star = new Viewbox
                {
                    Width = 12 * U, Height = 12 * U, Child = Icon("star"), Opacity = done ? 1 : 0.45,
                    HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, -groupGap / 2 - 6 * U, 0),
                    ToolTip = Tip("Нов ранг: " + PetWindow.ProjectRank(i + 1)),
                };
                Panel.SetZIndex(star, 2);
                Grid.SetRow(star, 1);
                Grid.SetColumn(star, i);
                grid.Children.Add(star);
            }

            if (i == _pop) { Pop(seg, 0.5); Pop(holder, 0.4); }
        }

        // ── Под сегашната част: Мечо и името на етапа ──
        if (cur >= 0)
        {
            var here = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6 * U, 0, 0) };
            var bear = Icon("bear");
            bear.VerticalAlignment = VerticalAlignment.Center;
            bear.Margin = new Thickness(0, 0, 5 * U, 0);
            here.Children.Add(bear);
            here.Children.Add(new TextBlock { Text = PetWindow.ProjectStages[cur].Name, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            // Около сегашната част (в краищата – подравнено към ръба, за да не излиза).
            int span = 5, startCol = Math.Clamp(cur - span / 2, 0, n - span);
            here.HorizontalAlignment = cur - startCol < span / 2 ? HorizontalAlignment.Left : cur - startCol > span / 2 ? HorizontalAlignment.Right : HorizontalAlignment.Center;
            Grid.SetRow(here, 2);
            Grid.SetColumn(here, startCol);
            Grid.SetColumnSpan(here, span);
            grid.Children.Add(here);
        }
        else
        {
            var doneText = new TextBlock { Text = "Играта излезе! Мечо вече е Издател.", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 6 * U, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
            Grid.SetRow(doneText, 2);
            Grid.SetColumnSpan(doneText, n);
            grid.Children.Add(doneText);
        }
        int focus = pet.ProjectHasClaim ? pet.ProjectClaimed : Math.Max(0, Math.Min(paid, n - 1));
        return Scroller(grid, 14 * U + focus * (segW + gap) + segW / 2);
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
        e.RenderTransformOrigin = new Point(0.5, 0.5);
        e.RenderTransform = tr;
        var a = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(700), FillBehavior = FillBehavior.Stop };
        a.KeyFrames.Add(new EasingDoubleKeyFrame(from, KeyTime.FromPercent(0)));
        a.KeyFrames.Add(new EasingDoubleKeyFrame(1.25, KeyTime.FromPercent(0.55)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        a.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromPercent(1)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } });
        tr.BeginAnimation(ScaleTransform.ScaleXProperty, a);
        tr.BeginAnimation(ScaleTransform.ScaleYProperty, a);
    }
}
