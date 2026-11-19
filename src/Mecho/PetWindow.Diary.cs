using System;
using System.Collections.Generic;
using System.Linq;

namespace Mecho;

/// <summary>
/// „В бърлогата“ и „Мечешки дневник“ в менюто: последно казаното, желанията му (разходка,
/// диван, рисуване), дневникът на деня, спечелените днес лешници и мечешката цел за деня
/// (по-трудна задача с повече лешници).
/// </summary>
public sealed partial class PetWindow
{
    // ── Последно казано ──

    /// <summary>Последните 10 казани неща (най-новото – последно); пазят се в записа.</summary>
    public IReadOnlyList<SaidLine> Said => _save.Said;

    private void NoteSaid(string text)
    {
        text = text.Trim();
        if (text.Length == 0) return;
        var said = _save.Said;
        if (said.Count > 0 && said[^1].Text == text && said[^1].Answer == null) return;
        said.Add(new SaidLine { At = DateTime.Now, Text = text });
        if (said.Count > 10) said.RemoveRange(0, said.Count - 10);
    }

    /// <summary>Попита нещо: въпросът влиза в „Последно каза“ (отговорът – щом избереш).</summary>
    internal void NoteQuestion(string text) => NoteSaid(text);

    /// <summary>Избра отговор на въпроса му: записва се към въпроса.</summary>
    internal void NoteAnswer(string question, string answer)
    {
        var line = _save.Said.LastOrDefault(l => l.Text == question.Trim() && l.Answer == null);
        if (line == null) return;
        line.Answer = answer.Trim();
        Persist();
        NotifyCare();
    }

    // ── Дневникът на деня ──

    /// <summary>Записва нещо в днешния дневник.</summary>
    private void Log(string icon, string text)
    {
        NoteTouched();
        var events = TodayRec.Events;
        events.Add(new DayEvent { At = DateTime.Now, Icon = icon, Text = text });
        if (events.Count > 40) events.RemoveRange(0, events.Count - 40);
    }

    /// <summary>Лешници с причина (за „Лешници днес“): задача, работа, погалване, подарък…</summary>
    private void GainNuts(int nuts, string reason)
    {
        if (nuts <= 0) return;
        CoreEngine.Bus.Send(new Mecho.Core.EarnNuts(nuts, reason));
        Persist();
        NotifyCare();
    }

    private void WireDiary()
    {
        CoreEngine.Bus.Subscribe<Mecho.Core.HazelnutsChanged>(this, c =>
        {
            // Само спечеленото (не върнатото при отказ, не Developer, не старите записи).
            if (c.New <= c.Old || c.Reason is null or "стар код" or "стар запис") return;
            ShowNutsGained(c.New - c.Old);
        });
    }

    /// <summary>Получи лешници: в менюто табелката подскача, а над мечока изплува „+N“.</summary>
    public event Action<int>? NutsGained;

    private void ShowNutsGained(int nuts)
    {
        if (nuts <= 0) return;
        NutsGained?.Invoke(nuts);
        if (SelfTest.Running || !IsVisible || OnWalk) return;
        // Над главата му (малко вдясно – да не закрие емоцията).
        var head = new System.Windows.Point(_bearRect.Left + _bearRect.Width * 0.6, _bearRect.Top + _bearRect.Height * 0.15);
        Dispatcher.BeginInvoke(() => new NutPopWindow(nuts, head).Pop());
    }

    // ── Желания: 20 неща, които понякога иска (плюс гушката – виж PetWindow.Petting.cs) ──

    /// <summary>Едно желание: ключ, „Иска …“, иконка, какво пише в дневника, възможно ли е сега.</summary>
    public sealed record Desire(string Kind, string Text, string Icon, string Done, Func<PetWindow, bool>? Possible = null);

    public static readonly Desire[] Desires =
    {
        new("walk", "Разхожда му се", "walk", "разходка"),
        new("rest", "Седи му се на дивана", "relax", "почивка на диванчето"),
        new("draw", "Рисува му се", "pencil", "рисуване заедно", p => Kitchen.Owns(p._save, Kitchen.Easel)),
        new("scene:reading", "Чете му се книжка", "book", "четене", p => p.SceneOk("reading")),
        new("scene:gaming", "Играе му се на компютъра", "game", "игра на компютъра", p => p.SceneOk("gaming")),
        new("scene:yoga", "Прави му се йога", "yoga", "йога", p => p.SceneOk("yoga")),
        new("scene:plant", "Иска да си погледне цветето", "plant", "грижа за цветето", p => p.SceneOk("plant")),
        new("scene:music", "Слуша му се музика", "music", "музика", p => p.SceneOk("music")),
        new("scene:knit", "Плете му се", "yarn", "плетене", p => p.SceneOk("knit")),
        new("scene:guitar", "Свири му се на китара", "guitar", "свирене на китара", p => p.SceneOk("guitar")),
        new("scene:boardplay", "Играе му се настолна игра", "dice", "настолна игра", p => p.SceneOk("boardplay")),
        new("scene:paintfun", "Рисува му се на статива", "easel", "рисуване за удоволствие", p => p.SceneOk("paintfun")),
        new("scene:write", "Пише му се в тефтера", "notes", "идеи в тефтера", p => p.SceneOk("write")),
        new("nap", "Спи му се", "moon", "дрямка", p => p._save.Energy < 85),
        new("focus", "Работи му се с теб", "hourglass", "работа заедно", p => !p.InFocus),
        new("snack", "Снаква му се", "food", "нещо за хапване", p => p._save.Fullness is >= 35 and < 60),
        new("hotdrink", "Иска нещо топло за пиене", "tea", "топла напитка", p => p.IsColdOutside),
        new("dress", "Преоблича му се", "wardrobe", "ново облекло", p => DailyOffer.HasWardrobe(p._save)),
    };

    private bool SceneOk(string id) => Scenes.All.FirstOrDefault(x => x.Id == id) is { } sc && Scenes.IsUnlocked(sc, _save);

    public static Desire? DesireOf(string? kind) => Desires.FirstOrDefault(d => d.Kind == kind);

    /// <summary>Какво иска сега (ключ от Desires) или null.</summary>
    public string? CurrentDesire => _save.Desire.Length > 0 && DateTime.Now < _save.DesireUntil && !IsAsleep && !OnWalk ? _save.Desire : null;

    /// <summary>Нов ден: 2–3 желания в случайни часове (10:00–20:00, поне 2 ч едно от друго) – не много често.</summary>
    private void PlanDesires()
    {
        string today = DayKey(DateTime.Today);
        if (_save.DesireTimes.StartsWith(today + ";")) return;
        int count = 2 + _rng.Next(2);
        var times = new List<int>();
        for (int tries = 0; times.Count < count && tries < 200; tries++)
        {
            int t = 10 * 60 + _rng.Next(10 * 60);
            if (times.All(x => Math.Abs(x - t) >= 120)) times.Add(t);
        }
        times.Sort();
        int now = (int)DateTime.Now.TimeOfDay.TotalMinutes;
        // Какво точно – решава се чак в часа му (по това, което е възможно тогава).
        _save.DesireTimes = today + ";" + string.Join(",", times.Where(t => t > now));
    }

    /// <summary>Свободен ли е да поиска нещо: не спи, не е навън, не работите, не яде, не си говорите.</summary>
    private bool FreeForWish => !IsAsleep && !OnWalk && !ChatIsOpen && !InFocus && _eating == null && !WaitingForFood;

    private void UpdateDesires()
    {
        PlanDesires();
        int semi = _save.DesireTimes.IndexOf(';');
        if (semi < 0) return;
        var list = _save.DesireTimes[(semi + 1)..].Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (list.Count == 0) return;
        int nowMin = (int)DateTime.Now.TimeOfDay.TotalMinutes;
        if (!int.TryParse(list[0].Split(':')[0], out int at)) at = nowMin;
        if (at > nowMin) return;
        // Зает в часа си – изчаква да се освободи; закъснее ли с над 2 ч – пропада.
        bool late = nowMin - at > 120;
        if (!late && (!FreeForWish || AnyWant)) return;   // зает или има друго желание – изчаква
        list.RemoveAt(0);
        _save.DesireTimes = _save.DesireTimes[..(semi + 1)] + string.Join(",", list);
        if (late) return;
        // Зает е с нещо (кътче, „Работи си“/„Почивай си“) – не иска друго кътче точно сега.
        bool busyScene = CurrentFun != null || WorksRandomly || RelaxesRandomly;
        var pool = Desires.Where(d => d.Kind != "pet" && (d.Possible?.Invoke(this) ?? true) && d.Kind != _lastDesire && !DoingOrDid(d.Kind)
            && !(busyScene && (d.Kind.StartsWith("scene:") || d.Kind == "rest"))).ToArray();
        if (pool.Length == 0) return;
        // Леко гладен – по-често му се снаква.
        var snack = pool.FirstOrDefault(d => d.Kind == "snack");
        StartDesire(snack != null && _rng.Next(2) == 0 ? "snack" : pool[_rng.Next(pool.Length)].Kind);
    }

    // ── Какво е правил (за да не поиска нещо, което тъкмо прави или е правил) ──

    private readonly Dictionary<string, DateTime> _didAt = new();

    /// <summary>Записва, че е правил това (сам или с теб) – за 30 мин не го иска.</summary>
    private void NoteDid(string kind) => _didAt[kind] = DateTime.Now;

    private bool DoingOrDid(string kind)
    {
        if (_didAt.TryGetValue(kind, out var at) && DateTime.Now - at < TimeSpan.FromMinutes(30)) return true;
        return kind switch
        {
            "walk" => OnWalk,
            "rest" => RelaxesRandomly || CurrentFun is { } f && !Scenes.Work.Contains(f),
            "focus" => InFocus,
            "nap" => IsAsleep,
            _ when kind.StartsWith("scene:") => CurrentFun?.Id == kind[6..],
            _ => false,
        };
    }

    /// <summary>Желае ли това кътче сега – тогава сам не го избира (чака теб).</summary>
    private bool WantedScene(Scene s) => CurrentDesire == "scene:" + s.Id;

    private string _lastDesire = "";

    /// <summary>След всяко желание (изпълнено или изтекло) – 1,5–3 ч без нови желания.</summary>
    private void WishPause() => _save.WishPauseUntil = DateTime.Now.AddMinutes(90 + _rng.Next(91));   // 1,5–3 ч

    /// <summary>Започва да иска нещо (за 60 мин).</summary>
    private void StartDesire(string kind)
    {
        _lastDesire = kind;
        _save.Desire = kind;
        _save.DesireUntil = DateTime.Now.AddMinutes(60);
        Persist();
        NotifyCare();
        Emote("heart", 3);
        if (!IsQuiet && DesireOf(kind) is { } d)
        {
            string key = "desire." + kind.Replace("scene:", "");
            string line = R.Has(key) ? R.Pick(key, _save.OwnerName) : d.Text + "…";
            Mutter(line, 5);
        }
    }

    /// <summary>Направихте каквото искаше: радва се, брои се към скрития брояч (лешници).</summary>
    private void FulfilDesire(string kind)
    {
        if (CurrentDesire != kind) return;
        _save.Desire = "";
        _save.DesireUntil = DateTime.MinValue;
        TodayRec.DesiresMet++;
        NoteDid(kind);
        WishPause();
        Joy(JoyPetted);
        NoteContent();
        Log(DesireOf(kind)?.Icon ?? "heart", "Изпълни желанието му: " + (DesireOf(kind)?.Done ?? kind));
        Emote("heart", 3);
        if (!CountWish()) Later(1.5, () => Say(R.Pick("desire.met", _save.OwnerName), 4));
        Persist();
        NotifyCare();
    }

    /// <summary>
    /// Награда за изпълнено желание (гушка, занимание, храна): скритият брояч – на скритото
    /// число (5–9, всеки път ново) голяма награда 5–10 лешника и нещо специално; иначе 1 шанс
    /// от 3 за +1 лешник. Двете не се събират. true – дошла е голямата.
    /// </summary>
    /// <summary>Случайно число от min до max, което клони към ниското (максимумът е рядък).</summary>
    private int LowRoll(int min, int max) => min + (int)Math.Floor(Math.Pow(_rng.NextDouble(), 2.2) * (max - min + 1));

    private bool CountWish()
    {
        if (_save.PetGoal <= 0) _save.PetGoal = NewPetGoal();
        _save.PetCount++;
        if (_save.PetCount >= _save.PetGoal)
        {
            _save.PetCount = 0;
            _save.PetGoal = NewPetGoal();
            int nuts = LowRoll(5, 10);
            GainNuts(nuts, "желания");
            Say(R.Pick("pet.reward", _save.OwnerName) + $" +{nuts} 🌰", 7);
            Emote("sparkle", 4);
            return true;
        }
        // Малката награда: +1 на всеки 4–6 изпълнени желания.
        if (_save.WishSmallGoal <= 0) _save.WishSmallGoal = 4 + _rng.Next(3);
        if (++_save.WishSmallCount >= _save.WishSmallGoal)
        {
            _save.WishSmallCount = 0;
            _save.WishSmallGoal = 4 + _rng.Next(3);
            GainNuts(1, "желания");
        }
        return false;
    }

    // ── Мисия: голяма цел за няколко дни ──

    /// <summary>Мисия: какво, колко, награда, докъде си стигнал (от деня, в който е почнала).</summary>
    private sealed record Mission(string Id, string Text, int Target, int Reward, Func<IEnumerable<DayRecord>, int> Have);

    private static readonly Mission[] Missions =
    {
        new("tasks25", "Свърши 25 задачи", 25, 0, d => d.Sum(r => r.TasksDone)),
        new("big6", "Свърши 6 големи задачи", 6, 0, d => d.Sum(r => r.BigTasks)),
        new("work15", "Работете заедно 15 часа", 900, 0, d => (int)d.Sum(r => r.WorkMinutes)),
        new("focus20", "Направете 20 фокуса", 20, 0, d => d.Sum(r => r.Pomodoros)),
        new("foods12", "Опитайте 12 различни ястия", 12, 0, d => d.SelectMany(r => r.Foods).Distinct().Count()),
        new("desires20", "Изпълни му 20 желания", 20, 0, d => d.Sum(r => r.DesiresMet)),
        new("streak5", "5 дни подред със свършена задача", 5, 0, Streak),
    };

    /// <summary>Колко дни подред (до днес или вчера) има поне една свършена задача.</summary>
    private static int Streak(IEnumerable<DayRecord> days)
    {
        var done = days.Where(r => r.TasksDone > 0).Select(r => r.Date).ToHashSet();
        var day = DateTime.Today;
        if (!done.Contains(DayKey(day))) day = day.AddDays(-1);
        int n = 0;
        while (done.Contains(DayKey(day))) { n++; day = day.AddDays(-1); }
        return n;
    }

    /// <summary>Сегашната мисия (почва нова на следващия ден след изпълнена).</summary>
    public (string Text, int Have, int Target, int Reward, bool Done, int Days) CurrentMission
    {
        get
        {
            string today = DayKey(DateTime.Today);
            var m = Missions.FirstOrDefault(x => x.Id == _save.MissionId);
            if (m == null || (_save.MissionDone && string.CompareOrdinal(_save.MissionStart, today) <= 0 && _save.MissionDoneDay != today))
            {
                var pool = Missions.Where(x => x.Id != _save.MissionId).ToArray();
                m = pool[_rng.Next(pool.Length)];
                _save.MissionId = m.Id;
                _save.MissionStart = today;
                _save.MissionDone = false;
                _save.MissionDoneDay = "";
            }
            string start = _save.MissionStart;
            var days = _save.Days.Where(r => string.CompareOrdinal(r.Date, start) >= 0);
            int have = m.Id == "streak5" ? Streak(_save.Days) : m.Have(days);
            int daysIn = DateTime.TryParse(start, out var st) ? (int)(DateTime.Today - st).TotalDays + 1 : 1;
            return (m.Text, Math.Min(m.Target, have), m.Target, m.Reward, _save.MissionDone, daysIn);
        }
    }

    private DateTime _nextGoalCheck;

    /// <summary>Стигнал ли си мисията: лешниците и нещо специално (веднъж).</summary>
    private void CheckBearGoal()
    {
        var g = CurrentMission;
        if (g.Done || g.Have < g.Target) return;
        PayMission(g.Text, LowRoll(15, 25));   // наградата е изненада: 15–25, по-често ниско
    }

    private void PayMission(string text, int reward)
    {
        _save.MissionDone = true;
        _save.MissionDoneDay = DayKey(DateTime.Today);
        NoteProud();
        Log("star", "Мисията изпълнена: " + text.ToLowerInvariant());
        Emote("sparkle", 4);
        // Понякога (1 от 6, ако има гардероб) вместо лешници – нещо ново: дреха, тапет или цвят на менюто.
        if (DailyOffer.HasWardrobe(_save) && _rng.Next(6) == 0)
        {
            var prizes = Clothes.All.Where(g => !g.Hidden && !Clothes.Owns(_save, g)).Select(g => (object)g)
                .Concat(Backdrops.All.Where(b => !Backdrops.Owns(_save, b)).Select(b => (object)b))
                .Concat(UiThemes.All.Where(t => !UiThemes.Owns(_save, t)).Select(t => (object)t)).ToList();
            if (prizes.Count > 0)
            {
                string name;
                switch (prizes[_rng.Next(prizes.Count)])
                {
                    case Garment g: _save.OwnedClothes.Add(g.Id); name = g.Name; break;
                    case Backdrop b: _save.Owned.Add("bg_" + b.Id); name = "тапет „" + b.Name + "“"; break;
                    case UiTheme t: _save.Owned.Add(t.Key); name = "меню „" + t.Name + "“"; break;
                    default: name = ""; break;
                }
                if (name.Length > 0)
                {
                    Say(R.Pick("goal.done", _save.OwnerName) + $" Награда: {name}! Виж в гардероба.", 8);
                    Persist();
                    NotifyCare();
                    return;
                }
            }
        }
        GainNuts(reward, "мисия");
        Say(R.Pick("goal.done", _save.OwnerName) + $" +{reward} 🌰", 7);
    }

    /// <summary>„Поговорим ли?“ – въпрос от него (желанието „да си поговорите“, менюто).</summary>
    public void StartTalk()
    {
        if (IsAsleep) return;
        FulfilDesire("talk");
        Ask(Conversations.Next(this));
    }

    /// <summary>Колко още ще прави това (секунди), 0 – докато не го спреш.</summary>
    public double SceneLeftSeconds => _sceneUntil > 0 ? Math.Max(0, _sceneUntil - Now) : 0;
}

/// <summary>
/// „+3“ с лешник, който изплува над главата на мечока и избледнява (за ~1,6 с) – когато
/// получиш лешници (задача, работа, желание, подарък, мисия…).
/// </summary>
internal sealed class NutPopWindow : System.Windows.Window
{
    private readonly System.Windows.Threading.DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(30) };
    private readonly DateTime _start = DateTime.Now;
    private readonly double _top;

    public NutPopWindow(int nuts, System.Windows.Point head)
    {
        WindowStyle = System.Windows.WindowStyle.None;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        IsHitTestVisible = false;
        ResizeMode = System.Windows.ResizeMode.NoResize;
        SizeToContent = System.Windows.SizeToContent.WidthAndHeight;
        FontFamily = Ui.GameFont;
        System.Windows.Media.TextOptions.SetTextRenderingMode(this, System.Windows.Media.TextRenderingMode.Aliased);
        var row = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        var text = new System.Windows.Controls.TextBlock
        {
            Text = "+" + nuts,
            FontSize = Ui.Title,
            FontWeight = System.Windows.FontWeights.Bold,
            Foreground = Ui.Honey,
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = System.Windows.Media.Color.FromRgb(43, 29, 22), ShadowDepth = 2, Direction = 315, BlurRadius = 0, Opacity = 1 },
        };
        row.Children.Add(text);
        var nut = Ui.Icon(nuts >= 5 ? "nut3" : nuts >= 2 ? "nut2" : "nut", 2);
        nut.Margin = new System.Windows.Thickness(4, 0, 0, 0);
        nut.VerticalAlignment = System.Windows.VerticalAlignment.Center;
        row.Children.Add(nut);
        Content = row;
        SourceInitialized += (_, _) => NativeMethods.MakeToolWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle);
        Loaded += (_, _) =>
        {
            Left = head.X - ActualWidth / 2;
            Top = head.Y - ActualHeight;
        };
        _top = head.Y;
        _timer.Tick += (_, _) => Tick();
        Closed += (_, _) => _timer.Stop();
    }

    public void Pop()
    {
        Show();
        _timer.Start();
    }

    private void Tick()
    {
        double t = (DateTime.Now - _start).TotalSeconds;
        if (t >= 1.6) { Close(); return; }
        // Подскача нагоре (бързо, после бавно) и избледнява в края.
        double rise = 46 * (1 - Math.Pow(1 - Math.Min(1, t / 1.2), 3));
        Top = _top - ActualHeight - rise;
        Opacity = t < 1.0 ? 1 : 1 - (t - 1.0) / 0.6;
    }
}
