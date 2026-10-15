using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using static Mecho.Ui;

namespace Mecho;

/// <summary>
/// Developer панел („Още“ → „Developer“): бутони за всичко, което мечокът
/// може, за да се тества бързо (лешници, нужди, подаръци, кътчета, анимации…).
/// </summary>
public sealed class DevWindow : Window
{
    private readonly PetWindow _pet;
    private readonly TextBlock _status = new() { Foreground = Muted, TextWrapping = TextWrapping.Wrap };
    private readonly System.Windows.Threading.DispatcherTimer _refresh = new() { Interval = TimeSpan.FromSeconds(1) };

    public DevWindow(PetWindow pet)
    {
        _pet = pet;
        Title = "Мечо: Developer";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = true;
        Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = GameFont;
        FontSize = Body;
        Foreground = Ink;
        UseLayoutRounding = true;
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.Aliased);
        Resources.Add(typeof(System.Windows.Controls.Primitives.ScrollBar), ScrollBarStyle());

        var header = new DockPanel { Margin = new Thickness(4, 0, 0, 6), Background = Brushes.Transparent, Cursor = Cursors.SizeAll };
        header.MouseLeftButtonDown += (_, _) =>
        {
            try { DragMove(); }
            catch (InvalidOperationException) { }
        };
        var close = Chip("✕", Close);
        DockPanel.SetDock(close, Dock.Right);
        header.Children.Add(close);
        header.Children.Add(new TextBlock { Text = "DEVELOPER", FontSize = Ui.Title, FontWeight = FontWeights.Bold, Foreground = Cream, VerticalAlignment = VerticalAlignment.Center });

        var body = new StackPanel { Width = 640 };
        body.Children.Add(_status);
        foreach (var group in pet.DevCommands().GroupBy(c => c.Group))
        {
            var ribbon = Ribbon(group.Key);
            ribbon.Margin = new Thickness(0, 10, 0, 6);
            body.Children.Add(ribbon);
            var wrap = new WrapPanel();
            foreach (var (_, label, act) in group)
            {
                var b = Button(label, () =>
                {
                    try { act(); }
                    catch (Exception ex)
                    {
                        CrashLog.Write(ex);
                        MessageBox.Show(ex.Message, "Грешка");
                    }
                    UpdateStatus();
                });
                b.Padding = new Thickness(8, 3, 8, 4);
                wrap.Children.Add(b);
            }
            body.Children.Add(wrap);
        }

        var scroll = new ScrollViewer { Content = body, MaxHeight = SystemParameters.WorkArea.Height * 0.8, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var root = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        root.Children.Add(new Border
        {
            Background = PaperTexture,
            BorderBrush = Ink,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(0, 0, 6, 6),
            Padding = new Thickness(12),
            Child = scroll,
        });
        Content = new Border
        {
            Background = WoodTexture,
            BorderBrush = Ink,
            BorderThickness = new Thickness(3),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(7, 5, 7, 7),
            Child = root,
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close();
        };
        _refresh.Tick += (_, _) => UpdateStatus();
        _refresh.Start();
        Closed += (_, _) => _refresh.Stop();
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        var s = _pet.Save;
        string gift = s.GiftPending ? "чака да се отвори" : s.GiftAt == DateTime.MinValue ? "днес няма / вече е" : $"в {s.GiftAt:HH:mm}";
        _status.Text = $"Лешници {s.Hazelnuts} · Ситост {s.Fullness:0} · Енергия {s.Energy:0} · Подарък: {gift} · " +
                       $"Купени: {s.Owned.Count} неща, {s.OwnedClothes.Count} дрехи · Задачи общо {s.TasksDoneTotal}\n{_pet.EnergyDebug}";
    }
}

public sealed partial class PetWindow
{
    /// <summary>Всички бутони на Developer панела: (група, надпис, действие).</summary>
    internal IEnumerable<(string Group, string Label, Action Act)> DevCommands()
    {
        const string nuts = "Лешници", needs = "Нужди", gifts = "Подаръци", unlock = "Отключване и прогрес";
        const string bear = "Мечо прави…", scenes = "Кътчета и работа", food = "Храна", clothes = "Гардероб, тапети и магазин";
        const string anims = "Анимации", files = "Файлове";

        yield return (nuts, "+10", () => AddNuts(10));
        yield return (nuts, "+100", () => AddNuts(100));
        yield return (nuts, "+1000", () => AddNuts(1000));
        yield return (nuts, "Нула", () => AddNuts(-_save.Hazelnuts));

        foreach (int v in new[] { 0, 20, 50, 100 })
        {
            int value = v;
            yield return (needs, $"Ситост {value}", () => { _save.Fullness = value; NotifyCare(); });
        }
        foreach (int v in new[] { 0, 10, 30, 100 })
        {
            int value = v;
            yield return (needs, $"Енергия {value}", () => { _save.Energy = value; NotifyCare(); });
        }
        const string draw = "Премигване (тест)";
        yield return (draw, "Спокоен, докато пиша: вкл/изкл", () =>
        {
            _save.CalmWhileTyping = !_save.CalmWhileTyping;
            Say(_save.CalmWhileTyping ? "Докато пишеш, ще стоя мирно." : "Ще се движа и докато пишеш.", 3);
        });
        yield return (draw, "Без прерисуване 30 с", () => DevFreezeDrawing(30));
        yield return (draw, "Без прерисуване 2 мин", () => DevFreezeDrawing(120));
        const string tired = "Умора и вид на деня";
        foreach (var k in Enum.GetValues<DayKind>())
        {
            var kind = k;
            yield return (tired, "Ден: " + DayKindName(kind), () => SetDayKind(kind));
        }
        yield return (tired, "Нов ден (случаен)", () => { _save.DayKindDate = ""; EnsureDayKind(); NotifyCare(); });
        yield return (tired, "Претоварен", DevOverload);
        yield return (tired, "В ритъм", DevFlow);
        yield return (tired, "Пита за дрямка", () => { DevAskNap(); TakeNapQuestion(); });
        yield return (tired, "Дрямка 1 мин", () => TakeNap(1));
        yield return (tired, "Почивка от мечо-доро", () =>
        {
            if (!InFocus) StartFocus();
            _save.PomodoroEndsAt = DateTime.Now;
            UpdatePomodoro();
        });

        yield return (gifts, "Донеси подарък сега", DevGiftNow);
        yield return (gifts, "Подарък направо в средата", DevGiftDrop);
        yield return (gifts, "Нов ден за подаръци", DevGiftNewDay);

        yield return (unlock, "Отключи всичко", DevUnlockAll);
        yield return (unlock, "Заключи всичко", DevLockAll);
        yield return (unlock, "Изчисти задачите и историята", () =>
        {
            _save.Tasks.Clear();
            _save.TaskHistory.Clear();
            _save.TasksDoneTotal = 0;
            Persist();
            NotifyCare();
        });
        yield return (unlock, "Нулирай целия прогрес", DevResetAll);

        yield return (bear, "Танц", () => Play("dance", length: 3));
        yield return (bear, "Радост", () => Play("happy"));
        yield return (bear, "Жонглира", Juggle);
        yield return (bear, "Наднича", Peekaboo);
        yield return (bear, "Пеперуда", StartChase);
        yield return (bear, "При курсора", FollowCursor);
        yield return (bear, "Зар", RollDice);
        yield return (bear, "Чуди се", Wonder);
        yield return (bear, "Галене", Pet);
        yield return (bear, "Приспи", GoToSleep);
        yield return (bear, "Събуди", WakeUp);
        yield return (bear, "Рисуване", OpenDrawing);
        yield return (bear, "Разговор", () => Ask(Conversations.Next(this)));
        yield return (bear, "Кажи нещо сега", () => { _lastMutter = -1000; Mutter(Lines.Chat(_save.OwnerName), 5); });
        yield return (bear, "Тих режим: изкл.", () => SetQuiet(false));

        foreach (var sc in Scenes.All)
        {
            var scene = sc;
            yield return (scenes, scene.Name, () => Relax(scene));
        }
        yield return (scenes, "Почивай си (случайно)", RelaxRandom);
        yield return (scenes, "Стани", GetUp);
        yield return (scenes, "Мечо-доро", StartFocus);
        yield return (scenes, "Спри мечо-дорото", StopPomodoro);
        yield return (scenes, "Работен таймер 1 мин", () => StartTimer(1, "тест", work: true));
        yield return (scenes, "Таймер 1 мин", () => StartTimer(1, "тест", work: false));
        yield return (scenes, "Спри таймера", StopTimer);

        foreach (var f in Kitchen.Foods)
        {
            var dish = f;
            yield return (food, $"Сготви: {dish.Name}", () =>
            {
                DevUnlockFood(dish);
                Cook(dish);
            });
        }
        yield return (food, "Готово веднага", DevFinishCooking);
        yield return (food, "Изчисти храната", DevClearFood);

        yield return (clothes, "Гардероб", () => OpenMenu(MenuWindow.WardrobeTab));
        yield return (clothes, "Облечи случайно", () =>
        {
            foreach (var slot in Clothes.Slots)
            {
                var options = Clothes.All.Where(g => g.SlotId == slot.Id && !g.Hidden).ToList();
                var g = options[_rng.Next(options.Count)];
                if (!Clothes.Owns(_save, g)) _save.OwnedClothes.Add(g.Id);
                _save.Outfit[slot.Id] = g.Id;
            }
            Persist();
            NotifyCare();
        });
        yield return (clothes, "Съблечи", Undress);
        yield return (food, "+1 страница рецепта", () => FindRecipePage());
        yield return (food, "Научи следващата рецепта", () =>
        {
            var next = Recipes.Next(_save);
            if (next == null) return;
            while (!Kitchen.HasRecipe(next, _save)) FindRecipePage();
        });
        yield return (clothes, "Нова оферта на деня", () =>
        {
            DailyOffer.Today(_save, force: true);
            Persist();
            NotifyCare();
        });
        yield return (clothes, "Гардеробът: има/няма", () =>
        {
            if (!_save.Owned.Remove(Kitchen.Wardrobe)) _save.Owned.Add(Kitchen.Wardrobe);
            Persist();
            NotifyCare();
        });
        yield return (clothes, "Следващ тапет", () =>
        {
            foreach (var b in Backdrops.All) if (!Backdrops.Owns(_save, b)) _save.Owned.Add("bg_" + b.Id);
            NextBackdrop();
        });

        foreach (var name in _lib.AnimationNames.OrderBy(n => n))
        {
            string anim = name;
            yield return (anims, anim, () => { if (!_inScene) Play(anim, length: 3); });
        }

        // ── Дребни неща за живот ──
        const string life = "Живот", weather = "Времето и печицата", talk = "Разговори и въпроси", modes = "Сам решава", lines = "Реплики";
        yield return (life, "Доставка: малка", () => { Deliver("🌱", "Тестова саксия", size: 0); _nextDelivery = 0; });
        yield return (life, "Доставка: средна", () => { Deliver("🍲", "Тестова тенджера", size: 1); _nextDelivery = 0; });
        yield return (life, "Доставка: голяма", () => { Deliver("🔥", "Тестова фурна", size: 2); _nextDelivery = 0; });
        yield return (life, "Хълца", Hiccup);
        yield return (life, "Киха", Sneeze);
        yield return (life, "Намира нещо", FindSomething);
        yield return (life, "Любимо място", () => { _nextFavSpot = 0; GoToFavSpot(); });
        yield return (life, "Сутрешен ритуал", () => { _save.RitualDay = ""; if (!MorningRitual()) { _save.RitualDay = ""; Say(R.Any("ritual.wake"), 3); Play("yawn"); } });
        yield return (life, "Гост: пчеличка", () => { EndVisitor(); StartVisitor(); });
        yield return (life, "Стряска се от мишката", () => { Say(R.Any("cursor.startle"), 2.5); Play("think", length: 1.2); });
        yield return (life, "„Ехо, тук ли си?“", () => Say(R.Pick("away.echo", _save.OwnerName), 6));
        yield return (life, "Върна се (30+ мин)", () => { Say(R.Pick("welcome.back", _save.OwnerName), 5); Play("happy"); });
        yield return (life, "Върна се (след часове)", () => { Say(R.Pick("welcome.missed", _save.OwnerName), 5); Play("happy"); });
        yield return (life, "Настроение: чудесно", () => { Joy(JoyPetted); Joy(JoyFed); Joy(JoyWorked); Say(R.Pick("mood.great", _save.OwnerName), 5); });
        yield return (life, "Настроение: тихичко", () => { _save.JoyFlags = 0; Say(R.Pick("mood.quiet", _save.OwnerName), 5); });
        yield return (life, "Скицира", () => Play("work", length: 5, after: () => Say(Lines.Pick(Lines.SketchDone, _save.OwnerName), 4)));
        yield return (life, "Хапва трохичка", () => { Play("eat", length: 2.5); Say(Lines.Pick(Lines.Snack, _save.OwnerName), 3); });
        yield return (life, "Протяга се", () => { Play("yawn"); Say(Lines.Pick(Lines.Sleepy, _save.OwnerName), 3); });
        yield return (life, "Катери се", Climb);
        yield return (life, "Приказка за игри", () => Say(Lines.Pick(Lines.Chatter, _save.OwnerName), 5));

        foreach (var (t, label) in new[] { (22.0, "22°C приятно"), (17.0, "17°C хладно"), (12.0, "12°C студено"), (5.0, "5°C много студено"), (-6.0, "−6°C мраз"), (31.0, "31°C жега") })
        {
            double temp = t;
            yield return (weather, label, () => { _tempC = temp; NotifyCare(); TalkAboutWeather(); });
        }
        foreach (var (code, wind, label) in new[] { (61, 5.0, "Дъжд"), (65, 5.0, "Силен дъжд"), (73, 5.0, "Сняг"), (95, 5.0, "Буря"), (45, 5.0, "Мъгла"), (2, 40.0, "Вятър"), (0, 5.0, "Слънце"), (3, 5.0, "Облаци") })
        {
            int c = code; double w = wind;
            yield return (weather, "Навън: " + label, () =>
            {
                Weather.Fake(c, w);
                _tempC ??= 15;
                NotifyCare();
                Say(R.Pick("weather." + (Weather.Kind(_tempC) ?? "sunny"), _save.OwnerName), 5);
            });
        }
        yield return (weather, "Без времето (няма интернет)", () => { _tempC = null; Weather.Fake(-1, 0); NotifyCare(); });
        yield return (weather, "Купи печицата", () => { if (!Kitchen.Owns(_save, Kitchen.Heater)) _save.Owned.Add(Kitchen.Heater); Persist(); NotifyCare(); });
        yield return (weather, "Печица: включи", () => { if (!Kitchen.Owns(_save, Kitchen.Heater)) _save.Owned.Add(Kitchen.Heater); SetHeater(true); });
        yield return (weather, "Печица: изключи", () => SetHeater(false));
        yield return (weather, "Пита за печицата", () => { _heaterAskUntil = Now + 120; TakeHeaterQuestion(); });
        yield return (weather, "Сяда до печицата", () => { if (_heaterWindow == null) Say("Първо включи печицата.", 2); else WarmUpByHeater(); });
        yield return (weather, "„Доближи печицата“", () => Say(R.Pick("heater.closer", _save.OwnerName), 5));

        foreach (var (name, ask) in Conversations.Topics)
        {
            var a = ask;
            yield return (talk, name, () => Ask(a(this)));
        }
        yield return (talk, "Обяд: пита", () => { AskAboutLunch(); TakeLunchQuestion(); });
        yield return (talk, "Предлага работа", () => Ask(Conversations.WorkOffer(this)));
        yield return (talk, "„Да спра ли?“", () => Ask(Conversations.StopAction(this, CurrentActionName)));
        yield return (talk, "Гладен (въпрос)", () => { double f = _save.Fullness; _save.Fullness = 10; Ask(Conversations.Next(this)); _save.Fullness = f; });
        yield return (talk, "Уморен (въпрос)", () => { double e = _save.Energy; _save.Energy = 10; Ask(Conversations.Next(this)); _save.Energy = e; });
        yield return (talk, "SOS", Sos);

        yield return (modes, "Сам решава сега", () => { _freeUntil = 0; SetIdle(); });
        yield return (modes, "Работи си", WorkRandom);
        yield return (modes, "Спри (като „Да, спри“)", StopAction);
        yield return (modes, "Приспи, докато готви", () =>
        {
            if (_cook == null) Say("Първо пусни нещо да се готви.", 2);
            else GoToSleep();
        });

        yield return (lines, "Отвори repliki.txt", () => OpenPath(R.FilePath));
        yield return (lines, "Презареди репликите", () => Say($"Прочетох репликите наново ({R.Reload()} раздела). Някои се сменят чак след рестарт.", 5));

        yield return (files, "Папката със записа", () => OpenPath(SaveData.Folder));
        yield return (files, "crash.log", () => OpenPath(CrashLog.Path));
        yield return (files, "Презареди рисунките", ReloadArt);
    }

    private void AddNuts(int n)
    {
        _save.Hazelnuts = Math.Max(0, _save.Hazelnuts + n);
        Persist();
        NotifyCare();
    }

    private void DevUnlockFood(Food f)
    {
        if (f.Tool is not (null or Kitchen.Jug or Kitchen.Table) && !_save.Owned.Contains(f.Tool)) _save.Owned.Add(f.Tool);
        if (!Kitchen.HasRecipe(f, _save)) _save.Owned.Add(Kitchen.RecipeId(f));
        _save.Hazelnuts = Math.Max(_save.Hazelnuts, 20);
        _save.BerriesReadyAt = DateTime.MinValue;
        _save.CoffeeTimes.Clear();
    }

    internal void DevLockAll()
    {
        if (_inScene) LeaveScene();
        _save.Owned.Clear();
        _save.OwnedClothes.Clear();
        _save.Outfit.Clear();
        _save.Background = Backdrops.Default;
        _save.CouchSkin = _save.BedSkin = Furniture.Classic;
        _save.Skins.Clear();
        _save.Pajama = "classic";
        _save.UiTheme = "classic";
        Ui.Apply(UiThemes.All[0]);
        _save.StayPut = false;
        Persist();
        NotifyCare();
    }

    /// <summary>Всичко отначало (името и настройките остават).</summary>
    internal void DevResetAll()
    {
        if (MessageBox.Show("Да изтрия ли целия прогрес (лешници, покупки, задачи, бележки)?", "Мечо",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        DevLockAll();
        var fresh = new SaveData();
        _save.Hazelnuts = fresh.Hazelnuts;
        _save.Fullness = fresh.Fullness;
        _save.Energy = fresh.Energy;
        _save.Tasks.Clear();
        _save.TaskHistory.Clear();
        _save.Notes.Clear();
        _save.TasksDoneTotal = 0;
        _save.PomodorosTotal = 0;
        _save.PomodorosToday = 0;
        _save.FocusMinutesBank = 0;
        _save.GiftDay = "";
        _save.GiftPending = false;
        _giftWindow?.Close();
        DevClearFood();
        Persist();
        NotifyCare();
        Say("Започваме отначало!", 3);
    }

    private static void OpenPath(string path)
    {
        if (!System.IO.File.Exists(path) && !System.IO.Directory.Exists(path))
        {
            MessageBox.Show($"Няма го още:\n{path}", "Мечо");
            return;
        }
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
}
