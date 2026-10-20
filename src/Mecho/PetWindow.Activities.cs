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
    private OverlayWindow _butterflyWindow = null!;
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
        // Пеперудата лети в свой малък прозорец: така прозорецът на мечока не се
        // прерисува целият 30 пъти в секунда, докато тя пърха.
        _butterflyWindow = new OverlayWindow(_butterfly);
    }

    /// <summary>Картинка на предмет (храна, уред) за панела.</summary>
    public System.Windows.Media.Imaging.BitmapSource PropImage(string name) => _lib.GetProp(name, 16, 16, 0).Image;

    /// <summary>
    /// Храната в менюто: цялото ястие (food_&lt;id&gt;_menu.png – цяла пица, кофичка пуканки…),
    /// а сготвена излиза на парчета (food_&lt;id&gt;.png). Без _menu – същата рисунка.
    /// </summary>
    public System.Windows.Media.Imaging.BitmapSource FoodMenuImage(Food f) =>
        PropImage(_lib.HasProp($"food_{f.ArtId}_menu") ? $"food_{f.ArtId}_menu" : "food_" + f.ArtId);

    /// <summary>Портрет за панела: първият кадър от „стои“, облечен.</summary>
    public System.Windows.Media.Imaging.BitmapSource Portrait => _dresser.Frame(_lib, "idle", 0, EffectiveOutfit);

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
        // Дълго без работа: сяда, а после прави някое хоби (виж PetWindow.Free.cs).
        if (FreeCycleStep()) return;

        bool tired = IsExhausted; // уморен: не му се играе, по-скоро се прозява
        bool near = Cooks;        // готви: не отива далеч от уреда
        var choices = new (string Name, double Weight, Action Do)[]
        {
            ("walk", tired ? 10 : 26, () =>
            {
                double x = WalkTarget();
                if (Math.Abs(x - _x) > 30) WalkTo(x);
                else SetIdle();
            }),
            ("wonder", 10, Wonder),
            ("climb", tired || near || !HasPlatforms ? 0 : 8, Climb),
            ("sketch", tired ? 1 : 5, () => Play("write", length: 25 + _rng.Next(35), after: () => Announce(Lines.Pick(Lines.SketchDone, _save.OwnerName), 5))),
            ("dice", tired ? 0 : 4, () =>
            {
                int n = _rng.Next(1, 7);
                Play("dice", after: () => Announce(Lines.DiceRoll(n) + " " + Lines.Pick(Lines.Playtest, _save.OwnerName), 5));
            }),
            ("dance", tired ? 0 : 3 * JoyBoost, () =>
            {
                Play("dance", length: 8 + _rng.Next(10));
                Announce(Lines.Pick(Lines.Dancing, _save.OwnerName), 3);
            }),
            ("snack", _save.Fullness < 90 ? 2 : 0, () =>
            {
                _held.Source = null;   // трохичка: нищо в лапите
                Play("eat", length: 2.5);
                Announce(Lines.Pick(Lines.Snack, _save.OwnerName), 3);
                _save.Fullness = Math.Min(100, _save.Fullness + 1);
            }),
            ("stretch", tired ? 10 : _save.Energy < 70 ? 3 : 1, () =>
            {
                Play("yawn");
                Announce(tired ? Lines.Pick(Lines.Sleepy, _save.OwnerName) : "*протяга се* Ааах.", 3);
            }),
            ("cursor", tired || near ? 0 : 5, FollowCursor),
            ("butterfly", tired || near ? 0 : 5, StartChase),
            ("peek", tired || near ? 0 : 3 * JoyBoost, Peekaboo),
            ("juggle", tired ? 0 : 3 * JoyBoost, Juggle),
            ("favspot", near ? 0 : FavSpotWeight, GoToFavSpot),
            ("hiccup", 1.2, Hiccup),
            ("sneeze", SneezeWeight, Sneeze),
            ("find", tired ? 1 : 3, FindSomething),
            ("idle", 22, SetIdle),
            // Печицата грее: когато няма какво да прави, отива и сяда до нея –
            // колкото по-студено, толкова по-често.
            ("warm", _heaterWindow == null || near ? 0 : Cold ? 10 + ColdLevel * 4 : 5, WarmUpByHeater),
            // Радиото свири: ако има енергия и не работи, понякога отива да танцува до него.
            // Хранилката е празна: понякога сам отива да сложи семенца.
            ("feeder", !FeederPlaced || _save.FeederFood > 0 || near || InFocus ? 0 : 4, FillFeeder),
            ("radio", !RadioPlaying || near || InFocus || _save.Energy < 40 ? 0 : 6 * JoyBoost, DanceByRadio),
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
                // Уморен е: прави го, но казва, че му се спи.
                if (tired && c.Name is "walk" or "wonder" or "sketch" or "dice" && _rng.Next(3) == 0)
                    Announce(Lines.Pick(Lines.SleepyDoing, _save.OwnerName), 3);
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
        var owned = Scenes.Fun.Where(f => CanDo(f) && f.Id != _lastActivity).ToList();
        if (owned.Count == 0) owned.Add(Scenes.Reading);
        double Weight(Scene f) =>
            (morning && (f == Scenes.Yoga || f == Scenes.Plant) ? 3 : 1) *
            (evening && (f == Scenes.Reading || f == Scenes.Music || f == Scenes.Knit || f == Scenes.Guitar) ? 3 : 1) *
            (IsExhausted && f == Scenes.Reading ? 3 : 1);
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
            Say(R.Any("work.first"), 3);
            return;
        }
        Say(Lines.Pick(Lines.RestToo, _save.OwnerName), 4);
        _idleSince = Now;
        if (_inScene) LeaveScene();
        StartFun();
    }

    /// <summary>Случайно отключено кътче.</summary>
    public Scene RandomUnlockedFun()
    {
        var owned = Scenes.Fun.Where(CanDo).ToList();
        return owned[_rng.Next(owned.Count)];
    }

    /// <summary>Чуди се какво да прави; понякога мечтае за нещо от магазина.</summary>
    private void Wonder()
    {
        var missing = Kitchen.Upgrades.Where(u => u.Shelf == Kitchen.FunShelf && !Kitchen.Owns(_save, u.Id)).ToList();
        string line = missing.Count > 0 && _rng.Next(3) == 0
            ? $"Ех, да имах {missing[_rng.Next(missing.Count)].Name.ToLowerInvariant()}…"
            : Lines.Pick(Lines.Wondering, _save.OwnerName);
        Announce(line, 4);
        bool thinks = _rng.Next(2) == 0;
        Play(thinks ? "think" : "idle", length: 3 + _rng.Next(3));
        if (thinks) Emote("think", 3);
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
                    Announce(Lines.Pick(Lines.Peekaboo, _save.OwnerName), 3);
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
        _juggleUntil = Now + 10 + _rng.NextDouble() * 8;
        Announce(Lines.Pick(Lines.JuggleStart, _save.OwnerName), 3);
        Play("juggle", length: _juggleUntil - Now, after: () => Announce(Lines.Pick(Lines.JuggleEnd, _save.OwnerName), 3));
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
            Announce(Lines.Pick(Lines.CursorFound, _save.OwnerName), 4);
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
        // Всеки път пеперуда с различен цвят.
        string[] colors = { "", "_yellow", "_blue", "_orange", "_white", "_green" };
        _butterflyProp = _lib.GetProp("butterfly" + colors[_rng.Next(colors.Length)], 9, 7, 0);
        _butterfly.Source = _butterflyProp.Image;
        _chaseLength = 10 + _rng.NextDouble() * 8;
        // Долита отстрани.
        int side = _rng.Next(2) == 0 ? -1 : 1;
        _bfX = Math.Clamp(_x + side * (ButterflyReach + 10), area.Left + 20, area.Right - 20);
        _bfY = area.Bottom - 180;
        _bfTargetX = _x;
        _bfVx = 0;
        _bfVy = 0;
        Announce(Lines.Pick(Lines.ButterflySeen, _save.OwnerName), 3);
        SetState(BearState.Chase, "walk");
    }

    private void UpdateChase(double dt)
    {
        if (!_bfActive)
        {
            // Пеперудата отлетя.
            Announce(Lines.Pick(_rng.Next(3) == 0 ? Lines.ButterflyCaught : Lines.ButterflyGone, _save.OwnerName), 4);
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

        // Тича след пеперудата, но спира, ако е точно под нея. С „мъртва зона“: спира
        // под 12, а тръгва чак над 40 – иначе на ръба сменяше „тича“/„радва се“ всеки кадър.
        double gap = Math.Abs(_bfX - _x);
        if (gap > 40 || (gap > 12 && _anim == "walk"))
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
            if (_bfY < area.Top + 6 || _bfY < _y - 600) _bfActive = false;
        }
        else
        {
            // Пърха насам-натам около мечока, на нивото на главата му.
            double reach = ButterflyReach;
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

    /// <summary>Докъде пърха пеперудата около мечока (в DIP).</summary>
    private double ButterflyReach => 260 * Math.Max(1, BearScale / 3.0);

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
