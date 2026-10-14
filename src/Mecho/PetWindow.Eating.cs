using System;
using System.Collections.Generic;
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
        if (!ThankForMeal()) Say(DoneLine(food), 4);
    }

    /// <summary>Изяде/изпи: понякога за точно това ядене, понякога общо („Беше вкусно!“).</summary>
    private string DoneLine(Food food) => Kitchen.IsCraving(food, _save)
        ? R.Pick("craving.done", _save.OwnerName).Replace("{ястие}", food.Name.ToLowerInvariant())
        : _rng.Next(2) == 0 && FoodTalk.For(food).Done.Length > 0
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
