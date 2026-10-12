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
        public bool Going { get; set; }     // тръгнал е към уреда за края
        public bool Forced { get; set; }    // Developer: готово веднага, без да чака мечока
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

    private void StartCooking(Food food, double fullnessEach, double energyEach, int cost, double seconds = -1)
    {
        string tool = food.Tool!;
        if (seconds < 0) seconds = CookSeconds(tool);

        // Уредът застава точно пред мечока (накъдето гледа; ако там няма място –
        // от другата страна). След рестарт – където е бил.
        // Ако е тръгнал нанякъде, спира (иначе ще влезе в уреда).
        if (_state == BearState.Walk) SetIdle();
        var area = Area;
        var art = CookerArtFor(food, tool, "");
        int dir = _facingLeft ? -1 : 1;
        if (_x + dir * (CookerGapFor(art, dir) + CookerHalf) > area.Right || _x + dir * (CookerGapFor(art, dir) + CookerHalf) < area.Left) dir = -dir;
        double reach = CookerGapFor(art, dir);
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

        if (!InFocus) Say(Lines.Pick(FoodTalk.For(food).Start, _save.OwnerName), 5);
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
    /// Мечокът до уреда: средите им са на толкова – видимата част на уреда
    /// откъм мечока плюс видимата половина на мечока и един пиксел (рисунките
    /// имат празни ръбове). Така лапата почти докосва уреда, но никога не е в него.
    /// dir: от коя страна е уредът спрямо мечока (1 = вдясно от мечока).
    /// </summary>
    private double CookerGap(int dir) => CookerGapFor(CookerArt(""), dir);

    private double CookerGapFor(System.Windows.Media.Imaging.BitmapSource art, int dir)
    {
        var (left, right) = Visible(art);
        var bear = Visible(_lib.Get("idle").Frames[0]);
        return ((dir > 0 ? left : right) + Math.Max(bear.Left, bear.Right) + 1) * PixelSize;
    }

    private readonly System.Collections.Generic.Dictionary<System.Windows.Media.Imaging.BitmapSource, (int, int)> _visible = new();

    /// <summary>Колко пиксела има от средата до най-далечния непрозрачен пиксел наляво и надясно.</summary>
    private (int Left, int Right) Visible(System.Windows.Media.Imaging.BitmapSource img)
    {
        if (_visible.TryGetValue(img, out var ext)) return ext;
        int w = img.PixelWidth, h = img.PixelHeight;
        var px = new uint[w * h];
        var conv = new System.Windows.Media.Imaging.FormatConvertedBitmap(img, System.Windows.Media.PixelFormats.Bgra32, null, 0);
        conv.CopyPixels(px, w * 4, 0);
        int minX = w, maxX = -1;
        for (int i = 0; i < px.Length; i++)
        {
            if (px[i] >> 24 == 0) continue;
            minX = Math.Min(minX, i % w);
            maxX = Math.Max(maxX, i % w);
        }
        ext = maxX < 0 ? (w / 2, w / 2) : (w / 2 - minX, maxX + 1 - w / 2);
        _visible[img] = ext;
        return ext;
    }

    /// <summary>Стои върху уреда (видимите им части се застъпват).</summary>
    private bool OverlapsCooker(Cooking c, double x) =>
        Math.Abs(x - c.Window.CenterX) < CookerGap(x < c.Window.CenterX ? 1 : -1) - PixelSize;

    /// <summary>
    /// Къде застава до уреда (не върху него): от страната, откъдето идва (или
    /// където има място), гледайки към уреда. extra отдалечава (за сядане).
    /// </summary>
    private (double X, bool FaceLeft) CookerSpot(double extra = 0)
    {
        var c = _cook!;
        var area = AreaAt(c.Window.CenterX, c.Window.Bottom - 1);
        double left = c.Window.CenterX - CookerGap(1) - extra, right = c.Window.CenterX + CookerGap(-1) + extra;
        bool leftOk = left - HalfWidth >= area.Left, rightOk = right + HalfWidth <= area.Right;
        bool useLeft = leftOk && (!rightOk || _x <= c.Window.CenterX);
        return useLeft ? (left, false) : (right, true);
    }

    /// <summary>В тенджерата, тигана и каничката бърка; другите само наднича.</summary>
    private static bool Stirs(string tool) => tool is Kitchen.Pot or Kitchen.Pan or Kitchen.Jug;

    /// <summary>Рисунката на уреда (каничката е с цвета на сока: cook_jug_orange…).</summary>
    private System.Windows.Media.Imaging.BitmapSource CookerArt(string frame) => CookerArtFor(_cook!.Food, _cook.Tool, frame);

    private System.Windows.Media.Imaging.BitmapSource CookerArtFor(Food food, string tool, string frame)
    {
        string name = tool == Kitchen.Jug ? "jug_" + food.Id.Replace("juice_", "") : tool;
        return _lib.GetProp("cook_" + name + frame, 22, 24, 0).Image;
    }

    private void UpdateCookingTag()
    {
        var c = _cook!;
        c.Window.SetTime(c.Popped ? null : Now >= c.PopAt ? "готово!" : Clock(TimeSpan.FromSeconds(Math.Max(0, c.PopAt - Now))));
    }

    /// <summary>Колко най-много чака уредът мечока, след като е готово (после изхвърчава сам).</summary>
    private const double CookWaitMax = 45;

    /// <summary>Мечокът може да дойде до уреда (не работи, не спи, не е скрит, не го държат).</summary>
    private bool CanComeToCooker(Cooking c) =>
        !InFocus && !IsAsleep && IsVisible && !_onTrampoline && !c.Window.IsDragging && !c.Window.IsFalling &&
        _state is not (BearState.Drag or BearState.Falling or BearState.Jump or BearState.Bounce);

    /// <summary>Стои до уреда (на земята, от която и да е страна).</summary>
    private bool AtCooker(Cooking c) =>
        Math.Abs(_x - CookerSpot().X) < 3 * PixelSize && Math.Abs(_y - c.Window.Bottom) < 4 * PixelSize &&
        _state is BearState.Idle or BearState.Busy;

    /// <summary>Кога тръгва за края: колкото му трябва да стигне, плюс малко.</summary>
    private double FinaleLead(Cooking c) => Math.Abs(_x - CookerSpot().X) / (IsExhausted ? WalkSpeed * 0.6 : WalkSpeed) + 4;

    /// <summary>
    /// Всеки кадър: уредът къкри. Малко преди края мечокът идва до уреда; като
    /// стане готово, уредът трепери, докато мечокът не застане до него, и
    /// тогава капакът хвръква и храната излита.
    /// </summary>
    private void UpdateCooking(double now, double dt)
    {
        var c = _cook!;
        c.Window.Step(dt);
        StepAsideFromCooker(c);
        if (!c.Popped)
        {
            UpdateCookingTag();
            bool canCome = CanComeToCooker(c);
            bool at = AtCooker(c);
            if (c.Going && (_state != BearState.Walk || !canCome)) c.Going = false;

            // Тръгва към уреда за края.
            if (canCome && !at && !c.Going && now > c.PopAt - FinaleLead(c))
            {
                if (_inScene) LeaveScene();
                var (spot, faceLeft) = CookerSpot();
                if (now < c.PopAt) Announce(Lines.Pick(FoodTalk.For(c.Food).Almost, _save.OwnerName), 3);
                c.Going = true;
                WalkTo(spot, () =>
                {
                    if (_cook is not { Popped: false } cc) return;
                    cc.Going = false;
                    _facingLeft = faceLeft;
                    Play(Stirs(cc.Tool) ? "cook" : "idle", length: Math.Max(2, cc.PopAt - Now + 1));
                });
            }

            c.Window.Shake(CookShaking ? Math.Round(Math.Sin(now * 50)) * PixelSize : 0);
            bool ready = now >= c.PopAt && (c.Forced || at || !canCome || now > c.PopAt + CookWaitMax);
            if (!ready)
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
            if (at) _facingLeft = c.Window.CenterX < _x;
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
            else if (!InFocus) Say(Lines.Pick(FoodTalk.For(c.Food).Pop, _save.OwnerName), 2);
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
    /// Мечокът не стои върху уреда: ако е застанал (или седнал) в него – например
    /// Тут пусна уреда върху него – отстъпва встрани, до уреда.
    /// </summary>
    private void StepAsideFromCooker(Cooking c)
    {
        if (c.Window.IsDragging || c.Window.IsFalling || c.Going || !OverlapsCooker(c, _x)) return;
        if (Math.Abs(_y - c.Window.Bottom) > 4 * PixelSize) return;   // на друга височина (на рисунка)
        bool sitting = _state == BearState.Couch && _inScene && _scene != Scenes.Focus && !_onTrampoline;
        if (_state != BearState.Idle && !sitting) return;
        if (sitting) LeaveScene();
        var (spot, faceLeft) = CookerSpot();
        WalkTo(spot, () => _facingLeft = faceLeft);
    }

    /// <summary>
    /// Режим готвене (вика се, когато е свободен): често се приближава до уреда
    /// (понякога само стои до него, понякога бърка или сяда), а от време на време
    /// се отдалечава малко. true = зае се.
    /// </summary>
    private bool CookingStep()
    {
        if (_cook is not { Popped: false } c || InFocus) return false;
        // Краят наближава (или е готово и уредът трепери): стои до уреда и чака.
        if (Now > c.PopAt - FinaleLead(c) - 2)
        {
            if (AtCooker(c))
            {
                _facingLeft = c.Window.CenterX < _x;
                Play(Stirs(c.Tool) ? "cook" : "idle", length: 3);
            }
            return true;
        }
        if (Now < _nextCookCheck) return false;
        _nextCookCheck = Now + 25 + _rng.Next(35);
        var talk = FoodTalk.For(c.Food);

        int roll = _rng.Next(100);
        if (roll < 40)
        {
            // Само се приближава и постоява до уреда, без да казва нищо.
            var (near, nearFace) = CookerSpot(_rng.NextDouble() * SpriteWidth * 0.5);
            if (Math.Abs(near - _x) < PixelSize * 2) return false;
            WalkTo(near, () => _facingLeft = nearFace);
            return true;
        }
        if (roll < 55)
        {
            // Бърка (тенджера, тиган, каничка) или наднича (фурна, кафе, чайник).
            var (stand, faceLeft) = CookerSpot();
            WalkTo(stand, () =>
            {
                _facingLeft = faceLeft;
                Announce(Lines.Pick(talk.Check, _save.OwnerName), 3);
                Play(Stirs(c.Tool) ? "cook" : "idle", length: 6 + _rng.Next(7));
            });
            return true;
        }
        if (roll < 75)
        {
            // Сяда до уреда за минута-две (малко по-настрани) и тогава казва нещо.
            var (seat, seatFace) = CookerSpot(SpriteWidth * 0.12);
            WalkTo(seat, () =>
            {
                StartScene(Scenes.Sit, quiet: true);
                _facingLeft = seatFace;
                _sceneUntil = Now + 60 + _rng.Next(90);
                if (!IsQuiet) Say(Lines.Pick(talk.Sit, _save.OwnerName), 3);
            });
            return true;
        }
        if (roll < 88)
        {
            // Отдалечава се малко от уреда (не стои само до него).
            int side = _x < c.Window.CenterX ? -1 : 1;
            if (_rng.Next(3) == 0) side = -side;
            var area = AreaAt(c.Window.CenterX, c.Window.Bottom - 1);
            double away = c.Window.CenterX + side * (CookerGap(-side) + SpriteWidth * (0.8 + _rng.NextDouble() * 1.4));
            WalkTo(Math.Clamp(away, area.Left + HalfWidth, area.Right - HalfWidth));
            return true;
        }
        Mutter(Lines.Pick(talk.During, _save.OwnerName), 3);
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
        if (_cook is { Popped: false } c)
        {
            c.PopAt = Now;
            c.Forced = true;
        }
    }
}
