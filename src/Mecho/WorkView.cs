using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Mecho.Core;
using static Mecho.Ui;

namespace Mecho;

/// <summary>
/// Задачите по дни (като Sunsama, но с мечок). Вляво – „Някога“ (без ден), после дните един
/// след друг – лента, която се превърта встрани (колелцето, тъчпадът, ◀ ▶, стрелките) и сама
/// се удължава назад и напред. Задачите се влачат между дните и нагоре-надолу в деня; всеки
/// ден показва колко е свършено и колко време е планирано спрямо часовете за деня. Горе:
/// търсене, етикети, само важните, скрий свършените, „План за деня“ и „Край на деня“. Вдясно –
/// подробностите на избраната задача (име, бележка, стъпки, кога, повторение, етикет, време…).
/// Изтриване и местене се отменят (Ctrl+Z или „Отмени“ долу).
/// </summary>
public sealed class WorkView : Border
{
    private enum Side { None, Details }

    private readonly PetWindow _pet;
    private readonly StackPanel _strip = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 0, 0, 0) };
    private readonly ScrollViewer _dayScroll;
    private readonly Border _someday = new();
    private readonly Border _side;
    private readonly TextBlock _rangeLabel = new() { FontWeight = FontWeights.Bold, Foreground = Ink, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _summary = new() { Foreground = Brushes.White, Opacity = 0.9, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Right };
    private readonly WrapPanel _filters = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBox _search;
    private readonly Border _todayChip;
    private NavPill _weekNav = null!;
    private readonly Border _toast;
    private readonly TextBlock _toastText = new() { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 260 };
    private readonly DispatcherTimer _toastTimer = new() { Interval = TimeSpan.FromSeconds(7) };
    private readonly DispatcherTimer _scrollTimer = new() { Interval = TimeSpan.FromMilliseconds(15) };
    private readonly Dictionary<DateTime, (string Sig, Border Column)> _columns = new();
    private readonly Dictionary<string, FrameworkElement> _addBoxes = new();
    private string _somedaySig = "";

    private DateTime _from;          // първият ден в лентата
    private int _count;              // колко дни има в лентата
    private double _target = double.NaN;
    private bool _extending;
    private Side _sideMode;
    private Guid? _selected;
    private string _filter = "";
    private bool _starOnly;
    private string _query = "";
    private string _sideShown = "";
    private Action? _undo;
    private (bool Has, DateTime? Day) _refocus;
    private enum FocusOn { None, Title, NewStep }
    private FocusOn _focusAfter;

    private static readonly CultureInfo Bg = new("bg-BG");
    private static double U => Body / 18;
    private static double ColumnWidth => 280 * U;   // по-широки дни – не е сбутано
    private static double Step => ColumnWidth + 10 * U;

    /// <summary>Понеделникът на седмицата, в която е денят.</summary>
    public static DateTime WeekOf(DateTime day)
    {
        int back = ((int)day.DayOfWeek + 6) % 7;
        return day.Date.AddDays(-back);
    }

    private DateTime Today => _pet.TaskToday;
    /// <summary>До колко се работи в деня (минути); null – почивен ден или още не знае.</summary>
    private int? EndFor(DateTime day) => _pet.WorkEndFor(day);

    /// <summary>Само за този ден (null – както обикновено).</summary>
    private void SetDayEnd(DateTime day, int? minutes)
    {
        _pet.SetDayEnd(day, minutes);
        _todayTimeline?.Refresh(true);
        Refresh(true);
    }

    private void SetUsualEnd(int minutes)
    {
        _pet.SetUsualEnd(minutes);
        _todayTimeline?.Refresh(true);
        Refresh(true);
    }

    /// <summary>Часове през половин час (за менютата).</summary>
    private static IEnumerable<int> HalfHours(int from, int to) => Enumerable.Range(0, (to - from) / 30 + 1).Select(i => from + i * 30);

    /// <summary>
    /// Менюто на поленцето „до 18:00“: само за деня (часове), „Почивен ден“/„Работим“,
    /// „Направи … обикновено“ и „Обикновено (всеки работен ден)“.
    /// </summary>
    private IEnumerable<PixelPopup.Entry> EndEntries(DateTime day)
    {
        bool rest = _pet.IsRestDay(day);
        int? over = _pet.DayEndOverride(day), usual = _pet.UsualEnd, cur = EndFor(day);
        var list = new List<PixelPopup.Entry>();
        if (!rest)
        {
            list.Add(new("Само за този ден", "calendar", null, Sub: () => HalfHours(15 * 60, 22 * 60)
                .Select(m => new PixelPopup.Entry("до " + Clock(m), null, () => SetDayEnd(day, m), On: cur == m))));
            if (over is { } o && usual is { } u && o != u)
            {
                list.Add(new($"Направи {Clock(o)} обикновено", "pin", () => { SetUsualEnd(o); SetDayEnd(day, null); }));
                list.Add(new($"Както обикновено ({Clock(u)})", "reroll", () => SetDayEnd(day, null)));
            }
            list.Add(new("Почивен ден", "relax", () => { _pet.SetRestDay(day, true); Refresh(true); }));
        }
        else list.Add(new("Работим този ден", "tasks", () => { _pet.SetRestDay(day, false); Refresh(true); }));
        list.Add(new("Обикновено (всеки работен ден)", "clock", null, Sub: () => HalfHours(15 * 60, 22 * 60)
            .Select(m => new PixelPopup.Entry("до " + Clock(m), null, () => SetUsualEnd(m), On: usual == m))));
        if (day.Date == DateTime.Today && _pet.WorkDoneToday != null)
            list.Add(new("Още не съм свършил", "close", () => { _pet.UndoWorkFinished(); Refresh(true); }));
        return list;
    }

    private static string Clock(int minutes) => $"{minutes / 60}:{minutes % 60:00}";

    /// <summary>Кой ден се гледа в „Днес“ (◀ ▶ сменят деня).</summary>
    private DateTime? _todayDay;

    public WorkView(PetWindow pet)
    {
        _pet = pet;
        _from = WeekOf(pet.TaskToday).AddDays(-7);
        _count = 42;
        Background = Brushes.Transparent;

        // ── Под него (само в „Седмица“): ◀ Днес ▶, кои дни се виждат, търсене, етикети … обобщение ──
        var weekNav = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 10, 4), VerticalAlignment = VerticalAlignment.Center };
        // ◀ ▶ (по седмица; и с колелцето, влачене, ←/→) и хапчето с дните / „Към днес“.
        _weekNav = MakeNav(() => ScrollBy(-1), () => ScrollBy(1), () => ScrollToDay(Today),
            b => DayPicker.Show(b, Today, Today, d => { if (d is { } x) ScrollToDay(x); }, allowSomeday: false, title: "Към кой ден?"),
            "Ден назад (←)", "Ден напред (→)");
        _todayChip = new Border();
        weekNav.Children.Add(_weekNav.Root);
        // ── Втори ред: търсене, етикети, само важните, скрий свършените … обобщение ──
        // Пренася се на втори ред, ако не се събира (стрелките, търсенето, филтрите).
        var bar = new WrapPanel { Margin = new Thickness(4, 0, 2, 6) };
        _weekBar = bar;
        DockPanel.SetDock(weekNav, Dock.Left);
        bar.Children.Add(weekNav);
        _search = MenuWindow.NewTextInput();
        _search.Padding = new Thickness(6, 3, 6, 3);
        _search.MaxLength = 100;
        _search.TextChanged += (_, _) =>
        {
            _query = _search.Text.Trim();
            Refresh();
        };
        _search.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            _search.Clear();
            Keyboard.ClearFocus();
            e.Handled = true;
        };
        var searchBox = new Border { Width = 150 * U, Margin = new Thickness(0, 0, 8, 4), Child = MenuWindow.WithHint(_search, "Търси…") };
        DockPanel.SetDock(searchBox, Dock.Left);
        bar.Children.Add(searchBox);
        // Тясно (малко меню): търсенето и надписът с дните – по-тесни, филтрите да се събират.
        bar.SizeChanged += (_, _) =>
        {
            bool narrow = bar.ActualWidth < 600 * U;
            searchBox.Width = (narrow ? 90 : 150) * U;

        };
        DockPanel.SetDock(_filters, Dock.Left);
        bar.Children.Add(_filters);
        // Обобщението на седмицата е в „Преглед“ (тук редът е тесен).

        // ── Тялото: „Някога“ | дните (лента) | вдясно подробности / план / край на деня ──
        _dayScroll = new ScrollViewer
        {
            Content = _strip,
            // Без лентата отдолу: дните се местят с колелцето, влачене със скрол-мишка, стрелките.
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Background = Brushes.Transparent,
        };
        _dayScroll.ScrollChanged += (_, _) => OnScrolled();
        _dayScroll.PreviewMouseWheel += OnWheel;
        // При влачене до левия/десния ръб – дните се превъртат.
        _dayScroll.PreviewDragOver += (_, e) =>
        {
            var p = e.GetPosition(_dayScroll);
            if (p.X < 40 * U) ScrollTo(_dayScroll.HorizontalOffset - 16 * U, smooth: false);
            else if (p.X > _dayScroll.ActualWidth - 40 * U) ScrollTo(_dayScroll.HorizontalOffset + 16 * U, smooth: false);
        };
        _scrollTimer.Tick += (_, _) => ScrollStep();

        _side = new Border { Width = 320 * U, Margin = new Thickness(10 * U, 0, 0, 0), Visibility = Visibility.Collapsed };
        // Тясно (малко меню) – панелът вдясно е по-тесен, задачите да не се смачкват.
        // В „Днес“ графикът е по-широк (задачите в него да се четат).
        SizeChanged += (_, _) => SideWidth();
        _someday.Width = ColumnWidth;
        _someday.Margin = new Thickness(0, 0, 10 * U, 0);

        // „Седмица“: „Някога“ | дните; „Днес“ и „Преглед“ – свои страници. Вдясно (общо за
        // „Днес“ и „Седмица“) – подробностите на задачата, планът, краят на деня, работата сега.
        var week = new DockPanel();
        // „Някога“ вече е в Backlog (отделна страница); дните са във фина рамка – не излизат от ръба.
        _someday.Visibility = Visibility.Collapsed;
        // Отместиш ли се от днес – отстрани изскача стрелка „обратно към днес“.
        _backLeft = BackChip(left: true);
        _backRight = BackChip(left: false);
        week.Children.Add(new Border
        {
            BorderBrush = WoodLight,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            // Отстъпът е вътре в лентата – дните се режат точно на рамката, не преди нея.
            Padding = new Thickness(0, 6 * U, 0, 2 * U),
            ClipToBounds = true,
            Child = new Grid { Children = { _dayScroll, _backLeft, _backRight } },
        });
        WirePan();
        _weekView = week;
        _todayView = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = _todayPanel };
        _reviewView = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = _reviewPanel };
        _backlogView = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = _backlogPanel };
        var host = new Grid { Children = { _todayView, _weekView, _reviewView, _backlogView, _calendarHost } };
        var body = new DockPanel();
        DockPanel.SetDock(_side, Dock.Right);
        body.Children.Add(_side);
        body.Children.Add(host);

        // „Отмени“ – долу в средата, над дните.
        var undo = Chip("Отмени", Undo);
        undo.Margin = new Thickness(10, 0, 0, 0);
        undo.ToolTip = Tip("Ctrl+Z");
        var hide = Chip("✕", HideToast);
        hide.Margin = new Thickness(6, 0, 0, 0);
        var toastRow = new StackPanel { Orientation = Orientation.Horizontal };
        toastRow.Children.Add(_toastText);
        toastRow.Children.Add(undo);
        toastRow.Children.Add(hide);
        _toast = new Border
        {
            Background = Cream,
            BorderBrush = Ink,
            BorderThickness = Px(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12, 6, 6, 6),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 18 * U),
            Child = toastRow,
            Visibility = Visibility.Collapsed,
            Effect = HardShadow(),
        };
        _toastTimer.Tick += (_, _) => HideToast();
        var bodyLayer = new Grid { Children = { body, _toast } };

        // Хартията и рамката са на менюто (тук – само съдържанието).
        var root = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        root.Children.Add(bar);
        root.Children.Add(bodyLayer);
        Child = root;
        _view = (View)Math.Clamp(_pet.Save.WorkView, 0, 4);
        _clock.Tick += (_, _) => UpdateClock();
        _clock.Start();
        _pet.CareChanged += OnCareChanged;
        _pet.TaskRemoved += OnTaskRemoved;
        // Скрит (друга страница на менюто) – не се строи; щом се покаже – наново.
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible && _stale) Refresh(true);
        };
        Loaded += (_, _) => Dispatcher.BeginInvoke(() =>
        {
            if (_view != View.Week) return;   // лентата се нарежда, когато „Седмица“ се отвори
            _weekPlaced = true;
            ScrollToDay(Today, smooth: false);
            _dayScroll.UpdateLayout();
            _ready = true;
        }, DispatcherPriority.Loaded);
        SetView(_view);
    }

    private bool _stale;

    /// <summary>Менюто се затваря завинаги (изход): абонаментите и часовниците спират.</summary>
    public void Dispose()
    {
        _pet.CareChanged -= OnCareChanged;
        _pet.TaskRemoved -= OnTaskRemoved;
        _scrollTimer.Stop();
        _toastTimer.Stop();
        _clock.Stop();
    }

    // ───────────────────────── Изгледи ─────────────────────────

    public enum View { Today, Week, Review, Backlog, Calendar }

    private View _view;
    private FrameworkElement _weekBar = null!, _weekView = null!;
    private ScrollViewer _todayView = null!, _reviewView = null!, _backlogView = null!;
    private readonly StackPanel _backlogPanel = new() { Margin = new Thickness(0, 0, 8, 12) };
    private readonly Border _calendarHost = new();
    private string _backlogShown = "";
    private readonly StackPanel _todayPanel = new() { Margin = new Thickness(4 * U, 0, 10 * U, 10 * U), MaxWidth = 760 };
    private readonly StackPanel _reviewPanel = new() { Margin = new Thickness(4 * U, 0, 10 * U, 10 * U), MaxWidth = 900 };
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };

    internal View CurrentView => _view;
    private bool _weekPlaced;

    /// <summary>Днес / Седмица / Преглед.</summary>
    public void SetView(View view)
    {
        Commit();
        // „План за деня“ и „Край на деня“ са само в „Днес“ – в другите изгледи се затварят.
        if (view == View.Today) _todayDay = null;   // „Днес“ винаги почва от днешния ден
        bool changed = _view != view;
        _view = view;
        _pet.Save.WorkView = (int)view;
        if (changed) ViewChanged?.Invoke(view);
        _todayView.Visibility = view == View.Today ? Visibility.Visible : Visibility.Collapsed;
        _weekView.Visibility = view == View.Week ? Visibility.Visible : Visibility.Collapsed;
        _reviewView.Visibility = view == View.Review ? Visibility.Visible : Visibility.Collapsed;
        _backlogView.Visibility = view == View.Backlog ? Visibility.Visible : Visibility.Collapsed;
        _calendarHost.Visibility = view == View.Calendar ? Visibility.Visible : Visibility.Collapsed;
        _weekBar.Visibility = view == View.Week ? Visibility.Visible : Visibility.Collapsed;
        if (view is View.Review or View.Backlog or View.Calendar)
        {
            _sideMode = Side.None;
            _selected = null;
        }
        _sideShown = "";
        _todayShown = _reviewShown = _backlogShown = "";
        SideWidth();
        Refresh(true);
        // „Седмица“ за пръв път – лентата застава на днес (иначе – където е била).
        if (view == View.Week && !_weekPlaced && IsLoaded)
        {
            _weekPlaced = true;
            _ready = false;
            Dispatcher.BeginInvoke(() =>
            {
                ScrollToDay(Today, smooth: false);
                _dayScroll.UpdateLayout();
                _ready = true;
            }, DispatcherPriority.Loaded);
        }
    }

    /// <summary>
    /// Навигацията по дни (като в Sunsama), еднаква в Днес, Седмица и Календар: ◀ ▶ и хапче
    /// „📅 Днес“; отместиш ли се – „📅 Утре | ↩ Към днес“. Цъкнеш ли надписа – календар за скок.
    /// </summary>
    private sealed class NavPill
    {
        public FrameworkElement Root = null!;
        public TextBlock Label = null!;
        public FrameworkElement Back = null!;
    }

    private NavPill MakeNav(Action prev, Action next, Action back, Action<FrameworkElement>? pick, string prevTip, string nextTip)
    {
        var nav = new NavPill();
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var p = HeaderChip("", prev, prevTip);
        p.Child = Icon("nav_prev");
        p.Margin = new Thickness(0, 0, 4, 0);
        var n = HeaderChip("", next, nextTip);
        n.Child = Icon("nav_next");
        n.Margin = new Thickness(0, 0, 8, 0);
        row.Children.Add(p);
        row.Children.Add(n);
        FrameworkElement Part(string icon, TextBlock text, Action<FrameworkElement> click, string tip)
        {
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            var i = Icon(icon);
            i.Margin = new Thickness(0, 0, 6, 0);
            i.VerticalAlignment = VerticalAlignment.Center;
            content.Children.Add(i);
            content.Children.Add(text);
            var box = new Border { Child = content, Padding = new Thickness(8, 3, 8, 4), Background = Brushes.Transparent, Cursor = Cursors.Hand, ToolTip = Tip(tip), CornerRadius = new CornerRadius(3) };
            box.MouseEnter += (_, _) => box.Background = Hover;
            box.MouseLeave += (_, _) => box.Background = Brushes.Transparent;
            TaskCards.Clickable(box, () => click(box));
            return box;
        }
        nav.Label = new TextBlock { FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        var label = Part("calendar", nav.Label, b => pick?.Invoke(b), "Избери ден");
        var backText = new TextBlock { Text = "Към днес", FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
        var backPart = Part("history", backText, _ => back(), "Обратно към днес (T)");
        var sep = new Border { Width = 1, Background = WoodLight, Margin = new Thickness(0, 3, 0, 3) };
        var backBox = new StackPanel { Orientation = Orientation.Horizontal, Children = { sep, backPart } };
        nav.Back = backBox;
        var pill = new Border
        {
            Background = Cream,
            BorderBrush = WoodDark,
            BorderThickness = Flat,
            CornerRadius = new CornerRadius(4),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { label, backBox } },
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.Children.Add(pill);
        nav.Root = row;
        return nav;
    }

    /// <summary>Как се казва денят в навигацията: Днес, Утре, Вчера или „ср, 18 ное“.</summary>
    private string NavDayName(DateTime d) =>
        d == Today ? "Днес" : d == Today.AddDays(1) ? "Утре" : d == Today.AddDays(-1) ? "Вчера" : d.ToString(d.Year == Today.Year ? "ddd, d MMM" : "d MMM yyyy", Bg);

    private void SideWidth()
    {
        bool wide = _view == View.Today && _sideMode == Side.None;
        // Графикът расте с прозореца (по-голямо меню – по-широки часове).
        if (ActualWidth > 0) _side.Width = Math.Clamp(ActualWidth * (wide ? 0.45 : 0.42), 200 * U, wide ? 560 * U : 320 * U);
    }

    private static Border HeaderChip(string text, Action click, string tip)
    {
        var chip = Chip(text, click);
        chip.Padding = new Thickness(7, 1, 7, 2);
        chip.VerticalAlignment = VerticalAlignment.Center;
        chip.Margin = new Thickness(4, 0, 0, 0);
        if (tip.Length > 0) chip.ToolTip = Tip(tip);
        return chip;
    }

    private void OnCareChanged(object? sender, EventArgs e) => Refresh();

    /// <summary>Отваря подробностите на задачата (от таблото, от менюто).</summary>
    public void SelectTask(Guid id)
    {
        var t = _pet.Save.Tasks.FirstOrDefault(x => x.Id == id);
        if (t == null) return;
        if (_view == View.Review) SetView(t.Day == Today ? View.Today : View.Week);
        Select(id, FocusOn.Title);
    }

    /// <summary>„Нова“ от таблото: полето за нова задача за днес.</summary>
    public void FocusNewTask()
    {
        if (_view != View.Today) SetView(View.Today);
        FocusAdd(Today);
    }

    /// <summary>Отваря от вън прегледа на седмицата (или затваря страничната карта).</summary>
    public void OpenSide(string side)
    {
        if (side == "review")
        {
            SetView(View.Review);
            return;
        }
        ShowSide(Side.None);
    }

    // ───────────────────────── Превъртане ─────────────────────────

    private int IndexOf(DateTime day) => (int)(day.Date - _from).TotalDays;

    /// <summary>Денят да е първият изцяло видим (вчерашният се подава отляво).</summary>
    private void ScrollToDay(DateTime day, bool smooth = true)
    {
        EnsureRange(day);
        ScrollTo(Math.Max(0, IndexOf(day) * Step - 26 * U), smooth);
    }

    private void ScrollBy(int days) => ScrollTo((double.IsNaN(_target) ? _dayScroll.HorizontalOffset : _target) + days * Step, smooth: true);

    private void ScrollTo(double offset, bool smooth)
    {
        offset = Math.Max(0, offset);
        if (!smooth)
        {
            _target = double.NaN;
            _scrollTimer.Stop();
            _dayScroll.ScrollToHorizontalOffset(offset);
            return;
        }
        _target = offset;
        if (!_scrollTimer.IsEnabled) _scrollTimer.Start();
    }

    /// <summary>Плавно: всеки кадър – една четвърт от оставащото.</summary>
    private void ScrollStep()
    {
        if (double.IsNaN(_target))
        {
            _scrollTimer.Stop();
            return;
        }
        double now = _dayScroll.HorizontalOffset;
        double max = Math.Max(0, _dayScroll.ExtentWidth - _dayScroll.ViewportWidth);
        double target = Math.Clamp(_target, 0, Math.Max(max, _target));
        double next = now + (target - now) * 0.28;
        if (Math.Abs(target - next) < 0.6) next = target;
        _dayScroll.ScrollToHorizontalOffset(next);
        if (next == target || (target > max && now >= max - 0.5))
        {
            _target = double.NaN;
            _scrollTimer.Stop();
        }
    }

    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        // Над списък с картички, който може да се превърти – надолу/нагоре. Иначе (и с Shift) – встрани.
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            for (var d = e.OriginalSource as DependencyObject; d != null && d != _dayScroll; d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
                if (d is ScrollViewer inner && inner.ScrollableHeight > 0) return;
        }
        ScrollTo((double.IsNaN(_target) ? _dayScroll.HorizontalOffset : _target) - e.Delta * 1.1, smooth: true);
        e.Handled = true;
    }

    /// <summary>Тъчпадът и мишките с колелце встрани (WM_MOUSEHWHEEL – менюто го препраща).</summary>
    public void HorizontalWheel(int delta)
    {
        if (_view != View.Week) return;
        ScrollTo((double.IsNaN(_target) ? _dayScroll.HorizontalOffset : _target) + delta * 1.1, smooth: true);
    }

    private bool _ready;              // лентата е на мястото си (след първото показване) – чак тогава расте
    private const int MaxDays = 400;   // повече дни в лентата не се пазят наведнъж

    /// <summary>
    /// Близо до края на лентата – още дни (две седмици) отзад или отпред. Става малко след
    /// превъртането (Background), веднъж наведнъж: иначе новата ширина и новото място идват
    /// в различни моменти и проверката може да се върти в кръг.
    /// </summary>
    private void OnScrolled()
    {
        UpdateRangeLabel();
        if (!_ready || _extending || _view != View.Week || _dayScroll.ViewportWidth <= 0) return;
        _extending = true;
        Dispatcher.BeginInvoke(() =>
        {
            try { ExtendIfNeeded(); }
            finally { Dispatcher.BeginInvoke(() => _extending = false, DispatcherPriority.ContextIdle); }
        }, DispatcherPriority.Background);
    }

    private void ExtendIfNeeded()
    {
        if (!IsLoaded || _dayScroll.ViewportWidth <= 0) return;
        double offset = _dayScroll.HorizontalOffset;
        if (offset < Step * 2 && _count + 14 <= MaxDays)
        {
            _from = _from.AddDays(-14);
            _count += 14;
            Refresh(true);
            _dayScroll.UpdateLayout();
            _dayScroll.ScrollToHorizontalOffset(offset + 14 * Step);
            if (!double.IsNaN(_target)) _target += 14 * Step;
            _dayScroll.UpdateLayout();
        }
        else if (offset + _dayScroll.ViewportWidth > _dayScroll.ExtentWidth - Step * 2 && _count + 14 <= MaxDays)
        {
            _count += 14;
            Refresh(true);
            _dayScroll.UpdateLayout();
        }
    }

    /// <summary>Денят да е в лентата (иначе тя се разширява до него).</summary>
    private void EnsureRange(DateTime day)
    {
        bool changed = false;
        if (day < _from.AddDays(7))
        {
            int add = (int)Math.Ceiling((_from.AddDays(7) - day).TotalDays / 7) * 7;
            _from = _from.AddDays(-add);
            _count += add;
            changed = true;
        }
        if (day > _from.AddDays(_count - 8))
        {
            _count = Math.Min(Math.Max(_count, IndexOf(day) + 15), MaxDays * 2);
            changed = true;
        }
        if (changed)
        {
            Refresh(true);
            _dayScroll.UpdateLayout();
        }
    }

    private Border? _backLeft, _backRight;

    /// <summary>„Обратно към днес“ – стрелка на ръба, от страната на днешния ден.</summary>
    private Border BackChip(bool left)
    {
        var chip = Chip("", () => ScrollToDay(Today));
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        if (left) row.Children.Add(Icon("nav_prev"));
        row.Children.Add(new TextBlock { Text = "Днес", FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 4, 0) });
        if (!left) row.Children.Add(Icon("nav_next"));
        chip.Child = row;
        chip.HorizontalAlignment = left ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        chip.VerticalAlignment = VerticalAlignment.Bottom;
        chip.Margin = new Thickness(8 * U, 0, 8 * U, 14 * U);
        chip.Padding = new Thickness(6, 3, 6, 4);
        chip.Effect = HardShadow();
        chip.ToolTip = Tip("Обратно към днес (T)");
        chip.Visibility = Visibility.Collapsed;
        return chip;
    }

    // ── Влачене на дните с мишката (по празното място, не по задачите и копчетата) ──
    private Point? _panFrom;
    private double _panOffset;
    private bool _panning;

    private void WirePan()
    {
        _dayScroll.AddHandler(MouseLeftButtonDownEvent, new MouseButtonEventHandler((_, e) =>
        {
            if (TaskCards.Dragging || !Pannable(e.OriginalSource as DependencyObject)) return;
            _panFrom = e.GetPosition(_dayScroll);
            _panOffset = _dayScroll.HorizontalOffset;
            _panning = false;
        }), true);
        _dayScroll.PreviewMouseMove += (_, e) =>
        {
            if (_panFrom is not { } from) return;
            if (e.LeftButton != MouseButtonState.Pressed) { EndPan(); return; }
            double dx = e.GetPosition(_dayScroll).X - from.X;
            if (!_panning && Math.Abs(dx) < 6) return;
            if (!_panning)
            {
                _panning = true;
                _dayScroll.CaptureMouse();
                _dayScroll.Cursor = Cursors.SizeWE;
            }
            ScrollTo(_panOffset - dx, smooth: false);
            e.Handled = true;
        };
        _dayScroll.PreviewMouseLeftButtonUp += (_, e) =>
        {
            if (_panning) e.Handled = true;
            EndPan();
        };
        _dayScroll.LostMouseCapture += (_, _) => { if (_panning) EndPan(); };
    }

    private void EndPan()
    {
        bool was = _panning;
        _panFrom = null;
        _panning = false;
        _dayScroll.Cursor = null;
        if (was) _dayScroll.ReleaseMouseCapture();
    }

    /// <summary>Може ли оттук да се влачат дните: не по задача, копче, поле за писане.</summary>
    private bool Pannable(DependencyObject? d)
    {
        for (; d != null && d != _dayScroll; d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
        {
            if (d is TextBox or System.Windows.Controls.Primitives.ScrollBar or ButtonBase) return false;
            if (d is FrameworkElement { Tag: TaskItem }) return false;
            if (d is FrameworkElement f && f.Cursor == Cursors.Hand) return false;
        }
        return true;
    }

    private void UpdateRangeLabel()
    {
        if (_dayScroll.ViewportWidth <= 0) return;
        int first = (int)Math.Floor((_dayScroll.HorizontalOffset + Step * 0.4) / Step);
        int last = (int)Math.Floor((_dayScroll.HorizontalOffset + _dayScroll.ViewportWidth - Step * 0.4) / Step);
        var a = _from.AddDays(Math.Max(0, first));
        var b = _from.AddDays(Math.Max(first, last));
        _rangeLabel.Text = a.Month == b.Month
            ? (a == b ? a.ToString("d MMMM yyyy", Bg) : $"{a.Day} – {b.ToString("d MMMM yyyy", Bg)}")
            : $"{a.ToString("d MMM", Bg)} – {b.ToString("d MMM yyyy", Bg)}";
        bool todayVisible = Today >= a && Today <= b;
        _weekNav.Label.Text = _rangeLabel.Text;
        _weekNav.Back.Visibility = todayVisible ? Visibility.Collapsed : Visibility.Visible;
        if (_backLeft != null && _backRight != null)
        {
            _backLeft.Visibility = !todayVisible && Today < a ? Visibility.Visible : Visibility.Collapsed;
            _backRight.Visibility = !todayVisible && Today > b ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    // ───────────────────────── Строене ─────────────────────────

    private bool Passes(TaskItem t)
    {
        if (_filter.Length > 0 && t.Tag != _filter) return false;
        if (_starOnly && !t.Star) return false;
        if (_query.Length > 0 && !(Has(t.Title) || Has(t.Description) || t.StepList.Any(x => Has(x.Title)))) return false;
        return true;
    }

    private bool Has(string text) => text.Contains(_query, StringComparison.CurrentCultureIgnoreCase);

    /// <summary>Строи наново това, което се е сменило (в „Седмица“ – всяка колона поотделно).</summary>
    public void Refresh(bool force = false)
    {
        if (TaskCards.Dragging) return;   // по време на влачене – не (картичката, която се влачи, ще изчезне)
        if (!IsVisible && IsLoaded)
        {
            _stale = true;
            return;
        }
        _stale = false;
        var state = _pet.TaskState;
        var today = Today;
        switch (_view)
        {
            case View.Today: BuildToday(state, _todayDay ?? today, force); break;
            case View.Review: BuildReview(state, today, force); break;
            case View.Backlog: BuildBacklog(state, today, force); break;
            case View.Calendar: BuildCalendar(state, today, force); break;
            default: RefreshWeek(state, today, force); break;
        }
        BuildSide();
        RestoreFocus();
        UpdateClock();
    }

    private void RefreshWeek(TasksState state, DateTime today, bool force)
    {
        // Задачите по дни – веднъж за всички колони.
        var byDay = new Dictionary<DateTime, List<TaskItem>>();
        void Put(DateTime day, TaskItem t)
        {
            if (!byDay.TryGetValue(day, out var list)) byDay[day] = list = new List<TaskItem>();
            list.Add(t);
        }
        var last = _from.AddDays(_count);
        foreach (var t in state.Open)
            if (t.Day is { } d && d >= _from && d < last) Put(d, t);
        foreach (var t in state.History.Reverse())
            if ((t.Day ?? t.DoneAt?.Date) is { } d && d >= _from && d < last) Put(d, t);

        string common = $"{today:yyyyMMdd}#{_filter}#{_starOnly}#{_query}#{_pet.Save.PlannerHideDone}#{_pet.Save.UiTheme}#{(_pet.InFocus ? _pet.FocusTask?.Id : null)}";
        // Избраната задача е само в подписа на нейната колона (иначе всеки клик строи всички дни).
        string Selected(List<TaskItem> tasks) => _sideMode == Side.Details && _selected is { } s && tasks.Any(t => t.Id == s) ? "#S" + s : "";
        if (force)
        {
            _columns.Clear();
            _somedaySig = "";
        }

        // „Някога“.
        var someday = state.Someday.ToList();
        string sig = common + Selected(someday) + "|" + string.Join("|", someday.Select(t => t.Signature + _pet.TaskOnBoard(t)));
        if (sig != _somedaySig)
        {
            _somedaySig = sig;
            _someday.Child = Column(null, someday.Where(Passes).ToList(), today);
        }

        // Дните.
        for (int i = 0; i < _count; i++)
        {
            var day = _from.AddDays(i);
            var tasks = byDay.TryGetValue(day, out var l) ? l : new List<TaskItem>();
            _pet.Save.DayNotes.TryGetValue(day.ToString("yyyy-MM-dd"), out var note);
            string daySig = common + Selected(tasks) + "|" + note + "|" + string.Join("|", tasks.Select(t => t.Signature + _pet.TaskOnBoard(t)));
            if (!_columns.TryGetValue(day, out var col) || col.Sig != daySig)
            {
                var column = Column(day, tasks.Where(Passes).ToList(), today);
                column.Width = ColumnWidth;
                column.Margin = new Thickness(0, 0, Step - ColumnWidth, 0);
                _columns[day] = (daySig, column);
            }
            var element = _columns[day].Column;
            if (i < _strip.Children.Count)
            {
                if (_strip.Children[i] != element)
                {
                    if (element.Parent is Panel p) p.Children.Remove(element);
                    if (i < _strip.Children.Count) _strip.Children.RemoveAt(i);
                    _strip.Children.Insert(Math.Min(i, _strip.Children.Count), element);
                }
            }
            else
            {
                if (element.Parent is Panel p) p.Children.Remove(element);
                _strip.Children.Add(element);
            }
        }
        while (_strip.Children.Count > _count) _strip.Children.RemoveAt(_strip.Children.Count - 1);
        // Старите колони извън лентата не се пазят.
        foreach (var gone in _columns.Keys.Where(d => d < _from || d >= last).ToList()) _columns.Remove(gone);

        BuildFilters();
        UpdateSummary(state, today);
        UpdateRangeLabel();
    }

    private string _filtersShown = "";

    private void BuildFilters()
    {
        string sig = $"{_filter}#{_starOnly}#{_pet.Save.PlannerHideDone}#{_pet.Save.UiTheme}#{TaskTags.Version}";
        if (sig == _filtersShown) return;
        _filtersShown = sig;
        _filters.Children.Clear();
        // Етикетът – едно копче (етикетите са свои и може да са много): изскача списък.
        var current = TaskTags.Find(_filter);
        Border tagChip = null!;
        tagChip = Chip("", () => PixelPopup.Show(tagChip, "Покажи само", () =>
            TaskTags.All.Select(tag => new PixelPopup.Entry(tag.Name, null, () => SetFilter(_filter == tag.Key ? "" : tag.Key, _starOnly),
                    On: _filter == tag.Key, Swatch: tag.Key))
                .Prepend(new PixelPopup.Entry("Всички етикети", null, () => SetFilter("", _starOnly), On: _filter.Length == 0))
                .Append(new PixelPopup.Entry("Етикети…", "gear", null, Sub: () => new[]
                {
                    new PixelPopup.Entry("", null, null, Custom: _ => TagEditor.Manager(_pet)),
                }))));
        var tagRow = new StackPanel { Orientation = Orientation.Horizontal };
        if (current != null) tagRow.Children.Add(TaskTags.Swatch(current.Key));
        var tagText = new TextBlock { Text = (current?.Name ?? "Етикет") + " ▾", FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 110 * U };
        tagRow.Children.Add(tagText);
        tagChip.Child = tagRow;
        tagChip.ToolTip = Tip(current != null ? $"Само „{current.Name}“ (новите задачи също ще са с този етикет)" : "Покажи само един етикет; оттук – и своите етикети");
        SetOn(tagChip, current != null);
        _filters.Children.Add(tagChip);
        var star = Chip("", () => SetFilter(_filter, !_starOnly));
        star.Child = Icon("flag_high");
        star.ToolTip = Tip("Само важните (приоритет „Важно“)");
        SetOn(star, _starOnly);
        _filters.Children.Add(star);
        var hideDone = Chip("", () =>
        {
            _pet.Save.PlannerHideDone = !_pet.Save.PlannerHideDone;
            _pet.Persist();
            Refresh(true);
        });
        hideDone.Child = IconText("check", _pet.Save.PlannerHideDone ? "скрити" : "", bold: true);
        hideDone.ToolTip = Tip(_pet.Save.PlannerHideDone ? "Свършените са скрити – цъкни, за да се виждат" : "Скрий свършените задачи");
        SetOn(hideDone, _pet.Save.PlannerHideDone);
        _filters.Children.Add(hideDone);
        foreach (FrameworkElement c in _filters.Children)
        {
            c.Margin = new Thickness(0, 0, 4, 2);
            if (c is Border b) b.Padding = new Thickness(6, 1, 6, 2);
        }
    }

    private void SetFilter(string key, bool starOnly)
    {
        _filter = key;
        _starOnly = starOnly;
        Refresh(true);
    }

    private void UpdateSummary(TasksState state, DateTime today)
    {
        var week = WeekOf(today);
        var tasks = Enumerable.Range(0, 7).SelectMany(i => state.OnDay(week.AddDays(i))).ToList();
        int done = tasks.Count(t => t.Done);
        int spent = tasks.Sum(t => t.Spent);
        var parts = new List<string>();
        if (tasks.Count > 0) parts.Add($"седмицата: {done}/{tasks.Count}");
        if (spent > 0) parts.Add($"работено {TaskCards.Duration(spent)}");
        int someday = state.Someday.Count();
        if (someday > 0) parts.Add($"някога {someday}");
        if (_query.Length > 0)
        {
            int found = state.Open.Count(Passes);
            parts.Insert(0, found == 0 ? "Нищо не е намерено" : $"Намерени: {found}");
        }
        _summary.Text = string.Join("  ·  ", parts);
    }

    /// <summary>Една колона: ден (или „Някога“ – day null).</summary>
    private Border Column(DateTime? day, List<TaskItem> tasks, DateTime today)
    {
        bool isToday = day == today, past = day < today;
        bool weekend = day is { DayOfWeek: DayOfWeek.Saturday or DayOfWeek.Sunday };
        var dock = new DockPanel();

        // ── Заглавие ──
        var head = new StackPanel { Margin = new Thickness(2 * U, 0, 2 * U, 6 * U), Background = Brushes.Transparent };
        var line = new DockPanel();
        int done = tasks.Count(t => t.Done), all = tasks.Count;
        TextBlock? countBox = null;   // „2/5“ – на реда с датата (името на деня да се събира)
        if (all > 0)
        {
            var count = new TextBlock
            {
                Text = $"{done}/{all}",
                Foreground = done == all ? Good : Muted,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0),
                ToolTip = Tip("Свършени от всички"),
            };
            DockPanel.SetDock(count, Dock.Right);
            countBox = count;
        }
        string name = day is { } d0 ? Capital(d0.ToString("dddd", Bg)) : "Някога";
        // „···“ – малко меню за деня (нова задача, всички недовършени – за днес, утре, „Някога“).
        var dayMenu = new TextBlock { Text = "···", FontWeight = FontWeights.Bold, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, ToolTip = Tip("Още за деня") };
        TaskCards.Clickable(dayMenu, () => PixelPopup.Show(dayMenu, day is { } dd ? Capital(dd.ToString("dddd", Bg)) : "Някога", ColumnEntries(day, tasks)));
        DockPanel.SetDock(dayMenu, Dock.Right);
        line.Children.Insert(0, dayMenu);
        // ⇅ – подреди задачите в деня (само когато са повече от една).
        if (tasks.Count(t => !t.Done) > 1)
        {
            var sortIcon = Icon("sort");
            var sortBox = new Border { Child = sortIcon, Background = Brushes.Transparent, Padding = new Thickness(3, 2, 3, 2), Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, ToolTip = Tip("Подреди задачите в деня"), Opacity = 0.75 };
            TaskCards.Clickable(sortBox, () => PixelPopup.Show(sortBox, "Подреди", SortEntries(day)));
            DockPanel.SetDock(sortBox, Dock.Right);
            line.Children.Insert(1, sortBox);
        }
        line.Children.Add(new TextBlock { Text = name, FontWeight = FontWeights.Bold, FontSize = Body * 1.15, Foreground = Ink, TextTrimming = TextTrimming.CharacterEllipsis });
        head.Children.Add(line);

        var sub = new DockPanel { Margin = new Thickness(0, 2 * U, 0, 0) };
        if (countBox != null) sub.Children.Add(countBox);
        if (isToday || day == today.AddDays(1) || day == today.AddDays(-1))
        {
            var pill = new Border
            {
                Background = isToday ? Honey : PaperDark,
                BorderBrush = Ink,
                BorderThickness = new Thickness(Math.Max(1, P / 2)),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(5, 0, 5, 1),
                Margin = new Thickness(0, 0, 6, 0),
                Child = new TextBlock { Text = isToday ? "днес" : day == today.AddDays(1) ? "утре" : "вчера", FontWeight = FontWeights.Bold },
            };
            DockPanel.SetDock(pill, Dock.Left);
            sub.Children.Add(pill);
        }
        sub.Children.Add(new TextBlock
        {
            // Късият месец („15 ноем.“) – събира се и до „днес“ и „2/5“; целият – при посочване.
            Text = day is { } d1 ? d1.ToString(d1.Year == today.Year ? "d MMM" : "d MMM yy", Bg) : "без ден",
            ToolTip = day is { } d3 ? Tip(d3.ToString("d MMMM yyyy", Bg)) : null,
            Foreground = Muted,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        head.Children.Add(sub);

        // Колко време са задачите в деня – само като сведение (без „не стига“).
        int planned = tasks.Where(t => !t.Done).Sum(t => t.Minutes);
        if (day != null && planned > 0)
            head.Children.Add(new TextBlock { Text = "задачи: " + TaskCards.Duration(planned), Foreground = Muted, Margin = new Thickness(0, 2 * U, 0, 0) });

        // Жълтото часовниче: пренесените (или, без автоматично пренасяне – останалите в минали
        // дни). Цъкнеш ли го – „Автоматично пренасяне: Да / Не“.
        int rolled = tasks.Count(t => !t.Done && t.Rolled > 0);
        bool auto = _pet.AutoRoll;
        int stayed = auto ? 0 : _pet.TaskState.Open.Count(t => !t.Done && t.Day is { } d && d < Today);
        if (isToday && (rolled > 0 || stayed > 0))
        {
            string text = rolled > 0
                ? (rolled == 1 ? "1 пренесена от вчера" : $"{rolled} пренесени от минали дни")
                : (stayed == 1 ? "1 недовършена остана в минал ден" : $"{stayed} недовършени останаха в минали дни");
            var info = IconText("history", text + "  ▾");
            info.Opacity = 0.75;
            info.Margin = new Thickness(0, 4 * U, 0, 0);
            info.Background = Brushes.Transparent;
            info.Cursor = Cursors.Hand;
            info.ToolTip = Tip(auto
                ? "Недовършени от минали дни – дойдоха сами за днес. Цъкни: автоматично пренасяне да/не."
                : "Автоматичното пренасяне е изключено – недовършените остават в деня си. Цъкни, за да го включиш.");
            info.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                PixelPopup.Show(info, "Автоматично пренасяне", () => new PixelPopup.Entry[]
                {
                    new("Да – недовършените идват за днес", "history", () => { _pet.AutoRoll = true; Refresh(true); }, On: _pet.AutoRoll),
                    new("Не – остават в деня си", "close", () => { _pet.AutoRoll = false; Refresh(true); }, On: !_pet.AutoRoll),
                });
            };
            head.Children.Add(info);
        }
        head.MouseRightButtonUp += (_, e) =>
        {
            PixelPopup.Show(head, null, ColumnEntries(day, tasks), atMouse: true);
            e.Handled = true;
        };
        DockPanel.SetDock(head, Dock.Top);
        dock.Children.Add(head);

        // ── Нова задача: „+ Задача“ отваря картата (име, ден, време, етикет, сложност) ──
        // Днес – „+ Задача“; другите дни – само „+“ (думата се показва, като посочиш).
        var addBox = AddRow("Задача", day, full: isToday);
        _addBoxes[BoxKey(day)] = addBox;
        if (past) addBox.Opacity = 0.7;
        addBox.Margin = new Thickness(0, 0, 0, 6 * U);
        DockPanel.SetDock(addBox, Dock.Top);
        dock.Children.Add(addBox);

        // Как е минал денят (от „Край на деня“) – най-долу.
        if (day is { } d2 && _pet.Save.DayNotes.TryGetValue(d2.ToString("yyyy-MM-dd"), out var note) && note.Length > 0)
        {
            var noteCard = new Border
            {
                Background = Cream,
                BorderBrush = WoodDark,
                BorderThickness = Flat,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8 * U, 5 * U, 8 * U, 6 * U),
                Margin = new Thickness(0, 4 * U, 0, 0),
                Child = new TextBlock { Text = "„" + Shorten(note, 160) + "“", Foreground = Muted, TextWrapping = TextWrapping.Wrap, FontStyle = FontStyles.Italic },
                ToolTip = Tip("Как мина денят: " + note),
            };
            DockPanel.SetDock(noteCard, Dock.Bottom);
            dock.Children.Add(noteCard);
        }

        // ── Картичките: несвършените (в реда си), после свършените ──
        var list = new StackPanel { Background = Brushes.Transparent, MinHeight = 60 * U };
        var openTasks = tasks.Where(t => !t.Done).ToList();
        var doneTasks = tasks.Where(t => t.Done).ToList();
        foreach (var t in openTasks) list.Children.Add(Card(t));
        if (doneTasks.Count > 0)
        {
            if (_pet.Save.PlannerHideDone)
            {
                var more = new TextBlock
                {
                    Text = doneTasks.Count == 1 ? "+ 1 свършена" : $"+ {doneTasks.Count} свършени",
                    Foreground = Muted,
                    Margin = new Thickness(2 * U, 2 * U, 0, 4 * U),
                    Cursor = Cursors.Hand,
                    ToolTip = Tip("Покажи свършените"),
                };
                more.MouseLeftButtonUp += (_, _) =>
                {
                    _pet.Save.PlannerHideDone = false;
                    Refresh(true);
                };
                list.Children.Add(more);
            }
            else foreach (var t in doneTasks) list.Children.Add(Card(t));
        }
        var scroll = new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Brushes.Transparent };
        dock.Children.Add(scroll);

        var column = new Border
        {
            Background = isToday ? Cream : weekend ? PaperDark : Paper,
            BorderBrush = isToday ? Ink : WoodDark,
            BorderThickness = isToday ? Px(1) : Flat,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(7 * U),
            Child = dock,
            Opacity = past ? 0.86 : 1,
        };
        TaskCards.DropTarget(column, list, (id, before) =>
        {
            if (_pet.Save.Tasks.FirstOrDefault(t => t.Id == id) is not { } task) return;
            var open = _pet.TaskState.Open;
            int index = open.FindIndex(t => t.Id == id);
            if (task.Day == day && before == NextOf(open, task)) return;   // на същото място
            _pet.MoveTask(task, day, before);
            if (_filter.Length > 0 && task.Tag.Length == 0) _pet.MarkTask(task, tag: _filter);
            if (task.Day != day)
                ShowToast($"„{Shorten(task.Title, 40)}“ → {DayName(day)}", () => _pet.PutTask(task, index));
        }, scroll, on =>
        {
            // Докато влачиш над деня – рамката му светва в мед (личи къде ще падне).
            column.BorderBrush = on ? Honey : (isToday ? Ink : WoodDark);
            column.BorderThickness = on || isToday ? Px(1) : Flat;
        });
        return column;
    }

    /// <summary>Следващата несвършена задача от същия ден (за „пуснато на същото място“).</summary>
    private static Guid? NextOf(System.Collections.Immutable.ImmutableList<TaskItem> open, TaskItem task)
    {
        int i = open.FindIndex(t => t.Id == task.Id);
        for (int j = i + 1; j < open.Count; j++)
            if (open[j].Day == task.Day) return open[j].Id;
        return null;
    }

    private Border Card(TaskItem t) => TaskCards.Build(_pet, t, new TaskCardOptions
    {
        // Цъкнеш ли задачата – отделно прозорче с всичко за нея (като в Sunsama).
        Select = OpenCard,
        Selected = false,   // избраната се отваря в прозорче – картичката не остава „бяла“
        Unscheduled = _view == View.Today && t.Start == null && !t.Done,
        Description = 60,
        Edit = OpenCard,
        Open = OpenCard,
    });

    private void OpenCard(TaskItem t)
    {
        _selected = t.Id;
        TaskCardWindow.Show(_pet, t, Window.GetWindow(this));
        Refresh(true);
    }

    private static string BoxKey(DateTime? day) => day?.ToString("yyyyMMdd") ?? "someday";

    private string DayName(DateTime? day) =>
        day == null ? "Някога" : day == Today ? "днес" : day == Today.AddDays(1) ? "утре" : day == Today.AddDays(-1) ? "вчера" : day.Value.ToString("dddd, d MMMM", Bg);

    /// <summary>Лентичка „колко време“: свършеното (мед/зелено) и останалото от часовете за деня.</summary>

    /// <summary>Малкото ⇅: подреди задачите от деня (без голямо копче).</summary>
    private Border SortChip(DateTime? day)
    {
        Border sort = null!;
        sort = SmallIcon("sort", () => PixelPopup.Show(sort, "Подреди", SortEntries(day)));
        sort.ToolTip = Tip("Подреди: по приоритет, време, етикет, сложност или име");
        sort.VerticalAlignment = VerticalAlignment.Center;
        return sort;
    }

    /// <summary>„Подреди“: нарежда несвършените задачи от деня (и ги записва в този ред).</summary>
    private Func<IEnumerable<PixelPopup.Entry>> SortEntries(DateTime? day) => () =>
    {
        void Sort(Func<IEnumerable<TaskItem>, IEnumerable<TaskItem>> order)
        {
            var open = _pet.TaskState.Open.Where(t => t.Day == day && !t.Done).ToList();
            _pet.OrderTasks(day, order(open).Select(t => t.Id).ToList());
        }
        return new PixelPopup.Entry[]
        {
            new("По приоритет", "flag_high", () => Sort(l => l.OrderByDescending(t => t.Level))),
            new("Първо кратките", "hourglass", () => Sort(l => l.OrderBy(t => t.Minutes == 0 ? int.MaxValue : t.Minutes))),
            new("Първо дългите", "hourglass", () => Sort(l => l.OrderByDescending(t => t.Minutes))),
            new("По етикет", "pin", () => Sort(l => l.OrderBy(t => t.Tag.Length == 0 ? 1 : 0).ThenBy(t => TaskTags.Find(t.Tag)?.Name ?? ""))),
            new("По сложност", "nut3", () => Sort(l => l.OrderByDescending(t => t.Size))),
            new("По име", "pencil", () => Sort(l => l.OrderBy(t => t.Title, StringComparer.CurrentCultureIgnoreCase))),
        };
    };

    /// <summary>Дясното копче на заглавието на деня: всички недовършени – утре, днес, „Някога“.</summary>
    private Func<IEnumerable<PixelPopup.Entry>> ColumnEntries(DateTime? day, List<TaskItem> tasks) => () =>
    {
        var list = new List<PixelPopup.Entry>();
        if (tasks.Count(t => !t.Done) > 1) list.Add(new("Подреди", "menu", null, Sub: SortEntries(day)));
        if (day is { } from && tasks.Any(t => !t.Done))
        {
            var today = Today;
            if (from != today) list.Add(new("Недовършените – за днес", "sun", () => MoveAll(from, today)));
            list.Add(new("Недовършените – за утре", "arrow_right", () => MoveAll(from, (from < today ? today : from).AddDays(1))));
            list.Add(new("Недовършените – в Чакащи", "backlog", () => MoveAll(from, null)));
        }
        return list;
    };

    private void MoveAll(DateTime from, DateTime? to)
    {
        var open = _pet.TaskState.Open;
        var before = open.Select((t, i) => (t, i)).Where(x => !x.t.Done && x.t.Day == from).ToList();
        if (before.Count == 0) return;
        _pet.MoveUnfinished(from, to);
        ShowToast($"{before.Count} {(before.Count == 1 ? "задача" : "задачи")} → {DayName(to)}", () =>
        {
            foreach (var (t, i) in before) _pet.PutTask(t, i);
        });
    }

    /// <summary>Поле с „+“ и бледа подсказка (точно там, където почва текстът).</summary>
    private static FrameworkElement WithPlus(TextBox box, string hint)
    {
        var grid = new Grid();
        grid.Children.Add(box);
        var plus = IconText("plus", hint);
        plus.IsHitTestVisible = false;
        plus.Opacity = 0.55;
        plus.Margin = new Thickness(box.Padding.Left + box.BorderThickness.Left, 0, 4, 0);
        plus.HorizontalAlignment = HorizontalAlignment.Left;
        plus.ClipToBounds = true;
        foreach (var tb in plus.Children.OfType<TextBlock>()) tb.TextTrimming = TextTrimming.CharacterEllipsis;
        box.TextChanged += (_, _) => plus.Visibility = box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        box.GotKeyboardFocus += (_, _) => plus.Opacity = 0.35;
        box.LostKeyboardFocus += (_, _) => plus.Opacity = 0.55;
        grid.Children.Add(plus);
        return grid;
    }

    private void FocusAdd(DateTime? day)
    {
        if (day is { } d && _view == View.Week) ScrollToDay(d);
        string boxKey = BoxKey(day);
        Dispatcher.BeginInvoke(() =>
        {
            if (_addBoxes.TryGetValue(boxKey, out var box) && box.IsVisible) OpenForm(day, box);
        }, DispatcherPriority.Input);
    }

    /// <summary>„+ Задача“: цъкаш – изскача картата за нова задача (за този ден).</summary>
    /// <summary>„+ Задача“ в „Днес“ (за обиколката).</summary>
    internal Border? TutorialAddRow { get; private set; }

    private Border AddRow(string text, DateTime? day, bool full = true)
    {
        var content = IconText("plus", text, bold: true);
        content.Opacity = 0.7;
        var words = content.Children.OfType<TextBlock>().ToList();
        foreach (var tb in words)
        {
            tb.TextTrimming = TextTrimming.CharacterEllipsis;
            if (!full) tb.Visibility = Visibility.Collapsed;
        }
        var row = new Border
        {
            Background = Brushes.Transparent,
            BorderBrush = WoodDark,
            BorderThickness = Flat,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 5, 8, 6),
            Cursor = Cursors.Hand,
            Child = content,
            ToolTip = Tip("Нова задача: име, ден, време, етикет, сложност (N)"),
        };
        row.MouseEnter += (_, _) =>
        {
            row.Background = Hover;
            content.Opacity = 1;
            foreach (var tb in words) tb.Visibility = Visibility.Visible;
        };
        row.MouseLeave += (_, _) =>
        {
            row.Background = Brushes.Transparent;
            content.Opacity = 0.7;
            if (!full) foreach (var tb in words) tb.Visibility = Visibility.Collapsed;
        };
        TaskCards.Clickable(row, () => OpenForm(day, row));
        if (_view == View.Today) TutorialAddRow = row;
        return row;
    }

    private void OpenForm(DateTime? day, FrameworkElement anchor) =>
        NewTaskWindow.Show(_pet, day, _filter.Length > 0 ? _filter : null, _starOnly, Window.GetWindow(this));

    private void RestoreFocus()
    {
        _refocus = (false, null);
    }

    // ───────────────────────── Отмени ─────────────────────────

    private void OnTaskRemoved(TaskItem task, int index)
    {
        if (_selected == task.Id) _selected = null;
        ShowToast($"Изтрита: „{Shorten(task.Title, 40)}“", () => _pet.PutTask(task, index));
    }

    private void ShowToast(string text, Action undo)
    {
        _undo = undo;
        _toastText.Text = text;
        _toast.Visibility = Visibility.Visible;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    private void HideToast()
    {
        _toastTimer.Stop();
        _toast.Visibility = Visibility.Collapsed;
        _undo = null;
    }

    private void Undo()
    {
        var undo = _undo;
        HideToast();
        undo?.Invoke();
    }

    // ───────────────────────── Клавиши ─────────────────────────

    /// <summary>Клавишите (менюто ги препраща, докато е на „Работа“).</summary>
    internal void OnKey(object sender, KeyEventArgs e)
    {
        var mods = Keyboard.Modifiers;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (mods == ModifierKeys.Control && key == Key.F)
        {
            _search.Focus();
            _search.SelectAll();
            e.Handled = true;
            return;
        }
        if (mods == ModifierKeys.Control && key == Key.Z && Keyboard.FocusedElement is not TextBox)
        {
            if (_undo != null) Undo();
            e.Handled = true;
            return;
        }
        if (mods == ModifierKeys.Control && key is Key.D1 or Key.D2 or Key.D3 or Key.NumPad1 or Key.NumPad2 or Key.NumPad3)
        {
            CommitReview();
            SetView(key is Key.D1 or Key.NumPad1 ? View.Today : key is Key.D2 or Key.NumPad2 ? View.Week : View.Review);
            e.Handled = true;
            return;
        }
        if (Keyboard.FocusedElement is TextBox) return;
        var sel = _selected is { } id ? _pet.Save.Tasks.FirstOrDefault(t => t.Id == id) : null;
        switch (key)
        {
            case Key.Escape:
                if (_sideMode != Side.None) ShowSide(Side.None);
                else if (_query.Length > 0) _search.Clear();
                else if (_filter.Length > 0 || _starOnly) SetFilter("", false);
                break;
            case Key.N when mods == ModifierKeys.None:
                FocusAdd(Today);
                break;
            case Key.T or Key.Home when mods == ModifierKeys.None && _view == View.Week:
                ScrollToDay(Today);
                break;
            case Key.Left when mods == ModifierKeys.None && _view == View.Week:
                ScrollBy(-1);
                break;
            case Key.Right when mods == ModifierKeys.None && _view == View.Week:
                ScrollBy(1);
                break;
            case Key.Left or Key.Right when mods == ModifierKeys.Alt && sel is { Done: false }:
            {
                int dir = key == Key.Left ? -1 : 1;
                var to = (sel.Day ?? Today.AddDays(dir < 0 ? 1 : -1)).AddDays(dir);
                var open = _pet.TaskState.Open;
                int index = open.FindIndex(t => t.Id == sel.Id);
                _pet.MoveTask(sel, to);
                ScrollToDayIfHidden(to);
                ShowToast($"„{Shorten(sel.Title, 40)}“ → {DayName(to)}", () => _pet.PutTask(sel, index));
                break;
            }
            case Key.Space when sel != null && _pet.Save.Tasks.Any(t => t.Id == sel.Id):
                _pet.SetTaskDone(sel, !sel.Done);
                break;
            case Key.Delete when sel != null:
                _pet.DeleteTask(sel);
                break;
            case Key.S when mods == ModifierKeys.None && sel is { Done: false }:
                _pet.MarkTask(sel, star: !sel.Star);
                break;
            case Key.D when mods == ModifierKeys.Control && sel != null:
                _pet.DuplicateTask(sel);
                break;
            case Key.Enter or Key.F2 when sel != null:
                OpenCard(sel);
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    private void ScrollToDayIfHidden(DateTime day)
    {
        double x = IndexOf(day) * Step;
        if (x < _dayScroll.HorizontalOffset || x + ColumnWidth > _dayScroll.HorizontalOffset + _dayScroll.ViewportWidth) ScrollToDay(day);
    }

    // ───────────────────────── Вдясно ─────────────────────────

    private void Select(Guid? id, FocusOn focus = FocusOn.None)
    {
        Commit();
        _selected = id;
        _focusAfter = focus;
        _sideMode = id == null ? (_sideMode == Side.Details ? Side.None : _sideMode) : Side.Details;
        _sideShown = "";
        Refresh();
    }

    private void ShowSide(Side side)
    {
        Commit();
        _sideMode = side;
        SideWidth();
        if (side != Side.Details) _selected = null;
        _sideShown = "";
        Refresh(true);
    }

    /// <summary>Запазва написаното отдясно (преди да се избере друго или да се цъкне копче).</summary>
    private readonly List<Action> _commits = new();
    private bool _sideDirty;

    private void Commit()
    {
        foreach (var c in _commits.ToList()) c();
        _sideDirty = false;
    }

    /// <summary>Копче отдясно: първо запазва написаното.</summary>
    private Action Act(Action a) => () =>
    {
        Commit();
        a();
    };

    private void BuildSide()
    {
        var t = _selected is { } id ? _pet.Save.Tasks.FirstOrDefault(x => x.Id == id) : null;
        if (_sideMode == Side.Details && t == null) _sideMode = Side.None;
        if (_sideMode == Side.None && _view == View.Today)
        {
            // В „Днес“ вдясно – денят по часове (като в Sunsama): задачите се пускат в часовете.
            _todayTimeline ??= new DayTimeline(_pet, OpenCard, headers: false);
            _todayTimeline.SetDays(new[] { _todayDay ?? Today });
            if (_sideShown != "timeline")
            {
                _sideShown = "timeline";
                _commits.Clear();
                var head = new DockPanel { Margin = new Thickness(0, 0, 0, 6 * U) };
                head.Children.Add(new TextBlock { Text = "По часове", FontWeight = FontWeights.Bold, FontSize = Body * 1.15 });
                var box = new DockPanel();
                DockPanel.SetDock(head, Dock.Top);
                box.Children.Add(head);
                (_todayTimeline.Parent as Panel)?.Children.Remove(_todayTimeline);
                box.Children.Add(_todayTimeline);
                _side.Visibility = Visibility.Visible;
                _side.Child = new Border
                {
                    Background = Tile,
                    BorderBrush = WoodDark,
                    BorderThickness = Flat,
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8 * U, 8 * U, 4 * U, 6 * U),
                    Child = box,
                };
            }
            _todayTimeline.Refresh();
            return;
        }
        if (_sideMode == Side.None)
        {
            _selected = null;
            _commits.Clear();
            _side.Visibility = Visibility.Collapsed;
            _side.Child = null;
            _sideShown = "";
            return;
        }
        string sig = _sideMode switch
        {
            Side.Details => t!.Signature + _pet.TaskOnBoard(t) + _pet.InFocus + _pet.FocusTask?.Id,
            _ => string.Join("|", _pet.TaskState.Open.Select(x => x.Signature)) + _pet.Save.PlannerHours + Today.ToString("yyyyMMdd") + _pet.Save.PlannedDay + _pet.Save.ShutdownDay,
        } + _sideMode + _pet.Save.UiTheme + TaskTags.Version;
        if (sig == _sideShown) return;
        // Докато се пише в полетата – не се строи наново (ще се изгуби написаното).
        if (_sideDirty && _side.IsKeyboardFocusWithin) return;
        _sideShown = sig;
        _sideDirty = false;
        _commits.Clear();
        _side.Visibility = Visibility.Visible;
        var content = Details(t!);
        _side.Child = SideCard(content);
    }

    private static Border SideCard(UIElement content) => new()
    {
        Background = Tile,
        BorderBrush = WoodDark,
        BorderThickness = Flat,
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(12 * U, 10 * U, 8 * U, 10 * U),
        Child = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(0, 0, 4 * U, 0) },
    };

    // ───────────────────────── Днес ─────────────────────────

    private string _todayShown = "", _reviewShown = "";

    /// <summary>
    /// „Днес“ (като Today във Sunsama): денят и колко е запълнен, целите за седмицата, покана
    /// за плана (сутрин), за края на деня (вечер) и за прегледа (в края на седмицата), поле за
    /// нова задача, задачите в реда им (влачат се) и свършените днес.
    /// </summary>
    private void BuildToday(TasksState state, DateTime today, bool force)
    {
        var tasks = state.OnDay(today).ToList();
        var monday = WeekOf(today);
        var review = _pet.WeekReviewFor(monday);
        string key = today.ToString("yyyy-MM-dd");
        int hour = DateTime.Now.Hour;
        bool realToday = today == Today;   // покани (план, край на деня…) – само за истинския днешен ден
        string sig = string.Join("|", tasks.Select(t => t.Signature + _pet.TaskOnBoard(t))) + $"#{today:yyyyMMdd}#{hour >= 17}#{hour < 15}"
            + $"#{_pet.Save.PlannedDay}#{_pet.Save.ShutdownDay}#{_pet.Save.PlanSeenDay}#{_pet.Save.ShutdownSeenDay}#{EndFor(today)}#{_pet.UsualEnd}#{_pet.IsRestDay(today)}#{_pet.WorkDoneToday}#{_pet.IsPinned(PinWindow.Tasks)}{_pet.BoardDay:yyyyMMdd}#{DateTime.Now:HH}{DateTime.Now.Minute / 10}#{Today:yyyyMMdd}#{_pet.Save.UiTheme}#{_selected}#{_sideMode}"
            + string.Join(",", review.Goals.Select(g => g.Text + g.Done)) + review.Done + (_pet.InFocus ? _pet.FocusTask?.Id : null)
            + string.Join(",", WeekReviewFor(monday.AddDays(-7)).Done)
            + "#due:" + string.Join(",", BacklogDue(state, today).Select(t => t.Signature));
        if (!force && sig == _todayShown) return;
        _todayShown = sig;
        var p = _todayPanel;
        p.Children.Clear();

        // ── Горе (като в Sunsama): ◀ ▶ [📅 Днес | ↩ Към днес], после денят и кратък ред с часовете ──
        var nav = MakeNav(() => { _todayDay = today.AddDays(-1); Refresh(true); }, () => { _todayDay = today.AddDays(1); Refresh(true); },
            () => { _todayDay = null; Refresh(true); },
            b => DayPicker.Show(b, today, Today, d => { if (d is { } x) { _todayDay = x == Today ? null : x; Refresh(true); } }, allowSomeday: false, title: "Към кой ден?"),
            "Ден назад", "Ден напред");
        nav.Label.Text = NavDayName(today);
        nav.Back.Visibility = realToday ? Visibility.Collapsed : Visibility.Visible;
        nav.Root.Margin = new Thickness(0, 0, 0, 10 * U);
        nav.Root.HorizontalAlignment = HorizontalAlignment.Left;
        p.Children.Add(nav.Root);
        p.Children.Add(new TextBlock
        {
            Text = Capital(today.ToString("dddd, d MMMM", Bg)),
            FontWeight = FontWeights.Bold,
            FontSize = Body * 1.4,
            TextWrapping = TextWrapping.Wrap,
        });
        int done = tasks.Count(t => t.Done), all = tasks.Count;
        int planned = tasks.Sum(t => t.Minutes), doneMinutes = tasks.Where(t => t.Done).Sum(t => t.Minutes);
        int left = tasks.Where(t => !t.Done).Sum(t => t.Minutes);
        var end = EndFor(today);
        // Ред: „Задачи 0/4 · 1ч 30м“; под него – поленцето „🕕 до 18:00 ▾ · остават 3ч“ (или
        // „Почивен ден“, „✓ свърших в 18:20“); още не знае – „До колко работиш обикновено?“.
        int dayMinutes = tasks.Sum(t => t.Minutes);
        TextBlock Line() => new() { Margin = new Thickness(0, 2 * U, 0, 0), Foreground = Muted, TextWrapping = TextWrapping.Wrap };
        var line1 = Line();
        line1.Inlines.Add(new System.Windows.Documents.Run("Задачи "));
        line1.Inlines.Add(new System.Windows.Documents.Run($"{done}/{all}") { FontWeight = FontWeights.Bold, Foreground = Ink });
        if (dayMinutes > 0)
        {
            line1.Inlines.Add(new System.Windows.Documents.Run("  ·  "));
            line1.Inlines.Add(new System.Windows.Documents.Run(TaskCards.Duration(dayMinutes)) { FontWeight = FontWeights.Bold, Foreground = Ink });
        }
        p.Children.Add(line1);
        bool asking = _pet.UsualEnd == null && !_pet.IsRestDay(today);
        {
            // Поленцето: малко, не пречи; цъкнеш ли – менюто.
            // Още не знае обичайния час – същото поленце, но пита; цъкнеш – всички часове.
            bool rest = _pet.IsRestDay(today);
            int? doneAt = realToday ? _pet.WorkDoneToday : null;
            string text = asking ? "До колко работиш?" : rest ? "Почивен ден" : doneAt is { } da ? "свърших в " + Clock(da) : end is { } e0 ? "до " + Clock(e0) + (_pet.DayEndOverride(today) != null ? " (днес)" : "") : "до —";
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var ic = Icon(rest ? "relax" : doneAt != null ? "check" : "clock");
            ic.Margin = new Thickness(0, 0, 5, 0);
            ic.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(ic);
            row.Children.Add(new TextBlock { Text = text + "  ▾", FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap });
            int nowMin = DateTime.Now.Hour * 60 + DateTime.Now.Minute;
            bool over = !rest && doneAt == null && end is { } e1 && realToday && left > Math.Max(0, e1 - nowMin);
            var chip = new Border
            {
                Background = Cream,
                BorderBrush = over ? Bad : asking ? Honey : WoodLight,
                BorderThickness = new Thickness(asking ? 2 : 1),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(7 * U, 2 * U, 8 * U, 3 * U),
                Child = row,
                Cursor = Cursors.Hand,
                ToolTip = Tip(asking ? "Кажи веднъж – после само го сменяш, ако трябва" : over ? "Задачите за днес не се събират до края на работния ден" : "Работният ден – цъкни, за да го смениш"),
            };
            TaskCards.Clickable(chip, () =>
            {
                if (asking)
                    PixelPopup.Show(chip, "До колко работиш обикновено?", () => HalfHours(15 * 60, 22 * 60)
                        .Select(m => new PixelPopup.Entry("до " + Clock(m), null, () => SetUsualEnd(m))));
                else PixelPopup.Show(chip, "Работният ден", () => EndEntries(today));
            });
            var wrap = new WrapPanel { Margin = new Thickness(0, 5 * U, 0, 0) };
            wrap.Children.Add(chip);
            if (!rest && doneAt == null && end is { } e && realToday && e > nowMin)
                wrap.Children.Add(new TextBlock { Text = "остават " + TaskCards.Duration(e - nowMin), Foreground = over ? Bad : Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8 * U, 0, 0, 0) });
            p.Children.Add(wrap);
        }

        // Целите за седмицата (от прегледа) – цъкаш и стават отметнати.
        if (review.Goals.Count > 0)
        {
            var goals = new WrapPanel { Margin = new Thickness(0, 10 * U, 0, 0) };
            goals.Children.Add(new TextBlock { Text = "Целите тази седмица:", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 4) });
            foreach (var g in review.Goals)
            {
                var goal = g;
                var chip = Chip("", () => _pet.SetWeekGoalDone(monday, goal.Id, !goal.Done));
                chip.Child = IconText(goal.Done ? "check" : null, goal.Text, bold: true);
                chip.Margin = new Thickness(0, 0, 6, 4);
                chip.ToolTip = Tip(goal.Done ? "Постигната! (цъкни, за да я върнеш)" : "Цъкни, когато я постигнеш");
                SetOn(chip, goal.Done);
                goals.Children.Add(chip);
            }
            p.Children.Add(goals);
        }

        // Покана: преглед на седмицата (пет–нед или пон за миналата).
        var lastMonday = monday.AddDays(-7);
        bool lastNotReviewed = today.DayOfWeek == DayOfWeek.Monday && !WeekReviewFor(lastMonday).Done
            && Enumerable.Range(0, 7).Any(i => state.OnDay(lastMonday.AddDays(i)).Any());
        // „План за деня“ и „Край на деня“ са махнати (не се показват покани за тях).
        // От Backlog: на които им дойде времето (като в Sunsama – питане дали да ги вземем).
        var dueTasks = BacklogDue(state, today);
        if (realToday && dueTasks.Count > 0) p.Children.Add(DueCard(dueTasks, today));
        if (realToday && lastNotReviewed)
            p.Children.Add(Invite("book", "Нова седмица! Да прегледаме миналата и да сложим цели за тази.", "Преглед", () =>
            {
                _reviewWeek = lastMonday;
                SetView(View.Review);
            }));
        else if (today.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday or DayOfWeek.Sunday && !review.Done && hour >= 12)
            p.Children.Add(Invite("book", "Седмицата свършва. Как мина? Какви са целите за следващата?", "Преглед на седмицата", () =>
            {
                _reviewWeek = monday;
                SetView(View.Review);
            }));

        // Нова задача за днес: картата с всичко (най-отгоре в деня).
        var addBox = AddRow("Задача", today);
        _addBoxes[BoxKey(today)] = addBox;
        // „+ Задача“, до нея ⇅ (подреди) и колко време са всички (като в Sunsama).
        var addRow = new DockPanel { Margin = new Thickness(0, 14 * U, 0, 8 * U) };
        int openMinutes = tasks.Where(t => !t.Done).Sum(t => t.Minutes);
        if (openMinutes > 0)
        {
            var total = new Border
            {
                Background = PaperDark,
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6, 1, 6, 2),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8 * U, 0, 0, 0),
                Child = new TextBlock { Text = $"{openMinutes / 60}:{openMinutes % 60:00}", FontWeight = FontWeights.Bold, Foreground = Ink },
                ToolTip = Tip($"Остават ~{TaskCards.Duration(openMinutes)} работа"),
            };
            DockPanel.SetDock(total, Dock.Right);
            addRow.Children.Add(total);
        }
        // „Табло“: закача задачите на този ден в отделното табло (едно е – друг ден го сменя).
        bool pinned = _pet.IsPinned(PinWindow.Tasks) && _pet.BoardDay == today;
        var board = SmallIcon(pinned ? "unpin" : "pin", () => { _pet.PinDay(today); Refresh(true); });
        board.ToolTip = Tip(pinned ? "Откачи таблото" : "Закачи задачите на деня в табло");
        SetOn(board, pinned);
        board.Margin = new Thickness(8 * U, 0, 0, 0);
        board.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(board, Dock.Right);
        addRow.Children.Add(board);
        if (tasks.Count(t => !t.Done) > 1)
        {
            var sort = SortChip(today);
            sort.Margin = new Thickness(8 * U, 0, 0, 0);
            DockPanel.SetDock(sort, Dock.Right);
            addRow.Children.Add(sort);
        }
        addRow.Children.Add(addBox);
        p.Children.Add(addRow);

        // Задачите: несвършените в реда си (влачат се), после свършените.
        var open = tasks.Where(t => !t.Done).ToList();
        var list = new StackPanel { Background = Brushes.Transparent, MinHeight = 80 * U };
        if (open.Count == 0 && done > 0)
            list.Children.Add(new TextBlock
            {
                Text = "Всичко за днес е свършено!",
                Foreground = Muted,
                Margin = new Thickness(2 * U, 4 * U, 0, 8 * U),
            });
        foreach (var t in open) list.Children.Add(Card(t));
        var listBox = new Border { Background = Brushes.Transparent, Child = list };
        TaskCards.DropTarget(listBox, list, (id, before) =>
        {
            if (_pet.Save.Tasks.FirstOrDefault(t => t.Id == id) is not { } task) return;
            var opened = _pet.TaskState.Open;
            int index = opened.FindIndex(t => t.Id == id);
            if (task.Day == today && before == NextOf(opened, task)) return;
            _pet.MoveTask(task, today, before);
            if (task.Day != today) ShowToast($"„{Shorten(task.Title, 40)}“ → днес", () => _pet.PutTask(task, index));
        }, _todayView);
        p.Children.Add(listBox);

        var doneTasks = tasks.Where(t => t.Done).ToList();
        if (doneTasks.Count > 0)
        {
            p.Children.Add(new TextBlock
            {
                Text = doneTasks.Count == 1 ? "Свършена днес · 1" : $"Свършени днес · {doneTasks.Count}",
                Foreground = Muted,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(2 * U, 12 * U, 0, 6 * U),
            });
            foreach (var t in doneTasks) p.Children.Add(Card(t));
        }
    }

    /// <summary>Покана (план, край на деня, преглед): медена картичка с текст и копче.</summary>
    private FrameworkElement Invite(string icon, string text, string button, Action click, Action? dismiss = null)
    {
        // Текстът отгоре (пренася се), копчетата – под него вдясно: в тясна колона не се смачква.
        var row = new StackPanel();
        row.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6 * U, 0, 0) };
        var go = Button(button, click, primary: true, icon: icon);
        go.Margin = new Thickness(0);
        go.VerticalAlignment = VerticalAlignment.Center;
        buttons.Children.Add(go);
        if (dismiss != null)
        {
            var x = SmallIcon("close", dismiss);
            x.ToolTip = Tip("Не днес");
            x.Tag = "dismiss";
            x.VerticalAlignment = VerticalAlignment.Center;
            x.Margin = new Thickness(6 * U, 0, 0, 0);
            buttons.Children.Add(x);
        }
        row.Children.Add(buttons);
        return new Border
        {
            Background = Cream,
            BorderBrush = Honey,
            BorderThickness = Px(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12 * U, 8 * U, 8 * U, 8 * U),
            Margin = new Thickness(0, 10 * U, 0, 0),
            Child = row,
            Tag = "invite:" + button,
        };
    }

    private WeekReview WeekReviewFor(DateTime monday) => _pet.WeekReviewFor(monday);

    // ── Сега: работата по задачите (вдясно в „Днес“) ──

    private TextBlock? _clockText, _clockWhat;

    /// <summary>Изгледът е сменен (и отвътре – от покана, от прегледа…): менюто сменя страницата горе и вляво.</summary>
    public event Action<View>? ViewChanged;

    /// <summary>„Към Фокус“ – менюто отваря страницата „Фокус“.</summary>
    public event Action? OpenFocus;

    private string FocusSignature()
    {
        var now = DateTime.Now;
        return $"hours#{now:yyyyMMddHH}{now.Minute / 5}#" + string.Join("|", _pet.OrderedTasks().Select(t => t.Signature))
            + $"#{_pet.Save.UiTheme}#{TaskTags.Version}#{_pet.Save.PlannerHours}";
    }

    /// <summary>
    /// „Днес по часове“: несвършените задачи, наредени една след друга от сега нататък
    /// (по колкото време са казали; без време – по 30 мин), върху часовете на деня. Червена
    /// черта – сега. Цъкнеш ли задача – картата ѝ.
    /// </summary>
    private UIElement FocusPanel()
    {
        var p = new StackPanel();
        p.Children.Add(new TextBlock { Text = "Днес по часове", FontWeight = FontWeights.Bold, FontSize = Body * 1.15, Margin = new Thickness(0, 0, 0, 8 * U) });
        var now = DateTime.Now;
        var tasks = _pet.OrderedTasks();
        // Почва се от следващите 15 минути.
        var start = now.Date.AddHours(now.Hour).AddMinutes((now.Minute / 15 + 1) * 15);
        var blocks = new List<(TaskItem Task, DateTime From, DateTime To)>();
        var at = start;
        foreach (var t in tasks)
        {
            int minutes = t.Minutes > 0 ? t.Minutes : 30;
            blocks.Add((t, at, at.AddMinutes(minutes)));
            at = at.AddMinutes(minutes);
        }
        int firstHour = now.Hour;
        var end = blocks.Count > 0 ? blocks[^1].To : now.AddHours(3);
        int lastHour = Math.Max(Math.Min(24, end.Minute > 0 ? end.Hour + 1 : end.Hour) + (end.Date > now.Date ? 24 : 0), firstHour + 4);
        lastHour = Math.Min(lastHour, firstHour + 30);
        double hourH = 46 * U, labelW = 40 * U;
        var canvas = new Canvas { Height = (lastHour - firstHour) * hourH + 6 * U, ClipToBounds = false };
        double Y(DateTime d) => ((d - now.Date).TotalHours - firstHour) * hourH;
        for (int h = firstHour; h <= lastHour; h++)
        {
            var line = new Border { Height = 1, Background = WoodLight, Width = 2000 };
            Canvas.SetLeft(line, labelW);
            Canvas.SetTop(line, (h - firstHour) * hourH);
            canvas.Children.Add(line);
            var label = new TextBlock { Text = $"{h % 24}:00", Foreground = Muted };
            Canvas.SetLeft(label, 0);
            Canvas.SetTop(label, (h - firstHour) * hourH - 7 * U);
            canvas.Children.Add(label);
        }
        foreach (var (t, from, to) in blocks)
        {
            var tag = TaskTags.Find(t.Tag);
            double top = Y(from) + 1, height = Math.Max(16 * U, Y(to) - Y(from) - 2);
            var text = new StackPanel();
            text.Children.Add(new TextBlock { Text = t.Title, FontWeight = FontWeights.Bold, TextTrimming = TextTrimming.CharacterEllipsis });
            if (height > 30 * U)
                text.Children.Add(new TextBlock { Text = $"{from:H:mm}–{to:H:mm}" + (t.Minutes > 0 ? "" : " (~30м)"), Foreground = Muted, TextTrimming = TextTrimming.CharacterEllipsis });
            var block = new Border
            {
                Height = height,
                Background = tag != null ? TaskTags.SoftBrushOf(t.Tag) : Cream,
                BorderBrush = tag != null ? TaskTags.BrushOf(t.Tag) : WoodDark,
                BorderThickness = new Thickness(Math.Max(2, P), 1, 1, 1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6 * U, 2 * U, 4 * U, 2 * U),
                Child = text,
                ClipToBounds = true,
                Cursor = Cursors.Hand,
                ToolTip = Tip($"{t.Title}\n{from:H:mm}–{to:H:mm}"),
            };
            var task = t;
            TaskCards.Clickable(block, () => OpenCard(task));
            Canvas.SetLeft(block, labelW + 4 * U);
            Canvas.SetTop(block, top);
            canvas.Children.Add(block);
        }
        // Сега – червена черта.
        var nowLine = new Border { Height = Math.Max(2, P / 2), Background = Bad, Width = 2000 };
        Canvas.SetLeft(nowLine, labelW - 4 * U);
        Canvas.SetTop(nowLine, Y(now));
        canvas.Children.Add(nowLine);
        // Широчината на задачите – колкото е панелът.
        canvas.SizeChanged += (_, _) =>
        {
            foreach (var b in canvas.Children.OfType<Border>().Where(b => b.Child != null))
                b.Width = Math.Max(40, canvas.ActualWidth - labelW - 6 * U);
        };
        var host = new Border { ClipToBounds = true, Child = canvas, Margin = new Thickness(0, 8 * U, 0, 0) };
        p.Children.Add(host);
        if (blocks.Count == 0)
            p.Children.Add(MenuWindow.MutedText("Няма задачи за днес."));
        else
        {
            var over = blocks[^1].To;
            string tail = over.Date > now.Date ? "след полунощ!" : over.ToString("H:mm");
            var sum = MenuWindow.MutedText($"Почнеш ли сега – всичко е свършено към {tail}");
            sum.Margin = new Thickness(0, 6 * U, 0, 0);
            p.Children.Add(sum);
        }
        _clockText = new TextBlock();   // UpdateClock: пречертава се, когато мине време
        _clockWhat = new TextBlock();
        return p;
    }

    private void UpdateClock()
    {
        if (_clockText == null || _clockWhat == null || !IsVisible) return;
        if (_view == View.Today && _sideMode == Side.None && FocusSignature() != _sideShown)
        {
            BuildSide();
            return;
        }
        static string Mmss(TimeSpan t) => $"{(int)t.TotalMinutes:00}:{t.Seconds:00}";
        if (_pet.PomodoroFocus)
        {
            _clockText.Text = Mmss(_pet.PomodoroLeft);
            _clockWhat.Text = "мечо-доро" + (_pet.Save.PomodoroSeries > 1 ? $" · {_pet.PomodoroNumber} от {_pet.Save.PomodoroSeries}" : "");
        }
        else if (_pet.Save.PomodoroPhase == PomodoroPhase.Break)
        {
            _clockText.Text = Mmss(_pet.PomodoroLeft);
            _clockWhat.Text = "почивка";
        }
        else if (_pet.TimerRunning)
        {
            _clockText.Text = Mmss(_pet.TimerLeft);
            _clockWhat.Text = _pet.WorkTimer ? "работен таймер" : "таймер";
        }
        else
        {
            _clockText.Text = $"{(int)_pet.FocusLength.TotalMinutes}:00";
            _clockWhat.Text = "готов за работа";
        }
    }

    // ───────────────────────── Backlog ─────────────────────────

    /// <summary>Задачите от Backlog, на които им дойде времето („следващата седмица“ вече е тази…).</summary>
    private List<TaskItem> BacklogDue(TasksState state, DateTime today) =>
        state.Someday.Where(t => t.BucketDue is { } due && due <= today).ToList();

    /// <summary>
    /// Backlog (като в Sunsama): групи – следващата седмица, месец, тримесечие, година, някога,
    /// никога. Всяка група с „+“ (нова задача направо в нея); задачите се влачат между групите
    /// и в дните. Отгоре – тези, на които им дойде времето.
    /// </summary>
    private void BuildBacklog(TasksState state, DateTime today, bool force)
    {
        var someday = state.Someday.ToList();
        string sig = $"{today:yyyyMMdd}#{_pet.Save.UiTheme}#{TaskTags.Version}#" + string.Join("|", someday.Select(t => t.Signature + _pet.TaskOnBoard(t)));
        if (!force && sig == _backlogShown) return;
        _backlogShown = sig;
        var p = _backlogPanel;
        p.Children.Clear();
        var due = BacklogDue(state, today);
        if (due.Count > 0) p.Children.Add(DueCard(due, today));
        foreach (var b in TaskCards.Buckets)
        {
            int bucket = b.Bucket;
            var tasks = someday.Where(t => t.Bucket == bucket).ToList();
            var head = new DockPanel();
            var add = Small("", () => NewTaskWindow.Show(_pet, null, near: Window.GetWindow(this), bucket: bucket));
            add.Child = Icon("plus");
            add.ToolTip = Tip("Нова задача тук");
            add.Margin = new Thickness(0, 3 * U, 4 * U, 3 * U);
            DockPanel.SetDock(add, Dock.Right);
            head.Children.Add(add);
            var title = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8 * U, 4 * U, 0, 4 * U) };
            title.Children.Add(TaskCards.Badge(b.Letter, b.Color));
            title.Children.Add(new TextBlock { Text = b.Name, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            if (tasks.Count > 0) title.Children.Add(new TextBlock { Text = "  " + tasks.Count, Foreground = Muted, VerticalAlignment = VerticalAlignment.Center });
            head.Children.Add(title);
            // Цялата лента е оцветена (и отстъпите), не само средата.
            var section = new Border
            {
                Background = PaperDark,
                CornerRadius = new CornerRadius(4),
                BorderBrush = WoodLight,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 10 * U, 0, 4 * U),
                Child = head,
            };
            var list = new StackPanel { Background = Brushes.Transparent, MinHeight = 18 * U, Margin = new Thickness(6 * U, 0, 0, 0) };
            foreach (var t in tasks) list.Children.Add(Card(t));
            // Цялата група (заглавието, списъкът и празното под него) приема пуснатата задача –
            // не е нужно да уцелиш точно списъка.
            var group = new StackPanel { Background = Brushes.Transparent };
            group.Children.Add(section);
            group.Children.Add(list);
            var box = new Border { Background = Brushes.Transparent, Child = group, Padding = new Thickness(0, 0, 0, 6 * U) };
            TaskCards.DropTarget(box, list, (id, before) =>
            {
                if (_pet.Save.Tasks.FirstOrDefault(t => t.Id == id) is not { } task) return;
                if (task.Day != null || task.Bucket != bucket) _pet.SetTaskBucket(task, bucket);
            });
            p.Children.Add(box);
        }
    }

    /// <summary>„Дойде времето“: задачите от Backlog, които са за тази седмица/месец… – за днес, отложи или махни.</summary>
    private FrameworkElement DueCard(List<TaskItem> due, DateTime today)
    {
        var p = new StackPanel();
        var head = new DockPanel();
        var all = Button("Всички за днес", Act(() => { foreach (var t in due) _pet.MoveTask(t, today); }), primary: true, icon: "sun");
        all.VerticalAlignment = VerticalAlignment.Center;
        all.Margin = new Thickness(8 * U, 0, 0, 0);
        DockPanel.SetDock(all, Dock.Right);
        head.Children.Add(all);
        head.Children.Add(new TextBlock
        {
            Text = due.Count == 1 ? "Дойде времето на 1 чакаща задача." : $"Дойде времето на {due.Count} чакащи задачи.",
            FontWeight = FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        });
        p.Children.Add(head);
        foreach (var t in due.Take(8))
        {
            var b = TaskCards.BucketOf(t.Bucket);
            p.Children.Add(MiniRow(t,
                ("Днес", "Сложи я за днес", () => _pet.MoveTask(t, today)),
                ("Утре", "За утре", () => _pet.MoveTask(t, today.AddDays(1))),
                ("Отложи", $"Пак „{b.Name}“ – от днес", () => _pet.SetTaskBucket(t, t.Bucket)),
                ("Някога", "В „Някога“", () => _pet.SetTaskBucket(t, TaskItem.SomedayBucket))));
        }
        return new Border
        {
            Background = Cream,
            BorderBrush = Honey,
            BorderThickness = Px(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12 * U, 8 * U, 8 * U, 8 * U),
            Margin = new Thickness(0, 6 * U, 0, 4 * U),
            Child = p,
            Tag = "due",
        };
    }

    // ───────────────────────── Календар ─────────────────────────

    private DayTimeline? _todayTimeline, _weekTimeline;
    private DateTime? _calDate;
    private int _calMode = 1;   // 0 – ден, 1 – седмица, 2 – месец
    private NavPill? _calNav;
    private readonly List<(int Mode, Border Chip)> _calModeChips = new();
    private Border _calBody = new();
    private Grid? _monthGrid;
    private string _monthShown = "";

    /// <summary>
    /// „Календар“: Ден, Седмица или Месец. В деня и седмицата – по часове (задачите с час се
    /// влачат, без час – бледо предложено време); в месеца – задачите по дни (влачат се от ден в
    /// ден, цъкнеш деня – изгледът „Ден“). Тук ще се виждат и срещите от Google Календар.
    /// </summary>
    private void BuildCalendar(TasksState state, DateTime today, bool force)
    {
        var at = _calDate ?? today;
        if (_weekTimeline == null)
        {
            _weekTimeline = new DayTimeline(_pet, OpenCard, headers: true);
            var bar = new WrapPanel { Margin = new Thickness(0, 0, 0, 8 * U) };
            foreach (var (mode, name) in new[] { (0, "Ден"), (1, "Седмица"), (2, "Месец") })
            {
                int m = mode;
                var chip = HeaderChip(name, () => { _calMode = m; Refresh(true); }, name);
                chip.Margin = new Thickness(0, 0, 4, 4);
                _calModeChips.Add((m, chip));
                bar.Children.Add(chip);
            }
            _calNav = MakeNav(() => { _calDate = CalStep(_calDate ?? Today, -1); Refresh(true); },
                () => { _calDate = CalStep(_calDate ?? Today, 1); Refresh(true); },
                () => { _calDate = null; Refresh(true); },
                b => DayPicker.Show(b, _calDate ?? Today, Today, d => { if (d is { } x) { _calDate = x; Refresh(true); } }, allowSomeday: false, title: "Към кой ден?"),
                "Назад", "Напред");
            var nav = (FrameworkElement)_calNav.Root;
            nav.Margin = new Thickness(12 * U, 0, 0, 4);
            nav.ToolTip = Tip("Влачи задачите в часовете; цъкни празно място – нова задача там.");
            bar.Children.Add(nav);
            var root = new DockPanel();
            DockPanel.SetDock(bar, Dock.Top);
            root.Children.Add(bar);
            _calBody = new Border
            {
                BorderBrush = WoodLight,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(4 * U, 6 * U, 4 * U, 2 * U),
            };
            root.Children.Add(_calBody);
            _calendarHost.Child = root;
            // Месец: колелцето на мишката сменя месеците.
            _calBody.PreviewMouseWheel += (_, e) =>
            {
                if (_calMode != 2) return;
                e.Handled = true;
                _calDate = CalStep(_calDate ?? Today, e.Delta < 0 ? 1 : -1);
                Refresh(true);
            };
        }
        foreach (var (m, chip) in _calModeChips) SetOn(chip, m == _calMode);

        if (_calMode == 2)
        {
            var first = new DateTime(at.Year, at.Month, 1);
            string name = first.ToString("MMMM yyyy", Bg);
            SetCalNav(char.ToUpper(name[0], Bg) + name[1..], today.Year == first.Year && today.Month == first.Month);
            BuildMonth(state, first, today, force);
            if (_calBody.Child != _monthGrid) _calBody.Child = _monthGrid;
            return;
        }
        if (_calBody.Child != _weekTimeline) _calBody.Child = _weekTimeline;
        if (_calMode == 0)
        {
            SetCalNav(NavDayName(at), at == today);
            _weekTimeline.SetDays(new[] { at });
        }
        else
        {
            var monday = WeekOf(at);
            var sunday = monday.AddDays(6);
            SetCalNav(monday == WeekOf(today) ? "Тази седмица" : monday.Month == sunday.Month
                    ? $"{monday.Day} – {sunday.ToString("d MMMM", Bg)}"
                    : $"{monday.ToString("d MMM", Bg)} – {sunday.ToString("d MMM", Bg)}", monday == WeekOf(today));
            _weekTimeline.SetDays(Enumerable.Range(0, 7).Select(i => monday.AddDays(i)));
        }
        _weekTimeline.Refresh(force);
    }

    private void SetCalNav(string label, bool isToday)
    {
        if (_calNav == null) return;
        _calNav.Label.Text = label;
        _calNav.Back.Visibility = isToday ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Напред/назад с ден, седмица или месец (според изгледа).</summary>
    private DateTime CalStep(DateTime from, int dir) => _calMode switch
    {
        0 => from.AddDays(dir),
        1 => from.AddDays(7 * dir),
        _ => new DateTime(from.Year, from.Month, 1).AddMonths(dir),
    };

    /// <summary>„още N“ в месеца: малко прозорче с всички задачи за деня (превърта се); цъкнеш ли – задачата.</summary>
    private void ShowDayTasks(FrameworkElement anchor, DateTime day, List<TaskItem> tasks)
    {
        PixelPopup.ShowCustom(anchor, close =>
        {
            var root = new StackPanel { Width = 240 * U, Margin = new Thickness(6 * U, 4 * U, 6 * U, 4 * U) };
            string name = day.ToString("dddd, d MMMM", Bg);
            root.Children.Add(new TextBlock { Text = char.ToUpper(name[0], Bg) + name[1..], FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 6 * U) });
            var list = new StackPanel();
            foreach (var t in tasks)
            {
                var task = t;
                var tag = TaskTags.Find(t.Tag);
                var row = new Border
                {
                    Background = tag != null ? TaskTags.SoftBrushOf(t.Tag) : Cream,
                    BorderBrush = tag != null ? TaskTags.BrushOf(t.Tag) : WoodLight,
                    BorderThickness = new Thickness(Math.Max(2, P / 2), 0, 0, 0),
                    CornerRadius = new CornerRadius(2),
                    Margin = new Thickness(0, 0, 0, 3 * U),
                    Padding = new Thickness(5 * U, 2 * U, 4 * U, 3 * U),
                    Cursor = Cursors.Hand,
                    Child = new TextBlock { Text = (t.Start is { } st ? st.ToString("H:mm") + "  " : "") + t.Title, TextTrimming = TextTrimming.CharacterEllipsis },
                    ToolTip = Tip(t.Title),
                };
                TaskCards.Clickable(row, () => { close(); OpenCard(task); });
                list.Children.Add(row);
            }
            root.Children.Add(new ScrollViewer { Content = list, MaxHeight = 260 * U, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            var add = Small("+ Задача", () => { close(); NewTaskWindow.Show(_pet, day, near: Window.GetWindow(this)); });
            add.Margin = new Thickness(0, 6 * U, 0, 0);
            add.HorizontalAlignment = HorizontalAlignment.Left;
            root.Children.Add(add);
            return root;
        });
    }

    /// <summary>Месецът: 7 колони (от понеделник), във всеки ден – задачите му (до 4 и „+още“).</summary>
    private void BuildMonth(TasksState state, DateTime first, DateTime today, bool force)
    {
        int shift = ((int)first.DayOfWeek + 6) % 7;
        var start = first.AddDays(-shift);
        int weeks = (int)Math.Ceiling((shift + DateTime.DaysInMonth(first.Year, first.Month)) / 7.0);
        var days = Enumerable.Range(0, weeks * 7).Select(i => start.AddDays(i)).ToList();
        string sig = $"{first:yyyyMM}#{today:yyyyMMdd}#{_pet.Save.UiTheme}#{TaskTags.Version}#" + string.Join("|", days.SelectMany(d => state.OnDay(d)).Select(t => t.Signature));
        if (!force && sig == _monthShown && _monthGrid != null) return;
        _monthShown = sig;
        var grid = new Grid();
        for (int c = 0; c < 7; c++) grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int r = 0; r < weeks; r++) grid.RowDefinitions.Add(new RowDefinition());
        string[] names = { "Пон", "Вт", "Ср", "Чет", "Пет", "Съб", "Нед" };
        for (int c = 0; c < 7; c++)
        {
            var h = new TextBlock { Text = names[c], Foreground = Muted, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 4 * U) };
            Grid.SetColumn(h, c);
            grid.Children.Add(h);
        }
        for (int i = 0; i < days.Count; i++)
        {
            var d = days[i];
            var tasks = state.OnDay(d).Where(t => !t.Done).ToList();   // свършените не се виждат в календара
            bool inMonth = d.Month == first.Month;
            var cell = new DockPanel { Background = Brushes.Transparent };
            var num = new TextBlock
            {
                Text = d.Day.ToString(),
                FontWeight = d == today ? FontWeights.Bold : FontWeights.Normal,
                Foreground = inMonth ? Ink : Muted,
                Margin = new Thickness(4 * U, 2 * U, 0, 2 * U),
            };
            var numBox = new Border
            {
                Child = num,
                Background = d == today ? Honey : Brushes.Transparent,
                CornerRadius = new CornerRadius(3),
                HorizontalAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(0, 0, 4 * U, 0),
            };
            DockPanel.SetDock(numBox, Dock.Top);
            cell.Children.Add(numBox);
            // Цъкнеш ли числото – денят по часове.
            var dayOf = d;
            numBox.Cursor = Cursors.Hand;
            numBox.ToolTip = Tip("Денят по часове");
            numBox.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                _calDate = dayOf;
                _calMode = 0;
                Refresh(true);
            };
            var list = new StackPanel();
            const int fit = 3;
            int visible = tasks.Count > fit ? fit - 1 : fit;   // „още N“ заема един ред
            foreach (var t in tasks.Take(visible))
            {
                var task = t;
                var tag = TaskTags.Find(t.Tag);
                var chip = new Border
                {
                    Background = tag != null ? TaskTags.SoftBrushOf(t.Tag) : Cream,
                    BorderBrush = tag != null ? TaskTags.BrushOf(t.Tag) : WoodLight,
                    BorderThickness = new Thickness(Math.Max(2, P / 2), 0, 0, 0),
                    CornerRadius = new CornerRadius(2),
                    Margin = new Thickness(2 * U, 0, 2 * U, 2 * U),
                    Padding = new Thickness(3 * U, 0, 2 * U, 1),
                    Opacity = t.Done ? 0.5 : 1,
                    Cursor = Cursors.Hand,
                    Tag = t,
                    Child = new TextBlock
                    {
                        Text = (t.Start is { } st ? st.ToString("H:mm") + " " : "") + t.Title,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        TextDecorations = t.Done ? TextDecorations.Strikethrough : null,
                        FontSize = Body * 0.9,
                    },
                    ToolTip = Tip(t.Title),
                };
                TaskCards.Clickable(chip, () => OpenCard(task));
                list.Children.Add(chip);
            }
            if (tasks.Count > visible)
            {
                var more = new TextBlock
                {
                    Text = $"още {tasks.Count - visible}",
                    Foreground = Ink,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(4 * U, 0, 0, 2 * U),
                    FontSize = Body * 0.9,
                    Cursor = Cursors.Hand,
                    Background = Brushes.Transparent,
                    ToolTip = Tip("Всички задачи за деня"),
                };
                var all = tasks;
                var dd = d;
                more.MouseLeftButtonUp += (_, e) =>
                {
                    e.Handled = true;
                    ShowDayTasks(more, dd, all);
                };
                list.Children.Add(more);
            }
            cell.Children.Add(list);
            var day = d;
            // Празното място в деня: курсор с „+“ и едно цъкане – нова задача за този ден.
            cell.MouseLeftButtonUp += (_, e) =>
            {
                if (e.Handled) return;
                e.Handled = true;
                NewTaskWindow.Show(_pet, day, near: Window.GetWindow(this));
            };
            cell.Cursor = PlusCursor.Get();
            var box = new Border
            {
                BorderBrush = WoodLight,
                BorderThickness = new Thickness(i % 7 == 0 ? 0 : 1, 1, 0, 0),
                Background = inMonth ? Brushes.Transparent : PaperDark,
                MinHeight = 64 * U,
                ClipToBounds = true,
                Child = cell,
            };
            // Задача, пусната в ден – става за този ден.
            TaskCards.DropTarget(box, list, (id, before) =>
            {
                if (_pet.Save.Tasks.FirstOrDefault(t => t.Id == id) is { } task && task.Day != day) _pet.MoveTask(task, day);
            });
            Grid.SetColumn(box, i % 7);
            Grid.SetRow(box, i / 7 + 1);
            grid.Children.Add(box);
        }
        _monthGrid = grid;
        _calBody.Child = grid;
    }

    // ───────────────────────── Преглед на седмицата ─────────────────────────

    private DateTime? _reviewWeek;

    /// <summary>Коя седмица се преглежда: избраната или (по подразбиране) тази.</summary>
    private DateTime ReviewMonday => _reviewWeek ?? WeekOf(Today);

    /// <summary>
    /// Прегледът (weekly review като в Sunsama): колко е свършено и колко е работено, по дни и
    /// по етикети, кои задачи все се пренасят, какво мина добре и какво да е различно, целите
    /// на тази седмица (постигнати ли са) и целите за следващата.
    /// </summary>
    private void BuildReview(TasksState state, DateTime today, bool force)
    {
        var monday = ReviewMonday;
        var days = Enumerable.Range(0, 7).Select(i => monday.AddDays(i)).ToList();
        var tasks = days.SelectMany(d => state.OnDay(d)).ToList();
        var review = _pet.WeekReviewFor(monday);
        var nextReview = _pet.WeekReviewFor(monday.AddDays(7));
        string sig = $"{monday:yyyyMMdd}#{today:yyyyMMdd}#" + string.Join("|", tasks.Select(t => t.Signature))
            + string.Join(",", review.Goals.Select(g => g.Text + g.Done)) + review.Done + "#" + string.Join(",", nextReview.Goals.Select(g => g.Text + g.Done))
            + "#" + string.Join(",", state.Open.Where(t => !t.Done && t.Rolled >= 2).Select(t => t.Signature)) + _pet.Save.UiTheme + "#step" + _reviewStep;
        if (!force && sig == _reviewShown) return;
        if (_reviewDirty && _reviewView.IsKeyboardFocusWithin) return;   // пише се – не се строи наново
        _reviewShown = sig;
        _reviewDirty = false;
        var p = _reviewPanel;
        p.Children.Clear();

        // Коя седмица.
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 10 * U) };
        var nav = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var prev = SmallIcon("nav_prev", () => { CommitReview(); _reviewWeek = monday.AddDays(-7); _reviewStep = 0; Refresh(true); });
        prev.ToolTip = Tip("Предишната седмица");
        var next = SmallIcon("nav_next", () => { CommitReview(); _reviewWeek = monday.AddDays(7); _reviewStep = 0; Refresh(true); });
        next.ToolTip = Tip("Следващата седмица");
        next.Visibility = monday.AddDays(7) > WeekOf(today) ? Visibility.Collapsed : Visibility.Visible;
        nav.Children.Add(prev);
        nav.Children.Add(next);
        DockPanel.SetDock(nav, Dock.Right);
        head.Children.Add(nav);
        string which = monday == WeekOf(today) ? "Тази седмица" : monday == WeekOf(today).AddDays(-7) ? "Миналата седмица" : "Седмицата";
        var end = monday.AddDays(6);
        var titleBox = new StackPanel();
        titleBox.Children.Add(new TextBlock { Text = which, FontWeight = FontWeights.Bold, FontSize = Body * 1.4 });
        titleBox.Children.Add(new TextBlock
        {
            Text = monday.Month == end.Month ? $"{monday.Day} – {end.ToString("d MMMM yyyy", Bg)}" : $"{monday.ToString("d MMMM", Bg)} – {end.ToString("d MMMM yyyy", Bg)}",
            Foreground = Muted,
        });
        head.Children.Add(titleBox);
        p.Children.Add(head);

        // Като в Sunsama: три стъпки – какво свърши, как мина, целите за следващата; после „Приключи“.
        _commitReview = null;
        string[] stepNames = { "1. Какво свърших", "2. Как мина седмицата", "3. Следващата седмица", "Минали седмици" };
        var stepRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 10 * U) };
        for (int i = 0; i < stepNames.Length; i++)
        {
            int si = i;
            var chip = Small(stepNames[i], () => { CommitReview(); _reviewStep = si; Refresh(true); });
            SetOn(chip, _reviewStep == i);
            stepRow.Children.Add(chip);
        }
        p.Children.Add(stepRow);

        if (_reviewStep == 0)
        {
        // Числата.
        var done = tasks.Where(t => t.Done).ToList();
        int spent = tasks.Sum(t => t.Spent);
        int rolled = tasks.Count(t => t.Rolled > 0);
        var best = days.Select(d => (Day: d, Done: state.OnDay(d).Count(t => t.Done))).OrderByDescending(x => x.Done).First();
        var tiles = new UniformGrid { Rows = 1, Margin = new Thickness(0, 0, 0, 12 * U) };
        tiles.Children.Add(StatTile("check", $"{done.Count}", tasks.Count == 0 ? "свършени" : $"свършени от {tasks.Count}"));
        tiles.Children.Add(StatTile("hourglass", spent > 0 ? TaskCards.Duration(spent) : "—", "работено по задачи"));
        tiles.Children.Add(StatTile("star", best.Done > 0 ? Capital(best.Day.ToString("ddd", Bg)) : "—", best.Done > 0 ? $"най-много: {best.Done}" : "най-добрият ден"));
        tiles.Children.Add(StatTile("history", $"{rolled}", "пренасяни"));
        p.Children.Add(tiles);

        // Продуктивност по дни – колко часа работа (работеното по задачите; ако го няма – времето на свършените).
        int Worked(DateTime d) => state.OnDay(d).Sum(t => t.Spent > 0 ? t.Spent : t.Done ? t.Minutes : 0);
        p.Children.Add(Label("Работа по дни"));
        int most = Math.Max(30, days.Max(Worked));
        // Без фиксирана височина: числото, стълбчето и денят винаги се събират (не се режат).
        var bars = new UniformGrid { Rows = 1, Margin = new Thickness(0, 4 * U, 0, 6 * U) };
        foreach (var d in days)
        {
            int minutes = Worked(d);
            var col = new DockPanel { Margin = new Thickness(3 * U, 0, 3 * U, 0) };
            var name = new TextBlock { Text = Capital(d.ToString("ddd", Bg)), HorizontalAlignment = HorizontalAlignment.Center, Foreground = d == today ? Ink : Muted, FontWeight = d == today ? FontWeights.Bold : FontWeights.Normal };
            DockPanel.SetDock(name, Dock.Bottom);
            col.Children.Add(name);
            // Числото е точно над стълбчето; всички стълбчета стъпват на една линия.
            var hours = new TextBlock { Text = minutes == 0 ? "" : TaskCards.Duration(minutes), HorizontalAlignment = HorizontalAlignment.Center, Foreground = Muted, Margin = new Thickness(0, 0, 0, 2 * U) };
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
            stack.Children.Add(hours);
            var area = new Border { Height = 70 * U + 26 * U, Child = stack };
            col.Children.Add(area);
            var bar = new Border
            {
                Width = 16 * U,
                Height = Math.Max(minutes > 0 ? 3 * U : 0, 70 * U * minutes / most),
                Margin = new Thickness(0, 0, 0, 3 * U),
                Background = Honey,
                BorderBrush = Ink,
                BorderThickness = new Thickness(minutes > 0 ? Math.Max(1, P / 2) : 0),
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Center,
                ToolTip = Tip($"{Capital(d.ToString("dddd, d MMMM", Bg))}: {TaskCards.Duration(minutes)}"),
            };
            stack.Children.Add(bar);
            bars.Children.Add(col);
        }
        p.Children.Add(bars);

        // Как прекара времето – кръг по етикети.
        var byTime = tasks.Where(t => t.Spent > 0 || t.Done).GroupBy(t => t.Tag)
            .Select(g => (Tag: g.Key, Minutes: g.Sum(t => t.Spent > 0 ? t.Spent : t.Minutes))).Where(x => x.Minutes > 0)
            .OrderByDescending(x => x.Minutes).ToList();
        p.Children.Add(Label("Как прекара времето"));
        var donutRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8 * U) };
        donutRow.Children.Add(Donut(byTime.Select(x => (x.Tag.Length > 0 && TaskTags.Find(x.Tag) is { } info ? info.Color : Color.FromRgb(170, 160, 150), (double)x.Minutes)).ToList()));
        var legend = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14 * U, 0, 0, 0) };
        if (byTime.Count == 0) legend.Children.Add(MenuWindow.MutedText("Още няма отбелязано време."));
        foreach (var x in byTime)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 3 * U) };
            row.Children.Add(TaskTags.ColorSquare(TaskTags.Find(x.Tag)?.Color ?? Color.FromRgb(170, 160, 150)));
            row.Children.Add(new TextBlock { Text = (TaskTags.Find(x.Tag)?.Name ?? "без етикет") + "  ·  " + TaskCards.Duration(x.Minutes), VerticalAlignment = VerticalAlignment.Center });
            legend.Children.Add(row);
        }
        donutRow.Children.Add(legend);
        p.Children.Add(donutRow);

        // Свършеното – по дни (кратко).
        if (done.Count > 0)
        {
            p.Children.Add(Label($"Свършено · {done.Count}"));
            foreach (var d in days)
            {
                var dayDone = state.OnDay(d).Where(t => t.Done).ToList();
                if (dayDone.Count == 0) continue;
                p.Children.Add(new TextBlock { Text = Capital(d.ToString("dddd", Bg)), FontWeight = FontWeights.Bold, Margin = new Thickness(0, 4 * U, 0, 2 * U) });
                foreach (var t in dayDone)
                {
                    var row = new DockPanel { Margin = new Thickness(6 * U, 0, 0, 2 * U) };
                    var check = Icon("check");
                    check.Margin = new Thickness(0, 0, 6, 0);
                    check.VerticalAlignment = VerticalAlignment.Top;
                    DockPanel.SetDock(check, Dock.Left);
                    row.Children.Add(check);
                    if (t.Spent > 0)
                    {
                        var time = new TextBlock { Text = TaskCards.Duration(t.Spent), Foreground = Muted, Margin = new Thickness(8, 0, 0, 0) };
                        DockPanel.SetDock(time, Dock.Right);
                        row.Children.Add(time);
                    }
                    row.Children.Add(new TextBlock { Text = t.Title, TextWrapping = TextWrapping.Wrap });
                    p.Children.Add(row);
                }
            }
        }

        // Задачите, които все се пренасят.
        var stuck = state.Open.Where(t => !t.Done && t.Rolled >= 2).Take(8).ToList();
        if (stuck.Count > 0)
        {
            p.Children.Add(Label("Все се пренасят – може би са големи? Раздели ги на стъпки или ги сложи за „Някога“."));
            foreach (var t in stuck)
                p.Children.Add(MiniRow(t,
                    ("Чакащи", "Без ден – в „Чакащи“", () => MoveWithToast(t, null)),
                    ("✏", "Отвори я (стъпки, ден…)", () =>
                    {
                        SetView(View.Week);
                        OpenCard(t);
                    })));
        }

        }
        if (_reviewStep == 1)
        {
        // Целите на тази седмица – постигнати ли са.
        if (review.Goals.Count > 0)
        {
            p.Children.Add(Label("Целите за тази седмица"));
            foreach (var g in review.Goals) p.Children.Add(GoalRow(monday, g));
        }

        // Размисъл.
        p.Children.Add(Label("Какво мина добре?"));
        var good = ReviewInput(review.Good);
        p.Children.Add(good);
        p.Children.Add(Label("Какво да е различно следващата седмица?"));
        var better = ReviewInput(review.Better);
        p.Children.Add(better);
        _commitReview = () => _pet.SetWeekThoughts(monday, good.Text, better.Text);
        good.LostKeyboardFocus += (_, _) => CommitReview();
        better.LostKeyboardFocus += (_, _) => CommitReview();

        }
        if (_reviewStep == 2)
        {
        // Целите за следващата седмица.
        var nextMonday = monday.AddDays(7);
        p.Children.Add(Label($"Целите за следващата седмица (до {PetWindow.MaxWeekGoals}) – виждат се в „Днес“"));
        foreach (var g in nextReview.Goals) p.Children.Add(GoalRow(nextMonday, g));
        if (nextReview.Goals.Count < PetWindow.MaxWeekGoals)
        {
            var goal = MenuWindow.NewTextInput();
            goal.Padding = new Thickness(6, 4, 6, 4);
            goal.MaxLength = 120;
            goal.TextChanged += (_, _) => _reviewDirty = goal.Text.Length > 0 || _reviewDirty;
            goal.KeyDown += (_, e) =>
            {
                if (e.Key != Key.Enter || string.IsNullOrWhiteSpace(goal.Text)) return;
                string text = goal.Text;
                goal.Clear();
                CommitReview();
                _reviewFocusGoal = true;
                _pet.AddWeekGoal(nextMonday, text);
                e.Handled = true;
            };
            p.Children.Add(WithPlus(goal, "Нова цел… (Enter)"));
            if (_reviewFocusGoal)
            {
                _reviewFocusGoal = false;
                Dispatcher.BeginInvoke(() => goal.Focus(), DispatcherPriority.Input);
            }
        }

        }

        // Минали седмици: прегледите с целите и мислите (най-новите отгоре).
        if (_reviewStep == 3)
        {
            var past = _pet.Save.WeekReviews
                .Select(kv => (Monday: DateTime.TryParse(kv.Key, out var m) ? m.Date : DateTime.MinValue, Review: kv.Value))
                .Where(x => x.Monday != DateTime.MinValue && (x.Review.Done || x.Review.Goals.Count > 0 || x.Review.Good.Length > 0 || x.Review.Better.Length > 0))
                .OrderByDescending(x => x.Monday).ToList();
            if (past.Count == 0) p.Children.Add(MenuWindow.MutedText("Още няма прегледи. Като направиш преглед на седмица, ще го видиш тук."));
            foreach (var (wk, r) in past)
            {
                var sun = wk.AddDays(6);
                var box = new StackPanel();
                var head2 = new DockPanel();
                var open = Small("Отвори", () => { CommitReview(); _reviewWeek = wk; _reviewStep = 0; Refresh(true); });
                DockPanel.SetDock(open, Dock.Right);
                head2.Children.Add(open);
                head2.Children.Add(new TextBlock
                {
                    Text = (wk.Month == sun.Month ? $"{wk.Day} – {sun.ToString("d MMMM yyyy", Bg)}" : $"{wk.ToString("d MMM", Bg)} – {sun.ToString("d MMM yyyy", Bg)}") + (r.Done ? "  ✓" : ""),
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment = VerticalAlignment.Center,
                });
                box.Children.Add(head2);
                if (r.Goals.Count > 0)
                {
                    box.Children.Add(new TextBlock { Text = "Цели", Foreground = Muted, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 6 * U, 0, 2 * U) });
                    foreach (var g in r.Goals)
                    {
                        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2 * U) };
                        var mark = Icon(g.Done ? "check" : "close");
                        mark.Opacity = g.Done ? 1 : 0.4;
                        mark.Margin = new Thickness(0, 0, 6, 0);
                        mark.VerticalAlignment = VerticalAlignment.Center;
                        row.Children.Add(mark);
                        row.Children.Add(new TextBlock { Text = g.Text, TextWrapping = TextWrapping.Wrap, Foreground = g.Done ? Ink : Muted });
                        box.Children.Add(row);
                    }
                }
                void Thought(string label, string text)
                {
                    if (text.Length == 0) return;
                    box.Children.Add(new TextBlock { Text = label, Foreground = Muted, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 6 * U, 0, 2 * U) });
                    box.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap });
                }
                Thought("Какво мина добре", r.Good);
                Thought("Какво да е различно", r.Better);
                p.Children.Add(new Border
                {
                    Background = Cream,
                    BorderBrush = WoodDark,
                    BorderThickness = Flat,
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10 * U, 8 * U, 8 * U, 8 * U),
                    Margin = new Thickness(0, 0, 0, 8 * U),
                    Child = box,
                });
            }
            return;
        }

        // ← Назад / Напред → (на последната – „Приключи седмицата“).
        var footer = new DockPanel { Margin = new Thickness(0, 18 * U, 0, 0) };
        if (_reviewStep > 0)
        {
            var back = Button("", () => { CommitReview(); _reviewStep--; Refresh(true); });
            back.Content = Icon("nav_prev");
            back.ToolTip = Tip("Назад");
            DockPanel.SetDock(back, Dock.Left);
            footer.Children.Add(back);
        }
        Button forward;
        if (_reviewStep < 2)
            forward = Button("Напред", () => { CommitReview(); _reviewStep++; Refresh(true); }, primary: true, icon: "nav_next");
        else
            forward = Button(review.Done ? "Седмицата е приключена ✓" : "Приключи седмицата", () =>
            {
                CommitReview();
                _pet.FinishWeekReview(monday);
            }, primary: !review.Done, icon: "check");
        forward.HorizontalAlignment = HorizontalAlignment.Stretch;
        footer.Children.Add(forward);
        p.Children.Add(footer);
    }

    private Action? _commitReview;
    private int _reviewStep;

    /// <summary>Кръг (поничка) от части – цвят и колко.</summary>
    private static FrameworkElement Donut(List<(Color Color, double Value)> parts)
    {
        double size = 120 * U, thick = 22 * U, r = (size - thick) / 2;
        var canvas = new Canvas { Width = size, Height = size };
        double total = parts.Sum(x => x.Value);
        if (total <= 0 || parts.Count == 1)
        {
            var ring = new System.Windows.Shapes.Ellipse
            {
                Width = size - thick,
                Height = size - thick,
                Stroke = total <= 0 ? Empty : new SolidColorBrush(parts[0].Color),
                StrokeThickness = thick,
            };
            Canvas.SetLeft(ring, thick / 2);
            Canvas.SetTop(ring, thick / 2);
            canvas.Children.Add(ring);
            return canvas;
        }
        double angle = -90;
        var c = new Point(size / 2, size / 2);
        Point At(double deg) => new(c.X + r * Math.Cos(deg * Math.PI / 180), c.Y + r * Math.Sin(deg * Math.PI / 180));
        foreach (var (color, value) in parts)
        {
            double sweep = value / total * 360;
            var fig = new PathFigure { StartPoint = At(angle), IsClosed = false };
            fig.Segments.Add(new ArcSegment(At(angle + sweep - 0.01), new Size(r, r), 0, sweep > 180, SweepDirection.Clockwise, true));
            canvas.Children.Add(new System.Windows.Shapes.Path
            {
                Data = new PathGeometry(new[] { fig }),
                Stroke = new SolidColorBrush(color),
                StrokeThickness = thick,
            });
            angle += sweep;
        }
        return canvas;
    }
    private bool _reviewDirty, _reviewFocusGoal;

    private void CommitReview()
    {
        _commitReview?.Invoke();
        _reviewDirty = false;
    }

    private TextBox ReviewInput(string text)
    {
        var box = MenuWindow.NewTextInput();
        box.Text = text;
        box.AcceptsReturn = true;
        box.TextWrapping = TextWrapping.Wrap;
        box.MinHeight = 56 * U;
        box.MaxHeight = 160 * U;
        box.MaxLength = 2000;
        box.VerticalContentAlignment = VerticalAlignment.Top;
        box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        box.TextChanged += (_, _) => _reviewDirty = true;
        return box;
    }

    private FrameworkElement GoalRow(DateTime monday, WeekGoal g)
    {
        var row = new DockPanel { Margin = new Thickness(0, 0, 0, 4 * U) };
        var check = TaskCards.CheckSquare(g.Done, on => { CommitReview(); _pet.SetWeekGoalDone(monday, g.Id, on); });
        check.Margin = new Thickness(0, 2, 6 * U, 0);
        DockPanel.SetDock(check, Dock.Left);
        row.Children.Add(check);
        var del = Chip("✕", () => { CommitReview(); _pet.DeleteWeekGoal(monday, g.Id); });
        del.Padding = new Thickness(4, 0, 4, 1);
        del.ToolTip = Tip("Махни целта");
        DockPanel.SetDock(del, Dock.Right);
        row.Children.Add(del);
        row.Children.Add(new TextBlock
        {
            Text = g.Text,
            TextWrapping = TextWrapping.Wrap,
            TextDecorations = g.Done ? TextDecorations.Strikethrough : null,
            Foreground = g.Done ? Muted : Ink,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 1, 6, 0),
        });
        return row;
    }

    private static Border StatTile(string icon, string value, string label)
    {
        var p = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        var v = IconText(icon, value, bold: true, size: Body * 1.3);
        v.HorizontalAlignment = HorizontalAlignment.Center;
        p.Children.Add(v);
        p.Children.Add(new TextBlock { Text = label, Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
        return new Border
        {
            Background = Tile,
            BorderBrush = WoodDark,
            BorderThickness = Flat,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6 * U, 8 * U, 6 * U, 8 * U),
            Margin = new Thickness(0, 0, 6 * U, 0),
            Child = p,
        };
    }

    private static DockPanel SideHeader(string text, Action close)
    {
        var top = new DockPanel { Margin = new Thickness(0, 0, 0, 8 * U) };
        var x = Chip("✕", close);
        x.ToolTip = Tip("Затвори (Esc)");
        x.Padding = new Thickness(6, 1, 6, 2);
        x.VerticalAlignment = VerticalAlignment.Top;
        DockPanel.SetDock(x, Dock.Right);
        top.Children.Add(x);
        top.Children.Add(new TextBlock { Text = text, FontWeight = FontWeights.Bold, FontSize = Body * 1.15, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center });
        return top;
    }

    private static TextBlock Label(string text) => new()
    {
        Text = text,
        Foreground = Muted,
        Margin = new Thickness(0, 10 * U, 0, 3 * U),
        TextWrapping = TextWrapping.Wrap,
    };

    private static Border SmallIcon(string icon, Action click)
    {
        var chip = IconChip(icon, click);
        chip.Padding = new Thickness(4, 2, 4, 3);
        chip.Margin = new Thickness(0, 0, 4 * U, 4 * U);
        return chip;
    }

    private static Border Small(string text, Action click)
    {
        var chip = Chip(text, click);
        chip.Padding = new Thickness(6, 2, 6, 3);
        chip.Margin = new Thickness(0, 0, 4 * U, 4 * U);
        return chip;
    }

    private TextBox Input(string text, bool multiline, int max)
    {
        var box = MenuWindow.NewTextInput();
        box.Text = text;
        box.MaxLength = max;
        box.TextWrapping = TextWrapping.Wrap;
        if (multiline)
        {
            box.AcceptsReturn = true;
            box.VerticalContentAlignment = VerticalAlignment.Top;
            box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        }
        box.TextChanged += (_, _) => _sideDirty = true;
        return box;
    }

    // ── Подробностите на задачата ──

    private UIElement Details(TaskItem t)
    {
        var p = new StackPanel();
        string when = t.Day is { } d ? Capital(d.ToString("dddd, d MMMM", Bg)) : "Някога";
        p.Children.Add(SideHeader(when, () => Select(null)));
        bool open = !t.Done;

        // Име и бележка (запазват се, щом излезеш от полето или натиснеш Enter в името).
        var title = Input(t.Title, multiline: false, TasksModule.MaxTitle);
        title.FontWeight = FontWeights.Bold;
        title.IsEnabled = open;
        var text = Input(t.Description, multiline: true, 4000);
        text.MinHeight = 56 * U;
        text.MaxHeight = 160 * U;
        text.Margin = new Thickness(0, 6 * U, 0, 0);
        text.IsEnabled = open;
        void Save()
        {
            if (_pet.Save.Tasks.FirstOrDefault(x => x.Id == t.Id) is not { Done: false } cur) return;
            string name = title.Text.Trim();
            if (name.Length == 0)
            {
                title.Text = cur.Title;
                return;
            }
            if (name == cur.Title && text.Text.Trim() == cur.Description) return;
            _pet.EditTask(cur, name, cur.Size, text.Text);
        }
        _commits.Add(Save);
        title.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            Commit();
            Keyboard.ClearFocus();
            e.Handled = true;
        };
        title.LostKeyboardFocus += (_, _) => { Save(); _sideDirty = false; };
        text.LostKeyboardFocus += (_, _) => { Save(); _sideDirty = false; };
        p.Children.Add(title);
        p.Children.Add(MenuWindow.WithHint(text, "Бележка…"));
        if (_focusAfter == FocusOn.Title && open)
        {
            _focusAfter = FocusOn.None;
            Dispatcher.BeginInvoke(() => { title.Focus(); title.CaretIndex = title.Text.Length; }, DispatcherPriority.Input);
        }

        // Стъпки.
        var steps = t.StepList;
        p.Children.Add(Label(steps.Count > 0 ? $"Стъпки · {steps.Count(x => x.Done)}/{steps.Count}" : "Стъпки"));
        foreach (var step in steps)
        {
            var st = step;
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 3 * U) };
            var check = TaskCards.CheckSquare(st.Done, on => Act(() => _pet.SetTaskStepDone(t, st.Id, on))(), enabled: open);
            check.Margin = new Thickness(0, 3, 6 * U, 0);
            DockPanel.SetDock(check, Dock.Left);
            row.Children.Add(check);
            var del = Chip("✕", Act(() => _pet.DeleteTaskStep(t, st.Id)));
            del.Padding = new Thickness(4, 0, 4, 1);
            del.Opacity = 0;
            del.VerticalAlignment = VerticalAlignment.Top;
            del.ToolTip = Tip("Махни стъпката");
            del.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            DockPanel.SetDock(del, Dock.Right);
            row.Children.Add(del);
            var name = Input(st.Title, multiline: false, TasksModule.MaxTitle);
            name.BorderThickness = new Thickness(0);
            name.Background = Brushes.Transparent;
            name.Padding = new Thickness(0, 2, 0, 2);
            name.IsEnabled = open;
            if (st.Done)
            {
                name.Foreground = Muted;
                name.TextDecorations = TextDecorations.Strikethrough;
            }
            void SaveStep()
            {
                string v = name.Text.Trim();
                if (v.Length == 0) name.Text = st.Title;
                else if (v != st.Title) _pet.RenameTaskStep(t, st.Id, v);
            }
            _commits.Add(SaveStep);
            name.LostKeyboardFocus += (_, _) => { SaveStep(); _sideDirty = false; };
            name.KeyDown += (_, e) =>
            {
                if (e.Key != Key.Enter) return;
                _focusAfter = FocusOn.NewStep;
                Commit();
                _sideShown = "";
                Refresh();
                e.Handled = true;
            };
            row.Children.Add(name);
            row.MouseEnter += (_, _) => del.Opacity = 1;
            row.MouseLeave += (_, _) => del.Opacity = 0;
            p.Children.Add(row);
        }
        if (open)
        {
            var newStep = MenuWindow.NewTextInput();
            newStep.Padding = new Thickness(6, 3, 6, 3);
            newStep.MaxLength = TasksModule.MaxTitle;
            newStep.TextChanged += (_, _) => _sideDirty = newStep.Text.Length > 0 || _sideDirty;
            newStep.KeyDown += (_, e) =>
            {
                if (e.Key != Key.Enter || string.IsNullOrWhiteSpace(newStep.Text)) return;
                string v = newStep.Text;
                newStep.Clear();
                _focusAfter = FocusOn.NewStep;
                Commit();
                _pet.AddTaskStep(t, v);
                e.Handled = true;
            };
            p.Children.Add(WithPlus(newStep, steps.Count == 0 ? "Раздели я на стъпки… (Enter)" : "Още стъпка… (Enter)"));
            if (_focusAfter == FocusOn.NewStep)
            {
                _focusAfter = FocusOn.None;
                Dispatcher.BeginInvoke(() => newStep.Focus(), DispatcherPriority.Input);
            }
        }

        if (open)
        {
            // Кога.
            var today = Today;
            p.Children.Add(Label("Кога"));
            var whenRow = new WrapPanel();
            (string Name, DateTime? Day)[] days =
            {
                ("Днес", today), ("Утре", today.AddDays(1)), ("Следв. седмица", WeekOf(today).AddDays(7)), ("Някога", null),
            };
            foreach (var (n, day) in days)
            {
                var target = day;
                var chip = Small(n, Act(() => MoveWithToast(t, target)));
                SetOn(chip, t.Day == day);
                whenRow.Children.Add(chip);
            }
            if (t.Day is { } cur)
            {
                var back = SmallIcon("nav_prev", Act(() => MoveWithToast(t, cur.AddDays(-1))));
                back.ToolTip = Tip("Ден назад (Alt+←)");
                var fwd = SmallIcon("nav_next", Act(() => MoveWithToast(t, cur.AddDays(1))));
                fwd.ToolTip = Tip("Ден напред (Alt+→)");
                whenRow.Children.Add(back);
                whenRow.Children.Add(fwd);
            }
            p.Children.Add(whenRow);

            // Етикет.
            p.Children.Add(Label("Етикет"));
            var tags = new WrapPanel();
            foreach (var tag in TaskTags.All)
            {
                var key = tag.Key;
                var chip = Small("", Act(() => _pet.MarkTask(t, tag: t.Tag == key ? "" : key)));
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(TaskTags.Swatch(key));
                row.Children.Add(new TextBlock { Text = tag.Name, FontWeight = FontWeights.Bold });
                chip.Child = row;
                SetOn(chip, t.Tag == key);
                tags.Children.Add(chip);
            }
            // „+“: нов етикет (и веднага на задачата); зъбчето – всички етикети.
            Border newTag = null!;
            newTag = Small("", () => PixelPopup.Show(newTag, "Нов етикет", () => new[]
            {
                new PixelPopup.Entry("", null, null, Custom: close => TagEditor.NewTag(_pet, key =>
                {
                    close();
                    _pet.MarkTask(t, tag: key);
                })),
            }));
            newTag.Child = Icon("plus");
            newTag.ToolTip = Tip("Нов етикет");
            tags.Children.Add(newTag);
            Border editTags = null!;
            editTags = Small("", () => PixelPopup.Show(editTags, "Етикети", () => new[]
            {
                new PixelPopup.Entry("", null, null, Custom: _ => TagEditor.Manager(_pet)),
            }));
            editTags.Child = Icon("gear");
            editTags.ToolTip = Tip("Етикетите: име, цвят, махни");
            tags.Children.Add(editTags);
            p.Children.Add(tags);

            // Важна и големина – на един ред.
            p.Children.Add(Label("Важна ли е? Каква е?"));
            var marks = new WrapPanel();
            var star = Small("", Act(() => _pet.MarkTask(t, star: !t.Star)));
            star.Child = IconText("star", t.Star ? "Важна" : "Не е важна", bold: true);
            SetOn(star, t.Star);
            marks.Children.Add(star);
            for (int i = 1; i <= 3; i++)
            {
                int size = i;
                var chip = Small(TaskCards.SizeNames[i - 1], Act(() => _pet.MarkTask(t, size: size)));
                chip.ToolTip = Tip($"+{(i switch { 3 => 5, 2 => 3, _ => 1 })} лешника, когато я свършиш");
                SetOn(chip, t.Size == i);
                marks.Children.Add(chip);
            }
            p.Children.Add(marks);

            // Време.
            p.Children.Add(Label(t.Spent > 0 ? $"Колко време?  ·  работено {TaskCards.Duration(t.Spent)}" : "Колко време?"));
            var times = new WrapPanel();
            foreach (int m in TaskCards.Estimates.Prepend(0))
            {
                int minutes = m;
                var chip = Small(m == 0 ? "—" : TaskCards.Duration(m), Act(() => _pet.MarkTask(t, minutes: minutes)));
                chip.ToolTip = Tip(m == 0 ? "Не знам" : $"Около {TaskCards.Duration(m)}");
                SetOn(chip, t.Minutes == m);
                times.Children.Add(chip);
            }
            p.Children.Add(times);

            if (t.Rolled > 1)
            {
                var hint = IconText("history", $"Пренасяна {t.Rolled} пъти.");
                hint.Margin = new Thickness(0, 10 * U, 0, 0);
                p.Children.Add(hint);
                p.Children.Add(new TextBlock
                {
                    Text = "Може би е голяма? Раздели я на стъпки или я сложи за „Някога“, докато ѝ дойде времето.",
                    Foreground = Muted,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2 * U, 0, 0),
                });
            }
        }

        // Действия.
        // Едно под друго, на цялата ширина – имената се виждат целите (две колони не се събираха).
        var actions = new StackPanel { Margin = new Thickness(0, 14 * U, 0, 0) };
        if (open)
        {
            actions.Children.Add(Button("Свършена", Act(() => _pet.SetTaskDone(t, true)), icon: "check"));
            bool onBoard = _pet.TaskOnBoard(t);
            var board = Button(onBoard ? "От таблото" : "В таблото", Act(() => _pet.SetTaskOnBoard(t, !onBoard)), icon: onBoard ? "unpin" : "pin");
            board.ToolTip = Tip(onBoard ? "В закаченото табло е (ако е за днес) – махни я оттам" : "Покажи я в закаченото табло (ако е за днес)");
            actions.Children.Add(board);
            var copy = Button("Копие", Act(() => _pet.DuplicateTask(t)), icon: "copy");
            copy.ToolTip = Tip("Още една такава задача (Ctrl+D)");
            actions.Children.Add(copy);
        }
        else if (_pet.Save.Tasks.Any(x => x.Id == t.Id))
        {
            actions.Children.Add(Button("Не е свършена", Act(() => _pet.SetTaskDone(t, false)), icon: "reroll"));
        }
        actions.Children.Add(Button("Изтрий", Act(() => _pet.DeleteTask(t)), icon: "close"));
        foreach (FrameworkElement b in actions.Children)
        {
            b.Margin = new Thickness(0, 0, 0, 5 * U);
            b.HorizontalAlignment = HorizontalAlignment.Stretch;
            if (b is Control c) c.HorizontalContentAlignment = HorizontalAlignment.Left;
        }
        p.Children.Add(actions);

        if (t.Created is { } created)
            p.Children.Add(new TextBlock
            {
                Text = "Написана " + created.ToString("d MMMM, HH:mm", Bg),
                Foreground = Muted,
                Opacity = 0.8,
                Margin = new Thickness(0, 8 * U, 0, 0),
            });
        return p;
    }

    private void MoveWithToast(TaskItem t, DateTime? day)
    {
        if (t.Day == day) return;
        var open = _pet.TaskState.Open;
        int index = open.FindIndex(x => x.Id == t.Id);
        _pet.MoveTask(t, day);
        if (day is { } d) ScrollToDayIfHidden(d);
        ShowToast($"„{Shorten(t.Title, 40)}“ → {DayName(day)}", () => _pet.PutTask(t, index));
    }

    // ── Малък ред за плана и края на деня: ✓, името, копчета ──

    private FrameworkElement MiniRow(TaskItem t, params (string Text, string Tip, Action Click)[] buttons)
    {
        var row = new DockPanel { Margin = new Thickness(0, 0, 0, 4 * U) };
        var check = TaskCards.CheckSquare(t.Done, on => _pet.SetTaskDone(t, on), enabled: _pet.Save.Tasks.Any(x => x.Id == t.Id));
        check.Margin = new Thickness(0, 2, 6 * U, 0);
        DockPanel.SetDock(check, Dock.Left);
        row.Children.Add(check);
        // Името, а под него копчетата (в тесния панел не излизат вдясно).
        var body = new StackPanel();
        body.Children.Add(new TextBlock
        {
            Text = t.Title,
            TextWrapping = TextWrapping.Wrap,
            TextDecorations = t.Done ? TextDecorations.Strikethrough : null,
            Foreground = t.Done ? Muted : Ink,
            Margin = new Thickness(0, 1, 4, 0),
        });
        if (buttons.Length > 0)
        {
            var chips = new WrapPanel { Margin = new Thickness(0, 3 * U, 0, 0) };
            foreach (var (text, tip, click) in buttons)
            {
                var chip = Chip(text, Act(click));
                chip.Padding = new Thickness(5, 0, 5, 1);
                chip.Margin = new Thickness(0, 0, 4, 2);
                chip.ToolTip = Tip(tip);
                chips.Children.Add(chip);
            }
            body.Children.Add(chips);
        }
        row.Children.Add(body);
        return row;
    }

    // ── За самопроверката ──
    internal DateTime FirstDay => _from;
    internal int DayCount => _count;
    internal int ColumnsShown => _strip.Children.Count;
    internal double Offset => _dayScroll.HorizontalOffset;
    internal string SideShown => _sideMode.ToString();
    internal bool ToastShown => _toast.Visibility == Visibility.Visible;
    internal void TestScrollTo(double offset) => ScrollTo(offset, smooth: false);
    internal bool Ready => _ready;
    internal int TimelineBlocks => _todayTimeline?.BlockCount ?? -1;
    internal int CalendarBlocks => _weekTimeline?.BlockCount ?? -1;
    internal DayTimeline? TodayTimeline => _todayTimeline;
    /// <summary>В „Днес“ има ли покана/карта (invite:План за деня, due…).</summary>
    internal bool TodayHas(string tag) => _todayPanel.Children.OfType<FrameworkElement>().Any(e => e.Tag as string == tag);
    internal int DueCount => BacklogDue(_pet.TaskState, Today).Count;
    internal void TestHours(DateTime day, int minutes) => SetUsualEnd(minutes);
    internal bool TodayAsksHours => FindText(_todayPanel, "До колко работиш?");
    internal FrameworkElement TestAnchor => _todayPanel.Children.OfType<FrameworkElement>().FirstOrDefault() ?? _todayPanel;
    internal FrameworkElement? TodayPart(string tag) => _todayPanel.Children.OfType<FrameworkElement>().FirstOrDefault(e => e.Tag as string == tag);

    private static bool FindText(DependencyObject root, string text)
    {
        if (root is TextBlock tb && tb.Text.StartsWith(text)) return true;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (FindText(VisualTreeHelper.GetChild(root, i), text)) return true;
        return false;
    }
    internal void TestCalendarMode(int mode) { _calMode = mode; Refresh(true); }
    internal int TodayCards => FindCards(_todayPanel);
    internal void TestScrollToDay(DateTime day) => ScrollToDay(day, smooth: false);
    internal void TestSelect(Guid? id) { if (id is { } x && _pet.Save.Tasks.FirstOrDefault(t => t.Id == x) is { } t) OpenCard(t); }
    internal void TestSide(string side) => OpenSide(side);
    internal void TestUndo() => Undo();
    internal void TestSearch(string text) => _search.Text = text;
    internal int VisibleCards(DateTime? day) =>
        (day == null ? _someday.Child : _columns.TryGetValue(day.Value, out var c) ? c.Column : null) is { } col
            ? FindCards(col) : -1;

    private static int FindCards(DependencyObject root)
    {
        int n = 0;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement { Tag: TaskItem }) n++;
            else n += FindCards(child);
        }
        return n;
    }

    private static string Capital(string s) => s.Length == 0 ? s : char.ToUpper(s[0], Bg) + s[1..];
}
