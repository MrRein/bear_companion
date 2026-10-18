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
    public int Hazelnuts { get; set; }
    public List<string> Owned { get; set; } = new();            // купени уреди и подобрения
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
    public bool FeederPlaced { get; set; }                       // хранилката за птички е сложена
    public int FeederFood { get; set; }                          // порции семенца (0–3)
    public double FeederX { get; set; } = double.NaN;
    public bool RadioPlaced { get; set; }                        // радиото е сложено (от „Сложи“)
    public bool RadioOn { get; set; }
    public double RadioX { get; set; } = double.NaN;
    public bool LampPlaced { get; set; }                        // Тут сложи лампата (свети, докато не я прибере)
    public bool LampOff { get; set; }                           // Тут изгаси лампата тази вечер (до сутринта)
    public string LampOffDay { get; set; } = "";
    public double LampX { get; set; } = double.NaN;
    public int BearScale { get; set; }                          // размер на мечока (0 = по подразбиране)
    public string Background { get; set; } = "meadow";         // фонът на портрета (виж Backdrops)
    public List<string> OwnedClothes { get; set; } = new();     // купени дрехи (виж Clothes)

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
    public bool HeaterOn { get; set; }
    public double HeaterX { get; set; } = double.NaN;
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

    public static string Folder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mecho");

    public static string FilePath => Path.Combine(Folder, "save.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        // X и Y са NaN, докато не се знае къде са.
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new JsonStringEnumConverter() },
    };

    public static SaveData Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<SaveData>(File.ReadAllText(FilePath), Options) ?? new SaveData();
        }
        catch (Exception)
        {
            // Повреден файл: започваме наново, но пазим стария за всеки случай.
            try { File.Copy(FilePath, FilePath + ".bad", true); } catch (Exception) { }
        }
        return new SaveData();
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

    public void Save()
    {
        try
        {
            LastSeen = DateTime.Now;
            Directory.CreateDirectory(Folder);
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, Options));
            File.Move(tmp, FilePath, true);
        }
        catch (Exception)
        {
            // Записът не е толкова важен, че да спре мечока.
        }
    }
}

public enum PomodoroPhase { Off, Focus, Break }

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
