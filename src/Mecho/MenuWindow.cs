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
    private Button _heaterButton = null!;
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
    private TextBox _taskInput = null!;
    private int _taskSize = 1;
    private readonly Border[] _sizeButtons = new Border[3];
    private readonly StackPanel _noteList = new();
    private TextBox _noteInput = null!;
    private TextBlock _pomTime = null!, _pomStatus = null!, _pomToday = null!;
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
        _tabs[TasksTab] = BuildTasksTab();
        _tabs[NotesTab] = BuildNotesTab();
        _tabs[PomodoroTab] = BuildPomodoroTab();
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

        _pet.CareChanged += (_, _) =>
        {
            if (IsOpen) Refresh(rebuild: true);
            else Array.Fill(_dirty, true);   // строи се наново, като се отвори
        };
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
        }
    }

    private void SelectTab(int index)
    {
        _tab = index;
        for (int i = 0; i < _tabs.Length; i++)
        {
            bool on = i == index;
            _tabs[i].Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            // Избраният таб е по-висок и слят с хартията отдолу, другите са по-тъмни.
            _tabButtons[i].Background = on ? Paper : WoodLight;
            _tabButtons[i].BorderThickness = on ? new Thickness(2, 2, 2, 0) : new Thickness(2, 2, 2, 2);
            _tabButtons[i].Margin = new Thickness(i == 0 ? 0 : 3, on ? 0 : 5 * U, 0, on ? -2 : 0);
        }
        // Гардеробът стои до мечока, докато е отворен табът.
        if (index == WardrobeTab && IsVisible) _pet.OpenWardrobe();
        else if (index != WardrobeTab) _pet.CloseWardrobe();
        Refresh(rebuild: false);
        BuildTab(index);
        if (index == TasksTab) Dispatcher.BeginInvoke(() => _taskInput.Focus(), DispatcherPriority.Input);
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
        var grid = new Grid { Margin = new Thickness(2, 0, 2, 0) };
        (string Icon, string Name)[] tabs =
        {
            ("bear", "Мечо"), ("food", "Храна"), ("shop", "Магазин"), ("wardrobe", "Гардероб"), ("tasks", "Задачи"), ("notes", "Бележки"), ("clock", "Време"), ("gear", "Още"),
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
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetColumn(tab, i);
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
        _weather = new TextBlock { FontSize = Ui.Body, Margin = new Thickness(0, -6 * U, 0, 8 * U), TextWrapping = TextWrapping.Wrap, Opacity = 0.8 };
        stats.Children.Add(_weather);
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
        }, icon: "pencil"));
        actions.Children.Add(_workButton);
        // Спри: спира каквото прави (кътче, батут, разходка) – таймерите си текат.
        _couchButton = Wide(Button("Спри", () =>
        {
            _pet.StopDoing();
            Refresh(true);
        }, icon: "stop"));
        actions.Children.Add(_couchButton);
        _heaterButton = Wide(Button("", () =>
        {
            _pet.SetHeater(!_pet.Save.HeaterOn);
            Refresh(false);
        }, icon: "sun"));
        actions.Children.Add(_heaterButton);
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

    private readonly WrapPanel _sleepOptions = new(), _quietOptions = new();
    private bool _sleepOpen, _quietOpen;
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

        value = new TextBlock { Foreground = Muted, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(value, 2);
        grid.Children.Add(value);
        return grid;
    }

    private static void SetBar(Border[] segments, TextBlock text, double value, string word)
    {
        int filled = (int)Math.Ceiling(Math.Clamp(value, 0, 100) / 100 * segments.Length - 0.01);
        var colour = value >= 50 ? Good : value >= 25 ? Okay : Bad;
        for (int i = 0; i < segments.Length; i++) segments[i].Background = i < filled ? colour : Empty;
        text.Text = word;
    }

    // ───────────────────────── Храна ─────────────────────────

    private UIElement BuildFoodTab()
    {
        var p = new StackPanel();
        _foodInfo = new TextBlock { Foreground = Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 0, 0, 10) };
        p.Children.Add(_foodInfo);
        p.Children.Add(_foodTiles);
        p.Children.Add(_recipeProgress);
        return new ScrollViewer { Content = p, MaxHeight = 560 * U, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    /// <summary>Плочки с храните: картинка, какво дава и бутон „Сготви“. Посочиш ли заключена, казва защо.</summary>
    private void BuildFoodTiles()
    {
        var s = _pet.Save;
        _foodTiles.Children.Clear();
        // Само научените рецепти (боровинките винаги); новите идват от страници.
        foreach (var food in Kitchen.Foods.Where(f => Kitchen.HasRecipe(f, s)))
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

            if (f.Tool != null)
            {
                double secs = PetWindow.CookSeconds(f.Tool);
                // Часовниче и колко време се готви.
                var time = IconText("clock", secs % 60 == 0 ? $"{secs / 60:0} мин" : $"{secs:0} с");
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
            _foodTiles.Children.Add(MakeTile(_pet.PropImage("food_" + f.Id), f.Name, effects, action, locked, why));
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
        Refresh(true);
        UpdateLayout();
        _showHistory = false;
        Refresh(true);
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
        string key = $"{goal?.Id}|{string.Join(",", choices.Select(c => c.Id))}|{_pet.GoalRerollUsed}|{filled}|{s.Hazelnuts}";
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
            bool ready = s.Hazelnuts >= goal.Price;
            var buy = Button(ready ? $"Купи  {goal.Price}" : $"{Math.Min(s.Hazelnuts, goal.Price)}/{goal.Price}", () =>
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

    /// <summary>Едно от трите неща за избор: картинка, цена, „Избери“ (и „Друго“).</summary>
    private Border GoalChoice(Upgrade u, bool canReroll)
    {
        var details = new StackPanel();
        var price = RichText($"{u.Price} 🌰");
        price.HorizontalAlignment = HorizontalAlignment.Center;
        details.Children.Add(price);
        details.Children.Add(Description(u.Description));
        if (canReroll)
        {
            var reroll = Button("Друго", () =>
            {
                _pet.RerollGoal(u.Id);
                Refresh(true);
            }, icon: "update");
            reroll.HorizontalAlignment = HorizontalAlignment.Center;
            reroll.Margin = new Thickness(0, 4, 0, 0);
            details.Children.Add(reroll);
        }
        var pick = Button("Избери", () =>
        {
            _pet.ChooseGoal(u.Id);
            Refresh(true);
        }, primary: true);
        return MakeTile(_pet.PropImage("shop_" + u.Id), u.Name, details, pick, dim: false, null);
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
        var left = DailyOffer.Left;
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
        box.Children.Add(new TextBlock
        {
            Text = $"Нови неща след {(int)left.TotalHours} ч {left.Minutes} мин.",
            Foreground = Ink,
            Margin = new Thickness(2, 0, 0, 6 * U),
        });

        var offer = DailyOffer.Today(s);
        var grid = new UniformGrid { Columns = 3 };
        foreach (var key in offer)
        {
            switch (DailyOffer.Resolve(key))
            {
                case Garment g: grid.Children.Add(GarmentShopTile(g)); break;
                case Backdrop b: grid.Children.Add(BackdropTile(b)); break;
                case Skin k: grid.Children.Add(SkinTile(k, shop: true)); break;
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

    private Border BackdropTile(Backdrop b)
    {
        var s = _pet.Save;
        bool owned = Backdrops.Owns(s, b), used = s.Background == b.Id;
        string? why = null;
        Button action;
        if (used)
        {
            action = Button("Сложен", () => { });
            action.IsEnabled = false;
        }
        else if (owned) action = Button("Сложи", () => _pet.UseBackdrop(b), primary: true);
        else action = BuyButton(b.Price, () => _pet.BuyBackdrop(b), out why);
        return MakeTile(_pet.BackdropImage(b), b.Name, Description("Тапет за портрета на Мечо."), action, dim: false, why, owned, 2);
    }

    /// <summary>Скин за диван/легло: в магазина се купува, в гардероба се слага.</summary>
    private Border SkinTile(Skin skin, bool shop)
    {
        var s = _pet.Save;
        bool owned = Furniture.Owns(s, skin), used = Furniture.Current(s, skin.Item) == skin;
        bool hasItem = Kitchen.Owns(s, skin.Item);
        string? why = null;
        Button action;
        if (used)
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
                why = skin.Item == Kitchen.Bed ? "Първо купи леглото." : "Първо купи дивана.";
            }
        }
        string note = shop ? (skin.Item == Kitchen.Bed ? "Скин за леглото." : "Скин за дивана.") : "";
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
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 3) };
            // Облечената дреха като малка картинка (или иконката на слота, ако няма).
            FrameworkElement icon;
            if (g != null)
            {
                var art = _pet.GarmentPreview(g);
                var image = Pixel(art, ArtScale(Math.Clamp(24 / Math.Max(art.PixelWidth, art.PixelHeight), 1, 3)));
                image.HorizontalAlignment = HorizontalAlignment.Center;
                image.VerticalAlignment = VerticalAlignment.Center;
                icon = new Grid { Width = 52 * U, Children = { image } };
            }
            else icon = new Grid { Width = 52 * U, Children = { Icon(slot.Icon) } };
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

        _wardrobeShelves.Children.Clear();
        _wardrobeLock.Visibility = DailyOffer.HasWardrobe(s) ? Visibility.Collapsed : Visibility.Visible;
        _wardrobeBody.Visibility = DailyOffer.HasWardrobe(s) ? Visibility.Visible : Visibility.Collapsed;
        if (!DailyOffer.HasWardrobe(s)) return;

        // Раздели: всеки вид дреха/аксесоар, тапетите и мебелите (ако има диван или легло).
        var sections = Clothes.Shelves.Select(slot => (Id: slot.Id, Name: slot.Name, Icon: slot.Icon)).ToList();
        sections.Add(("wallpapers", "Тапети", "picture"));
        if (Kitchen.Owns(s, Kitchen.Couch) || Kitchen.Owns(s, Kitchen.Bed)) sections.Add(("furniture", "Мебели", "couch"));
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
        else if (_wardrobeSection == "furniture")
        {
            foreach (var item in new[] { Kitchen.Couch, Kitchen.Bed })
                if (Kitchen.Owns(s, item))
                    foreach (var skin in Furniture.SkinsFor(item).Where(k => Furniture.Owns(s, k))) grid.Children.Add(SkinTile(skin, shop: false));
            empty = "Нови скинове има в офертата на деня.";
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

    private UIElement BuildTasksTab()
    {
        var p = new StackPanel();
        _taskInput = NewTextInput();
        _taskInput.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) AddTask();
        };
        p.Children.Add(WithHint(_taskInput, "Нова задача…"));

        var sizes = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        string[] labels = { "малка +1", "средна +3", "голяма +5" };
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
        _taskSummary = new TextBlock { Foreground = Muted, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        bottom.Children.Add(_taskSummary);
        p.Children.Add(bottom);
        return p;
    }

    private Border _historyButton = null!;
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
            var total = IconText("nut", $"+{nuts}", bold: true);
            DockPanel.SetDock(total, Dock.Right);
            header.Children.Add(total);
            header.Children.Add(new TextBlock { Text = $"{name}  ·  {day.Count()}", FontWeight = FontWeights.Bold });
            _taskList.Children.Add(header);

            foreach (var t in day)
            {
                var row = new DockPanel();
                var reward = IconText("nut", $"+{t.Reward}");
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
            row.Children.Add(check);

            var reward = IconText("nut", $"+{t.Reward}", bold: true);
            reward.Margin = new Thickness(8, 0, 8, 0);
            reward.Opacity = t.Done ? 0.5 : 1;
            Grid.SetColumn(reward, 1);
            row.Children.Add(reward);

            var del = Chip("✕", () => _pet.DeleteTask(t));
            del.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(del, 2);
            row.Children.Add(del);

            _taskList.Children.Add(Card(row, t.Done));
        }

        int today = _pet.Save.Tasks.Count(t => t.Done);
        _taskSummary.Text = $"Свършени днес: {today}   ·   Общо: {_pet.Save.TasksDoneTotal}";
    }

    private void AddTask()
    {
        if (string.IsNullOrWhiteSpace(_taskInput.Text)) return;
        _pet.AddTask(_taskInput.Text, _taskSize);
        _taskInput.Clear();
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
        p.Children.Add(WithHint(_noteInput, "Запиши нещо… (Ctrl+Enter)"));

        var save = Button("Запиши", AddNote, primary: true, icon: "pencil");
        save.HorizontalAlignment = HorizontalAlignment.Left;
        save.Margin = new Thickness(0, 8, 0, 0);
        p.Children.Add(save);

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

    // ───────────────────────── Време ─────────────────────────

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
        var pomRibbon = Ribbon("Мечо-доро", "honey");
        pomRibbon.Margin = new Thickness(0, 0, 0, 8);
        p.Children.Add(pomRibbon);
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
        _pomTime = new TextBlock { FontFamily = MonoFont, FontSize = Huge, HorizontalAlignment = HorizontalAlignment.Center };
        _pomStatus = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };
        p.Children.Add(new Border
        {
            Background = TileArt,
            BorderBrush = Ink,
            BorderThickness = Flat,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 6, 10, 10),
            Child = new StackPanel { Children = { _pomTime, _pomStatus } },
        });

        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 0) };
        _pomStart = Button("Започни 25 минути", _pet.StartFocus, primary: true, icon: "play");
        _pomStop = Button("Спри", _pet.StopPomodoro, icon: "stop");
        row.Children.Add(_pomStart);
        row.Children.Add(_pomStop);
        p.Children.Add(row);

        _pomToday = new TextBlock { Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Center };
        p.Children.Add(_pomToday);

        p.Children.Add(Ribbon("Таймер", "clock"));
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
        _timerStop = Button("Спри таймера", _pet.StopTimer, icon: "stop");
        _timerStop.Margin = new Thickness(6, 0, 0, 0);
        custom.Children.Add(_timerStop);
        p.Children.Add(custom);
        _timerReward = new TextBlock { Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap };
        p.Children.Add(_timerReward);
        _timerChoice = 25;
        UpdateTimerChoice();

        p.Children.Add(new TextBlock
        {
            Text = "Всеки 10 минути мечо-доро или таймер носят 1 лешник.",
            Foreground = Muted,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 0),
        });
        return p;
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
        SetRichText(_timerReward, _timerKind == "work" ? $"Избрано: {_timerChoice:0.#} мин  ·  ще получиш {nuts} 🌰" : $"Избрано: {_timerChoice:0.#} мин");
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
            _dev ??= new DevWindow(_pet);
            _dev.Closed += (_, _) => _dev = null;
            _dev.Show();
            _dev.Activate();
        }, icon: "gear");
        foreach (var b in new[] { _updateButton, reset, hide, dev })
        {
            b.HorizontalAlignment = HorizontalAlignment.Stretch;
            b.HorizontalContentAlignment = HorizontalAlignment.Left;
            b.Margin = new Thickness(0, 0, 0, 8);
            p.Children.Add(b);
        }
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

        p.Children.Add(MutedText($"Мечо, версия {Updater.CurrentName}. Шрифт Pixeloid (GGBotNet, SIL OFL)."));
        return p;
    }

    // ───────────────────────── Обновяване ─────────────────────────

    private void Refresh(bool rebuild)
    {
        var s = _pet.Save;
        _nuts.Text = s.Hazelnuts.ToString();

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
        SetBar(_energyBar, _energyText, s.Energy, _pet.IsAsleep ? "спи…" : s.Energy switch
        {
            >= 70 => "бодър",
            >= PetWindow.TiredBelow => "добре",
            >= 15 => "уморен",
            _ => "капнал!",
        });
        var weather = _pet.WeatherText;
        _weather.Text = weather ?? "";
        _weather.Visibility = weather == null ? Visibility.Collapsed : Visibility.Visible;
        _heaterButton.Visibility = Kitchen.Owns(s, Kitchen.Heater) ? Visibility.Visible : Visibility.Collapsed;
        SetButton(_heaterButton, s.HeaterOn ? "Изгаси печицата" : "Печицата", "sun");
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
                _pomStatus.Text = s.PomodoroSeries > 1 ? $"Работим заедно ({_pet.PomodoroNumber} от {s.PomodoroSeries})." : "Работим заедно.";
                break;
            case PomodoroPhase.Break:
                _pomTime.Text = $"{(int)left.TotalMinutes:00}:{left.Seconds:00}";
                _pomStatus.Text = s.PomodoroSeriesLeft > 0 ? $"Почивка. После – {_pet.PomodoroNumber + 1} от {s.PomodoroSeries}." : "Почивка: стани, раздвижи се, пийни вода.";
                break;
            default:
                _pomTime.Text = $"{(int)_pet.FocusLength.TotalMinutes:00}:00";
                SetRichText(_pomStatus, $"{_pet.PomodoroMode.Hint} Носи {_pet.PomodoroNuts} 🌰.");
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
        if (_pet.Updater.IsAvailable) SetButton(_updateButton, $"Обнови до версия {_pet.Updater.LatestName}", "update");
        else SetButton(_updateButton, $"Провери за обновление (сега: {Updater.CurrentName})", "update");
        _updateButton.IsEnabled = !_pet.Updater.IsBusy;
    }

    // ───────────────────────── Малки помощници ─────────────────────────

    private static TextBlock MutedText(string text) => new()
    {
        Text = text,
        Foreground = Muted,
        TextWrapping = TextWrapping.Wrap,
    };

    private static TextBox NewTextInput() => new()
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
    private static UIElement WithHint(TextBox box, string hint)
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
    private static Border Card(UIElement child, bool faded) => new()
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
