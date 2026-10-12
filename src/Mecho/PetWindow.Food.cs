using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Mecho;

// Храната се появява из екрана като предмети, които Тут занася при мечока.
public sealed partial class PetWindow
{
    private const int MaxFoodOnScreen = 12;

    /// <summary>След толкова секунди неизядената храна изчезва от екрана.</summary>
    internal const double FoodLifetime = 30 * 60;

    private static readonly string[] FoodCleared =
    {
        "*прибира забравената храна* Ще изстине, ще я сготвим пак.", "*мете трохичките* Чисто!", "Храната стоя твърде дълго. Прибрах я.",
    };

    /// <summary>
    /// Мечокът е скрит (или пак показан): храната, уредът и подаръкът се
    /// скриват и показват с него.
    /// </summary>
    private void SyncCompanions()
    {
        bool show = IsVisible;
        foreach (var f in _food)
        {
            if (show && !f.IsVisible) f.Show();
            else if (!show && f.IsVisible) f.Hide();
        }
        _cook?.Window.SetAway(!show);
        if (_giftWindow != null)
        {
            if (show && !_giftWindow.IsVisible) _giftWindow.Show();
            else if (!show && _giftWindow.IsVisible) _giftWindow.Hide();
        }
    }
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
        if (Cooks)
        {
            Say("Чакай, още готвя!", 3);
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

        double fullness = Kitchen.FullnessOf(food, _save) / food.Count, energy = food.Energy / food.Count;
        NotifyCare();
        Persist();
        if (food.Tool != null)
        {
            // Уредът се готви сам до мечока; храната излита от него (виж PetWindow.Cooking.cs).
            StartCooking(food, fullness, energy, cost);
            return;
        }
        var from = new Point(_x, _bearRect.Top + _bearRect.Height * 0.3);
        for (int i = 0; i < food.Count; i++) SpawnFood(food, fullness, energy, from, fromCooker: false);
        FoodReady(food);
    }

    /// <summary>
    /// Едно парче храна изскача от точката и се разлита по екрана с инерция (без
    /// гравитация): плъзга се, отскача от ръбовете и спира на различни места.
    /// От уреда хвърчи нагоре като от тенджера.
    /// </summary>
    private void SpawnFood(Food food, double fullness, double energy, Point from, bool fromCooker)
    {
        // Парчетата са в няколко вида (food_popcorn_1, _2…), ако има такива рисунки.
        int kinds = 0;
        while (_lib.HasProp($"food_{food.Id}_{kinds + 1}")) kinds++;
        var prop = _lib.GetProp(kinds > 0 ? $"food_{food.Id}_{1 + _rng.Next(kinds)}" : "food_" + food.Id, 16, 16, 0);
        var w = new FoodWindow(this, food, fullness, energy, prop, PixelSize);
        w.Left = from.X - w.Width / 2;
        w.Top = from.Y - w.Height / 2;
        double angle = fromCooker
            ? -Math.PI / 2 + (_rng.NextDouble() - 0.5) * 2.2
            : _rng.NextDouble() * Math.PI * 2;
        double speed = 900 + _rng.NextDouble() * 900;
        w.VelocityX = Math.Cos(angle) * speed;
        w.VelocityY = Math.Sin(angle) * speed - 300; // повечето хвърчат нагоре
        w.Closed += (_, _) => _food.Remove(w);
        _food.Add(w);
        if (IsVisible) w.Show();
    }

    private double _foodReadyAt = -1000;

    /// <summary>Храната е навън: мечокът моли да му я донесат.</summary>
    private void FoodReady(Food food)
    {
        _foodReadyAt = Now;
        Say(food.Count > 1
            ? $"{food.Icon} {food.Name}! Опа, разхвърчаха се! Донеси ми ги всичките!"
            : $"{food.Icon} {food.Name}! Донеси ми го тук, моля!", 5);
        _nextFoodReminder = Now + 40;
        if (_state is BearState.Walk or BearState.Chase or BearState.Busy) SetIdle();
    }

    /// <summary>Купува рецепта: ястието се отключва завинаги.</summary>
    public void BuyRecipe(Food food)
    {
        if (Kitchen.HasRecipe(food, _save) || !Kitchen.HasTool(food, _save)) return;
        int price = Kitchen.RecipePrice(food);
        if (_save.Hazelnuts < price) return;
        _save.Hazelnuts -= price;
        _save.Owned.Add(Kitchen.RecipeId(food));
        Say($"Нова рецепта: {food.Name}! Сега мога да я готвя.", 4);
        if (CanAnimateFreely) Play("happy");
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
        // Диванът идва веднага, ако тъкмо чете на земята.
        if (upgrade.Id == Kitchen.Couch && _inScene && _scene == Scenes.Reading) LoadSceneArt(Scenes.Reading);
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
        EatFood(food);
    }

    /// <summary>Изяжда парче храна (дори да е сит). Връща false, ако спи.</summary>
    private bool EatFood(FoodWindow food)
    {
        // Първите 3 секунди храната още лети: не може да я изяде веднага.
        if (food.JustSpawned) return false;
        if (IsResting)
        {
            Say("Ззз… после…", 2);
            return false;
        }

        _save.Fullness = Math.Min(100, _save.Fullness + food.Fullness);
        _save.Energy = Kitchen.EnergyAfter(_save.Energy, food.Energy);
        var bite = food.Art;
        food.Close();
        _food.Remove(food);
        bool last = _food.Count == 0;
        if (last)
        {
            if (!ThankForMeal()) Say(Lines.Pick(Lines.AllFoodEaten, _save.OwnerName), 4);
        }
        else
        {
            var lines = food.Food.Lines;
            Say($"{lines[_rng.Next(lines.Length)]} Още {_food.Count}!", 3);
        }
        // Напитка: сяда и я пие бавно, с чашата в лапата.
        if (IsDrink(food.Food) && CanAnimateFreely && !InFocus)
        {
            StartDrinking(food.Food);
        }
        // Държи храната в лапите и яде бавно (няколко секунди), после танцува.
        else if (CanAnimateFreely)
        {
            _held.Source = bite;
            Play("eat", length: EatTime, after: last ? () => Play("dance", length: 3) : null);
        }
        NotifyCare();
        Persist();
        return true;
    }

    private const double EatTime = 3 * BiteTime;
    private readonly System.Windows.Controls.Image _held = new() { IsHitTestVisible = false, Visibility = Visibility.Collapsed };

    private const double BiteTime = 4.0 / 3;

    /// <summary>
    /// Храната в лапите, докато яде: от корема се вдига към устата (когато
    /// отваря уста) и пак слиза, докато дъвче; с всяка хапка е по-малка.
    /// </summary>
    private Rect HeldRect(Rect bear)
    {
        bool on = _anim == "eat" && _held.Source != null && _state == BearState.Busy;
        _held.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        if (!on) return Rect.Empty;
        System.Windows.Media.RenderOptions.SetBitmapScalingMode(_held, System.Windows.Media.BitmapScalingMode.NearestNeighbor);
        var img = (System.Windows.Media.Imaging.BitmapSource)_held.Source!;
        int bites = (int)(_animTime / BiteTime);
        double scale = Math.Max(0.3, 0.6 - 0.08 * bites);
        double w = img.PixelWidth * PixelSize * scale, h = img.PixelHeight * PixelSize * scale;
        _held.Width = w;
        _held.Height = h;
        double lift = 0.5 - 0.5 * Math.Cos(2 * Math.PI * _animTime / BiteTime);
        double y = 0.68 + (0.5 - 0.68) * lift;
        return new Rect(bear.Left + bear.Width / 2 - w / 2, bear.Top + bear.Height * y - h / 2, w, h);
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
        _nextFoodReminder = now + 45 + _rng.Next(30);
        var nearest = _food.OrderBy(f => Math.Abs(f.Left + f.Width / 2 - _x)).First();
        _facingLeft = nearest.Left + nearest.Width / 2 < _x;
        var line = Lines.WantFood[_rng.Next(Lines.WantFood.Length)];
        Say(string.Format(line, _save.OwnerName, _food.Count, nearest.Food.Icon), 4);
        Play(_rng.Next(2) == 0 ? "sad" : "idle", length: 2);
    }

    /// <summary>
    /// Храната е без гравитация: лети с инерция, забавя се (триене), отскача от
    /// ръбовете на екрана и спира. Хвърлена храна, която улучи мечока, той хваща;
    /// докато скача на батута, хваща всичко, през което мине.
    /// </summary>
    private void UpdateFood(double dt)
    {
        var bear = _bearRect;
        foreach (var f in _food.ToList())
        {
            if (f.IsDragging) continue;

            // Забравена храна (над 30 минути навън) се разчиства сама.
            if (f.Age > FoodLifetime)
            {
                f.Close();
                _food.Remove(f);
                if (_food.Count == 0 && IsVisible && !InFocus && !IsResting)
                    Announce(Lines.Pick(FoodCleared, _save.OwnerName), 4);
                continue;
            }

            if (_state == BearState.Bounce && bear.IntersectsWith(f.Bounds))
            {
                EatFood(f);
                continue;
            }

            double speed = Math.Abs(f.VelocityX) + Math.Abs(f.VelocityY);
            if (speed < 10)
            {
                f.VelocityX = f.VelocityY = 0;
                f.Thrown = false;
                continue;
            }

            var b = f.Bounds;
            var area = AreaAt(b.Left + b.Width / 2, b.Top + b.Height / 2);
            double x = b.Left + f.VelocityX * dt, y = b.Top + f.VelocityY * dt;
            if (x < area.Left) { x = area.Left; f.VelocityX = Math.Abs(f.VelocityX) * 0.6; }
            if (x > area.Right - b.Width) { x = area.Right - b.Width; f.VelocityX = -Math.Abs(f.VelocityX) * 0.6; }
            if (y < area.Top) { y = area.Top; f.VelocityY = Math.Abs(f.VelocityY) * 0.6; }
            if (y > area.Bottom - b.Height) { y = area.Bottom - b.Height; f.VelocityY = -Math.Abs(f.VelocityY) * 0.6; }
            double friction = Math.Exp(-2.6 * dt);
            f.VelocityX *= friction;
            f.VelocityY *= friction;
            f.Left = x;
            f.Top = y;

            if (f.Thrown && bear.IntersectsWith(f.Bounds))
            {
                Say(Lines.Pick(Lines.CaughtFood, _save.OwnerName), 2);
                EatFood(f);
            }
        }
    }
}
