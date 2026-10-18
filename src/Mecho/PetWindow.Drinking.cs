using System;
using System.Collections.Generic;

namespace Mecho;

// Пиене: седи с чашата (sit_drinking се върти 2–4 пъти), после отпива
// (sit_drinking_sip); при чай и кафе понякога първо духа (sit_drinking_blow).
// Напитката (чаша чай, кафе, чаша сок) се слага в лапата върху анимацията –
// същите три анимации стават за всички напитки.
public sealed partial class PetWindow
{
    private string? _cup;            // какво държи (tea, coffee, juice_orange…), или null
    private bool _cupHot;
    private int _sipsLeft = -1;      // колко глътки до края (-1 = докато не стане, при чая на масичката)
    private int _loopsBeforeSip;
    private bool _blewThisSip;
    private Food? _drinkFood;        // какво пие (за коментарите)
    private bool _drinkLast;         // последното от поръчката

    private static bool IsDrink(Food f) => Kitchen.IsDrink(f);

    /// <summary>Напитка: сяда и я пие бавно (вместо да я изяде наведнъж).</summary>
    private void StartDrinking(Food food, bool last = false)
    {
        _drinkFood = food;
        _drinkLast = last;
        if (_inScene) LeaveScene();
        StartScene(Scenes.Drink, quiet: true);
        BeginCup(Kitchen.CupFor(food), sips: 4 + _rng.Next(3), hot: Kitchen.IsHot(food));
    }

    private void BeginCup(string drink, int sips, bool? hot = null)
    {
        _cup = drink;
        _cupHot = hot ?? drink is "tea" or "coffee";
        _sipsLeft = sips;
        _loopsBeforeSip = 2 + _rng.Next(3);
        _blewThisSip = false;
        _anim = "sit_drinking";
        _animTime = 0;
    }

    /// <summary>Чашата в лапата: само докато е в някоя от трите анимации за пиене.</summary>
    private IReadOnlyDictionary<string, string> WithCup(IReadOnlyDictionary<string, string> outfit)
    {
        // Рисува/полива/плете/свири: четката, лейката, куките или китарата в лапите.
        if (Dresser.HoldsTool(_anim) && !(_anim.StartsWith("doing") && !_inScene))
        {
            string tool = _scene == Scenes.Plant ? _plantTool
                : _scene == Scenes.Knit ? "knitting"
                : _scene == Scenes.Guitar ? Furniture.CurrentArt(_save, Kitchen.Guitar)
                : _brush;
            return new Dictionary<string, string>(outfit) { [Clothes.Tool.Id] = tool };
        }
        if (_cup == null || !_anim.StartsWith("sit_drinking")) return outfit;
        var withCup = new Dictionary<string, string>(outfit) { [Clothes.Cup.Id] = _cup };
        return withCup;
    }

    /// <summary>С коя четка рисува (сменя се всеки път, като седне на статива).</summary>
    private string _brush = "paintbrush_red";

    private void UpdateDrinking(double now)
    {
        double loop = _lib.Get("sit_drinking").Duration;
        if (_anim == "sit_drinking")
        {
            if (_animTime < loop * _loopsBeforeSip) return;
            // Горещо: понякога първо духа. После отпива.
            string next = _cupHot && !_blewThisSip && _rng.Next(2) == 0 ? "sit_drinking_blow" : "sit_drinking_sip";
            _blewThisSip = next == "sit_drinking_blow";
            _anim = next;
            _animTime = 0;
            if (next == "sit_drinking_blow") Mutter("*духа* Горещо!", 2);
            return;
        }

        if (_anim is "sit_drinking_blow" or "sit_drinking_sip")
        {
            if (_animTime < _lib.Get(_anim).Duration) return;
            bool sipped = _anim == "sit_drinking_sip";
            if (_anim == "sit_drinking_blow")
            {
                _anim = "sit_drinking_sip";   // след духането отпива
                _animTime = 0;
                return;
            }
            _anim = "sit_drinking";
            _animTime = 0;
            _loopsBeforeSip = 2 + _rng.Next(3);
            _blewThisSip = false;
            if (sipped && _sipsLeft > 0 && --_sipsLeft == 0 && _scene == Scenes.Drink)
            {
                if (_drinkFood is not { } drink) Announce(Lines.Pick(Scenes.Drink.End, _save.OwnerName), 3);
                else
                {
                    Joy(JoyFed);
                    if (_drinkLast && _food.Count == 0) FinishedAll(drink);
                    else
                    {
                        Say(DoneLine(drink), 3);
                        FulfillWish(drink);
                    }
                }
                _drinkFood = null;
                LeaveScene();
            }
            else if (sipped && _drinkFood is { } d && _rng.Next(3) == 0)
                Say(Lines.Pick(FoodTalk.For(d).Chew, _save.OwnerName), 2);
        }
    }
}
