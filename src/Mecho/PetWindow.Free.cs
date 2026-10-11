using System;
using System.Linq;

namespace Mecho;

// Когато Тут не му е казала какво да прави:
// 1. дълго се скита (разходки, дребни неща) – 30–50 мин;
// 2. после сяда и си седи;
// 3. към 90-ата минута прави едно от отключените хобита за 10–20 мин;
// 4. после пак сяда, а след още 40–60 мин – пак хоби.
// От време на време (не често) подпитва дали да не свършат нещо заедно.
public sealed partial class PetWindow
{
    private double _sitAfter = 40 * 60;     // секунди скитане преди да седне
    private double _hobbyAfter = 90 * 60;   // секунди до хобито (от началото на цикъла)
    private bool _autoHobby;                // хобито е избрано от него, не от Тут
    private double _nextOffer = 45 * 60;    // кога пак да предложи работа
    private double _offerUntil;             // докато е в сила предложението (клик го отваря)

    /// <summary>Нов цикъл „свободен“. sitNow: веднага сяда (след хоби).</summary>
    private void StartFreeCycle(bool sitNow)
    {
        _sitAfter = (30 + _rng.Next(21)) * 60;
        _hobbyAfter = sitNow ? _sitAfter + (40 + _rng.Next(21)) * 60 : (85 + _rng.Next(11)) * 60;
        _idleSince = sitNow ? Now - _sitAfter : Now;
    }

    /// <summary>Вика се от ChooseActivity: време ли е да седне или за хоби.</summary>
    private bool FreeCycleStep()
    {
        if (_idleSince <= 0) StartFreeCycle(sitNow: false);
        double free = Now - _idleSince;
        if (free < _sitAfter) return false;
        if (_save.Energy < 35)
        {
            TakeNap(Lines.Pick(Lines.BoredNap, _save.OwnerName));
            StartFreeCycle(sitNow: false);
            return true;
        }
        if (free >= _hobbyAfter) StartAutoHobby();
        else StartScene(Scenes.Sit, quiet: false);
        return true;
    }

    private void UpdateSitting(double now)
    {
        // Работа, храна, подарък, умора: става.
        if (InFocus || WaitingForFood || _save.Energy < 20 || (_save.GiftAt != DateTime.MinValue && DateTime.Now >= _save.GiftAt && !_save.GiftPending))
        {
            double since = _idleSince;
            LeaveScene();
            _idleSince = since;
            return;
        }
        if (NativeMethods.IdleSeconds() > AwayToSleep)
        {
            LeaveScene();
            return;
        }
        if (now - _idleSince >= _hobbyAfter)
        {
            LeaveScene();
            StartAutoHobby();
            return;
        }
        if (!IsQuiet && now > _nextChatter)
        {
            _nextChatter = now + 600 + _rng.Next(600);
            Mutter(Lines.Pick(Scenes.Sit.During, _save.OwnerName), 4);
        }
    }

    /// <summary>Едно от отключените хобита за 10–20 мин (после пак сяда).</summary>
    private void StartAutoHobby()
    {
        var options = Scenes.Fun.Where(f => Scenes.IsUnlocked(f, _save) && f != _lastFun).ToList();
        var scene = options.Count > 0 ? options[_rng.Next(options.Count)] : Scenes.Reading;
        double seconds = (10 + _rng.Next(11)) * 60;
        if (scene == Scenes.Trampoline)
        {
            StartTrampoline(seconds);
            StartFreeCycle(sitNow: true);   // батутът не минава през LeaveScene
            return;
        }
        StartScene(scene, quiet: false);
        _sceneUntil = Now + seconds;
        _autoHobby = true;
    }

    // ── „Няма ли да правим нещо?“ ──

    /// <summary>От време на време (на 45–90 мин) предлага работа; клик върху мечока отваря избора.</summary>
    private void MaybeOfferWork(double now)
    {
        if (now < _nextOffer) return;
        _nextOffer = now + (45 + _rng.Next(46)) * 60;
        if (IsQuiet || InFocus || TimerRunning || IsAsleep || LateNight || NativeMethods.IdleSeconds() > 120) return;
        Say(Lines.Pick(Lines.WorkOffer, _save.OwnerName) + " (цъкни ме)", 8);
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
