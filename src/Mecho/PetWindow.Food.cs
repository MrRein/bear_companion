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
    /// Уредът и храната винаги са пред мечока: след като прозорецът на мечока
    /// излезе отгоре (цъкане, влачене, рисуване), те пак се слагат над него.
    /// </summary>
    private void RaiseCompanions()
    {
        if (_cook is { Window.IsVisible: true } c) NativeMethods.BringToTop(new System.Windows.Interop.WindowInteropHelper(c.Window).Handle);
        if (_heaterWindow is { IsVisible: true } h) NativeMethods.BringToTop(new System.Windows.Interop.WindowInteropHelper(h).Handle);
        if (_parcel is { IsVisible: true } pc) NativeMethods.BringToTop(new System.Windows.Interop.WindowInteropHelper(pc).Handle);
        if (_visitor is { IsVisible: true } v) NativeMethods.BringToTop(new System.Windows.Interop.WindowInteropHelper(v).Handle);
        // Храната над главата му остава под балончето.
        foreach (var f in _food)
            if (f.IsVisible && f.Top > _bearRect.Top) NativeMethods.BringToTop(new System.Windows.Interop.WindowInteropHelper(f).Handle);
    }

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
        _heaterWindow?.SetAway(!show);
        _visitor?.SetAway(!show);
        _parcel?.SetAway(!show);
        if (!show)
        {
            _bubbleWindow.Conceal();
            _butterflyWindow.Conceal();
        }
        if (_trampWindow != null)
        {
            if (!show && _trampWindow.IsVisible) _trampWindow.Hide();
            else if (show && _onTrampoline && !_trampWindow.IsVisible) _trampWindow.Show();
        }
        if (show) RaiseCompanions();
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
        if (!Kitchen.CanCook(food, _save) || BusyEating()) return;
        if (IsResting)
        {
            Say(R.Any("food.asleep"), 3);
            return;
        }
        if (Cooks || _pendingCook != null)
        {
            Say(R.Any("food.cooking"), 3);
            return;
        }
        if (_food.Count + food.Count > MaxFoodOnScreen)
        {
            Say(R.Any("food.other"), 4);
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
                Say(R.Any("food.coffeelimit"), 4);
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
            // Спира каквото прави, мръдва малко встрани и чак тогава слага уреда
            // (виж PetWindow.Cooking.cs).
            BeginCooking(food, fullness, energy, cost);
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
        NativeMethods.BringToTop(new System.Windows.Interop.WindowInteropHelper(w).Handle);
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

    private Food? _learnedWhileWorking;

    /// <summary>
    /// Намерена страница от готварската книга (тихо: само лентата в „Храна“
    /// расте). Ако довърши рецептата, мечокът казва коя е – но не по време на
    /// работа: тогава чака почивката. Връщa надпис за наградата (" +📖").
    /// </summary>
    internal string FindRecipePage()
    {
        if (Recipes.Next(_save) == null) return "";
        var learned = Recipes.AddPage(_save);
        if (learned != null)
        {
            if (InFocus) _learnedWhileWorking = learned;
            else AnnounceRecipe(learned);
        }
        NotifyCare();
        Persist();
        return " +📖";
    }

    private void AnnounceRecipe(Food food)
    {
        Say($"Научих нова рецепта… {food.Icon} {food.Name}! Виж в „Храна“.", 6);
        if (CanAnimateFreely) Play("happy");
    }

    /// <summary>Рецепта, научена по време на работа: казва я, щом работата свърши.</summary>
    private void TellLearnedRecipe()
    {
        if (_learnedWhileWorking == null || InFocus || IsAsleep) return;
        var food = _learnedWhileWorking;
        _learnedWhileWorking = null;
        AnnounceRecipe(food);
    }

    /// <summary>Дрехите, които идват с гардероба.</summary>
    internal static readonly string[] WardrobeBonus = { "tee_red", "jeans", "jeans_blue" };

    /// <summary>Купува уред или подобрение с лешници.</summary>
    public void Buy(Upgrade upgrade)
    {
        if (OwnsOrOrdered(upgrade.Id)) return;
        if (_save.Hazelnuts < upgrade.Price)
        {
            Say($"{upgrade.Icon} {upgrade.Name} струва {upgrade.Price} 🌰. Имаме {_save.Hazelnuts}. Още малко работа!", 4);
            return;
        }
        _save.Hazelnuts -= upgrade.Price;
        _save.Ordered.Add(upgrade.Id);
        GoalBought(upgrade.Id);
        Say(R.Pick("delivery.ordered", _save.OwnerName), 4);
        Order(upgrade);
        NotifyCare();
        Persist();
    }

    /// <summary>Поръчката идва с доставка; отключва се чак когато мечокът отвори кашона.</summary>
    private void Order(Upgrade upgrade) =>
        Deliver(upgrade.Icon, upgrade.Name, size: SizeOf(upgrade.Id), onOpen: () => Receive(upgrade));

    /// <summary>Нещо, купено, но още неотворено (вече не се предлага в магазина).</summary>
    private bool OwnsOrOrdered(string id) => Kitchen.Owns(_save, id) || _save.Ordered.Contains(id);

    /// <summary>При пускане: неотворените поръчки пак идват.</summary>
    private void ResumeOrders()
    {
        foreach (var id in _save.Ordered.ToList())
        {
            if (Kitchen.Find(id) is { } u && !Kitchen.Owns(_save, id)) Order(u);
            else _save.Ordered.Remove(id);
        }
    }

    /// <summary>Отвори кашона: нещото вече е негово (уредът – с рецептата си, гардеробът – с дрехи).</summary>
    private void Receive(Upgrade upgrade)
    {
        _save.Ordered.Remove(upgrade.Id);
        if (!_save.Owned.Contains(upgrade.Id)) _save.Owned.Add(upgrade.Id);
        LearnDefaultRecipe(upgrade.Id);
        // Гардеробът идва с подарък: червена тениска и два чифта дънки.
        if (upgrade.Id == Kitchen.Wardrobe)
            foreach (var id in WardrobeBonus)
                if (!_save.OwnedClothes.Contains(id)) _save.OwnedClothes.Add(id);
        NotifyCare();
        Persist();
        {
            Say(upgrade.Id == Kitchen.Wardrobe
                ? $"Ура! {upgrade.Icon} {upgrade.Name}! И вътре има червена тениска и дънки!"
                : $"Ура! {upgrade.Icon} {upgrade.Name}!", 5);
            // Диванът идва веднага, ако тъкмо чете на земята.
            if (upgrade.Id == Kitchen.Couch && _inScene && _scene == Scenes.Reading) LoadSceneArt(Scenes.Reading);
            // Ново нещо за почивка: веднага го пробва (ако не работите).
            var scene = Scenes.All.FirstOrDefault(f => f.Unlock == upgrade.Id);
            if (scene != null && !InFocus && !_save.StayPut && !IsAsleep && _state is not (BearState.Drag or BearState.Falling))
                StartScene(scene, 180 + _rng.Next(120));
            else if (CanAnimateFreely) Play("dance", length: 3);
        }
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
            Say(R.Any("food.asleep2"), 2);
            return false;
        }

        _save.Fullness = Math.Min(100, _save.Fullness + food.Fullness);
        // Не е спал добре: кафето му помага повече (до 70).
        bool coffee = food.Food.Energy >= 30;
        _save.Energy = Kitchen.EnergyAfter(_save.Energy, food.Energy, coffee && Today == DayKind.BadNight ? 70 : Kitchen.FoodEnergyCap);
        if (coffee && DateTime.Now.Hour >= 17) _save.LateCoffee = DateTime.Today.ToString("yyyy-MM-dd");
        MaybeFeast();
        var bite = food.Art;
        food.Close();
        _food.Remove(food);
        bool last = _food.Count == 0;
        // Казва нещо за точно това ядене („Мммм, кафе!“), и колко остават.
        var talk = FoodTalk.For(food.Food);
        string line = Lines.Pick(talk.Receive, _save.OwnerName);
        Say(last ? line : $"{line} Още {_food.Count}!", 3);
        // Напитка: сяда и я пие бавно, с чашата в лапата.
        if (IsDrink(food.Food) && CanAnimateFreely && !InFocus)
        {
            StartDrinking(food.Food, last);
        }
        // Държи храната в лапите и яде бавно, с паузи.
        else if (CanAnimateFreely)
        {
            StartEating(food.Food, bite, last);
        }
        else if (last) FinishedAll(food.Food);
        NotifyCare();
        Persist();
        return true;
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
        _nextFoodReminder = now + 90 + _rng.Next(60);
        var nearest = _food.OrderBy(f => Math.Abs(f.Left + f.Width / 2 - _x)).First();
        _facingLeft = nearest.Left + nearest.Width / 2 < _x;
        Say(Lines.Fill(Lines.PickAny(Lines.WantFood), _save.OwnerName, _food.Count, nearest.Food.Icon), 4);
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
            NativeMethods.MoveWindow(f, x, y);

            if (f.Thrown && bear.IntersectsWith(f.Bounds))
            {
                Say(Lines.Pick(Lines.CaughtFood, _save.OwnerName), 2);
                EatFood(f);
            }
        }
    }
}
