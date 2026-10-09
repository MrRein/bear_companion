using System;
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
    string[] Start, string[] During, string[] End);

public static class Scenes
{
    public static readonly Scene Reading = new("reading", "couch", null, 0, "read",
        Lines.SatDown, Lines.Reading, Lines.DoneReading);

    /// <summary>Работният режим: сериозен, с очила, на бюро. Не говори, докато работи.</summary>
    public static readonly Scene Focus = new("focus", "stool", "desk", 1, "focus",
        new[] { "*слага очилата* Работим.", "Сериозен режим. Започваме.", "Фокус. Аз пиша, ти рисуваш." },
        Array.Empty<string>(),
        new[] { "*сваля очилата* Готово!", "Свършихме. Браво на нас!", "Уф, свършихме работа!" });

    public static readonly Scene Gaming = new("gaming", "stool", "computer", -1, "game",
        new[] { "*включва компютъра* Само едно ниво!", "Време за игричка!", "Да видим дали ще мина босa." },
        new[] { "Ееей! Почти!", "Още едно ниво…", "Кой е сложил тук този шип?!", "РЕКОРД! …а, не, сбърках.", "Пиу-пиу-пиу!", "Тази игра има нужда от повече мечки." },
        new[] { "Добре, стига игри. Очите ми са квадратни.", "Запазих играта. Ставам.", "Спечелих! Е, почти." });

    public static readonly Scene Painting = new("painting", "stool", "easel", 1, "paint",
        new[] { "Ще рисувам като {0}!", "Статив, четка, вдъхновение!", "Днес съм художник." },
        new[] { "Малко синьо тук…", "Хмм, това облак ли е или овца?", "{0}, как правиш сенките?", "Шедьовър в процес на работа.", "*мацва с четката*" },
        new[] { "Готово! Ще го закача на хладилника.", "Нарисувах те! …прилича малко на мен.", "Изкуството е трудно. Ставам." });

    public static readonly Scene Tea = new("tea", "stool", "table_tea", 1, "drink",
        new[] { "Време за чай.", "*налива чай* Ммм.", "Малка почивка с чай." },
        new[] { "*сърба*", "С мед е най-хубав.", "Горещо! *духа*", "{0}, искаш ли и ти чай?" },
        new[] { "Ах, стопли ме.", "Чашата е празна. Ставам.", "Хубав чай. Да си почина пак после." });

    public static readonly Scene Yoga = new("yoga", "mat", null, 0, "yoga",
        new[] { "Малко йога!", "Разтягане! Пробвай и ти, {0}.", "*постила постелка*" },
        new[] { "Вдишай… издишай…", "Поза „дърво“. Аз съм дърво.", "Олеле, не ме бутай!", "{0}, протегни се и ти." },
        new[] { "Намасте!", "Чувствам се като ново мече.", "Готово. Гъвкав съм като макарона." });

    public static readonly Scene Plant = new("plant", null, "plant", 1, "water",
        new[] { "Да полея цветето.", "Цветенцето е жадно!", "*взима лейката*" },
        new[] { "Расти, расти!", "Пораснало е с един лист!", "*шшш*" },
        new[] { "Готово. Ще порасне голямо.", "Цветето каза „мерси“. Шегувам се. Или?" });

    public static readonly Scene Music = new("music", null, "radio", -1, "music",
        new[] { "*пуска музика*", "Малко музика!", "Това е любимата ми песен!" },
        new[] { "♪ ла-ла-ла ♪", "*клати глава*", "Туц-туц-туц.", "{0}, чуваш ли? Супер е!" },
        new[] { "Песента свърши.", "*спира музиката* Пак после.", "Хубаво беше." });

    public static readonly Scene Board = new("board", "stool", "table_board", 1, "work",
        new[] { "Да пробвам новия си прототип!", "Масичка, карти, зарове. Готово.", "Играя срещу себе си. Ще спечеля!" },
        new[] { "Хмм, тази карта е твърде силна.", "Записвам: по-малко правила!", "Ход на мечока. После пак ход на мечока.", "Нова механика! …не, стара е." },
        new[] { "Плейтестът мина. Почти.", "Победих. Мен си.", "Прибирам картите." });
}

// Кътчетата: сяда, прави нещо и после всичко изчезва.
public sealed partial class PetWindow
{
    private bool _inScene;
    private Scene? _scene;
    private double _sceneUntil;     // 0 = докато не го вдигнеш (само „Седни“)
    private Prop? _seatProp, _sideProp;

    private double SeatHeight => _seatProp == null ? 0 : _seatProp.Seat * PixelSize;

    /// <summary>„Седни“: сяда на дивана и чете, докато не го вдигнеш.</summary>
    public void StayHere()
    {
        _save.StayPut = true;
        _save.SleepingByChoice = false;
        if (!IsReading) StartScene(Scenes.Reading, quiet: false);
        _sceneUntil = 0;
        Persist();
    }

    /// <summary>Става от дивана и диванът изчезва.</summary>
    public void GetUp()
    {
        _save.StayPut = false;
        if (_inScene) Say(Lines.Pick(Lines.StandUp, _save.OwnerName));
        LeaveScene();
        Persist();
    }

    /// <summary>Кътче за малко (едно от нещата, които прави, когато е свободен).</summary>
    private void StartScene(Scene scene, double seconds)
    {
        StartScene(scene, quiet: false);
        _sceneUntil = Now + seconds;
    }

    private void StartScene(Scene scene, bool quiet)
    {
        _scene = scene;
        _inScene = true;
        _sceneUntil = 0;
        LoadSceneArt(scene);
        _facingLeft = false;
        SetState(BearState.Couch, scene.Anim);
        ClampX();
        if (!quiet) Say(Lines.Pick(scene.Start, _save.OwnerName));
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
        _inScene = false;
        _scene = null;
        _sceneUntil = 0;
        if (_state is BearState.Couch) SetIdle();
    }

    private void LoadSceneArt(Scene scene)
    {
        _seatProp = scene.Seat == null ? null : _lib.GetProp(scene.Seat, 16, 8, 6);
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

        bool stay = _save.StayPut && scene == Scenes.Reading;
        if (!stay && _sceneUntil > 0 && now > _sceneUntil)
        {
            Say(Lines.Pick(scene.End, _save.OwnerName), 3);
            LeaveScene();
            return;
        }

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

        bool away = NativeMethods.IdleSeconds() > AwayToSleep;
        if (scene == Scenes.Reading)
        {
            // Ако Тут я няма, задрямва с книжката на корема.
            string anim = away ? "read_sleep" : "read";
            if (anim != _anim)
            {
                _anim = anim;
                _animTime = 0;
                if (!away) Say(Lines.Pick(Lines.WelcomeBack, _save.OwnerName));
            }
        }
        else if (away)
        {
            // Тут я няма: прибира кътчето и отива да дремне.
            LeaveScene();
            return;
        }

        if (!away && !IsQuiet && !InFocus && scene.During.Length > 0 && now > _nextChatter)
        {
            _nextChatter = now + (stay ? 180 : 25) + _rng.Next(stay ? 180 : 20);
            Say(Lines.Pick(scene.During, _save.OwnerName), 5);
        }
    }
}
