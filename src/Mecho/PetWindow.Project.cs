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
        /// <summary>С какво работи в този етап (мебел), или null – на столчето/бюрото.</summary>
        public string? Needs { get; init; }
    }

    /// <summary>Въпрос към Тут за проекта: след кой етап, как се казва в „Завършени проекти“, опции.</summary>
    public sealed record ProjectQuestion(string Key, int AfterStage, string Label, string Question, string[] Options, string FreePrompt = "Или напиши твое:");

    /// <summary>
    /// Вид проект (настолна игра, комикс, илюстрация…): етапите, въпросите към Тут, финалната
    /// награда. Нов вид = нов ProjectDef в ProjectDefs – всичко друго (пътят, сандъчетата,
    /// подетапите, завършените проекти) работи само.
    /// </summary>
    public sealed class ProjectDef
    {
        public required string Id { get; init; }
        public required string Kind { get; init; }          // „Настолна игра“
        public required string Icon { get; init; }          // иконката (ui)
        public required ProjectStage[] Stages { get; init; }
        public ProjectQuestion[] Questions { get; init; } = Array.Empty<ProjectQuestion>();
        /// <summary>Възможни имена (въпросът „name“ – три от тях + своето).</summary>
        public string[] Names { get; init; } = Array.Empty<string>();
        /// <summary>Какво остава накрая (предмет за „Неща“), или "".</summary>
        public string FinalItem { get; init; } = "";
        /// <summary>Как се казва финалът в сандъчето („Кутията с играта + титла „Издател““).</summary>
        public string FinalLabel { get; init; } = "Готовият проект";
        /// <summary>Какво казва Мечо, когато вземе финала.</summary>
        public string FinishLine { get; init; } = "Готово! Направихме го заедно!";
        /// <summary>Надписът, когато всичко е готово („Играта излезе!“).</summary>
        public string DoneText { get; init; } = "Готово!";
        /// <summary>Наградите по етапи, ако са предварително решени (иначе – от loot_list). Като „up:desk|nuts:4“.</summary>
        public string[]? FixedRewards { get; init; }
        /// <summary>Ценността на сандъчетата, ако е предварително решена („NSNSGX“).</summary>
        public string? FixedTiers { get; init; }
        /// <summary>Репликите при минат етап: „{LinesKey}.stage.N“.</summary>
        public string LinesKey { get; init; } = "project";
    }

    private static readonly ProjectStage[] BoardGameStages =
    {
        new("Мислене на идеи", "мисли и драска в тефтера", 60) { Desc = "В този етап мечокът мисли и записва идеи за играта.", Steps = new[] { "Събира идеи в тефтера", "Избира темата на играта", "Описва играта на една страница" } },
        new("Механиките", "измисля правилата", 90) { Desc = "В този етап мечокът разработва и тества механиките на играта.", Steps = new[] { "Измисля как се печелят точки", "Измисля ходовете на играчите", "Тества механиките на хартия" }, Needs = Kitchen.Desk },
        new("Хартиен прототип", "реже и лепи карти", 120) { Desc = "В този етап мечокът изрязва и лепи първата хартиена версия.", Steps = new[] { "Прави хартиени карти", "Рисува полето на картон", "Тества хартиения прототип" }, Needs = Kitchen.Desk },
        new("Играе сам", "тества играта сам", 150) { Desc = "В този етап мечокът играе сам, за да види дали играта работи.", Steps = new[] { "Изиграва цяла партия сам", "Записва какво е скучно", "Записва какво е твърде лесно" }, Needs = Kitchen.BoardGame },
        new("Поправя правилата", "поправя правилата", 180) { Desc = "В този етап мечокът оправя правилата там, където играта куца.", Steps = new[] { "Опростява сложните правила", "Балансира картите", "Пробва новите правила" }, Needs = Kitchen.BoardGame },
        new("Тест с приятели", "тества с горските приятели", 200) { Desc = "В този етап мечокът кани горски приятели да изпробват играта.", Steps = new[] { "Кани една Бибка и Гномчето", "Гледа как играят", "Събира мненията им" }, Needs = Kitchen.BoardGame },
        new("Книжката с правила", "пише книжката с правила", 220) { Desc = "В този етап мечокът пише ясни правила с примери и картинки.", Steps = new[] { "Пише правилата стъпка по стъпка", "Добавя примери с картинки", "Дава книжката на приятел да я пробва" }, Needs = Kitchen.Desk },
        new("Рисунки", "рисува картите", 240) { Desc = "В този етап мечокът рисува картите, полето и фигурките.", Steps = new[] { "Рисува картите", "Рисува полето", "Рисува фигурките" }, Needs = Kitchen.Easel },
        new("Кутия и дизайн", "прави кутията", 260) { Desc = "В този етап мечокът прави кутията и измисля как да изглежда играта.", Steps = new[] { "Измисля името на играта", "Рисува корицата на кутията", "Подрежда всичко в кутията" }, Needs = Kitchen.Computer },
        new("Пробни бройки", "чака пробните бройки", 280) { Desc = "В този етап мечокът поръчва първите отпечатани бройки и ги проверява.", Steps = new[] { "Поръчва пробни бройки", "Проверява качеството", "Поправя грешките в печата" }, Needs = Kitchen.Desk },
        new("Общност", "пише на хора, които обичат игри", 300) { Desc = "В този етап мечокът разказва за играта на хора, които обичат настолни игри.", Steps = new[] { "Разказва за играта из гората", "Показва играта на панаира", "Събира първите фенове" }, Needs = Kitchen.Computer },
        new("Демо на панаира", "показва играта на панаира", 310) { Desc = "В този етап мечокът показва играта на горския панаир и я пробва с непознати.", Steps = new[] { "Подготвя щанд за панаира", "Показва играта на посетители", "Записва какво им харесва" } },
        new("Кампания", "пише имейли за кампанията", 320) { Desc = "В този етап мечокът пише имейли и събира поддръжници за играта.", Steps = new[] { "Прави страница за кампанията", "Пише имейли на фенове", "Събира поддръжници" }, Needs = Kitchen.Computer },
        new("Производство", "следи производството", 340) { Desc = "В този етап мечокът следи как фабриката прави всички кутии.", Steps = new[] { "Избира фабрика", "Одобрява последната проба", "Следи производството" } },
        new("Изпращане", "опакова колети", 360) { Desc = "В този етап мечокът опакова и изпраща играта до хората.", Steps = new[] { "Опакова кутиите", "Пише адресите", "Изпраща колетите" } },
        new("Пускане", "подготвя голямото пускане", 400) { Desc = "В този етап мечокът пуска играта по магазините – и празнува!", Steps = new[] { "Подготвя празника", "Пуска играта в магазините", "Празнува с приятелите" } },
    };

    public const string GameBox = "game_box";

    public static readonly ProjectDef BoardGameProject = new()
    {
        Id = "boardgame",
        Kind = "Настолна игра",
        Icon = "dice",
        Stages = BoardGameStages,
        FinalItem = GameBox,
        FinalLabel = "Кутията с играта + титла „Издател“",
        FinishLine = "Вече съм Издател! Кутията с играта е в „Неща“ – сложи я, където искаш.",
        DoneText = "Играта излезе!",
        Names = new[]
        {
            "Горски тайни", "Медено приключение", "Лешникова треска", "Пътят на мечето", "Бърлогата",
            "Нощта на светулките", "Големият мед", "Горската поща", "Зимен сън", "Пчелна кошера",
        },
        Questions = new[]
        {
            new ProjectQuestion("theme", 0, "Тема", "Каква да е темата на играта? Ти реши!", new[] { "Горски приключения", "Космос", "Пирати", "Кухня и рецепти" }),
            new ProjectQuestion("goal", 1, "Играчите", "Какво правят играчите в играта?", new[] { "Събират мед", "Състезават се", "Строят бърлоги", "Решават загадки" }),
            new ProjectQuestion("hero", 5, "Главен герой", "Кой да е главният герой на играта?", new[] { "Мечо", "Една Бибка", "Морковеното гномче", "Заешката бибка" }),
            new ProjectQuestion("name", 8, "Име", "Кутията е почти готова… Как да кръстим играта?", Array.Empty<string>(), "Или измисли свое име:"),
        },
    };

    /// <summary>
    /// Зинчето с рисунки – кратък първи проект (6 етапа). Води те през играта: бюрото идва от
    /// първото сандъче, стативът – когато трябва да рисува, накрая – гардеробът (с него се
    /// отваря и магазинът). По пътя – 5 страници рецепта, купон за храна и лешници.
    /// </summary>
    public static readonly ProjectDef ZineProject = new()
    {
        Id = "zine",
        Kind = "Зинче с рисунки",
        Icon = "book",
        LinesKey = "project.zine",
        FinalLabel = "Готовото зинче",
        FinishLine = "Зинчето е готово! И гардеробът пристига – вече може да се обличам, а магазинът е отворен!",
        DoneText = "Зинчето е готово!",
        Stages = new ProjectStage[]
        {
            new("Идея", "мисли за какво да е зинчето", 40) { Desc = "В този етап мечокът решава за какво ще е зинчето.", Steps = new[] { "Драска идеи в тефтера", "Избира темата", "Решава колко страници да има" } },
            new("Скици", "скицира страниците", 60) { Desc = "В този етап мечокът скицира всяка страница на бюрото.", Steps = new[] { "Сгъва листа на страници", "Скицира корицата", "Скицира вътрешните страници" }, Needs = Kitchen.Desk },
            new("Рисунки", "рисува илюстрациите", 80) { Desc = "В този етап мечокът рисува илюстрациите на статива.", Steps = new[] { "Рисува корицата", "Рисува страниците", "Оцветява всичко" }, Needs = Kitchen.Easel },
            new("Надписи", "пише текстовете", 80) { Desc = "В този етап мечокът пише заглавието и надписите.", Steps = new[] { "Пише заглавието", "Пише надписите", "Проверява правописа" }, Needs = Kitchen.Desk },
            new("Сгъване", "сгъва и подвързва", 90) { Desc = "В този етап мечокът сгъва, реже и подвързва бройките.", Steps = new[] { "Копира страниците", "Сгъва листите", "Подвързва с конец" }, Needs = Kitchen.Desk },
            new("Раздаване", "раздава зинчето", 100) { Desc = "В този етап мечокът раздава зинчето на горските приятели.", Steps = new[] { "Подарява на зайчето", "Оставя едно в библиотеката", "Пази едно за себе си" } },
        },
        Names = new[] { "Горски дневник", "Мечешки истории", "Малки радости", "Листопад", "Медена книжка", "Тихата гора", "Пътешествие", "Звездна нощ" },
        Questions = new[]
        {
            new ProjectQuestion("theme", 0, "Тема", "За какво да е зинчето? Ти реши!", new[] { "Гората", "Храна и рецепти", "Приятелите ми", "Сезоните" }),
            new ProjectQuestion("name", 3, "Име", "Зинчето почти е готово… Как да го кръстим?", Array.Empty<string>(), "Или измисли свое име:"),
        },
        // Сандъчетата: бюрото – веднага; стативът – точно преди рисунките; накрая – гардеробът.
        FixedTiers = "NSNSGX",
        FixedRewards = new[]
        {
            "up:desk|nuts:4",
            "up:easel|page",
            "page|nuts:5",
            "coupon:food|page",
            "page|page|nuts:6",
            "box|up:wardrobe|nuts:8",
        },
    };

    /// <summary>
    /// Ролева игра (като „Подземия и дракони“, но горска): светът, картата, героите, правилата,
    /// приключението и голямата игра с приятелите. 10 етапа; наградите – от loot_list.
    /// </summary>
    public static readonly ProjectDef RpgProject = new()
    {
        Id = "rpg",
        Kind = "Ролева игра",
        Icon = "flag",
        LinesKey = "project.rpg",
        FinalLabel = "Книгата на приключенията + титла „Майстор на играта“",
        FinishLine = "Вече съм Майстор на играта! Приключението е готово – да играем пак някой път!",
        DoneText = "Приключението е готово!",
        Stages = new ProjectStage[]
        {
            new("Светът", "измисля света", 80) { Desc = "В този етап мечокът измисля света на играта – къде живеят героите.", Steps = new[] { "Записва идеи за света", "Измисля историята му", "Избира какво е вълшебно в него" } },
            new("Картата", "рисува картата на света", 100) { Desc = "В този етап мечокът рисува картата – гори, планини и тайни места.", Steps = new[] { "Скицира континента", "Рисува градовете и пътищата", "Скрива съкровища по картата" }, Needs = Kitchen.Easel },
            new("Героите", "измисля героите", 120) { Desc = "В този етап мечокът измисля какви герои може да бъдат играчите.", Steps = new[] { "Измисля класовете", "Пише им умения", "Рисува им листове" }, Needs = Kitchen.Desk },
            new("Правилата", "пише правилата и заровете", 140) { Desc = "В този етап мечокът пише как се хвърлят заровете и какво става после.", Steps = new[] { "Решава кога се хвърля зар", "Пише как се бие и как се говори", "Пробва правилата сам" }, Needs = Kitchen.Desk },
            new("Първото приключение", "пише приключението", 160) { Desc = "В този етап мечокът пише първото приключение – от началото до съкровището.", Steps = new[] { "Пише началото", "Измисля загадките", "Пише голямата битка" }, Needs = Kitchen.Desk },
            new("Чудовищата", "рисува чудовищата", 180) { Desc = "В този етап мечокът рисува и описва чудовищата и злодея.", Steps = new[] { "Измисля чудовищата", "Рисува ги", "Пише силните и слабите им страни" }, Needs = Kitchen.Easel },
            new("Тест с приятели", "играе с горските приятели", 200) { Desc = "В този етап мечокът играе приключението с приятелите си за пръв път.", Steps = new[] { "Кани приятелите", "Води първата игра", "Записва какво беше скучно" }, Needs = Kitchen.BoardGame },
            new("Баланс", "поправя баланса", 220) { Desc = "В този етап мечокът оправя това, което беше твърде лесно или твърде трудно.", Steps = new[] { "Поправя чудовищата", "Поправя наградите", "Пробва пак" }, Needs = Kitchen.Desk },
            new("Книгата на играча", "подрежда книгата", 240) { Desc = "В този етап мечокът подрежда всичко в една красива книга.", Steps = new[] { "Подрежда правилата", "Слага рисунките", "Прави корицата" }, Needs = Kitchen.Computer },
            new("Голямата игра", "води голямата игра", 260) { Desc = "В този етап мечокът води цялото приключение с приятелите – от начало до край!", Steps = new[] { "Събира приятелите", "Води приключението", "Празнуват победата" }, Needs = Kitchen.BoardGame },
        },
        Names = new[] { "Тайната на старата гора", "Кралството на пчелите", "Пазителите на мъха", "Сянката над езерото", "Лешниковата корона", "Пътят към облаците", "Песента на вълците", "Ключът от бърлогата" },
        Questions = new[]
        {
            new ProjectQuestion("theme", 0, "Свят", "Какъв да е светът на ролевата игра?", new[] { "Вълшебна гора", "Облачни острови", "Подводно кралство", "Космическа станция" }),
            new ProjectQuestion("hero", 2, "Първият герой", "Кой да е първият герой, който може да избереш?", new[] { "Рицар-мечок", "Магьосник-бухал", "Ловец-лисица", "Бард-славей" }),
            new ProjectQuestion("goal", 5, "Злодей", "Кой е злодеят в приключението?", new[] { "Ледената вещица", "Гладният дракон", "Сивият вълк", "Сенчестият крал" }),
            new ProjectQuestion("name", 8, "Име", "Книгата е почти готова… Как да кръстим приключението?", Array.Empty<string>(), "Или измисли свое име:"),
        },
    };

    /// <summary>Всички видове проекти (после – комикс, видео игра, книга с илюстрации…).</summary>
    public static readonly ProjectDef[] ProjectDefs = { BoardGameProject, ZineProject, RpgProject };

    /// <summary>Картинката-трофей на проект („Завършени проекти“): рисунката на финала или иконката.</summary>
    public System.Windows.FrameworkElement TrophyArt(ProjectDef? def, int scale)
    {
        if (def?.FinalItem is { Length: > 0 } item && _lib.HasProp(item)) return Ui.Pixel(PropImage(item), scale);
        return Ui.Icon(def?.Icon ?? "star", Math.Max(1, scale - 1));
    }

    /// <summary>
    /// Наградата за проекти – значка, която пада от финалното сандъче и сменя предишната.
    /// Каква е – според броя завършени проекти: камък 1–3, бронз 4–6, сребро 7–10, злато 11–15,
    /// изумруд 16–20, диамант 21–28, митрил 29+ (assets/ui/rank_*.png).
    /// </summary>
    public static readonly (string Id, int From)[] RankTiers =
        { ("stone", 1), ("bronze", 4), ("silver", 7), ("gold", 11), ("emerald", 16), ("diamond", 21), ("mythril", 29) };

    /// <summary>Цветът на нивото (светлият тон за плочката, тъмният за рамката) – взети от медала.</summary>
    public static (System.Windows.Media.Color Light, System.Windows.Media.Color Dark) RankColors(string id) => id switch
    {
        "stone" => (Rgb(176, 168, 160), Rgb(96, 84, 80)),
        "bronze" => (Rgb(222, 132, 72), Rgb(110, 44, 20)),
        "silver" => (Rgb(184, 204, 226), Rgb(84, 100, 120)),
        "gold" => (Rgb(240, 192, 40), Rgb(124, 68, 0)),
        "emerald" => (Rgb(96, 192, 96), Rgb(28, 76, 24)),
        "diamond" => (Rgb(60, 200, 236), Rgb(0, 84, 116)),
        "mythril" => (Rgb(196, 112, 212), Rgb(72, 20, 84)),
        _ => (Rgb(240, 192, 40), Rgb(124, 68, 0)),
    };

    private static System.Windows.Media.Color Rgb(byte r, byte g, byte b) => System.Windows.Media.Color.FromRgb(r, g, b);

    public static string RankFor(int done) => done <= 0 ? "" : RankTiers.Last(t => done >= t.From).Id;

    /// <summary>Сегашната значка (стари записи без нея – по броя завършени).</summary>
    public string RankBadge => _save.RankBadge == "none" ? "" : _save.RankBadge.Length > 0 ? _save.RankBadge : RankFor(_save.HallOfFame.Count);

    /// <summary>Няма започнат проект (нов мечок или избира следващия).</summary>
    public bool NoProject => string.IsNullOrEmpty(_save.ProjectType);

    /// <summary>Проектът, по който работи (или последният завършен).</summary>
    public ProjectDef CurrentProject => ProjectDefs.FirstOrDefault(d => d.Id == _save.ProjectType) ?? ZineProject;

    /// <summary>Какво може да се избере за нов проект: в самото начало – само зинчето.</summary>
    public IReadOnlyList<ProjectDef> ProjectChoices =>
        _save.GuidedStart && _save.HallOfFame.Count == 0 ? new[] { ZineProject } : ProjectDefs;

    /// <summary>Почва нов проект от избрания вид (всичко на проекта – отначало).</summary>
    public void StartProject(ProjectDef def)
    {
        if (HasActiveProject) return;
        _save.ProjectType = def.Id;
        _save.ProjectPoints = 0; _save.ProjectPaid = 0; _save.ProjectBank = 0;
        _save.GameName = ""; _save.ProjectNote = "";
        _save.ProjectPlan.Clear(); _save.ProjectGot.Clear(); _save.ProjectOpened.Clear(); _save.ProjectOpenedInit = true;
        _save.ProjectTiers = ""; _save.ProjectAnswers.Clear(); _save.ProjectPlanVersion = 3;
        _save.ProjectStartedAt = DateTime.Now; _save.ProjectWorkMinutes = 0; _save.ProjectTasks = 0;
        Remember("project.start", 5, ("вид", def.Kind));
        Say($"Почваме: {def.Kind.ToLowerInvariant()}! Като работиш, и аз работя по него.", 5);
        Emote("sparkle", 3);
        NotifyCare();
        Persist();
    }

    /// <summary>„Избери нов проект“: Мечо пита какво да почнете (в началото – само зинчето).</summary>
    public void ChooseProject()
    {
        if (HasActiveProject) { ProjectWindow.Toggle(this); return; }
        var all = ProjectChoices;
        if (all.Count == 1)
        {
            Ask(new ChatQuestion("Да направим заедно нещо? Почваме с малко зинче с рисунки!",
                new ChatOption(all[0].Kind, p => p.StartProject(all[0])), new ChatOption("После", _ => { })));
            return;
        }
        // Мечо предлага два (различни от последния завършен, ако може) – ти избираш или „Ти избери“.
        string last = _save.HallOfFame.LastOrDefault()?.Type ?? "";
        var pool = all.Where(d => d.Id != last).ToList();
        if (pool.Count < 2) pool = all.ToList();
        var two = pool.OrderBy(_ => _rng.Next()).Take(2).ToArray();
        Ask(new ChatQuestion($"Мечтая да направим {two[0].Kind.ToLowerInvariant()}… или пък {two[1].Kind.ToLowerInvariant()}? Какво ще кажеш?",
            new ChatOption(two[0].Kind, p => p.StartProject(two[0])),
            new ChatOption(two[1].Kind, p => p.StartProject(two[1])),
            new ChatOption("Ти избери", p => p.StartProject(two[p._rng.Next(2)])),
            new ChatOption("После", _ => { })));
    }

    /// <summary>Етапите на сегашния проект.</summary>
    public ProjectStage[] ProjectStages => CurrentProject.Stages;

    /// <summary>Има ли активен проект (не е завършен – или финалното сандъче още чака).</summary>
    public bool HasActiveProject => !NoProject && (!ProjectDone || ProjectHasClaim);

    /// <summary>Ранговете на Мечо по пътя – нов на всеки три етапа (последният – когато играта излезе).</summary>
    private static readonly string[] ProjectRanks = { "Мечтател", "Изобретател", "Тестер", "Художник", "Предприемач", "Издател" };

    /// <summary>Кой ранг е при толкова минати етапа (0…5).</summary>
    public int ProjectRankIndex(int paid) => paid >= ProjectStages.Length ? ProjectRanks.Length - 1 : Math.Min(ProjectRanks.Length - 2, paid / 3);

    public string ProjectRank(int paid) => ProjectRanks[ProjectRankIndex(paid)];


    /// <summary>Колко точки трябват до края на етап i (0…15), общо.</summary>
    private int ProjectTotalTo(int stage) => ProjectStages.Take(stage + 1).Sum(s => s.Points);

    /// <summary>Готов ли е целият проект (играта е пусната).</summary>
    public bool ProjectDone => NoProject || _save.ProjectPaid >= ProjectStages.Length;

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
            if (ProjectBlocked) live = 0;   // чака сандъче/отговор – не расте
            else if (PomodoroFocus) live = Math.Max(0, (FocusLength - PomodoroLeft).TotalMinutes);
            else if (TimerRunning && _save.TimerIsWork) live = Math.Max(0, _save.TimerMinutes - TimerLeft.TotalMinutes);
            return now.Need <= 0 ? 0 : Math.Clamp((now.Have + live) / now.Need, 0, 0.98);
        }
    }

    public string ProjectNote => _save.ProjectNote;

    private void WireProject()
    {
        var bus = CoreEngine.Bus;
        bus.Subscribe<Mecho.Core.WorkPaid>(this, s =>
        {
            if (!ProjectDone) _save.ProjectWorkMinutes += s.Minutes;
            AddProjectPoints(s.Minutes);
            ShowProjectPoints(s.Minutes);
        });
        bus.Subscribe<Mecho.Core.TaskCompleted>(this, s =>
        {
            if (!ProjectDone) _save.ProjectTasks++;
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
        // Проектът чака (неотворено сандъче / въпрос без отговор): работата се пази настрана
        // и влиза, щом отвориш сандъчето или отговориш – а Мечо ти напомня.
        if (ProjectBlocked)
        {
            _save.ProjectBank = Math.Max(0, _save.ProjectBank + points);
            if (points > 0) NudgeProject();
            Persist();
            return;
        }
        double floor = _save.ProjectPaid == 0 ? 0 : ProjectTotalTo(_save.ProjectPaid - 1);
        _save.ProjectPoints = Math.Max(floor, _save.ProjectPoints + points);
        while (!ProjectDone && !ProjectBlocked && _save.ProjectPoints >= ProjectTotalTo(_save.ProjectPaid))
            FinishProjectStage(_save.ProjectPaid);
        // Стигна етап и той чака (сандъче/отговор): лентата спира точно на него, а остатъкът
        // се пази и влиза чак когато изпълниш условието. Накрая – не отвъд последния етап.
        if (ProjectBlocked || ProjectDone)
        {
            double cap = ProjectTotalTo(Math.Max(0, _save.ProjectPaid - 1));
            if (_save.ProjectPoints > cap)
            {
                if (!ProjectDone) _save.ProjectBank += _save.ProjectPoints - cap;
                _save.ProjectPoints = cap;
            }
        }
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
        string key = $"{CurrentProject.LinesKey}.stage.{i + 1}";
        string line = R.Has(key) ? R.Text(key, _save.OwnerName, ("игра", GameTitle)) : $"Етап „{st.Name}“ – готово!";
        _save.ProjectNote = line;
        Log("star", $"Проектът: „{st.Name}“ – готово!");
        Emote("sparkle", 4);
        React(i == ProjectStages.Length - 1 ? "dance" : "happy", i == ProjectStages.Length - 1 ? 4 : 0);
        Say(line + " Наградата те чака в пътя на проекта!", 7);
        // Мечо те пита как да се развие проектът (тема, име…) – докато не отговориш, проектът чака.
        if (!ProjectNoRewards && !SelfTest.Running && CurrentProject.Questions.FirstOrDefault(q => q.AfterStage == i) is { } question && !Answered(question))
            Later(9, () => AskProjectIdea(question.Key));
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
        ResumeProject();
        NotifyCare();
        Persist();
        return reward;
    }

    /// <summary>
    /// С какво работи на етап i (0…15) – там сяда, докато ти работиш: бюрото (пише),
    /// масичката (тества), статива (рисува), компютъра (дизайн, имейли). null – на столчето.
    /// </summary>
    public string? StageNeeds(int i) => i >= 0 && i < ProjectStages.Length ? ProjectStages[i].Needs : null;

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

    private string GameTitle => _save.GameName.Length > 0 ? "„" + _save.GameName + "“" : CurrentProject == ZineProject ? "зинчето" : "играта";

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
        string plan = RollProjectRewardItems(i);
        // Финалното сандъче: и наградата за проекти (значката – според това кой поред е проектът).
        if (i == ProjectStages.Length - 1) plan += "|rank:" + RankFor(_save.HallOfFame.Count + 1);
        return plan;
    }

    private string RollProjectRewardItems(int i)
    {
        if (CurrentProject.FixedRewards is { } fixedPlan && i < fixedPlan.Length) return fixedPlan[i];
        var items = new List<string>();
        bool final = i == ProjectStages.Length - 1;
        char tier = final ? 'X' : ProjectTier(i);
        string source = TierSource(tier);
        if (final) items.Add("box");
        if (i == 8) items.Add("name");
        // Следващият етап иска нещо, което го няма (бюрото, масичката, статива, компютъра).
        if (!final && StageNeeds(i + 1) is { } need && !OwnsOrOrdered(need) && !PlannedElsewhere("up:" + need, i)) items.Add("up:" + need);
        // Колко неща – от loot_list („Настройки“): обикновено 1–2, ценно 3, много ценно 4, финалът 5 (+ кутията).
        var (cmin, cmax) = tier switch { 'S' => (3, 3), 'G' => (4, 4), 'X' => (5, 5), _ => (1, 2) };
        cmin = Loot.SettingInt($"{source} неща мин", cmin);
        cmax = Math.Max(cmin, Loot.SettingInt($"{source} неща макс", cmax));
        int rolled = cmin + _rng.Next(cmax - cmin + 1);
        int count = final ? items.Count + rolled : Math.Max(items.Count + (tier == 'N' ? 0 : 1), rolled);
        int upFrom = Loot.SettingInt("мебели от етап", 9);
        for (int guard = 0; items.Count < count && guard < 30; guard++)
        {
            string? code = RollLoot(source, c =>
                !items.Any(x => x == c || x.StartsWith(c + ":"))
                && !PlannedElsewhere(c, i)
                && (!c.StartsWith("up:") || i + 1 >= upFrom));
            if (code == null) break;
            items.Add(code == "nuts" ? "nuts:" + ProjectNutsFor(i) * (final ? 3 : 1) : code);
        }
        if (items.Count == 0) items.Add("nuts:" + ProjectNutsFor(i));
        // Само едно нещо – по-голямо.
        if (items.Count == 1)
        {
            if (items[0].StartsWith("nuts:")) items[0] = "nuts:" + (ProjectNutsFor(i) * 2 + 2);
            else if (items[0].StartsWith("coupon:")) items[0] = "coupon:pct50";
        }
        // Понякога едно от тях е скрито (не нещата, които етапът изисква).
        if (!final && items.Count >= 2 && _rng.Next(100) < Loot.Setting("скрито нещо %", 33))
        {
            int k = 1 + _rng.Next(items.Count - 1);
            if (!items[k].StartsWith("up:") && items[k] is not ("name" or "box")) items[k] = "?" + items[k];
        }
        return string.Join("|", items);
    }

    /// <summary>Колоната в loot_list за вида сандъче.</summary>
    private static string TierSource(char tier) => tier switch
    {
        'S' => Loot.Silver, 'G' => Loot.Gold, 'X' => Loot.Legend, _ => Loot.Wood,
    };

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
            _save.ProjectTiers = CurrentProject.FixedTiers is { } ft && ft.Length == n ? ft : RollTiers(n);
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
            // Колко от всеки вид – от шансовете в loot_list (44 / 31 / 19 / 6 → на 16 етапа 7 / 5 / 3 / 1).
            int ns = (int)Math.Round(n * Loot.Setting("шанс " + Loot.Silver + " %", 31) / 100);
            int ng = (int)Math.Round(n * Loot.Setting("шанс " + Loot.Gold + " %", 19) / 100);
            ns = Math.Clamp(ns, 0, n - 4);
            ng = Math.Clamp(ng, 0, n - 4 - ns);
            rest.AddRange(Enumerable.Repeat('N', n - 1 - 3 - ns - ng));
            rest.AddRange(Enumerable.Repeat('S', ns));
            rest.AddRange(Enumerable.Repeat('G', ng));
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
            "coupon" => "Купон: " + CouponOf(int.TryParse(id, out int cp) ? "pct" + cp : id).Tip,
            "page" => "Страница от рецепта",
            "slot" => SlotName(id) + " (изненада коя)",
            "cloth" => Clothes.All.FirstOrDefault(g => g.Id == id)?.Name ?? "Дреха",
            "up" => Kitchen.Find(id)?.Name ?? "Мебел",
            "pajama" or "bg" or "theme" or "skin" => LootName(item),
            "name" => "Името (ти избираш)",
            "box" => CurrentProject.FinalLabel,
            "rank" => "Награда за проект",
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
            "coupon" => CouponOf(int.TryParse(id, out int cp) ? "pct" + cp : id).Icon,
            "page" => "book",
            "slot" => Clothes.Slots.FirstOrDefault(s => s.Id == id)?.Icon ?? "shirt",
            "cloth" => Clothes.All.FirstOrDefault(g => g.Id == id) is { } g ? g.Slot.Icon : "shirt",
            "up" => "couch",
            "pajama" or "bg" or "theme" or "skin" => LootIcon(item),
            "rank" => "rank_" + id,
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

    /// <summary>Какъв вид е пакетчето (видимо отвън, преди да го отвориш).</summary>
    public static string ProjectRewardKind(string plan) => plan.Split(':')[0] switch
    {
        "nuts" => "Лешници",
        "coupon" => "Купон за магазина",
        "page" => "Страница от рецепта",
        "cloth" => "Нещо за гардероба",
        "up" or "skin" => "Нещо за бърлогата",
        "pajama" or "bg" or "theme" => "Нещо за гардероба",
        "name" => "Името",
        "rank" => "Награда за проект",
        "box" => "Голямата награда",
        _ => "Изненада",
    };

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
                case "coupon":
                    string code = int.TryParse(id, out int pct) ? "pct" + pct : id;
                    _save.CouponCodes.Add(code);
                    said.Add("купон „" + CouponOf(code).Tip + "“");
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
                        SetOrigin(g.Id, ChestOrigin);
                        said.Add(g.Name);
                        _gotItems.Add("cloth:" + g.Id);
                        continue;
                    }
                    break;
                }
                case "cloth" when Clothes.All.FirstOrDefault(g => g.Id == id) is { } g && !Clothes.Owns(_save, g):
                    _save.OwnedClothes.Add(g.Id);
                    SetOrigin(g.Id, ChestOrigin);
                    said.Add(g.Name);
                    _gotItems.Add(item);
                    continue;
                case "up" or "pajama" or "bg" or "theme" or "skin" when GrantLoot(item, ChestOrigin) is { } got:
                    said.Add(got);
                    _gotItems.Add(item);
                    continue;
                case "name":
                    _gotItems.Add(item);
                    continue;
                case "rank":
                    _save.RankBadge = id;
                    said.Add("награда за проект");
                    _gotItems.Add(item);
                    continue;
                case "box":
                    FinishGame();
                    said.Add(CurrentProject.FinalLabel.ToLowerInvariant());
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

    /// <summary>Видът на купона: как се казва, иконката, какво прави.</summary>
    public sealed record CouponKind(string Code, string Label, string Icon, string Tip);

    /// <summary>
    /// Купоните. Всеки сваля цената на определен вид покупки: shop – всичко в магазина
    /// (дрехи, мебели и уреди, тапети…), cloth – дрехи, home – мебели и уреди, food – готвене.
    /// </summary>
    public static readonly CouponKind[] CouponKinds =
    {
        // Една картинка на цвят: червеният – за всичко; синият – дрехи; зеленият – мебели; оранжевият – храна; лилавият – безплатно нещо.
        new("pct15", "−15%", "coupon", "−15% за всичко в магазина"),
        new("pct25", "−25%", "coupon", "−25% за всичко в магазина"),
        new("pct50", "−50%", "coupon", "−50% за всичко в магазина"),
        new("cloth50", "−50% дрехи", "coupon_cloth", "−50% за дрехите"),
        new("home30", "−30% мебели", "coupon_home", "−30% за мебелите и уредите"),
        new("food50", "−50% храна", "coupon_food", "Следващото ястие – на половин цена"),
        new("food", "Безплатно готвене", "coupon_food", "Следващото ястие се готви без лешници"),
        new("free", "Безплатно нещо", "coupon_luck", "Едно нещо от магазина – безплатно"),
    };

    public static CouponKind CouponOf(string code) => CouponKinds.FirstOrDefault(k => k.Code == code) ?? CouponKinds[0];

    /// <summary>
    /// Купоните винаги минават оттук – един източник на истината: стари записи → кодове;
    /// активният от вчера се връща обратно (не се губи).
    /// </summary>
    private void SyncCoupons()
    {
        if (_save.CouponPercent > 0) { _save.CouponCodes.Add("pct" + _save.CouponPercent); _save.CouponPercent = 0; }
        foreach (int p in _save.Coupons) _save.CouponCodes.Add("pct" + p);
        _save.Coupons.Clear();
        if (_save.ActiveCoupon > 0) { _save.ActiveCouponCode = "pct" + _save.ActiveCoupon; _save.ActiveCouponDay = DayKey(DateTime.Today); _save.ActiveCoupon = 0; }
        for (int k = 0; k < _save.CouponCodes.Count; k++) if (_save.CouponCodes[k] == "luck") _save.CouponCodes[k] = "free";
        if (_save.ActiveCouponCode == "luck") _save.ActiveCouponCode = "free";
        if (_save.ActiveCouponCode.Length > 0 && _save.ActiveCouponDay != DayKey(DateTime.Today))
        {
            _save.CouponCodes.Add(_save.ActiveCouponCode);   // денят свърши – изключва се сам, не се губи
            _save.ActiveCouponCode = "";
            _save.ActiveCouponDay = "";
        }
    }

    /// <summary>Активният купон (код) или "". Пази се (и при затваряне на менюто), докато не купиш нещо, за което важи.</summary>
    public string ActiveCoupon
    {
        get { SyncCoupons(); return _save.ActiveCouponCode; }
    }

    /// <summary>Купоните по вид и колко са (за менюто: „5 [иконка] −50%“). what – само тези, които важат за това (null – всички).</summary>
    public IReadOnlyList<(CouponKind Kind, int Count)> CouponStacks(string? what = null)
    {
        SyncCoupons();
        return CouponKinds.Where(k => what == null || CouponAppliesTo(k.Code, what))
            .Select(k => (k, _save.CouponCodes.Count(c => c == k.Code))).Where(x => x.Item2 > 0).ToList();
    }

    /// <summary>Активира купон. Има ли друг активен – той се връща обратно (сменят се).</summary>
    public bool UseCoupon(string code)
    {
        SyncCoupons();
        int at = _save.CouponCodes.IndexOf(code);
        if (at < 0) return false;
        _save.CouponCodes.RemoveAt(at);
        if (_save.ActiveCouponCode.Length > 0) _save.CouponCodes.Add(_save.ActiveCouponCode);
        _save.ActiveCouponCode = code;
        _save.ActiveCouponDay = DayKey(DateTime.Today);
        Say($"Купон „{CouponOf(code).Tip}“ – важи до първата покупка или до края на деня.", 4);
        NotifyCare();
        Persist();
        return true;
    }

    /// <summary>✕ на активния купон: изключва се и се връща обратно (не се губи).</summary>
    public void DeactivateCoupon()
    {
        SyncCoupons();
        if (_save.ActiveCouponCode.Length == 0) return;
        _save.CouponCodes.Add(_save.ActiveCouponCode);
        _save.ActiveCouponCode = "";
        _save.ActiveCouponDay = "";
        NotifyCare();
        Persist();
    }

    /// <summary>Важи ли купонът за този вид покупка (shop/cloth/home/other – магазина; food – готвене; none – нищо).</summary>
    private static bool CouponAppliesTo(string code, string what) => what switch
    {
        "none" => false,
        "food" => code is "food" or "food50",
        "cloth" => code is "pct15" or "pct25" or "pct50" or "free" or "cloth50",
        "home" => code is "pct15" or "pct25" or "pct50" or "free" or "home30",
        _ => code is "pct15" or "pct25" or "pct50" or "free",
    };

    /// <summary>Колко % сваля активният купон за този вид покупка (0 – не важи).</summary>
    public int CouponOff(string what)
    {
        string code = ActiveCoupon;
        if (code.Length == 0 || !CouponAppliesTo(code, what)) return 0;
        return code switch { "pct15" => 15, "food50" => 50, "pct25" => 25, "pct50" => 50, "cloth50" => 50, "home30" => 30, "food" => 100, "free" => 100, _ => 0 };
    }

    /// <summary>Цената с активния купон – навсякъде, където се показва или плаща цена.</summary>
    public int WithCoupon(int price, string what = "other") => CouponOff(what) is int off && off > 0 ? (int)Math.Round(price * (100 - off) / 100.0) : price;

    /// <summary>Плаща: цената с купона; купонът (ако е важал) се използва.</summary>
    private int PayWithCoupon(int price, string what = "other")
    {
        int off = CouponOff(what);
        int cost = WithCoupon(price, what);
        if (off > 0 && price > 0)
        {
            Say(off >= 100 ? $"Купонът го направи безплатно! ({price} → 0 🌰)" : $"Купонът свали цената с {off}%: {price} → {cost} 🌰", 4);
            _save.ActiveCouponCode = "";
            _save.ActiveCouponDay = "";
        }
        return cost;
    }

    /// <summary>Готвене: цената с купона „Безплатно готвене“ (ако е активен – използва се).</summary>
    private int CookCost(int cost) => PayWithCoupon(cost, "food");

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
        TodayRec.Hobbies.Add("game_release");
        RememberProjectDone();
        // Залата на славата: играта, която направихте заедно – с това, което ти реши за нея.
        string day = DateTime.Today.ToString("d MMMM yyyy", new System.Globalization.CultureInfo("bg-BG"));
        if (!_save.HallOfFame.Any(f => f.Name == _save.GameName && f.Done == day))
            _save.HallOfFame.Add(new FameEntry
            {
                Id = Reviews.NewId(CurrentProject.Id, _save.HallOfFame),
                Kind = CurrentProject.Kind,
                Type = CurrentProject.Id,
                Name = _save.GameName,
                Facts = CurrentProject.Questions.Where(q => q.Key != "name" && _save.ProjectAnswers.ContainsKey(q.Key))
                    .Select(q => q.Label + ": " + _save.ProjectAnswers[q.Key]).ToList(),
                Theme = _save.ProjectAnswers.GetValueOrDefault("theme", ""),
                Goal = _save.ProjectAnswers.GetValueOrDefault("goal", ""),
                Hero = _save.ProjectAnswers.GetValueOrDefault("hero", ""),
                Done = day,
                Started = _save.ProjectStartedAt?.ToString("yyyy-MM-dd") ?? "",
                DoneDay = DateTime.Today.ToString("yyyy-MM-dd"),
                WorkMinutes = _save.ProjectWorkMinutes,
                Tasks = _save.ProjectTasks,
                Chests = ProjectStages.Length,
                Items = GotItems().Count(x => !x.StartsWith("nuts:")),
                Nuts = GotItems().Where(x => x.StartsWith("nuts:")).Sum(x => int.TryParse(x[5..], out int n) ? n : 0),
                Rare = GotItems().Count(x => Loot.RarityOf(x) is Rarity.Rare or Rarity.Legendary),
            });
        // Стари записи без код – получават; после – файлът за разработчика (ревютата).
        foreach (var f in _save.HallOfFame.Where(f => f.Id.Length == 0)) f.Id = Reviews.NewId(f.Type.Length > 0 ? f.Type : "project", _save.HallOfFame);
        if (!SelfTest.Running) Reviews.Export(_save.HallOfFame);
        return CurrentProject.FinishLine;
    }

    /// <summary>Всичко получено от сандъчетата на сегашния проект (+ последното, което се отваря сега).</summary>
    private IEnumerable<string> GotItems() =>
        _save.ProjectGot.SelectMany(RewardItems).Concat(_gotItems).Where(x => x is not ("box" or "name") && !x.StartsWith("rank:")).Select(x => x.TrimStart('?'));

    /// <summary>Отговорен ли е въпросът (името – с името на проекта).</summary>
    public bool Answered(ProjectQuestion q) => q.Key == "name" ? _save.GameName.Length > 0 : _save.ProjectAnswers.ContainsKey(q.Key);

    /// <summary>Въпросът, на който Тут още не е отговорила (а етапът му е минат), или null.</summary>
    public ProjectQuestion? PendingQuestion =>
        CurrentProject.Questions.FirstOrDefault(q => q.AfterStage < _save.ProjectPaid && !Answered(q));

    /// <summary>Проектът чака: неотворено сандъче или въпрос без отговор.</summary>
    public bool ProjectBlocked => ProjectHasClaim || PendingQuestion != null;

    /// <summary>Опциите на въпроса (за името – три случайни от имената на вида).</summary>
    public string[] QuestionOptions(ProjectQuestion q) =>
        q.Key == "name" ? CurrentProject.Names.OrderBy(_ => _rng.Next()).Take(3).ToArray() : q.Options;

    /// <summary>Тут отговори: записва го, а натрупаната работа влиза в проекта.</summary>
    public void AnswerProject(ProjectQuestion q, string value)
    {
        value = value.Trim();
        if (value.Length == 0) return;
        if (q.Key == "name") NameGame(value);
        else
        {
            _save.ProjectAnswers[q.Key] = value;
            Say(R.Pick("project.idea.ok", _save.OwnerName).Replace("{отговор}", value), 4);
        }
        ResumeProject();
        NotifyCare();
        Persist();
    }

    /// <summary>Мечо пита (балонче с опции и поле за свое).</summary>
    public void AskProjectIdea(string key)
    {
        if (CurrentProject.Questions.FirstOrDefault(x => x.Key == key) is not { } q || Answered(q)) return;
        Ask(new ChatQuestion(q.Question, QuestionOptions(q).Select(o => new ChatOption(o, p => p.AnswerProject(q, o))).ToArray())
        {
            FreeText = (p, text) => p.AnswerProject(q, text),
            FreePrompt = q.FreePrompt,
            FreeButton = q.Key == "name" ? "Кръсти я" : "Това",
        });
    }

    /// <summary>Етап 9: как да кръстим играта (въпросът „name“).</summary>
    public void AskGameName() => AskProjectIdea("name");

    /// <summary>Вече не чака: натрупаната работа влиза в проекта (и може да мине още етап).</summary>
    private void ResumeProject()
    {
        if (ProjectBlocked || _save.ProjectBank <= 0) return;
        double bank = _save.ProjectBank;
        _save.ProjectBank = 0;
        AddProjectPoints(bank);
    }

    private double _nextNudge;

    /// <summary>
    /// Проектът чака: Мечо напомня (най-много на 15 минути) – пита въпроса или казва да
    /// отвориш сандъчето, с бутон, който отваря проекта направо при него.
    /// </summary>
    /// <summary>Проектът чака: предлага мисъл на режисьора (той решава кога да напомни).</summary>
    private void NudgeProject()
    {
        if (SelfTest.Running || ProjectNoRewards || !ProjectBlocked) return;
        Think("project.nudge", 6, cooldownHours: 1);
    }

    internal void ProjectNudge(bool force = false)
    {
        if (SelfTest.Running || ProjectNoRewards || ProjectDone && !ProjectHasClaim) return;
        if (!force && Now < _nextNudge) return;
        if (!ProjectBlocked) return;
        _nextNudge = Now + 15 * 60;
        if (PendingQuestion is { } q) { AskProjectIdea(q.Key); return; }
        Ask(new ChatQuestion("Имаш неотворено сандъче от проекта! Отвори го, за да продължим нататък.",
            new ChatOption("Отвори го", p => ProjectWindow.Open(p)),
            new ChatOption("После", _ => { })));
    }

    internal void NameGame(string name)
    {
        _save.GameName = name;
        _save.ProjectNote = $"{CurrentProject.Kind} се казва „{name}“!";
        Say($"„{name}“! Звучи страхотно – пиша го на корицата.", 5);
        Emote("sparkle", 3);
        NotifyCare();
        Persist();
    }

    /// <summary>Веднъж в секунда: разказва за проекта (в началото) и писмата от феновете (след етап 11).</summary>
    private double _nextChooseNudge;

    private void UpdateProject()
    {
        // Няма проект: от време на време Мечо пита какво да почнете (през режисьора).
        if (NoProject && !SelfTest.Running && Now >= _nextChooseNudge)
        {
            _nextChooseNudge = Now + 60;
            Think("project.choose", 7, cooldownHours: 6);
        }
        // Стари записи: наградите отвъд видимото бяха теглени предварително (често без дрехи,
        // защото още нямаше гардероб) – теглят се наново, когато се стигне до тях.
        if (_save.ProjectPlanVersion < 3 && FurniturePlaced(GameBox)) ToggleFromMenu(GameBox);   // кутията вече не е в „Неща“ – прибира се
        if (_save.ProjectPlanVersion < 2)
        {
            for (int k = _save.ProjectPaid + 1; k < _save.ProjectPlan.Count; k++)
                if (!_save.ProjectOpened.Contains(k)) _save.ProjectPlan[k] = "";
            _save.ProjectPlanVersion = 2;
        }
        if (_save.ProjectPlanVersion < 3) _save.ProjectPlanVersion = 3;
        // Стари записи: проектът чака, а точките са отвъд етапа – остатъкът отива настрана.
        if ((ProjectBlocked || ProjectDone) && _save.ProjectPaid > 0 && _save.ProjectPoints > ProjectTotalTo(_save.ProjectPaid - 1))
        {
            double cap = ProjectTotalTo(_save.ProjectPaid - 1);
            if (!ProjectDone) _save.ProjectBank += _save.ProjectPoints - cap;
            _save.ProjectPoints = cap;
            NotifyCare();
        }
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
