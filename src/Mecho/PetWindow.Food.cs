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

    /// <summary>
    /// Мечокът сготвя ястие (ако има уреда и лешниците). То се появява из екрана
    /// и Тут трябва да го занесе при него.
    /// </summary>
    public void Cook(Food food)
    {
        if (!Kitchen.CanCook(food, _save)) return;
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
        if (food.Cost == 0 && Kitchen.BerriesIn(_save) > 0)
        {
            Say($"Храстът още не е дал нови боровинки. След {Kitchen.BerriesIn(_save)} мин.", 4);
            return;
        }
        if (food.Energy >= 30)
        {
            // Две кафета на половин час стигат.
            _save.CoffeeTimes.RemoveAll(t => (DateTime.Now - t).TotalMinutes > 30);
            if (_save.CoffeeTimes.Count >= 2)
            {
                Say("Сърцето ми прави туп-туп-туп! Стига кафе за сега.", 4);
                return;
            }
        }
        int cost = Kitchen.CostOf(food, _save);
        if (_save.Hazelnuts < cost)
        {
            Say($"Трябват ми {cost} 🌰, а имам {_save.Hazelnuts}. Да свършим някоя задача?", 4);
            return;
        }

        _save.Hazelnuts -= cost;
        if (food.Cost == 0) _save.BerriesReadyAt = DateTime.Now.AddMinutes(Kitchen.BerryMinutes);
        if (food.Energy >= 30) _save.CoffeeTimes.Add(DateTime.Now);

        var area = Area;
        var prop = _lib.GetProp("food_" + food.Id, 16, 16, 0);
        double fullness = Kitchen.FullnessOf(food, _save) / food.Count;
        for (int i = 0; i < food.Count; i++)
        {
            var w = new FoodWindow(this, food, fullness, food.Energy / food.Count, prop, PixelSize);
            // Появява се някъде по екрана (не върху мечока) и пада на земята.
            // Разпръсква се по целия екран (не върху мечока) и си виси там, докато не я събереш.
            double x, y;
            int tries = 0;
            do
            {
                x = area.Left + 30 + _rng.NextDouble() * Math.Max(0, area.Width - 60 - w.Width);
                y = area.Top + 30 + _rng.NextDouble() * Math.Max(0, area.Height - 60 - w.Height);
            }
            while (Math.Abs(x - _x) < 150 && Math.Abs(y - _y) < 200 && ++tries < 30);
            w.Left = x;
            w.Top = y;
            w.Closed += (_, _) => _food.Remove(w);
            _food.Add(w);
            w.Show();
        }
        Say(food.Count > 1
            ? $"{food.Icon} {food.Name}! Разпилях ги из екрана, донеси ми ги всичките!"
            : $"{food.Icon} {food.Name}! Донеси ми го тук, моля!", 5);
        _nextFoodReminder = Now + 15;
        if (_state is BearState.Walk or BearState.Chase or BearState.Busy) SetIdle();
        NotifyCare();
        Persist();
    }

    /// <summary>Купува уред или подобрение с лешници.</summary>
    public void Buy(Upgrade upgrade)
    {
        if (Kitchen.Owns(_save, upgrade.Id)) return;
        if (_save.Hazelnuts < upgrade.Price)
        {
            Say($"{upgrade.Icon} {upgrade.Name} струва {upgrade.Price} 🌰. Имаме {_save.Hazelnuts}. Още малко работа!", 4);
            return;
        }
        _save.Hazelnuts -= upgrade.Price;
        _save.Owned.Add(upgrade.Id);
        Say($"Ура! {upgrade.Icon} {upgrade.Name}! {upgrade.Description}", 6);
        // Ново нещо за почивка: веднага го пробва (ако не работите).
        var scene = Scenes.Fun.FirstOrDefault(f => f.Unlock == upgrade.Id);
        if (scene != null && !InFocus && !_save.StayPut && !IsAsleep && _state is not (BearState.Drag or BearState.Falling))
            StartScene(scene, 180 + _rng.Next(120));
        else if (CanAnimateFreely) Play("dance", length: 3);
        NotifyCare();
        Persist();
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
        _save.Energy = Math.Min(100, _save.Energy + food.Energy);
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

    /// <summary>Храната е без гравитация: стои там, където е, докато Тут не я занесе.</summary>
    private void UpdateFood(double dt)
    {
    }
}
