using System;
using System.Windows.Interop;

namespace Mecho;

// Full screen (video, game, presentation) on the bear's monitor: he says something like
// „Ще се разходя малко“, walks out through the edge and is gone – the loop then runs slowly
// (hidden), so he costs almost nothing. He comes back once nothing has been full screen for
// a few minutes (a pause, or leaving full screen for a moment, does not bring him back).
// Asleep, or busy cooking/eating: he is just hidden quietly and shown again later.
public sealed partial class PetWindow
{
    /// <summary>Full screen must last this long before he leaves (seconds).</summary>
    private const double FullscreenLeaveAfter = 8;

    /// <summary>And must be gone this long before he comes back (seconds).</summary>
    private const double FullscreenBackAfter = 5 * 60;

    private const double FullscreenCheckEvery = 2;

    private double _nextFullscreenCheck, _fullscreenSince = -1, _fullscreenOffSince = -1;
    private bool _fullscreenHidden;   // hidden quietly (asleep/busy), not on a walk
    private bool _fullscreenStay;     // she called him back during full screen: no leaving until it ends

    /// <summary>The check; the self-test replaces it.</summary>
    internal Func<bool> FullscreenNow;

    private bool DetectFullscreen() =>
        NativeMethods.FullscreenOnMonitorOf(new WindowInteropHelper(this).Handle);

    /// <summary>Every loop (cheap: really checks every 2 s).</summary>
    private void UpdateFullscreen(double now)
    {
        if (now < _nextFullscreenCheck) return;
        _nextFullscreenCheck = now + FullscreenCheckEvery;
        bool full = FullscreenNow();
        if (full)
        {
            _fullscreenOffSince = -1;
            if (_fullscreenStay) return;
            if (_fullscreenSince < 0) _fullscreenSince = now;
            if (now - _fullscreenSince >= FullscreenLeaveAfter) LeaveForFullscreen();
        }
        else
        {
            _fullscreenSince = -1;
            _fullscreenStay = false;
            if (!_save.WalkFullscreen && !_fullscreenHidden) return;
            if (_fullscreenOffSince < 0) _fullscreenOffSince = now;
            if (now - _fullscreenOffSince >= FullscreenBackAfter) BackAfterFullscreen();
        }
    }

    private void LeaveForFullscreen()
    {
        if (_save.WalkFullscreen || _fullscreenHidden || OnWalk || !IsVisible) return;
        if (_state is BearState.Drag or BearState.Falling) return;   // she is holding him
        // Asleep or busy (cooking, eating): no walk – just out of the way.
        if (IsAsleep || BusyEating() || _cook != null || _pendingCook != null)
        {
            _fullscreenHidden = true;
            HideBubble();
            Hide();
            return;
        }
        // Long enough to cover a film; he comes back earlier, when full screen ends.
        GoForWalk(4 * 60, solo: false, fullscreen: true);
    }

    private void BackAfterFullscreen()
    {
        _fullscreenOffSince = -1;
        if (_fullscreenHidden)
        {
            _fullscreenHidden = false;
            ShowBear();
            return;
        }
        if (OnWalk) ReturnFromWalk(called: false);
        else
        {
            _save.WalkFullscreen = false;
            Persist();
        }
    }
}
