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
    public double VelocityY { get; set; }
    public bool IsDragging => _dragging;

    public FoodWindow(PetWindow pet, Food food, double fullness, Prop prop, double pixelSize)
    {
        _pet = pet;
        Food = food;
        Fullness = fullness;
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
            _grab = e.GetPosition(this);
            image.CaptureMouse();
            e.Handled = true;
        };
        image.MouseMove += (_, e) =>
        {
            if (!_dragging) return;
            var p = e.GetPosition(this);
            Left += p.X - _grab.X;
            Top += p.Y - _grab.Y;
        };
        image.MouseLeftButtonUp += (_, e) =>
        {
            if (!_dragging) return;
            _dragging = false;
            image.ReleaseMouseCapture();
            e.Handled = true;
            _pet.FoodDropped(this);
        };
    }

    public Rect Bounds => new(Left, Top, ActualWidth > 0 ? ActualWidth : Width, ActualHeight > 0 ? ActualHeight : Height);
}
