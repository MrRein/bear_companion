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
        bus.Subscribe<Mecho.Core.WorkPaid>(this, s => AddProjectPoints(s.Minutes));
        bus.Subscribe<Mecho.Core.TaskCompleted>(this, s => AddProjectPoints(s.Size switch { 3 => 40, 2 => 20, 1 => 10, _ => 0 }));
        bus.Subscribe<Mecho.Core.TaskReopened>(this, s => AddProjectPoints(-s.Reward * 10));
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
        string reward = ProjectNoRewards ? (i == 14 ? FinishGame() : "") : i switch
        {
            2 => GiveUpgradeOr(Kitchen.BoardGame, i),
            4 => GiveProjectNuts(i),
            8 => "",
            14 => FinishGame(),
            _ => ProjectRandomReward(i),
        };
        string line = R.Text($"project.stage.{i + 1}", _save.OwnerName, ("игра", GameTitle));
        _save.ProjectNote = line;
        Log("star", $"Проектът: „{st.Name}“ – готово!");
        Emote("sparkle", 4);
        React(i == 14 ? "dance" : "happy", i == 14 ? 4 : 0);
        Say(line + (reward.Length > 0 ? " " + reward : ""), 7);
        if (i == 8 && _save.GameName.Length == 0 && !ProjectNoRewards) Later(8, AskGameName);
    }

    private string GameTitle => _save.GameName.Length > 0 ? "„" + _save.GameName + "“" : "играта";

    private int ProjectNutsFor(int i) => i < 5 ? 3 + _rng.Next(3) : i < 10 ? 4 + _rng.Next(3) : 5 + _rng.Next(3);

    private string GiveProjectNuts(int i)
    {
        int n = ProjectNutsFor(i);
        GainNuts(n, "проект");
        return $"+{n} 🌰";
    }

    /// <summary>Случайна награда: 50% лешници, 45% нещо от магазина, 5% мебел (няма ли – по-надолу).</summary>
    private string ProjectRandomReward(int i)
    {
        int roll = _rng.Next(100);
        if (roll >= 95 && GiveRandomUpgrade(Kitchen.FurnitureShelf) is { } furniture) return furniture;
        if (roll >= 50 && GiveRandomShopThing() is { } thing) return thing;
        return GiveProjectNuts(i);
    }

    /// <summary>Нещо от магазина, което още нямаш: уред/хоби, а с гардероб – и дреха, тапет или меню.</summary>
    private string? GiveRandomShopThing()
    {
        var pool = Kitchen.Upgrades.Where(u => u.Shelf != Kitchen.FurnitureShelf && !OwnsOrOrdered(u.Id)).Select(u => (object)u);
        if (DailyOffer.HasWardrobe(_save))
            pool = pool.Concat(Clothes.All.Where(g => !g.Hidden && !Clothes.Owns(_save, g)))
                .Concat(Backdrops.All.Where(b => !Backdrops.Owns(_save, b)))
                .Concat(UiThemes.All.Where(t => !UiThemes.Owns(_save, t)));
        var list = pool.ToList();
        if (list.Count == 0) return null;
        switch (list[_rng.Next(list.Count)])
        {
            case Upgrade u: return GiftUpgrade(u);
            case Garment g: _save.OwnedClothes.Add(g.Id); return $"Подарък: {g.Name}! (в гардероба)";
            case Backdrop b: _save.Owned.Add("bg_" + b.Id); return $"Подарък: тапет „{b.Name}“!";
            case UiTheme t: _save.Owned.Add(t.Key); return $"Подарък: меню „{t.Name}“!";
        }
        return null;
    }

    private string? GiveRandomUpgrade(string shelf)
    {
        var list = Kitchen.Upgrades.Where(u => u.Shelf == shelf && !OwnsOrOrdered(u.Id)).ToList();
        return list.Count == 0 ? null : GiftUpgrade(list[_rng.Next(list.Count)]);
    }

    private string GiveUpgradeOr(string id, int i) =>
        Kitchen.Find(id) is { } u && !OwnsOrOrdered(id) ? GiftUpgrade(u) : ProjectRandomReward(i);

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
