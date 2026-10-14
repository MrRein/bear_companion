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
    private double _ground, _vy;
    private bool _falling;

    /// <summary>Тут цъкна уреда.</summary>
    public event Action? Clicked;

    /// <summary>Тут премести уреда (нова среда x и дъно y в DIP, преди да кацне на земята).</summary>
    public event Action<double, double>? Dropped;

    private bool _pressed, _dragging;
    private Point _pressScreen, _pressWindow;

    public bool IsDragging => _dragging;

    /// <summary>Пада към земята, след като Тут го пусне.</summary>
    public bool IsFalling => _falling;

    /// <summary>Подсказката при задържане на мишката (уред, печица…).</summary>
    public string Hint
    {
        set => _image.ToolTip = Tip(value);
    }

    public CookerWindow(string title = "Готвене")
    {
        Title = title;
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
        Hint = "Готви се.";

        SourceInitialized += (_, _) => NativeMethods.MakeToolWindow(new WindowInteropHelper(this).Handle);
        // Хващаш го и го местиш (таймерът е с него); кратко цъкване пита дали да спре.
        _image.MouseLeftButtonDown += (_, e) =>
        {
            _pressed = true;
            _dragging = _falling = false;
            _pressScreen = PointToScreen(e.GetPosition(this));
            _pressWindow = new Point(Left, Top);
            _image.CaptureMouse();
            e.Handled = true;
        };
        _image.MouseMove += (_, e) =>
        {
            if (!_pressed) return;
            var now = PointToScreen(e.GetPosition(this));
            var dpi = VisualTreeHelper.GetDpi(this);
            double dx = (now.X - _pressScreen.X) / dpi.DpiScaleX, dy = (now.Y - _pressScreen.Y) / dpi.DpiScaleY;
            if (!_dragging && Math.Abs(dx) + Math.Abs(dy) < 5) return;
            _dragging = true;
            Left = _pressWindow.X + dx;
            Top = _pressWindow.Y + dy;
            _centerX = Left + ActualWidth / 2 - _shake;
            _bottom = Top + ActualHeight;
        };
        _image.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            _image.ReleaseMouseCapture();
            bool dragged = _dragging;
            _pressed = _dragging = false;
            if (dragged) Dropped?.Invoke(Left + ActualWidth / 2 - _shake, Top + ActualHeight);
            else Clicked?.Invoke();
        };
    }

    /// <summary>Показва уреда: средата му е на centerX, дъното на bottom (DIP).</summary>
    public void Place(BitmapSource art, double pixelSize, double centerX, double bottom)
    {
        _centerX = centerX;
        _bottom = bottom;
        SetArt(art, pixelSize);
        if (!IsVisible && !_away)
        {
            Show();
            NativeMethods.BringToTop(new WindowInteropHelper(this).Handle);
        }
        Reposition();
    }

    private bool _away;

    /// <summary>Мечокът е скрит: уредът също (готвенето продължава).</summary>
    public void SetAway(bool away)
    {
        _away = away;
        if (away && IsVisible) Hide();
        else if (!away && !IsVisible && _image.Source != null)
        {
            Show();
            Reposition();
        }
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

    /// <summary>Пада оттам, където е, до земята (ground) със средата на centerX.</summary>
    public void FallTo(double centerX, double ground)
    {
        _centerX = centerX;
        _bottom = Math.Min(_bottom, ground);
        _ground = ground;
        _vy = 0;
        _falling = _bottom < ground - 0.5;
        if (!_falling) _bottom = ground;
        Reposition();
    }

    /// <summary>Всеки кадър: гравитация като при мечока. true = току-що кацна.</summary>
    public bool Step(double dt)
    {
        if (!_falling) return false;
        _vy += 2200 * dt;
        _bottom += _vy * dt;
        if (_bottom >= _ground)
        {
            _bottom = _ground;
            _falling = false;
        }
        Reposition();
        return !_falling;
    }

    public double CenterX => _centerX;

    /// <summary>Дъното на уреда на земята (ако пада – там, където ще кацне).</summary>
    public double Bottom => _falling ? _ground : _bottom;

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
