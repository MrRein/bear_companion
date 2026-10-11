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
    private double _freeUntil = 25 * 60;    // докога продължава разходката (после избира блок)
    private double _lastHobbyAt = -3600;
    private bool _autoBlock;                // седи или прави хоби по свой избор (не на Тут)
    private double _nextOffer = 45 * 60;    // кога пак да предложи работа
    private double _offerUntil;             // докато е в сила предложението (клик го отваря)

    private double Minutes(int from, int to) => (from + _rng.Next(to - from + 1)) * 60;

    /// <summary>След блок (или в началото): кратка разходка, после пак избира.</summary>
    private void StartFreeCycle(bool sitNow) => _freeUntil = Now + (sitNow ? Minutes(3, 10) : Minutes(20, 40));

    /// <summary>Вика се от ChooseActivity: време ли е за нов блок. true = зае се с нещо.</summary>
    private bool FreeCycleStep()
    {
        if (Now < _freeUntil || Cooks) return false;   // докато се готви, не тръгва на хоби

        bool canHobby = Now - _lastHobbyAt > 45 * 60;
        var blocks = new (string Name, double Weight)[]
        {
            ("wander", 3),
            ("sit", 4),
            ("hobby", canHobby ? 2.5 : 0),
            ("nap", _save.Energy < 45 ? 5 : _save.Energy < 65 ? 1 : 0),
        };
        double roll = _rng.NextDouble() * blocks.Sum(b => b.Weight);
        string pick = blocks[^1].Name;
        foreach (var b in blocks)
        {
            if (roll < b.Weight)
            {
                pick = b.Name;
                break;
            }
            roll -= b.Weight;
        }

        switch (pick)
        {
            case "sit":
                StartScene(Scenes.Sit, quiet: false);
                _sceneUntil = Now + Minutes(15, 45);
                _autoBlock = true;
                return true;
            case "hobby":
                StartAutoHobby();
                return true;
            case "nap":
                TakeNap(Lines.Pick(Lines.BoredNap, _save.OwnerName));
                StartFreeCycle(sitNow: true);
                return true;
            default:
                _freeUntil = Now + Minutes(15, 40);
                return false;
        }
    }

    private void UpdateSitting(double now)
    {
        // Работа, храна, подарък, умора: става (и после пак избира).
        if (InFocus || WaitingForFood || _save.Energy < 20 || (_save.GiftAt != DateTime.MinValue && DateTime.Now >= _save.GiftAt && !_save.GiftPending))
        {
            LeaveScene();
            return;
        }
        if (NativeMethods.IdleSeconds() > AwayToSleep)
        {
            LeaveScene();
            return;
        }
        if (!IsQuiet && now > _nextChatter)
        {
            _nextChatter = now + 600 + _rng.Next(600);
            Mutter(Lines.Pick(Scenes.Sit.During, _save.OwnerName), 4);
        }
    }

    /// <summary>Едно от отключените хобита за 10–20 мин.</summary>
    private void StartAutoHobby()
    {
        var options = Scenes.Fun.Where(f => Scenes.IsUnlocked(f, _save) && f != _lastFun).ToList();
        var scene = options.Count > 0 ? options[_rng.Next(options.Count)] : Scenes.Reading;
        double seconds = Minutes(10, 20);
        _lastHobbyAt = Now;
        if (scene == Scenes.Trampoline)
        {
            StartTrampoline(seconds);
            _freeUntil = Now + seconds + Minutes(3, 10);   // батутът не минава през LeaveScene
            return;
        }
        StartScene(scene, quiet: false);
        _sceneUntil = Now + seconds;
        _autoBlock = true;
    }

    // ── „Няма ли да правим нещо?“ ──

    /// <summary>От време на време (на 45–90 мин) предлага работа; клик върху мечока отваря избора.</summary>
    private void MaybeOfferWork(double now)
    {
        if (now < _nextOffer) return;
        _nextOffer = now + Minutes(45, 90);
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
