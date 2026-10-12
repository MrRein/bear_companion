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
}

public static class Scenes
{
    /// <summary>Забавленията: кътчета, в които сяда сам, когато скучае, или когато Тут му каже „Почивай си“.</summary>
    public static Scene[] Fun => new[] { Reading, Painting, Gaming, Tea, Yoga, Plant, Music, Board, Trampoline };

    /// <summary>Батутът е особен: не седи, а подскача (виж PetWindow.Trampoline.cs).</summary>
    public static readonly Scene Trampoline = new("trampoline", null, null, 0, "bounce",
        new[] { "Ще поскачам малко!", "Батут! Уиииии!", "*пуф* Батут! Да скачаме!", "Гледай колко високо!" },
        new[] { "Уиии!", "По-високо!", "Виждам целия екран оттук!", "Хоп! Хоп! Хоп!" },
        new[] { "Уф, задъхах се. Стига скачане.", "Добре, слизам.", "Краката ми са като желе!" })
    { Name = "Скачай на батута", Icon = "trampoline", Unlock = Kitchen.Trampoline };

    /// <summary>Гардеробът: появява се до него, докато е отворен табът „Гардероб“.</summary>
    public static readonly Scene Wardrobe = new("wardrobe", null, "wardrobe", 1, "idle",
        new[] { "*отваря гардероба* Какво да облека?", "Гардеробът! Ще ме облечеш ли?", "*рови в гардероба* Мм, толкова избор." },
        Array.Empty<string>(),
        new[] { "*затваря гардероба* Готов съм!", "Как изглеждам?", "Модата е важна за мечките." })
    { Name = "Гардероб", Icon = "wardrobe" };

    /// <summary>Седи на земята и пие напитката, която му дадоха (чай, кафе, сок).</summary>
    public static readonly Scene Drink = new("drink", null, null, 0, "sit_drinking",
        Array.Empty<string>(), Array.Empty<string>(),
        new[] { "Ах, хубаво беше!", "*оставя чашата* Мм, благодаря!", "Изпих го до дъно." })
    { Name = "Пие", Icon = "tea" };

    /// <summary>Просто си седи (когато дълго няма какво да прави).</summary>
    public static readonly Scene Sit = new("sit", null, null, 0, "sit",
        new[] { "Ще си почина малко.", "Ох, хубаво е да се седне.", "Ще поседя тук." },
        new[] { "*гледа облаците*", "*подсвирква си*", "Тихо е днес.", "*клати крачета*" },
        new[] { "Хайде, ставам.", "*протяга се* Стига седене." })
    { Name = "Седни", Icon = "couch" };

    public static Scene ById(string? id) => Fun.FirstOrDefault(s => s.Id == id) ?? Reading;

    public static bool IsUnlocked(Scene scene, SaveData save) => scene.Unlock == null || Kitchen.Owns(save, scene.Unlock);

    public static readonly Scene Reading = new("reading", "couch", null, 0, "read",
        Lines.SatDown, Lines.Reading, Lines.DoneReading)
    { Name = "Чети книжка", Icon = "book", Unlock = null };

    /// <summary>Работният режим: сериозен, с очила, на бюро. Не говори, докато работи.</summary>
    public static readonly Scene Focus = new("focus", "stool", "desk", 1, "focus",
        new[] { "*слага очилата* Работим.", "Сериозен режим. Започваме.", "Фокус. Аз пиша, ти рисуваш." },
        Array.Empty<string>(),
        new[] { "*сваля очилата* Готово!", "Свършихме. Браво на нас!", "Уф, свършихме работа!" });

    public static readonly Scene Gaming = new("gaming", "stool", "computer", -1, "game",
        new[] { "Ще поиграя малко!", "*включва компютъра* Само едно ниво!", "Време за игричка!", "Да видим дали ще мина босa." },
        new[] { "Ееей! Почти!", "Още едно ниво…", "Кой е сложил тук този шип?!", "РЕКОРД! …а, не, сбърках.", "Пиу-пиу-пиу!", "Тази игра има нужда от повече мечки." },
        new[] { "Добре, стига игри. Очите ми са квадратни.", "Запазих играта. Ставам.", "Спечелих! Е, почти." })
    { Name = "Играй на компютър", Icon = "game", Unlock = Kitchen.Computer };

    public static readonly Scene Painting = new("painting", "stool", "easel", 1, "paint",
        new[] { "Отивам да порисувам малко.", "Ще рисувам като {0}!", "Статив, четка, вдъхновение!", "Днес съм художник." },
        new[] { "Малко синьо тук…", "Хмм, това облак ли е или овца?", "{0}, как правиш сенките?", "Шедьовър в процес на работа.", "*мацва с четката*" },
        new[] { "Готово! Ще го закача на хладилника.", "Нарисувах те! …прилича малко на мен.", "Изкуството е трудно. Ставам." })
    { Name = "Рисувай на статив", Icon = "easel", Unlock = Kitchen.Easel };

    public static readonly Scene Tea = new("tea", "stool", "table_tea", 1, "sit_drinking",
        new[] { "Ще си направя чай.", "Време за чай.", "*налива чай* Ммм.", "Малка почивка с чай." },
        new[] { "*сърба*", "С мед е най-хубав.", "Горещо! *духа*", "{0}, искаш ли и ти чай?" },
        new[] { "Ах, стопли ме.", "Чашата е празна. Ставам.", "Хубав чай. Да си почина пак после." })
    { Name = "Пий чай", Icon = "tea", Unlock = Kitchen.Kettle };

    public static readonly Scene Yoga = new("yoga", "mat", null, 0, "yoga",
        new[] { "Ще се поразтегна малко.", "Малко йога!", "Разтягане! Пробвай и ти, {0}.", "*постила постелка*" },
        new[] { "Вдишай… издишай…", "Поза „дърво“. Аз съм дърво.", "Олеле, не ме бутай!", "{0}, протегни се и ти." },
        new[] { "Намасте!", "Чувствам се като ново мече.", "Готово. Гъвкав съм като макарона." })
    { Name = "Прави йога", Icon = "yoga", Unlock = Kitchen.YogaMat };

    public static readonly Scene Plant = new("plant", null, "plant", 1, "water",
        new[] { "Ще погледна цветето.", "Да полея цветето.", "Цветенцето е жадно!", "*взима лейката*" },
        new[] { "Расти, расти!", "Пораснало е с един лист!", "*шшш*" },
        new[] { "Готово. Ще порасне голямо.", "Цветето каза „мерси“. Шегувам се. Или?" })
    { Name = "Полей цветето", Icon = "plant", Unlock = Kitchen.PlantPot };

    public static readonly Scene Music = new("music", null, "radio", -1, "music",
        new[] { "Ще послушам малко музика.", "*пуска музика*", "Малко музика!", "Това е любимата ми песен!" },
        new[] { "♪ ла-ла-ла ♪", "*клати глава*", "Туц-туц-туц.", "{0}, чуваш ли? Супер е!" },
        new[] { "Песента свърши.", "*спира музиката* Пак после.", "Хубаво беше." })
    { Name = "Слушай музика", Icon = "music", Unlock = Kitchen.Radio };

    public static readonly Scene Board = new("board", "stool", "table_board", 1, "work",
        new[] { "Ще поиграя на настолна игра.", "Да пробвам новия си прототип!", "Масичка, карти, зарове. Готово.", "Играя срещу себе си. Ще спечеля!" },
        new[] { "Хмм, тази карта е твърде силна.", "Записвам: по-малко правила!", "Ход на мечока. После пак ход на мечока.", "Нова механика! …не, стара е." },
        new[] { "Плейтестът мина. Почти.", "Победих. Мен си.", "Прибирам картите." })
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
        if (!Scenes.IsUnlocked(scene, _save))
        {
            var item = scene.Unlock == null ? null : Kitchen.Find(scene.Unlock);
            Say($"Нямам {item?.Name.ToLowerInvariant()}… Може да ми го купиш от магазина за {item?.Price} 🌰.", 4);
            return;
        }
        if (InFocus)
        {
            Say("Първо работата! После почивка.", 3);
            return;
        }
        _save.StayPut = true;
        _save.StayScene = scene.Id;
        _save.SleepingByChoice = false;
        if (scene == Scenes.Trampoline ? !_onTrampoline : _scene != scene) StartScene(scene, quiet: false);
        _sceneUntil = 0;
        _idleSince = Now;
        Persist();
    }

    /// <summary>„Почивай си“ без избор: сменя кътчетата на случаен принцип.</summary>
    public const string RandomRelax = "random";

    private Scene? _lastFun;   // за да не повтаря същото кътче подред

    public bool RelaxesRandomly => _save.StayPut && _save.StayScene == RandomRelax;

    /// <summary>„Почивай си“: прави различни неща за почивка едно след друго, докато не стане.</summary>
    public void RelaxRandom()
    {
        if (InFocus)
        {
            Say("Първо работата! После почивка.", 3);
            return;
        }
        _save.StayPut = true;
        _save.StayScene = RandomRelax;
        _save.SleepingByChoice = false;
        var last = CurrentFun;
        if (_onTrampoline) EndBounce(null);
        if (_inScene) LeaveScene();
        StartRandomRelax(last);
        _idleSince = Now;
        Persist();
    }

    /// <summary>Следващото случайно кътче (по възможност различно от предното), за 3–6 минути.</summary>
    private void StartRandomRelax(Scene? last)
    {
        var options = Scenes.Fun.Where(f => Scenes.IsUnlocked(f, _save) && f != last).ToList();
        var scene = options.Count > 0 ? options[_rng.Next(options.Count)] : Scenes.Reading;
        if (scene == Scenes.Trampoline) StartTrampoline(180 + _rng.Next(180));
        else
        {
            StartScene(scene, quiet: false);
            _sceneUntil = Now + 180 + _rng.Next(180);
        }
    }

    /// <summary>Забавлението, което прави в момента (избрано от Тут или само), или null.</summary>
    public Scene? CurrentFun => _onTrampoline ? Scenes.Trampoline : _inScene && _scene != null && Scenes.Fun.Contains(_scene) ? _scene : null;

    /// <summary>Кътчето, в което Тут го е оставила (или null).</summary>
    public Scene? ChosenScene => _save.StayPut && !RelaxesRandomly ? Scenes.ById(_save.StayScene) : null;

    /// <summary>Става от дивана и диванът изчезва.</summary>
    public void GetUp()
    {
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
        _scene = scene;
        _inScene = true;
        _sceneUntil = 0;
        LoadSceneArt(scene);
        _facingLeft = false;
        SetState(BearState.Couch, scene.Anim);
        ClampX();
        if (IsExhausted && scene != Scenes.Focus && _rng.Next(2) == 0) Announce(Lines.Pick(Lines.SleepyDoing, _save.OwnerName), 3);
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
        SetState(BearState.Couch, _scene.Anim);
        ClampX();
    }

    private void LeaveScene()
    {
        if (_scene != null && Scenes.Fun.Contains(_scene)) _lastFun = _scene;
        bool wasAuto = _autoBlock;
        _cup = null;
        _autoBlock = false;
        _inScene = false;
        _scene = null;
        _sceneUntil = 0;
        _idleSince = Now;
        // След блок, който си е избрал сам (седене, хоби): кратка разходка и пак
        // избира; след нещо, избрано от Тут, свободата почва отначало.
        StartFreeCycle(sitNow: wasAuto);
        if (_state is BearState.Couch) SetIdle();
    }

    private void LoadSceneArt(Scene scene)
    {
        string? seat = scene.Seat == "couch" && !HasCouch ? null : scene.Seat;
        if (seat == "couch") seat = Furniture.Current(_save, Kitchen.Couch).Prop;   // скинът на дивана
        _seatProp = seat == null ? null : _lib.GetProp(seat, 16, 8, 6);
        _sideProp = scene.Side == null ? null : _lib.GetProp(scene.Side, 16, 16, 0);
        SizeSceneArt();
    }

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
            _nextChatter = now + (stay ? 600 : 120) + _rng.Next(stay ? 600 : 120);
            Mutter(Lines.Pick(scene.During, _save.OwnerName), 5);
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
