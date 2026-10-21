using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Mecho;

// Няколко монитора. Тръгне ли мечокът към нещо на друг екран (уред, мебел, кътчето си…):
//   1. отива до най-близкия долен ъгъл на своя екран и излиза от него (целият зад ръба);
//   2. скрива се;
//   3. появява се зад най-близкия до нещото долен ъгъл на другия екран и влиза оттам;
//   4. щом е целият вътре – продължава обикновено към нещото.
// Важи за всяко ходене към нещо (WalkTo и до предмета на кътчето – FollowSceneAnchor).
// Прекъснат ли е, докато го няма (заспа, вдигнаха го от менюто…) – пак влиза от ъгъла.
// Старото (до 1.0.98) е в archive/PetWindow.Screens.v1.cs.txt.
public sealed partial class PetWindow
{
    private enum Hop { None, Leaving, Away, Entering }

    private Hop _hop;
    private double _hopTarget;        // нещото, до което отива (x на другия екран)
    private double _hopX;             // докъде ходи сега: зад ъгъла (излиза) или вътре до ъгъла (влиза)
    private double _hopAt;            // кога да се появи на другия екран
    private double _hopTouched;       // кога за последно някое ходене е питало (иначе е прекъснат)

    /// <summary>Минава между екраните (излиза, няма го или влиза): земята и ръбовете не важат.</summary>
    private bool Hopping => _hop != Hop.None;

    /// <summary>Работната площ на екрана, в който е x; при екрани един над друг – по-близкият до мечока.</summary>
    private Rect ScreenAreaFor(double x)
    {
        double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var areas = System.Windows.Forms.Screen.AllScreens
            .Select(s => new Rect(s.WorkingArea.Left / dpi, s.WorkingArea.Top / dpi, s.WorkingArea.Width / dpi, s.WorkingArea.Height / dpi))
            .Where(a => x >= a.Left && x < a.Right)
            .OrderBy(a => Math.Abs(a.Bottom - _y))
            .ToList();
        return areas.Count > 0 ? areas[0] : AreaAt(x, _y - 1);
    }

    /// <summary>Нещото е на друг екран (а мечокът е видим и в своя).</summary>
    private bool OnOtherScreen(double target)
    {
        if (double.IsNaN(target) || !IsVisible || _walkingOut || _walkingBack || OnWalk) return false;
        var here = Area;
        if (_x < here.Left || _x > here.Right) return false;
        if (target >= here.Left && target < here.Right) return false;
        return ScreenAreaFor(target) != here;
    }

    /// <summary>Колко зад ръба е, когато е целият навън.</summary>
    private double Outside => SpriteWidth / 2 + PixelSize * 2;

    private bool UpdateCrossing(double dt) => UpdateCrossing(_walkTarget, dt);

    /// <summary>
    /// От всяко ходене към target: ако е на друг екран – води мечока през ъглите и връща
    /// true (ходенето този кадър не мърда); иначе false и ходенето си продължава.
    /// </summary>
    private bool UpdateCrossing(double target, double dt)
    {
        _hopTouched = Now;
        if (_hop == Hop.None)
        {
            if (!OnOtherScreen(target)) return false;
            // 1. Към най-близкия ъгъл на своя екран (не непременно откъм нещото).
            var here = Area;
            bool left = _x - here.Left < here.Right - _x;
            _hop = Hop.Leaving;
            _hopTarget = target;
            _hopX = left ? here.Left - Outside : here.Right + Outside;
        }
        double speed = IsExhausted ? WalkSpeed * 0.6 : WalkSpeed;
        switch (_hop)
        {
            case Hop.Leaving:
                WalkAnim();
                if (StepTowards(_hopX, speed, dt))
                {
                    // 2. Излезе – скрива се.
                    _hop = Hop.Away;
                    _hopAt = Now + 1;
                    HideBubble();
                    Hide();
                }
                return true;
            case Hop.Away:
                if (Now >= _hopAt) AppearAtCorner();
                return true;
            case Hop.Entering:
                WalkAnim();
                if (StepTowards(_hopX, speed, dt))
                {
                    // 4. Целият е вътре: оттук – обикновено ходене към нещото.
                    _hop = Hop.None;
                    _stateTime = 0;   // ходенето почва отначало (иначе спира след минута)
                    return false;
                }
                return true;
        }
        return false;
    }

    /// <summary>3. Зад най-близкия до нещото долен ъгъл на неговия екран – и почва да влиза.</summary>
    private void AppearAtCorner()
    {
        var to = ScreenAreaFor(_hopTarget);
        bool left = _hopTarget - to.Left < to.Right - _hopTarget;
        _x = left ? to.Left - Outside : to.Right + Outside;
        _y = to.Bottom;
        _facingLeft = !left;
        _hopX = left ? to.Left + SpriteWidth / 2 + PixelSize * 2 : to.Right - SpriteWidth / 2 - PixelSize * 2;
        _hop = Hop.Entering;
        ShowBear();
    }

    private void WalkAnim()
    {
        string walk = IsExhausted ? "walk_tired" : "walk";
        if (_anim == walk) return;
        _anim = walk;
        _animTime = 0;
    }

    /// <summary>Никое ходене вече не го води (прекъснаха го, вдигнаха го…).</summary>
    private bool CrossingAbandoned => Hopping && Now - _hopTouched > 0.5;

    /// <summary>
    /// Прекъснат насред минаването. Няма го – пак влиза от ъгъла до нещото (с ходене, не
    /// изведнъж). Тъкмо излиза или влиза – спира там и пак е на земята.
    /// </summary>
    private void CancelCrossing()
    {
        if (_hop == Hop.Away)
        {
            AppearAtCorner();
            WalkTo(_hopX);   // докато влиза, ходенето води UpdateCrossing (Hop.Entering)
            _hopTouched = Now;
            return;
        }
        _hop = Hop.None;
    }
}
