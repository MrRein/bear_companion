using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace Mecho;

// Дребни неща, които му дават живот: сутрешен ритуал, любимо място (всеки ден
// различно), поглежда към курсора, хълца и киха, „Ехо, тук ли си?“, радва се,
// като се върнеш, гости (пчеличка, катеричка), говори за времето навън, намира
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
        UpdateSkyTalk(now);
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
        _nextFavSpot = Now + Minutes(45, 90);
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
        if (_state != BearState.Idle || _inScene || IsAsleep) return;
        double dx = c.X - _x, dy = c.Y - (_y - SpriteHeight / 2);
        if (Math.Abs(dx) > 260 || Math.Abs(dy) > 220 || Math.Abs(dx) < 12) return;
        // Мишката профуча покрай него: стряска се (рядко).
        if (speed > 2500 && Math.Abs(dx) < 120 && now > _nextStartle && !IsQuiet)
        {
            _nextStartle = now + Minutes(8, 15);
            _facingLeft = dx < 0;
            Say(R.Any("cursor.startle"), 2.5);
            Play("think", length: 1.2);
            return;
        }
        _facingLeft = dx < 0;
    }

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

    // ── 10. Гости: пчеличка и катеричка ──

    private CookerWindow? _visitor;
    private string _visitorKind = "";
    private double _nextVisitor = 40 * 60, _visitorX, _visitorY, _visitorDir, _visitorTime, _visitorPause;
    private bool _visitorGreeted;

    private void UpdateVisitor(double now)
    {
        if (_visitor == null)
        {
            if (now < _nextVisitor) return;
            _nextVisitor = now + Minutes(60, 150);
            if (!IsVisible || IsAsleep) return;
            StartVisitor(_rng.Next(2) == 0 ? "bee" : "squirrel");
            return;
        }
        double dt = 0.033;
        _visitorTime += dt;
        var area = Area;
        bool bee = _visitorKind == "bee";
        if (_visitorPause > 0) _visitorPause -= dt;
        else _visitorX += _visitorDir * (bee ? 70 : 150) * dt;
        // Спира за малко до мечока (пчеличката пърха около главата му, катеричката сяда).
        if (!_visitorGreeted && Math.Abs(_visitorX - _x) < (bee ? 90 : 140))
        {
            _visitorGreeted = true;
            _visitorPause = bee ? 4 : 2.5;
            GreetVisitor();
        }
        _visitorY = bee
            ? area.Bottom - SpriteHeight * 0.9 + Math.Sin(_visitorTime * 3) * 18 + Math.Sin(_visitorTime * 11) * 3
            : area.Bottom;
        string art = _visitorKind + ((int)(_visitorTime * (bee ? 12 : _visitorPause > 0 ? 0 : 8)) % 2 == 0 ? "" : "_b");
        var img = _lib.GetProp(art, bee ? 11 : 18, bee ? 11 : 16, 0).Image;
        _visitor.Place(img, PixelSize, _visitorX, _visitorY);
        if (_visitor.Content is FrameworkElement fe) fe.LayoutTransform = new ScaleTransform(_visitorDir < 0 ? -1 : 1, 1);
        _visitor.SetAway(!IsVisible);
        if (_visitorX < area.Left - 60 || _visitorX > area.Right + 60) EndVisitor();
    }

    private void StartVisitor(string kind)
    {
        var area = Area;
        _visitorKind = kind;
        _visitorDir = _rng.Next(2) == 0 ? 1 : -1;
        _visitorX = _visitorDir > 0 ? area.Left - 40 : area.Right + 40;
        _visitorTime = 0;
        _visitorPause = 0;
        _visitorGreeted = false;
        _visitor = new CookerWindow(kind == "bee" ? "Пчеличка" : "Катеричка")
        {
            Hint = kind == "bee" ? "Пчеличка! Цъкни я." : "Катеричка! Цъкни я.",
        };
        _visitor.SetTime(null);
        _visitor.Clicked += () => Say(R.Pick($"visit.{_visitorKind}.click", _save.OwnerName), 3);
    }

    private void GreetVisitor()
    {
        if (IsQuiet && InFocus) return;
        if (!InFocus) Say(R.Pick($"visit.{_visitorKind}", _save.OwnerName), 4);
        if (_state is BearState.Idle or BearState.Walk && !_inScene)
        {
            _facingLeft = _visitorX < _x;
            Play("happy", after: () => _facingLeft = _visitorX < _x);
        }
        else if (!_inScene || _scene == Scenes.Sit)
            _facingLeft = _visitorX < _x;
    }

    private void EndVisitor()
    {
        _visitor?.Close();
        _visitor = null;
        if (_visitorGreeted && !InFocus && !IsAsleep) Later(1, () => Mutter(R.Pick($"visit.{_visitorKind}.gone", _save.OwnerName), 3));
    }

    // ── 11. Приказки за времето навън ──

    private double _nextSkyTalk = 20 * 60;

    private void UpdateSkyTalk(double now)
    {
        if (now < _nextSkyTalk) return;
        _nextSkyTalk = now + Minutes(45, 100);
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
        if (_rng.Next(4) != 0) return null;
        int joy = JoyToday;
        if (joy >= 3) return R.Pick("mood.great", _save.OwnerName);
        if (joy == 0 && DateTime.Now.Hour >= 13) return R.Pick("mood.quiet", _save.OwnerName);
        return null;
    }
}
