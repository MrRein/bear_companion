using System;
using System.Linq;

namespace Mecho;

/// <summary>Какъв ден е за мечока (избира се сутрин; вижда се само в Developer).</summary>
public enum DayKind { Normal, Energetic, SleepyNoon, BadNight, NightOwl, Playful, Lazy }

// Умора: енергията пада според това какво прави (работа уморява най-много),
// часа (сутрин е свеж, следобед отпада, вечер му се доспива) и какъв ден е.
// „Почивай си“ я вдига – толкова повече, колкото е по-уморен (до 80; догоре само
// със сън). Всеки ден е малко различен: вид на деня, случайни ±15%, плюс
// ситуации – претоварен, „в ритъм“, гордост, преял, студ, галене, късно кафе.
// Виж BEHAVIOR.md, „Енергия и умора“.
public sealed partial class PetWindow
{
    // Колко уморява (точки на час, преди множителите).
    private const double DrainFocus = 12, DrainSelfWork = 9, DrainAwake = 4;

    /// <summary>Почивката (без сън) вдига най-много дотук.</summary>
    private const double RestCap = 80;

    /// <summary>Капнал (под 15): почивката го вдига само до 40, докато не поспи.</summary>
    private const double DeepTiredCap = 40;

    public static string DayKindName(DayKind k) => k switch
    {
        DayKind.Energetic => "Енергичен",
        DayKind.SleepyNoon => "Сънлив обяд",
        DayKind.BadNight => "Не е спал добре",
        DayKind.NightOwl => "Нощна птица",
        DayKind.Playful => "Игрив",
        DayKind.Lazy => "Мързелив",
        _ => "Обикновен",
    };

    private static string Day(DateTime d) => d.ToString("yyyy-MM-dd");

    /// <summary>Видът на днешния ден (избира се при първото питане за деня).</summary>
    public DayKind Today
    {
        get
        {
            EnsureDayKind();
            return Enum.TryParse<DayKind>(_save.DayKind, out var k) ? k : DayKind.Normal;
        }
    }

    private void EnsureDayKind()
    {
        string today = Day(DateTime.Today);
        if (_save.DayKindDate != today) RollOverDay(today);
    }

    /// <summary>Нов ден: вчерашната работа влиза в „умората от седмицата“ и се избира видът на деня.</summary>
    private void RollOverDay(string today)
    {
        if (DateTime.TryParse(_save.DayKindDate, out var last))
        {
            double worked = _save.WorkMinutesToday;
            double load = _save.Fatigue + (worked >= 150 ? 1 : worked < 60 ? -1 : 0);
            if (_save.DayKind == nameof(DayKind.Lazy)) load -= 1;
            // Дни без мечока: почивал е.
            load -= Math.Max(0, (DateTime.Today - last.Date).TotalDays - 1);
            _save.Fatigue = Math.Clamp(load, 0, 5);
            _save.WasDrained = _save.DayLowEnergy < 15;
        }
        var kind = PickDayKind(DateTime.Today);
        _save.DayKind = kind.ToString();
        _save.DayKindDate = today;
        if (kind == DayKind.Lazy) _save.LastLazy = today;
        _save.WorkMinutesToday = 0;
        _save.DayLowEnergy = 100;
        _flowsToday = 0;
        _noonNapAsked = false;
    }

    /// <summary>
    /// Избира деня. Повече работа през седмицата → по-често мързелив/сънлив; добре
    /// отпочинал → по-често енергичен. Мързелив не два дни подред.
    /// </summary>
    private DayKind PickDayKind(DateTime date)
    {
        double f = _save.Fatigue;
        bool weekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        bool lazyYesterday = _save.LastLazy == Day(date.AddDays(-1));
        bool lateBed = _save.WentToBed >= date && _save.WentToBed < date.AddHours(6);
        var weights = new (DayKind Kind, double W)[]
        {
            (DayKind.Normal, 50),
            (DayKind.Energetic, Math.Max(3, 15 + (f <= 0 ? 10 : 0) - (f >= 3 ? 10 : 0))),
            (DayKind.SleepyNoon, 12 + (f >= 2 ? 5 : 0)),
            (DayKind.BadNight, 8 + (_save.WasDrained || lateBed ? 10 : 0)),
            (DayKind.NightOwl, 5),
            (DayKind.Playful, 5 + (weekend ? 5 : 0)),
            (DayKind.Lazy, lazyYesterday ? 0 : 5 + (weekend ? 5 : 0) + (f >= 3 ? 5 : 0) + (date.DayOfWeek == DayOfWeek.Friday ? 3 : 0)),
        };
        double roll = _rng.NextDouble() * weights.Sum(w => w.W);
        foreach (var (kind, w) in weights)
        {
            if (roll < w) return kind;
            roll -= w;
        }
        return DayKind.Normal;
    }

    /// <summary>Самопроверка и Developer: слага вида на деня.</summary>
    internal void SetDayKind(DayKind kind)
    {
        EnsureDayKind();
        _save.DayKind = kind.ToString();
        NotifyCare();
    }

    /// <summary>Днешното разместване на скоростите (0,85–1,15, различно всеки ден).</summary>
    private static double DayJitter
    {
        get
        {
            var d = DateTime.Today;
            return 0.85 + new Random(d.Year * 1000 + d.DayOfYear + 31).NextDouble() * 0.3;
        }
    }

    /// <summary>
    /// Колко бързо се уморява в този час: сутрин е свеж (×0,7), следобед отпада
    /// (13–15 ч. ×1,4), вечер му се доспива (19–22 ч. ×1,5), късно е изморен (×4).
    /// Нощната птица е мудна сутрин и бодра до 23 ч.
    /// </summary>
    internal static double TimeFactor(DateTime now, DayKind kind)
    {
        double h = now.TimeOfDay.TotalHours;
        bool owl = kind == DayKind.NightOwl;
        if (h >= 22 || h < 6) return owl && h >= 22 && h < 23 ? 0.8 : 4;
        if (h >= 19) return owl ? 0.8 : 1.5;
        if (h >= 13 && h < 15) return 1.4;
        if (h < 12) return owl && h < 11 ? 1.3 : now.DayOfWeek == DayOfWeek.Monday && h < 10 ? 1.1 : 0.7;
        return 1;
    }

    private bool Working => _inScene && _scene == Scenes.Focus;

    private bool SelfWorking => _inScene && _scene != null && Scenes.Work.Contains(_scene);

    private bool HeaterWarms => _save.HeaterOn && Kitchen.Owns(_save, Kitchen.Heater);

    /// <summary>Колко се мени енергията на час точно сега (+ почива, − уморява).</summary>
    internal double EnergyRate()
    {
        if (IsAsleep) return RestRate;
        if (_anim == "read_sleep") return 0;
        var kind = Today;
        double e = _save.Energy;

        // Почива си: колкото е по-уморен, толкова повече му помага.
        var rest = _onTrampoline ? Scenes.Trampoline : _inScene && _scene != null && !Working && !SelfWorking ? _scene : null;
        if (rest != null)
        {
            double gain = e < 30 ? 20 : e < 50 ? 15 : e < RestCap ? 6 : 0;
            double factor = RestFactor(rest, e);
            if (factor < 0) return DrainAwake * -factor * TimeFactor(DateTime.Now, kind);
            if (kind == DayKind.Playful) factor *= 2;
            if (ColdLevel >= 2 && HeaterWarms) factor *= 1.3;
            return gain * factor * DayJitter;
        }

        bool work = Working || SelfWorking;
        double rate = Working ? DrainFocus : SelfWorking ? DrainSelfWork : DrainAwake;
        double f = TimeFactor(DateTime.Now, kind) * DayJitter;
        // Първите 2 часа след събуждане е свеж.
        if (kind != DayKind.NightOwl && (DateTime.Now - _save.WokeAt).TotalHours < 2) f *= 0.5;
        if (work)
        {
            if (kind == DayKind.Energetic) f *= 0.5;
            if (kind == DayKind.Lazy) f *= 1.5;
            if (InFlow) f *= 0.3;
            if (Overloaded) f *= 1.5;
        }
        if (kind == DayKind.SleepyNoon && DateTime.Now.TimeOfDay is var t && t >= new TimeSpan(12, 30, 0) && t < new TimeSpan(14, 30, 0)) f *= 2.5;
        if (ColdLevel >= 2 && !HeaterWarms) f *= 1.2;
        if (Now < _feastUntil) f *= 1.5;
        return -rate * f;
    }

    /// <summary>
    /// Колко добре почива в това кътче. Чай и диван – най-добре; йогата и батутът
    /// ободряват само бодър мечок (капнал – уморяват; отрицателно = уморява).
    /// </summary>
    private double RestFactor(Scene s, double e)
    {
        if (s == Scenes.Tea) return 1.2;
        if (s == Scenes.Reading) return HasCouch ? 1.2 : 1;
        if (s == Scenes.Music) return 1.1;
        if (s == Scenes.Plant) return 0.9;
        if (s == Scenes.Gaming) return 0.7;
        if (s == Scenes.Yoga) return e >= 50 ? 1.2 : 0.7;
        if (s == Scenes.Trampoline) return e >= 50 ? 0.5 : -1;
        return 0.5;   // седи, пие, при гардероба
    }

    /// <summary>Докъде го вдига почивката (дрямката има своя граница).</summary>
    private double EnergyCap =>
        IsAsleep ? (_save.NapUntil != DateTime.MinValue ? _save.NapCap : 100) : _deepTired ? DeepTiredCap : RestCap;

    private bool _deepTired;

    /// <summary>Вика се от UpdateCare: мени енергията и следи ситуациите през деня.</summary>
    private void UpdateEnergy(double dt, double now)
    {
        double rate = EnergyRate();
        double e = _save.Energy;
        if (rate > 0) e = Math.Max(e, Math.Min(EnergyCap, e + rate * dt / 3600));
        else e += rate * dt / 3600;
        _save.Energy = Math.Clamp(e, 0, 100);

        if (_save.Energy < 15) _deepTired = true;
        if (IsAsleep) _deepTired = false;
        if (!IsAsleep) _save.DayLowEnergy = Math.Min(_save.DayLowEnergy, _save.Energy);

        // Работа без почивка: след 90 мин е претоварен, докато не почине 10 мин.
        if (Working || SelfWorking)
        {
            _workStreak += dt;
            _restStreak = 0;
            _save.WorkMinutesToday += dt / 60;
        }
        else if ((_restStreak += dt) > 600) _workStreak = 0;
        if (Overloaded && !_overloadSaid && !IsQuiet)
        {
            _overloadSaid = true;
            Say(R.Pick("energy.overload", _save.OwnerName), 5);
        }
        if (!Overloaded) _overloadSaid = false;

        UpdateLazyGrumble(now);
        MaybeAskNap(now);
    }

    // ── Ситуации ──

    private double _workStreak, _restStreak, _flowUntil, _feastUntil, _nextPride, _nextPetEnergy, _nextLazyLine;
    private bool _overloadSaid;
    private int _flowsToday;

    /// <summary>Работи над 90 мин без почивка.</summary>
    private bool Overloaded => _workStreak > 90 * 60;

    /// <summary>„В ритъм“: работата почти не го уморява.</summary>
    private bool InFlow => Now < _flowUntil;

    /// <summary>Почва мечо-доро бодър: понякога влиза „в ритъм“ за 40 мин (до 2 пъти на ден).</summary>
    private void MaybeFlow()
    {
        if (_save.Energy < 50 || _flowsToday >= 2 || InFlow) return;
        if (_rng.NextDouble() >= (Today == DayKind.Energetic ? 0.4 : 0.2)) return;
        _flowsToday++;
        _flowUntil = Now + 40 * 60;
        Say(R.Pick("energy.flow", _save.OwnerName), 4);
    }

    /// <summary>Трудна задача или 3 мечо-дора: гордостта го ободрява (веднъж на 2 ч).</summary>
    private void Pride()
    {
        if (Now < _nextPride || IsAsleep) return;
        _nextPride = Now + 2 * 3600;
        _save.Energy = Math.Max(_save.Energy, Math.Min(90, _save.Energy + 8));
    }

    /// <summary>Преял на обяд: половин час е дремлив.</summary>
    private void MaybeFeast()
    {
        int h = DateTime.Now.Hour;
        if (_save.Fullness >= 90 && h >= 12 && h < 15) _feastUntil = Now + 1800;
    }

    /// <summary>Галене: мъничко го ободрява (веднъж на час, до 60).</summary>
    private void PetEnergy()
    {
        if (Now < _nextPetEnergy || _save.Energy >= 60) return;
        _nextPetEnergy = Now + 3600;
        _save.Energy = Math.Min(60, _save.Energy + 2);
    }

    /// <summary>Мързелив ден: работи, но от време на време мрънка.</summary>
    private void UpdateLazyGrumble(double now)
    {
        if (Today != DayKind.Lazy || !(Working || SelfWorking) || IsQuiet || now < _nextLazyLine) return;
        if (_nextLazyLine > 0) Mutter(R.Pick("lazy.during", _save.OwnerName), 4);
        _nextLazyLine = now + Minutes(10, 20);
    }

    /// <summary>Мечо-доро на мързелив ден: бонус лешник „за смелостта“.</summary>
    private int LazyBonus()
    {
        if (Today != DayKind.Lazy) return 0;
        _save.Hazelnuts++;
        return 1;
    }

    /// <summary>Реплика при почване на работа (мързелив ден – неохотна).</summary>
    private string WorkStartLine() =>
        Today == DayKind.Lazy && _rng.Next(3) > 0 ? R.Pick("lazy.work", _save.OwnerName) : Lines.Pick(Lines.FocusStart, _save.OwnerName);

    /// <summary>Сутрешната реплика според деня (не казва какъв е денят, само се усеща).</summary>
    private string MorningLine()
    {
        string key = "day." + Today.ToString().ToLowerInvariant();
        return R.Has(key) && _rng.Next(5) > 0 ? R.Any(key) : R.Any("ritual.ready");
    }

    // ── Сутрин ──

    /// <summary>
    /// Колко бодър става сутрин: легнал след 00:00 – 80, след 01:30 – 65; късно
    /// кафе – 85; не е спал добре – 70; нощна птица – 80; понеделник – 90.
    /// </summary>
    private double MorningEnergy()
    {
        var today = DateTime.Today;
        double e = 100;
        var bed = _save.WentToBed;
        if (bed >= today.AddHours(1.5) && bed < today.AddHours(6)) e = 65;
        else if (bed >= today && bed < today.AddHours(6)) e = 80;
        if (_save.LateCoffee == Day(today.AddDays(-1))) e = Math.Min(e, 85);
        e = Today switch
        {
            DayKind.BadNight => Math.Min(e, 70),
            DayKind.NightOwl => Math.Min(e, 80),
            _ => e,
        };
        if (today.DayOfWeek == DayOfWeek.Monday) e = Math.Min(e, 90);
        return e;
    }

    // ── Дрямка ──

    private double _napAskUntil, _nextNapAsk = 30 * 60;
    private int _napAskMinutes;
    private bool _noonNapAsked;

    /// <summary>За колко минути би дремнал сега (колкото по-уморен, толкова по-дълго).</summary>
    public int NapMinutes => Today == DayKind.SleepyNoon && DateTime.Now.Hour is >= 12 and < 15
        ? new[] { 15, 20, 25 }[DateTime.Today.DayOfYear % 3]
        : _save.Energy < 15 ? 30 : _save.Energy < 25 ? 20 : 15;

    /// <summary>
    /// Денем (11–18 ч.), уморен (или в сънлив обяд) – пита дали да дремне за
    /// определено време. Не по-често от веднъж на 2 ч.
    /// </summary>
    private void MaybeAskNap(double now)
    {
        if (now < _nextNapAsk || IsResting || IsQuiet || _state == BearState.Drag || Working) return;
        var t = DateTime.Now.TimeOfDay;
        if (t < TimeSpan.FromHours(11) || t >= TimeSpan.FromHours(18)) return;
        bool noon = Today == DayKind.SleepyNoon && !_noonNapAsked && t >= new TimeSpan(12, 45, 0) && t < new TimeSpan(14, 30, 0) && _save.Energy < 60;
        if (!noon && _save.Energy >= 30) return;
        if (noon) _noonNapAsked = true;
        AskNap(NapMinutes);
    }

    private void AskNap(int minutes)
    {
        _nextNapAsk = Now + 2 * 3600;
        _napAskMinutes = minutes;
        _napAskUntil = Now + 150;
        Say(R.Pick("nap.ask", _save.OwnerName).Replace("{число}", minutes.ToString()), 8);
    }

    /// <summary>Клик скоро след „Да дремна ли…“: въпрос с бутони.</summary>
    private bool TakeNapQuestion()
    {
        if (Now > _napAskUntil) return false;
        _napAskUntil = 0;
        int m = _napAskMinutes;
        Ask(new ChatQuestion($"Да дремна ли {m} минути?",
            new ChatOption("😴 Да, дремни", p => p.TakeNap(m)),
            new ChatOption("☕ Не сега", p => p.Say(R.Any("nap.no"), 3))));
        return true;
    }

    /// <summary>Дрямка за толкова минути: става сам, по-бодър (но не догоре).</summary>
    public void TakeNap(int minutes)
    {
        if (IsAsleep || BusyEating()) return;
        if (_cook != null || _pendingCook != null)
        {
            Say(Lines.Pick(Lines.NoSleepCooking, _save.OwnerName), 4);
            return;
        }
        if (_save.Energy >= 90)
        {
            Say(R.Any("sleep.nottired"), 3);
            return;
        }
        if (_inScene) LeaveScene();
        _save.NapUntil = DateTime.Now.AddMinutes(minutes);
        _save.NapCap = Math.Min(100, _save.Energy + minutes * 1.8 + 5);
        _save.SleepingByChoice = true;
        Say(R.Pick("nap.going", _save.OwnerName).Replace("{число}", minutes.ToString()), 4);
        Play("yawn", then: "sleep");
        Persist();
    }

    /// <summary>Вика се от UpdateSleep: дрямката свърши ли – става.</summary>
    private bool NapOver()
    {
        if (_save.NapUntil == DateTime.MinValue || DateTime.Now < _save.NapUntil) return false;
        _save.NapUntil = DateTime.MinValue;
        _save.SleepingByChoice = false;
        Say(R.Pick("nap.woke", _save.OwnerName), 5);
        Play("happy");
        NotifyCare();
        return true;
    }

    // ── Почивка при мечо-доро ──

    private bool _breakRelax;

    /// <summary>Почивка при мечо-доро: сам сяда в „Почивай си“ – уморен на чай/четене, бодър на йога/батут.</summary>
    private void StartBreakRelax()
    {
        LeaveWorkMode();
        if (!CanAnimateFreely || IsAsleep) return;
        double seconds = Math.Max(30, (_save.PomodoroEndsAt - DateTime.Now).TotalSeconds);
        var tired = new[] { Scenes.Tea, Scenes.Reading, Scenes.Music };
        var fresh = new[] { Scenes.Yoga, Scenes.Trampoline, Scenes.Plant, Scenes.Gaming, Scenes.Music };
        var pool = (_save.Energy < 50 ? tired : fresh).Where(s => Scenes.IsUnlocked(s, _save)).ToList();
        if (pool.Count == 0) pool = tired.Where(s => Scenes.IsUnlocked(s, _save)).ToList();
        var scene = pool.Count > 0 ? pool[_rng.Next(pool.Count)] : Scenes.Reading;
        Play("dance", length: 3, after: () =>
        {
            if (InFocus || !CanAnimateFreely || _save.PomodoroPhase != PomodoroPhase.Break) return;
            _breakRelax = true;
            if (scene == Scenes.Trampoline) StartTrampoline(seconds, quiet: true);
            else
            {
                StartScene(scene, quiet: true);
                _sceneUntil = Now + seconds;
            }
        });
        // След 4 мечо-дора и уморен: предлага дрямка за почивката.
        if (_save.PomodorosToday % 4 == 0 && _save.Energy < 40) AskNap(Math.Max(10, PomodoroMode.Break));
    }

    /// <summary>Почивката свърши: става от кътчето (ако е седнал там за почивката).</summary>
    private void EndBreakRelax()
    {
        if (!_breakRelax) return;
        _breakRelax = false;
        if (_onTrampoline) EndBounce(null);
        else if (_inScene && _scene != Scenes.Focus) LeaveScene();
    }

    /// <summary>За Developer: какво става с умората сега.</summary>
    internal string EnergyDebug =>
        $"Ден: {DayKindName(Today)} · Умора от седмицата {_save.Fatigue:0} · {EnergyRate():+0.0;-0.0} /ч" +
        (InFlow ? " · в ритъм" : "") + (Overloaded ? " · претоварен" : "") + (Now < _feastUntil ? " · преял" : "") +
        (_deepTired ? " · капнал" : "") + $" · работа днес {_save.WorkMinutesToday:0} мин";

    /// <summary>Developer: за тестове на ситуациите.</summary>
    internal void DevOverload() => _workStreak = 91 * 60;

    internal void DevFlow() => _flowUntil = Now + 40 * 60;

    internal void DevAskNap() => AskNap(NapMinutes);
}
