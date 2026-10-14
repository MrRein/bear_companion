using System;
using System.Linq;

namespace Mecho;

// Когато Тут не му е казала какво да прави, денят му е от дълги спокойни
// „блокове“, избрани на случаен принцип: разхожда се и прави дребни неща
// (15–40 мин), сяда и си седи (15–45 мин), прави някое хоби (10–20 мин, не
// по-често от веднъж на ~45 мин) или дремва, ако е уморен. Между блоковете
// се поразхожда малко. Така не прави твърде много неща сам.
// От време на време (не често) подпитва дали да не свършат нещо заедно.
public sealed partial class PetWindow
{
    private double _freeUntil = 15 * 60;    // докога се разхожда свободно (после сам решава: почивка или работа)
    private bool _autoBlock;                // седи или прави хоби по свой избор (не на Тут)
    private double _nextOffer = 45 * 60;    // кога пак да предложи работа
    private double _offerUntil;             // докато е в сила предложението (клик го отваря)

    private double Minutes(int from, int to) => (from + _rng.Next(to - from + 1)) * 60;

    /// <summary>След блок (или в началото): кратка разходка, после пак избира.</summary>
    private void StartFreeCycle(bool sitNow) => _freeUntil = Now + (sitNow ? Minutes(3, 10) : Minutes(20, 40));

    /// <summary>
    /// Вика се от ChooseActivity: ако дълго никой не му е казал какво да прави,
    /// сам решава да си почива или да работи (по часовника) – като „Почивай си“
    /// или „Работи си“ в менюто, които тогава светят и Тут може да ги изключи.
    /// </summary>
    private bool FreeCycleStep()
    {
        if (Now < _freeUntil || Cooks || InFocus || IsAsleep) return false;
        PickAutoMode();
        if (RelaxesRandomly) StartRandomRelax(_lastFun);
        else StartRandomWork(_lastFun);
        Persist();
        return true;
    }

    private double _autoUntil;

    /// <summary>Избира почивка или работа за следващия блок (по часовника, с малко случайност).</summary>
    private void PickAutoMode()
    {
        double work = WorkChance(DateTime.Now);
        bool working = _rng.NextDouble() < work;
        _save.StayPut = true;
        _save.AutoMode = true;
        _save.StayScene = working ? RandomWork : RandomRelax;
        // Работа: 45–60 мин. Почивка в работно време – кратка (15–20 мин), иначе 30–60.
        _autoUntil = Now + (working ? Minutes(45, 60) : work >= 0.7 ? Minutes(15, 20) : Minutes(30, 60));
    }

    /// <summary>
    /// Колко вероятно е да работи сега. Делник: 9–12 работи (рядко почива), 12–13:30
    /// почива, 13:30–18 работи (с почивки по 15–20 мин), след 18 по-скоро почива.
    /// Почивни дни: по-скоро почива. Всяка граница е с ±15 мин, различно всеки ден.
    /// </summary>
    internal static double WorkChance(DateTime now)
    {
        if (now.Hour >= 22 || now.Hour < 6) return 0.05;
        if (now.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return 0.2;
        var day = new Random(now.Year * 1000 + now.DayOfYear);
        TimeSpan At(int h, int m) => new TimeSpan(h, m, 0) + TimeSpan.FromMinutes(day.Next(-15, 16));
        var t = now.TimeOfDay;
        if (t < At(9, 0)) return 0.2;
        if (t < At(12, 0)) return 0.9;
        if (t < At(13, 30)) return 0.1;
        if (t < At(18, 0)) return 0.8;
        return 0.2;
    }

    /// <summary>Тут го изключи („Стани“ или „Да, спри“): разхожда се, а след 30–45 мин пак решава сам.</summary>
    private void StopAutoMode()
    {
        _save.AutoMode = false;
        _freeUntil = Now + Minutes(30, 45);
    }

    private void UpdateSitting(double now)
    {
        // Работа, храна, подарък, умора: става (и после пак избира).
        if (InFocus || WaitingForFood || (_save.GiftAt != DateTime.MinValue && DateTime.Now >= _save.GiftAt && !_save.GiftPending))
        {
            LeaveScene();
            return;
        }
        if (!IsQuiet && now > _nextChatter)
        {
            _nextChatter = now + 2400 + _rng.Next(1800);
            Mutter(Lines.Pick(Scenes.Sit.During, _save.OwnerName), 4);
        }
    }

    // ── „Няма ли да правим нещо?“ ──

    /// <summary>От време на време (на 45–90 мин) предлага работа; клик върху мечока отваря избора.</summary>
    private void MaybeOfferWork(double now)
    {
        if (now < _nextOffer) return;
        _nextOffer = now + Minutes(90, 150);
        if (IsQuiet || InFocus || TimerRunning || IsAsleep || LateNight || NativeMethods.IdleSeconds() > 120) return;
        Say(Lines.Pick(Lines.WorkOffer, _save.OwnerName), 8);
        _offerUntil = now + 180;
    }

    /// <summary>Клик скоро след предложението: отваря въпроса с бутоните.</summary>
    private bool TakeOffer()
    {
        if (Now > _offerUntil) return false;
        _offerUntil = 0;
        Ask(Conversations.WorkOffer(this));
        return true;
    }
}
