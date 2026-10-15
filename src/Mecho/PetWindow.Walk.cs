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

    public void GoForWalk(double minutes)
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
        Persist();
        Say(R.Pick("walk.leave", _save.OwnerName), 3);
        // Към по-близкия ръб и малко отвъд него.
        var area = Area;
        bool left = _x - area.Left < area.Right - _x;
        double edge = left ? area.Left - SpriteWidth : area.Right + SpriteWidth;
        Later(1.2, () =>
        {
            if (!_walkingOut) return;
            WalkTo(edge, () =>
            {
                _walkingOut = false;
                HideBubble();
                Hide();
                NotifyCare();
            });
        });
        NotifyCare();
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
        _save.WalkUntil = DateTime.MinValue;
        Persist();
        // Влиза отстрани, откъдето е по-близо до мястото, където беше.
        var area = Area;
        bool left = _x - area.Left < area.Right - _x;
        double inside = left ? area.Left + SpriteWidth * 1.5 : area.Right - SpriteWidth * 1.5;
        _x = left ? area.Left - SpriteWidth / 2 : area.Right + SpriteWidth / 2;
        _y = Ground(area.Bottom);
        if (!IsVisible) Show();
        _walkingBack = true;
        WalkTo(inside, () =>
        {
            _walkingBack = false;
            _facingLeft = !left;
            Say(R.Pick(called ? "walk.called" : "walk.back", _save.OwnerName), 4);
            Emote(called ? "question" : "happy");
            if (CanAnimateFreely) Play("happy");
        });
        NotifyCare();
    }
}
