using System;
using System.Collections.Generic;

namespace Mecho;

// Пиене: седи с чашата (sit_drinking се върти 2–4 пъти), после отпива
// (sit_drinking_sip); при чай и кафе понякога първо духа (sit_drinking_blow).
// Напитката е рисунката ѝ от храната (food/latte.png, food/tea.png…), закачена за лапата
// (точката _cup от hold_sheet, ръкавите отгоре) – същите три анимации стават за всички напитки.
public sealed partial class PetWindow
{
    private string? _cup;            // рисунката на напитката в лапата (food_latte, food_tea…), или null
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
        BeginCup("food_" + food.ArtId, sips: 4 + _rng.Next(3), hot: Kitchen.IsHot(food));
    }

    private void BeginCup(string drink, int sips, bool? hot = null)
    {
        _cup = drink;
        _cupHot = hot ?? drink is "food_tea" or "food_coffee";
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
        // Само в кътче, което има свой предмет, и не докато слага мебелта (тогава лапите са празни –
        // иначе с „doing“ се появяваше четката, например докато слага бюрото за работа).
        if (Dresser.HoldsTool(_anim) && _inScene && _placing == null)
        {
            string? tool = _scene == Scenes.Plant ? _plantTool
                : _scene == Scenes.Knit ? "knitting"
                : _scene == Scenes.Guitar ? Furniture.CurrentArt(_save, Kitchen.Guitar)
                : _scene == Scenes.Board || _scene == Scenes.BoardPlay ? CardsTool   // на играта: карти в лапите
                : _scene?.Easel == true ? _brush                                     // на статива: четка
                : null;
            if (tool == null) return outfit;
            return new Dictionary<string, string>(outfit) { [Clothes.Tool.Id] = tool };
        }
        // Напитката не е в кадъра: тя е отделна рисунка в лапата (HeldRect).
        return outfit;
    }

    private static readonly string[] CardColors = { "red", "blue", "green", "purple", "yellow" };

    /// <summary>
    /// Картите на настолната игра (tools/make_cards.py): цветът и броят (2–4) се сменят
    /// на всеки 2 часа – не всеки път, а като нова игра.
    /// </summary>
    private static string CardsTool
    {
        get
        {
            var now = DateTime.Now;
            int slot = now.DayOfYear * 12 + now.Hour / 2;
            return $"cards_{CardColors[slot % CardColors.Length]}_{2 + slot / CardColors.Length % 3}";
        }
    }

    /// <summary>С коя четка рисува (сменя се всеки път, като седне на статива).</summary>
    private string _brush = "paintbrush_red";

    /// <summary>Изпи го (или „Да допия ли бързо?“ – Да): казва нещо и става.</summary>
    private void FinishDrink()
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
        _sipsLeft = 0;
        LeaveScene();
    }

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
            if (sipped && _sipsLeft > 0 && --_sipsLeft == 0 && _scene == Scenes.Drink) FinishDrink();
            else if (sipped && _drinkFood is { } d && _rng.Next(3) == 0)
                Say(Lines.Pick(FoodTalk.For(d).Chew, _save.OwnerName), 2);
        }
    }
}
