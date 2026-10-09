using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace Mecho;

/// <summary>
/// Парче храна, което се появява из екрана. Хващаш го с мишката и го занасяш
/// при мечока. Ако го пуснеш другаде, пада на земята.
/// </summary>
public sealed class FoodWindow : Window
{
    private readonly PetWindow _pet;
    private bool _dragging;
    private Point _grab;

    public Food Food { get; }
    public double Fullness { get; }
    public double Energy { get; }
    public double VelocityY { get; set; }
    public double VelocityX { get; set; }

    /// <summary>Хвърлена е от Тут: ако улучи мечока в движение, той я хваща.</summary>
    public bool Thrown { get; set; }

    private readonly DateTime _spawned = DateTime.Now;

    /// <summary>Току-що разпръсната (под 3 секунди): мечокът още не може да я изяде.</summary>
    public bool JustSpawned => (DateTime.Now - _spawned).TotalSeconds < 3;

    private Point _lastScreen;
    private DateTime _lastMove;
    public bool IsDragging => _dragging;

    public FoodWindow(PetWindow pet, Food food, double fullness, double energy, Prop prop, double pixelSize)
    {
        _pet = pet;
        Food = food;
        Fullness = fullness;
        Energy = energy;
        Title = food.Name;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        UseLayoutRounding = true;
        WindowStartupLocation = WindowStartupLocation.Manual;

        var image = new Image
        {
            Source = prop.Image,
            Width = prop.Image.PixelWidth * pixelSize,
            Height = prop.Image.PixelHeight * pixelSize,
            Cursor = Cursors.Hand,
            ToolTip = $"{food.Icon} {food.Name}: занеси ги на мечока",
        };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
        Content = image;
        Width = image.Width;
        Height = image.Height;

        SourceInitialized += (_, _) => NativeMethods.MakeToolWindow(new WindowInteropHelper(this).Handle);

        image.MouseLeftButtonDown += (_, e) =>
        {
            _dragging = true;
            VelocityY = 0;
            VelocityX = 0;
            Thrown = false;
            _grab = e.GetPosition(this);
            _lastScreen = new Point(Left, Top);
            _lastMove = DateTime.Now;
            image.CaptureMouse();
            e.Handled = true;
        };
        image.MouseMove += (_, e) =>
        {
            if (!_dragging) return;
            var p = e.GetPosition(this);
            Left += p.X - _grab.X;
            Top += p.Y - _grab.Y;
            // Скорост на хвърляне: от последното движение.
            var now = DateTime.Now;
            double dt = Math.Max(0.008, (now - _lastMove).TotalSeconds);
            VelocityX = VelocityX * 0.5 + (Left - _lastScreen.X) / dt * 0.5;
            VelocityY = VelocityY * 0.5 + (Top - _lastScreen.Y) / dt * 0.5;
            _lastScreen = new Point(Left, Top);
            _lastMove = now;
        };
        image.MouseLeftButtonUp += (_, e) =>
        {
            if (!_dragging) return;
            _dragging = false;
            image.ReleaseMouseCapture();
            e.Handled = true;
            // Пусната без движение: спира. Хвърлена: лети и се плъзга.
            if ((DateTime.Now - _lastMove).TotalSeconds > 0.1) VelocityX = VelocityY = 0;
            Thrown = Math.Abs(VelocityX) + Math.Abs(VelocityY) > 150;
            _pet.FoodDropped(this);
        };
    }

    public Rect Bounds => new(Left, Top, ActualWidth > 0 ? ActualWidth : Width, ActualHeight > 0 ? ActualHeight : Height);
}
