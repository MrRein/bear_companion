using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static Mecho.Ui;

namespace Mecho;

/// <summary>
/// Уредът, в който се готви (тенджера, тиган, фурна…): отделен прозорец на
/// земята с таймер над него. Мечокът е свободен: може да работи, да се
/// разхожда и да се връща да бърка. Клик върху уреда пита дали да спре.
/// </summary>
public sealed class CookerWindow : Window
{
    private readonly Image _image = new() { Cursor = Cursors.Hand };
    private readonly TextBlock _time = new() { FontWeight = FontWeights.Bold };
    private readonly Border _tag;
    private double _centerX, _bottom, _shake;

    /// <summary>Тут цъкна уреда.</summary>
    public event Action? Clicked;

    public CookerWindow()
    {
        Title = "Готвене";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.Manual;
        FontFamily = MonoFont;
        FontSize = Body;
        Foreground = Ink;
        UseLayoutRounding = true;
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.Aliased);
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);

        _tag = Pill(_time);
        _tag.HorizontalAlignment = HorizontalAlignment.Center;
        _tag.Margin = new Thickness(0, 0, 0, 3);
        var stack = new StackPanel();
        stack.Children.Add(_tag);
        stack.Children.Add(_image);
        Content = stack;
        _image.ToolTip = Tip("Готви се. Цъкни, ако искаш да го спреш.");

        SourceInitialized += (_, _) => NativeMethods.MakeToolWindow(new WindowInteropHelper(this).Handle);
        _image.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            Clicked?.Invoke();
        };
    }

    /// <summary>Показва уреда: средата му е на centerX, дъното на bottom (DIP).</summary>
    public void Place(BitmapSource art, double pixelSize, double centerX, double bottom)
    {
        _centerX = centerX;
        _bottom = bottom;
        SetArt(art, pixelSize);
        if (!IsVisible) Show();
        Reposition();
    }

    public void SetArt(BitmapSource art, double pixelSize)
    {
        if (ReferenceEquals(_image.Source, art)) return;
        _image.Source = art;
        _image.Width = art.PixelWidth * pixelSize;
        _image.Height = art.PixelHeight * pixelSize;
    }

    /// <summary>Остава време (null = скрий табелката).</summary>
    public void SetTime(string? text)
    {
        _tag.Visibility = text == null ? Visibility.Hidden : Visibility.Visible;
        if (text != null && _time.Text != text) _time.Text = text;
    }

    /// <summary>Тресе се точно преди капакът да хвръкне.</summary>
    public void Shake(double dx)
    {
        if (Math.Abs(dx - _shake) < 0.01) return;
        _shake = dx;
        Reposition();
    }

    public double CenterX => _centerX;
    public double Bottom => _bottom;

    /// <summary>Горе на уреда (за откъдето излита храната).</summary>
    public Point Spout => new(_centerX, _bottom - _image.Height + _image.Height * 0.2);

    private void Reposition()
    {
        UpdateLayout();
        double w = ActualWidth > 0 ? ActualWidth : _image.Width;
        double h = ActualHeight > 0 ? ActualHeight : _image.Height;
        Left = _centerX - w / 2 + _shake;
        Top = _bottom - h;
    }
}
