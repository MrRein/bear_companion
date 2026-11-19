using System;
using System.Linq;
using System.Collections.Generic;

namespace Mecho;

/// <summary>
/// Храна или напитка, която мечокът готви. Cost е в лешници (0 = безплатна);
/// Tool е уредът, който трябва да е купен (null = не трябва нищо). Count е на
/// колко парчета се появява из екрана (само с Pieces); рисунката е assets/props/food/{Id}.png.
/// </summary>
public sealed record Food(
    string Id, string Name, string Icon, double Fullness, double Energy, int Cost, int Count, string? Tool, string[] Lines)
{
    /// <summary>Първата стъпка в друг уред (миксер за тестото, кафемашина за кафето…), или null.</summary>
    public string? Prep { get; init; }

    /// <summary>Чия рисунка ползва (food_{Art}.png), ако няма своя; null – своята (food_{Id}).</summary>
    public string? Art { get; init; }

    /// <summary>
    /// food_pieces: сготвено излиза на парчета (Count) с отделна рисунка {Id}_piece.png,
    /// ако я има. Без – едно цяло, със същата рисунка като в менюто.
    /// </summary>
    public bool Pieces { get; init; }

    /// <summary>На колко хапки го изяжда (0 – 4–6, както обикновено). Пуканките – много.</summary>
    public int Bites { get; init; }

    /// <summary>Името на рисунката: food_{ArtId}.</summary>
    public string ArtId => Art ?? Id;
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

    /// <summary>Масичката за сандвичи: не се купува, винаги я има (основната храна).</summary>
    public const string Table = "table";
    public const string Bed = "bed", Fridge = "fridge", Cookbook = "cookbook", Headphones = "headphones";

    // Предмети за почивка: всеки отключва кътче (виж Scenes).
    public const string Easel = "easel", Computer = "computer", YogaMat = "yoga_mat", PlantPot = "plant", Radio = "radio", BoardGame = "board_game";
    public const string Trampoline = "trampoline", Couch = "couch", Wardrobe = "wardrobe", Heater = "heater";
    public const string YarnBasket = "yarn_basket", Guitar = "guitar", Lamp = "lamp";
    public const string Desk = "desk", Feeder = "feeder";

    // Раздели в магазина
    public const string KitchenShelf = "Кухня", FunShelf = "Хобита", FurnitureShelf = "Мебели", HelpShelf = "Подобрения";

    /// <summary>Безплатните боровинки се берат най-много веднъж на толкова минути.</summary>
    public const int BerryMinutes = 10;

    /// <summary>
    /// Рисунките (assets/props/food/): {Id}.png е цялото ястие – в менюто и сготвено.
    /// С Pieces = true сготвеното излиза на Count парчета ({Id}_piece.png, ако я има);
    /// иначе е едно цяло (Count = 1) и се яде на хапки (Bites).
    /// </summary>
    public static readonly Food[] Foods =
    {
        new("berries", "Боровинки", "🫐", 12, 0, 0, 3, null,
            new[] { "Боровинки! Сини като небето.", "Ням! Още една.", "Кисело-сладко. Обичам." }) { Pieces = true },
        new("sandwich_veg", "Сандвич с домат", "🫓", 28, 0, 6, 1, Table,
            new[] { "Сандвич! Прост и перфектен.", "Хляб, домат, сирене… ням!", "Най-хубавото е, когато ти го направиш." }),
        new("sandwich_ham", "Сандвич с шунка", "🥪", 32, 0, 8, 1, Table, Array.Empty<string>()),
        new("sandwich_egg", "Сандвич с яйце", "🥯", 30, 0, 8, 1, Table, Array.Empty<string>()),
        new("sandwich_olives", "Сандвич с маслини", "🫒", 28, 0, 8, 1, Table, Array.Empty<string>()),
        new("popcorn", "Пуканки", "🍿", 24, 0, 7, 1, Pot,
            new[] { "Пук! Хванах я с уста!", "Хрус-хрус.", "Пуканки като за кино вечер!" }) { Bites = 14 },
        new("meatballs", "Кюфтенца", "🍖", 40, 0, 12, 3, Pan,
            new[] { "Ох, горещо! *духа* Мммм.", "Кюфтенце! Най-хубавият ден!", "Цвър-цвър… ням." }) { Pieces = true },
        new("potatoes", "Мечешки картофки", "🥔", 50, 0, 12, 1, Oven,
            new[] { "Мечешки картофки от фурната! Ухааа.", "Хрупкави отвън, меки отвътре. Перфектни.", "Ще ги направя пак утре. И вдругиден." }) { Bites = 8 },
        new("coffee", "Кафе", "☕", 3, 35, 5, 1, CoffeeMachine,
            new[] { "Кафе! Очите ми се отвориха!", "Ааах. Сега мога да прототипирам цял ден.", "Бззз! Енергия!" }),
        new("tea", "Чай с мед", "🍵", 6, 12, 4, 1, Kettle,
            new[] { "Чай с мед. Като у дома.", "Топличко… мммм.", "Мед в чая е най-хубавото изобретение." }),
        new("juice_orange", "Портокалов сок", "🧃", 8, 10, 4, 1, Jug,
            new[] { "Портокалов сок! Слънце в чаша.", "*сърба през сламката* Мм, кисело-сладко.", "Витамини! Ще съм бодър." }),
        new("juice_berry", "Боровинков сок", "🧃", 10, 8, 5, 1, Jug,
            new[] { "Боровинков сок! Езикът ми ще посинее.", "*сърба* Като горска поляна.", "Мм, сладичко." }),

        // ── Още рецепти (учат се от страници; ⭐ = в два уреда един след друг) ──
        new("lemonade", "Лимонада с мед", "🍋", 8, 10, 6, 1, Jug, Array.Empty<string>()),
        new("soup_veg", "Зеленчукова супа", "🥣", 35, 0, 8, 1, Pot,
            new[] { "Зеленчукова супа! Морков, картоф, мммм.", "*духа* Топличко за коремчето.", "Като на баба Мецана." }),
        new("soup_roast", "Печена супа", "🫕", 45, 0, 9, 1, Pot, Array.Empty<string>()) { Prep = Oven },
        new("corn", "Царевица на кочан", "🌽", 20, 0, 5, 2, Pot, Array.Empty<string>()) { Pieces = true },
        new("cocoa", "Какао", "🍫", 8, 8, 4, 1, Kettle, Array.Empty<string>()),
        new("linden_tea", "Липов чай", "🌼", 5, 5, 3, 1, Kettle, Array.Empty<string>()),

        // ── Само докато трае събитие (Mods.ShopExtras; виж EventTag) ──
        // Без уред: идва готово (като от аптеката) – не се „бърка“ в каничката за сок.
        new(Medicine, "Лекарство: липов чай с мед", "🌼", 4, 10, 2, 1, null,
            new[] { "*сърба* Горчичко… но ми става по-добре.", "Благодаря, че се грижиш за мен.", "Мм. Вече не ми е толкова студено." }) { Art = "linden_tea" },
        new("eggs", "Яйца на очи", "🥚", 25, 5, 6, 2, Pan, Array.Empty<string>()) { Pieces = true },
        new("pancakes", "Палачинки с мед", "🥞", 40, 0, 7, 3, Pan, Array.Empty<string>()) { Prep = Mixer, Pieces = true },
        new("espresso", "Еспресо", "🫘", 2, 40, 5, 1, CoffeeMachine, Array.Empty<string>()),
        new("latte", "Лате", "🥛", 6, 30, 6, 1, CoffeeMachine, Array.Empty<string>()),
        new("cappuccino", "Капучино", "🫧", 6, 40, 6, 1, Mixer, Array.Empty<string>()) { Prep = CoffeeMachine },
        new("iced_coffee", "Ледено кафе", "🧋", 6, 30, 5, 1, IceCream, Array.Empty<string>()) { Prep = CoffeeMachine },
        new("cookies", "Бисквитки", "🍪", 15, 0, 7, 4, Oven, Array.Empty<string>()) { Pieces = true },
        new("muffins", "Мъфини с боровинки", "🧁", 30, 5, 8, 3, Oven, Array.Empty<string>()) { Prep = Mixer, Pieces = true },
        new("pizza_margarita", "Пица Маргарита", "🍕", 60, 0, 10, 4, Oven, Array.Empty<string>()) { Prep = Mixer, Pieces = true },
        new("smoothie", "Смути", "🥤", 12, 15, 7, 1, Mixer, Array.Empty<string>()),
        new("pressed_cheese", "Мачкан с кашкавал", "🍞", 25, 0, 7, 1, SandwichMaker, Array.Empty<string>()),
        new("pressed_ham", "Мачкан с шунка", "🥓", 35, 0, 7, 1, SandwichMaker, Array.Empty<string>()),
        new("pressed_pizza", "Пица сандвич", "🥙", 40, 0, 7, 1, SandwichMaker, Array.Empty<string>()),
        new("waffle_chocolate", "Гофрета със шоколад", "🟫", 28, 0, 6, 1, SandwichMaker, Array.Empty<string>()),
        new("waffle_blueberry", "Гофрета с боровинки", "🟦", 26, 0, 6, 1, SandwichMaker, Array.Empty<string>()),
        new("waffle_icecream", "Гофрета със сладолед", "🍧", 35, 0, 6, 1, SandwichMaker, Array.Empty<string>()) { Prep = IceCream },
        new("icecream_vanilla", "Ванилов сладолед", "🍥", 15, 0, 7, 1, IceCream, Array.Empty<string>()),
        new("icecream_choko", "Шоколадов сладолед", "🍦", 15, 0, 7, 1, IceCream, Array.Empty<string>()),
        new("icecream_berry", "Боровинков сладолед", "🟣", 15, 0, 7, 1, IceCream, Array.Empty<string>()) { Prep = Mixer },
        new("skewers", "Шишчета", "🍢", 50, 0, 8, 1, Grill, Array.Empty<string>()) { Bites = 7 },
    };

    /// <summary>
    /// Рисунката на сготвеното: с Pieces – парчето (food_{Art}_piece), ако е нарисувано;
    /// иначе цялото ястие (food_{Art}), както в менюто.
    /// </summary>
    public static string CookedArt(Food f, Func<string, bool> has) =>
        f.Pieces && has($"food_{f.ArtId}_piece") ? $"food_{f.ArtId}_piece" : "food_" + f.ArtId;

    /// <summary>Стари ястия, сменени с нови (за научените рецепти, желанията и любимата храна в стари записи).</summary>
    public static readonly Dictionary<string, string> Renamed = new()
    {
        ["sandwich"] = "sandwich_veg", ["soup"] = "soup_veg", ["roast_soup"] = "soup_roast", ["pizza"] = "pizza_margarita",
        ["toast"] = "pressed_cheese", ["pressed_sandwich"] = "pressed_ham",
        ["waffle_choco"] = "waffle_chocolate", ["waffle_honey"] = "waffle_blueberry", ["icecream_honey"] = "icecream_vanilla",
    };


    // ── Храненията: кое за кога е логично ──

    /// <summary>За кое хранене е ястието: закуска, обяд, вечеря, нещо между храненията (може няколко).</summary>
    [Flags]
    public enum Meal
    {
        None = 0,
        Breakfast = 1,
        Lunch = 2,
        Dinner = 4,
        Snack = 8,
    }

    private const Meal B = Meal.Breakfast, L = Meal.Lunch, D = Meal.Dinner, S = Meal.Snack;

    /// <summary>Храненията на всяко ястие. Каквото го няма тук – нещо между храненията.</summary>
    private static readonly Dictionary<string, Meal> MealMap = new()
    {
        ["sandwich_veg"] = B | S, ["sandwich_ham"] = B | L, ["sandwich_egg"] = B, ["sandwich_olives"] = B | S,
        ["popcorn"] = S, ["meatballs"] = L | D, ["potatoes"] = L | D,
        ["coffee"] = B, ["tea"] = B | S, ["juice_orange"] = B | S, ["juice_berry"] = S, ["lemonade"] = S,
        ["soup_veg"] = L | D, ["soup_roast"] = L | D, ["corn"] = S, ["cocoa"] = B | S, ["linden_tea"] = S,
        ["eggs"] = B, ["pancakes"] = B, ["espresso"] = B | S, ["latte"] = B, ["cappuccino"] = B,
        ["iced_coffee"] = S, ["cookies"] = S, ["muffins"] = B | S, ["pizza_margarita"] = L | D, ["smoothie"] = B | S,
        ["pressed_cheese"] = B | S, ["pressed_ham"] = B | L, ["pressed_pizza"] = L | D,
        ["waffle_chocolate"] = S, ["waffle_blueberry"] = B | S, ["waffle_icecream"] = S,
        ["icecream_vanilla"] = S, ["icecream_choko"] = S, ["icecream_berry"] = S, ["skewers"] = L | D,
    };

    public static Meal MealsOf(Food f) => MealMap.TryGetValue(f.Id, out var m) ? m : Meal.Snack;

    /// <summary>
    /// Кое хранене е сега: закуска 5:00–10:30, обяд 11:40–14:30, вечеря 18:30–21:00,
    /// между тях (и късно вечер, и нощем) – нещо между храненията.
    /// </summary>
    public static Meal MealAt(DateTime t)
    {
        double h = t.Hour + t.Minute / 60.0;
        return h >= 5 && h < 10.5 ? Meal.Breakfast
            : h >= 11 + 40 / 60.0 && h < 14.5 ? Meal.Lunch
            : h >= 18.5 && h < 21 ? Meal.Dinner
            : Meal.Snack;
    }

    /// <summary>Следващото истинско хранене след t (за „за следващия път“).</summary>
    public static Meal NextMeal(DateTime t)
    {
        double h = t.Hour + t.Minute / 60.0;
        return h < 10.5 ? (h >= 5 ? Meal.Lunch : Meal.Breakfast) : h < 14.5 ? Meal.Dinner : h < 21 ? Meal.Dinner : Meal.Breakfast;
    }

    /// <summary>Логично ли е да му се яде това сега.</summary>
    public static bool FitsNow(Food f, DateTime t) => (MealsOf(f) & MealAt(t)) != 0;

    /// <summary>Пие се (седнал, с чаша), а не се яде.</summary>
    /// <summary>Лекарството (болен ден): продава се само докато е болен.</summary>
    public const string Medicine = "medicine";

    /// <summary>Таг в Mods.ShopExtras, без който храната я няма (или null – винаги я има).</summary>
    public static string? EventTag(Food f) => f.Id == Medicine ? Mecho.Core.GameEvents.Medicine : null;

    /// <summary>Обикновена храна (не само за събитие) – за желания, любими, рецепти.</summary>
    public static bool Regular(Food f) => EventTag(f) == null;

    /// <summary>Таговете на днешните събития (PetWindow го свързва с ядрото).</summary>
    public static Func<System.Collections.Generic.IReadOnlySet<string>> ExtrasNow { get; set; } = () => new System.Collections.Generic.HashSet<string>();

    public static bool IsDrink(Food f) => f.Id is "tea" or "coffee" or "lemonade" or "cocoa" or "linden_tea" or "espresso" or "latte"
        or "cappuccino" or "iced_coffee" or "smoothie" or Medicine || f.Id.StartsWith("juice");

    /// <summary>Коя чаша държи (рисунките в assets/clothes: tea, coffee, juice_orange, juice_berry).</summary>
    public static string CupFor(Food f) => f.Id switch
    {
        "espresso" or "latte" or "cappuccino" or "iced_coffee" => "coffee",
        "cocoa" or "linden_tea" or Medicine => "tea",
        "lemonade" => "juice_orange",
        "smoothie" => "juice_berry",
        _ => f.Id,
    };

    /// <summary>Горещо е (понякога духа).</summary>
    public static bool IsHot(Food f) => f.Id is "tea" or "coffee" or "cocoa" or "linden_tea" or Medicine or "espresso" or "latte" or "cappuccino";

    public static readonly Upgrade[] Upgrades =
    {
        new(Pot, "Тенджера", "🍲", 35, "За варене."),
        new(Kettle, "Чайник", "🫖", 40, "За топли напитки."),
        new(Pan, "Тиган", "🍳", 45, "За пържене."),
        new(CoffeeMachine, "Кафе машина", "☕", 50, "За кафе."),
        new(Oven, "Фурна", "🔥", 70, "За печене."),
        new(Mixer, "Миксер", "🥄", 35, "За разбиване и смесване."),
        new(SandwichMaker, "Сандвичарник", "🧇", 50, "За сандвичи и тостове."),
        new(IceCream, "Машинка за сладолед", "🍨", 50, "За домашен сладолед."),
        new(Grill, "Скара", "♨️", 60, "За печене на скара."),

        new(PlantPot, "Саксия с растение", "🌱", 35, "Растение, което расте с грижите му.", FunShelf),

        new(YogaMat, "Постелка за йога", "🧘", 30, "За йога.", FunShelf),
        new(Radio, "Радио", "📻", 40, "Музика.", FunShelf),
        new(BoardGame, "Настолна игра", "🎲", 50, "Игра на масичка.", FunShelf),
        new(Easel, "Статив", "🎨", 60, "За рисуване.", FunShelf),
        // Батутът е архивиран (archive/trampoline) – не се продава.
        new(Computer, "Компютър", "🎮", 70, "Игрички.", FunShelf),
        new(YarnBasket, "Кошница с прежда", "🧶", 35, "За плетене.", FunShelf),
        new(Guitar, "Китара", "🎸", 45, "Свири си песнички.", FunShelf),

        new(Couch, "Диван", "🛋️", 60, "Мек диван за четене.", FurnitureShelf),
        new(Bed, "Меко легло", "🛏️", 70, "Наспива се по-бързо.", FurnitureShelf),
        new(Heater, "Печица", "🌡", 50, "Топличко за зимата.", FurnitureShelf),
        new(Wardrobe, "Гардероб", "👕", 50, "Дрехи, аксесоари и тапети.", FurnitureShelf),
        new(Lamp, "Лампа", "🪔", 35, "Свети вечер.", FurnitureShelf),
        new(Desk, "Бюро", "🗄", 55, "Работи на бюро, седнал на столче до него. Слагаш го от „Неща на Мечо“ (или той си го слага сам).", FurnitureShelf),
        new(Feeder, "Хранилка за птички", "🐦", 65, "Слагаш я някъде, той слага храна, а понякога идват птички.", FurnitureShelf),
        // Хладилникът, слушалките и готварската книга са махнати от магазина засега
        // (който ги има отпреди, ефектът им остава).
    };

    public static Upgrade? Find(string id) => Upgrades.FirstOrDefault(u => u.Id == id);

    public static bool Owns(SaveData save, string id) => save.Owned.Contains(id);

    public static bool HasTool(Food food, SaveData save) => Has(food.Tool, save) && Has(food.Prep, save);

    private static bool Has(string? tool, SaveData save) => tool is null or Jug or Table || Owns(save, tool);

    /// <summary>
    /// Рецептата се купува веднъж (освен за боровинките); после ястието се готви
    /// колкото пъти искаш. Пази се в Owned като „recipe_&lt;храна&gt;“.
    /// </summary>
    /// <summary>Рецептата, която идва с уреда (останалите се научават от страници).</summary>
    public static string? DefaultRecipe(string tool) => tool switch
    {
        Pot => "soup_veg",
        Pan => "meatballs",
        Oven => "potatoes",
        CoffeeMachine => "coffee",
        Kettle => "tea",
        Jug => "juice_orange",
        Table => "sandwich_veg",
        Mixer => "smoothie",
        SandwichMaker => "pressed_cheese",
        IceCream => "icecream_vanilla",
        Grill => "skewers",
        _ => null,
    };

    public static bool IsDefaultRecipe(Food food) => food.Tool != null && DefaultRecipe(food.Tool) == food.Id;

    public static bool HasRecipe(Food food, SaveData save) =>
        EventTag(food) is { } tag ? ExtrasNow().Contains(tag)
        : food.Cost == 0 || food.Tool == Table || Owns(save, RecipeId(food));

    /// <summary>Разделите в „Храна“.</summary>
    public static readonly string[] Categories = { "Сандвичи", "Манджи", "Хапки", "Сладко", "Кафе и чай", "Сокове" };

    public static string CategoryOf(Food f) => f.Id switch
    {
        _ when f.Id.StartsWith("sandwich_") || f.Id.StartsWith("pressed_") => "Сандвичи",
        "berries" or "popcorn" or "corn" or "cookies" => "Хапки",
        "pancakes" or "muffins" => "Сладко",
        _ when f.Id.StartsWith("waffle") || f.Id.StartsWith("icecream") => "Сладко",
        "coffee" or "espresso" or "latte" or "cappuccino" or "cocoa" or "tea" or "linden_tea" or Medicine => "Кафе и чай",
        _ when f.Id.StartsWith("juice") || f.Id is "lemonade" or "smoothie" or "iced_coffee" => "Сокове",
        _ => "Манджи",
    };

    /// <summary>Топла напитка (за „Иска нещо топло за пиене“, когато му е студено).</summary>
    public static bool IsHotDrink(Food f) => f.Id is "coffee" or "espresso" or "latte" or "cappuccino" or "cocoa" or "tea" or "linden_tea";

    /// <summary>Нещо малко за хапване („Снаква му се“): хапките, сладоледите, смутито.</summary>
    public static bool IsSnack(Food f) => CategoryOf(f) == "Хапки" || f.Id.StartsWith("icecream") || f.Id == "smoothie";

    /// <summary>Вид храна, когато е много гладен: „Иска супа“ – коя да е от тях.</summary>
    public sealed record FoodGroup(string Id, string Wish, string[] Foods);

    public static readonly FoodGroup[] Groups =
    {
        new("soup", "Иска супа", new[] { "soup_veg", "soup_roast" }),
        new("sandwich", "Иска сандвич", new[] { "sandwich_veg", "sandwich_ham", "sandwich_egg", "sandwich_olives", "pressed_cheese", "pressed_ham", "pressed_pizza" }),
        new("pizza", "Иска пица", new[] { "pizza_margarita", "pressed_pizza" }),
        new("meat", "Иска нещо месно", new[] { "meatballs", "skewers", "sandwich_ham", "pressed_ham" }),
    };

    public static FoodGroup? GroupById(string? id) => Groups.FirstOrDefault(g => g.Id == id);

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
