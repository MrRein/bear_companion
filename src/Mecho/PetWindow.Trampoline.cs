using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace Mecho;

/// <summary>Малък прозорец само с картинка, през който кликовете минават (батутът).</summary>
public sealed class PropWindow : Window
{
    private readonly Image _image = new();

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
        Content = _image;
        SourceInitialized += (_, _) => NativeMethods.MakeClickThrough(new WindowInteropHelper(this).Handle);
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
}

// Батутът: мечокът подскача; ако из екрана има храна, отива под нея и я хваща във въздуха.
public sealed partial class PetWindow
{
    private PropWindow? _trampWindow;
    private bool _onTrampoline;       // скача (или отива към батута)
    private double _trampTop;         // докъде стъпва на батута (екранно y)
    private double _bounceUntil;      // 0 = докато Тут не каже „Стани“
    private double _bounceVy;
    private bool _hunting;            // гони храна с батута

    private Prop TrampolineProp => _lib.GetProp("trampoline", 28, 8, 4);

    public bool IsBouncing => _onTrampoline;

    /// <summary>Скача на батута (seconds = 0: докато не кажеш „Стани“).</summary>
    private void StartTrampoline(double seconds, bool quiet = false)
    {
        _onTrampoline = true;
        _hunting = false;
        _bounceUntil = seconds > 0 ? Now + seconds : 0;
        if (!quiet) Say(Lines.Pick(Scenes.Trampoline.Start, _save.OwnerName), 3);
        BeginBounce();
    }

    /// <summary>Отива под най-близката храна и скача към нея с батута.</summary>
    private void HuntFoodWithTrampoline()
    {
        var target = _food.Where(f => !f.IsDragging)
            .OrderBy(f => Math.Abs(f.Left + f.Width / 2 - _x)).FirstOrDefault();
        if (target == null) return;
        _onTrampoline = true;
        _hunting = true;
        _bounceUntil = 0;
        var area = Area;
        double x = Math.Clamp(target.Left + target.Width / 2, area.Left + HalfWidth, area.Right - HalfWidth);
        Say(Lines.Pick(Lines.TrampolineHunt, _save.OwnerName), 3);
        if (Math.Abs(x - _x) > 8) WalkTo(x, BeginBounce);
        else BeginBounce();
    }

    private void BeginBounce()
    {
        if (!_onTrampoline) return;
        var prop = TrampolineProp;
        _trampWindow ??= new PropWindow();
        double ground = Ground(_y);
        _trampWindow.ShowAt(prop, PixelSize, _x, ground);
        _trampTop = ground - prop.Seat * PixelSize;
        _y = _trampTop;
        _facingLeft = false;
        SetState(BearState.Bounce, "bounce");
        NativeMethods.BringToTop(new WindowInteropHelper(this).Handle);
        Launch(NextBounceHeight());
    }

    /// <summary>Колко високо да скочи: до храната над него, иначе за кеф.</summary>
    private double NextBounceHeight()
    {
        var above = _food.Where(f => !f.IsDragging && Math.Abs(f.Left + f.Width / 2 - _x) < 70 && f.Top + f.Height < _trampTop)
            .OrderByDescending(f => f.Top).FirstOrDefault();
        if (above != null) return _trampTop - above.Top + SpriteHeight * 0.3;
        return 60 + _rng.NextDouble() * 160;
    }

    private void Launch(double height) => _bounceVy = -Math.Sqrt(2 * Gravity * Math.Max(30, height));

    private void UpdateBounce(double dt)
    {
        _bounceVy += Gravity * dt;
        _y += _bounceVy * dt;
        if (_y < _trampTop || _bounceVy < 0) return;

        // Каца на батута: още един скок или стига.
        _y = _trampTop;
        bool stay = _save.StayPut && _save.StayScene == Scenes.Trampoline.Id;
        if (_hunting)
        {
            if (_food.Count == 0)
            {
                EndBounce(Lines.Pick(Lines.AllFoodEaten, _save.OwnerName));
                return;
            }
            bool foodAbove = _food.Any(f => Math.Abs(f.Left + f.Width / 2 - _x) < 70);
            if (!foodAbove)
            {
                // Тук няма повече: слиза и отива под следващата храна.
                EndBounce(null);
                HuntFoodWithTrampoline();
                return;
            }
        }
        else if (!stay && _bounceUntil > 0 && Now > _bounceUntil)
        {
            EndBounce(Lines.Pick(Scenes.Trampoline.End, _save.OwnerName));
            return;
        }
        else if (!IsQuiet && _rng.Next(6) == 0)
            Say(Lines.Pick(Scenes.Trampoline.During, _save.OwnerName), 2);
        Launch(NextBounceHeight());
    }

    private void EndBounce(string? line)
    {
        _onTrampoline = false;
        _hunting = false;
        _trampWindow?.Hide();
        if (line != null) Say(line, 3);
        if (_state == BearState.Bounce)
        {
            _y = Ground(_trampTop + PixelSize * 6);
            SetIdle();
        }
    }
}
