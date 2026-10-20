using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Mecho;

// Грижа и работа: глад, енергия, храна, задачи и мечо-доро.
public sealed partial class PetWindow
{
    // Колко бързо се променят нуждите (точки на час).
    private const double HungerAwake = 22;     // сит → гладен за ~3,5 часа
    private const double HungerAsleep = 4;
    private const double RestAsleep = 240;     // от 20 до 100 за ~20 минути дрямка (с легло: 10)

    private double RestRate => RestAsleep * (Kitchen.Owns(_save, Kitchen.Bed) ? 2 : 1);

    /// <summary>Много е уморен: ходи бавно и не му се играе.</summary>
    /// <summary>Уморен: под толкова енергия ходи и стои уморено.</summary>
    public const double TiredBelow = 35;

    public bool IsExhausted => _save.Energy < TiredBelow;

    /// <summary>Видовете мечо-доро: работа и почивка в минути.</summary>
    public static readonly (string Id, string Name, int Focus, int Break, string Hint)[] PomodoroModes =
    {
        ("25", "25 / 5", 25, 5, "Класическото: 25 минути работа, 5 почивка."),
        ("52", "52 / 17", 52, 17, "Дълбок фокус: 52 минути съсредоточена работа, после 17 минути почивка."),
        ("90", "90 / 20", 90, 20, "Голям блок по естествения цикъл на енергията: 90 минути работа, 20 почивка."),
    };

    public (string Id, string Name, int Focus, int Break, string Hint) PomodoroMode =>
        PomodoroModes.FirstOrDefault(m => m.Id == _save.PomodoroMode) is { Id: not null } m ? m : PomodoroModes[0];

    public TimeSpan FocusLength => TimeSpan.FromMinutes(PomodoroMode.Focus);
    public TimeSpan BreakLength => TimeSpan.FromMinutes(PomodoroMode.Break);

    /// <summary>Сменя вида мечо-доро (само докато не тече).</summary>
    public void SetPomodoroMode(string id)
    {
        if (PomodoroModes.FirstOrDefault(m => m.Id == id) is not { Id: not null } mode) return;
        if (!CoreEngine.Bus.Send(new Mecho.Core.SetPomodoroMode(mode.Id, mode.Focus, mode.Break)).Ok) return;
        Persist();
        NotifyCare();
    }

    private double _nextNeedLine;

    // Табелката над мечока: мед – работа/таймер; зелена – почивка между мечо-дората.
    private static readonly Brush TagWork = Frozen(Color.FromRgb(255, 214, 102));
    private static readonly Brush TagBreak = Frozen(Color.FromRgb(140, 205, 120));

    private static Brush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    /// <summary>Колко мечо-дора подред (1–4); сменя се, докато не тече.</summary>
    public void SetPomodoroSeries(int count)
    {
        if (!CoreEngine.Bus.Send(new Mecho.Core.SetPomodoroSeries(count)).Ok) return;
        Persist();
        NotifyCare();
    }

    /// <summary>Кое поред е текущото мечо-доро (1-во от 4 …).</summary>
    public int PomodoroNumber => _save.PomodoroSeries - _save.PomodoroSeriesLeft;
    private readonly Border _timerTag = new();
    private readonly TextBlock _timerText = new();
    private readonly StackPanel _timerRow = new() { Orientation = Orientation.Horizontal };

    public SaveData Save => _save;

    /// <summary>Вика се, когато нещо в грижата се промени (за панела).</summary>
    public event EventHandler? CareChanged;

    private void NotifyCare()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        CareChanged?.Invoke(this, EventArgs.Empty);
        if (watch.Elapsed.TotalMilliseconds > 3) PerfNote("известие към менюто и закачените прозорци", watch.Elapsed.TotalMilliseconds);
    }

    private void InitCare()
    {
        _timerText.FontFamily = Ui.GameFont;
        _timerText.FontWeight = FontWeights.Normal;
        _timerText.FontSize = Ui.Body;
        _timerText.FontFamily = Ui.MonoFont;
        _timerText.Foreground = Ui.Ink;
        _timerTag.Child = _timerRow;
        _timerTag.Background = TagWork;
        _timerTag.BorderBrush = Ui.Ink;
        _timerTag.BorderThickness = new Thickness(2);
        _timerTag.CornerRadius = new CornerRadius(3);
        _timerTag.Padding = new Thickness(5, 0, 5, 1);
        _timerTag.IsHitTestVisible = false;
        _timerTag.Visibility = Visibility.Collapsed;
        _root.Children.Add(_timerTag);
        InitEmotes();

        // Колко го е нямало (за поздрава при пускане – виж OnStart).
        _startAway = _save.LastSeen == default ? 999 : Math.Max(0, (DateTime.Now - _save.LastSeen).TotalHours);
        _startLastSeen = _save.LastSeen;
        ApplyOfflineTime();
        MorningRefresh(fromStart: true);
        // Отпреди гардеробът да се купува: който вече има дрехи или тапети, го има.
        if (!Kitchen.Owns(_save, Kitchen.Wardrobe) && (_save.OwnedClothes.Count > 0 || _save.Owned.Any(o => o.StartsWith("bg_"))))
            _save.Owned.Add(Kitchen.Wardrobe);
        StartNewDay();
    }

    /// <summary>
    /// Докато програмата е спряна, мечокът почива (компютърът спи) и огладнява
    /// бавно. Никога не се връща умрял от глад: най-много „огладнял“.
    /// </summary>
    private void ApplyOfflineTime()
    {
        if (_save.LastSeen == default) return;
        double hours = Math.Max(0, (DateTime.Now - _save.LastSeen).TotalHours);
        if (hours < 0.05) return;
        // Компютърът е бил спрян поне 2 часа: наспал се е.
        if (hours >= 2) _save.Energy = Math.Min(100, _save.Energy + hours * RestRate);
        // Вечер не става бодър само защото компютърът е бил спрян.
        _save.Energy = Math.Min(_save.Energy, EveningCap(DateTime.Now));
        _save.Fullness = Math.Max(Math.Min(_save.Fullness, 30), _save.Fullness - hours * HungerAsleep);
    }

    /// <summary>
    /// Сутрин (5–12 ч.) е бодър, все едно е спал: веднъж на ден енергията е 100.
    /// При пускане на програмата – веднага; ако е стояла пусната цяла нощ – само
    /// ако компютърът не е пипан поне 3 часа (иначе Тут е будувала с него).
    /// </summary>
    private void MorningRefresh(bool fromStart)
    {
        var now = DateTime.Now;
        string today = now.ToString("yyyy-MM-dd");
        if (_save.MorningRefill == today || now.Hour < 5 || now.Hour >= 12) return;
        if (!fromStart && NativeMethods.IdleSeconds() < 3 * 3600) return;
        _save.MorningRefill = today;
        _save.Energy = MorningEnergy();
        _save.WokeAt = DateTime.Now;
    }

    /// <summary>
    /// След 23:00 (нощната птица – след полунощ): много голям шанс (85%) да капне от умора –
    /// енергията пада до 10–18 (веднъж на вечер). Иначе само до 30 (както в 22:00).
    /// </summary>
    private void LateNightDrop()
    {
        var now = DateTime.Now;
        bool owl = Today == DayKind.NightOwl;
        bool late = owl ? now.Hour < 5 : now.Hour >= 23 || now.Hour < 5;
        if (!late || IsAsleep) return;
        string key = (now.Hour < 5 ? now.Date.AddDays(-1) : now.Date).ToString("yyyy-MM-dd");
        if (_save.LateDropDay == key) return;
        _save.LateDropDay = key;
        if (_rng.NextDouble() >= 0.85) return;
        double to = 10 + _rng.Next(9);
        if (_save.Energy <= to) return;
        _save.Energy = to;
        if (!IsQuiet && !InFocus) Say(Lines.Pick(Lines.Bedtime, _save.OwnerName), 6);
        Emote("zzz", 4);
    }

    /// <summary>В 22:00 енергията пада рязко (веднъж на вечер): доспива му се.</summary>
    private void NightDrop()
    {
        var now = DateTime.Now;
        if (now.Hour < (Today == DayKind.NightOwl ? 23 : 22) || IsAsleep) return;
        string today = now.ToString("yyyy-MM-dd");
        if (_save.NightDropDay == today) return;
        _save.NightDropDay = today;
        if (_save.Energy > 30)
        {
            _save.Energy = 30;
            if (!IsQuiet && !InFocus) Say(Lines.Pick(Lines.Bedtime, _save.OwnerName), 5);
        }
    }

    /// <summary>Нов ден: свършените вчера задачи се махат, броячът на мечо-дора се нулира.</summary>
    private void StartNewDay()
    {
        string today = DateTime.Today.ToString("yyyy-MM-dd");
        if (_save.PomodoroDay != today)
        {
            _save.PomodoroDay = today;
            _save.PomodorosToday = 0;
        }
        // Свършените от минали дни отиват в историята (виж „Задачи“ → „История“; ядрото го прави и само в полунощ).
        CoreEngine.Bus.Send(new Mecho.Core.ArchiveDoneTasks());
    }

    private bool IsResting => IsAsleep || _anim == "read_sleep";

    /// <summary>Докъде най-много стига енергията в този час: от 17 ч. надолу, в 22 ч. – 35.</summary>
    internal static double EveningCap(DateTime t)
    {
        double h = t.TimeOfDay.TotalHours;
        if (h >= 5 && h < 17) return 100;
        if (h >= 17 && h < 19) return 85;
        if (h >= 19 && h < 21) return 65;
        if (h >= 21 && h < 22) return 50;
        return 35;   // 22:00–5:00
    }

    private void UpdateCare(double dt, double now)
    {
        double h = dt / 3600;
        // Гладът спада в ядрото (NeedsModule – по-бавно в съня, × модификатора на събитията).
        // Умора и почивка (виж PetWindow.Energy.cs).
        EnsureDayKind();
        double energyBefore = _save.Energy;
        UpdateEnergy(dt, now);
        // Вечерна разходка-дрямка: навън спи под някое дърво – енергията се вдига като в съня.
        bool walkNap = _save.WalkNap && OnWalk && !_walkingOut;
        if (walkNap) _save.Energy = Math.Min(100, energyBefore + RestRate * h);
        // Вечер енергията бавно слиза към тавана за часа (вечер е уморен, каквото и да е било).
        double cap = EveningCap(DateTime.Now);
        if (!IsAsleep && !walkNap && _save.Energy > cap) _save.Energy = Math.Max(cap, _save.Energy - 30 * h);
        MorningRefresh(fromStart: false);
        NightDrop();
        LateNightDrop();

        UpdatePomodoro();
        UpdateTimer();
        TellLearnedRecipe();
        UpdateMeals();
        AskAboutLunch();

        // Казва колко е гладен или уморен: 6 нива, колкото е по-зле, толкова по-често.
        if (!IsResting && !IsQuiet && !InFocus && _state != BearState.Drag)
        {
            if (now > _nextNeedLine && NeedLevel(_save.Fullness) is int hl)
            {
                _nextNeedLine = now + NeedPause(hl);
                SayNeed("hunger", hl);
            }
            else if (now > _nextTiredLine && NeedLevel(_save.Energy) is int tl)
            {
                _nextTiredLine = now + NeedPause(tl);
                if (LateNight && tl <= 3) Say(Lines.Pick(Lines.Bedtime, _save.OwnerName), 5);
                else SayNeed("tired", tl);
            }
        }
    }

    private double _nextTiredLine;

    /// <summary>
    /// Нивото на нуждата: 1 – страшно (0–20), 2 – много (21–35), 3 – доста (36–50),
    /// 4 – гладен/уморен (51–65), 5 – малко (66–80), 6 – леко (81–95); над 95 – нищо.
    /// </summary>
    internal static int? NeedLevel(double value) => value switch
    {
        <= 20 => 1,
        <= 35 => 2,
        <= 50 => 3,
        <= 65 => 4,
        <= 80 => 5,
        <= 95 => 6,
        _ => null,
    };

    /// <summary>След колко секунди пак може да го каже (колкото по-зле, толкова по-често).</summary>
    private double NeedPause(int level) => level switch
    {
        1 => Minutes(10, 16),
        2 => Minutes(16, 26),
        3 => Minutes(26, 40),
        4 => Minutes(45, 70),
        5 => Minutes(70, 110),
        _ => Minutes(120, 200),
    };

    /// <summary>Казва нещо за глада/умората (на ниво 6 – почти никога; понякога само емоция).</summary>
    private void SayNeed(string kind, int level)
    {
        if (kind == "hunger" && FoodComing) return;   // вече се готви или храната го чака
        // Леките нива – почти никога (иначе е досадно): 6 – 1 от 10, 5 – 1 от 5, 4 – 1 от 2.
        if (level == 6 && _rng.Next(10) != 0) return;
        if (level == 5 && _rng.Next(5) != 0) return;
        if (level == 4 && _rng.Next(2) != 0) return;
        if (level <= 3 && _rng.Next(4) == 0)
        {
            Emote(kind == "hunger" ? "hungry" : "zzz", 3.5);
            return;
        }
        // Гладен по време на обяд/вечеря: репликите за обяда.
        var meal = CurrentMeal;
        if (kind == "hunger" && level <= 2 && meal != null)
        {
            Say(Lines.Pick(meal == "lunch" ? Lines.LunchTime : Lines.DinnerTime, _save.OwnerName), 5);
            return;
        }
        string line = R.Pick($"need.{kind}.{level}", _save.OwnerName);
        if (level >= 5) Mutter(line, 4);
        else Say(line, level <= 2 ? 5 : 4);
    }

    // ───────────────────────── Нощ ─────────────────────────

    /// <summary>Късно е (след 22:00): иска да спи.</summary>
    private static bool LateNight => DateTime.Now.Hour >= 22 || DateTime.Now.Hour < 6;


    // ───────────────────────── Обяд и вечеря ─────────────────────────

    /// <summary>Обяд 11:40–13:30, вечеря 18:30–20:00 (иначе null).</summary>
    private static string? CurrentMeal
    {
        get
        {
            var t = DateTime.Now.TimeOfDay;
            if (t >= new TimeSpan(11, 40, 0) && t <= new TimeSpan(13, 30, 0)) return "lunch";
            if (t >= new TimeSpan(18, 30, 0) && t <= new TimeSpan(20, 0, 0)) return "dinner";
            return null;
        }
    }

    private bool MealDone(string key) => _save.MealsDone.Contains($"{key}:{DateTime.Today:yyyy-MM-dd}");

    private void MarkMeal(string key)
    {
        string today = DateTime.Today.ToString("yyyy-MM-dd");
        _save.MealsDone.RemoveAll(m => !m.EndsWith(today));
        _save.MealsDone.Add($"{key}:{today}");
    }

    /// <summary>Стане ли време за обяд или вечеря, огладнява рязко (веднъж на ден всяко).</summary>
    private void UpdateMeals()
    {
        var meal = CurrentMeal;
        if (meal == null || MealDone(meal)) return;
        MarkMeal(meal);
        _save.Fullness = Math.Min(_save.Fullness, 22);
        _nextNeedLine = Now + 600 + _rng.Next(300);
        // Обяд: пита Тут дали тя няма да обядва (щом не работите и не спи).
        if (meal == "lunch") _lunchAskPending = true;
        else if (!IsResting && !InFocus && !IsQuiet)
            Say(Lines.Pick(Lines.DinnerTime, _save.OwnerName), 5);
        NotifyCare();
    }

    private bool _lunchAskPending;
    private double _lunchAskUntil;

    /// <summary>„Ти няма ли да обядваш?“ – цъкнеш ли го, докато е балончето, има отговори.</summary>
    private void AskAboutLunch()
    {
        if (!_lunchAskPending || CurrentMeal != "lunch" || IsResting || InFocus || IsQuiet || _state == BearState.Drag) return;
        _lunchAskPending = false;
        Say(Lines.Fill(R.Any("lunch.ask"), _save.OwnerName), 10);
        _lunchAskUntil = Now + 120;
    }

    private bool TakeLunchQuestion()
    {
        if (Now > _lunchAskUntil) return false;
        _lunchAskUntil = 0;
        Ask(Conversations.Lunch());
        return true;
    }

    /// <summary>След като е ял по време на обяд/вечеря: благодари (веднъж на ден за всяко).</summary>
    private bool ThankForMeal()
    {
        var meal = CurrentMeal;
        if (meal == null || MealDone(meal + "-thanks")) return false;
        MarkMeal(meal + "-thanks");
        Say(Lines.Pick(meal == "lunch" ? Lines.LunchThanks : Lines.DinnerThanks, _save.OwnerName), 5);
        return true;
    }

    // ───────────────────────── Храна ─────────────────────────

    public void Pet()
    {
        if (BusyEating()) return;
        if (IsResting)
        {
            Say(R.Any("sleep.petted"), 3);
            return;
        }
        Say(Lines.Pick(Lines.Petted, _save.OwnerName), 3);
        Emote("heart");
        TodayRec.Petted++;
        Joy(JoyPetted);
        PetEnergy();
        React("happy");
    }

    /// <summary>Може ли да пусне анимация, без да прекъсне дивана, влаченето или съня.</summary>
    private bool CanAnimateFreely => !OnCouchMission && !_onTrampoline && !IsAsleep && _state is not (BearState.Drag or BearState.Falling or BearState.Jump or BearState.Bounce);

    /// <summary>
    /// Тръгнал е по задача (отива някъде с цел, носи нещо, изнася саксията…): кратка
    /// реакция с Play би изтрила следващата стъпка (_afterAction) и би го оставила
    /// „заседнал“ – затова тогава реагира само с емоция над главата.
    /// </summary>
    private bool OnErrand => _afterAction != null || _walkingOut || _walkingBack || _peeking || _plantPushing
        || _giftRun != GiftRun.None || _pendingCook != null || _closet != null || _trip != null || _anchorWalk;

    /// <summary>Кратка реакция (радост, танц) – само ако е свободен; иначе емоция, без да го прекъсва.</summary>
    private void React(string anim, double length = 0)
    {
        if (CanAnimateFreely && !OnErrand && !_inScene && _state is BearState.Idle or BearState.Walk)
            Play(anim, length: length);
        else if (!IsAsleep)
            Emote(anim == "dance" ? "sparkle" : "happy", 2);
    }

    // ───────────────────────── Задачи ─────────────────────────

    public void AddTask(string title, int size, string? description = null)
    {
        if (!CoreEngine.Bus.Send(new Mecho.Core.AddTask(title, size, description)).Ok) return;
        Say(Lines.Pick(Lines.TaskAdded, _save.OwnerName), 3);
        NotifyCare();
        Persist();
    }

    /// <summary>Поправя името, описанието и трудността (✏ в менюто).</summary>
    public void EditTask(TaskItem task, string title, int size, string? description)
    {
        if (!CoreEngine.Bus.Send(new Mecho.Core.EditTask(task.Id, title, size, description)).Ok) return;
        NotifyCare();
        Persist();
    }

    /// <summary>Подредбата на задачите: 0 най-нови, 1 най-стари, 2 по големина (големите горе), 3 по големина (малките горе).</summary>
    public int TasksSort => _save.TasksSort is >= 0 and <= 3 ? _save.TasksSort : _save.TasksOldestFirst ? 1 : 0;

    /// <summary>Несвършените задачи в избрания ред: тези в таблото – най-горе, после по подредбата.</summary>
    public System.Collections.Generic.List<TaskItem> OrderedTasks(bool boardOnly = false)
    {
        var open = _save.Tasks.Where(t => !t.Done && (!boardOnly || TaskOnBoard(t))).ToList();
        int sort = TasksSort;
        if (sort != 1) open.Reverse();   // в ядрото новите са отзад; при еднаква големина – най-новите горе
        var byBoard = open.OrderBy(t => TaskOnBoard(t) ? 0 : 1);
        return (sort switch
        {
            2 => byBoard.ThenByDescending(t => t.Size),
            3 => byBoard.ThenBy(t => t.Size),
            _ => byBoard,
        }).ToList();
    }

    public void SetTasksSort(int sort)
    {
        _save.TasksSort = ((sort % 4) + 4) % 4;
        NotifyCare();
        Persist();
    }

    /// <summary>Бележките в избрания ред: закачените – най-горе, после най-новите (или най-старите).</summary>
    public System.Collections.Generic.List<NoteItem> OrderedNotes(bool pinnedOnly = false) =>
        (_save.NotesOldestFirst ? _save.Notes.OrderBy(n => n.Created) : _save.Notes.OrderByDescending(n => n.Created))
            .Where(n => !pinnedOnly || n.Pinned)
            .OrderBy(n => n.Pinned ? 0 : 1)
            .ToList();

    public void SetTasksOldestFirst(bool on) => SetTasksSort(on ? 1 : 0);

    public void SetNotesOldestFirst(bool on)
    {
        _save.NotesOldestFirst = on;
        NotifyCare();
        Persist();
    }

    public void SetTaskDone(TaskItem task, bool done)
    {
        // Наградата, брояча и историята ги пази ядрото (TasksModule); тук – само реакцията.
        if (_save.Tasks.FirstOrDefault(t => t.Id == task.Id) is not { } cur || cur.Done == done) return;
        bool firstBig = done && cur.Size >= 3 && !cur.PageRolled;
        if (!CoreEngine.Bus.Send(new Mecho.Core.SetTaskDone(cur.Id, done)).Ok) return;
        task = cur;
        RecordTask(task, done);

        if (!done)
        {
            NotifyCare();
            Persist();
            return;
        }

        // Трудна задача: малък шанс за страница (веднъж на задача).
        string page = firstBig && _rng.Next(4) == 0 ? FindRecipePage() : "";

        Joy(JoyWorked);
        if (task.Size >= 3) Pride();
        Say(Lines.Pick(task.Size switch { 3 => Lines.BigTaskDone, 2 => Lines.TaskDone, _ => Lines.SmallTaskDone }, _save.OwnerName) +
            $" +{task.Reward} 🌰" + page, 4);

        React(task.Size >= 3 ? "dance" : "happy", task.Size >= 3 ? 3 : 0);
        Emote("sparkle");
        NotifyCare();
        Persist();
    }

    public void DeleteTask(TaskItem task)
    {
        CoreEngine.Bus.Send(new Mecho.Core.DeleteTask(task.Id));
        NotifyCare();
        Persist();
    }

    // ───────────────────────── Бележки ─────────────────────────

    public NoteItem? AddNote(string text, bool quiet = false) => AddNote("", text, pinned: true, quiet);

    public NoteItem? AddNote(string title, string text, bool pinned, bool quiet = false)
    {
        title = title.Trim();
        text = text.Trim();
        if (text.Length == 0 && title.Length == 0) return null;
        var note = new NoteItem { Title = title, Text = text, Pinned = pinned };
        _save.Notes.Insert(0, note);
        if (!quiet) Say(Lines.Pick(Lines.NoteAdded, _save.OwnerName), 3);
        NotifyCare();
        Persist();
        return note;
    }

    /// <summary>В таблото със задачите ли е задачата (по подразбиране – да).</summary>
    public bool TaskOnBoard(TaskItem task) => !_save.TasksOffBoard.Contains(task.Id);

    public void SetTaskOnBoard(TaskItem task, bool on)
    {
        _save.TasksOffBoard.Remove(task.Id);
        if (!on) _save.TasksOffBoard.Add(task.Id);
        NotifyCare();
        Persist();
    }

    /// <summary>Поправя бележката (✏): име, съдържание, табло; напомнянето ѝ взима новото име.</summary>
    public void EditNote(NoteItem note, string title, string text, bool pinned)
    {
        title = title.Trim();
        text = text.Trim();
        if (title.Length == 0 && text.Length == 0) return;
        note.Title = title;
        note.Text = text;
        note.Pinned = pinned;
        foreach (var r in _save.Reminders.Where(r => r.NoteId == note.Id)) r.Text = note.Label;
        NotifyCare();
        Persist();
    }

    /// <summary>В закаченото табло ли е бележката.</summary>
    public void SetNotePinned(NoteItem note, bool pinned)
    {
        note.Pinned = pinned;
        NotifyCare();
        Persist();
    }

    /// <summary>Изтрива я: отива в „История“ (най-много 200), откъдето може да се върне.</summary>
    public void DeleteNote(NoteItem note)
    {
        _save.Notes.Remove(note);
        _save.Reminders.RemoveAll(r => r.NoteId == note.Id);   // напомнянето е част от бележката
        note.Deleted = DateTime.Now;
        _save.NoteHistory.Add(note);
        if (_save.NoteHistory.Count > 200) _save.NoteHistory.RemoveRange(0, _save.NoteHistory.Count - 200);
        NotifyCare();
        Persist();
    }

    /// <summary>Връща изтрита бележка (най-горе при бележките).</summary>
    public void RestoreNote(NoteItem note)
    {
        if (!_save.NoteHistory.Remove(note)) return;
        note.Deleted = null;
        _save.Notes.Insert(0, note);
        NotifyCare();
        Persist();
    }

    /// <summary>Изтрива я завинаги (от „История“).</summary>
    public void PurgeNote(NoteItem note)
    {
        _save.NoteHistory.Remove(note);
        NotifyCare();
        Persist();
    }

    // ───────────────────────── Мечо-доро ─────────────────────────

    public bool PomodoroFocus => _save.PomodoroPhase == PomodoroPhase.Focus;

    /// <summary>Работен таймер тече (не таймер за чай и пране).</summary>
    public bool WorkTimer => TimerRunning && _save.TimerIsWork;

    /// <summary>Работим: мечо-доро или работен таймер. Тогава мечокът е сериозен и мълчи.</summary>
    public bool InFocus => PomodoroFocus || WorkTimer;

    /// <summary>Сяда на бюрото и работи сериозно, докато трае работата.</summary>
    private void EnterWorkMode()
    {
        if (_scene == Scenes.Focus || IsAsleep || _state is BearState.Drag or BearState.Falling or BearState.Jump || BusyEating(say: false)) return;
        _save.SleepingByChoice = false;
        _save.StayPut = false;
        StartScene(Scenes.Focus, quiet: false);
        if (IsExhausted) Say(Lines.Pick(Lines.WorkTired, _save.OwnerName), 4);
        _nextFocusSwitch = Now + 15 + _rng.Next(15);
    }

    /// <summary>Работата свърши: става от бюрото.</summary>
    private void LeaveWorkMode()
    {
        if (_scene == Scenes.Focus) LeaveScene();
    }

    private double _nextFocusSwitch;

    /// <summary>
    /// В работния режим сменя заниманията си: пише, мисли, записва. Ако се умори,
    /// клюма над лаптопа, а ако съвсем капне, отива да дремне.
    /// </summary>
    private void UpdateFocusScene(double now)
    {
        if (CheckFoodWhileWorking()) return;
        if (IsExhausted)
        {
            if (_anim != "focus_tired")
            {
                _anim = "focus_tired";
                _animTime = 0;
                Say(Lines.Pick(Lines.WorkTired, _save.OwnerName), 4);
            }
            return;
        }
        if (now < _nextFocusSwitch) return;
        _nextFocusSwitch = now + 45 + _rng.Next(75);
        // Работи: мисли, пише в тефтера, чете (ненарисуваните още – прескача).
        var anims = new[] { "focus", "focus", "think", "focus_write", "write_sit", "read" }.Where(_lib.Has).ToArray();
        string next = anims[_rng.Next(anims.Length)];
        if (next == "think") Emote(_rng.Next(3) == 0 ? "idea" : "think", 3);
        if (next != _anim)
        {
            _anim = next;
            _animTime = 0;
        }
    }

    public TimeSpan PomodoroLeft =>
        _save.PomodoroPhase == PomodoroPhase.Off ? TimeSpan.Zero : Max(TimeSpan.Zero, _save.PomodoroEndsAt - DateTime.Now);

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

    /// <summary>Тут пуска мечо-доро (поредицата почва отначало).</summary>
    public void StartFocus()
    {
        NoteWorkStarted();
        if (!CoreEngine.Bus.Send(new Mecho.Core.StartPomodoro(PomodoroMode.Focus, PomodoroMode.Break, NewSeries: true)).Ok) return;
        FocusBegan();
    }

    /// <summary>
    /// Връзката с ядрото (WorkModule): то засича края на работата, почивката и
    /// таймера, а мечокът тук само реагира. Вика се веднъж от конструктора.
    /// </summary>
    private void WireWork()
    {
        var bus = CoreEngine.Bus;
        bus.Subscribe<Mecho.Core.PomodoroFocusDone>(this, _ => FocusDone());
        bus.Subscribe<Mecho.Core.PomodoroBreakDone>(this, s => BreakDone(s.NextFocus));
        bus.Subscribe<Mecho.Core.WorkTimerFinished>(this, s => TimerDone(s.Minutes, s.Label, s.Kind));
        bus.Subscribe<Mecho.Core.PlantGrew>(this, PlantGrew);
    }

    /// <summary>Работата почна (Тут я пусна или е следващата от поредицата).</summary>
    private void FocusBegan()
    {
        // Ако е уморен, EnterWorkMode казва „ти поработи, аз ще посънча“ и той дрямва.
        EndBreakRelax();
        // Казва и колко лешника ще донесе (само тук: тогава наистина ги получаваш).
        if (!IsExhausted)
        {
            string start = FocusTask is { } ft ? R.Pick("focustask.start", _save.OwnerName).Replace("{задача}", ft.Title) : WorkStartLine();
            Say(start + " " + R.Pick("focus.nuts", _save.OwnerName).Replace("{число}", PomodoroNuts.ToString()), 5);
        }
        EnterWorkMode();
        MaybeFlow();
        OpenParcelFirst();
        NotifyCare();
        Persist();
    }

    /// <summary>
    /// Плаща работата: за всеки 10 минути мечо-доро или таймер по 1 лешник.
    /// Остатъкът под 10 минути се пази за следващия път.
    /// </summary>
    private int PayForMinutes(double minutes)
    {
        if (minutes <= 0) return 0;
        int nuts = Mecho.Core.WorkModule.NutsFor(_save.FocusMinutesBank, minutes);
        return CoreEngine.Bus.Send(new Mecho.Core.PayWork(minutes)).Ok ? nuts : 0;
    }

    private static string Earned(int nuts) => nuts > 0 ? $" +{nuts} 🌰" : "";

    /// <summary>Колко лешника ще донесат толкова минути работа (с остатъка от миналия път).</summary>
    public int NutsFor(double minutes) => Mecho.Core.WorkModule.NutsFor(_save.FocusMinutesBank, minutes);

    /// <summary>Колко лешника носи едно мечо-доро от избрания вид (със слушалките +1).</summary>
    public int PomodoroNuts => NutsFor(FocusLength.TotalMinutes) + (Kitchen.Owns(_save, Kitchen.Headphones) ? 1 : 0);

    public void StopPomodoro()
    {
        if (_save.PomodoroPhase == PomodoroPhase.Off) return;
        bool wasFocus = InFocus;
        int nuts = wasFocus ? PayForMinutes((FocusLength - PomodoroLeft).TotalMinutes) : 0;
        if (wasFocus && PomodoroFocus) RecordWork((FocusLength - PomodoroLeft).TotalMinutes);
        CoreEngine.Bus.Send(new Mecho.Core.StopPomodoro());
        EndBreakRelax();
        if (!InFocus) LeaveWorkMode();
        Say((wasFocus ? "Добре, спираме. Ще продължим после." : "Почивката свърши по-рано. Хайде!") + Earned(nuts), 3);
        NotifyCare();
        Persist();
    }

    /// <summary>Пита ядрото дали нещо е свършило (то и само проверява всяка секунда).</summary>
    private void UpdatePomodoro() => CoreEngine.Bus.Send(new Mecho.Core.CheckWork());

    /// <summary>Работата свърши (ядрото вече е преброило мечо-дорото и е пуснало почивката).</summary>
    private void FocusDone()
    {
        StartNewDay();
        RecordPomodoro(FocusLength.TotalMinutes);
        int nuts = PayForMinutes(FocusLength.TotalMinutes) + LazyBonus();
        if (Kitchen.Owns(_save, Kitchen.Headphones))
        {
            _save.Hazelnuts++;
            nuts++;
        }
        // Понякога (1 на 5) – страница от готварската книга.
        string page = _rng.Next(5) == 0 ? FindRecipePage() : "";
        Joy(JoyWorked);
        if (_save.PomodorosToday % 3 == 0) Pride();
        Say(Lines.Pick(Lines.FocusDone, _save.OwnerName) + Earned(nuts) + page, 8);
        Sounds.Play(_save);
        Emote("happy", 3);
        AskAboutFocusTask();
        if (!InFocus) StartBreakRelax();
        NotifyCare();
        Persist();
    }

    /// <summary>Почивката свърши. next – ядрото вече пусна следващото от поредицата.</summary>
    private void BreakDone(bool next)
    {
        EndBreakRelax();
        if (next) FocusBegan();
        else
        {
            Say(Lines.Pick(Lines.BreakDone, _save.OwnerName), 6);
            Sounds.Play(_save);
            React("happy");
        }
        NotifyCare();
        Persist();
    }

    // ───────────────────────── Таймер ─────────────────────────

    public bool TimerRunning => _save.TimerEndsAt != DateTime.MinValue;

    public TimeSpan TimerLeft => TimerRunning ? Max(TimeSpan.Zero, _save.TimerEndsAt - DateTime.Now) : TimeSpan.Zero;

    public void StartTimer(double minutes, string label, bool work) => StartTimer(minutes, label, work ? "work" : "other");

    /// <summary>Видът таймер: "work" – работят заедно (носи лешници), "rest" – почивка (зелен, почива си), "other" – друго.</summary>
    public bool RestTimer => TimerRunning && _save.TimerKind == "rest";

    public void StartTimer(double minutes, string label, string kind)
    {
        if (minutes <= 0) return;
        if (TimerRunning && _save.TimerIsWork) PayForMinutes(_save.TimerMinutes - TimerLeft.TotalMinutes);
        bool work = kind == "work";
        if (work) NoteWorkStarted();
        string text = label.Trim().Length == 0 && work && FocusTask is { } ft ? ft.Title : label.Trim();
        if (!CoreEngine.Bus.Send(new Mecho.Core.StartWorkTimer(minutes, text, kind)).Ok) return;
        Say($"⏰ Пускам таймер за {minutes:0.#} мин.{(_save.TimerLabel.Length > 0 ? $" ({_save.TimerLabel})" : "")}", 3);
        if (work) EnterWorkMode();
        else if (kind == "rest" && !InFocus && !IsAsleep)
        {
            LeaveWorkMode();
            RelaxRandom();
        }
        OpenParcelFirst();
        NotifyCare();
        Persist();
    }

    public void StopTimer()
    {
        if (!TimerRunning) return;
        int nuts = _save.TimerIsWork ? PayForMinutes(_save.TimerMinutes - TimerLeft.TotalMinutes) : 0;
        if (_save.TimerIsWork) RecordWork(_save.TimerMinutes - TimerLeft.TotalMinutes);
        bool rest = RestTimer;
        CoreEngine.Bus.Send(new Mecho.Core.StopWorkTimer());
        if (!InFocus) LeaveWorkMode();
        if (rest && RelaxesRandomly) GetUp();
        Say("Спрях таймера." + Earned(nuts), 2);
        NotifyCare();
        Persist();
    }

    private void UpdateTimer() => CoreEngine.Bus.Send(new Mecho.Core.CheckWork());

    /// <summary>Таймерът свърши (ядрото вече го е спряло).</summary>
    private void TimerDone(double minutes, string label, string kind)
    {
        bool rest = kind == Mecho.Core.WorkTimerKinds.Rest, work = kind == Mecho.Core.WorkTimerKinds.Work;
        // Лешници – само за работен таймер.
        int nuts = work ? PayForMinutes(minutes) : 0;
        if (work) RecordWork(minutes);
        Say((label.Length > 0 ? $"⏰ Времето изтече: {label}!" : "⏰ Времето изтече!") + Earned(nuts), 12);
        if (work) AskAboutFocusTask();
        Sounds.Play(_save);
        if (rest)
        {
            // Почивката свърши: става от кътчето.
            if (RelaxesRandomly) GetUp();
        }
        else if (!work) { }
        else if (!InFocus) RelaxAfterWork();
        else React("dance", 3);
        NotifyCare();
        Persist();
    }

    /// <summary>След работа: става от бюрото, танцува и сяда на чай за почивката.</summary>
    private void RelaxAfterWork()
    {
        LeaveWorkMode();
        if (CanAnimateFreely && !IsAsleep)
            Play("dance", length: 3, after: () => { if (!InFocus && CanAnimateFreely) StartScene(Scenes.Reading, 120 + _rng.Next(60)); });
    }

    // ───────────────────────── Таймерът, когато мечокът е скрит ─────────────────────────

    private Window? _timerBadge;
    private readonly StackPanel _timerBadgeRow = new() { Orientation = Orientation.Horizontal };
    private string _timerBadgeShown = "";

    /// <summary>
    /// Скрит ли е мечокът, а тече мечо-доро или таймер: малка табелка в долния десен
    /// ъгъл (над часовника) показва колко остава. Клик върху нея връща мечока.
    /// </summary>
    private Brush? _badgeBack;

    private void UpdateTimerBadge(System.Collections.Generic.List<string> parts)
    {
        // „Таймери“ (десен бутон на иконката) е изключено: скрит мечок – без табелка.
        bool show = !IsVisible && parts.Count > 0 && _save.ShowTimers;
        if (!show)
        {
            _timerBadge?.Hide();
            return;
        }
        if (_timerBadge == null)
        {
            var pill = Ui.Pill(_timerBadgeRow);
            _badgeBack = pill.Background;
            pill.Cursor = System.Windows.Input.Cursors.Hand;
            pill.ToolTip = Ui.Tip("Мечокът е скрит, но времето тече. Цъкни, за да го покажеш.");
            _timerBadge = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Topmost = true,
                ShowInTaskbar = false,
                ShowActivated = false,
                ResizeMode = ResizeMode.NoResize,
                SizeToContent = SizeToContent.WidthAndHeight,
                FontFamily = Ui.GameFont,
                FontSize = Ui.Body,
                Foreground = Ui.Ink,
                Content = pill,
                WindowStartupLocation = WindowStartupLocation.Manual,
            };
            System.Windows.Media.TextOptions.SetTextRenderingMode(_timerBadge, System.Windows.Media.TextRenderingMode.Aliased);
            _timerBadge.SourceInitialized += (_, _) =>
                NativeMethods.MakeToolWindow(new System.Windows.Interop.WindowInteropHelper(_timerBadge).Handle);
            pill.MouseLeftButtonUp += (_, _) =>
            {
                _timerBadge.Hide();
                if (OnWalk) ReturnFromWalk(called: true);
                else ComeBackFromEdge();
            };
        }
        string text = string.Join("   ", parts);
        if (text != _timerBadgeShown)
        {
            _timerBadgeShown = text;
            Ui.SetIconLine(_timerBadgeRow, text, t =>
            {
                t.FontFamily = Ui.MonoFont;
                t.FontWeight = FontWeights.Bold;
            }, nudge: 1);
        }
        if (_timerBadge.Content is Border badge)
        {
            var back = _save.PomodoroPhase == PomodoroPhase.Break || (RestTimer && _save.PomodoroPhase == PomodoroPhase.Off) ? TagBreak : _badgeBack;
            if (!ReferenceEquals(badge.Background, back)) badge.Background = back;
        }
        if (!_timerBadge.IsVisible) _timerBadge.Show();
        // Над часовника: долният десен ъгъл на главния екран (над лентата със задачите).
        var area = SystemParameters.WorkArea;
        _timerBadge.Left = area.Right - _timerBadge.ActualWidth - 8;
        _timerBadge.Top = area.Bottom - _timerBadge.ActualHeight - 8;
    }

    /// <summary>За иконката до часовника (при посочване): таймерите с думи.</summary>
    public string TimersText
    {
        get
        {
            var parts = new System.Collections.Generic.List<string>();
            if (_save.PomodoroPhase != PomodoroPhase.Off) parts.Add($"{(InFocus ? "Мечо-доро" : "Почивка")} {Clock(PomodoroLeft)}");
            if (TimerRunning) parts.Add($"Таймер {Clock(TimerLeft)}");
            if (OnWalk && !_walkingOut) parts.Add($"На разходка още {Clock(WalkLeft)}");
            return parts.Count == 0 ? "Мечо" : "Мечо · " + string.Join(" · ", parts);
        }
    }

    private static string Clock(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{(int)t.TotalMinutes:00}:{t.Seconds:00}";

    private string? _timerTagShown;

    private void UpdateTimerTag()
    {
        var parts = new System.Collections.Generic.List<string>();
        if (_save.PomodoroPhase != PomodoroPhase.Off) parts.Add($"{(InFocus ? "🍯" : "☕")} {Clock(PomodoroLeft)}");
        if (TimerRunning) parts.Add($"⏰ {Clock(TimerLeft)}");
        UpdateTimerBadge(parts);
        // Работим по задача: името ѝ (кратко) до часовника.
        if (InFocus && FocusTaskShort.Length > 0) parts.Add(FocusTaskShort);
        if (parts.Count == 0)
        {
            _timerTag.Visibility = Visibility.Collapsed;
            return;
        }
        // Текстът се сменя веднъж в секунда; иначе табелката се прерисуваше всеки кадър.
        string text = string.Join("   ", parts);
        if (text != _timerTagShown)
        {
            _timerTagShown = text;
            Ui.SetIconLine(_timerRow, text, t =>
            {
                t.FontFamily = _timerText.FontFamily;
                t.FontSize = _timerText.FontSize;
                t.Foreground = _timerText.Foreground;
            }, nudge: 1);
        }
        // Зелена – почивка (между мечо-дората или таймер „Почивка“).
        var back = _save.PomodoroPhase == PomodoroPhase.Break || (RestTimer && _save.PomodoroPhase == PomodoroPhase.Off) ? TagBreak : TagWork;
        if (!ReferenceEquals(_timerTag.Background, back)) _timerTag.Background = back;
        _timerTag.Visibility = Visibility.Visible;
    }
}
