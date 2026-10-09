using System;
using System.Diagnostics;
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
}

/// <summary>Прозрачен прозорец, в който живее мечокът. Прозорецът върви заедно с него.</summary>
public sealed class PetWindow : Window
{
    private const double WindowW = 280;
    private const double WindowH = 220;
    private const double WalkSpeed = 45;      // DIP в секунда
    private const double Gravity = 2200;      // DIP/s²
    private const double AwayToSleep = 5 * 60; // секунди без мишка и клавиатура

    private readonly SaveData _save;
    private readonly Random _rng = new();
    private readonly Image _sprite = new();
    private readonly Border _bubble;
    private readonly TextBlock _bubbleText;
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
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
    private string? _afterBusy;   // анимация, която да последва (напр. yawn → sleep)
    private bool _sleepIsAway;    // заспал, защото Тут я няма
    private BitmapSource? _shownFrame;

    private double _bubbleLeft;
    private double _nextChatter;
    private double _lastTime;
    private double _lastSave;

    // Мишка
    private bool _pressed;
    private Point _pressScreen;
    private Vector _grabOffset;
    private int _pokes;
    private double _pokeWindowStart;

    public bool IsAsleep => _state == BearState.Sleep;
    public bool IsQuiet => DateTime.Now < _save.QuietUntil;

    public PetWindow(SaveData save)
    {
        _save = save;
        _lib = SpriteLibrary.Load();

        Title = "Мечо";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        Width = WindowW;
        Height = WindowH;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;

        RenderOptions.SetBitmapScalingMode(_sprite, BitmapScalingMode.NearestNeighbor);
        _sprite.HorizontalAlignment = HorizontalAlignment.Center;
        _sprite.VerticalAlignment = VerticalAlignment.Bottom;
        _sprite.RenderTransformOrigin = new Point(0.5, 0.5);
        _sprite.Cursor = Cursors.Hand;

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
            MaxWidth = WindowW - 8,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
        };

        var root = new Grid();
        root.Children.Add(_sprite);
        root.Children.Add(_bubble);
        Content = root;

        _sprite.MouseLeftButtonDown += OnMouseDown;
        _sprite.MouseMove += OnMouseMove;
        _sprite.MouseLeftButtonUp += OnMouseUp;
        _sprite.ContextMenu = BuildMenu();

        var area = SystemParameters.WorkArea;
        _x = double.IsNaN(save.X) ? area.Left + area.Width * 0.75 : save.X;
        _y = area.Bottom;
        ClampX();
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = _x - WindowW / 2;
        Top = _y - WindowH;

        SourceInitialized += (_, _) =>
        {
            NativeMethods.MakeToolWindow(new WindowInteropHelper(this).Handle);
            ApplySpriteSize();
        };
        DpiChanged += (_, _) => ApplySpriteSize();
        ApplySpriteSize();
        Loaded += (_, _) => OnStart();

        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += (_, _) => Tick();
    }

    private double Now => _clock.Elapsed.TotalSeconds;

    private void OnStart()
    {
        _lastTime = Now;
        _nextChatter = Now + 120 + _rng.Next(180);
        if (_save.SleepingByChoice)
        {
            EnterSleep(away: false);
        }
        else
        {
            Play("happy");
            Say(Lines.Greeting(_save.OwnerName));
        }
        UpdateWindowPosition();
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
            case BearState.Drag: break;
        }

        if (_state != BearState.Drag && _state != BearState.Falling)
        {
            // Лентата със задачи може да се е преместила.
            _y = SystemParameters.WorkArea.Bottom;
            ClampX();
        }

        UpdateFrame();
        UpdateBubble(now);
        UpdateWindowPosition();

        if (now - _lastSave > 60)
        {
            _lastSave = now;
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

        if (!IsQuiet && now > _nextChatter)
        {
            _nextChatter = now + 240 + _rng.Next(240);
            Say(Lines.Pick(Lines.Chatter, _save.OwnerName), 6);
        }

        if (_stateTime < _stateLength) return;

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
        double dir = Math.Sign(_walkTarget - _x);
        _facingLeft = dir < 0;
        _x += dir * WalkSpeed * dt;
        if (Math.Sign(_walkTarget - _x) != dir || _stateTime > 30)
        {
            _x = _walkTarget;
            SetIdle();
        }
    }

    private void UpdateBusy()
    {
        if (_stateTime < _stateLength) return;
        if (_afterBusy == "sleep") EnterSleep(_sleepIsAway);
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
    }

    private void UpdateFalling(double dt)
    {
        _vy += Gravity * dt;
        _y += _vy * dt;
        double floor = SystemParameters.WorkArea.Bottom;
        if (_y < floor) return;

        _y = floor;
        _vy = 0;
        if (floor - _fallFrom > 120)
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
        if (state != BearState.Busy) _afterBusy = null;
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
        _afterBusy = then;
        _sleepIsAway = away;
        _animTime = 0;
        SetState(BearState.Busy, anim, length > 0 ? length : a.Duration + 0.4);
        _animTime = 0;
    }

    private void EnterSleep(bool away)
    {
        _sleepIsAway = away;
        SetState(BearState.Sleep, "sleep");
    }

    public void GoToSleep()
    {
        if (IsAsleep) return;
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
        Play("dice");
        Say(Lines.DiceRoll(n));
    }

    public void ReloadArt()
    {
        _lib = SpriteLibrary.Load();
        _shownFrame = null;
        ApplySpriteSize();
        Say("Ново облекло? Ура!");
    }

    public void Persist()
    {
        _save.X = _x;
        _save.Save();
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
            {
                Say(Lines.Pick(Lines.WokenByDrag, _save.OwnerName));
            }
            else Say(Lines.Pick(Lines.Dragged, _save.OwnerName), 2);
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

    private void Poke()
    {
        double now = Now;
        if (now - _pokeWindowStart > 3)
        {
            _pokeWindowStart = now;
            _pokes = 0;
        }
        _pokes++;

        if (IsAsleep)
        {
            Say(Lines.Pick(Lines.PokedAsleep, _save.OwnerName), 3);
            return;
        }
        if (_state is BearState.Falling) return;
        if (_pokes >= 6)
        {
            Say(Lines.Pick(Lines.TooManyPokes, _save.OwnerName));
            Play("sad", length: 3);
            _pokes = 0;
            return;
        }
        Say(Lines.Pick(Lines.Poked, _save.OwnerName), 3);
        Play("happy");
    }

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu();
        var hello = new MenuItem { Header = "👋 Здравей!" };
        hello.Click += (_, _) => { if (!IsAsleep) { Play("dance", length: 3); Say(Lines.Pick(Lines.Poked, _save.OwnerName)); } };
        var dice = new MenuItem { Header = "🎲 Хвърли зар" };
        dice.Click += (_, _) => RollDice();
        var sleep = new MenuItem();
        sleep.Click += (_, _) => { if (IsAsleep) WakeUp(); else GoToSleep(); };
        var quiet = new MenuItem();
        quiet.Click += (_, _) => SetQuiet(!IsQuiet);
        var hide = new MenuItem { Header = "🙈 Скрий (иконката е до часовника)" };
        hide.Click += (_, _) => Hide();

        menu.Items.Add(hello);
        menu.Items.Add(dice);
        menu.Items.Add(sleep);
        menu.Items.Add(quiet);
        menu.Items.Add(new Separator());
        menu.Items.Add(hide);

        menu.Opened += (_, _) =>
        {
            sleep.Header = IsAsleep ? "☀️ Събуди се" : "🌙 Лягай да спиш";
            quiet.Header = IsQuiet ? "🔔 Може да говориш" : "🤫 Тихо за 1 час";
            dice.IsEnabled = !IsAsleep;
            hello.IsEnabled = !IsAsleep;
        };
        return menu;
    }

    // ───────────────────────── Рисуване ─────────────────────────

    private double SpriteWidth => _sprite.Width;
    private double SpriteHeight => _sprite.Height;

    /// <summary>
    /// Мечокът се увеличава с цяло число физически пиксели, за да е pixel art-ът
    /// чист и при 125% или 150% мащаб на Windows.
    /// </summary>
    private void ApplySpriteSize()
    {
        double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        int pixelScale = Math.Max(1, (int)Math.Round(_lib.Scale * dpi));
        _sprite.Width = _lib.FrameWidth * pixelScale / dpi;
        _sprite.Height = _lib.FrameHeight * pixelScale / dpi;
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

    private void UpdateWindowPosition()
    {
        double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        double left = Math.Round((_x - WindowW / 2) * dpi) / dpi;
        double top = Math.Round((_y - WindowH) * dpi) / dpi;
        if (Math.Abs(Left - left) > 0.01) Left = left;
        if (Math.Abs(Top - top) > 0.01) Top = top;

        // Балончето не излиза извън екрана, дори когато мечокът е до ръба.
        if (_bubble.Visibility == Visibility.Visible)
        {
            var area = SystemParameters.WorkArea;
            double w = _bubble.ActualWidth;
            double inWindow = (WindowW - w) / 2;
            double minLeft = area.Left + 4 - left;
            double maxLeft = area.Right - 4 - w - left;
            double bl = Math.Max(minLeft, Math.Min(maxLeft, inWindow));
            if (Math.Abs(bl - _bubbleLeft) > 0.5)
            {
                _bubbleLeft = bl;
                _bubble.Margin = new Thickness(bl, 0, 0, SpriteHeight + 4);
            }
        }
    }

    private void ClampX()
    {
        var area = SystemParameters.WorkArea;
        double half = SpriteWidth > 0 ? SpriteWidth / 2 : 48;
        _x = Math.Clamp(_x, area.Left + half, area.Right - half);
    }

    // ───────────────────────── Балонче ─────────────────────────

    private double _bubbleUntil;

    public void Say(string text, double seconds = 4)
    {
        _bubbleText.Text = text;
        _bubble.Visibility = Visibility.Visible;
        _bubble.Margin = new Thickness(_bubbleLeft, 0, 0, SpriteHeight + 4);
        _bubbleUntil = Now + seconds + text.Length * 0.04;
    }

    private void HideBubble() => _bubble.Visibility = Visibility.Collapsed;

    private void UpdateBubble(double now)
    {
        if (_bubble.Visibility == Visibility.Visible && now > _bubbleUntil) HideBubble();
    }
}
