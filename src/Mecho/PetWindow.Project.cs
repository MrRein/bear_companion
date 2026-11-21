using System;
using System.Collections.Generic;
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
        new("Демо на панаира", "показва играта на панаира", 310) { Desc = "В този етап мечокът показва играта на горския панаир и я пробва с непознати.", Steps = new[] { "Подготвя щанд за панаира", "Показва играта на посетители", "Записва какво им харесва" } },
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

    /// <summary>Колко точки трябват до края на етап i (0…15), общо.</summary>
    private static int ProjectTotalTo(int stage) => ProjectStages.Take(stage + 1).Sum(s => s.Points);

    /// <summary>Готов ли е целият проект (играта е пусната).</summary>
    public bool ProjectDone => _save.ProjectPaid >= ProjectStages.Length;

    /// <summary>Сегашният етап (0…15), докъде е и колко трябват.</summary>
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
        _ = Opened;   // старите записи: платеното дотук се води отворено
        _save.ProjectPaid = i + 1;
        var st = ProjectStages[i];
        ProjectRewardPlan(i);   // наградата е решена отдавна (виждаше се в пътя) – чака да я вземеш
        LastStageDone = i;
        StageDone?.Invoke(i, "");
        string line = R.Text($"project.stage.{i + 1}", _save.OwnerName, ("игра", GameTitle));
        _save.ProjectNote = line;
        Log("star", $"Проектът: „{st.Name}“ – готово!");
        Emote("sparkle", 4);
        React(i == ProjectStages.Length - 1 ? "dance" : "happy", i == ProjectStages.Length - 1 ? 4 : 0);
        Say(line + " Наградата те чака в пътя на проекта!", 7);
        // Мечо те пита как да се развие играта (тема, какво правят играчите, любим герой).
        if (!ProjectNoRewards && !SelfTest.Running)
        {
            string? ask = i switch { 0 => "theme", 1 => "goal", 5 => "hero", _ => null };
            if (ask != null && !_save.ProjectAnswers.ContainsKey(ask)) Later(9, () => AskProjectIdea(ask));
        }
    }

    /// <summary>
    /// Кои сандъчета са отворени (в какъвто ред искаш). Старите записи: брояч „колко по ред“
    /// (или нищо – тогава всички платени дотук се водят отворени).
    /// </summary>
    private List<int> Opened
    {
        get
        {
            if (!_save.ProjectOpenedInit)
            {
                int c = _save.ProjectClaimed < 0 ? _save.ProjectPaid : Math.Min(_save.ProjectClaimed, _save.ProjectPaid);
                _save.ProjectOpened.Clear();
                for (int k = 0; k < c; k++) _save.ProjectOpened.Add(k);
                _save.ProjectOpenedInit = true;
            }
            return _save.ProjectOpened;
        }
    }

    /// <summary>Сандъчето на етап i е отворено.</summary>
    public bool IsProjectOpened(int i) => Opened.Contains(i);

    /// <summary>Колко сандъчета са отворени (за опресняването).</summary>
    public int ProjectClaimed => Opened.Count;

    /// <summary>Има ли сандъче, което чака да го отвориш.</summary>
    public bool ProjectHasClaim => Enumerable.Range(0, _save.ProjectPaid).Any(i => !IsProjectOpened(i));

    /// <summary>Най-ранното неотворено (за превъртането), или -1.</summary>
    public int ProjectFirstClaim => Enumerable.Range(0, _save.ProjectPaid).FirstOrDefault(i => !IsProjectOpened(i), -1);

    /// <summary>Сандъчето на етап i може ли да се отвори сега (етапът е минат, още не е отворено).</summary>
    public bool CanClaimProject(int i) => i < _save.ProjectPaid && !IsProjectOpened(i);

    /// <summary>Цъкна наградата в пътя: дава я и казва какво е. „“ – нищо за вземане.</summary>
    public string ClaimProjectReward(int i)
    {
        if (!CanClaimProject(i)) return "";
        string plan = ProjectRewardPlan(i);
        _gotItems.Clear();
        string reward = ProjectNoRewards ? (i == ProjectStages.Length - 1 ? FinishGame() : "") : PayProjectReward(i, plan);
        // Какво точно получи (скритите и „коя да е шапка“ – вече разкрити).
        _save.ProjectGot[i] = _gotItems.Count > 0 ? string.Join("|", _gotItems) : string.Join("|", RewardItems(plan).Select(x => x.TrimStart('?')));
        Opened.Add(i);
        Emote("sparkle", 3);
        if (reward.Length > 0 && !ProjectNoRewards) Say(reward, 5);
        if (i == 8 && _save.GameName.Length == 0 && !ProjectNoRewards) Later(1.5, AskGameName);
        NotifyCare();
        Persist();
        return reward;
    }

    /// <summary>
    /// С какво работи на етап i (0…15) – там сяда, докато ти работиш: бюрото (пише),
    /// масичката (тества), статива (рисува), компютъра (дизайн, имейли). null – на столчето.
    /// </summary>
    public static string? StageNeeds(int i) => i switch
    {
        1 or 2 or 6 or 9 => Kitchen.Desk,
        3 or 4 or 5 => Kitchen.BoardGame,
        7 => Kitchen.Easel,
        8 or 10 or 12 => Kitchen.Computer,
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
    public bool ProjectRewardShown(int i) => i <= _save.ProjectPaid;

    /// <summary>
    /// Сандъчето на етапа: 1–3 неща (рядко 4) – лешници, страница от рецепта, купон за
    /// магазина, дреха „коя да е от вида“ (с гардероб); рядко мебел. Едно-единствено нещо е
    /// по-голямо (повече лешници, по-голям купон). Понякога някое е скрито („?“).
    /// Записва се като „nuts:6|?page|slot:hat“.
    /// </summary>
    private string RollProjectReward(int i)
    {
        var items = new List<string>();
        if (i == ProjectStages.Length - 1)
        {
            // Финалът – специалното сандъче, най-пълното: кутията с играта и много неща.
            items.Add("box");
            items.Add("nuts:" + ProjectNutsFor(i) * 3);
            items.Add("coupon:50");
            if (Recipes.Next(_save) != null) items.Add("page");
            if (DailyOffer.HasWardrobe(_save) && RandomSlot() is { } fs) items.Add(fs);
            if (RandomUpgrade(Kitchen.FurnitureShelf, i) is { } ff) items.Add(ff);
            return string.Join("|", items);
        }
        if (i == 8) items.Add("name");
        // Следващият етап иска нещо, което го няма (бюрото, масичката, статива, компютъра).
        if (StageNeeds(i + 1) is { } need && !OwnsOrOrdered(need) && !PlannedElsewhere("up:" + need, i)) items.Add("up:" + need);
        // Колко неща – според ценността на сандъчето: обикновено 1–2, ценно 3, много ценно 4.
        char tier = ProjectTier(i);
        int count = Math.Max(items.Count + (tier == 'N' ? 0 : 1), tier == 'G' ? 4 : tier == 'S' ? 3 : 1 + _rng.Next(2));
        bool wardrobe = DailyOffer.HasWardrobe(_save);
        var used = new HashSet<string>();
        for (int guard = 0; items.Count < count && guard < 30; guard++)
        {
            int w = _rng.Next(100);
            string kind = w < 40 ? "nuts" : w < 58 ? "page" : w < 73 ? "coupon" : w < 97 ? "slot" : "up";
            if (!used.Add(kind)) continue;
            string? item = kind switch
            {
                "nuts" => "nuts:" + ProjectNutsFor(i),
                "page" => Recipes.Next(_save) != null ? "page" : null,
                "coupon" => "coupon:25",
                "slot" => wardrobe ? RandomSlot() : null,
                _ => i >= ProjectStages.Length / 2 ? RandomUpgrade(Kitchen.FurnitureShelf, i) : null,   // мебел – рядко и чак във втората половина
            };
            if (item != null) items.Add(item);
        }
        if (items.Count == 0) items.Add("nuts:" + ProjectNutsFor(i));
        // Само едно нещо – по-голямо.
        if (items.Count == 1)
        {
            if (items[0].StartsWith("nuts:")) items[0] = "nuts:" + (ProjectNutsFor(i) * 2 + 2);
            else if (items[0].StartsWith("coupon:")) items[0] = "coupon:50";
        }
        // Понякога едно от тях е скрито (не нещата, които етапът изисква).
        if (items.Count >= 2 && _rng.Next(3) == 0)
        {
            int k = 1 + _rng.Next(items.Count - 1);
            if (!items[k].StartsWith("up:") && items[k] is not ("name" or "box")) items[k] = "?" + items[k];
        }
        return string.Join("|", items);
    }

    /// <summary>
    /// Ценността на сандъчето на етап i: N – обикновено, S – ценно, G – много ценно, X – финалът.
    /// На 16 етапа: 7 обикновени, 5 ценни, 3 много ценни и специалното накрая. Подредбата е
    /// случайна (веднъж за проекта), но: първите три са обикновени, много ценните са чак във
    /// втората половина, а еднакво ценни не стоят едно до друго.
    /// </summary>
    public char ProjectTier(int i)
    {
        int n = ProjectStages.Length;
        if (_save.ProjectTiers.Length != n)
        {
            _save.ProjectTiers = RollTiers(n);
            // Наградите отвъд сегашния етап (още невидими) – наново, по новите сандъчета.
            for (int k = _save.ProjectPaid + 1; k < _save.ProjectPlan.Count; k++) _save.ProjectPlan[k] = "";
        }
        return _save.ProjectTiers[i];
    }

    private string RollTiers(int n)
    {
        for (int attempt = 0; attempt < 400; attempt++)
        {
            var rest = new List<char>();
            rest.AddRange(Enumerable.Repeat('N', 4));
            rest.AddRange(Enumerable.Repeat('S', 5));
            rest.AddRange(Enumerable.Repeat('G', 3));
            var order = new List<char> { 'N', 'N', 'N' };
            order.AddRange(rest.OrderBy(_ => _rng.Next()));
            bool ok = true;
            for (int k = 0; k < order.Count && ok; k++)
            {
                if (order[k] == 'G' && k < n / 2) ok = false;
                if (k > 0 && order[k] != 'N' && order[k] == order[k - 1]) ok = false;
            }
            if (ok && order.Count == n - 1) return new string(order.ToArray()) + "X";
        }
        return "NNNSNNSNGSNSGSG".PadRight(n - 1, 'N')[..(n - 1)] + "X";
    }

    /// <summary>Вид дреха (шапка, блуза…), от който има още нови – „коя да е“, разкрива се при отваряне.</summary>
    private string? RandomSlot()
    {
        var slots = Clothes.Shelves.Where(sl => Clothes.All.Any(g => !g.Hidden && g.SlotId == sl.Id && !Clothes.Owns(_save, g))).ToList();
        return slots.Count == 0 ? null : "slot:" + slots[_rng.Next(slots.Count)].Id;
    }

    /// <summary>Нещата в сандъчето (стар запис – едно нещо).</summary>
    public static string[] RewardItems(string plan) => plan.Split('|', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Как се казва едно нещо (скритото – „???“).</summary>
    public string ItemLabel(string item)
    {
        if (item.StartsWith("?")) return "??? – изненада";
        string kind = item.Split(':')[0], id = item.Contains(':') ? item[(item.IndexOf(':') + 1)..] : "";
        return kind switch
        {
            "nuts" => id + " лешника",
            "coupon" => $"Купон −{id}% за магазина",
            "page" => "Страница от рецепта",
            "slot" => SlotName(id) + " (изненада коя)",
            "cloth" => Clothes.All.FirstOrDefault(g => g.Id == id)?.Name ?? "Дреха",
            "up" => Kitchen.Find(id)?.Name ?? "Мебел",
            "name" => "Името на играта",
            "box" => "Кутията с играта + титла „Издател“",
            _ => item,   // стар запис – вече е текст
        };
    }

    /// <summary>Иконката на едно нещо от сандъчето.</summary>
    public static string ItemIcon(string item)
    {
        if (item.StartsWith("?")) return "question";
        string kind = item.Split(':')[0], id = item.Contains(':') ? item[(item.IndexOf(':') + 1)..] : "";
        return kind switch
        {
            "nuts" => "nut3",
            "coupon" => "coupon",
            "page" => "book",
            "slot" => Clothes.Slots.FirstOrDefault(s => s.Id == id)?.Icon ?? "shirt",
            "cloth" => Clothes.All.FirstOrDefault(g => g.Id == id) is { } g ? g.Slot.Icon : "shirt",
            "up" => "couch",
            "name" => "pencil",
            "box" => "star",
            _ => "gift",
        };
    }

    private static string SlotName(string id) => id switch
    {
        "hat" => "Шапка", "glasses" => "Очила", "neck" => "Аксесоар", "shirt" => "Блуза",
        "pants" => "Панталон", "shoes" => "Обувки", "gloves" => "Ръкавици", _ => "Дреха",
    };

    /// <summary>Какво точно получи при последното отваряне (за „Взето“).</summary>
    private readonly List<string> _gotItems = new();

    private bool PlannedElsewhere(string item, int i) =>
        _save.ProjectPlan.Where((p, k) => k != i && k >= _save.ProjectPaid).Any(p => RewardItems(p).Any(x => x.TrimStart('?') == item));

    private string? RandomUpgrade(string shelf, int i)
    {
        var list = Kitchen.Upgrades.Where(u => u.Shelf == shelf && u.Id is not (Kitchen.Wardrobe or Kitchen.Heater) && !OwnsOrOrdered(u.Id) && !PlannedElsewhere("up:" + u.Id, i)).ToList();
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
    public string ProjectRewardLabel(string plan) => string.Join(", ", RewardItems(plan).Select(ItemLabel));

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

    /// <summary>Отваря сандъчето: дава всяко нещо в него (вече го има – вместо него лешници).</summary>
    private string PayProjectReward(int i, string plan)
    {
        var said = new List<string>();
        foreach (var raw in RewardItems(plan))
        {
            string item = raw.TrimStart('?');
            string kind = item.Split(':')[0], id = item.Contains(':') ? item[(item.IndexOf(':') + 1)..] : "";
            switch (kind)
            {
                case "nuts" when int.TryParse(id, out int n):
                    GainNuts(n, "проект");
                    said.Add($"+{n} 🌰");
                    _gotItems.Add(item);
                    continue;
                case "coupon" when int.TryParse(id, out int pct):
                    _save.Coupons.Add(pct);
                    said.Add($"купон −{pct}% (в магазина)");
                    _gotItems.Add(item);
                    continue;
                case "page" when Recipes.Next(_save) != null:
                    FindRecipePage();
                    said.Add(LastLearned != null ? "нова рецепта: " + LastLearned.Name : "страница от рецепта");
                    _gotItems.Add(item);
                    continue;
                case "slot":
                {
                    // Коя да е от вида – разкрива се сега.
                    var pool = Clothes.All.Where(g => !g.Hidden && g.SlotId == id && !Clothes.Owns(_save, g)).ToList();
                    if (pool.Count > 0)
                    {
                        var g = pool[_rng.Next(pool.Count)];
                        _save.OwnedClothes.Add(g.Id);
                        said.Add(g.Name);
                        _gotItems.Add("cloth:" + g.Id);
                        continue;
                    }
                    break;
                }
                case "cloth" when Clothes.All.FirstOrDefault(g => g.Id == id) is { } g && !Clothes.Owns(_save, g):
                    _save.OwnedClothes.Add(g.Id);
                    said.Add(g.Name);
                    _gotItems.Add(item);
                    continue;
                case "up" when Kitchen.Find(id) is { } u && !OwnsOrOrdered(id):
                    GiftUpgrade(u);
                    said.Add(u.Name + " (идва с доставка)");
                    _gotItems.Add(item);
                    continue;
                case "name":
                    _gotItems.Add(item);
                    continue;
                case "box":
                    FinishGame();
                    said.Add("кутията с играта и титла „Издател“");
                    _gotItems.Add(item);
                    continue;
            }
            // Вече го имаш / няма какво – лешници вместо него.
            int m = ProjectNutsFor(i);
            GainNuts(m, "проект");
            said.Add($"+{m} 🌰");
            _gotItems.Add("nuts:" + m);
        }
        return said.Count == 0 ? "" : "В сандъчето: " + string.Join(", ", said) + "!";
    }

    /// <summary>
    /// Активният купон (%), или 0: важи до първата покупка и до края на деня. Старият
    /// единичен купон (CouponPercent) отива в списъка с купоните.
    /// </summary>
    public int ActiveCoupon
    {
        get
        {
            if (_save.CouponPercent > 0) { _save.Coupons.Add(_save.CouponPercent); _save.CouponPercent = 0; }
            if (_save.ActiveCoupon > 0 && _save.ActiveCouponDay != DayKey(DateTime.Today)) { _save.ActiveCoupon = 0; _save.ActiveCouponDay = ""; }
            return _save.ActiveCoupon;
        }
    }

    /// <summary>Купоните, които още не са използвани (без активния).</summary>
    public IReadOnlyList<int> Coupons
    {
        get { _ = ActiveCoupon; return _save.Coupons; }
    }

    /// <summary>Активира купона (k – кой от списъка); друг активен ли има – не.</summary>
    public bool ActivateCoupon(int k)
    {
        if (ActiveCoupon > 0 || k < 0 || k >= _save.Coupons.Count) return false;
        _save.ActiveCoupon = _save.Coupons[k];
        _save.ActiveCouponDay = DayKey(DateTime.Today);
        _save.Coupons.RemoveAt(k);
        Say($"Купон −{_save.ActiveCoupon}% – цените в магазина са намалени до края на деня!", 4);
        NotifyCare();
        Persist();
        return true;
    }

    /// <summary>Цената с активния купон (ако има).</summary>
    public int WithCoupon(int price) => ActiveCoupon > 0 ? (int)Math.Round(price * (100 - ActiveCoupon) / 100.0) : price;

    /// <summary>Плаща с активния купон (ако има) – купонът се използва.</summary>
    private int PayWithCoupon(int price)
    {
        int cost = WithCoupon(price);
        if (ActiveCoupon > 0)
        {
            Say($"Купонът свали цената с {_save.ActiveCoupon}%: {price} → {cost} 🌰", 4);
            _save.ActiveCoupon = 0;
            _save.ActiveCouponDay = "";
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
        // Залата на славата: играта, която направихте заедно – с това, което ти реши за нея.
        string day = DateTime.Today.ToString("d MMMM yyyy", new System.Globalization.CultureInfo("bg-BG"));
        if (!_save.HallOfFame.Any(f => f.Name == _save.GameName && f.Done == day))
            _save.HallOfFame.Add(new FameEntry
            {
                Kind = "Настолна игра",
                Name = _save.GameName,
                Theme = _save.ProjectAnswers.GetValueOrDefault("theme", ""),
                Goal = _save.ProjectAnswers.GetValueOrDefault("goal", ""),
                Hero = _save.ProjectAnswers.GetValueOrDefault("hero", ""),
                Done = day,
            });
        return "Вече съм Издател! Кутията с играта е в „Неща“ – сложи я, където искаш.";
    }

    /// <summary>Въпросите за играта: какво решаваш ти – отива в Залата на славата накрая.</summary>
    public void AskProjectIdea(string what)
    {
        var (q, opts) = what switch
        {
            "theme" => ("Каква да е темата на играта? Ти реши!", new[] { "Горски приключения", "Космос", "Пирати", "Кухня и рецепти" }),
            "goal" => ("Какво правят играчите в играта?", new[] { "Събират мед", "Състезават се", "Строят бърлоги", "Решават загадки" }),
            _ => ("Кой да е главният герой на играта?", new[] { "Мечо", "Таралежът", "Зайчето", "Катеричката" }),
        };
        void Save(string v)
        {
            if (v.Trim().Length == 0) return;
            _save.ProjectAnswers[what] = v.Trim();
            Say(R.Pick("project.idea.ok", _save.OwnerName).Replace("{отговор}", v.Trim()), 4);
            Persist();
        }
        Ask(new ChatQuestion(q, opts.Select(o => new ChatOption(o, _ => Save(o))).ToArray())
        {
            FreeText = (_, text) => Save(text),
            FreePrompt = "Или напиши твое:",
            FreeButton = "Това",
        });
    }

    /// <summary>Етап 9: как да кръстим играта – три имена (и пак, докато не избереш).</summary>
    public void AskGameName()
    {
        if (_save.GameName.Length > 0 || _save.ProjectPaid < 9) return;
        var names = GameNames.OrderBy(_ => _rng.Next()).Take(3).ToArray();
        Ask(new ChatQuestion("Кутията е почти готова… Как да кръстим играта?",
            names.Select(n => new ChatOption(n, p => p.NameGame(n))).ToArray())
        {
            FreeText = (p, text) => { if (text.Trim().Length > 0) p.NameGame(text.Trim()); },
            FreePrompt = "Или измисли свое име:",
            FreeButton = "Кръсти я",
        });
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
