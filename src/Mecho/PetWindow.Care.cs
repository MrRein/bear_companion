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
    private const double TiredAwake = 8;       // от 100 сутрин до ~40 следобед: уморен е рядко
    private const double TiredWorking = 12;    // докато работи: ~5 енергия за едно мечо-доро
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
        if (_save.PomodoroPhase != PomodoroPhase.Off || PomodoroModes.All(m => m.Id != id)) return;
        _save.PomodoroMode = id;
        Persist();
        NotifyCare();
    }

    private double _nextNeedLine;
    private readonly Border _timerTag = new();
    private readonly TextBlock _timerText = new();

    public SaveData Save => _save;

    /// <summary>Вика се, когато нещо в грижата се промени (за панела).</summary>
    public event EventHandler? CareChanged;

    private void NotifyCare() => CareChanged?.Invoke(this, EventArgs.Empty);

    private void InitCare()
    {
        _timerText.FontFamily = Ui.GameFont;
        _timerText.FontWeight = FontWeights.Normal;
        _timerText.FontSize = Ui.Body;
        _timerText.FontFamily = Ui.MonoFont;
        _timerText.Foreground = new SolidColorBrush(Color.FromRgb(59, 36, 20));
        _timerTag.Child = _timerText;
        _timerTag.Background = new SolidColorBrush(Color.FromRgb(255, 214, 102));
        _timerTag.BorderBrush = new SolidColorBrush(Color.FromRgb(59, 36, 20));
        _timerTag.BorderThickness = new Thickness(2);
        _timerTag.CornerRadius = new CornerRadius(3);
        _timerTag.Padding = new Thickness(5, 0, 5, 1);
        _timerTag.IsHitTestVisible = false;
        _timerTag.Visibility = Visibility.Collapsed;
        _root.Children.Add(_timerTag);

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
        _save.Energy = 100;
    }

    /// <summary>В 22:00 енергията пада рязко (веднъж на вечер): доспива му се.</summary>
    private void NightDrop()
    {
        var now = DateTime.Now;
        if (now.Hour < 22 || IsAsleep) return;
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
        // Свършените от минали дни отиват в историята (виж „Задачи“ → „История“).
        var old = _save.Tasks.Where(t => t.Done && t.DoneAt?.Date < DateTime.Today).ToList();
        if (old.Count > 0)
        {
            _save.TaskHistory.InsertRange(0, old.OrderByDescending(t => t.DoneAt));
            if (_save.TaskHistory.Count > 2000) _save.TaskHistory.RemoveRange(2000, _save.TaskHistory.Count - 2000);
            _save.Tasks.RemoveAll(old.Contains);
        }
    }

    private bool IsResting => IsAsleep || _anim == "read_sleep";

    private void UpdateCare(double dt, double now)
    {
        double h = dt / 3600;
        _save.Fullness = Math.Clamp(_save.Fullness - (IsAsleep ? HungerAsleep : HungerAwake) * h, 0, 100);
        // Спи (Тут го е сложила): почива. Дреме над книжката: енергията стои.
        double tiring = (_scene == Scenes.Focus ? TiredWorking : TiredAwake) * NightTiredness;
        double change = IsAsleep ? RestRate : _anim == "read_sleep" ? 0 : -tiring;
        _save.Energy = Math.Clamp(_save.Energy + change * h, 0, 100);
        MorningRefresh(fromStart: false);
        NightDrop();

        UpdatePomodoro();
        UpdateTimer();
        TellLearnedRecipe();
        UpdateMeals();
        AskAboutLunch();

        // Казва, че е гладен или сънлив, но не по-често от веднъж на няколко минути.
        if (!IsResting && !IsQuiet && !InFocus && now > _nextNeedLine && _state != BearState.Drag)
        {
            if (_save.Fullness < 25)
            {
                _nextNeedLine = now + 600 + _rng.Next(300);
                var meal = CurrentMeal;
                Say(Lines.Pick(meal == "lunch" ? Lines.LunchTime : meal == "dinner" ? Lines.DinnerTime : Lines.Hungry, _save.OwnerName), 5);
            }
            else if (LateNight && _save.Energy < 50)
            {
                _nextNeedLine = now + 420 + _rng.Next(240);
                Say(Lines.Pick(Lines.Bedtime, _save.OwnerName), 5);
            }
            else if (_save.Energy < TiredBelow)
            {
                // Колкото е по-уморен, толкова по-често го казва.
                _nextNeedLine = now + (_save.Energy < 12 ? 300 : 600) + _rng.Next(300);
                Say(Lines.Pick(Lines.Sleepy, _save.OwnerName), 5);
            }
        }
    }

    // ───────────────────────── Нощ ─────────────────────────

    /// <summary>След 22:00 се уморява по-бързо, след 22:30 – много бързо (до сутринта).</summary>
    private static double NightTiredness
    {
        get
        {
            // След 22:00 пада много бързо (кафето вдига най-много до половината,
            // но след това пак бързо пада).
            var t = DateTime.Now.TimeOfDay;
            return t >= new TimeSpan(22, 0, 0) || t < new TimeSpan(6, 0, 0) ? 4 : 1;
        }
    }

    /// <summary>Късно е (след 22:00): иска да спи.</summary>
    private static bool LateNight => DateTime.Now.Hour >= 22 || DateTime.Now.Hour < 6;

    /// <summary>Нощен сън (22:00–7:00): не е дрямка, спи до сутринта.</summary>
    private static bool NightSleep => DateTime.Now.Hour >= 22 || DateTime.Now.Hour < 7;

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
        Say($"{_save.OwnerName}, ти няма ли да обядваш? (цъкни ме)", 10);
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
            Say("*мърка насън*", 3);
            return;
        }
        Say(Lines.Pick(Lines.Petted, _save.OwnerName), 3);
        if (CanAnimateFreely) Play("happy");
    }

    /// <summary>Може ли да пусне анимация, без да прекъсне дивана, влаченето или съня.</summary>
    private bool CanAnimateFreely => !OnCouchMission && !_onTrampoline && !IsAsleep && _state is not (BearState.Drag or BearState.Falling or BearState.Jump or BearState.Bounce);

    // ───────────────────────── Задачи ─────────────────────────

    public void AddTask(string title, int size)
    {
        title = title.Trim();
        if (title.Length == 0) return;
        _save.Tasks.Add(new TaskItem { Title = title, Size = Math.Clamp(size, 1, 3) });
        Say(Lines.Pick(Lines.TaskAdded, _save.OwnerName), 3);
        NotifyCare();
        Persist();
    }

    public void SetTaskDone(TaskItem task, bool done)
    {
        if (task.Done == done) return;
        task.Done = done;
        task.DoneAt = done ? DateTime.Now : null;

        if (!done)
        {
            // Отмяна: връщаме наградата.
            _save.Hazelnuts = Math.Max(0, _save.Hazelnuts - task.Reward);
            _save.TasksDoneTotal = Math.Max(0, _save.TasksDoneTotal - 1);
            NotifyCare();
            Persist();
            return;
        }

        _save.Hazelnuts += task.Reward;
        _save.TasksDoneTotal++;
        // Трудна задача: малък шанс за страница (веднъж на задача).
        string page = "";
        if (task.Size >= 3 && !task.PageRolled)
        {
            task.PageRolled = true;
            if (_rng.Next(4) == 0) page = FindRecipePage();
        }

        Say(Lines.Pick(task.Size switch { 3 => Lines.BigTaskDone, 2 => Lines.TaskDone, _ => Lines.SmallTaskDone }, _save.OwnerName) +
            $" +{task.Reward} 🌰" + page, 4);

        if (CanAnimateFreely) Play(task.Size >= 3 ? "dance" : "happy", length: task.Size >= 3 ? 3 : 0);
        NotifyCare();
        Persist();
    }

    public void DeleteTask(TaskItem task)
    {
        _save.Tasks.Remove(task);
        NotifyCare();
        Persist();
    }

    // ───────────────────────── Бележки ─────────────────────────

    public void AddNote(string text, bool quiet = false)
    {
        text = text.Trim();
        if (text.Length == 0) return;
        _save.Notes.Insert(0, new NoteItem { Text = text });
        if (!quiet) Say(Lines.Pick(Lines.NoteAdded, _save.OwnerName), 3);
        NotifyCare();
        Persist();
    }

    public void DeleteNote(NoteItem note)
    {
        _save.Notes.Remove(note);
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
        _nextFocusSwitch = now + 15 + _rng.Next(20);
        string[] anims = { "focus", "focus", "think", "write" };
        string next = anims[_rng.Next(anims.Length)];
        if (next != _anim)
        {
            _anim = next;
            _animTime = 0;
        }
    }

    public TimeSpan PomodoroLeft =>
        _save.PomodoroPhase == PomodoroPhase.Off ? TimeSpan.Zero : Max(TimeSpan.Zero, _save.PomodoroEndsAt - DateTime.Now);

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

    public void StartFocus()
    {
        _save.PomodoroPhase = PomodoroPhase.Focus;
        _save.PomodoroEndsAt = DateTime.Now + FocusLength;
        // Ако е уморен, EnterWorkMode казва „ти поработи, аз ще посънча“ и той дрямва.
        if (!IsExhausted) Say(Lines.Pick(Lines.FocusStart, _save.OwnerName), 4);
        EnterWorkMode();
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
        _save.FocusMinutesBank += minutes;
        int nuts = (int)Math.Floor(_save.FocusMinutesBank / 10 + 1e-9);
        _save.FocusMinutesBank -= nuts * 10;
        _save.Hazelnuts += nuts;
        return nuts;
    }

    private static string Earned(int nuts) => nuts > 0 ? $" +{nuts} 🌰" : "";

    public void StopPomodoro()
    {
        if (_save.PomodoroPhase == PomodoroPhase.Off) return;
        bool wasFocus = InFocus;
        int nuts = wasFocus ? PayForMinutes((FocusLength - PomodoroLeft).TotalMinutes) : 0;
        _save.PomodoroPhase = PomodoroPhase.Off;
        if (!InFocus) LeaveWorkMode();
        Say((wasFocus ? "Добре, спираме. Ще продължим после." : "Почивката свърши по-рано. Хайде!") + Earned(nuts), 3);
        NotifyCare();
        Persist();
    }

    private void UpdatePomodoro()
    {
        if (_save.PomodoroPhase == PomodoroPhase.Off || DateTime.Now < _save.PomodoroEndsAt) return;

        StartNewDay();
        if (InFocus)
        {
            _save.PomodorosTotal++;
            _save.PomodorosToday++;
            int nuts = PayForMinutes(FocusLength.TotalMinutes);
            if (Kitchen.Owns(_save, Kitchen.Headphones))
            {
                _save.Hazelnuts++;
                nuts++;
            }
            _save.PomodoroPhase = PomodoroPhase.Break;
            _save.PomodoroEndsAt = DateTime.Now + BreakLength;
            // Понякога (1 на 5) – страница от готварската книга.
            string page = _rng.Next(5) == 0 ? FindRecipePage() : "";
            Say(Lines.Pick(Lines.FocusDone, _save.OwnerName) + Earned(nuts) + page, 8);
            if (!InFocus) RelaxAfterWork();
        }
        else
        {
            _save.PomodoroPhase = PomodoroPhase.Off;
            Say(Lines.Pick(Lines.BreakDone, _save.OwnerName), 6);
            if (CanAnimateFreely) Play("happy");
        }
        NotifyCare();
        Persist();
    }

    // ───────────────────────── Таймер ─────────────────────────

    public bool TimerRunning => _save.TimerEndsAt != DateTime.MinValue;

    public TimeSpan TimerLeft => TimerRunning ? Max(TimeSpan.Zero, _save.TimerEndsAt - DateTime.Now) : TimeSpan.Zero;

    public void StartTimer(double minutes, string label, bool work)
    {
        if (minutes <= 0) return;
        if (TimerRunning) PayForMinutes(_save.TimerMinutes - TimerLeft.TotalMinutes);
        _save.TimerEndsAt = DateTime.Now.AddMinutes(minutes);
        _save.TimerMinutes = minutes;
        _save.TimerLabel = label.Trim();
        _save.TimerIsWork = work;
        Say($"⏰ Пускам таймер за {minutes:0.#} мин.{(_save.TimerLabel.Length > 0 ? $" ({_save.TimerLabel})" : "")}", 3);
        if (work) EnterWorkMode();
        NotifyCare();
        Persist();
    }

    public void StopTimer()
    {
        if (!TimerRunning) return;
        int nuts = PayForMinutes(_save.TimerMinutes - TimerLeft.TotalMinutes);
        _save.TimerEndsAt = DateTime.MinValue;
        if (!InFocus) LeaveWorkMode();
        Say("Спрях таймера." + Earned(nuts), 2);
        NotifyCare();
        Persist();
    }

    private void UpdateTimer()
    {
        if (!TimerRunning || DateTime.Now < _save.TimerEndsAt) return;
        string label = _save.TimerLabel;
        _save.TimerEndsAt = DateTime.MinValue;
        int nuts = PayForMinutes(_save.TimerMinutes);
        Say((label.Length > 0 ? $"⏰ Времето изтече: {label}!" : "⏰ Времето изтече!") + Earned(nuts), 12);
        if (!InFocus) RelaxAfterWork();
        else if (CanAnimateFreely) Play("dance", length: 3);
        NotifyCare();
        Persist();
    }

    /// <summary>След работа: става от бюрото, танцува и сяда на чай за почивката.</summary>
    private void RelaxAfterWork()
    {
        LeaveWorkMode();
        if (CanAnimateFreely && !IsAsleep)
            Play("dance", length: 3, after: () => { if (!InFocus && CanAnimateFreely) StartScene(Scenes.IsUnlocked(Scenes.Tea, _save) ? Scenes.Tea : Scenes.Reading, 120 + _rng.Next(60)); });
    }

    // ───────────────────────── Таймерът, когато мечокът е скрит ─────────────────────────

    private Window? _timerBadge;
    private TextBlock? _timerBadgeText;
    private string _timerBadgeShown = "";

    /// <summary>
    /// Скрит ли е мечокът, а тече мечо-доро или таймер: малка табелка в долния десен
    /// ъгъл (над часовника) показва колко остава. Клик върху нея връща мечока.
    /// </summary>
    private void UpdateTimerBadge(System.Collections.Generic.List<string> parts)
    {
        bool show = !IsVisible && parts.Count > 0;
        if (!show)
        {
            _timerBadge?.Hide();
            return;
        }
        if (_timerBadge == null)
        {
            _timerBadgeText = Ui.RichText("", bold: true);
            _timerBadgeText.FontFamily = Ui.MonoFont;
            var pill = Ui.Pill(_timerBadgeText);
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
                Show();
            };
        }
        string text = string.Join("   ", parts);
        if (text != _timerBadgeShown)
        {
            _timerBadgeShown = text;
            Ui.SetRichText(_timerBadgeText!, text);
        }
        if (!_timerBadge.IsVisible) _timerBadge.Show();
        // Над часовника: долният десен ъгъл на главния екран (над лентата със задачите).
        var area = SystemParameters.WorkArea;
        _timerBadge.Left = area.Right - _timerBadge.ActualWidth - 8;
        _timerBadge.Top = area.Bottom - _timerBadge.ActualHeight - 8;
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
            Ui.SetRichText(_timerText, text);
        }
        _timerTag.Visibility = Visibility.Visible;
    }
}
