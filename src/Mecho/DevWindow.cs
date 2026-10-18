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
        _status.Text = (_pet.DevMode ? "DEV MODE (има всичко) · " : "") + $"Лешници {s.Hazelnuts} · Ситост {s.Fullness:0} · Енергия {s.Energy:0} · Подарък: {gift} · " +
                       $"Купени: {s.Owned.Count} неща, {s.OwnedClothes.Count} дрехи · Задачи общо {s.TasksDoneTotal}\n{_pet.EnergyDebug}";
    }
}

/// <summary>Паролата за Developer панела (за да не го отваря Тут случайно).</summary>
public sealed class DevPasswordWindow : Window
{
    private readonly PasswordBox _box = new() { Width = 180, FontSize = Ui.Body, Padding = new Thickness(4, 2, 4, 2) };
    private readonly TextBlock _hint = new() { Foreground = Muted, Margin = new Thickness(0, 6, 0, 0), Text = " " };
    private readonly PetWindow _pet;

    private DevPasswordWindow(PetWindow pet)
    {
        _pet = pet;
        Title = "Мечо: Developer";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = GameFont;
        FontSize = Body;
        Foreground = Ink;
        UseLayoutRounding = true;
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.Aliased);

        var ok = Button("Влез", Submit, primary: true);
        var cancel = Button("Откажи", () => { DialogResult = false; });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        var body = new StackPanel();
        body.Children.Add(new TextBlock { Text = "Парола за Developer:", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 6) });
        body.Children.Add(_box);
        body.Children.Add(_hint);
        body.Children.Add(buttons);
        Content = new Border
        {
            Background = WoodTexture,
            BorderBrush = Ink,
            BorderThickness = new Thickness(3),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(7),
            Child = new Border { Background = PaperTexture, BorderBrush = Ink, BorderThickness = new Thickness(2), Padding = new Thickness(12), Child = body },
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) Submit();
            else if (e.Key == Key.Escape) DialogResult = false;
        };
        Loaded += (_, _) => { Activate(); _box.Focus(); };
    }

    private void Submit()
    {
        if (_pet.DevPasswordOk(_box.Password))
        {
            DialogResult = true;
            return;
        }
        _box.Clear();
        _hint.Text = "Грешна парола.";
    }

    /// <summary>Пита за паролата (веднъж, докато програмата е пусната). true – може.</summary>
    public static bool Ask(PetWindow pet)
    {
        if (pet.DevUnlocked) return true;
        bool ok = new DevPasswordWindow(pet).ShowDialog() == true;
        if (ok) pet.DevUnlocked = true;
        return ok;
    }
}

public sealed partial class PetWindow
{
    /// <summary>Паролата за Developer е въведена (до затваряне на програмата).</summary>
    internal bool DevUnlocked { get; set; }

    internal bool DevPasswordOk(string password) => string.Equals(password.Trim(), "mecho", StringComparison.OrdinalIgnoreCase);

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

        yield return (unlock, "Dev Mode: вкл./изкл. (има всичко; при изключване всичко се връща)", () => SetDevMode(!DevMode));
        yield return (unlock, "Отключи всичко", DevUnlockAll);
        yield return (unlock, "Заключи всичко", DevLockAll);
        yield return (unlock, "Изчисти задачите и историята", () =>
        {
            CoreEngine.Bus.Send(new Mecho.Core.SetTasks(Mecho.Core.TasksState.Empty));
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
        yield return (food, "Покажи зоната за даване на храна (5 с)", DevShowFeedZone);
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

        foreach (var (t, label) in new[] { (-6.0, "−6°C мраз"), (3.0, "3°C много студено"), (8.0, "8°C студено"), (13.0, "13°C хладно"), (18.0, "18°C приятно"), (22.0, "22°C топло"), (27.0, "27°C горещо"), (33.0, "33°C жега") })
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
        yield return (talk, "Въпросник: питай сега (неотговорен)", () =>
        {
            var q = Survey.NextUnanswered(_save, _rng) ?? Survey.All.Values.ElementAtOrDefault(_rng.Next(Math.Max(1, Survey.All.Count)));
            if (q != null) Ask(Survey.Ask(this, q));
        });
        yield return (talk, "Въпросник: спомен от отговорите", () => Say(Survey.RemarkFor(_save, _rng) ?? "Още нищо не си ми казала.", 5));
        yield return (talk, "Въпросник: отвори otgovori.txt", () =>
        {
            Survey.Export(_save);
            if (!SelfTest.Running) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Survey.AnswersPath) { UseShellExecute = true });
        });
        yield return (talk, "Въпросник: забрави отговорите", () => { _save.Answers.Clear(); Survey.Export(_save); Persist(); });
        yield return (talk, "„Ще се работи ли днес?“ – питай", () => Ask(Conversations.WorkPlan(this, again: false)));
        yield return (talk, "Ден: забрави плана", () => { _save.WorkPlan = ""; _save.WorkPlanDay = ""; Say("Не знам какъв ден е днес.", 3); });

        yield return (modes, "Сам решава сега", () => { _freeUntil = 0; SetIdle(); });
        yield return (modes, "Работи си", WorkRandom);
        yield return (modes, "Спри (като „Да, спри“)", StopAction);
        yield return (modes, "Приспи, докато готви", () =>
        {
            if (_cook == null) Say("Първо пусни нещо да се готви.", 2);
            else GoToSleep();
        });

        // ── Новите неща ──
        const string walk = "Разходка", giftNew = "Подарък в лапите", garden = "Растението", radio = "Радио, хранилка, бюро", emotes = "Емоции",
            memory = "Спомени и любима храна", pins = "Закачени и задачи", looks = "Скинове, пижами, меню", misc = "Лампа, звук, енергия";
        yield return (walk, "Разходи се 1 мин", () => GoForWalk(1));
        yield return (walk, "Разходи се 10 мин", () => GoForWalk(10));
        yield return (walk, "Вечерна дрямка-разходка 2 мин", () => { _save.Energy = 10; _save.WalkUntil = DateTime.MinValue; GoForWalk(2); _save.WalkNap = true; });
        yield return (walk, "Сам тръгва (1 мин, както когато скучае)", () => GoForWalk(1, solo: true));
        yield return (walk, "Сам тръгва при следващия избор (шанс 30%)", () => { _nextSoloWalk = 0; _freeUntil = 0; });
        yield return (walk, "Желание: даде му каквото иска (награда)", () =>
        {
            if (Wishes() is { Length: > 0 } w)
            {
                TodayRec.WishGifts = 0;
                for (int i = 0; i < 100 && !WishThanks(w[0]); i++) TodayRec.WishGifts = 0;
            }
        });
        yield return (walk, "Повикай го (като иконката)", () => ReturnFromWalk(called: true));
        yield return (walk, "Връща се сам (времето изтече)", () => { if (OnWalk) _save.WalkUntil = DateTime.Now; });
        foreach (var color in GiftColors)
        {
            string c = color;
            yield return (giftNew, "Подаръкът: " + (c.Length == 0 ? "син" : c), () =>
            {
                _giftWindow?.Close();
                _save.GiftColor = c;
                _save.GiftPending = false;
                PresentGift();
            });
        }
        yield return (giftNew, "Цял път: излиза и се връща", () => { _giftWindow?.Close(); _save.GiftPending = false; StartGiftRun(); });
        yield return (giftNew, "Държи подаръка", () => { _giftWindow?.Close(); _save.GiftPending = true; HoldGift(); });
        yield return (giftNew, "Подай го (като цъкнеш мечока)", () => OpenHeldGift());
        yield return (giftNew, "Остави го в средата", PutGiftDown);
        yield return (garden, "Извади растението и се грижи", () => { if (!_save.Owned.Contains(Kitchen.PlantPot)) _save.Owned.Add(Kitchen.PlantPot); InstantModes = true; Relax(Scenes.Plant); InstantModes = false; });
        yield return (garden, "Прибери растението", () => { if (InGarden) { GetUp(); } });
        yield return (garden, "Полива (лейка)", () => DevPlantCare("wateringcan"));
        yield return (garden, "Разкопава (лопатка)", () => DevPlantCare("shovel"));
        yield return (garden, "Подрязва (ножица)", () => DevPlantCare("scissors"));
        yield return (garden, "Любува му се", () => DevPlantCare("admire"));
        yield return (garden, "Расте с времето сега", () => { _save.PlantLastGrow = DateTime.Now.AddDays(-5.1); GrowPlantByTime(); DrawGarden(); Persist(); });
        yield return (garden, "Смени вида (слънчоглед → монстера → кактус)", () =>
        {
            var skins = Furniture.SkinsFor(Kitchen.PlantPot);
            foreach (var k in skins) if (k.Price > 0 && !_save.Owned.Contains($"skin_{k.Item}_{k.Id}")) _save.Owned.Add($"skin_{k.Item}_{k.Id}");
            int i = Array.FindIndex(skins, k => k.Id == Furniture.Current(_save, Kitchen.PlantPot).Id);
            UseSkin(skins[(i + 1) % skins.Length]);
            DrawGarden();
        });
        yield return (garden, "Следваща картина на статива", () => { EaselShift++; if (_inScene && _scene is { Easel: true }) LoadSceneArt(_scene); Say("Картина " + EaselNumber, 2); });
        yield return (garden, "Растението: +1 фаза", () => { _save.PlantGrowToday = 0; GrowPlant(fromCare: true); });
        yield return (garden, "Растението: голямо (фаза 10)", () => { _save.PlantStage = PlantStages - 1; DrawGarden(); Persist(); });
        yield return (garden, "Растението: кълнче (фаза 1)", () => { _save.PlantStage = 0; DrawGarden(); Persist(); });
        yield return (radio, "Радио: купи и сложи", () => { if (!_save.Owned.Contains(Kitchen.Radio)) _save.Owned.Add(Kitchen.Radio); SetRadio(true); });
        yield return (radio, "Радио: вкл./изкл.", () => SetRadioOn(!_save.RadioOn));
        yield return (radio, "Танцувай до радиото", () => { if (!RadioPlaying) { if (!_save.Owned.Contains(Kitchen.Radio)) _save.Owned.Add(Kitchen.Radio); SetRadio(true); } Later(0.5, DanceByRadio); });
        yield return (radio, "Хранилка: купи и сложи", () => { if (!_save.Owned.Contains(Kitchen.Feeder)) _save.Owned.Add(Kitchen.Feeder); SetFeeder(true); });
        yield return (radio, "Хранилка: напълни я", FillFeeder);
        yield return (radio, "Птичка – сега", () => { _save.FeederFood = Math.Max(1, _save.FeederFood); _nextBird = 0; });
        yield return (radio, "Бюро и столче: купи / махни", () =>
        {
            bool has = _save.Owned.Contains(Kitchen.Desk);
            if (has) _save.Owned.Remove(Kitchen.Desk); else _save.Owned.Add(Kitchen.Desk);
            if (_inScene && _scene != null) LoadSceneArt(_scene);
        });
        foreach (var e in new[] { "heart", "zzz", "idea", "exclaim", "question", "note", "sweat", "hungry", "cold", "sparkle", "happy", "sad" })
        {
            string em = e;
            yield return (emotes, em, () => Emote(em, 4));
        }
        yield return (emotes, "Сам показва как се чувства", () => { _nextAmbientEmote = 0; UpdateAmbientEmote(Now); });
        yield return (memory, "Сутрешен спомен сега", () => { if (!MorningMemory()) Say("Няма записан вчерашен ден.", 3); });
        yield return (memory, "Измисли богато „вчера“", () =>
        {
            var y = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd");
            _save.Days.RemoveAll(d => d.Date == y);
            _save.Days.Add(new DayRecord
            {
                Date = y, Pomodoros = 1 + _rng.Next(8), WorkMinutes = 60 + _rng.Next(200), TasksDone = _rng.Next(6), BigTasks = _rng.Next(2),
                Foods = Kitchen.Foods.Where(f => f.Fullness >= 5).OrderBy(_ => _rng.Next()).Take(_rng.Next(4)).Select(f => f.Id).ToList(),
                FavFed = _rng.Next(2) == 0, Petted = _rng.Next(7), WokeEarly = _rng.Next(2) == 0, NapWoken = _rng.Next(3) == 0,
                BedAt = DateTime.Today.AddHours(_rng.Next(2) == 0 ? -1 : 1), MinFullness = _rng.Next(100), MinEnergy = _rng.Next(100),
                Hobbies = Scenes.Fun.OrderBy(_ => _rng.Next()).Take(2).Select(f => f.Id).ToList(), Clothes = _rng.Next(5),
            });
            _save.Days.Sort((a, b) => string.CompareOrdinal(a.Date, b.Date));
            MorningMemory();
        });
        yield return (memory, "Не сме се виждали 3 дни", () =>
        {
            var d = DateTime.Today.AddDays(-3).ToString("yyyy-MM-dd");
            _save.Days.RemoveAll(x => string.CompareOrdinal(x.Date, DateTime.Today.ToString("yyyy-MM-dd")) < 0 && string.CompareOrdinal(x.Date, d) > 0);
            if (!_save.Days.Any(x => x.Date == d)) _save.Days.Insert(0, new DayRecord { Date = d });
            _save.Days.Sort((a, b) => string.CompareOrdinal(a.Date, b.Date));
            MorningMemory();
        });
        yield return (memory, "Следобеден спомен", LaterMemory);
        yield return (memory, "Любима храна: нова", () => { _save.FavFoodDay = ""; Say("Днес ми се яде: " + (FavoriteFood?.Name ?? "нищо"), 4); NotifyCare(); });
        yield return (memory, "„Днес ми се яде…“", () => { _save.FavFoodSaid = false; TodayRec.FavFed = false; SayFavoriteWish(); });
        yield return (memory, "Дай му любимото", () => { if (FavoriteFood is { } f) { TodayRec.FavFed = false; FavoriteEaten(f); } });
        yield return (pins, "Закачи задачите", () => SetPinned(PinWindow.Tasks, !IsPinned(PinWindow.Tasks)));
        yield return (pins, "Закачи бележките", () => SetPinned(PinWindow.Notes, !IsPinned(PinWindow.Notes)));
        yield return (pins, "Мечо-доро по нова задача", () => { AddTask("Тестова задача", 2); StartFocusOn(_save.Tasks.Last()); });
        yield return (pins, "„Свърши ли задачата?“", AskAboutFocusTask);
        foreach (var t in UiThemes.All)
        {
            var theme = t;
            yield return (looks, "Меню: " + theme.Name, () => { if (!UiThemes.Owns(_save, theme)) _save.Owned.Add(theme.Key); UseUiTheme(theme); });
        }
        foreach (var pj in Clothes.PajamaSets)
        {
            var p = pj;
            yield return (looks, "Пижама: " + p.Name, () => { if (!Clothes.Owns(_save, p)) _save.OwnedClothes.Add(p.Key); UsePajama(p); _save.PajamasAtNight = true; Emote("sparkle"); });
        }
        yield return (looks, "Всички видове мебели и уреди", () =>
        {
            foreach (var k in Furniture.ByItem.Values.SelectMany(x => x))
                if (!_save.Owned.Contains($"skin_{k.Item}_{k.Id}")) _save.Owned.Add($"skin_{k.Item}_{k.Id}");
            Persist();
            NotifyCare();
        });
        const string core = "Ядро (шина, модули, състояние)";
        void Show(string title, string text) => MessageBox.Show(text.Length > 0 ? text : "(празно)", "Ядро: " + title);
        yield return (core, "Модули", () => Show("модули", string.Join("\n", CoreEngine.Modules.Modules.Select(m =>
            $"{m.Id}: {m.Status}{(m.Reason != null ? " – " + m.Reason : "")}{(m.Faults > 0 ? $" (грешки: {m.Faults})" : "")}"))));
        yield return (core, "Последни сигнали", () => Show("сигнали", string.Join("\n", CoreEngine.Bus.Recent.Reverse().Take(30).Select(r =>
            $"{r.At:HH:mm:ss}  {r.Name}  {r.Signal}"))));
        yield return (core, "Модификатори", () => Show("модификатори", CoreEngine.Modifiers.Describe()));
        yield return (core, "Състояние (слоеве)", () => Show("състояние", CoreEngine.Store.Export().ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true })));
        yield return (core, "Лог на ядрото", () => Show("лог", string.Join("\n", (Engine.CoreSetup.Memory?.Lines ?? Array.Empty<string>()).Reverse().Take(40))));
        const string perf = "Скорост (засичане)";
        yield return (perf, "Тест за скорост (доклад, ~1,5 мин) → perf_report.txt", () => _ = RunPerfReport());
        yield return (perf, "Покажи скоростта", () => Say(PerfSummary(), 14));
        yield return (perf, "Нулирай измерването", PerfReset);
        yield return (perf, "Записвай бавните моменти (perf.log): вкл./изкл.", () =>
        {
            PerfLogging = !PerfLogging;
            Say(PerfLogging ? "Записвам бавните моменти в perf.log." : "Спрях да записвам.", 3);
        });
        yield return (perf, "Отвори perf.log", () =>
        {
            if (!System.IO.File.Exists(PerfLogPath)) System.IO.File.WriteAllText(PerfLogPath, "");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(PerfLogPath) { UseShellExecute = true });
        });
        yield return (misc, "Обиколка (tutorial) отначало", () => StartTutorial());
        yield return (misc, "Скрий / покажи всички рисунки", () => SetDrawingsHidden(!DrawingsHidden));
        yield return (misc, "Магазин: днешният лимит е изчерпан", () => { TodayRec.UpgradesBought = UpgradeLimitToday; NotifyCare(); });
        yield return (misc, "Магазин: нулирай днешния лимит", () => { TodayRec.UpgradesBought = 0; NotifyCare(); });
        yield return (misc, "Лампа: купи и сложи", () => { if (!_save.Owned.Contains(Kitchen.Lamp)) _save.Owned.Add(Kitchen.Lamp); SetLamp(true); });
        yield return (misc, "Лампа: изгаси", () => SetLamp(false));
        yield return (misc, "Динг (звук)", () => Sounds.Play(_save));
        yield return (misc, "Енергия: вечерен таван", () => { _save.Energy = 100; Say($"Таванът сега: {EveningCap(DateTime.Now):0}", 3); NotifyCare(); });
        yield return (misc, "Плети", () => { if (!_save.Owned.Contains(Kitchen.YarnBasket)) _save.Owned.Add(Kitchen.YarnBasket); Relax(Scenes.Knit); });
        yield return (misc, "Свири на китара", () => { if (!_save.Owned.Contains(Kitchen.Guitar)) _save.Owned.Add(Kitchen.Guitar); Relax(Scenes.Guitar); });
        yield return (misc, "Сготви сандвич", () => Cook(Kitchen.Foods.First(f => f.Id == "sandwich")));

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

    private static string DevBackupPath => System.IO.Path.Combine(SaveData.Folder, "dev_backup.json");

    /// <summary>Dev Mode: всичко е отключено, а истинският прогрес чака в dev_backup.json.</summary>
    public bool DevMode => System.IO.File.Exists(DevBackupPath);

    /// <summary>
    /// Включва: пази копие на записа, отключва всичко и дава лешници. Изключва: връща
    /// купеното, дрехите, лешниците и вида отпреди (задачите, мечо-дората и т.н. остават).
    /// </summary>
    internal void SetDevMode(bool on)
    {
        if (on == DevMode) return;
        if (on)
        {
            _save.Core = CoreEngine.Store.Export();   // прогресът в ядрото – също в копието
            _save.SaveTo(DevBackupPath);
            DevUnlockAll();
            _save.Hazelnuts = Math.Max(_save.Hazelnuts, 9999);
            Say("Dev Mode: имам всичко!", 3);
        }
        else
        {
            var old = SaveData.LoadFrom(DevBackupPath);
            if (old != null)
            {
                if (_inScene) LeaveScene();
                // Лешниците, притежаваното и предметите на земята са в ядрото.
                if (old.Core != null)
                    CoreEngine.Store.Restore(old.Core, Mecho.Core.EconomyModule.WalletKey, Mecho.Core.EconomyModule.InventoryKey, Mecho.Core.PlacementModule.Key);
                else
                {
                    CoreEngine.Bus.Send(new Mecho.Core.SetInventory(old.LegacyOwned ?? new(), old.LegacyOwnedClothes ?? new(), "dev mode"));
                    CoreEngine.Bus.Send(new Mecho.Core.SetNuts(old.LegacyHazelnuts, "dev mode"));
                }
                _save.Outfit = old.Outfit;
                _save.Skins = old.Skins;
                _save.CouchSkin = old.CouchSkin;
                _save.BedSkin = old.BedSkin;
                _save.Pajama = old.Pajama;
                _save.UiTheme = old.UiTheme;
                _save.Background = old.Background;
                _save.RecipePages = old.RecipePages;
                _save.NextRecipe = old.NextRecipe;
                _save.PlantStage = old.PlantStage;
                Ui.Apply(UiThemes.All.FirstOrDefault(t => t.Id == _save.UiTheme) ?? UiThemes.All[0]);
                _bedProp = null;
            }
            System.IO.File.Delete(DevBackupPath);
            Say("Dev Mode е изключен. Всичко е както беше.", 3);
        }
        PersistNow();
        NotifyCare();
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
        CoreEngine.Bus.Send(new Mecho.Core.SetTasks(Mecho.Core.TasksState.Empty));
        _save.Notes.Clear();
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
