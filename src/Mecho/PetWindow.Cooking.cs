using System;
using System.Windows;

namespace Mecho;

// Мечокът готви пред Тут: слага уреда до себе си, бърка, уредът се тресе,
// капакът хвръква и храната излита от уреда. Пуканките пукат една по една.
public sealed partial class PetWindow
{
    private const double ShakeBeforePop = 0.8;

    private sealed class Cooking
    {
        public required Food Food { get; init; }
        public required Scene Scene { get; init; }
        public required string Tool { get; init; }
        public double Fullness { get; init; }
        public double Energy { get; init; }
        public int Left { get; set; }
        public double PopAt { get; init; }
        public double NextPieceAt { get; set; }
        public double DoneAt { get; set; }
        public bool Popped { get; set; }
    }

    private Cooking? _cook;

    private bool Cooks => _cook != null;

    /// <summary>Уредът се тресе точно преди капакът да хвръкне.</summary>
    private double CookShake =>
        _cook is { Popped: false } c && _inScene && _scene == c.Scene && Now > c.PopAt - ShakeBeforePop
            ? Math.Round(Math.Sin(Now * 50)) * PixelSize
            : 0;

    /// <summary>Може ли да готви пред Тут (иначе храната просто изскача от него).</summary>
    private bool CanCookInFront(Food food) =>
        food.Tool != null && !InFocus && !IsAsleep && !_onTrampoline &&
        _state is not (BearState.Drag or BearState.Falling or BearState.Jump or BearState.Bounce);

    private void StartCooking(Food food, double fullnessEach, double energyEach)
    {
        string tool = food.Tool!;
        double seconds = tool switch
        {
            Kitchen.Oven => 5,
            Kitchen.Pan => 4,
            Kitchen.Pot => 3.5,
            _ => 3,
        };
        // Тенджерата и тигана ги бърка; фурната, чайника и кафето ги чака.
        string anim = tool is Kitchen.Pot or Kitchen.Pan ? "cook" : "idle";
        var scene = new Scene("cooking", null, "cook_" + tool, 1, anim,
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

        if (_inScene) LeaveScene();
        _cook = new Cooking
        {
            Food = food,
            Scene = scene,
            Tool = tool,
            Fullness = fullnessEach,
            Energy = energyEach,
            Left = food.Count,
            PopAt = Now + seconds,
        };
        StartScene(scene, quiet: true);
        Say(Lines.Pick(CookStartLines(tool), _save.OwnerName), seconds);
    }

    private static string[] CookStartLines(string tool) => tool switch
    {
        Kitchen.Pot => new[] { "*сипва царевица в тенджерата* Слушай…", "Пуканки! Капакът е затворен… засега.", "*бърка* Още малко…" },
        Kitchen.Pan => new[] { "*цвър-цвър* Пържа кюфтенца!", "*обръща кюфтенцата* Ухаааа.", "Цвърчат! Не пипай, горещо е!" },
        Kitchen.Oven => new[] { "*слага тавата във фурната* Чакаме…", "*наднича през стъклото* Още не са готови.", "Мечешки картофки! Фурната загрява…" },
        Kitchen.CoffeeMachine => new[] { "*бззз* Кафемашината работи!", "*подскача нетърпеливо* Хайде, хайде…", "Мелим, мелим… *бррр*" },
        _ => new[] { "*чайникът загрява*", "Водата почти завря…", "*слага мед в чашата*" },
    };

    private void UpdateCooking(double now)
    {
        var c = _cook!;
        if (!c.Popped)
        {
            if (now < c.PopAt)
            {
                // Два кадъра: къкри, цвърчи, свети.
                string frame = (int)(now / 0.3) % 2 == 0 ? "" : "_b";
                _side.Source = _lib.GetProp("cook_" + c.Tool + frame, 22, 24, 0).Image;
                return;
            }
            c.Popped = true;
            c.NextPieceAt = now;
            _side.Source = _lib.GetProp("cook_" + c.Tool + "_open", 22, 24, 0).Image;
            if (_scene?.Anim == "cook" && CanAnimateFreely) _anim = "idle";
        }

        if (c.Left > 0 && now >= c.NextPieceAt)
        {
            bool popcorn = c.Tool == Kitchen.Pot && c.Food.Count > 1;
            int pieces = popcorn ? 1 : c.Left;
            for (int i = 0; i < pieces; i++) SpawnFood(c.Food, c.Fullness, c.Energy, CookOrigin, fromCooker: true);
            c.Left -= pieces;
            if (popcorn)
            {
                Say(c.Left == 0 ? "…пук!" : _rng.Next(3) == 0 ? "Пук-пук!" : "Пук!", 0.6);
                // Последната закъснява: тъкмо си мислиш, че е свършило.
                c.NextPieceAt = now + (c.Left == 1 ? 1.4 : 0.25 + _rng.NextDouble() * 0.3);
            }
            else Say(Lines.Pick(CookPopLines(c.Tool), _save.OwnerName), 2);
            if (c.Left == 0) c.DoneAt = now + 1.2;
            return;
        }

        if (c.Left == 0 && now >= c.DoneAt)
        {
            var food = c.Food;
            _cook = null;
            LeaveScene();
            FoodReady(food);
        }
    }

    private static string[] CookPopLines(string tool) => tool switch
    {
        Kitchen.Pan => new[] { "*хоп* Подхвърлих ги!", "Опа! Хвръкнаха от тигана!" },
        Kitchen.Oven => new[] { "*отваря фурната* Готови са!", "Хрупкави! *духа на лапата*" },
        Kitchen.CoffeeMachine => new[] { "*пшшш* Кафето е готово!", "Ааа, мирише на сутрин!" },
        _ => new[] { "*фиууу* Чайникът свири!", "Готово! С мед!" },
    };

    /// <summary>Откъдето излита храната: горе на уреда (или от мечока, ако го няма).</summary>
    private Point CookOrigin =>
        _inScene && _sideProp != null && !_sideRect.IsEmpty
            ? new Point(_sideRect.Left + _sideRect.Width / 2, _sideRect.Top + 4 * PixelSize)
            : new Point(_x, _bearRect.Top + _bearRect.Height * 0.3);

    /// <summary>Готвенето е прекъснато: каквото е останало, излиза наведнъж от мечока.</summary>
    private void FlushCooking()
    {
        var c = _cook!;
        _cook = null;
        var from = new Point(_x, _bearRect.Top + _bearRect.Height * 0.3);
        for (int i = 0; i < c.Left; i++) SpawnFood(c.Food, c.Fullness, c.Energy, from, fromCooker: false);
        if (c.Left > 0) FoodReady(c.Food);
    }
}
