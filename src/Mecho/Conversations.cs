using System;
using System.Collections.Generic;
using System.Linq;

namespace Mecho;

/// <summary>
/// За какво си говори мечокът, когато го цъкнеш. Първо проверява дали му трябва
/// нещо (храна, сън), иначе избира случайна тема (не същата като миналия път).
/// </summary>
public static class Conversations
{
    private static readonly Random Rng = new();
    private static int _lastTopic = -1;

    private static ChatOption Reply(string label, string answer) => new(label, p => p.Say(answer, 5));

    private static string Pick(params string[] lines) => lines[Rng.Next(lines.Length)];

    public static ChatQuestion Next(PetWindow pet)
    {
        var s = pet.Save;
        string name = s.OwnerName;

        if (pet.IsAsleep)
        {
            return new ChatQuestion("*Мечокът спи сладко и хърка тихичко.*",
                new ChatOption("☀️ Събуди го", p => p.WakeUp()),
                new ChatOption("🤫 Остави го да спи", _ => { }));
        }
        if (s.Fullness < 25) return Hungry(pet);
        if (s.Energy < 20)
        {
            return new ChatQuestion("*прозява се* Доспа ми се. Може ли да дремна?",
                new ChatOption("🌙 Да, лека нощ", p => p.GoToSleep()),
                Reply("Още малко, моля", "Добре… *клепачите му падат*"));
        }
        if (pet.IsReading && Rng.Next(2) == 0)
        {
            return new ChatQuestion("Чета книжка за настолни игри. Искаш ли да стана?",
                new ChatOption("🚶 Да, стани", p => p.GetUp()),
                Reply("📖 Чети си", "Мерси! Стигнах до най-интересното."));
        }

        var topics = new List<Func<PetWindow, ChatQuestion>>
        {
            HowAreYou, WhatNow, WhatDrawing, SketchOffer, ArtProgress, Wrist, DrawMe, Colour,
            IdeaOffer, DiceOrCards, Tastiest, FunFact, LuckyDice, Water, Couch,
        };
        int i;
        do i = Rng.Next(topics.Count);
        while (i == _lastTopic);
        _lastTopic = i;
        return topics[i](pet);
    }

    /// <summary>Докато тече мед-доро и я питаш втори път: да спрем ли?</summary>
    public static ChatQuestion StopFocus(PetWindow pet)
    {
        int left = (int)Math.Ceiling(pet.PomodoroLeft.TotalMinutes);
        return new ChatQuestion($"Остават {left} мин. от мед-дорото. Искаш ли да спрем?",
            new ChatOption("■ Да, спри го", p => p.StopPomodoro()),
            Reply("🍯 Не, продължаваме", Pick("Браво! Още малко и почивка.", "Така те искам! Аз пиша правила.", "Супер. Мълча като мишка.")));
    }

    private static ChatQuestion Hungry(PetWindow pet)
    {
        var options = Kitchen.Foods
            .Where(f => Kitchen.IsUnlocked(f, pet.Save))
            .Select(f => new ChatOption($"{f.Icon} {f.Name}", p => p.Feed(f)))
            .ToList();
        options.Add(Reply("По-късно", "Добре… *къркор*"));
        return new ChatQuestion("Гладен съм… ще ме нахраниш ли?", options.ToArray());
    }

    private static ChatQuestion HowAreYou(PetWindow pet) => new($"Как си днес, {pet.Save.OwnerName}?",
        new ChatOption("😊 Супер!", p => p.Cheer("Ура! И аз съм супер, щом ти си супер.")),
        new ChatOption("😴 Уморена", p => p.Ask(new ChatQuestion("Да направим ли малка почивка? Ще пазя бюрото.",
            Reply("☕ Да, 5 минути", "Стани, раздвижи се, пийни вода. Аз ще пазя."),
            Reply("Не, ще продължа", "Добре. Но после чай!")))),
        new ChatOption("😐 Така-така", p => p.Cheer("Ела да те гушна. *мечешка прегръдка*")),
        Reply("😤 Ядосана", "Грр! Кой те ядоса? Ще му изям лешниците!"));

    private static ChatQuestion WhatNow(PetWindow pet) => new("Какво ще правим сега?",
        new ChatOption("📋 Ще запиша задача", p => p.OpenMenu(MenuWindow.TasksTab)),
        new ChatOption("📝 Ще си запиша бележка", p => p.OpenMenu(MenuWindow.NotesTab)),
        new ChatOption("🍯 Пусни мед-доро", p => p.StartFocus()),
        Reply("☕ Почивам си", "Добра идея. И аз ще си почина."));

    private static ChatQuestion IdeaOffer(PetWindow pet) => new("Искаш ли идея за игра?",
        new ChatOption("💡 Да!", p => p.Ask(Idea())),
        Reply("Не сега", "Добре, пазя ги в тефтера."));

    /// <summary>Генератор на идеи: механика + тема + ограничение.</summary>
    public static ChatQuestion Idea()
    {
        string[] mechanics =
        {
            "зарове", "карти", "плочки", "поставяне на работници", "търг", "блъф",
            "събиране на комплекти", "строене на тесте", "рисуване", "пясъчен часовник",
        };
        string[] themes =
        {
            "пчели", "пирати", "зимен сън", "кухня", "космос", "горски пощальони",
            "изгубени чорапи", "дракони-готвачи", "библиотека", "пикник",
        };
        string[] limits =
        {
            "само един зар", "най-много 10 карти", "само за двама", "трае 5 минути", "без нито една дума",
            "всички печелят или губят заедно", "всеки ход е таен", "играе се на една салфетка",
        };
        string idea = $"Игра с {mechanics[Rng.Next(mechanics.Length)]} за {themes[Rng.Next(themes.Length)]}. " +
                      $"Ограничение: {limits[Rng.Next(limits.Length)]}.";
        return new ChatQuestion(idea,
            new ChatOption("🎲 Още една!", p => p.Ask(Idea())),
            new ChatOption("📝 Запиши я в бележките", p =>
            {
                p.AddNote("💡 " + idea, quiet: true);
                p.Cheer("Записах я в бележките!");
            }),
            Reply("Стига толкова", "Добре! Отивам да я прототипирам."));
    }

    private static ChatQuestion DiceOrCards(PetWindow pet) => new("Кое е по-хубаво: зарове или карти?",
        Reply("🎲 Зарове", "Шумни и щастливи! Съгласен."),
        Reply("🃏 Карти", "Шумолят като листа. Обожавам ги."),
        Reply("🤷 И двете", "Дипломатично! Като истински дизайнер."));

    private static ChatQuestion Tastiest(PetWindow pet)
    {
        var options = Kitchen.Foods
            .Where(f => Kitchen.IsUnlocked(f, pet.Save))
            .Select(f => Reply($"{f.Icon} {f.Name}", $"Знаех си! {f.Name} са най-вкусни!"))
            .ToList();
        options.Add(Reply("😋 Всичко!", "Ето затова те обичам."));
        return new ChatQuestion("Кое е най-вкусното на света?", options.ToArray());
    }

    private static ChatQuestion FunFact(PetWindow pet) => new("Искаш ли да чуеш нещо интересно?",
        new ChatOption("🤓 Да!", p => p.Say(Pick(
            "Най-старите намерени зарове са на около 5000 години.",
            "Офицерът в шаха някога е бил… слон. Наистина!",
            "„Монополи“ започва като игра, която да покаже колко е лошо да имаш монопол.",
            "Тетрис е измислен в Москва през 1984 г.",
            "Пуканките пукат, защото водата в зърното става на пара.",
            "Лешникът е истинска ядка. А фъстъкът не е: той е бобово растение!",
            "Мечките подушват храна от километри. Затова знам кога правиш кюфтенца.",
            "Ако плейтестърите спорят за правилата, значи им пука. Добър знак!",
            "Аниматорите на Дисни описват 12 принципа на анимацията. Първият е разтягане и свиване!",
            "Анимация „на двойки“ значи нова рисунка на всеки 2 кадъра: 12 рисунки в секунда.",
            "„Снежанка“ (1937) е първият пълнометражен рисуван филм на Дисни.",
            "Фенакистископът от 1830-те е въртящ се диск, който прави рисунките да мърдат.",
            "Най-добрият начин да нарисуваш мечка е да гледаш мечка. Аз съм на разположение."), 8)),
        Reply("Не сега", "Добре, пазя го за после."));

    private static ChatQuestion LuckyDice(PetWindow pet) => new("Да хвърлим ли зар за късмет?",
        new ChatOption("🎲 Да!", p => p.RollDice()),
        Reply("Не", "Добре, пазя късмета за после."));

    private static ChatQuestion Water(PetWindow pet) => new($"Пила ли си вода скоро, {pet.Save.OwnerName}?",
        new ChatOption("💧 Да", p => p.Cheer("Браво! Мечешко одобрение.")),
        Reply("😅 Ой, не", "Хайде, една чаша! Аз ще чакам тук."));

    private static ChatQuestion Couch(PetWindow pet) => pet.StaysPut
        ? new ChatQuestion("Тук ми е много удобно. Да остана ли на дивана?",
            Reply("🛋️ Остани", "Ура! Още една глава."),
            new ChatOption("🚶 Разходи се", p => p.GetUp()))
        : new ChatQuestion("Искаш ли да седна да почета малко?",
            new ChatOption("🛋️ Да", p => p.StayHere()),
            Reply("🚶 Не, разхождай се", "Йей, разходка!"));

    // ───────────────────────── Рисуване и анимация (Тут е илюстратор и аниматор) ─────────────────────────

    private static ChatQuestion WhatDrawing(PetWindow pet) => new($"Какво рисуваш днес, {pet.Save.OwnerName}?",
        Reply("✏️ Скици", "Обичам скиците! Най-живите линии са в тях."),
        Reply("🎨 Илюстрация", "Ууу! Ще ми покажеш ли, като е готова?"),
        new ChatOption("🎞️ Анимация", p => p.Ask(new ChatQuestion("Колко кадъра ти остават?",
            Reply("Малко", "Почти си готова! Аз ще държа палци. Е, лапи."),
            Reply("Много", "Кадър по кадър. Аз ще ти нося кюфтенца."),
            Reply("Не ги броя", "Мъдро. Броенето плаши кадрите.")))),
        Reply("😶 Още нищо", "Празният лист е най-страшен. Нарисувай една линия и тя ще ти каже накъде."));

    private static ChatQuestion SketchOffer(PetWindow pet) => new("Искаш ли тема за бърза скица?",
        new ChatOption("✏️ Да!", p => p.Ask(SketchPrompt())),
        Reply("Не сега", "Добре. Пазя ги за после."));

    /// <summary>Тема за скица: герой + действие + особеност.</summary>
    public static ChatQuestion SketchPrompt()
    {
        string[] who =
        {
            "Мечок", "Заек-пощальон", "Лисица-готвачка", "Сънлив бухал", "Таралеж-рокер", "Бобър-архитект",
            "Пчела-детектив", "Котка-астронавт", "Жаба-балерина", "Охлюв-състезател",
        };
        string[] doing =
        {
            "кара тротинетка", "пече мечешки картофки", "се крие от дъжда", "чете под одеялото", "танцува на маса",
            "лови пуканки с уста", "носи огромен пакет", "заспива на работа", "се оглежда в локва", "хвърля зар",
        };
        string[] twist =
        {
            "само с три цвята", "в 5 минути", "с една непрекъсната линия", "отгоре, като птица", "в стил стара гравюра",
            "като 4 кадъра анимация", "силует, без детайли", "с огромни емоции", "отдолу, като мравка", "в квадратче 3×3 см",
        };
        string prompt = $"{who[Rng.Next(who.Length)]} {doing[Rng.Next(doing.Length)]}. {Capital(twist[Rng.Next(twist.Length)])}.";
        return new ChatQuestion(prompt,
            new ChatOption("🎲 Друга тема", p => p.Ask(SketchPrompt())),
            new ChatOption("📝 Запиши я", p =>
            {
                p.AddNote("✏️ " + prompt, quiet: true);
                p.Cheer("Записах я в бележките!");
            }),
            new ChatOption("🍯 Пусни 25 минути", p => p.StartFocus()),
            Reply("Стига толкова", "Нарисувай я и ми я покажи!"));
    }

    private static string Capital(string s) => s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..];

    private static ChatQuestion ArtProgress(PetWindow pet) => new("Как върви проектът ти?",
        Reply("🚀 Добре!", "Знаех си! Ти си най-добрата художничка, която познавам."),
        new ChatOption("🧱 Заседнала съм", p => p.Ask(new ChatQuestion("Какво те спира?",
            Reply("🤷 Не знам какво да рисувам", "Нарисувай чашата си. После я нарисувай като чудовище."),
            Reply("😒 Не ми харесва", "Обърни рисунката огледално. Свежият поглед вижда всичко."),
            Reply("🎞️ Движението е дървено", "Пробвай с повече разтягане и свиване. И изпревари движението с малко обратно."),
            new ChatOption("⏳ Нямам време", p2 => p2.Ask(new ChatQuestion("Едно мед-доро? Само 25 минути, аз ще пазя.",
                new ChatOption("🍯 Хайде", p3 => p3.StartFocus()),
                Reply("Не сега", "Добре. Тук съм, когато решиш."))))))),
        Reply("🙊 Не питай", "Добре, мълча. *шшт*"));

    private static ChatQuestion Wrist(PetWindow pet) => new("Как е ръката ти? Много рисуваш днес.",
        Reply("👍 Добре е", "Супер! Все пак, завърти китките веднъж. За мен."),
        Reply("😣 Боли малко", "Почивка! Завърти китките 10 пъти, разпери пръсти, погледни надалеч. Аз броя: 1, 2, 3…"),
        Reply("👀 Очите ме болят", "20-20-20: на всеки 20 минути гледай 20 секунди нещо далеч. Аз ще гледам с теб."));

    private static ChatQuestion DrawMe(PetWindow pet) => new($"{pet.Save.OwnerName}… ще ме нарисуваш ли някой ден?",
        new ChatOption("🎨 Да!", p => p.Cheer("Ура! Моля те, с шапка. И с кюфтенце.")),
        Reply("😉 Вече те рисувам", "Знаех си! Затова съм толкова красив."),
        Reply("🤔 Може би", "Ще чакам. Мечките са търпеливи. Почти."));

    private static ChatQuestion Colour(PetWindow pet) => new("Кой е любимият ти цвят тази седмица?",
        Reply("🔴 Червен", "Като диванчето ми!"),
        Reply("🟡 Жълт", "Като мед! Отличен вкус."),
        Reply("🔵 Син", "Като небето, когато пеперудите летят."),
        Reply("🟢 Зелен", "Като гората у дома."),
        Reply("🎨 Всички", "Художничка! Не можеш да избереш едно дете."));
}
