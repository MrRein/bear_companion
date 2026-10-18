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
        // Не му се иска нещо, което сега не може да се сготви (заключено).
        return _save.WishList.Select(id => Kitchen.Foods.FirstOrDefault(f => f.Id == id))
            .Where(f => f != null && Kitchen.CanCook(f, _save)).ToArray()!;
    }

    /// <summary>Нов ден: нов списък с желания.</summary>
    private void EnsureWishes()
    {
        string today = DayKey(DateTime.Today);
        if (_save.WishDay == today) return;
        _save.WishDay = today;
        _save.WishList.Clear();
        var cravings = Kitchen.Cravings(_save);
        if (FavoriteFood is { } fav) _save.WishList.Add(fav.Id);
        foreach (var f in cravings)
            if (_save.WishList.Count < Math.Max(1, cravings.Length) && !_save.WishList.Contains(f.Id)) _save.WishList.Add(f.Id);
    }

    /// <summary>Получи желаното: маха го от списъка; понякога (рядко) му се дояжда нещо друго за следващия път.</summary>
    private void FulfillWish(Food food)
    {
        EnsureWishes();
        if (!_save.WishList.Remove(food.Id)) return;
        Persist();
        NotifyCare();
        if (_save.TodayCount("wish.added") >= 2 || _rng.NextDouble() > 0.15) return;
        var options = Kitchen.Foods.Where(f => f.Cost > 0 && f.Fullness >= 5 && Kitchen.CanCook(f, _save)
            && f.Id != food.Id && !_save.WishList.Contains(f.Id) && !TodayRec.Foods.Contains(f.Id)).ToList();
        if (options.Count == 0) return;
        var next = options[_rng.Next(options.Count)];
        _save.WishList.Add(next.Id);
        _save.CountToday("wish.added");
        Persist();
        NotifyCare();
        Later(8, () => Mutter(R.Text("wish.new", _save.OwnerName, ("ястие", next.Name.ToLowerInvariant())), 5));
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
