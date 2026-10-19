using System;
using System.Windows;

namespace Mecho;

// „Разходи се“: мечокът излиза през най-близкия ръб на екрана и го няма за
// избраното време (10 мин, 30 мин, 1 ч или свое). Докато го няма, нищо не се
// случва (подаръци, гости, пеперуди). Таймерите си текат. Връща се сам, когато
// времето изтече, или по-рано – ако Тут цъкне мечето до часовника.
public sealed partial class PetWindow
{
    private bool _walkingOut;     // върви към ръба
    private bool _walkingBack;    // тъкмо се е върнал (влиза отстрани)

    /// <summary>На разходка е (или тръгва): няма го на екрана.</summary>
    public bool OnWalk => _walkingOut || _save.WalkUntil > DateTime.Now;

    public TimeSpan WalkLeft => _save.WalkUntil > DateTime.Now ? _save.WalkUntil - DateTime.Now : TimeSpan.Zero;

    public void GoForWalk(double minutes) => GoForWalk(minutes, solo: false);

    /// <summary>Тръгва на разходка; solo – сам се е сетил (без „Разходи се“ от менюто).</summary>
    private void GoForWalk(double minutes, bool solo)
    {
        if (minutes <= 0 || OnWalk) return;
        if (IsAsleep)
        {
            Say(R.Any("walk.asleep"), 3);
            return;
        }
        if (BusyEating() ) return;
        if (_cook != null || _pendingCook != null)
        {
            Say(R.Pick("walk.cooking", _save.OwnerName), 4);
            return;
        }
        if (_onTrampoline) EndBounce(null);
        if (_inScene) LeaveScene();
        _save.StayPut = false;
        _walkingOut = true;
        _save.WalkUntil = DateTime.Now.AddMinutes(minutes);
        // Вечер и много му се спи: ще посънча навън – разходката се брои за сън.
        _save.WalkNap = !solo && (LateForBed || DateTime.Now.Hour >= 19) && _save.Energy < TiredBelow;
        _save.WalkSolo = solo;
        Persist();
        // Работим (мечо-доро или работен таймер): „Добре, ти работи, аз ще се разходя.“
        string leave = _save.WalkNap ? "walk.leave.nap" : solo ? "walk.leave.solo" : InFocus ? "walk.leave.work" : "walk.leave";
        Say(R.Pick(leave, _save.OwnerName), 3);
        if (_state is BearState.Busy or BearState.Couch) SetIdle();
        WalkOutToEdge();
        NotifyCare();
    }

    /// <summary>
    /// Към по-близкия ръб: средата му остава в екрана (иначе при два монитора ще
    /// излезе на другия), а стигне ли ръба, се скрива. Вика се и наново, ако нещо го
    /// е отклонило по пътя (виж CheckIntegrity).
    /// </summary>
    private void WalkOutToEdge()
    {
        var area = Area;
        bool left = _x - area.Left < area.Right - _x;
        double edge = left ? area.Left + 1 : area.Right - 1;
        WalkTo(edge, () =>
        {
            if (!_walkingOut) return;
            _walkingOut = false;
            HideBubble();
            Hide();
            NotifyCare();
        });
    }

    private double _nextSoloWalk = 90 * 60;   // най-рано кога пак може да тръгне сам

    /// <summary>
    /// Рядко, когато никой не му е казал какво да прави, сам излиза за 2, 5, 10 или
    /// 15 минути (вика се от FreeCycleStep). Връща се сам или от мечето до часовника.
    /// </summary>
    private bool MaybeWanderOff()
    {
        // Докато менюто е отворено, не излиза сам от екрана (да не изчезне, докато Тут избира нещо за него).
        if (Now < _nextSoloWalk || MenuIsOpen) return false;
        _nextSoloWalk = Now + Minutes(120, 240);
        if (_rng.NextDouble() > 0.3) return false;
        if (OnWalk || IsAsleep || InFocus || TimerRunning || Cooks || _pendingCook != null || CarriesGift || _eating != null
            || LateNight || _save.Energy < 15 || WaitingForFood || _save.GiftPending) return false;
        int[] choices = { 2, 5, 10, 15 };
        GoForWalk(choices[_rng.Next(choices.Length)], solo: true);
        return OnWalk;
    }

    /// <summary>Вика се всеки цикъл: времето изтече ли – връща се.</summary>
    private void UpdateWalkAway()
    {
        if (_walkingOut || _save.WalkUntil == DateTime.MinValue) return;
        if (DateTime.Now >= _save.WalkUntil) ReturnFromWalk(called: false);
    }

    /// <summary>Връща се от разходката (сам или защото Тут го повика).</summary>
    public void ReturnFromWalk(bool called)
    {
        if (_save.WalkUntil == DateTime.MinValue && !_walkingOut) return;
        _walkingOut = false;
        bool napped = _save.WalkNap, solo = _save.WalkSolo;
        _save.WalkNap = false;
        _save.WalkSolo = false;
        _save.WalkUntil = DateTime.MinValue;
        Persist();
        // Влиза отстрани, откъдето е по-близо до мястото, където беше.
        var area = Area;
        bool left = _x - area.Left < area.Right - _x;
        // Ако още работим, влиза поне 1/5 от екрана навътре и чак тогава сяда да работи.
        double depth = InFocus ? Math.Max(SpriteWidth * 1.5, area.Width / 5) : SpriteWidth * 1.5;
        double inside = left ? area.Left + depth : area.Right - depth;
        _x = left ? area.Left - SpriteWidth / 2 : area.Right + SpriteWidth / 2;
        _y = Ground(area.Bottom);
        _walkingBack = true;
        ShowBear();   // вече на новото място (иначе за миг се мярка там, откъдето излезе)
        WalkTo(inside, () =>
        {
            _walkingBack = false;
            _facingLeft = !left;
            Say(R.Pick(called ? "walk.called" : napped ? "walk.back.nap" : solo ? "walk.back.solo" : "walk.back", _save.OwnerName), 5);
            Emote(called ? "question" : "happy");
            React("happy");
        });
        NotifyCare();
    }
}
