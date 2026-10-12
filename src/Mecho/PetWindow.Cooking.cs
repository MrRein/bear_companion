using System;
using System.Linq;
using System.Windows;

namespace Mecho;

// Готвене: уредът (тенджера, тиган, фурна, кафемашина, чайник) е отделен
// прозорец на земята с таймер над него. Мечокът е свободен – може да работи
// (мечо-доро), да се разхожда и да прави други неща, но е в „режим готвене“:
// от време на време се връща, бърка, сяда до уреда и коментира. Като стане,
// капакът хвръква и храната излита от уреда. Клик върху уреда пита дали да го
// спре (лешниците се връщат). Готвенето се помни и след рестарт.
public sealed partial class PetWindow
{
    private const double ShakeBeforePop = 0.8;

    private sealed class Cooking
    {
        public required Food Food { get; init; }
        public required string Tool { get; init; }
        public required CookerWindow Window { get; init; }
        public double Fullness { get; init; }
        public double Energy { get; init; }
        public int Cost { get; init; }
        public int Left { get; set; }
        public double PopAt { get; set; }
        public double NextPieceAt { get; set; }
        public double DoneAt { get; set; }
        public bool Popped { get; set; }
        public bool Finale { get; set; }    // дошъл е за края
    }

    private Cooking? _cook;
    private double _nextCookCheck;

    private bool Cooks => _cook != null;

    /// <summary>Уредът се тресе точно преди капакът да хвръкне (за темпото на цикъла).</summary>
    private bool CookShaking => _cook is { Popped: false } c && Now > c.PopAt - ShakeBeforePop;

    public static double CookSeconds(string tool) => tool switch
    {
        Kitchen.Oven => 10 * 60,
        Kitchen.Pan => 6 * 60,
        Kitchen.Pot => 3 * 60,
        Kitchen.Jug => 90,
        _ => 60,
    };

    private static readonly string[] CookingDuring =
    {
        "*бърка*", "Мирише вкусно!", "Още малко…", "*опитва* Мм, почти.", "Не надничай, {0}! Изненада е.", "*тананика си, докато готви*",
    };

    private static readonly string[] CookingCheck =
    {
        "Да видя как върви готвенето…", "*бърка* Мм, ухае.", "Само да разбъркам.", "*наднича* Още не е готово.",
    };

    private static readonly string[] CookingAlmost = { "Почти е готово! Отивам да видя.", "Още малко… *тича към котлона*", "Ще стане всеки момент!" };

    private static readonly string[] CookingSit = { "*сяда до котлона* Ще го пазя.", "Ще поседя тук, да не изгори.", "*сяда* Чакаме…" };

    private void StartCooking(Food food, double fullnessEach, double energyEach, int cost, double seconds = -1)
    {
        string tool = food.Tool!;
        if (seconds < 0) seconds = CookSeconds(tool);

        // Уредът застава точно пред мечока (накъдето гледа; ако там няма място –
        // от другата страна). След рестарт – където е бил.
        var area = Area;
        double reach = CookerGap;
        int dir = _facingLeft ? -1 : 1;
        if (_x + dir * (reach + CookerHalf) > area.Right || _x + dir * (reach + CookerHalf) < area.Left) dir = -dir;
        double x = !double.IsNaN(_save.CookX) && _save.CookFood == food.Id ? _save.CookX : _x + dir * reach;
        x = Math.Clamp(x, area.Left + CookerHalf, area.Right - CookerHalf);
        if (_state is BearState.Idle or BearState.Walk) _facingLeft = x < _x;

        var window = new CookerWindow();
        _cook = new Cooking
        {
            Food = food,
            Tool = tool,
            Window = window,
            Fullness = fullnessEach,
            Energy = energyEach,
            Cost = cost,
            Left = food.Count,
            PopAt = Now + seconds,
        };
        window.Clicked += AskStopCooking;
        window.Dropped += (cx, _) =>
        {
            // Тут го премести: пада плавно на земята там, където го пусна.
            var a = AreaAt(cx, window.Top + window.ActualHeight / 2);
            double nx = Math.Clamp(cx, a.Left + CookerHalf, a.Right - CookerHalf);
            window.FallTo(nx, a.Bottom);
            _save.CookX = nx;
            Persist();
        };
        window.SetAway(!IsVisible);
        window.Place(CookerArt(""), PixelSize, x, area.Bottom);
        UpdateCookingTag();

        _save.CookFood = food.Id;
        _save.CookReadyAt = DateTime.Now.AddSeconds(seconds);
        _save.CookCost = cost;
        _save.CookX = x;
        Persist();

        if (!InFocus) Say(Lines.Pick(CookStartLines(tool), _save.OwnerName), 5);
        _nextCookCheck = Now + 20;
    }

    /// <summary>След рестарт: продължава готвенето оттам, докъдето е стигнало.</summary>
    private void ResumeCooking()
    {
        if (_save.CookFood == null) return;
        var food = Kitchen.Foods.FirstOrDefault(f => f.Id == _save.CookFood);
        if (food?.Tool == null)
        {
            _save.CookFood = null;
            return;
        }
        double left = Math.Max(3, (_save.CookReadyAt - DateTime.Now).TotalSeconds);
        StartCooking(food, Kitchen.FullnessOf(food, _save) / food.Count, food.Energy / food.Count, _save.CookCost, left);
    }

    /// <summary>Половината ширина на уреда (рисунките са 22 пиксела).</summary>
    private double CookerHalf => 11 * PixelSize;

    /// <summary>
    /// Мечокът до уреда: средите им са на толкова (рисунките имат празни ръбове,
    /// така лапата почти докосва уреда).
    /// </summary>
    private double CookerGap => CookerHalf + SpriteWidth / 2 - 6 * PixelSize;

    /// <summary>
    /// Къде застава до уреда (не върху него): от страната, откъдето идва (или
    /// където има място), гледайки към уреда. extra отдалечава (за сядане).
    /// </summary>
    private (double X, bool FaceLeft) CookerSpot(double extra = 0)
    {
        var c = _cook!;
        var area = AreaAt(c.Window.CenterX, c.Window.Bottom - 1);
        double off = CookerGap + extra;
        double left = c.Window.CenterX - off, right = c.Window.CenterX + off;
        bool leftOk = left - HalfWidth >= area.Left, rightOk = right + HalfWidth <= area.Right;
        bool useLeft = leftOk && (!rightOk || _x <= c.Window.CenterX);
        return useLeft ? (left, false) : (right, true);
    }

    /// <summary>В тенджерата, тигана и каничката бърка; другите само наднича.</summary>
    private static bool Stirs(string tool) => tool is Kitchen.Pot or Kitchen.Pan or Kitchen.Jug;

    /// <summary>Рисунката на уреда (каничката е с цвета на сока: cook_jug_orange…).</summary>
    private System.Windows.Media.Imaging.BitmapSource CookerArt(string frame)
    {
        var c = _cook!;
        string name = c.Tool == Kitchen.Jug ? "jug_" + c.Food.Id.Replace("juice_", "") : c.Tool;
        return _lib.GetProp("cook_" + name + frame, 22, 24, 0).Image;
    }

    private void UpdateCookingTag()
    {
        var c = _cook!;
        c.Window.SetTime(c.Popped ? null : Clock(TimeSpan.FromSeconds(Math.Max(0, c.PopAt - Now))));
    }

    /// <summary>Всеки кадър: уредът къкри, тресе се, капакът хвръква, храната излита.</summary>
    private void UpdateCooking(double now, double dt)
    {
        var c = _cook!;
        if (c.Window.Step(dt))
        {
            // Кацна: ако чака края до уреда, отива при новото място.
            if (!c.Popped) c.Finale = false;
        }
        if (!c.Popped)
        {
            UpdateCookingTag();
            // Към края отива при уреда и чака там капакът да хвръкне.
            if (!c.Finale && c.PopAt - now < 25 && !InFocus && !IsAsleep && !c.Window.IsDragging && !c.Window.IsFalling &&
                _state is not (BearState.Drag or BearState.Falling or BearState.Jump or BearState.Bounce))
            {
                c.Finale = true;
                if (_onTrampoline) EndBounce(null);
                if (_inScene && !InFocus) LeaveScene();
                var (spot, faceLeft) = CookerSpot();
                Announce(Lines.Pick(CookingAlmost, _save.OwnerName), 3);
                WalkTo(spot, () =>
                {
                    _facingLeft = faceLeft;
                    if (_cook is { Popped: false } cc)
                        Play(Stirs(cc.Tool) ? "cook" : "idle", length: Math.Max(2, cc.PopAt - Now + 2));
                });
            }
            c.Window.Shake(CookShaking ? Math.Round(Math.Sin(now * 50)) * PixelSize : 0);
            if (now < c.PopAt)
            {
                // Два кадъра: къкри, цвърчи, свети.
                c.Window.SetArt(CookerArt((int)(now / 0.3) % 2 == 0 ? "" : "_b"), PixelSize);
                return;
            }
            c.Popped = true;
            c.NextPieceAt = now;
            c.Window.Shake(0);
            c.Window.SetArt(CookerArt("_open"), PixelSize);
            UpdateCookingTag();
        }

        if (c.Left > 0 && now >= c.NextPieceAt)
        {
            bool popcorn = c.Tool == Kitchen.Pot && c.Food.Count > 1;
            int pieces = popcorn ? 1 : c.Left;
            for (int i = 0; i < pieces; i++) SpawnFood(c.Food, c.Fullness, c.Energy, c.Window.Spout, fromCooker: true);
            c.Left -= pieces;
            if (popcorn)
            {
                if (!InFocus) Say(c.Left == 0 ? "…пук!" : _rng.Next(3) == 0 ? "Пук-пук!" : "Пук!", 0.6);
                // Последната закъснява: тъкмо си мислиш, че е свършило.
                c.NextPieceAt = now + (c.Left == 1 ? 1.4 : 0.25 + _rng.NextDouble() * 0.3);
            }
            else if (!InFocus) Say(Lines.Pick(CookPopLines(c.Tool), _save.OwnerName), 2);
            if (c.Left == 0) c.DoneAt = now + 1.5;
            return;
        }

        if (c.Left == 0 && now >= c.DoneAt) EndCooking(food: c.Food);
    }

    private void EndCooking(Food? food)
    {
        var c = _cook!;
        _cook = null;
        c.Window.Close();
        _save.CookFood = null;
        Persist();
        if (food != null && !InFocus) FoodReady(food);
    }

    /// <summary>
    /// Режим готвене (вика се, когато е свободен): от време на време отива при
    /// уреда и бърка или сяда до него; иначе прави нещо друго. true = зае се.
    /// </summary>
    private bool CookingStep()
    {
        if (_cook is not { Popped: false } c || InFocus || Now < _nextCookCheck) return false;
        _nextCookCheck = Now + 40 + _rng.Next(80);

        var (stand, faceLeft) = CookerSpot();
        int roll = _rng.Next(10);
        if (roll < 5)
        {
            // Бърка (тенджера, тиган) или наднича (фурна, кафе, чайник).
            WalkTo(stand, () =>
            {
                _facingLeft = faceLeft;
                Announce(Lines.Pick(CookingCheck, _save.OwnerName), 3);
                Play(Stirs(c.Tool) ? "cook" : "idle", length: 6 + _rng.Next(7));
            });
            return true;
        }
        if (roll < 8)
        {
            // Сяда до уреда за минута-две (малко по-настрани, не върху него).
            var (seat, seatFace) = CookerSpot(SpriteWidth * 0.12);
            WalkTo(seat, () =>
            {
                StartScene(Scenes.Sit, quiet: true);
                _facingLeft = seatFace;
                _sceneUntil = Now + 60 + _rng.Next(90);
                Announce(Lines.Pick(CookingSit, _save.OwnerName), 3);
            });
            return true;
        }
        Mutter(Lines.Pick(CookingDuring, _save.OwnerName), 3);
        return false;
    }

    /// <summary>Клик върху уреда: „Да спра ли готвенето?“</summary>
    private void AskStopCooking()
    {
        if (_cook is not { Popped: false } c) return;
        Ask(new ChatQuestion($"Да спра ли готвенето на {c.Food.Name.ToLowerInvariant()}? Лешниците ти се връщат.",
            new ChatOption("Да, спри", p => p.StopCooking()),
            new ChatOption("Не, нека се готви", p => p.Cheer("Добре! Още малко търпение."))));
    }

    /// <summary>Спира готвенето (сбъркано ястие): уредът изчезва, лешниците се връщат.</summary>
    public void StopCooking()
    {
        if (_cook is not { Popped: false } c) return;
        _save.Hazelnuts += c.Cost;
        if (c.Food.Cost == 0) _save.BerriesReadyAt = DateTime.MinValue;
        EndCooking(food: null);
        Say("Добре, спрях котлона. Лешниците са обратно при теб.", 4);
        NotifyCare();
    }

    /// <summary>Developer: готово веднага.</summary>
    internal void DevFinishCooking()
    {
        if (_cook is { Popped: false } c) c.PopAt = Now;
    }

    private static string[] CookStartLines(string tool) => tool switch
    {
        Kitchen.Pot => new[] { "*сипва царевица в тенджерата* Ще пукат след малко!", "Пуканки! Капакът е затворен… засега.", "Тенджерата е на котлона!" },
        Kitchen.Pan => new[] { "*цвър-цвър* Пържа кюфтенца!", "Кюфтенцата са в тигана. Ще ги наглеждам.", "Цвърчат! Не пипай, горещо е!" },
        Kitchen.Oven => new[] { "*слага тавата във фурната* Чакаме…", "Мечешки картофки! Фурната загрява…", "Във фурната са. Ще ги наглеждам." },
        Kitchen.CoffeeMachine => new[] { "*бззз* Кафемашината работи!", "*подскача нетърпеливо* Хайде, хайде…", "Мелим, мелим… *бррр*" },
        Kitchen.Jug => new[] { "*изстисква плодовете в каничката* Сега ще го разбъркам.", "Сок в каничка! Трябва да се разбърка хубаво.", "*дрън-дрън с лъжичката*" },
        _ => new[] { "*чайникът загрява*", "Водата скоро ще заври.", "*слага мед в чашата*" },
    };

    private static string[] CookPopLines(string tool) => tool switch
    {
        Kitchen.Pan => new[] { "*хоп* Подхвърлих ги!", "Опа! Хвръкнаха от тигана!" },
        Kitchen.Oven => new[] { "*отваря фурната* Готови са!", "Хрупкави! *духа на лапата*" },
        Kitchen.CoffeeMachine => new[] { "*пшшш* Кафето е готово!", "Ааа, мирише на сутрин!" },
        Kitchen.Jug => new[] { "*налива в чашата* Сокът е готов!", "Разбъркан и студен! Наздраве!" },
        _ => new[] { "*фиууу* Чайникът свири!", "Готово! С мед!" },
    };
}
