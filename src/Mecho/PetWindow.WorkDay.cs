using System;
using System.Linq;

namespace Mecho;

/// <summary>
/// Работният ден: до колко работиш обикновено (веднъж), смяна само за днес (поленцето в
/// „Днес“, ивицата в „По часове“), почивни дни (събота и неделя, или „Не“ на „Ще работим ли
/// днес?“) и вечерното „Свърши ли работа за днес?“ (Да / Още малко / Не питай днес).
/// </summary>
public sealed partial class PetWindow
{
    private static string Key(DateTime day) => day.ToString("yyyy-MM-dd");

    /// <summary>До колко работи обикновено (минути) или null – още не знае.</summary>
    public int? UsualEnd
    {
        get
        {
            if (_save.UsualEnd > 0) return _save.UsualEnd;
            // Отпреди: казвал е „До колко ще работиш днес?“ – последното става обикновено.
            if (_save.DayEnd.Values.Any(v => v > 0))
            {
                _save.UsualEnd = _save.LastDayEnd;
                return _save.UsualEnd;
            }
            return null;
        }
    }

    public void SetUsualEnd(int minutes)
    {
        _save.UsualEnd = Math.Clamp(minutes, 10 * 60, 23 * 60 + 30);
        _save.LastDayEnd = _save.UsualEnd;
        Persist();
        NotifyCare();
    }

    /// <summary>Почивен ли е денят: ръчно избраното; иначе „Не“ на „Ще работим ли днес?“; иначе събота и неделя (освен ако днес каза, че ще работите).</summary>
    public bool IsRestDay(DateTime day)
    {
        day = day.Date;
        if (_save.RestDays.TryGetValue(Key(day), out bool rest)) return rest;
        bool today = day == DateTime.Today;
        if (today && WorkPlan == PlanNo) return true;
        if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return !(today && WorkPlan is PlanYes or PlanLight);
        return false;
    }

    public void SetRestDay(DateTime day, bool? rest)
    {
        if (rest is { } r) _save.RestDays[Key(day)] = r;
        else _save.RestDays.Remove(Key(day));
        foreach (var old in _save.RestDays.Keys.Where(k => DateTime.TryParse(k, out var d) && d < DateTime.Today.AddDays(-60)).ToList())
            _save.RestDays.Remove(old);
        Persist();
        NotifyCare();
    }

    /// <summary>Сменен ли е часът само за този ден.</summary>
    public int? DayEndOverride(DateTime day) => _save.DayEnd.TryGetValue(Key(day), out int m) && m > 0 ? m : null;

    /// <summary>До колко се работи в деня: за деня; иначе обикновеното; почивен ден – null.</summary>
    public int? WorkEndFor(DateTime day) => IsRestDay(day) ? null : DayEndOverride(day) ?? UsualEnd;

    /// <summary>Само за този ден (null – както обикновено).</summary>
    public void SetDayEnd(DateTime day, int? minutes)
    {
        if (minutes is { } m) _save.DayEnd[Key(day)] = Math.Clamp(m, 10 * 60, 23 * 60 + 55);
        else _save.DayEnd.Remove(Key(day));
        foreach (var old in _save.DayEnd.Keys.Where(k => DateTime.TryParse(k, out var d) && d < DateTime.Today.AddDays(-60)).ToList())
            _save.DayEnd.Remove(old);
        // Сменен е часът днес – вечерното питане тръгва наново от новия час.
        if (day.Date == DateTime.Today) _save.EndAskNext = DateTime.MinValue;
        Persist();
        NotifyCare();
    }

    /// <summary>В колко каза „Свърших“ днес (минути) или null.</summary>
    public int? WorkDoneToday => _save.WorkDoneDay == Key(DateTime.Today) ? _save.WorkDoneAt : null;

    private bool _preEndSaid, _overtimeSaid;
    private string _workDayKey = "";

    /// <summary>Веднъж в секунда: коментар преди края, вечерното питане, „работим повече“.</summary>
    private void UpdateWorkDay()
    {
        var now = DateTime.Now;
        string today = Key(now);
        if (_workDayKey != today)
        {
            _workDayKey = today;
            _preEndSaid = _overtimeSaid = false;
        }
        if (SelfTest.Running || IsRestDay(now) || WorkDoneToday != null || WorkEndFor(now) is not { } end) return;
        int nowMin = now.Hour * 60 + now.Minute;
        bool light = WorkPlan == PlanLight;
        int askAt = end - (light ? 60 : 0);   // „Леко“ – пита час по-рано
        bool free = IsVisible && !IsAsleep && !OnWalk && !ChatIsOpen && !MenuIsOpen && _eating == null;

        // Около час преди края – кратко „още малко“ (веднъж).
        if (!_preEndSaid && nowMin >= askAt - 60 && nowMin < askAt - 30 && free && !InFocus && !IsQuiet)
        {
            _preEndSaid = true;
            Mutter(R.Pick("workday.soon", _save.OwnerName), 4);
        }

        // Работите повече от обикновено (мечо-доро/работен таймер половин час след края).
        if (!_overtimeSaid && InFocus && nowMin >= end + 30 && !IsQuiet)
        {
            _overtimeSaid = true;
            Later(2, () => Mutter(R.Pick("workday.overtime", _save.OwnerName), 4));
        }

        // Вечерното питане.
        if (_save.EndNoAskDay == today || nowMin < askAt || !free || InFocus) return;
        if (_save.EndAskDay != today)
        {
            _save.EndAskDay = today;
            _save.EndAskCount = 0;
            _save.EndAskNext = DateTime.MinValue;
        }
        if (_save.EndAskCount >= 3 || now < _save.EndAskNext) return;
        _save.EndAskCount++;
        _save.EndAskNext = now.AddMinutes(30);   // не отговори – пак след 30 мин (най-много 3 пъти)
        Persist();
        AskWorkDone();
    }

    /// <summary>„Свърши ли работа за днес?“</summary>
    internal void AskWorkDone() => Ask(new ChatQuestion(R.Pick("workday.ask", _save.OwnerName),
        new ChatOption("✅ Да, свърших", p => p.WorkFinished()),
        new ChatOption("⏳ Още малко", p => p.AskHowMuchMore()),
        new ChatOption("🤫 Не питай днес", p => { p._save.EndNoAskDay = Key(DateTime.Today); p.Persist(); p.Say("Добре, днес мълча за това.", 3); })));

    private void AskHowMuchMore() => Ask(new ChatQuestion("Колко още мислиш?",
        new ChatOption("15 мин", p => p.MoreWork(15)),
        new ChatOption("30 мин", p => p.MoreWork(30)),
        new ChatOption("1 ч", p => p.MoreWork(60)),
        new ChatOption("2 ч", p => p.MoreWork(120))));

    /// <summary>„Още малко“: краят за днес се мести (и ивицата в „По часове“); пита пак тогава.</summary>
    private void MoreWork(int minutes)
    {
        var now = DateTime.Now;
        int at = (now.Hour * 60 + now.Minute + minutes + 4) / 5 * 5;
        SetDayEnd(now.Date, at);
        _save.EndAskCount = 0;
        _save.EndAskNext = now.AddMinutes(minutes);
        Persist();
        Say(R.Pick("workday.more", _save.OwnerName).Replace("{час}", $"{at / 60}:{at % 60:00}"), 4);
    }

    /// <summary>„Да, свърших“: не пита повече днес; радва се и казва колко сте свършили.</summary>
    internal void WorkFinished()
    {
        var now = DateTime.Now;
        _save.WorkDoneDay = Key(now);
        _save.WorkDoneAt = now.Hour * 60 + now.Minute;
        Log("moon", "Край на работния ден");
        var (done, _, _) = TodayLoad();
        Say(R.Text(done > 0 ? "workday.done" : "workday.done.none", _save.OwnerName, ("брой", done.ToString())), 5);
        React("happy");
        Persist();
        NotifyCare();
    }

    /// <summary>Връща „свърших“ (ако е цъкнато по погрешка).</summary>
    public void UndoWorkFinished()
    {
        _save.WorkDoneDay = "";
        Persist();
        NotifyCare();
    }
}
