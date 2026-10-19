using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mecho;

/// <summary>Всичко, което мечокът помни между пусканията.</summary>
public sealed class SaveData
{
    public string OwnerName { get; set; } = "Тут";
    public double X { get; set; } = double.NaN;
    public double Y { get; set; } = double.NaN;

    // Къде Тут е преместила панела „Мечо“ (NaN = до мечока)
    public double MenuLeft { get; set; } = double.NaN;
    public double MenuTop { get; set; } = double.NaN;
    public bool SleepingByChoice { get; set; }
    public bool StayPut { get; set; }
    public string StayScene { get; set; } = "reading";   // кое кътче е избрала Тут („Почивай си“)
    public DateTime QuietUntil { get; set; }
    public bool StartWithWindows { get; set; } = true;
    public bool FirstRunDone { get; set; }
    public bool TutorialDone { get; set; }                       // обиколката „Как се играе?“ е показана
    public DateTime LastSeen { get; set; }

    // Грижа (0–100)


    // Прогрес
    public List<string> MealsDone { get; set; } = new();      // "lunch:2026-10-11", "lunch-thanks:…" (виж Meals)
    public List<int> Garden { get; set; } = new();               // стадиите на цветята в градинката (0–4)
    public List<string> GardenColors { get; set; } = new();
    public List<string> GardenGrown { get; set; } = new();       // в кой ден е расло всяко (веднъж на ден)
    public bool SoundOn { get; set; } = true;                    // „динг“ в края на мечо-доро и таймер
    public double SoundVolume { get; set; } = 0.6;
    public DateTime WalkUntil { get; set; } = DateTime.MinValue;
    public bool WalkSolo { get; set; }                           // тръгна сам (никой не му каза какво да прави)
    public bool WalkNap { get; set; }                            // вечерна разходка-дрямка: брои се за сън  // на разходка извън екрана до (виж PetWindow.Walk.cs)            // скинът на менюто (виж UiThemes)             // коя пижама облича вечер (виж Clothes.PajamaSets)
    public bool ShowTimers { get; set; } = true;                 // табелката с таймерите до часовника, докато мечокът е скрит
    public bool DrawingsHidden { get; set; }                     // „Скрий всичко“ от иконката: рисунките са скрити
    public bool LampOff { get; set; }                           // Тут изгаси лампата тази вечер (до сутринта)
    public int BearScale { get; set; }                          // размер на мечока (0 = по подразбиране)

    /// <summary>Офертата на деня в магазина: за кой ден е и какво има (виж DailyOffer).</summary>
    public string OfferDay { get; set; } = "";

    /// <summary>Рецептата, която се събира в момента (тайна), и колко страници има от нея.</summary>
    public string? NextRecipe { get; set; }
    public int RecipePages { get; set; }
    public List<string> OfferItems { get; set; } = new();
    public DateTime BerriesReadyAt { get; set; }
    public List<DateTime> CoffeeTimes { get; set; } = new();

    /// <summary>Денят, в който сутринта се е събудил бодър (енергия 100).</summary>
    public string MorningRefill { get; set; } = "";

    /// <summary>Купени, но още неотворени (идват с доставка; отключват се при отварянето).</summary>
    public List<string> Ordered { get; set; } = new();

    // Умора (виж PetWindow.Energy.cs)
    public string DayKind { get; set; } = "";           // какъв ден е (вижда се само в Developer)
    public string DayKindDate { get; set; } = "";
    public string LastLazy { get; set; } = "";          // последният мързелив ден (не два подред)
    public double Fatigue { get; set; }                 // умора от седмицата (0–5)
    public bool WasDrained { get; set; }                // вчера е капнал (под 15)
    public double DayLowEnergy { get; set; } = 100;
    public double WorkMinutesToday { get; set; }
    public DateTime WentToBed { get; set; }             // кога го сложи да спи снощи
    public string LateCoffee { get; set; } = "";        // денят, в който пи кафе след 17 ч.
    public DateTime WokeAt { get; set; }                // кога се събуди тази сутрин
    public DateTime NapUntil { get; set; } = DateTime.MinValue;   // дрямка до (MinValue = не дреме)
    public double NapCap { get; set; } = 100;           // дрямката го вдига дотук
    public bool SleepForDay { get; set; }
    public bool WatchApps { get; set; } = true;          // поглежда коя програма е отпред и понякога казва нещо (PetWindow.Apps.cs)
    public bool TrayClickHides { get; set; }            // Developer: цъкане на иконката до часовника скрива/показва мечока (иначе – отваря менюто)
    public bool CalmWhileTyping { get; set; }           // докато Тут пише – прерисува се по-рядко (само ако курсорът премигва; иначе накъсва)
    public bool TypingCalmReset { get; set; }           // веднъж: старото „включено“ по подразбиране се изключва (то беше причината за накъсването)

    /// <summary>Вечерта, в която в 22:00 енергията вече е паднала.</summary>
    public string NightDropDay { get; set; } = "";

    /// <summary>Вечерта, в която в 23:00 е решено дали ще капне (много голям шанс).</summary>
    public string LateDropDay { get; set; } = "";

    /// <summary>„Почивай си“/„Работи си“ е избрано от самия мечок (по часовника), не от Тут.</summary>
    public bool AutoMode { get; set; }
    public string JoyDay { get; set; } = "";        // за кой ден са JoyFlags
    public int JoyFlags { get; set; }               // днес: 1 – галиха го, 2 – нахраниха го, 4 – работиха заедно
    public string RitualDay { get; set; } = "";
    public string? GoalId { get; set; }                        // за какво събираме в магазина (виж PetWindow.Goal.cs)
    public List<string> GoalChoices { get; set; } = new();     // трите неща за избор
    public bool GoalRerollUsed { get; set; }                   // „презареди“ вече е ползвано за този избор
    public int GoalsDone { get; set; }                         // колко цели са купени     // сутрешният ритуал (веднъж на ден)

    /// <summary>Къде е (по интернет връзката) – за температурата.</summary>
    public double WeatherLat { get; set; } = double.NaN;
    public double WeatherLon { get; set; } = double.NaN;
    public string WeatherCity { get; set; } = "";
    public string WeatherPlaceDay { get; set; } = "";

    /// <summary>Печицата е включена.</summary>

    public List<NoteItem> Notes { get; set; } = new();
    public List<ReminderItem> Reminders { get; set; } = new();
    public List<DayRecord> Days { get; set; } = new();          // какво сме правили всеки ден (последните 60)
    public string MemoryDay { get; set; } = "";                 // за кой ден вече каза спомена от вчера
    public string MemoryLaterDay { get; set; } = "";            // втори, по-късен спомен (следобед)
    public List<string> MemoryUsed { get; set; } = new();       // последните казани видове спомени (да не се повтарят)
    public string FavFood { get; set; } = "";                   // любимата храна за днес (виж PetWindow.Memory.cs)
    public string FavFoodDay { get; set; } = "";
    public string WishTimes { get; set; } = "";                // днешните желания, които още не са се появили (минути от полунощ)
    public Dictionary<string, DateTime> WishUntil { get; set; } = new();   // докога трае всяко появило се желание
    public string OddCraving { get; set; } = "";               // тайното странно желание за днес (храна), "" – няма
    public string OddCravingDay { get; set; } = "";
    public int OddCravingMeal { get; set; }                     // за кое хранене (Kitchen.Meal)
    public bool FavFoodSaid { get; set; }                       // каза ли днес какво му се яде
    public PinState PinTasks { get; set; } = new();              // закаченото прозорче със задачите
    public PinState PinNotes { get; set; } = new();
    public Dictionary<string, SurveyAnswer> Answers { get; set; } = new();   // въпросникът: id → какво отговори Тут
    /// <summary>
    /// Броячи „колко пъти днес“ (въпросник, спомени, нови желания…) – сами се нулират на
    /// нов ден. Новите броячи да са тук (CountToday/TodayCount), а не като нова двойка
    /// полета „XxxDay“ + „XxxToday“.
    /// </summary>
    public Dictionary<string, DayCounter> Daily { get; set; } = new();

    // ─────────────────────────────────────────────────────────────────────────
    // ФАСАДА КЪМ ЯДРОТО (фаза 3, вълна 1: икономика и предмети на земята).
    // Тези данни вече живеят САМО в ядрото (Mecho.Core: EconomyModule, PlacementModule).
    // Старите свойства по-долу са тънки фасади: четат от GameStore и пращат команди,
    // за да не се пренаписва целият стар код наведнъж. Нов код да ползва направо
    // ядрото (Store.Get<WalletState>(), Bus.Send(new Purchase(...)) …).
    // „Legacy…“ полетата само четат стари записи (преди ядрото); при Bind се местят в
    // ядрото и се нулират, за да не се записват повече.
    // ─────────────────────────────────────────────────────────────────────────

    private Mecho.Core.GameStore? _store;
    private Mecho.Core.SignalBus? _bus;
    private InventoryList? _owned, _ownedClothes;

    /// <summary>Свързва записа с ядрото и мести старите данни в него (веднъж).</summary>
    public void Bind(Mecho.Core.GameEngine engine)
    {
        // Стари записи: „спокойно докато пиша“ беше включено по подразбиране и правеше
        // анимацията накъсана, докато Тут пише. Изключва се веднъж (Developer може пак да го пусне).
        if (!TypingCalmReset)
        {
            CalmWhileTyping = false;
            TypingCalmReset = true;
        }
        _store = engine.Store;
        _bus = engine.Bus;
        var st = engine.Store;
        if (!st.WasLoaded(Mecho.Core.EconomyModule.WalletKey))
            _bus.Send(new Mecho.Core.SetNuts(LegacyHazelnuts, "стар запис"));
        if (!st.WasLoaded(Mecho.Core.EconomyModule.InventoryKey))
            _bus.Send(new Mecho.Core.SetInventory(LegacyOwned ?? new List<string>(), LegacyOwnedClothes ?? new List<string>(), "стар запис"));
        if (!st.WasLoaded(Mecho.Core.NeedsModule.Key))
            _bus.Send(new Mecho.Core.SetNeeds(LegacyFullness, LegacyEnergy, "стар запис"));
        if (!st.WasLoaded(Mecho.Core.KitchenModule.CookingKey) && LegacyCookFood != null)
            _bus.Send(new Mecho.Core.StartCook(LegacyCookFood, LegacyCookReadyAt, Math.Max(0, LegacyCookCost), LegacyCookStage is 0 ? 0 : 1, LegacyCookX));
        if (!st.WasLoaded(Mecho.Core.KitchenModule.WishesKey) && LegacyWishList != null)
            _bus.Send(new Mecho.Core.SetWishes(LegacyWishDay ?? "", LegacyWishList));
        if (!st.WasLoaded(Mecho.Core.WorkModule.PomodoroKey))
            _bus.Send(new Mecho.Core.SetPomodoro(new Mecho.Core.PomodoroState((Mecho.Core.WorkPhase)LegacyPomodoroPhase, LegacyPomodoroEndsAt,
                LegacyPomodoroMode ?? "25", LegacyModeLength(true), LegacyModeLength(false), LegacyPomodoroSeries, LegacyPomodoroSeriesLeft,
                LegacyPomodorosToday, LegacyPomodoroDay ?? "", LegacyPomodorosTotal)));
        if (!st.WasLoaded(Mecho.Core.WorkModule.TimerKey))
            _bus.Send(new Mecho.Core.SetWorkTimer(new Mecho.Core.WorkTimerState(LegacyTimerEndsAt, LegacyTimerMinutes, LegacyTimerLabel ?? "",
                LegacyTimerKind ?? (LegacyTimerIsWork ? "work" : "other"))));
        if (!st.WasLoaded(Mecho.Core.WorkModule.BankKey))
            _bus.Send(new Mecho.Core.SetWorkBank(LegacyFocusMinutesBank));
        if (!st.WasLoaded(Mecho.Core.TasksModule.Key) && (LegacyTasks != null || LegacyTaskHistory != null || LegacyTasksDoneTotal != 0))
            _bus.Send(new Mecho.Core.SetTasks(new Mecho.Core.TasksState(
                System.Collections.Immutable.ImmutableList.CreateRange((LegacyTasks ?? new()).Select(t => t.ToCore())),
                System.Collections.Immutable.ImmutableList.CreateRange((LegacyTaskHistory ?? new()).Select(t => t.ToCore())),
                LegacyTasksDoneTotal, LegacyFocusTask)));
        if (!st.WasLoaded(Mecho.Core.WardrobeModule.Key))
        {
            var skins = new Dictionary<string, string>(LegacySkins ?? new());
            // Най-старите записи пазеха диван и легло отделно.
            if (!string.IsNullOrEmpty(LegacyCouchSkin) && LegacyCouchSkin != "classic") skins.TryAdd(Kitchen.Couch, LegacyCouchSkin);
            if (!string.IsNullOrEmpty(LegacyBedSkin) && LegacyBedSkin != "classic") skins.TryAdd(Kitchen.Bed, LegacyBedSkin);
            _bus.Send(new Mecho.Core.SetOutfit(LegacyOutfit ?? new()));
            _bus.Send(new Mecho.Core.SetSkins(skins));
            _bus.Send(new Mecho.Core.SetLook(LegacyPajama ?? "classic", LegacyPajamasAtNight ?? true, LegacyUiTheme ?? "classic", LegacyBackground ?? "meadow"));
        }
        if (!st.WasLoaded(Mecho.Core.GiftsModule.Key) && LegacyGiftDay != null)
            _bus.Send(new Mecho.Core.SetGift(new Mecho.Core.GiftState(LegacyGiftDay, LegacyGiftAt, LegacyGiftPending, LegacyGiftColor ?? "")));
        if (!st.WasLoaded(Mecho.Core.GardenModule.Key))
            _bus.Send(new Mecho.Core.SetPlant(new Mecho.Core.PlantState(LegacyPlantStage, LegacyPlantCareMinutes, LegacyPlantLastGrow, LegacyPlantGrowDay ?? "", LegacyPlantGrowToday)));
        if (!st.WasLoaded(Mecho.Core.PlacementModule.Key))
        {
            void Put(string kind, bool placed, bool bear, bool on, bool onByBear, double x, int supply = 0, string offDay = "") =>
                _bus.Send(new Mecho.Core.SetPlacement(kind, new Mecho.Core.Placement(placed, bear ? Mecho.Core.PlacedBy.Bear : Mecho.Core.PlacedBy.Player, on, onByBear, x, supply, offDay)));
            Put(Kitchen.Radio, LegacyRadioPlaced, LegacyRadioAuto, LegacyRadioOn, LegacyRadioAutoOn, LegacyRadioX);
            Put(Kitchen.Heater, LegacyHeaterOn, false, LegacyHeaterOn, false, LegacyHeaterX);
            Put(Kitchen.Lamp, LegacyLampPlaced, false, LegacyLampPlaced, false, LegacyLampX, 0, LegacyLampOffDay ?? "");
            Put(Kitchen.Feeder, LegacyFeederPlaced, false, false, false, LegacyFeederX, LegacyFeederFood);
            Put(Kitchen.PlantPot, LegacyPlantPlaced, false, false, false, LegacyPlantX);
        }
        // Старите полета вече не се записват.
        LegacyHazelnuts = 0;
        LegacyFullness = LegacyEnergy = 0;
        LegacyOwned = LegacyOwnedClothes = null;
        LegacyRadioPlaced = LegacyRadioOn = LegacyRadioAuto = LegacyRadioAutoOn = LegacyHeaterOn = LegacyLampPlaced = LegacyFeederPlaced = LegacyPlantPlaced = false;
        LegacyFeederFood = 0;
        LegacyLampOffDay = null;
        LegacyCookFood = null;
        LegacyCookReadyAt = default;
        LegacyCookCost = 0;
        LegacyCookStage = 0;
        LegacyCookX = double.NaN;
        LegacyWishList = null;
        LegacyGiftDay = LegacyGiftColor = LegacyPlantGrowDay = null;
        LegacyGiftAt = LegacyPlantLastGrow = DateTime.MinValue;
        LegacyGiftPending = false;
        LegacyPlantStage = LegacyPlantGrowToday = 0;
        LegacyPlantCareMinutes = 0;
        LegacyOutfit = LegacySkins = null;
        LegacyCouchSkin = LegacyBedSkin = LegacyPajama = LegacyUiTheme = LegacyBackground = null;
        LegacyPajamasAtNight = null;
        LegacyTasks = LegacyTaskHistory = null;
        LegacyTasksDoneTotal = 0;
        LegacyFocusTask = null;
        LegacyPomodoroPhase = PomodoroPhase.Off;
        LegacyPomodoroEndsAt = default;
        LegacyPomodoroMode = null;
        LegacyPomodoroSeries = 0;          // 0/false – по подразбиране, не се записват
        LegacyPomodoroSeriesLeft = LegacyPomodorosToday = LegacyPomodorosTotal = 0;
        LegacyPomodoroDay = null;
        LegacyTimerEndsAt = DateTime.MinValue;
        LegacyTimerMinutes = LegacyFocusMinutesBank = 0;
        LegacyTimerLabel = LegacyTimerKind = null;
        LegacyTimerIsWork = false;
        LegacyWishDay = null;
        LegacyRadioX = LegacyHeaterX = LegacyLampX = LegacyFeederX = LegacyPlantX = double.NaN;
    }

    // ── Лешници ──
    [System.Text.Json.Serialization.JsonIgnore]
    public int Hazelnuts
    {
        get => _store?.Get<Mecho.Core.WalletState>().Nuts ?? LegacyHazelnuts;
        set
        {
            if (_bus == null) LegacyHazelnuts = value;
            else _bus.Send(new Mecho.Core.SetNuts(value, "стар код"));
        }
    }

    // ── Нужди (вълна 2: NeedsModule; гладът спада в модула, енергията – още в PetWindow.Energy) ──
    [System.Text.Json.Serialization.JsonIgnore]
    public double Fullness
    {
        get => _store?.Get<Mecho.Core.NeedsState>().Fullness ?? LegacyFullness;
        set
        {
            if (_bus == null) LegacyFullness = value;
            else _bus.Send(new Mecho.Core.SetNeeds(value, null, "стар код"));
        }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public double Energy
    {
        get => _store?.Get<Mecho.Core.NeedsState>().Energy ?? LegacyEnergy;
        set
        {
            if (_bus == null) LegacyEnergy = value;
            else _bus.Send(new Mecho.Core.SetNeeds(null, value, "стар код"));
        }
    }

    // ── Кухня (вълна 3: KitchenModule) – само за четене; пише се с команди (StartCook, EndCook, SetWishes…) ──
    private Mecho.Core.CookingState CookNow => _store?.Get<Mecho.Core.CookingState>() ?? Mecho.Core.CookingState.None;

    [System.Text.Json.Serialization.JsonIgnore] public string? CookFood => _store == null ? LegacyCookFood : CookNow.FoodId;
    [System.Text.Json.Serialization.JsonIgnore] public DateTime CookReadyAt => _store == null ? LegacyCookReadyAt : CookNow.ReadyAt;
    [System.Text.Json.Serialization.JsonIgnore] public int CookCost => _store == null ? LegacyCookCost : CookNow.Cost;
    [System.Text.Json.Serialization.JsonIgnore] public int CookStage => _store == null ? LegacyCookStage : CookNow.Stage;
    [System.Text.Json.Serialization.JsonIgnore] public double CookX => _store == null ? LegacyCookX : CookNow.X;

    /// <summary>Какво му се иска днес (изпълненото се маха).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<string> WishList => _store?.Get<Mecho.Core.WishesState>().Foods ?? (IReadOnlyList<string>?)LegacyWishList ?? Array.Empty<string>();

    /// <summary>За кой ден е списъкът. "" – изчиства го (утре/тест: ще се направи нов).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string WishDay
    {
        get => _store?.Get<Mecho.Core.WishesState>().Day ?? LegacyWishDay ?? "";
        set
        {
            if (_bus == null) LegacyWishDay = value;
            else _bus.Send(new Mecho.Core.SetWishes(value ?? "", value == "" ? Array.Empty<string>() : WishList.ToArray()));
        }
    }

    // ── Работа (вълна 4: WorkModule) – ядрото засича края; тук са тънки фасади ──
    private Mecho.Core.PomodoroState Pom => _store?.Get<Mecho.Core.PomodoroState>() ?? Mecho.Core.PomodoroState.Default;
    private Mecho.Core.WorkTimerState Tmr => _store?.Get<Mecho.Core.WorkTimerState>() ?? Mecho.Core.WorkTimerState.None;
    private void SetPom(Func<Mecho.Core.PomodoroState, Mecho.Core.PomodoroState> change) => _bus?.Send(new Mecho.Core.SetPomodoro(change(Pom)));
    private void SetTmr(Func<Mecho.Core.WorkTimerState, Mecho.Core.WorkTimerState> change) => _bus?.Send(new Mecho.Core.SetWorkTimer(change(Tmr)));

    /// <summary>Дължините на вида мечо-доро от стар запис (виж PetWindow.PomodoroModes).</summary>
    private int LegacyModeLength(bool focus) => (LegacyPomodoroMode, focus) switch
    {
        ("52", true) => 52, ("52", false) => 17, ("90", true) => 90, ("90", false) => 20, (_, true) => 25, _ => 5,
    };

    [System.Text.Json.Serialization.JsonIgnore]
    public PomodoroPhase PomodoroPhase { get => _store == null ? LegacyPomodoroPhase : (PomodoroPhase)Pom.Phase; set { if (_store == null) LegacyPomodoroPhase = value; else SetPom(p => p with { Phase = (Mecho.Core.WorkPhase)value }); } }

    /// <summary>Видът мечо-доро: "25" (25/5), "52" (52/17) или "90" (90/20).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string PomodoroMode => _store == null ? LegacyPomodoroMode ?? "25" : Pom.Mode;

    [System.Text.Json.Serialization.JsonIgnore]
    public int PomodoroSeries => _store == null ? LegacyPomodoroSeries : Pom.Series;

    [System.Text.Json.Serialization.JsonIgnore]
    public int PomodoroSeriesLeft { get => _store == null ? LegacyPomodoroSeriesLeft : Pom.SeriesLeft; set { if (_store == null) LegacyPomodoroSeriesLeft = value; else SetPom(p => p with { SeriesLeft = value }); } }

    [System.Text.Json.Serialization.JsonIgnore]
    public DateTime PomodoroEndsAt { get => _store == null ? LegacyPomodoroEndsAt : Pom.EndsAt; set { if (_store == null) LegacyPomodoroEndsAt = value; else SetPom(p => p with { EndsAt = value }); } }

    [System.Text.Json.Serialization.JsonIgnore]
    public int PomodorosToday { get => _store == null ? LegacyPomodorosToday : Pom.Today; set { if (_store == null) LegacyPomodorosToday = value; else SetPom(p => p with { Today = value }); } }

    [System.Text.Json.Serialization.JsonIgnore]
    public string PomodoroDay { get => _store == null ? LegacyPomodoroDay ?? "" : Pom.Day; set { if (_store == null) LegacyPomodoroDay = value; else SetPom(p => p with { Day = value ?? "" }); } }

    [System.Text.Json.Serialization.JsonIgnore]
    public int PomodorosTotal { get => _store == null ? LegacyPomodorosTotal : Pom.Total; set { if (_store == null) LegacyPomodorosTotal = value; else SetPom(p => p with { Total = value }); } }

    /// <summary>Таймерът (MinValue = не тече).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public DateTime TimerEndsAt { get => _store == null ? LegacyTimerEndsAt : Tmr.EndsAt; set { if (_store == null) LegacyTimerEndsAt = value; else SetTmr(t => t with { EndsAt = value }); } }

    [System.Text.Json.Serialization.JsonIgnore]
    public string TimerLabel => _store == null ? LegacyTimerLabel ?? "" : Tmr.Label;

    [System.Text.Json.Serialization.JsonIgnore]
    public double TimerMinutes => _store == null ? LegacyTimerMinutes : Tmr.Minutes;

    /// <summary>Работен таймер: мечокът работи сериозно (= TimerKind "work").</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool TimerIsWork => _store == null ? LegacyTimerIsWork : Tmr.IsWork;

    /// <summary>"work" – работен, "rest" – почивка, "other" – друго.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string TimerKind { get => _store == null ? LegacyTimerKind ?? "work" : Tmr.Kind; set { if (_store == null) LegacyTimerKind = value; else SetTmr(t => t with { Kind = value ?? "work" }); } }

    /// <summary>Минути работа, още неизплатени в лешници.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public double FocusMinutesBank
    {
        get => _store?.Get<Mecho.Core.WorkBankState>().Minutes ?? LegacyFocusMinutesBank;
        set { if (_bus == null) LegacyFocusMinutesBank = value; else _bus.Send(new Mecho.Core.SetWorkBank(value)); }
    }

    // ── Задачи (вълна 4б: TasksModule) – само за четене; пише се с AddTask, SetTaskDone… ──
    private Mecho.Core.TasksState TasksNow => _store?.Get<Mecho.Core.TasksState>() ?? Mecho.Core.TasksState.Empty;

    /// <summary>Днешните задачи (и несвършените).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<TaskItem> Tasks => TasksNow.Open;

    /// <summary>Свършените от минали дни (най-новите отпред).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<TaskItem> TaskHistory => TasksNow.History;

    [System.Text.Json.Serialization.JsonIgnore]
    public int TasksDoneTotal => TasksNow.DoneTotal;

    /// <summary>Задачата, по която работим (мечо-доро/таймер).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public Guid? FocusTask
    {
        get => TasksNow.Focus;
        set => _bus?.Send(new Mecho.Core.SetFocusTask(value));
    }

    // ── Видът (вълна 5: WardrobeModule) ──
    private LooksMap? _outfit, _skins;

    internal Mecho.Core.LooksState? LooksNow => _store?.Get<Mecho.Core.LooksState>();

    internal Dictionary<string, string> LegacyMap(bool skins) => skins ? (LegacySkins ??= new()) : (LegacyOutfit ??= new());

    private Mecho.Core.LooksState Looks => LooksNow ?? Mecho.Core.LooksState.Default;

    /// <summary>Слот → облечената дреха (изглед към LooksState.Outfit).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public LooksMap Outfit => _outfit ??= new LooksMap(this, skins: false);

    /// <summary>Предмет → сложеният скин (изглед към LooksState.Skins; виж Furniture).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public LooksMap Skins => _skins ??= new LooksMap(this, skins: true);

    [System.Text.Json.Serialization.JsonIgnore]
    public string CouchSkin
    {
        get => Skins.TryGetValue(Kitchen.Couch, out var v) ? v : _store == null ? LegacyCouchSkin ?? "classic" : "classic";
        set { if (_store == null) LegacyCouchSkin = value; else Skins[Kitchen.Couch] = value; }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public string BedSkin
    {
        get => Skins.TryGetValue(Kitchen.Bed, out var v) ? v : _store == null ? LegacyBedSkin ?? "classic" : "classic";
        set { if (_store == null) LegacyBedSkin = value; else Skins[Kitchen.Bed] = value; }
    }

    /// <summary>Коя пижама облича вечер (виж Clothes.PajamaSets).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string Pajama
    {
        get => _store == null ? LegacyPajama ?? "classic" : Looks.Pajama;
        set { if (_bus == null) LegacyPajama = value; else _bus.Send(new Mecho.Core.SetLook(Pajama: value)); }
    }

    /// <summary>Пижама вечер (виж PetWindow.Furniture.cs).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool PajamasAtNight
    {
        get => _store == null ? LegacyPajamasAtNight ?? true : Looks.PajamasAtNight;
        set { if (_bus == null) LegacyPajamasAtNight = value; else _bus.Send(new Mecho.Core.SetLook(PajamasAtNight: value)); }
    }

    /// <summary>Скинът на менюто (виж UiThemes).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string UiTheme
    {
        get => _store == null ? LegacyUiTheme ?? "classic" : Looks.UiTheme;
        set { if (_bus == null) LegacyUiTheme = value; else _bus.Send(new Mecho.Core.SetLook(UiTheme: value)); }
    }

    /// <summary>Фонът на портрета (виж Backdrops).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string Background
    {
        get => _store == null ? LegacyBackground ?? "meadow" : Looks.Background;
        set { if (_bus == null) LegacyBackground = value; else _bus.Send(new Mecho.Core.SetLook(Background: value)); }
    }

    // ── Подаръкът на деня и растението (вълна 6: GiftsModule, GardenModule) ──
    private Mecho.Core.GiftState Gift => _store?.Get<Mecho.Core.GiftState>() ?? Mecho.Core.GiftState.None;
    private Mecho.Core.PlantState Plant => _store?.Get<Mecho.Core.PlantState>() ?? Mecho.Core.PlantState.Seed;
    private void SetGiftState(Func<Mecho.Core.GiftState, Mecho.Core.GiftState> f) => _bus?.Send(new Mecho.Core.SetGift(f(Gift)));
    private void SetPlantState(Func<Mecho.Core.PlantState, Mecho.Core.PlantState> f) => _bus?.Send(new Mecho.Core.SetPlant(f(Plant)));

    /// <summary>За кой ден е решено дали има подарък.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string GiftDay { get => _store == null ? LegacyGiftDay ?? "" : Gift.Day; set { if (_store == null) LegacyGiftDay = value; else SetGiftState(g => g with { Day = value ?? "" }); } }

    /// <summary>Кога тръгва за подаръка (MinValue = днес няма).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public DateTime GiftAt { get => _store == null ? LegacyGiftAt : Gift.At; set { if (_store == null) LegacyGiftAt = value; else SetGiftState(g => g with { At = value }); } }

    /// <summary>Подаръкът чака да се отвори.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool GiftPending { get => _store == null ? LegacyGiftPending : Gift.Pending; set { if (_store == null) LegacyGiftPending = value; else SetGiftState(g => g with { Pending = value }); } }

    /// <summary>Цветът на днешния подарък ("" – синият).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string GiftColor { get => _store == null ? LegacyGiftColor ?? "" : Gift.Color; set { if (_store == null) LegacyGiftColor = value; else SetGiftState(g => g with { Color = value ?? "" }); } }

    /// <summary>Растението: фаза 0–9.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public int PlantStage { get => _store == null ? LegacyPlantStage : Plant.Stage; set { if (_store == null) LegacyPlantStage = value; else SetPlantState(p => p with { Stage = value }); } }

    /// <summary>Грижа от последната фаза (на 8 мин – фаза).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public double PlantCareMinutes { get => _store == null ? LegacyPlantCareMinutes : Plant.CareMinutes; set { if (_store == null) LegacyPlantCareMinutes = value; else SetPlantState(p => p with { CareMinutes = value }); } }

    [System.Text.Json.Serialization.JsonIgnore]
    public DateTime PlantLastGrow { get => _store == null ? LegacyPlantLastGrow : Plant.LastGrow; set { if (_store == null) LegacyPlantLastGrow = value; else SetPlantState(p => p with { LastGrow = value }); } }

    [System.Text.Json.Serialization.JsonIgnore]
    public string PlantGrowDay { get => _store == null ? LegacyPlantGrowDay ?? "" : Plant.GrowDay; set { if (_store == null) LegacyPlantGrowDay = value; else SetPlantState(p => p with { GrowDay = value ?? "" }); } }

    [System.Text.Json.Serialization.JsonIgnore]
    public int PlantGrowToday { get => _store == null ? LegacyPlantGrowToday : Plant.GrowToday; set { if (_store == null) LegacyPlantGrowToday = value; else SetPlantState(p => p with { GrowToday = value }); } }

    // ── Притежаваното ──
    /// <summary>Купени уреди, мебели, скинове, тапети… (изглед към InventoryState.Items).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public InventoryList Owned => _owned ??= new InventoryList(this, clothes: false);

    /// <summary>Купени дрехи и пижами (изглед към InventoryState.Clothes).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public InventoryList OwnedClothes => _ownedClothes ??= new InventoryList(this, clothes: true);

    internal Mecho.Core.InventoryState? InventoryNow => _store?.Get<Mecho.Core.InventoryState>();

    internal Mecho.Core.SignalBus? BusNow => _bus;

    internal List<string> LegacyList(bool clothes) => clothes ? (LegacyOwnedClothes ??= new List<string>()) : (LegacyOwned ??= new List<string>());

    // ── Предмети на земята ──
    private Mecho.Core.Placement Place(string kind) => _store?.Get<Mecho.Core.PlacementState>().Get(kind) ?? Mecho.Core.Placement.None;

    private void SetPlace(string kind, Func<Mecho.Core.Placement, Mecho.Core.Placement> change) =>
        _bus?.Send(new Mecho.Core.SetPlacement(kind, change(Place(kind))));

    [System.Text.Json.Serialization.JsonIgnore]
    public bool RadioPlaced
    {
        get => _store == null ? LegacyRadioPlaced : Place(Kitchen.Radio).Placed;
        set
        {
            if (_store == null) { LegacyRadioPlaced = value; return; }
            if (value) _bus!.Send(new Mecho.Core.PlaceItem(Kitchen.Radio, Mecho.Core.PlacedBy.Player));
            else _bus!.Send(new Mecho.Core.RemoveItem(Kitchen.Radio));
        }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool RadioAuto
    {
        get => _store == null ? LegacyRadioAuto : Place(Kitchen.Radio) is { Placed: true, Owner: Mecho.Core.PlacedBy.Bear };
        set { if (_store == null) LegacyRadioAuto = value; else SetPlace(Kitchen.Radio, p => p with { Owner = value ? Mecho.Core.PlacedBy.Bear : Mecho.Core.PlacedBy.Player }); }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool RadioOn
    {
        get => _store == null ? LegacyRadioOn : Place(Kitchen.Radio).On;
        set { if (_store == null) LegacyRadioOn = value; else _bus!.Send(new Mecho.Core.SwitchItem(Kitchen.Radio, value)); }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool RadioAutoOn
    {
        get => _store == null ? LegacyRadioAutoOn : Place(Kitchen.Radio).OnByBear;
        set { if (_store == null) LegacyRadioAutoOn = value; else SetPlace(Kitchen.Radio, p => p with { OnByBear = value }); }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public double RadioX
    {
        get => _store == null ? LegacyRadioX : Place(Kitchen.Radio).X;
        set { if (_store == null) LegacyRadioX = value; else SetPlace(Kitchen.Radio, p => p with { X = value }); }
    }

    /// <summary>Печицата стои на земята (включена или не).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HeaterPlaced
    {
        get => _store == null ? LegacyHeaterOn : Place(Kitchen.Heater).Placed;
        set
        {
            if (_store == null) { LegacyHeaterOn = value; return; }
            if (value) _bus!.Send(new Mecho.Core.PlaceItem(Kitchen.Heater, Mecho.Core.PlacedBy.Player));
            else _bus!.Send(new Mecho.Core.RemoveItem(Kitchen.Heater));
        }
    }

    /// <summary>Печицата грее: сложена и включена (включиш ли я – слага се, ако я няма).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HeaterOn
    {
        get => _store == null ? LegacyHeaterOn : Place(Kitchen.Heater) is { Placed: true, On: true };
        set
        {
            if (_store == null) { LegacyHeaterOn = value; return; }
            if (value && !Place(Kitchen.Heater).Placed) _bus!.Send(new Mecho.Core.PlaceItem(Kitchen.Heater, Mecho.Core.PlacedBy.Player));
            _bus!.Send(new Mecho.Core.SwitchItem(Kitchen.Heater, value));
        }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public double HeaterX
    {
        get => _store == null ? LegacyHeaterX : Place(Kitchen.Heater).X;
        set { if (_store == null) LegacyHeaterX = value; else SetPlace(Kitchen.Heater, p => p with { X = value }); }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool LampPlaced
    {
        get => _store == null ? LegacyLampPlaced : Place(Kitchen.Lamp).Placed;
        set
        {
            if (_store == null) { LegacyLampPlaced = value; return; }
            if (value) _bus!.Send(new Mecho.Core.PlaceItem(Kitchen.Lamp, Mecho.Core.PlacedBy.Player));
            else _bus!.Send(new Mecho.Core.RemoveItem(Kitchen.Lamp));
        }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public string LampOffDay
    {
        get => _store == null ? LegacyLampOffDay ?? "" : Place(Kitchen.Lamp).OffDay;
        set { if (_store == null) LegacyLampOffDay = value; else _bus!.Send(new Mecho.Core.SetOffDay(Kitchen.Lamp, value ?? "")); }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public double LampX
    {
        get => _store == null ? LegacyLampX : Place(Kitchen.Lamp).X;
        set { if (_store == null) LegacyLampX = value; else SetPlace(Kitchen.Lamp, p => p with { X = value }); }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool FeederPlaced
    {
        get => _store == null ? LegacyFeederPlaced : Place(Kitchen.Feeder).Placed;
        set
        {
            if (_store == null) { LegacyFeederPlaced = value; return; }
            if (value) _bus!.Send(new Mecho.Core.PlaceItem(Kitchen.Feeder, Mecho.Core.PlacedBy.Player));
            else _bus!.Send(new Mecho.Core.RemoveItem(Kitchen.Feeder));
        }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public int FeederFood
    {
        get => _store == null ? LegacyFeederFood : Place(Kitchen.Feeder).Supply;
        set { if (_store == null) LegacyFeederFood = value; else _bus!.Send(new Mecho.Core.SetSupply(Kitchen.Feeder, value)); }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public double FeederX
    {
        get => _store == null ? LegacyFeederX : Place(Kitchen.Feeder).X;
        set { if (_store == null) LegacyFeederX = value; else SetPlace(Kitchen.Feeder, p => p with { X = value }); }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool PlantPlaced
    {
        get => _store == null ? LegacyPlantPlaced : Place(Kitchen.PlantPot).Placed;
        set
        {
            if (_store == null) { LegacyPlantPlaced = value; return; }
            if (value) _bus!.Send(new Mecho.Core.PlaceItem(Kitchen.PlantPot, Mecho.Core.PlacedBy.Player));
            else _bus!.Send(new Mecho.Core.RemoveItem(Kitchen.PlantPot));
        }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public double PlantX
    {
        get => _store == null ? LegacyPlantX : Place(Kitchen.PlantPot).X;
        set { if (_store == null) LegacyPlantX = value; else SetPlace(Kitchen.PlantPot, p => p with { X = value }); }
    }

    // ── Само за четене на стари записи (преди ядрото); след Bind са празни и не се записват ──
    private const System.Text.Json.Serialization.JsonIgnoreCondition IfDefault = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault;
    [System.Text.Json.Serialization.JsonPropertyName("Hazelnuts"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public int LegacyHazelnuts { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("Fullness"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public double LegacyFullness { get; set; } = 80;
    [System.Text.Json.Serialization.JsonPropertyName("Energy"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public double LegacyEnergy { get; set; } = 90;
    [System.Text.Json.Serialization.JsonPropertyName("Owned"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public List<string>? LegacyOwned { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("OwnedClothes"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public List<string>? LegacyOwnedClothes { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("RadioPlaced"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public bool LegacyRadioPlaced { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("RadioOn"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public bool LegacyRadioOn { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("RadioAuto"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public bool LegacyRadioAuto { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("RadioAutoOn"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public bool LegacyRadioAutoOn { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("RadioX")] public double LegacyRadioX { get; set; } = double.NaN;
    [System.Text.Json.Serialization.JsonPropertyName("HeaterOn"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public bool LegacyHeaterOn { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("HeaterX")] public double LegacyHeaterX { get; set; } = double.NaN;
    [System.Text.Json.Serialization.JsonPropertyName("LampPlaced"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public bool LegacyLampPlaced { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("LampOffDay"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public string? LegacyLampOffDay { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("LampX")] public double LegacyLampX { get; set; } = double.NaN;
    [System.Text.Json.Serialization.JsonPropertyName("FeederPlaced"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public bool LegacyFeederPlaced { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("FeederFood"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public int LegacyFeederFood { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("FeederX")] public double LegacyFeederX { get; set; } = double.NaN;
    [System.Text.Json.Serialization.JsonPropertyName("PlantPlaced"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public bool LegacyPlantPlaced { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("PlantX")] public double LegacyPlantX { get; set; } = double.NaN;
    [System.Text.Json.Serialization.JsonPropertyName("CookFood"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public string? LegacyCookFood { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("CookReadyAt"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public DateTime LegacyCookReadyAt { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("CookCost"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public int LegacyCookCost { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("CookStage"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public int LegacyCookStage { get; set; } = 1;
    [System.Text.Json.Serialization.JsonPropertyName("CookX")] public double LegacyCookX { get; set; } = double.NaN;
    [System.Text.Json.Serialization.JsonPropertyName("WishList"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public List<string>? LegacyWishList { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("PomodoroPhase"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public PomodoroPhase LegacyPomodoroPhase { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("PomodoroEndsAt"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public DateTime LegacyPomodoroEndsAt { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("PomodoroMode"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public string? LegacyPomodoroMode { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("PomodoroSeries"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public int LegacyPomodoroSeries { get; set; } = 1;
    [System.Text.Json.Serialization.JsonPropertyName("PomodoroSeriesLeft"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public int LegacyPomodoroSeriesLeft { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("PomodorosToday"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public int LegacyPomodorosToday { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("PomodoroDay"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public string? LegacyPomodoroDay { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("PomodorosTotal"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public int LegacyPomodorosTotal { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("TimerEndsAt"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public DateTime LegacyTimerEndsAt { get; set; } = DateTime.MinValue;
    [System.Text.Json.Serialization.JsonPropertyName("TimerLabel"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public string? LegacyTimerLabel { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("TimerMinutes"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public double LegacyTimerMinutes { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("TimerIsWork"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public bool LegacyTimerIsWork { get; set; } = true;
    [System.Text.Json.Serialization.JsonPropertyName("TimerKind"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public string? LegacyTimerKind { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("FocusMinutesBank"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public double LegacyFocusMinutesBank { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("Tasks"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public List<LegacyTaskItem>? LegacyTasks { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("TaskHistory"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public List<LegacyTaskItem>? LegacyTaskHistory { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("TasksDoneTotal"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public int LegacyTasksDoneTotal { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("FocusTask"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public Guid? LegacyFocusTask { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("Outfit"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public Dictionary<string, string>? LegacyOutfit { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("Skins"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public Dictionary<string, string>? LegacySkins { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("CouchSkin"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public string? LegacyCouchSkin { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("BedSkin"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public string? LegacyBedSkin { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("Pajama"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public string? LegacyPajama { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("PajamasAtNight"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public bool? LegacyPajamasAtNight { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("UiTheme"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public string? LegacyUiTheme { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("Background"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public string? LegacyBackground { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("GiftDay"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public string? LegacyGiftDay { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("GiftAt"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public DateTime LegacyGiftAt { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("GiftPending"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public bool LegacyGiftPending { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("GiftColor"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public string? LegacyGiftColor { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("PlantStage"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public int LegacyPlantStage { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("PlantCareMinutes"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public double LegacyPlantCareMinutes { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("PlantLastGrow"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public DateTime LegacyPlantLastGrow { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("PlantGrowDay"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public string? LegacyPlantGrowDay { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("PlantGrowToday"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public int LegacyPlantGrowToday { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("WishDay"), System.Text.Json.Serialization.JsonIgnore(Condition = IfDefault)] public string? LegacyWishDay { get; set; }

    /// <summary>Състоянието на ядрото (слоевете на модулите, виж Mecho.Core.GameStore) – пази се тук.</summary>
    public System.Text.Json.Nodes.JsonObject? Core { get; set; }
    public string WorkPlanDay { get; set; } = "";               // „Ще се работи ли днес?“ – за кой ден
    public string WorkPlan { get; set; } = "";                  // yes / light / no / later („още не знам“)
    public int WorkPlanAsks { get; set; }                       // колко пъти е питал днес

    /// <summary>
    /// Папката със записа (%AppData%\Mecho). Самопроверката и тестовете я сменят с
    /// временна (UseFolder), за да не пипат истинския прогрес; може да се смени и с
    /// променливата MECHO_DATA_DIR.
    /// </summary>
    public static string Folder => _folder ??=
        Environment.GetEnvironmentVariable("MECHO_DATA_DIR") is { Length: > 0 } dir ? dir
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mecho");

    private static string? _folder;

    /// <summary>Записът е в друга папка (самопроверка, тестове).</summary>
    public static void UseFolder(string folder)
    {
        _folder = folder;
        Directory.CreateDirectory(folder);
    }

    public static string FilePath => Path.Combine(Folder, "save.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        // X и Y са NaN, докато не се знае къде са.
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Версията на формата на записа. Вдига се при всяка промяна, за която старите
    /// записи трябва да се преправят (виж Migrate) – преименуване, смяна на тип,
    /// преместване в раздел. Нови полета с подразбиране НЕ искат нова версия.
    /// </summary>
    public const int CurrentSchema = 1;

    /// <summary>С каква версия е записан файлът (0 – от преди версиите).</summary>
    public int SchemaVersion { get; set; }

    /// <summary>Денят като ключ („2026-10-18“) – един и същ навсякъде.</summary>
    public static string DayKey(DateTime day) => day.ToString("yyyy-MM-dd");

    /// <summary>Колко пъти днес се е случило (0, ако е от друг ден).</summary>
    public int TodayCount(string name) =>
        Daily.TryGetValue(name, out var c) && c.Day == DayKey(DateTime.Today) ? c.Count : 0;

    /// <summary>Още веднъж днес (на нов ден броенето почва от 1).</summary>
    public int CountToday(string name)
    {
        string today = DayKey(DateTime.Today);
        if (!Daily.TryGetValue(name, out var c)) Daily[name] = c = new DayCounter();
        if (c.Day != today)
        {
            c.Day = today;
            c.Count = 0;
        }
        return ++c.Count;
    }

    private static string Backup(int n) => Path.Combine(Folder, $"save.bak{n}.json");

    /// <summary>
    /// Чете записа. Повреден ли е – пази го като .bad и опитва резервните копия
    /// (save.bak1, save.bak2); чак тогава почва отначало.
    /// </summary>
    public static SaveData Load()
    {
        if (!File.Exists(FilePath)) return new SaveData { SchemaVersion = CurrentSchema };
        foreach (string path in new[] { FilePath, Backup(1), Backup(2) })
        {
            try
            {
                if (!File.Exists(path)) continue;
                var data = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(path), Options);
                if (data == null) continue;
                if (path != FilePath) CrashLog.Note($"save.json беше повреден – зареден е {Path.GetFileName(path)}");
                return Migrate(data);
            }
            catch (Exception ex)
            {
                if (path == FilePath)
                {
                    try { File.Copy(FilePath, FilePath + ".bad", true); } catch (Exception) { }
                    CrashLog.Write(ex);
                }
            }
        }
        return new SaveData { SchemaVersion = CurrentSchema };
    }

    /// <summary>Преправя стар запис до сегашната версия (стъпка по стъпка).</summary>
    private static SaveData Migrate(SaveData s)
    {
        // if (s.SchemaVersion < 2) { … преместване/преименуване … }
        s.SchemaVersion = CurrentSchema;
        return s;
    }

    /// <summary>Запис от друг файл (резервното копие на Dev Mode); null, ако не става.</summary>
    public static SaveData? LoadFrom(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<SaveData>(File.ReadAllText(path), Options);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Копие на записа в друг файл (Dev Mode пази истинския прогрес там).</summary>
    public void SaveTo(string path)
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(path, JsonSerializer.Serialize(this, Options));
    }

    private static DateTime _lastBackup = DateTime.MinValue;
    private static bool _saveErrorLogged;

    public void Save()
    {
        try
        {
            LastSeen = DateTime.Now;
            SchemaVersion = CurrentSchema;
            Directory.CreateDirectory(Folder);
            // Веднъж на час: резервно копие (последните две), за да има към какво да се върне.
            if (File.Exists(FilePath) && DateTime.Now - _lastBackup > TimeSpan.FromHours(1))
            {
                _lastBackup = DateTime.Now;
                if (File.Exists(Backup(1))) File.Copy(Backup(1), Backup(2), true);
                File.Copy(FilePath, Backup(1), true);
            }
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, Options));
            File.Move(tmp, FilePath, true);
        }
        catch (Exception ex)
        {
            // Записът не е толкова важен, че да спре мечока – но се отбелязва (веднъж).
            if (!_saveErrorLogged)
            {
                _saveErrorLogged = true;
                CrashLog.Write(ex);
            }
        }
    }
}

public enum PomodoroPhase { Off, Focus, Break }

/// <summary>Брояч за един ден: в кой ден и колко пъти (виж SaveData.Daily).</summary>
public sealed class DayCounter
{
    public string Day { get; set; } = "";
    public int Count { get; set; }
}

/// <summary>Отговор от въпросника: избран бутон или написан текст.</summary>
public sealed class SurveyAnswer
{
    public string Text { get; set; } = "";
    public bool Free { get; set; }          // написан на ръка (не бутон)
    public DateTime At { get; set; }
}

/// <summary>Задача от списъка на Тут.</summary>
/// <summary>Задача в стар запис (преди ядрото). Сега задачите са Mecho.Core.TaskEntry (TaskItem).</summary>
public sealed class LegacyTaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";

    /// <summary>Какво включва (по избор): в закачения прозорец излиза при посочване.</summary>
    public string Description { get; set; } = "";

    /// <summary>1 = малка, 2 = средна, 3 = голяма.</summary>
    public int Size { get; set; } = 1;

    public bool Done { get; set; }
    public DateTime? DoneAt { get; set; }

    /// <summary>Вече е дала шанс за страница (отмяна и пак „свършено“ не дава втори).</summary>
    public bool PageRolled { get; set; }

    internal Mecho.Core.TaskEntry ToCore() => new(Id, Title ?? "", Description ?? "", Math.Clamp(Size, 1, 3), Done, DoneAt, PageRolled);
}

/// <summary>Един ден заедно: колко сме работили, какво е ял, кога е легнал… (за спомените).</summary>
public sealed class DayRecord
{
    public string Date { get; set; } = "";
    public int Pomodoros { get; set; }
    public double WorkMinutes { get; set; }
    public int TasksDone { get; set; }
    public int BigTasks { get; set; }
    public List<string> Foods { get; set; } = new();
    public bool FavFed { get; set; }
    public int UpgradesBought { get; set; }    // колко неща от „Събираме за…“ е купила днес (лимит 2–3 на ден)
    public int WishGifts { get; set; }         // колко пъти днес е върнал услугата (лешник/страница) за желано ядене
    public int Petted { get; set; }
    public bool WokeEarly { get; set; }        // Тут го събуди рано сутрин
    public bool NapWoken { get; set; }         // събуди го от дрямка
    public DateTime? BedAt { get; set; }       // кога легна за деня
    public double MinFullness { get; set; } = 100;
    public double MinEnergy { get; set; } = 100;
    public List<string> Hobbies { get; set; } = new();
    public List<string> Bought { get; set; } = new();
    public int Clothes { get; set; }           // колко пъти се преоблича
}

/// <summary>
/// Напомняне: текст, кога (дата и час) и дали се повтаря. NoteId – ако е за бележка.
/// Повтарящото се след „Готово“ минава на следващия път; еднократното изчезва.
/// </summary>
public sealed class ReminderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Text { get; set; } = "";
    public DateTime Due { get; set; }
    /// <summary>none, daily, weekdays, weekly, monthly, yearly.</summary>
    public string Repeat { get; set; } = "none";
    public Guid? NoteId { get; set; }
    public DateTime Created { get; set; } = DateTime.Now;
}

/// <summary>Бележка в тефтера на мечока.</summary>
public sealed class NoteItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Text { get; set; } = "";
    public DateTime Created { get; set; } = DateTime.Now;
}
