using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Mecho;

public enum BearState
{
    Idle,
    Walk,
    Busy,     // играе анимация веднъж (радост, зар, работа…) и се връща към Idle
    Sleep,
    Drag,
    Falling,
    Couch,    // седи на дивана и чете
    Chase,    // гони пеперуда
    Jump,     // скача на нарисувана платформа
}

/// <summary>
/// Прозрачен прозорец, в който живее мечокът. Прозорецът следи мечока (и дивана,
/// ако е навън) и се разтяга точно колкото трябва.
/// </summary>
public sealed partial class PetWindow : Window
{
    private const double BubbleZone = 140;     // половината от най-широкото балонче
    private const double WalkSpeed = 45;       // DIP в секунда
    private const double Gravity = 2200;       // DIP/s²
    private const double AwayToSleep = 5 * 60; // секунди без мишка и клавиатура

    private readonly SaveData _save;
    private readonly Random _rng = new();
    private readonly Canvas _root = new();
    private readonly Image _sprite = new();
    private readonly Image _seat = new();   // седалката на кътчето (диван, столче, постелка)
    private readonly Image _side = new();   // предметът до мечока (компютър, статив…)
    private readonly Border _bubble;
    private readonly TextBlock _bubbleText;
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Updater _updater = new();
    private SpriteLibrary _lib;

    // Положение в екранни DIP: x е средата на мечока, y е под стъпалата.
    private double _x, _y, _vy, _fallFrom;
    private bool _facingLeft;

    private BearState _state = BearState.Idle;
    private string _anim = "idle";
    private double _animTime;
    private double _stateTime;
    private double _stateLength;
    private double _walkTarget;
    private string? _afterBusy;   // какво следва след анимацията: "sleep"
    private bool _sleepIsAway;    // заспал, защото Тут я няма
    private BitmapSource? _shownFrame;

    // Диванът

    private double _nextChatter;
    private double _lastTime;
    private double _lastSave;
    private double _bubbleUntil;
    private double _nextUpdateCheck;
    private Rect _bearRect;
    private int _announcedVersion;

    // Мишка
    private bool _pressed;
    private Point _pressScreen;
    private Vector _grabOffset;

    public bool IsAsleep => _state == BearState.Sleep;
    public bool IsQuiet => DateTime.Now < _save.QuietUntil;
    public bool StaysPut => _save.StayPut;
    public bool CanSleep => IsAsleep || !OnCouchMission;
    public Updater Updater => _updater;

    /// <summary>В кътче е (диван, компютър…); ако го влачиш, кътчето идва с него.</summary>
    private bool OnCouchMission => _inScene;

    public PetWindow(SaveData save)
    {
        _save = save;
        // Размерите на шрифта и иконките зависят от мащаба на екрана.
        Ui.Init(VisualTreeHelper.GetDpi(this).DpiScaleX);
        _lib = SpriteLibrary.Load();
        Title = "Мечо";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        UseLayoutRounding = true;
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.Aliased); // пикселният шрифт да е рязък
        SnapsToDevicePixels = true;

        RenderOptions.SetBitmapScalingMode(_sprite, BitmapScalingMode.NearestNeighbor);
        RenderOptions.SetBitmapScalingMode(_seat, BitmapScalingMode.NearestNeighbor);
        RenderOptions.SetBitmapScalingMode(_side, BitmapScalingMode.NearestNeighbor);
        _sprite.RenderTransformOrigin = new Point(0.5, 0.5);
        _sprite.Cursor = Cursors.Hand;
        _seat.Visibility = Visibility.Collapsed;
        _seat.IsHitTestVisible = false;
        _side.Visibility = Visibility.Collapsed;
        _side.IsHitTestVisible = false;

        _bubbleText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            FontFamily = Ui.GameFont,
            FontWeight = FontWeights.Normal,
            FontSize = Ui.Body,
            Foreground = new SolidColorBrush(Color.FromRgb(59, 36, 20)),
        };
        _bubble = new Border
        {
            Child = _bubbleText,
            Background = new SolidColorBrush(Color.FromRgb(255, 248, 230)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(59, 36, 20)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 4, 8, 5),
            MaxWidth = BubbleZone * 2 - 8,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
        };

        _root.Children.Add(_side);
        _root.Children.Add(_seat);
        _root.Children.Add(_sprite);
        _root.Children.Add(_bubble);
        Content = _root;
        InitCare();
        InitActivities();
        SourceInitialized += (_, _) => InitDrawings();

        _sprite.MouseLeftButtonDown += OnMouseDown;
        _sprite.MouseMove += OnMouseMove;
        _sprite.MouseLeftButtonUp += OnMouseUp;
        _sprite.MouseRightButtonDown += OnRightDown;
        _sprite.MouseRightButtonUp += OnRightUp;

        var primary = SystemParameters.WorkArea;
        _x = double.IsNaN(save.X) ? primary.Left + primary.Width * 0.75 : save.X;
        _y = double.IsNaN(save.Y) ? primary.Bottom : save.Y;
        _y = Area.Bottom;
        ApplySpriteSize();
        ClampX();
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = _x - WindowW / 2;
        Top = _y + 4 - WindowH;
        Width = WindowW;
        Height = WindowH;

        SourceInitialized += (_, _) =>
        {
            NativeMethods.MakeToolWindow(new WindowInteropHelper(this).Handle);
            ApplySpriteSize();
        };
        DpiChanged += (_, _) => ApplySpriteSize();
        Loaded += (_, _) => OnStart();

        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += (_, _) => Tick();
    }

    private double Now => _clock.Elapsed.TotalSeconds;

    private void OnStart()
    {
        _lastTime = Now;
        _nextChatter = Now + 120 + _rng.Next(180);
        _nextUpdateCheck = Now + 60;
        if (_save.StayPut)
        {
            // Пак си е на дивана, където го оставихме.
            StartScene(Scenes.Reading, quiet: true);
            Say(Lines.Greeting(_save.OwnerName));
        }
        else if (_save.SleepingByChoice)
        {
            EnterSleep(away: false);
        }
        else
        {
            Play("happy");
            Say(Lines.Greeting(_save.OwnerName));
        }
        UpdateLayoutAndWindow();
        _timer.Start();
    }

    // ───────────────────────── Основен цикъл ─────────────────────────

    private void Tick()
    {
        double now = Now;
        double dt = Math.Clamp(now - _lastTime, 0, 0.1);
        _lastTime = now;
        _stateTime += dt;
        _animTime += dt;

        switch (_state)
        {
            case BearState.Idle: UpdateIdle(now); break;
            case BearState.Walk: UpdateWalk(dt); break;
            case BearState.Busy: UpdateBusy(); break;
            case BearState.Sleep: UpdateSleep(); break;
            case BearState.Falling: UpdateFalling(dt); break;
            case BearState.Couch: UpdateScene(now); break;
            case BearState.Chase: UpdateChase(dt); break;
            case BearState.Jump: UpdateJump(dt); break;
            case BearState.Drag: break;
        }
        UpdateCare(dt, now);
        UpdateFood(dt);
        UpdateButterfly(dt);

        if (_state is not (BearState.Drag or BearState.Falling or BearState.Jump))
        {
            // Земята: нарисувана платформа или долният край (лентата със задачи може да се е преместила).
            double ground = Ground(_y);
            if (ground > _y + PixelSize * 1.5)
            {
                // Платформата свърши (или я изтриха): пада.
                if (_state == BearState.Walk) Say(Lines.Pick(Lines.Dragged, _save.OwnerName), 2);
                _fallFrom = _y;
                _vy = 0;
                SetState(BearState.Falling, _inScene ? _anim : "drag");
            }
            else _y = ground;
            if (!_peeking) ClampX();
        }

        if (_bubble.Visibility == Visibility.Visible && now > _bubbleUntil) HideBubble();
        UpdateTimerTag();
        UpdateFrame();
        UpdateLayoutAndWindow();

        if (now > _nextUpdateCheck)
        {
            _nextUpdateCheck = now + 6 * 3600;
            _ = CheckForUpdates(manual: false);
        }

        if (now - _lastSave > 60)
        {
            _lastSave = now;
            StartNewDay();
            Persist();
        }
    }

    private void UpdateIdle(double now)
    {
        if (NativeMethods.IdleSeconds() > AwayToSleep)
        {
            Say(Lines.Pick(Lines.GoingToSleep, _save.OwnerName));
            Play("yawn", then: "sleep", away: true);
            return;
        }

        if (WantsNap)
        {
            Say(Lines.Pick(Lines.Sleepy, _save.OwnerName));
            Play("yawn", then: "sleep");
            return;
        }

        if (!IsQuiet && !InFocus && !WaitingForFood && now > _nextChatter)
        {
            _nextChatter = now + 240 + _rng.Next(240);
            Say(Lines.Pick(Lines.Chatter, _save.OwnerName), 6);
        }

        if (_stateTime < _stateLength) return;

        // Докато панелът е отворен, стои до него.
        if (MenuIsOpen || ChatIsOpen)
        {
            SetIdle();
            return;
        }

        // Чака си храната: докато не му я донесеш, не иска да прави нищо друго.
        if (WaitingForFood)
        {
            WaitForFood(now);
            return;
        }

        // По време на мечо-доро работи до Тут и не се разхожда.
        if (InFocus)
        {
            EnterWorkMode();
            return;
        }

        ChooseActivity();
    }

    private void UpdateWalk(double dt)
    {
        if (StepTowards(_walkTarget, IsExhausted ? WalkSpeed * 0.6 : WalkSpeed, dt) || _stateTime > 60) FinishThen();
    }

    /// <summary>Мести мечока към target. Връща true, когато е стигнал.</summary>
    private bool StepTowards(double target, double speed, double dt)
    {
        if (double.IsNaN(target)) return true;
        double dir = Math.Sign(target - _x);
        if (dir == 0) return true;
        _facingLeft = dir < 0;
        _x += dir * speed * dt;
        if (Math.Sign(target - _x) == dir) return false;
        _x = target;
        return true;
    }

    private void UpdateBusy()
    {
        if (_stateTime < _stateLength) return;
        if (_afterBusy == "sleep") EnterSleep(_sleepIsAway);
        else FinishThen();
    }

    private void UpdateSleep()
    {
        // Заспал, защото Тут я нямаше. Щом се върне, се събужда.
        if (_sleepIsAway && NativeMethods.IdleSeconds() < 2)
        {
            Say(Lines.Pick(Lines.WelcomeBack, _save.OwnerName));
            Play("happy");
        }
        // Дрямка: става сам, щом се наспи.
        else if (!_sleepIsAway && _save.Energy >= 100)
        {
            _save.SleepingByChoice = false;
            Say(Lines.Pick(Lines.WokeUp, _save.OwnerName));
            Play("happy");
            NotifyCare();
        }
    }

    private void UpdateFalling(double dt)
    {
        double before = _y;
        _vy += Gravity * dt;
        _y += _vy * dt;
        // Каца на първото нарисувано нещо под него (или на земята).
        double floor = Ground(before);
        if (_y < floor) return;

        _y = floor;
        _vy = 0;
        bool hard = floor - _fallFrom > 120;
        if (_inScene)
        {
            // С дивана (или столчето) кацането е меко.
            if (hard) Say(Lines.Pick(Lines.CouchLanded, _save.OwnerName));
            ResumeScene();
        }
        else if (hard)
        {
            Say(Lines.Pick(Lines.Fell, _save.OwnerName));
            Play("fall");
        }
        else SetIdle();
    }

    // ───────────────────────── Състояния ─────────────────────────

    private void SetState(BearState state, string anim, double length = 0)
    {
        _state = state;
        if (state is BearState.Drag or BearState.Sleep or BearState.Couch) _peeking = false;
        if (state != BearState.Busy) _afterBusy = null;
        _afterAction = null;
        _stateTime = 0;
        _stateLength = length;
        if (anim != _anim) _animTime = 0;
        _anim = anim;
    }

    private void SetIdle() => SetState(BearState.Idle, "idle", 3 + _rng.NextDouble() * 6);

    /// <summary>Пуска анимация веднъж (или за length секунди, ако се повтаря).</summary>
    private void Play(string anim, string? then = null, bool away = false, double length = 0, Action? after = null)
    {
        var a = _lib.Get(anim);
        SetState(BearState.Busy, anim, length > 0 ? length : a.Duration + 0.4);
        _afterBusy = then;
        _afterAction = after;
        _sleepIsAway = away;
        _animTime = 0;
    }

    private void EnterSleep(bool away)
    {
        _sleepIsAway = away;
        SetState(BearState.Sleep, "sleep");
    }

    public void GoToSleep()
    {
        if (IsAsleep || OnCouchMission) return;
        if (_save.Energy >= 95)
        {
            Say("Не ми се спи още! Бодър съм.", 3);
            return;
        }
        _save.SleepingByChoice = true;
        Say(Lines.Pick(Lines.GoingToSleep, _save.OwnerName));
        Play("yawn", then: "sleep");
        Persist();
    }

    public void WakeUp()
    {
        _save.SleepingByChoice = false;
        if (IsAsleep || _afterBusy == "sleep")
        {
            Say(Lines.Pick(Lines.WokeUp, _save.OwnerName));
            Play("happy");
        }
        Persist();
    }

    public void SetQuiet(bool quiet)
    {
        _save.QuietUntil = quiet ? DateTime.Now.AddHours(1) : DateTime.MinValue;
        if (quiet) HideBubble();
        else Say("Пак мога да говоря!");
        Persist();
    }

    public void RollDice()
    {
        if (IsAsleep || _state is BearState.Drag or BearState.Falling) return;
        int n = _rng.Next(1, 7);
        if (!OnCouchMission) Play("dice");
        Say(Lines.DiceRoll(n));
    }

    public void ReloadArt()
    {
        _lib = SpriteLibrary.Load();
        if (_scene != null) LoadSceneArt(_scene);
        _butterflyProp = _lib.GetProp("butterfly", 9, 7, 0);
        _butterfly.Source = _butterflyProp.Image;
        _shownFrame = null;
        ApplySpriteSize();
        Say("Ново облекло? Ура!");
    }

    public void Persist()
    {
        _save.X = _x;
        _save.Y = _y;
        _save.Save();
    }

    // ───────────────────────── Обновления ─────────────────────────

    /// <summary>
    /// Проверява за нова версия. Сам (на 6 часа) казва само когато има нова;
    /// при ръчна проверка казва и „нямам нова“ или „не успях“.
    /// </summary>
    public async Task CheckForUpdates(bool manual)
    {
        if (manual) Say("Проверявам за нова версия…", 10);
        var result = await _updater.CheckAsync();
        switch (result)
        {
            case Updater.Result.Available when manual || _announcedVersion != _updater.LatestVersion:
                _announcedVersion = _updater.LatestVersion;
                Say($"Има нова версия ({_updater.LatestVersion})! Десен бутон върху мен → „Обнови“.", 8);
                break;
            case Updater.Result.UpToDate when manual:
                Say($"Имам най-новата версия ({Updater.CurrentVersion}). Ура!");
                break;
            case Updater.Result.Failed when manual:
                Say("Не успях да проверя. Има ли интернет?");
                break;
        }
    }

    public async Task InstallUpdate()
    {
        if (!_updater.IsAvailable || _updater.IsBusy) return;
        Say("Обновявам се… ей сега се връщам!", 30);
        Persist();
        if (await _updater.DownloadAndStartInstallAsync())
            Application.Current.Shutdown();
        else
            Say("Не успях да се обновя. Ще пробвам пак по-късно.");
    }

    // ───────────────────────── Мишка ─────────────────────────

    private Point MouseScreen(MouseEventArgs e)
    {
        var p = e.GetPosition(this);
        return new Point(Left + p.X, Top + p.Y);
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _pressed = true;
        _pressScreen = MouseScreen(e);
        _chatOpenAtPress = ChatIsOpen || Now - _chatClosedAt < 0.3;
        _sprite.CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_pressed) return;
        var p = MouseScreen(e);

        if (_state != BearState.Drag)
        {
            if ((p - _pressScreen).Length < 5) return;
            bool goingToSleep = _afterBusy == "sleep";
            _save.SleepingByChoice = false;
            if (_inScene)
                Say(Lines.Pick(Lines.FlyingCouch, _save.OwnerName), 3);
            else if (IsAsleep || goingToSleep)
                Say(Lines.Pick(Lines.WokenByDrag, _save.OwnerName));
            else
                Say(Lines.Pick(Lines.Dragged, _save.OwnerName), 2);

            // Ако седи на дивана, носиш го заедно с дивана и той си чете.
            _grabOffset = new Point(_x, _y) - _pressScreen;
            SetState(BearState.Drag, _inScene ? "read" : "drag");
        }

        // Може да се мести и на друг екран: пазим го в екрана под мишката.
        var area = AreaAt(p.X, p.Y);
        double half = HalfWidth;
        _x = Math.Clamp(p.X + _grabOffset.X, area.Left + half, Math.Max(area.Left + half, area.Right - half));
        _y = Math.Clamp(p.Y + _grabOffset.Y, area.Top + SpriteHeight + (_inScene ? SeatHeight : 0), area.Bottom);
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_pressed) return;
        _pressed = false;
        _sprite.ReleaseMouseCapture();
        e.Handled = true;

        if (_state == BearState.Drag)
        {
            _fallFrom = _y;
            _vy = 0;
            SetState(BearState.Falling, _inScene ? _anim : "drag");
            Persist();
            return;
        }
        OnLeftClick();
    }

    private void OnRightDown(object sender, MouseButtonEventArgs e)
    {
        _menuOpenAtPress = MenuIsOpen || Now - _menuClosedAt < 0.3;
        e.Handled = true;
    }

    private void OnRightUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_state is BearState.Drag or BearState.Falling) return;
        OnRightClick();
    }

    private double _lastFocusPoke = -100;

    /// <summary>
    /// Ляв клик: разговор. По време на мечо-доро напомня, че работим, а ако го
    /// цъкнеш пак скоро, пита дали да спре мечо-дорото.
    /// </summary>
    private void OnLeftClick()
    {
        if (ChatIsOpen)
        {
            _chat!.Hide();
            return;
        }
        if (_chatOpenAtPress) return;

        if (InFocus)
        {
            if (Now - _lastFocusPoke < 20)
            {
                _lastFocusPoke = -100;
                Ask(Conversations.StopFocus(this));
            }
            else
            {
                _lastFocusPoke = Now;
                Say(Lines.Pick(Lines.FocusPoked, _save.OwnerName), 3);
            }
            return;
        }
        Ask(Conversations.Next(this));
    }

    /// <summary>Десен бутон: гъделичкане и панелът „Мечо“.</summary>
    private void OnRightClick()
    {
        if (!ToggleMenu(fromClick: true)) return;

        if (IsAsleep || _anim == "read_sleep")
            Say(Lines.Pick(Lines.PokedAsleep, _save.OwnerName), 3);
        else
        {
            Say(Lines.Pick(Lines.Tickled, _save.OwnerName), 3);
            if (CanAnimateFreely && _state != BearState.Busy) Play("happy");
        }
    }

    // ───────────────────────── Разговор ─────────────────────────

    private ChatWindow? _chat;
    private double _chatClosedAt = -10;
    private bool _chatOpenAtPress;

    public bool ChatIsOpen => _chat is { IsVisible: true };

    /// <summary>Мечокът пита нещо и показва бутони за отговор.</summary>
    public void Ask(ChatQuestion question)
    {
        if (MenuIsOpen) _menu!.Hide();
        HideBubble();
        _chat ??= new ChatWindow(this);
        _chat.Ask(question, _bearRect, Area);
    }

    internal void ChatClosed() => _chatClosedAt = Now;

    /// <summary>Казва нещо и подскача от радост (ако може).</summary>
    public void Cheer(string text)
    {
        Say(text, 4);
        if (CanAnimateFreely && !IsReading) Play("happy");
    }

    public bool IsReading => _state == BearState.Couch && _scene == Scenes.Reading;

    // ───────────────────────── Панелът ─────────────────────────

    private MenuWindow? _menu;
    private double _menuClosedAt = -10;
    private bool _menuOpenAtPress;

    public bool MenuIsOpen => _menu is { IsVisible: true };

    /// <summary>Отваря панела или го затваря. Връща true, ако го е отворил.</summary>
    public bool ToggleMenu(bool fromClick = false)
    {
        if (MenuIsOpen)
        {
            _menu!.Hide();
            return false;
        }
        // Кликът върху мечока първо затваря панела (той губи фокус); да не го отворим пак веднага.
        if (fromClick && _menuOpenAtPress) return false;
        OpenMenu(MenuWindow.BearTab);
        return true;
    }

    /// <summary>Отваря панела на даден таб.</summary>
    public void OpenMenu(int tab)
    {
        if (ChatIsOpen) _chat!.Hide();
        _menu ??= new MenuWindow(this);
        _menu.ShowNear(_bearRect, Area, tab);
    }

    internal void MenuClosed() => _menuClosedAt = Now;

    // ───────────────────────── Рисуване ─────────────────────────

    private double SpriteWidth => _sprite.Width;
    private double SpriteHeight => _sprite.Height;

    /// <summary>Колко DIP е един пиксел от рисунката.</summary>
    private double PixelSize { get; set; } = 3;

    /// <summary>
    /// Рисунките се увеличават с цяло число физически пиксели, за да е pixel art-ът
    /// чист и при 125% или 150% мащаб на Windows.
    /// </summary>
    private void ApplySpriteSize()
    {
        double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        int pixelScale = Math.Max(1, (int)Math.Round(_lib.Scale * dpi));
        PixelSize = pixelScale / dpi;
        _sprite.Width = _lib.FrameWidth * PixelSize;
        _sprite.Height = _lib.FrameHeight * PixelSize;
        SizeSceneArt();
    }

    private void UpdateFrame()
    {
        var frame = _lib.Get(_anim).FrameAt(_animTime);
        if (!ReferenceEquals(frame, _shownFrame))
        {
            _shownFrame = frame;
            _sprite.Source = frame;
        }
        _sprite.RenderTransform = _facingLeft ? new ScaleTransform(-1, 1) : Transform.Identity;
    }

    // Прозорецът на мечока е с постоянен размер и върви точно с мечока. Ако се
    // разтягаше и следеше пеперудата или балончето, Windows понякога местеше или
    // оразмеряваше прозореца кадър по-рано от рисунката: мечокът трепереше, а
    // балончето се отрязваше.
    private const double WindowW = 600;
    private const double WindowH = 340;
    private Rect _window;

    /// <summary>Нарежда мечока, дивана, табелката, балончето и пеперудата в прозореца.</summary>
    private void UpdateLayoutAndWindow()
    {
        var area = Area;
        double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;

        // Котвата (стъпалата на мечока) е на цял физически пиксел: така мечокът
        // е винаги на едно и също място в прозореца и не трепти от закръгляне.
        double ax = Math.Round(_x * dpi), ay = Math.Round(_y * dpi);
        double wpx = Math.Round(WindowW * dpi / 2) * 2, hpx = Math.Round(WindowH * dpi);
        double xs = ax / dpi, ys = ay / dpi;
        _window = new Rect((ax - wpx / 2) / dpi, (ay + 4 - hpx) / dpi, wpx / dpi, hpx / dpi);

        var bear = new Rect(xs - SpriteWidth / 2, ys - SpriteHeight - (_inScene ? SeatHeight : 0), SpriteWidth, SpriteHeight);
        _bearRect = bear;

        Rect seat = Rect.Empty, side = Rect.Empty;
        bool hasSeat = _inScene && _seatProp != null, hasSide = _inScene && _sideProp != null;
        _seat.Visibility = hasSeat ? Visibility.Visible : Visibility.Collapsed;
        _side.Visibility = hasSide ? Visibility.Visible : Visibility.Collapsed;
        if (hasSeat) seat = new Rect(xs - _seat.Width / 2, ys - _seat.Height, _seat.Width, _seat.Height);
        if (hasSide)
        {
            // Предметът стои до мечока (отляво или отдясно), стъпил на същата земя.
            double overlap = 2 * PixelSize;
            double left = _scene!.SideDir < 0 ? bear.Left - _side.Width + overlap : bear.Right - overlap;
            side = new Rect(left, ys - _side.Height, _side.Width, _side.Height);
        }

        double above = bear.Top - 4;
        Rect tag = Rect.Empty;
        if (_timerTag.Visibility == Visibility.Visible)
        {
            _timerTag.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var ts = _timerTag.DesiredSize;
            tag = new Rect(xs - ts.Width / 2, above - ts.Height, ts.Width, ts.Height);
            above = tag.Top - 3;
        }

        Rect bubble = Rect.Empty;
        if (_bubble.Visibility == Visibility.Visible)
        {
            _bubble.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var size = _bubble.DesiredSize;
            // Балончето стои и в екрана, и в прозореца.
            double minX = Math.Max(area.Left, _window.Left) + 4;
            double maxX = Math.Min(area.Right, _window.Right) - 4 - size.Width;
            double minY = Math.Max(area.Top, _window.Top) + 4;
            double left = Math.Clamp(xs - size.Width / 2, minX, Math.Max(minX, maxX));
            double top = above - size.Height;
            if (top < minY)
            {
                // Горе няма място (вдигнат е до горния ръб): балончето отива встрани от мечока.
                left = bear.Right + 6 <= maxX ? bear.Right + 6 : bear.Left - 6 - size.Width;
                left = Math.Clamp(left, minX, Math.Max(minX, maxX));
                top = Math.Max(minY, bear.Top);
            }
            bubble = new Rect(left, top, size.Width, size.Height);
        }

        Rect butterfly = ButterflyRect;
        var nuts = JuggleRects(bear);

        Place(_sprite, bear, _window.Left, _window.Top, dpi);
        if (hasSeat) Place(_seat, seat, _window.Left, _window.Top, dpi);
        if (hasSide) Place(_side, side, _window.Left, _window.Top, dpi);
        if (!bubble.IsEmpty) Place(_bubble, bubble, _window.Left, _window.Top, dpi);
        if (!tag.IsEmpty) Place(_timerTag, tag, _window.Left, _window.Top, dpi);
        if (!butterfly.IsEmpty) Place(_butterfly, butterfly, _window.Left, _window.Top, dpi);
        for (int i = 0; i < nuts.Length; i++)
            if (_juggleNuts[i].Visibility == Visibility.Visible) Place(_juggleNuts[i], nuts[i], _window.Left, _window.Top, dpi);

        if (Math.Abs(Width - _window.Width) > 0.01) Width = _window.Width;
        if (Math.Abs(Height - _window.Height) > 0.01) Height = _window.Height;
        if (Math.Abs(Left - _window.Left) > 0.001) Left = _window.Left;
        if (Math.Abs(Top - _window.Top) > 0.001) Top = _window.Top;
    }

    private static void Place(UIElement element, Rect screen, double winLeft, double winTop, double dpi)
    {
        Canvas.SetLeft(element, Math.Round((screen.Left - winLeft) * dpi) / dpi);
        Canvas.SetTop(element, Math.Round((screen.Top - winTop) * dpi) / dpi);
    }

    /// <summary>
    /// Работната площ (без лентата със задачите) на екрана, където е мечокът.
    /// Докато е зает с дивана, това е екранът на дивана, за да не се обърка,
    /// ако излезе през ръба към съседен екран.
    /// </summary>
    private Rect Area
    {
        get
        {
            return AreaAt(_x, _y - 1);
        }
    }

    /// <summary>Работната площ на екрана, в който е точката (в DIP), или на най-близкия.</summary>
    public Rect AreaAt(double x, double y)
    {
        double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        if (double.IsNaN(x) || double.IsNaN(y)) return SystemParameters.WorkArea;
        var px = new System.Drawing.Point((int)Math.Round(x * dpi), (int)Math.Round(y * dpi));
        var wa = System.Windows.Forms.Screen.FromPoint(px).WorkingArea;
        return new Rect(wa.Left / dpi, wa.Top / dpi, wa.Width / dpi, wa.Height / dpi);
    }

    /// <summary>Половината ширина на мечока (или на дивана, ако седи на него).</summary>
    private double HalfWidth
    {
        get
        {
            double w = SpriteWidth;
            if (double.IsNaN(w) || w <= 0) w = 96;
            if (_inScene && _seatProp != null) w = Math.Max(w, _seat.Width);
            // Предметът отстрани трябва също да е на екрана.
            if (_inScene && _sideProp != null) w = Math.Max(w, SpriteWidth + 2 * _side.Width);
            return w / 2;
        }
    }

    private void ClampX()
    {
        var area = Area;
        double half = HalfWidth;
        _x = Math.Clamp(_x, area.Left + half, Math.Max(area.Left + half, area.Right - half));
    }

    // ───────────────────────── Балонче ─────────────────────────

    public void Say(string text, double seconds = 4)
    {
        _bubbleText.Text = text;
        _bubble.Visibility = Visibility.Visible;
        // Балончето не бива да остава под панела, храната или друг прозорец „винаги отгоре“.
        if (!ChatIsOpen) NativeMethods.BringToTop(new WindowInteropHelper(this).Handle);
        _bubbleUntil = Now + seconds + text.Length * 0.04;
    }

    private void HideBubble() => _bubble.Visibility = Visibility.Collapsed;
}
