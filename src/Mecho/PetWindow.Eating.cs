using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Mecho;

// Ядене: държи парчето в лапите и яде бавно, с паузи, както пие напитките.
// Хапва (eat се върти няколко пъти), после само държи храната (eat_hold),
// после пак хапва… С всяка хапка парчето е по-малко. Храната е в лапите:
// точката е в eat_cup.png / eat_hold_cup.png и се мести с лапите кадър по кадър.
public sealed partial class PetWindow
{
    private Food? _eating;
    private bool _eatingLast;
    private int _bitesLeft, _bitesTotal;
    private readonly System.Windows.Controls.Image _held = new() { IsHitTestVisible = false, Visibility = Visibility.Collapsed };
    private readonly Dictionary<BitmapSource, (int X, int Y)?> _heldPoints = new();

    /// <summary>
    /// Яде или пие: не се прекъсва с други неща („Момент, сега ям.“). say=false –
    /// без приказка (когато мечокът сам щеше да почне нещо друго).
    /// </summary>
    internal bool BusyEating(bool say = true)
    {
        bool eat = _eating != null, drink = _inScene && _scene == Scenes.Drink;
        if (!eat && !drink) return false;
        if (say) Say(eat ? R.Any("busy.eat") : R.Any("busy.drink"), 2);
        return true;
    }

    private void StartEating(Food food, BitmapSource bite, bool last)
    {
        _held.Source = bite;
        _eating = food;
        _eatingLast = last;
        _bitesTotal = _bitesLeft = 4 + _rng.Next(3);
        EatHold(1.5 + _rng.NextDouble());
    }

    /// <summary>Пауза: само държи храната (2–6 секунди), после хапва.</summary>
    private void EatHold(double seconds) => Play("eat_hold", length: seconds, after: EatBite);

    /// <summary>Една хапка: дъвче (анимацията се върти 3–6 пъти).</summary>
    private void EatBite()
    {
        if (_eating is not { } food) return;
        if (_rng.Next(3) == 0) Say(Lines.Pick(FoodTalk.For(food).Chew, _save.OwnerName), 2);
        Play("eat", length: _lib.Get("eat").Duration * (3 + _rng.Next(4)), after: () =>
        {
            if (--_bitesLeft > 0) EatHold(2.5 + _rng.NextDouble() * 3.5);
            else EatDone();
        });
    }

    private void EatDone()
    {
        var food = _eating;
        _eating = null;
        _held.Source = null;
        if (food == null) return;
        if (_eatingLast && _food.Count == 0)
        {
            FinishedAll(food);
            Play("dance", length: 3);
        }
        // Изяде едно парче, а има още: понякога казва нещо кратко.
        else if (_rng.Next(10) < 3) Say(R.Pick("ate.piece", _save.OwnerName), 3);
    }

    /// <summary>Изяде (или изпи) всичко: благодари за обяда/вечерята или казва нещо за яденето.</summary>
    private void FinishedAll(Food food)
    {
        Joy(JoyFed);
        CoreEngine.Bus.Publish(new Mecho.Core.FoodEaten(food.Id, Whole: true));
        TookMedicine(food);
        // Странното желание: доволен е, че Тут го е разбрала (и то вече не се иска).
        if (OddCravingNow?.Id == food.Id)
        {
            _save.OddCraving = "";
            Emote("heart", 4);
            Say(R.Pick("craving.odd.done", _save.OwnerName).Replace("{ястие}", food.Name.ToLowerInvariant()), 5);
            FulfillWish(food);
            return;
        }
        bool thanked = WishThanks(food);
        if (!thanked && !ThankForMeal()) Say(DoneLine(food), 4);
        FulfillWish(food);
    }

    /// <summary>
    /// Каквото му се иска сега (сърчицата в „Храна“ и разделът „Желание“). Всяка сутрин –
    /// нов списък: любимото за деня и в някои дни още едно-две. Изпълненото се маха;
    /// понякога след ядене се появява ново (за следващото хранене).
    /// </summary>
    public Food[] Wishes()
    {
        EnsureWishes();
        SpawnDueWishes();
        // Само докато трае (до края на храненето, за което се е появило) и ако може да се сготви сега.
        var now = DateTime.Now;
        return _save.WishList.Select(id => Kitchen.Foods.FirstOrDefault(f => f.Id == id))
            .Where(f => f != null && Kitchen.CanCook(f, _save)
                && _save.WishUntil.TryGetValue(f.Id, out var until) && until > now).ToArray()!;
    }

    /// <summary>
    /// Нов ден: желанията не са гарантирани – като събитие. Колко ще има днес: 0 (54%),
    /// 1 (36%), 2 (8%), 3 (2%), всяко в случаен час (7:30–20:30, поне час и половина
    /// едно от друго). Какво му се иска се решава чак тогава (SpawnDueWishes) – по храненето
    /// в този час и по това колко е гладен. Понякога – и тайно „странно“ желание.
    /// </summary>
    private void EnsureWishes()
    {
        string today = DayKey(DateTime.Today) + "|events";
        if (_save.WishDay == today) return;
        double roll = _rng.NextDouble();
        int count = roll < 0.54 ? 0 : roll < 0.90 ? 1 : roll < 0.98 ? 2 : 3;
        var times = new List<int>();   // минути от полунощ
        for (int tries = 0; times.Count < count && tries < 200; tries++)
        {
            int m = 7 * 60 + 30 + _rng.Next(13 * 60);
            if (times.All(t => Math.Abs(t - m) >= 90)) times.Add(m);
        }
        times.Sort();
        _save.WishTimes = string.Join(",", times);
        _save.WishUntil.Clear();
        CoreEngine.Bus.Send(new Mecho.Core.SetWishes(today, Array.Empty<string>()));
        var can = Kitchen.Foods.Where(f => f.Cost > 0 && f.Fullness >= 5 && Kitchen.Regular(f) && Kitchen.CanCook(f, _save)).ToList();
        PickOddCraving(today, can);
    }

    /// <summary>Дошъл ли е часът на някое от днешните желания: появява се (и го казва).</summary>
    private void SpawnDueWishes()
    {
        if (_save.WishTimes.Length == 0) return;
        var now = DateTime.Now;
        int minute = now.Hour * 60 + now.Minute;
        var times = _save.WishTimes.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
        var due = times.Where(t => t <= minute).ToList();
        if (due.Count == 0) return;
        _save.WishTimes = string.Join(",", times.Except(due));
        // Програмата е била спряна дълго: изпуснатото желание просто не се е случило.
        if (minute - due.Max() > 120) return;
        SpawnWish(now, announce: true);
    }

    /// <summary>
    /// Ново желание сега: логично за това хранене (закуска/обяд/вечеря/между храненията) и
    /// според глада – много гладен иска нещо солидно, леко гладен – нещо малко.
    /// </summary>
    internal Food? SpawnWish(DateTime now, bool announce)
    {
        EnsureWishes();   // първо днешният ден (иначе подреждането му би изтрило това желание)
        var meal = Kitchen.MealAt(now);
        var can = Kitchen.Foods.Where(f => f.Cost > 0 && f.Fullness >= 2 && Kitchen.Regular(f) && Kitchen.CanCook(f, _save)
            && (Kitchen.MealsOf(f) & meal) != 0 && !_save.WishList.Contains(f.Id)).ToList();
        if (can.Count == 0) return null;
        double full = _save.Fullness;
        var fits = can.Where(f => full < 35 ? f.Fullness >= 30 : full < 65 ? f.Fullness is >= 12 and <= 35 : f.Fullness <= 15).ToList();
        if (fits.Count == 0) fits = can;
        var food = fits[_rng.Next(fits.Count)];
        if (!CoreEngine.Bus.Send(new Mecho.Core.AddWish(food.Id)).Ok) return null;
        // Трае до края на храненето (между храненията – два часа).
        var day = now.Date;
        _save.WishUntil[food.Id] = meal switch
        {
            Kitchen.Meal.Breakfast => day.AddHours(10.5),
            Kitchen.Meal.Lunch => day.AddHours(14.5),
            Kitchen.Meal.Dinner => day.AddHours(21),
            _ => now.AddHours(2),
        };
        Persist();
        NotifyCare();
        if (announce && !IsQuiet && !IsAsleep && !InFocus)
            Later(2, () =>
            {
                Mutter(R.Pick("wish.appear", _save.OwnerName).Replace("{ястие}", food.Name.ToLowerInvariant()), 5);
                Emote("hungry", 3);
            });
        return food;
    }

    // ── Странното желание ──

    /// <summary>
    /// Понякога (1 от 4 дни) за едно хранене му се яде нещо съвсем не на място (сладолед
    /// за закуска, пица следобед…). Не пише никъде – само го казва, като го цъкнеш или сам.
    /// </summary>
    private void PickOddCraving(string day, List<Food> can)
    {
        _save.OddCravingDay = day;
        _save.OddCraving = "";
        if (_rng.Next(4) != 0) return;
        var meal = new[] { Kitchen.Meal.Breakfast, Kitchen.Meal.Lunch, Kitchen.Meal.Dinner, Kitchen.Meal.Snack }[_rng.Next(4)];
        var odd = can.Where(f => (Kitchen.MealsOf(f) & meal) == 0 && !_save.WishList.Contains(f.Id)).ToList();
        if (odd.Count == 0) return;
        _save.OddCraving = odd[_rng.Next(odd.Count)].Id;
        _save.OddCravingMeal = (int)meal;
    }

    /// <summary>Странното желание – само в своето хранене, ако още не го е изял и може да се сготви.</summary>
    internal Food? OddCravingNow
    {
        get
        {
            EnsureWishes();
            if (_save.OddCraving.Length == 0 || _save.OddCravingDay != _save.WishDay) return null;
            if ((int)Kitchen.MealAt(DateTime.Now) != _save.OddCravingMeal) return null;
            var f = Kitchen.Foods.FirstOrDefault(x => x.Id == _save.OddCraving);
            return f != null && Kitchen.CanCook(f, _save) ? f : null;
        }
    }

    /// <summary>„Малко странно, но много ми се яде …“</summary>
    internal string OddCravingLine(Food f) => R.Pick("craving.odd", _save.OwnerName).Replace("{ястие}", f.Name.ToLowerInvariant());

    /// <summary>Получи желаното: маха го от списъка; понякога (рядко) му се дояжда нещо друго за следващия път.</summary>
    private void FulfillWish(Food food)
    {
        EnsureWishes();
        if (!CoreEngine.Bus.Send(new Mecho.Core.FulfillWish(food.Id)).Ok) return;
        Persist();
        NotifyCare();
    }

    /// <summary>
    /// Даде му каквото искаше: понякога (до 2 пъти на ден) ти връща услугата – лешник
    /// или два, а много рядко страница от готварската книга, която пазеше.
    /// </summary>
    private bool WishThanks(Food food)
    {
        if (!Wishes().Contains(food) || TodayRec.WishGifts >= 2) return false;
        double roll = _rng.NextDouble();
        if (roll < 0.05 && Recipes.Next(_save) != null)
        {
            TodayRec.WishGifts++;
            string page = FindRecipePage();
            Say(Fill("wish.page", ("ястие", food.Name.ToLowerInvariant())) + page, 7);
            Emote("heart", 4);
            return true;
        }
        if (roll < 0.4)
        {
            TodayRec.WishGifts++;
            int nuts = 1 + _rng.Next(3);
            _save.Hazelnuts += nuts;
            Persist();
            NotifyCare();
            Say(Fill("wish.nuts", ("ястие", food.Name.ToLowerInvariant())) + $" +{nuts} 🌰", 6);
            Emote("happy", 3);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Изяде/изпи: понякога за точно това ядене, понякога общо („Беше вкусно!“).
    /// Ако и след това е под половината сит (и няма още храна на екрана) – казва, че
    /// още е гладен: след напитка „Сега бих хапнал нещо“, след ядене „Мм… още едно?“.
    /// </summary>
    private string DoneLine(Food food) =>
        _save.Fullness < StillHungryBelow && _food.Count == 0 && _cook == null && _pendingCook == null
            ? R.Pick(IsDrink(food) ? "drank.hungry" : "ate.stillhungry", _save.OwnerName)
            : DoneLineAny(food);

    /// <summary>Под толкова ситост след яденето още му се яде.</summary>
    internal const double StillHungryBelow = 50;

    private string DoneLineAny(Food food) => Wishes().Contains(food)
        ? R.Pick("craving.done", _save.OwnerName).Replace("{ястие}", food.Name.ToLowerInvariant())
        : _rng.Next(5) > 0 && FoodTalk.For(food).Done.Length > 0
        ? Lines.Pick(FoodTalk.For(food).Done, _save.OwnerName)
        : R.Pick(IsDrink(food) ? "drank.done" : "ate.done", _save.OwnerName);

    /// <summary>
    /// Храната в лапите: средата ѝ е на точката за този кадър; с всяка хапка е
    /// по-малка.
    /// </summary>
    private Rect HeldRect(Rect bear)
    {
        bool on = _anim is "eat" or "eat_hold" && _held.Source != null && _state is BearState.Busy or BearState.Drag or BearState.Falling;
        _held.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        if (!on) return Rect.Empty;
        System.Windows.Media.RenderOptions.SetBitmapScalingMode(_held, System.Windows.Media.BitmapScalingMode.NearestNeighbor);
        var img = (BitmapSource)_held.Source!;
        double scale = 0.6 * (0.45 + 0.55 * Math.Max(1, _bitesLeft) / Math.Max(1, _bitesTotal));
        double w = img.PixelWidth * PixelSize * scale, h = img.PixelHeight * PixelSize * scale;
        _held.Width = w;
        _held.Height = h;

        var (px, py) = HeldPoint() ?? (16, 21);
        if (_facingLeft) px = _lib.FrameWidth - 1 - px;
        double cx = bear.Left + (px + 0.5) * PixelSize, cy = bear.Top + (py + 0.5) * PixelSize;
        return new Rect(cx - w / 2, cy - h / 2, w, h);
    }

    /// <summary>Точката за храната в текущия кадър (от eat_cup.png).</summary>
    private (int X, int Y)? HeldPoint()
    {
        var strip = _lib.Layer(_anim, "cup");
        if (strip == null || strip.Length == 0) return null;
        var frame = strip[_lib.Get(_anim).IndexAt(_animTime) % strip.Length];
        if (_heldPoints.TryGetValue(frame, out var point)) return point;
        point = Dresser.FirstPoint(frame);
        _heldPoints[frame] = point;
        return point;
    }
}
