using System;
using System.Linq;

namespace Mecho;

/// <summary>
/// Храна или напитка, която мечокът готви. Cost е в лешници (0 = безплатна);
/// Tool е уредът, който трябва да е купен (null = не трябва нищо). Count е на
/// колко парчета се появява из екрана; рисунката е assets/props/food_{Id}.png.
/// </summary>
public sealed record Food(
    string Id, string Name, string Icon, double Fullness, double Energy, int Cost, int Count, string? Tool, string[] Lines)
{
    /// <summary>Първата стъпка в друг уред (миксер за тестото, кафемашина за кафето…), или null.</summary>
    public string? Prep { get; init; }
}

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
    public const string Mixer = "mixer", SandwichMaker = "sandwich", IceCream = "icecream", Grill = "grill";

    /// <summary>Каничката за сокове: не се купува, винаги я има.</summary>
    public const string Jug = "jug";
    public const string Bed = "bed", Fridge = "fridge", Cookbook = "cookbook", Headphones = "headphones";

    // Предмети за почивка: всеки отключва кътче (виж Scenes).
    public const string Easel = "easel", Computer = "computer", YogaMat = "yoga_mat", PlantPot = "plant", Radio = "radio", BoardGame = "board_game";
    public const string Trampoline = "trampoline", Couch = "couch", Wardrobe = "wardrobe", Heater = "heater";

    // Раздели в магазина
    public const string KitchenShelf = "Кухня", FunShelf = "Хобита", FurnitureShelf = "Мебели", HelpShelf = "Подобрения";

    /// <summary>Безплатните боровинки се берат най-много веднъж на толкова минути.</summary>
    public const int BerryMinutes = 10;

    public static readonly Food[] Foods =
    {
        new("berries", "Боровинки", "🫐", 12, 0, 0, 3, null,
            new[] { "Боровинки! Сини като небето.", "Ням! Още една.", "Кисело-сладко. Обичам." }),
        new("popcorn", "Пуканки", "🍿", 24, 0, 2, 5, Pot,
            new[] { "Пук! Хванах я с уста!", "Хрус-хрус.", "Пуканки като за кино вечер!" }),
        new("meatballs", "Кюфтенца", "🍖", 40, 0, 4, 3, Pan,
            new[] { "Ох, горещо! *духа* Мммм.", "Кюфтенце! Най-хубавият ден!", "Цвър-цвър… ням." }),
        new("potatoes", "Мечешки картофки", "🥔", 50, 0, 5, 4, Oven,
            new[] { "Мечешки картофки от фурната! Ухааа.", "Хрупкави отвън, меки отвътре. Перфектни.", "Ще ги направя пак утре. И вдругиден." }),
        new("coffee", "Кафе", "☕", 3, 35, 2, 1, CoffeeMachine,
            new[] { "Кафе! Очите ми се отвориха!", "Ааах. Сега мога да прототипирам цял ден.", "Бззз! Енергия!" }),
        new("tea", "Чай с мед", "🍵", 6, 12, 1, 1, Kettle,
            new[] { "Чай с мед. Като у дома.", "Топличко… мммм.", "Мед в чая е най-хубавото изобретение." }),
        new("juice_orange", "Портокалов сок", "🧃", 8, 10, 2, 1, Jug,
            new[] { "Портокалов сок! Слънце в чаша.", "*сърба през сламката* Мм, кисело-сладко.", "Витамини! Ще съм бодър." }),
        new("juice_berry", "Боровинков сок", "🧃", 10, 8, 3, 1, Jug,
            new[] { "Боровинков сок! Езикът ми ще посинее.", "*сърба* Като горска поляна.", "Мм, сладичко." }),

        // ── Още рецепти (учат се от страници; ⭐ = в два уреда един след друг) ──
        new("lemonade", "Лимонада с мед", "🍋", 8, 10, 2, 1, Jug, Array.Empty<string>()),
        new("soup", "Супичка", "🥣", 35, 0, 3, 1, Pot, Array.Empty<string>()),
        new("corn", "Царевица на кочан", "🌽", 20, 0, 2, 2, Pot, Array.Empty<string>()),
        new("cocoa", "Какао", "🍫", 8, 8, 2, 1, Kettle, Array.Empty<string>()),
        new("linden_tea", "Липов чай", "🌼", 5, 5, 1, 1, Kettle, Array.Empty<string>()),
        new("eggs", "Яйца на очи", "🥚", 25, 5, 2, 2, Pan, Array.Empty<string>()),
        new("pancakes", "Палачинки с мед", "🥞", 40, 0, 4, 3, Pan, Array.Empty<string>()) { Prep = Mixer },
        new("espresso", "Еспресо", "🫘", 2, 40, 3, 1, CoffeeMachine, Array.Empty<string>()),
        new("latte", "Лате", "🥛", 6, 30, 3, 1, CoffeeMachine, Array.Empty<string>()),
        new("mocha", "Мока", "🟤", 8, 30, 4, 1, CoffeeMachine, Array.Empty<string>()),
        new("cappuccino", "Капучино", "🫧", 6, 40, 4, 1, Mixer, Array.Empty<string>()) { Prep = CoffeeMachine },
        new("iced_coffee", "Ледено кафе", "🧋", 6, 30, 4, 1, IceCream, Array.Empty<string>()) { Prep = CoffeeMachine },
        new("cookies", "Бисквитки", "🍪", 15, 0, 2, 4, Oven, Array.Empty<string>()),
        new("muffins", "Мъфини с боровинки", "🧁", 30, 5, 4, 3, Oven, Array.Empty<string>()) { Prep = Mixer },
        new("pizza", "Пица", "🍕", 60, 0, 6, 4, Oven, Array.Empty<string>()) { Prep = Mixer },
        new("smoothie", "Смути", "🥤", 12, 15, 2, 1, Mixer, Array.Empty<string>()),
        new("toast", "Тост с кашкавал", "🍞", 25, 0, 2, 1, SandwichMaker, Array.Empty<string>()),
        new("sandwich_ham", "Сандвич с шунка", "🥪", 35, 0, 3, 1, SandwichMaker, Array.Empty<string>()),
        new("sandwich_honey", "Сандвич с банан и мед", "🍌", 30, 5, 3, 1, SandwichMaker, Array.Empty<string>()),
        new("waffle_honey", "Гофрета с мед", "🟨", 25, 0, 2, 2, SandwichMaker, Array.Empty<string>()),
        new("waffle_choco", "Шоколадова гофрета", "🟫", 28, 0, 3, 2, SandwichMaker, Array.Empty<string>()),
        new("waffle_icecream", "Гофрети със сладолед", "🍧", 35, 0, 5, 2, SandwichMaker, Array.Empty<string>()) { Prep = IceCream },
        new("icecream_honey", "Медено-лешников сладолед", "🍦", 15, 0, 3, 1, IceCream, Array.Empty<string>()),
        new("icecream_berry", "Боровинков сладолед", "🟣", 15, 0, 3, 1, IceCream, Array.Empty<string>()) { Prep = Mixer },
        new("skewers", "Шишчета", "🍢", 50, 0, 5, 3, Grill, Array.Empty<string>()),
        new("veggies", "Печени зеленчуци", "🫑", 30, 0, 3, 3, Grill, Array.Empty<string>()),
        new("grilled_bread", "Скарени филийки с мед", "🥖", 25, 0, 2, 2, Grill, Array.Empty<string>()) { Prep = Oven },
    };

    /// <summary>
    /// Какво му се яде днес: 1–2 ястия от тези, които може да се сготвят (уред и
    /// рецепта ги има). Различно всеки ден, но едно и също през деня.
    /// </summary>
    public static Food[] Cravings(SaveData save)
    {
        var can = Foods.Where(f => f.Cost > 0 && CanCook(f, save)).OrderBy(f => f.Id).ToList();
        if (can.Count == 0) return Array.Empty<Food>();
        var day = new Random(DateTime.Today.Year * 1000 + DateTime.Today.DayOfYear + 31);
        var picks = can.OrderBy(_ => day.Next()).Take(can.Count >= 4 ? 2 : 1).ToArray();
        return picks;
    }

    public static bool IsCraving(Food f, SaveData save) => Cravings(save).Contains(f);

    /// <summary>Пие се (седнал, с чаша), а не се яде.</summary>
    public static bool IsDrink(Food f) => f.Id is "tea" or "coffee" or "lemonade" or "cocoa" or "linden_tea" or "espresso" or "latte"
        or "mocha" or "cappuccino" or "iced_coffee" or "smoothie" || f.Id.StartsWith("juice");

    /// <summary>Коя чаша държи (рисунките в assets/clothes: tea, coffee, juice_orange, juice_berry).</summary>
    public static string CupFor(Food f) => f.Id switch
    {
        "espresso" or "latte" or "mocha" or "cappuccino" or "iced_coffee" => "coffee",
        "cocoa" or "linden_tea" => "tea",
        "lemonade" => "juice_orange",
        "smoothie" => "juice_berry",
        _ => f.Id,
    };

    /// <summary>Горещо е (понякога духа).</summary>
    public static bool IsHot(Food f) => f.Id is "tea" or "coffee" or "cocoa" or "linden_tea" or "espresso" or "latte" or "mocha" or "cappuccino";

    public static readonly Upgrade[] Upgrades =
    {
        new(Pot, "Тенджера", "🍲", 10, "Пуканки."),
        new(Kettle, "Чайник", "🫖", 15, "Чай с мед."),
        new(Pan, "Тиган", "🍳", 25, "Кюфтенца."),
        new(CoffeeMachine, "Кафе машина", "☕", 30, "Кафе за енергия."),
        new(Oven, "Фурна", "🔥", 45, "Картофки."),
        new(Mixer, "Миксер", "🥄", 20, "Смути."),
        new(SandwichMaker, "Сандвичарник", "🧇", 25, "Тостове."),
        new(IceCream, "Машинка за сладолед", "🍨", 35, "Сладолед."),
        new(Grill, "Скара", "♨️", 40, "Шишчета."),

        new(PlantPot, "Саксия с цвете", "🌱", 10, "Цвете за поливане.", FunShelf),

        new(YogaMat, "Постелка за йога", "🧘", 15, "За йога.", FunShelf),
        new(Radio, "Радио", "📻", 20, "Музика.", FunShelf),
        new(BoardGame, "Настолна игра", "🎲", 25, "Игра на масичка.", FunShelf),
        new(Easel, "Статив", "🎨", 30, "За рисуване.", FunShelf),
        new(Trampoline, "Батут", "🤸", 35, "За скачане.", FunShelf),
        new(Computer, "Компютър", "🎮", 50, "Игрички.", FunShelf),

        new(Headphones, "Слушалки", "🎧", 35, "+1 🌰 за мечо-доро.", HelpShelf),
        new(Couch, "Диван", "🛋️", 20, "Мек диван за четене.", FurnitureShelf),
        new(Bed, "Меко легло", "🛏️", 40, "Наспива се по-бързо.", FurnitureShelf),
        new(Heater, "Печица", "🔥", 30, "Топличко за зимата.", FurnitureShelf),
        new(Wardrobe, "Гардероб", "👕", 25, "Дрехи, аксесоари и тапети.", FurnitureShelf),
        new(Fridge, "Хладилник", "🧊", 50, "Ястията са с 1 🌰 по-евтини.", HelpShelf),
        new(Cookbook, "Готварска книга", "📖", 60, "Ястията засищат повече.", HelpShelf),
    };

    public static Upgrade? Find(string id) => Upgrades.FirstOrDefault(u => u.Id == id);

    public static bool Owns(SaveData save, string id) => save.Owned.Contains(id);

    public static bool HasTool(Food food, SaveData save) => Has(food.Tool, save) && Has(food.Prep, save);

    private static bool Has(string? tool, SaveData save) => tool is null or Jug || Owns(save, tool);

    /// <summary>
    /// Рецептата се купува веднъж (освен за боровинките); после ястието се готви
    /// колкото пъти искаш. Пази се в Owned като „recipe_&lt;храна&gt;“.
    /// </summary>
    /// <summary>Рецептата, която идва с уреда (останалите се научават от страници).</summary>
    public static string? DefaultRecipe(string tool) => tool switch
    {
        Pot => "popcorn",
        Pan => "meatballs",
        Oven => "potatoes",
        CoffeeMachine => "coffee",
        Kettle => "tea",
        Jug => "juice_orange",
        Mixer => "smoothie",
        SandwichMaker => "toast",
        IceCream => "icecream_honey",
        Grill => "skewers",
        _ => null,
    };

    public static bool IsDefaultRecipe(Food food) => food.Tool != null && DefaultRecipe(food.Tool) == food.Id;

    public static bool HasRecipe(Food food, SaveData save) => food.Cost == 0 || Owns(save, RecipeId(food));

    public static string RecipeId(Food food) => "recipe_" + food.Id;

    public static int RecipePrice(Food food) => food.Id switch
    {
        "popcorn" => 5,
        "tea" => 4,
        "coffee" => 6,
        "meatballs" => 10,
        "potatoes" => 12,
        _ => 5,
    };

    public static bool CanCook(Food food, SaveData save) => HasTool(food, save) && HasRecipe(food, save);

    public static int CostOf(Food food, SaveData save) =>
        food.Cost == 0 ? 0 : Math.Max(1, food.Cost - (Owns(save, Fridge) ? 1 : 0));

    /// <summary>Храната (кафе, чай) вдига енергията най-много до половината; догоре само със сън.</summary>
    public const double FoodEnergyCap = 50;

    /// <summary>Енергията след храна с толкова енергия.</summary>
    public static double EnergyAfter(double energy, double gain, double cap = FoodEnergyCap) =>
        gain <= 0 || energy >= cap ? energy : Math.Min(cap, energy + gain);

    public static double FullnessOf(Food food, SaveData save) => food.Fullness * (Owns(save, Cookbook) ? 1.25 : 1);

    /// <summary>След колко минути пак може да набере боровинки (0 = сега).</summary>
    public static int BerriesIn(SaveData save) =>
        Math.Max(0, (int)Math.Ceiling((save.BerriesReadyAt - DateTime.Now).TotalMinutes));
}
