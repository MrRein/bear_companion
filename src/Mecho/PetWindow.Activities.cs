using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace Mecho;

// Какво прави мечокът сам, когато никой не му е казал какво да прави.
public sealed partial class PetWindow
{
    private Action? _afterAction;   // какво да направи, след като свърши анимацията или разходката

    // Пеперудата
    private readonly Image _butterfly = new();
    private readonly ScaleTransform _flap = new(1, 1);
    private Prop _butterflyProp = null!;
    private bool _bfActive, _bfLeaving;
    private double _bfX, _bfY, _bfVx, _bfVy, _bfTime, _bfTargetX, _chaseLength;

    private void InitActivities()
    {
        _butterflyProp = _lib.GetProp("butterfly", 9, 7, 0);
        RenderOptions.SetBitmapScalingMode(_butterfly, BitmapScalingMode.NearestNeighbor);
        _butterfly.Source = _butterflyProp.Image;
        _butterfly.RenderTransformOrigin = new Point(0.5, 0.5);
        _butterfly.RenderTransform = _flap;
        _butterfly.IsHitTestVisible = false;
        _butterfly.Visibility = Visibility.Collapsed;
        _root.Children.Add(_butterfly);
    }

    /// <summary>Картинка на предмет (храна, уред) за панела.</summary>
    public System.Windows.Media.Imaging.BitmapSource PropImage(string name) => _lib.GetProp(name, 16, 16, 0).Image;

    /// <summary>Портрет за панела: първият кадър от „стои“.</summary>
    public System.Windows.Media.Imaging.BitmapSource Portrait => _lib.Get("idle").Frames[0];

    /// <summary>Свърши едно нещо: застава и прави каквото е обещал след него.</summary>
    private void FinishThen()
    {
        var then = _afterAction;
        SetIdle();
        then?.Invoke();
    }

    private void WalkTo(double x, Action? then = null)
    {
        _walkTarget = x;
        SetState(BearState.Walk, IsExhausted ? "walk_tired" : "walk");
        _afterAction = then;
    }

    private string _lastActivity = "";

    /// <summary>
    /// Избира следващото занимание. Тежестите казват колко често е всяко; зависят от
    /// умората и от часа (сутрин йога и чай, следобед игри и рисуване, вечер четене и
    /// музика). Едно и също занимание не се повтаря два пъти подред.
    /// </summary>
    private void ChooseActivity()
    {
        var area = Area;
        bool tired = IsExhausted; // уморен: не му се играе, по-скоро сяда или се прозява
        int hour = DateTime.Now.Hour;
        bool morning = hour is >= 6 and < 12, afternoon = hour is >= 12 and < 18, evening = hour >= 18 || hour < 6;
        int busy = InFocus ? 0 : 1;     // по време на мечо-доро не сяда в кътчета

        var choices = new (string Name, double Weight, Action Do)[]
        {
            ("walk", tired ? 6 : 20, () =>
            {
                double x = WalkTarget();
                if (Math.Abs(x - _x) > 30) WalkTo(x);
                else SetIdle();
            }),
            ("climb", tired || !HasPlatforms ? 0 : 10, Climb),
            ("sketch", tired ? 2 : 8, () => Play("work", length: 8 + _rng.Next(8), after: () => Say(Lines.Pick(Lines.SketchDone, _save.OwnerName), 5))),
            ("dice", tired ? 0 : 4, () =>
            {
                int n = _rng.Next(1, 7);
                Play("dice", after: () => Say(Lines.DiceRoll(n) + " " + Lines.Pick(Lines.Playtest, _save.OwnerName), 5));
            }),
            ("dance", tired ? 0 : 3, () =>
            {
                Play("dance", length: 3);
                Say(Lines.Pick(Lines.Dancing, _save.OwnerName), 3);
            }),
            ("snack", _save.Fullness < 90 ? 3 : 0, () =>
            {
                Play("eat");
                Say(Lines.Pick(Lines.Snack, _save.OwnerName), 3);
                _save.Fullness = Math.Min(100, _save.Fullness + 1);
            }),
            ("stretch", tired ? 10 : _save.Energy < 70 ? 3 : 1, () =>
            {
                Play("yawn");
                Say(tired ? Lines.Pick(Lines.Sleepy, _save.OwnerName) : "*протяга се* Ааах.", 3);
            }),
            ("cursor", tired ? 0 : 6, FollowCursor),
            ("butterfly", tired ? 0 : 5, StartChase),
            ("peek", tired ? 0 : 4, Peekaboo),
            ("juggle", tired ? 0 : 4, Juggle),

            // Кътчета: сяда с предмет за около минута.
            ("reading", busy * (tired ? 12 : evening ? 9 : 5), () => StartScene(Scenes.Reading, 40 + _rng.Next(50))),
            ("gaming", busy * (tired ? 1 : afternoon || evening ? 8 : 4), () => StartScene(Scenes.Gaming, 45 + _rng.Next(45))),
            ("painting", busy * (tired ? 1 : afternoon ? 7 : 4), () => StartScene(Scenes.Painting, 40 + _rng.Next(40))),
            ("tea", busy * (morning ? 6 : tired ? 6 : 3), () => StartScene(Scenes.Tea, 30 + _rng.Next(30))),
            ("yoga", busy * (tired ? 0 : morning ? 6 : 2), () => StartScene(Scenes.Yoga, 25 + _rng.Next(25))),
            ("plant", busy * (morning ? 4 : 2), () => StartScene(Scenes.Plant, 15 + _rng.Next(15))),
            ("music", busy * (evening ? 7 : 3), () => StartScene(Scenes.Music, 30 + _rng.Next(40))),
            ("board", busy * (tired ? 1 : afternoon ? 6 : 3), () => StartScene(Scenes.Board, 40 + _rng.Next(40))),

            ("idle", 12, SetIdle),
        };

        double total = 0;
        foreach (var c in choices) if (c.Name != _lastActivity || c.Name == "idle") total += c.Weight;
        double roll = _rng.NextDouble() * total;
        foreach (var c in choices)
        {
            if (c.Name == _lastActivity && c.Name != "idle") continue;
            if (roll < c.Weight)
            {
                _lastActivity = c.Name;
                c.Do();
                return;
            }
            roll -= c.Weight;
        }
        SetIdle();
    }

    // ───────────────────────── Наднича иззад ръба ─────────────────────────

    private bool _peeking; // може да излезе наполовина извън екрана

    /// <summary>Отива до ръба, скрива се зад него и изскача: „Бау!“.</summary>
    private void Peekaboo()
    {
        var area = Area;
        int dir = _x - area.Left < area.Right - _x ? -1 : 1;
        double half = HalfWidth;
        double inside = dir < 0 ? area.Left + half : area.Right - half;
        double hidden = dir < 0 ? area.Left - half * 0.6 : area.Right + half * 0.6;
        WalkTo(inside, () =>
        {
            _peeking = true;
            WalkTo(hidden, () =>
            {
                _facingLeft = dir > 0;
                Play("idle", length: 2.5 + _rng.NextDouble() * 2, after: () =>
                {
                    Say(Lines.Pick(Lines.Peekaboo, _save.OwnerName), 3);
                    Play("happy", after: () => WalkTo(inside, () => _peeking = false));
                });
            });
        });
    }

    // ───────────────────────── Жонглира ─────────────────────────

    private readonly System.Collections.Generic.List<Image> _juggleNuts = new();
    private double _juggleUntil, _juggleStart;

    private void Juggle()
    {
        _juggleStart = Now;
        _juggleUntil = Now + 6 + _rng.NextDouble() * 4;
        Say(Lines.Pick(Lines.JuggleStart, _save.OwnerName), 3);
        Play("juggle", length: _juggleUntil - Now, after: () => Say(Lines.Pick(Lines.JuggleEnd, _save.OwnerName), 3));
    }

    /// <summary>Лешниците летят в кръг над главата му, докато жонглира.</summary>
    private Rect[] JuggleRects(Rect bear)
    {
        bool on = _anim == "juggle" && Now < _juggleUntil;
        if (_juggleNuts.Count == 0)
        {
            var nut = _lib.GetProp("nut", 5, 5, 0);
            for (int i = 0; i < 3; i++)
            {
                var img = new Image { Source = nut.Image, IsHitTestVisible = false };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.NearestNeighbor);
                _juggleNuts.Add(img);
                _root.Children.Add(img);
            }
        }
        var rects = new Rect[3];
        for (int i = 0; i < 3; i++)
        {
            var img = _juggleNuts[i];
            img.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            if (!on) continue;
            img.Width = ((BitmapSource)img.Source).PixelWidth * PixelSize;
            img.Height = ((BitmapSource)img.Source).PixelHeight * PixelSize;
            double t = (Now - _juggleStart) * 3.2 + i * Math.PI * 2 / 3;
            double cx = bear.Left + bear.Width / 2 + Math.Cos(t) * bear.Width * 0.3;
            double cy = bear.Top + bear.Height * 0.15 - Math.Abs(Math.Sin(t)) * bear.Height * 0.55;
            rects[i] = new Rect(cx - img.Width / 2, cy - img.Height / 2, img.Width, img.Height);
        }
        return rects;
    }

    /// <summary>Отива до курсора на мишката (ако е на неговия екран) и го поздравява.</summary>
    private void FollowCursor()
    {
        double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var pos = System.Windows.Forms.Cursor.Position;
        double cx = pos.X / dpi, cy = pos.Y / dpi;
        var area = Area;
        if (!area.Contains(new Point(cx, cy)) || Math.Abs(cx - _x) < 60)
        {
            SetIdle();
            return;
        }
        double half = HalfWidth;
        WalkTo(Math.Clamp(cx, area.Left + half, area.Right - half), () =>
        {
            Say(Lines.Pick(Lines.CursorFound, _save.OwnerName), 4);
            Play("happy");
        });
    }

    // ───────────────────────── Пеперудата ─────────────────────────

    private void StartChase()
    {
        var area = Area;
        _bfActive = true;
        _bfLeaving = false;
        _bfTime = 0;
        _chaseLength = 10 + _rng.NextDouble() * 8;
        // Долита отстрани.
        int side = _rng.Next(2) == 0 ? -1 : 1;
        _bfX = Math.Clamp(_x + side * (WindowW / 2 - 50), area.Left + 20, area.Right - 20);
        _bfY = area.Bottom - 180;
        _bfTargetX = _x;
        _bfVx = 0;
        _bfVy = 0;
        Say(Lines.Pick(Lines.ButterflySeen, _save.OwnerName), 3);
        SetState(BearState.Chase, "walk");
    }

    private void UpdateChase(double dt)
    {
        if (!_bfActive)
        {
            // Пеперудата отлетя.
            Say(Lines.Pick(_rng.Next(3) == 0 ? Lines.ButterflyCaught : Lines.ButterflyGone, _save.OwnerName), 4);
            Play(_rng.Next(2) == 0 ? "happy" : "sad", length: 2);
            return;
        }
        if (_bfLeaving)
        {
            SetAnimIfDifferent("idle");
            return;
        }
        if (_stateTime > _chaseLength)
        {
            _bfLeaving = true;
            _bfVy = -160;
            _bfVx = (_rng.NextDouble() - 0.5) * 200;
            return;
        }

        // Тича след пеперудата, но спира, ако е точно под нея.
        if (Math.Abs(_bfX - _x) > 12)
        {
            SetAnimIfDifferent("walk");
            StepTowards(_bfX, WalkSpeed * 1.6, dt);
            ClampX();
        }
        else SetAnimIfDifferent("happy");
    }

    private void SetAnimIfDifferent(string anim)
    {
        if (_anim == anim) return;
        _anim = anim;
        _animTime = 0;
    }

    private void UpdateButterfly(double dt)
    {
        if (!_bfActive) return;
        _bfTime += dt;
        var area = Area;

        // Ако мечокът спре да я гони (вдигнат е, заспал…), пеперудата отлита.
        if (_state != BearState.Chase && !_bfLeaving)
        {
            _bfLeaving = true;
            _bfVy = -160;
        }

        if (_bfLeaving)
        {
            _bfX += _bfVx * dt;
            _bfY += _bfVy * dt;
            if (_bfY < Math.Max(area.Top, _window.Top) + 6) _bfActive = false;
        }
        else
        {
            // Пърха насам-натам около мечока, на нивото на главата му.
            // Стои в прозореца на мечока (около него), за да не го разтяга.
            double reach = WindowW / 2 - 40;
            if (_rng.NextDouble() < dt / 1.4)
                _bfTargetX = Math.Clamp(_x + (_rng.NextDouble() - 0.5) * 2 * reach, area.Left + 30, area.Right - 30);
            _bfVx += Math.Sign(_bfTargetX - _bfX) * 220 * dt;
            _bfVx = Math.Clamp(_bfVx * (1 - 0.8 * dt), -140, 140);
            _bfX = Math.Clamp(_bfX + _bfVx * dt, _x - reach, _x + reach);
            _bfY = area.Bottom - 150 + Math.Sin(_bfTime * 2.3) * 45 + Math.Sin(_bfTime * 7) * 6;
        }

        // Махане с крилца.
        _flap.ScaleY = (int)(_bfTime * 9) % 2 == 0 ? 1 : 0.45;
        _flap.ScaleX = _bfVx < 0 ? -1 : 1;
    }

    private Rect ButterflyRect
    {
        get
        {
            _butterfly.Visibility = _bfActive ? Visibility.Visible : Visibility.Collapsed;
            if (!_bfActive) return Rect.Empty;
            double w = _butterflyProp.Image.PixelWidth * PixelSize;
            double h = _butterflyProp.Image.PixelHeight * PixelSize;
            _butterfly.Width = w;
            _butterfly.Height = h;
            return new Rect(_bfX - w / 2, _bfY - h / 2, w, h);
        }
    }
}
