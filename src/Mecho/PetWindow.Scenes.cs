using System;
using System.Linq;
using System.Windows;

namespace Mecho;

/// <summary>
/// Кътче: мечокът сяда (или застава) с някакъв предмет и прави нещо за малко.
/// Seat е седалката под него (null = стои прав), Side е предметът до него
/// (SideDir -1 = отляво, +1 = отдясно), Anim е анимацията, докато е там.
/// Рисунките са в assets/props, анимациите в assets/bear.
/// </summary>
public sealed record Scene(
    string Id, string? Seat, string? Side, int SideDir, string Anim,
    string[] Start, string[] During, string[] End)
{
    /// <summary>Как се казва в менюто „Почивай си“.</summary>
    public string Name { get; init; } = "";

    /// <summary>Иконката в менюто (assets/ui/&lt;Icon&gt;.png).</summary>
    public string Icon { get; init; } = "couch";

    /// <summary>Кой предмет от магазина го отключва (null = свободно).</summary>
    public string? Unlock { get; init; }

    /// <summary>Анимацията, докато Тут не е нарисувала своята (Anim) – например doing_sit с предмет в лапата.</summary>
    public string? Fallback { get; init; }
}

public static class Scenes
{
    /// <summary>Забавленията: кътчета, в които сяда сам, когато скучае, или когато Тут му каже „Почивай си“.</summary>
    public static Scene[] Fun => new[] { Reading, Gaming, Tea, Yoga, Plant, Music, Knit, Guitar, Trampoline };

    /// <summary>Работа (без мечо-доро): „Работи си“ – бюро, тефтер, настолна игра, статив.</summary>
    public static Scene[] Work => new[] { Desk, Write, Board, Painting };

    public static Scene[] All => Fun.Concat(Work).ToArray();

    /// <summary>Работи на бюрото сам (без мечо-доро): мисли, скицира, пише.</summary>
    public static readonly Scene Desk = new("desk", "stool", "desk", 1, "focus",
        R.Get("scene.desk.start"),
        R.Get("scene.desk.during"),
        R.Get("scene.desk.end"))
    { Name = "Работи на бюрото", Icon = "tasks" };

    /// <summary>Пише в тефтера (без предмет).</summary>
    public static readonly Scene Write = new("write", null, null, 0, "write",
        R.Get("scene.write.start"),
        R.Get("scene.write.during"),
        R.Get("scene.write.end"))
    { Name = "Пиши в тефтера", Icon = "notes" };

    /// <summary>Батутът е особен: не седи, а подскача (виж PetWindow.Trampoline.cs).</summary>
    public static readonly Scene Trampoline = new("trampoline", null, null, 0, "bounce",
        R.Get("scene.trampoline.start"),
        R.Get("scene.trampoline.during"),
        R.Get("scene.trampoline.end"))
    { Name = "Скачай на батута", Icon = "trampoline", Unlock = Kitchen.Trampoline };

    /// <summary>Гардеробът: появява се до него, докато е отворен табът „Гардероб“.</summary>
    public static readonly Scene Wardrobe = new("wardrobe", null, "wardrobe", 1, "idle",
        R.Get("scene.wardrobe.start"),
        Array.Empty<string>(),
        R.Get("scene.wardrobe.end"))
    { Name = "Гардероб", Icon = "wardrobe" };

    /// <summary>Седи на земята и пие напитката, която му дадоха (чай, кафе, сок).</summary>
    public static readonly Scene Drink = new("drink", null, null, 0, "sit_drinking",
        Array.Empty<string>(), Array.Empty<string>(),
        R.Get("scene.drink.end"))
    { Name = "Пие", Icon = "tea" };

    /// <summary>Просто си седи (когато дълго няма какво да прави).</summary>
    public static readonly Scene Sit = new("sit", null, null, 0, "sit",
        R.Get("scene.sit.start"),
        R.Get("scene.sit.during"),
        R.Get("scene.sit.end"))
    { Name = "Седни", Icon = "couch" };

    public static Scene ById(string? id) => All.FirstOrDefault(s => s.Id == id) ?? Reading;

    public static bool IsUnlocked(Scene scene, SaveData save) => scene.Unlock == null || Kitchen.Owns(save, scene.Unlock);

    public static readonly Scene Reading = new("reading", "couch", null, 0, "read",
        Lines.SatDown, Lines.Reading, Lines.DoneReading)
    { Name = "Чети книжка", Icon = "book", Unlock = null };

    /// <summary>Работният режим: сериозен, с очила, на бюро. Не говори, докато работи.</summary>
    public static readonly Scene Focus = new("focus", "stool", "desk", 1, "focus",
        R.Get("scene.focus.start"),
        Array.Empty<string>(),
        R.Get("scene.focus.end"));

    public static readonly Scene Gaming = new("gaming", "stool", "computer", -1, "game",
        R.Get("scene.gaming.start"),
        R.Get("scene.gaming.during"),
        R.Get("scene.gaming.end"))
    { Name = "Играй на компютър", Icon = "game", Unlock = Kitchen.Computer };

    public static readonly Scene Painting = new("painting", "stool", "easel", 1, "doing_sit",   // с четка в лапата
        R.Get("scene.painting.start"),
        R.Get("scene.painting.during"),
        R.Get("scene.painting.end"))
    { Name = "Рисувай на статив", Icon = "easel", Unlock = Kitchen.Easel };

    public static readonly Scene Tea = new("tea", "stool", "table_tea", 1, "sit_drinking",
        R.Get("scene.tea.start"),
        R.Get("scene.tea.during"),
        R.Get("scene.tea.end"))
    { Name = "Пий чай", Icon = "tea", Unlock = Kitchen.Kettle };

    public static readonly Scene Yoga = new("yoga", "mat", null, 0, "yoga",
        R.Get("scene.yoga.start"),
        R.Get("scene.yoga.during"),
        R.Get("scene.yoga.end"))
    { Name = "Прави йога", Icon = "yoga", Unlock = Kitchen.YogaMat };

    public static readonly Scene Plant = new("plant", null, null, 1, "doing",   // издърпва саксията и се грижи за растението (PetWindow.Garden.cs)
        R.Get("scene.plant.start"),
        R.Get("scene.plant.during"),
        R.Get("scene.plant.end"))
    { Name = "Грижи се за растението", Icon = "plant", Unlock = Kitchen.PlantPot };

    public static readonly Scene Music = new("music", null, "radio", -1, "dance",   // танцува на музиката
        R.Get("scene.music.start"),
        R.Get("scene.music.during"),
        R.Get("scene.music.end"))
    { Name = "Слушай музика", Icon = "music", Unlock = Kitchen.Radio };

    /// <summary>Плете на столче, кошницата с преждата е до него (куките са в лапите).</summary>
    public static readonly Scene Knit = new("knit", "stool", "yarn_basket", -1, "knit",
        R.Get("scene.knit.start"),
        R.Get("scene.knit.during"),
        R.Get("scene.knit.end"))
    { Name = "Плети", Icon = "yarn", Unlock = Kitchen.YarnBasket, Fallback = "doing_sit" };

    /// <summary>Свири на китара на столчето (китарата е в лапите; скинът ѝ се вижда).</summary>
    public static readonly Scene Guitar = new("guitar", "stool", null, 0, "guitar",
        R.Get("scene.guitar.start"),
        R.Get("scene.guitar.during"),
        R.Get("scene.guitar.end"))
    { Name = "Свири на китара", Icon = "guitar", Unlock = Kitchen.Guitar, Fallback = "doing_sit" };

    public static readonly Scene Board = new("board", "stool", "table_board", 1, "work",
        R.Get("scene.board.start"),
        R.Get("scene.board.during"),
        R.Get("scene.board.end"))
    { Name = "Играй настолна игра", Icon = "board", Unlock = Kitchen.BoardGame };
}

// Кътчетата: сяда, прави нещо и после всичко изчезва.
public sealed partial class PetWindow
{
    private bool _inScene;
    private Scene? _scene;
    private double _sceneUntil;     // 0 = докато не го вдигнеш (само „Седни“)
    private Prop? _seatProp, _sideProp;

    private double SeatHeight => _seatProp == null ? 0 : _seatProp.Seat * PixelSize;

    /// <summary>Диванът се купува; без него чете седнал на земята.</summary>
    private bool HasCouch => Kitchen.Owns(_save, Kitchen.Couch);

    /// <summary>„Седни“: сяда на дивана и чете, докато не го вдигнеш.</summary>
    public void StayHere() => Relax(Scenes.Reading);

    /// <summary>
    /// „Почивай си“: Тут избира кътче (четене, статив, компютър…) и мечокът стои там,
    /// докато не натисне „Стани“. Заключените кътчета искат предмет от магазина.
    /// </summary>
    public void Relax(Scene scene)
    {
        if (BusyEating()) return;
        if (!Scenes.IsUnlocked(scene, _save))
        {
            var item = scene.Unlock == null ? null : Kitchen.Find(scene.Unlock);
            Say($"Нямам {item?.Name.ToLowerInvariant()}…", 3);
            return;
        }
        if (InFocus)
        {
            Say(R.Any("work.first"), 3);
            return;
        }
        _save.StayPut = true;
        _save.StayScene = scene.Id;
        _save.AutoMode = false;
        _save.SleepingByChoice = false;
        // Не веднага: става, помисля няколко секунди и чак тогава отива (виж UpdateIdle).
        if (scene == Scenes.Trampoline ? !_onTrampoline : _scene != scene) BeginModeChange(1, 2, R.Pick($"scene.{scene.Id}.intent", _save.OwnerName));
        _sceneUntil = 0;
        _idleSince = Now;
        OpenParcelFirst();
        Persist();
    }

    /// <summary>„Почивай си“ без избор: сменя кътчетата на случаен принцип.</summary>
    public const string RandomRelax = "random";

    private Scene? _lastFun;   // за да не повтаря същото кътче подред

    public bool RelaxesRandomly => _save.StayPut && _save.StayScene == RandomRelax;

    /// <summary>„Работи си“ без избор: сменя работните занимания сам.</summary>
    public const string RandomWork = "random_work";

    public bool WorksRandomly => _save.StayPut && _save.StayScene == RandomWork;

    /// <summary>Сам е решил да си почива/работи (по часовника) – Тут може да го изключи.</summary>
    public bool AutoMode => _save.StayPut && _save.AutoMode;

    /// <summary>„Работи си“: сменя работните занимания (бюро, тефтер, игра, статив), докато не стане.</summary>
    public void WorkRandom()
    {
        if (BusyEating()) return;
        if (InFocus)
        {
            Say(R.Any("work.already"), 3);
            return;
        }
        _save.StayPut = true;
        _save.StayScene = RandomWork;
        _save.AutoMode = false;
        _save.SleepingByChoice = false;
        BeginModeChange(1, 2.5, R.Pick("mode.work.think", _save.OwnerName));
        _idleSince = Now;
        OpenParcelFirst();
        Persist();
    }

    private double _modeDecideAt;     // в режим „остани“: кога решава какво да прави (плавен преход)
    private bool _sayIntentNext;      // при решението: да каже ли какво е избрал

    /// <summary>
    /// Плавно влизане в режим: оставя заниманието, постоява и мисли (from–to секунди),
    /// казва какво му е наум и чак после избира какво да прави (UpdateIdle).
    /// </summary>
    private void BeginModeChange(double from, double to, string? thought)
    {
        if (_onTrampoline) EndBounce(null);
        if (_inScene) LeaveScene();
        _modeDecideAt = Now + from + _rng.NextDouble() * (to - from);
        _sayIntentNext = _save.StayScene is RandomRelax or RandomWork;
        if (!string.IsNullOrEmpty(thought)) Say(thought, 3);
        // Самопроверката не чака (там проверяваме самите занимания; паузата – отделно).
        if (InstantModes)
        {
            _modeDecideAt = 0;
            DecideMode();
            return;
        }
        if (CanAnimateFreely) Play("think", length: _modeDecideAt - Now);
    }

    internal bool InstantModes = SelfTest.Running;

    /// <summary>Решава какво да прави в режима: случайно занимание (почивай/работи си) или избраното кътче.</summary>
    private void DecideMode()
    {
        // Сам е решил (по часовника): блокът свърши – избира пак почивка или работа.
        if (_save.AutoMode && Now > _autoUntil) PickAutoMode();
        Scene? started = null;
        if (RelaxesRandomly) started = StartRandomRelax(_lastFun);
        else if (WorksRandomly) started = StartRandomWork(_lastFun);
        else StartScene(Scenes.ById(_save.StayScene), quiet: true);
        if (_sayIntentNext && started != null) SayIntent(started, Scenes.Fun);
        _sayIntentNext = false;
    }

    /// <summary>Казва какво мисли да прави („Ще си поработя по идеи за настолна игра.“).</summary>
    private void SayIntent(Scene scene, Scene[] all) => Say(R.Pick($"scene.{scene.Id}.intent", _save.OwnerName), 5);

    /// <summary>Следващото работно занимание (различно от предното), за 10–20 минути.</summary>
    private Scene StartRandomWork(Scene? last, bool quiet = true)
    {
        var options = Scenes.Work.Where(f => Scenes.IsUnlocked(f, _save) && f != last).ToList();
        var scene = options.Count > 0 ? options[_rng.Next(options.Count)] : Scenes.Desk;
        StartScene(scene, quiet: quiet);
        _sceneUntil = Now + 600 + _rng.Next(600);
        return scene;
    }

    /// <summary>„Почивай си“: прави различни неща за почивка едно след друго, докато не стане.</summary>
    public void RelaxRandom()
    {
        if (BusyEating()) return;
        if (InFocus)
        {
            Say(R.Any("work.first"), 3);
            return;
        }
        _save.StayPut = true;
        _save.StayScene = RandomRelax;
        _save.AutoMode = false;
        _save.SleepingByChoice = false;
        BeginModeChange(1, 2.5, R.Pick("mode.relax.think", _save.OwnerName));
        _idleSince = Now;
        OpenParcelFirst();
        Persist();
    }

    /// <summary>Следващото случайно кътче (по възможност различно от предното), за 3–6 минути.</summary>
    private Scene StartRandomRelax(Scene? last, bool quiet = true)
    {
        var options = Scenes.Fun.Where(f => Scenes.IsUnlocked(f, _save) && f != last).ToList();
        var scene = options.Count > 0 ? options[_rng.Next(options.Count)] : Scenes.Reading;
        if (scene == Scenes.Trampoline) StartTrampoline(180 + _rng.Next(180), quiet: quiet);
        else
        {
            StartScene(scene, quiet: quiet);
            _sceneUntil = Now + 180 + _rng.Next(180);
        }
        return scene;
    }

    /// <summary>Забавлението, което прави в момента (избрано от Тут или само), или null.</summary>
    public Scene? CurrentFun => _onTrampoline ? Scenes.Trampoline : _inScene && _scene != null && Scenes.All.Contains(_scene) ? _scene : null;

    /// <summary>Кътчето, в което Тут го е оставила (или null).</summary>
    public Scene? ChosenScene => _save.StayPut && !RelaxesRandomly && !WorksRandomly ? Scenes.ById(_save.StayScene) : null;

    /// <summary>„Спри“: спира каквото прави (кътче, батут, разходка, пеперуда). Таймерите си текат.</summary>
    public void StopDoing()
    {
        if (IsAsleep || BusyEating()) return;
        GetUp();
        _freeUntil = Now + Minutes(10, 20);
        if (_state is BearState.Walk or BearState.Chase || (_state == BearState.Busy && _afterBusy != "sleep")) SetIdle();
    }

    /// <summary>Става от дивана и диванът изчезва.</summary>
    public void GetUp()
    {
        if (_save.StayPut) StopAutoMode();
        _save.StayPut = false;
        if (_onTrampoline) EndBounce(null);
        LeaveScene();
        Persist();
    }

    /// <summary>Кътче за малко (едно от нещата, които прави, когато е свободен).</summary>
    private void StartScene(Scene scene, double seconds)
    {
        if (scene == Scenes.Trampoline)
        {
            StartTrampoline(seconds);
            return;
        }
        StartScene(scene, quiet: false);
        _sceneUntil = Now + seconds;
    }

    private void StartScene(Scene scene, bool quiet)
    {
        if (scene == Scenes.Trampoline)
        {
            if (_inScene) LeaveScene();
            StartTrampoline(0, quiet);
            return;
        }
        if (_onTrampoline) EndBounce(null);
        // Статив: всеки път с друга четка (червена или синя).
        if (scene == Scenes.Painting) _brush = _rng.Next(2) == 0 ? "paintbrush_red" : "paintbrush_blue";
        _scene = scene;
        _inScene = true;
        _sceneUntil = 0;
        if (Scenes.Fun.Contains(scene) && !TodayRec.Hobbies.Contains(scene.Id)) TodayRec.Hobbies.Add(scene.Id);
        LoadSceneArt(scene);
        _facingLeft = false;
        SetState(BearState.Couch, SceneAnim(scene));
        ClampX();
        if (scene == Scenes.Plant) StartGarden();
        if (IsExhausted && scene != Scenes.Focus && _rng.Next(2) == 0) Announce(Lines.Pick(Lines.SleepyDoing, _save.OwnerName), 3);
        else if (!quiet && scene == Scenes.Painting && R.Has($"easel.{EaselNumber}"))
            Announce(R.Pick($"easel.{EaselNumber}", _save.OwnerName));   // какво рисува днес
        else if (!quiet) Announce(Lines.Pick(scene == Scenes.Reading && !HasCouch ? Lines.SatOnFloor : scene.Start, _save.OwnerName));
    }

    /// <summary>След като е бил вдигнат и пуснат: сяда пак в същото кътче.</summary>
    private void ResumeScene()
    {
        if (_scene == null)
        {
            _inScene = false;
            SetIdle();
            return;
        }
        _facingLeft = false;
        SetState(BearState.Couch, SceneAnim(_scene));
        ClampX();
    }

    /// <summary>Анимацията на кътчето: нарисуваната или временната (Fallback).</summary>
    private string SceneAnim(Scene scene) => scene.Fallback != null && !_lib.Has(scene.Anim) ? scene.Fallback : scene.Anim;

    private void LeaveScene()
    {
        if (_scene != null && Scenes.All.Contains(_scene)) _lastFun = _scene;
        PutPlantAway();
        bool wasAuto = _autoBlock;
        _cup = null;
        _autoBlock = false;
        _inScene = false;
        _scene = null;
        _sceneUntil = 0;
        _idleSince = Now;
        // В режим (почивай/работи си): между две занимания постоява 3–6 с, после решава.
        if (_save.StayPut && (RelaxesRandomly || WorksRandomly) && !InstantModes) _modeDecideAt = Math.Max(_modeDecideAt, Now + 3 + _rng.NextDouble() * 3);
        // След блок, който си е избрал сам (седене, хоби): кратка разходка и пак
        // избира; след нещо, избрано от Тут, свободата почва отначало.
        StartFreeCycle(sitNow: wasAuto);
        if (_state is BearState.Couch) SetIdle();
    }

    private void LoadSceneArt(Scene scene)
    {
        string? seat = scene.Seat == "couch" && !HasCouch ? null : scene.Seat;
        if (seat == "couch") seat = Furniture.Current(_save, Kitchen.Couch).Prop;   // скинът на дивана
        // Бюрото със столчето се купува (едно); без него работи и играе на земята.
        if (seat == "stool") seat = Kitchen.Owns(_save, Kitchen.Desk) ? Furniture.StoolArt(_save) : null;
        // Права анимация (work, write…) не сяда на столче – иначе стои прав върху него.
        if (seat != null && StandingAnims.Contains(SceneAnim(scene))) seat = null;
        string? side = scene.Side;
        if (side == "desk") side = Kitchen.Owns(_save, Kitchen.Desk) ? Furniture.CurrentArt(_save, Kitchen.Desk) : null;
        if (side == "easel") side = EaselArt();
        // Музиката: сложеното радио е на земята – тогава до него няма второ; иначе е скинът на радиото.
        if (side == "radio") side = RadioPlaced ? null : Furniture.CurrentArt(_save, Kitchen.Radio);
        _seatProp = seat == null ? null : _lib.GetProp(seat, 16, 8, 7);
        _sideProp = side == null ? null : _lib.GetProp(side, 16, 16, 0);
        SizeSceneArt();
    }

    /// <summary>Коя картина е на статива днес (сменя се на 3 дни): 1…9.</summary>
    private int EaselNumber => 1 + (DateTime.Today.DayOfYear / 3 + EaselShift) % 9;

    internal int EaselShift;   // Developer: „Следваща картина“

    /// <summary>Статива: картината на него се сменя на 3 дни (easel_1…9; ако ги няма – празният).</summary>
    private string EaselArt()
    {
        int n = EaselNumber;
        return _lib.HasProp("easel_" + n) ? "easel_" + n : "easel";
    }

    /// <summary>Анимации, в които мечокът е прав (с тях не сяда на столчето/дивана).</summary>
    private static readonly System.Collections.Generic.HashSet<string> StandingAnims = new()
    {
        "idle", "work", "work_tired", "write", "write_tired", "doing", "doing_tired", "dance", "walk", "happy", "think", "juggle", "yoga",
    };

    private void SizeSceneArt()
    {
        if (_seatProp != null)
        {
            _seat.Source = _seatProp.Image;
            _seat.Width = _seatProp.Image.PixelWidth * PixelSize;
            _seat.Height = _seatProp.Image.PixelHeight * PixelSize;
        }
        if (_sideProp != null)
        {
            _side.Source = _sideProp.Image;
            _side.Width = _sideProp.Image.PixelWidth * PixelSize;
            _side.Height = _sideProp.Image.PixelHeight * PixelSize;
        }
    }

    private void UpdateScene(double now)
    {
        var scene = _scene;
        if (scene == null)
        {
            LeaveScene();
            return;
        }

        bool stay = _save.StayPut && scene.Id == _save.StayScene;
        if (!stay && _sceneUntil > 0 && now > _sceneUntil)
        {
            Announce(Lines.Pick(scene.End, _save.OwnerName), 3);
            LeaveScene();
            return;
        }

        if (scene == Scenes.Wardrobe)
        {
            UpdateWardrobe(now);
            return;
        }

        if (scene == Scenes.Sit)
        {
            UpdateSitting(now);
            return;
        }

        if (scene == Scenes.Drink)
        {
            UpdateDrinking(now);
            return;
        }
        if (scene == Scenes.Tea) UpdateDrinking(now);
        if (scene == Scenes.Plant) UpdateGarden(now);


        if (scene == Scenes.Focus)
        {
            // Работи, докато трае мечо-дорото или работният таймер. Мълчи.
            if (!InFocus)
            {
                Say(Lines.Pick(scene.End, _save.OwnerName), 3);
                LeaveScene();
                return;
            }
            UpdateFocusScene(now);
            return;
        }

        bool dozing = false;
        if (scene == Scenes.Reading)
        {
            // Капнал е: задрямва над книжката (енергията от това не се вдига).
            dozing = _save.Energy < DozeEnergy && now > _readAwakeUntil;
            string anim = dozing ? "read_sleep" : "read";
            if (anim != _anim)
            {
                _anim = anim;
                _animTime = 0;
            }
        }

        if (!dozing && !IsQuiet && !InFocus && scene.During.Length > 0 && now > _nextChatter)
        {
            _nextChatter = now + (stay ? 2400 : 480) + _rng.Next(stay ? 1800 : 480);
            // На настолната игра и на бюрото понякога си говори за игрите.
            bool games = scene == Scenes.Board || scene == Scenes.Desk || scene == Scenes.Write;
            string during = scene == Scenes.Painting && R.Has($"easel.{EaselNumber}") && _rng.Next(2) == 0
                ? R.Pick($"easel.{EaselNumber}", _save.OwnerName)
                : Lines.Pick(games && _rng.Next(2) == 0 ? Lines.Chatter : scene.During, _save.OwnerName);
            Mutter(during, 5);
        }
    }

    /// <summary>Под толкова енергия задрямва над книжката.</summary>
    private const double DozeEnergy = 15;

    /// <summary>Цъкнаха го, докато дреме над книжката: известно време пак чете.</summary>
    private double _readAwakeUntil;

    /// <summary>Дреме над книжката и Тут го цъкна: стряска се и пак чете (3 минути).</summary>
    private bool StartleFromDoze()
    {
        if (_anim != "read_sleep") return false;
        _readAwakeUntil = Now + 180;
        _anim = "read";
        _animTime = 0;
        Say(Lines.Pick(Lines.DozeStartled, _save.OwnerName), 3);
        return true;
    }
}
