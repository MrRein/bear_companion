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

    /// <summary>Последните реплики (най-новата – последна), най-много 6.</summary>
    public IReadOnlyList<(DateTime At, string Text)> Said => _said;

    private void NoteSaid(string text)
    {
        text = text.Trim();
        if (text.Length == 0) return;
        if (_said.Count > 0 && _said[^1].Text == text) return;
        _said.Add((DateTime.Now, text));
        if (_said.Count > 6) _said.RemoveAt(0);
    }

    // ── Дневникът на деня ──

    /// <summary>Записва нещо в днешния дневник.</summary>
    private void Log(string icon, string text)
    {
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

    // ── Желания: разходка, диван, рисуване ──

    /// <summary>Какво иска сега (walk, rest, draw) или null.</summary>
    public string? CurrentDesire => _save.Desire.Length > 0 && DateTime.Now < _save.DesireUntil && !IsAsleep && !OnWalk ? _save.Desire : null;

    public static string DesireIcon(string kind) => kind switch { "walk" => "walk", "rest" => "relax", "draw" => "pencil", _ => "heart" };

    public static string DesireText(string kind) => kind switch
    {
        "walk" => "Иска на разходка",
        "rest" => "Иска да си почине на диванчето",
        "draw" => "Иска да порисувате",
        _ => "Иска нещо",
    };

    /// <summary>Нов ден: 2–3 желания в случайни часове (10:00–20:00, поне 2 ч едно от друго).</summary>
    private void PlanDesires()
    {
        string today = DayKey(DateTime.Today);
        if (_save.DesireTimes.StartsWith(today + ";")) return;
        var kinds = new List<string> { "walk", "rest" };
        if (Kitchen.Owns(_save, Kitchen.Easel)) kinds.Add("draw");
        int count = 2 + _rng.Next(2);
        var times = new List<int>();
        for (int tries = 0; times.Count < count && tries < 200; tries++)
        {
            int t = 10 * 60 + _rng.Next(10 * 60);
            if (times.All(x => Math.Abs(x - t) >= 120)) times.Add(t);
        }
        times.Sort();
        int now = (int)DateTime.Now.TimeOfDay.TotalMinutes;
        _save.DesireTimes = today + ";" + string.Join(",", times.Where(t => t > now).Select(t => $"{t}:{kinds[_rng.Next(kinds.Count)]}"));
    }

    private void UpdateDesires()
    {
        PlanDesires();
        int semi = _save.DesireTimes.IndexOf(';');
        if (semi < 0) return;
        var list = _save.DesireTimes[(semi + 1)..].Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (list.Count == 0 || IsAsleep || OnWalk || ChatIsOpen) return;
        var parts = list[0].Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], out int at)) { list.RemoveAt(0); _save.DesireTimes = _save.DesireTimes[..(semi + 1)] + string.Join(",", list); return; }
        if (at > (int)DateTime.Now.TimeOfDay.TotalMinutes) return;
        list.RemoveAt(0);
        _save.DesireTimes = _save.DesireTimes[..(semi + 1)] + string.Join(",", list);
        _save.Desire = parts[1];
        _save.DesireUntil = DateTime.Now.AddMinutes(60);
        Persist();
        NotifyCare();
        Emote("heart", 3);
        if (!IsQuiet) Mutter(R.Pick("desire." + parts[1], _save.OwnerName), 5);
    }

    /// <summary>Направихте каквото искаше (разходка, диван, рисуване): радва се.</summary>
    private void FulfilDesire(string kind)
    {
        if (CurrentDesire != kind) return;
        _save.Desire = "";
        _save.DesireUntil = DateTime.MinValue;
        TodayRec.DesiresMet++;
        Joy(JoyPetted);
        Log(DesireIcon(kind), "Изпълни желанието му: " + (kind switch { "walk" => "разходка", "rest" => "почивка на диванчето", "draw" => "рисуване", _ => kind }));
        Later(1.5, () => Say(R.Pick("desire.met", _save.OwnerName), 4));
        Emote("heart", 3);
        NotifyCare();
    }

    // ── Мечешката цел за деня ──

    /// <summary>Цел: какво, колко, награда, докъде си стигнал днес, възможна ли е.</summary>
    private sealed record BearGoal(string Id, string Text, int Target, int Reward, Func<PetWindow, int> Have, Func<PetWindow, bool>? Possible = null);

    private static readonly BearGoal[] BearGoals =
    {
        new("tasks6", "Свърши 6 задачи", 6, 9, p => p.TodayRec.TasksDone),
        new("big1", "Свърши голяма задача", 1, 7, p => p.TodayRec.BigTasks),
        new("big2", "Свърши 2 големи задачи", 2, 14, p => p.TodayRec.BigTasks),
        new("work3h", "Работете заедно 3 часа", 180, 12, p => (int)p.TodayRec.WorkMinutes),
        new("focus4", "Направете 4 фокуса", 4, 10, p => p.TodayRec.Pomodoros),
        new("foods3", "Нахрани го с 3 различни храни", 3, 7, p => p.TodayRec.Foods.Count),
        new("pet3", "Погали го 3 пъти", 3, 8, p => p.TodayRec.Petted),
        new("desires2", "Изпълни 2 негови желания", 2, 10, p => p.TodayRec.DesiresMet),
    };

    /// <summary>Днешната цел (избира се веднъж на ден).</summary>
    public (string Text, int Have, int Target, int Reward, bool Paid) TodayGoal
    {
        get
        {
            var rec = TodayRec;
            var goal = BearGoals.FirstOrDefault(g => g.Id == rec.Goal);
            if (goal == null)
            {
                var pool = BearGoals.Where(g => g.Possible?.Invoke(this) ?? true).ToArray();
                goal = pool[_rng.Next(pool.Length)];
                rec.Goal = goal.Id;
            }
            return (goal.Text, Math.Min(goal.Target, goal.Have(this)), goal.Target, goal.Reward, rec.GoalPaid);
        }
    }

    private DateTime _nextGoalCheck;

    /// <summary>Стигнал ли си целта: лешниците и нещо специално от него (веднъж на ден).</summary>
    private void CheckBearGoal()
    {
        var g = TodayGoal;
        if (g.Paid || g.Have < g.Target) return;
        TodayRec.GoalPaid = true;
        GainNuts(g.Reward, "мечешка цел");
        Log("star", "Мечешката цел: " + g.Text.ToLowerInvariant());
        Say(R.Pick("goal.done", _save.OwnerName) + $" +{g.Reward} 🌰", 7);
        Emote("sparkle", 4);
    }

    /// <summary>Колко още ще прави това (секунди), 0 – докато не го спреш.</summary>
    public double SceneLeftSeconds => _sceneUntil > 0 ? Math.Max(0, _sceneUntil - Now) : 0;
}
