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

    private static string Pick(params string[] lines) => Lines.PickAny(lines);

    /// <summary>„Няма ли да правим нещо?“: мечо-доро, таймер, задача, бележка.</summary>
    public static ChatQuestion WorkOffer(PetWindow pet) => new($"Какво ще правим, {pet.Save.OwnerName}?",
        new ChatOption($"Мечо-доро ({pet.PomodoroMode.Focus} мин)", p => p.StartFocus()),
        new ChatOption("Таймер", p => p.OpenMenu(MenuWindow.PomodoroTab)),
        new ChatOption("Ще запиша задача", p => p.OpenMenu(MenuWindow.TasksTab)),
        new ChatOption("Ще запиша бележка", p => p.OpenMenu(MenuWindow.NotesTab)),
        new ChatOption("Не сега", p => p.Cheer("Добре! Тук съм, ако потрябвам.")));

    public static ChatQuestion Next(PetWindow pet)
    {
        var s = pet.Save;
        string name = s.OwnerName;

        if (pet.IsAsleep)
        {
            return new ChatQuestion(Lines.Pick(Lines.PokedAsleep, name),
                new ChatOption("☀️ Събуди го", p => p.WakeUp()),
                new ChatOption("🤫 Остави го да спи", _ => { }));
        }
        if (s.Fullness < 25) return Hungry(pet);
        if (s.Energy < PetWindow.TiredBelow)
        {
            var options = new List<ChatOption> { new("🌙 Да, лека нощ", p => p.GoToSleep()) };
            // Кафе и чай, ако има с какво да ги направи.
            options.AddRange(Kitchen.Foods
                .Where(f => f.Energy > 0 && Kitchen.CanCook(f, s))
                .Select(f => new ChatOption($"{f.Icon} {f.Name} ({Kitchen.CostOf(f, s)} 🌰)", p => p.Cook(f))));
            options.Add(Reply("Още малко, моля", "Добре… *клепачите му падат*"));
            return new ChatQuestion(R.Any("conv.tired"), options.ToArray());
        }
        if (pet.IsReading && Rng.Next(2) == 0)
        {
            return new ChatQuestion(R.Any("conv.reading"),
                new ChatOption("🚶 Да, стани", p => p.GetUp()),
                Reply("📖 Чети си", R.Any("conv.reading.keep")));
        }

        var topics = Topics.Select(t => t.Ask).ToList();
        int i;
        do i = Rng.Next(topics.Count);
        while (i == _lastTopic);
        _lastTopic = i;
        return topics[i](pet);
    }

    /// <summary>Темите за разговор (и за Developer панела).</summary>
    internal static readonly (string Name, Func<PetWindow, ChatQuestion> Ask)[] Topics =
    {
        ("Как си", HowAreYou), ("Какво ще правим", WhatNow), ("Какво рисуваш", WhatDrawing), ("Тема за скица", SketchOffer),
        ("Как върви проектът", ArtProgress), ("Ръката", Wrist), ("Ще ме нарисуваш ли", DrawMe), ("Любим цвят", Colour),
        ("Идея за игра", IdeaOffer), ("Зарове или карти", DiceOrCards), ("Най-вкусното", Tastiest), ("Интересен факт", FunFact),
        ("Зар за късмет", LuckyDice), ("Вода", Water), ("Да почета ли", Couch), ("Сън", Dream), ("Име за игра", GameName),
        ("Виц", Joke), ("Комплимент", Compliment), ("Кое би избрала", WouldYouRather), ("Новини от гората", ForestNews),
        ("Чай или кафе", TeaOrCoffee), ("Протягане", Stretch), ("Най-хубавото днес", BestMoment), ("Мъдрост", MechoWisdom),
        ("Прегръдка", Hug), ("Музика", Music), ("Тайна", Secret),
    };

    /// <summary>Обяд: „Ти няма ли да обядваш?“</summary>
    public static ChatQuestion Lunch() => new("Ти няма ли да обядваш?",
        Reply("🍽️ Да, сега ще ходя", "Хайде, трябва да се хапчи!"),
        Reply("✅ Вече обядвах", "Ааа, добре, аз се притесних."),
        Reply("🙅 Не, днес няма", "Неее, трябва да се храниш. Хапни, моля те."));

    /// <summary>Цъкнаха го, докато прави нещо: да спре ли.</summary>
    public static ChatQuestion StopAction(PetWindow pet, string what) => new(R.Any("conv.stop.ask").Replace("{какво}", what).Replace("{Какво}", Capital(what)),
        new ChatOption("■ Да, спри", p => { p.StopAction(); p.Say(R.Any("conv.stop.yes"), 3); }),
        Reply("Не, продължавай", R.Any("conv.stop.no")));

    public static ChatQuestion StopFocus(PetWindow pet)
    {
        bool pomodoro = pet.PomodoroFocus;
        int left = (int)Math.Ceiling((pomodoro ? pet.PomodoroLeft : pet.TimerLeft).TotalMinutes);
        return new ChatQuestion(pomodoro ? $"Остават {left} мин. от мечо-дорото. Искаш ли да спрем?" : $"Остават {left} мин. от работния таймер. Да го спрем ли?",
            new ChatOption("■ Да, спри го", p => { if (pomodoro) p.StopPomodoro(); else p.StopTimer(); }),
            Reply("🍯 Не, продължаваме", R.Any("conv.stopfocus.no")));
    }

    private static ChatQuestion Hungry(PetWindow pet)
    {
        var s = pet.Save;
        var options = Kitchen.Foods
            .Where(f => f.Fullness >= 10 && Kitchen.CanCook(f, s))
            .Select(f => new ChatOption(
                Kitchen.CostOf(f, s) == 0 ? $"{f.Icon} {f.Name} (безплатни)" : $"{f.Icon} {f.Name} ({Kitchen.CostOf(f, s)} 🌰)",
                p => p.Cook(f)))
            .ToList();
        options.Add(new ChatOption("🍳 Отвори кухнята", p => p.OpenMenu(MenuWindow.FoodTab)));
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
        new ChatOption("🍯 Пусни мечо-доро", p => p.StartFocus()),
        new ChatOption("☕ Почивам си", p => p.RestToo()));

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
            .Where(f => f.Fullness >= 10)
            .Take(4)
            .Select(f => Reply($"{f.Icon} {f.Name}", $"Знаех си! {f.Name} са най-вкусни!"))
            .ToList();
        options.Add(Reply("😋 Всичко!", "Ето затова те обичам."));
        return new ChatQuestion("Кое е най-вкусното на света?", options.ToArray());
    }

    private static ChatQuestion FunFact(PetWindow pet) => new("Искаш ли да чуеш нещо интересно?",
        new ChatOption("🤓 Да!", p => p.Say(R.Any("conv.funfact"), 8)),
        Reply("Не сега", "Добре, пазя го за после."));

    private static ChatQuestion LuckyDice(PetWindow pet) => new("Да хвърлим ли зар за късмет?",
        new ChatOption("🎲 Да!", p => p.RollDice()),
        Reply("Не", "Добре, пазя късмета за после."));

    private static ChatQuestion Water(PetWindow pet) => new($"Пила ли си вода скоро, {pet.Save.OwnerName}?",
        new ChatOption("💧 Да", p => p.Cheer("Браво! Мечешко одобрение.")),
        Reply("😅 Ой, не", "Хайде, една чаша! Аз ще чакам тук."));

    private static ChatQuestion Couch(PetWindow pet) => pet.StaysPut
        ? new ChatQuestion("Тук ми е много удобно. Да остана ли да чета?",
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
            new ChatOption("🍯 Пусни мечо-доро", p => p.StartFocus()),
            Reply("Стига толкова", "Нарисувай я и ми я покажи!"));
    }

    private static string Capital(string s) => s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..];

    private static ChatQuestion ArtProgress(PetWindow pet) => new("Как върви проектът ти?",
        Reply("🚀 Добре!", "Знаех си! Ти си най-добрата художничка, която познавам."),
        new ChatOption("🧱 Заседнала съм", p => p.Ask(new ChatQuestion("Какво те спира?",
            Reply("🤷 Не знам какво да рисувам", "Нарисувай чашата си. После я нарисувай като чудовище."),
            Reply("😒 Не ми харесва", "Обърни рисунката огледално. Свежият поглед вижда всичко."),
            Reply("🎞️ Движението е дървено", "Пробвай с повече разтягане и свиване. И изпревари движението с малко обратно."),
            new ChatOption("⏳ Нямам време", p2 => p2.Ask(new ChatQuestion($"Едно мечо-доро? Само {pet.PomodoroMode.Focus} минути, аз ще пазя.",
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

    // ───────────────────────── Още теми (характер) ─────────────────────────

    private static ChatQuestion Dream(PetWindow pet)
    {
        string dream = R.Any("conv.dream");
        return new ChatQuestion("Знаеш ли какво сънувах? " + dream,
            Reply("😂 Хаха!", R.Any("conv.dream.haha")),
            Reply("🌙 А аз сънувах…", R.Any("conv.dream.mine")),
            Reply("🤔 Странно", "Мечешките сънища са странни. Затова са хубави."));
    }

    private static ChatQuestion GameName(PetWindow pet)
    {
        string[] names = { "„Медена война“", "„Лешникова треска“", "„Пионки в гората“", "„Зар и мечок“", "„Кюфтенце рали“", "„Тихо, мечки!“", "„Пуканков купон“", "„Бърлога 3000“" };
        var picks = names.OrderBy(_ => Rng.Next()).Take(3).ToArray();
        return new ChatQuestion("Помогни ми да кръстя новата си игра! Кое име е най-хубаво?",
            Reply(picks[0], R.Any("conv.gamename.1")),
            Reply(picks[1], R.Any("conv.gamename.2")),
            Reply(picks[2], R.Any("conv.gamename.3")),
            Reply("✏️ Измисли друго", "Хм… „Мечо и Тут спасяват деня“? Твърде дълго? Ще мисля."));
    }

    private static ChatQuestion Joke(PetWindow pet) => new("Искаш ли да ти кажа виц?",
        new ChatOption("😄 Давай!", p => p.Say(R.Any("conv.joke"), 8)),
        Reply("Не сега", "Добре. Вицът ще отлежи и ще стане по-смешен."));

    private static ChatQuestion Compliment(PetWindow pet) => new($"Може ли да ти кажа нещо, {pet.Save.OwnerName}?",
        new ChatOption("💛 Кажи", p => p.Say(R.Any("conv.compliment"), 7)),
        Reply("😳 Не, срамувам се", "Хи-хи, добре. Но знай, че е нещо хубаво."));

    private static ChatQuestion WouldYouRather(PetWindow pet)
    {
        (string a, string b, string ra, string rb)[] pairs =
        {
            ("🍯 Безкраен мед", "🍿 Безкрайни пуканки", "Сладко решение! Като мен.", "Шумно и щастливо! Като мен."),
            ("🐝 Да говориш с пчели", "🦫 Да строиш като Бобъра", "Ще им измъкнем рецептата за мед!", "Ще ми направиш пионки? Ура!"),
            ("🌙 Вечна нощ със звезди", "☀️ Вечен летен ден", "Романтично. И сънливо.", "Пикник завинаги!"),
            ("✏️ Да рисуваш само с молив", "🎨 Само с един цвят", "Класика! Всичко е в линиите.", "Смело! Ще стане стилно."),
            ("🎲 Игра само със зарове", "🃏 Игра само с карти", "Шумна игра!", "Шумоляща игра!"),
            ("🐻 Да си мечка за ден", "🧑‍🎨 Аз да съм човек за ден", "Ще спиш много и ще ядеш мед. Хубав ден!", "Ще рисувам! И ще нося обувки на два крака."),
        };
        var q = pairs[Rng.Next(pairs.Length)];
        return new ChatQuestion("Кое би избрала?",
            Reply(q.a, q.ra),
            Reply(q.b, q.rb),
            Reply("🤷 И двете!", "Алчна! Харесва ми."));
    }

    private static ChatQuestion ForestNews(PetWindow pet) => new("Искаш ли новини от гората?",
        new ChatOption("🌲 Да!", p => p.Say(R.Any("conv.forest"), 7)),
        Reply("Не сега", "Добре, ще ги пазя. Гората не бърза."));

    private static ChatQuestion TeaOrCoffee(PetWindow pet) => new("Чай или кафе днес?",
        Reply("☕ Кафе", R.Any("conv.coffee")),
        Reply("🍵 Чай", R.Any("conv.tea")),
        Reply("🥤 Сок", "Свежо! Аз съм за боровинков."),
        Reply("💧 Вода", "Най-мъдрият избор. Мечешко одобрение!"));

    private static ChatQuestion Stretch(PetWindow pet) => new("Хайде да се протегнем заедно!",
        Reply("🙆 Хайде", R.Any("conv.stretch")),
        Reply("⏳ После", "Добре, но аз ще ти напомня. Мечешко обещание."));

    private static ChatQuestion BestMoment(PetWindow pet) => new(DateTime.Now.Hour >= 17
            ? $"Кое беше най-хубавото днес, {pet.Save.OwnerName}?"
            : $"Какво хубаво те чака днес, {pet.Save.OwnerName}?",
        Reply("🎨 Нещо, което нарисувах", "Знаех си! Рисуването прави деня хубав."),
        Reply("🍽️ Нещо вкусно", "Значи сме от един отбор!"),
        Reply("🐻 Ти!", "*червенее* Аз?! Ох. Ти също си моето най-хубаво."),
        Reply("🤷 Още не знам", "Ще го намерим заедно. Може да е нещо малко."));

    private static ChatQuestion MechoWisdom(PetWindow pet) => new("Искаш ли мечешка мъдрост?",
        new ChatOption("🦉 Да", p => p.Say(R.Any("conv.wisdom"), 7)),
        Reply("Не сега", "Мъдростта ще почака. Тя е търпелива."));

    private static ChatQuestion Hug(PetWindow pet) => new("Искаш ли мечешка прегръдка?",
        new ChatOption("🤗 Да!", p => { p.Pet(); }),
        Reply("Сега не", "Добре. Пазя я за после. Тя не изтича."));

    private static ChatQuestion Music(PetWindow pet) => new("Какво слушаш, докато рисуваш?",
        Reply("🎵 Музика", "Танцувам вътрешно. И малко външно."),
        Reply("🎧 Подкаст", "Ууу, интересно! После ми разкажи."),
        Reply("🤫 Тишина", "Тишината е хубава. Аз ще съм тих. Почти."),
        Reply("📺 Сериал на фона", "Класика. Аз също гледам с едно око."));

    private static ChatQuestion Secret(PetWindow pet) => new("Мога ли да ти кажа една тайна?",
        new ChatOption("🤫 Кажи", p => p.Say(R.Any("conv.secret"), 7)),
        Reply("Не, пази си я", "Добре. Тя ще стане още по-тайна."));
}
