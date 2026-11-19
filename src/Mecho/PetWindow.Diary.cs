using System;
using System.Collections.Generic;
using System.Linq;

namespace Mecho;

/// <summary>
/// „В бърлогата“ и „Мечешки дневник“ в менюто: последно казаното, желанията му (разходка,
/// диван, рисуване), дневникът на деня, спечелените днес лешници и мечешката цел за деня
/// (по-трудна задача с повече лешници).
/// </summary>
public sealed partial class PetWindow
{
    // ── Последно казано ──

    private readonly List<(DateTime At, string Text)> _said = new();

    /// <summary>Последните реплики (най-новата – последна), най-много 10.</summary>
    public IReadOnlyList<(DateTime At, string Text)> Said => _said;

    private void NoteSaid(string text)
    {
        text = text.Trim();
        if (text.Length == 0) return;
        if (_said.Count > 0 && _said[^1].Text == text) return;
        _said.Add((DateTime.Now, text));
        if (_said.Count > 10) _said.RemoveAt(0);
    }

    // ── Дневникът на деня ──

    /// <summary>Записва нещо в днешния дневник.</summary>
    private void Log(string icon, string text)
    {
        NoteTouched();
        var events = TodayRec.Events;
        events.Add(new DayEvent { At = DateTime.Now, Icon = icon, Text = text });
        if (events.Count > 40) events.RemoveRange(0, events.Count - 40);
    }

    /// <summary>Лешници с причина (за „Лешници днес“): задача, работа, погалване, подарък…</summary>
    private void GainNuts(int nuts, string reason)
    {
        if (nuts <= 0) return;
        CoreEngine.Bus.Send(new Mecho.Core.EarnNuts(nuts, reason));
        Persist();
        NotifyCare();
    }

    private void WireDiary()
    {
        CoreEngine.Bus.Subscribe<Mecho.Core.HazelnutsChanged>(this, c =>
        {
            // Само спечеленото (не върнатото при отказ, не Developer, не старите записи).
            if (c.New <= c.Old || c.Reason is null or "стар код" or "стар запис") return;
            string why = c.Reason switch { "задача" => "задачи", "работа" => "работа", _ => c.Reason };
            var by = TodayRec.NutsBy;
            by[why] = (by.TryGetValue(why, out var n) ? n : 0) + (c.New - c.Old);
        });
    }

    /// <summary>Спечелените днес лешници (общо и по откъде, най-много първо).</summary>
    public (int Total, (string Why, int Nuts)[] By) NutsToday
    {
        get
        {
            var by = TodayRec.NutsBy.OrderByDescending(kv => kv.Value).Select(kv => (kv.Key, kv.Value)).ToArray();
            return (by.Sum(x => x.Value), by);
        }
    }

    // ── Желания: 20 неща, които понякога иска (плюс гушката – виж PetWindow.Petting.cs) ──

    /// <summary>Едно желание: ключ, „Иска …“, иконка, какво пише в дневника, възможно ли е сега.</summary>
    public sealed record Desire(string Kind, string Text, string Icon, string Done, Func<PetWindow, bool>? Possible = null);

    public static readonly Desire[] Desires =
    {
        new("walk", "Разхожда му се", "walk", "разходка"),
        new("rest", "Седи му се на дивана", "relax", "почивка на диванчето"),
        new("draw", "Рисува му се", "pencil", "рисуване заедно", p => Kitchen.Owns(p._save, Kitchen.Easel)),
        new("scene:reading", "Чете му се книжка", "book", "четене", p => p.SceneOk("reading")),
        new("scene:gaming", "Играе му се на компютъра", "game", "игра на компютъра", p => p.SceneOk("gaming")),
        new("scene:yoga", "Прави му се йога", "yoga", "йога", p => p.SceneOk("yoga")),
        new("scene:plant", "Иска да си погледне цветето", "plant", "грижа за цветето", p => p.SceneOk("plant")),
        new("scene:music", "Слуша му се музика", "music", "музика", p => p.SceneOk("music")),
        new("scene:knit", "Плете му се", "yarn", "плетене", p => p.SceneOk("knit")),
        new("scene:guitar", "Свири му се на китара", "guitar", "свирене на китара", p => p.SceneOk("guitar")),
        new("scene:boardplay", "Играе му се настолна игра", "dice", "настолна игра", p => p.SceneOk("boardplay")),
        new("scene:paintfun", "Рисува му се на статива", "easel", "рисуване за удоволствие", p => p.SceneOk("paintfun")),
        new("scene:write", "Пише му се в тефтера", "notes", "идеи в тефтера", p => p.SceneOk("write")),
        new("nap", "Спи му се", "moon", "дрямка", p => p._save.Energy < 85),
        new("focus", "Работи му се с теб", "hourglass", "работа заедно", p => !p.InFocus),
        new("eat", "Яде му се", "food", "хапване", p => p._save.Fullness < 80),
        new("drink", "Пие му се нещо", "tea", "нещо за пиене"),
        new("sweet", "Яде му се нещо сладко", "honey", "нещо сладко"),
        new("dress", "Преоблича му се", "wardrobe", "ново облекло", p => DailyOffer.HasWardrobe(p._save)),
    };

    private bool SceneOk(string id) => Scenes.All.FirstOrDefault(x => x.Id == id) is { } sc && Scenes.IsUnlocked(sc, _save);

    public static Desire? DesireOf(string? kind) => Desires.FirstOrDefault(d => d.Kind == kind);

    /// <summary>Какво иска сега (ключ от Desires) или null.</summary>
    public string? CurrentDesire => _save.Desire.Length > 0 && DateTime.Now < _save.DesireUntil && !IsAsleep && !OnWalk ? _save.Desire : null;

    /// <summary>Нов ден: 2–3 желания в случайни часове (10:00–20:00, поне 2 ч едно от друго) – не много често.</summary>
    private void PlanDesires()
    {
        string today = DayKey(DateTime.Today);
        if (_save.DesireTimes.StartsWith(today + ";")) return;
        int count = 2 + _rng.Next(2);
        var times = new List<int>();
        for (int tries = 0; times.Count < count && tries < 200; tries++)
        {
            int t = 10 * 60 + _rng.Next(10 * 60);
            if (times.All(x => Math.Abs(x - t) >= 120)) times.Add(t);
        }
        times.Sort();
        int now = (int)DateTime.Now.TimeOfDay.TotalMinutes;
        // Какво точно – решава се чак в часа му (по това, което е възможно тогава).
        _save.DesireTimes = today + ";" + string.Join(",", times.Where(t => t > now));
    }

    private void UpdateDesires()
    {
        PlanDesires();
        int semi = _save.DesireTimes.IndexOf(';');
        if (semi < 0) return;
        var list = _save.DesireTimes[(semi + 1)..].Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (list.Count == 0 || IsAsleep || OnWalk || ChatIsOpen || CurrentDesire != null) return;
        string first = list[0].Split(':')[0];
        if (int.TryParse(first, out int at) && at > (int)DateTime.Now.TimeOfDay.TotalMinutes) return;
        list.RemoveAt(0);
        _save.DesireTimes = _save.DesireTimes[..(semi + 1)] + string.Join(",", list);
        var pool = Desires.Where(d => d.Possible?.Invoke(this) ?? true).Where(d => d.Kind != _lastDesire).ToArray();
        if (pool.Length == 0) return;
        StartDesire(pool[_rng.Next(pool.Length)].Kind);
    }

    private string _lastDesire = "";

    /// <summary>Започва да иска нещо (за 60 мин).</summary>
    private void StartDesire(string kind)
    {
        _lastDesire = kind;
        _save.Desire = kind;
        _save.DesireUntil = DateTime.Now.AddMinutes(60);
        Persist();
        NotifyCare();
        Emote("heart", 3);
        if (!IsQuiet && DesireOf(kind) is { } d)
        {
            string key = "desire." + kind.Replace("scene:", "");
            string line = R.Has(key) ? R.Pick(key, _save.OwnerName) : d.Text + "…";
            Mutter(line, 5);
        }
    }

    /// <summary>Направихте каквото искаше: радва се, брои се към скрития брояч (лешници).</summary>
    private void FulfilDesire(string kind)
    {
        if (CurrentDesire != kind) return;
        _save.Desire = "";
        _save.DesireUntil = DateTime.MinValue;
        TodayRec.DesiresMet++;
        Joy(JoyPetted);
        NoteContent();
        Log(DesireOf(kind)?.Icon ?? "heart", "Изпълни желанието му: " + (DesireOf(kind)?.Done ?? kind));
        Emote("heart", 3);
        if (!CountWish()) Later(1.5, () => Say(R.Pick("desire.met", _save.OwnerName), 4));
        Persist();
        NotifyCare();
    }

    /// <summary>
    /// Скритият брояч на изпълнените желания (гушки и всичко останало): стигнеш ли скритото
    /// число (5–9, всеки път ново) – 1–3 лешника (3 – рядко) и нещо специално. true – наградата дойде.
    /// </summary>
    private bool CountWish()
    {
        if (_save.PetGoal <= 0) _save.PetGoal = NewPetGoal();
        _save.PetCount++;
        if (_save.PetCount < _save.PetGoal) return false;
        _save.PetCount = 0;
        _save.PetGoal = NewPetGoal();
        double roll = _rng.NextDouble();
        int nuts = roll < 0.1 ? 3 : roll < 0.45 ? 2 : 1;
        GainNuts(nuts, "желания");
        Say(R.Pick("pet.reward", _save.OwnerName) + $" +{nuts} 🌰", 7);
        Emote("sparkle", 4);
        return true;
    }

    // ── Мисия: голяма цел за няколко дни ──

    /// <summary>Мисия: какво, колко, награда, докъде си стигнал (от деня, в който е почнала).</summary>
    private sealed record Mission(string Id, string Text, int Target, int Reward, Func<IEnumerable<DayRecord>, int> Have);

    private static readonly Mission[] Missions =
    {
        new("tasks25", "Свърши 25 задачи", 25, 30, d => d.Sum(r => r.TasksDone)),
        new("big6", "Свърши 6 големи задачи", 6, 35, d => d.Sum(r => r.BigTasks)),
        new("work15", "Работете заедно 15 часа", 900, 40, d => (int)d.Sum(r => r.WorkMinutes)),
        new("focus20", "Направете 20 фокуса", 20, 35, d => d.Sum(r => r.Pomodoros)),
        new("foods12", "Опитайте 12 различни ястия", 12, 30, d => d.SelectMany(r => r.Foods).Distinct().Count()),
        new("pet12", "Погали го 12 пъти", 12, 30, d => d.Sum(r => r.Petted)),
        new("desires10", "Изпълни 10 негови желания", 10, 35, d => d.Sum(r => r.DesiresMet)),
        new("streak5", "5 дни подред със свършена задача", 5, 40, Streak),
    };

    /// <summary>Колко дни подред (до днес или вчера) има поне една свършена задача.</summary>
    private static int Streak(IEnumerable<DayRecord> days)
    {
        var done = days.Where(r => r.TasksDone > 0).Select(r => r.Date).ToHashSet();
        var day = DateTime.Today;
        if (!done.Contains(DayKey(day))) day = day.AddDays(-1);
        int n = 0;
        while (done.Contains(DayKey(day))) { n++; day = day.AddDays(-1); }
        return n;
    }

    /// <summary>Сегашната мисия (почва нова на следващия ден след изпълнена).</summary>
    public (string Text, int Have, int Target, int Reward, bool Done, int Days) CurrentMission
    {
        get
        {
            string today = DayKey(DateTime.Today);
            var m = Missions.FirstOrDefault(x => x.Id == _save.MissionId);
            if (m == null || (_save.MissionDone && string.CompareOrdinal(_save.MissionStart, today) <= 0 && _save.MissionDoneDay != today))
            {
                var pool = Missions.Where(x => x.Id != _save.MissionId).ToArray();
                m = pool[_rng.Next(pool.Length)];
                _save.MissionId = m.Id;
                _save.MissionStart = today;
                _save.MissionDone = false;
                _save.MissionDoneDay = "";
            }
            string start = _save.MissionStart;
            var days = _save.Days.Where(r => string.CompareOrdinal(r.Date, start) >= 0);
            int have = m.Id == "streak5" ? Streak(_save.Days) : m.Have(days);
            int daysIn = DateTime.TryParse(start, out var st) ? (int)(DateTime.Today - st).TotalDays + 1 : 1;
            return (m.Text, Math.Min(m.Target, have), m.Target, m.Reward, _save.MissionDone, daysIn);
        }
    }

    private DateTime _nextGoalCheck;

    /// <summary>Стигнал ли си мисията: лешниците и нещо специално (веднъж).</summary>
    private void CheckBearGoal()
    {
        var g = CurrentMission;
        if (g.Done || g.Have < g.Target) return;
        PayMission(g.Text, g.Reward);
    }

    private void PayMission(string text, int reward)
    {
        _save.MissionDone = true;
        _save.MissionDoneDay = DayKey(DateTime.Today);
        NoteProud();
        GainNuts(reward, "мисия");
        Log("star", "Мисията изпълнена: " + text.ToLowerInvariant());
        Say(R.Pick("goal.done", _save.OwnerName) + $" +{reward} 🌰", 7);
        Emote("sparkle", 4);
    }

    /// <summary>„Поговорим ли?“ – въпрос от него (желанието „да си поговорите“, менюто).</summary>
    public void StartTalk()
    {
        if (IsAsleep) return;
        FulfilDesire("talk");
        Ask(Conversations.Next(this));
    }

    /// <summary>Колко още ще прави това (секунди), 0 – докато не го спреш.</summary>
    public double SceneLeftSeconds => _sceneUntil > 0 ? Math.Max(0, _sceneUntil - Now) : 0;
}
