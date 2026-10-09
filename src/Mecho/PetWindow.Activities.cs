using System;
using System.Linq;
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
    private double _idleSince;          // откога се скита без работа и без кътче
    private double _boredAfter = 240;   // след колко секунди скитане решава сам какво да прави

    /// <summary>
    /// Поведението е на три нива:
    /// 1. Скита се: разходки, чуди се какво да прави, дребни неща (пеперуда, наднича,
    ///    курсор, жонглира, скица, зар, катерене по рисунките).
    /// 2. Забавления: ако дълго (3–6 мин) никой не му е казал какво да прави, решава
    ///    сам: уморен ли е, дрямва; иначе сяда в някое от своите кътчета за 3–6 мин.
    ///    Тут също може да избере кътче („Почивай си“), тогава стои там до „Стани“.
    /// 3. Работа: мечо-доро или работен таймер (виж EnterWorkMode).
    /// Едно и също не се повтаря подред; тежестите зависят от умората.
    /// </summary>
    private void ChooseActivity()
    {
        if (_idleSince <= 0) _idleSince = Now;
        if (Now - _idleSince > _boredAfter)
        {
            _idleSince = Now;
            _boredAfter = 180 + _rng.Next(180);
            if (_save.Energy < 45)
            {
                TakeNap(Lines.Pick(Lines.BoredNap, _save.OwnerName));
                return;
            }
            StartFun();
            return;
        }

        bool tired = IsExhausted; // уморен: не му се играе, по-скоро се прозява
        var choices = new (string Name, double Weight, Action Do)[]
        {
            ("walk", tired ? 10 : 26, () =>
            {
                double x = WalkTarget();
                if (Math.Abs(x - _x) > 30) WalkTo(x);
                else SetIdle();
            }),
            ("wonder", 10, Wonder),
            ("climb", tired || !HasPlatforms ? 0 : 8, Climb),
            ("sketch", tired ? 1 : 5, () => Play("work", length: 8 + _rng.Next(8), after: () => Say(Lines.Pick(Lines.SketchDone, _save.OwnerName), 5))),
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
            ("snack", _save.Fullness < 90 ? 2 : 0, () =>
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
            ("cursor", tired ? 0 : 5, FollowCursor),
            ("butterfly", tired ? 0 : 5, StartChase),
            ("peek", tired ? 0 : 3, Peekaboo),
            ("juggle", tired ? 0 : 3, Juggle),
            ("idle", 14, SetIdle),
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

    /// <summary>Сяда в някое от кътчетата, които има (сутрин по-често йога и чай, вечер четене и музика).</summary>
    private void StartFun()
    {
        int hour = DateTime.Now.Hour;
        bool morning = hour is >= 6 and < 12, evening = hour >= 18 || hour < 6;
        var owned = Scenes.Fun.Where(f => Scenes.IsUnlocked(f, _save) && f.Id != _lastActivity).ToList();
        if (owned.Count == 0) owned.Add(Scenes.Reading);
        double Weight(Scene f) =>
            (morning && (f == Scenes.Yoga || f == Scenes.Tea || f == Scenes.Plant) ? 3 : 1) *
            (evening && (f == Scenes.Reading || f == Scenes.Music) ? 3 : 1) *
            (IsExhausted && (f == Scenes.Reading || f == Scenes.Tea) ? 3 : 1);
        double total = owned.Sum(Weight), roll = _rng.NextDouble() * total;
        var pick = owned[^1];
        foreach (var f in owned)
        {
            if ((roll -= Weight(f)) < 0)
            {
                pick = f;
                break;
            }
        }
        _lastActivity = pick.Id;
        StartScene(pick, 180 + _rng.Next(180));
    }

    /// <summary>„Почивам си“ (от разговора): и той си почива в някое от кътчетата си.</summary>
    public void RestToo()
    {
        if (InFocus)
        {
            Say("Първо работата! После почивка.", 3);
            return;
        }
        Say(Lines.Pick(Lines.RestToo, _save.OwnerName), 4);
        _idleSince = Now;
        if (_inScene) LeaveScene();
        StartFun();
    }

    /// <summary>Случайно отключено кътче (за „Почивай си → Случайно“).</summary>
    public Scene RandomUnlockedFun()
    {
        var owned = Scenes.Fun.Where(f => Scenes.IsUnlocked(f, _save)).ToList();
        return owned[_rng.Next(owned.Count)];
    }

    /// <summary>Чуди се какво да прави; понякога мечтае за нещо от магазина.</summary>
    private void Wonder()
    {
        var missing = Kitchen.Upgrades.Where(u => u.Shelf == Kitchen.FunShelf && !Kitchen.Owns(_save, u.Id)).ToList();
        string line = missing.Count > 0 && _rng.Next(3) == 0
            ? $"Ех, да имах {missing[_rng.Next(missing.Count)].Name.ToLowerInvariant()}…"
            : Lines.Pick(Lines.Wondering, _save.OwnerName);
        Say(line, 4);
        Play(_rng.Next(2) == 0 ? "think" : "idle", length: 3 + _rng.Next(3));
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
