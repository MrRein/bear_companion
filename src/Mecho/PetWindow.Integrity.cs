using System;

namespace Mecho;

// Предпазна мрежа: поведението е верига от „отиди до…, после…“ и ако нещо прекъсне
// веригата (Тут цъкне, дойде храна, падне), мечокът може да остане в междинно
// състояние – наполовина зад ръба, „излиза на разходка“ завинаги… Веднъж в секунда
// тук се проверява, че всяко такова състояние още има кой да го доведе до края; ако
// няма – веригата се подновява или състоянието се изчиства.
// (Дългосрочно: всяко занимание става отделен обект с Enter/Exit – виж ARCHITECTURE.md.)
public sealed partial class PetWindow
{
    private double _nextIntegrity;
    private double _strayPeekSince = -1;

    private void CheckIntegrity(double now)
    {
        if (now < _nextIntegrity) return;
        _nextIntegrity = now + 1;
        bool held = _state is BearState.Drag or BearState.Falling or BearState.Jump;
        if (held) return;

        // Тръгнал е на разходка, но нещо го е отклонило: пак тръгва към ръба.
        if (_walkingOut && _state != BearState.Walk) WalkOutToEdge();

        // Връщаше се от разходка, а вече не върви: значи е вътре.
        if (_walkingBack && _state != BearState.Walk) _walkingBack = false;

        // Наполовина зад ръба без причина (прекъснато надничане) повече от 3 с: влиза.
        bool peekOk = _giftRun != GiftRun.None || _state == BearState.Walk || (_state == BearState.Busy && _anim is "idle" or "happy");
        if (_peeking && !peekOk)
        {
            if (_strayPeekSince < 0) _strayPeekSince = now;
            else if (now - _strayPeekSince > 3)
            {
                _peeking = false;
                _strayPeekSince = -1;
                ClampX();
            }
        }
        else _strayPeekSince = -1;
    }
}
