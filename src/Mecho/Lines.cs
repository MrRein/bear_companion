using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Mecho;

/// <summary>
/// Какво казва мечокът. {0} е името на Тут.
///
/// Характерът му: топъл, малко драматичен и много гладен мечок, който мечтае да
/// прави настолни игри. Обожава Тут и рисунките ѝ, има си приятели и врагове в
/// гората (Бобъра, който все обещава дървени пионки; пчелите, които пазят меда;
/// катерицата, която му краде лешниците), рецепти от баба Мецана и прототип на
/// игра, която се казва „Медена война“. Губи от себе си на карти и се гордее с това.
///
/// Репликите не се повтарят: всеки списък е като тесте карти – разбърква се и се
/// изтеглят всички, преди да дойде ред на същата реплика пак.
/// </summary>
public static class Lines
{
    private static readonly Random Rng = new();

    // Тестетата: за всеки списък – кои реплики остават да се кажат.
    private static readonly ConditionalWeakTable<string[], Queue<int>> Decks = new();
    private static readonly ConditionalWeakTable<string[], StrongBox<int>> LastOf = new();

    public static string Pick(string[] lines, string owner)
    {
        if (lines.Length == 0) return "";
        return Fill(PickAny(lines), owner);
    }

    // Списъци, написани направо на мястото (new[] {…} / params): пазим първия
    // екземпляр за всяко съдържание, за да има и те тесте.
    private static readonly Dictionary<string, string[]> Inline = new();

    /// <summary>Слага {0}, {1}, {2}… в репликата (без грешка, ако във файла има странни скоби).</summary>
    public static string Fill(string line, params object[] args)
    {
        for (int i = 0; i < args.Length; i++) line = line.Replace("{" + i + "}", args[i]?.ToString());
        return line;
    }

    /// <summary>Следващата реплика от тестето (без {0}). Работи и за списък, създаден на място.</summary>
    public static string PickAny(params string[] lines)
    {
        if (lines.Length == 0) return "";
        string key = string.Join("\u0001", lines);
        if (!Inline.TryGetValue(key, out var kept)) Inline[key] = kept = lines;
        return kept[Draw(kept)];
    }

    /// <summary>Тегли следващата реплика от тестето (без повторение, докато не мине цялото).</summary>
    private static int Draw(string[] lines)
    {
        if (lines.Length == 1) return 0;
        var deck = Decks.GetValue(lines, _ => new Queue<int>());
        var last = LastOf.GetValue(lines, _ => new StrongBox<int>(-1));
        if (deck.Count == 0)
        {
            var order = Enumerable.Range(0, lines.Length).OrderBy(_ => Rng.Next()).ToList();
            // Новото тесте да не почне с репликата, с която старото свърши.
            if (order[0] == last.Value) (order[0], order[^1]) = (order[^1], order[0]);
            foreach (int i in order) deck.Enqueue(i);
        }
        last.Value = deck.Dequeue();
        return last.Value;
    }

    /// <summary>Приказка от нищото: обикновената или – понякога – за часа, деня и сезона.</summary>
    public static string Chat(string owner)
    {
        var now = DateTime.Now;
        var pools = new List<string[]> { Chatter, Chatter, Chatter, AboutTut, AboutFood, Forest, Thoughts };
        int h = now.Hour;
        if (h is >= 5 and < 11) pools.Add(MorningChat);
        else if (h is >= 14 and < 17) pools.Add(AfternoonChat);
        else if (h is >= 18 and < 22) pools.Add(EveningChat);
        else if (h >= 22 || h < 5) pools.Add(NightChat);
        pools.Add(now.DayOfWeek switch
        {
            DayOfWeek.Monday => MondayChat,
            DayOfWeek.Friday => FridayChat,
            DayOfWeek.Saturday or DayOfWeek.Sunday => WeekendChat,
            _ => MidweekChat,
        });
        pools.Add(now.Month switch
        {
            12 or 1 or 2 => WinterChat,
            3 or 4 or 5 => SpringChat,
            6 or 7 or 8 => SummerChat,
            _ => AutumnChat,
        });
        if (Holiday(now) is { } holiday) { pools.Add(holiday); pools.Add(holiday); }
        return Pick(pools[Rng.Next(pools.Count)], owner);
    }

    private static string[]? Holiday(DateTime d) => (d.Month, d.Day) switch
    {
        (12, >= 20 and <= 26) => Christmas,
        (12, >= 30) or (1, 1) => NewYear,
        (3, 1) => MartaDay,
        (2, 14) => Valentine,
        _ => null,
    };

    // ───────────────────────── Приказки от нищото ─────────────────────────

    public static readonly string[] Chatter = R.Get("Chatter");

    public static readonly string[] AboutTut = R.Get("AboutTut");

    public static readonly string[] AboutFood = R.Get("AboutFood");

    public static readonly string[] Forest = R.Get("Forest");

    public static readonly string[] Thoughts = R.Get("Thoughts");

    public static readonly string[] MorningChat = R.Get("MorningChat");

    public static readonly string[] AfternoonChat = R.Get("AfternoonChat");

    public static readonly string[] EveningChat = R.Get("EveningChat");

    public static readonly string[] NightChat = R.Get("NightChat");

    public static readonly string[] MondayChat = R.Get("MondayChat");

    public static readonly string[] MidweekChat = R.Get("MidweekChat");

    public static readonly string[] FridayChat = R.Get("FridayChat");

    public static readonly string[] WeekendChat = R.Get("WeekendChat");

    public static readonly string[] WinterChat = R.Get("WinterChat");

    public static readonly string[] SpringChat = R.Get("SpringChat");

    public static readonly string[] SummerChat = R.Get("SummerChat");

    public static readonly string[] AutumnChat = R.Get("AutumnChat");

    public static readonly string[] Christmas = R.Get("Christmas");

    public static readonly string[] NewYear = R.Get("NewYear");

    public static readonly string[] MartaDay = R.Get("MartaDay");

    public static readonly string[] Valentine = R.Get("Valentine");

    // ───────────────────────── Разходка и игри ─────────────────────────

    public static readonly string[] SketchDone = R.Get("SketchDone");

    public static readonly string[] Playtest = R.Get("Playtest");

    public static readonly string[] Dancing = R.Get("Dancing");

    public static readonly string[] Snack = R.Get("Snack");

    public static readonly string[] CursorFound = R.Get("CursorFound");

    public static readonly string[] ButterflySeen = R.Get("ButterflySeen");

    public static readonly string[] ButterflyCaught = R.Get("ButterflyCaught");

    public static readonly string[] ButterflyGone = R.Get("ButterflyGone");

    public static readonly string[] PokedAsleep = R.Get("PokedAsleep");

    public static readonly string[] Dragged = R.Get("Dragged");

    public static readonly string[] Fell = R.Get("Fell");

    public static readonly string[] WelcomeBack = R.Get("WelcomeBack");

    public static readonly string[] GoingToSleep = R.Get("GoingToSleep");

    public static readonly string[] WokeUp = R.Get("WokeUp");

    public static readonly string[] SatDown = R.Get("SatDown");

    public static readonly string[] SatOnFloor = R.Get("SatOnFloor");

    public static readonly string[] FlyingCouch = R.Get("FlyingCouch");

    public static readonly string[] LiftedInScene = R.Get("LiftedInScene");

    public static readonly string[] LiftedAtWork = R.Get("LiftedAtWork");

    public static readonly string[] LandedInScene = R.Get("LandedInScene");

    public static readonly string[] CouchLanded = R.Get("CouchLanded");

    public static readonly string[] DoneReading = R.Get("DoneReading");

    public static readonly string[] Reading = R.Get("Reading");

    public static readonly string[] Hungry = R.Get("Hungry");

    public static readonly string[] Sleepy = R.Get("Sleepy");

    public static readonly string[] SleepyDoing = R.Get("SleepyDoing");

    public static readonly string[] DozeStartled = R.Get("DozeStartled");

    public static readonly string[] Petted = R.Get("Petted");

    public static readonly string[] TaskAdded = R.Get("TaskAdded");

    public static readonly string[] SmallTaskDone = R.Get("SmallTaskDone");

    public static readonly string[] TaskDone = R.Get("TaskDone");

    public static readonly string[] BigTaskDone = R.Get("BigTaskDone");

    public static readonly string[] FocusStart = R.Get("FocusStart");

    public static readonly string[] FocusDone = R.Get("FocusDone");

    public static readonly string[] BreakDone = R.Get("BreakDone");

    public static readonly string[] FocusPoked = R.Get("FocusPoked");

    public static readonly string[] NoteAdded = R.Get("NoteAdded");

    public static readonly string[] WantFood = R.Get("WantFood");

    public static readonly string[] Peekaboo = R.Get("Peekaboo");

    public static readonly string[] JuggleStart = R.Get("JuggleStart");

    public static readonly string[] JuggleEnd = R.Get("JuggleEnd");

    public static readonly string[] DrawStart = R.Get("DrawStart");

    public static readonly string[] DrawDone = R.Get("DrawDone");

    public static readonly string[] Climbed = R.Get("Climbed");

    public static readonly string[] WorkTired = R.Get("WorkTired");

    public static readonly string[] Wondering = R.Get("Wondering");

    public static readonly string[] CaughtFood = R.Get("CaughtFood");

    public static readonly string[] RestToo = R.Get("RestToo");

    public static readonly string[] MissedTrampoline = R.Get("MissedTrampoline");

    public static readonly string[] SuperBounce = R.Get("SuperBounce");

    public static string Greeting(string owner)
    {
        int h = DateTime.Now.Hour;
        string key = h switch { < 5 => "greet.night", < 11 => "greet.morning", < 18 => "greet.day", _ => "greet.evening" };
        return R.Pick(key, owner);
    }

    public static string DiceRoll(int n) =>
        R.Any(n switch { 6 => "dice.6", 1 => "dice.1", _ => "dice.other" }).Replace("{число}", n.ToString());

    public static readonly string[] SleepyPoke = R.Get("SleepyPoke");

    public static readonly string[] NoSleepCooking = R.Get("NoSleepCooking");

    public static readonly string[] LunchTime = R.Get("LunchTime");

    public static readonly string[] DinnerTime = R.Get("DinnerTime");

    public static readonly string[] LunchThanks = R.Get("LunchThanks");

    public static readonly string[] DinnerThanks = R.Get("DinnerThanks");

    public static readonly string[] Bedtime = R.Get("Bedtime");

    public static readonly string[] WorkOffer = R.Get("WorkOffer");

    public static readonly string[] SosIntro = R.Get("SosIntro");

    public static readonly string[] SosOpened = R.Get("SosOpened");

    public static readonly string[] SosThanks = R.Get("SosThanks");

    public static readonly string[] GiftBrought = R.Get("GiftBrought");

    public static readonly string[] GiftWaiting = R.Get("GiftWaiting");

    public static readonly string[] GiftOpened = R.Get("GiftOpened");

    public static readonly string[] NewClothes = R.Get("NewClothes");

    public static readonly string[] PutOn = R.Get("PutOn");

    public static readonly string[] PutOnHat = R.Get("PutOnHat");

    public static readonly string[] TookOff = R.Get("TookOff");

    // ───────────────────────── Студ и топло ─────────────────────────

    public static readonly string[] CoolWantsSleeves = R.Get("CoolWantsSleeves");

    public static readonly string[] ColdWantsSweater = R.Get("ColdWantsSweater");

    public static readonly string[] VeryColdWantsMore = R.Get("VeryColdWantsMore");

    public static readonly string[] FrostWantsEverything = R.Get("FrostWantsEverything");

    public static readonly string[] CoolButFine = R.Get("CoolButFine");

    public static readonly string[] WarmInWinter = R.Get("WarmInWinter");

    public static readonly string[] HeaterPlease = R.Get("HeaterPlease");

    public static readonly string[] ColdNoHeater = R.Get("ColdNoHeater");

    public static readonly string[] FreezingNoHeater = R.Get("FreezingNoHeater");

    public static readonly string[] HotHeaterOff = R.Get("HotHeaterOff");

    public static readonly string[] HotTooDressed = R.Get("HotTooDressed");

    public static readonly string[] HotButFine = R.Get("HotButFine");

    public static readonly string[] MildTooDressed = R.Get("MildTooDressed");

    public static readonly string[] MildWeather = R.Get("MildWeather");

    public static readonly string[] HeaterLove = R.Get("HeaterLove");
}
