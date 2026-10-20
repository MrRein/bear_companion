using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Mecho;

// Няколко монитора: тръгне ли към уред или мебел на друг екран, мечокът не минава
// „по въздуха“ между тях – стига до ръба на своя екран откъм целта и излиза, а след
// 5–6 секунди се показва на ръба на другия екран (откъм страната, от която идва) и
// продължава до целта.
public sealed partial class PetWindow
{
    private int _crossPhase;          // 0 – не минава; 1 – върви към ръба; 2 – излязъл е, чака
    private double _crossEdge, _crossAt;
    private Rect _crossFrom;

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
    /// Ходене (от UpdateWalk): към ръба, ако целта е на друг екран; докато го няма – чака;
    /// после пак обикновено ходене към целта. Връща true, ако е поело кадъра.
    /// </summary>
    private bool UpdateCrossing(double dt)
    {
        if (_crossPhase == 0 && OnOtherScreen(_walkTarget))
        {
            _crossPhase = 1;
            _crossFrom = Area;
            _crossEdge = _walkTarget > _x ? _crossFrom.Right - 1 : _crossFrom.Left + 1;
        }
        if (_crossPhase == 1)
        {
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
            if (Now >= _crossAt) ArriveOnTargetScreen();
            return true;
        }
        return false;
    }

    /// <summary>Показва се на ръба на екрана с целта (откъм страната, от която идва) и продължава.</summary>
    private void ArriveOnTargetScreen()
    {
        var to = ScreenAreaFor(_walkTarget);
        bool fromLeft = _crossFrom.Left + _crossFrom.Width / 2 < to.Left + to.Width / 2;
        _x = fromLeft ? to.Left + 1 : to.Right - 1;
        _y = to.Bottom;
        _facingLeft = !fromLeft;
        _crossPhase = 0;
        _stateTime = 0;   // ходенето до целта почва отначало (иначе спира след минута)
        ShowBear();
    }

    /// <summary>Нещо друго го прекъсна, докато го нямаше: показва се на другия екран.</summary>
    private void CancelCrossing()
    {
        if (_crossPhase == 2 && !IsVisible) ArriveOnTargetScreen();
        _crossPhase = 0;
    }
}
