using System;
using System.Collections.Generic;
using System.Linq;

namespace Mecho;

/// <summary>
/// Погалването: не по всяко време, а когато той поиска – няколко пъти на ден (3–5), в
/// случайни часове (9:00–21:00, поне час и половина едно от друго). Тогава плочката
/// „Погали“ светва за 40 минути. Скрит брояч: стигне ли скритото число (5–9, всеки път
/// ново), получаваш 1–3 лешника (3 – рядко) и той казва нещо специално.
/// </summary>
public sealed partial class PetWindow
{
    private const double PetWindowMinutes = 40;

    /// <summary>Може ли да се погали сега (иска да го погалиш).</summary>
    public bool WantsPetting => DateTime.Now < _save.PetOpenUntil && !IsAsleep && !OnWalk;

    /// <summary>Нов ден: кога ще поиска да го погалиш.</summary>
    private void PlanPetting()
    {
        string today = DayKey(DateTime.Today);
        if (_save.PetDay == today) return;
        _save.PetDay = today;
        int count = 3 + _rng.Next(3);                       // 3–5 пъти на ден
        var times = new List<int>();
        for (int tries = 0; times.Count < count && tries < 200; tries++)
        {
            int t = 9 * 60 + _rng.Next(12 * 60);           // 9:00–21:00
            if (times.All(x => Math.Abs(x - t) >= 90)) times.Add(t);
        }
        times.Sort();
        // Часовете, които вече са минали (пуснат е следобед), не се броят.
        int now = (int)DateTime.Now.TimeOfDay.TotalMinutes;
        _save.PetTimes = string.Join(",", times.Where(t => t > now));
        if (_save.PetGoal <= 0) _save.PetGoal = NewPetGoal();
    }

    private int NewPetGoal() => 5 + _rng.Next(5);           // 5–9

    /// <summary>Всеки кадър: дошъл ли е часът да поиска.</summary>
    private void UpdatePetting()
    {
        PlanPetting();
        UpdateDesires();
        if (DateTime.Now >= _nextGoalCheck)
        {
            _nextGoalCheck = DateTime.Now.AddSeconds(3);
            CheckBearGoal();
        }
        if (_save.PetTimes.Length == 0 || IsAsleep || ChatIsOpen) return;
        var times = _save.PetTimes.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
        int now = (int)DateTime.Now.TimeOfDay.TotalMinutes;
        if (times[0] > now) return;
        times.RemoveAt(0);
        _save.PetTimes = string.Join(",", times);
        _save.PetOpenUntil = DateTime.Now.AddMinutes(PetWindowMinutes);
        Persist();
        NotifyCare();
        Emote("heart", 4);
        if (!IsQuiet) Mutter(R.Pick("pet.wants", _save.OwnerName), 5);
    }

    /// <summary>
    /// Галиш го. invited – той сам те покани (прегръдката в разговора): тогава може винаги.
    /// Иначе само когато иска (WantsPetting) – след това пак чака следващия път.
    /// </summary>
    public void Pet() => Pet(invited: false);

    public void Pet(bool invited)
    {
        if (BusyEating()) return;
        if (IsResting)
        {
            Say(R.Any("sleep.petted"), 3);
            return;
        }
        if (!invited && !WantsPetting)
        {
            Say(R.Pick("pet.notnow", _save.OwnerName), 3);
            return;
        }
        _save.PetOpenUntil = DateTime.MinValue;
        TodayRec.Petted++;
        Log("heart", "Погален");
        Joy(JoyPetted);
        PetEnergy();
        React("happy");
        // Скритият брояч (общ за гушките и всички други желания).
        if (!CountWish())
        {
            Say(Lines.Pick(Lines.Petted, _save.OwnerName), 3);
            Emote("heart");
        }
        Persist();
        NotifyCare();
    }
}
