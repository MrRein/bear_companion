using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using static Mecho.Ui;

namespace Mecho;

/// <summary>Къде стои закаченото прозорче и как е (пази се в save.json).</summary>
public sealed class PinState
{
    public bool Open { get; set; }
    public double X { get; set; } = double.NaN;
    public double Y { get; set; } = double.NaN;
    public double W { get; set; }
    public double H { get; set; }
    public bool Collapsed { get; set; }    // смалено до иконка (X, Y, W, H са на разгънатото)
    public bool Faded { get; set; }        // полупрозрачно, докато мишката не е отгоре
    public bool Centered { get; set; }     // веднъж е било в средата на екрана (старите версии го слагаха в ъгъла)
}

/// <summary>
/// Закачено прозорче със задачите (за днес) или бележките: стои отгоре, където го сложиш.
/// Удобства: отметка на задача направо оттук, звезда (важна), влачене нагоре-надолу (и
/// от седмицата насам), дясно копче за всичко останало, поле за бърза задача за днес,
/// „Седмица“; смаляване до малка иконка (влачи се където искаш, цъкната – пак се
/// разгъва), полупрозрачно, размер с дръжката долу вдясно; мястото и размерът се помнят.
/// </summary>
public sealed class PinWindow : Window
{
    public const string Tasks = "tasks", Notes = "notes";

    private readonly PetWindow _pet;
    private readonly string _kind;
    private readonly StackPanel _list = new();
    private readonly ScrollViewer _scroll;
    private readonly Border _body;
    private readonly TextBlock _count = new() { Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
    private readonly Border _collapse, _fade;
    private readonly Border _full, _iconView;
    private readonly TextBlock _iconCount = new() { FontWeight = FontWeights.Bold, Foreground = Ink };
    private readonly Border _iconBadge;
    private string _shown = "";
    private TextBlock? _titleText;

    private static readonly System.Globalization.CultureInfo Bg = new("bg-BG");

    /// <summary>Денят на таблото с работата: „Днес“, „Утре“ или „сряда, 18 ное“.</summary>
    private string DayName(DateTime d) =>
        d == _pet.TaskToday ? "Днес" : d == _pet.TaskToday.AddDays(1) ? "Утре" : d == _pet.TaskToday.AddDays(-1) ? "Вчера" : d.ToString("dddd, d MMM", Bg);
    private bool _menuWasOpen;

    private static double U => Body / 18;

    /// <summary>Отначало: бележките са по-дълги надолу (текстът им е повече).</summary>
    private double DefaultHeight => _kind == Notes ? 440 * U : 320 * U;

    private PinState State => _kind == Tasks ? _pet.Save.PinTasks : _pet.Save.PinNotes;

    public PinWindow(PetWindow pet, string kind)
    {
        _pet = pet;
        _kind = kind;
        Title = kind == Tasks ? "Задачи" : "Бележки";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        ShowInTaskbar = false;
        Topmost = true;
        FontFamily = GameFont;
        FontSize = Body;
        Foreground = Ink;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        MinWidth = 260 * U;
        MinHeight = 90 * U;
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.Aliased);
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        Resources.Add(typeof(ScrollBar), ScrollBarStyle());
        WindowStartupLocation = WindowStartupLocation.Manual;

        var s = State;
        // Първото отваряне (и веднъж след старите версии, които го слагаха в ъгъла) – в
        // средата; после стои там, където Тут го е оставила.
        if (!s.Centered)
        {
            s.X = double.NaN;
            s.Y = double.NaN;
            s.Centered = true;
        }
        Width = s.W > 0 ? s.W : 360 * U;   // по-широко – задачите не се късат по средата на думата
        Height = s.H > 0 ? s.H : DefaultHeight;
        var area = SystemParameters.WorkArea;
        // Всички монитори заедно – запомненото място на втория монитор не скача на главния.
        var all = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        // Първия път – в средата на екрана (после там, където Тут го е сложила).
        Left = double.IsNaN(s.X) ? area.Left + (area.Width - Width) / 2 : Math.Clamp(s.X, all.Left - Width + 60, all.Right - 60);
        Top = double.IsNaN(s.Y) ? area.Top + Math.Max(40, (area.Height - Height) / 2) : Math.Clamp(s.Y, all.Top, all.Bottom - 40);

        // Заглавие: иконка, име, брой; копчета: свий, прозрачно, затвори. Влачи се оттук.
        var header = new DockPanel { Margin = new Thickness(2, 0, 2, 5), Background = Brushes.Transparent, Cursor = Cursors.SizeAll };
        var close = Chip("✕", () => _pet.SetPinned(_kind, false));
        close.ToolTip = Tip("Откачи");
        _fade = Chip("👁", () => { State.Faded = !State.Faded; ApplyFade(); _pet.SavePins(); });
        _fade.ToolTip = Tip("Полупрозрачно, когато мишката не е отгоре");
        _collapse = Chip("", ToggleCollapse);
        _collapse.Child = Icon("minimize");
        _collapse.ToolTip = Tip("Смали до иконка (цъкни я, за да се разгъне пак)");
        // Менюто на мечока: отваря го, а ако е отворено – затваря го. Цъкането по таблото
        // само по себе си затваря менюто (то губи фокуса), затова се гледа как е било при натискането.
        var menu = Chip("", () => _pet.ToggleMenuFrom(_kind == Tasks ? MenuWindow.TasksTab : MenuWindow.NotesTab, _menuWasOpen));
        menu.Child = Icon("bear");
        menu.PreviewMouseLeftButtonDown += (_, _) => _menuWasOpen = _pet.MenuOpenOrJustClosed;
        menu.ToolTip = Tip("Менюто на Мечо (цъкни пак – затваря се)");
        foreach (var b in new[] { close, _collapse, _fade, menu })
        {
            b.Margin = new Thickness(4, 0, 0, 0);
            b.Padding = new Thickness(6, 1, 6, 2);
            DockPanel.SetDock(b, Dock.Right);
            header.Children.Add(b);
        }
        // Иконка и име; тясно ли е – името се съкращава с „…“ (не се скрива под копчетата).
        var title = new DockPanel { LastChildFill = true };
        var titleIcon = Icon(kind == Tasks ? "tasks" : "notes");
        titleIcon.Margin = new Thickness(0, 0, 6, 0);
        titleIcon.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(titleIcon, Dock.Left);
        title.Children.Add(titleIcon);
        _titleText = new TextBlock { Text = Title, FontWeight = FontWeights.Bold, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        title.Children.Add(_titleText);
        title.VerticalAlignment = VerticalAlignment.Center;
        // На дървената рамка – бели букви.
        System.Windows.Documents.TextElement.SetForeground(title, Brushes.White);
        _count.Foreground = Brushes.White;
        DockPanel.SetDock(_count, Dock.Right);
        title.Children.Insert(1, _count);
        header.Children.Add(title);   // последното – взима останалото място (името се съкращава)
        header.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2)
            {
                ToggleCollapse();
                return;
            }
            try { DragMove(); } catch (InvalidOperationException) { }
            Remember();
        };

        // Прозрачен фон: и празното място под картичките приема пусната задача.
        _scroll = new ScrollViewer { Content = _list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Brushes.Transparent };
        var bodyPanel = new DockPanel();
        var bottom = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
        if (kind == Tasks)
        {
            // Бърза задача за днес (Enter) и под нея: Седмица, Нова (с всичко за нея).
            var quick = MenuWindow.NewTextInput();
            quick.Padding = new Thickness(6, 4, 6, 4);
            quick.KeyDown += (_, e) =>
            {
                if (e.Key != Key.Enter || string.IsNullOrWhiteSpace(quick.Text)) return;
                string text = quick.Text;
                quick.Clear();
                _pet.AddTask(text, 0, day: _pet.BoardDay, quiet: true);
                e.Handled = true;
            };
            bottom.Children.Add(MenuWindow.WithHint(quick, "Задача… (Enter)"));
            var week = Button("Работа", _pet.OpenPlanner, icon: "tasks");
            week.ToolTip = Tip("Днес, седмицата по дни, планът и прегледът – в голямото меню");
            var add = Button("Нова", () => _pet.OpenNew(kind), primary: true, icon: "plus");
            add.ToolTip = Tip("Нова задача с всичко за нея (описание, етикет, ден…)");
            week.Margin = new Thickness(0, 6, 3, 0);
            add.Margin = new Thickness(3, 6, 0, 0);
            bottom.Children.Add(new UniformGrid { Rows = 1, Children = { week, add } });
            TaskCards.DropTarget(_scroll, _list, DropTask, _scroll);
        }
        else
        {
            // „➕ Нова“ отваря менюто на новата бележка (с всичко за нея – напомняне…).
            var newButton = Button("Нова", () => _pet.OpenNew(kind), primary: true, icon: "plus");
            newButton.Margin = new Thickness(0);
            newButton.HorizontalAlignment = HorizontalAlignment.Stretch;
            bottom.Children.Add(newButton);
        }
        DockPanel.SetDock(bottom, Dock.Bottom);
        bodyPanel.Children.Add(bottom);
        bodyPanel.Children.Add(_scroll);
        _body = new Border
        {
            Background = PaperTexture,
            BorderBrush = Ink,
            BorderThickness = Flat,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8 * U),
            Child = bodyPanel,
        }.Skinned("card");

        var root = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        root.Children.Add(_body);
        _full = new Border
        {
            Background = WoodTexture,
            BorderBrush = Ink,
            BorderThickness = Flat,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(6, 4, 6, 6),
            Child = root,
        }.Skinned("frame");

        // Смалено: само картинката (бележката / работата) без фон, с колко има в нея.
        var picture = Icon(kind == Tasks ? "tasks" : "notes");
        picture.Width *= 3;
        picture.Height *= 3;
        _iconBadge = new Border
        {
            Background = Honey,
            BorderBrush = Ink,
            BorderThickness = Px(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 0, 4, 1),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, -2, -2),
            Child = _iconCount,
            IsHitTestVisible = false,
        };
        var iconGrid = new Grid { Children = { picture, _iconBadge } };
        _iconView = new Border
        {
            Background = Brushes.Transparent,   // само картинката (прозрачното още се хваща с мишката)
            Padding = new Thickness(4),
            Child = iconGrid,
            Cursor = Cursors.Hand,
            ToolTip = Tip($"{Title} – цъкни, за да се разгъне; влачи, за да го преместиш"),
        };
        // Влачи се; ако не е мръднало – цъкване: разгъва се.
        _iconView.MouseLeftButtonDown += (_, e) =>
        {
            double x = Left, y = Top;
            try { DragMove(); } catch (InvalidOperationException) { }
            if (Math.Abs(Left - x) < 4 && Math.Abs(Top - y) < 4) ToggleCollapse();
            else Remember();
            e.Handled = true;
        };
        Content = State.Collapsed ? _iconView : _full;
        _zoom = 1;
        ApplyZoom();

        MouseEnter += (_, _) => Opacity = 1;
        MouseLeave += (_, _) => ApplyFade();
        LocationChanged += (_, _) => Remember();
        SizeChanged += (_, _) => { if (!State.Collapsed) Remember(); };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && Keyboard.FocusedElement is not TextBox) { ToggleCollapse(); e.Handled = true; }
        };
        Closed += (_, _) => Remember();
        SourceInitialized += (_, _) => NativeMethods.MakeToolWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle);
        ApplyCollapse();
        ApplyFade();
        Refresh();
    }

    private double _zoom = 1;

    /// <summary>Мащабът от „Още“ → Размер (и таблото, и иконката).</summary>
    public void ApplyZoom()
    {
        double z = _pet.Save.UiZoom is >= 0.75 and <= 2 ? _pet.Save.UiZoom : 1;
        var t = Math.Abs(z - 1) < 0.01 ? Transform.Identity : new ScaleTransform(z, z);
        _full.LayoutTransform = t;
        _iconView.LayoutTransform = t;
        // Отначало (без запомнен размер) – началният размер по мащаба.
        if (!IsLoaded && State.W <= 0 && !State.Collapsed)
        {
            Width *= z;
            Height *= z;
        }
        else if (Math.Abs(z - _zoom) > 0.001 && !State.Collapsed && IsLoaded)
        {
            Width = Width / _zoom * z;
            Height = Height / _zoom * z;
        }
        MinWidth = 260 * U * z;
        MinHeight = State.Collapsed ? 0 : 90 * U * z;
        _zoom = z;
    }

    private void Remember()
    {
        if (_applying) return;
        var s = State;
        if (s.Collapsed)
        {
            // Иконката стои там, където е горният десен ъгъл на таблото (там е и копчето за смаляване).
            s.X = Left + IconSize.Width - (s.W > 0 ? s.W : Width);
            s.Y = Top;
            return;
        }
        s.X = Left;
        s.Y = Top;
        if (ActualWidth > 0)
        {
            s.W = ActualWidth;
            s.H = ActualHeight;
        }
    }

    /// <summary>Колко е голяма иконката (смаленото табло).</summary>
    private Size IconSize
    {
        get
        {
            _iconView.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return _iconView.DesiredSize;
        }
    }

    private void ApplyFade()
    {
        SetOn(_fade, State.Faded);
        Opacity = State.Faded && !IsMouseOver ? 0.55 : 1;
    }

    /// <summary>Смалява до иконка / разгъва пак.</summary>
    public void ToggleCollapse()
    {
        if (!State.Collapsed) Remember();
        State.Collapsed = !State.Collapsed;
        ApplyCollapse();
        _pet.SavePins();
    }

    public bool IsIcon => State.Collapsed;

    private void ApplyCollapse()
    {
        // Докато се смалява/разгъва, размерът и мястото се менят за миг (ResizeMode,
        // SizeToContent): те не бива да се запомнят, иначе разгънатото излиза мъничко.
        var s = State;
        double width = s.W > 0 ? s.W : Width, height = s.H > 0 ? s.H : DefaultHeight;
        // Мониторът, на който е таблото (при два монитора не скача на главния).
        var area = IsLoaded && FloatWindow.MonitorArea(this) is { } m ? m : SystemParameters.WorkArea;
        _applying = true;
        if (s.Collapsed)
        {
            var icon = IconSize;
            Content = _iconView;
            ResizeMode = ResizeMode.NoResize;
            MinWidth = MinHeight = 0;
            SizeToContent = SizeToContent.WidthAndHeight;
            double x = double.IsNaN(s.X) ? Left : s.X;
            Left = Math.Clamp(x + width - icon.Width, area.Left, area.Right - icon.Width);
            Top = Math.Clamp(double.IsNaN(s.Y) ? Top : s.Y, area.Top, area.Bottom - icon.Height);
        }
        else
        {
            Content = _full;
            SizeToContent = SizeToContent.Manual;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            MinWidth = 260 * U * _zoom;
            MinHeight = 90 * U * _zoom;
            Width = width;
            Height = height;
            // Разгъва се от иконката (горният ѝ десен ъгъл), без да излиза от екрана.
            double x = double.IsNaN(s.X) ? Left : s.X, y = double.IsNaN(s.Y) ? Top : s.Y;
            Left = Math.Clamp(x, area.Left, Math.Max(area.Left, area.Right - width));
            Top = Math.Clamp(y, area.Top, Math.Max(area.Top, area.Bottom - Math.Min(height, area.Height)));
            s.W = width;
            s.H = height;
            s.X = Left;
            s.Y = Top;
        }
        _applying = false;
    }

    private bool _applying;

    /// <summary>Строи списъка наново, само ако нещо се е сменило.</summary>
    public void Refresh(bool force = false)
    {
        var s = _pet.Save;
        if (force) _shown = "";
        if (TaskCards.Dragging) return;   // по време на влачене – не (картичката, която се влачи, ще изчезне)
        string sig = _kind == Tasks
            ? string.Join("|", BoardTasks().Select(t => t.ToString())) + "#" + s.FocusTask + s.UiTheme + "#" + _pet.BoardDay.ToString("yyyyMMdd") + _pet.TaskToday.ToString("yyyyMMdd")
            : string.Join("|", _pet.OrderedNotes(pinnedOnly: true).Select(n => n.Id + n.Title + "¦" + n.Text));
        if (sig == _shown) return;
        _shown = sig;
        _list.Children.Clear();
        if (_kind == Tasks) BuildTasks();
        else BuildNotes();
    }

    /// <summary>Днешните в таблото: несвършените в реда им, после свършените днес.</summary>
    private System.Collections.Generic.List<TaskItem> BoardTasks()
    {
        var day = _pet.BoardDay;
        var all = _pet.TaskState.OnDay(day).Where(_pet.TaskOnBoard).ToList();
        return all.Where(t => !t.Done).Concat(all.Where(t => t.Done).OrderBy(t => t.DoneAt)).ToList();
    }

    private void BuildTasks()
    {
        // Само днешните задачи в таблото (📍 ги маха оттук, в менюто 📌 ги връща).
        var tasks = BoardTasks();
        if (_titleText != null) _titleText.Text = "Работа · " + DayName(_pet.BoardDay);
        int done = tasks.Count(t => t.Done);
        _count.Text = tasks.Count > 0 ? $"· {done}/{tasks.Count}" : "";
        int left = tasks.Count - done;
        _iconCount.Text = left.ToString();
        _iconBadge.Visibility = left > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (tasks.Count == 0) _list.Children.Add(MenuWindow.MutedText("Няма задачи за този ден."));
        else if (left == 0) _list.Children.Add(MenuWindow.MutedText("Всичко е свършено! 🍯"));
        foreach (var task in tasks)
        {
            var t = task;
            FrameworkElement? side = null;
            if (!t.Done)
            {
                // 📍 махни от таблото, под него ✕ изтрий.
                var off = Chip("📍", () => _pet.SetTaskOnBoard(t, false));
                off.ToolTip = Tip("Махни я от таблото (задачата остава)");
                var del = Chip("✕", () => _pet.DeleteTask(t));
                del.ToolTip = Tip("Изтрий");
                var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(2, 0, 0, 0) };
                foreach (var b in new[] { off, del })
                {
                    b.Padding = new Thickness(5, 0, 5, 1);
                    b.Margin = new Thickness(4, 0, 0, 3);
                    panel.Children.Add(b);
                }
                side = panel;
            }
            var card = TaskCards.Build(_pet, t, new TaskCardOptions
            {
                Side = side,
                Edit = x => _pet.EditTaskInMenu(x),
                Selected = IsCurrent(t),
            });
            _list.Children.Add(card);
        }
    }

    private bool IsCurrent(TaskItem t) => !t.Done && _pet.Save.FocusTask == t.Id && _pet.InFocus;

    /// <summary>Пусната тук задача (от таблото или от седмицата): за днес, в таблото, на това място.</summary>
    private void DropTask(Guid id, Guid? before)
    {
        if (_pet.Save.Tasks.FirstOrDefault(t => t.Id == id) is not { } task) return;
        if (!_pet.TaskOnBoard(task)) _pet.SetTaskOnBoard(task, true);
        _pet.MoveTask(task, _pet.BoardDay, before);
    }

    private void BuildNotes()
    {
        // Само бележките с „📌 В таблото“.
        var notes = _pet.OrderedNotes(pinnedOnly: true);
        _count.Text = notes.Count > 0 ? $"· {notes.Count}" : "";
        _iconCount.Text = notes.Count.ToString();
        _iconBadge.Visibility = notes.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (notes.Count == 0) _list.Children.Add(MenuWindow.MutedText("Няма закачени бележки."));
        foreach (var note in notes)
        {
            var n = note;
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            // Само за четене, но се маркира и копира; името – удебелено.
            var text = new StackPanel();
            if (n.Title.Length > 0) text.Children.Add(new TextBlock { Text = n.Title, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, Foreground = Ink });
            if (n.Text.Length > 0) text.Children.Add(new TextBox
            {
                Text = n.Text,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = Ink,
                FontFamily = GameFont,
                FontSize = Body,
                Padding = new Thickness(0),
            });
            grid.Children.Add(text);
            var buttons = new StackPanel { VerticalAlignment = VerticalAlignment.Top };
            var unpin = Chip("📍", () => _pet.SetNotePinned(n, false));
            unpin.ToolTip = Tip("Махни я от таблото (бележката остава)");
            var del = Chip("✕", () => _pet.DeleteNote(n));
            del.ToolTip = Tip("Изтрий");
            foreach (var b in new[] { unpin, del })
            {
                b.Padding = new Thickness(5, 0, 5, 1);
                b.Margin = new Thickness(4, 0, 0, 3);
                buttons.Children.Add(b);
            }
            Grid.SetColumn(buttons, 1);
            grid.Children.Add(buttons);
            var card = MenuWindow.Card(grid, false);
            card.Padding = new Thickness(6, 4, 4, 4);
            card.Margin = new Thickness(0, 0, 0, 4);
            _list.Children.Add(card);
        }
    }
}
