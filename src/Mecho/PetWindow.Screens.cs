using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Mecho;

// Няколко монитора: тръгне ли към уред или мебел на друг екран, мечокът не минава
// „по въздуха“ между тях – стига до ръба на своя екран откъм целта и излиза, а след
// 5–6 секунди влиза с ходене иззад ръба на другия екран (откъм страната, от която
// идва) и продължава до целта. Прекъснат ли е, докато го няма – пак влиза с ходене.
// Важи за всяко ходене към нещо: обикновеното (WalkTo) и до предмета на кътчето
// (бюро, статив, диван… – FollowSceneAnchor), напр. като го преместиш на друг екран.
public sealed partial class PetWindow
{
    private int _crossPhase;          // 0 – не минава; 1 – върви към ръба; 2 – излязъл е, чака
    private double _crossEdge, _crossAt;
    private Rect _crossFrom, _crossTo;
    private bool _crossEntering;      // влиза иззад ръба на новия екран (още не е целият вътре)
    private double _crossTarget = double.NaN;   // накъде е тръгнал (на другия екран)
    private double _crossTouched;     // кога за последно някое ходене е питало за минаването

    /// <summary>Работната площ на екрана, в който е x (по хоризонтала); при екрани един над друг – по-близкият до мечока.</summary>
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

    /// <summary>Целта е на друг екран (а мечокът е видим и в своя).</summary>
    private bool OnOtherScreen(double target)
    {
        if (double.IsNaN(target) || !IsVisible || _walkingOut || _walkingBack || OnWalk) return false;
        var here = Area;
        if (_x < here.Left || _x > here.Right) return false;
        if (target >= here.Left && target <= here.Right) return false;
        return ScreenAreaFor(target) != here;
    }

    /// <summary>
    /// Ходене към target (от UpdateWalk или до предмета на кътчето): към ръба, ако целта е
    /// на друг екран; докато го няма – чака; после пак обикновено ходене към целта.
    /// Връща true, ако е поело кадъра.
    /// </summary>
    private bool UpdateCrossing(double dt) => UpdateCrossing(_walkTarget, dt);

    private bool UpdateCrossing(double target, double dt)
    {
        _crossTouched = Now;
        // Влиза иззад ръба: докато не е целият в новия екран, не мисли за „друг екран“.
        if (_crossEntering)
        {
            if (_x - SpriteWidth / 2 >= _crossTo.Left && _x + SpriteWidth / 2 <= _crossTo.Right) _crossEntering = false;
            return false;
        }
        if (_crossPhase == 0 && OnOtherScreen(target))
        {
            _crossPhase = 1;
            _crossFrom = Area;
            _crossTarget = target;
            _crossEdge = target > _x ? _crossFrom.Right - 1 : _crossFrom.Left + 1;
        }
        if (_crossPhase == 1)
        {
            string walk = IsExhausted ? "walk_tired" : "walk";
            if (_anim != walk)
            {
                _anim = walk;
                _animTime = 0;
            }
            if (StepTowards(_crossEdge, IsExhausted ? WalkSpeed * 0.6 : WalkSpeed, dt))
            {
                // Стигна ръба: излиза.
                _crossPhase = 2;
                _crossAt = Now + 5 + _rng.NextDouble();
                HideBubble();
                Hide();
            }
            return true;
        }
        if (_crossPhase == 2)
        {
            if (Now >= _crossAt) ArriveOnTargetScreen(_crossTarget);
            return true;
        }
        return false;
    }

    /// <summary>Никое ходене вече не пита за минаването (прекъснаха го, вдигнаха го…).</summary>
    private bool CrossingAbandoned => _crossPhase != 0 && Now - _crossTouched > 0.5;

    /// <summary>Застава иззад ръба на екрана с целта (откъм страната, от която идва) и се показва.</summary>
    private void ArriveOnTargetScreen(double target)
    {
        var to = ScreenAreaFor(target);
        bool fromLeft = _crossFrom.Left + _crossFrom.Width / 2 < to.Left + to.Width / 2;
        _x = fromLeft ? to.Left - SpriteWidth / 2 : to.Right + SpriteWidth / 2;
        _y = to.Bottom;
        _facingLeft = !fromLeft;
        _crossTo = to;
        _crossEntering = true;
        _crossPhase = 0;
        _stateTime = 0;   // ходенето до целта почва отначало (иначе спира след минута)
        ShowBear();
    }

    /// <summary>
    /// Нещо друго го прекъсна, докато го нямаше: пак влиза с ходене иззад ръба на другия
    /// екран (малко навътре), вместо изведнъж да се появи.
    /// </summary>
    private void CancelCrossing()
    {
        if (_crossPhase == 2 && !IsVisible)
        {
            ArriveOnTargetScreen(_crossTarget);
            bool fromLeft = _x < _crossTo.Left;
            double inside = fromLeft ? _crossTo.Left + SpriteWidth * 1.5 : _crossTo.Right - SpriteWidth * 1.5;
            WalkTo(inside);
            return;
        }
        _crossPhase = 0;
    }
}
