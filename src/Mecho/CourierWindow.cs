using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Mecho;

/// <summary>
/// Куриерчетата: малки жълти пиленца, които носят кашона над главите си
/// (колкото по-голям е кашонът, толкова повече са). Само рисунка, не се цъкат.
/// </summary>
public sealed class CourierWindow : Window
{
    private readonly Canvas _canvas = new();
    private readonly Image _box = new();
    private readonly List<Image> _couriers = new();
    private double _centerX, _bottom;

    public CourierWindow(int count)
    {
        Title = "Доставка";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        IsHitTestVisible = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        UseLayoutRounding = true;
        Content = _canvas;
        RenderOptions.SetBitmapScalingMode(_box, BitmapScalingMode.NearestNeighbor);
        _canvas.Children.Add(_box);
        for (int i = 0; i < count; i++)
        {
            var c = new Image();
            RenderOptions.SetBitmapScalingMode(c, BitmapScalingMode.NearestNeighbor);
            _couriers.Add(c);
            _canvas.Children.Add(c);
        }
        SourceInitialized += (_, _) => NativeMethods.MakeToolWindow(new WindowInteropHelper(this).Handle);
    }

    public double CenterX => _centerX;

    /// <summary>
    /// Нарежда: кашонът (или нищо, ако вече е оставен) над главите им; frame –
    /// крачка (две рисунки на човечетата, разминати, за да не вървят в крак);
    /// faceLeft – накъде вървят.
    /// </summary>
    public void Place(BitmapSource? box, BitmapSource walkA, BitmapSource walkB, double pixel, double centerX, double bottom, bool frame, bool faceLeft)
    {
        _centerX = centerX;
        _bottom = bottom;
        double cw = walkA.PixelWidth * pixel, ch = walkA.PixelHeight * pixel;
        double gap = 1 * pixel;
        double rowW = _couriers.Count * cw + (_couriers.Count - 1) * gap;
        double boxW = box == null ? 0 : box.PixelWidth * pixel, boxH = box == null ? 0 : box.PixelHeight * pixel;
        double w = Math.Max(rowW, boxW), h = ch + boxH - (box == null ? 0 : 3 * pixel);
        Width = w;
        Height = h;
        _canvas.Width = w;
        _canvas.Height = h;
        var flip = faceLeft ? Transform.Identity : new ScaleTransform(-1, 1, cw / 2, 0);
        for (int i = 0; i < _couriers.Count; i++)
        {
            var c = _couriers[i];
            c.Source = (i % 2 == 0) == frame ? walkA : walkB;
            c.Width = cw;
            c.Height = ch;
            c.RenderTransform = flip;
            Canvas.SetLeft(c, (w - rowW) / 2 + i * (cw + gap));
            Canvas.SetTop(c, h - ch);
        }
        _box.Visibility = box == null ? Visibility.Collapsed : Visibility.Visible;
        if (box != null)
        {
            _box.Source = box;
            _box.Width = boxW;
            _box.Height = boxH;
            Canvas.SetLeft(_box, (w - boxW) / 2);
            Canvas.SetTop(_box, 0);
        }
        Left = _centerX - w / 2;
        Top = _bottom - h;
        if (!IsVisible) Show();
    }
}
