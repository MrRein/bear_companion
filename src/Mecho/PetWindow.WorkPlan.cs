using System;

namespace Mecho;

// „Ще се работи ли днес?“: веднъж на ден (делник, сутрин–следобед) мечокът пита.
// Отговорът важи до края на деня:
//  • „Да“ – предлага работа и мечо-доро по-често, сам по-често „работи си“;
//  • „Малко“ – предлага по-рядко;
//  • „Не, почивам“ – не предлага работа изобщо и почти само почива;
//  • „Още не знам“ – пита пак след 2–3 часа (най-много 2 пъти на ден).
// Ако Тут сама пусне мечо-доро или работен таймер, денът става работен (без въпрос).
public sealed partial class PetWindow
{
    public const string PlanYes = "yes", PlanLight = "light", PlanNo = "no", PlanLater = "later";

    /// <summary>Какво е казала Тут за днес („“ – още не е питал).</summary>
    public string WorkPlan => _save.WorkPlanDay == DayKey(DateTime.Today) ? _save.WorkPlan : "";

    private double _nextPlanAsk = 240;

    /// <summary>Записва плана за днес (отговор или сама пуснала работа).</summary>
    public void SetWorkPlan(string plan)
    {
        string today = DayKey(DateTime.Today);
        if (_save.WorkPlanDay != today)
        {
            _save.WorkPlanDay = today;
            _save.WorkPlanAsks = 0;
        }
        _save.WorkPlan = plan;
        // Решено е: не пита пак днес (освен „още не знам“).
        _nextPlanAsk = plan == PlanLater ? Now + Minutes(120, 180) : Now + 3600;
        // Предложението за работа идва по новия ритъм.
        _nextOffer = Now + plan switch { PlanYes => Minutes(20, 40), PlanLight => Minutes(90, 150), _ => Minutes(60, 120) };
        Persist();
    }

    /// <summary>Колко по-често/рядко да предлага работа и сам да „работи си“ според плана.</summary>
    private double WorkPlanFactor => WorkPlan switch
    {
        PlanYes => 1.3,
        PlanLight => 0.6,
        PlanNo => 0.1,
        _ => 1,
    };

    /// <summary>Тут сама пусна работа: денът е работен (ако беше казала „не“ – мечокът се радва).</summary>
    private void NoteWorkStarted()
    {
        string plan = WorkPlan;
        if (plan is PlanYes or PlanLight) return;
        SetWorkPlan(PlanYes);
        if (plan == PlanNo && !SelfTest.Running) Later(1.5, () => Say(R.Pick("plan.changed", _save.OwnerName), 4));
    }

    /// <summary>Пита за деня (веднъж; „още не знам“ – още веднъж по-късно).</summary>
    private void UpdateWorkPlan(double now)
    {
        if (now < _nextPlanAsk) return;
        _nextPlanAsk = now + 300;
        var t = DateTime.Now;
        string plan = WorkPlan;
        if (plan is PlanYes or PlanLight or PlanNo) return;
        // Сама е почнала да работи: ясно е.
        if (InFocus || TimerRunning)
        {
            SetWorkPlan(PlanYes);
            return;
        }
        if (t.Hour is < 8 or >= 17 || t.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return;
        if (_save.WorkPlanDay == DayKey(DateTime.Today) && _save.WorkPlanAsks >= 2) return;
        if (IsQuiet || IsAsleep || IsResting || ChatIsOpen || MenuIsOpen || OnWalk || NativeMethods.IdleSeconds() > 60) return;
        if (_state is not (BearState.Idle or BearState.Walk or BearState.Couch)) return;
        if (_save.WorkPlanDay != DayKey(DateTime.Today))
        {
            _save.WorkPlanDay = DayKey(DateTime.Today);
            _save.WorkPlan = "";
            _save.WorkPlanAsks = 0;
        }
        _save.WorkPlanAsks++;
        Ask(Conversations.WorkPlan(this, again: plan == PlanLater));
    }
}
