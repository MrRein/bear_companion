using System;
using System.Collections.Generic;
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
    public double Fullness { get; set; } = 80;
    public double Energy { get; set; } = 90;

    // Прогрес
    public string GiftDay { get; set; } = "";                  // за кой ден е решено дали има подарък
    public DateTime GiftAt { get; set; } = DateTime.MinValue;  // кога тръгва за подаръка (MinValue = днес няма)
    public bool GiftPending { get; set; }
    public string GiftColor { get; set; } = "";                // цветът на днешния подарък ("" – синият)                      // подаръкът чака в средата на екрана
    public string? CookFood { get; set; }                     // какво се готви (виж PetWindow.Cooking.cs)
    public DateTime CookReadyAt { get; set; }
    public int CookCost { get; set; }
    public int CookStage { get; set; } = 1;                     // рецепта в два уреда: 0 – първият, 1 – вторият
    public double CookX { get; set; } = double.NaN;
    public List<string> MealsDone { get; set; } = new();      // "lunch:2026-10-11", "lunch-thanks:…" (виж Meals)
    public string CouchSkin { get; set; } = "classic";
    public string BedSkin { get; set; } = "classic";
    public Dictionary<string, string> Skins { get; set; } = new(); // предмет → сложеният скин (виж Furniture)
    public bool PajamasAtNight { get; set; } = true;            // пижама вечер (виж PetWindow.Furniture.cs)
    public string Pajama { get; set; } = "classic";
    public string UiTheme { get; set; } = "classic";
    public int PlantStage { get; set; }                          // растението: фаза 0–9
    public double PlantCareMinutes { get; set; }                 // грижа от последната фаза (на 8 мин – фаза)
    public DateTime PlantLastGrow { get; set; } = DateTime.MinValue;
    public string PlantGrowDay { get; set; } = "";
    public int PlantGrowToday { get; set; }
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
    public string Background { get; set; } = "meadow";         // фонът на портрета (виж Backdrops)

    /// <summary>Офертата на деня в магазина: за кой ден е и какво има (виж DailyOffer).</summary>
    public string OfferDay { get; set; } = "";

    /// <summary>Рецептата, която се събира в момента (тайна), и колко страници има от нея.</summary>
    public string? NextRecipe { get; set; }
    public int RecipePages { get; set; }
    public List<string> OfferItems { get; set; } = new();
    public Dictionary<string, string> Outfit { get; set; } = new(); // слот → облечената дреха
    public double FocusMinutesBank { get; set; }                // минути фокус, още неизплатени в лешници
    public DateTime BerriesReadyAt { get; set; }
    public List<DateTime> CoffeeTimes { get; set; } = new();
    public int TasksDoneTotal { get; set; }
    public int PomodorosTotal { get; set; }
    public string PomodoroDay { get; set; } = "";

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
    public bool CalmWhileTyping { get; set; } = true;   // докато Тут пише – почти не се прерисува (курсорът премигваше)               // „Лягай за деня“: спи до сутринта

    /// <summary>Вечерта, в която в 22:00 енергията вече е паднала.</summary>
    public string NightDropDay { get; set; } = "";

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
    public int PomodorosToday { get; set; }

    // Мечо-доро, което тече в момента (за да продължи след рестарт)
    public PomodoroPhase PomodoroPhase { get; set; }

    /// <summary>Видът мечо-доро: "25" (25/5), "52" (52/17) или "90" (90/20).</summary>
    public string PomodoroMode { get; set; } = "25";
    public int PomodoroSeries { get; set; } = 1;        // колко мечо-дора подред (почивката е между тях)
    public int PomodoroSeriesLeft { get; set; }         // още колко след текущото
    public DateTime PomodoroEndsAt { get; set; }

    // Обикновен таймер (MinValue = не тече)
    public DateTime TimerEndsAt { get; set; } = DateTime.MinValue;
    public string TimerLabel { get; set; } = "";
    public double TimerMinutes { get; set; }
    public bool TimerIsWork { get; set; } = true;   // работен таймер: мечокът работи сериозно
    public string TimerKind { get; set; } = "work"; // "work" – работен, "rest" – почивка, "other" – друго

    public List<TaskItem> Tasks { get; set; } = new();
    public List<TaskItem> TaskHistory { get; set; } = new();   // свършените от минали дни (най-новите отпред)
    public List<NoteItem> Notes { get; set; } = new();
    public List<DayRecord> Days { get; set; } = new();          // какво сме правили всеки ден (последните 60)
    public string MemoryDay { get; set; } = "";                 // за кой ден вече каза спомена от вчера
    public string MemoryLaterDay { get; set; } = "";            // втори, по-късен спомен (следобед)
    public List<string> MemoryUsed { get; set; } = new();       // последните казани видове спомени (да не се повтарят)
    public string FavFood { get; set; } = "";                   // любимата храна за днес (виж PetWindow.Memory.cs)
    public string FavFoodDay { get; set; } = "";
    public bool FavFoodSaid { get; set; }                       // каза ли днес какво му се яде
    public Guid? FocusTask { get; set; }                         // задачата, по която работим (мечо-доро/таймер)
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
        _store = engine.Store;
        _bus = engine.Bus;
        var st = engine.Store;
        if (!st.WasLoaded(Mecho.Core.EconomyModule.WalletKey))
            _bus.Send(new Mecho.Core.SetNuts(LegacyHazelnuts, "стар запис"));
        if (!st.WasLoaded(Mecho.Core.EconomyModule.InventoryKey))
            _bus.Send(new Mecho.Core.SetInventory(LegacyOwned ?? new List<string>(), LegacyOwnedClothes ?? new List<string>(), "стар запис"));
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
        LegacyOwned = LegacyOwnedClothes = null;
        LegacyRadioPlaced = LegacyRadioOn = LegacyRadioAuto = LegacyRadioAutoOn = LegacyHeaterOn = LegacyLampPlaced = LegacyFeederPlaced = LegacyPlantPlaced = false;
        LegacyFeederFood = 0;
        LegacyLampOffDay = null;
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

    /// <summary>Печицата: „сложена“ = включена (стои на земята, докато грее).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HeaterOn
    {
        get => _store == null ? LegacyHeaterOn : Place(Kitchen.Heater).Placed;
        set
        {
            if (_store == null) { LegacyHeaterOn = value; return; }
            if (value) _bus!.Send(new Mecho.Core.PlaceItem(Kitchen.Heater, Mecho.Core.PlacedBy.Player));
            else _bus!.Send(new Mecho.Core.RemoveItem(Kitchen.Heater));
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

    /// <summary>Състоянието на ядрото (слоевете на модулите, виж Mecho.Core.GameStore) – пази се тук.</summary>
    public System.Text.Json.Nodes.JsonObject? Core { get; set; }
    public List<string> WishList { get; set; } = new();        // какво му се иска (id-та на ястия) – изпълненото се маха
    public string WishDay { get; set; } = "";                   // за кой ден е списъкът
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
public sealed class TaskItem
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

    public int Reward => Size switch { 3 => 5, 2 => 3, _ => 1 };
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

/// <summary>Бележка в тефтера на мечока.</summary>
public sealed class NoteItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Text { get; set; } = "";
    public DateTime Created { get; set; } = DateTime.Now;
}
