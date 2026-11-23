using System;
using System.Linq;
using Mecho.Core;

namespace Mecho;

// Фаза 3 – Мечо забелязва теб. Наблюденията са в ядрото (CareModule); тук приложението
// казва какво става (CareContext) и как се показват мислите, които искат бутони.
public sealed partial class PetWindow
{
    private DateTime _careLastLook = DateTime.MinValue;
    private double _careSinceBreak, _careIdle;

    /// <summary>Колко минути почивка нулират „работиш без почивка“ (кратките почивки на мечо-дорото не).</summary>
    private const double RealBreakMinutes = 10;

    /// <summary>Сегашната обстановка за наблюденията (вика я ядрото веднъж в минута).</summary>
    internal CareContext CareContextNow()
    {
        var now = DateTime.Now;
        double minutes = _careLastLook == DateTime.MinValue ? 0 : Math.Clamp((now - _careLastLook).TotalMinutes, 0, 30);
        _careLastLook = now;
        bool working = InFocus || TimerRunning && _save.TimerIsWork;
        if (working)
        {
            _careSinceBreak += minutes;
            _careIdle = 0;
        }
        else if ((_careIdle += minutes) >= RealBreakMinutes) _careSinceBreak = 0;
        // Колко дни преди днес не е работено (0 – вчера е работено).
        int gap = 0;
        var day = DateTime.Today.AddDays(-1);
        while (gap < 60 && _save.Days.FirstOrDefault(d => d.Date == DayKey(day)) is not { Pomodoros: > 0 })
        {
            gap++;
            day = day.AddDays(-1);
        }
        if (gap >= 60 || !_save.Days.Any(d => d.Pomodoros > 0 && d.Date != DayKey(DateTime.Today))) gap = 0;   // никога не е работено – не е „завръщане“
        return new CareContext(now, working, _careSinceBreak, _save.PomodorosToday, TodayRec.TasksDone, WorkStreak(), gap,
            Awake: !IsAsleep && !SelfTest.Running);
    }

    /// <summary>Показвачите на мислите, които искат бутони (вика се от SetupAlive).</summary>
    private void SetupCare()
    {
        // Отлагана задача: предлага да я разделите – бутонът отваря картата ѝ (там са стъпките).
        OnThought("care.procrastinate", t =>
        {
            var task = Guid.TryParse(t.Fact("id"), out var id) ? _save.Tasks.FirstOrDefault(x => x.Id == id && !x.Done) : null;
            if (task == null) return;
            string line = R.Text("care.procrastinate", _save.OwnerName, ("задача", task.Title), ("дни", t.Fact("дни")));
            Ask(new ChatQuestion(line,
                new ChatOption("Да я разделим", p => TaskCardWindow.Show(p, task)),
                new ChatOption("Не сега", p => { if (R.Has("care.procrastinate.later")) p.Say(R.Pick("care.procrastinate.later", p.Save.OwnerName), 3); })));
        }, needsLines: true);
        // Дълга работа: предлага почивка (бутонът пуска почивка до него).
        OnThought("care.overwork", t =>
        {
            string line = R.Text("care.overwork", _save.OwnerName, ("часа", t.Fact("часа")), ("минути", t.Fact("минути")));
            Ask(new ChatQuestion(line,
                new ChatOption("Да, почивка ☕", p => { p._careSinceBreak = 0; p.RelaxRandom(); }),
                new ChatOption("Още малко", p => { if (R.Has("care.overwork.later")) p.Say(R.Pick("care.overwork.later", p.Save.OwnerName), 3); })));
        }, needsLines: true);
        // Голяма задача: празник.
        OnThought("care.bigtask", t =>
        {
            Say(R.Text("care.bigtask", _save.OwnerName, ("задача", t.Fact("задача"))), 6);
            Emote("sparkle", 4);
            React("dance", 2.5);
        }, needsLines: true);
    }
}
