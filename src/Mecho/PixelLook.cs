using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Mecho;

/// <summary>
/// Пикселният вид на менюто и прозорчетата: всяка рамка с контура на менюто (Ui.Ink) е без
/// гладко заоблени ъгли – вместо тях ъгълът е стъпало от цели пиксели (заобленото с радиус 6+ –
/// две стъпала, по-малкото – едно). Прави се само веднъж, щом рамката се появи, за всички
/// прозорци наведнъж (бутоните, копчетата, табовете, лентите…).
/// </summary>
public static class PixelLook
{
    private static readonly ConditionalWeakTable<Border, StrongBox<CornerRadius>> Corners = new();
    private static bool _on;

    public static void Register()
    {
        if (_on) return;
        _on = true;
        EventManager.RegisterClassHandler(typeof(Border), FrameworkElement.LoadedEvent, new RoutedEventHandler((s, _) =>
        {
            if (s is Border b) Apply(b);
        }));
    }

    private static void Apply(Border b)
    {
        if (!ReferenceEquals(b.BorderBrush, Ui.Ink)) return;
        if (Corners.TryGetValue(b, out _))
        {
            Clip(b);
            return;
        }
        var r = b.CornerRadius;
        Corners.Add(b, new StrongBox<CornerRadius>(r));
        // Малка заобленост отвътре: така контурът минава и през вътрешния ъгъл на стъпалото
        // (иначе в ъгъла остава дупка по диагонал); външната част отрязва Clip.
        double u = Math.Clamp(b.BorderThickness.Left, 1.5, 3);
        double Inner(double radius) => Steps(radius) switch { 2 => u * 2.5, 1 => u * 1.5, _ => 0 };
        b.CornerRadius = new CornerRadius(Inner(r.TopLeft), Inner(r.TopRight), Inner(r.BottomRight), Inner(r.BottomLeft));
        b.SizeChanged += (_, _) => Clip(b);
        Clip(b);
    }

    private static int Steps(double radius) => radius >= 6 ? 2 : radius > 0 ? 1 : 0;

    /// <summary>Изрязва ъглите на стъпала (според заоблеността, която рамката е имала).</summary>
    private static void Clip(Border b)
    {
        if (!Corners.TryGetValue(b, out var box)) return;
        var r = box.Value;
        double w = b.ActualWidth, h = b.ActualHeight;
        // Един „пиксел“ – колкото контура (поне 1,5, най-много 3).
        double u = Math.Clamp(b.BorderThickness.Left, 1.5, 3);
        int tl = Steps(r.TopLeft), tr = Steps(r.TopRight), br = Steps(r.BottomRight), bl = Steps(r.BottomLeft);
        if (tl + tr + br + bl == 0 || w < 6 * u || h < 6 * u)
        {
            b.Clip = null;
            return;
        }
        var pts = new List<Point>();
        // Горе вляво: от левия ръб към горния.
        foreach (var p in Corner(tl, u)) pts.Add(p);
        // Горе вдясно: огледално, от горния ръб към десния.
        var c = Corner(tr, u);
        for (int i = c.Count - 1; i >= 0; i--) pts.Add(new Point(w - c[i].X, c[i].Y));
        // Долу вдясно: от десния ръб към долния.
        foreach (var p in Corner(br, u)) pts.Add(new Point(w - p.X, h - p.Y));
        // Долу вляво: от долния ръб към левия.
        c = Corner(bl, u);
        for (int i = c.Count - 1; i >= 0; i--) pts.Add(new Point(c[i].X, h - c[i].Y));

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(pts[0], true, true);
            ctx.PolyLineTo(pts.GetRange(1, pts.Count - 1), false, false);
        }
        geo.Freeze();
        b.Clip = geo;
    }

    /// <summary>Стъпалото на горния ляв ъгъл: от (0, n·u) до (n·u, 0).</summary>
    private static List<Point> Corner(int n, double u)
    {
        var pts = new List<Point> { new(0, n * u) };
        for (int i = 1; i <= n; i++)
        {
            pts.Add(new Point(i * u, (n - i + 1) * u));
            pts.Add(new Point(i * u, (n - i) * u));
        }
        return pts;
    }
}
