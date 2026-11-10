using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using static Mecho.Ui;

namespace Mecho;

/// <summary>
/// Обиколката „Как се живее с Мечо?“ на екрана: квадратче около нещото, за което се говори
/// (мечокът, лентите, таб…), и до него балонче с текста и само „Добре, стига“. Квадратчето не
/// пречи на цъкането (кликовете минават през него), а балончето не взима фокуса – менюто под
/// него не се затваря.
/// </summary>
public sealed class TutorialOverlay
{
    private readonly Window _frame;
    private readonly Window _panel;
    private readonly TextBlock _text;
    private readonly Button _stop;
    private readonly DispatcherTimer _timer;
    private Func<Rect?> _target = () => null;
    private double _phase;

    public event Action? Stopped;

    public TutorialOverlay()
    {
        // Квадратчето: черен контур и мед, пулсира леко.
        _frame = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            ShowActivated = false,
            ResizeMode = ResizeMode.NoResize,
            Content = new Border
            {
                BorderBrush = Ink,
                BorderThickness = Px(1),
                Child = new Border { BorderBrush = Honey, BorderThickness = Px(1), Child = new Border { BorderBrush = Ink, BorderThickness = new Thickness(Math.Max(1, P / 2)) } },
            },
            IsHitTestVisible = false,
        };
        _frame.SourceInitialized += (_, _) => NativeMethods.MakeClickThrough(new WindowInteropHelper(_frame).Handle);

        _text = Wrapped("", 300);
        _stop = Button("Добре, стига", () => Stop(), primary: false);
        _stop.HorizontalAlignment = HorizontalAlignment.Right;
        _stop.Margin = new Thickness(0, 8 * P / 4, 0, 0);
        var body = new StackPanel { MaxWidth = 320 };
        body.Children.Add(_text);
        body.Children.Add(_stop);
        _panel = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            ShowActivated = false,
            ResizeMode = ResizeMode.NoResize,
            SizeToContent = SizeToContent.WidthAndHeight,
            FontFamily = GameFont,
            FontSize = Body,
            Foreground = Ink,
            UseLayoutRounding = true,
            Content = Frame(body, new Thickness(10, 8, 10, 8)),
        };
        _panel.SourceInitialized += (_, _) => NativeMethods.MakeNoActivate(new WindowInteropHelper(_panel).Handle);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        _timer.Tick += (_, _) => Update();
    }

    public bool IsShown => _panel.IsVisible;

    /// <summary>Нова стъпка: текстът, кое да се огражда (или нищо) и надписът на бутона.</summary>
    public void Show(string text, Func<Rect?> target, string button = "Добре, стига")
    {
        SetRichText(_text, text);
        SetButton(_stop, button);
        _target = target;
        if (!_panel.IsVisible) _panel.Show();
        _timer.Start();
        Update();
    }

    public void Close()
    {
        _timer.Stop();
        _frame.Hide();
        _panel.Hide();
    }

    private void Stop()
    {
        Close();
        Stopped?.Invoke();
    }

    private void Update()
    {
        var target = _target();
        _phase += 0.12;
        var screen = SystemParameters.WorkArea;
        double pad = 2 * P;
        if (target is Rect t && !t.IsEmpty)
        {
            var r = new Rect(t.X - pad, t.Y - pad, t.Width + 2 * pad, t.Height + 2 * pad);
            _frame.Left = r.X;
            _frame.Top = r.Y;
            _frame.Width = r.Width;
            _frame.Height = r.Height;
            _frame.Opacity = 0.75 + 0.25 * Math.Sin(_phase);
            if (!_frame.IsVisible) _frame.Show();
            screen = AreaOf(t);
            PlacePanel(r, screen);
        }
        else
        {
            if (_frame.IsVisible) _frame.Hide();
            _panel.UpdateLayout();
            _panel.Left = screen.Left + (screen.Width - _panel.ActualWidth) / 2;
            _panel.Top = screen.Top + (screen.Height - _panel.ActualHeight) / 2;
        }
        // Над всичко (и над менюто, което е „винаги отгоре“).
        NativeMethods.BringToTop(new WindowInteropHelper(_frame).Handle);
        NativeMethods.BringToTop(new WindowInteropHelper(_panel).Handle);
    }

    /// <summary>Балончето – под квадратчето, ако има място; иначе над него; иначе встрани.</summary>
    private void PlacePanel(Rect r, Rect screen)
    {
        _panel.UpdateLayout();
        double w = _panel.ActualWidth, h = _panel.ActualHeight, gap = 3 * P;
        double x = Math.Clamp(r.X + r.Width / 2 - w / 2, screen.Left, Math.Max(screen.Left, screen.Right - w));
        double y = r.Bottom + gap;
        if (y + h > screen.Bottom) y = r.Top - gap - h;
        if (y < screen.Top)
        {
            y = Math.Clamp(r.Y, screen.Top, Math.Max(screen.Top, screen.Bottom - h));
            x = r.Right + gap + w <= screen.Right ? r.Right + gap : Math.Max(screen.Left, r.Left - gap - w);
        }
        _panel.Left = x;
        _panel.Top = y;
    }

    /// <summary>Работната площ на екрана, на който е квадратчето.</summary>
    private static Rect AreaOf(Rect r)
    {
        var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)(r.X + r.Width / 2), (int)(r.Y + r.Height / 2)));
        var wa = screen.WorkingArea;
        // Екраните са в пиксели; при мащаб различен от 100% – грубо в DIP по основния мащаб.
        double k = Ui.Dpi;
        return new Rect(wa.Left / k, wa.Top / k, wa.Width / k, wa.Height / k);
    }

    /// <summary>Мястото на елемент от менюто на екрана (в DIP), или null, ако не се вижда.</summary>
    public static Rect? ScreenRect(FrameworkElement? e)
    {
        if (e == null || !e.IsVisible || e.ActualWidth <= 0) return null;
        var source = PresentationSource.FromVisual(e);
        if (source?.CompositionTarget == null) return null;
        var from = source.CompositionTarget.TransformFromDevice;
        var a = from.Transform(e.PointToScreen(new Point(0, 0)));
        var b = from.Transform(e.PointToScreen(new Point(e.ActualWidth, e.ActualHeight)));
        return new Rect(a, b);
    }
}
