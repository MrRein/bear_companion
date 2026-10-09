using System;
using System.Windows;
using System.Windows.Interop;

namespace Mecho;

// Рисуване на платформи и как мечокът стъпва, пада и скача по тях.
public sealed partial class PetWindow
{
    private readonly DrawingStore _drawings = new();
    private DrawWindow? _drawWindow;

    // Скок към платформа
    private double _jumpFromX, _jumpFromY, _jumpToX, _jumpToY, _jumpT;

    /// <summary>Колко широко стъпва (половин ширина на стъпалата или на седалката).</summary>
    private double FeetHalf => _inScene && _seatProp != null ? _seat.Width * 0.4 : Math.Max(6, SpriteWidth * 0.22);

    /// <summary>Земята под мечока: нарисувана платформа или долният край на екрана.</summary>
    private double Ground(double feetY) => _drawings.GroundAt(Area, _x, FeetHalf, feetY);

    /// <summary>При пускане: показва рисунките от минал път на всички екрани.</summary>
    private void InitDrawings()
    {
        double dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this).DpiScaleX;
        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
            var wa = screen.WorkingArea;
            var area = new Rect(wa.Left / dpi, wa.Top / dpi, wa.Width / dpi, wa.Height / dpi);
            _drawings.For(area, PixelSize).ShowLayer(true);
        }
        NativeMethods.BringToTop(new WindowInteropHelper(this).Handle);
    }

    /// <summary>„Рисувай“: върху екрана на мечока се отваря платно с четка, гума и цветове.</summary>
    public void OpenDrawing()
    {
        if (_drawWindow != null)
        {
            _drawWindow.Activate();
            return;
        }
        if (MenuIsOpen) _menu!.Hide();
        if (ChatIsOpen) _chat!.Hide();
        var canvas = _drawings.For(Area, PixelSize);
        _drawWindow = new DrawWindow(this, canvas);
        _drawWindow.Open();
        Say(Lines.Pick(Lines.DrawStart, _save.OwnerName), 5);
    }

    internal void DrawingClosed(DrawingCanvas canvas)
    {
        _drawWindow = null;
        canvas.Save(DrawingStore.Folder);
        canvas.ShowLayer(true);
        NativeMethods.BringToTop(new WindowInteropHelper(this).Handle);
        if (!canvas.IsEmpty)
        {
            Say(Lines.Pick(Lines.DrawDone, _save.OwnerName), 4);
            if (_state is BearState.Idle or BearState.Walk) Climb();
        }
    }

    /// <summary>Тук ли има нарисувано, по което да се катери.</summary>
    private bool HasPlatforms => _drawings.Existing(Area) != null;

    /// <summary>Стои ли на платформа (а не на земята).</summary>
    private bool OnPlatform => _y < Area.Bottom - 1;

    /// <summary>Намира платформа над себе си, отива под нея и скача.</summary>
    private void Climb()
    {
        var ledge = _drawings.FindLedge(Area, _x, _y, 280, 30, 230, _rng);
        if (ledge is not Point target)
        {
            SetIdle();
            return;
        }
        double side = Math.Sign(target.X - _x);
        double from = target.X - (side == 0 ? 1 : side) * 50;
        var area = Area;
        from = Math.Clamp(from, area.Left + HalfWidth, area.Right - HalfWidth);
        WalkTo(from, () => StartJump(target.X, target.Y));
    }

    private void StartJump(double x, double y)
    {
        _jumpFromX = _x;
        _jumpFromY = _y;
        _jumpToX = x;
        _jumpToY = y;
        _jumpT = 0;
        _facingLeft = x < _x;
        SetState(BearState.Jump, "happy");
        Say("Хоп!", 1.5);
    }

    private void UpdateJump(double dt)
    {
        _jumpT = Math.Min(1, _jumpT + dt / 0.7);
        double height = Math.Max(40, _jumpFromY - _jumpToY + 40);
        _x = _jumpFromX + (_jumpToX - _jumpFromX) * _jumpT;
        _y = _jumpFromY + (_jumpToY - _jumpFromY) * _jumpT - Math.Sin(Math.PI * _jumpT) * height * 0.5;
        if (_jumpT < 1) return;
        _x = _jumpToX;
        _y = _jumpToY;
        SetIdle();
        Say(Lines.Pick(Lines.Climbed, _save.OwnerName), 3);
    }

    /// <summary>Къде да се разходи: ако е на платформа, най-често остава на нея.</summary>
    private double WalkTarget()
    {
        var area = Area;
        if (OnPlatform && _rng.NextDouble() < 0.75 && _drawings.PlatformSpan(area, _x, _y) is var (left, right))
        {
            double margin = FeetHalf;
            if (right - left > margin * 2 + 10)
                return left + margin + _rng.NextDouble() * (right - left - 2 * margin);
            return _x;
        }
        return area.Left + 60 + _rng.NextDouble() * Math.Max(0, area.Width - 120);
    }
}
