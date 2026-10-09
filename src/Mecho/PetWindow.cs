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
    Couch,    // „Стой тук“: носи дивана, чете, прибира дивана (виж CouchPhase)
}

public enum CouchPhase
{
    None,
    GoingOut,    // отива до ръба на екрана и излиза
    Waiting,     // чака малко зад ръба (после идва _couchNext)
    Bringing,    // бута дивана навътре
    Walking,     // отива до _walkTarget (после идва _couchNext)
    Sitting,     // седи и чете
    TakingAway,  // бута дивана навън
}

/// <summary>
/// Прозрачен прозорец, в който живее мечокът. Прозорецът следи мечока (и дивана,
/// ако е навън) и се разтяга точно колкото трябва.
/// </summary>
public sealed partial class PetWindow : Window
{
    private const double BubbleZone = 140;     // място за балончето от двете страни на мечока
    private const double WalkSpeed = 45;       // DIP в секунда
    private const double PushSpeed = 30;       // с дивана е по-бавно
    private const double Gravity = 2200;       // DIP/s²
    private const double AwayToSleep = 5 * 60; // секунди без мишка и клавиатура

    private readonly SaveData _save;
    private readonly Random _rng = new();
    private readonly Canvas _root = new();
    private readonly Image _sprite = new();
    private readonly Image _couch = new();
    private readonly Border _bubble;
    private readonly TextBlock _bubbleText;
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Updater _updater = new();
    private SpriteLibrary _lib;
    private Prop _couchProp;

    // Положение в екранни DIP: x е средата на мечока, y е под стъпалата.
    private double _x, _y, _vy, _fallFrom;
    private bool _facingLeft;

    private BearState _state = BearState.Idle;
    private string _anim = "idle";
    private double _animTime;
    private double _stateTime;
    private double _stateLength;
    private double _walkTarget;
    private string? _afterBusy;   // какво следва след анимацията: "sleep" или "couch"
    private bool _sleepIsAway;    // заспал, защото Тут я няма
    private BitmapSource? _shownFrame;

    // Диванът
    private CouchPhase _couchPhase;
    private CouchPhase _couchNext;
    private bool _couchVisible;
    private double _couchX;        // средата на дивана
    private int _couchEdge = 1;    // -1: диванът живее зад левия ръб, +1: зад десния

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

    /// <summary>Зает с дивана: не прави нищо друго, докато не седне или не го прибере.</summary>
    private bool OnCouchMission => _couchPhase != CouchPhase.None || _couchVisible || _save.StayPut || _afterBusy == "couch";

    public PetWindow(SaveData save)
    {
        _save = save;
        _lib = SpriteLibrary.Load();
        _couchProp = _lib.GetProp("couch", 48, 24, 10);

        Title = "Мечо";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;

        RenderOptions.SetBitmapScalingMode(_sprite, BitmapScalingMode.NearestNeighbor);
        RenderOptions.SetBitmapScalingMode(_couch, BitmapScalingMode.NearestNeighbor);
        _sprite.RenderTransformOrigin = new Point(0.5, 0.5);
        _sprite.Cursor = Cursors.Hand;
        _couch.Visibility = Visibility.Collapsed;
        _couch.IsHitTestVisible = false;

        _bubbleText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            FontFamily = new FontFamily("Segoe UI"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
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

        _root.Children.Add(_couch);
        _root.Children.Add(_sprite);
        _root.Children.Add(_bubble);
        Content = _root;
        InitCare();

        _sprite.MouseLeftButtonDown += OnMouseDown;
        _sprite.MouseMove += OnMouseMove;
        _sprite.MouseLeftButtonUp += OnMouseUp;
        _sprite.ContextMenu = BuildMenu();

        var area = SystemParameters.WorkArea;
        _x = double.IsNaN(save.X) ? area.Left + area.Width * 0.75 : save.X;
        _y = area.Bottom;
        ApplySpriteSize();
        ClampX();
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = _x - BubbleZone;
        Top = _y - 220;
        Width = BubbleZone * 2;
        Height = 220;

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
            _couchEdge = _save.CouchEdge < 0 ? -1 : 1;
            _couchX = ClampCouchSpot(_save.CouchX);
            _couchVisible = true;
            SitOnCouch(quiet: true);
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
            case BearState.Couch: UpdateCouch(dt, now); break;
            case BearState.Drag: break;
        }
        UpdateCare(dt, now);

        if (_state != BearState.Drag && _state != BearState.Falling)
        {
            // Лентата със задачи може да се е преместила.
            _y = SystemParameters.WorkArea.Bottom;
            if (!MayLeaveScreen) ClampX();
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

    private bool MayLeaveScreen =>
        _state == BearState.Couch && _couchPhase is CouchPhase.GoingOut or CouchPhase.Waiting
            or CouchPhase.Bringing or CouchPhase.TakingAway or CouchPhase.Walking;

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

        if (!IsQuiet && !InFocus && now > _nextChatter)
        {
            _nextChatter = now + 240 + _rng.Next(240);
            Say(Lines.Pick(Lines.Chatter, _save.OwnerName), 6);
        }

        if (_stateTime < _stateLength) return;

        // Докато панелът е отворен, стои до него.
        if (MenuIsOpen)
        {
            SetIdle();
            return;
        }

        // По време на мед-доро работи до Тут и не се разхожда.
        if (InFocus)
        {
            Play("work", length: 30);
            return;
        }

        // Решава какво да прави.
        int roll = _rng.Next(100);
        if (roll < 50)
        {
            var area = SystemParameters.WorkArea;
            _walkTarget = area.Left + 60 + _rng.NextDouble() * Math.Max(0, area.Width - 120);
            if (Math.Abs(_walkTarget - _x) > 30) SetState(BearState.Walk, "walk");
            else SetIdle();
        }
        else if (roll < 62) Play("work", length: 15 + _rng.Next(20));
        else if (roll < 68) Play("dice");
        else SetIdle();
    }

    private void UpdateWalk(double dt)
    {
        if (StepTowards(_walkTarget, WalkSpeed, dt) || _stateTime > 30) SetIdle();
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
        else if (_afterBusy == "couch") ContinueCouch();
        else SetIdle();
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
        _vy += Gravity * dt;
        _y += _vy * dt;
        double floor = SystemParameters.WorkArea.Bottom;
        if (_y < floor) return;

        _y = floor;
        _vy = 0;
        bool hard = floor - _fallFrom > 120;
        if (hard) Say(Lines.Pick(Lines.Fell, _save.OwnerName));

        if (OnCouchMission)
        {
            if (hard) Play("fall", then: "couch");
            else ContinueCouch();
        }
        else if (hard) Play("fall");
        else SetIdle();
    }

    // ───────────────────────── Диванът („Стой тук“) ─────────────────────────

    private double CouchWidth => _couch.Width;
    private double SeatHeight => _couchProp.Seat * PixelSize;

    /// <summary>Разстояние между средата на мечока и средата на дивана, докато го бута.</summary>
    private double PushOffset => (SpriteWidth + CouchWidth) / 2 - 2 * PixelSize;

    public void StayHere()
    {
        if (_save.StayPut) return;
        _save.StayPut = true;
        _save.SleepingByChoice = false;
        if (!_couchVisible)
        {
            // Диванът идва от по-близкия ръб и застава там, където е мечокът сега.
            var area = SystemParameters.WorkArea;
            _couchX = ClampCouchSpot(_x);
            _couchEdge = _x - area.Left < area.Right - _x ? -1 : 1;
            _save.CouchX = _couchX;
            Say(Lines.Pick(Lines.GoingForCouch, _save.OwnerName));
        }
        ContinueCouch();
        Persist();
    }

    public void GetUp()
    {
        if (!_save.StayPut) return;
        _save.StayPut = false;
        Say(Lines.Pick(Lines.StandUp, _save.OwnerName));
        ContinueCouch();
        Persist();
    }

    /// <summary>
    /// Решава следващата стъпка с дивана според това дали трябва да стои и
    /// къде са мечокът и диванът. Вика се при всяка промяна и след приземяване.
    /// </summary>
    private void ContinueCouch()
    {
        var area = SystemParameters.WorkArea;
        if (_save.StayPut)
        {
            if (!_couchVisible)
            {
                _walkTarget = _couchEdge < 0 ? area.Left - SpriteWidth : area.Right + SpriteWidth;
                SetCouchPhase(CouchPhase.GoingOut, "walk");
            }
            else if (Math.Abs(_x - _couchX) < 1) SitOnCouch(quiet: false);
            else WalkThen(_couchX, CouchPhase.Sitting);
        }
        else if (_couchVisible)
        {
            // Застава от вътрешната страна на дивана и го бута към ръба.
            WalkThen(_couchX - _couchEdge * PushOffset, CouchPhase.TakingAway);
        }
        else if (_x < area.Left + SpriteWidth / 2 || _x > area.Right - SpriteWidth / 2)
        {
            // Извън екрана (или наполовина): прибира се до мястото, където беше диванът.
            WalkThen(ClampCouchSpot(_save.CouchX), CouchPhase.None);
        }
        else
        {
            _couchPhase = CouchPhase.None;
            SetIdle();
        }
    }

    private void WalkThen(double target, CouchPhase next)
    {
        _walkTarget = target;
        _couchNext = next;
        SetCouchPhase(CouchPhase.Walking, "walk");
    }

    private void WaitThen(CouchPhase next)
    {
        _couchNext = next;
        SetCouchPhase(CouchPhase.Waiting, "idle");
        _stateLength = 1.5;
    }

    private void SetCouchPhase(CouchPhase phase, string anim)
    {
        SetState(BearState.Couch, anim);
        _couchPhase = phase;
    }

    private void SitOnCouch(bool quiet)
    {
        _x = _couchX;
        _save.CouchX = _couchX;
        _facingLeft = false;
        SetCouchPhase(CouchPhase.Sitting, "read");
        if (!quiet) Say(Lines.Pick(Lines.SatDown, _save.OwnerName));
    }

    private void UpdateCouch(double dt, double now)
    {
        var area = SystemParameters.WorkArea;
        switch (_couchPhase)
        {
            case CouchPhase.GoingOut:
                if (StepTowards(_walkTarget, WalkSpeed * 1.3, dt)) WaitThen(CouchPhase.Bringing);
                break;

            case CouchPhase.Waiting:
                if (_stateTime < _stateLength) break;
                if (_couchNext == CouchPhase.Bringing)
                {
                    // Диванът тръгва изцяло зад ръба, мечокът е зад него.
                    _couchX = _couchEdge < 0 ? area.Left - CouchWidth / 2 : area.Right + CouchWidth / 2;
                    _x = _couchX + _couchEdge * PushOffset;
                    _couchVisible = true;
                    SetCouchPhase(CouchPhase.Bringing, "push");
                    Say(Lines.Pick(Lines.Pushing, _save.OwnerName), 3);
                }
                else ContinueCouch();
                break;

            case CouchPhase.Bringing:
            {
                double spot = ClampCouchSpot(_save.CouchX);
                bool there = StepTowards(spot + _couchEdge * PushOffset, PushSpeed, dt);
                _couchX = _x - _couchEdge * PushOffset;
                if (there) SitOnCouch(quiet: false);
                break;
            }

            case CouchPhase.TakingAway:
            {
                double gone = _couchEdge < 0 ? area.Left - CouchWidth / 2 - 4 : area.Right + CouchWidth / 2 + 4;
                bool there = StepTowards(gone - _couchEdge * PushOffset, PushSpeed, dt);
                _couchX = _x + _couchEdge * PushOffset;
                if (there)
                {
                    _couchVisible = false;
                    WaitThen(CouchPhase.None);
                }
                break;
            }

            case CouchPhase.Walking:
                if (!StepTowards(_walkTarget, WalkSpeed, dt) && _stateTime < 60) break;
                switch (_couchNext)
                {
                    case CouchPhase.Sitting: SitOnCouch(quiet: false); break;
                    case CouchPhase.TakingAway: SetCouchPhase(CouchPhase.TakingAway, "push"); break;
                    default:
                        _couchPhase = CouchPhase.None;
                        SetIdle();
                        break;
                }
                break;

            case CouchPhase.Sitting:
            {
                // Ако Тут я няма, задрямва с книжката на корема.
                bool away = NativeMethods.IdleSeconds() > AwayToSleep;
                string anim = away ? "read_sleep" : "read";
                if (anim != _anim)
                {
                    _anim = anim;
                    _animTime = 0;
                    if (!away) Say(Lines.Pick(Lines.WelcomeBack, _save.OwnerName));
                }
                if (!away && !IsQuiet && now > _nextChatter)
                {
                    _nextChatter = now + 180 + _rng.Next(180);
                    Say(Lines.Pick(Lines.Reading, _save.OwnerName), 6);
                }
                break;
            }
        }
    }

    /// <summary>Диванът трябва да се вижда целият.</summary>
    private double ClampCouchSpot(double x)
    {
        var area = SystemParameters.WorkArea;
        double half = (double.IsNaN(CouchWidth) ? 144 : CouchWidth) / 2;
        if (double.IsNaN(x)) x = area.Left + area.Width * 0.75;
        return Math.Clamp(x, area.Left + half, Math.Max(area.Left + half, area.Right - half));
    }

    // ───────────────────────── Състояния ─────────────────────────

    private void SetState(BearState state, string anim, double length = 0)
    {
        _state = state;
        if (state != BearState.Busy) _afterBusy = null;
        if (state != BearState.Couch) _couchPhase = CouchPhase.None;
        _stateTime = 0;
        _stateLength = length;
        if (anim != _anim) _animTime = 0;
        _anim = anim;
    }

    private void SetIdle() => SetState(BearState.Idle, "idle", 3 + _rng.NextDouble() * 6);

    /// <summary>Пуска анимация веднъж (или за length секунди, ако се повтаря).</summary>
    private void Play(string anim, string? then = null, bool away = false, double length = 0)
    {
        var a = _lib.Get(anim);
        SetState(BearState.Busy, anim, length > 0 ? length : a.Duration + 0.4);
        _afterBusy = then;
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
        _couchProp = _lib.GetProp("couch", 48, 24, 10);
        _shownFrame = null;
        ApplySpriteSize();
        Say("Ново облекло? Ура!");
    }

    public void Persist()
    {
        _save.X = _x;
        _save.CouchEdge = _couchEdge;
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
        _menuOpenAtPress = MenuIsOpen || Now - _menuClosedAt < 0.3;
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
            if (IsAsleep || goingToSleep)
                Say(Lines.Pick(Lines.WokenByDrag, _save.OwnerName));
            else
                Say(Lines.Pick(Lines.Dragged, _save.OwnerName), 2);

            // Ако е седял, хващаме го там, където се вижда (над седалката).
            if (_couchPhase == CouchPhase.Sitting) _y -= SeatHeight;
            _grabOffset = new Point(_x, _y) - _pressScreen;
            SetState(BearState.Drag, "drag");
        }

        var area = SystemParameters.WorkArea;
        _x = p.X + _grabOffset.X;
        _y = Math.Clamp(p.Y + _grabOffset.Y, area.Top + SpriteHeight, area.Bottom);
        ClampX();
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
            SetState(BearState.Falling, "drag");
            Persist();
            return;
        }
        Poke();
    }

    /// <summary>Клик върху мечока: отваря (или затваря) панела и мечокът реагира.</summary>
    private void Poke()
    {
        if (!ToggleMenu(fromClick: true)) return;

        if (IsAsleep || _anim == "read_sleep")
            Say(Lines.Pick(Lines.PokedAsleep, _save.OwnerName), 3);
        else if (_state == BearState.Couch && _couchPhase == CouchPhase.Sitting)
            Say(Lines.Pick(Lines.PokedReading, _save.OwnerName), 3);
        else if (CanAnimateFreely && _state != BearState.Busy)
            Play("happy");
    }

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

        _menu ??= new MenuWindow(this);
        _menu.ShowNear(_bearRect);
        return true;
    }

    internal void MenuClosed() => _menuClosedAt = Now;

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu();
        var open = new MenuItem { Header = "📋 Отвори панела" };
        open.Click += (_, _) => ToggleMenu();
        menu.Items.Add(open);
        menu.Items.Add(new Separator());
        var hello = new MenuItem { Header = "👋 Здравей!" };
        hello.Click += (_, _) =>
        {
            if (IsAsleep) return;
            if (!OnCouchMission) Play("dance", length: 3);
            Say(Lines.Pick(Lines.Poked, _save.OwnerName));
        };
        var dice = new MenuItem { Header = "🎲 Хвърли зар" };
        dice.Click += (_, _) => RollDice();
        var stay = new MenuItem();
        stay.Click += (_, _) => { if (_save.StayPut) GetUp(); else StayHere(); };
        var sleep = new MenuItem();
        sleep.Click += (_, _) => { if (IsAsleep) WakeUp(); else GoToSleep(); };
        var quiet = new MenuItem();
        quiet.Click += (_, _) => SetQuiet(!IsQuiet);
        var update = new MenuItem();
        update.Click += (_, _) =>
        {
            if (_updater.IsAvailable) _ = InstallUpdate();
            else _ = CheckForUpdates(manual: true);
        };
        var hide = new MenuItem { Header = "🙈 Скрий (иконката е до часовника)" };
        hide.Click += (_, _) => Hide();

        menu.Items.Add(hello);
        menu.Items.Add(dice);
        menu.Items.Add(stay);
        menu.Items.Add(sleep);
        menu.Items.Add(quiet);
        menu.Items.Add(new Separator());
        menu.Items.Add(update);
        menu.Items.Add(hide);

        menu.Opened += (_, _) =>
        {
            stay.Header = _save.StayPut ? "🚶 Стани от дивана" : "🛋️ Стой тук и почети";
            sleep.Header = IsAsleep ? "☀️ Събуди се" : "🌙 Лягай да спиш";
            quiet.Header = IsQuiet ? "🔔 Може да говориш" : "🤫 Тихо за 1 час";
            dice.IsEnabled = !IsAsleep;
            hello.IsEnabled = !IsAsleep;
            sleep.IsEnabled = CanSleep;
            update.Header = _updater.IsAvailable
                ? $"⬆️ Обнови до версия {_updater.LatestVersion}"
                : $"🔄 Провери за обновление (сега: {Updater.CurrentVersion})";
            update.IsEnabled = !_updater.IsBusy;
        };
        return menu;
    }

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
        _couch.Source = _couchProp.Image;
        _couch.Width = _couchProp.Image.PixelWidth * PixelSize;
        _couch.Height = _couchProp.Image.PixelHeight * PixelSize;
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

    /// <summary>
    /// Нарежда мечока, дивана и балончето в екранни координати и разтяга
    /// прозореца точно около тях.
    /// </summary>
    private void UpdateLayoutAndWindow()
    {
        var area = SystemParameters.WorkArea;
        bool sitting = _state == BearState.Couch && _couchPhase == CouchPhase.Sitting;

        var bear = new Rect(_x - SpriteWidth / 2, _y - SpriteHeight - (sitting ? SeatHeight : 0), SpriteWidth, SpriteHeight);
        var bounds = new Rect(_x - BubbleZone, bear.Top - 4, BubbleZone * 2, bear.Bottom - bear.Top + 4);

        Rect couch = Rect.Empty;
        _couch.Visibility = _couchVisible ? Visibility.Visible : Visibility.Collapsed;
        if (_couchVisible)
        {
            couch = new Rect(_couchX - CouchWidth / 2, area.Bottom - _couch.Height, CouchWidth, _couch.Height);
            bounds.Union(couch);
        }

        _bearRect = bear;
        double above = bear.Top - 4;

        Rect tag = Rect.Empty;
        if (_timerTag.Visibility == Visibility.Visible)
        {
            _timerTag.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var ts = _timerTag.DesiredSize;
            tag = new Rect(_x - ts.Width / 2, above - ts.Height, ts.Width, ts.Height);
            bounds.Union(tag);
            above = tag.Top - 3;
        }

        Rect bubble = Rect.Empty;
        if (_bubble.Visibility == Visibility.Visible)
        {
            _bubble.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var size = _bubble.DesiredSize;
            // Балончето не излиза извън екрана, дори когато мечокът е до ръба.
            double left = Math.Clamp(_x - size.Width / 2, area.Left + 4, Math.Max(area.Left + 4, area.Right - 4 - size.Width));
            bubble = new Rect(left, above - size.Height, size.Width, size.Height);
            bounds.Union(bubble);
        }

        // Прозорецът е подравнен към физическите пиксели, за да не се размазва рисунката.
        double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        double winLeft = Math.Floor(bounds.Left * dpi) / dpi;
        double winTop = Math.Floor(bounds.Top * dpi) / dpi;
        double winW = Math.Ceiling((bounds.Right - winLeft) * dpi) / dpi;
        double winH = Math.Ceiling((bounds.Bottom - winTop) * dpi) / dpi;

        Place(_sprite, bear, winLeft, winTop, dpi);
        if (_couchVisible) Place(_couch, couch, winLeft, winTop, dpi);
        if (!bubble.IsEmpty) Place(_bubble, bubble, winLeft, winTop, dpi);
        if (!tag.IsEmpty) Place(_timerTag, tag, winLeft, winTop, dpi);

        if (Math.Abs(Left - winLeft) > 0.01) Left = winLeft;
        if (Math.Abs(Top - winTop) > 0.01) Top = winTop;
        if (Math.Abs(Width - winW) > 0.01) Width = winW;
        if (Math.Abs(Height - winH) > 0.01) Height = winH;
    }

    private static void Place(UIElement element, Rect screen, double winLeft, double winTop, double dpi)
    {
        Canvas.SetLeft(element, Math.Round((screen.Left - winLeft) * dpi) / dpi);
        Canvas.SetTop(element, Math.Round((screen.Top - winTop) * dpi) / dpi);
    }

    private void ClampX()
    {
        var area = SystemParameters.WorkArea;
        double half = SpriteWidth > 0 ? SpriteWidth / 2 : 48;
        _x = Math.Clamp(_x, area.Left + half, area.Right - half);
    }

    // ───────────────────────── Балонче ─────────────────────────

    public void Say(string text, double seconds = 4)
    {
        _bubbleText.Text = text;
        _bubble.Visibility = Visibility.Visible;
        _bubbleUntil = Now + seconds + text.Length * 0.04;
    }

    private void HideBubble() => _bubble.Visibility = Visibility.Collapsed;
}
