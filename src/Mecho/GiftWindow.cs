using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using static Mecho.Ui;

namespace Mecho;

/// <summary>
/// Подаръкът в средата на екрана: долита от лапите на мечока и или се отваря
/// сам (той го подаде), или леко подскача и чака клик. Като се отвори, капакът
/// хвръква и се вижда какво има вътре.
/// </summary>
public sealed class GiftWindow : Window
{
    private readonly PetWindow _pet;
    private readonly Image _box;
    private readonly StackPanel _panel = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly BitmapSource _open;
    private Point _from, _to;
    private double _t;
    private bool _opened, _openWhenLanded;

    /// <summary>Отваря се сам, щом кацне в средата (мечокът го подаде).</summary>
    public void OpenWhenLanded() => _openWhenLanded = true;
    private DateTime _openedAt;

    public GiftWindow(PetWindow pet, BitmapSource closed, BitmapSource open, double scale)
    {
        _pet = pet;
        _open = open;
        Title = "Подарък";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.Manual;
        FontFamily = GameFont;
        FontSize = Body;
        Foreground = Ink;
        UseLayoutRounding = true;
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.Aliased);

        _box = new Image
        {
            Source = closed,
            Width = closed.PixelWidth * scale,
            Height = closed.PixelHeight * scale,
            Cursor = Cursors.Hand,
            ToolTip = Tip("Подарък от Мечо! Цъкни, за да го отвориш."),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 6, 0, 0),
        };
        RenderOptions.SetBitmapScalingMode(_box, BitmapScalingMode.NearestNeighbor);
        _panel.Children.Add(_box);
        Content = _panel;

        SourceInitialized += (_, _) => NativeMethods.MakeToolWindow(new WindowInteropHelper(this).Handle);
        _box.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            if (!_opened) _pet.OpenGift(this);
            else Close();
        };
        _timer.Tick += (_, _) => Tick();
        Closed += (_, _) => _timer.Stop();
    }

    /// <summary>Долита от точката from до средата на екрана (в DIP).</summary>
    public void FlyIn(Point from, Point center)
    {
        Show();
        UpdateLayout();
        _from = new Point(from.X - ActualWidth / 2, from.Y - ActualHeight / 2);
        _to = new Point(center.X - ActualWidth / 2, center.Y - ActualHeight / 2);
        _t = 0;
        Left = _from.X;
        Top = _from.Y;
        _timer.Start();
    }

    private void Tick()
    {
        _t += 0.033;
        if (_opened)
        {
            if ((DateTime.Now - _openedAt).TotalSeconds > 8) Close();
            return;
        }
        if (_t < 0.8)
        {
            // Дъга нагоре към средата.
            double k = _t / 0.8, ease = 1 - (1 - k) * (1 - k);
            NativeMethods.MoveWindow(this, _from.X + (_to.X - _from.X) * ease, _from.Y + (_to.Y - _from.Y) * ease - Math.Sin(Math.PI * k) * 80);
            return;
        }
        // Подаден от мечока: каца и след миг се отваря сам.
        if (_openWhenLanded && _t > 1.1)
        {
            NativeMethods.MoveWindow(this, _to.X, _to.Y);
            _pet.OpenGift(this);
            return;
        }
        // Чака: подскача леко.
        NativeMethods.MoveWindow(this, _to.X, _to.Y - Math.Abs(Math.Sin(_t * 3)) * 10);
    }

    /// <summary>Отворен: капакът хвръква, отдолу излиза какво има вътре.</summary>
    public void Reveal(BitmapSource art, int artScale, string name, string note)
    {
        _opened = true;
        _openedAt = DateTime.Now;
        _box.Source = _open;
        _box.ToolTip = null;
        Top = _to.Y;

        var prize = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        var image = Pixel(art, ArtScale(artScale));
        image.HorizontalAlignment = HorizontalAlignment.Center;
        image.Margin = new Thickness(0, 0, 0, 6);
        prize.Children.Add(image);
        prize.Children.Add(new TextBlock { Text = name, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center });
        prize.Children.Add(new TextBlock { Text = note, Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0) });
        var card = Frame(prize, new Thickness(12, 10, 12, 10));
        card.Margin = new Thickness(0, 0, 0, 4);
        card.Cursor = Cursors.Hand;
        card.MouseLeftButtonUp += (_, _) => Close();
        _panel.Children.Insert(0, card);
        UpdateLayout();
        // Остава центриран, докато расте нагоре.
        Left = _to.X + _box.Width / 2 - ActualWidth / 2;
        Top = _to.Y + _box.Height - ActualHeight;
    }
}
