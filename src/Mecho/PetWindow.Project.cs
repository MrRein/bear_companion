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
    public sealed record ProjectStage(string Name, string Doing, int Points)
    {
        /// <summary>Какво прави мечокът в този етап (едно изречение, за прозореца на проекта).</summary>
        public string Desc { get; init; } = "";
        /// <summary>Трите подетапа: отмятат се на 1/3, 2/3 и в края на етапа.</summary>
        public string[] Steps { get; init; } = Array.Empty<string>();
    }

    public static readonly ProjectStage[] ProjectStages =
    {
        new("Мислене на идеи", "мисли и драска в тефтера", 60) { Desc = "В този етап мечокът мисли и записва идеи за играта.", Steps = new[] { "Събира идеи в тефтера", "Избира темата на играта", "Описва играта на една страница" } },
        new("Механиките", "измисля правилата", 90) { Desc = "В този етап мечокът разработва и тества механиките на играта.", Steps = new[] { "Измисля как се печелят точки", "Измисля ходовете на играчите", "Тества механиките на хартия" } },
        new("Хартиен прототип", "реже и лепи карти", 120) { Desc = "В този етап мечокът изрязва и лепи първата хартиена версия.", Steps = new[] { "Прави хартиени карти", "Рисува полето на картон", "Тества хартиения прототип" } },
        new("Играе сам", "тества играта сам", 150) { Desc = "В този етап мечокът играе сам, за да види дали играта работи.", Steps = new[] { "Изиграва цяла партия сам", "Записва какво е скучно", "Записва какво е твърде лесно" } },
        new("Поправя правилата", "поправя правилата", 180) { Desc = "В този етап мечокът оправя правилата там, където играта куца.", Steps = new[] { "Опростява сложните правила", "Балансира картите", "Пробва новите правила" } },
        new("Тест с приятели", "тества с горските приятели", 200) { Desc = "В този етап мечокът кани горски приятели да изпробват играта.", Steps = new[] { "Кани зайчето и таралежа", "Гледа как играят", "Събира мненията им" } },
        new("Книжката с правила", "пише книжката с правила", 220) { Desc = "В този етап мечокът пише ясни правила с примери и картинки.", Steps = new[] { "Пише правилата стъпка по стъпка", "Добавя примери с картинки", "Дава книжката на приятел да я пробва" } },
        new("Рисунки", "рисува картите", 240) { Desc = "В този етап мечокът рисува картите, полето и фигурките.", Steps = new[] { "Рисува картите", "Рисува полето", "Рисува фигурките" } },
        new("Кутия и дизайн", "прави кутията", 260) { Desc = "В този етап мечокът прави кутията и измисля как да изглежда играта.", Steps = new[] { "Измисля името на играта", "Рисува корицата на кутията", "Подрежда всичко в кутията" } },
        new("Пробни бройки", "чака пробните бройки", 280) { Desc = "В този етап мечокът поръчва първите отпечатани бройки и ги проверява.", Steps = new[] { "Поръчва пробни бройки", "Проверява качеството", "Поправя грешките в печата" } },
        new("Общност", "пише на хора, които обичат игри", 300) { Desc = "В този етап мечокът разказва за играта на хора, които обичат настолни игри.", Steps = new[] { "Разказва за играта из гората", "Показва играта на панаира", "Събира първите фенове" } },
        new("Кампания", "пише имейли за кампанията", 320) { Desc = "В този етап мечокът пише имейли и събира поддръжници за играта.", Steps = new[] { "Прави страница за кампанията", "Пише имейли на фенове", "Събира поддръжници" } },
        new("Производство", "следи производството", 340) { Desc = "В този етап мечокът следи как фабриката прави всички кутии.", Steps = new[] { "Избира фабрика", "Одобрява последната проба", "Следи производството" } },
        new("Изпращане", "опакова колети", 360) { Desc = "В този етап мечокът опакова и изпраща играта до хората.", Steps = new[] { "Опакова кутиите", "Пише адресите", "Изпраща колетите" } },
        new("Пускане", "подготвя голямото пускане", 400) { Desc = "В този етап мечокът пуска играта по магазините – и празнува!", Steps = new[] { "Подготвя празника", "Пуска играта в магазините", "Празнува с приятелите" } },
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

    /// <summary>Колко от трите подетапа на сегашния етап са отметнати (0…2; третият – с края на етапа).</summary>
    public int ProjectStepsDone => ProjectDone ? 3 : Math.Min(2, (int)Math.Floor(ProjectProgress * 3 + 1e-9));

    /// <summary>
    /// Докъде е лентата на етапа (0…1) – с работата, която тече в момента (минутите на
    /// мечо-дорото/таймера), за да се вижда как расте, докато работите. Без числа на екрана.
    /// </summary>
    public double ProjectProgress
    {
        get
        {
            if (ProjectDone) return 1;
            var now = ProjectNow;
            double live = 0;
            if (PomodoroFocus) live = Math.Max(0, (FocusLength - PomodoroLeft).TotalMinutes);
            else if (TimerRunning && _save.TimerIsWork) live = Math.Max(0, _save.TimerMinutes - TimerLeft.TotalMinutes);
            return now.Need <= 0 ? 0 : Math.Clamp((now.Have + live) / now.Need, 0, 0.98);
        }
    }

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
        if (points <= 0) return;
        Later(0.9, ProjectArrow);
    }

    /// <summary>Малка стрелкичка нагоре изплува над мечока: това, което прави, бута проекта.</summary>
    private void ProjectArrow()
    {
        if (ProjectDone || SelfTest.Running || !IsVisible || OnWalk) return;
        var at = new System.Windows.Point(_bearRect.Left + _bearRect.Width * 0.28, _bearRect.Top + _bearRect.Height * 0.3);
        new NutPopWindow("", "arrow_up", at).Pop();
    }

    private double _nextProjectArrow;

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
        _ = ProjectClaimed;   // старите записи: платеното дотук се води взето
        _save.ProjectPaid = i + 1;
        var st = ProjectStages[i];
        ProjectRewardPlan(i);   // наградата е решена отдавна (виждаше се в пътя) – чака да я вземеш
        LastStageDone = i;
        StageDone?.Invoke(i, "");
        string line = R.Text($"project.stage.{i + 1}", _save.OwnerName, ("игра", GameTitle));
        _save.ProjectNote = line;
        Log("star", $"Проектът: „{st.Name}“ – готово!");
        Emote("sparkle", 4);
        React(i == 14 ? "dance" : "happy", i == 14 ? 4 : 0);
        Say(line + " Наградата те чака в пътя на проекта!", 7);
    }

    /// <summary>Колко награди от етапите са взети (по ред). Старите записи – всичко платено дотук.</summary>
    public int ProjectClaimed
    {
        get
        {
            if (_save.ProjectClaimed < 0 || _save.ProjectClaimed > _save.ProjectPaid) _save.ProjectClaimed = _save.ProjectPaid;
            return _save.ProjectClaimed;
        }
    }

    /// <summary>Има ли награда, която чака да я вземеш (най-ранната).</summary>
    public bool ProjectHasClaim => ProjectClaimed < _save.ProjectPaid;

    /// <summary>Наградата за етап i може ли да се вземе сега (минат е, не е взета, по ред).</summary>
    public bool CanClaimProject(int i) => i == ProjectClaimed && i < _save.ProjectPaid;

    /// <summary>Цъкна наградата в пътя: дава я и казва какво е. „“ – нищо за вземане.</summary>
    public string ClaimProjectReward(int i)
    {
        if (!CanClaimProject(i)) return "";
        string plan = ProjectRewardPlan(i);
        string reward = ProjectNoRewards ? (i == 14 ? FinishGame() : "") : PayProjectReward(i, plan);
        _save.ProjectGot[i] = ProjectRewardLabel(plan);
        _save.ProjectClaimed = i + 1;
        Emote("sparkle", 3);
        if (reward.Length > 0 && !ProjectNoRewards) Say(reward, 5);
        if (i == 8 && _save.GameName.Length == 0 && !ProjectNoRewards) Later(1.5, AskGameName);
        NotifyCare();
        Persist();
        return reward;
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
        // Пакетчето: най-често лешници; понякога купон за магазина или страница от рецепта;
        // с гардероб – дреха; рядко мебел (без гардероба – него го купуваш сам).
        int roll = _rng.Next(100);
        if (roll >= 95 && RandomUpgrade(Kitchen.FurnitureShelf, i) is { } furniture) return furniture;
        if (roll >= 85 && DailyOffer.HasWardrobe(_save) && RandomCloth(i) is { } cloth) return cloth;
        if (roll >= 70 && Recipes.Next(_save) != null) return "page";
        if (roll >= 55) return "coupon:" + (_rng.Next(3) == 0 ? 50 : 25);
        return "nuts:" + ProjectNutsFor(i);
    }

    private bool PlannedElsewhere(string plan, int i) =>
        _save.ProjectPlan.Where((p, k) => k != i && k >= _save.ProjectPaid).Contains(plan);

    private string? RandomUpgrade(string shelf, int i)
    {
        var list = Kitchen.Upgrades.Where(u => u.Shelf == shelf && u.Id != Kitchen.Wardrobe && !OwnsOrOrdered(u.Id) && !PlannedElsewhere("up:" + u.Id, i)).ToList();
        return list.Count == 0 ? null : "up:" + list[_rng.Next(list.Count)].Id;
    }

    /// <summary>Дреха, която още нямаш (тениска, шапка…) – само с гардероб.</summary>
    private string? RandomCloth(int i)
    {
        var list = Clothes.All.Where(g => !g.Hidden && !Clothes.Owns(_save, g) && !PlannedElsewhere("cloth:" + g.Id, i)).ToList();
        return list.Count == 0 ? null : "cloth:" + list[_rng.Next(list.Count)].Id;
    }

    /// <summary>Какъв вид е пакетчето (видимо отвън, преди да го отвориш).</summary>
    public static string ProjectRewardKind(string plan) => plan.Split(':')[0] switch
    {
        "nuts" => "Лешници",
        "coupon" => "Купон за магазина",
        "page" => "Страница от рецепта",
        "cloth" => "Нещо за гардероба",
        "up" => "Нещо за бърлогата",
        "name" => "Името на играта",
        "box" => "Голямата награда",
        _ => "Изненада",
    };

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
            "coupon" => $"Купон −{id}% за магазина",
            "page" => "Страница от рецепта",
            "up" => Kitchen.Find(id) is { } u ? u.Name : "Изненада",
            "cloth" => Clothes.All.FirstOrDefault(g => g.Id == id)?.Name ?? "Дреха",
            "bg" => "Тапет „" + (Backdrops.All.FirstOrDefault(b => b.Id == id)?.Name ?? "") + "“",
            "theme" => "Меню „" + (UiThemes.All.FirstOrDefault(t => t.Key == id)?.Name ?? "") + "“",
            "name" => "Името на играта",
            "box" => "Кутията с играта + титла",
            _ => "Изненада",
        };
    }

    /// <summary>Кадрите на мечока (за картата на проекта): основният вид, без дрехи.</summary>
    public System.Windows.Media.Imaging.BitmapSource[] BearFrames(string anim) => _lib.Has(anim) ? _lib.Get(anim).Frames : Array.Empty<System.Windows.Media.Imaging.BitmapSource>();

    /// <summary>Картинката на нещото от магазина (за картата с наградата), ако я има.</summary>
    public System.Windows.Media.Imaging.BitmapSource? ShopArt(string plan) =>
        plan.StartsWith("up:") && _lib.HasProp("shop_" + plan[3..]) ? PropImage("shop_" + plan[3..]) : null;

    /// <summary>Иконка за наградата в прогреса.</summary>
    public static string ProjectRewardIcon(string plan) => plan.Split(':')[0] switch
    {
        "nuts" => "nut2",
        "coupon" => "shop",
        "page" => "book",
        "up" => "couch",
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
            case "coupon" when int.TryParse(id, out int pct):
                _save.CouponPercent = Math.Max(_save.CouponPercent, pct);
                return $"Купон −{pct}% за следващата покупка в магазина!";
            case "page" when Recipes.Next(_save) != null:
                string got = FindRecipePage();
                return LastLearned != null ? "Нова рецепта: " + LastLearned.Name + "!" : $"Страница от рецепта! {_save.RecipePages}/{Recipes.Pages}" + got;
            case "name":
                return "";
            case "box":
                return FinishGame();
        }
        int m = ProjectNutsFor(i);
        GainNuts(m, "проект");
        return $"+{m} 🌰";
    }

    /// <summary>Цената с купона от проекта (ако има).</summary>
    public int WithCoupon(int price) => _save.CouponPercent > 0 ? (int)Math.Round(price * (100 - _save.CouponPercent) / 100.0) : price;

    /// <summary>Плаща с купона (ако има) – купонът се използва.</summary>
    private int PayWithCoupon(int price)
    {
        int cost = WithCoupon(price);
        if (_save.CouponPercent > 0)
        {
            Say($"Купонът свали цената с {_save.CouponPercent}%: {price} → {cost} 🌰", 4);
            _save.CouponPercent = 0;
        }
        return cost;
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
        // Докато работите: от време на време стрелкичката – проектът расте.
        bool working = InFocus || TimerRunning && _save.TimerIsWork;
        if (working && !ProjectDone && Now >= _nextProjectArrow)
        {
            if (_nextProjectArrow > 0) ProjectArrow();
            _nextProjectArrow = Now + 70 + _rng.Next(50);
        }
        else if (!working) _nextProjectArrow = 0;
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
        if (!ProjectDone && !ProjectHasClaim && StageNeeds(ProjectNow.Stage) is { } need && !OwnsOrOrdered(need) && Kitchen.Find(need) is { } u)
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
