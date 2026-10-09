using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace Mecho;

/// <summary>
/// Батутът: малък прозорец с картинка. Хваща се с мишката и се мести по земята
/// (само наляво-надясно) – така Тут насочва мечока.
/// </summary>
public sealed class PropWindow : Window
{
    private readonly Image _image = new();
    private bool _dragging;
    private double _grabX;

    /// <summary>Тут го мести (екранно x на средата).</summary>
    public event Action<double>? Dragged;

    public bool IsDragging => _dragging;
    public double CenterX => Left + Width / 2;

    public PropWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);
        _image.Cursor = System.Windows.Input.Cursors.SizeWE;
        _image.ToolTip = Ui.Tip("Хвани батута и го мести под мечока! Център = нагоре, ляво/дясно = настрани.");
        Content = _image;
        SourceInitialized += (_, _) => NativeMethods.MakeToolWindow(new WindowInteropHelper(this).Handle);

        _image.MouseLeftButtonDown += (_, e) =>
        {
            _dragging = true;
            _grabX = e.GetPosition(this).X;
            _image.CaptureMouse();
            e.Handled = true;
        };
        _image.MouseMove += (_, e) =>
        {
            if (!_dragging) return;
            Left += e.GetPosition(this).X - _grabX;
            Dragged?.Invoke(CenterX);
        };
        _image.MouseLeftButtonUp += (_, e) =>
        {
            _dragging = false;
            _image.ReleaseMouseCapture();
            e.Handled = true;
        };
    }

    public void ShowAt(Prop prop, double pixelSize, double centerX, double bottom)
    {
        _image.Source = prop.Image;
        Width = _image.Width = prop.Image.PixelWidth * pixelSize;
        Height = _image.Height = prop.Image.PixelHeight * pixelSize;
        Left = centerX - Width / 2;
        Top = bottom - Height;
        if (!IsVisible) Show();
    }

    public void MoveTo(double centerX) => Left = centerX - Width / 2;
}

// Батутът като мини игра: мечокът подскача, Тут мести батута под него.
// Удари ли центъра, отива право нагоре; ляво или дясно – натам. Каквото храна
// мине във въздуха, изяжда. Ако Тут не пипа батута, мечокът играе сам.
public sealed partial class PetWindow
{
    private PropWindow? _trampWindow;
    private bool _onTrampoline;       // в играта с батута (скача, пада или се връща към него)
    private double _trampTop;         // висината на батута (екранно y, където стъпва)
    private double _bounceUntil;      // 0 = докато Тут не каже „Стани“
    private double _bounceVx, _bounceVy;
    private bool _hunting;            // пуснат е заради храна: свършва, когато я изяде
    private double _userSteerUntil;   // Тут мести батута: до тогава не играе сам

    private const double BounceSideSpeed = 520;   // DIP/s при удар в самия край

    private Prop TrampolineProp => _lib.GetProp("trampoline", 28, 8, 4);
    private double TrampHalf => (_trampWindow?.Width ?? TrampolineProp.Image.PixelWidth * PixelSize) / 2;
    private double TrampX => _trampWindow?.CenterX ?? _x;

    public bool IsBouncing => _onTrampoline;

    /// <summary>Скача на батута (seconds = 0: докато не кажеш „Стани“).</summary>
    private void StartTrampoline(double seconds, bool quiet = false)
    {
        _onTrampoline = true;
        _hunting = false;
        _bounceUntil = seconds > 0 ? Now + seconds : 0;
        if (!quiet) Say(Lines.Pick(Scenes.Trampoline.Start, _save.OwnerName), 3);
        PlaceTrampoline(_x);
        BeginBounce();
    }

    /// <summary>Из екрана има храна: вади батута и отива да си я хване.</summary>
    private void HuntFoodWithTrampoline()
    {
        if (_food.Count == 0) return;
        _onTrampoline = true;
        _hunting = true;
        _bounceUntil = 0;
        Say(Lines.Pick(Lines.TrampolineHunt, _save.OwnerName), 3);
        PlaceTrampoline(_x);
        BeginBounce();
    }

    private void PlaceTrampoline(double x)
    {
        if (_trampWindow == null)
        {
            _trampWindow = new PropWindow();
            _trampWindow.Dragged += cx =>
            {
                // Батутът стои на екрана и на земята.
                var area = Area;
                double clamped = Math.Clamp(cx, area.Left + TrampHalf, area.Right - TrampHalf);
                if (Math.Abs(clamped - cx) > 0.5) _trampWindow.MoveTo(clamped);
                _userSteerUntil = Now + 5;
            };
        }
        var prop = TrampolineProp;
        double ground = Area.Bottom;
        _trampWindow.ShowAt(prop, PixelSize, x, ground);
        _trampTop = ground - prop.Seat * PixelSize;
        NativeMethods.BringToTop(new WindowInteropHelper(this).Handle);
    }

    private void BeginBounce()
    {
        if (!_onTrampoline) return;
        if (_trampWindow is not { IsVisible: true }) PlaceTrampoline(_x);
        _x = TrampX;
        _y = _trampTop;
        _facingLeft = false;
        SetState(BearState.Bounce, "bounce");
        NativeMethods.BringToTop(new WindowInteropHelper(this).Handle);
        _bounceVx = 0;
        LaunchFromTrampoline(0);
    }

    private bool UserSteering => Now < _userSteerUntil || _trampWindow?.IsDragging == true;

    /// <summary>
    /// Отскок. offset е къде е паднал спрямо батута (-1 ляв край, 0 център, +1 десен).
    /// Ако Тут не играе, мечокът сам се насочва към най-близката храна.
    /// </summary>
    private void LaunchFromTrampoline(double offset)
    {
        var area = Area;
        double height = Math.Min(area.Height * 0.55, 520);
        if (!UserSteering && _food.Count > 0)
        {
            // Играе сам: прицелва се така, че да мине през храната на върха на скока.
            var food = _food.Where(f => !f.IsDragging).OrderBy(f => Math.Abs(f.Left + f.Width / 2 - _x)).FirstOrDefault();
            if (food != null)
            {
                height = Math.Clamp(_trampTop - food.Top - food.Height / 2, 60, area.Height - 40);
                double up = Math.Sqrt(2 * Gravity * height);
                double toApex = up / Gravity;
                _bounceVx = Math.Clamp((food.Left + food.Width / 2 - _x) / toApex, -900, 900);
                _bounceVy = -up;
                return;
            }
        }
        if (!UserSteering && _food.Count == 0) height = 60 + _rng.NextDouble() * 160;
        // Центърът (±20%) праща право нагоре; иначе настрани според мястото.
        _bounceVx = Math.Abs(offset) < 0.2 ? 0 : offset * BounceSideSpeed;
        _bounceVy = -Math.Sqrt(2 * Gravity * height);
    }

    private void UpdateBounce(double dt)
    {
        var area = Area;
        _bounceVy += Gravity * dt;
        _x += _bounceVx * dt;
        _y += _bounceVy * dt;

        // Отскача от страните на екрана.
        double half = SpriteWidth / 2;
        if (_x < area.Left + half) { _x = area.Left + half; _bounceVx = Math.Abs(_bounceVx) * 0.8; }
        if (_x > area.Right - half) { _x = area.Right - half; _bounceVx = -Math.Abs(_bounceVx) * 0.8; }
        if (_bounceVx != 0) _facingLeft = _bounceVx < 0;

        // Играе сам: батутът тръгва към мястото, където ще падне.
        if (!UserSteering && _trampWindow != null && _bounceVy > 0)
        {
            double fallTime = Math.Max(0.05, (_trampTop - _y) / Math.Max(1, _bounceVy));
            double landX = Math.Clamp(_x + _bounceVx * Math.Min(fallTime, 1.5), area.Left + TrampHalf, area.Right - TrampHalf);
            double step = Math.Clamp(landX - TrampX, -1200 * dt, 1200 * dt);
            _trampWindow.MoveTo(TrampX + step);
        }

        if (_bounceVy < 0 || _y < _trampTop) return;

        // Пада: на батута ли е?
        double offset = (_x - TrampX) / TrampHalf;
        if (Math.Abs(offset) <= 1.1)
        {
            _y = _trampTop;
            if (ShouldStopBouncing())
            {
                EndBounce(_hunting ? Lines.Pick(Lines.AllFoodEaten, _save.OwnerName) : Lines.Pick(Scenes.Trampoline.End, _save.OwnerName));
                return;
            }
            if (!IsQuiet && _rng.Next(5) == 0) Say(Lines.Pick(Scenes.Trampoline.During, _save.OwnerName), 2);
            LaunchFromTrampoline(Math.Clamp(offset, -1, 1));
            return;
        }

        // Изпусна батута: пада на земята и се връща на него.
        double ground = Area.Bottom;
        if (_y < ground) return;
        _y = ground;
        Say(Lines.Pick(Lines.MissedTrampoline, _save.OwnerName), 3);
        Play("fall", after: () =>
        {
            if (!_onTrampoline) return;
            if (ShouldStopBouncing()) EndBounce(null);
            else WalkTo(Math.Clamp(TrampX, area.Left + HalfWidth, area.Right - HalfWidth), BeginBounce);
        });
    }

    private bool ShouldStopBouncing()
    {
        bool stay = _save.StayPut && _save.StayScene == Scenes.Trampoline.Id;
        if (stay) return false;
        if (_hunting) return _food.Count == 0;
        return _bounceUntil > 0 && Now > _bounceUntil && _food.Count == 0;
    }

    private void EndBounce(string? line)
    {
        _onTrampoline = false;
        _hunting = false;
        _trampWindow?.Hide();
        if (line != null) Say(line, 3);
        if (_state is BearState.Bounce or BearState.Busy)
        {
            _y = Ground(_y);
            SetIdle();
        }
    }
}
