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
        public int Stage { get; init; } = 1;  // 0 – първата стъпка в друг уред (Food.Prep), 1 – главният уред
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
        public bool Forced { get; set; }    // Developer: готово веднага, без да чака Тут
        public bool Clicked { get; set; }   // Тут цъкна уреда, след като е готово
        public double NextCall { get; set; } // кога пак да я извика да цъкне
    }

    private Cooking? _cook;
    private double _nextCookCheck;

    private bool Cooks => _cook != null;

    /// <summary>Уредът се тресе точно преди капакът да хвръкне (за темпото на цикъла).</summary>
    /// <summary>Тресе се точно преди да е готово, а после (докато не го цъкнеш) – по 1 с на всеки 5.</summary>
    private bool CookShaking => _cook is { Popped: false } c && Now > c.PopAt - ShakeBeforePop &&
        (Now < c.PopAt || (Now - c.PopAt) % 5 < 1);

    private (Food Food, double Fullness, double Energy, int Cost)? _pendingCook;
    private double _pendingCookAt;

    /// <summary>
    /// „Сготви“: спира каквото прави (и работата), мръдва малко встрани и тогава
    /// слага уреда пред себе си. Ако нещо го прекъсне по пътя – слага го, където е.
    /// </summary>
    private void BeginCooking(Food food, double fullness, double energy, int cost)
    {
        _pendingCook = (food, fullness, energy, cost);
        _pendingCookAt = Now + 8;
        if (_onTrampoline) EndBounce(null);
        if (_inScene) LeaveScene();
        if (_state is BearState.Drag or BearState.Falling or BearState.Jump || IsAsleep)
        {
            StartPendingCook();
            return;
        }
        var area = Area;
        int dir = _rng.Next(2) == 0 ? -1 : 1;
        double step = SpriteWidth * (0.5 + _rng.NextDouble() * 0.4);
        if (_x + dir * (step + SpriteWidth * 1.5) > area.Right || _x + dir * (step + SpriteWidth * 1.5) < area.Left) dir = -dir;
        // Още не е пуснал уреда: казва какво е избрал („Ще направя супа! Да си взема
        // тенджерата…“); репликите за пуснат уред са, като се появи уредът.
        string first = food.Prep ?? food.Tool ?? "";
        Say(R.Pick("cook.choose", _save.OwnerName).Replace("{ястие}", food.Name.ToLowerInvariant()).Replace("{уред}", ToolName(first)), 5);
        WalkTo(_x + dir * step, () =>
        {
            _facingLeft = dir < 0;
            StartPendingCook();
        });
    }

    private void StartPendingCook()
    {
        if (_pendingCook is not { } p) return;
        _pendingCook = null;
        StartCooking(p.Food, p.Fullness, p.Energy, p.Cost);
    }

    /// <summary>Уредите на рецептата поред (първо Prep, ако има) и колко секунди е всеки.</summary>
    public static (string Tool, double Seconds)[] CookSteps(Food f) =>
        f.Tool == null ? Array.Empty<(string, double)>()
        : f.Prep != null ? new[] { (f.Prep, CookSeconds(f.Prep)), (f.Tool, CookSeconds(f.Tool)) }
        : new[] { (f.Tool, CookSeconds(f.Tool)) };

    public static double CookSeconds(string tool) => tool switch
    {
        Kitchen.Oven => 10 * 60,
        Kitchen.Pan => 6 * 60,
        Kitchen.Pot => 3 * 60,
        Kitchen.Grill => 6 * 60,
        Kitchen.IceCream => 4 * 60,
        Kitchen.SandwichMaker => 3 * 60,
        Kitchen.Jug => 90,
        Kitchen.Table => 2 * 60,
        _ => 60,
    };

    private void StartCooking(Food food, double fullnessEach, double energyEach, int cost, double seconds = -1, bool quiet = false, int stage = -1)
    {
        // Рецепта в два уреда: първо в Prep (миксер, кафемашина…), после в главния.
        if (stage < 0) stage = food.Prep != null ? 0 : 1;
        string tool = stage == 0 ? food.Prep! : food.Tool!;
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
            Stage = stage,
            Window = window,
            Fullness = fullnessEach,
            Energy = energyEach,
            Cost = cost,
            Left = food.Count,
            PopAt = Now + seconds,
        };
        window.Clicked += CookerClicked;
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
        _save.CookStage = stage;
        _save.CookReadyAt = DateTime.Now.AddSeconds(seconds);
        _save.CookCost = cost;
        _save.CookX = x;
        Persist();

        if (!InFocus && !quiet) Say(Lines.Pick(FoodTalk.For(food, tool).Start, _save.OwnerName), 5);
        _nextCookCheck = Now + 20;
        _nextWorkCookCheck = Now + 180 + _rng.Next(120);
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
        StartCooking(food, Kitchen.FullnessOf(food, _save) / food.Count, food.Energy / food.Count, _save.CookCost, left,
            stage: food.Prep != null ? _save.CookStage : 1);
    }

    /// <summary>Половината ширина на уреда (рисунките са 22 пиксела).</summary>
    private double CookerHalf => 11 * PixelSize;

    /// <summary>
    /// Мечокът до уреда: средите им са на толкова – видимата част на уреда
    /// откъм мечока плюс видимата половина на мечока и малко празно място (CookerSpace) (рисунките
    /// имат празни ръбове). Така лапата почти докосва уреда, но никога не е в него.
    /// dir: от коя страна е уредът спрямо мечока (1 = вдясно от мечока).
    /// </summary>
    private double CookerGap(int dir) => CookerGapFor(CookerArt(""), dir);

    private double CookerGapFor(System.Windows.Media.Imaging.BitmapSource art, int dir)
    {
        var (left, right) = Visible(art);
        var bear = Visible(_lib.Get("idle").Frames[0]);
        return ((dir > 0 ? left : right) + Math.Max(bear.Left, bear.Right) + CookerSpace) * PixelSize;
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
        Math.Abs(x - c.Window.CenterX) < CookerGap(x < c.Window.CenterX ? 1 : -1) - (CookerSpace - 1) * PixelSize;

    /// <summary>Празни пиксели между мечока и уреда: уредът е пред него, а не върху него.</summary>
    private const int CookerSpace = 5;

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
        // Каничката е с цвета на сока; другите уреди – със сложения скин (cook_pot_copper…).
        string name = tool == Kitchen.Jug ? "cook_jug_" + (food.Id == "juice_berry" ? "berry" : "orange") + frame : Furniture.CurrentArt(_save, tool, frame);
        return _lib.GetProp(name, 22, 24, 0).Image;
    }

    private void UpdateCookingTag()
    {
        var c = _cook!;
        c.Window.SetTime(c.Popped ? null : Now >= c.PopAt ? "готово!" : Clock(TimeSpan.FromSeconds(Math.Max(0, c.PopAt - Now))));
    }


    /// <summary>Мечокът може да дойде до уреда (не работи, не спи, не е скрит, не го държат).</summary>
    private bool CanComeToCooker(Cooking c) =>
        !IsAsleep && IsVisible && !_onTrampoline && !c.Window.IsDragging && !c.Window.IsFalling && _eating == null &&
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
                if (now < c.PopAt) Announce(Lines.Pick(FoodTalk.For(c.Food, c.Tool).Almost, _save.OwnerName), 3, food: true);
                c.Going = true;
                WalkTo(spot, () =>
                {
                    if (_cook is not { Popped: false } cc) return;
                    cc.Going = false;
                    _facingLeft = faceLeft;
                    PlayNear(CookerAnchor, Stirs(cc.Tool) ? "cook" : "idle", length: Math.Max(2, cc.PopAt - Now + 1));
                });
            }

            c.Window.Shake(CookShaking ? Math.Round(Math.Sin(now * 50)) * PixelSize : 0);
            // Първата стъпка (рецепта в два уреда) е готова: мечокът сам я пренася във
            // втория уред – отива до уреда и „цъка“ вместо Тут. Ако не може да дойде
            // (спи, вдигнат е, работите), след 15 с го прави и без да идва.
            if (now >= c.PopAt && c.Stage == 0 && !c.Forced && !c.Clicked)
            {
                if (at || !canCome || now > c.PopAt + 15)
                {
                    if (at) _facingLeft = c.Window.CenterX < _x;
                    NextCookingStage(c);
                    return;
                }
                c.Window.SetArt(CookerArt((int)(now / 0.3) % 2 == 0 ? "" : "_b"), PixelSize);
                return;
            }
            // Готово е: чака Тут да цъкне уреда. Мечокът стои до него и я вика.
            if (now >= c.PopAt && !c.Forced && !c.Clicked)
            {
                if (at && now > c.NextCall)
                {
                    c.NextCall = now + 35 + _rng.Next(25);
                    _facingLeft = c.Window.CenterX < _x;
                    Say(Lines.Pick(ReadyLines(c), _save.OwnerName), 5);
                    if (CanAnimateFreely && _state == BearState.Idle) Play("happy");
                }
                c.Window.SetArt(CookerArt((int)(now / 0.3) % 2 == 0 ? "" : "_b"), PixelSize);
                return;
            }
            bool ready = now >= c.PopAt;
            if (!ready)
            {
                // Два кадъра: къкри, цвърчи, свети.
                c.Window.SetArt(CookerArt((int)(now / 0.3) % 2 == 0 ? "" : "_b"), PixelSize);
                return;
            }
            // Първата стъпка е готова: мечокът „пренася купичката“ във втория уред.
            if (c.Stage == 0)
            {
                NextCookingStage(c);
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
            else if (!InFocus) Say(Lines.Pick(FoodTalk.For(c.Food, c.Tool).Pop, _save.OwnerName), 2);
            if (c.Left == 0) c.DoneAt = now + 1.5;
            return;
        }

        if (c.Left == 0 && now >= c.DoneAt) EndCooking(food: c.Food);
    }

    /// <summary>Рецепта в два уреда: първият е готов – затваря се и на същото място се включва вторият.</summary>
    private void NextCookingStage(Cooking c)
    {
        _cook = null;
        _save.CookX = c.Window.CenterX;
        c.Window.Close();
        StartCooking(c.Food, c.Fullness, c.Energy, c.Cost, quiet: true, stage: 1);
        string next = ToolName(c.Food.Tool!);
        if (!InFocus) Say(R.Pick("cook.nextstep", _save.OwnerName).Replace("{уред}", next).Replace("{ястие}", c.Food.Name.ToLowerInvariant()), 4);
        if (CanAnimateFreely && _state == BearState.Idle) Play("happy");
    }

    /// <summary>Уредът с член, за репликите („тенджерата“, „миксера“…).</summary>
    private static string ToolName(string tool) => tool switch
    {
        Kitchen.Pot => "тенджерата", Kitchen.Pan => "тигана", Kitchen.Oven => "фурната",
        Kitchen.CoffeeMachine => "кафемашината", Kitchen.Kettle => "чайника", Kitchen.Mixer => "миксера",
        Kitchen.SandwichMaker => "сандвичарника", Kitchen.IceCream => "машинката за сладолед", Kitchen.Grill => "скарата",
        Kitchen.Table => "масичката",
        _ => "каничката",
    };

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
        if (_cook is not { Popped: false } c) return false;
        // Краят наближава (или е готово и чака Тут): стои до уреда (и по време на работа).
        if (CookFinale)
        {
            if (AtCooker(c))
            {
                _facingLeft = c.Window.CenterX < _x;
                PlayNear(CookerAnchor, Stirs(c.Tool) ? "cook" : "idle", length: 3);
            }
            return true;
        }
        if (InFocus || Now < _nextCookCheck) return false;
        _nextCookCheck = Now + 25 + _rng.Next(35);
        var talk = FoodTalk.For(c.Food, c.Tool);

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
                Announce(Lines.Pick(talk.Check, _save.OwnerName), 3, food: true);
                PlayNear(CookerAnchor, Stirs(c.Tool) ? "cook" : "idle", length: 6 + _rng.Next(7));
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
                _sceneNear = CookerAnchor;   // седи до уреда – премести ли го Тут, отива при него
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
        Mutter(Lines.Pick(talk.During, _save.OwnerName), 3, food: true);
        return false;
    }

    /// <summary>Клик върху уреда: „Да спра ли готвенето?“</summary>
    /// <summary>Ястието с член („пуканките“, „кафето“) – за репликите.</summary>
    private static string Capital(string s) => s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..];

    private static string TheFood(Food f) => f.Id switch
    {
        "popcorn" => "пуканките",
        "meatballs" => "кюфтенцата",
        "potatoes" => "картофките",
        "coffee" => "кафето",
        "tea" => "чая",
        _ when f.Id.StartsWith("juice") => "сока",
        _ => f.Name.ToLowerInvariant(),
    };

    /// <summary>
    /// Какво казва, докато се готви: само за храната. Ако прави нещо друго
    /// (чете, работи…) – логично: „Ще почета още малко, докато чакам пуканките.“
    /// </summary>
    private string CookingChatter()
    {
        var c = _cook!;
        string the = TheFood(c.Food);
        string[] lines;
        if (_inScene && _scene != null && (_scene == Scenes.Focus || Scenes.Work.Contains(_scene)))
            lines = R.Get("cook.while.work");
        else if (_inScene && _scene == Scenes.Reading)
            lines = R.Get("cook.while.read");
        else if (_inScene || _onTrampoline)
            lines = R.Get("cook.while.other");
        else
            lines = FoodTalk.For(c.Food, c.Tool).During;
        return Lines.Pick(lines, _save.OwnerName).Replace("{ястие}", the).Replace("{Ястие}", Capital(the));
    }

    /// <summary>Краят наближава или е готово: стои до уреда (дори да работят).</summary>
    private bool CookFinale => _cook is { Popped: false } c && Now > c.PopAt - FinaleLead(c) - 2;

    private double _nextWorkCookCheck;

    /// <summary>
    /// По време на работа (на бюрото) от време на време (на 3–5 мин) става да
    /// погледне храната и после пак сяда да работи. true = стана.
    /// </summary>
    private bool CheckFoodWhileWorking()
    {
        if (_cook is not { Popped: false } c || CookFinale || Now < _nextWorkCookCheck) return false;
        _nextWorkCookCheck = Now + 180 + _rng.Next(120);
        var (stand, faceLeft) = CookerSpot();
        LeaveScene();
        WalkTo(stand, () =>
        {
            _facingLeft = faceLeft;
            PlayNear(CookerAnchor, Stirs(c.Tool) ? "cook" : "idle", length: 4 + _rng.Next(3));
        });
        return true;
    }

    /// <summary>Клик върху уреда: готово ли е – изважда храната; иначе пита дали да спре.</summary>
    private void CookerClicked()
    {
        if (_cook is { Popped: false } c && Now >= c.PopAt)
        {
            c.Clicked = true;
            return;
        }
        AskStopCooking();
    }

    /// <summary>Готово е: вика Тут да цъкне уреда (според ястието).</summary>
    private static string[] ReadyLines(Cooking c)
    {
        string tool = ToolName(c.Tool);
        string food = $"{c.Food.Icon} {c.Food.Name}";
        var lines = R.Get(c.Tool == Kitchen.Jug ? "cook.ready.juice" : "cook.ready");
        return lines.Select(l => l.Replace("{уред}", tool).Replace("{Уред}", Capital(tool)).Replace("{ястие}", food)).ToArray();
    }

    private void AskStopCooking()
    {
        if (_cook is not { Popped: false } c) return;
        Ask(new ChatQuestion($"Да спра ли готвенето на {c.Food.Name.ToLowerInvariant()}? Лешниците ти се връщат.",
            new ChatOption("Да, спри", p => p.StopCooking()),
            new ChatOption("Не, нека се готви", p => p.Cheer(R.Any("cook.keep")))));
    }

    /// <summary>Спира готвенето (сбъркано ястие): уредът изчезва, лешниците се връщат.</summary>
    public void StopCooking()
    {
        if (_cook is not { Popped: false } c) return;
        _save.Hazelnuts += c.Cost;
        if (c.Food.Cost == 0) _save.BerriesReadyAt = DateTime.MinValue;
        EndCooking(food: null);
        Say(R.Any("cook.stopped"), 4);
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
