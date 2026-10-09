using System;
using System.Linq;

namespace Mecho;

/// <summary>
/// Храна или напитка, която мечокът готви. Cost е в лешници (0 = безплатна);
/// Tool е уредът, който трябва да е купен (null = не трябва нищо). Count е на
/// колко парчета се появява из екрана; рисунката е assets/props/food_{Id}.png.
/// </summary>
public sealed record Food(
    string Id, string Name, string Icon, double Fullness, double Energy, int Cost, int Count, string? Tool, string[] Lines);

/// <summary>Нещо за купуване с лешници: уред за кухнята или подобрение.</summary>
/// <summary>Нещо за купуване с лешници. Shelf е разделът в магазина.</summary>
public sealed record Upgrade(string Id, string Name, string Icon, int Price, string Description, string Shelf = Kitchen.KitchenShelf);

/// <summary>
/// Икономиката: лешниците се печелят с работа (задачи, мечо-доро, таймери) и се
/// харчат за храна и за уреди и подобрения, които отключват нови неща.
/// </summary>
public static class Kitchen
{
    public const string Pot = "pot", Pan = "pan", Oven = "oven", CoffeeMachine = "coffee_machine", Kettle = "kettle";
    public const string Bed = "bed", Fridge = "fridge", Cookbook = "cookbook", Headphones = "headphones";

    // Предмети за почивка: всеки отключва кътче (виж Scenes).
    public const string Easel = "easel", Computer = "computer", YogaMat = "yoga_mat", PlantPot = "plant", Radio = "radio", BoardGame = "board_game";
    public const string Trampoline = "trampoline";

    // Раздели в магазина
    public const string KitchenShelf = "Кухня", FunShelf = "За почивка", HelpShelf = "Подобрения";

    /// <summary>Безплатните боровинки се берат най-много веднъж на толкова минути.</summary>
    public const int BerryMinutes = 30;

    public static readonly Food[] Foods =
    {
        new("berries", "Боровинки", "🫐", 12, 0, 0, 3, null,
            new[] { "Боровинки! Сини като небето.", "Ням! Още една.", "Кисело-сладко. Обичам." }),
        new("popcorn", "Пуканки", "🍿", 24, 0, 2, 5, Pot,
            new[] { "Пук! Хванах я с уста!", "Хрус-хрус.", "Пуканки като за кино вечер!" }),
        new("meatballs", "Кюфтенца", "🍖", 40, 0, 4, 3, Pan,
            new[] { "Ох, горещо! *духа* Мммм.", "Кюфтенце! Най-хубавият ден!", "Цвър-цвър… ням." }),
        new("potatoes", "Мечешки картофки", "🥔", 50, 0, 5, 1, Oven,
            new[] { "Мечешки картофки от фурната! Ухааа.", "Хрупкави отвън, меки отвътре. Перфектни.", "Ще ги направя пак утре. И вдругиден." }),
        new("coffee", "Кафе", "☕", 3, 35, 2, 1, CoffeeMachine,
            new[] { "Кафе! Очите ми се отвориха!", "Ааах. Сега мога да прототипирам цял ден.", "Бззз! Енергия!" }),
        new("tea", "Чай с мед", "🍵", 6, 12, 1, 1, Kettle,
            new[] { "Чай с мед. Като у дома.", "Топличко… мммм.", "Мед в чая е най-хубавото изобретение." }),
    };

    public static readonly Upgrade[] Upgrades =
    {
        new(Pot, "Тенджера", "🍲", 10, "Мечокът може да пука пуканки."),
        new(Kettle, "Чайник", "🫖", 15, "Чай с мед и кътче за чай."),
        new(Pan, "Тиган", "🍳", 25, "Мечокът може да пържи кюфтенца."),
        new(CoffeeMachine, "Кафе машина", "☕", 30, "Кафе: вдига енергията много (най-много 2 на половин час)."),
        new(Oven, "Фурна", "🔥", 45, "Мечокът може да пече мечешки картофки."),

        new(PlantPot, "Саксия с цвете", "🌱", 10, "Мечокът полива цветето.", FunShelf),
        new(YogaMat, "Постелка за йога", "🧘", 15, "Мечокът прави йога.", FunShelf),
        new(Radio, "Радио", "📻", 20, "Мечокът слуша музика.", FunShelf),
        new(BoardGame, "Настолна игра", "🎲", 25, "Мечокът плейтества на масичка.", FunShelf),
        new(Easel, "Статив", "🎨", 30, "Мечокът рисува като Тут.", FunShelf),
        new(Trampoline, "Батут", "🤸", 35, "Мечокът скача, а разпиляната храна събира сам във въздуха!", FunShelf),
        new(Computer, "Компютър", "🎮", 50, "Мечокът играе игрички.", FunShelf),

        new(Headphones, "Слушалки", "🎧", 35, "+1 🌰 за всяко завършено мечо-доро.", HelpShelf),
        new(Bed, "Меко легло", "🛏️", 40, "Мечокът се наспива два пъти по-бързо.", HelpShelf),
        new(Fridge, "Хладилник", "🧊", 50, "Всяко ястие струва с 1 🌰 по-малко.", HelpShelf),
        new(Cookbook, "Готварска книга", "📖", 60, "Всяко ястие засища с 25% повече.", HelpShelf),
    };

    public static Upgrade? Find(string id) => Upgrades.FirstOrDefault(u => u.Id == id);

    public static bool Owns(SaveData save, string id) => save.Owned.Contains(id);

    public static bool CanCook(Food food, SaveData save) => food.Tool == null || Owns(save, food.Tool);

    public static int CostOf(Food food, SaveData save) =>
        food.Cost == 0 ? 0 : Math.Max(1, food.Cost - (Owns(save, Fridge) ? 1 : 0));

    public static double FullnessOf(Food food, SaveData save) => food.Fullness * (Owns(save, Cookbook) ? 1.25 : 1);

    /// <summary>След колко минути пак може да набере боровинки (0 = сега).</summary>
    public static int BerriesIn(SaveData save) =>
        Math.Max(0, (int)Math.Ceiling((save.BerriesReadyAt - DateTime.Now).TotalMinutes));
}
