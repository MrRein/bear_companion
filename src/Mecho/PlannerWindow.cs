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
public sealed class PlannerWindow : Window
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
    private readonly Border _todayChip, _planChip, _shutdownChip;
    private readonly Border _toast;
    private readonly TextBlock _toastText = new() { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 420 };
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

    private PinState State => _pet.Save.Planner;
    private DateTime Today => _pet.TaskToday;
    private int CapacityMinutes => Math.Clamp(_pet.Save.PlannerHours, 1, 16) * 60;

    public PlannerWindow(PetWindow pet)
    {
        _pet = pet;
        _from = WeekOf(pet.TaskToday).AddDays(-7);
        _count = 42;
        Title = "Задачи – Мечо";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        ShowInTaskbar = true;
        FontFamily = GameFont;
        FontSize = Body;
        Foreground = Ink;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        MinWidth = 640 * U;
        MinHeight = 400 * U;
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.Aliased);
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        Resources.Add(typeof(ScrollBar), ScrollBarStyle());
        WindowStartupLocation = WindowStartupLocation.Manual;
        try { Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/mecho.ico")); } catch (Exception) { }

        // Мястото и размерът – запомнени; отначало почти целият екран, в средата.
        var area = SystemParameters.WorkArea;
        var s = State;
        Width = s.W > 0 ? Math.Min(s.W, area.Width) : Math.Min(area.Width - 60, Step * 5 + ColumnWidth + 330 * U);
        Height = s.H > 0 ? Math.Min(s.H, area.Height) : Math.Min(area.Height - 60, 680 * U);
        Left = double.IsNaN(s.X) ? area.Left + (area.Width - Width) / 2 : Math.Clamp(s.X, area.Left - Width + 80, area.Right - 80);
        Top = double.IsNaN(s.Y) ? area.Top + (area.Height - Height) / 2 : Math.Clamp(s.Y, area.Top, area.Bottom - 60);

        // ── Горе, първи ред: Задачи, ◀ Днес ▶, кои дни се виждат … План, Край, ?, смали, ✕ ──
        var top = new DockPanel { Margin = new Thickness(4, 0, 2, 4), Background = Brushes.Transparent, Cursor = Cursors.SizeAll };
        var close = HeaderChip("✕", Close, "Затвори");
        var minimize = HeaderChip("", () => WindowState = WindowState.Minimized, "Смали в лентата долу");
        minimize.Child = Icon("minimize");
        var help = HeaderChip("", () => { }, "");
        help.Child = Icon("question");
        help.ToolTip = Tip(
            "Бързо писане: „Скица #рисуване 30м !“ – етикет, време, важна.\n" +
            "Клавиши: N – нова за днес · T – днес · ← → – дни · Ctrl+F – търси · Ctrl+Z – отмени.\n" +
            "Избрана задача: Space – свършена · Alt+← → – ден назад/напред · S – важна · Ctrl+D – копие · Enter – име · Delete – изтрий.\n" +
            "Колелцето над дните ги мести встрани (Shift+колелце – винаги встрани).");
        ToolTipService.SetShowDuration(help, 60000);
        ToolTipService.SetInitialShowDelay(help, 100);
        _shutdownChip = HeaderChip("", () => ShowSide(_sideMode == Side.Shutdown ? Side.None : Side.Shutdown), "Край на деня: какво стана, какво остава за утре, как мина денят");
        _shutdownChip.Child = IconText("moon", "Край на деня", bold: true);
        _planChip = HeaderChip("", () => ShowSide(_sideMode == Side.Plan ? Side.None : Side.Plan), "План за деня: вчерашните, колко време имаш, какво от „Някога“");
        _planChip.Child = IconText("sun", "План за деня", bold: true);
        foreach (var b in new[] { close, minimize, help, _shutdownChip, _planChip })
        {
            DockPanel.SetDock(b, Dock.Right);
            top.Children.Add(b);
        }
        _shutdownChip.Margin = _planChip.Margin = new Thickness(6, 0, 0, 0);
        var title = IconText("tasks", "Задачи", bold: true);
        System.Windows.Documents.TextElement.SetForeground(title, Brushes.White);
        title.Margin = new Thickness(0, 0, 12, 0);
        DockPanel.SetDock(title, Dock.Left);
        top.Children.Add(title);
        var prev = HeaderChip("◀", () => ScrollBy(-7), "Седмица назад (←: ден назад)");
        _todayChip = HeaderChip("Днес", () => ScrollToDay(Today), "Към днес (T)");
        var next = HeaderChip("▶", () => ScrollBy(7), "Седмица напред (→: ден напред)");
        foreach (var b in new[] { prev, _todayChip, next })
        {
            b.Margin = new Thickness(0, 0, 4, 0);
            DockPanel.SetDock(b, Dock.Left);
            top.Children.Add(b);
        }
        _rangeLabel.Margin = new Thickness(8, 0, 8, 0);
        top.Children.Add(_rangeLabel);
        top.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                return;
            }
            try { DragMove(); } catch (InvalidOperationException) { }
        };

        // ── Втори ред: търсене, етикети, само важните, скрий свършените … обобщение ──
        var bar = new DockPanel { Margin = new Thickness(4, 0, 2, 6) };
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
        var searchBox = new Border { Width = 190 * U, Margin = new Thickness(0, 0, 8, 0), Child = MenuWindow.WithHint(_search, "Търси…  (Ctrl+F)") };
        DockPanel.SetDock(searchBox, Dock.Left);
        bar.Children.Add(searchBox);
        DockPanel.SetDock(_filters, Dock.Left);
        bar.Children.Add(_filters);
        _summary.Margin = new Thickness(8, 0, 4, 0);
        bar.Children.Add(_summary);

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
        _someday.Width = ColumnWidth;
        _someday.Margin = new Thickness(0, 0, 10 * U, 0);

        var body = new DockPanel();
        DockPanel.SetDock(_someday, Dock.Left);
        DockPanel.SetDock(_side, Dock.Right);
        body.Children.Add(_someday);
        body.Children.Add(_side);
        body.Children.Add(_dayScroll);

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

        var paper = new Border
        {
            Background = PaperTexture,
            BorderBrush = Ink,
            BorderThickness = Flat,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10 * U, 10 * U, 10 * U, 6 * U),
            Child = bodyLayer,
        }.Skinned("card");

        var root = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        DockPanel.SetDock(bar, Dock.Top);
        root.Children.Add(top);
        root.Children.Add(bar);
        root.Children.Add(paper);
        Content = new Border
        {
            Background = WoodTexture,
            BorderBrush = Ink,
            BorderThickness = Flat,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 6, 8, 8),
            Child = root,
        }.Skinned("frame");

        PreviewKeyDown += OnKey;
        LocationChanged += (_, _) => Remember();
        SizeChanged += (_, _) => Remember();
        _pet.CareChanged += OnCareChanged;
        _pet.TaskRemoved += OnTaskRemoved;
        SourceInitialized += (_, _) => HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(WndProc);
        Closed += (_, _) =>
        {
            _pet.CareChanged -= OnCareChanged;
            _pet.TaskRemoved -= OnTaskRemoved;
            _scrollTimer.Stop();
            _toastTimer.Stop();
            Remember();
        };
        Loaded += (_, _) => Dispatcher.BeginInvoke(() => ScrollToDay(Today, smooth: false), DispatcherPriority.Loaded);
        Refresh(true);
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

    private void Remember()
    {
        if (WindowState != WindowState.Normal || ActualWidth <= 0) return;
        var s = State;
        s.X = Left;
        s.Y = Top;
        s.W = ActualWidth;
        s.H = ActualHeight;
    }

    /// <summary>Отваря от вън плана или края на деня.</summary>
    public void OpenSide(string side)
    {
        ShowSide(side == "plan" ? Side.Plan : side == "shutdown" ? Side.Shutdown : Side.None);
        if (side is "plan" or "shutdown") ScrollToDay(Today);
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

    /// <summary>Тъчпадът и мишките с колелце встрани (WM_MOUSEHWHEEL).</summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_MOUSEHWHEEL = 0x020E;
        if (msg == WM_MOUSEHWHEEL)
        {
            int delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
            ScrollTo((double.IsNaN(_target) ? _dayScroll.HorizontalOffset : _target) + delta * 1.1, smooth: true);
            handled = true;
        }
        return IntPtr.Zero;
    }

    /// <summary>Близо до края на лентата – още дни (две седмици) отзад или отпред.</summary>
    private void OnScrolled()
    {
        UpdateRangeLabel();
        if (_extending || _dayScroll.ViewportWidth <= 0) return;
        double offset = _dayScroll.HorizontalOffset;
        if (offset < Step * 2)
        {
            _extending = true;
            _from = _from.AddDays(-14);
            _count += 14;
            Refresh(true);
            _dayScroll.UpdateLayout();
            _dayScroll.ScrollToHorizontalOffset(offset + 14 * Step);
            if (!double.IsNaN(_target)) _target += 14 * Step;
            _extending = false;
        }
        else if (offset + _dayScroll.ViewportWidth > _dayScroll.ExtentWidth - Step * 2)
        {
            _extending = true;
            _count += 14;
            Refresh(true);
            _extending = false;
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
            _count = IndexOf(day) + 15;
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

    /// <summary>Строи наново това, което се е сменило (всяка колона поотделно).</summary>
    public void Refresh(bool force = false)
    {
        if (TaskCards.Dragging) return;   // по време на влачене – не (картичката, която се влачи, ще изчезне)
        var state = _pet.TaskState;
        var today = Today;
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
        UpdateRitualChips();
        BuildSide();
        UpdateRangeLabel();
        RestoreFocus();
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
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(TaskTags.Swatch(key));
            row.Children.Add(new TextBlock { Text = tag.Name, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            chip.Child = row;
            chip.ToolTip = Tip($"Само „{tag.Name}“ (новите задачи също ще са с този етикет)");
            SetOn(chip, _filter == key);
            _filters.Children.Add(chip);
        }
        var star = Chip("", () => SetFilter(_filter, !_starOnly));
        star.Child = IconText("star", "Важни", bold: true);
        star.ToolTip = Tip("Само важните");
        SetOn(star, _starOnly);
        _filters.Children.Add(star);
        var hideDone = Chip("", () =>
        {
            _pet.Save.PlannerHideDone = !_pet.Save.PlannerHideDone;
            _pet.Persist();
            Refresh(true);
        });
        hideDone.Child = IconText("check", "Скрий свършените", bold: true);
        hideDone.ToolTip = Tip("Свършените задачи не се показват в дните");
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
        if (tasks.Count > 0) parts.Add($"Тази седмица: {done} от {tasks.Count}");
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

    /// <summary>„План за деня“ свети сутрин, докато денят не е планиран; „Край на деня“ – вечер.</summary>
    private void UpdateRitualChips()
    {
        string key = Today.ToString("yyyy-MM-dd");
        int hour = DateTime.Now.Hour;
        bool plan = _pet.Save.PlannedDay != key && hour < 15;
        bool shutdown = _pet.Save.ShutdownDay != key && hour >= 17;
        UiSkin.Set(_planChip, _sideMode == Side.Plan ? "button_on" : plan ? "button_honey" : "button");
        UiSkin.Set(_shutdownChip, _sideMode == Side.Shutdown ? "button_on" : shutdown ? "button_honey" : "button");
        _planChip.Background = plan ? Honey : PaperDark;
        _shutdownChip.Background = shutdown ? Honey : PaperDark;
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
            line.Children.Add(count);
        }
        string name = day is { } d0 ? Capital(d0.ToString("dddd", Bg)) : "Някога";
        line.Children.Add(new TextBlock { Text = name, FontWeight = FontWeights.Bold, FontSize = Body * 1.15, Foreground = Ink, TextTrimming = TextTrimming.CharacterEllipsis });
        head.Children.Add(line);

        var sub = new DockPanel { Margin = new Thickness(0, 2 * U, 0, 0) };
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
        head.ContextMenu = ColumnMenu(day, tasks);
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
            var q = TaskQuick.Parse(add.Text);
            add.Clear();
            _refocus = (true, day);
            var first = _pet.TaskState.Open.FirstOrDefault(t => t.Day == day && !t.Done);
            _pet.AddTask(q.Title, 1, null, day, someday: day == null, tag: q.Tag ?? (_filter.Length > 0 ? _filter : null),
                star: q.Star || _starOnly, minutes: q.Minutes, quiet: true, before: first?.Id);
        };
        var addBox = WithPlus(add, day == null ? "Някога…" : "Задача…  (#етикет 30м !)");
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
            Text = planned == 0 ? $"свободно · {capacity / 60}ч за деня" : $"~{TaskCards.Duration(planned)} от {capacity / 60}ч" + (over ? " – много е!" : ""),
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
    private ContextMenu? ColumnMenu(DateTime? day, List<TaskItem> tasks)
    {
        var menu = new ContextMenu { FontFamily = GameFont, FontSize = Body, Foreground = Ink, Background = Cream, BorderBrush = Ink, BorderThickness = Px(1) };
        MenuItem Item(string text, Action click)
        {
            var item = new MenuItem { Header = text, FontFamily = GameFont, FontSize = Body, Foreground = Ink };
            item.Click += (_, _) => click();
            return item;
        }
        menu.Items.Add(Item("Нова задача тук", () => FocusAdd(day)));
        if (day is { } from && tasks.Any(t => !t.Done))
        {
            var today = Today;
            if (from != today) menu.Items.Add(Item("Всички недовършени – за днес", () => MoveAll(from, today)));
            menu.Items.Add(Item("Всички недовършени – за утре", () => MoveAll(from, (from < today ? today : from).AddDays(1))));
            menu.Items.Add(Item("Всички недовършени – за „Някога“", () => MoveAll(from, null)));
        }
        return menu;
    }

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
        if (day is { } d) ScrollToDay(d);
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

    private void OnKey(object sender, KeyEventArgs e)
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
            case Key.T or Key.Home when mods == ModifierKeys.None:
                ScrollToDay(Today);
                break;
            case Key.Left when mods == ModifierKeys.None:
                ScrollBy(-1);
                break;
            case Key.Right when mods == ModifierKeys.None:
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
        _side.Child = new Border
        {
            Background = Tile,
            BorderBrush = WoodDark,
            BorderThickness = Flat,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12 * U, 10 * U, 8 * U, 10 * U),
            Child = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(0, 0, 4 * U, 0) },
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

            // Повтаря се.
            p.Children.Add(Label("Повтаря се"));
            var repeats = new WrapPanel();
            (string Key, string Name)[] kinds =
            {
                (TaskRepeat.None, "Не"), (TaskRepeat.Daily, "Всеки ден"), (TaskRepeat.Weekdays, "Делнични"),
                (TaskRepeat.Weekly, "Всяка седмица"), (TaskRepeat.Monthly, "Всеки месец"),
            };
            foreach (var (k, n) in kinds)
            {
                var kind = k;
                var chip = Small(n, Act(() => _pet.SetTaskRepeat(t, kind)));
                SetOn(chip, t.Repeat == k);
                repeats.Children.Add(chip);
            }
            p.Children.Add(repeats);

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
        var actions = new UniformGrid { Columns = 2, Margin = new Thickness(0, 14 * U, 0, 0) };
        if (open)
        {
            bool current = _pet.InFocus && _pet.FocusTask?.Id == t.Id;
            var focus = Button(current ? "Работим!" : "Мечо-доро", Act(() => { if (!current) _pet.StartFocusOn(t); }), primary: true, icon: "honey");
            focus.ToolTip = Tip(current ? "По нея се работи сега" : "Пусни мечо-доро по тази задача (времето се записва към нея)");
            actions.Children.Add(focus);
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
        foreach (FrameworkElement b in actions.Children) b.Margin = new Thickness(0, 0, 4 * U, 4 * U);
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
