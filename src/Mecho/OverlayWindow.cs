using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Mecho;

/// <summary>
/// Малък прозрачен прозорец „винаги отгоре“ за едно нещо, което се мести само
/// (балончето, пеперудата). Кликовете минават през него, не взима фокус и го
/// няма в Alt+Tab. Така прозорецът на мечока е малък: прозрачен прозорец се
/// прерисува целият при всяка промяна, а балончето и пеперудата искаха много място.
/// </summary>
public sealed class OverlayWindow : Window
{
    public OverlayWindow(UIElement content)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        Focusable = false;
        IsHitTestVisible = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.Aliased);
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = -10000;
        Top = -10000;
        Content = content;
        SourceInitialized += (_, _) => NativeMethods.MakeClickThrough(new WindowInteropHelper(this).Handle);
    }

    /// <summary>Колко е голямо съдържанието (след смяна – веднага, без да чака кадъра).</summary>
    public Size Measure()
    {
        var c = (UIElement)Content;
        c.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return c.DesiredSize;
    }

    /// <summary>Слага го (горният ляв ъгъл, в DIP) и го показва, ако е скрит.</summary>
    public void Place(double left, double top)
    {
        if (!IsVisible)
        {
            // Първо мястото и размерът, после показване: да не се мерне на старото място.
            Left = left;
            Top = top;
            Show();
            UpdateLayout();
        }
        NativeMethods.MoveWindow(this, left, top);
    }

    public void Conceal()
    {
        if (IsVisible) Hide();
    }
}
