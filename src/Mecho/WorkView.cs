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
    private enum Side { None, Details, Plan, Shutdown }

    private readonly PetWindow _pet;
    private readonly StackPanel _strip = new() { Orientation = Orientation.Horizontal };
    private readonly ScrollViewer _dayScroll;
    private readonly Border _someday = new();
    private readonly Border _side;
    private readonly TextBlock _rangeLabel = new() { FontWeight = FontWeights.Bold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _summary = new() { Foreground = Brushes.White, Opacity = 0.9, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Right };
    private readonly WrapPanel _filters = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBox _search;
    private readonly Border _todayChip;
    private readonly Border _toast;
    private readonly TextBlock _toastText = new() { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 260 };
    private readonly DispatcherTimer _toastTimer = new() { Interval = TimeSpan.FromSeconds(7) };
    private readonly DispatcherTimer _scrollTimer = new() { Interval = TimeSpan.FromMilliseconds(15) };
    private readonly Dictionary<DateTime, (string Sig, Border Column)> _columns = new();
    private readonly Dictionary<string, TextBox> _addBoxes = new();
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
    private string? _shutdownDraft;
    private Action? _undo;
    private (bool Has, DateTime? Day) _refocus;
    private enum FocusOn { None, Title, NewStep }
    private FocusOn _focusAfter;

    private static readonly CultureInfo Bg = new("bg-BG");
    private static double U => Body / 18;
    private static double ColumnWidth => 236 * U;
    private static double Step => ColumnWidth + 10 * U;

    /// <summary>Понеделникът на седмицата, в която е денят.</summary>
    public static DateTime WeekOf(DateTime day)
    {
        int back = ((int)day.DayOfWeek + 6) % 7;
        return day.Date.AddDays(-back);
    }

    private DateTime Today => _pet.TaskToday;
    private int CapacityMinutes => Math.Clamp(_pet.Save.PlannerHours, 1, 16) * 60;

    public WorkView(PetWindow pet)
    {
        _pet = pet;
        _from = WeekOf(pet.TaskToday).AddDays(-7);
        _count = 42;
        Background = Brushes.Transparent;

        // ── Под него (само в „Седмица“): ◀ Днес ▶, кои дни се виждат, търсене, етикети … обобщение ──
        var weekNav = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
        var prev = HeaderChip("◀", () => ScrollBy(-7), "Седмица назад (←: ден назад)");
        _todayChip = HeaderChip("Днес", () => ScrollToDay(Today), "Към днес (T)");
        var next = HeaderChip("▶", () => ScrollBy(7), "Седмица напред (→: ден напред)");
        foreach (var b in new[] { prev, _todayChip, next })
        {
            b.Margin = new Thickness(0, 0, 4, 0);
            weekNav.Children.Add(b);
        }
        _rangeLabel.Margin = new Thickness(6, 0, 0, 0);
        _rangeLabel.MinWidth = 120 * U;
        weekNav.Children.Add(_rangeLabel);
        // ── Втори ред: търсене, етикети, само важните, скрий свършените … обобщение ──
        var bar = new DockPanel { Margin = new Thickness(4, 0, 2, 6) };
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
        var searchBox = new Border { Width = 150 * U, Margin = new Thickness(0, 0, 8, 0), Child = MenuWindow.WithHint(_search, "Търси…") };
        DockPanel.SetDock(searchBox, Dock.Left);
        bar.Children.Add(searchBox);
        DockPanel.SetDock(_filters, Dock.Left);
        bar.Children.Add(_filters);
        // Обобщението на седмицата е в „Преглед“ (тук редът е тесен).

        // ── Тялото: „Някога“ | дните (лента) | вдясно подробности / план / край на деня ──
        _dayScroll = new ScrollViewer
        {
            Content = _strip,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
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
        SizeChanged += (_, _) => _side.Width = Math.Clamp(ActualWidth * 0.42, 200 * U, 320 * U);
        _someday.Width = ColumnWidth;
        _someday.Margin = new Thickness(0, 0, 10 * U, 0);

        // „Седмица“: „Някога“ | дните; „Днес“ и „Преглед“ – свои страници. Вдясно (общо за
        // „Днес“ и „Седмица“) – подробностите на задачата, планът, краят на деня, работата сега.
        var week = new DockPanel();
        DockPanel.SetDock(_someday, Dock.Left);
        week.Children.Add(_someday);
        week.Children.Add(_dayScroll);
        _weekView = week;
        _todayView = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = _todayPanel };
        _reviewView = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = _reviewPanel };
        var host = new Grid { Children = { _todayView, _weekView, _reviewView } };
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
        _view = (View)Math.Clamp(_pet.Save.WorkView, 0, 2);
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

    public enum View { Today, Week, Review }

    private View _view;
    private FrameworkElement _weekBar = null!, _weekView = null!;
    private ScrollViewer _todayView = null!, _reviewView = null!;
    private readonly StackPanel _todayPanel = new() { Margin = new Thickness(4 * U, 0, 10 * U, 10 * U), MaxWidth = 760 };
    private readonly StackPanel _reviewPanel = new() { Margin = new Thickness(4 * U, 0, 10 * U, 10 * U), MaxWidth = 900 };
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };

    internal View CurrentView => _view;
    private bool _weekPlaced;

    /// <summary>Днес / Седмица / Преглед.</summary>
    public void SetView(View view)
    {
        Commit();
        _view = view;
        _pet.Save.WorkView = (int)view;
        _todayView.Visibility = view == View.Today ? Visibility.Visible : Visibility.Collapsed;
        _weekView.Visibility = view == View.Week ? Visibility.Visible : Visibility.Collapsed;
        _reviewView.Visibility = view == View.Review ? Visibility.Visible : Visibility.Collapsed;
        _weekBar.Visibility = view == View.Week ? Visibility.Visible : Visibility.Collapsed;
        if (view == View.Review)
        {
            _sideMode = Side.None;
            _selected = null;
        }
        _sideShown = "";
        _todayShown = _reviewShown = "";
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

    /// <summary>Отваря от вън плана или края на деня.</summary>
    public void OpenSide(string side)
    {
        // Планът и краят на деня са в „Днес“; прегледът – в „Преглед“.
        if (side == "review")
        {
            SetView(View.Review);
            return;
        }
        if (side is "plan" or "shutdown" && _view != View.Today) SetView(View.Today);
        ShowSide(side == "plan" ? Side.Plan : side == "shutdown" ? Side.Shutdown : Side.None);
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
        SetOn(_todayChip, todayVisible);
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
            case View.Today: BuildToday(state, today, force); break;
            case View.Review: BuildReview(state, today, force); break;
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

        string common = $"{today:yyyyMMdd}#{_filter}#{_starOnly}#{_query}#{_pet.Save.PlannerHideDone}#{_pet.Save.PlannerHours}#{_pet.Save.UiTheme}#{(_pet.InFocus ? _pet.FocusTask?.Id : null)}";
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
        string sig = $"{_filter}#{_starOnly}#{_pet.Save.PlannerHideDone}#{_pet.Save.UiTheme}";
        if (sig == _filtersShown) return;
        _filtersShown = sig;
        _filters.Children.Clear();
        var all = Chip("Всички", () => SetFilter("", false));
        SetOn(all, _filter.Length == 0 && !_starOnly);
        all.ToolTip = Tip("Всички задачи");
        _filters.Children.Add(all);
        foreach (var tag in TaskTags.All)
        {
            var key = tag.Key;
            var chip = Chip("", () => SetFilter(_filter == key ? "" : key, _starOnly));
            // Само цветното квадратче (името – при посочване); избраният етикет показва и името си.
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var swatch = TaskTags.Swatch(key);
            row.Children.Add(swatch);
            if (_filter == key) row.Children.Add(new TextBlock { Text = tag.Name, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            else swatch.Margin = new Thickness(0);
            chip.Child = row;
            chip.ToolTip = Tip($"Само „{tag.Name}“ (новите задачи също ще са с този етикет)");
            SetOn(chip, _filter == key);
            _filters.Children.Add(chip);
        }
        var star = Chip("", () => SetFilter(_filter, !_starOnly));
        star.Child = Icon("star");
        star.ToolTip = Tip("Само важните");
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
            Text = day is { } d1 ? d1.ToString(d1.Year == today.Year ? "d MMMM" : "d MMMM yyyy", Bg) : "без ден",
            Foreground = Muted,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        head.Children.Add(sub);

        // Време: планирано спрямо часовете за деня (свършеното – запълнено).
        int planned = tasks.Sum(t => t.Minutes);
        int doneMinutes = tasks.Where(t => t.Done).Sum(t => t.Minutes);
        if (day != null && (planned > 0 || !past))
            head.Children.Add(LoadBar(planned, doneMinutes, CapacityMinutes, compact: true));

        int rolled = tasks.Count(t => !t.Done && t.Rolled > 0);
        if (isToday && rolled > 0)
        {
            var info = IconText("history", rolled == 1 ? "1 пренесена от вчера" : $"{rolled} пренесени от минали дни");
            info.Opacity = 0.75;
            info.Margin = new Thickness(0, 4 * U, 0, 0);
            info.ToolTip = Tip("Недовършени от минали дни – дойдоха сами за днес. „План за деня“ помага да решиш какво да правиш с тях.");
            head.Children.Add(info);
        }
        head.MouseRightButtonUp += (_, e) =>
        {
            PixelPopup.Show(head, null, ColumnEntries(day, tasks), atMouse: true);
            e.Handled = true;
        };
        DockPanel.SetDock(head, Dock.Top);
        dock.Children.Add(head);

        // ── Нова задача (Enter): най-отгоре в деня ──
        var add = MenuWindow.NewTextInput();
        add.Padding = new Thickness(6, 4, 6, 4);
        add.MaxLength = TasksModule.MaxTitle + 40;
        string boxKey = BoxKey(day);
        _addBoxes[boxKey] = add;
        add.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                add.Clear();
                Keyboard.ClearFocus();
                e.Handled = true;
                return;
            }
            if (e.Key != Key.Enter || string.IsNullOrWhiteSpace(add.Text)) return;
            e.Handled = true;
            string text = add.Text;
            add.Clear();
            _refocus = (true, day);
            var first = _pet.TaskState.Open.FirstOrDefault(t => t.Day == day && !t.Done);
            _pet.AddTask(text, 1, null, day, someday: day == null, tag: _filter.Length > 0 ? _filter : null,
                star: _starOnly, quiet: true, before: first?.Id);
        };
        var addBox = WithPlus(add, day == null ? "Някога…" : "Задача…");
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
        if (openTasks.Count == 0 && (doneTasks.Count == 0 || _pet.Save.PlannerHideDone))
            list.Children.Add(new TextBlock
            {
                Text = _query.Length > 0 || _filter.Length > 0 || _starOnly ? ""
                    : day == null ? "Нещата без ден. Довлечи ги в ден, когато им дойде времето."
                    : past ? "" : isToday ? "Нищо за днес още. Напиши отгоре или довлечи от „Някога“." : "Свободен ден.",
                Foreground = Muted,
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.8,
                Margin = new Thickness(2 * U, 2 * U, 2 * U, 6 * U),
            });
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
        }, scroll);
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
        Select = x => Select(_selected == x.Id && _sideMode == Side.Details ? null : x.Id),
        Selected = _selected == t.Id && _sideMode == Side.Details,
        Description = 60,
        Edit = x => Select(x.Id, FocusOn.Title),
        Open = x => Select(x.Id, FocusOn.Title),
    });

    private static string BoxKey(DateTime? day) => day?.ToString("yyyyMMdd") ?? "someday";

    private string DayName(DateTime? day) =>
        day == null ? "Някога" : day == Today ? "днес" : day == Today.AddDays(1) ? "утре" : day == Today.AddDays(-1) ? "вчера" : day.Value.ToString("dddd, d MMMM", Bg);

    /// <summary>Лентичка „колко време“: свършеното (мед/зелено) и останалото от часовете за деня.</summary>
    private static FrameworkElement LoadBar(int planned, int done, int capacity, bool compact)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 5 * U, 0, 0) };
        bool over = planned > capacity;
        int total = Math.Max(capacity, planned);
        var bar = new Grid { Height = (compact ? 1.5 : 2.5) * P };
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(done, 0.0001), GridUnitType.Star) });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(planned - done, 0.0001), GridUnitType.Star) });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(total - planned, 0.0001), GridUnitType.Star) });
        var fill = new Border { Background = Good };
        var rest = new Border { Background = over ? Bad : Honey };
        var free = new Border { Background = Empty };
        Grid.SetColumn(rest, 1);
        Grid.SetColumn(free, 2);
        bar.Children.Add(fill);
        bar.Children.Add(rest);
        bar.Children.Add(free);
        panel.Children.Add(new Border { BorderBrush = Ink, BorderThickness = new Thickness(Math.Max(1, P / 2)), Child = bar });
        panel.Children.Add(new TextBlock
        {
            Text = planned == 0 ? $"свободно ({capacity / 60}ч)" : $"~{TaskCards.Duration(planned)} от {capacity / 60}ч" + (over ? " – много!" : ""),
            Foreground = over ? Bad : Muted,
            FontWeight = over ? FontWeights.Bold : FontWeights.Normal,
            Margin = new Thickness(0, 2 * U, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        panel.ToolTip = Tip(over
            ? $"Над {capacity / 60} часа за един ден – премести нещо за друг ден (часовете за деня се сменят в „План за деня“)."
            : "Колко време е планирано (по това, което си написала за задачите). Зелено – свършеното.");
        return panel;
    }

    /// <summary>Дясното копче на заглавието на деня: всички недовършени – утре, днес, „Някога“.</summary>
    private Func<IEnumerable<PixelPopup.Entry>> ColumnEntries(DateTime? day, List<TaskItem> tasks) => () =>
    {
        var list = new List<PixelPopup.Entry> { new("Нова задача тук", "plus", () => FocusAdd(day)) };
        if (day is { } from && tasks.Any(t => !t.Done))
        {
            var today = Today;
            if (from != today) list.Add(new("Недовършените – за днес", "sun", () => MoveAll(from, today)));
            list.Add(new("Недовършените – за утре", "arrow_right", () => MoveAll(from, (from < today ? today : from).AddDays(1))));
            list.Add(new("Недовършените – за „Някога“", "history", () => MoveAll(from, null)));
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
            if (_addBoxes.TryGetValue(boxKey, out var box)) box.Focus();
        }, DispatcherPriority.Input);
    }

    private void RestoreFocus()
    {
        if (!_refocus.Has) return;
        var day = _refocus.Day;
        _refocus = (false, null);
        if (_addBoxes.TryGetValue(BoxKey(day), out var box) && !box.IsKeyboardFocused)
            Dispatcher.BeginInvoke(() => box.Focus(), DispatcherPriority.Input);
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
                Select(sel.Id, FocusOn.Title);
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
            // В „Днес“ вдясно винаги има нещо: работата сега (мечо-доро, таймер, следващата задача).
            string focusSig = FocusSignature();
            if (focusSig == _sideShown) return;
            _sideShown = focusSig;
            _commits.Clear();
            _side.Visibility = Visibility.Visible;
            _side.Child = SideCard(FocusPanel());
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
        } + _sideMode + _pet.Save.UiTheme;
        if (sig == _sideShown) return;
        // Докато се пише в полетата – не се строи наново (ще се изгуби написаното).
        if (_sideDirty && _side.IsKeyboardFocusWithin) return;
        _sideShown = sig;
        _sideDirty = false;
        _commits.Clear();
        _side.Visibility = Visibility.Visible;
        var content = _sideMode switch
        {
            Side.Details => Details(t!),
            Side.Plan => Plan(),
            _ => Shutdown(),
        };
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
        string sig = string.Join("|", tasks.Select(t => t.Signature + _pet.TaskOnBoard(t))) + $"#{today:yyyyMMdd}#{hour >= 17}#{hour < 15}"
            + $"#{_pet.Save.PlannedDay}#{_pet.Save.ShutdownDay}#{_pet.Save.PlannerHours}#{_pet.Save.UiTheme}#{_selected}#{_sideMode}"
            + string.Join(",", review.Goals.Select(g => g.Text + g.Done)) + review.Done + (_pet.InFocus ? _pet.FocusTask?.Id : null)
            + string.Join(",", WeekReviewFor(monday.AddDays(-7)).Done);
        if (!force && sig == _todayShown) return;
        _todayShown = sig;
        var p = _todayPanel;
        p.Children.Clear();

        // Денят и колко е запълнен.
        p.Children.Add(new TextBlock
        {
            Text = Capital(today.ToString("dddd, d MMMM", Bg)),
            FontWeight = FontWeights.Bold,
            FontSize = Body * 1.4,
            TextWrapping = TextWrapping.Wrap,
        });
        int done = tasks.Count(t => t.Done), all = tasks.Count;
        int planned = tasks.Sum(t => t.Minutes), doneMinutes = tasks.Where(t => t.Done).Sum(t => t.Minutes);
        p.Children.Add(new TextBlock
        {
            Text = all == 0 ? "Още няма задачи за днес." : $"Свършени {done} от {all}" + (planned > 0 ? $"  ·  ~{TaskCards.Duration(planned)} работа" : ""),
            Foreground = Muted,
            Margin = new Thickness(0, 2 * U, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        });
        var load = LoadBar(planned, doneMinutes, CapacityMinutes, compact: false);
        load.MaxWidth = 420 * U;
        load.HorizontalAlignment = HorizontalAlignment.Left;
        p.Children.Add(load);

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

        // Покана: план (сутрин), край на деня (вечер), преглед на седмицата (пет–нед или пон за миналата).
        var lastMonday = monday.AddDays(-7);
        bool lastNotReviewed = today.DayOfWeek == DayOfWeek.Monday && !WeekReviewFor(lastMonday).Done
            && Enumerable.Range(0, 7).Any(i => state.OnDay(lastMonday.AddDays(i)).Any());
        if (_pet.Save.PlannedDay != key && hour < 15)
            p.Children.Add(Invite("sun", "Добро утро! Да планираме деня – вчерашните, колко време имаш, какво да вземем от „Някога“.", "План за деня", () => ShowSide(Side.Plan)));
        else if (_pet.Save.ShutdownDay != key && hour >= 17)
            p.Children.Add(Invite("moon", "Денят свършва. Да видим какво стана и какво остава за утре.", "Край на деня", () => ShowSide(Side.Shutdown)));
        if (lastNotReviewed)
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

        // Нова задача за днес (Enter) – най-отгоре.
        var add = MenuWindow.NewTextInput();
        add.Padding = new Thickness(8, 6, 8, 6);
        add.MaxLength = TasksModule.MaxTitle;
        _addBoxes[BoxKey(today)] = add;
        add.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                add.Clear();
                Keyboard.ClearFocus();
                e.Handled = true;
                return;
            }
            if (e.Key != Key.Enter || string.IsNullOrWhiteSpace(add.Text)) return;
            e.Handled = true;
            string text = add.Text;
            add.Clear();
            _refocus = (true, today);
            var first = _pet.TaskState.Open.FirstOrDefault(t => t.Day == today && !t.Done);
            _pet.AddTask(text, 1, null, today, quiet: true, before: first?.Id);
        };
        var addBox = WithPlus(add, "Нова задача за днес…  (Enter)");
        addBox.Margin = new Thickness(0, 14 * U, 0, 8 * U);
        p.Children.Add(addBox);

        // Задачите: несвършените в реда си (влачат се), после свършените.
        var open = tasks.Where(t => !t.Done).ToList();
        var list = new StackPanel { Background = Brushes.Transparent, MinHeight = 80 * U };
        if (open.Count == 0)
            list.Children.Add(new TextBlock
            {
                Text = done > 0 ? "Всичко за днес е свършено! Почивка или нещо от „Седмица“?" : "Напиши задача отгоре или довлечи от „Седмица“.",
                Foreground = Muted,
                TextWrapping = TextWrapping.Wrap,
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
    private FrameworkElement Invite(string icon, string text, string button, Action click)
    {
        var row = new DockPanel();
        var go = Button(button, click, primary: true, icon: icon);
        go.Margin = new Thickness(10 * U, 0, 0, 0);
        go.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(go, Dock.Right);
        row.Children.Add(go);
        row.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center });
        return new Border
        {
            Background = Cream,
            BorderBrush = Honey,
            BorderThickness = Px(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12 * U, 8 * U, 8 * U, 8 * U),
            Margin = new Thickness(0, 10 * U, 0, 0),
            Child = row,
        };
    }

    private WeekReview WeekReviewFor(DateTime monday) => _pet.WeekReviewFor(monday);

    // ── Сега: работата по задачите (вдясно в „Днес“) ──

    private TextBlock? _clockText, _clockWhat;

    /// <summary>„Към Фокус“ – менюто отваря страницата „Фокус“.</summary>
    public event Action? OpenFocus;

    private string FocusSignature()
    {
        var next = _pet.OrderedTasks().FirstOrDefault();
        return $"focus#{_pet.InFocus}#{_pet.Save.PomodoroPhase}#{_pet.TimerRunning}#{_pet.FocusTask?.Signature}#{next?.Signature}#{_pet.Save.PomodorosToday}#{_pet.FocusLength.TotalMinutes}#{_pet.Save.UiTheme}";
    }

    /// <summary>
    /// „Сега“: какво тече (мечо-доро, почивка, таймер) и колко остава – голямо; по коя задача;
    /// иначе – следващата задача и копчета да почнеш (мечо-доро или таймер по нея).
    /// </summary>
    private UIElement FocusPanel()
    {
        var p = new StackPanel();
        p.Children.Add(new TextBlock { Text = "Сега", FontWeight = FontWeights.Bold, FontSize = Body * 1.15, Margin = new Thickness(0, 0, 0, 8 * U) });
        bool focus = _pet.PomodoroFocus, rest = _pet.Save.PomodoroPhase == PomodoroPhase.Break, timer = _pet.TimerRunning;
        _clockText = new TextBlock { FontWeight = FontWeights.Bold, FontSize = Body * 2, HorizontalAlignment = HorizontalAlignment.Center };
        _clockWhat = new TextBlock { Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center };
        var clock = new StackPanel { Margin = new Thickness(0, 4 * U, 0, 10 * U) };
        clock.Children.Add(_clockText);
        clock.Children.Add(_clockWhat);
        p.Children.Add(new Border
        {
            Background = focus || timer ? Cream : Paper,
            BorderBrush = focus ? Honey : WoodDark,
            BorderThickness = focus ? Px(1) : Flat,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8 * U),
            Child = clock,
        });

        var current = _pet.FocusTask;
        var next = current ?? _pet.OrderedTasks().FirstOrDefault();
        if (focus || timer)
        {
            if (current != null)
            {
                p.Children.Add(Label("Работим по"));
                p.Children.Add(new TextBlock { Text = current.Title, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap });
                var doneNow = Button("Свършена!", Act(() =>
                {
                    _pet.SetTaskDone(current, true);
                    _pet.SetFocusTask(null);
                }), icon: "check");
                doneNow.Margin = new Thickness(0, 8 * U, 0, 0);
                doneNow.HorizontalAlignment = HorizontalAlignment.Left;
                p.Children.Add(doneNow);
            }
            var stop = Button(focus ? "Спри мечо-дорото" : "Спри таймера", Act(() =>
            {
                if (_pet.PomodoroFocus || _pet.Save.PomodoroPhase == PomodoroPhase.Break) _pet.StopPomodoro();
                else _pet.StopTimer();
            }), icon: "stop");
            stop.Margin = new Thickness(0, 10 * U, 0, 0);
            stop.HorizontalAlignment = HorizontalAlignment.Left;
            p.Children.Add(stop);
        }
        else if (rest)
        {
            p.Children.Add(MenuWindow.MutedText("Почивка. Стани, раздвижи се, пийни вода."));
            var stop = Button("Спри", Act(() => _pet.StopPomodoro()), icon: "stop");
            stop.Margin = new Thickness(0, 10 * U, 0, 0);
            stop.HorizontalAlignment = HorizontalAlignment.Left;
            p.Children.Add(stop);
        }
        else
        {
            p.Children.Add(Label(next != null ? "Следва" : "Няма задачи за днес"));
            if (next != null)
            {
                p.Children.Add(new TextBlock { Text = next.Title, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap });
                if (next.Minutes > 0) p.Children.Add(MenuWindow.MutedText("~" + TaskCards.Duration(next.Minutes)));
            }
            // Мечо-дорото и таймерът се пускат от „Фокус“ (там се избира и задачата).
            var start = Button("Към „Фокус“", Act(() => OpenFocus?.Invoke()), primary: true, icon: "honey");
            start.Margin = new Thickness(0, 10 * U, 0, 0);
            start.HorizontalAlignment = HorizontalAlignment.Stretch;
            start.ToolTip = Tip("Мечо-доро или таймер – по задача или без");
            p.Children.Add(start);
        }

        int pomodoros = _pet.Save.PomodorosToday;
        if (pomodoros > 0)
        {
            var today = IconText("honey", pomodoros == 1 ? "1 мечо-доро днес" : $"{pomodoros} мечо-дора днес");
            today.Margin = new Thickness(0, 14 * U, 0, 0);
            today.Opacity = 0.85;
            p.Children.Add(today);
        }
        UpdateClock();
        return p;
    }

    /// <summary>Всяка секунда – само часовникът в „Сега“ (без да се строи панелът наново).</summary>
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
            + "#" + string.Join(",", state.Open.Where(t => !t.Done && t.Rolled >= 2).Select(t => t.Signature)) + _pet.Save.UiTheme;
        if (!force && sig == _reviewShown) return;
        if (_reviewDirty && _reviewView.IsKeyboardFocusWithin) return;   // пише се – не се строи наново
        _reviewShown = sig;
        _reviewDirty = false;
        var p = _reviewPanel;
        p.Children.Clear();

        // Коя седмица.
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 10 * U) };
        var nav = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var prev = Small("◀", () => { CommitReview(); _reviewWeek = monday.AddDays(-7); Refresh(true); });
        prev.ToolTip = Tip("Предишната седмица");
        var next = Small("▶", () => { CommitReview(); _reviewWeek = monday.AddDays(7); Refresh(true); });
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

        // Числата.
        var done = tasks.Where(t => t.Done).ToList();
        int spent = tasks.Sum(t => t.Spent);
        int rolled = tasks.Count(t => t.Rolled > 0);
        var best = days.Select(d => (Day: d, Done: state.OnDay(d).Count(t => t.Done))).OrderByDescending(x => x.Done).First();
        var tiles = new UniformGrid { Rows = 1, Margin = new Thickness(0, 0, 0, 12 * U) };
        tiles.Children.Add(StatTile("check", $"{done.Count}", tasks.Count == 0 ? "свършени" : $"свършени от {tasks.Count}"));
        tiles.Children.Add(StatTile("hourglass", spent > 0 ? TaskCards.Duration(spent) : "—", "работено по задачи"));
        tiles.Children.Add(StatTile("star", best.Done > 0 ? Capital(best.Day.ToString("dddd", Bg)) : "—", best.Done > 0 ? $"най-много: {best.Done}" : "най-добрият ден"));
        tiles.Children.Add(StatTile("history", $"{rolled}", "пренасяни"));
        p.Children.Add(tiles);

        // По дни – стълбчета.
        p.Children.Add(Label("По дни"));
        int most = Math.Max(1, days.Max(d => state.OnDay(d).Count()));
        var bars = new UniformGrid { Rows = 1, Height = 110 * U };
        foreach (var d in days)
        {
            var onDay = state.OnDay(d).ToList();
            int dn = onDay.Count(t => t.Done), an = onDay.Count;
            var col = new DockPanel { Margin = new Thickness(3 * U, 0, 3 * U, 0) };
            var name = new TextBlock { Text = Capital(d.ToString("ddd", Bg)), HorizontalAlignment = HorizontalAlignment.Center, Foreground = d == today ? Ink : Muted, FontWeight = d == today ? FontWeights.Bold : FontWeights.Normal };
            DockPanel.SetDock(name, Dock.Bottom);
            col.Children.Add(name);
            var countText = new TextBlock { Text = an == 0 ? "" : $"{dn}/{an}", HorizontalAlignment = HorizontalAlignment.Center, Foreground = Muted };
            DockPanel.SetDock(countText, Dock.Top);
            col.Children.Add(countText);
            var stack = new Grid { VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Center, Width = 18 * U };
            double h = 70 * U;
            if (an > 0)
            {
                stack.Children.Add(new Border { Height = h * an / most, Background = Empty, BorderBrush = Ink, BorderThickness = new Thickness(Math.Max(1, P / 2)), VerticalAlignment = VerticalAlignment.Bottom });
                if (dn > 0) stack.Children.Add(new Border { Height = h * dn / most, Background = Good, BorderBrush = Ink, BorderThickness = new Thickness(Math.Max(1, P / 2)), VerticalAlignment = VerticalAlignment.Bottom });
            }
            stack.ToolTip = Tip($"{Capital(d.ToString("dddd, d MMMM", Bg))}: свършени {dn} от {an}");
            col.Children.Add(stack);
            bars.Children.Add(col);
        }
        p.Children.Add(bars);

        // По етикети.
        var byTag = tasks.GroupBy(t => t.Tag).Select(g => (Tag: g.Key, Done: g.Count(t => t.Done), All: g.Count(), Spent: g.Sum(t => t.Spent)))
            .OrderByDescending(x => x.Done).ThenByDescending(x => x.All).ToList();
        if (byTag.Count > 0 && !(byTag.Count == 1 && byTag[0].Tag.Length == 0))
        {
            p.Children.Add(Label("По етикети"));
            int top = Math.Max(1, byTag.Max(x => x.All));
            foreach (var x in byTag)
            {
                var row = new Grid { Margin = new Thickness(0, 0, 0, 4 * U) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120 * U) });
                row.ColumnDefinitions.Add(new ColumnDefinition());
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var label = new StackPanel { Orientation = Orientation.Horizontal };
                if (x.Tag.Length > 0) label.Children.Add(TaskTags.Swatch(x.Tag));
                label.Children.Add(new TextBlock { Text = TaskTags.Find(x.Tag)?.Name ?? "без етикет", TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center });
                row.Children.Add(label);
                var track = new Grid { Height = 2 * P, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6 * U, 0, 8 * U, 0) };
                track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(x.All, 0.0001), GridUnitType.Star) });
                track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(top - x.All, 0.0001), GridUnitType.Star) });
                var fill = new Grid();
                fill.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(x.Done, 0.0001), GridUnitType.Star) });
                fill.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(x.All - x.Done, 0.0001), GridUnitType.Star) });
                var a = new Border { Background = x.Tag.Length > 0 ? TaskTags.BrushOf(x.Tag) : Muted };
                var b = new Border { Background = x.Tag.Length > 0 ? TaskTags.BrushOf(x.Tag, 80) : Empty };
                Grid.SetColumn(b, 1);
                fill.Children.Add(a);
                fill.Children.Add(b);
                track.Children.Add(new Border { BorderBrush = Ink, BorderThickness = new Thickness(Math.Max(1, P / 2)), Child = fill });
                Grid.SetColumn(track, 1);
                row.Children.Add(track);
                var numbers = new TextBlock { Text = $"{x.Done}/{x.All}" + (x.Spent > 0 ? $" · {TaskCards.Duration(x.Spent)}" : ""), Foreground = Muted, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(numbers, 2);
                row.Children.Add(numbers);
                p.Children.Add(row);
            }
        }

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
                    ("Някога", "За „Някога“", () => MoveWithToast(t, null)),
                    ("✏", "Отвори я (стъпки, ден…)", () =>
                    {
                        SetView(View.Week);
                        Select(t.Id, FocusOn.Title);
                    })));
        }

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

        var finish = Button(review.Done ? "Прегледът е направен ✓" : "Готово – прегледът е направен", () =>
        {
            CommitReview();
            _pet.FinishWeekReview(monday);
        }, primary: !review.Done, icon: "book");
        finish.Margin = new Thickness(0, 16 * U, 0, 0);
        finish.HorizontalAlignment = HorizontalAlignment.Left;
        p.Children.Add(finish);
    }

    private Action? _commitReview;
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
                var back = Small("◀", Act(() => MoveWithToast(t, cur.AddDays(-1))));
                back.ToolTip = Tip("Ден назад (Alt+←)");
                var fwd = Small("▶", Act(() => MoveWithToast(t, cur.AddDays(1))));
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
        var chips = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
        foreach (var (text, tip, click) in buttons)
        {
            var chip = Chip(text, Act(click));
            chip.Padding = new Thickness(5, 0, 5, 1);
            chip.Margin = new Thickness(3, 0, 0, 0);
            chip.ToolTip = Tip(tip);
            chips.Children.Add(chip);
        }
        DockPanel.SetDock(chips, Dock.Right);
        row.Children.Add(chips);
        row.Children.Add(new TextBlock
        {
            Text = t.Title,
            TextWrapping = TextWrapping.Wrap,
            TextDecorations = t.Done ? TextDecorations.Strikethrough : null,
            Foreground = t.Done ? Muted : Ink,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 1, 4, 0),
        });
        return row;
    }

    // ── План за деня ──

    private UIElement Plan()
    {
        var today = Today;
        var state = _pet.TaskState;
        var p = new StackPanel();
        p.Children.Add(SideHeader("План за " + DayName(today) + ", " + today.ToString("d MMMM", Bg), () => ShowSide(Side.None)));
        p.Children.Add(new TextBlock
        {
            Text = "Три кратки стъпки: какво да правим с вчерашните, колко време имаш и какво да добавим.",
            Foreground = Muted,
            TextWrapping = TextWrapping.Wrap,
        });

        // 1. От минали дни.
        var rolled = state.Open.Where(t => !t.Done && t.Day == today && t.Rolled > 0).ToList();
        p.Children.Add(Label($"1. Пренесени от минали дни · {rolled.Count}"));
        if (rolled.Count == 0) p.Children.Add(MenuWindow.MutedText("Няма – чисто начало!"));
        foreach (var t in rolled)
            p.Children.Add(MiniRow(t,
                ("Утре", "За утре", () => MoveWithToast(t, today.AddDays(1))),
                ("Някога", "За „Някога“", () => MoveWithToast(t, null)),
                ("✕", "Изтрий", () => _pet.DeleteTask(t))));

        // 2. Колко време.
        var todays = state.OnDay(today).ToList();
        int planned = todays.Sum(t => t.Minutes), doneMinutes = todays.Where(t => t.Done).Sum(t => t.Minutes);
        p.Children.Add(Label("2. Колко часа работа имаш днес?"));
        var hours = new WrapPanel();
        foreach (int h in new[] { 2, 3, 4, 5, 6, 7, 8, 10 })
        {
            int hh = h;
            var chip = Small(h + "ч", Act(() =>
            {
                _pet.Save.PlannerHours = hh;
                _pet.Persist();
                Refresh(true);
            }));
            SetOn(chip, _pet.Save.PlannerHours == h);
            hours.Children.Add(chip);
        }
        p.Children.Add(hours);
        p.Children.Add(LoadBar(planned, doneMinutes, CapacityMinutes, compact: false));
        int unknown = todays.Count(t => !t.Done && t.Minutes == 0);
        if (unknown > 0)
            p.Children.Add(new TextBlock
            {
                Text = unknown == 1 ? "1 задача е без време – цъкни я и кажи колко ще отнеме." : $"{unknown} задачи са без време – цъкни ги и кажи колко ще отнемат.",
                Foreground = Muted,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4 * U, 0, 0),
            });

        // 3. От „Някога“.
        var someday = state.Someday.ToList();
        p.Children.Add(Label($"3. Нещо от „Някога“? · {someday.Count}"));
        if (someday.Count == 0) p.Children.Add(MenuWindow.MutedText("„Някога“ е празно."));
        foreach (var t in someday.OrderBy(t => t.Star ? 0 : 1).Take(12))
            p.Children.Add(MiniRow(t, ("+ Днес", "Сложи я за днес", () => MoveWithToast(t, today))));
        if (someday.Count > 12) p.Children.Add(MenuWindow.MutedText($"… и още {someday.Count - 12} (в колоната вляво)."));

        var go = Button("Готово – почваме!", Act(() =>
        {
            _pet.PlannedToday();
            ShowSide(Side.None);
        }), primary: true, icon: "sun");
        go.Margin = new Thickness(0, 14 * U, 0, 0);
        go.HorizontalAlignment = HorizontalAlignment.Stretch;
        p.Children.Add(go);
        return p;
    }

    // ── Край на деня ──

    private UIElement Shutdown()
    {
        var today = Today;
        var state = _pet.TaskState;
        var p = new StackPanel();
        p.Children.Add(SideHeader("Край на деня", () => ShowSide(Side.None)));

        var todays = state.OnDay(today).ToList();
        var done = todays.Where(t => t.Done).ToList();
        var left = todays.Where(t => !t.Done).ToList();
        int spent = todays.Sum(t => t.Spent);
        p.Children.Add(new TextBlock
        {
            Text = done.Count == 0 ? "Днес нищо не е отметнато – и това е наред."
                : $"Свършени {done.Count}" + (spent > 0 ? $" · работено {TaskCards.Duration(spent)}" : "") + $" · +{done.Sum(t => t.Reward)} лешника",
            TextWrapping = TextWrapping.Wrap,
            FontWeight = FontWeights.Bold,
        });
        foreach (var t in done) p.Children.Add(MiniRow(t));

        p.Children.Add(Label($"Остава · {left.Count}"));
        if (left.Count == 0) p.Children.Add(MenuWindow.MutedText("Нищо не остава. Браво!"));
        foreach (var t in left)
            p.Children.Add(MiniRow(t,
                ("Утре", "За утре", () => MoveWithToast(t, today.AddDays(1))),
                ("Някога", "За „Някога“", () => MoveWithToast(t, null))));
        if (left.Count > 1)
        {
            var allTomorrow = Button("Всички за утре", Act(() => MoveAll(today, today.AddDays(1))), icon: "arrow_right");
            allTomorrow.HorizontalAlignment = HorizontalAlignment.Left;
            allTomorrow.Margin = new Thickness(0, 4 * U, 0, 0);
            p.Children.Add(allTomorrow);
        }

        p.Children.Add(Label("Как мина денят? (по избор – вижда се после под деня)"));
        _pet.Save.DayNotes.TryGetValue(today.ToString("yyyy-MM-dd"), out var old);
        // Написаното се пази, докато панелът се строи наново (копчетата горе местят задачи).
        var note = Input(_shutdownDraft ?? old ?? "", multiline: true, 2000);
        note.TextChanged += (_, _) => _shutdownDraft = note.Text;
        note.MinHeight = 70 * U;
        note.MaxHeight = 180 * U;
        p.Children.Add(note);

        var close = Button("Затвори деня", Act(() =>
        {
            _pet.ShutdownToday(note.Text);
            _shutdownDraft = null;
            ShowSide(Side.None);
        }), primary: true, icon: "moon");
        close.Margin = new Thickness(0, 12 * U, 0, 0);
        close.HorizontalAlignment = HorizontalAlignment.Stretch;
        p.Children.Add(close);
        return p;
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
    internal int TodayCards => FindCards(_todayPanel);
    internal void TestScrollToDay(DateTime day) => ScrollToDay(day, smooth: false);
    internal void TestSelect(Guid? id) => Select(id);
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
