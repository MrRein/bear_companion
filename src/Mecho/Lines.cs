using System;

namespace Mecho;

/// <summary>Какво казва мечокът. {0} е името на Тут.</summary>
public static class Lines
{
    private static readonly Random Rng = new();

    public static string Pick(string[] lines, string owner) => string.Format(lines[Rng.Next(lines.Length)], owner);

    public static readonly string[] Chatter =
    {
        "Мисля си за игра с пчели и търг…",
        "Колко страни трябва да има един зар? Шест? Двайсет? Сто?",
        "Тази пионка ме гледа странно.",
        "Всяка добра игра започва с купа пуканки.",
        "Ако правилата са повече от една страница, нещо не е наред.",
        "Нарисувах карта… после я изядох. Беше от вафла.",
        "Кооперативна игра срещу пчелите. Помисли само!",
        "Хвърлих зар наум. Шестица!",
        "Лешниците стават страхотни пионки.",
        "{0}, ти си най-добрият плейтестър.",
        "Бобърът обеща дървени пионки. Чакам.",
        "Обичам миризмата на нова кутия игра.",
        "Днес е хубав ден за прототип.",
        "Да теглиш карта на всеки ход… кой го е измислил? Гений.",
        "Мисля, че ми трябва още една карта. Или кюфтенце.",
        "Мечешки картофки + настолна игра = перфектна вечер.",
    };

    public static readonly string[] Poked =
    {
        "Хи-хи!", "Гъделичка!", "Здрасти, {0}!", "Мечешка прегръдка!", "Пак ли ме цъкна? Хареса ми.", "Ура!",
    };

    public static readonly string[] TooManyPokes = { "Стига ме цъкаш!", "Ей, не съм бутон!", "Ще ти взема зара!" };

    public static readonly string[] PokedAsleep = { "Ззз… още пет минутки…", "Мм… сънувам зарове…", "Ззз… лешници…" };

    public static readonly string[] WokenByDrag = { "Ей! Спях!", "Къде ме носиш насън?!" };

    public static readonly string[] Dragged = { "Уиии!", "Леко, леко!", "Летя!" };

    public static readonly string[] Fell = { "Ох, дупето ми!", "Пак ли?! Хи-хи.", "Приземих се като пионка!" };

    public static readonly string[] WelcomeBack = { "Добре дошла, {0}!", "О, върна се! Липсваше ми.", "Ззз… а? {0}! Тук съм!" };

    public static readonly string[] GoingToSleep = { "Ще дремна малко…", "Лека нощ, {0}.", "*прозява се* Ззз…" };

    public static readonly string[] WokeUp = { "Добро утро! Колко спах?", "*протяга се* Готов съм!", "Сънувах нова игра!" };

    public static string Greeting(string owner)
    {
        int h = DateTime.Now.Hour;
        if (h < 5) return $"Още ли не спиш, {owner}?";
        if (h < 11) return $"Добро утро, {owner}!";
        if (h < 18) return $"Добър ден, {owner}!";
        return $"Добър вечер, {owner}!";
    }

    public static string DiceRoll(int n) => n switch
    {
        6 => "Шестица! Ура!",
        1 => "Единица… пак.",
        _ => $"Хвърлих {n}!",
    };
}
