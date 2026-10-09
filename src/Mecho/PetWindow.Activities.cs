using System;
using System.Windows;
using System.Windows.Controls;
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
        SetState(BearState.Walk, "walk");
        _afterAction = then;
    }

    /// <summary>Избира следващото занимание. Тежестите са колко често да се случва всяко.</summary>
    private void ChooseActivity()
    {
        var area = Area;
        bool tired = IsExhausted; // уморен: не му се играе, по-скоро сяда или се прозява
        var choices = new (int Weight, Action Do)[]
        {
            (tired ? 6 : 26, () =>
            {
                double x = area.Left + 60 + _rng.NextDouble() * Math.Max(0, area.Width - 120);
                if (Math.Abs(x - _x) > 30) WalkTo(x);
                else SetIdle();
            }),
            (tired ? 2 : 12, () => Play("work", length: 8 + _rng.Next(8), after: () => Say(Lines.Pick(Lines.SketchDone, _save.OwnerName), 5))),
            (tired ? 0 : 7, () =>
            {
                int n = _rng.Next(1, 7);
                Play("dice", after: () => Say(Lines.DiceRoll(n) + " " + Lines.Pick(Lines.Playtest, _save.OwnerName), 5));
            }),
            (tired ? 0 : 5, () =>
            {
                Play("dance", length: 3);
                Say(Lines.Pick(Lines.Dancing, _save.OwnerName), 3);
            }),
            (_save.Fullness < 90 ? 5 : 0, () =>
            {
                Play("eat");
                Say(Lines.Pick(Lines.Snack, _save.OwnerName), 3);
                _save.Fullness = Math.Min(100, _save.Fullness + 1);
            }),
            (InFocus ? 0 : tired ? 12 : 7, () => ReadForAWhile(40 + _rng.Next(50))),
            (tired ? 12 : _save.Energy < 70 ? 4 : 1, () =>
            {
                Play("yawn");
                Say(tired ? Lines.Pick(Lines.Sleepy, _save.OwnerName) : "*протяга се* Ааах.", 3);
            }),
            (tired ? 0 : 8, FollowCursor),
            (tired ? 0 : 7, StartChase),
            (16, SetIdle),
        };

        int total = 0;
        foreach (var c in choices) total += c.Weight;
        int roll = _rng.Next(total);
        foreach (var c in choices)
        {
            if (roll < c.Weight)
            {
                c.Do();
                return;
            }
            roll -= c.Weight;
        }
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
