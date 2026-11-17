using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Mecho.Core;
using static Mecho.Ui;

namespace Mecho;

/// <summary>
/// Малко отделно прозорче (разкачено от менюто): дървена рамка, хартия, заглавие, ✕.
/// Влачи се за горния ред (и за празното място); Esc го затваря; мястото се помни.
/// </summary>
internal abstract class FloatWindow : Window
{
    protected readonly PetWindow Pet;
    protected readonly DockPanel Head = new() { Margin = new Thickness(0, 0, 0, 8), Background = Brushes.Transparent, Cursor = Cursors.SizeAll };
    protected readonly ContentControl Body = new();
    private readonly string _placeKey;
    private static readonly Dictionary<string, Point> Places = new();
    private static readonly List<FloatWindow> Open = new();

    protected static double U => Ui.Body / 18;

    /// <summary>Някое от прозорчетата е активно (менюто тогава не се скрива).</summary>
    public static bool AnyActive => Open.Any(w => w.IsActive);

    public static void CloseAll()
    {
        foreach (var w in Open.ToList()) w.Close();
    }

    protected FloatWindow(PetWindow pet, string placeKey, double width)
    {
        Pet = pet;
        _placeKey = placeKey;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        SizeToContent = SizeToContent.Height;
        Width = width;
        FontFamily = GameFont;
        FontSize = Ui.Body;
        Foreground = Ink;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.Aliased);
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        Resources.Add(typeof(ScrollBar), ScrollBarStyle());
        WindowStartupLocation = WindowStartupLocation.Manual;

        var close = Chip("", Close);
        close.Child = Icon("close");
        close.ToolTip = Tip("Затвори (Esc)");
        close.Margin = new Thickness(8, 0, 0, 0);
        close.VerticalAlignment = VerticalAlignment.Top;
        DockPanel.SetDock(close, Dock.Right);
        Head.Children.Add(close);
        var root = new DockPanel();
        DockPanel.SetDock(Head, Dock.Top);
        root.Children.Add(Head);
        root.Children.Add(Body);
        Content = Ui.Frame(root, new Thickness(14 * U, 10 * U, 12 * U, 14 * U));
        MouseLeftButtonDown += (_, e) =>
        {
            if (e.Handled || InTextBox(e.OriginalSource as DependencyObject)) return;
            try { DragMove(); }
            catch (InvalidOperationException) { }
            Places[_placeKey] = new Point(Left, Top);
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape || e.Handled) return;
            Close();
            e.Handled = true;
        };
        Deactivated += (_, _) => Dispatcher.BeginInvoke(() => Pet.FloatDeactivated(), DispatcherPriority.Input);
        Closed += (_, _) => Open.Remove(this);
        Open.Add(this);
    }

    private static bool InTextBox(DependencyObject? d)
    {
        for (; d != null; d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
            if (d is TextBox) return true;
        return false;
    }

    /// <summary>
    /// Върху менюто на мечока – на същия монитор, където е то (и при два монитора); без меню –
    /// където е било последно, иначе в средата на главния екран.
    /// </summary>
    protected void Place(Window? near)
    {
        var area = SystemParameters.WorkArea;
        bool onMenu = near is { IsVisible: true };
        if (onMenu && MonitorArea(near!) is { } m) area = m;
        void Put(double h)
        {
            if (onMenu)
            {
                Left = Math.Clamp(near!.Left + (near.ActualWidth - Width) / 2, area.Left, Math.Max(area.Left, area.Right - Width));
                Top = Math.Clamp(near.Top + Math.Max(40 * U, (near.ActualHeight - h) / 3), area.Top, Math.Max(area.Top, area.Bottom - h));
            }
            else if (Places.TryGetValue(_placeKey, out var p))
            {
                Left = p.X;
                Top = p.Y;
            }
            else
            {
                Left = area.Left + (area.Width - Width) / 2;
                Top = area.Top + Math.Max(40, (area.Height - h) / 3);
            }
        }
        // Веднага на правилното място (иначе за миг се показва на главния екран), после – по височината.
        Put(300 * U);
        Loaded += (_, _) => Put(ActualHeight);
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public int dwFlags; }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

    /// <summary>Работната площ на монитора, на който е прозорецът (в единиците на WPF).</summary>
    internal static Rect? MonitorArea(Window w)
    {
        try
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(w).Handle;
            if (hwnd == IntPtr.Zero) return null;
            var info = new MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(MonitorFromWindow(hwnd, 2), ref info)) return null;
            var source = PresentationSource.FromVisual(w);
            var m = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
            var tl = m.Transform(new Point(info.rcWork.Left, info.rcWork.Top));
            var br = m.Transform(new Point(info.rcWork.Right, info.rcWork.Bottom));
            return new Rect(tl, br);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Копче-хапче в реда с избора (Ден, Време, Етикет…): иконка/цвят + текст.</summary>
    protected static Border MetaChip(string? icon, string text, Action<Border> click, string tip, Color? dot = null, bool on = false)
    {
        Border chip = null!;
        chip = Chip("", () => click(chip));
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        if (dot is { } c) row.Children.Add(TaskTags.ColorSquare(c));
        else if (icon != null)
        {
            var i = Icon(icon);
            i.Margin = new Thickness(0, 0, 6, 0);
            i.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(i);
        }
        row.Children.Add(new TextBlock { Text = text, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 220 * U });
        chip.Child = row;
        chip.Padding = new Thickness(8, 3, 8, 4);
        chip.Margin = new Thickness(0, 0, 6 * U, 6 * U);
        chip.ToolTip = Tip(tip);
        SetOn(chip, on);
        return chip;
    }

    protected static TextBox BigInput(string text, double size)
    {
        var box = MenuWindow.NewTextInput();
        box.Text = text;
        box.FontSize = size;
        box.Padding = new Thickness(8, 5, 8, 6);
        return box;
    }
}

/// <summary>Изборът на ден: Днес, Утре, следващата седмица, „Някога“ и календар.</summary>
internal static class DayPicker
{
    private static readonly CultureInfo Bg = new("bg-BG");
    private static double U => Body / 18;

    public static string Name(DateTime? day, DateTime today, int bucket = 0) => day switch
    {
        null => TaskCards.BucketOf(bucket).Name,
        { } d when d == today => "Днес",
        { } d when d == today.AddDays(1) => "Утре",
        { } d when d == today.AddDays(-1) => "Вчера",
        { } d => d.ToString(d.Year == today.Year ? "ddd, d MMM" : "d MMM yyyy", Bg),
    };

    /// <summary>
    /// Изскача под копчето: Днес, Утре, групите на Backlog (като в Sunsama) и календар.
    /// pick получава деня; pickBucket – групата (без него – само „Някога“).
    /// </summary>
    public static void Show(FrameworkElement anchor, DateTime? current, DateTime today, Action<DateTime?> pick, bool allowSomeday = true,
        string title = "Кога?", Action<int>? pickBucket = null, int currentBucket = -1)
    {
        PixelPopup.Show(anchor, title, () =>
        {
            var list = new List<PixelPopup.Entry>
            {
                new("Днес", "sun", () => pick(today), On: current == today),
                new("Утре", "nav_next", () => pick(today.AddDays(1)), On: current == today.AddDays(1)),
            };
            if (pickBucket != null)
                list.Add(new("Чакащи", "backlog", null, Sub: () => TaskCards.Buckets.Select(b =>
                    new PixelPopup.Entry(b.Name, null, () => pickBucket(b.Bucket), On: current == null && currentBucket == b.Bucket, Dot: b.Color, Letter: b.Letter))));
            else if (allowSomeday) list.Add(new("Някога (без ден)", "history", () => pick(null), On: current == null));
            list.Add(new("", null, null, Custom: close => Calendar(current, today, d => { close(); pick(d); })));
            return list;
        });
    }

    /// <summary>Месец с дни (от понеделник); днес – с медена рамка, избраният – меден.</summary>
    public static FrameworkElement Calendar(DateTime? selected, DateTime today, Action<DateTime> pick)
    {
        var month = new DateTime((selected ?? today).Year, (selected ?? today).Month, 1);
        var root = new StackPanel { Margin = new Thickness(4 * U, 6 * U, 4 * U, 2 * U), MinWidth = 280 * U };
        void Fill()
        {
            root.Children.Clear();
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 4 * U) };
            var prev = IconChip("nav_prev", () => { month = month.AddMonths(-1); Fill(); });
            var next = IconChip("nav_next", () => { month = month.AddMonths(1); Fill(); });
            prev.Padding = next.Padding = new Thickness(6, 1, 6, 2);
            // Място между стрелките и месеца – да не е натъпкано.
            prev.Margin = new Thickness(0, 0, 14 * U, 0);
            next.Margin = new Thickness(14 * U, 0, 0, 0);
            DockPanel.SetDock(prev, Dock.Left);
            DockPanel.SetDock(next, Dock.Right);
            head.Children.Add(prev);
            head.Children.Add(next);
            string name = month.ToString("MMMM yyyy", Bg);
            head.Children.Add(new TextBlock { Text = char.ToUpper(name[0], Bg) + name[1..], FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            root.Children.Add(head);
            var grid = new UniformGrid { Columns = 7 };
            foreach (var d in new[] { "П", "В", "С", "Ч", "П", "С", "Н" })
                grid.Children.Add(new TextBlock { Text = d, Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 2 * U) });
            int shift = ((int)month.DayOfWeek + 6) % 7;
            var first = month.AddDays(-shift);
            for (int i = 0; i < 42; i++)
            {
                var day = first.AddDays(i);
                if (i >= 35 && day.Month != month.Month) break;
                bool inMonth = day.Month == month.Month, isSel = day == selected, isToday = day == today;
                var cell = new Border
                {
                    Width = 36 * U,
                    Height = 32 * U,
                    Margin = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    // Избраният ден – цялото квадратче медено с рамка (вижда се ясно); днес – само рамка.
                    Background = isSel ? Honey : Brushes.Transparent,
                    BorderBrush = isSel || isToday ? Ink : Brushes.Transparent,
                    BorderThickness = Px(isSel ? 1 : 0.5),
                    Child = new TextBlock
                    {
                        Text = day.Day.ToString(),
                        FontWeight = isSel || isToday ? FontWeights.Bold : FontWeights.Normal,
                        Foreground = inMonth ? (day < today ? Muted : Ink) : Muted,
                        Opacity = inMonth ? 1 : 0.5,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                };
                cell.MouseEnter += (_, _) => { if (!isSel) cell.Background = Hover; };
                cell.MouseLeave += (_, _) => cell.Background = isSel ? Honey : Brushes.Transparent;
                TaskCards.Clickable(cell, () => pick(day));
                grid.Children.Add(cell);
            }
            root.Children.Add(grid);
        }
        Fill();
        return root;
    }
}

/// <summary>
/// „Нова задача“ – отделно прозорче (като в Sunsama): отгоре името, отдолу на един ред
/// Ден (с календар), Време, Етикет, Приоритет и Сложност; Enter или „Добави“.
/// </summary>
internal sealed class NewTaskWindow : FloatWindow
{
    private static NewTaskWindow? _current;
    private readonly TextBox _title;
    private readonly WrapPanel _meta = new();
    private DateTime? _day;
    private int _minutes, _size, _priority, _bucket;   // сложност 0 – „никаква“ (докато не избереш)
    private bool _priorityChosen;                        // избран ли е приоритет (и „Нормално“ се отбелязва)
    private DateTime? _start;
    private string _tag;

    public static NewTaskWindow? Current => _current;

    /// <summary>Самопроверката: полето за името.</summary>
    internal TextBox TitleBox => _title;

    public static void Show(PetWindow pet, DateTime? day, string? tag = null, bool star = false, Window? near = null, int bucket = 0, DateTime? start = null)
    {
        // Отворена ли е вече за същия ден – второто цъкане я затваря.
        if (_current != null && _current._day == day && start == null && _current._title.Text.Trim().Length == 0)
        {
            _current.Close();
            return;
        }
        if (_current != null)
        {
            _current._day = day;
            _current._bucket = bucket;
            _current._start = start;
            _current.FillMeta();
            _current.Activate();
            _current._title.Focus();
            return;
        }
        _current = new NewTaskWindow(pet, day, tag ?? "", star ? 2 : 0) { _bucket = bucket, _start = start };
        _current.FillMeta();
        _current.Place(near ?? pet.MenuWin);
        _current.Show();
        _current.Activate();
    }

    private NewTaskWindow(PetWindow pet, DateTime? day, string tag, int priority) : base(pet, "newtask", 640 * U)
    {
        _day = day;
        _tag = TaskTags.Find(tag) != null ? tag : "";
        _priority = priority;
        Title = "Нова задача";
        Head.Children.Add(new TextBlock { Text = "Нова задача", FontWeight = FontWeights.Bold, FontSize = Ui.Title, VerticalAlignment = VerticalAlignment.Center });

        var p = new StackPanel();
        _title = BigInput("", Ui.Title);
        _title.MaxLength = TasksModule.MaxTitle;
        p.Children.Add(MenuWindow.WithHint(_title, "Какво ще правиш?"));
        _meta.Margin = new Thickness(0, 10 * U, 0, 0);
        var row = new DockPanel();
        var add = Ui.Button("Добави", Add, primary: true, icon: "plus");
        add.Margin = new Thickness(6 * U, 10 * U, 0, 6 * U);
        add.VerticalAlignment = VerticalAlignment.Top;
        add.ToolTip = Tip("Добави (Enter)");
        DockPanel.SetDock(add, Dock.Right);
        row.Children.Add(add);
        row.Children.Add(_meta);
        p.Children.Add(row);
        Body.Content = p;
        FillMeta();

        _title.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            Add();
            e.Handled = true;
        };
        Loaded += (_, _) => Dispatcher.BeginInvoke(() => { _title.Focus(); Keyboard.Focus(_title); }, DispatcherPriority.Input);
        Closed += (_, _) => _current = null;
        // Тук има „Добави“: цъкнеш ли извън прозорчето – затваря се, без да записва нищо.
        Deactivated += (_, _) => Dispatcher.BeginInvoke(() =>
        {
            if (!IsLoaded || IsActive || SelfTest.Running) return;
            if (PixelPopup.Last is { IsOpen: true }) return;   // избира се от изскачащо меню
            Close();
        }, DispatcherPriority.Input);
    }

    private void FillMeta()
    {
        var today = Pet.TaskToday;
        _meta.Children.Clear();
        _meta.Children.Add(MetaChip("clock", DayPicker.Name(_day, today, _bucket), chip =>
            DayPicker.Show(chip, _day, today, d => { _day = d; FillMeta(); }, pickBucket: b => { _day = null; _bucket = b; FillMeta(); }, currentBucket: _bucket),
            "Кога", on: true));
        if (_start is { } st && _day == st.Date)
            _meta.Children.Add(MetaChip("calendar", st.ToString("H:mm"), chip =>
                PixelPopup.Show(chip, "В графика", () => new[] { new PixelPopup.Entry("Без час", "close", () => { _start = null; FillMeta(); }) }),
                "В графика на деня от този час", on: true));
        _meta.Children.Add(MetaChip("hourglass", _minutes > 0 ? TaskCards.Duration(_minutes) : "Време", chip =>
            PixelPopup.Show(chip, "Колко време?", () => TaskCards.Estimates
                .Select(m => new PixelPopup.Entry(TaskCards.Duration(m), null, () => { _minutes = m; FillMeta(); }, On: _minutes == m))
                .Append(new PixelPopup.Entry("Не знам", null, () => { _minutes = 0; FillMeta(); }, On: _minutes == 0))),
            "Колко време ще отнеме", on: _minutes > 0));
        var tag = TaskTags.Find(_tag);
        _meta.Children.Add(MetaChip("pin", tag?.Name ?? "Етикет", chip =>
            PixelPopup.Show(chip, "Етикет", () => TaskTags.All
                .Select(t => new PixelPopup.Entry(t.Name, null, () => { _tag = t.Key; FillMeta(); }, On: _tag == t.Key, Swatch: t.Key))
                .Append(new PixelPopup.Entry("Без етикет", null, () => { _tag = ""; FillMeta(); }, On: _tag.Length == 0))
                .Append(new PixelPopup.Entry("Нов етикет", "plus", null, Sub: () => new[]
                {
                    new PixelPopup.Entry("", null, null, Custom: close => TagEditor.NewTag(Pet, key => { close(); _tag = key; FillMeta(); })),
                }))),
            "Етикет", tag?.Color, on: tag != null));
        var pr = TaskCards.PriorityOf(_priority);
        _meta.Children.Add(MetaChip(TaskCards.FlagIcon(_priority), _priorityChosen || _priority != 0 ? pr.Name : "Приоритет", chip =>
            PixelPopup.Show(chip, "Приоритет", () => TaskCards.Priorities
                .Select(x => new PixelPopup.Entry(x.Name, TaskCards.FlagIcon(x.Level), () => { _priority = x.Level; _priorityChosen = true; FillMeta(); },
                    On: (_priorityChosen || _priority != 0) && _priority == x.Level))),
            "Приоритет", on: _priorityChosen || _priority != 0));
        _meta.Children.Add(MetaChip(_size > 0 ? TaskCards.SizeIcon(_size) : "nut", _size > 0 ? TaskCards.Capital(TaskCards.SizeName(_size)) : "Сложност", chip =>
            PixelPopup.Show(chip, "Сложност", () => TaskCards.SizeEntries(_size, n => { _size = n; FillMeta(); })),
            "Сложност – колко лешника носи", on: _size > 0));
    }

    private void Add()
    {
        string text = _title.Text.Trim();
        if (text.Length == 0)
        {
            _title.BorderBrush = Bad;
            _title.Focus();
            return;
        }
        var first = Pet.TaskState.Open.FirstOrDefault(t => t.Day == _day && !t.Done);
        var added = Pet.AddTask(text, _size, null, _day, someday: _day == null, tag: _tag.Length > 0 ? _tag : null, star: _priority == 2,
            minutes: _minutes, quiet: true, before: first?.Id, priority: _priority, bucket: _bucket);
        if (added != null && _start is { } st && _day == st.Date) Pet.ScheduleTask(added, st, _minutes > 0 ? null : 30);
        Close();
    }
}

/// <summary>
/// Картата на задача – отделно прозорче (като в Sunsama), подредено на три части:
/// * горе – отметката, името (пишеш направо в него), „···“ и ✕; под името – колко лешника носи;
/// * свойствата – по ред „надпис – стойност“ (етикет, приоритет, кога, срок, време, сложност);
///   стойността се цъка и изскача малък избор;
/// * бележки и подзадачи.
/// </summary>
internal sealed class TaskCardWindow : FloatWindow
{
    private static readonly Dictionary<Guid, TaskCardWindow> Windows = new();
    private static readonly CultureInfo Bg = new("bg-BG");
    private readonly Guid _id;
    private readonly ContentControl _check = new() { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 8, 0, 0) };
    private readonly StackPanel _reward = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
    private readonly Grid _props = new();
    private readonly TextBox _title, _notes;
    private readonly TextBlock _stepsLabel;
    private readonly StackPanel _steps = new();
    private readonly TextBox _newStep;
    private string _shown = "";

    public static TaskCardWindow? For(Guid id) => Windows.TryGetValue(id, out var w) ? w : null;

    /// <summary>Самопроверката: бележките и записът им (както при излизане от полето).</summary>
    internal TextBox NotesBox => _notes;
    internal void TestSave() => SaveText();

    // Цъкнеш ли пак същата задача – картата се затваря (а не се отваря отново).
    private static Guid _closedId;
    private static DateTime _closedAt;

    public static void Show(PetWindow pet, TaskItem task, Window? near = null)
    {
        if (Windows.TryGetValue(task.Id, out var open))
        {
            open.Close();
            return;
        }
        if (_closedId == task.Id && (DateTime.Now - _closedAt).TotalMilliseconds < 400) return;
        var w = new TaskCardWindow(pet, task);
        w.Place(near ?? pet.MenuWin);
        w.Show();
        w.Activate();
    }

    private TaskItem? Task => Pet.Save.Tasks.FirstOrDefault(t => t.Id == _id);

    private TaskCardWindow(PetWindow pet, TaskItem task) : base(pet, "taskcard", 520 * U)
    {
        _id = task.Id;
        Windows[_id] = this;
        Title = task.Title;

        // ── Горе: ✓, името, лешниците; вдясно „···“ (✕ е от рамката) ──
        Border more = null!;
        more = Chip("···", () => { if (Task is { } t) PixelPopup.Show(more, null, TaskCards.TaskEntries(Pet, t, null)); });
        more.ToolTip = Tip("Още: премести, копие, табло, изтрий…");
        more.Margin = new Thickness(8, 0, 0, 0);
        more.VerticalAlignment = VerticalAlignment.Top;
        DockPanel.SetDock(more, Dock.Right);
        Head.Children.Add(more);
        DockPanel.SetDock(_check, Dock.Left);
        Head.Children.Add(_check);
        _title = new TextBox
        {
            Text = task.Title,
            MaxLength = TasksModule.MaxTitle,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = GameFont,
            FontSize = Ui.Title,
            FontWeight = FontWeights.Bold,
            Foreground = Ink,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = Brushes.Transparent,
            Padding = new Thickness(2, 2, 2, 3),
            CaretBrush = Ink,
            AcceptsReturn = false,
        };
        // Пише се направо в името – чертата отдолу се вижда само докато пишеш/посочиш.
        _title.GotKeyboardFocus += (_, _) => _title.BorderBrush = Honey;
        _title.LostKeyboardFocus += (_, _) => { _title.BorderBrush = Brushes.Transparent; SaveText(); };
        _title.MouseEnter += (_, _) => { if (!_title.IsKeyboardFocused) _title.BorderBrush = WoodLight; };
        _title.MouseLeave += (_, _) => { if (!_title.IsKeyboardFocused) _title.BorderBrush = Brushes.Transparent; };
        _title.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            SaveText();
            Keyboard.ClearFocus();
            e.Handled = true;
        };
        var titleBox = new StackPanel { Margin = new Thickness(8 * U, 0, 0, 0) };
        titleBox.Children.Add(_title);
        titleBox.Children.Add(_reward);
        Head.Children.Add(titleBox);

        var p = new StackPanel();
        // ── Свойствата: „надпис – стойност“ ──
        _props.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110 * U) });
        _props.ColumnDefinitions.Add(new ColumnDefinition());
        p.Children.Add(new Border
        {
            BorderBrush = WoodLight,
            BorderThickness = new Thickness(0, 1, 0, 1),
            Padding = new Thickness(0, 6 * U, 0, 6 * U),
            Margin = new Thickness(0, 2 * U, 0, 0),
            Child = _props,
        });

        // ── Бележки ──
        p.Children.Add(Caption("Бележки"));
        _notes = MenuWindow.NewTextInput();
        _notes.Text = task.Description;
        _notes.AcceptsReturn = true;
        _notes.TextWrapping = TextWrapping.Wrap;
        _notes.MinHeight = 70 * U;
        _notes.MaxHeight = 220 * U;
        _notes.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _notes.Margin = new Thickness(0);
        _notes.LostKeyboardFocus += (_, _) => SaveText();
        p.Children.Add(MenuWindow.WithHint(_notes, "Бележки…"));

        // ── Подзадачи ──
        _stepsLabel = Caption("Подзадачи");
        p.Children.Add(_stepsLabel);
        p.Children.Add(_steps);
        _newStep = MenuWindow.NewTextInput();
        _newStep.MaxLength = TasksModule.MaxTitle;
        _newStep.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter || string.IsNullOrWhiteSpace(_newStep.Text) || Task is not { } t) return;
            Pet.AddTaskStep(t, _newStep.Text.Trim());
            _newStep.Clear();
            e.Handled = true;
        };
        var addStep = MenuWindow.WithHint(_newStep, "+ Подзадача (Enter)");
        ((FrameworkElement)addStep).Margin = new Thickness(0, 2 * U, 0, 0);
        p.Children.Add(addStep);
        Body.Content = p;

        Rebuild(task);
        // Цъкнеш ли извън картата – записва написаното и се затваря (като в Sunsama).
        Deactivated += (_, _) => Dispatcher.BeginInvoke(() =>
        {
            if (!IsLoaded || IsActive || SelfTest.Running) return;
            if (PixelPopup.Last is { IsOpen: true }) return;   // изскачащо меню от картата – още се избира
            SaveText();
            Close();
        }, DispatcherPriority.Input);
        Pet.CareChanged += OnChanged;
        Closed += (_, _) =>
        {
            SaveText();
            Pet.CareChanged -= OnChanged;
            Windows.Remove(_id);
            _closedId = _id;
            _closedAt = DateTime.Now;
        };
        void OnChanged(object? s, EventArgs e)
        {
            if (Task is not { } t)
            {
                Close();
                return;
            }
            Rebuild(t);
        }
    }

    private static TextBlock Caption(string text) => new()
    {
        Text = text,
        Foreground = Muted,
        FontWeight = FontWeights.Bold,
        Margin = new Thickness(0, 12 * U, 0, 4 * U),
    };

    private void SaveText()
    {
        if (Task is not { } t || t.Done) return;
        string title = _title.Text.Trim(), notes = _notes.Text.Trim();
        if (title.Length == 0) { _title.Text = t.Title; return; }
        if (title == t.Title && notes == t.Description) return;
        Pet.EditTask(t, title, t.Size, notes);
    }

    /// <summary>Стойност в реда: иконка/цвят, текст, ▾ – лека (без рамка), свети при посочване.</summary>
    private static Border Value(string? icon, string text, Action<Border>? click, Color? dot = null, bool muted = false, string? tip = null, int reward = 0)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        if (dot is { } c) row.Children.Add(TaskTags.ColorSquare(c));
        else if (icon != null)
        {
            var i = Icon(icon);
            i.Margin = new Thickness(0, 0, 6, 0);
            i.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(i);
        }
        row.Children.Add(new TextBlock { Text = text, FontWeight = muted ? FontWeights.Normal : FontWeights.Bold, Foreground = muted ? Muted : Ink, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
        if (reward > 0)
        {
            var r = Ui.RewardText(reward, bold: true);
            r.Margin = new Thickness(8, 0, 0, 0);
            row.Children.Add(r);
        }
        if (click != null) row.Children.Add(new TextBlock { Text = "  ▾", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center });
        var box = new Border
        {
            Child = row,
            Padding = new Thickness(6, 3, 8, 4),
            CornerRadius = new CornerRadius(3),
            Background = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Left,
            Cursor = click != null ? Cursors.Hand : null,
        };
        if (tip != null) box.ToolTip = Tip(tip);
        if (click != null)
        {
            box.MouseEnter += (_, _) => box.Background = Hover;
            box.MouseLeave += (_, _) => box.Background = Brushes.Transparent;
            TaskCards.Clickable(box, () => click(box));
        }
        return box;
    }

    private void Prop(string label, FrameworkElement value)
    {
        int r = _props.RowDefinitions.Count;
        _props.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var l = new TextBlock { Text = label, Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 1, 6, 1) };
        Grid.SetRow(l, r);
        Grid.SetRow(value, r);
        Grid.SetColumn(value, 1);
        _props.Children.Add(l);
        _props.Children.Add(value);
    }

    private void Rebuild(TaskItem t)
    {
        string sig = t.Signature + TaskTags.Version + Pet.TaskToday.ToString("yyyyMMdd");
        if (sig == _shown) return;
        _shown = sig;
        Title = t.Title;
        var today = Pet.TaskToday;

        _check.Content = TaskCards.CheckSquare(t.Done, on => Pet.SetTaskDone(t, on));
        if (!_title.IsKeyboardFocused && _title.Text != t.Title) _title.Text = t.Title;
        if (!_notes.IsKeyboardFocused && _notes.Text != t.Description) _notes.Text = t.Description;
        _title.IsReadOnly = _notes.IsReadOnly = t.Done;
        _title.TextDecorations = t.Done ? TextDecorations.Strikethrough : null;
        // „Свършена в 14:32 ·“ или „като я свършиш“ + еднаквото „+3 🌰“ (в получер).
        _reward.Children.Clear();
        TextBlock Muted1(string text) => new() { Text = text, Foreground = Muted, VerticalAlignment = VerticalAlignment.Center };
        if (t.Done)
            _reward.Children.Add(Muted1((t.DoneAt is { } doneAt ? $"Свършена в {doneAt:H:mm}" + (doneAt.Date != today ? $" ({doneAt.ToString("d MMM", Bg)})" : "") : "Свършена") + "  ·  "));
        _reward.Children.Add(Ui.RewardText(t.Reward, bold: true));
        if (!t.Done) _reward.Children.Add(Muted1("  като я свършиш"));

        // Свойствата.
        _props.Children.Clear();
        _props.RowDefinitions.Clear();
        var tag = TaskTags.Find(t.Tag);
        Prop("Етикет", Value(tag == null ? "pin" : null, tag?.Name ?? "без етикет", b => PixelPopup.Show(b, "Етикет", TaskCards.TagEntries(Pet, t)), tag?.Color, muted: tag == null));
        var pr = TaskCards.PriorityOf(t.Level);
        Prop("Приоритет", Value(TaskCards.FlagIcon(t.Level), pr.Name, b => PixelPopup.Show(b, "Приоритет", TaskCards.PriorityEntries(Pet, t)), muted: t.Level == 0));
        Prop("Кога", Value("clock", DayPicker.Name(t.Day, today, t.Bucket) + (t.Start is { } at ? ", " + at.ToString("H:mm") : ""),
            b => DayPicker.Show(b, t.Day, today, d => Pet.MoveTask(t, d), pickBucket: x => Pet.SetTaskBucket(t, x), currentBucket: t.Bucket)));
        Prop("Срок", Value("bell", t.Due is { } dd ? Capital(dd.ToString("dddd, d MMMM", Bg)) + (dd < today && !t.Done ? " – мина!" : "") : "няма",
            b =>
            {
                if (t.Due != null)
                    PixelPopup.Show(b, "Срок", () => new[]
                    {
                        new PixelPopup.Entry("Друг ден…", "calendar", null, Sub: () => new[]
                        {
                            new PixelPopup.Entry("", null, null, Custom: close => DayPicker.Calendar(t.Due, today, d => { close(); Pet.MarkTask(t, due: d); })),
                        }),
                        new PixelPopup.Entry("Без срок", "close", () => Pet.MarkTask(t, clearDue: true)),
                    });
                else DayPicker.Show(b, null, today, d => { if (d is { } x) Pet.MarkTask(t, due: x); }, allowSomeday: false, title: "Срок до");
            }, muted: t.Due == null));
        Prop("Време", Value("hourglass", t.Minutes > 0 ? TaskCards.Duration(t.Minutes) : "не е казано",
            b => PixelPopup.Show(b, "Колко време?", TaskCards.TimeEntries(Pet, t)), muted: t.Minutes == 0,
            tip: t.Spent > 0 ? $"Работено досега: {TaskCards.Duration(t.Spent)}" : null));
        Prop("Сложност", Value(t.Size > 0 ? TaskCards.SizeIcon(t.Size) : "close", Capital(TaskCards.SizeName(t.Size)),
            t.Done ? null : b => PixelPopup.Show(b, "Сложност", () => TaskCards.SizeEntries(t.Size, n => Pet.MarkTask(t, size: n))),
            muted: t.Size == 0, reward: t.Reward));

        // Подзадачите.
        var steps = t.StepList;
        _stepsLabel.Text = steps.Count > 0 ? $"Подзадачи · {steps.Count(x => x.Done)}/{steps.Count}" : "Подзадачи";
        _steps.Children.Clear();
        foreach (var step in steps)
        {
            var st = step;
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 4 * U) };
            var check = TaskCards.CheckSquare(st.Done, on => Pet.SetTaskStepDone(t, st.Id, on));
            DockPanel.SetDock(check, Dock.Left);
            row.Children.Add(check);
            var del = new TextBlock { Text = "✕", Foreground = Muted, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 2, 0), ToolTip = Tip("Махни подзадачата") };
            TaskCards.Clickable(del, () => Pet.DeleteTaskStep(t, st.Id));
            DockPanel.SetDock(del, Dock.Right);
            row.Children.Add(del);
            var name = new TextBox
            {
                Text = st.Title,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = GameFont,
                FontSize = Ui.Body,
                Foreground = st.Done ? Muted : Ink,
                TextDecorations = st.Done ? TextDecorations.Strikethrough : null,
                VerticalAlignment = VerticalAlignment.Center,
            };
            void Rename()
            {
                string v = name.Text.Trim();
                if (v.Length > 0 && v != st.Title) Pet.RenameTaskStep(t, st.Id, v);
            }
            name.LostKeyboardFocus += (_, _) => Rename();
            name.KeyDown += (_, e) =>
            {
                if (e.Key != Key.Enter) return;
                Rename();
                Keyboard.ClearFocus();
                e.Handled = true;
            };
            row.Children.Add(name);
            _steps.Children.Add(row);
        }
    }

    private static string Capital(string s) => s.Length == 0 ? s : char.ToUpper(s[0], Bg) + s[1..];
}
