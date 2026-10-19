using System;
using System.Collections.Generic;
using System.Linq;

namespace Mecho;

// Паметта: всеки ден се записва какво сме правили (мечо-дора, задачи, храна, кога
// е легнал, събуди ли го рано…). Сутрин, щом е свободен, мечокът си спомня нещо от
// вчера – понякога пита (с отговори). Следобед понякога още един спомен.
// Любима храна за деня: една от отключените; дадеш ли му я – сърчице.
public sealed partial class PetWindow
{
    private static string DayKey(DateTime d) => d.ToString("yyyy-MM-dd");

    /// <summary>Днешният запис (създава се, ако още го няма).</summary>
    private DayRecord TodayRec
    {
        get
        {
            string key = DayKey(DateTime.Today);
            var rec = _save.Days.LastOrDefault();
            if (rec?.Date == key) return rec;
            rec = _save.Days.FirstOrDefault(d => d.Date == key);
            if (rec != null) return rec;
            rec = new DayRecord { Date = key };
            _save.Days.Add(rec);
            if (_save.Days.Count > 60) _save.Days.RemoveRange(0, _save.Days.Count - 60);
            return rec;
        }
    }

    /// <summary>Последният ден преди днес, в който сме били заедно (и преди колко дни).</summary>
    private (DayRecord? Rec, int Gap) LastDay()
    {
        string today = DayKey(DateTime.Today);
        var rec = _save.Days.Where(d => string.CompareOrdinal(d.Date, today) < 0).OrderBy(d => d.Date, StringComparer.Ordinal).LastOrDefault();
        if (rec == null || !DateTime.TryParse(rec.Date, out var date)) return (null, 0);
        return (rec, (int)(DateTime.Today - date.Date).TotalDays);
    }

    /// <summary>Колко дни подред (до вчера) сме правили мечо-доро.</summary>
    private int WorkStreak()
    {
        int n = 0;
        var day = DateTime.Today.AddDays(-1);
        while (_save.Days.FirstOrDefault(d => d.Date == DayKey(day)) is { Pomodoros: > 0 })
        {
            n++;
            day = day.AddDays(-1);
        }
        return n;
    }

    // ── Записване (викат го местата, където се случва нещо) ──

    private void RecordPomodoro(double minutes)
    {
        TodayRec.Pomodoros++;
        TodayRec.WorkMinutes += minutes;
    }

    private void RecordWork(double minutes) => TodayRec.WorkMinutes += Math.Max(0, minutes);

    private void RecordTask(TaskItem t, bool done)
    {
        var r = TodayRec;
        r.TasksDone = Math.Max(0, r.TasksDone + (done ? 1 : -1));
        if (t.Size >= 3) r.BigTasks = Math.Max(0, r.BigTasks + (done ? 1 : -1));
    }

    private void RecordFood(Food f)
    {
        var r = TodayRec;
        if (!r.Foods.Contains(f.Id)) r.Foods.Add(f.Id);
    }

    /// <summary>Легна за деня: след полунощ се брои към вчерашния ден.</summary>
    private void RecordBed()
    {
        var now = DateTime.Now;
        string key = DayKey(now.Hour < 5 ? DateTime.Today.AddDays(-1) : DateTime.Today);
        var rec = _save.Days.FirstOrDefault(d => d.Date == key) ?? TodayRec;
        rec.BedAt = now;
    }

    private void RecordNeeds()
    {
        var r = TodayRec;
        r.MinFullness = Math.Min(r.MinFullness, _save.Fullness);
        if (!IsAsleep) r.MinEnergy = Math.Min(r.MinEnergy, _save.Energy);
    }

    // ── Любимата храна за днес ──

    /// <summary>Любимото за днес (от храните, които може да сготви), избира се веднъж на ден.</summary>
    public Food? FavoriteFood
    {
        get
        {
            string today = DayKey(DateTime.Today);
            // Любимото е нещо, което може да сготви (иначе – ново, ако уредът го няма вече).
            if (_save.FavFoodDay == today && Kitchen.Foods.FirstOrDefault(f => f.Id == _save.FavFood) is { } old && !Kitchen.CanCook(old, _save))
                _save.FavFoodDay = "";
            if (_save.FavFoodDay != today)
            {
                var can = Kitchen.Foods.Where(f => f.Fullness >= 5 && Kitchen.Regular(f) && Kitchen.CanCook(f, _save)).ToList();
                if (can.Count == 0) return null;
                // Не същото като вчера, ако има избор.
                var pick = can.Where(f => f.Id != _save.FavFood).ToList();
                _save.FavFood = (pick.Count > 0 ? pick : can)[_rng.Next((pick.Count > 0 ? pick : can).Count)].Id;
                _save.FavFoodDay = today;
                _save.FavFoodSaid = false;
            }
            return Kitchen.Foods.FirstOrDefault(f => f.Id == _save.FavFood);
        }
    }

    /// <summary>Яде любимото за деня (първото парче): сърчице и благодари.</summary>
    private void FavoriteEaten(Food f)
    {
        RecordFood(f);
        if (FavoriteFood?.Id != f.Id || TodayRec.FavFed) return;
        TodayRec.FavFed = true;
        Emote("heart", 4);
        Joy(JoyPetted);
        Later(1.2, () => Say(R.Put(R.Pick("fav.eaten", _save.OwnerName), "храна", f.Name.ToLowerInvariant()), 4));
    }

    // ── Спомените ──

    private double _memoryAt = 45;   // не веднага при пускане: първо се огледа и поздрави

    private bool FreeToReminisce =>
        !IsAsleep && !InFocus && !IsQuiet && _state == BearState.Idle && _eating == null && _cook == null
        && _bubble.Visibility != System.Windows.Visibility.Visible && !MenuIsOpen && _trip == null;

    private void UpdateMemory(double now)
    {
        if (now < _memoryAt) return;
        _memoryAt = now + 20;
        RecordNeeds();
        if (!FreeToReminisce) return;
        string today = DayKey(DateTime.Today);
        int hour = DateTime.Now.Hour;
        if (_save.MemoryDay != today && hour >= 6)
        {
            _save.MemoryDay = today;
            if (MorningMemory()) _memoryAt = now + 600;
            Persist();
            return;
        }
        // Следобед (14–19 ч.): понякога още един спомен или „днес ми се яде…“.
        if (_save.MemoryLaterDay != today && hour is >= 14 and < 19 && _rng.Next(6) == 0)
        {
            _save.MemoryLaterDay = today;
            if (_rng.Next(2) == 0) LaterMemory();
            else SayFavoriteWish();
            Persist();
        }
        else if (!_save.FavFoodSaid && _save.FavFoodDay == today && hour is >= 10 and < 20 && _save.Fullness < 60 && _rng.Next(8) == 0)
            SayFavoriteWish();
    }

    /// <summary>„Днес ми се яде…“ (веднъж на ден).</summary>
    private void SayFavoriteWish()
    {
        if (FavoriteFood is not { } f || _save.FavFoodSaid || TodayRec.FavFed || !Kitchen.FitsNow(f, DateTime.Now)) return;
        _save.FavFoodSaid = true;
        Say(R.Put(R.Pick("fav.wish", _save.OwnerName), "храна", f.Name.ToLowerInvariant()), 5);
        Emote("hungry", 3);
    }

    private string Fill(string key, params (string Name, string Value)[] values)
    {
        string line = R.Pick(key, _save.OwnerName);
        foreach (var (name, value) in values) line = R.Put(line, name, value);
        return line;
    }

    /// <summary>Сутрешният спомен от вчера. Връща false, ако няма какво да каже.</summary>
    private bool MorningMemory()
    {
        var (y, gap) = LastDay();
        if (y == null) return false;
        var options = new List<(string Key, double Weight, Action Do)>();
        void Add(string key, double weight, Action act)
        {
            // Казаното наскоро – по-рядко.
            if (_save.MemoryUsed.Contains(key)) weight *= 0.2;
            options.Add((key, weight, act));
        }

        if (gap >= 2)
        {
            Add("missed", 50, () => { Say(Fill("memory.missed", ("число", gap.ToString())), 6); Emote("heart", 3); });
            Pick();
            return true;
        }
        string Food(string id) => (Kitchen.Foods.FirstOrDefault(f => f.Id == id)?.Name ?? id).ToLowerInvariant();

        if (y.Pomodoros > 0)
            Add("pomodoros", 3, () => Ask(new ChatQuestion(
                Fill(y.Pomodoros == 1 ? "memory.pomodoros.one" : "memory.pomodoros", ("число", y.Pomodoros.ToString()), ("още", (y.Pomodoros + 1).ToString())),
                new ChatOption("Да, хайде! 💪", p => p.Say(p.Fill("memory.more.yes"), 4)),
                new ChatOption("Ще видим", p => p.Say(p.Fill("memory.more.maybe"), 4)))));
        if (y.WorkMinutes >= 180 || y.Pomodoros >= 6)
            Add("hardwork", 4, () => Ask(new ChatQuestion(Fill("memory.hardwork"),
                new ChatOption("Да, по-леко", p => p.Say(p.Fill("memory.easy.yes"), 4)),
                new ChatOption("Не, давай!", p => p.Say(p.Fill("memory.easy.no"), 4)))));
        if (y.MinEnergy < 25 || y.WorkMinutes >= 150 || y.BedAt is { Hour: >= 23 or < 5 })
            Add("tired", 4, () => Ask(new ChatQuestion(Fill("memory.tired"),
                new ChatOption("Да…", p => p.Later(0.8, () => p.Ask(new ChatQuestion(p.Fill("memory.tired.rest"),
                    new ChatOption("Да, почивка ☕", p2 => { p2.Say(p2.Fill("memory.tired.rest.yes"), 4); p2.RelaxRandom(); }),
                    new ChatOption("Не, ще работим", p2 => p2.Say(p2.Fill("memory.tired.rest.no"), 4)))))),
                new ChatOption("Не, бодра съм!", p => p.Say(p.Fill("memory.tired.no"), 4)))));
        if (y.Pomodoros == 0 && y.TasksDone == 0)
            Add("lazy", 2, () => Ask(new ChatQuestion(Fill("memory.lazy"),
                new ChatOption("Да!", p => p.Say(p.Fill("memory.lazy.yes"), 4)),
                new ChatOption("Не днес", p => p.Say(p.Fill("memory.lazy.no"), 4)))));
        if (y.TasksDone >= 3)
            Add("tasks", 3, () => { Say(Fill("memory.tasks", ("число", y.TasksDone.ToString())), 5); Emote("sparkle"); });
        if (y.BigTasks > 0)
            Add("bigtask", 2, () => Say(Fill("memory.bigtask"), 5));
        if (y.WokeEarly)
            Add("wokeearly", 4, () => { Say(Fill("memory.wokeearly"), 5); Emote("zzz"); });
        if (y.NapWoken)
            Add("napwoken", 2, () => Say(Fill("memory.napwoken"), 5));
        if (y.BedAt is { Hour: >= 0 and < 5 })
            Add("latebed", 3, () => Ask(new ChatQuestion(Fill("memory.latebed"),
                new ChatOption("Да, по-рано", p => p.Say(p.Fill("memory.latebed.yes"), 4)),
                new ChatOption("Ще видим…", p => p.Say(p.Fill("memory.latebed.no"), 4)))));
        else if (y.BedAt is { Hour: >= 20 and < 22 })
            Add("earlybed", 1.5, () => Say(Fill("memory.earlybed"), 5));
        if (y.MinFullness < 15)
            Add("hungry", 3, () => { Say(Fill("memory.hungry"), 5); Emote("hungry"); });
        if (y.FavFed && y.Foods.Count > 0)
            Add("favfed", 2.5, () => Say(Fill("memory.favfed"), 5));
        if (y.Foods.Count >= 3)
            Add("feast", 2, () => Say(Fill("memory.feast", ("храна", Food(y.Foods[0])), ("храна2", Food(y.Foods[1]))), 5));
        else if (y.Foods.Count == 1)
            Add("onefood", 1.5, () => Say(Fill("memory.onefood", ("храна", Food(y.Foods[0]))), 5));
        if (y.Petted >= 4)
            Add("petted", 2, () => { Say(Fill("memory.petted"), 4); Emote("heart"); });
        else if (y.Petted == 0)
            Add("notpetted", 1, () => Say(Fill("memory.notpetted"), 4));
        if (y.Bought.Count > 0)
            Add("bought", 3, () => Say(Fill("memory.bought", ("нещо", y.Bought[^1])), 5));
        if (y.Clothes >= 3)
            Add("clothes", 1.5, () => Say(Fill("memory.clothes"), 4));
        foreach (var h in y.Hobbies.Take(3))
            if (Scenes.Fun.Any(f => f.Id == h)) Add("hobby." + h, 1.5, () => Say(Fill("memory.hobby." + h), 5));
        int streak = WorkStreak();
        if (streak >= 3)
            Add("streak", 3, () => { Say(Fill("memory.streak", ("число", streak.ToString())), 5); Emote("sparkle"); });
        var before = _save.Days.Where(d => string.CompareOrdinal(d.Date, y.Date) < 0).OrderBy(d => d.Date, StringComparer.Ordinal).LastOrDefault();
        if (before != null && y.Pomodoros > before.Pomodoros && before.Pomodoros > 0)
            Add("better", 2, () => Say(Fill("memory.better"), 5));
        if (_save.LateCoffee == DayKey(DateTime.Today.AddDays(-1)))
            Add("latecoffee", 2.5, () => Say(Fill("memory.latecoffee"), 5));
        if (DateTime.Today.DayOfWeek == DayOfWeek.Monday)
            Add("monday", 1, () => Say(Fill("memory.monday"), 5));
        if (options.Count == 0)
            Add("plain", 1, () => Say(Fill("memory.plain"), 4));
        Pick();
        return true;

        void Pick()
        {
            double total = options.Sum(o => o.Weight), roll = _rng.NextDouble() * total;
            var pick = options[^1];
            foreach (var o in options)
                if ((roll -= o.Weight) < 0) { pick = o; break; }
            _save.MemoryUsed.Remove(pick.Key);
            _save.MemoryUsed.Add(pick.Key);
            if (_save.MemoryUsed.Count > 6) _save.MemoryUsed.RemoveAt(0);
            pick.Do();
        }
    }

    /// <summary>Следобеден спомен: днешният ден досега (кратко, без въпроси).</summary>
    private void LaterMemory()
    {
        var t = TodayRec;
        string key = t.Pomodoros >= 3 ? "memory.today.work"
            : t.TasksDone >= 2 ? "memory.today.tasks"
            : t.Foods.Count == 0 ? "memory.today.nofood"
            : t.Pomodoros == 0 && t.TasksDone == 0 ? "memory.today.calm"
            : "memory.today.nice";
        Say(Fill(key, ("число", (key == "memory.today.tasks" ? t.TasksDone : t.Pomodoros).ToString())), 5);
    }
}
