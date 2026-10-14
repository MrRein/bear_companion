using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Mecho;

// Дребни неща, които му дават живот: сутрешен ритуал, любимо място (всеки ден
// различно), поглежда към курсора, хълца и киха, „Ехо, тук ли си?“, радва се,
// като се върнеш, гост (пчеличка), говори за времето навън, намира
// си неща и има леко настроение за деня (по-весел, ако е галян, нахранен и сте
// работили заедно; никога тъжен).
public sealed partial class PetWindow
{
    // ── Отложени реплики/действия (хълцане, кихане, ритуал) ──

    private readonly List<(double At, Action Do)> _later = new();

    private void Later(double seconds, Action action) => _later.Add((Now + seconds, action));

    private void UpdateLife(double now)
    {
        for (int i = _later.Count - 1; i >= 0; i--)
        {
            if (now < _later[i].At) continue;
            var a = _later[i].Do;
            _later.RemoveAt(i);
            a();
        }
        UpdateCursorLook(now);
        UpdatePresence(now);
        UpdateVisitor(now);
        UpdateDelivery(now);
        UpdateSkyTalk(now);
        UpdateWishes(now);
    }

    // ── 1. Сутрешен ритуал ──

    /// <summary>Първото пускане сутрин (5–12 ч.): прозява се, протяга се и тогава поздравява.</summary>
    private bool MorningRitual()
    {
        string today = DateTime.Today.ToString("yyyy-MM-dd");
        int h = DateTime.Now.Hour;
        if (h is < 5 or >= 12 || _save.RitualDay == today) return false;
        _save.RitualDay = today;
        Say(R.Any("ritual.wake"), 3);
        Play("yawn", after: () =>
        {
            Say(R.Any("ritual.stretch"), 3);
            Play("yoga", length: 2.5, after: () =>
            {
                Say(Lines.Greeting(_save.OwnerName) + " " + R.Any("ritual.ready"), 5);
                Play("happy");
            });
        });
        return true;
    }

    // ── 2. Любимо място за деня ──

    private double _nextFavSpot;

    /// <summary>Мястото, което днес му харесва: близо до левия или десния край, всеки ден различно.</summary>
    private double FavSpotX
    {
        get
        {
            var area = Area;
            var day = new Random(DateTime.Today.Year * 1000 + DateTime.Today.DayOfYear + 7);
            double edge = area.Width * (0.04 + day.NextDouble() * 0.1);
            double x = day.Next(2) == 0 ? area.Left + edge : area.Right - edge;
            return Math.Clamp(x, area.Left + HalfWidth, area.Right - HalfWidth);
        }
    }

    private double FavSpotWeight => Now < _nextFavSpot || Math.Abs(_x - FavSpotX) < 80 ? 0 : 4;

    private void GoToFavSpot()
    {
        _nextFavSpot = Now + Minutes(90, 150);
        WalkTo(FavSpotX, () =>
        {
            Say(R.Pick("favspot", _save.OwnerName), 4);
            Play("happy", after: () => SetState(BearState.Idle, "idle", 15 + _rng.Next(20)));
        });
    }

    // ── 3. Поглежда към курсора; бързо движение близо до него го стряска ──

    private Point _lastCursor;
    private double _lastCursorAt, _nextStartle = 120;

    private void UpdateCursorLook(double now)
    {
        if (now - _lastCursorAt < 0.1) return;
        double dt = now - _lastCursorAt;
        _lastCursorAt = now;
        double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var pos = System.Windows.Forms.Cursor.Position;
        var c = new Point(pos.X / dpi, pos.Y / dpi);
        double speed = (c - _lastCursor).Length / Math.Max(dt, 0.05);
        _lastCursor = c;
        // Само когато стои спокойно поне 3 с (иначе би се обърнал веднага след
        // като се е обърнал към кашона, уреда… – премигване).
        if (_state != BearState.Idle || _inScene || IsAsleep || _stateTime < 3) return;
        double dx = c.X - _x, dy = c.Y - (_y - SpriteHeight / 2);
        if (Math.Abs(dx) > 260 || Math.Abs(dy) > 220 || Math.Abs(dx) < 40)
        {
            _lookSince = -1;
            return;
        }
        // Мишката профуча покрай него: стряска се (рядко).
        if (speed > 2500 && Math.Abs(dx) < 120 && now > _nextStartle && !IsQuiet)
        {
            _nextStartle = now + Minutes(8, 15);
            _facingLeft = dx < 0;
            _lastTurn = now;
            Say(R.Any("cursor.startle"), 2.5);
            Play("think", length: 1.2);
            return;
        }
        // Обръща се към мишката само ако тя стои от другата му страна поне 0,8 с и
        // не по-често от веднъж на 2,5 с – иначе се върти напред-назад.
        bool left = dx < 0;
        if (left == _facingLeft)
        {
            _lookSince = -1;
            return;
        }
        if (_lookSince < 0) _lookSince = now;
        if (now - _lookSince < 0.8 || now - _lastTurn < 2.5) return;
        _facingLeft = left;
        _lastTurn = now;
        _lookSince = -1;
    }

    private double _lookSince = -1, _lastTurn;

    // ── 4. Хълцане и кихане ──

    private void Hiccup()
    {
        int n = 3 + _rng.Next(3);
        Say(R.Any("hiccup.start"), 1.5);
        Play("idle", length: 1.6 * n + 1);
        for (int i = 1; i < n; i++) Later(1.6 * i, () => Say(R.Any("hiccup.mid"), 1.2));
        Later(1.6 * n, () => Say(R.Pick("hiccup.end", _save.OwnerName), 3));
    }

    private void Sneeze()
    {
        Say(R.Any("sneeze.build"), 1.4);
        Play("think", length: 1.4, after: () =>
        {
            Say(R.Any("sneeze.achoo"), 1.4);
            Play("happy", after: () => Say(R.Pick("sneeze.end", _save.OwnerName), 3));
        });
    }

    private double SneezeWeight => DateTime.Now.Month is 12 or 1 or 2 || Cold ? 2 : 0.8;

    // ── 5–6. „Ехо, тук ли си?“ и радост, когато се върнеш ──

    private double _awaySince = -1, _nextEcho;
    private int _echoes;

    private void UpdatePresence(double now)
    {
        double idle = NativeMethods.IdleSeconds();
        if (idle > 60)
        {
            if (_awaySince < 0) _awaySince = now - idle;
            // Дълго не си мърдала мишката: „Ехо, тук ли си?“ (след 20 мин, после на 40, най-много 3 пъти).
            if (idle > 20 * 60 && now > _nextEcho && _echoes < 3 && !IsAsleep && !InFocus && !IsQuiet)
            {
                _nextEcho = now + 40 * 60;
                _echoes++;
                Say(R.Pick("away.echo", _save.OwnerName), 6);
                if (CanAnimateFreely && !_inScene) Play("think", length: 2);
            }
            return;
        }
        if (_awaySince < 0) return;
        double away = now - _awaySince;
        _awaySince = -1;
        _echoes = 0;
        _nextEcho = 0;
        if (away < 30 * 60 || IsAsleep || IsQuiet) return;
        // Върна се след 30+ мин: радва се (а ако е бил цял ден/нощ – липсвала му е).
        string key = away > 6 * 3600 ? "welcome.missed" : "welcome.back";
        Say(R.Pick(key, _save.OwnerName), 5);
        if (_inScene || InFocus || !CanAnimateFreely) return;
        double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var pos = System.Windows.Forms.Cursor.Position;
        var area = Area;
        double cx = Math.Clamp(pos.X / dpi, area.Left + HalfWidth, area.Right - HalfWidth);
        if (Math.Abs(cx - _x) > 60 && area.Contains(new Point(pos.X / dpi, pos.Y / dpi)))
            WalkTo(cx, () => Play("happy"));
        else
            Play("happy");
    }

    // ── 10. Гост: пчеличка (като пеперудата: долита, покръжи около главата му и отлита) ──

    private CookerWindow? _visitor;
    private double _nextVisitor = 40 * 60, _visitorX, _visitorY, _visitorDir, _visitorTime, _visitorHover, _visitorLast;
    private int _visitorPhase;          // 0 – долита, 1 – кръжи около главата му, 2 – отлита
    private bool _visitorGreeted;

    private void UpdateVisitor(double now)
    {
        if (_visitor == null)
        {
            if (now < _nextVisitor) return;
            _nextVisitor = now + Minutes(60, 150);
            if (!IsVisible || IsAsleep) return;
            StartVisitor();
            return;
        }
        double dt = Math.Clamp(now - _visitorLast, 0, 0.1);
        _visitorLast = now;
        _visitorTime += dt;
        var area = Area;
        double headY = _y - SpriteHeight * 0.85;
        switch (_visitorPhase)
        {
            case 0:
                _visitorX += _visitorDir * 230 * dt;
                _visitorY = headY - 20 + Math.Sin(_visitorTime * 4) * 14;
                if (Math.Abs(_visitorX - _x) < 70)
                {
                    _visitorPhase = 1;
                    _visitorHover = 2.5 + _rng.NextDouble();
                    _visitorGreeted = true;
                    GreetVisitor();
                }
                break;
            case 1:
                _visitorHover -= dt;
                _visitorX = _x + Math.Sin(_visitorTime * 3.2) * 55;
                _visitorY = headY - 25 + Math.Cos(_visitorTime * 6.4) * 14;
                _visitorDir = Math.Cos(_visitorTime * 3.2) >= 0 ? 1 : -1;
                if (_visitorHover <= 0) _visitorPhase = 2;
                break;
            default:
                _visitorX += _visitorDir * 200 * dt;
                _visitorY -= 170 * dt;
                break;
        }
        string art = (int)(_visitorTime * 14) % 2 == 0 ? "bee" : "bee_b";
        // Първо „скрит ли е мечокът“, после мястото – иначе скрита пчеличка се
        // показваше за кадър и пак се скриваше (премигване).
        _visitor.SetAway(!IsVisible);
        _visitor.Place(_lib.GetProp(art, 11, 11, 0).Image, PixelSize, _visitorX, _visitorY);
        var flip = _visitorDir < 0 ? FlipLeft : Transform.Identity;
        if (_visitor.Content is FrameworkElement fe && !ReferenceEquals(fe.LayoutTransform, flip)) fe.LayoutTransform = flip;
        if (_visitorY < area.Top - 40 || _visitorX < area.Left - 60 || _visitorX > area.Right + 60) EndVisitor();
    }

    private void StartVisitor()
    {
        var area = Area;
        _visitorDir = _x > area.Left + area.Width / 2 ? 1 : -1;     // идва откъм по-далечния край
        _visitorX = _visitorDir > 0 ? area.Left - 30 : area.Right + 30;
        _visitorTime = 0;
        _visitorPhase = 0;
        _visitorLast = Now;
        _visitorGreeted = false;
        _visitor = new CookerWindow("Пчеличка") { Hint = "Пчеличка! Цъкни я." };
        _visitor.SetTime(null);
        _visitor.Clicked += () => Say(R.Pick("visit.bee.click", _save.OwnerName), 3);
    }

    private void GreetVisitor()
    {
        if (IsQuiet || InFocus) return;
        if (_rng.Next(3) != 0) Say(R.Pick("visit.bee", _save.OwnerName), 3);
        if (_state is BearState.Idle or BearState.Walk && !_inScene)
        {
            _facingLeft = _visitorX < _x;
            Play("happy");
        }
    }

    private void EndVisitor()
    {
        _visitor?.Close();
        _visitor = null;
    }

    // ── 11. Приказки за времето навън ──

    private double _nextSkyTalk = 20 * 60;

    private void UpdateSkyTalk(double now)
    {
        if (now < _nextSkyTalk) return;
        _nextSkyTalk = now + Minutes(150, 270);
        if (Weather.Kind(_tempC) is not { } kind || IsAsleep || InFocus) return;
        Mutter(R.Pick("weather." + kind, _save.OwnerName), 5);
    }

    // ── 12. Намира си неща ──

    private void FindSomething()
    {
        Play("think", length: 1.5, after: () =>
        {
            Say(R.Pick("found", _save.OwnerName), 4);
            Play("happy");
        });
    }

    // ── 13. Настроение за деня ──

    private const int JoyPetted = 1, JoyFed = 2, JoyWorked = 4;

    private void Joy(int flag)
    {
        string today = DateTime.Today.ToString("yyyy-MM-dd");
        if (_save.JoyDay != today)
        {
            _save.JoyDay = today;
            _save.JoyFlags = 0;
        }
        _save.JoyFlags |= flag;
    }

    /// <summary>Колко хубаво му е днес: 0–3 (галиха го, нахраниха го, работиха заедно).</summary>
    private int JoyToday
    {
        get
        {
            if (_save.JoyDay != DateTime.Today.ToString("yyyy-MM-dd")) return 0;
            int f = _save.JoyFlags;
            return (f & 1) + ((f >> 1) & 1) + ((f >> 2) & 1);
        }
    }

    /// <summary>Множител за веселите неща (танц, жонглиране, наднича): по-весел, когато днес му е хубаво.</summary>
    private double JoyBoost => JoyToday switch { >= 3 => 2.2, 2 => 1.5, 1 => 1, _ => DateTime.Now.Hour >= 12 ? 0.5 : 1 };

    /// <summary>Понякога вместо обикновена приказка казва как се чувства днес.</summary>
    private string? MoodLine()
    {
        // Понякога казва какво му се яде днес (само от отключеното).
        if (_rng.Next(5) == 0 && Kitchen.Cravings(_save) is { Length: > 0 } c)
            return R.Pick("craving", _save.OwnerName).Replace("{ястие}", c[_rng.Next(c.Length)].Name.ToLowerInvariant());
        if (_rng.Next(4) != 0) return null;
        int joy = JoyToday;
        if (joy >= 3) return R.Pick("mood.great", _save.OwnerName);
        if (joy == 0 && DateTime.Now.Hour >= 13) return R.Pick("mood.quiet", _save.OwnerName);
        return null;
    }

    // ── Желания: гардеробът е с предимство; иначе – нещо, за което стигат лешниците ──

    private double _nextWish = 60 * 60;

    /// <summary>
    /// Целта в магазина: стигнат ли лешниците – казва го веднага (веднъж); иначе
    /// рядко (на 2–4 ч) споменава за какво събираме.
    /// </summary>
    private void UpdateWishes(double now)
    {
        if (IsQuiet || InFocus || IsAsleep) return;
        if (!_goalReadySaid && Goal is { } d && _save.Hazelnuts >= d.Price && now > 20)
        {
            _goalReadySaid = true;
            Say(R.Pick("goal.ready", _save.OwnerName).Replace("{нещо}", $"{d.Icon} {d.Name.ToLowerInvariant()}"), 6);
            return;
        }
        if (now < _nextWish) return;
        _nextWish = now + Minutes(120, 240);
        if (Cooks) return;
        TalkAboutGoal();
    }
}
