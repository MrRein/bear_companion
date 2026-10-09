using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Mecho;

// Храната се появява из екрана като предмети, които Тут занася при мечока.
public sealed partial class PetWindow
{
    private const int MaxFoodOnScreen = 12;
    private readonly List<FoodWindow> _food = new();

    /// <summary>Тут избира храна: тя се появява из екрана и трябва да се занесе на мечока.</summary>
    public void Feed(Food food)
    {
        if (!Kitchen.IsUnlocked(food, _save)) return;
        if (IsResting)
        {
            Say("Ззз… ще ям, като стана.", 3);
            return;
        }
        if (_save.Fullness >= 95)
        {
            Say(Lines.Pick(Lines.Full, _save.OwnerName), 3);
            return;
        }
        if (_food.Count + food.Count > MaxFoodOnScreen)
        {
            Say("Първо ми донеси другата храна!", 4);
            return;
        }

        var area = Area;
        var prop = _lib.GetProp("food_" + food.Id, 16, 16, 0);
        for (int i = 0; i < food.Count; i++)
        {
            var w = new FoodWindow(this, food, food.Fullness / food.Count, prop, PixelSize);
            // Появява се някъде по екрана (не върху мечока) и пада на земята.
            double x;
            do x = area.Left + 40 + _rng.NextDouble() * Math.Max(0, area.Width - 80 - w.Width);
            while (food.Count < 8 && Math.Abs(x - _x) < 150 && area.Width > 600);
            w.Left = x;
            w.Top = area.Top + area.Height * (0.1 + 0.5 * _rng.NextDouble());
            w.Closed += (_, _) => _food.Remove(w);
            _food.Add(w);
            w.Show();
        }
        Say(food.Count > 1
            ? $"Разпилях {food.Name.ToLowerInvariant()} из екрана! Донеси ми ги {food.Icon}"
            : $"{food.Icon} {food.Name}! Донеси ги тук, моля!", 5);
    }

    /// <summary>Тут пусна храна. Ако е върху мечока, той я изяжда; иначе пада на земята.</summary>
    internal void FoodDropped(FoodWindow food)
    {
        var near = _bearRect;
        near.Inflate(24, 24);
        if (!near.IntersectsWith(food.Bounds)) return;

        if (IsResting)
        {
            Say("Ззз… после…", 2);
            return;
        }
        if (_save.Fullness >= 98)
        {
            Say(Lines.Pick(Lines.Full, _save.OwnerName), 3);
            return;
        }

        _save.Fullness = Math.Min(100, _save.Fullness + food.Fullness);
        var lines = food.Food.Lines;
        Say(lines[_rng.Next(lines.Length)], 3);
        if (CanAnimateFreely) Play("eat");
        food.Close();
        NotifyCare();
        Persist();
    }

    /// <summary>Пуснатата храна пада до земята на своя екран.</summary>
    private void UpdateFood(double dt)
    {
        foreach (var f in _food.ToList())
        {
            if (f.IsDragging) continue;
            var b = f.Bounds;
            double floor = AreaAt(b.Left + b.Width / 2, b.Top + b.Height / 2).Bottom - b.Height;
            if (f.Top >= floor - 0.5)
            {
                f.VelocityY = 0;
                if (Math.Abs(f.Top - floor) > 0.5) f.Top = floor;
                continue;
            }
            f.VelocityY += Gravity * 0.6 * dt;
            f.Top = Math.Min(floor, f.Top + f.VelocityY * dt);
        }
    }
}
