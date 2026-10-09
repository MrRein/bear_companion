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
            HowAreYou, WhatNow, IdeaOffer, DiceOrCards, Tastiest, FunFact, LuckyDice, Water, Couch, GameProgress,
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
            "Ако плейтестърите спорят за правилата, значи им пука. Добър знак!"), 8)),
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

    private static ChatQuestion GameProgress(PetWindow pet) => new("Как върви играта, по която работиш?",
        Reply("🚀 Добре!", "Ще стане хит! Искам да съм първият плейтестър."),
        new ChatOption("🧱 Заседнала съм", p => p.Ask(new ChatQuestion("Какво те спира?",
            Reply("📜 Правилата", "Махни едно правило и виж какво става. Понякога по-малко е повече."),
            Reply("😐 Не е забавно", "Кой е най-хубавият момент в играта? Направи повече от него."),
            Reply("⚖️ Балансът", "Удвои едно число и виж кое се чупи. После го върни наполовина."),
            Reply("🤯 Всичко", "Тогава почивка, чай и пак. Идеите идват, когато не ги гониш.")))),
        Reply("🙊 Не питай", "Добре, мълча. *шшт*"));
}
