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
    private Image _portrait = null!;
    private Border[] _fullBar = null!, _energyBar = null!;
    private TextBlock _fullText = null!, _energyText = null!, _mood = null!;
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
    private Button _timerStop = null!;
    private Border _workChip = null!;
    private bool _timerWork = true;
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

        _pet.CareChanged += (_, _) => { if (IsVisible) Refresh(rebuild: true); };
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
        SelectTab(tab);
        Show();
        if (tab == WardrobeTab) _pet.OpenWardrobe();
        UpdateLayout();

        var s = _pet.Save;
        if (!double.IsNaN(s.MenuLeft) && !double.IsNaN(s.MenuTop))
        {
            var home = _pet.AreaAt(s.MenuLeft + 20, s.MenuTop + 20);
            if (home.Contains(new Point(s.MenuLeft + 20, s.MenuTop + 20)))
            {
                Left = Math.Clamp(s.MenuLeft, home.Left, Math.Max(home.Left, home.Right - ActualWidth));
                Top = Math.Clamp(s.MenuTop, home.Top, Math.Max(home.Top, home.Bottom - ActualHeight));
                Activate();
                return;
            }
        }

        double left = bear.Right + 8;
        if (left + ActualWidth > area.Right - 4) left = bear.Left - ActualWidth - 8;
        Left = Math.Clamp(left, area.Left + 4, Math.Max(area.Left + 4, area.Right - ActualWidth - 4));
        Top = Math.Max(area.Top + 4, area.Bottom - ActualHeight - 4);
        Activate();
    }

    private void SelectTab(int index)
    {
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
        Refresh(rebuild: true);
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
            ToolTip = Tip("Хвани ме оттук, за да ме преместиш"),
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
        nuts.ToolTip = Tip("Лешници: печелят се със задачи и с работа (всеки 10 минути мечо-доро или таймер = 1). Харчат се за храна и в магазина.");
        Grid.SetColumn(nuts, 1);
        grid.Children.Add(nuts);

        var close = Chip("✕", Hide);
        // Червеното ✕ е пикселна иконка; фонът е светъл, за да се вижда.
        close.VerticalAlignment = VerticalAlignment.Center;
        close.ToolTip = Tip("Затвори (Esc)");
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

        // Портрет: мечокът на тревата под небето.
        _portrait = Pixel(_pet.Portrait, ArtScale(3));
        _portrait.VerticalAlignment = VerticalAlignment.Bottom;
        _portrait.HorizontalAlignment = HorizontalAlignment.Center;
        _portrait.Margin = new Thickness(0, 0, 0, 6 * U);
        var scene = new Grid { Width = 150 * U, Height = 150 * U, ClipToBounds = true };
        scene.RowDefinitions.Add(new RowDefinition());
        scene.RowDefinitions.Add(new RowDefinition { Height = new GridLength(24 * U) });
        scene.Children.Add(new Border { Background = Sky });
        var grass = new Border { Background = Grass, BorderBrush = Ink, BorderThickness = new Thickness(0, 2, 0, 0) };
        Grid.SetRow(grass, 1);
        scene.Children.Add(grass);
        Grid.SetRowSpan(_portrait, 2);
        scene.Children.Add(_portrait);
        top.Children.Add(new Border
        {
            BorderBrush = Ink,
            BorderThickness = new Thickness(3, 3, 3, 5),
            CornerRadius = new CornerRadius(6),
            Child = scene,
            VerticalAlignment = VerticalAlignment.Top,
        });

        var stats = new StackPanel { Margin = new Thickness(16 * U, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        _mood = new TextBlock { FontSize = Ui.Title, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 10 * U), TextWrapping = TextWrapping.Wrap };
        stats.Children.Add(_mood);
        stats.Children.Add(Bar("honey", "Ситост", out _fullBar, out _fullText));
        stats.Children.Add(Bar("energy", "Енергия", out _energyBar, out _energyText));
        Grid.SetColumn(stats, 1);
        top.Children.Add(stats);
        p.Children.Add(top);

        p.Children.Add(Ribbon("Какво да правим?"));
        var actions = new UniformGrid { Columns = 4 };
        actions.Children.Add(Wide(Button("Нахрани", () => SelectTab(FoodTab), icon: "food")));
        _sleepButton = Wide(Button("", () =>
        {
            if (_pet.IsAsleep) _pet.WakeUp();
            else _pet.GoToSleep();
            Refresh(false);
        }));
        actions.Children.Add(_sleepButton);
        actions.Children.Add(Wide(Button("Погали", _pet.Pet, icon: "heart")));
        actions.Children.Add(Wide(Button("Зар", _pet.RollDice, icon: "dice")));
        _quietButton = Wide(Button("", () =>
        {
            _pet.SetQuiet(!_pet.IsQuiet);
            Refresh(false);
        }));
        actions.Children.Add(_quietButton);
        actions.Children.Add(Wide(Button("Рисувай", () => { Hide(); _pet.OpenDrawing(); }, icon: "pencil")));
        actions.Children.Add(Wide(Button("Поговори", () => { Hide(); _pet.Ask(Conversations.Next(_pet)); }, icon: "bear")));
        _couchButton = Wide(Button("Стани", () =>
        {
            _pet.GetUp();
            Refresh(true);
        }, icon: "walk"));
        _couchButton.ToolTip = Tip("Става от кътчето и пак се разхожда.");
        actions.Children.Add(_couchButton);
        p.Children.Add(actions);

        // „Почивай си“: Тут избира какво да прави мечокът. Стои там до „Стани“.
        p.Children.Add(Ribbon("Почивай си", "couch"));
        p.Children.Add(_relaxGrid);
        return new ScrollViewer { Content = p, MaxHeight = 600 * U, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private readonly UniformGrid _relaxGrid = new() { Columns = 2 };

    private void BuildRelaxButtons()
    {
        var s = _pet.Save;
        var chosen = _pet.CurrentFun;
        _relaxGrid.Children.Clear();
        var random = Wide(Button("Случайно", () =>
        {
            _pet.Relax(_pet.RandomUnlockedFun());
            Refresh(true);
        }, icon: "dice"));
        random.HorizontalContentAlignment = HorizontalAlignment.Left;
        random.ToolTip = Tip("Мечокът сам избира едно от отключените кътчета.");
        _relaxGrid.Children.Add(random);
        foreach (var scene in Scenes.Fun)
        {
            var sc = scene;
            bool unlocked = Scenes.IsUnlocked(sc, s);
            // Каквото прави сега, свети; цъкнеш ли го пак, спира.
            var b = Wide(Button(sc.Name, () =>
            {
                if (_pet.CurrentFun == sc) _pet.GetUp();
                else _pet.Relax(sc);
                Refresh(true);
            }, primary: chosen == sc, icon: unlocked ? sc.Icon : "lock"));
            b.HorizontalContentAlignment = HorizontalAlignment.Left;
            if (!unlocked)
            {
                var item = Kitchen.Find(sc.Unlock!);
                b.IsEnabled = false;
                b.ToolTip = Tip($"Заключено. Трябва „{item?.Name}“ от магазина ({item?.Price} лешника).");
                ToolTipService.SetShowOnDisabled(b, true);
            }
            else if (chosen == sc) b.ToolTip = Tip("Сега прави това. Цъкни пак, за да спре.");
            _relaxGrid.Children.Add(b);
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
        ((FrameworkElement)segments[0].Parent).ToolTip = Tip($"{value:0} от 100");
    }

    // ───────────────────────── Храна ─────────────────────────

    private UIElement BuildFoodTab()
    {
        var p = new StackPanel();
        _foodInfo = new TextBlock { Foreground = Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 0, 0, 10) };
        p.Children.Add(_foodInfo);
        p.Children.Add(_foodTiles);
        return new ScrollViewer { Content = p, MaxHeight = 560 * U, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    /// <summary>Плочки с храните: картинка, какво дава и бутон „Сготви“. Посочиш ли заключена, казва защо.</summary>
    private void BuildFoodTiles()
    {
        var s = _pet.Save;
        _foodTiles.Children.Clear();
        foreach (var food in Kitchen.Foods)
        {
            var f = food;
            int cost = Kitchen.CostOf(f, s);

            // Какво дава: значки с иконки на един ред.
            var effects = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
            if (f.Fullness >= 5) effects.Children.Add(Stat("honey", $"+{Kitchen.FullnessOf(f, s):0}", "ситост"));
            if (f.Energy > 0) effects.Children.Add(Stat("energy", $"+{f.Energy:0}", "енергия"));

            Button action;
            string? why = null;
            bool locked = !Kitchen.CanCook(f, s);
            if (locked)
            {
                var tool = Kitchen.Find(f.Tool!);
                action = Button(tool?.Name ?? "Заключено", () => SelectTab(ShopTab), icon: "lock");
                why = $"Заключено. Трябва ти „{tool?.Name}“ от магазина: {tool?.Price} лешника (имаш {s.Hazelnuts}). " +
                      "Цъкни бутона, за да отидеш в магазина.";
            }
            else if (cost == 0 && Kitchen.BerriesIn(s) > 0)
            {
                action = Button($"{Kitchen.BerriesIn(s)} мин", () => { }, icon: "hourglass");
                action.IsEnabled = false;
                why = $"Храстът още не е дал нови боровинки. Пак след {Kitchen.BerriesIn(s)} мин.";
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
                    why = $"Трябват {cost} лешника, а имаш {s.Hazelnuts}. Свърши някоя задача или пусни мечо-доро.";
                }
            }
            _foodTiles.Children.Add(MakeTile(_pet.PropImage("food_" + f.Id), f.Name, effects, action, locked, why));
        }
    }

    /// <summary>Значка: иконка и число (например мед +24).</summary>
    private static Border Stat(string icon, string value, string what) => new()
    {
        Background = Cream,
        BorderBrush = WoodDark,
        BorderThickness = new Thickness(2),
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(6 * U, 2, 8 * U, 2),
        Margin = new Thickness(2, 0, 2, 0),
        Child = IconText(icon, value, bold: true),
        ToolTip = Tip($"{value} {what}"),
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

    private void BuildShopTiles()
    {
        var s = _pet.Save;
        _shopTiles.Children.Clear();
        UniformGrid? grid = null;
        string shelf = "";
        foreach (var upgrade in Kitchen.Upgrades.OrderBy(u => u.Shelf == Kitchen.KitchenShelf ? 0 : u.Shelf == Kitchen.FunShelf ? 1 : 2))
        {
            if (upgrade.Shelf != shelf)
            {
                shelf = upgrade.Shelf;
                var ribbon = Ribbon(shelf, shelf == Kitchen.KitchenShelf ? "food" : shelf == Kitchen.FunShelf ? "couch" : "plus");
                if (_shopTiles.Children.Count == 0) ribbon.Margin = new Thickness(0, 0, 0, 8);
                _shopTiles.Children.Add(ribbon);
                grid = new UniformGrid { Columns = 3 };
                _shopTiles.Children.Add(grid);
            }
            var u = upgrade;
            Button action;
            string? why = null;
            bool owned = Kitchen.Owns(s, u.Id);
            if (owned)
            {
                action = Button("Купено", () => { });
                action.IsEnabled = false;
            }
            else
            {
                action = Button($"Купи  {u.Price}", () => _pet.Buy(u), primary: true, icon: "nut");
                if (s.Hazelnuts < u.Price)
                {
                    action.IsEnabled = false;
                    why = $"Трябват {u.Price} лешника, а имаш {s.Hazelnuts}. Още {u.Price - s.Hazelnuts}!";
                }
            }
            var description = new TextBlock
            {
                Text = u.Description,
                Foreground = Muted,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
            };
            grid!.Children.Add(MakeTile(_pet.PropImage("shop_" + u.Id), u.Name, description, action, dim: false, why, owned));
        }
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
            Height = 96 * U,
            Child = artGrid,
        };

        var dock = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(artBox, Dock.Top);
        dock.Children.Add(artBox);

        action.Margin = new Thickness(0, 8 * U, 0, 0);
        action.HorizontalAlignment = HorizontalAlignment.Stretch;
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
            BorderThickness = new Thickness(2, 2, 2, 4),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8 * U),
            Margin = new Thickness(0, 0, 8 * U, 8 * U),
            Child = dock,
        };
        tile.MouseEnter += (_, _) => tile.Background = Hover;
        tile.MouseLeave += (_, _) => tile.Background = Tile;
        if (why != null)
        {
            // Подсказката излиза и над изключения бутон.
            tile.ToolTip = Tip(why);
            action.ToolTip = Tip(why);
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
        _model = Pixel(_pet.Portrait, ArtScale(3));
        _model.HorizontalAlignment = HorizontalAlignment.Center;
        _model.VerticalAlignment = VerticalAlignment.Bottom;
        _model.Margin = new Thickness(0, 0, 0, 6 * U);
        top.Children.Add(new Border
        {
            Background = TileArt,
            BorderBrush = Ink,
            BorderThickness = new Thickness(3, 3, 3, 5),
            CornerRadius = new CornerRadius(6),
            Width = 150 * U,
            Height = 150 * U,
            Child = _model,
            VerticalAlignment = VerticalAlignment.Top,
        });

        var right = new StackPanel { Margin = new Thickness(16 * U, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        right.Children.Add(new TextBlock { Text = "Облечено", FontSize = Ui.Title, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 6 * U) });
        right.Children.Add(_wornList);
        right.Children.Add(new TextBlock
        {
            Text = "Дрехите се купуват с лешници. Облечи някоя и мечокът ще се преоблече зад вратата на гардероба.",
            Foreground = Muted,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6 * U, 0, 0),
        });
        Grid.SetColumn(right, 1);
        top.Children.Add(right);
        p.Children.Add(top);
        p.Children.Add(_wardrobeShelves);
        return new ScrollViewer { Content = p, MaxHeight = 560 * U, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private void BuildWardrobe()
    {
        var s = _pet.Save;
        _model.Source = _pet.Portrait;

        _wornList.Children.Clear();
        foreach (var slot in Clothes.Shelves)
        {
            var g = Clothes.Worn(s, slot);
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 3) };
            var icon = Icon(slot.Icon);
            icon.Margin = new Thickness(0, 0, 6, 0);
            icon.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(icon, Dock.Left);
            row.Children.Add(icon);
            if (g != null)
            {
                var off = Chip("✕", () => _pet.TakeOff(slot));
                off.ToolTip = Tip("Свали");
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
        foreach (var slot in Clothes.Shelves)
        {
            var ribbon = Ribbon(slot.Name, slot.Icon);
            _wardrobeShelves.Children.Add(ribbon);
            var grid = new UniformGrid { Columns = 3 };
            _wardrobeShelves.Children.Add(grid);
            foreach (var garment in Clothes.All.Where(g => g.SlotId == slot.Id))
            {
                var g = garment;
                bool owned = Clothes.Owns(s, g), worn = Clothes.IsWorn(s, g);
                string? why = null;
                Button action;
                if (worn) action = Button("Свали", () => _pet.TakeOff(slot));
                else if (owned) action = Button("Облечи", () => _pet.Wear(g), primary: true);
                else
                {
                    action = Button($"Купи  {g.Price}", () => _pet.BuyGarment(g), primary: true, icon: "nut");
                    if (s.Hazelnuts < g.Price)
                    {
                        action.IsEnabled = false;
                        why = $"Трябват {g.Price} лешника, а имаш {s.Hazelnuts}. Още {g.Price - s.Hazelnuts}!";
                    }
                }
                // Плочката показва мечока с тази дреха и с всичко друго, което е облякъл.
                var outfit = new Dictionary<string, string>(s.Outfit) { [g.SlotId] = g.Id };
                var details = new TextBlock
                {
                    Text = worn ? "Облечено" : owned ? "В гардероба" : "",
                    Foreground = Muted,
                    TextAlignment = TextAlignment.Center,
                };
                grid.Children.Add(MakeTile(_pet.DressedPreview(outfit), g.Name, details, action, dim: false, why, owned: worn, artScale: 2));
            }
        }
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
            _sizeButtons[i].ToolTip = Tip($"Носи {labels[i].Split(' ')[1].TrimStart('+')} лешника");
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

        _taskSummary = new TextBlock { Foreground = Muted, Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap };
        p.Children.Add(_taskSummary);
        return p;
    }

    private void BuildTaskList()
    {
        _taskList.Children.Clear();
        var tasks = _pet.Save.Tasks.OrderBy(t => t.Done).ToList();
        if (tasks.Count == 0)
            _taskList.Children.Add(MutedText("Няма задачи. Напиши какво ще правиш днес, а мечокът ще ти пази списъка."));

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
            del.ToolTip = Tip("Изтрий");
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
            _sizeButtons[i].Background = i + 1 == _taskSize ? Honey : PaperDark;
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
            _noteList.Children.Add(MutedText("Тефтерът е празен. Запиши идея, телефон, мисъл, която те разсейва… Мечокът ще я пази."));
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
            del.ToolTip = Tip("Изтрий");
            Grid.SetColumn(del, 1);
            grid.Children.Add(del);

            _noteList.Children.Add(Card(grid, false));
        }
    }

    // ───────────────────────── Време ─────────────────────────

    private UIElement BuildPomodoroTab()
    {
        var p = new StackPanel();
        var pomRibbon = Ribbon("Мечо-доро", "honey");
        pomRibbon.Margin = new Thickness(0, 0, 0, 8);
        p.Children.Add(pomRibbon);
        _pomTime = new TextBlock { FontFamily = MonoFont, FontSize = Huge, HorizontalAlignment = HorizontalAlignment.Center };
        _pomStatus = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };
        p.Children.Add(new Border
        {
            Background = TileArt,
            BorderBrush = Ink,
            BorderThickness = new Thickness(2, 2, 2, 4),
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

        // Работен таймер: мечокът сяда на бюрото и работи сериозно, докато тече.
        _timerWork = _pet.Save.TimerIsWork;
        _workChip = Chip("", () =>
        {
            _timerWork = !_timerWork;
            UpdateWorkChip();
        });
        _workChip.HorizontalAlignment = HorizontalAlignment.Left;
        _workChip.Margin = new Thickness(0, 8, 0, 0);
        _workChip.ToolTip = Tip("Работен: мечокът сяда на бюрото, слага очила и работи сериозно с теб. Изключи го за таймер за чай или пране.");
        p.Children.Add(_workChip);
        UpdateWorkChip();

        var presets = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        foreach (int m in new[] { 5, 10, 15, 30, 45, 60 })
        {
            int minutes = m;
            var chip = Chip($"{m} мин", () => _pet.StartTimer(minutes, _timerLabel.Text, _timerWork));
            chip.Margin = new Thickness(0, 0, 6, 6);
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
        custom.Children.Add(_timerMinutes);
        custom.Children.Add(new TextBlock { Text = "мин", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 8, 0) });
        var start = Button("Пусни", StartCustomTimer, primary: true, icon: "play");
        start.Margin = new Thickness(0);
        custom.Children.Add(start);
        _timerStop = Button("Спри таймера", _pet.StopTimer, icon: "stop");
        _timerStop.Margin = new Thickness(6, 0, 0, 0);
        custom.Children.Add(_timerStop);
        p.Children.Add(custom);

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
        SetChip(_workChip, _timerWork ? "✓ Работен (мечокът работи с теб)" : "✕ Не е за работа");
        _workChip.Background = _timerWork ? Honey : PaperDark;
    }

    private void StartCustomTimer()
    {
        string text = _timerMinutes.Text.Trim().Replace(',', '.');
        if (double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double m) && m > 0 && m <= 24 * 60)
        {
            _pet.StartTimer(m, _timerLabel.Text, _timerWork);
            _timerMinutes.Clear();
        }
    }

    // ───────────────────────── Още ─────────────────────────

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
        foreach (var b in new[] { _updateButton, reset, hide })
        {
            b.HorizontalAlignment = HorizontalAlignment.Stretch;
            b.HorizontalContentAlignment = HorizontalAlignment.Left;
            b.Margin = new Thickness(0, 0, 0, 8);
            p.Children.Add(b);
        }
        p.Children.Add(MutedText($"Мечо, версия {Updater.CurrentVersion}. Шрифт Pixeloid (GGBotNet, SIL OFL)."));
        return p;
    }

    // ───────────────────────── Обновяване ─────────────────────────

    private void Refresh(bool rebuild)
    {
        var s = _pet.Save;
        _nuts.Text = s.Hazelnuts.ToString();

        // Мечо
        _portrait.Source = _pet.Portrait;
        _mood.Text = _pet.IsAsleep ? "Ззз… спи сладко" :
            s.Fullness < 25 ? "Гладен е!" :
            s.Energy < 25 ? "Много е уморен" :
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
            >= 40 => "добре",
            >= 25 => "уморен",
            _ => "капнал!",
        });
        if (_pet.IsAsleep) SetButton(_sleepButton, "Събуди", "sun");
        else SetButton(_sleepButton, "Приспи", "moon");
        _sleepButton.IsEnabled = _pet.CanSleep;
        _couchButton.IsEnabled = _pet.StaysPut || _pet.CurrentFun != null;
        if (_pet.IsQuiet) SetButton(_quietButton, "Говори", "bell");
        else SetButton(_quietButton, "Тихо 1 ч", "quiet");

        // Храна и магазин
        _foodInfo.Text = "Сготвеното се появява из екрана: занеси го при мечока с мишката.";
        _shopInfo.Text = "Уредите отключват нови ястия, а подобренията помагат на мечока. Лешниците се печелят " +
                         "със задачи и с работа: всеки 10 мин мечо-доро или таймер = 1" +
                         (s.FocusMinutesBank >= 1 ? $" (събрани {s.FocusMinutesBank:0} мин към следващия)." : ".");
        if (rebuild)
        {
            BuildRelaxButtons();
            BuildFoodTiles();
            BuildShopTiles();
            BuildWardrobe();
            BuildTaskList();
            BuildNoteList();
        }

        // Време
        var left = _pet.PomodoroLeft;
        switch (s.PomodoroPhase)
        {
            case PomodoroPhase.Focus:
                _pomTime.Text = $"{(int)left.TotalMinutes:00}:{left.Seconds:00}";
                _pomStatus.Text = "Работим заедно. Мечокът мълчи.";
                break;
            case PomodoroPhase.Break:
                _pomTime.Text = $"{(int)left.TotalMinutes:00}:{left.Seconds:00}";
                _pomStatus.Text = "Почивка: стани, раздвижи се, пийни вода.";
                break;
            default:
                _pomTime.Text = $"{(int)PetWindow.FocusLength.TotalMinutes:00}:00";
                _pomStatus.Text = "Готова ли си?";
                break;
        }
        _pomStart.Visibility = s.PomodoroPhase == PomodoroPhase.Focus ? Visibility.Collapsed : Visibility.Visible;
        SetButton(_pomStart, s.PomodoroPhase == PomodoroPhase.Break ? "Нов мечо-доро" : "Започни 25 минути", "play");
        _pomStop.Visibility = s.PomodoroPhase == PomodoroPhase.Off ? Visibility.Collapsed : Visibility.Visible;
        _pomToday.Text = $"Днес: {s.PomodorosToday}   ·   Общо: {s.PomodorosTotal}";

        if (_pet.TimerRunning)
        {
            var t = _pet.TimerLeft;
            string clock = t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{(int)t.TotalMinutes:00}:{t.Seconds:00}";
            _timerTime.Text = s.TimerLabel.Length > 0 ? $"{clock}  {s.TimerLabel}" : clock;
        }
        else _timerTime.Text = "--:--";
        _timerStop.Visibility = _pet.TimerRunning ? Visibility.Visible : Visibility.Collapsed;

        // Още
        if (_pet.Updater.IsAvailable) SetButton(_updateButton, $"Обнови до версия {_pet.Updater.LatestVersion}", "update");
        else SetButton(_updateButton, $"Провери за обновление (сега: {Updater.CurrentVersion})", "update");
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
        BorderThickness = new Thickness(2, 2, 2, 3),
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
        BorderThickness = new Thickness(2, 2, 2, 3),
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(10, 7, 7, 7),
        Margin = new Thickness(0, 0, 0, 6),
        Child = child,
    };
}
