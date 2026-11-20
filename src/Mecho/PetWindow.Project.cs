using System;
using System.Linq;

namespace Mecho;

// ПРОЕКТЪТ НА МЕЧО: прави настолна игра – 15 етапа (идея → пускане). Върви само с твоята
// работа: всяка минута мечо-доро / работен таймер – 1 точка; свършена задача – малка 10,
// средна 20, голяма 40. Мине ли етап – казва какво е станало и дава награда:
//  • повечето етапи – случайна: 50% лешници (малко), 45% нещо от магазина, 5% мебел;
//  • 3 (хартиен прототип) – масичката за настолна игра (ако я няма);
//  • 5 – лешници; 9 – избираш името на играта; 11 – понякога пишат фенове;
//  • 15 – празник, титла „Издател“ и кутията с играта (слагаш/прибираш я от „Неща“).
// Таблицата с етапите: docs/project_boardgame.csv.
public sealed partial class PetWindow
{
    public sealed record ProjectStage(string Name, string Doing, int Points);

    public static readonly ProjectStage[] ProjectStages =
    {
        new("Идеята", "мисли и драска в тефтера", 60),
        new("Механиките", "измисля правилата", 90),
        new("Хартиен прототип", "реже и лепи карти", 120),
        new("Играе сам", "тества играта сам", 150),
        new("Поправя правилата", "поправя правилата", 180),
        new("Тест с приятели", "тества с горските приятели", 200),
        new("Книжката с правила", "пише книжката с правила", 220),
        new("Рисунки", "рисува картите", 240),
        new("Кутия и дизайн", "прави кутията", 260),
        new("Пробни бройки", "чака пробните бройки", 280),
        new("Общност", "пише на хора, които обичат игри", 300),
        new("Кампания", "пише имейли за кампанията", 320),
        new("Производство", "следи производството", 340),
        new("Изпращане", "опакова колети", 360),
        new("Пускане", "подготвя голямото пускане", 400),
    };

    public const string GameBox = "game_box";

    /// <summary>Ранговете на Мечо по пътя – нов на всеки три етапа (последният – когато играта излезе).</summary>
    private static readonly string[] ProjectRanks = { "Мечтател", "Изобретател", "Тестер", "Художник", "Предприемач", "Издател" };

    /// <summary>Кой ранг е при толкова минати етапа (0…5).</summary>
    public static int ProjectRankIndex(int paid) => paid >= ProjectStages.Length ? ProjectRanks.Length - 1 : Math.Min(ProjectRanks.Length - 2, paid / 3);

    public static string ProjectRank(int paid) => ProjectRanks[ProjectRankIndex(paid)];

    private static readonly string[] GameNames =
    {
        "Горски тайни", "Медено приключение", "Лешникова треска", "Пътят на мечето", "Бърлогата",
        "Нощта на светулките", "Големият мед", "Горската поща", "Зимен сън", "Пчелна кошера",
    };

    /// <summary>Колко точки трябват до края на етап i (0…14), общо.</summary>
    private static int ProjectTotalTo(int stage) => ProjectStages.Take(stage + 1).Sum(s => s.Points);

    /// <summary>Готов ли е целият проект (играта е пусната).</summary>
    public bool ProjectDone => _save.ProjectPaid >= ProjectStages.Length;

    /// <summary>Сегашният етап (0…14), докъде е и колко трябват.</summary>
    public (int Stage, ProjectStage Info, double Have, int Need) ProjectNow
    {
        get
        {
            int i = Math.Min(_save.ProjectPaid, ProjectStages.Length - 1);
            int before = i == 0 ? 0 : ProjectTotalTo(i - 1);
            var st = ProjectStages[i];
            double have = ProjectDone ? st.Points : Math.Clamp(_save.ProjectPoints - before, 0, st.Points);
            return (i, st, have, st.Points);
        }
    }

    public string GameName => _save.GameName;
    public string ProjectNote => _save.ProjectNote;

    private void WireProject()
    {
        var bus = CoreEngine.Bus;
        bus.Subscribe<Mecho.Core.WorkPaid>(this, s => { AddProjectPoints(s.Minutes); ShowProjectPoints(s.Minutes); });
        bus.Subscribe<Mecho.Core.TaskCompleted>(this, s =>
        {
            int pts = s.Size switch { 3 => 40, 2 => 20, 1 => 10, _ => 0 };
            AddProjectPoints(pts);
            ShowProjectPoints(pts);
        });
        bus.Subscribe<Mecho.Core.TaskReopened>(this, s => AddProjectPoints(-s.Reward * 10));
        // Мина етап: прогресът се отваря сам и празнува (без да краде фокуса).
        StageDone += (i, reward) =>
        {
            if (SelfTest.Running || ProjectNoRewards || !IsVisible) return;
            Dispatcher.BeginInvoke(() => ProjectWindow.Open(this, null, i, reward));
        };
    }

    /// <summary>Точките изплуват над мечока (след лешниците – малко по-късно, да не се застъпват).</summary>
    private void ShowProjectPoints(double points)
    {
        int n = (int)Math.Round(points);
        if (n <= 0 || ProjectDone || SelfTest.Running || !IsVisible || OnWalk) return;
        var at = new System.Windows.Point(_bearRect.Left + _bearRect.Width * 0.3, _bearRect.Top + _bearRect.Height * 0.25);
        Later(0.9, () => new NutPopWindow("+" + n, "board", at, Ui.Good).Pop());
    }

    /// <summary>Точки за проекта (отрицателни – върната задача; не връща платен етап).</summary>
    internal void AddProjectPoints(double points)
    {
        if (points == 0 || ProjectDone) return;
        double floor = _save.ProjectPaid == 0 ? 0 : ProjectTotalTo(_save.ProjectPaid - 1);
        _save.ProjectPoints = Math.Max(floor, _save.ProjectPoints + points);
        while (!ProjectDone && _save.ProjectPoints >= ProjectTotalTo(_save.ProjectPaid))
            FinishProjectStage(_save.ProjectPaid);
        NotifyCare();
        Persist();
    }

    /// <summary>Етап i е готов: казва го и дава наградата.</summary>
    /// <summary>Самопроверката: етапите минават без награди (без колети и лешници).</summary>
    internal bool ProjectNoRewards;

    private void FinishProjectStage(int i)
    {
        _save.ProjectPaid = i + 1;
        var st = ProjectStages[i];
        // Наградата е решена отдавна (виждаше се в прогреса): сега се дава.
        string plan = ProjectRewardPlan(i);
        string reward = ProjectNoRewards ? (i == 14 ? FinishGame() : "") : PayProjectReward(i, plan);
        _save.ProjectGot[i] = ProjectRewardLabel(plan);
        LastStageDone = i;
        StageDone?.Invoke(i, reward);
        string line = R.Text($"project.stage.{i + 1}", _save.OwnerName, ("игра", GameTitle));
        _save.ProjectNote = line;
        Log("star", $"Проектът: „{st.Name}“ – готово!");
        Emote("sparkle", 4);
        React(i == 14 ? "dance" : "happy", i == 14 ? 4 : 0);
        Say(line + (reward.Length > 0 ? " " + reward : ""), 7);
        if (i == 8 && _save.GameName.Length == 0 && !ProjectNoRewards) Later(8, AskGameName);
    }

    /// <summary>
    /// С какво работи на етап i (0…14) – там сяда, докато ти работиш: бюрото (пише),
    /// масичката (тества), статива (рисува), компютъра (дизайн, имейли). null – на столчето.
    /// </summary>
    public static string? StageNeeds(int i) => i switch
    {
        1 or 2 or 6 or 9 => Kitchen.Desk,
        3 or 4 or 5 => Kitchen.BoardGame,
        7 => Kitchen.Easel,
        8 or 10 or 11 => Kitchen.Computer,
        _ => null,
    };

    /// <summary>Докато работите (мечо-доро/таймер): при коя мебел е – на етапа, ако я има; иначе бюрото.</summary>
    private string? _focusItem;

    private string? ProjectFocusItem() =>
        !ProjectDone && StageNeeds(ProjectNow.Stage) is { } need && need != Kitchen.Desk && Kitchen.Owns(_save, need) ? need : null;

    /// <summary>Мебелта на кътчето (работата – според проекта).</summary>
    private string? SceneItem(Scene scene) => scene == Scenes.Focus && _focusItem != null ? _focusItem : scene.Item;

    /// <summary>От коя страна е мебелта (компютърът е отляво).</summary>
    private int SceneSideDir(Scene? scene) =>
        scene == Scenes.Focus && _focusItem == Kitchen.Computer ? -1 : scene?.SideDir ?? 1;

    /// <summary>Анимациите при работа според мебелта на проекта.</summary>
    private string[] FocusAnims => _focusItem switch
    {
        Kitchen.BoardGame => new[] { "doing_sit", "doing_sit", "think", "write_sit" },
        Kitchen.Easel => new[] { "doing_sit", "doing_sit", "think" },
        Kitchen.Computer => new[] { "game", "game", "think" },
        _ => new[] { "focus", "focus", "think", "focus_write", "write_sit", "read" },
    };

    private string GameTitle => _save.GameName.Length > 0 ? "„" + _save.GameName + "“" : "играта";

    private int ProjectNutsFor(int i) => i < 5 ? 3 + _rng.Next(3) : i < 10 ? 4 + _rng.Next(3) : 5 + _rng.Next(3);

    /// <summary>Мина етап (номер, какво каза за наградата) – прозорецът с прогреса празнува.</summary>
    public event Action<int, string>? StageDone;

    /// <summary>Последният минат етап (за празника в прозореца); -1 – още няма.</summary>
    public int LastStageDone { get; private set; } = -1;

    /// <summary>
    /// Наградата за етап i: решава се, когато етапът влезе в „следващите три“ (тогава се
    /// вижда), и после не се мени. Запис: nuts:N, up:id, cloth:id, bg:id, theme:key, name, box.
    /// </summary>
    public string ProjectRewardPlan(int i)
    {
        while (_save.ProjectPlan.Count < ProjectStages.Length) _save.ProjectPlan.Add("");
        while (_save.ProjectGot.Count < ProjectStages.Length) _save.ProjectGot.Add("");
        if (_save.ProjectPlan[i].Length == 0) _save.ProjectPlan[i] = RollProjectReward(i);
        return _save.ProjectPlan[i];
    }

    /// <summary>Наградата се вижда ли вече: минатите, сегашният и следващите два.</summary>
    public bool ProjectRewardShown(int i) => i < _save.ProjectPaid + 3;

    private string RollProjectReward(int i)
    {
        if (i == 14) return "box";
        if (i == 8) return "name";
        // Следващият етап иска нещо, което го няма (бюрото, масичката, статива, компютъра): то е наградата.
        if (StageNeeds(i + 1) is { } need && !OwnsOrOrdered(need) && !PlannedElsewhere("up:" + need, i)) return "up:" + need;
        if (i == 4) return "nuts:" + ProjectNutsFor(i);
        int roll = _rng.Next(100);
        if (roll >= 95 && RandomUpgrade(Kitchen.FurnitureShelf, i) is { } furniture) return furniture;
        if (roll >= 50 && RandomShopThing(i) is { } thing) return thing;
        return "nuts:" + ProjectNutsFor(i);
    }

    private bool PlannedElsewhere(string plan, int i) =>
        _save.ProjectPlan.Where((p, k) => k != i && k >= _save.ProjectPaid).Contains(plan);

    private string? RandomUpgrade(string shelf, int i)
    {
        var list = Kitchen.Upgrades.Where(u => u.Shelf == shelf && !OwnsOrOrdered(u.Id) && !PlannedElsewhere("up:" + u.Id, i)).ToList();
        return list.Count == 0 ? null : "up:" + list[_rng.Next(list.Count)].Id;
    }

    /// <summary>Нещо от магазина, което още нямаш: уред/хоби, а с гардероб – и дреха, тапет или меню.</summary>
    private string? RandomShopThing(int i)
    {
        var pool = Kitchen.Upgrades.Where(u => u.Shelf != Kitchen.FurnitureShelf && !OwnsOrOrdered(u.Id)).Select(u => "up:" + u.Id);
        if (DailyOffer.HasWardrobe(_save))
            pool = pool.Concat(Clothes.All.Where(g => !g.Hidden && !Clothes.Owns(_save, g)).Select(g => "cloth:" + g.Id))
                .Concat(Backdrops.All.Where(b => !Backdrops.Owns(_save, b)).Select(b => "bg:" + b.Id))
                .Concat(UiThemes.All.Where(t => !UiThemes.Owns(_save, t)).Select(t => "theme:" + t.Key));
        var list = pool.Where(p => !PlannedElsewhere(p, i)).ToList();
        return list.Count == 0 ? null : list[_rng.Next(list.Count)];
    }

    /// <summary>Как се казва наградата (в прогреса и в дневника).</summary>
    public string ProjectRewardLabel(string plan)
    {
        string kind = plan.Split(':')[0], id = plan.Contains(':') ? plan[(plan.IndexOf(':') + 1)..] : "";
        return kind switch
        {
            "nuts" => id + " лешника",
            "up" => Kitchen.Find(id) is { } u ? u.Name : "Изненада",
            "cloth" => Clothes.All.FirstOrDefault(g => g.Id == id)?.Name ?? "Дреха",
            "bg" => "Тапет „" + (Backdrops.All.FirstOrDefault(b => b.Id == id)?.Name ?? "") + "“",
            "theme" => "Меню „" + (UiThemes.All.FirstOrDefault(t => t.Key == id)?.Name ?? "") + "“",
            "name" => "Името на играта",
            "box" => "Кутията с играта + титла",
            _ => "Изненада",
        };
    }

    /// <summary>Картинката на нещото от магазина (за картата с наградата), ако я има.</summary>
    public System.Windows.Media.Imaging.BitmapSource? ShopArt(string plan) =>
        plan.StartsWith("up:") && _lib.HasProp("shop_" + plan[3..]) ? PropImage("shop_" + plan[3..]) : null;

    /// <summary>Иконка за наградата в прогреса.</summary>
    public static string ProjectRewardIcon(string plan) => plan.Split(':')[0] switch
    {
        "nuts" => "nut2",
        "up" => "gift",
        "cloth" => "shirt",
        "bg" => "picture",
        "theme" => "menu",
        "name" => "pencil",
        "box" => "star",
        _ => "question",
    };

    /// <summary>Дава наградата (вече я има – вместо нея лешници).</summary>
    private string PayProjectReward(int i, string plan)
    {
        string kind = plan.Split(':')[0], id = plan.Contains(':') ? plan[(plan.IndexOf(':') + 1)..] : "";
        switch (kind)
        {
            case "nuts" when int.TryParse(id, out int n):
                GainNuts(n, "проект");
                return $"+{n} 🌰";
            case "up" when Kitchen.Find(id) is { } u && !OwnsOrOrdered(id):
                return GiftUpgrade(u) + (StageNeeds(i + 1) == id ? " Трябва ми за следващия етап!" : "");
            case "cloth" when Clothes.All.FirstOrDefault(g => g.Id == id) is { } g && !Clothes.Owns(_save, g):
                _save.OwnedClothes.Add(g.Id);
                return $"Подарък: {g.Name}! (в гардероба)";
            case "bg" when Backdrops.All.FirstOrDefault(b => b.Id == id) is { } b && !Backdrops.Owns(_save, b):
                _save.Owned.Add("bg_" + b.Id);
                return $"Подарък: тапет „{b.Name}“!";
            case "theme" when UiThemes.All.FirstOrDefault(t => t.Key == id) is { } t && !UiThemes.Owns(_save, t):
                _save.Owned.Add(t.Key);
                return $"Подарък: меню „{t.Name}“!";
            case "name":
                return "";
            case "box":
                return FinishGame();
        }
        int m = ProjectNutsFor(i);
        GainNuts(m, "проект");
        return $"+{m} 🌰";
    }

    /// <summary>Безплатно: идва с доставка, както купеното.</summary>
    private string GiftUpgrade(Upgrade u)
    {
        _save.Ordered.Add(u.Id);
        Order(u);
        return $"Подарък: {u.Icon} {u.Name} – идва с доставка!";
    }

    /// <summary>Етап 15: играта излезе – титла и кутията с играта (за слагане от „Неща“).</summary>
    private string FinishGame()
    {
        if (!_save.Owned.Contains(GameBox)) _save.Owned.Add(GameBox);
        TodayRec.Hobbies.Add("game_release");
        return "Вече съм Издател! Кутията с играта е в „Неща“ – сложи я, където искаш.";
    }

    /// <summary>Етап 9: как да кръстим играта – три имена (и пак, докато не избереш).</summary>
    public void AskGameName()
    {
        if (_save.GameName.Length > 0 || _save.ProjectPaid < 9) return;
        var names = GameNames.OrderBy(_ => _rng.Next()).Take(3).ToArray();
        Ask(new ChatQuestion("Кутията е почти готова… Как да кръстим играта?",
            names.Select(n => new ChatOption(n, p => p.NameGame(n))).ToArray()));
    }

    internal void NameGame(string name)
    {
        _save.GameName = name;
        _save.ProjectNote = $"Играта се казва „{name}“!";
        Say($"„{name}“! Звучи страхотно – пиша го на кутията.", 5);
        Emote("sparkle", 3);
        NotifyCare();
        Persist();
    }

    /// <summary>Веднъж в секунда: разказва за проекта (в началото) и писмата от феновете (след етап 11).</summary>
    private void UpdateProject()
    {
        if (SelfTest.Running || !FreeForWish || _inScene && _scene == Scenes.Focus) return;
        if (!_save.ProjectIntro)
        {
            if (Now < 120) return;   // малко след пускането, не веднага
            _save.ProjectIntro = true;
            string intro = R.Pick("project.intro", _save.OwnerName);
            _save.ProjectNote = intro;
            Say(intro, 9);
            Persist();
            return;
        }
        // Етапът иска нещо, което го няма (стар запис отпреди наградите): идва като подарък.
        if (!ProjectDone && StageNeeds(ProjectNow.Stage) is { } need && !OwnsOrOrdered(need) && Kitchen.Find(need) is { } u)
        {
            Say($"За етапа „{ProjectNow.Info.Name}“ ми трябва нещо… " + GiftUpgrade(u), 6);
            Persist();
            return;
        }
        // Феновете: след „Общност“ понякога (най-много веднъж на ден) идва писмо.
        string today = DayKey(DateTime.Today);
        if (_save.ProjectPaid >= 11 && _save.ProjectFanDay != today && _rng.Next(3 * 3600) == 0)
        {
            _save.ProjectFanDay = today;
            string fan = R.Text("project.fan", _save.OwnerName, ("игра", GameTitle));
            Say(fan, 7);
            Persist();
        }
    }
}
