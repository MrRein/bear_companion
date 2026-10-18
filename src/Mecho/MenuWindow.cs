using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using static Mecho.Ui;

namespace Mecho;

/// <summary>
/// Панелът „Мечо“ (десен бутон върху мечока), направен като меню на игра:
/// дървена рамка, хартия, табове с пикселни иконки, плочки с картинки и
/// подсказки. Мести се за заглавието и помни мястото си. Затваря се с Esc,
/// с ✕ или с клик другаде.
/// </summary>
public sealed class MenuWindow : Window
{
    public const int BearTab = 0, FoodTab = 1, ShopTab = 2, WardrobeTab = 3, TasksTab = 4, NotesTab = 5, PomodoroTab = 6, MoreTab = 7;

    private const int Segments = 10; // деленца в лентите за ситост и енергия

    private readonly PetWindow _pet;
    private readonly DispatcherTimer _refresh;
    private readonly Border[] _tabButtons = new Border[8];
    private readonly UIElement[] _tabs = new UIElement[8];

    /// <summary>Мерна единица: всичко е пропорционално на големината на шрифта.</summary>
    private static double U => Body / 18;

    // Динамични части
    private readonly TextBlock _nuts = new() { FontWeight = FontWeights.Bold };
    private Image _portrait = null!, _portraitBg = null!, _modelBg = null!;

    /// <summary>
    /// Рамка с фона и мечока върху него (фонът е 50 x 50, мечокът стъпва на реда 48).
    /// Клик сменя фона с друг купен.
    /// </summary>
    private Border PortraitBox(out Image bear, out Image background)
    {
        int scale = ArtScale(3);
        background = Pixel(_pet.BackdropImage(_pet.CurrentBackdrop), scale);
        bear = Pixel(_pet.Portrait, scale);
        bear.VerticalAlignment = VerticalAlignment.Bottom;
        bear.HorizontalAlignment = HorizontalAlignment.Center;
        bear.Margin = new Thickness(0, 0, 0, 2 * scale / Dpi);
        var grid = new Grid { Width = background.Width, Height = background.Height, ClipToBounds = true };
        grid.Children.Add(background);
        grid.Children.Add(bear);
        var box = new Border
        {
            BorderBrush = Ink,
            BorderThickness = new Thickness(3, 3, 3, 5),
            CornerRadius = new CornerRadius(6),
            Child = grid,
            VerticalAlignment = VerticalAlignment.Top,
            Cursor = Cursors.Hand,
        };
        box.MouseLeftButtonUp += (_, _) => _pet.NextBackdrop();
        return box;
    }
    private Border[] _fullBar = null!, _energyBar = null!;
    private TextBlock _fullText = null!, _energyText = null!, _mood = null!, _weather = null!;
    private StackPanel _weatherRow = null!;
    private readonly Border _weatherIcon = new() { VerticalAlignment = VerticalAlignment.Center };
    private string? _weatherIconName;
    private Button _sleepButton = null!, _couchButton = null!, _quietButton = null!;
    // Мрежа по 3: всички плочки в един ред са еднакво високи, бутоните са на една линия.
    private readonly UniformGrid _foodTiles = new() { Columns = 3 };
    private readonly StackPanel _shopTiles = new();   // раздели: кухня, за почивка, подобрения
    private TextBlock _foodInfo = null!, _shopInfo = null!;
    private readonly StackPanel _wardrobeShelves = new();
    private readonly StackPanel _wornList = new();
    private Image _model = null!;
    private readonly StackPanel _taskList = new();
    private TextBlock _taskSummary = null!;
    private TextBox _taskInput = null!, _taskText = null!;
    private int _taskSize = 1;
    private readonly Border[] _sizeButtons = new Border[3];
    private readonly StackPanel _noteList = new();
    private TextBox _noteInput = null!;
    private TextBlock _pomTime = null!, _pomStatus = null!, _pomToday = null!;
    private readonly StackPanel _pomNuts = new() { Orientation = Orientation.Horizontal }, _timerNuts = new() { Orientation = Orientation.Horizontal };

    /// <summary>„+3 🌰“ – числото и лешникът, центрирани един спрямо друг.</summary>
    private static void SetNuts(StackPanel row, int nuts)
    {
        if (row.Tag is int shown && shown == nuts) return;
        row.Tag = nuts;
        row.Children.Clear();
        if (nuts <= 0) return;
        row.Children.Add(RewardText(nuts, bold: true));
    }
    private Button _pomStart = null!, _pomStop = null!;
    private TextBlock _timerTime = null!;
    private TextBox _timerLabel = null!, _timerMinutes = null!;
    private TextBlock _timerReward = null!;
    private double _timerChoice;
    private readonly List<(double Minutes, Border Chip)> _timerChips = new();
    private Button _timerStop = null!;
    private string _timerKind = "work";
    private readonly List<(string Kind, Border Chip)> _kindChips = new();
    private Button _updateButton = null!;

    public MenuWindow(PetWindow pet)
    {
        _pet = pet;
        Title = "Мечо";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        SizeToContent = SizeToContent.WidthAndHeight;
        FontFamily = GameFont;
        FontSize = Body;
        Foreground = Ink;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.Aliased);
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        Resources.Add(typeof(ScrollBar), ScrollBarStyle());

        _tabs[BearTab] = BuildBearTab();
        _tabs[FoodTab] = BuildFoodTab();
        _tabs[ShopTab] = BuildShopTab();
        _tabs[WardrobeTab] = BuildWardrobeTab();
        // Задачите са в таба „Работа“ (вдясно); TasksTab води там.
        var tasksPanel = BuildTasksTab();
        _tabs[TasksTab] = new Border();
        _tabs[NotesTab] = BuildNotesTab();
        _tabs[PomodoroTab] = BuildWorkTab(BuildPomodoroTab(), tasksPanel);
        _tabs[MoreTab] = BuildMoreTab();

        var content = new Grid { Width = 740 * U, MinHeight = 380 * U };
        foreach (var t in _tabs) content.Children.Add(t);

        var root = new DockPanel();
        var header = BuildHeader();
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        var tabs = BuildTabs();
        DockPanel.SetDock(tabs, Dock.Top);
        root.Children.Add(tabs);
        root.Children.Add(new Border
        {
            Background = PaperTexture,
            BorderBrush = Ink,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(0, 0, 6, 6),
            Padding = new Thickness(14 * U, 12 * U, 14 * U, 14 * U),
            Child = content,
        });

        Content = new Border
        {
            Background = WoodTexture,
            BorderBrush = Ink,
            BorderThickness = new Thickness(3),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(7, 5, 7, 7),
            Child = root,
        };

        _pet.CareChanged += (_, _) => StateChangedRefresh();
        // Менюто само показва състоянието: всяка промяна в ядрото (лешници, притежавано,
        // предмети на земята) го пречертава. Абонаментите се махат при затваряне.
        var bus = _pet.CoreEngine.Bus;
        bus.Subscribe<Mecho.Core.StateChanged<Mecho.Core.WalletState>>(this, _ => StateChangedRefresh());
        bus.Subscribe<Mecho.Core.StateChanged<Mecho.Core.InventoryState>>(this, _ => StateChangedRefresh());
        bus.Subscribe<Mecho.Core.StateChanged<Mecho.Core.PlacementState>>(this, _ => StateChangedRefresh());
        Closed += (_, _) => bus.Unsubscribe(this);
        _refresh = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _refresh.Tick += (_, _) => Refresh(rebuild: false);

        Deactivated += (_, _) => Hide();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) _refresh.Start();
            else
            {
                _refresh.Stop();
                _pet.CloseWardrobe();
                _pet.MenuClosed();
            }
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Hide();
        };

        SelectTab(BearTab);
    }

    /// <summary>
    /// Показва панела там, където Тут го е оставила, или до мечока (вдясно или
    /// вляво, където има място), за да се вижда балончето над него.
    /// </summary>
    public void ShowNear(Rect bear, Rect area, int tab)
    {
        _hideToken++;            // отваря се пак, докато се скриваше: не го крий
        _hiding = false;
        Opacity = 1;
        // Windows може да го е „свил“ (Win+D, „покажи работния плот“): винаги нормален размер.
        if (WindowState != WindowState.Normal) WindowState = WindowState.Normal;
        SizeToContent = SizeToContent.WidthAndHeight;
        _dirty[tab] = true;      // винаги свежо (офертата може да е сменена от Developer)
        SelectTab(tab);
        // Размерът и мястото се смятат преди показването, за да не подскача.
        if (Content is UIElement content) content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = Content is UIElement c ? c.DesiredSize : new Size(ActualWidth, ActualHeight);
        PlaceNear(bear, area, size);
        Show();
        if (tab == WardrobeTab) _pet.OpenWardrobe();
        UpdateLayout();
        PlaceNear(bear, area, new Size(ActualWidth, ActualHeight));
        Activate();
    }

    private void PlaceNear(Rect bear, Rect area, Size size)
    {

        var s = _pet.Save;
        if (!double.IsNaN(s.MenuLeft) && !double.IsNaN(s.MenuTop))
        {
            var home = _pet.AreaAt(s.MenuLeft + 20, s.MenuTop + 20);
            if (home.Contains(new Point(s.MenuLeft + 20, s.MenuTop + 20)))
            {
                Left = Math.Clamp(s.MenuLeft, home.Left, Math.Max(home.Left, home.Right - size.Width));
                Top = Math.Clamp(s.MenuTop, home.Top, Math.Max(home.Top, home.Bottom - size.Height));
                return;
            }
        }

        double left = bear.Right + 8;
        if (left + size.Width > area.Right - 4) left = bear.Left - size.Width - 8;
        Left = Math.Clamp(left, area.Left + 4, Math.Max(area.Left + 4, area.Right - size.Width - 4));
        Top = Math.Max(area.Top + 4, area.Bottom - size.Height - 4);
    }

    private bool _hiding;
    private int _hideToken;

    /// <summary>Скрива се (или вече се скрива).</summary>
    public bool IsOpen => IsVisible && !_hiding;

    /// <summary>
    /// Скрива панела. Прозрачният прозорец помни последния си кадър и при
    /// следващото отваряне за миг показваше стария таб: затова първо става
    /// невидим (рисува се празен) и чак тогава се скрива.
    /// </summary>
    public new void Hide()
    {
        if (!IsVisible || _hiding) return;
        _hiding = true;
        _pet.MenuClosed();   // цъкане по мечока точно сега не бива да го отвори пак
        Opacity = 0;
        int token = ++_hideToken;
        _ = System.Threading.Tasks.Task.Delay(40).ContinueWith(_ => Dispatcher.BeginInvoke(() =>
        {
            if (token != _hideToken) return;
            base.Hide();
            _hiding = false;
            Opacity = 1;
        }));
    }

    private int _tab;
    private readonly bool[] _dirty = Enumerable.Repeat(true, 8).ToArray();

    private void BuildTab(int index)
    {
        if (!_dirty[index]) return;
        _dirty[index] = false;
        switch (index)
        {
            case BearTab: BuildRelaxButtons(); break;
            case FoodTab: BuildFoodTiles(); break;
            case ShopTab: BuildShopTiles(); break;
            case WardrobeTab: BuildWardrobe(); break;
            case TasksTab: BuildTaskList(); break;
            case NotesTab: BuildNoteList(); break;
            case PomodoroTab: BuildFocusTasks(); BuildTaskList(); break;
        }
    }

    /// <summary>Тест за скорост: колко мс строи всеки таб (наново) и подрежда менюто.</summary>
    internal List<(string Tab, double Ms)> BenchTabs()
    {
        var names = new[] { "Мечо", "Храна", "Магазин", "Гардероб", "Задачи", "Бележки", "Работа", "Още" };
        var result = new List<(string, double)>();
        for (int i = 0; i < names.Length; i++)
        {
            if (i == TasksTab) continue;
            _dirty[i] = true;
            _foodKey = null;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            SelectTab(i);
            RefreshNow(false);
            UpdateLayout();
            result.Add((names[i], watch.Elapsed.TotalMilliseconds));
        }
        SelectTab(BearTab);
        return result;
    }

    private void SelectTab(int index)
    {
        bool tasks = index == TasksTab;
        if (tasks)
        {
            index = PomodoroTab;   // задачите са в „Работа“ → „Задачи“
            ShowWorkPart(2);
        }
        _tab = index;
        for (int i = 0; i < _tabs.Length; i++)
        {
            bool on = i == index;
            _tabs[i].Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            // Избраният таб е по-висок и слят с хартията отдолу, другите са по-тъмни.
            _tabButtons[i].Background = on ? Paper : WoodLight;
            _tabButtons[i].BorderThickness = on ? new Thickness(2, 2, 2, 0) : new Thickness(2, 2, 2, 2);
            // Всички табове са еднакво големи; избраният само е слят с хартията отдолу.
            _tabButtons[i].Margin = new Thickness(i == 0 ? 0 : 3, 0, 0, on ? -2 : 0);
        }
        // Гардеробът стои до мечока, докато е отворен табът.
        if (index == WardrobeTab && IsVisible) _pet.OpenWardrobe();
        else if (index != WardrobeTab) _pet.CloseWardrobe();
        Refresh(rebuild: false);
        BuildTab(index);
        if (tasks) Dispatcher.BeginInvoke(() => _taskInput.Focus(), DispatcherPriority.Input);
        if (index == NotesTab) Dispatcher.BeginInvoke(() => _noteInput.Focus(), DispatcherPriority.Input);
    }

    // ───────────────────────── Рамка: заглавие и табове ─────────────────────────

    private UIElement BuildHeader()
    {
        // Заглавието е дръжка: за него панелът се мести, а мястото се запомня.
        var grid = new Grid
        {
            Margin = new Thickness(4, 0, 0, 6),
            Background = Brushes.Transparent,
            Cursor = Cursors.SizeAll,
        };
        grid.MouseLeftButtonDown += (_, e) =>
        {
            try { DragMove(); }
            catch (InvalidOperationException) { return; }
            _pet.Save.MenuLeft = Left;
            _pet.Save.MenuTop = Top;
            _pet.Persist();
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var bear = Icon("bear", 2);
        bear.Margin = new Thickness(0, 0, 8, 0);
        title.Children.Add(bear);
        title.Children.Add(new TextBlock
        {
            Text = "МЕЧО",
            FontSize = Ui.Title,
            FontWeight = FontWeights.Bold,
            Foreground = Cream,
            VerticalAlignment = VerticalAlignment.Center,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Color.FromRgb(59, 36, 20), ShadowDepth = 2, Direction = 315, BlurRadius = 0, Opacity = 1 },
        });
        grid.Children.Add(title);

        var nutRow = new StackPanel { Orientation = Orientation.Horizontal };
        var nut = Icon("nut");
        nut.Margin = new Thickness(0, 0, 6, 0);
        nutRow.Children.Add(nut);
        _nuts.VerticalAlignment = VerticalAlignment.Center;
        nutRow.Children.Add(_nuts);
        var nuts = Pill(nutRow);
        nuts.Margin = new Thickness(0, 0, 8, 0);
        Grid.SetColumn(nuts, 1);
        grid.Children.Add(nuts);

        var close = Chip("✕", Hide);
        // Червеното ✕ е пикселна иконка; фонът е светъл, за да се вижда.
        close.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(close, 2);
        grid.Children.Add(close);
        return grid;
    }

    private UIElement BuildTabs()
    {
        // Всички табове са еднакво широки (UniformGrid); скритите не заемат място.
        var grid = new UniformGrid { Rows = 1, Margin = new Thickness(2, 0, 2, 0) };
        (string Icon, string Name)[] tabs =
        {
            ("bear", "Мечо"), ("food", "Храна"), ("shop", "Магазин"), ("wardrobe", "Гардероб"), ("tasks", "Задачи"), ("notes", "Бележки"), ("tasks", "Работа"), ("gear", "Още"),
        };
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            var label = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            var icon = Icon(tabs[i].Icon, 2);
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            label.Children.Add(icon);
            label.Children.Add(new TextBlock { Text = tabs[i].Name, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 3, 0, 0) });
            var tab = new Border
            {
                BorderBrush = Ink,
                CornerRadius = new CornerRadius(6, 6, 0, 0),
                Padding = new Thickness(2, 5 * U, 2, 5 * U),
                Cursor = Cursors.Hand,
                Child = label,
            };
            tab.MouseEnter += (_, _) => { if (tab.Background != Paper) tab.Background = Hover; };
            tab.MouseLeave += (_, _) => { if (tab.Background == Hover) tab.Background = WoodLight; };
            tab.MouseLeftButtonDown += (_, e) => e.Handled = true;
            tab.MouseLeftButtonUp += (_, e) =>
            {
                SelectTab(index);
                e.Handled = true;
            };
            Panel.SetZIndex(tab, 1);
            grid.Children.Add(tab);
            _tabButtons[i] = tab;
        }
        return grid;
    }

    // ───────────────────────── Мечо ─────────────────────────

    private UIElement BuildBearTab()
    {
        var p = new StackPanel();

        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.ColumnDefinitions.Add(new ColumnDefinition());

        // Портрет: мечокът на избрания фон; клик сменя фона.
        top.Children.Add(PortraitBox(out _portrait, out _portraitBg));

        var stats = new StackPanel { Margin = new Thickness(16 * U, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        _mood = new TextBlock { FontSize = Ui.Title, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 10 * U), TextWrapping = TextWrapping.Wrap };
        stats.Children.Add(_mood);
        // Времето навън: пикселна иконка (слънце, облак, дъжд…) и градусите; думите – при посочване.
        _weather = new TextBlock { FontSize = Ui.Body, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
        _weatherRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, -4 * U, 0, 8 * U), Background = Brushes.Transparent };
        _weatherRow.Children.Add(_weatherIcon);
        _weatherRow.Children.Add(_weather);
        stats.Children.Add(_weatherRow);
        stats.Children.Add(Bar("honey", "Ситост", out _fullBar, out _fullText));
        stats.Children.Add(Bar("energy", "Енергия", out _energyBar, out _energyText));
        Grid.SetColumn(stats, 1);
        top.Children.Add(stats);
        p.Children.Add(top);

        p.Children.Add(Ribbon("Какво да правим?"));
        var actions = new UniformGrid { Columns = 3 };
        actions.Children.Add(Wide(Button("Нахрани", () => SelectTab(FoodTab), icon: "food")));
        // Приспи: отваря избор колко да спи (вечер – и „Лягай за деня“). Спи ли – буди го.
        _sleepButton = Wide(Button("", () =>
        {
            if (_pet.IsAsleep) _pet.WakeUp();
            else _sleepOpen = !_sleepOpen;
            _quietOpen = false;
            Refresh(false);
        }));
        actions.Children.Add(_sleepButton);
        actions.Children.Add(Wide(Button("Погали", _pet.Pet, icon: "heart")));
        // Тихо: избор за колко (30 мин, 1 ч, 2 ч). Тих ли е – пак говори.
        _quietButton = Wide(Button("", () =>
        {
            if (_pet.IsQuiet) _pet.SetQuietFor(0);
            else _quietOpen = !_quietOpen;
            _sleepOpen = false;
            Refresh(false);
        }));
        actions.Children.Add(_quietButton);
        // Разходи се: излиза от екрана за 10 мин, 30 мин, 1 ч или колкото напишеш.
        _walkButton = Wide(Button("Разходи се", () =>
        {
            _walkOpen = !_walkOpen;
            _sleepOpen = _quietOpen = false;
            Refresh(false);
        }, icon: "walk"));
        actions.Children.Add(_walkButton);
        // „Почивай си“ / „Работи си“: мечокът сам сменя заниманията, докато не спре.
        _relaxButton = Wide(Button("Почивай си", () =>
        {
            if (_pet.RelaxesRandomly) _pet.GetUp();
            else _pet.RelaxRandom();
            Refresh(true);
        }, icon: "couch"));
        actions.Children.Add(_relaxButton);
        _workButton = Wide(Button("Работи си", () =>
        {
            if (_pet.WorksRandomly) _pet.GetUp();
            else _pet.WorkRandom();
            Refresh(true);
        }, icon: "tasks"));
        actions.Children.Add(_workButton);
        // Спри: спира каквото прави (кътче, батут, разходка) – таймерите си текат.
        _couchButton = Wide(Button("Спри", () =>
        {
            _pet.StopDoing();
            Refresh(true);
        }, icon: "stop"));
        actions.Children.Add(_couchButton);
        // Уреди (печица, лампа, радио…) и Мебели (бюро, диван, легло…): неща, които Тут
        // слага на земята (мечокът ги носи) и прибира.
        _placeButton = Wide(Button("Уреди", () =>
        {
            _placeOpen = _placeGroup != PlaceGroup.Devices || !_placeOpen;
            _placeGroup = PlaceGroup.Devices;
            _sleepOpen = _quietOpen = _walkOpen = false;
            Refresh(false);
        }, icon: "shop_heater"));
        actions.Children.Add(_placeButton);
        _furnitureButton = Wide(Button("Мебели", () =>
        {
            _placeOpen = _placeGroup != PlaceGroup.Furniture || !_placeOpen;
            _placeGroup = PlaceGroup.Furniture;
            _sleepOpen = _quietOpen = _walkOpen = false;
            Refresh(false);
        }, icon: "couch"));
        actions.Children.Add(_furnitureButton);
        // Рисувай – само със статива.
        _drawButton = Wide(Button("Рисувай", () => { Hide(); _pet.OpenDrawing(); }, icon: "pencil"));
        actions.Children.Add(_drawButton);
        p.Children.Add(actions);

        // Колко да спи: 3 готови + свое число (минути); вечер отпред – „Лягай за деня“.
        _sleepOptions.Margin = new Thickness(0, 2, 0, 6);
        p.Children.Add(_sleepOptions);
        _quietOptions.Margin = new Thickness(0, 2, 0, 6);
        foreach (var (label, minutes) in new[] { ("30 мин", 30), ("1 ч", 60), ("2 ч", 120) })
        {
            int m = minutes;
            var chip = Chip(label, () =>
            {
                _pet.SetQuietFor(m);
                _quietOpen = false;
                Refresh(false);
            });
            chip.Margin = new Thickness(0, 0, 6, 4);
            _quietOptions.Children.Add(chip);
        }
        p.Children.Add(_quietOptions);
        _walkOptions.Margin = new Thickness(0, 2, 0, 6);
        foreach (var (label, minutes) in new[] { ("10 мин", 10), ("30 мин", 30), ("1 ч", 60) })
        {
            int m = minutes;
            var chip = Chip(label, () => GoForWalk(m));
            chip.Margin = new Thickness(0, 0, 6, 4);
            _walkOptions.Children.Add(chip);
        }
        var walkOwn = NewTextInput();
        walkOwn.Width = 60 * U;
        walkOwn.VerticalAlignment = VerticalAlignment.Center;
        var walkHint = WithHint(walkOwn, "мин");
        ((FrameworkElement)walkHint).Margin = new Thickness(0, 0, 6, 4);
        _walkOptions.Children.Add(walkHint);
        var walkGo = Chip("Тръгвай", () =>
        {
            if (!double.TryParse(walkOwn.Text.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var m) || m <= 0 || m > 24 * 60)
            {
                walkOwn.BorderBrush = Bad;
                return;
            }
            walkOwn.Clear();
            GoForWalk(m);
        });
        walkGo.Margin = new Thickness(0, 0, 6, 4);
        _walkOptions.Children.Add(walkGo);
        p.Children.Add(_walkOptions);
        _placeOptions.Margin = new Thickness(0, 2, 0, 6);
        p.Children.Add(_placeOptions);

        // SOS: мечокът дава нещо за мотивация (листът е motivation.md в GitHub).
        var sos = Wide(Button("SOS: мотивация", () =>
        {
            Hide();
            _pet.Sos();
        }, icon: "heart"));
        sos.Margin = new Thickness(0, 0, 6, 6);
        p.Children.Add(sos);
        return new ScrollViewer { Content = p, MaxHeight = 600 * U, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private readonly WrapPanel _sleepOptions = new(), _quietOptions = new(), _walkOptions = new();
    private bool _sleepOpen, _quietOpen, _walkOpen, _placeOpen;
    private readonly WrapPanel _placeOptions = new();
    private Button _placeButton = null!, _furnitureButton = null!;

    private enum PlaceGroup { Devices, Furniture }

    private PlaceGroup _placeGroup = PlaceGroup.Devices;

    /// <summary>Уредите за слагане (печица, лампа…), по ред.</summary>
    private static readonly string[] Devices = { Kitchen.Heater, Kitchen.Lamp, Kitchen.Radio, Kitchen.Feeder, Kitchen.PlantPot };

    /// <summary>Какво може да се сложи (само купеното): светнало = сложено (или го носи); цъкане слага/прибира.</summary>
    private void BuildPlaceOptions()
    {
        var s = _pet.Save;
        _placeOptions.Children.Clear();
        void Add(string item, string label, bool on)
        {
            if (!Kitchen.Owns(s, item)) return;
            on |= _pet.Carrying(item);
            var chip = Chip((Kitchen.Find(item)?.Icon ?? "") + " " + label, () => { _pet.ToggleFromMenu(item); Refresh(false); });
            chip.Margin = new Thickness(0, 0, 6, 4);
            chip.ToolTip = Tip(on ? "Навън е – цъкни, за да го прибереш (ако мечокът го ползва, спира)." : "Цъкни: мечокът ще го сложи (зает ли е – появява се до него).");
            SetOn(chip, on);
            _placeOptions.Children.Add(chip);
        }
        if (_placeGroup == PlaceGroup.Devices)
        {
            Add(Kitchen.Heater, "Печица", s.HeaterPlaced);
            Add(Kitchen.Lamp, "Лампа", _pet.LampOn);
            Add(Kitchen.Radio, "Радио", _pet.RadioPlaced);
            Add(Kitchen.Feeder, "Хранилка", _pet.FeederPlaced);
            Add(Kitchen.PlantPot, "Растение", _pet.PlantOut);
            return;
        }
        Add(Kitchen.Desk, "Бюро", _pet.FurniturePlaced(Kitchen.Desk));
        Add(Kitchen.Computer, "Компютър", _pet.FurniturePlaced(Kitchen.Computer));
        Add(Kitchen.Easel, "Статив", _pet.FurniturePlaced(Kitchen.Easel));
        Add(Kitchen.BoardGame, "Настолна игра", _pet.FurniturePlaced(Kitchen.BoardGame));
        Add(Kitchen.YarnBasket, "Кошница с прежда", _pet.FurniturePlaced(Kitchen.YarnBasket));
        Add(Kitchen.Couch, "Диван", _pet.FurniturePlaced(Kitchen.Couch));
        Add(Kitchen.YogaMat, "Постелка", _pet.FurniturePlaced(Kitchen.YogaMat));
        Add(Kitchen.Bed, "Легло", _pet.FurniturePlaced(Kitchen.Bed));
    }
    private Button _walkButton = null!;

    private void GoForWalk(double minutes)
    {
        _walkOpen = false;
        Hide();
        _pet.GoForWalk(minutes);
    }
    private Button _drawButton = null!;
    private string _sleepOptionsKey = "";

    /// <summary>Изборът колко да спи (строи се наново, когато вечерта дойде).</summary>
    private void BuildSleepOptions()
    {
        bool late = PetWindow.LateForBed;
        string key = late ? "late" : "day";
        if (key == _sleepOptionsKey) return;
        _sleepOptionsKey = key;
        _sleepOptions.Children.Clear();
        void Add(UIElement e)
        {
            if (e is FrameworkElement f) f.Margin = new Thickness(0, 0, 6, 4);
            _sleepOptions.Children.Add(e);
        }
        if (late)
            Add(Button("Лягай за деня", () =>
            {
                _pet.SleepForDay();
                _sleepOpen = false;
                Refresh(false);
            }, primary: true, icon: "moon"));
        foreach (var (label, minutes) in new[] { ("20 мин", 20), ("1 ч", 60), ("2 ч", 120) })
        {
            int m = minutes;
            Add(Chip(label, () =>
            {
                _pet.Sleep(m);
                _sleepOpen = false;
                Refresh(false);
            }));
        }
        var own = NewTextInput();
        own.Width = 60 * U;
        own.VerticalAlignment = VerticalAlignment.Center;
        Add(WithHint(own, "мин"));
        Add(Chip("Спи", () =>
        {
            if (!double.TryParse(own.Text.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var m) || m <= 0 || m > 24 * 60)
            {
                own.BorderBrush = Bad;
                return;
            }
            _pet.Sleep(m);
            own.Clear();
            _sleepOpen = false;
            Refresh(false);
        }));
    }

    private readonly UniformGrid _relaxGrid = new() { Columns = 2 };
    private readonly UniformGrid _workGrid = new() { Columns = 2 };
    private Button _relaxButton = null!, _workButton = null!;
    private bool _relaxOpen, _workOpen;

    private void BuildRelaxButtons()
    {
        var s = _pet.Save;
        var chosen = _pet.CurrentFun;
        bool auto = _pet.AutoMode;
        bool random = _pet.RelaxesRandomly;
        SetOn(_relaxButton, random);
        bool working = _pet.WorksRandomly;
        SetOn(_workButton, working);
        FillSceneGrid(_relaxGrid, _relaxOpen, Scenes.Fun, chosen, random);
        FillSceneGrid(_workGrid, _workOpen, Scenes.Work, chosen, working);
    }

    /// <summary>Списъкът (☰) с кътчетата: избраното свети, цъкнеш ли го пак – спира.</summary>
    private void FillSceneGrid(UniformGrid grid, bool open, Scene[] scenes, Scene? chosen, bool random)
    {
        var s = _pet.Save;
        grid.Children.Clear();
        grid.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        foreach (var scene in scenes)
        {
            var sc = scene;
            bool unlocked = Scenes.IsUnlocked(sc, s);
            // Каквото прави сега, свети; цъкнеш ли го пак, спира.
            var b = Wide(Button(sc.Name, () =>
            {
                if (_pet.CurrentFun == sc) _pet.GetUp();
                else _pet.Relax(sc);
                Refresh(true);
            }, primary: chosen == sc && !random, icon: unlocked ? sc.Icon : "lock"));
            b.HorizontalContentAlignment = HorizontalAlignment.Left;
            if (!unlocked) b.IsEnabled = false;
            grid.Children.Add(b);
        }
    }

    private static Button Wide(Button b)
    {
        b.HorizontalAlignment = HorizontalAlignment.Stretch;
        b.Margin = new Thickness(0, 0, 6, 6);
        return b;
    }

    private static UIElement Bar(string icon, string label, out Border[] segments, out TextBlock value)
    {
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130 * U) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Children.Add(IconText(icon, label, bold: true));

        var track = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        segments = new Border[Segments];
        for (int i = 0; i < Segments; i++)
        {
            segments[i] = new Border
            {
                Width = 18 * U,
                Height = 20 * U,
                Margin = new Thickness(0, 0, 2, 0),
                BorderBrush = Ink,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(2),
                Background = Empty,
            };
            track.Children.Add(segments[i]);
        }
        Grid.SetColumn(track, 1);
        grid.Children.Add(track);

        // Думата (сит, гладен, бодър…) излиза като подсказка, когато мишката е върху лентата.
        value = new TextBlock { Foreground = Muted, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
        grid.Background = Brushes.Transparent;
        Grid.SetColumn(value, 2);
        grid.Children.Add(value);
        return grid;
    }

    private static void SetBar(Border[] segments, TextBlock text, double value, string word)
    {
        int filled = (int)Math.Ceiling(Math.Clamp(value, 0, 100) / 100 * segments.Length - 0.01);
        var colour = value >= 50 ? Good : value >= 25 ? Okay : Bad;
        for (int i = 0; i < segments.Length; i++) segments[i].Background = i < filled ? colour : Empty;
        if (text.Text == word) return;
        text.Text = word;
        if (text.Parent is FrameworkElement bar) bar.ToolTip = Tip(char.ToUpper(word[0]) + word[1..]);
    }

    // ───────────────────────── Храна ─────────────────────────

    private UIElement BuildFoodTab()
    {
        var p = new StackPanel();
        _foodInfo = new TextBlock { Foreground = Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 0, 0, 10) };
        p.Children.Add(_foodInfo);
        // Страниците за следващата рецепта: най-отгоре (посочиш ли ги – обяснение).
        _recipeProgress.Margin = new Thickness(0, 0, 0, 10 * U);
        p.Children.Add(_recipeProgress);
        _foodCats.Margin = new Thickness(0, 0, 0, 8 * U);
        p.Children.Add(_foodCats);
        p.Children.Add(_foodTiles);
        return new ScrollViewer { Content = p, MaxHeight = 560 * U, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private readonly WrapPanel _foodCats = new();

    private string _foodCat = "Всичко";
    private string? _foodKey;
    private const string Wish = "Желание";

    /// <summary>Плочки с храните: картинка, какво дава и бутон „Сготви“. Посочиш ли заключена, казва защо.</summary>
    private void BuildFoodTiles(bool force = false)
    {
        var s = _pet.Save;
        // Плочките (около 40, с барчета) струват десетки мс: строят се наново само
        // ако нещо, което се вижда в тях, се е сменило.
        var wishes = _pet.Wishes();
        string key = string.Join("|", _foodCat, s.Hazelnuts, (int)s.Fullness, (int)s.Energy, Kitchen.BerriesIn(s),
            _pet.FavoriteFood?.Id, string.Join(",", wishes.Select(f => f.Id)), s.Owned.Count, s.RecipePages,
            string.Join(",", s.Skins.Values));
        if (!force && key == _foodKey) return;
        _foodKey = key;
        _foodTiles.Children.Clear();
        // Раздели: Всичко, Манджи, Закуски, Сладко, Напитки (само тези, в които има нещо).
        var known = Kitchen.Foods.Where(f => Kitchen.HasRecipe(f, s)).ToList();
        _foodCats.Children.Clear();
        // „Желание“: какво му се иска днес (любимото и желанията) – дадеш ли му го, понякога връща услугата.
        var cats = new[] { "Всичко" }.Concat(wishes.Length > 0 ? new[] { Wish } : Array.Empty<string>())
            .Concat(Kitchen.Categories.Where(c => known.Any(f => Kitchen.CategoryOf(f) == c))).ToList();
        if (!cats.Contains(_foodCat)) _foodCat = "Всичко";
        foreach (var cat in cats)
        {
            string c = cat;
            var chip = Chip(c == Wish ? "❤ " + c : c, () => { _foodCat = c; BuildFoodTiles(); });
            chip.Margin = new Thickness(0, 0, 6, 4);
            SetOn(chip, c == _foodCat);
            _foodCats.Children.Add(chip);
        }
        // Само научените рецепти (боровинките винаги); новите идват от страници.
        var shown = _foodCat == Wish ? wishes.ToList()
            : known.Where(f => _foodCat == "Всичко" || Kitchen.CategoryOf(f) == _foodCat).ToList();
        foreach (var food in shown)
        {
            var f = food;
            int cost = Kitchen.CostOf(f, s);

            // Какво дава: барчета – сегашното и (в мед) колко ще добави.
            var effects = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            double full = Kitchen.FullnessOf(f, s);
            if (f.Fullness >= 5) effects.Children.Add(GainBar("honey", s.Fullness, Math.Min(100, s.Fullness + full), "ситост"));
            if (f.Energy > 0)
                effects.Children.Add(GainBar("energy", s.Energy, Kitchen.EnergyAfter(s.Energy, f.Energy), "енергия",
                    s.Energy >= Kitchen.FoodEnergyCap ? "Догоре – само със сън." : "Догоре – само със сън."));

            // Любимото за днес: сърчице (дадеш ли му го – радва се специално).
            if (wishes.Contains(f))
            {
                // Само сърчицето (без надпис): това му се иска днес.
                var fav = IconText("heart", "");
                fav.HorizontalAlignment = HorizontalAlignment.Center;
                fav.Margin = new Thickness(0, 2, 0, 0);
                effects.Children.Add(fav);
            }
            // Колко се готви във всеки уред (рецепта в два уреда – два реда): иконката на
            // уреда и минутите; ако уредът няма иконка – часовниче.
            foreach (var (tool, secs) in PetWindow.CookSteps(f))
            {
                string icon = LoadIcon("shop_" + tool) != null ? "shop_" + tool : "clock";
                var time = IconText(icon, secs % 60 == 0 ? $"{secs / 60:0} мин" : $"{secs:0} с");
                time.HorizontalAlignment = HorizontalAlignment.Center;
                time.Margin = new Thickness(0, 2, 0, 0);
                time.Opacity = 0.75;
                effects.Children.Add(time);
            }

            Button action;
            string? why = null;
            bool locked = !Kitchen.CanCook(f, s);
            if (locked)
            {
                // Рецептата я знае, но уредът го няма.
                var tool = Kitchen.Find(f.Tool!);
                action = Button(tool?.Name ?? "Заключено", () => OpenShop(), icon: "lock");
                why = $"Трябва „{tool?.Name}“.";
            }
            else if (cost == 0 && Kitchen.BerriesIn(s) > 0)
            {
                action = Button($"{Kitchen.BerriesIn(s)} мин", () => { }, icon: "hourglass");
                action.IsEnabled = false;
                why = $"Пак след {Kitchen.BerriesIn(s)} мин.";
            }
            else
            {
                action = Button(cost == 0 ? "Набери" : $"Сготви  {cost}", () =>
                {
                    // Храната се появява из екрана; панелът се скрива, за да я занесеш.
                    _pet.Cook(f);
                    Hide();
                }, primary: true, icon: cost == 0 ? null : "nut");
                if (s.Hazelnuts < cost)
                {
                    action.IsEnabled = false;
                    why = $"Трябват {cost} лешника, а имаш {s.Hazelnuts}.";
                }
            }
            _foodTiles.Children.Add(MakeTile(_pet.FoodMenuImage(f), f.Name, effects, action, locked, why));
        }
        BuildRecipeProgress();
    }

    private readonly StackPanel _recipeProgress = new() { Margin = new Thickness(0, 12, 0, 0) };

    /// <summary>„Следваща рецепта: ▮▮▯ 2/3 страници“ – коя е, е тайна.</summary>
    private void BuildRecipeProgress()
    {
        var s = _pet.Save;
        _recipeProgress.Children.Clear();
        if (!Recipes.Started(s)) return;
        var next = Recipes.Next(s);
        if (next == null)
        {
            _recipeProgress.Children.Add(MutedText("Знаеш всички рецепти!"));
            return;
        }
        int need = Recipes.PagesFor(next);
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(Icon("book", 2));
        row.Children.Add(new TextBlock
        {
            Text = "  Следваща рецепта: ",
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
        });
        for (int i = 0; i < need; i++)
            row.Children.Add(new Border
            {
                Width = 10 * U,
                Height = 14 * U,
                Margin = new Thickness(0, 0, 3, 0),
                Background = i < s.RecipePages ? Honey : Empty,
                BorderBrush = Ink,
                BorderThickness = new Thickness(2),
                VerticalAlignment = VerticalAlignment.Center,
            });
        row.Children.Add(new TextBlock { Text = $"  {s.RecipePages}/{need} страници", VerticalAlignment = VerticalAlignment.Center });
        // Обяснението е само на (?): трябва да задържиш мишката върху него.
        var help = new TextBlock { Text = "  (?)", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Help, Background = Brushes.Transparent };
        help.ToolTip = Tip("Страници от готварска книга.\nСъбереш ли достатъчно, мечокът научава нова рецепта за уредите, които имате.");
        ToolTipService.SetShowDuration(help, 60000);
        ToolTipService.SetInitialShowDelay(help, 600);
        row.Children.Add(help);
        _recipeProgress.Children.Add(row);
    }

    /// <summary>Значка: иконка и число (например мед +24).</summary>
    /// <summary>
    /// Малко барче от 10 деленца: колкото има сега (в цвят) и колкото ще добави
    /// храната (в мед).
    /// </summary>
    private static UIElement GainBar(string icon, double now, double after, string what, string? note = null)
    {
        const int segments = 10;
        int have = (int)Math.Ceiling(Math.Clamp(now, 0, 100) / 100 * segments - 0.01);
        int will = (int)Math.Ceiling(Math.Clamp(after, 0, 100) / 100 * segments - 0.01);
        var colour = now >= 50 ? Good : now >= 25 ? Okay : Bad;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
        var i0 = Icon(icon);
        i0.Margin = new Thickness(0, 0, 4, 0);
        i0.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(i0);
        for (int i = 0; i < segments; i++)
        {
            row.Children.Add(new Border
            {
                Width = 7 * U,
                Height = 10 * U,
                Margin = new Thickness(0, 0, 1, 0),
                Background = i < have ? colour : i < will ? Honey : Empty,
                BorderBrush = i >= have && i < will ? WoodDark : Brushes.Transparent,
                BorderThickness = new Thickness(i >= have && i < will ? 1 : 0),
            });
        }
        int gain = (int)Math.Round(after - now);
        return row;
    }

    private static Border Stat(string icon, string value, string what) => new()
    {
        Background = Cream,
        BorderBrush = WoodDark,
        BorderThickness = new Thickness(2),
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(6 * U, 2, 8 * U, 2),
        Margin = new Thickness(2, 0, 2, 0),
        Child = IconText(icon, value, bold: true),
    };

    // ───────────────────────── Магазин ─────────────────────────

    private UIElement BuildShopTab()
    {
        var p = new StackPanel();
        _shopInfo = new TextBlock { Foreground = Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 0, 0, 10) };
        p.Children.Add(_shopInfo);
        p.Children.Add(_shopTiles);
        return new ScrollViewer { Content = p, MaxHeight = 560 * U, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    /// <summary>Самопроверка: строи магазина (целта и офертата).</summary>
    internal void SelfTestShop()
    {
        SelectTab(ShopTab);
        _goalKey = "";
        BuildShopTiles();
        UpdateLayout();
    }

    /// <summary>Самопроверка: историята на задачите.</summary>
    internal void SelfTestHistory()
    {
        SelectTab(TasksTab);
        _showHistory = true;
        RefreshNow(true);
        UpdateLayout();
        _showHistory = false;
        RefreshNow(true);
    }

    /// <summary>Отваря магазина (например от гардероба или от заключена храна).</summary>
    private void OpenShop() => SelectTab(ShopTab);

    // Магазинът е от две части: „Събираме за…“ (избор от 3 неща, лентата се пълни
    // с лешниците) и офертата на деня (дрехи, тапети, скинове – след гардероба).
    private void BuildShopTiles()
    {
        _shopTiles.Children.Clear();
        _goalKey = "";
        BuildGoal();
        _shopTiles.Children.Add(_goalBox);
        BuildDailyOffer();
    }

    private readonly StackPanel _goalBox = new() { Margin = new Thickness(0, 0, 0, 12) };
    private string _goalKey = "";

    /// <summary>Целта: лента, която се пълни с лешниците, и „Купи“; или избор от три неща.</summary>
    private void BuildGoal()
    {
        var s = _pet.Save;
        var goal = _pet.Goal;
        var choices = _pet.GoalChoices;
        int filled = goal == null ? 0 : (int)Math.Min(Segments, Math.Floor((double)s.Hazelnuts / goal.Price * Segments));
        string key = $"{goal?.Id}|{string.Join(",", choices.Select(c => c.Id))}|{_pet.GoalRerollUsed}|{filled}|{s.Hazelnuts}|{_pet.ShopSoldOut}";
        if (key == _goalKey) return;
        _goalKey = key;
        _goalBox.Children.Clear();
        _goalBox.Children.Add(Ribbon("Събираме за…", "nut"));

        if (goal != null)
        {
            var row = new DockPanel { Margin = new Thickness(0, 2, 0, 4) };
            var img = Pixel(_pet.PropImage("shop_" + goal.Id), 4);
            img.Margin = new Thickness(0, 0, 10, 0);
            DockPanel.SetDock(img, Dock.Left);
            row.Children.Add(img);
            bool soldOut = _pet.ShopSoldOut;
            bool ready = s.Hazelnuts >= goal.Price && !soldOut;
            var buy = Button(soldOut ? "Утре" : ready ? $"Купи  {goal.Price}" : $"{Math.Min(s.Hazelnuts, goal.Price)}/{goal.Price}", () =>
            {
                _pet.TakeGoal();
                Refresh(true);
            }, primary: ready, icon: "nut");
            buy.IsEnabled = ready;
            buy.Margin = new Thickness(8, 0, 0, 0);
            DockPanel.SetDock(buy, Dock.Right);
            row.Children.Add(buy);
            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(new TextBlock { Text = goal.Name, FontWeight = FontWeights.Bold });
            info.Children.Add(new TextBlock { Text = goal.Description, Foreground = Muted, TextWrapping = TextWrapping.Wrap });
            var track = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            for (int i = 0; i < Segments; i++)
                track.Children.Add(new Border
                {
                    Width = 14 * U, Height = 14 * U, Margin = new Thickness(0, 0, 2, 0),
                    BorderBrush = Ink, BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(2),
                    Background = i < filled ? Good : Empty,
                });
            info.Children.Add(track);
            row.Children.Add(info);
            _goalBox.Children.Add(row);
            if (soldOut) _goalBox.Children.Add(MutedText("Няма повече продукти за днес – провери пак утре."));
            return;
        }

        if (choices.Count == 0)
        {
            _goalBox.Children.Add(MutedText("Имаш всичко! Скоро ще има още неща."));
            return;
        }

        bool start = choices.Count == 1 && choices[0].Id == Kitchen.Wardrobe;
        _goalBox.Children.Add(MutedText(start
            ? "Избери за какво да събираме лешници. Почваме с гардероба!"
            : _pet.GoalRerollUsed
                ? "Избери за какво да събираме лешници:"
                : "Избери за какво да събираме лешници:"));
        var grid = new UniformGrid { Columns = 3 };
        if (start) grid.Children.Add(LockedChoice());
        foreach (var u in choices) grid.Children.Add(GoalChoice(u, canReroll: !start && !_pet.GoalRerollUsed));
        if (start) grid.Children.Add(LockedChoice());
        _goalBox.Children.Add(grid);
    }

    /// <summary>Едно от трите неща за избор: картинка, цена, „Избери“; горе вдясно ↻ го сменя с друго.</summary>
    private UIElement GoalChoice(Upgrade u, bool canReroll)
    {
        var details = new StackPanel();
        var price = RichText($"{u.Price} 🌰");
        price.HorizontalAlignment = HorizontalAlignment.Center;
        details.Children.Add(price);
        details.Children.Add(Description(u.Description));
        var pick = Button("Избери", () =>
        {
            _pet.ChooseGoal(u.Id);
            Refresh(true);
        }, primary: true);
        var tile = MakeTile(_pet.PropImage("shop_" + u.Id), u.Name, details, pick, dim: false, null);
        if (!canReroll) return tile;
        // Малко копче със стрелки в горния десен ъгъл: сменя това нещо с друго (веднъж).
        var reroll = Chip("🔄", () =>
        {
            _pet.RerollGoal(u.Id);
            Refresh(true);
        });
        reroll.ToolTip = Tip("Смени с друго");
        reroll.Padding = new Thickness(4, 1, 4, 2);
        reroll.HorizontalAlignment = HorizontalAlignment.Right;
        reroll.VerticalAlignment = VerticalAlignment.Top;
        reroll.Margin = new Thickness(0, 6, 12, 0);
        return new Grid { Children = { tile, reroll } };
    }

    /// <summary>Заключена „?“: в началото до гардероба.</summary>
    private Border LockedChoice()
    {
        var action = Button("Заключено", () => { }, icon: "lock");
        action.IsEnabled = false;
        return MakeTile(_pet.PropImage("mystery"), "???", Description("Ще се отключи след гардероба."), action, dim: true,
            "Първо гардеробът! После тук ще има избор.");
    }

    /// <summary>
    /// Офертата на деня: 3 неща (дрехи, тапети, скинове), нови всеки ден в 00:00.
    /// Без гардероб няма дрехи и тапети.
    /// </summary>
    private void BuildDailyOffer()
    {
        var s = _pet.Save;
        // Без гардероб офертата я няма изобщо (в нея са само дрехи, тапети и дамаски).
        if (!DailyOffer.HasWardrobe(s)) return;
        // Офертата е на по-тъмна подложка, за да се отделя от целта отгоре.
        var box = new StackPanel();
        _shopTiles.Children.Add(new Border
        {
            Background = Empty,
            BorderBrush = Ink,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8 * U, 6 * U, 8 * U, 8 * U),
            Child = box,
        });
        box.Children.Add(Ribbon("Оферта на деня", "gift"));

        var offer = DailyOffer.Today(s);
        var grid = new UniformGrid { Columns = 3 };
        foreach (var key in offer)
        {
            switch (DailyOffer.Resolve(key))
            {
                case Garment g: grid.Children.Add(GarmentShopTile(g)); break;
                case Backdrop b: grid.Children.Add(BackdropTile(b)); break;
                case Skin k: grid.Children.Add(SkinTile(k, shop: true)); break;
                case PajamaSet p: grid.Children.Add(PajamaTile(p, shop: true)); break;
                case UiTheme t: grid.Children.Add(ThemeTile(t, shop: true)); break;
            }
        }
        if (grid.Children.Count > 0) box.Children.Add(grid);
        if (!DailyOffer.HasWardrobe(s) || offer.Count == 0)
        {
            box.Children.Add(new TextBlock
            {
                Text = DailyOffer.HasWardrobe(s)
                    ? "Днес няма нищо ново."
                    : "Първо гардеробът.",
                Foreground = Ink,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(2, 6 * U, 0, 0),
            });
        }
    }

    /// <summary>Бутон „Купи N“ (изключен с обяснение, ако не стигат лешниците).</summary>
    private Button BuyButton(int price, Action buy, out string? why)
    {
        int nuts = _pet.Save.Hazelnuts;
        var action = Button($"Купи  {price}", buy, primary: true, icon: "nut");
        why = null;
        if (nuts < price)
        {
            action.IsEnabled = false;
            why = $"Още {price - nuts} лешника.";
        }
        return action;
    }

    private static TextBlock Description(string text) => new()
    {
        Text = text,
        Foreground = Muted,
        TextAlignment = TextAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
    };

    private Border GarmentShopTile(Garment g)
    {
        bool owned = Clothes.Owns(_pet.Save, g);
        string? why = null;
        Button action = owned
            ? Button("В гардероба", () => SelectTab(WardrobeTab), icon: "wardrobe")
            : BuyButton(g.Price, () => _pet.BuyGarment(g), out why);
        if (owned) why = "Вече е в гардероба.";
        var art = _pet.GarmentPreview(g);
        int scale = Math.Clamp(80 / Math.Max(art.PixelWidth, art.PixelHeight), 3, 8);
        return MakeTile(art, g.Name, Description(""), action, dim: false, why, owned, scale);
    }

    /// <summary>„В гардероба“: купеното от офертата се слага/сменя от гардероба (отваря раздела).</summary>
    private Button ToWardrobe(string section, out string? why)
    {
        why = "Вече е в гардероба.";
        return Button("В гардероба", () => { _wardrobeSection = section; SelectTab(WardrobeTab); }, icon: "wardrobe");
    }

    private Border BackdropTile(Backdrop b)
    {
        var s = _pet.Save;
        bool owned = Backdrops.Owns(s, b), used = s.Background == b.Id;
        string? why = null;
        Button action;
        if (owned) action = ToWardrobe("wallpapers", out why);   // от офертата: купеното се слага от гардероба
        else action = BuyButton(b.Price, () => _pet.BuyBackdrop(b), out why);
        return MakeTile(_pet.BackdropImage(b), b.Name, Description("Тапет за портрета на Мечо."), action, dim: false, why, owned, 2);
    }

    /// <summary>Цвят на менюто: в магазина се купува (и отива в гардероба), в гардероба се слага.</summary>
    private Border ThemeTile(UiTheme t, bool shop)
    {
        var s = _pet.Save;
        bool owned = UiThemes.Owns(s, t), used = UiThemes.Current(s) == t;
        string? why = null;
        Button action;
        if (shop && owned) action = ToWardrobe("themes", out why);
        else if (used)
        {
            action = Button("Сложен", () => { });
            action.IsEnabled = false;
        }
        else if (owned) action = Button("Сложи", () => { _pet.UseUiTheme(t); BuildWardrobe(); }, primary: true);
        else action = BuyButton(t.Price, () => { _pet.BuyUiTheme(t); BuildWardrobe(); }, out why);
        return MakeTile(UiThemes.Preview(t), t.Name, Description("Цвят на меню"), action, dim: false, why, owned && (used || !shop), 3);
    }

    /// <summary>Пижама: в магазина се купува (и става избраната), в гардероба се избира.</summary>
    private Border PajamaTile(PajamaSet p, bool shop)
    {
        var s = _pet.Save;
        bool owned = Clothes.Owns(s, p), used = Clothes.CurrentPajama(s) == p;
        string? why = null;
        Button action;
        if (shop && owned) action = ToWardrobe("pajamas", out why);
        else if (used)
        {
            action = Button("Избрана", () => { });
            action.IsEnabled = false;
        }
        else if (owned) action = Button("Избери", () => _pet.UsePajama(p), primary: true);
        else action = BuyButton(p.Price, () => _pet.BuyPajama(p), out why);
        return MakeTile(_pet.PajamaPreview(p), p.Name, Description(shop ? "Пижама за вечерта." : ""), action, dim: false, why, owned && (used || !shop), 2);
    }

    /// <summary>Скин за мебел или уред: в магазина се купува, в гардероба се слага.</summary>
    private Border SkinTile(Skin skin, bool shop)
    {
        var s = _pet.Save;
        bool owned = Furniture.Owns(s, skin), used = Furniture.Current(s, skin.Item) == skin;
        bool hasItem = Kitchen.Owns(s, skin.Item);
        string? why = null;
        Button action;
        if (shop && owned) action = ToWardrobe("furniture", out why);
        else if (used)
        {
            action = Button("Сложен", () => { });
            action.IsEnabled = false;
        }
        else if (owned) action = Button("Сложи", () => _pet.UseSkin(skin), primary: true);
        else
        {
            action = BuyButton(skin.Price, () => _pet.BuySkin(skin), out why);
            if (!hasItem)
            {
                action.IsEnabled = false;
                why = $"Първо купи „{Kitchen.Find(skin.Item)?.Name}“.";
            }
        }
        string note = shop ? $"Друг вид „{Kitchen.Find(skin.Item)?.Name}“." : "";
        return MakeTile(_pet.SkinImage(skin), skin.Name, Description(note), action, dim: false, why, owned && (used || !shop), 2);
    }

    /// <summary>
    /// Плочка: картинка, име, какво дава и бутон. Бутонът е закрепен отдолу, така че
    /// в мрежата всички бутони стоят на една линия, колкото и дълги да са имената.
    /// why = подсказка при посочване (защо е заключено и т.н.).
    /// </summary>
    private static Border MakeTile(BitmapSource art, string name, UIElement details, Button action, bool dim, string? why, bool owned = false, int artScale = 4)
    {
        var image = Pixel(art, ArtScale(artScale));
        image.HorizontalAlignment = HorizontalAlignment.Center;
        image.VerticalAlignment = VerticalAlignment.Center;
        if (dim) image.Opacity = 0.35;
        var artGrid = new Grid();
        artGrid.Children.Add(image);
        if (dim || owned)
        {
            // Катинарче или отметка в ъгъла.
            var badge = owned
                ? Icon("check", 2)
                : Icon("lock");
            badge.HorizontalAlignment = HorizontalAlignment.Right;
            badge.VerticalAlignment = VerticalAlignment.Top;
            badge.Margin = new Thickness(0, 4, 6, 0);
            artGrid.Children.Add(badge);
        }
        var artBox = new Border
        {
            Background = TileArt,
            BorderBrush = WoodDark,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(4),
            MinHeight = 96 * U,
            Child = artGrid,
        };

        var dock = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(artBox, Dock.Top);
        dock.Children.Add(artBox);

        action.Margin = new Thickness(0, 8 * U, 0, 0);
        action.HorizontalAlignment = HorizontalAlignment.Stretch;
        // Дълъг надпис („Рецепта 10“) в тясна плочка: смалява се, вместо да излезе от кутията.
        // Надписът първо се откача от бутона: WPF не позволява два „родителя“.
        if (action.Content is UIElement content)
        {
            action.Content = null;
            action.Content = new Viewbox { Child = content, StretchDirection = StretchDirection.DownOnly };
        }
        DockPanel.SetDock(action, Dock.Bottom);
        dock.Children.Add(action);

        var middle = new StackPanel { VerticalAlignment = VerticalAlignment.Top };
        middle.Children.Add(new TextBlock
        {
            Text = name,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8 * U, 0, 6 * U),
        });
        middle.Children.Add(details);
        dock.Children.Add(middle);

        var tile = new Border
        {
            Background = Tile,
            BorderBrush = Ink,
            BorderThickness = Flat,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8 * U),
            Margin = new Thickness(0, 0, 8 * U, 8 * U),
            Child = dock,
        };
        tile.MouseEnter += (_, _) => tile.Background = Hover;
        tile.MouseLeave += (_, _) => tile.Background = Tile;
        if (why != null && (dim || !action.IsEnabled))
        {
            // Подсказката излиза и над изключения бутон (само за заключеното).
            ToolTipService.SetShowOnDisabled(action, true);
            ToolTipService.SetInitialShowDelay(tile, 200);
        }
        return tile;
    }

    // ───────────────────────── Гардероб ─────────────────────────

    private UIElement BuildWardrobeTab()
    {
        var p = new StackPanel();
        var top = new Grid { Margin = new Thickness(0, 0, 0, 6 * U) };
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.ColumnDefinitions.Add(new ColumnDefinition());

        // Мечокът в цял ръст с облеклото, като в огледало.
        top.Children.Add(PortraitBox(out _model, out _modelBg));

        var right = new StackPanel { Margin = new Thickness(16 * U, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        right.Children.Add(new TextBlock { Text = "Облечено", FontSize = Ui.Title, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 6 * U) });
        right.Children.Add(_wornList);
        _pajamaChip = Chip("", () => _pet.SetPajamas(!_pet.Save.PajamasAtNight));
        _pajamaChip.HorizontalAlignment = HorizontalAlignment.Left;
        _pajamaChip.Margin = new Thickness(0, 6 * U, 0, 0);
        right.Children.Add(_pajamaChip);
        right.Children.Add(new TextBlock
        {
            Text = "Облечи нещо на мечока.",
            Foreground = Muted,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6 * U, 0, 0),
        });
        Grid.SetColumn(right, 1);
        top.Children.Add(right);
        _wardrobeBody.Children.Add(top);
        _wardrobeBody.Children.Add(_wardrobeShelves);
        p.Children.Add(_wardrobeBody);

        // Без гардероб: заключено, с бутон към магазина.
        _wardrobeLock.Children.Add(Ribbon("Гардеробът е заключен", "lock"));
        _wardrobeLock.Children.Add(new TextBlock
        {
            Text = "Първо гардеробът – избери го в магазина.",
            Foreground = Muted,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(2, 0, 0, 10 * U),
        });
        var go = Button("Към магазина", () => OpenShop(), primary: true, icon: "shop");
        go.HorizontalAlignment = HorizontalAlignment.Center;
        _wardrobeLock.Children.Add(go);
        p.Children.Add(_wardrobeLock);
        return new ScrollViewer { Content = p, MaxHeight = 560 * U, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private Border _pajamaChip = null!;

    /// <summary>Предметите, за които има поне един купен друг вид (само тогава се виждат и класическите).</summary>
    private static IEnumerable<string> SkinItems(SaveData s) =>
        Furniture.ByItem.Where(kv => Kitchen.Owns(s, kv.Key) && kv.Value.Any(k => k.Price > 0 && Furniture.Owns(s, k))).Select(kv => kv.Key);

    private void BuildWardrobe()
    {
        var s = _pet.Save;
        _pajamaChip.Child = IconText("moon", s.PajamasAtNight
            ? (_pet.PajamaTime ? "Пижама вечер: вкл. (сега е с пижама)" : "Пижама вечер: вкл.")
            : "Пижама вечер: изкл.");
        SetOn(_pajamaChip, s.PajamasAtNight);
        _model.Source = _pet.Portrait;

        _wornList.Children.Clear();
        foreach (var slot in Clothes.Shelves)
        {
            var g = Clothes.Worn(s, slot);
            if (g == null) continue;   // само облеченото
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 3) };
            // Иконката на вида дреха (шапка, шал, блуза…), а името казва коя е.
            var slotIcon = Icon(slot.Icon, 2);
            slotIcon.HorizontalAlignment = HorizontalAlignment.Center;
            FrameworkElement icon = new Grid { Width = 36 * U, Children = { slotIcon } };
            icon.ToolTip = Tip(slot.Name);
            icon.Margin = new Thickness(0, 0, 6, 0);
            icon.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(icon, Dock.Left);
            row.Children.Add(icon);
            if (g != null)
            {
                var off = Chip("✕", () => _pet.TakeOff(slot));
                off.VerticalAlignment = VerticalAlignment.Center;
                DockPanel.SetDock(off, Dock.Right);
                row.Children.Add(off);
            }
            row.Children.Add(new TextBlock
            {
                Text = g?.Name ?? "—",
                Foreground = g == null ? Muted : Ink,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            _wornList.Children.Add(row);
        }

        if (_wornList.Children.Count == 0) _wornList.Children.Add(MutedText("Нищо."));
        _wardrobeShelves.Children.Clear();
        _wardrobeLock.Visibility = DailyOffer.HasWardrobe(s) ? Visibility.Collapsed : Visibility.Visible;
        _wardrobeBody.Visibility = DailyOffer.HasWardrobe(s) ? Visibility.Visible : Visibility.Collapsed;
        if (!DailyOffer.HasWardrobe(s)) return;

        // Раздели: всеки вид дреха/аксесоар, тапетите и мебелите (ако има диван или легло).
        // Раздел се вижда чак когато има нещо в него (за безплатните – поне едно купено).
        var sections = Clothes.Shelves
            .Where(slot => Clothes.All.Any(g => g.SlotId == slot.Id && !g.Hidden && Clothes.Owns(s, g)))
            .Select(slot => (Id: slot.Id, Name: slot.Name, Icon: slot.Icon)).ToList();
        if (Clothes.PajamaSets.Count(p => Clothes.Owns(s, p)) > 1) sections.Add(("pajamas", "Пижами", "pajama"));
        if (UiThemes.All.Count(t => UiThemes.Owns(s, t)) > 1) sections.Add(("themes", "Цвят на меню", "menu"));
        if (Backdrops.All.Count(b => Backdrops.Owns(s, b)) > 1) sections.Add(("wallpapers", "Тапети", "picture"));
        if (SkinItems(s).Any()) sections.Add(("furniture", "Мебели и уреди", "couch"));
        if (sections.Count == 0)
        {
            _wardrobeShelves.Children.Add(MutedText("Гардеробът е празен. Нови неща има в офертата на деня."));
            return;
        }
        if (!sections.Any(x => x.Id == _wardrobeSection)) _wardrobeSection = sections[0].Id;

        var bar = new WrapPanel { Margin = new Thickness(0, 0, 0, 8 * U) };
        foreach (var section in sections)
        {
            string id = section.Id;
            var chip = Chip("", () =>
            {
                _wardrobeSection = id;
                BuildWardrobe();
            });
            chip.Child = IconText(section.Icon, section.Name);
            SetOn(chip, id == _wardrobeSection);
            chip.Margin = new Thickness(0, 0, 5, 5);
            bar.Children.Add(chip);
        }
        _wardrobeShelves.Children.Add(bar);

        var grid = new UniformGrid { Columns = 3 };
        _wardrobeShelves.Children.Add(grid);
        string empty;
        if (_wardrobeSection == "wallpapers")
        {
            foreach (var backdrop in Backdrops.All.Where(b => Backdrops.Owns(s, b)))
            {
                var b = backdrop;
                bool used = s.Background == b.Id;
                Button action;
                if (used)
                {
                    action = Button("Сложен", () => { });
                    action.IsEnabled = false;
                }
                else action = Button("Сложи", () => _pet.UseBackdrop(b), primary: true);
                grid.Children.Add(MakeTile(_pet.BackdropImage(b), b.Name, new TextBlock(), action, dim: false, null, owned: used, artScale: 2));
            }
            empty = "Нови тапети има в офертата на деня.";
        }
        else if (_wardrobeSection == "themes")
        {
            // Скиновете на менюто идват от офертата на деня и от подаръците.
            foreach (var t in UiThemes.All.Where(t => UiThemes.Owns(s, t))) grid.Children.Add(ThemeTile(t, shop: false));
            empty = "";
        }
        else if (_wardrobeSection == "pajamas")
        {
            foreach (var p in Clothes.PajamaSets.Where(p => Clothes.Owns(s, p))) grid.Children.Add(PajamaTile(p, shop: false));
            empty = "Нови пижами има в офертата на деня.";
        }
        else if (_wardrobeSection == "furniture")
        {
            foreach (var item in SkinItems(s))
                    foreach (var skin in Furniture.SkinsFor(item).Where(k => Furniture.Owns(s, k))) grid.Children.Add(SkinTile(skin, shop: false));
            empty = "Други видове има в офертата на деня.";
        }
        else
        {
            var slot = Clothes.Shelves.First(x => x.Id == _wardrobeSection);
            foreach (var garment in Clothes.All.Where(g => g.SlotId == slot.Id && !g.Hidden && Clothes.Owns(s, g)))
            {
                var g = garment;
                bool worn = Clothes.IsWorn(s, g);
                var action = worn ? Button("Свали", () => _pet.TakeOff(slot)) : Button("Облечи", () => _pet.Wear(g), primary: true);
                // Плочката показва самата дреха; по-малките са по-увеличени.
                var art = _pet.GarmentPreview(g);
                int scale = Math.Clamp(80 / Math.Max(art.PixelWidth, art.PixelHeight), 3, 8);
                var details = new TextBlock
                {
                    Text = worn ? "Облечено" : "",
                    Foreground = Muted,
                    TextAlignment = TextAlignment.Center,
                };
                grid.Children.Add(MakeTile(art, g.Name, details, action, dim: false, null, owned: worn, artScale: scale));
            }
            empty = $"Още нямаш {slot.Name.ToLowerInvariant()}.";
        }
        if (grid.Children.Count == 0) _wardrobeShelves.Children.Add(MutedText(empty));
        var more = Button("Офертата на деня", () => OpenShop(), icon: "shop");
        more.HorizontalAlignment = HorizontalAlignment.Center;
        more.Margin = new Thickness(0, 10 * U, 0, 0);
        _wardrobeShelves.Children.Add(more);
    }

    private string _wardrobeSection = "hat";
    private readonly StackPanel _wardrobeBody = new();
    private readonly StackPanel _wardrobeLock = new();

    /// <summary>Самопроверка: гардеробът с дрехите и фоновете.</summary>
    internal void SelfTestWardrobe()
    {
        SelectTab(WardrobeTab);
        BuildWardrobe();
        UpdateLayout();
    }

    // ───────────────────────── Задачи ─────────────────────────

    /// <summary>
    /// „Работа“: отгоре три копчета – Мечо-доро | Таймер | Задачи – и под тях само
    /// избраната част, на цялата ширина (вместо три неща, натъпкани едно до друго).
    /// </summary>
    private UIElement BuildWorkTab(UIElement timers, UIElement tasks)
    {
        // BuildPomodoroTab строи мечо-дорото и таймера заедно: делим ги при лентата „Таймер“.
        var all = (StackPanel)timers;
        var pomodoro = new StackPanel();
        var timer = new StackPanel();
        bool inTimer = false;
        foreach (var child in all.Children.Cast<UIElement>().ToList())
        {
            if (child == _timerRibbon) inTimer = true;
            all.Children.Remove(child);
            if (child == _timerRibbon) continue;
            (inTimer ? timer : pomodoro).Children.Add(child);
        }
        // „Работя по:“ стои най-отгоре и важи и за мечо-дорото, и за работния таймер.
        _workParts[0] = pomodoro;
        _workParts[1] = timer;
        _workParts[2] = tasks;

        var root = new StackPanel();
        var bar = new UniformGrid { Rows = 1, Margin = new Thickness(0, 0, 0, 10 * U) };
        (string Icon, string Name)[] parts = { ("honey", "Мечо-доро"), ("clock", "Таймер"), ("tasks", "Задачи") };
        for (int i = 0; i < parts.Length; i++)
        {
            int index = i;
            var chip = Chip("", () => ShowWorkPart(index));
            chip.Child = IconText(parts[i].Icon, parts[i].Name);
            ((FrameworkElement)chip.Child).HorizontalAlignment = HorizontalAlignment.Center;
            chip.Margin = new Thickness(i == 0 ? 0 : 6, 0, 0, 0);
            _workChips[i] = chip;
            bar.Children.Add(chip);
        }
        root.Children.Add(bar);
        foreach (var part in _workParts) root.Children.Add(part);
        ShowWorkPart(_workPart);
        return root;
    }

    private readonly UIElement[] _workParts = new UIElement[3];
    private readonly Border[] _workChips = new Border[3];
    private int _workPart;

    private void ShowWorkPart(int index)
    {
        _workPart = index;
        for (int i = 0; i < 3; i++)
        {
            _workParts[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;
            SetOn(_workChips[i], i == index);
        }
        if (index == 2 && IsVisible) Dispatcher.BeginInvoke(() => _taskInput.Focus(), DispatcherPriority.Input);
    }

    private UIElement BuildTasksTab()
    {
        var p = new StackPanel();
        _taskInput = NewTextInput();
        _taskInput.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) AddTask();
        };
        p.Children.Add(WithHint(_taskInput, "Име на задачата…"));
        // Какво точно е (по избор): в закачения прозорец излиза при посочване на задачата.
        _taskText = NewTextInput();
        _taskText.AcceptsReturn = true;
        _taskText.TextWrapping = TextWrapping.Wrap;
        _taskText.MaxHeight = 90 * U;
        _taskText.Margin = new Thickness(0, 6, 0, 0);
        _taskText.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                AddTask();
                e.Handled = true;
            }
        };
        p.Children.Add(WithHint(_taskText, "Какво включва? (по избор)"));

        var sizes = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        string[] labels = { "малка", "средна", "голяма" };
        for (int i = 0; i < 3; i++)
        {
            int size = i + 1;
            _sizeButtons[i] = Chip(labels[i], () =>
            {
                _taskSize = size;
                UpdateSizeButtons();
            });
            _sizeButtons[i].Margin = new Thickness(0, 0, 6, 0);
            _sizeButtons[i].VerticalAlignment = VerticalAlignment.Center;
            sizes.Children.Add(_sizeButtons[i]);
        }
        var add = Button("Добави", AddTask, primary: true, icon: "plus");
        add.Margin = new Thickness(4, 0, 0, 0);
        sizes.Children.Add(add);
        p.Children.Add(sizes);
        UpdateSizeButtons();

        p.Children.Add(new ScrollViewer
        {
            Content = _taskList,
            MaxHeight = 360 * U,
            Margin = new Thickness(0, 12, 0, 0),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        });

        var bottom = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        _historyButton = Chip("История", () =>
        {
            _showHistory = !_showHistory;
            Refresh(true);
        });
        DockPanel.SetDock(_historyButton, Dock.Right);
        bottom.Children.Add(_historyButton);
        _pinTasks = Chip("📌 Закачи", () => _pet.SetPinned(PinWindow.Tasks, !_pet.IsPinned(PinWindow.Tasks)));
        _pinTasks.ToolTip = Tip("Задачите в малко прозорче, което стои на екрана");
        _pinTasks.Margin = new Thickness(0, 0, 6, 0);
        DockPanel.SetDock(_pinTasks, Dock.Right);
        bottom.Children.Add(_pinTasks);
        _taskSummary = new TextBlock { Foreground = Muted, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        bottom.Children.Add(_taskSummary);
        p.Children.Add(bottom);
        return p;
    }

    private Border _historyButton = null!, _pinTasks = null!, _pinNotes = null!;
    private bool _showHistory;

    /// <summary>Историята: свършените задачи, групирани по дни (най-новите горе).</summary>
    private void BuildTaskHistory()
    {
        var s = _pet.Save;
        var done = s.Tasks.Where(t => t.Done).Concat(s.TaskHistory)
            .Where(t => t.DoneAt != null)
            .OrderByDescending(t => t.DoneAt)
            .ToList();
        if (done.Count == 0)
            _taskList.Children.Add(MutedText("Още няма свършени задачи. Първата ще се появи тук!"));

        var culture = new System.Globalization.CultureInfo("bg-BG");
        foreach (var day in done.GroupBy(t => t.DoneAt!.Value.Date))
        {
            string name = day.Key == DateTime.Today ? "Днес"
                : day.Key == DateTime.Today.AddDays(-1) ? "Вчера"
                : day.Key.ToString("d MMMM, dddd", culture);
            int nuts = day.Sum(t => t.Reward);
            var header = new DockPanel { Margin = new Thickness(0, _taskList.Children.Count == 0 ? 0 : 10, 0, 4) };
            var total = RewardText(nuts, bold: true);
            DockPanel.SetDock(total, Dock.Right);
            header.Children.Add(total);
            header.Children.Add(new TextBlock { Text = $"{name}  ·  {day.Count()}", FontWeight = FontWeights.Bold });
            _taskList.Children.Add(header);

            foreach (var t in day)
            {
                var row = new DockPanel();
                var reward = RewardText(t.Reward);
                reward.Opacity = 0.7;
                DockPanel.SetDock(reward, Dock.Right);
                row.Children.Add(reward);
                var time = new TextBlock { Text = t.DoneAt!.Value.ToString("HH:mm"), Foreground = Muted, Margin = new Thickness(0, 0, 8, 0) };
                DockPanel.SetDock(time, Dock.Left);
                row.Children.Add(time);
                row.Children.Add(new TextBlock { Text = t.Title, TextWrapping = TextWrapping.Wrap });
                _taskList.Children.Add(Card(row, faded: false));
            }
        }
        _taskSummary.Text = $"Общо свършени: {s.TasksDoneTotal}";
    }

    private void BuildTaskList()
    {
        _taskList.Children.Clear();
        SetOn(_historyButton, _showHistory);
        SetOn(_pinTasks, _pet.IsPinned(PinWindow.Tasks));
        SetOn(_pinNotes, _pet.IsPinned(PinWindow.Notes));
        if (_showHistory)
        {
            BuildTaskHistory();
            return;
        }
        // Свършените се виждат само в „История“.
        var tasks = _pet.Save.Tasks.Where(t => !t.Done).ToList();
        if (tasks.Count == 0)
            _taskList.Children.Add(MutedText("Няма задачи."));

        foreach (var task in tasks)
        {
            var t = task;
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var check = new CheckBox
            {
                IsChecked = t.Done,
                VerticalContentAlignment = VerticalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Content = new TextBlock
                {
                    Text = t.Title,
                    TextWrapping = TextWrapping.Wrap,
                    TextDecorations = t.Done ? TextDecorations.Strikethrough : null,
                    Foreground = t.Done ? Muted : Ink,
                    Margin = new Thickness(4, 0, 0, 0),
                },
            };
            check.Click += (_, _) => _pet.SetTaskDone(t, check.IsChecked == true);
            if (t.Description.Length > 0)
            {
                var both = new StackPanel { Margin = new Thickness(4, 0, 0, 0) };
                var head = (TextBlock)check.Content;
                head.Margin = new Thickness(0);
                check.Content = both;
                both.Children.Add(head);
                both.Children.Add(new TextBlock { Text = t.Description, Foreground = Muted, TextWrapping = TextWrapping.Wrap });
            }
            row.Children.Add(check);

            var reward = RewardText(t.Reward, bold: true);
            reward.Margin = new Thickness(8, 0, 8, 0);
            reward.Opacity = t.Done ? 0.5 : 1;
            Grid.SetColumn(reward, 1);
            row.Children.Add(reward);

            // ▶ – мечо-доро по задачата; по която работим, свети.
            bool current = _pet.Save.FocusTask == t.Id;
            var play = Chip(current && _pet.InFocus ? "★" : "▶", () =>
            {
                if (_pet.InFocus) _pet.SetFocusTask(t);
                else _pet.StartFocusOn(t);
            });
            play.ToolTip = Tip(_pet.InFocus ? "Сега работим по нея" : "Мечо-доро по тази задача");
            play.VerticalAlignment = VerticalAlignment.Center;
            play.Margin = new Thickness(0, 0, 6, 0);
            SetOn(play, current);
            Grid.SetColumn(play, 2);
            row.Children.Add(play);

            var del = Chip("✕", () => _pet.DeleteTask(t));
            del.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(del, 3);
            row.Children.Add(del);

            _taskList.Children.Add(Card(row, t.Done));
        }

        int today = _pet.Save.Tasks.Count(t => t.Done);
        _taskSummary.Text = $"Свършени днес: {today}   ·   Общо: {_pet.Save.TasksDoneTotal}";
    }

    private void AddTask()
    {
        if (string.IsNullOrWhiteSpace(_taskInput.Text)) return;
        _pet.AddTask(_taskInput.Text, _taskSize, _taskText.Text);
        _taskInput.Clear();
        _taskText.Clear();
        _taskInput.Focus();
    }

    private void UpdateSizeButtons()
    {
        for (int i = 0; i < 3; i++)
            SetOn(_sizeButtons[i], i + 1 == _taskSize);
    }

    // ───────────────────────── Бележки ─────────────────────────

    private UIElement BuildNotesTab()
    {
        var p = new StackPanel();
        _noteInput = NewTextInput();
        _noteInput.AcceptsReturn = true;
        _noteInput.TextWrapping = TextWrapping.Wrap;
        _noteInput.MinHeight = 70 * U;
        _noteInput.MaxHeight = 160 * U;
        _noteInput.VerticalContentAlignment = VerticalAlignment.Top;
        _noteInput.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _noteInput.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                AddNote();
                e.Handled = true;
            }
        };
        p.Children.Add(WithHint(_noteInput, "Запиши нещо…"));

        var save = Button("Запиши", AddNote, primary: true, icon: "pencil");
        save.Margin = new Thickness(0, 0, 8, 0);
        _pinNotes = Chip("📌 Закачи", () => _pet.SetPinned(PinWindow.Notes, !_pet.IsPinned(PinWindow.Notes)));
        _pinNotes.ToolTip = Tip("Бележките в малко прозорче, което стои на екрана");
        _pinNotes.VerticalAlignment = VerticalAlignment.Center;
        p.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0), Children = { save, _pinNotes } });

        p.Children.Add(new ScrollViewer
        {
            Content = _noteList,
            MaxHeight = 360 * U,
            Margin = new Thickness(0, 10, 0, 0),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        });
        return p;
    }

    private void AddNote()
    {
        if (string.IsNullOrWhiteSpace(_noteInput.Text)) return;
        _pet.AddNote(_noteInput.Text);
        _noteInput.Clear();
        _noteInput.Focus();
    }

    private void BuildNoteList()
    {
        _noteList.Children.Clear();
        SetOn(_pinNotes, _pet.IsPinned(PinWindow.Notes));
        if (_pet.Save.Notes.Count == 0)
        {
            _noteList.Children.Add(MutedText("Тефтерът е празен."));
            return;
        }
        foreach (var note in _pet.Save.Notes.ToList())
        {
            var n = note;
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var body = new StackPanel();
            // TextBox само за четене, за да може текстът да се маркира и копира.
            body.Children.Add(new TextBox
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
            body.Children.Add(new TextBlock { Text = n.Created.ToString("d.MM.yyyy, HH:mm"), Foreground = Muted, Margin = new Thickness(0, 2, 0, 0) });
            grid.Children.Add(body);

            var del = Chip("✕", () => _pet.DeleteNote(n));
            del.VerticalAlignment = VerticalAlignment.Top;
            Grid.SetColumn(del, 1);
            grid.Children.Add(del);

            _noteList.Children.Add(Card(grid, false));
        }
    }

    // ───────────────────────── Работа ─────────────────────────

    private readonly List<(string Id, Border Chip)> _pomModes = new();
    private readonly List<(int Count, Border Chip)> _pomSeries = new();
    private static readonly Brush BreakInk = MakeBreakInk();

    private static Brush MakeBreakInk()
    {
        var b = new SolidColorBrush(Color.FromRgb(60, 130, 50));
        b.Freeze();
        return b;
    }   // зелени цифри в почивката

    private UIElement BuildPomodoroTab()
    {
        var p = new StackPanel();
        // Вид мечо-доро: 25/5, 52/17, 90/20 (сменя се, докато не тече).
        var modes = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        foreach (var mode in PetWindow.PomodoroModes)
        {
            string id = mode.Id;
            var chip = Chip(mode.Name, () => _pet.SetPomodoroMode(id));
            chip.Margin = new Thickness(0, 0, 6, 4);
            _pomModes.Add((id, chip));
            modes.Children.Add(chip);
        }
        p.Children.Add(modes);
        // Колко подред: след всяко – зелена почивка, после само почва следващото.
        var series = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        var seriesLabel = new TextBlock { Text = "Подред:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 4) };
        series.Children.Add(seriesLabel);
        for (int n = 1; n <= 4; n++)
        {
            int count = n;
            var chip = Chip(n.ToString(), () => _pet.SetPomodoroSeries(count));
            chip.Margin = new Thickness(0, 0, 6, 4);
            _pomSeries.Add((count, chip));
            series.Children.Add(chip);
        }
        p.Children.Add(series);
        // По коя задача работим (по избор): името ѝ стои над мечока, а накрая той пита свърши ли.
        var taskRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var taskLabel = new TextBlock { Text = "Работя по:", VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 4, 6, 0) };
        DockPanel.SetDock(taskLabel, Dock.Left);
        taskRow.Children.Add(taskLabel);
        taskRow.Children.Add(_focusTasks);
        p.Children.Add(taskRow);
        _pomTime = new TextBlock { FontFamily = MonoFont, FontSize = Huge, HorizontalAlignment = HorizontalAlignment.Center };
        // Колко лешника носи: вдясно до часовника (само преди да е почнало).
        // Лешниците – вдясно, на нивото на часовника (по средата му, не в горния ъгъл).
        _pomNuts.HorizontalAlignment = HorizontalAlignment.Right;
        _pomNuts.VerticalAlignment = VerticalAlignment.Center;
        _pomStatus = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };
        p.Children.Add(new Border
        {
            Background = TileArt,
            BorderBrush = Ink,
            BorderThickness = Flat,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 6, 10, 10),
            Child = new StackPanel { Children = { new Grid { Children = { _pomTime, _pomNuts } }, _pomStatus } },
        });

        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 0) };
        _pomStart = Button("Започни 25 минути", _pet.StartFocus, primary: true, icon: "play");
        _pomStop = Button("Спри", _pet.StopPomodoro, icon: "stop");
        row.Children.Add(_pomStart);
        row.Children.Add(_pomStop);
        p.Children.Add(row);

        _pomToday = new TextBlock { Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Center };
        p.Children.Add(_pomToday);

        _timerRibbon = Ribbon("Таймер", "clock");
        p.Children.Add(_timerRibbon);
        _timerTime = new TextBlock { FontFamily = MonoFont, FontSize = Ui.Title, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 6) };
        p.Children.Add(_timerTime);

        _timerLabel = NewTextInput();
        p.Children.Add(WithHint(_timerLabel, "За какво е? (чай, пране… по избор)"));

        // Видът таймер: Работен / Почивка / Друго (избраният свети).
        _timerKind = _pet.Save.TimerKind is "work" or "rest" or "other" ? _pet.Save.TimerKind : "work";
        var kinds = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        foreach (var (kind, name) in new[] { ("work", "Работен"), ("rest", "Почивка"), ("other", "Друго") })
        {
            string k = kind;
            var chip = Chip(name, () =>
            {
                _timerKind = k;
                UpdateWorkChip();
                UpdateTimerChoice();
            });
            chip.Margin = new Thickness(0, 0, 6, 4);
            _kindChips.Add((k, chip));
            kinds.Children.Add(chip);
        }
        p.Children.Add(kinds);
        UpdateWorkChip();

        // Избираш времето (цъкане или свое число) и чак тогава „Пусни“.
        var presets = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        foreach (int m in new[] { 5, 10, 15, 30, 45, 60 })
        {
            int minutes = m;
            var chip = Chip($"{m} мин", () =>
            {
                _timerChoice = minutes;
                _timerMinutes.Clear();
                UpdateTimerChoice();
            });
            chip.Margin = new Thickness(0, 0, 6, 6);
            _timerChips.Add((minutes, chip));
            presets.Children.Add(chip);
        }
        p.Children.Add(presets);

        var custom = new StackPanel { Orientation = Orientation.Horizontal };
        _timerMinutes = NewTextInput();
        _timerMinutes.Width = 70 * U;
        _timerMinutes.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) StartCustomTimer();
        };
        _timerMinutes.TextChanged += (_, _) =>
        {
            _timerMinutes.ClearValue(Control.BorderBrushProperty);
            if (TypedMinutes() is { } typed) _timerChoice = typed;
            UpdateTimerChoice();
        };
        custom.Children.Add(_timerMinutes);
        custom.Children.Add(new TextBlock { Text = "мин", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 8, 0) });
        var start = Button("Пусни", StartCustomTimer, primary: true, icon: "play");
        start.Margin = new Thickness(0);
        custom.Children.Add(start);
        _timerNuts.VerticalAlignment = VerticalAlignment.Center;
        _timerNuts.Margin = new Thickness(8, 0, 0, 0);
        custom.Children.Add(_timerNuts);
        _timerStop = Button("Спри таймера", _pet.StopTimer, icon: "stop");
        _timerStop.Margin = new Thickness(6, 0, 0, 0);
        custom.Children.Add(_timerStop);
        p.Children.Add(custom);
        _timerReward = new TextBlock { Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap };
        p.Children.Add(_timerReward);
        _timerChoice = 25;
        UpdateTimerChoice();
        return p;
    }

    private readonly WrapPanel _focusTasks = new();
    private Border _timerRibbon = null!;

    /// <summary>„Работя по:“ – без задача или някоя от несвършените (най-много 8).</summary>
    private void BuildFocusTasks()
    {
        _focusTasks.Children.Clear();
        var s = _pet.Save;
        var none = Chip("без задача", () => _pet.SetFocusTask(null));
        none.Margin = new Thickness(0, 0, 6, 4);
        SetOn(none, _pet.FocusTask == null);
        _focusTasks.Children.Add(none);
        foreach (var task in s.Tasks.Where(t => !t.Done).Take(8))
        {
            var t = task;
            var chip = Chip(t.Title.Length > 24 ? t.Title[..23] + "…" : t.Title, () => _pet.SetFocusTask(t));
            chip.Margin = new Thickness(0, 0, 6, 4);
            SetOn(chip, s.FocusTask == t.Id);
            _focusTasks.Children.Add(chip);
        }
        if (s.Tasks.All(t => t.Done)) _focusTasks.Children.Add(MutedText("(няма задачи – добави в „Задачи“)"));
    }

    private void UpdateWorkChip()
    {
        foreach (var (kind, chip) in _kindChips) SetOn(chip, kind == _timerKind);
    }

    /// <summary>Написаното число минути (или null, ако не е число).</summary>
    private double? TypedMinutes()
    {
        string text = _timerMinutes.Text.Trim().Replace(',', '.');
        return double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double m) && m > 0 && m <= 24 * 60
            ? m : null;
    }

    /// <summary>Избраното време свети, а под него пише колко лешника носи.</summary>
    private void UpdateTimerChoice()
    {
        foreach (var (m, chip) in _timerChips) SetOn(chip, Math.Abs(m - _timerChoice) < 0.01 && _timerMinutes.Text.Trim().Length == 0);
        int nuts = _pet.NutsFor(_timerChoice);
        _timerReward.Text = $"Избрано: {_timerChoice:0.#} мин";
        SetNuts(_timerNuts, _timerKind == "work" ? nuts : 0);
    }

    /// <summary>„Пусни“: пуска таймера с избраното време.</summary>
    private void StartCustomTimer()
    {
        if (_timerMinutes.Text.Trim().Length > 0 && TypedMinutes() == null)
        {
            // Написано е нещо, което не е число: червена рамка за миг.
            _timerMinutes.BorderBrush = Brushes.IndianRed;
            return;
        }
        if (_timerChoice <= 0) return;
        _pet.StartTimer(_timerChoice, _timerLabel.Text, _timerKind);
        _timerMinutes.Clear();
    }

    // ───────────────────────── Още ─────────────────────────

    private DevWindow? _dev;

    private UIElement BuildMoreTab()
    {
        var p = new StackPanel();
        _updateButton = Button("", () =>
        {
            if (_pet.Updater.IsAvailable) _ = _pet.InstallUpdate();
            else _ = _pet.CheckForUpdates(manual: true);
        });
        var reset = Button("Панелът да се отваря до мечока", () =>
        {
            _pet.Save.MenuLeft = double.NaN;
            _pet.Save.MenuTop = double.NaN;
            _pet.Persist();
            Hide();
            _pet.OpenMenu(MoreTab);
        }, icon: "pin");
        var hide = Button("Скрий мечока (иконката е до часовника)", () =>
        {
            Hide();
            _pet.Hide();
        }, icon: "hide");
        var dev = Button("Developer: тествай всичко", () =>
        {
            Hide();
            // С парола („mecho“), за да не се пипа случайно.
            if (!DevPasswordWindow.Ask(_pet)) return;
            _dev ??= new DevWindow(_pet);
            _dev.Closed += (_, _) => _dev = null;
            _dev.Show();
            _dev.Activate();
        }, icon: "gear");
        var tutorial = Button("Как се играе? (обиколка с Мечо)", () =>
        {
            Hide();
            _pet.StartTutorial();
        }, icon: "question");
        foreach (var b in new[] { tutorial, _updateButton, reset, hide, dev })
        {
            b.HorizontalAlignment = HorizontalAlignment.Stretch;
            b.HorizontalContentAlignment = HorizontalAlignment.Left;
            b.Margin = new Thickness(0, 0, 0, 8);
            p.Children.Add(b);
        }
        // Звук: „динг“, когато свърши мечо-доро, почивка или таймер.
        p.Children.Add(new TextBlock { Text = "Звук", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 6, 0, 4) });
        var soundRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        var soundOn = Chip("", () => { });
        var levels = new List<(double Level, Border Chip)>();
        void ShowSound()
        {
            var s = _pet.Save;
            soundOn.Child = IconText("sound", s.SoundOn ? "Звук: вкл." : "Звук: изкл.");
            SetOn(soundOn, s.SoundOn);
            foreach (var (level, chip) in levels) SetOn(chip, s.SoundOn && Math.Abs(s.SoundVolume - level) < 0.05);
        }
        soundOn = Chip("", () =>
        {
            _pet.Save.SoundOn = !_pet.Save.SoundOn;
            _pet.Persist();
            ShowSound();
            Sounds.Play(_pet.Save);
        });
        soundOn.Margin = new Thickness(0, 0, 8, 4);
        soundRow.Children.Add(soundOn);
        foreach (var (name, level) in new[] { ("тихо", 0.25), ("средно", 0.6), ("силно", 1.0) })
        {
            double v = level;
            var chip = Chip(name, () =>
            {
                _pet.Save.SoundOn = true;
                _pet.Save.SoundVolume = v;
                _pet.Persist();
                ShowSound();
                Sounds.Play(_pet.Save);
            });
            chip.Margin = new Thickness(0, 0, 6, 4);
            levels.Add((v, chip));
            soundRow.Children.Add(chip);
        }
        p.Children.Add(soundRow);
        ShowSound();

        // Размерът на мечока на екрана (колко пиксела на екрана е един пиксел от рисунката).
        var sizeLabel = new TextBlock { FontWeight = FontWeights.Bold, Margin = new Thickness(0, 6, 0, 4) };
        var slider = new Slider
        {
            Minimum = 1,
            Maximum = 6,
            TickFrequency = 1,
            IsSnapToTickEnabled = true,
            TickPlacement = System.Windows.Controls.Primitives.TickPlacement.BottomRight,
            Value = _pet.BearScale,
            Margin = new Thickness(0, 0, 0, 10),
            Cursor = Cursors.Hand,
        };
        void ShowSize() => sizeLabel.Text = $"Размер на Мечо: {(int)slider.Value}× " + ((int)slider.Value switch
        {
            1 => "(мъничък)",
            2 => "(малък)",
            3 => "(нормален)",
            4 => "(голям)",
            5 => "(много голям)",
            _ => "(огромен)",
        });
        ShowSize();
        slider.ValueChanged += (_, _) =>
        {
            ShowSize();
            _pet.SetBearScale((int)slider.Value);
        };
        p.Children.Add(sizeLabel);
        p.Children.Add(slider);

        p.Children.Add(MutedText("Мечо. Шрифт Pixeloid (GGBotNet, SIL OFL)."));
        p.Children.Add(MutedText($"Версия {Updater.CurrentName}"));
        return p;
    }

    // ───────────────────────── Обновяване ─────────────────────────

    private bool _rebuildQueued;

    /// <summary>
    /// Обновява менюто. Строенето на таба наново (rebuild) не става веднага, а веднъж,
    /// след като свършат всички промени в този момент: три цъкания подред или няколко
    /// известия наведнъж = едно строене (иначе менюто „засичаше“).
    /// </summary>
    /// <summary>Нещо в състоянието се смени: отворено – пречертава (събрано наведнъж); иначе – при отваряне.</summary>
    private void StateChangedRefresh()
    {
        if (IsOpen) Refresh(rebuild: true);
        else Array.Fill(_dirty, true);
    }

    private void Refresh(bool rebuild)
    {
        if (!rebuild || !IsOpen || SelfTest.Running)
        {
            RefreshNow(rebuild);
            return;
        }
        RefreshNow(false);
        if (_rebuildQueued) return;
        _rebuildQueued = true;
        Dispatcher.BeginInvoke(() =>
        {
            _rebuildQueued = false;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            RefreshNow(true);
            _pet.PerfNote("менюто: строене на таба", watch.Elapsed.TotalMilliseconds);
        }, DispatcherPriority.Background);
    }

    private void RefreshNow(bool rebuild)
    {
        var s = _pet.Save;
        _nuts.Text = s.Hazelnuts.ToString();
        // Скрити табове: „Задачи“ (те са в „Работа“) и гардеробът, докато не е купен.
        _tabButtons[TasksTab].Visibility = Visibility.Collapsed;
        _tabButtons[WardrobeTab].Visibility = DailyOffer.HasWardrobe(s) ? Visibility.Visible : Visibility.Collapsed;

        // Мечо
        _portrait.Source = _pet.Portrait;
        _portraitBg.Source = _modelBg.Source = _pet.BackdropImage(_pet.CurrentBackdrop);
        BuildGoal();
        _mood.Text = _pet.IsAsleep ? "Ззз… спи сладко" :
            s.Fullness < 25 ? "Гладен е!" :
            s.Energy < PetWindow.TiredBelow ? "Много е уморен" :
            s.Fullness >= 70 && s.Energy >= 60 ? "Щастлив е!" : "Добре е";
        SetBar(_fullBar, _fullText, s.Fullness, s.Fullness switch
        {
            >= 80 => "сит",
            >= 50 => "добре",
            >= 25 => "огладнял",
            _ => "гладен!",
        });
        SetBar(_energyBar, _energyText, s.Energy, _pet.IsAsleep ? "спи…" : _pet.IsSick ? "болничък" : s.Energy switch
        {
            >= 70 => "бодър",
            >= 50 => "добре",
            >= PetWindow.TiredBelow => "леко уморен",
            >= 15 => "уморен",
            _ => "капнал!",
        });
        var weather = _pet.WeatherText;
        _weatherRow.Visibility = weather == null ? Visibility.Collapsed : Visibility.Visible;
        if (_pet.WeatherBadge is { } badge)
        {
            if (_weatherIconName != badge.Icon)
            {
                _weatherIconName = badge.Icon;
                _weatherIcon.Child = Icon(badge.Icon, 2);
            }
            _weather.Text = badge.Temp;
            // Днешните събития (пижамен ден, Коледа…) – в подсказката до времето.
            var events = _pet.ActiveEventNames();
            if (events.Length > 0) weather = (weather ?? "") + "\nДнес: " + string.Join(", ", events);
            if (weather != null && !Equals((_weatherRow.ToolTip as ToolTip)?.Tag, weather))
            {
                var tip = Tip(weather.Replace("🌡 ", "").Trim());
                tip.Tag = weather;
                _weatherRow.ToolTip = tip;
            }
        }
        bool canDevices = Devices.Any(i => Kitchen.Owns(s, i));
        bool canFurniture = PetWindow.FurnitureKinds.Any(i => Kitchen.Owns(s, i));
        _placeButton.Visibility = canDevices ? Visibility.Visible : Visibility.Collapsed;
        _furnitureButton.Visibility = canFurniture ? Visibility.Visible : Visibility.Collapsed;
        if (_placeGroup == PlaceGroup.Devices ? !canDevices : !canFurniture) _placeOpen = false;
        SetOn(_placeButton, _placeOpen && _placeGroup == PlaceGroup.Devices);
        SetOn(_furnitureButton, _placeOpen && _placeGroup == PlaceGroup.Furniture);
        BuildPlaceOptions();
        _placeOptions.Visibility = _placeOpen ? Visibility.Visible : Visibility.Collapsed;
        if (_pet.IsAsleep) SetButton(_sleepButton, "Събуди", "sun");
        else SetButton(_sleepButton, "Приспи", "moon");
        _sleepButton.IsEnabled = _pet.CanSleep;
        if (_pet.IsAsleep) _sleepOpen = false;
        if (_pet.IsQuiet) _quietOpen = false;
        BuildSleepOptions();
        SetOn(_sleepButton, _sleepOpen);
        SetOn(_quietButton, _quietOpen);
        _sleepOptions.Visibility = _sleepOpen ? Visibility.Visible : Visibility.Collapsed;
        _quietOptions.Visibility = _quietOpen ? Visibility.Visible : Visibility.Collapsed;
        SetOn(_walkButton, _walkOpen);
        _walkOptions.Visibility = _walkOpen ? Visibility.Visible : Visibility.Collapsed;
        _drawButton.Visibility = Kitchen.Owns(s, Kitchen.Easel) ? Visibility.Visible : Visibility.Collapsed;
        if (_pet.IsQuiet) SetButton(_quietButton, "Говори", "bell");
        else SetButton(_quietButton, "Тихо", "quiet");

        // Храна и магазин
        _foodInfo.Visibility = Visibility.Collapsed;
        _shopInfo.Visibility = Visibility.Collapsed;
        if (rebuild)
        {
            // Строи се само отвореният таб; другите – когато се отворят.
            for (int i = 0; i < _dirty.Length; i++) _dirty[i] = true;
            BuildTab(_tab);
        }

        // Време
        var left = _pet.PomodoroLeft;
        switch (s.PomodoroPhase)
        {
            case PomodoroPhase.Focus:
                _pomTime.Text = $"{(int)left.TotalMinutes:00}:{left.Seconds:00}";
                SetNuts(_pomNuts, 0);
                _pomStatus.Text = s.PomodoroSeries > 1 ? $"Работим заедно ({_pet.PomodoroNumber} от {s.PomodoroSeries})." : "Работим заедно.";
                break;
            case PomodoroPhase.Break:
                _pomTime.Text = $"{(int)left.TotalMinutes:00}:{left.Seconds:00}";
                SetNuts(_pomNuts, 0);
                _pomStatus.Text = s.PomodoroSeriesLeft > 0 ? $"Почивка. После – {_pet.PomodoroNumber + 1} от {s.PomodoroSeries}." : "Почивка: стани, раздвижи се, пийни вода.";
                break;
            default:
                _pomTime.Text = $"{(int)_pet.FocusLength.TotalMinutes:00}:00";
                _pomStatus.Text = _pet.PomodoroMode.Hint;
                SetNuts(_pomNuts, _pet.PomodoroNuts);
                break;
        }
        _pomStart.Visibility = s.PomodoroPhase == PomodoroPhase.Focus ? Visibility.Collapsed : Visibility.Visible;
        SetButton(_pomStart, s.PomodoroPhase == PomodoroPhase.Break ? "Нов мечо-доро" : $"Започни {_pet.PomodoroMode.Focus} минути", "play");
        bool running = s.PomodoroPhase != PomodoroPhase.Off;
        foreach (var (id, chip) in _pomModes)
        {
            SetOn(chip, id == _pet.PomodoroMode.Id);
            // Докато тече, видът не се сменя: копчетата са бледи и не се натискат.
            chip.IsHitTestVisible = !running;
            chip.Opacity = running && id != _pet.PomodoroMode.Id ? 0.5 : 1;
            chip.BorderThickness = id == _pet.PomodoroMode.Id ? Sunken : running ? Flat : Raised;
        }
        foreach (var (count, chip) in _pomSeries)
        {
            bool on = count == s.PomodoroSeries;
            SetOn(chip, on);
            chip.IsHitTestVisible = !running;
            chip.Opacity = running && !on ? 0.5 : 1;
            chip.BorderThickness = on ? Sunken : running ? Flat : Raised;
        }
        _pomTime.Foreground = s.PomodoroPhase == PomodoroPhase.Break ? BreakInk : Ink;
        _pomStop.Visibility = s.PomodoroPhase == PomodoroPhase.Off ? Visibility.Collapsed : Visibility.Visible;
        _pomToday.Text = $"Днес: {s.PomodorosToday}   ·   Общо: {s.PomodorosTotal}";

        if (_pet.TimerRunning)
        {
            var t = _pet.TimerLeft;
            string clock = t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{(int)t.TotalMinutes:00}:{t.Seconds:00}";
            _timerTime.Text = s.TimerLabel.Length > 0 ? $"{clock}  {s.TimerLabel}" : clock;
        }
        else _timerTime.Text = "--:--";
        _timerTime.Foreground = _pet.RestTimer ? BreakInk : Ink;
        _timerStop.Visibility = _pet.TimerRunning ? Visibility.Visible : Visibility.Collapsed;

        // Още
        if (_pet.Updater.IsAvailable) SetButton(_updateButton, "Обнови", "update");
        else SetButton(_updateButton, "Провери за обновление", "update");
        _updateButton.IsEnabled = !_pet.Updater.IsBusy;
    }

    // ───────────────────────── Малки помощници ─────────────────────────

    internal static TextBlock MutedText(string text) => new()
    {
        Text = text,
        Foreground = Muted,
        TextWrapping = TextWrapping.Wrap,
    };

    internal static TextBox NewTextInput() => new()
    {
        FontFamily = GameFont,
        FontSize = Body,
        Padding = new Thickness(8, 6, 8, 6),
        BorderBrush = Ink,
        BorderThickness = Flat,
        Background = Cream,
        Foreground = Ink,
        VerticalContentAlignment = VerticalAlignment.Center,
    };

    /// <summary>Поле с бледа подсказка, докато е празно.</summary>
    internal static UIElement WithHint(TextBox box, string hint)
    {
        var text = new TextBlock
        {
            Text = hint,
            Foreground = Muted,
            IsHitTestVisible = false,
            Margin = new Thickness(11, 0, 0, 0),
            VerticalAlignment = box.AcceptsReturn ? VerticalAlignment.Top : VerticalAlignment.Center,
        };
        if (box.AcceptsReturn) text.Margin = new Thickness(11, 9, 0, 0);
        box.TextChanged += (_, _) => text.Visibility = box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        var grid = new Grid();
        grid.Children.Add(box);
        grid.Children.Add(text);
        return grid;
    }

    /// <summary>Картичка за задача или бележка.</summary>
    internal static Border Card(UIElement child, bool faded) => new()
    {
        Background = faded ? PaperDark : Tile,
        BorderBrush = WoodDark,
        BorderThickness = Flat,
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(10, 7, 7, 7),
        Margin = new Thickness(0, 0, 0, 6),
        Child = child,
    };
}
