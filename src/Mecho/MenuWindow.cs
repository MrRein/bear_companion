using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
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
    private readonly FrameworkElement[] _tabButtons = new FrameworkElement[8];
    private readonly UIElement[] _tabs = new UIElement[9];

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
    private FrameworkElement? _fullRow, _energyRow;

    /// <summary>За обиколката: къде на екрана е нещото (лентите, таб, цялото меню).</summary>
    internal Rect? TutorialTarget(string what)
    {
        if (what == "menu") return new Rect(Left, Top, ActualWidth, ActualHeight);
        if (what == "bars")
            return TutorialOverlay.ScreenRect(_fullRow) is Rect a && TutorialOverlay.ScreenRect(_energyRow) is Rect b ? Rect.Union(a, b) : null;
        if (what.StartsWith("tab:") && int.TryParse(what[4..], out int i) && i >= 0 && i < _tabButtons.Length)
            return TutorialOverlay.ScreenRect(_tabButtons[i]);
        return null;
    }
    private TextBlock _fullText = null!, _energyText = null!, _mood = null!, _weather = null!;
    private StackPanel _weatherRow = null!;
    private readonly Border _weatherIcon = new() { VerticalAlignment = VerticalAlignment.Center };
    private string? _weatherIconName;
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
        // Размерът се мени с дръжката долу вдясно (нещата се пренареждат, страниците се
        // превъртат); мащабът (100/125/150%) е в „Още“. Помнят се.
        ResizeMode = ResizeMode.CanResizeWithGrip;
        ShowInTaskbar = false;
        Topmost = true;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        SizeToContent = SizeToContent.Manual;
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
        _tabs[TasksTab] = new Border();          // старият индекс (задачите са в „Работа“ → Днес)
        _tabs[NotesTab] = BuildNotesTab();
        _tabs[PomodoroTab] = BuildPomodoroTab(); // „Фокус“: мечо-доро и таймер
        _tabs[MoreTab] = BuildMoreTab();
        Work = new WorkView(_pet);
        Work.OpenFocus += () => ShowPage(PageFocus);
        // Изгледът е сменен отвътре (покана, „Отвори“…) – горе и вляво се вижда точната страница.
        Work.ViewChanged += view =>
        {
            string page = view switch
            {
                WorkView.View.Week => PageWeek,
                WorkView.View.Review => PageReview,
                WorkView.View.Backlog => PageBacklog,
                WorkView.View.Calendar => PageCalendar,
                _ => _page is PagePlan or PageShutdown ? _page : PageToday,
            };
            if (page == _page || !IsWorkPage) return;
            _page = page;
            _pet.Save.MenuPage = page;
            UpdateNavSelection();
        };
        _tabs[WorkPage] = Work;

        // Всяка страница се превърта сама: колкото и малък да е прозорецът, нищо не изчезва.
        var content = new Grid();
        for (int i = 0; i < _tabs.Length; i++)
        {
            if (_tabs[i] is not ScrollViewer && _tabs[i] is FrameworkElement page && i != TasksTab && i != WorkPage) _tabs[i] = TabScroll(page, 0);
            content.Children.Add(_tabs[i]);
        }

        // Горе – тънка лента: къде си (Мечо › Храна) и лешниците; за нея се мести менюто.
        // Вляво – страниците. Отдясно – хартията; в раздела „Мечо“ над нея винаги стои мечокът.
        var root = new DockPanel();
        var top = BuildTopBar();
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        _nav = BuildNav();
        DockPanel.SetDock(_nav, Dock.Left);
        root.Children.Add(_nav);
        _hero = BuildHero();
        var pageArea = new DockPanel();
        DockPanel.SetDock(_hero, Dock.Top);
        pageArea.Children.Add(_hero);
        pageArea.Children.Add(content);
        _paper = new Border
        {
            Background = PaperTexture,
            BorderBrush = Ink,
            BorderThickness = Flat,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14 * U, 12 * U, 14 * U, 14 * U),
            Child = pageArea,
        }.Skinned("panel");
        root.Children.Add(_paper);

        _frame = new Border
        {
            Background = WoodTexture,
            BorderBrush = Ink,
            BorderThickness = Flat,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(7, 5, 7, 7),
            Child = root,
        }.Skinned("frame");
        Content = _frame;
        ApplyZoom();
        SizeChanged += (_, _) => RememberSize();

        _pet.CareChanged += (_, _) => StateChangedRefresh();
        // Менюто само показва състоянието: всяка промяна в ядрото (лешници, притежавано,
        // предмети на земята) го пречертава. Абонаментите се махат при затваряне.
        var bus = _pet.CoreEngine.Bus;
        bus.Subscribe<Mecho.Core.StateChanged<Mecho.Core.WalletState>>(this, _ => StateChangedRefresh());
        bus.Subscribe<Mecho.Core.StateChanged<Mecho.Core.InventoryState>>(this, _ => StateChangedRefresh());
        bus.Subscribe<Mecho.Core.StateChanged<Mecho.Core.PlacementState>>(this, _ => StateChangedRefresh());
        Closed += (_, _) =>
        {
            bus.Unsubscribe(this);
            Work.Dispose();
        };
        _refresh = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _refresh.Tick += (_, _) => Refresh(rebuild: false);

        // Малкото меню (Мечо, Храна…) се скрива, щом цъкнеш другаде; „Работа“ стои като прозорец.
        // Цъкнато другаде – менюто се скрива; но не и когато е към отделните прозорчета за задачи.
        Deactivated += (_, _) => Dispatcher.BeginInvoke(() =>
        {
            if (!IsActive && !FloatWindow.AnyActive) Hide();
        }, System.Windows.Threading.DispatcherPriority.Input);
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
            if (_tab == WorkPage)
            {
                Work.OnKey(this, e);
                if (e.Handled || e.Key != Key.Escape || Keyboard.FocusedElement is TextBox) return;
            }
            if (e.Key == Key.Escape)
            {
                Hide();
                e.Handled = true;
            }
        };
        SourceInitialized += (_, _) => HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(WndProc);

        ShowPage(PageBear);
    }

    /// <summary>„Работа“ (Днес, Седмица, Преглед) – вътре в менюто.</summary>
    public WorkView Work { get; private set; } = null!;

    /// <summary>Тъчпадът встрани (WM_MOUSEHWHEEL) – към седмицата.</summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_MOUSEHWHEEL = 0x020E;
        if (msg == WM_MOUSEHWHEEL && _tab == WorkPage)
        {
            Work.HorizontalWheel((short)((wParam.ToInt64() >> 16) & 0xFFFF));
            handled = true;
        }
        return IntPtr.Zero;
    }

    // ───────────────────────── Страници ─────────────────────────

    public const int WorkPage = 8;

    public const string PageBear = "bear", PageFood = "food", PageWardrobe = "wardrobe", PageShop = "shop",
        PageToday = "today", PageFocus = "focus", PageNotes = "notes", PagePlan = "plan", PageShutdown = "shutdown",
        PageWeek = "week", PageReview = "review", PageMore = "more", PageCalendar = "calendar", PageBacklog = "backlog";

    /// <summary>Страница: иконка, име, кой таб показва, на кое място в лентата (Group – под „Работа“).</summary>
    private sealed record PageInfo(string Id, string Icon, string Name, int Tab, bool InNav, bool Work = false);

    private static readonly PageInfo[] Pages =
    {
        new(PageBear, "bear", "Мечо", BearTab, true),
        new(PageNotes, "notes", "Бележки", NotesTab, true),
        new(PageToday, "sun", "Днес", WorkPage, true, Work: true),
        new(PageFocus, "honey", "Фокус", PomodoroTab, true, Work: true),
        new(PageWeek, "board", "Седмица", WorkPage, true, Work: true),
        new(PageCalendar, "calendar", "Календар", WorkPage, true, Work: true),
        new(PageBacklog, "backlog", "Чакащи", WorkPage, true, Work: true),
        new(PageReview, "book", "Преглед", WorkPage, true, Work: true),
        new(PageShop, "shop", "Магазин", ShopTab, true),
        new(PageMore, "gear", "Още", MoreTab, true),
        // В раздела „Мечо“ (картите под мечока), не в лентата:
        new(PageFood, "food", "Храна", FoodTab, false),
        new(PageWardrobe, "wardrobe", "Гардероб", WardrobeTab, false),
        // Поканите в „Днес“ (от иконката до часовника):
        new(PagePlan, "sun", "План за деня", WorkPage, false, Work: true),
        new(PageShutdown, "moon", "Край на деня", WorkPage, false, Work: true),
    };

    public static bool IsPage(string? id) => Pages.Any(p => p.Id == id);

    /// <summary>Стар таб (и част на „Работа“) → страница.</summary>
    public static string PageForTab(int tab, int workPart = 2) => tab switch
    {
        TasksTab => PageToday,
        PomodoroTab => workPart == 2 ? PageToday : PageFocus,
        _ => Pages.FirstOrDefault(p => p.Tab == tab && !p.Work)?.Id ?? PageBear,
    };

    private string _page = PageBear;

    /// <summary>Сега – на страница от „Работа“.</summary>
    public bool IsWorkPage => Pages.FirstOrDefault(p => p.Id == _page)?.Work == true;

    public string CurrentPage => _page;

    /// <summary>Отваря страница: таба ѝ и (за „Работа“) изгледа; менюто става голямо или малко.</summary>
    public void ShowPage(string id)
    {
        var page = Pages.FirstOrDefault(p => p.Id == id) ?? Pages[0];
        if (page.Tab == WardrobeTab && !DailyOffer.HasWardrobe(_pet.Save)) page = Pages[0];
        _page = page.Id;
        // „План за деня“ и „Край на деня“ не се помнят – иначе всяко отваряне на менюто пак ги вади.
        _pet.Save.MenuPage = page.Id is PagePlan or PageShutdown ? PageToday : page.Id;
        if (page.Work) _workNavOpen = true;
        SelectTab(page.Tab);
        switch (page.Id)
        {
            case PageToday: Work.SetView(WorkView.View.Today); break;
            case PageWeek: Work.SetView(WorkView.View.Week); break;
            case PageReview: Work.SetView(WorkView.View.Review); break;
            case PageCalendar: Work.SetView(WorkView.View.Calendar); break;
            case PageBacklog: Work.SetView(WorkView.View.Backlog); break;
            case PagePlan: Work.OpenSide("plan"); break;
            case PageShutdown: Work.OpenSide("shutdown"); break;
        }
        UpdateNavSelection();
        UpdateHero();
        if (page.Work) _pet.TutorialEvent("tab:" + PomodoroTab);   // обиколката: „отвори Работа“
    }

    private void SelectTab(int index)
    {
        _pet.TutorialEvent("tab:" + index);
        if (index == TasksTab) index = WorkPage;
        _tab = index;
        for (int i = 0; i < _tabs.Length; i++)
            _tabs[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;
        // Гардеробът стои до мечока, докато е отворен табът.
        if (index == WardrobeTab && IsVisible) _pet.OpenWardrobe();
        else if (index != WardrobeTab) _pet.CloseWardrobe();
        Refresh(rebuild: false);
        BuildTab(index);
        if (index == NotesTab && _notesView == 3) Dispatcher.BeginInvoke(() => _noteTitle.Focus(), DispatcherPriority.Input);
    }

    /// <summary>
    /// Показва менюто на страница: малкото – там, където Тут го е оставила, или до мечока
    /// (вдясно или вляво, където има място); голямото („Работа“) – където е било (отначало в средата).
    /// </summary>
    public void ShowNear(Rect bear, Rect area, string page)
    {
        _hideToken++;            // отваря се пак, докато се скриваше: не го крий
        _hiding = false;
        Opacity = 1;
        // Windows може да го е „свил“ (Win+D, „покажи работния плот“): винаги нормален размер.
        if (WindowState != WindowState.Normal) WindowState = WindowState.Normal;
        var info = Pages.FirstOrDefault(p => p.Id == page) ?? Pages[0];
        _dirty[info.Tab] = true;   // винаги свежо (офертата може да е сменена от Developer)
        ShowPage(page);
        ApplyZoom();
        FitToArea(area);
        PlaceNear(bear, area, new Size(Width, Height));
        Show();
        if (_tab == WardrobeTab) _pet.OpenWardrobe();
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
        _pet.Persist();      // размерът и страницата
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
    private readonly bool[] _dirty = Enumerable.Repeat(true, 9).ToArray();

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
            case NotesTab: BuildNoteList(); break;
        }
    }

    /// <summary>Тест за скорост: колко мс строи всеки таб (наново) и подрежда менюто.</summary>
    internal List<(string Tab, double Ms)> BenchTabs()
    {
        var result = new List<(string, double)>();
        foreach (var page in Pages)
        {
            if (page.Tab < _dirty.Length) _dirty[page.Tab] = true;
            _foodKey = null;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            ShowPage(page.Id);
            RefreshNow(false);
            UpdateLayout();
            result.Add((page.Name, watch.Elapsed.TotalMilliseconds));
        }
        ShowPage(PageBear);
        return result;
    }

    // ───────────────────────── Горе и вляво ─────────────────────────

    private FrameworkElement _nav = null!;
    private Border _paper = null!;
    private readonly Dictionary<string, (Border Item, TextBlock Name)> _navItems = new();
    private readonly TextBlock _workBadgeText = new() { FontWeight = FontWeights.Bold, Foreground = Ink };
    private Border _workBadge = null!;
    private readonly StackPanel _workChildren = new();
    private Border? _workBox;
    private StackPanel _navList = null!;
    private Grid _navHost = null!;
    private TextBlock _workArrow = null!;
    private bool _navCompact, _workNavOpen;
    private readonly TextBlock _where = new()
    {
        FontWeight = FontWeights.Bold,
        Foreground = OnWood,
        VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis,
    };

    /// <summary>Тънката лента горе: къде си (Мечо › Храна) и лешниците. За нея се мести менюто.</summary>
    private UIElement BuildTopBar()
    {
        var bar = new DockPanel { Margin = new Thickness(6, 0, 2, 5), Background = Brushes.Transparent, Cursor = Cursors.SizeAll };
        var nutRow = new StackPanel { Orientation = Orientation.Horizontal };
        var nut = Icon("nut");
        nut.Margin = new Thickness(0, 0, 6, 0);
        nutRow.Children.Add(nut);
        _nuts.VerticalAlignment = VerticalAlignment.Center;
        nutRow.Children.Add(_nuts);
        var nuts = Pill(nutRow);
        nuts.Cursor = Cursors.Hand;
        nuts.ToolTip = Tip("Лешници – за храна, уреди и мебели (цъкни – магазинът)");
        nuts.MouseLeftButtonDown += (_, e) => e.Handled = true;
        nuts.MouseLeftButtonUp += (_, e) =>
        {
            ShowPage(PageShop);
            e.Handled = true;
        };
        DockPanel.SetDock(nuts, Dock.Right);
        bar.Children.Add(nuts);
        _where.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Color.FromRgb(13, 18, 28), ShadowDepth = 1.5, Direction = 315, BlurRadius = 0, Opacity = 1 };
        bar.Children.Add(_where);
        bar.MouseLeftButtonDown += (_, e) =>
        {
            if (e.Handled) return;
            try { DragMove(); }
            catch (InvalidOperationException) { return; }
            _pet.Save.MenuLeft = Left;
            _pet.Save.MenuTop = Top;
            _pet.Persist();
        };
        return bar;
    }

    /// <summary>
    /// Лентата вляво: копчета с иконка и име (както преди). „Работа“ се разгъва: Днес, Фокус,
    /// Седмица, Преглед. Тесен прозорец – само иконките. Празното място също мести менюто.
    /// </summary>
    private FrameworkElement BuildNav()
    {
        var list = new StackPanel { Margin = new Thickness(0, 0, 7, 0) };
        foreach (var page in Pages.Where(p => p.InNav && !p.Work))
        {
            list.Children.Add(NavItem(page, child: false));
            if (page.Id == PageNotes)
            {
                // „Работа ▸“ – разгъва/свива; като се разгъне, отваря последната си страница (или Днес).
                var work = NavItem(new PageInfo("work", "tasks", "Работа", WorkPage, true), child: false);
                list.Children.Add(work);
                foreach (var child in Pages.Where(p => p.InNav && p.Work)) _workChildren.Children.Add(NavItem(child, child: true));
                // Под „Работа“ – с линия отляво (личи си, че са в нея; и когато са само иконки).
                _workBox = new Border
                {
                    BorderBrush = WoodDark,
                    BorderThickness = new Thickness(Math.Max(2, P / 2), 0, 0, 0),
                    Margin = new Thickness(8 * U, 0, 0, 6 * U),
                    Padding = new Thickness(6 * U, 0, 0, 0),
                    Child = _workChildren,
                };
                list.Children.Add(_workBox);
            }
        }
        // Под „Още“ – „Затвори“ (менюто се затваря и с Esc и като цъкнеш другаде).
        var closeRow = new DockPanel();
        var closeIcon = Icon("close", 2);
        closeIcon.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(closeIcon, Dock.Left);
        closeRow.Children.Add(closeIcon);
        var closeName = new TextBlock { Text = "Затвори", FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8 * U, 0, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
        closeRow.Children.Add(closeName);
        var close = Chip("", Hide);
        close.Child = closeRow;
        close.Padding = new Thickness(8 * U, 5 * U, 10 * U, 6 * U);
        close.Margin = new Thickness(0, 10 * U, 0, 5 * U);
        close.HorizontalAlignment = HorizontalAlignment.Stretch;
        close.ToolTip = Tip("Затвори менюто (Esc)");
        ToolTipService.SetIsEnabled(close, false);
        _navItems["close"] = (close, closeName);
        list.Children.Add(close);
        var scroll = new ScrollViewer
        {
            Content = list,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Background = Brushes.Transparent,
        };
        scroll.MouseLeftButtonDown += (_, e) =>
        {
            if (e.Handled) return;
            try { DragMove(); }
            catch (InvalidOperationException) { return; }
            _pet.Save.MenuLeft = Left;
            _pet.Save.MenuTop = Top;
            _pet.Persist();
        };
        _navList = list;
        // Дръжка на десния ръб: дърпаш – лентата става по-широка/тясна; съвсем тясна – само иконки.
        var grip = new Border
        {
            Width = 6 * U,
            Background = Brushes.Transparent,
            Cursor = Cursors.SizeWE,
            HorizontalAlignment = HorizontalAlignment.Right,
            ToolTip = Tip("Дръпни: по-широка или само иконки"),
        };
        double startX = 0, startW = 0;
        bool dragging = false;
        grip.MouseLeftButtonDown += (_, e) =>
        {
            dragging = true;
            startX = e.GetPosition(this).X;
            startW = _navHost.ActualWidth;
            grip.CaptureMouse();
            e.Handled = true;
        };
        grip.MouseMove += (_, e) =>
        {
            if (!dragging) return;
            double w = startW + e.GetPosition(this).X - startX;
            // Плавно: по-тясно – имената се режат; под ~120 – само иконките (копчетата същите).
            bool collapse = w < 120 * U;
            _pet.Save.NavCollapsed = collapse;
            if (!collapse) _pet.Save.NavWidth = Math.Clamp(w, 120 * U, 320 * U) / U;
            UpdateNav();
        };
        grip.MouseLeftButtonUp += (_, _) =>
        {
            if (!dragging) return;
            dragging = false;
            grip.ReleaseMouseCapture();
            _pet.Persist();
        };
        grip.MouseEnter += (_, _) => grip.Background = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0));
        grip.MouseLeave += (_, _) => { if (!dragging) grip.Background = Brushes.Transparent; };
        _navHost = new Grid { Children = { scroll, grip } };
        return _navHost;
    }

    private string _lastWorkPage = PageToday;

    private Border NavItem(PageInfo page, bool child)
    {
        var icon = Icon(page.Icon, child ? 1 : 2);
        icon.VerticalAlignment = VerticalAlignment.Center;
        var name = new TextBlock
        {
            Text = page.Name,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8 * U, 0, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var row = new DockPanel();
        if (page.Id == "work")
        {
            _workArrow = new TextBlock { Text = "▸", FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
            DockPanel.SetDock(_workArrow, Dock.Right);
            row.Children.Add(_workArrow);
            _workBadge = new Border
            {
                Background = Honey,
                BorderBrush = Ink,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(4, 0, 4, 1),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0),
                Child = _workBadgeText,
                Visibility = Visibility.Collapsed,
            };
            DockPanel.SetDock(_workBadge, Dock.Right);
            row.Children.Add(_workBadge);
        }
        DockPanel.SetDock(icon, Dock.Left);
        row.Children.Add(icon);
        row.Children.Add(name);
        var item = Chip("", () =>
        {
            if (page.Id == "work")
            {
                // Отворена ли е – свива се; иначе се разгъва на последната страница от „Работа“.
                if (_workNavOpen && IsWorkPage) _workNavOpen = false;
                else ShowPage(_lastWorkPage);
                UpdateNavSelection();
            }
            else ShowPage(page.Id);
        });
        item.Child = row;
        item.Padding = child ? new Thickness(8 * U, 3 * U, 8 * U, 4 * U) : new Thickness(8 * U, 5 * U, 10 * U, 6 * U);
        item.Margin = new Thickness(0, 0, 0, (child ? 3 : 5) * U);
        item.HorizontalAlignment = HorizontalAlignment.Stretch;
        item.ToolTip = Tip(page.Name);
        ToolTipService.SetIsEnabled(item, false);
        _navItems[page.Id] = (item, name);
        // За обиколката: „Работа“ е самото копче „Работа“.
        if (page.Id == "work") _tabButtons[PomodoroTab] = item;
        else if (page.Tab < _tabButtons.Length && !page.Work) _tabButtons[page.Tab] = item;
        return item;
    }

    /// <summary>Избраната страница – натисната (медена); „Работа“ – разгъната, докато си в нея.</summary>
    private void UpdateNavSelection()
    {
        bool inWork = IsWorkPage;
        if (inWork) _lastWorkPage = _page is PagePlan or PageShutdown ? PageToday : _page;
        // Храна и Гардероб са в „Мечо“.
        string selected = _page is PageFood or PageWardrobe ? PageBear : _page is PagePlan or PageShutdown ? PageToday : _page;
        foreach (var (id, (item, _)) in _navItems)
            SetOn(item, id == selected || (id == "work" && inWork));
        _workChildren.Visibility = _workNavOpen ? Visibility.Visible : Visibility.Collapsed;
        if (_workBox != null) _workBox.Visibility = _workChildren.Visibility;
        if (_workArrow != null) _workArrow.Text = _workNavOpen ? "▾" : "▸";
        var info = Pages.FirstOrDefault(p => p.Id == _page);
        _where.Text = _page switch
        {
            PageFood => "Мечо  ›  Храна",
            PageWardrobe => "Мечо  ›  Гардероб",
            PagePlan => "Работа  ›  План за деня",
            PageShutdown => "Работа  ›  Край на деня",
            _ when info?.Work == true => "Работа  ›  " + info.Name,
            _ => info?.Name ?? "Мечо",
        };
    }

    /// <summary>Тесен прозорец – в лентата само иконки.</summary>
    private void UpdateNav()
    {
        double zoom = Zoom;
        double width = ActualWidth > 0 ? ActualWidth : Width;
        // Само иконки: свита е с дърпане – или прозорецът е тесен. Копчетата си остават същите
        // (същата височина и отстъпи) – само името се скрива и иконката застава в средата.
        bool compact = _pet.Save.NavCollapsed || (!double.IsNaN(width) && width / zoom < 760 * U);
        _navHost.Width = compact ? double.NaN : _pet.Save.NavWidth > 0 ? _pet.Save.NavWidth * U : double.NaN;
        if (compact == _navCompact) return;
        _navCompact = compact;
        foreach (var (id, (item, name)) in _navItems)
        {
            name.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            ToolTipService.SetIsEnabled(item, compact);
            if (item.Child is FrameworkElement row) row.HorizontalAlignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
        }
        if (_workArrow != null) _workArrow.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        if (_workBox != null)
        {
            _workBox.Margin = compact ? new Thickness(4 * U, 0, 0, 6 * U) : new Thickness(8 * U, 0, 0, 6 * U);
            _workBox.Padding = compact ? new Thickness(4 * U, 0, 0, 0) : new Thickness(6 * U, 0, 0, 0);
        }
    }

    // ───────────────────────── Размер и мащаб ─────────────────────────

    private Border _frame = null!;
    private bool _sizing;

    /// <summary>Мащабът на менюто (1 – 100%).</summary>
    private double Zoom => _pet.Save.UiZoom is >= 0.75 and <= 2 ? _pet.Save.UiZoom : 1;

    /// <summary>Началният размер (при мащаб 100%) – един и същ за всички страници (не подскача).</summary>
    private static Size DefaultSize => new(920 * U, 700 * U);

    /// <summary>Мащабът и запомненият размер (в единици при 100%).</summary>
    internal void ApplyZoom()
    {
        double z = Zoom;
        _frame.LayoutTransform = Math.Abs(z - 1) < 0.01 ? Transform.Identity : new ScaleTransform(z, z);
        RenderOptions.SetBitmapScalingMode(_frame, BitmapScalingMode.NearestNeighbor);
        var s = _pet.Save;
        double w = s.MenuW > 0 ? s.MenuW : DefaultSize.Width, h = s.MenuH > 0 ? s.MenuH : DefaultSize.Height;
        _sizing = true;
        MinWidth = 640 * U * z;
        MinHeight = 520 * U * z;
        Width = Math.Max(MinWidth, w * z);
        Height = Math.Max(MinHeight, h * z);
        _sizing = false;
        UpdateNav();
    }

    /// <summary>Да не е по-голямо от екрана.</summary>
    private void FitToArea(Rect area)
    {
        _sizing = true;
        if (Width > area.Width - 8) Width = Math.Max(MinWidth, area.Width - 8);
        if (Height > area.Height - 8) Height = Math.Max(MinHeight, area.Height - 8);
        _sizing = false;
    }

    private void RememberSize()
    {
        UpdateNav();
        if (_sizing || !IsVisible || ActualWidth <= 0) return;
        double z = Zoom;
        _pet.Save.MenuW = ActualWidth / z;
        _pet.Save.MenuH = ActualHeight / z;
    }

    /// <summary>„Още“ → размер: мащаб и обратно към началния размер.</summary>
    private void SetZoom(double zoom, bool resetSize = false)
    {
        _pet.Save.UiZoom = zoom;
        if (resetSize) _pet.Save.MenuW = _pet.Save.MenuH = 0;
        ApplyZoom();
        _pet.ZoomChanged();
        _pet.Persist();
    }

    // ───────────────────────── Мечо ─────────────────────────

    // ── Мечокът горе (в целия раздел „Мечо“: и в Храна, и в Гардероб) ──

    private Border _hero = null!;
    private Border _heroBack = null!;

    /// <summary>
    /// Портретът (изражението следва глада и умората), настроението, времето, ситостта и
    /// енергията. Стои над страниците „Мечо“, „Храна“ и „Гардероб“; в Храна и Гардероб вдясно
    /// е „← Мечо“.
    /// </summary>
    private Border BuildHero()
    {
        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.ColumnDefinitions.Add(new ColumnDefinition());
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

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
        _fullRow = (FrameworkElement)Bar("honey", "Ситост", out _fullBar, out _fullText);
        _energyRow = (FrameworkElement)Bar("energy", "Енергия", out _energyBar, out _energyText);
        foreach (var row in new[] { _fullRow, _energyRow })
        {
            row.MouseEnter += (_, _) => _pet.TutorialEvent("bars");   // обиколката: „посочи лентите“
            stats.Children.Add(row);
        }
        Grid.SetColumn(stats, 1);
        top.Children.Add(stats);

        _heroBack = Chip("", () => ShowPage(PageBear));
        _heroBack.Child = new TextBlock { Text = "← Мечо", FontWeight = FontWeights.Bold };
        _heroBack.VerticalAlignment = VerticalAlignment.Top;
        _heroBack.ToolTip = Tip("Обратно към Мечо");
        Grid.SetColumn(_heroBack, 2);
        top.Children.Add(_heroBack);

        return new Border
        {
            Child = top,
            Margin = new Thickness(0, 0, 0, 10 * U),
            Padding = new Thickness(0, 0, 0, 10 * U),
            // Плътна черта: тук се отрязва страницата, когато се превърта.
            BorderBrush = WoodDark,
            BorderThickness = new Thickness(0, 0, 0, P),
        };
    }

    /// <summary>Мечокът горе – само в раздела „Мечо“ (Мечо, Храна, Гардероб).</summary>
    private void UpdateHero()
    {
        if (_hero == null) return;
        bool bear = _page is PageBear or PageFood or PageWardrobe;
        _hero.Visibility = bear ? Visibility.Visible : Visibility.Collapsed;
        _heroBack.Visibility = _page is PageFood or PageWardrobe ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── Плочките в „Мечо“ ──

    /// <summary>Плочка: голяма иконка, име (и ред отдолу); „!“ в ъгъла, когато му трябва.</summary>
    private sealed class HomeTile
    {
        public Button Button = null!;
        public Border IconHost = null!;
        public TextBlock Label = null!;
        public TextBlock? Sub;
        public Border Badge = null!;
        public int Size;
        public string Key = "";
    }

    private HomeTile MakeTile(string icon, string label, Action click, int size = 3, string? sub = null, string? tip = null)
    {
        bool wide = size >= 4;
        var t = new HomeTile { Size = wide ? 3 : 2 };
        t.IconHost = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = wide ? new Thickness(2 * U, 0, 10 * U, 0) : new Thickness(0, 2 * U, 0, 4 * U),
        };
        t.Label = new TextBlock
        {
            FontWeight = FontWeights.Bold,
            TextAlignment = wide ? TextAlignment.Left : TextAlignment.Center,
            HorizontalAlignment = wide ? HorizontalAlignment.Left : HorizontalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            FontSize = wide ? Ui.Title : Ui.Body,
        };
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(t.Label);
        if (sub != null)
        {
            t.Sub = new TextBlock { Text = sub, Foreground = Muted, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 1 * U, 0, 0) };
            text.Children.Add(t.Sub);
        }
        Panel stack;
        if (wide)
        {
            var row = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(t.IconHost, Dock.Left);
            row.Children.Add(t.IconHost);
            row.Children.Add(text);
            stack = row;
        }
        else
        {
            var col = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            col.Children.Add(t.IconHost);
            col.Children.Add(text);
            stack = col;
        }
        t.Badge = new Border
        {
            Background = Bad,
            BorderBrush = Ink,
            BorderThickness = Flat,
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(5 * U, 0, 5 * U, 1 * U),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
            Child = new TextBlock { Text = "!", Foreground = Cream, FontWeight = FontWeights.Bold },
        };
        var grid = new Grid();
        grid.Children.Add(stack);
        grid.Children.Add(t.Badge);
        var b = Button("", click);
        b.Content = grid;
        b.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        b.VerticalContentAlignment = VerticalAlignment.Stretch;
        if (wide)
        {
            b.Padding = new Thickness(8 * U, 8 * U, 8 * U, 8 * U);
            b.HorizontalAlignment = HorizontalAlignment.Stretch;
            b.Margin = new Thickness(0, 0, 8 * U, 8 * U);
        }
        else
        {
            // Еднакви плочки в мрежата: една височина, ширината – равна част от реда.
            b.Padding = new Thickness(2 * U, 4 * U, 2 * U, 4 * U);
            b.Height = 70 * U;
            b.HorizontalAlignment = HorizontalAlignment.Stretch;
            t.Label.TextWrapping = TextWrapping.NoWrap;
            t.Label.TextTrimming = TextTrimming.CharacterEllipsis;
            b.Margin = new Thickness(0, 0, 6 * U, 6 * U);
        }
        if (tip != null) b.ToolTip = Tip(tip);
        t.Button = b;
        SetTile(t, label, icon);
        return t;
    }

    /// <summary>Сменя името и иконката (само ако са други – менюто се опреснява всяка секунда).</summary>
    private static void SetTile(HomeTile t, string label, string icon, string? sub = null)
    {
        if (sub != null && t.Sub != null && t.Sub.Text != sub) t.Sub.Text = sub;
        string key = icon + "\u0001" + label;
        if (t.Key == key) return;
        t.Key = key;
        t.Label.Text = label;
        t.IconHost.Child = Icon(icon, t.Size);
    }

    /// <summary>Натисната (включено/отворено) или „трябва му“ (медена, с „!“).</summary>
    private static void TileState(HomeTile t, bool on, bool need = false)
    {
        bool honey = need && !on;
        string state = on ? "on" : honey ? "honey" : "";
        if (t.Button.Tag as string != state)
        {
            SetOn(t.Button, on);
            if (honey) UiSkin.Set(t.Button, "button_honey");
            t.Button.Tag = state;
        }
        t.Badge.Visibility = need ? Visibility.Visible : Visibility.Collapsed;
    }

    private HomeTile _foodTile = null!, _wardrobeTile = null!, _sleepTile = null!, _petTile = null!, _walkTile = null!;
    private HomeTile _relaxTile = null!, _workTile = null!, _drawTile = null!, _quietTile = null!, _placeTile = null!, _stopTile = null!;
    private Grid _bigTiles = null!;

    /// <summary>
    /// „Мечо“: мечокът е горе (портрет, настроение, ленти); тук – две големи плочки Храна и
    /// Гардероб (единственото място, откъдето се отваря храната) и под тях всичко, което
    /// можете да правите заедно – плочки с иконки, без групи. Каквото му трябва сега (гладен,
    /// уморен) свети в мед и има „!“. Изборите (колко да спи, разходка…) се отварят под плочките.
    /// </summary>
    private UIElement BuildBearTab()
    {
        var p = new StackPanel { Margin = new Thickness(0, 4 * U, 0, 0) };

        _bigTiles = new Grid();
        _bigTiles.ColumnDefinitions.Add(new ColumnDefinition());
        _bigTiles.ColumnDefinitions.Add(new ColumnDefinition());
        _foodTile = MakeTile("food", "Храна", () => ShowPage(PageFood), 4, "Сготви нещо", "Храната: готви, хапва и пие");
        _wardrobeTile = MakeTile("wardrobe", "Гардероб", () => ShowPage(PageWardrobe), 4, "Облечи го", "Дрехите на Мечо");
        Grid.SetColumn(_wardrobeTile.Button, 1);
        _bigTiles.Children.Add(_foodTile.Button);
        _bigTiles.Children.Add(_wardrobeTile.Button);
        _tabButtons[FoodTab] = _foodTile.Button;          // обиколката: „отвори Храна“
        _tabButtons[WardrobeTab] = _wardrobeTile.Button;
        p.Children.Add(_bigTiles);

        // Приспи: избор колко да спи (вечер – и „Лягай за деня“). Спи ли – буди го.
        _sleepTile = MakeTile("moon", "Приспи", () =>
        {
            if (_pet.IsAsleep) _pet.WakeUp();
            else _sleepOpen = !_sleepOpen;
            _quietOpen = _walkOpen = _placeOpen = false;
            Refresh(false);
        });
        _petTile = MakeTile("heart", "Погали", _pet.Pet);
        // Разходи се: излиза от екрана за 10 мин, 30 мин, 1 ч или колкото напишеш. Навън ли е – „Повикай“.
        _walkTile = MakeTile("walk", "Разходка", () =>
        {
            if (_pet.OnWalk)
            {
                _pet.ReturnFromWalk(called: true);
                _walkOpen = false;
                Refresh(false);
                return;
            }
            _walkOpen = !_walkOpen;
            _sleepOpen = _quietOpen = _placeOpen = false;
            Refresh(false);
        });
        // „Почивай си“ / „Работи си“: мечокът сам сменя заниманията, докато не спре.
        _relaxTile = MakeTile("relax", "Почивай си", () =>
        {
            if (_pet.RelaxesRandomly) _pet.GetUp();
            else _pet.RelaxRandom();
            Refresh(true);
        }, tip: "Сам си избира какво да прави за почивка");
        _workTile = MakeTile("tasks", "Работи си", () =>
        {
            if (_pet.WorksRandomly) _pet.GetUp();
            else _pet.WorkRandom();
            Refresh(true);
        }, tip: "Работи до теб (бюро, книги…)");
        // Рисувай – само със статива.
        _drawTile = MakeTile("pencil", "Рисувай", () => { Hide(); _pet.OpenDrawing(); });
        // Тихо: за колко (30 мин, 1 ч, 2 ч). Тих ли е – пак говори.
        _quietTile = MakeTile("quiet", "Тихо", () =>
        {
            if (_pet.IsQuiet) _pet.SetQuietFor(0);
            else _quietOpen = !_quietOpen;
            _sleepOpen = _walkOpen = _placeOpen = false;
            Refresh(false);
        }, tip: "Да не говори известно време");
        // Неща на Мечо: уредите (печица, лампа, радио…) и мебелите, които слага и прибира.
        _placeTile = MakeTile("couch", "Неща", () =>
        {
            _placeOpen = !_placeOpen;
            _sleepOpen = _quietOpen = _walkOpen = false;
            Refresh(false);
        }, tip: "Уредите и мебелите на Мечо: сложи или прибери");
        // Спри: спира каквото прави (кътче, батут, разходка) – таймерите си текат.
        _stopTile = MakeTile("stop", "Спри", () =>
        {
            _pet.StopDoing();
            Refresh(true);
        }, tip: "Спира каквото прави сега");

        // SOS: мечокът дава нещо за мотивация (листът е motivation.md в GitHub).
        _sosTile = MakeTile("heart", "SOS", () =>
        {
            Hide();
            _pet.Sos();
        }, tip: "Нещо мило за кураж, когато ти е трудно");

        // Две карти: какво прави сега (и какво му се яде) и какво сме правили днес.
        _cards = new Grid { Margin = new Thickness(0, 0, 0, 2 * U) };
        _cards.ColumnDefinitions.Add(new ColumnDefinition());
        _cards.ColumnDefinitions.Add(new ColumnDefinition());
        _cards.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _cards.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _nowCard = InfoCard("Сега", "bear", BuildNowCard());
        _todayCard = InfoCard("Днес заедно", "sun", _todayBox);
        _cards.Children.Add(_nowCard);
        _cards.Children.Add(_todayCard);
        p.Children.Add(_cards);

        // Всичко, което може да прави – еднакви плочки в мрежа.
        p.Children.Add(new TextBlock { Text = "Заедно", Foreground = Muted, FontWeight = FontWeights.Bold, Margin = new Thickness(2 * U, 4 * U, 0, 4 * U) });
        _actionGrid = new UniformGrid { Columns = 5 };
        foreach (var t in new[] { _sleepTile, _petTile, _walkTile, _quietTile, _relaxTile, _workTile, _drawTile, _placeTile, _stopTile, _sosTile })
            _actionGrid.Children.Add(t.Button);
        p.Children.Add(_actionGrid);
        p.SizeChanged += (_, _) => LayoutBearTab(p.ActualWidth);

        // Изборите под плочките (отворен е най-много един).
        _sleepOptions.Margin = new Thickness(0);
        p.Children.Add(Pocket(_sleepOptions));
        _walkOptions.Margin = new Thickness(0);
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
        p.Children.Add(Pocket(_walkOptions));
        _quietOptions.Margin = new Thickness(0);
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
        p.Children.Add(Pocket(_quietOptions));
        _placeOptions.Margin = new Thickness(0);
        p.Children.Add(Pocket(_placeOptions));

        return TabScroll(p, 600 * U);
    }

    private HomeTile _sosTile = null!;
    private Grid _cards = null!;
    private Border _nowCard = null!, _todayCard = null!;
    private UniformGrid _actionGrid = null!;
    private readonly Border _nowIcon = new() { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 8, 0) };
    private readonly TextBlock _nowTitle = new() { FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _nowSub = new() { Foreground = Muted, TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _wishBox = new() { Margin = new Thickness(0, 8, 0, 0) };
    private readonly StackPanel _todayBox = new();
    private string _nowKey = "", _wishKey = "", _todayKey = "";
    private double _bearWidth;

    /// <summary>Карта с малко заглавие (иконка + име) и съдържание.</summary>
    private static Border InfoCard(string title, string icon, UIElement body)
    {
        var stack = new StackPanel();
        var head = IconText(icon, title, bold: true);
        head.Margin = new Thickness(0, 0, 0, 6 * U);
        stack.Children.Add(head);
        stack.Children.Add(body);
        return new Border
        {
            Background = Cream,
            BorderBrush = WoodDark,
            BorderThickness = Flat,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10 * U, 8 * U, 10 * U, 10 * U),
            Margin = new Thickness(0, 0, 8 * U, 8 * U),
            Child = stack,
        };
    }

    private UIElement BuildNowCard()
    {
        var stack = new StackPanel();
        var row = new DockPanel();
        DockPanel.SetDock(_nowIcon, Dock.Left);
        row.Children.Add(_nowIcon);
        var text = new StackPanel();
        text.Children.Add(_nowTitle);
        text.Children.Add(_nowSub);
        row.Children.Add(text);
        stack.Children.Add(row);
        stack.Children.Add(_wishBox);
        return stack;
    }

    /// <summary>Широко – картите една до друга и повече плочки на ред; тясно – една под друга.</summary>
    private void LayoutBearTab(double width)
    {
        if (width <= 0 || Math.Abs(width - _bearWidth) < 1) return;
        _bearWidth = width;
        bool wide = width >= 470 * U;
        Grid.SetColumn(_todayCard, wide ? 1 : 0);
        Grid.SetRow(_todayCard, wide ? 0 : 1);
        Grid.SetColumnSpan(_nowCard, wide ? 1 : 2);
        Grid.SetColumnSpan(_todayCard, wide ? 1 : 2);
        FitActionGrid();
    }

    /// <summary>Колко плочки на ред: колкото се събират (поне ~100 px всяка), редовете – по равно.</summary>
    private void FitActionGrid()
    {
        if (_actionGrid == null || _bearWidth <= 0) return;
        int n = _actionGrid.Children.Cast<UIElement>().Count(c => c.Visibility != Visibility.Collapsed);
        if (n == 0) return;
        int cols = Math.Clamp((int)(_bearWidth / (104 * U)), 3, n);
        int rows = (n + cols - 1) / cols;
        cols = (n + rows - 1) / rows;
        if (_actionGrid.Columns != cols) _actionGrid.Columns = cols;
    }

    private static string Capital(string text) => text.Length == 0 ? text : char.ToUpper(text[0]) + text[1..];

    /// <summary>„Сега“: какво прави и какво му се яде; „Днес заедно“: задачи, фокус, гушкане, хапнато.</summary>
    private void UpdateBearCards(SaveData s)
    {
        // Какво прави сега.
        string icon, title, sub;
        var fun = _pet.CurrentFun;
        if (_pet.IsAsleep) (icon, title, sub) = ("moon", "Спи", "Ззз… събуди го от „Събуди“, ако ти липсва.");
        else if (_pet.OnWalk) (icon, title, sub) = ("walk", "На разходка е", "Ще се върне сам – или го повикай.");
        else if (fun != null)
            (icon, title, sub) = (fun.Icon, "„" + Capital(fun.Doing) + "…“",
                Scenes.Work.Contains(fun) ? "Работи до теб" : "Почива си");
        else if (_pet.WorksRandomly) (icon, title, sub) = ("tasks", "Работи до теб", "Сам сменя заниманията.");
        else if (_pet.RelaxesRandomly) (icon, title, sub) = ("relax", "Почива си", "Сам си избира какво да прави.");
        else (icon, title, sub) = ("bear", "Мотае се наоколо", "Дай му занимание отдолу.");
        if (_pet.IsQuiet) sub += " Тихо е.";
        string key = icon + "|" + title + "|" + sub;
        if (key != _nowKey)
        {
            _nowKey = key;
            _nowIcon.Child = Icon(icon, 2);
            _nowTitle.Text = title;
            _nowSub.Text = sub;
        }

        // Какво му се яде – картинките на желанията; цъкнеш ли – Храна с желанията.
        var wishes = _pet.Wishes().Take(4).ToArray();
        string wk = string.Join(",", wishes.Select(f => f.Id));
        if (wk != _wishKey)
        {
            _wishKey = wk;
            _wishBox.Children.Clear();
            if (wishes.Length > 0)
            {
                _wishBox.Children.Add(new TextBlock { Text = "Иска му се", Foreground = Muted, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 3 * U) });
                var row = new WrapPanel();
                foreach (var f in wishes)
                {
                    var food = f;
                    var img = Pixel(_pet.FoodMenuImage(f), 2);
                    var chip = new Border
                    {
                        Background = Paper,
                        BorderBrush = WoodDark,
                        BorderThickness = Flat,
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(3 * U),
                        Margin = new Thickness(0, 0, 4 * U, 4 * U),
                        Child = img,
                        Cursor = System.Windows.Input.Cursors.Hand,
                        ToolTip = Tip(food.Name + " – сготви му"),
                    };
                    chip.MouseLeftButtonUp += (_, _) =>
                    {
                        _foodCat = Wish;
                        ShowPage(PageFood);
                    };
                    row.Children.Add(chip);
                }
                _wishBox.Children.Add(row);
            }
        }

        // Днес заедно.
        var rec = _pet.TodayRecord;
        var (done, all, _) = _pet.TodayLoad();
        int focus = (int)Math.Round(s.WorkMinutesToday);
        string tk = $"{done}/{all}|{focus}|{rec.Petted}|{string.Join(",", rec.Foods)}";
        if (tk == _todayKey) return;
        _todayKey = tk;
        _todayBox.Children.Clear();
        void Line(string ic, string text)
        {
            var line = IconText(ic, text);
            line.Margin = new Thickness(0, 0, 0, 5 * U);
            _todayBox.Children.Add(line);
        }
        Line("tasks", all == 0 ? "Няма задачи за днес" : $"Задачи: {done} от {all}");
        Line("hourglass", focus == 0 ? "Още не сме работили" : focus < 60 ? $"Работихме {focus} мин" : $"Работихме {focus / 60} ч {focus % 60:00} мин");
        Line("heart", rec.Petted == 0 ? "Още не е погален" : rec.Petted == 1 ? "Погален веднъж" : $"Погален {rec.Petted} пъти");
        var eaten = rec.Foods.Select(id => Kitchen.Foods.FirstOrDefault(f => f.Id == id)).Where(f => f != null).Cast<Food>().ToList();
        if (eaten.Count == 0) Line("food", "Още не е хапнал");
        else
        {
            var row = new WrapPanel { Margin = new Thickness(0, 0, 0, 2 * U) };
            var label = IconText("food", "Хапна:");
            label.Margin = new Thickness(0, 0, 6 * U, 0);
            label.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(label);
            foreach (var f in eaten.Take(8))
            {
                var img = Pixel(_pet.FoodMenuImage(f), 1);
                img.Margin = new Thickness(0, 0, 3 * U, 0);
                img.VerticalAlignment = VerticalAlignment.Center;
                img.ToolTip = Tip(f.Name);
                row.Children.Add(img);
            }
            _todayBox.Children.Add(row);
        }
    }

    /// <summary>Плочките в „Мечо“ според това какво прави и от какво има нужда.</summary>
    private void UpdateBearTiles(SaveData s)
    {
        bool hungry = s.Fullness < 35;
        var wish = _pet.Wishes().FirstOrDefault();
        SetTile(_foodTile, "Храна", "food", wish != null ? "Иска му се: " + wish.Name : "Сготви нещо");
        TileState(_foodTile, false, hungry && !_pet.IsAsleep && !_pet.OnWalk);
        bool wardrobe = DailyOffer.HasWardrobe(s);
        _wardrobeTile.Button.Visibility = wardrobe ? Visibility.Visible : Visibility.Collapsed;
        Grid.SetColumnSpan(_foodTile.Button, wardrobe ? 1 : 2);
        int worn = Clothes.Shelves.Count(slot => Clothes.Worn(s, slot) != null);
        bool cold = _pet.NeedsWarmerClothes;
        SetTile(_wardrobeTile, "Гардероб", "wardrobe", cold ? "Студено му е!" : worn == 0 ? "Облечи го" : "Облечени: " + worn);
        TileState(_wardrobeTile, false, cold && !_pet.OnWalk);

        if (_pet.IsAsleep) SetTile(_sleepTile, "Събуди", "sun");
        else SetTile(_sleepTile, "Приспи", "moon");
        _sleepTile.Button.IsEnabled = _pet.CanSleep || _pet.IsAsleep;
        bool tired = !_pet.IsAsleep && s.Energy < PetWindow.TiredBelow && _pet.CanSleep;
        TileState(_sleepTile, _sleepOpen, tired);

        SetTile(_walkTile, _pet.OnWalk ? "Повикай" : "Разходка", "walk");
        TileState(_walkTile, _walkOpen || _pet.OnWalk);
        TileState(_relaxTile, _pet.RelaxesRandomly);
        TileState(_workTile, _pet.WorksRandomly);
        _drawTile.Button.Visibility = Kitchen.Owns(s, Kitchen.Easel) ? Visibility.Visible : Visibility.Collapsed;
        if (_pet.IsQuiet) SetTile(_quietTile, "Говори", "bell");
        else SetTile(_quietTile, "Тихо", "quiet");
        TileState(_quietTile, _quietOpen);
        TileState(_placeTile, _placeOpen);
        TileState(_petTile, false);
        TileState(_stopTile, false);
        TileState(_sosTile, false);
        UpdateBearCards(s);
        FitActionGrid();
    }

    /// <summary>
    /// Подменю, което се отваря под бутоните (колко да спи, разходка, „Неща на Мечо“…):
    /// на по-тъмна подложка с рамка, като офертата на деня – отделя се от останалото.
    /// Вижда се, докато се вижда самото подменю.
    /// </summary>
    private Border Pocket(FrameworkElement child, bool follow = true)
    {
        var pocket = new Border
        {
            Background = Empty,
            BorderBrush = Ink,
            BorderThickness = Flat,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(6 * U, 6 * U, 6 * U, 2 * U),
            Margin = new Thickness(0, 2, 0, 6),
            Child = child,
            Visibility = child.Visibility,
        }.Skinned("pocket");
        // Само за подменютата, които се строят веднъж (иначе всяко прерисуване оставя слушател завинаги).
        if (follow)
            System.ComponentModel.DependencyPropertyDescriptor.FromProperty(VisibilityProperty, typeof(UIElement))
                .AddValueChanged(child, (_, _) => pocket.Visibility = child.Visibility);
        return pocket;
    }

    private readonly WrapPanel _sleepOptions = new(), _quietOptions = new(), _walkOptions = new();
    private bool _sleepOpen, _quietOpen, _walkOpen, _placeOpen;
    private readonly WrapPanel _placeOptions = new();

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
        // Първо уредите, после мебелите.
        Add(Kitchen.Heater, "Печица", s.HeaterPlaced);
        Add(Kitchen.Lamp, "Лампа", _pet.LampOn);
        Add(Kitchen.Radio, "Радио", _pet.RadioPlaced);
        Add(Kitchen.Feeder, "Хранилка", _pet.FeederPlaced);
        Add(Kitchen.PlantPot, "Растение", _pet.PlantOut);
        Add(Kitchen.Desk, "Бюро", _pet.FurniturePlaced(Kitchen.Desk));
        Add(Kitchen.Computer, "Компютър", _pet.FurniturePlaced(Kitchen.Computer));
        Add(Kitchen.Easel, "Статив", _pet.FurniturePlaced(Kitchen.Easel));
        Add(Kitchen.BoardGame, "Настолна игра", _pet.FurniturePlaced(Kitchen.BoardGame));
        Add(Kitchen.YarnBasket, "Кошница с прежда", _pet.FurniturePlaced(Kitchen.YarnBasket));
        Add(Kitchen.Couch, "Диван", _pet.FurniturePlaced(Kitchen.Couch));
        Add(Kitchen.YogaMat, "Постелка", _pet.FurniturePlaced(Kitchen.YogaMat));
        Add(Kitchen.Bed, "Легло", _pet.FurniturePlaced(Kitchen.Bed));
    }

    private void GoForWalk(double minutes)
    {
        _walkOpen = false;
        Hide();
        _pet.GoForWalk(minutes);
    }
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
    private bool _relaxOpen, _workOpen;

    private void BuildRelaxButtons()
    {
        var s = _pet.Save;
        var chosen = _pet.CurrentFun;
        bool random = _pet.RelaxesRandomly;
        bool working = _pet.WorksRandomly;
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
            // Деленце от пиксели на храната; съседните делят един контур (отрицателно поле).
            segments[i] = new Border
            {
                Width = 5 * P,
                Height = 6 * P,
                Margin = new Thickness(0, 0, i < Segments - 1 ? -P : 0, 0),
                BorderBrush = Ink,
                BorderThickness = Flat,
                Background = Empty,
                Child = Shine(),      // горният пиксел – по-светъл (като на храната)
            };
            if (UiSkin.Has("cell_empty"))
            {
                segments[i].Child = null;
                UiSkin.Set(segments[i], "cell_empty");
            }
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
        string cell = value >= 50 ? "cell_good" : value >= 25 ? "cell_okay" : "cell_bad";
        for (int i = 0; i < segments.Length; i++)
        {
            if (UiSkin.Get(segments[i]) != null) UiSkin.Set(segments[i], i < filled ? cell : "cell_empty");
            else segments[i].Background = i < filled ? colour : Empty;
        }
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
        return TabScroll(p, 560 * U);
    }

    private readonly WrapPanel _foodCats = new();

    private string _foodCat = "Всичко";
    private string? _foodKey;
    private const string Wish = "Желания";

    /// <summary>Плочки с храните: картинка, какво дава и бутон „Сготви“. Посочиш ли заключена, казва защо.</summary>
    private void BuildFoodTiles(bool force = false)
    {
        var s = _pet.Save;
        // Плочките (около 40, с барчета) струват десетки мс: строят се наново само
        // ако нещо, което се вижда в тях, се е сменило.
        var wishes = _pet.Wishes();
        string key = string.Join("|", _foodCat, s.Hazelnuts, (int)s.Fullness, (int)s.Energy, Kitchen.BerriesIn(s),
            string.Join(",", wishes.Select(f => f.Id)), s.Owned.Count, s.RecipePages,
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

            // Колко се готви: часовниче и общото време на всички уреди; ако са няколко –
            // при посочване пише всеки уред с неговото време.
            var steps = PetWindow.CookSteps(f).ToList();
            if (steps.Count > 0)
            {
                static string Dur(double secs) => secs % 60 == 0 ? $"{secs / 60:0} мин" : secs > 60 ? $"{Math.Floor(secs / 60):0} мин {secs % 60:0} с" : $"{secs:0} с";
                var time = IconText("clock", Dur(steps.Sum(x => x.Item2)));
                time.HorizontalAlignment = HorizontalAlignment.Center;
                time.Margin = new Thickness(0, 2, 0, 0);
                time.Opacity = 0.75;
                time.Background = Brushes.Transparent;   // посочването хваща целия ред
                if (steps.Count > 1)
                    time.ToolTip = Tip(string.Join("\n", steps.Select(x => $"{Kitchen.Find(x.Item1)?.Name ?? x.Item1}: {Dur(x.Item2)}")));
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
            // Сърчице горе вдясно в картинката: това му се яде.
            _foodTiles.Children.Add(MakeTile(WithShadow(_pet.FoodMenuImage(f)), f.Name, effects, action, locked, why, heart: wishes.Contains(f)));
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
                Width = 3 * P,
                Height = 4 * P,
                Margin = new Thickness(0, 0, P, 0),
                Background = i < s.RecipePages ? Honey : Empty,
                BorderBrush = Ink,
                BorderThickness = Flat,
                VerticalAlignment = VerticalAlignment.Center,
            }.Skinned(i < s.RecipePages ? "cell_honey" : "cell_empty"));
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
    /// <summary>
    /// Скролът на таб: стига горе и долу до самия ръб на хартията (над отстъпа ѝ), а отстъпът е
    /// вътре в него – така съдържанието се плъзга до ръба, вместо да се реже по-ниско и над
    /// него да остава ивица празна хартия.
    /// </summary>
    private static ScrollViewer TabScroll(FrameworkElement content, double maxHeight)
    {
        double top = 12 * U, bottom = 14 * U;     // отстъпът на хартията (виж рамката в конструктора)
        content.Margin = new Thickness(content.Margin.Left, content.Margin.Top + top, content.Margin.Right, content.Margin.Bottom + bottom);
        // Страницата запълва прозореца (размерът му се мени); maxHeight вече не се ползва.
        return new ScrollViewer
        {
            Content = content,
            Margin = new Thickness(0, -top, 0, -bottom),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
    }

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
        // Звена (като лентите за ситост/енергия): сегашното в цвят, колкото ще добави
        // храната – медени и примигват (да се вижда, че идват), останалото – празно.
        string dot = now >= 50 ? "dot_good" : now >= 25 ? "dot_okay" : "dot_bad";
        var cells = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        System.Windows.Media.Animation.DoubleAnimation? pulse = null;
        for (int i = 0; i < segments; i++)
        {
            bool gainCell = i >= have && i < will;
            var link = new Border
            {
                Width = 5 * P,
                Height = 5 * P,
                Margin = new Thickness(0, 0, i < segments - 1 ? -P : 0, 0),
                BorderBrush = Ink,
                BorderThickness = Flat,
                Background = i < have ? colour : gainCell ? Honey : Empty,
            };
            if (UiSkin.Has("dot_empty") && !gainCell) UiSkin.Set(link, i < have ? dot : "dot_empty");
            if (gainCell)
            {
                // Медено с бяло петънце горе – ясно различно от празното.
                link.Child = new Border { Background = Cream, Width = P, Height = P, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(P, P, 0, 0), Opacity = 0.8 };
                pulse ??= new System.Windows.Media.Animation.DoubleAnimation(1, 0.45, TimeSpan.FromMilliseconds(650))
                {
                    AutoReverse = true,
                    RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                };
                link.BeginAnimation(OpacityProperty, pulse);
            }
            cells.Children.Add(link);
        }
        row.Children.Add(cells);
        int gain = (int)Math.Round(Math.Min(100, after) - now);
        row.ToolTip = Tip(gain > 0 ? $"Сега {Math.Round(now)}/100 → {Math.Round(Math.Min(100, after))}/100 {what}" + (note != null ? "\n" + note : "") : (note ?? what));
        row.Background = Brushes.Transparent;
        return row;
    }

    private static Border Stat(string icon, string value, string what) => new()
    {
        Background = Cream,
        BorderBrush = WoodDark,
        BorderThickness = Flat,
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
        return TabScroll(p, 560 * U);
    }

    /// <summary>Самопроверка: строи магазина (целта и офертата).</summary>
    internal void SelfTestShop()
    {
        ShowPage(PageShop);
        _goalKey = "";
        BuildShopTiles();
        UpdateLayout();
    }

    /// <summary>Самопроверка: размерът – най-малкият (true), началният (false) или запомненият (null).</summary>
    internal void SelfTestSize(bool? small)
    {
        _sizing = true;
        if (small == true)
        {
            Width = MinWidth;
            Height = MinHeight;
        }
        else
        {
            Width = DefaultSize.Width * Zoom;
            Height = DefaultSize.Height * Zoom;
        }
        _sizing = false;
        if (small == null) ApplyZoom();
        UpdateLayout();
        UpdateNav();
    }

    internal void SelfTestPage(string page)
    {
        if (Pages.FirstOrDefault(p => p.Id == page) is { } info) _dirty[info.Tab] = true;
        ShowPage(page);
        RefreshNow(true);
    }

    /// <summary>Отваря магазина (например от гардероба или от заключена храна).</summary>
    private void OpenShop() => ShowPage(PageShop);

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
                    Width = 4 * P, Height = 4 * P, Margin = new Thickness(0, 0, i < Segments - 1 ? -P : 0, 0),
                    BorderBrush = Ink, BorderThickness = Flat,
                    Background = i < filled ? Good : Empty,
                }.Skinned(i < filled ? "cell_good" : "cell_empty"));
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
            BorderThickness = Flat,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8 * U, 6 * U, 8 * U, 8 * U),
            Child = box,
        }.Skinned("pocket"));
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
            ? Button("В гардероба", () => ShowPage(PageWardrobe), icon: "wardrobe")
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
        return Button("В гардероба", () => { _wardrobeSection = section; ShowPage(PageWardrobe); }, icon: "wardrobe");
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
        if (shop && owned) action = ToWardrobe(skin.Item, out why);
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
    private static Border MakeTile(BitmapSource art, string name, UIElement details, Button action, bool dim, string? why, bool owned = false, int artScale = 4, bool heart = false)
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
        else if (heart)
        {
            var badge = Icon("heart", 2);
            badge.HorizontalAlignment = HorizontalAlignment.Right;
            badge.VerticalAlignment = VerticalAlignment.Top;
            badge.Margin = new Thickness(0, 4, 6, 0);
            artGrid.Children.Add(badge);
        }
        var artBox = new Border
        {
            Background = TileArt,
            BorderBrush = WoodDark,
            BorderThickness = Flat,
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
        tile.Skinned("tile");
        artBox.Skinned("tile_art");
        tile.MouseEnter += (_, _) => UiSkin.SetState(tile, "hover");
        tile.MouseLeave += (_, _) => UiSkin.SetState(tile, null);
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

        // Мечокът с облеклото се вижда горе (общият портрет) – тук няма второ „огледало“.
        var mirror = PortraitBox(out _model, out _modelBg);
        mirror.Visibility = Visibility.Collapsed;
        top.Children.Add(mirror);

        var right = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
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
        return TabScroll(p, 560 * U);
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

        // Раздели (горе): Всичко · Облекло · Дом · Други; под тях – подраздели. Подраздел се
        // вижда чак когато има нещо в него (за безплатните – поне едно купено), раздел – щом има подраздели.
        var clothes = Clothes.Shelves
            .Where(slot => Clothes.All.Any(g => g.SlotId == slot.Id && !g.Hidden && Clothes.Owns(s, g)))
            .Select(slot => (Id: slot.Id, Name: slot.Name, Icon: slot.Icon)).ToList();
        if (Clothes.PajamaSets.Count(p => Clothes.Owns(s, p)) > 1) clothes.Add(("pajamas", "Пижами", "pajama"));
        var home = SkinItems(s).Select(id => (Id: id, Name: ((Kitchen.Find(id)?.Icon ?? "") + " " + (Kitchen.Find(id)?.Name ?? id)).Trim(), Icon: "")).ToList();
        var other = new List<(string Id, string Name, string Icon)>();
        if (Backdrops.All.Count(b => Backdrops.Owns(s, b)) > 1) other.Add(("wallpapers", "Тапети", "picture"));
        if (UiThemes.All.Count(t => UiThemes.Owns(s, t)) > 1) other.Add(("themes", "Цвят на меню", "menu"));
        var groups = new List<(string Id, string Name, string Icon, List<(string Id, string Name, string Icon)> Subs)>
        {
            ("clothes", "Облекло", "shirt", clothes),
            ("home", "Дом", "couch", home),
            ("other", "Други", "picture", other),
        };
        groups.RemoveAll(g => g.Subs.Count == 0);
        if (groups.Count == 0)
        {
            _wardrobeShelves.Children.Add(MutedText("Гардеробът е празен. Нови неща има в офертата на деня."));
            return;
        }
        // Старите имена (от „В гардероба“ в офертата): „furniture“ – първата мебел.
        if (_wardrobeSection == "furniture") _wardrobeSection = home.Count > 0 ? home[0].Id : AllWardrobe;
        if (_wardrobeSection == "") _wardrobeSection = groups[0].Subs[0].Id;
        var group = groups.FirstOrDefault(g => g.Subs.Any(x => x.Id == _wardrobeSection));
        if (_wardrobeSection != AllWardrobe && group.Subs == null) _wardrobeSection = AllWardrobe;

        var bar = new WrapPanel { Margin = new Thickness(0, 0, 0, 8 * U) };
        void AddChip(WrapPanel panel, string label, string icon, bool on, Action pick)
        {
            // Без име на иконка: емоджито в началото на текста става пикселната иконка (като навсякъде, Chip).
            var chip = Chip(icon.Length > 0 ? "" : label, () => { pick(); BuildWardrobe(); });
            if (icon.Length > 0) chip.Child = IconText(icon, label);
            SetOn(chip, on);
            chip.Margin = new Thickness(0, 0, 5, 5);
            panel.Children.Add(chip);
        }
        AddChip(bar, "Всичко", "wardrobe", _wardrobeSection == AllWardrobe, () => _wardrobeSection = AllWardrobe);
        foreach (var g in groups)
        {
            var first = g.Subs[0].Id;
            AddChip(bar, g.Name, g.Icon, group.Id == g.Id, () => _wardrobeSection = first);
        }
        _wardrobeShelves.Children.Add(bar);

        // Съдържанието е на по-тъмна подложка (като офертата и подменютата в „Мечо“); отгоре – подразделите.
        var grid = new UniformGrid { Columns = 3 };
        var shelf = new StackPanel();
        if (group.Subs is { } subs)
        {
            var sub = new WrapPanel { Margin = new Thickness(0, 0, 0, 8 * U) };
            foreach (var x in subs)
            {
                string id = x.Id;
                AddChip(sub, x.Name, x.Icon, id == _wardrobeSection, () => _wardrobeSection = id);
            }
            shelf.Children.Add(sub);
        }
        shelf.Children.Add(grid);
        _wardrobeShelves.Children.Add(Pocket(shelf, follow: false));

        // „Всичко“: всичко, което имаш, подред; иначе – само избраният подраздел.
        var shown = _wardrobeSection == AllWardrobe ? groups.SelectMany(g => g.Subs).Select(x => x.Id).ToList() : new List<string> { _wardrobeSection };
        string empty = "";
        foreach (var id in shown) empty = WardrobeTiles(id, grid);
        if (_wardrobeSection == AllWardrobe) empty = "Гардеробът е празен. Нови неща има в офертата на деня.";
        if (grid.Children.Count == 0) shelf.Children.Add(MutedText(empty));
        var more = Button("Офертата на деня", () => OpenShop(), icon: "shop");
        more.HorizontalAlignment = HorizontalAlignment.Center;
        more.Margin = new Thickness(0, 10 * U, 0, 0);
        _wardrobeShelves.Children.Add(more);
    }

    /// <summary>Гардероб → „Всичко“ (горе).</summary>
    private const string AllWardrobe = "all";

    /// <summary>Плочките на един подраздел на гардероба (дрехи от вид, пижами, тапети, цвят на меню, скинове на мебел). Връща текста, ако е празен.</summary>
    private string WardrobeTiles(string id, UniformGrid grid)
    {
        var s = _pet.Save;
        if (id == "wallpapers")
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
            return "Нови тапети има в офертата на деня.";
        }
        if (id == "themes")
        {
            // Скиновете на менюто идват от офертата на деня и от подаръците.
            foreach (var t in UiThemes.All.Where(t => UiThemes.Owns(s, t))) grid.Children.Add(ThemeTile(t, shop: false));
            return "";
        }
        if (id == "pajamas")
        {
            foreach (var p in Clothes.PajamaSets.Where(p => Clothes.Owns(s, p))) grid.Children.Add(PajamaTile(p, shop: false));
            return "Нови пижами има в офертата на деня.";
        }
        if (Furniture.ByItem.ContainsKey(id))
        {
            foreach (var skin in Furniture.SkinsFor(id).Where(k => Furniture.Owns(s, k))) grid.Children.Add(SkinTile(skin, shop: false));
            return "Други видове има в офертата на деня.";
        }
        var slot = Clothes.Shelves.FirstOrDefault(x => x.Id == id);
        if (slot == null) return "";
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
        return $"Още нямаш {slot.Name.ToLowerInvariant()}.";
    }

    private string _wardrobeSection = "";   // празно – първият подраздел на първия раздел („Всичко“ е тежко – само ако го избереш)
    private readonly StackPanel _wardrobeBody = new();
    private readonly StackPanel _wardrobeLock = new();

    /// <summary>Самопроверка: гардеробът с дрехите и фоновете.</summary>
    internal void SelfTestWardrobe()
    {
        ShowPage(PageWardrobe);
        BuildWardrobe();
        UpdateLayout();
    }

    // ───────────────────────── Задачи ─────────────────────────

    /// <summary>
    /// „Работа“: отгоре три копчета – Мечо-доро | Таймер | Задачи – и под тях само
    /// избраната част, на цялата ширина (вместо три неща, натъпкани едно до друго).
    /// </summary>
    /// <summary>„Най-нови / Най-стари“ – подредбата на списъка (закачените са винаги горе).</summary>
    /// <summary>Подредбата на бележките: само стрелка (▲ най-старите горе, ▼ най-новите горе) – да се събира на реда.</summary>
    private Border SortChip(Func<bool> oldestFirst, Action<bool> set)
    {
        Border chip = null!;
        chip = Chip(oldestFirst() ? "▲" : "▼", () =>
        {
            set(!oldestFirst());
            SetChip(chip, oldestFirst() ? "▲" : "▼");
        });
        chip.ToolTip = Tip("Подредба: най-новите или най-старите горе (закачените са винаги най-отгоре)");
        chip.HorizontalAlignment = HorizontalAlignment.Right;
        chip.Margin = new Thickness(0, 0, 0, 6);
        return chip;
    }

    /// <summary>
    /// Долният ред при задачите и бележките: ➕ Нова, 📌 Табло и История – еднакви, един до друг.
    /// </summary>
    private UniformGrid BottomBar(Action onNew, out Border newButton, Action onBoard, out Border board, string boardTip, Action onHistory, out Border history)
    {
        var bar = new UniformGrid { Rows = 1, Margin = new Thickness(0, 10, 0, 0) };
        newButton = Chip("", onNew);
        newButton.Child = IconText("plus", "Нова", bold: true);
        ((FrameworkElement)newButton.Child).HorizontalAlignment = HorizontalAlignment.Center;
        board = Chip("", onBoard);
        board.Child = IconText("pin", "Табло", bold: true);
        ((FrameworkElement)board.Child).HorizontalAlignment = HorizontalAlignment.Center;
        board.ToolTip = Tip(boardTip);
        history = Chip("", onHistory);
        history.Child = IconText("history", "История", bold: true);
        ((FrameworkElement)history.Child).HorizontalAlignment = HorizontalAlignment.Center;
        history.ToolTip = Tip("Какво е минало оттук");
        board.Margin = new Thickness(6, 0, 0, 0);
        history.Margin = new Thickness(6, 0, 0, 0);
        foreach (var b in new FrameworkElement[] { newButton, board, history }) b.VerticalAlignment = VerticalAlignment.Stretch;
        bar.Children.Add(newButton);
        bar.Children.Add(board);
        bar.Children.Add(history);
        return bar;
    }

    private Border _pinNotes = null!;

    /// <summary>Четири места две по две (празно място – null) – копчетата до задача/бележка.</summary>
    private static UniformGrid TwoByTwo(FrameworkElement? a, FrameworkElement? b, FrameworkElement? c, FrameworkElement? d)
    {
        var grid = new UniformGrid { Rows = 2, Columns = 2, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(6, 0, 0, 0) };
        foreach (var e in new[] { a, b, c, d })
        {
            if (e == null)
            {
                grid.Children.Add(new Border());
                continue;
            }
            e.Margin = new Thickness(2);
            grid.Children.Add(e);
        }
        Grid.SetColumn(grid, 1);
        return grid;
    }

    // ───────────────────────── Бележки ─────────────────────────

    private UIElement BuildNotesTab()
    {
        var root = new StackPanel();
        // Отгоре: Всички | Закачени | Напомняния (отдолу – ➕ Нова, Табло, История, като при задачите).
        // Трите на един ред, еднакво широки (подредбата е до тях – само стрелка).
        var top = _notesTop;
        top.Margin = new Thickness(0, 0, 4, 0);
        _notesChip = Chip("📝 Всички", () => { _notesView = 0; BuildNoteList(); });
        _pinnedChip = Chip("📌 Закачени", () => { _notesView = 1; BuildNoteList(); });
        _remindersChip = Chip("⏰ Напомняния", () => { _notesView = 2; BuildNoteList(); });
        foreach (var c in new[] { _notesChip, _pinnedChip, _remindersChip })
        {
            c.Margin = new Thickness(0, 0, 4, 4);
            c.Padding = new Thickness(4, 3, 4, 4);
            top.Children.Add(c);
        }

        // Нова бележка (таб „➕ Нова“, отделно от списъка): име (удебелено) и съдържание.
        var p = _noteEditor;
        _noteTitle = NewTextInput();
        _noteTitle.FontWeight = FontWeights.Bold;
        _noteTitle.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) AddNote();
                else _noteInput.Focus();
                e.Handled = true;
            }
        };
        p.Children.Add(WithHint(_noteTitle, "Име"));
        _noteInput = NewTextInput();
        _noteInput.AcceptsReturn = true;
        _noteInput.TextWrapping = TextWrapping.Wrap;
        _noteInput.MinHeight = 60 * U;
        _noteInput.MaxHeight = 160 * U;
        _noteInput.Margin = new Thickness(0, 6, 0, 0);
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

        // 📌 Закачи – в таблото ли да е (може да се махне и после); ⏰ – напомни ми.
        _newPinned = Chip("📌 Закачи", () => { _newNotePinned = !_newNotePinned; SetOn(_newPinned, _newNotePinned); });
        _newPinned.ToolTip = Tip("Да се вижда ли в закаченото табло");
        _newPinned.Margin = new Thickness(0, 0, 8, 0);
        _newPinned.VerticalAlignment = VerticalAlignment.Center;
        SetOn(_newPinned, _newNotePinned);
        _remToggle = Chip("⏰ Напомни", () =>
        {
            _remOn = !_remOn || _remFor != null;
            _remFor = null;
            ShowReminderPanel();
        });
        _remToggle.ToolTip = Tip("Мечокът ще ти напомни за тази бележка");
        _remToggle.VerticalAlignment = VerticalAlignment.Center;
        _remToggle.Margin = new Thickness(0, 0, 8, 0);
        p.Children.Add(new WrapPanel { Margin = new Thickness(0, 8, 0, 0), Children = { _newPinned, _remToggle } });
        root.Children.Add(p);
        root.Children.Add(BuildReminderPanel());
        // Под Закачи/Напомни (и избора кога): Добави и Отказ – еднакво големи.
        _addNoteButton = Button("Добави", AddNote, primary: true, icon: "plus");
        var cancel = Button("Отказ", () => ShowNewNote(false), icon: "close");
        _addNoteButton.Margin = new Thickness(0, 0, 3, 0);   // еднакво големи
        cancel.Margin = new Thickness(3, 0, 0, 0);
        _noteActions.Children.Add(_addNoteButton);
        _noteActions.Children.Add(cancel);
        _noteActions.Rows = 1;
        _noteActions.Margin = new Thickness(0, 10, 0, 0);
        root.Children.Add(_noteActions);
        // Над списъка, на един ред: Всички | Закачени | Напомняния – и вдясно подредбата.
        _noteSort = SortChip(() => _pet.Save.NotesOldestFirst, on => _pet.SetNotesOldestFirst(on));
        _noteSort.VerticalAlignment = VerticalAlignment.Top;
        DockPanel.SetDock(_noteSort, Dock.Right);
        _notesRow.Children.Add(_noteSort);
        _notesRow.Children.Add(top);
        root.Children.Add(_notesRow);

        root.Children.Add(new ScrollViewer
        {
            Content = _noteList,
            MaxHeight = 480 * U,
            Margin = new Thickness(0, 10, 0, 0),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        });
        root.Children.Add(_notesBottom = BottomBar(() => ShowNewNote(_notesView != 3), out _newNoteButton,
            () => _pet.SetPinned(PinWindow.Notes, !_pet.IsPinned(PinWindow.Notes)), out _pinNotes, "Закачените бележки в малко табло, което стои на екрана",
            () => { _notesView = _notesView == 4 ? 0 : 4; BuildNoteList(); }, out _noteHistoryButton));
        return root;
    }

    private Border _newNoteButton = null!, _noteHistoryButton = null!, _noteSort = null!;
    private readonly UniformGrid _notesTop = new() { Rows = 1 };
    private readonly DockPanel _notesRow = new() { Margin = new Thickness(0, 10, 0, 0) };
    private readonly UniformGrid _noteActions = new();
    private UniformGrid _notesBottom = null!;
    private Button _addNoteButton = null!;
    private NoteItem? _editNote;   // поправя се (✏) – иначе е нова

    /// <summary>
    /// „➕ Нова“ (или ✏ на бележка): само полетата – без табовете горе, списъка и долните
    /// бутони; след „Добави“/„Запази“ или „Отказ“ – пак бележките.
    /// </summary>
    internal void ShowNewNote(bool on, NoteItem? edit = null)
    {
        _notesView = on ? 3 : 0;
        _editNote = on ? edit : null;
        _remFor = null;
        _remOn = false;
        _noteTitle.Text = edit?.Title ?? "";
        _noteInput.Text = edit?.Text ?? "";
        _newNotePinned = edit?.Pinned ?? true;
        SetOn(_newPinned, _newNotePinned);
        SetButton(_addNoteButton, edit != null ? "Запази" : "Добави", edit != null ? "check" : "plus");
        BuildNoteList();
        if (on && IsVisible) Dispatcher.BeginInvoke(() => _noteTitle.Focus(), DispatcherPriority.Input);
    }

    private readonly StackPanel _noteEditor = new(), _remList = new();
    private TextBox _noteTitle = null!;
    private Border _notesChip = null!, _pinnedChip = null!, _remindersChip = null!, _newPinned = null!;
    private int _notesView;                  // 0 – всички, 1 – закачените, 2 – напомнянията, 3 – нова бележка, 4 – история (изтритите)
    private bool _newNotePinned = true;

    private void AddNote()
    {
        if (string.IsNullOrWhiteSpace(_noteInput.Text) && string.IsNullOrWhiteSpace(_noteTitle.Text)) return;
        DateTime when = default;
        bool remind = _remOn && _remFor == null;
        if (remind && !TryReminderWhen(out when)) return;   // часът не става – не записва още
        var note = _editNote;
        if (note != null) _pet.EditNote(note, _noteTitle.Text, _noteInput.Text, _newNotePinned);
        else note = _pet.AddNote(_noteTitle.Text, _noteInput.Text, _newNotePinned, quiet: remind);
        if (note != null && remind) _pet.SetNoteReminder(note, when, _remDay ? _remRepeat : "none");
        ShowNewNote(false);   // записана – обратно към бележките
    }

    private void BuildNoteList()
    {
        SetOn(_notesChip, _notesView == 0);
        SetOn(_pinnedChip, _notesView == 1);
        SetOn(_remindersChip, _notesView == 2);
        SetOn(_newNoteButton, _notesView == 3);
        SetOn(_noteHistoryButton, _notesView == 4);
        SetOn(_pinNotes, _pet.IsPinned(PinWindow.Notes));
        bool editing = _notesView == 3;
        _noteEditor.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
        _noteActions.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
        _notesRow.Visibility = editing ? Visibility.Collapsed : Visibility.Visible;
        _notesBottom.Visibility = editing ? Visibility.Collapsed : Visibility.Visible;
        _noteSort.Visibility = _notesView is 0 or 1 ? Visibility.Visible : Visibility.Hidden;   // Hidden – редът не подскача
        // Бележката, за която се слага напомняне, е изтрита – затваря.
        if (_remFor != null && !_pet.Save.Notes.Contains(_remFor))
        {
            _remFor = null;
            _remOn = false;
        }
        ShowReminderPanel();
        _noteList.Children.Clear();
        if (_notesView == 3) return;   // новата бележка е сама, без списъка
        if (_notesView == 4)
        {
            BuildNoteHistory();
            return;
        }
        if (_notesView == 2)
        {
            BuildReminderList();
            _noteList.Children.Add(_remList);
            return;
        }
        // Закачените (в таблото) – най-горе, после по подредбата.
        var notes = _pet.OrderedNotes(pinnedOnly: _notesView == 1);
        if (notes.Count == 0)
        {
            _noteList.Children.Add(MutedText(_notesView == 1 ? "Няма закачени бележки. Сложи 📌 на бележка и тя ще е в таблото." : "Тефтерът е празен."));
            return;
        }
        foreach (var note in notes)
        {
            var n = note;
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var body = new StackPanel();
            if (n.Title.Length > 0)
                body.Children.Add(new TextBlock { Text = n.Title, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, Foreground = Ink });
            // TextBox само за четене, за да може текстът да се маркира и копира.
            if (n.Text.Length > 0) body.Children.Add(new TextBox
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
            var rem = _pet.ReminderFor(n);
            if (rem != null)
            {
                var meta = RichText("⏰ " + ReminderMeta(rem), bold: true);
                meta.Foreground = rem.Due <= DateTime.Now ? Bad : Ink;
                meta.Margin = new Thickness(0, 2, 0, 0);
                meta.HorizontalAlignment = HorizontalAlignment.Left;
                body.Children.Add(meta);
            }
            grid.Children.Add(body);

            // Две по две: 📌 табло, ⏰ напомняне / ✏ редактирай, ✕ изтрий.
            var pin = Chip("📌", () => _pet.SetNotePinned(n, !n.Pinned));
            pin.ToolTip = Tip(n.Pinned ? "В таблото е – цъкни, за да я махнеш оттам" : "Сложи я в таблото");
            SetOn(pin, n.Pinned);
            var remind = Chip("⏰", () => EditReminderFor(n));
            remind.ToolTip = Tip(rem == null ? "Напомни ми за тази бележка" : "Смени напомнянето");
            SetOn(remind, rem != null || _remFor == n);
            var edit = Chip("✏", () => ShowNewNote(true, n));
            edit.ToolTip = Tip("Редактирай");
            var del = Chip("✕", () => _pet.DeleteNote(n));
            del.ToolTip = Tip("Изтрий");
            grid.Children.Add(TwoByTwo(pin, remind, edit, del));

            // Датата – долу вдясно, бледо.
            var card = new StackPanel();
            card.Children.Add(grid);
            card.Children.Add(new TextBlock
            {
                Text = n.Created.ToString("d.MM.yyyy, HH:mm"),
                Foreground = Muted,
                Opacity = 0.6,
                FontSize = Body * 0.85,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 2, 0, 0),
            });
            _noteList.Children.Add(Card(card, false));
        }
    }

    /// <summary>Историята: изтритите бележки (най-новите горе) – ↺ връща, ✕ трие завинаги.</summary>
    private void BuildNoteHistory()
    {
        var gone = _pet.Save.NoteHistory.OrderByDescending(n => n.Deleted).ToList();
        if (gone.Count == 0)
        {
            _noteList.Children.Add(MutedText("Няма изтрити бележки. Изтриеш ли някоя, ще е тук и ще можеш да я върнеш."));
            return;
        }
        foreach (var note in gone)
        {
            var n = note;
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var body = new StackPanel();
            if (n.Title.Length > 0) body.Children.Add(new TextBlock { Text = n.Title, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, Foreground = Ink });
            if (n.Text.Length > 0) body.Children.Add(new TextBlock { Text = Shorten(n.Text, 120), TextWrapping = TextWrapping.Wrap, Foreground = Ink });
            body.Children.Add(new TextBlock { Text = "Изтрита " + (n.Deleted ?? n.Created).ToString("d.MM.yyyy, HH:mm"), Foreground = Muted, Margin = new Thickness(0, 2, 0, 0) });
            grid.Children.Add(body);
            var buttons = new StackPanel { VerticalAlignment = VerticalAlignment.Top };
            var back = Chip("🔄", () => _pet.RestoreNote(n));
            back.ToolTip = Tip("Върни я при бележките");
            var del = Chip("✕", () => _pet.PurgeNote(n));
            del.ToolTip = Tip("Изтрий завинаги");
            foreach (var b in new[] { back, del })
            {
                b.Margin = new Thickness(4, 0, 0, 4);
                buttons.Children.Add(b);
            }
            Grid.SetColumn(buttons, 1);
            grid.Children.Add(buttons);
            _noteList.Children.Add(Card(grid, true));
        }
    }

    private static string ReminderMeta(ReminderItem r) =>
        PetWindow.WhenText(r.Due) + (r.Repeat != "none" ? " · " + PetWindow.RepeatName(r.Repeat).ToLowerInvariant() : "");

    /// <summary>Всички напомняния (най-близките горе): кога, повтаряне, бележката; ⏰ смени, ✓ готово, ✕ махни.</summary>
    private void BuildReminderList()
    {
        _remList.Children.Clear();
        var list = _pet.Save.Reminders.OrderBy(r => r.Due).ToList();
        if (list.Count == 0)
        {
            _remList.Children.Add(MutedText("Няма напомняния. Слагат се към бележка – с „⏰ Напомни“ или ⏰ до бележката."));
            return;
        }
        foreach (var reminder in list)
        {
            var r = reminder;
            var note = _pet.Save.Notes.FirstOrDefault(n => n.Id == r.NoteId);
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var body = new StackPanel();
            body.Children.Add(new TextBlock { Text = note?.Label ?? r.Text, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, Foreground = Ink });
            if (note != null && note.Title.Length > 0 && note.Text.Length > 0)
                body.Children.Add(new TextBlock { Text = Shorten(note.Text, 80), TextWrapping = TextWrapping.Wrap, Foreground = Muted });
            var meta = RichText("⏰ " + ReminderMeta(r));
            meta.Foreground = r.Due <= DateTime.Now ? Bad : Ink;
            meta.HorizontalAlignment = HorizontalAlignment.Left;
            meta.Margin = new Thickness(0, 2, 0, 0);
            body.Children.Add(meta);
            grid.Children.Add(body);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
            if (note != null)
            {
                var edit = Chip("⏰", () => { _notesView = 0; EditReminderFor(note); });
                edit.ToolTip = Tip("Смени кога");
                edit.Margin = new Thickness(0, 0, 4, 0);
                buttons.Children.Add(edit);
            }
            var done = Chip("✓", () => _pet.CompleteReminder(r));
            done.ToolTip = Tip(r.Repeat == "none" ? "Готово (маха се)" : "Готово (следващия път)");
            done.Margin = new Thickness(0, 0, 4, 0);
            buttons.Children.Add(done);
            var del = Chip("✕", () => _pet.DeleteReminder(r));
            del.ToolTip = Tip("Махни напомнянето (бележката остава)");
            buttons.Children.Add(del);
            Grid.SetColumn(buttons, 1);
            grid.Children.Add(buttons);
            _remList.Children.Add(Card(grid, false));
        }
    }

    // ── Напомняне към бележка ──

    private Border _remToggle = null!, _remOtherDay = null!;
    private readonly StackPanel _remPanel = new(), _remAfterPane = new(), _remDayPane = new(), _remEditButtons = new();
    private TextBox _remMinutes = null!;
    private readonly WrapPanel _remModes = new(), _remAfter = new(), _remDays = new(), _remTimes = new(), _remRepeats = new();
    private TextBlock _remForLabel = null!, _remWhen = null!, _remHourText = null!, _remMinuteText = null!;
    private Calendar _remCalendar = null!;
    private Button _remRemove = null!;
    private bool _remOn, _remDay;            // тикчето е сложено; „Ден и час“ (иначе „След…“)
    private NoteItem? _remFor;               // напомняне за вече записана бележка (иначе – за новата)
    private double _remAfterMin = 30;
    private DateTime _remDate = DateTime.Today;
    private int _remHour = Math.Min(23, DateTime.Now.Hour + 1), _remMinute;
    private string _remRepeat = "none";
    private static readonly (string Label, double Minutes)[] RemAfterChoices =
        { ("5 мин", 5), ("10 мин", 10), ("15 мин", 15), ("30 мин", 30), ("1 ч", 60), ("2 ч", 120), ("3 ч", 180) };

    /// <summary>Кога да напомни: „След…“ (5 мин… 3 ч или свои минути) или „Ден и час“ (днес, утре или от календарчето; часовник; повторение).</summary>
    private UIElement BuildReminderPanel()
    {
        var p = _remPanel;
        p.Visibility = Visibility.Collapsed;
        p.Margin = new Thickness(0, 6, 0, 0);
        _remForLabel = new TextBlock { Foreground = Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) };
        p.Children.Add(_remForLabel);
        p.Children.Add(_remModes);

        // След…: готови и свои минути.
        _remAfterPane.Children.Add(_remAfter);
        var minutesRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        minutesRow.Children.Add(new TextBlock { Text = "или след", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        _remMinutes = NewTextInput();
        _remMinutes.Width = 60 * U;
        _remMinutes.TextChanged += (_, _) =>
        {
            if (double.TryParse(_remMinutes.Text.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var m) && m > 0 && m <= 7 * 24 * 60)
            {
                _remAfterMin = m;
                BuildReminderPicker();
            }
            else UpdateReminderWhen();
        };
        minutesRow.Children.Add(WithHint(_remMinutes, "мин"));
        minutesRow.Children.Add(new TextBlock { Text = "минути", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) });
        _remAfterPane.Children.Add(minutesRow);
        p.Children.Add(_remAfterPane);

        // Ден: днес, утре или друг – от календарчето.
        var d = _remDayPane;
        d.Children.Add(Label("Ден:"));
        _remOtherDay = Chip("📅 Друг ден", () =>
        {
            _remCalendar.Visibility = _remCalendar.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        });
        d.Children.Add(_remDays);
        _remCalendar = new Calendar
        {
            DisplayDateStart = DateTime.Today,
            FirstDayOfWeek = DayOfWeek.Monday,
            HorizontalAlignment = HorizontalAlignment.Left,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 2, 0, 4),
            Background = Paper,
            BorderBrush = Ink,
            Language = System.Windows.Markup.XmlLanguage.GetLanguage("bg-BG"),
        };
        _remCalendar.SelectedDatesChanged += (_, _) =>
        {
            if (_remCalendar.SelectedDate is not { } day) return;
            _remDate = day.Date;
            _remCalendar.Visibility = Visibility.Collapsed;
            BuildReminderPicker();
        };
        // Календарът държи мишката след избор (следващото цъкане иначе се губи).
        _remCalendar.PreviewMouseUp += (_, _) =>
        {
            if (Mouse.Captured is CalendarItem) Mouse.Capture(null);
        };
        d.Children.Add(_remCalendar);

        // Час: часовник – ▲/▼ (или колелцето на мишката) за часа и минутите; и готови часове.
        d.Children.Add(Label("Час:"));
        var clock = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        _remHourText = ClockDigit(clock, h => SetReminderTime(_remHour + h, _remMinute));
        clock.Children.Add(new TextBlock { Text = ":", FontSize = Body * 1.6, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 4, 0) });
        _remMinuteText = ClockDigit(clock, m => SetReminderTime(_remHour, _remMinute + m * 5));
        var face = new Border
        {
            Child = clock,
            Background = PaperDark,
            BorderBrush = Ink,
            BorderThickness = Flat,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 2, 8, 2),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 6),
        };
        d.Children.Add(face);
        foreach (var t in new[] { (9, 0), (12, 0), (15, 0), (18, 0), (20, 0), (22, 0) })
        {
            var (h, m) = t;
            var chip = Chip($"{h}:{m:00}", () => SetReminderTime(h, m));
            chip.Margin = new Thickness(0, 0, 6, 4);
            _remTimes.Children.Add(chip);
        }
        d.Children.Add(_remTimes);
        d.Children.Add(Label("Повтаря се:"));
        d.Children.Add(_remRepeats);
        p.Children.Add(d);

        _remWhen = RichText("", bold: true);
        _remWhen.HorizontalAlignment = HorizontalAlignment.Left;
        _remWhen.Margin = new Thickness(0, 6, 0, 4);
        p.Children.Add(_remWhen);

        // За вече записана бележка: Напомни ми / Махни / Отказ (за новата – „Запиши“ горе).
        _remEditButtons.Orientation = Orientation.Horizontal;
        var apply = Button("Напомни ми", ApplyReminder, primary: true, icon: "clock");
        apply.Margin = new Thickness(0, 0, 8, 0);
        _remRemove = Button("Махни", () =>
        {
            if (_remFor != null) _pet.ClearNoteReminder(_remFor);
            CloseReminderPanel();
        });
        _remRemove.Margin = new Thickness(0, 0, 8, 0);
        _remEditButtons.Children.Add(apply);
        _remEditButtons.Children.Add(_remRemove);
        _remEditButtons.Children.Add(Button("Отказ", CloseReminderPanel));
        p.Children.Add(_remEditButtons);
        BuildReminderPicker();
        return p;
    }

    /// <summary>Едно поле на часовника: ▲, числото, ▼ (и колелцето на мишката върху него).</summary>
    private TextBlock ClockDigit(Panel host, Action<int> step)
    {
        var col = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var up = Chip("▲", () => step(1));
        var down = Chip("▼", () => step(-1));
        foreach (var b in new[] { up, down })
        {
            b.Padding = new Thickness(6, 0, 6, 1);
            b.HorizontalAlignment = HorizontalAlignment.Center;
        }
        var digits = new TextBlock
        {
            FontSize = Body * 1.6,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 1, 0, 1),
            Background = Brushes.Transparent,
            Cursor = Cursors.SizeNS,
        };
        col.MouseWheel += (_, e) =>
        {
            step(e.Delta > 0 ? 1 : -1);
            e.Handled = true;
        };
        col.Children.Add(up);
        col.Children.Add(digits);
        col.Children.Add(down);
        host.Children.Add(col);
        return digits;
    }

    private void SetReminderTime(int hour, int minute)
    {
        // Минутите минават през 60 (59 → 0 и часът +1); часът – през 24.
        hour += (int)Math.Floor(minute / 60.0);
        _remMinute = ((minute % 60) + 60) % 60;
        _remHour = ((hour % 24) + 24) % 24;
        UpdateReminderWhen();
    }

    private TextBlock Label(string text) => new() { Text = text, Foreground = Muted, Margin = new Thickness(0, 8, 0, 3) };

    private void ShowReminderPanel()
    {
        if (_remToggle == null) return;
        // За нова бележка – в „➕ Нова“; за записана (⏰ до нея) – над списъка.
        bool here = _remFor == null ? _notesView == 3 : _notesView is 0 or 1;
        _remPanel.Visibility = _remOn && here ? Visibility.Visible : Visibility.Collapsed;
        SetOn(_remToggle, _remOn && _remFor == null);
        if (!_remOn) return;
        if (_remFor != null)
        {
            _remForLabel.Text = $"Напомняне за: „{Shorten(_remFor.Label, 60)}“";
            _remForLabel.Visibility = Visibility.Visible;
            _remEditButtons.Visibility = Visibility.Visible;
            _remRemove.Visibility = _pet.ReminderFor(_remFor) != null ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            _remForLabel.Visibility = Visibility.Collapsed;
            _remEditButtons.Visibility = Visibility.Collapsed;
        }
        BuildReminderPicker();
    }

    /// <summary>⏰ до бележка: отваря „кога“ за нея (с досегашното напомняне, ако има).</summary>
    private void EditReminderFor(NoteItem note)
    {
        if (_remFor == note && _notesView is 0 or 1)
        {
            CloseReminderPanel();
            return;
        }
        _remFor = note;
        _remOn = true;
        if (_pet.ReminderFor(note) is { } r)
        {
            _remDay = true;
            _remDate = r.Due.Date;
            _remHour = r.Due.Hour;
            _remMinute = r.Due.Minute;
            _remRepeat = r.Repeat;
        }
        BuildNoteList();
    }

    private void CloseReminderPanel()
    {
        _remFor = null;
        _remOn = false;
        _remRepeat = "none";
        BuildNoteList();
    }

    private void ApplyReminder()
    {
        if (_remFor == null || !TryReminderWhen(out var when)) return;
        _pet.SetNoteReminder(_remFor, when, _remDay ? _remRepeat : "none");
        CloseReminderPanel();
    }

    private DateTime DayTime => _remDate.Date + new TimeSpan(_remHour, _remMinute, 0);

    /// <summary>Избраното „кога“ (в бъдещето); не става ли – казва го в червено.</summary>
    private bool TryReminderWhen(out DateTime when)
    {
        when = _remDay ? DayTime : DateTime.Now.AddMinutes(_remAfterMin);
        if (when > DateTime.Now) return true;
        UpdateReminderWhen();
        return false;
    }

    /// <summary>Чиповете (режим, след колко, ден, повторения) – светва избраното; отдолу пише кога точно.</summary>
    private void BuildReminderPicker()
    {
        _remModes.Children.Clear();
        foreach (var (label, day) in new[] { ("⏳ След…", false), ("⏰ Ден и час", true) })
        {
            bool isDay = day;
            var chip = Chip(label, () => { _remDay = isDay; BuildReminderPicker(); });
            chip.Margin = new Thickness(0, 0, 6, 4);
            SetOn(chip, _remDay == isDay);
            _remModes.Children.Add(chip);
        }
        _remAfterPane.Visibility = _remDay ? Visibility.Collapsed : Visibility.Visible;
        _remDayPane.Visibility = _remDay ? Visibility.Visible : Visibility.Collapsed;

        _remAfter.Children.Clear();
        foreach (var (label, minutes) in RemAfterChoices)
        {
            double m = minutes;
            var chip = Chip(label, () => { _remAfterMin = m; _remMinutes.Text = ""; BuildReminderPicker(); });
            chip.Margin = new Thickness(0, 0, 6, 4);
            SetOn(chip, Math.Abs(_remAfterMin - m) < 0.01);
            _remAfter.Children.Add(chip);
        }

        // Днес, утре и „друг ден“ (светва, ако е избран ден от календарчето).
        _remDays.Children.Clear();
        foreach (var (name, day) in new[] { ("Днес", DateTime.Today), ("Утре", DateTime.Today.AddDays(1)) })
        {
            var dd = day;
            var chip = Chip(name, () => { _remDate = dd; _remCalendar.Visibility = Visibility.Collapsed; BuildReminderPicker(); });
            chip.Margin = new Thickness(0, 0, 6, 4);
            SetOn(chip, _remDate == dd);
            _remDays.Children.Add(chip);
        }
        bool other = _remDate > DateTime.Today.AddDays(1);
        SetChip(_remOtherDay, other ? "📅 " + _remDate.ToString("ddd, d MMM", new System.Globalization.CultureInfo("bg-BG")) : "📅 Друг ден");
        SetOn(_remOtherDay, other);
        _remOtherDay.Margin = new Thickness(0, 0, 6, 4);
        _remDays.Children.Add(_remOtherDay);
        if (_remDate < DateTime.Today) _remDate = DateTime.Today;
        _remCalendar.SelectedDate = other ? _remDate : null;
        _remCalendar.DisplayDate = _remDate;

        _remRepeats.Children.Clear();
        foreach (var (id, name) in PetWindow.ReminderRepeats)
        {
            string r = id;
            var chip = Chip(name, () => { _remRepeat = r; BuildReminderPicker(); });
            chip.Margin = new Thickness(0, 0, 6, 4);
            SetOn(chip, _remRepeat == r);
            _remRepeats.Children.Add(chip);
        }
        UpdateReminderWhen();
    }

    private void UpdateReminderWhen()
    {
        if (_remWhen == null) return;
        _remHourText.Text = _remHour.ToString("00");
        _remMinuteText.Text = _remMinute.ToString("00");
        if (!_remDay)
        {
            _remWhen.Foreground = Ink;
            SetRichText(_remWhen, $"⏰ {PetWindow.WhenText(DateTime.Now.AddMinutes(_remAfterMin))} (след {PetWindow.MinutesText(_remAfterMin)})");
            return;
        }
        var when = DayTime;
        _remWhen.Foreground = when <= DateTime.Now ? Bad : Ink;
        SetRichText(_remWhen, when <= DateTime.Now ? "Този час вече мина – избери по-късен."
            : $"⏰ {PetWindow.WhenText(when)}{(_remRepeat == "none" ? "" : " · " + PetWindow.RepeatName(_remRepeat).ToLowerInvariant())}");
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
        // Мечо-доро: заглавие като на „Таймер“ по-долу.
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

    private Border _timerRibbon = null!;

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
        var tutorial = Button("Как се живее с Мечо?", () =>
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
        // Размер: мащаб на всичко (менюто, „Работа“, таблата) и началният размер на менюто.
        p.Children.Add(new TextBlock { Text = "Размер", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 6, 0, 4) });
        var sizeRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        var zoomChips = new List<(double Zoom, Border Chip)>();
        foreach (var (z, name) in new[] { (1.0, "100%"), (1.25, "125%"), (1.5, "150%") })
        {
            double zoom = z;
            var chip = Chip(name, () =>
            {
                SetZoom(zoom);
                foreach (var (zz, c) in zoomChips) SetOn(c, Math.Abs(zz - Zoom) < 0.01);
            });
            chip.Margin = new Thickness(0, 0, 6, 4);
            chip.ToolTip = Tip("Колко едро да е всичко (менюто, „Работа“, таблата)");
            SetOn(chip, Math.Abs(z - Zoom) < 0.01);
            zoomChips.Add((z, chip));
            sizeRow.Children.Add(chip);
        }
        var resetSize = Chip("Началният размер", () => SetZoom(Zoom, resetSize: true));
        resetSize.Margin = new Thickness(6, 0, 6, 4);
        resetSize.ToolTip = Tip("Менюто отново с началния си размер (иначе го местиш с дръжката долу вдясно)");
        sizeRow.Children.Add(resetSize);
        p.Children.Add(sizeRow);

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

        // Мащаб на всичко нарисувано (мечокът, мебелите, уредите, храната…): колко пиксела на екрана е един пиксел от рисунката.
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
        void ShowSize() => sizeLabel.Text = $"Мащаб: {(int)slider.Value}× " + ((int)slider.Value switch
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
        // Гардеробът се вижда в лентата, щом е купен; до „Днес“ – колко задачи остават.
        var (_, allToday, _) = _pet.TodayLoad();
        int leftToday = _pet.OrderedTasks().Count;
        _workBadgeText.Text = leftToday.ToString();
        _workBadge.Visibility = Visibility.Collapsed;   // броят задачи до „Работа“ не се показва
        _workBadge.ToolTip = Tip($"Остават {leftToday} от {allToday} задачи за днес");
        if (_navItems.TryGetValue(PageWardrobe, out var wardrobe))
            wardrobe.Item.Visibility = DailyOffer.HasWardrobe(s) ? Visibility.Visible : Visibility.Collapsed;

        // Мечо
        _portrait.Source = _pet.MoodPortrait;   // изражението: гладен – тъжен, уморен – уморен…
        _portraitBg.Source = _modelBg.Source = _pet.BackdropImage(_pet.CurrentBackdrop);
        BuildGoal();
        _mood.Text = _pet.IsAsleep ? "Ззз… спи сладко" :
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
        bool canPlace = Devices.Concat(PetWindow.FurnitureKinds).Any(i => Kitchen.Owns(s, i));
        _placeTile.Button.Visibility = canPlace ? Visibility.Visible : Visibility.Collapsed;
        if (!canPlace) _placeOpen = false;
        BuildPlaceOptions();
        _placeOptions.Visibility = _placeOpen ? Visibility.Visible : Visibility.Collapsed;
        if (_pet.IsAsleep) _sleepOpen = false;
        if (_pet.IsQuiet) _quietOpen = false;
        if (_pet.OnWalk) _walkOpen = false;
        BuildSleepOptions();
        _sleepOptions.Visibility = _sleepOpen ? Visibility.Visible : Visibility.Collapsed;
        _quietOptions.Visibility = _quietOpen ? Visibility.Visible : Visibility.Collapsed;
        _walkOptions.Visibility = _walkOpen ? Visibility.Visible : Visibility.Collapsed;
        UpdateBearTiles(s);

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
            UiSkin.Set(chip, id == _pet.PomodoroMode.Id ? "button_on" : running ? "button_flat" : "button");
        }
        foreach (var (count, chip) in _pomSeries)
        {
            bool on = count == s.PomodoroSeries;
            SetOn(chip, on);
            chip.IsHitTestVisible = !running;
            chip.Opacity = running && !on ? 0.5 : 1;
            UiSkin.Set(chip, on ? "button_on" : running ? "button_flat" : "button");
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
            // Точно там, където почва написаното (ръб + отстъп + каретката).
            Margin = new Thickness(box.BorderThickness.Left + box.Padding.Left + 2, 0, 0, 0),
            VerticalAlignment = box.AcceptsReturn ? VerticalAlignment.Top : VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
        };
        if (box.AcceptsReturn) text.Margin = new Thickness(box.BorderThickness.Left + box.Padding.Left + 2, box.BorderThickness.Top + box.Padding.Top + 1, 0, 0);
        // И ако вече има написано (описанието на отворена задача) – подсказката не се вижда.
        void Sync() => text.Visibility = box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        box.TextChanged += (_, _) => Sync();
        box.Loaded += (_, _) => Sync();
        Sync();
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
