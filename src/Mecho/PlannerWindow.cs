using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using static Mecho.Ui;

namespace Mecho;

/// <summary>
/// Седмицата със задачите: вляво „Някога“ (без ден), после седемте дни от понеделник до
/// неделя. Задачите се влачат между дните и нагоре-надолу в деня; всеки ден показва колко
/// е свършено и колко време е планирано (червено, ако е твърде много); долу във всеки ден
/// се пише нова задача (Enter). Цъкната задача се отваря вдясно – там се поправя и маркира
/// (етикет, важна, време, големина, кой ден). Етикетите отгоре филтрират. Недовършените от
/// минали дни сами идват за днес (ядрото, TasksModule).
/// </summary>
public sealed class PlannerWindow : Window
{
    private readonly PetWindow _pet;
    private readonly Grid _days = new();
    private readonly ScrollViewer _dayScroll;
    private readonly Border _someday = new();
    private readonly Border _details;
    private readonly TextBlock _weekLabel = new() { FontWeight = FontWeights.Bold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _weekSummary = new() { Foreground = Brushes.White, Opacity = 0.85, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
    private readonly WrapPanel _filters = new() { VerticalAlignment = VerticalAlignment.Center };
    private DateTime _week;
    private Guid? _selected;
    private string _filter = "";
    private string _shown = "", _detailsShown = "";
    /// <summary>След добавяне: полето на кой ден да вземе фокуса пак (за писане една след друга).</summary>
    private (bool Has, DateTime? Day) _refocus;
    private readonly Dictionary<string, TextBox> _addBoxes = new();

    private static readonly CultureInfo Bg = new("bg-BG");
    private static double U => Body / 18;
    private static double ColumnWidth => 196 * U;
    private const int MaxDayMinutes = 8 * 60;

    /// <summary>Понеделникът на седмицата, в която е денят.</summary>
    public static DateTime WeekOf(DateTime day)
    {
        int back = ((int)day.DayOfWeek + 6) % 7;
        return day.Date.AddDays(-back);
    }

    private PinState State => _pet.Save.Planner;

    public PlannerWindow(PetWindow pet)
    {
        _pet = pet;
        _week = WeekOf(pet.TaskToday);
        Title = "Седмица – Мечо";
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
        MinWidth = 560 * U;
        MinHeight = 360 * U;
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.Aliased);
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        Resources.Add(typeof(ScrollBar), ScrollBarStyle());
        WindowStartupLocation = WindowStartupLocation.Manual;
        try { Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/mecho.ico")); } catch (Exception) { }

        // Мястото и размерът – запомнени; отначало почти целият екран, в средата.
        var area = SystemParameters.WorkArea;
        var s = State;
        Width = s.W > 0 ? Math.Min(s.W, area.Width) : Math.Min(area.Width - 60, ColumnWidth * 8 + 300 * U);
        Height = s.H > 0 ? Math.Min(s.H, area.Height) : Math.Min(area.Height - 60, 640 * U);
        Left = double.IsNaN(s.X) ? area.Left + (area.Width - Width) / 2 : Math.Clamp(s.X, area.Left - Width + 80, area.Right - 80);
        Top = double.IsNaN(s.Y) ? area.Top + (area.Height - Height) / 2 : Math.Clamp(s.Y, area.Top, area.Bottom - 60);

        // ── Горе: иконка, седмицата, ◀ Днес ▶, етикетите, свий, ✕ (влачи се оттук) ──
        var header = new DockPanel { Margin = new Thickness(4, 0, 2, 6), Background = Brushes.Transparent, Cursor = Cursors.SizeAll };
        var close = Chip("✕", Close);
        close.ToolTip = Tip("Затвори седмицата");
        var minimize = Chip("", () => WindowState = WindowState.Minimized);
        minimize.Child = Icon("minimize");
        minimize.ToolTip = Tip("Смали в лентата долу");
        foreach (var b in new[] { close, minimize })
        {
            b.Margin = new Thickness(4, 0, 0, 0);
            b.Padding = new Thickness(6, 1, 6, 2);
            DockPanel.SetDock(b, Dock.Right);
            header.Children.Add(b);
        }
        _filters.Margin = new Thickness(0, 0, 10, 0);
        DockPanel.SetDock(_filters, Dock.Right);
        header.Children.Add(_filters);

        var title = IconText("tasks", "Седмица", bold: true);
        System.Windows.Documents.TextElement.SetForeground(title, Brushes.White);
        title.Margin = new Thickness(0, 0, 12, 0);
        header.Children.Add(title);
        var prev = Chip("◀", () => GoWeek(-7));
        prev.ToolTip = Tip("Миналата седмица");
        var todayChip = Chip("Днес", () => { _week = WeekOf(_pet.TaskToday); Refresh(true); });
        todayChip.ToolTip = Tip("Тази седмица");
        var next = Chip("▶", () => GoWeek(7));
        next.ToolTip = Tip("Следващата седмица");
        foreach (var b in new[] { prev, todayChip, next })
        {
            b.Margin = new Thickness(0, 0, 4, 0);
            b.Padding = new Thickness(7, 1, 7, 2);
            b.VerticalAlignment = VerticalAlignment.Center;
            header.Children.Add(b);
        }
        _weekLabel.Margin = new Thickness(8, 0, 0, 0);
        header.Children.Add(_weekLabel);
        header.Children.Add(_weekSummary);
        header.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                return;
            }
            try { DragMove(); } catch (InvalidOperationException) { }
        };

        // ── Тялото: „Някога“ | дните (превъртат се встрани) | подробностите ──
        _dayScroll = new ScrollViewer
        {
            Content = _days,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        _dayScroll.SizeChanged += (_, _) => FitDays();
        // При влачене до левия/десния ръб – дните се превъртат.
        _dayScroll.PreviewDragOver += (_, e) =>
        {
            var p = e.GetPosition(_dayScroll);
            if (p.X < 30 * U) _dayScroll.ScrollToHorizontalOffset(_dayScroll.HorizontalOffset - 14 * U);
            else if (p.X > _dayScroll.ActualWidth - 30 * U) _dayScroll.ScrollToHorizontalOffset(_dayScroll.HorizontalOffset + 14 * U);
        };
        // Колелцето над заглавията на дните мести встрани (в самите списъци – надолу).
        _dayScroll.PreviewMouseWheel += (_, e) =>
        {
            if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) return;
            _dayScroll.ScrollToHorizontalOffset(_dayScroll.HorizontalOffset - e.Delta / 2.0);
            e.Handled = true;
        };

        _details = new Border
        {
            Width = 290 * U,
            Margin = new Thickness(8 * U, 0, 0, 0),
            Visibility = Visibility.Collapsed,
        };
        _someday.Width = ColumnWidth;
        _someday.Margin = new Thickness(0, 0, 8 * U, 0);

        var body = new DockPanel();
        DockPanel.SetDock(_someday, Dock.Left);
        DockPanel.SetDock(_details, Dock.Right);
        body.Children.Add(_someday);
        body.Children.Add(_details);
        body.Children.Add(_dayScroll);

        var paper = new Border
        {
            Background = PaperTexture,
            BorderBrush = Ink,
            BorderThickness = Flat,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8 * U),
            Child = body,
        }.Skinned("card");

        var root = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
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

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && Keyboard.FocusedElement is not TextBox)
            {
                if (_selected != null) Select(null);
                else Close();
                e.Handled = true;
            }
        };
        LocationChanged += (_, _) => Remember();
        SizeChanged += (_, _) => Remember();
        _pet.CareChanged += OnCareChanged;
        Closed += (_, _) =>
        {
            _pet.CareChanged -= OnCareChanged;
            Remember();
        };
        Refresh(true);
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

    private void GoWeek(int days)
    {
        _week = _week.AddDays(days);
        Refresh(true);
    }

    /// <summary>Дните запълват мястото; ако е тясно – по-широки от него и се превъртат встрани.</summary>
    private void FitDays()
    {
        double need = ColumnWidth * 7 + 6 * 8 * U;
        double room = _dayScroll.ViewportWidth > 0 ? _dayScroll.ViewportWidth : _dayScroll.ActualWidth;
        double width = Math.Max(need, room);
        if (Math.Abs(_days.Width - width) > 0.5 || double.IsNaN(_days.Width)) _days.Width = width;
    }

    // ───────────────────────── Строене ─────────────────────────

    /// <summary>Строи наново, само ако нещо се е сменило (или force).</summary>
    public void Refresh(bool force = false)
    {
        if (TaskCards.Dragging) return;   // по време на влачене – не (картичката, която се влачи, ще изчезне)
        var state = _pet.TaskState;
        var today = _pet.TaskToday;
        string sig = string.Join("|", state.Open.Select(t => $"{t.Id}{t.Title}{t.Description}{t.Size}{t.Done}{t.Day:yyyyMMdd}{t.Tag}{t.Star}{t.Minutes}{t.Rolled}{_pet.TaskOnBoard(t)}"))
            + $"#{state.History.Count}#{_week:yyyyMMdd}#{today:yyyyMMdd}#{_filter}#{_selected}#{_pet.Save.UiTheme}";
        if (!force && sig == _shown)
        {
            BuildDetails();
            return;
        }
        _shown = sig;
        BuildFilters();
        BuildWeek(state, today);
        BuildDetails();
        RestoreFocus();
    }

    private void BuildFilters()
    {
        _filters.Children.Clear();
        var all = Chip("Всички", () => SetFilter(""));
        SetOn(all, _filter.Length == 0);
        all.ToolTip = Tip("Всички задачи");
        _filters.Children.Add(all);
        foreach (var tag in TaskTags.All)
        {
            var key = tag.Key;
            var chip = Chip("", () => SetFilter(_filter == key ? "" : key));
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(TaskTags.Swatch(key));
            row.Children.Add(new TextBlock { Text = tag.Name, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            chip.Child = row;
            chip.ToolTip = Tip($"Само „{tag.Name}“ (новите задачи също ще са с този етикет)");
            SetOn(chip, _filter == key);
            _filters.Children.Add(chip);
        }
        foreach (FrameworkElement c in _filters.Children)
        {
            c.Margin = new Thickness(0, 0, 4, 0);
            if (c is Border b) b.Padding = new Thickness(6, 1, 6, 2);
        }
    }

    private void SetFilter(string key)
    {
        _filter = key;
        Refresh(true);
    }

    private bool Passes(TaskItem t) => _filter.Length == 0 || t.Tag == _filter;

    private void BuildWeek(Mecho.Core.TasksState state, DateTime today)
    {
        _addBoxes.Clear();
        var end = _week.AddDays(6);
        _weekLabel.Text = _week.Month == end.Month
            ? $"{_week.Day} – {end.ToString("d MMMM yyyy", Bg)}"
            : $"{_week.ToString("d MMMM", Bg)} – {end.ToString("d MMMM yyyy", Bg)}";
        var weekTasks = Enumerable.Range(0, 7).SelectMany(i => state.OnDay(_week.AddDays(i))).Where(Passes).ToList();
        int minutes = weekTasks.Where(t => !t.Done).Sum(t => t.Minutes);
        _weekSummary.Text = weekTasks.Count == 0 ? ""
            : $"·  свършени {weekTasks.Count(t => t.Done)} от {weekTasks.Count}" + (minutes > 0 ? $"  ·  остават ~{TaskCards.Duration(minutes)}" : "");

        _someday.Child = Column("Някога", "без ден", null, state.Someday.Where(Passes).ToList(), today: false, past: false);

        _days.Children.Clear();
        _days.ColumnDefinitions.Clear();
        for (int i = 0; i < 7; i++)
        {
            var day = _week.AddDays(i);
            _days.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            string name = Capital(day.ToString("dddd", Bg));
            string sub = day == today ? "днес" : day == today.AddDays(1) ? "утре" : day == today.AddDays(-1) ? "вчера" : day.ToString("d MMMM", Bg);
            if (day == today || day == today.AddDays(1) || day == today.AddDays(-1)) sub += " · " + day.ToString("d MMM", Bg);
            var col = Column(name, sub, day, state.OnDay(day).Where(Passes).ToList(), today: day == today, past: day < today);
            col.Margin = new Thickness(i == 0 ? 0 : 4 * U, 0, i == 6 ? 0 : 4 * U, 0);
            Grid.SetColumn(col, i);
            _days.Children.Add(col);
        }
        FitDays();
    }

    /// <summary>Една колона: заглавие (ден, дата, свършени, време), картичките, поле за нова.</summary>
    private Border Column(string name, string sub, DateTime? day, List<TaskItem> tasks, bool today, bool past)
    {
        var dock = new DockPanel();

        // Заглавието.
        var head = new StackPanel { Margin = new Thickness(2 * U, 0, 2 * U, 6 * U) };
        var line = new DockPanel();
        int done = tasks.Count(t => t.Done), all = tasks.Count;
        if (all > 0)
        {
            var count = new TextBlock { Text = $"{done}/{all}", Foreground = done == all ? Good : Muted, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Bottom };
            DockPanel.SetDock(count, Dock.Right);
            line.Children.Add(count);
        }
        line.Children.Add(new TextBlock { Text = name, FontWeight = FontWeights.Bold, FontSize = Body * 1.15, Foreground = Ink });
        head.Children.Add(line);
        int minutes = tasks.Where(t => !t.Done).Sum(t => t.Minutes);
        var subline = new DockPanel();
        if (minutes > 0)
        {
            bool tooMuch = day != null && minutes > MaxDayMinutes;
            var time = new TextBlock
            {
                Text = "~" + TaskCards.Duration(minutes),
                Foreground = tooMuch ? Bad : Muted,
                FontWeight = tooMuch ? FontWeights.Bold : FontWeights.Normal,
                ToolTip = Tip(tooMuch ? $"Над {MaxDayMinutes / 60} часа работа за един ден – премести нещо за друг ден." : "Толкова време остава (по това, което си написала за задачите)."),
            };
            DockPanel.SetDock(time, Dock.Right);
            subline.Children.Add(time);
        }
        subline.Children.Add(new TextBlock { Text = sub, Foreground = today ? Ink : Muted });
        head.Children.Add(subline);
        // Лентичка: колко от деня е свършено.
        if (all > 0 && day != null)
        {
            var bar = new Grid { Height = 1.5 * P, Margin = new Thickness(0, 4 * U, 0, 0) };
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(done, 0.0001), GridUnitType.Star) });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(all - done, 0.0001), GridUnitType.Star) });
            var fill = new Border { Background = done == all ? Good : Honey };
            var rest = new Border { Background = Empty };
            Grid.SetColumn(rest, 1);
            bar.Children.Add(fill);
            bar.Children.Add(rest);
            head.Children.Add(new Border { BorderBrush = Ink, BorderThickness = new Thickness(Math.Max(1, P / 2)), Child = bar, Margin = new Thickness(0, 4 * U, 0, 0) });
            bar.Margin = new Thickness(0);
        }
        int rolled = tasks.Count(t => !t.Done && t.Rolled > 0);
        if (today && rolled > 0)
        {
            var info = IconText("reroll", rolled == 1 ? "1 пренесена" : $"{rolled} пренесени");
            info.Opacity = 0.75;
            info.Margin = new Thickness(0, 4 * U, 0, 0);
            info.ToolTip = Tip("Недовършени от минали дни – дойдоха сами за днес.");
            head.Children.Add(info);
        }
        DockPanel.SetDock(head, Dock.Top);
        dock.Children.Add(head);

        // Поле за нова задача (Enter – добавя и остава за следващата).
        var add = MenuWindow.NewTextInput();
        add.Padding = new Thickness(6, 4, 6, 4);
        string key = day?.ToString("yyyyMMdd") ?? "someday";
        _addBoxes[key] = add;
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
            string text = add.Text;
            add.Clear();
            _refocus = (true, day);
            _pet.AddTask(text, 1, null, day, someday: day == null, tag: _filter.Length > 0 ? _filter : null, quiet: true);
            e.Handled = true;
        };
        var addBox = WithPlus(add, day == null ? "Някога…" : "Задача…");
        if (past) addBox.Opacity = 0.6;
        DockPanel.SetDock(addBox, Dock.Bottom);
        dock.Children.Add(addBox);

        // Картичките.
        var list = new StackPanel { Background = Brushes.Transparent, MinHeight = 40 * U };
        if (tasks.Count == 0)
            list.Children.Add(new TextBlock
            {
                Text = day == null ? "Тук – нещата без ден. Довлечи ги в ден, когато им дойде времето." : past ? "" : "Довлечи задача тук или напиши отдолу.",
                Foreground = Muted,
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.8,
                Margin = new Thickness(2 * U, 2 * U, 2 * U, 6 * U),
            });
        // Несвършените – отгоре (в реда си), свършените – отдолу.
        foreach (var t in tasks.Where(t => !t.Done).Concat(tasks.Where(t => t.Done)))
            list.Children.Add(TaskCards.Build(_pet, t, new TaskCardOptions
            {
                Select = x => Select(_selected == x.Id ? null : x.Id),
                Selected = _selected == t.Id,
                Description = 60,
                Edit = x => Select(x.Id),
            }));
        var scroll = new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 0, 0, 6 * U) };
        dock.Children.Add(scroll);

        var column = new Border
        {
            Background = today ? Cream : Paper,
            BorderBrush = today ? Ink : WoodDark,
            BorderThickness = today ? Px(1) : Flat,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6 * U, 6 * U, 6 * U, 6 * U),
            Child = dock,
            Opacity = past ? 0.88 : 1,
        };
        TaskCards.DropTarget(column, list, (id, before) =>
        {
            if (_pet.Save.Tasks.FirstOrDefault(t => t.Id == id) is not { } task) return;
            _pet.MoveTask(task, day, before);
            if (_filter.Length > 0 && task.Tag.Length == 0) _pet.MarkTask(task, tag: _filter);
        }, scroll);
        return column;
    }

    /// <summary>Поле с „+“ и бледа подсказка.</summary>
    private static UIElement WithPlus(TextBox box, string hint)
    {
        var grid = new Grid();
        grid.Children.Add(box);
        var plus = IconText("plus", hint);
        plus.IsHitTestVisible = false;
        plus.Opacity = 0.55;
        plus.Margin = new Thickness(8, 0, 0, 0);
        plus.HorizontalAlignment = HorizontalAlignment.Left;
        box.TextChanged += (_, _) => plus.Visibility = box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        box.GotKeyboardFocus += (_, _) => plus.Opacity = 0.35;
        box.LostKeyboardFocus += (_, _) => plus.Opacity = 0.55;
        grid.Children.Add(plus);
        return grid;
    }

    private void RestoreFocus()
    {
        if (!_refocus.Has) return;
        string key = _refocus.Day?.ToString("yyyyMMdd") ?? "someday";
        _refocus = (false, null);
        if (_addBoxes.TryGetValue(key, out var box)) Dispatcher.BeginInvoke(() => box.Focus(), System.Windows.Threading.DispatcherPriority.Input);
    }

    // ───────────────────────── Подробностите (вдясно) ─────────────────────────

    /// <summary>Запазва написаното в подробностите (преди да се избере друга задача или да се цъкне копче).</summary>
    private Action? _commitDetails;
    private bool _detailsDirty;

    private void Select(Guid? id)
    {
        _commitDetails?.Invoke();
        _selected = id;
        _detailsShown = "";
        Refresh(true);
    }

    private void BuildDetails()
    {
        var t = _selected is { } id ? _pet.Save.Tasks.FirstOrDefault(x => x.Id == id) : null;
        if (t == null)
        {
            _commitDetails = null;
            _selected = null;
            _details.Visibility = Visibility.Collapsed;
            _details.Child = null;
            _detailsShown = "";
            return;
        }
        string sig = $"{t}{_pet.TaskOnBoard(t)}{_pet.InFocus}";
        if (sig == _detailsShown) return;
        // Докато се пише в полетата – не се строи наново (ще се изгуби написаното).
        if (_detailsDirty && _details.IsKeyboardFocusWithin && _detailsShown.StartsWith(t.Id.ToString())) return;
        _detailsDirty = false;
        _detailsShown = t.Id + sig;
        _details.Visibility = Visibility.Visible;

        var p = new StackPanel();
        var top = new DockPanel { Margin = new Thickness(0, 0, 0, 8 * U) };
        var close = Chip("✕", () => Select(null));
        close.ToolTip = Tip("Затвори (Esc)");
        close.Padding = new Thickness(6, 1, 6, 2);
        DockPanel.SetDock(close, Dock.Right);
        top.Children.Add(close);
        string when = t.Day is { } d ? Capital(d.ToString("dddd, d MMMM", Bg)) : "Някога";
        top.Children.Add(new TextBlock { Text = when, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
        p.Children.Add(top);

        bool open = !t.Done;
        var title = MenuWindow.NewTextInput();
        title.Text = t.Title;
        title.TextWrapping = TextWrapping.Wrap;
        title.FontWeight = FontWeights.Bold;
        title.IsEnabled = open;
        var text = MenuWindow.NewTextInput();
        text.Text = t.Description;
        text.AcceptsReturn = true;
        text.TextWrapping = TextWrapping.Wrap;
        text.MinHeight = 60 * U;
        text.MaxHeight = 140 * U;
        text.VerticalContentAlignment = VerticalAlignment.Top;
        text.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        text.Margin = new Thickness(0, 6 * U, 0, 0);
        text.IsEnabled = open;
        void Save()
        {
            if (!_detailsDirty) return;
            _detailsDirty = false;
            if (_pet.Save.Tasks.FirstOrDefault(x => x.Id == t.Id) is not { Done: false } cur) return;
            if (title.Text.Trim().Length == 0) title.Text = cur.Title;
            if (title.Text.Trim() == cur.Title && text.Text.Trim() == cur.Description) return;
            _pet.EditTask(cur, title.Text, cur.Size, text.Text);
        }
        title.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            Save();
            e.Handled = true;
        };
        title.LostKeyboardFocus += (_, _) => Save();
        text.LostKeyboardFocus += (_, _) => Save();
        title.TextChanged += (_, _) => _detailsDirty = true;
        text.TextChanged += (_, _) => _detailsDirty = true;
        _commitDetails = Save;
        // Копчетата първо запазват написаното (иначе подробностите не биха се опреснили).
        Action Act(Action a) => () =>
        {
            Save();
            a();
        };
        p.Children.Add(title);
        p.Children.Add(MenuWindow.WithHint(text, "Бележка към задачата…"));

        if (open)
        {
            // Кога.
            var today = _pet.TaskToday;
            p.Children.Add(Label("Кога"));
            var whenRow = new WrapPanel();
            (string Name, DateTime? Day)[] days =
            {
                ("Днес", today), ("Утре", today.AddDays(1)), ("Следв. седмица", WeekOf(today).AddDays(7)), ("Някога", null),
            };
            foreach (var (n, day) in days)
            {
                var target = day;
                var chip = Small(n, Act(() => _pet.MoveTask(t, target)));
                SetOn(chip, t.Day == day);
                whenRow.Children.Add(chip);
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

            // Важна.
            p.Children.Add(Label("Важна ли е?"));
            var star = Small("", Act(() => _pet.MarkTask(t, star: !t.Star)));
            star.Child = IconText("star", t.Star ? "Важна" : "Не е важна", bold: true);
            star.HorizontalAlignment = HorizontalAlignment.Left;
            SetOn(star, t.Star);
            p.Children.Add(star);

            // Големина.
            p.Children.Add(Label("Каква е?"));
            var sizes = new WrapPanel();
            for (int i = 1; i <= 3; i++)
            {
                int size = i;
                var chip = Small(TaskCards.SizeNames[i - 1] + $"  +{(i switch { 3 => 5, 2 => 3, _ => 1 })} 🌰", Act(() => _pet.MarkTask(t, size: size)));
                SetOn(chip, t.Size == i);
                sizes.Children.Add(chip);
            }
            p.Children.Add(sizes);

            // Време.
            p.Children.Add(Label("Колко време?"));
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
                var hint = IconText("reroll", $"Пренасяна {t.Rolled} пъти.");
                hint.Margin = new Thickness(0, 10 * U, 0, 0);
                p.Children.Add(hint);
                p.Children.Add(new TextBlock
                {
                    Text = "Може би е голяма? Раздели я на по-малки стъпки или я сложи за „Някога“, докато ѝ дойде времето.",
                    Foreground = Muted,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2 * U, 0, 0),
                });
            }
        }

        // Действия.
        var actions = new UniformGrid { Columns = 2, Margin = new Thickness(0, 12 * U, 0, 0) };
        if (open)
        {
            var focus = Button("Мечо-доро", Act(() => _pet.StartFocusOn(t)), primary: true, icon: "honey");
            focus.ToolTip = Tip("Пусни мечо-доро по тази задача");
            var done = Button("Свършена", Act(() => _pet.SetTaskDone(t, true)), icon: "check");
            actions.Children.Add(focus);
            actions.Children.Add(done);
            bool onBoard = _pet.TaskOnBoard(t);
            var board = Button(onBoard ? "От таблото" : "В таблото", () => _pet.SetTaskOnBoard(t, !onBoard), icon: onBoard ? "unpin" : "pin");
            board.ToolTip = Tip(onBoard ? "В закаченото табло е (ако е за днес) – махни я оттам" : "Покажи я в закаченото табло (ако е за днес)");
            actions.Children.Add(board);
        }
        else
        {
            actions.Children.Add(Button("Не е свършена", () => _pet.SetTaskDone(t, false), icon: "reroll"));
        }
        var delete = Button("Изтрий", () =>
        {
            _pet.DeleteTask(t);
            Select(null);
        }, icon: "close");
        actions.Children.Add(delete);
        foreach (FrameworkElement b in actions.Children) b.Margin = new Thickness(0, 0, 4 * U, 4 * U);
        p.Children.Add(actions);

        _details.Child = new Border
        {
            Background = Tile,
            BorderBrush = WoodDark,
            BorderThickness = Flat,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10 * U),
            Child = new ScrollViewer { Content = p, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
        };
    }

    private static string Capital(string s) => s.Length == 0 ? s : char.ToUpper(s[0], Bg) + s[1..];

    private static TextBlock Label(string text) => new()
    {
        Text = text,
        Foreground = Muted,
        Margin = new Thickness(0, 10 * U, 0, 3 * U),
    };

    private static Border Small(string text, Action click)
    {
        var chip = Chip(text, click);
        chip.Padding = new Thickness(6, 2, 6, 3);
        chip.Margin = new Thickness(0, 0, 4 * U, 4 * U);
        return chip;
    }
}
