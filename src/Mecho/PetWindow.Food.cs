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
            ? $"Разпилях {food.Name.ToLowerInvariant()} из екрана! Донеси ми ги всичките {food.Icon}"
            : $"{food.Icon} {food.Name}! Донеси ги тук, моля!", 5);
        _nextFoodReminder = Now + 15;
        if (_state is BearState.Walk or BearState.Chase or BearState.Busy) SetIdle();
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

        // Изяжда всичко, което му донесеш, дори да е сит.
        _save.Fullness = Math.Min(100, _save.Fullness + food.Fullness);
        food.Close();
        _food.Remove(food);
        if (_food.Count == 0)
        {
            Say(Lines.Pick(Lines.AllFoodEaten, _save.OwnerName), 4);
            if (CanAnimateFreely) Play("dance", length: 3);
        }
        else
        {
            var lines = food.Food.Lines;
            Say($"{lines[_rng.Next(lines.Length)]} Още {_food.Count}!", 3);
            if (CanAnimateFreely) Play("eat");
        }
        NotifyCare();
        Persist();
    }

    /// <summary>Има храна из екрана, която още не му е донесена.</summary>
    private bool WaitingForFood => _food.Count > 0;

    private double _nextFoodReminder;

    /// <summary>Стои, гледа храната и напомня да му я донесеш.</summary>
    private void WaitForFood(double now)
    {
        if (now < _nextFoodReminder)
        {
            if (_anim != "idle") SetIdle();
            return;
        }
        _nextFoodReminder = now + 12 + _rng.Next(10);
        var nearest = _food.OrderBy(f => Math.Abs(f.Left + f.Width / 2 - _x)).First();
        _facingLeft = nearest.Left + nearest.Width / 2 < _x;
        var line = Lines.WantFood[_rng.Next(Lines.WantFood.Length)];
        Say(string.Format(line, _save.OwnerName, _food.Count, nearest.Food.Icon), 4);
        Play(_rng.Next(2) == 0 ? "sad" : "idle", length: 2);
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
