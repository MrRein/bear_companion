using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Mecho;

/// <summary>
/// „Mecho.exe --selftest“: пуска мечока с всичко отключено и минава през менюто,
/// магазина, кътчетата, готвенето и гардероба. Всяка грешка се записва в
/// selftest.log и изходният код е 1. GitHub Actions го пуска преди всяка нова
/// версия, за да не излезе версия, която пада.
/// </summary>
public static class SelfTest
{
    public static string LogPath => Path.Combine(SaveData.Folder, "selftest.log");

    /// <summary>Тече самопроверка (тогава обиколката не се пуска сама).</summary>
    public static bool Running { get; private set; }

    public static int Run()
    {
        Running = true;
        Directory.CreateDirectory(SaveData.Folder);
        File.WriteAllText(LogPath, "");
        var errors = new List<string>();
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, e) =>
        {
            errors.Add(e.Exception.ToString());
            Log("ГРЕШКА: " + e.Exception);
            e.Handled = true;
        };
        app.Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                await Steps(errors);
            }
            catch (Exception ex)
            {
                errors.Add(ex.ToString());
                Log("ГРЕШКА: " + ex);
            }
            Log(errors.Count == 0 ? "OK" : $"{errors.Count} грешки");
            // Накрая пак – само първият ред на всяка (иначе се губят в дългия лог).
            foreach (var e in errors) Log("  ✗ " + e.Split('\n')[0].Trim());
            app.Shutdown(errors.Count == 0 ? 0 : 1);
        });
        // Ако нещо увисне, след 14 минути спираме с грешка.
        var watchdog = new DispatcherTimer { Interval = TimeSpan.FromMinutes(14) };
        watchdog.Tick += (_, _) =>
        {
            Log("ГРЕШКА: тестът увисна");
            app.Shutdown(2);
        };
        watchdog.Start();
        return app.Run();
    }

    public static void Log(string line)
    {
        Console.WriteLine(line);
        try { File.AppendAllText(LogPath, line + Environment.NewLine); }
        catch (Exception) { }
    }

    private static async Task Steps(List<string> errors)
    {
        var save = new SaveData { Hazelnuts = 10000, FirstRunDone = true, StartWithWindows = false };
        var pet = new PetWindow(save);
        pet.Show();
        await Step("старт", () => { }, 1500);
        await pet.SelfTestSteps(Step);

        async Task Step(string name, Action action, int waitMs)
        {
            Log("• " + name);
            int before = errors.Count;
            try
            {
                action();
            }
            catch (Exception ex)
            {
                errors.Add($"„{name}“: {ex}");
                Log($"ГРЕШКА в „{name}“: {ex}");
            }
            await Task.Delay(waitMs);
            if (errors.Count > before) Log($"  ↑ грешка по време на „{name}“");
        }
    }
}

public sealed partial class PetWindow
{
    private Mecho.Core.NeedsChanged? _coreNeeds;
    private readonly object _coreListener = new();   // самопроверката слуша с този собственик (не с мечока – той има свои абонаменти)

    /// <summary>Стъпките на самопроверката (имат достъп до всичко вътре в мечока).</summary>
    internal async Task SelfTestSteps(Func<string, Action, int, Task> step)
    {
        await step("умора: обикновен ден за теста", () =>
        {
            _nextNapAsk = double.MaxValue;
            SetDayKind(DayKind.Normal);
            // Без сами изскачащи спомени и въпроси по време на проверката.
            _save.MemoryDay = _save.MemoryLaterDay = DateTime.Today.ToString("yyyy-MM-dd");
            // На машината за проверка няма мишка и клавиатура: „Тут пише“ би замразил мечока.
            _save.CalmWhileTyping = false;
            _save.TutorialDone = true;
        }, 100);
        await step("гардеробът е заключен", () =>
        {
            OpenMenu(MenuWindow.WardrobeTab);
            _menu!.UpdateLayout();
            if (AtWardrobe) throw new Exception("гардеробът дойде, без да е купен");
        }, 400);
        await step("оферта на деня", () =>
        {
            var t = new SaveData();
            if (DailyOffer.Today(t).Any(k => k.StartsWith("g:") || k.StartsWith("bg:"))) throw new Exception("дрехи без гардероб");
            t.Owned.Add(Kitchen.Wardrobe);
            var first = DailyOffer.Today(t).ToList();
            if (first.Count != DailyOffer.Count || first.Distinct().Count() != first.Count || first.Any(k => DailyOffer.Resolve(k) == null))
                throw new Exception("офертата е грешна: " + string.Join(",", first));
            if (!DailyOffer.Today(t).SequenceEqual(first)) throw new Exception("офертата се смени в същия ден");
            if (first.Select(DailyOffer.GroupOf).Distinct().Count() != first.Count) throw new Exception("две неща от един вид: " + string.Join(",", first));
            t.Owned.Add(Kitchen.Couch);
            for (int i = 0; i < 20; i++)
                if (DailyOffer.Today(t, force: true).Count(k => k.StartsWith("skin:couch")) > 1) throw new Exception("повече от един диван");
            t.Owned.Remove(Kitchen.Couch);
            t.OfferItems = first.ToList();
            foreach (var k in first)
                if (DailyOffer.Resolve(k) is Garment g) t.OwnedClothes.Add(g.Id);
                else if (DailyOffer.Resolve(k) is Backdrop b) t.Owned.Add("bg_" + b.Id);
                else if (DailyOffer.Resolve(k) is PajamaSet p) t.OwnedClothes.Add(p.Key);
            t.OfferDay = "2000-01-01";
            var next = DailyOffer.Today(t);
            if (next.Any(first.Contains)) throw new Exception("купено излезе пак");
            if (DailyOffer.Today(t).Any(k => k.StartsWith("skin:"))) throw new Exception("скин без диван/легло");
            t.OfferItems = new List<string> { "skin:pot:copper" };
            t.OfferDay = DateTime.Today.ToString("yyyy-MM-dd");
            if (DailyOffer.Today(t).Contains("skin:pot:copper")) throw new Exception("друг вид на тенджера без тенджера");
            _menu!.Hide();
        }, 400);
        await step("скин по подразбиране", () =>
        {
            var t = new SaveData { CouchSkin = "velvet" };
            if (Furniture.Current(t, Kitchen.Couch).Id != Furniture.Classic) throw new Exception("некупен скин е сложен");
            t.Owned.Add("skin_couch_velvet");
            if (Furniture.Current(t, Kitchen.Couch).Id != "velvet") throw new Exception("купеният скин не е сложен");
            // Новите скинове (печица, уреди, лампа, китара): всеки има рисунка.
            foreach (var (item, skins) in Furniture.ByItem)
            {
                if (Kitchen.Find(item) == null) throw new Exception("скин за непознат предмет: " + item);
                foreach (var k in skins)
                {
                    bool art = item == Kitchen.Guitar ? _lib.Clothes("tool_" + k.Prop) != null : _lib.HasProp(k.Prop);
                    if (!art) throw new Exception("няма рисунка за скина " + k.Prop);
                }
            }
            t.Skins[Kitchen.Pot] = "copper";
            if (Furniture.CurrentArt(t, Kitchen.Pot, "_b") != "cook_pot_b") throw new Exception("некупен скин на уред");
            t.Owned.Add("skin_pot_copper");
            if (Furniture.CurrentArt(t, Kitchen.Pot, "_b") != "cook_pot_copper_b") throw new Exception("скинът на уреда не е сложен");
        }, 100);
        await step("лампа: кога свети", () =>
        {
            if (!IsDark(new DateTime(2026, 1, 10, 17, 30, 0)) || IsDark(new DateTime(2026, 1, 10, 12, 0, 0))) throw new Exception("зимата");
            if (IsDark(new DateTime(2026, 7, 10, 19, 0, 0)) || !IsDark(new DateTime(2026, 7, 10, 23, 0, 0))) throw new Exception("лятото");
            if (!IsDark(new DateTime(2026, 7, 11, 3, 0, 0))) throw new Exception("нощем");
            _save.Owned.Add(Kitchen.Lamp);
            SetLamp(true);
            UpdateLamp();
            if (_lampWindow == null) throw new Exception("сложената лампа я няма");
            SetLamp(false);
            UpdateLamp();
            if (_lampWindow != null) throw new Exception("прибраната лампа остана");
            SetLamp(true);
            _save.Owned.Remove(Kitchen.Lamp);
            UpdateLamp();
            if (_lampWindow != null) throw new Exception("лампа без лампа");
        }, 300);
        await step("плетене и китара", () =>
        {
            if (IsAsleep) WakeUp();
            foreach (var (scene, item, tool) in new[] { (Scenes.Knit, Kitchen.YarnBasket, "knitting"), (Scenes.Guitar, Kitchen.Guitar, "guitar") })
            {
                if (!_save.Owned.Contains(item)) _save.Owned.Add(item);
                StartScene(scene, 60);
                if (_scene != scene) throw new Exception("не седна: " + scene.Id);
                if (!EffectiveOutfit.TryGetValue(Clothes.Tool.Id, out var held) || held != tool) throw new Exception("няма предмет в лапите: " + scene.Id);
                UpdateFrame();
                LeaveScene();
            }
            _save.Skins[Kitchen.Guitar] = "ukulele";
            _save.Owned.Add("skin_guitar_ukulele");
            StartScene(Scenes.Guitar, 60);
            if (!EffectiveOutfit.TryGetValue(Clothes.Tool.Id, out var uke) || uke != "guitar_ukulele") throw new Exception("скинът на китарата не се вижда");
            UpdateFrame();
            LeaveScene();
            _save.Skins.Remove(Kitchen.Guitar);
            _save.Owned.Remove(Kitchen.YarnBasket);
            _save.Owned.Remove(Kitchen.Guitar);
        }, 600);
        await step("видове мечо-доро", () =>
        {
            foreach (var mode in PomodoroModes)
            {
                SetPomodoroMode(mode.Id);
                if (FocusLength.TotalMinutes != mode.Focus || BreakLength.TotalMinutes != mode.Break) throw new Exception("грешен вид " + mode.Id);
            }
            StartFocus();
            SetPomodoroMode("25");
            if (PomodoroMode.Id != "90") throw new Exception("видът се смени по време на работа");
            OpenMenu(MenuWindow.PomodoroTab);
            _menu!.UpdateLayout();
            StopPomodoro();
            SetPomodoroMode("25");
        }, 600);
        await step("рецепти от страници", () =>
        {
            var t = new SaveData();
            if (Recipes.Next(t) != null || Recipes.AddPage(t) != null) throw new Exception("страници без нито един уред");
            // Учат се само рецепти за уреди, които ги има: с всички уреди – всички рецепти.
            foreach (var u in Kitchen.Upgrades) t.Owned.Add(u.Id);
            int learned = 0, pages = 0;
            while (Recipes.Next(t) is { } next)
            {
                int need = Recipes.PagesFor(next), got = 0;
                Food? done = null;
                while (done == null)
                {
                    done = Recipes.AddPage(t);
                    got++;
                    pages++;
                    if (got > need) throw new Exception($"{next.Id}: повече от {need} страници");
                }
                if (done != next || got != need || !Kitchen.HasRecipe(next, t)) throw new Exception($"{next.Id}: грешно отключване");
                learned++;
            }
            if (learned != Kitchen.Foods.Count(f => f.Cost > 0 && !Kitchen.IsDefaultRecipe(f))) throw new Exception("не всички рецепти се научиха");
            // Всеки уред идва със своята първа рецепта.
            foreach (var tool in new[] { Kitchen.Pot, Kitchen.Pan, Kitchen.Oven, Kitchen.CoffeeMachine, Kitchen.Kettle, Kitchen.Jug,
                Kitchen.Mixer, Kitchen.SandwichMaker, Kitchen.IceCream, Kitchen.Grill })
                if (Kitchen.DefaultRecipe(tool) == null) throw new Exception("уред без рецепта: " + tool);
            if (Recipes.AddPage(t) != null) throw new Exception("страница след всичко");
            FindRecipePage();
            OpenMenu(MenuWindow.FoodTab);
            _menu!.UpdateLayout();
        }, 400);
        await step("доставка", () =>
        {
            _save.Hazelnuts += 100;
            var pot = Kitchen.Upgrades.First(u => u.Id == Kitchen.Pot);
            _save.Owned.Remove(Kitchen.Pot);
            _save.Owned.Remove(Kitchen.RecipeId(Kitchen.Foods.First(f => f.Id == "soup")));
            TodayRec.UpgradesBought = 0;
            Buy(pot);
            if (Kitchen.Owns(_save, Kitchen.Pot)) throw new Exception("тенджерата е отключена преди доставката");
            if (GoalChoices.Any(u => u.Id == Kitchen.Pot)) throw new Exception("поръчаната тенджера пак е в магазина");
            _nextDelivery = 0;
        }, 2500);
        await step("доставката пада", () => { if (_parcel == null) throw new Exception("няма кашон"); }, 3000);
        await step("доставката се отваря", () => { OpenParcel(); }, 2500);
        await step("доставката свърши", () =>
        {
            if (_parcel != null) throw new Exception("кашончето остана");
            if (!Kitchen.Owns(_save, Kitchen.Pot) || _save.Ordered.Contains(Kitchen.Pot)) throw new Exception("тенджерата не се отключи");
            if (!Kitchen.HasRecipe(Kitchen.Foods.First(f => f.Id == "soup"), _save)) throw new Exception("тенджерата дойде без супата");
            _trip = null;
            SetIdle();
        }, 300);
        await step("доставка: „само да отворя доставката“", () =>
        {
            ClearDeliveries();
            _holdParcelTrip = true;
            Deliver("📦", "Тест", size: 0);
            _nextDelivery = 0;
        }, 2500);
        await step("доставка: каза му да почива", () =>
        {
            _holdParcelTrip = false;
            try
            {
                if (_parcel == null || !_parcelLanded) throw new Exception("няма кашон");
                SetIdle();
                Relax(Scenes.Reading);
                if (_trip == null) throw new Exception("не тръгна първо към кашона");
                if (!_trip.StayPut || _trip.Scene != Scenes.Reading) throw new Exception("забрави какво му казах");
            }
            finally
            {
                ClearDeliveries();
                GetUp();
            }
        }, 300);
        double tripX = 0;
        await step("доставка: спира четенето и отива", () =>
        {
            ClearDeliveries();
            SetIdle();
            StartScene(Scenes.Reading, quiet: true);
            tripX = _x;
            Deliver("📦", "Тест", size: 0);
            _nextDelivery = 0;
        }, 2500);
        await step("доставка: тръгна към кашона", () => { if (_trip == null || _inScene) throw new Exception("не тръгна към кашона"); }, 6000);
        await step("доставка: отваря", () => { if (_parcel != null) OpenParcel(); }, 9000);
        await step("доставка: върна се да чете", () =>
        {
            if (_parcel != null) throw new Exception("кашончето остана");
            if (_trip != null || !_inScene || _scene != Scenes.Reading) throw new Exception("не се върна в кътчето");
            if (Math.Abs(_x - tripX) > 2) throw new Exception("не е на старото място");
            GetUp();
        }, 300);
        await step("отключи всичко", DevUnlockAll, 200);
        for (int tab = 0; tab <= MenuWindow.MoreTab; tab++)
        {
            int t = tab;
            await step($"меню, таб {t}", () =>
            {
                OpenMenu(t);
                _menu!.UpdateLayout();
            }, 400);
        }
        await step("магазин: целта и офертата", () => _menu!.SelfTestShop(), 400);
        await step("задачи и история", () =>
        {
            int nuts = _save.Hazelnuts, total = _save.TasksDoneTotal;
            AddTask("Тестова задача", 2);
            SetTaskDone(_save.Tasks[^1], true);
            // Вълна 4б: наградата и броячът са от ядрото (TasksModule).
            if (_save.Hazelnuts != nuts + 3 || _save.TasksDoneTotal != total + 1) throw new Exception("задачата не плати през ядрото");
            _menu!.SelfTestHistory();
        }, 400);
        await step("затвори менюто", () => _menu!.Hide(), 300);

        foreach (var scene in Scenes.All)
        {
            var sc = scene;
            await step($"кътче {sc.Id}", () => Relax(sc), 1200);
        }
        await step("почивай си на случаен принцип", RelaxRandom, 800);
        await step("стани", GetUp, 500);

        // Всички стари ястия, по едно за всеки нов уред и всички в два уреда (иначе тестът става твърде дълъг).
        var newTools = new System.Collections.Generic.HashSet<string>();
        var toCook = Kitchen.Foods.Where(f => f.Id is "berries" or "popcorn" or "meatballs" or "potatoes" or "coffee" or "tea" or "juice_orange" or "juice_berry"
            || f.Prep != null || (f.Tool != null && Kitchen.IsDefaultRecipe(f) && newTools.Add(f.Tool))).ToList();
        foreach (var food in toCook)
        {
            var f = food;
            if (f.Prep != null)
            {
                await step($"готви {f.Id}: първа стъпка", () =>
                {
                    _save.CoffeeTimes.Clear();
                    Cook(f);
                    StartPendingCook();
                    if (_cook?.Tool != f.Prep)
                        throw new Exception($"не почна от {f.Prep} (каза: „{BubbleTextForTest}“; готви: {_cook?.Food.Id}, яде: {_eating?.Id}, " +
                            $"пие: {_inScene && _scene == Scenes.Drink}, спи: {IsResting}, лешници: {_save.Hazelnuts}, храна: {_food.Count}, състояние: {_state})");
                }, 1000);
                await step($"готви {f.Id}: втори уред", () =>
                {
                    DevFinishCooking();
                }, 1200);
                await step($"готови {f.Id}: в главния уред", () =>
                {
                    if (_cook?.Tool != f.Tool) throw new Exception("не мина в " + f.Tool);
                    DevFinishCooking();
                }, 4000);
                await step("изчисти храната", DevClearFood, 300);
                continue;
            }
            await step($"готви {f.Id}", () =>
            {
                _save.BerriesReadyAt = DateTime.MinValue;
                _save.CoffeeTimes.Clear();
                Cook(f);
                StartPendingCook();
            }, 1500);
            await step($"готово: {f.Id}", DevFinishCooking, 5000);
            await step("изчисти храната", DevClearFood, 300);
        }

        await step("два уреда: мечокът сам пренася", () =>
        {
            var two = Kitchen.Foods.First(f => f.Prep != null);
            _save.CoffeeTimes.Clear();
            Cook(two);
            StartPendingCook();
            if (_cook?.Stage != 0) throw new Exception("не почна от първия уред");
            _cook.PopAt = Now - 16;   // готово отдавна: сам минава във втория уред
        }, 1000);
        await step("два уреда: във втория уред", () =>
        {
            if (_cook?.Stage != 1) throw new Exception("първата стъпка чака цъкане");
            DevFinishCooking();
        }, 4000);
        await step("изчисти храната", DevClearFood, 300);
        await step("готвене: идва за края", () =>
        {
            TestCook(Kitchen.Foods.First(f => f.Tool == Kitchen.Pan));
            _cook!.PopAt = Now + 3;
        }, 6000);
        await step("готвене: уредът чака мечока", DevFinishCooking, 3000);
        await step("храната на парчета", () => { foreach (var f in _food) f.UpdateLayout(); DevClearFood(); }, 300);
        await step("готово: чака цъкване", () =>
        {
            TestCook(Kitchen.Foods.First(f => f.Tool == Kitchen.Pan));
            _cook!.PopAt = Now;
        }, 1500);
        await step("готово: цъкнато", () =>
        {
            if (_cook is not { Popped: false }) throw new Exception("храната излезе, без да цъкнеш уреда");
            CookerClicked();
        }, 4000);
        await step("след цъкването", () =>
        {
            if (_cook is { Popped: false }) throw new Exception("цъкването не извади храната");
            DevClearFood();
        }, 300);
        await step("уредът пада плавно", () =>
        {
            TestCook(Kitchen.Foods.First(f => f.Tool == Kitchen.Pot));
            var w = _cook!.Window;
            double ground = w.Bottom;
            w.Place(CookerArt(""), PixelSize, w.CenterX, ground - 200);
            w.FallTo(w.CenterX, ground);
            if (!w.IsFalling) throw new Exception("уредът не пада");
        }, 1500);
        await step("уредът кацна", () =>
        {
            if (_cook!.Window.IsFalling) throw new Exception("уредът не кацна");
            StopCooking();
        }, 500);
        await step("уредът върху мечока", () =>
        {
            TestCook(Kitchen.Foods.First(f => f.Tool == Kitchen.Oven));
            _x = _cook!.Window.CenterX;
            SetIdle();
        }, 4000);
        await step("мечокът отстъпи до уреда", () =>
        {
            if (OverlapsCooker(_cook!, _x)) throw new Exception("още стои в уреда");
            StopCooking();
        }, 500);
        await step("сок в каничка", () =>
        {
            TestCook(Kitchen.Foods.First(f => f.Tool == Kitchen.Jug));
            if (_cook?.Tool != Kitchen.Jug) throw new Exception("сокът не е в каничка");
            DevFinishCooking();
        }, 4000);
        await step("яде: храната към устата", () =>
        {
            DevClearFood();
            StartEating(Kitchen.Foods.First(f => f.Id == "meatballs"), _lib.GetProp("food_meatballs", 16, 16, 0).Image, last: true);
        }, 2500);
        await step("яде: хапва", () =>
        {
            if (_anim is not ("eat" or "eat_hold") || HeldPoint() == null) throw new Exception("не държи храната в лапите");
            EatBite();
        }, 1500);
        await step("яде: свършва", () =>
        {
            _bitesLeft = 1;
            EatDone();
            foreach (var f in Kitchen.Foods) _ = FoodTalk.For(f);
        }, 500);
        await step("забравената храна се разчиства", () =>
        {
            SpawnFood(Kitchen.Foods[0], 1, 0, new Point(_x, _bearRect.Top), fromCooker: false);
            foreach (var f in _food) f.DevAge(FoodLifetime + 1);
        }, 600);
        await step("балончето: веднага над мечока", () =>
        {
            Say("Тест на балончето – трябва да е точно над мечока.", 3);
            if (!_bubbleWindow.Showing) throw new Exception("балончето не се показа");
            double bottom = _bubbleWindow.Top + _bubbleWindow.ActualHeight;
            double mid = _bubbleWindow.Left + _bubbleWindow.ActualWidth / 2;
            if (bottom > _bearRect.Top + 1 || Math.Abs(mid - (_bearRect.Left + _bearRect.Width / 2)) > _bearRect.Width + 300)
                throw new Exception($"балончето не е над мечока ({_bubbleWindow.Left:0},{_bubbleWindow.Top:0} спрямо {_bearRect})");
            if (Width > 400 * Math.Max(1, BearScale / 3.0)) throw new Exception("прозорецът на мечока е твърде голям: " + Width);
        }, 300);
        await step("балончето: скрива се с мечока", () =>
        {
            Hide();
            if (_bubbleWindow.Showing) throw new Exception("балончето остана без мечока");
            Show();
            HideBubble();
            if (_bubbleWindow.Showing) throw new Exception("балончето не се скри");
        }, 300);
        await step("пеперуда: в свой прозорец", () =>
        {
            SetIdle();
            StartChase();
        }, 600);
        await step("пеперуда: лети", () =>
        {
            if (!_butterflyWindow.Showing) throw new Exception("пеперудата не се вижда");
            _bfActive = false;
            SetIdle();
        }, 300);
        await step("пеперуда: отлетя", () =>
        {
            if (_butterflyWindow.Showing) throw new Exception("пеперудата остана");
        }, 200);
        await step("скрий мечока", () =>
        {
            if (_food.Count > 0) throw new Exception("храната не се разчисти");
            SpawnFood(Kitchen.Foods[0], 1, 0, new Point(_x, _bearRect.Top), fromCooker: false);
            Hide();
            if (_food.Any(f => f.IsVisible)) throw new Exception("храната не се скри");
        }, 500);
        await step("покажи мечока", () =>
        {
            Show();
            if (_food.Any(f => !f.IsVisible)) throw new Exception("храната не се показа");
            DevClearFood();
        }, 800);
        await step("готвене: спри", () =>
        {
            var pot = Kitchen.Foods.First(f => f.Tool == Kitchen.Pot);
            Cook(pot);
            StartPendingCook();
            // Вълна 3: какво се готви е в ядрото (оттам се продължава след рестарт).
            if (CoreEngine.Store.Get<Mecho.Core.CookingState>().FoodId != pot.Id || _save.CookFood != pot.Id)
                throw new Exception("готвенето не е в ядрото");
            _save.Energy = 50;
            GoToSleep();
            if (IsAsleep || _save.SleepingByChoice) throw new Exception("заспа, докато се готви");
            AskStopCooking();
            _chat?.Hide();
            StopCooking();
            if (CoreEngine.Store.Get<Mecho.Core.CookingState>().Cooking) throw new Exception("спряното готвене остана в ядрото");
        }, 800);
        await step("готвене и мечо-доро заедно", () =>
        {
            Cook(Kitchen.Foods.First(f => f.Tool == Kitchen.Oven));
            StartPendingCook();
            StartFocus();
        }, 1500);
        await step("готово по време на работа", () => { DevFinishCooking(); }, 5000);
        await step("спри мечо-дорото", () => { StopPomodoro(); DevClearFood(); }, 1000);
        await step("пие чай", () => { DevClearFood(); StartDrinking(Kitchen.Foods.First(f => f.Id == "tea")); }, 6000);
        await step("пие сок", () => StartDrinking(Kitchen.Foods.First(f => f.Id == "juice_orange")), 6000);
        await step("пиене: всички кадри с чаша", () =>
        {
            foreach (var cup in new[] { "tea", "coffee", "juice_orange", "juice_berry" })
                foreach (var anim in new[] { "sit_drinking", "sit_drinking_sip", "sit_drinking_blow" })
                    for (int i = 0; i < 42; i++)
                        _dresser.Frame(_lib, anim, i, new Dictionary<string, string>(_save.Outfit) { ["cup"] = cup });
            LeaveScene();
        }, 300);
        string? oldShirt = null;
        await step("гардероб докато чете: не идва", () =>
        {
            if (!Kitchen.Owns(_save, Kitchen.Wardrobe)) _save.Owned.Add(Kitchen.Wardrobe);
            DevClearFood();
            if (IsAsleep) WakeUp();
            if (_inScene) LeaveScene();
            SetIdle();
            StartScene(Scenes.Reading, 300);
            OpenMenu(MenuWindow.WardrobeTab);
            if (AtWardrobe || _scene != Scenes.Reading) throw new Exception("гардеробът прекъсна четенето само с отварянето на таба");
        }, 500);
        await step("гардероб докато чете: смени дреха", () =>
        {
            oldShirt = _save.Outfit.TryGetValue("shirt", out var o) ? o : null;
            var shirt = Clothes.All.First(g => g.SlotId == "shirt" && !g.Hidden && Clothes.Owns(_save, g) && g.Id != oldShirt);
            Wear(shirt);
            if (_closet == null || _inScene) throw new Exception("не спря да отиде до гардероба");
            // Видимото облекло по пътя (без пижамата – тя зависи от часа на проверката).
            (_shownOutfit ?? _save.Outfit).TryGetValue("shirt", out var shown);
            if (shown != oldShirt) throw new Exception("преоблече се пред очите ни по пътя");
        }, 4000);
        await step("гардероб докато чете: гардеробът дойде", () =>
        {
            if (!AtWardrobe) throw new Exception("гардеробът не дойде след смяната");
            _menu!.Hide();
        }, 5000);
        await step("гардероб докато чете: пак чете", () =>
        {
            if (_closet != null || !_inScene || _scene != Scenes.Reading) throw new Exception("не се върна да чете");
            if (_sceneUntil <= Now) throw new Exception("забрави колко му оставаше");
            LeaveScene();
        }, 300);
        await step("гардероб", () => { OpenMenu(MenuWindow.WardrobeTab); _menu!.SelfTestWardrobe(); }, 800);
        await step("скин на менюто", () =>
        {
            foreach (var t in UiThemes.All)
            {
                if (!UiThemes.Owns(_save, t)) _save.Owned.Add(t.Key);
                UseUiTheme(t);
                if (((System.Windows.Media.SolidColorBrush)Ui.Honey).Color != t.Honey) throw new Exception("скинът не се сложи: " + t.Id);
                UiThemes.Preview(t);
                _menu!.SelfTestWardrobe();
            }
            UseUiTheme(UiThemes.All[0]);
        }, 600);
        foreach (var g in Clothes.All)
        {
            var garment = g;
            await step($"облечи {garment.Id}", () => Wear(garment), 1200);
        }
        foreach (var b in Backdrops.All)
        {
            var backdrop = b;
            await step($"фон {backdrop.Id}", () => UseBackdrop(backdrop), 150);
        }
        await step("всички анимации облечен", () =>
        {
            foreach (var anim in new[] { "idle", "walk", "sleep", "eat", "happy", "dance", "read", "focus", "cook", "bounce",
                                         "sad", "think", "tired", "walk_tired", "sleep_peek", "drag", "fall", "eat_hold" })
            {
                if (_lib.Layer(anim, "shirt") == null) throw new Exception($"няма шаблон за блуза: {anim} (assets/clothes/layers)");
                for (int i = 0; i < 8; i++) _dresser.Frame(_lib, anim, i, _save.Outfit);
            }
        }, 200);
        await step("дрехи от clothes.json и шал под ръцете", () =>
        {
            if (Clothes.Find("tee_navy") == null) throw new Exception("clothes.json не се зареди");
            if (_lib.Layer("eat", "front") == null) throw new Exception("няма слой front за eat");
            if (_lib.Layer("idle", "sleeves") == null) throw new Exception("няма слой sleeves (ръкави)");
            foreach (var kind in new[] { "tee", "blouse", "sweater", "gloves", "shoes", "pants" })
                if (_lib.Clothes("icons/" + kind) == null) throw new Exception("няма картинка за гардероба: " + kind);
            foreach (var g in Clothes.All.Where(g => g.Slot.Sprite == null && !g.Hidden))
                if (_dresser.Preview(_lib, g) == null) throw new Exception("няма картинка за " + g.Id);
            foreach (var shirt in new[] { "tee_white", "sweater_red" })
            {
                _save.Outfit["shirt"] = shirt;
                _save.Outfit["glasses"] = "round";
                _save.Outfit["hat"] = "beanie";
                foreach (var anim in new[] { "idle", "eat", "happy", "read" }) _dresser.Frame(_lib, anim, 0, _save.Outfit);
            }
            if (_lib.Layer("read", "book") == null || _lib.Clothes("book_6") == null) throw new Exception("няма книгите");
            foreach (var book in new[] { "book_1", "book_6" })
            {
                _dresser.Book = book;
                _dresser.Frame(_lib, "read", 0, _save.Outfit);
            }
            _save.OwnedClothes.Add("tee_navy");
            _save.Outfit["neck"] = "scarf_red";
            for (int i = 0; i < 4; i++) _dresser.Frame(_lib, "eat", i, _save.Outfit);
        }, 200);
        await step("аксесоари", () =>
        {
            foreach (var g in Clothes.All.Where(g => g.SlotId is "glasses" or "neck")) Wear(g);
            foreach (var anim in new[] { "idle", "walk", "read", "dance", "sleep" })
                for (int i = 0; i < 4; i++) _dresser.Frame(_lib, anim, i, _save.Outfit);
        }, 800);
        await step("пижама", () =>
        {
            var night = new Dictionary<string, string>(_save.Outfit);
            foreach (var p in Clothes.PajamaSets)
            {
                foreach (var (slot, id) in Clothes.PajamaOutfit(p)) night[slot] = id;
                foreach (var anim in new[] { "idle", "sleep", "read_sleep" })
                    for (int i = 0; i < 4; i++) _dresser.Frame(_lib, anim, i, night);
                if (Clothes.Find(p.Top) == null || Clothes.Find(p.Pants) == null) throw new Exception("пижама без дрехи: " + p.Id);
            }
            // Некупена пижама не се облича; купената – да.
            _save.Pajama = "stars";
            _save.OwnedClothes.Remove("pajama_stars");
            if (Clothes.CurrentPajama(_save).Id != "classic") throw new Exception("некупена пижама");
            _save.OwnedClothes.Add("pajama_stars");
            if (Clothes.CurrentPajama(_save).Id != "stars") throw new Exception("купената пижама не е сложена");
            _save.Pajama = "classic";
            SetPajamas(false);
            SetPajamas(true);
        }, 300);
        await step("скинове", () =>
        {
            foreach (var skin in Furniture.CouchSkins) UseSkin(skin);
            Relax(Scenes.Reading);
            foreach (var skin in Furniture.BedSkins) UseSkin(skin);
            foreach (var skin in Furniture.ByItem.Values.SelectMany(x => x)) UseSkin(skin);
            foreach (var p in Clothes.PajamaSets) { UsePajama(p); PajamaPreview(p); }
            foreach (var skin in Furniture.ByItem.Values.SelectMany(x => x)) if (Furniture.Owns(_save, skin)) SkinImage(skin);
        }, 1000);
        await step("размер", () => { SetBearScale(5); SetBearScale(1); SetBearScale(3); }, 800);
        await step("съблечи", Undress, 1200);
        await step("затвори менюто", () => _menu!.Hide(), 500);
        await step("подарък: тръгва", DevGiftNow, 3000);
        await step("подарък: в средата", DevGiftDrop, 1500);
        await step("подарък: отваряне", () => OpenGift(_giftWindow!), 1500);
        await step("подарък: още един", () => { DevGiftDrop(); OpenGift(_giftWindow!); }, 1000);
        await step("подарък: нов ден", DevGiftNewDay, 500);
        await step("емоции", () =>
        {
            foreach (var e in new[] { "heart", "zzz", "idea", "exclaim", "question", "note", "sweat", "hungry", "cold", "sparkle", "happy", "sad" })
            {
                if (Ui.LoadIcon("emote_" + e) == null) throw new Exception("няма рисунка за емоция " + e);
                Emote(e);
                if (CurrentEmote != e) throw new Exception("емоцията не се показа: " + e);
                UpdateLayoutAndWindow();
            }
        }, 800);
        await step("закачени задачи и бележки", () =>
        {
            AddTask("Тестова задача за прозорчето", 2);
            AddNote("Тестова бележка");
            SetPinned(PinWindow.Tasks, true);
            SetPinned(PinWindow.Notes, true);
            if (!IsPinned(PinWindow.Tasks) || !IsPinned(PinWindow.Notes)) throw new Exception("не се закачи");
            NotifyCare();
        }, 600);
        await step("задача към мечо-дорото", () =>
        {
            var t = _save.Tasks.First(x => x.Title == "Тестова задача за прозорчето");
            StartFocusOn(t);
            if (FocusTask != t || !InFocus) throw new Exception("мечо-дорото не е по задачата");
            UpdateTimerTag();
            if (!_timerTagShown!.Contains("Тестова")) throw new Exception("задачата не е на табелката");
            StopPomodoro();
            SetTaskDone(t, true);
            if (FocusTask != null) throw new Exception("свършена задача остана текуща");
            SetPinned(PinWindow.Tasks, false);
            SetPinned(PinWindow.Notes, false);
            if (IsPinned(PinWindow.Tasks)) throw new Exception("не се откачи");
            DeleteNote(_save.Notes.First(n => n.Text == "Тестова бележка"));
        }, 600);
        await step("спомени от вчера", () =>
        {
            // Спомените сами не изскачат по време на теста (иначе въпросът пречи на следващите стъпки).
            _save.MemoryDay = _save.MemoryLaterDay = DateTime.Today.ToString("yyyy-MM-dd");
            var y = DayKey(DateTime.Today.AddDays(-1));
            _save.Days.RemoveAll(d => d.Date == y);
            _save.Days.Insert(Math.Max(0, _save.Days.Count - 1), new DayRecord
            {
                Date = y, Pomodoros = 7, WorkMinutes = 200, TasksDone = 4, BigTasks = 1, Foods = { "berries", "berries" },
                FavFed = true, Petted = 5, WokeEarly = true, NapWoken = true, BedAt = DateTime.Today.AddHours(-2),
                MinFullness = 10, MinEnergy = 10, Hobbies = { "reading", "knit" }, Bought = { "тенджера" }, Clothes = 4,
            });
            for (int i = 0; i < 30; i++)
            {
                if (!MorningMemory()) throw new Exception("няма спомен, а вчера е имало много");
                _chat?.Hide();
            }
            LaterMemory();
            if (FavoriteFood == null) throw new Exception("няма любима храна за днес");
            _save.FavFoodSaid = false;
            SayFavoriteWish();
            HideBubble();
        }, 400);
        await step("градинка", () =>
        {
            if (!_save.Owned.Contains(Kitchen.PlantPot)) _save.Owned.Add(Kitchen.PlantPot);
            if (IsAsleep) WakeUp();
            DevClearFood();
            if (_inScene) LeaveScene();
            StartScene(Scenes.Plant, 120);
            if (_gardenWindow == null) throw new Exception("няма градинка");
            // Расте от грижите (8 мин – фаза, най-много 2 на ден) и бавно сам (5 дни).
            _save.PlantStage = 2;
            _save.PlantGrowToday = 0;
            _save.PlantGrowDay = "";
            _save.PlantCareMinutes = 8;
            GrowPlant(fromCare: true);
            if (_save.PlantStage != 3) throw new Exception("растението не порасна от грижите");
            GrowPlant(fromCare: true);
            GrowPlant(fromCare: true);
            if (_save.PlantStage != 4) throw new Exception("порасна повече от 2 фази за ден");
            _save.PlantLastGrow = DateTime.Now.AddDays(-11);
            GrowPlantByTime();
            if (_save.PlantStage != 6) throw new Exception("не порасна само с времето (2 фази за 11 дни)");
            // Стига до ръба, издърпва саксията и почва грижите.
            for (int i = 0; i < 3000 && _gardenPhase < 2; i++) UpdateGarden(Now + i * 0.1);
            if (_gardenPhase != 2 || _plantOut < 1) throw new Exception($"не издърпа саксията (фаза {_gardenPhase}, навън {_plantOut:0.0})");
            foreach (var t in new[] { "wateringcan", "shovel", "scissors" })
                if (!Clothes.All.Any(g => g.Id == t)) throw new Exception("няма инструмент " + t);
            for (int i = 0; i < 40; i++) UpdateGarden(Now + i * 0.1);
            // Капчици от лейката.
            _gardenWaterUntil = Now + 5;
            _plantTool = "wateringcan";
            _anim = "doing";
            for (int i = 0; i < 10; i++) { _nextDrop = 0; UpdateDrops(Now, 0.016); }
            if (_drops.Count == 0) throw new Exception("лейката не капе");
            UpdateLayoutAndWindow();
        }, 1500);
        await step("градинка: свърши", () =>
        {
            LeaveScene();
            if (!_plantPushing) throw new Exception("не бута саксията обратно");
            _plantPushing = false;
            _plantOut = 0;
            HideGarden();
            if (_gardenWindow is { Showing: true }) throw new Exception("саксията не се прибра");
            UpdateDrops(Now, 0.016);
            if (_drops.Count > 0) throw new Exception("капчиците останаха");
        }, 300);
        await step("табелка: иконката е на средата на цифрите", () =>
        {
            _save.PomodoroPhase = PomodoroPhase.Off;
            StartFocus();
            UpdateTimerTag();
            UpdateLayoutAndWindow();
            _timerTag.UpdateLayout();
            SelfTest.Log($"   табелка: иконката е отместена с {Ui.LastIconOffset:0.##} спрямо цифрите ({_timerTag.ActualWidth:0}×{_timerTag.ActualHeight:0})");
            if (_timerRow.Children.Count < 2) throw new Exception("табелката е без иконка или без време");
            StopPomodoro();
            if (!R.Has("walk.leave.work") || !R.Has("lamp.ask")) throw new Exception("няма реплики за разходка по време на работа / лампа");
            for (int l = 1; l < 6; l++)
                if (Conversations.HungerAskChance(l) < Conversations.HungerAskChance(l + 1)) throw new Exception("по-гладен трябва да пита по-често");
        }, 300);
        await step("разговори от razgovori.txt", () =>
        {
            if (Talks.TopLevel.Length < 100) throw new Exception($"разговорите са само {Talks.TopLevel.Length}");
            foreach (var t in Talks.All.Values)
                foreach (var o in t.Options)
                    if (o.Next != null && !Talks.All.ContainsKey(o.Next)) throw new Exception($"{t.Id}: няма продължение {o.Next}");
            Ask(Talks.Ask(this, Talks.TopLevel[0]));
            _chat?.Hide();
        }, 300);
        await step("радио и хранилка", () =>
        {
            foreach (var id in new[] { Kitchen.Radio, Kitchen.Feeder })
                if (!_save.Owned.Contains(id)) _save.Owned.Add(id);
            SetRadio(true);
            SetFeeder(true);
            UpdateRadio(Now);
            UpdateFeeder(Now, 0.03);
            if (_radioWindow == null || _feederWindow == null) throw new Exception("радиото или хранилката не се появиха");
            if (!RadioPlaying) throw new Exception("сложеното радио не свири");
            _save.FeederFood = 2;
            _nextBird = 0;
            UpdateFeeder(Now, 0.03);
            if (_birdPhase != 1) throw new Exception("птичката не долетя");
            for (int i = 0; i < 60; i++) UpdateFeeder(Now, 0.1);
            SetRadio(false);
            SetFeeder(false);
            UpdateRadio(Now);
            UpdateFeeder(Now, 0.03);
            if (_radioWindow != null || _feederWindow != null) throw new Exception("не се прибраха");
        }, 300);
        await step("уреди: сам сложеното радио се маха, сложеното от Тут остава", () =>
        {
            bool instant = InstantModes;
            InstantModes = true;
            if (_inScene) LeaveScene();
            SetRadio(false);
            UpdateRadio(Now);
            StartScene(Scenes.Music, quiet: true);
            UpdateRadio(Now);
            if (!RadioPlaying || !_save.RadioAuto || _radioWindow == null) throw new Exception("за танца не сложи радиото");
            if (_sideProp != null) throw new Exception("второ радио до мечока");
            LeaveScene();
            UpdateRadio(Now);
            if (RadioPlaced || _radioWindow != null) throw new Exception("сам сложеното радио остана");
            SetRadio(true);
            SetRadioOn(false);
            UpdateRadio(Now);
            StartScene(Scenes.Music, quiet: true);
            if (!RadioPlaying || _save.RadioAuto) throw new Exception("радиото на Тут не се пусна за танца");
            LeaveScene();
            UpdateRadio(Now);
            if (!RadioPlacedByOwner || _save.RadioOn) throw new Exception("радиото на Тут трябва да остане (и пак изключено)");
            // Само по едно: излишно радио се затваря.
            var extra = new CookerWindow("Радио");
            extra.Show();
            CloseDuplicatePlaceables();
            if (extra.IsVisible) throw new Exception("две радиа");
            SetRadio(false);
            UpdateRadio(Now);
            // Растението: сложено от Тут – остава след грижата.
            if (!_save.Owned.Contains(Kitchen.PlantPot)) _save.Owned.Add(Kitchen.PlantPot);
            SetPlant(true);
            UpdatePlacedPlant();
            if (_gardenWindow is not { Showing: true }) throw new Exception("сложеното растение не се вижда");
            StartScene(Scenes.Plant, quiet: true);
            LeaveScene();
            UpdatePlacedPlant();
            if (!_save.PlantPlaced || _gardenWindow is not { Showing: true }) throw new Exception("сложеното растение изчезна след грижата");
            SetPlant(false);
            if (_gardenWindow is { Showing: true }) throw new Exception("растението не се прибра");
            InstantModes = instant;
        }, 300);
        await step("предмети: прави нещото само до предмета, иначе се връща", () =>
        {
            bool instant = InstantModes;
            InstantModes = true;
            if (_inScene) LeaveScene();
            SetIdle();
            if (!_save.Owned.Contains(Kitchen.PlantPot)) _save.Owned.Add(Kitchen.PlantPot);
            var area = Area;
            _x = area.Left + area.Width * 0.3;
            SetPlant(true);
            _save.PlantX = area.Left + area.Width * 0.7;
            StartScene(Scenes.Plant, quiet: true);
            _gardenPhase = 2;
            _x = area.Left + area.Width * 0.2;   // „вдигнат и пуснат“ далече от растението
            _tickDt = 0.1;
            UpdateScene(Now);
            if (!_anchorWalk || !_anim.StartsWith("walk")) throw new Exception("далече от растението, а не тръгна към него: " + _anim);
            for (int i = 0; i < 400 && _anchorWalk; i++) UpdateScene(Now);
            if (_anchorWalk || !NearAnchor(PlantAnchor)) throw new Exception("не стигна до растението");
            if (_anim.StartsWith("walk")) throw new Exception("стигна, но не продължи с растението");
            // Вдигнат: виси с „drag“, не полива във въздуха.
            LiftUp();
            if (_anim != "drag") throw new Exception("вдигнат, но анимацията е " + _anim);
            SetState(BearState.Couch, "idle");
            LeaveScene();
            SetPlant(false);
            SetIdle();
            InstantModes = instant;
        }, 300);
        await step("желания: изпълненото се маха", () =>
        {
            _save.WishDay = "";
            var w = Wishes();
            if (w.Length == 0) throw new Exception("няма желания");
            bool fulfilled = false;
            var listener = new object();
            CoreEngine.Bus.Subscribe<Mecho.Core.WishFulfilled>(listener, x => fulfilled |= x.FoodId == w[0].Id);
            FulfillWish(w[0]);
            CoreEngine.Bus.Unsubscribe(listener);
            if (Wishes().Contains(w[0])) throw new Exception("изпълненото желание остана");
            if (!fulfilled) throw new Exception("няма сигнал WishFulfilled");
            _save.WishDay = "";
            HideBubble();
        }, 300);
        await step("плавен преход: „Почивай си“ мисли, после почва", () =>
        {
            InstantModes = false;
            if (IsAsleep) WakeUp();
            DevClearFood();
            if (_inScene) LeaveScene();
            SetIdle();
            RelaxRandom();
            if (_inScene) throw new Exception("почна веднага, без да помисли");
            if (_modeDecideAt <= Now) throw new Exception("няма пауза за мислене");
            _modeDecideAt = 0;
            SetIdle();
            _stateTime = 99;
            UpdateIdle(Now);
            if (!_inScene && !_onTrampoline) throw new Exception("след паузата не избра занимание");
            InstantModes = true;
            GetUp();
        }, 300);
        await step("скорост", () => SelfTest.Log("   " + PerfSummary()), 100);
        await step("въпросник: бутон, свободен отговор, файл и спомен", () =>
        {
            if (Survey.All.Count < 10) throw new Exception("въпросникът е празен");
            if (Survey.Plain("✏️ С молив") != "С молив") throw new Exception("етикетът не се чисти: " + Survey.Plain("✏️ С молив"));
            var q = Survey.All["draw_tool"];
            var chat = Survey.Ask(this, q);
            if (chat.FreeText == null || chat.Options.Length < 3) throw new Exception("няма поле или бутони");
            chat.Options[0].Do(this);
            if (_save.Answers["draw_tool"].Text != "С молив") throw new Exception("бутонът не се запомни");
            chat.FreeText(this, "с туш и перо");
            if (_save.Answers["draw_tool"] is not { Free: true, Text: "с туш и перо" }) throw new Exception("написаното не се запомни");
            if (!System.IO.File.ReadAllText(Survey.AnswersPath).Contains("с туш и перо")) throw new Exception("няма го в otgovori.txt");
            string? remark = Survey.RemarkFor(_save, new Random(1));
            if (remark == null || remark.Contains('{')) throw new Exception("спомен: " + remark);
            foreach (var x in Survey.All.Values)
                if (x.Text.Length == 0 || (x.Options.Length == 0 && x.FreeReplies.Length == 0)) throw new Exception("празен въпрос " + x.Id);
            _save.Answers.Clear();
            HideBubble();
        }, 200);
        await step("„Ще се работи ли днес?“ сменя колко пита за работа", () =>
        {
            SetWorkPlan(PlanNo);
            double no = WorkPlanFactor;
            SetWorkPlan(PlanYes);
            if (no >= 0.5 || WorkPlanFactor <= 1) throw new Exception("планът не влияе");
            SetWorkPlan(PlanNo);
            NoteWorkStarted();
            if (WorkPlan != PlanYes) throw new Exception("пуснатата работа не направи деня работен");
            var chat = Conversations.WorkPlan(this, again: false);
            if (chat.Options.Length != 4 || chat.Text.Contains('{')) throw new Exception("въпросът за деня");
            _save.WorkPlan = "";
            _save.WorkPlanDay = "";
        }, 200);
        await step("реплики с {храна}: всички полета се попълват", () =>
        {
            foreach (var key in new[] { "fav.wish", "fav.eaten", "memory.onefood", "memory.feast" })
                foreach (var l in R.Get(key))
                {
                    string line = R.Put(R.Put(R.Put(l, "храна", "мед"), "храна2", "чай"), "име", "Тут");
                    if (line.Contains('{')) throw new Exception($"{key}: остана празно поле в „{line}“");
                }
        }, 100);
        await step("поздрав за всяка част от деня", () =>
        {
            for (int h = 0; h < 24; h++)
                if (!R.Has("greet." + Lines.DaySlot(new DateTime(2026, 1, 1, h, 0, 0)))) throw new Exception($"няма поздрав за {h} ч.");
            foreach (var k in new[] { "greet.back.morning", "greet.back.day", "greet.back.evening", "greet.back.night", "greet.long", "greet.restart" })
                if (!R.Has(k)) throw new Exception("няма " + k);
        }, 100);
        SelfTest.Log("• тест за скорост (доклад)");
        try
        {
            await RunPerfReport();
            foreach (var line in System.IO.File.ReadAllLines(PerfReportPath)) SelfTest.Log("   " + line);
        }
        catch (Exception e) { SelfTest.Log("   тестът за скорост не мина: " + e.Message); }
        await step("нива на глад и умора", () =>
        {
            foreach (var (v, lvl) in new[] { (0.0, 1), (20.0, 1), (21.0, 2), (35.0, 2), (36.0, 3), (50.0, 3), (51.0, 4), (65.0, 4), (66.0, 5), (80.0, 5), (81.0, 6), (95.0, 6) })
                if (NeedLevel(v) != lvl) throw new Exception($"{v} трябва да е ниво {lvl}");
            if (NeedLevel(96) != null) throw new Exception("над 95 няма ниво");
            for (int l = 1; l <= 6; l++)
                foreach (var k in new[] { "hunger", "tired" })
                    if (!R.Has($"need.{k}.{l}")) throw new Exception($"няма реплики need.{k}.{l}");
        }, 100);
        await step("подарък в лапите", () =>
        {
            _giftWindow?.Close();
            _save.GiftPending = false;
            PresentGift();
            if (_giftRun != GiftRun.Holding || !_save.GiftPending) throw new Exception("не го подаде");
            if (_anim != GiftHoldAnim) throw new Exception("не държи подаръка с hold: " + _anim);
            UpdateLayoutAndWindow();
            if (_carry.Visibility != Visibility.Visible) throw new Exception("подаръкът не се вижда в лапите");
        }, 1500);
        await step("подарък: цъкаш – пуска го, долита в средата и се отваря", () =>
        {
            if (!OpenHeldGift()) throw new Exception("цъкането не го подаде");
            if (_giftWindow == null) throw new Exception("подаръкът не долетя");
        }, 2500);
        await step("подарък: отворен", () =>
        {
            if (_save.GiftPending) throw new Exception("подаръкът не се отвори сам");
            _chat?.Hide();
        }, 500);
        await step("подарък: прекъснат – оставя го в средата", () =>
        {
            _giftWindow?.Close();
            _save.GiftPending = false;
            PresentGift();
            SetIdle();          // прекъсна го (вдигнат, заспа…)
            UpdateGifts(Now);
            if (_giftRun != GiftRun.None || _giftWindow == null || !_save.GiftPending) throw new Exception("не го остави в средата");
            _giftWindow.Close();
            _save.GiftPending = false;
            _chat?.Hide();
        }, 1500);
        await step("подарък: затвори", () => { _giftWindow?.Close(); SetIdle(); }, 300);
        await step("разходка", () =>
        {
            if (IsAsleep) WakeUp();
            if (_inScene) LeaveScene();
            SetIdle();
            GoForWalk(1);
            if (!OnWalk) throw new Exception("не тръгна на разходка");
        }, 6000);
        await step("разходка: повикай го", () =>
        {
            ReturnFromWalk(called: true);
            if (OnWalk || !IsVisible) throw new Exception("не се върна");
        }, 3000);
        await step("сам излиза на разходка", () =>
        {
            SetIdle();
            GoForWalk(2, solo: true);
            if (!OnWalk || !_save.WalkSolo) throw new Exception("не тръгна сам");
        }, 4000);
        await step("сам: връща се и казва какво е правил", () =>
        {
            ReturnFromWalk(called: false);
            if (OnWalk || _save.WalkSolo || !IsVisible) throw new Exception("не се върна от самостоятелната разходка");
            if (!R.Has("walk.leave.solo") || !R.Has("walk.back.solo") || !R.Has("wish.nuts") || !R.Has("wish.page")) throw new Exception("няма реплики за самостоятелна разходка");
        }, 3000);
        await step("желание: връща услугата", () =>
        {
            var wishes = Wishes();
            if (wishes.Length == 0) throw new Exception("няма желания");
            int nuts = _save.Hazelnuts, pages = _save.RecipePages;
            string? next = _save.NextRecipe;
            bool thanked = false;
            for (int i = 0; i < 200 && !thanked; i++)
            {
                TodayRec.WishGifts = 0;
                thanked = WishThanks(wishes[0]);
            }
            if (!thanked) throw new Exception("не върна услугата за 200 опита");
            if (_save.Hazelnuts <= nuts && _save.RecipePages == pages && _save.NextRecipe == next) throw new Exception("нищо не даде");
            TodayRec.WishGifts = 2;
            if (WishThanks(wishes[0])) throw new Exception("даде повече от 2 пъти на ден");
            if (WishThanks(Kitchen.Foods.First(f => !wishes.Contains(f)))) throw new Exception("даде за храна, която не иска");
            HideBubble();
        }, 400);
        await step("задача с описание + закачен прозорец свит/разгънат", () =>
        {
            AddTask("Задача с описание", 1, "Подробности за задачата");
            var t = _save.Tasks.Last();
            if (t.Description != "Подробности за задачата") throw new Exception("описанието не се запази");
            SetPinned(PinWindow.Tasks, true);
            var pin = _pins[PinWindow.Tasks];
            pin.Height = 333;
            _save.PinTasks.H = 333;
            pin.UpdateLayout();
            pin.ToggleCollapse();
            pin.UpdateLayout();
            pin.ToggleCollapse();
            pin.UpdateLayout();
            if (Math.Abs(pin.Height - 333) > 2) throw new Exception($"след разгъване е {pin.Height:0}, а беше 333");
            SetPinned(PinWindow.Tasks, false);
            DeleteTask(t);
            if (_save.Tasks.Any(x => x.Id == t.Id)) throw new Exception("изтритата задача остана в ядрото");
        }, 600);
        await step("обиколка (tutorial)", () =>
        {
            for (int i = 0; i < 12; i++)
            {
                StartTutorial(i);
                _chat?.Hide();
            }
            if (!_save.TutorialDone) throw new Exception("обиколката не се отбеляза като видяна");
        }, 500);
        await step("сандвич на масата", () =>
        {
            var sandwich = Kitchen.Foods.First(f => f.Id == "sandwich");
            if (!Kitchen.CanCook(sandwich, new SaveData())) throw new Exception("сандвичът не е винаги отключен");
            if (Kitchen.CategoryOf(sandwich) == "Напитки") throw new Exception("грешна категория");
            TestCook(sandwich);
            if (_cook?.Tool != Kitchen.Table) throw new Exception("сандвичът не е на масата");
            EndCooking(food: null);
            DevClearFood();
            Sounds.Play(_save);
        }, 1500);
        await step("ядро: работи, модулите са пуснати, мостът пуска сигнали", () =>
        {
            if (!CoreEngine.Started) throw new Exception("ядрото не е пуснато");
            if (CoreEngine.Modules.Modules.FirstOrDefault(m => m.Id == "legacy.bridge") is not { Status: Mecho.Core.ModuleStatus.Running })
                throw new Exception("мостът към стария код не работи");
            // Вълна 1: лешниците, притежаваното и предметите на земята са само в ядрото.
            foreach (var id in new[] { "economy", "placement", "bear", "needs", "kitchen", "work", "tasks" })
                if (!CoreEngine.Modules.IsRunning(id)) throw new Exception($"модулът {id} не работи");
            int nuts = CoreEngine.Store.Get<Mecho.Core.WalletState>().Nuts;
            if (_save.Hazelnuts != nuts || _save.LegacyHazelnuts != 0) throw new Exception("лешниците не са в ядрото");
            var needs = CoreEngine.Store.Get<Mecho.Core.NeedsState>();
            if (Math.Abs(needs.Energy - _save.Energy) > 0.001 || _save.LegacyEnergy != 0) throw new Exception("нуждите не са в ядрото");
            int before = nuts;
            _save.Hazelnuts += 3;   // стар код → команда → ядро
            if (CoreEngine.Store.Get<Mecho.Core.WalletState>().Nuts != before + 3) throw new Exception("старият код не пише в ядрото");
            _save.Hazelnuts -= 3;
            _save.Owned.Add("selftest_item");
            if (!CoreEngine.Store.Get<Mecho.Core.InventoryState>().Has("selftest_item")) throw new Exception("притежаваното не е в ядрото");
            _save.Owned.Remove("selftest_item");
            string tmp = System.IO.Path.Combine(SaveData.Folder, "core_check.json");
            _save.Core = CoreEngine.Store.Export();
            _save.SaveTo(tmp);
            string json = System.IO.File.ReadAllText(tmp);
            if (json.Contains("\"Hazelnuts\"") || json.Contains("\"Owned\"") || json.Contains("\"WishList\"") || json.Contains("\"CookFood\"") || json.Contains("\"PomodoroPhase\"") || json.Contains("\"TimerEndsAt\"") || json.Contains("\"TimerIsWork\"") || json.Contains("\"TaskHistory\"")) throw new Exception("старите полета пак се записват (двоен източник)");
            if (!json.Contains("economy.wallet")) throw new Exception("ядрото не е в записа");
            _coreNeeds = null;
            CoreEngine.Bus.Subscribe<Mecho.Core.NeedsChanged>(_coreListener, s => _coreNeeds = s);
            _save.Energy = Math.Max(0, _save.Energy - 20);
        }, 1600);
        await step("ядро: NeedsChanged дойде по шината", () =>
        {
            CoreEngine.Bus.Unsubscribe(_coreListener);
            if (_coreNeeds == null) throw new Exception("мостът не пусна NeedsChanged за 1,5 с");
            if (CoreEngine.Modules.Modules.Any(m => m.Status == Mecho.Core.ModuleStatus.Faulted)) throw new Exception("има спрян модул");
            if (_save.Core == null && !CoreEngine.Store.IsDirty) PersistNow();
        }, 200);
        await step("developer панел", () =>
        {
            var dev = new DevWindow(this);
            dev.Show();
            dev.UpdateLayout();
            dev.Close();
        }, 500);
        await step("developer: Dev Mode (всичко) и връщане", () =>
        {
            if (DevMode) SetDevMode(false);
            int owned = _save.Owned.Count, nuts = _save.Hazelnuts;
            SetDevMode(true);
            if (!DevMode || !Kitchen.Upgrades.All(u => _save.Owned.Contains(u.Id))) throw new Exception("Dev Mode не отключи всичко");
            SetDevMode(false);
            if (DevMode || _save.Owned.Count != owned || _save.Hazelnuts != nuts) throw new Exception("Dev Mode не върна прогреса");
            if (!DevPasswordOk("mecho") || DevPasswordOk("tut")) throw new Exception("паролата не се проверява");
        }, 500);
        await step("developer: живот, времето, разговори", () =>
        {
            foreach (var (group, label, act) in DevCommands())
            {
                if (group is not ("Живот" or "Времето и печицата" or "Разговори и въпроси")) continue;
                try { act(); }
                catch (Exception e) { throw new Exception($"„{label}“: {e.Message}"); }
                _chat?.UpdateLayout();
                _chat?.Hide();
            }
            EndVisitor();
            SetHeater(false);
            _save.Owned.Remove(Kitchen.Heater);
            _tempC = null;
            Weather.Fake(-1, 0);
            GetUp();
            SetIdle();
        }, 2000);
        await step("SOS", () =>
        {
            var list = Motivations.Parse("- https://example.com Пример\n- [Име](https://example.org)\n- Текст\nдруго");
            if (list.Count != 3 || list[0].Name != "Пример" || list[1].Link != "https://example.org" || list[2].Link != null)
                throw new Exception("Motivations.Parse");
            Sos();
            _chat?.UpdateLayout();
        }, 800);
        await step("SOS: друго", () => { Sos(); _chat?.Hide(); }, 500);
        await step("седи", () => { if (_inScene) LeaveScene(); StartScene(Scenes.Sit, quiet: false); }, 800);
        await step("сам решава: почивка или работа", () =>
        {
            LeaveScene();
            _freeUntil = 0;
            if (!FreeCycleStep() || !AutoMode || !(RelaxesRandomly || WorksRandomly)) throw new Exception("не избра сам");
        }, 1000);
        await step("часовник почивка/работа", () =>
        {
            var monday = new DateTime(2026, 10, 12);
            if (WorkChance(monday.AddHours(10)) < 0.8 || WorkChance(monday.AddHours(12.75)) > 0.2 ||
                WorkChance(monday.AddHours(15)) < 0.7 || WorkChance(monday.AddHours(20)) > 0.3 || WorkChance(monday.AddDays(5).AddHours(11)) > 0.3)
                throw new Exception("грешен часовник");
            StopAction();
            if (AutoMode || _inScene) throw new Exception("не спря");
        }, 500);
        await step("работи си", () => { WorkRandom(); if (!WorksRandomly) throw new Exception("не работи"); }, 1000);
        await step("цъкнат: реплики според заниманието и състоянието", () =>
        {
            double energy = _save.Energy, full = _save.Fullness;
            var ids = Scenes.All.Select(s => (s.Id, Group: Scenes.Work.Contains(s) ? "work" : "relax")).Append(("sit", "relax")).Append(("focus", "work"));
            // „Тъкмо {какво}… Да спра ли?“: всяко занимание казва какво прави в първо лице
            // (не името от менюто – „Играй настолна игра“).
            foreach (var sc in Scenes.All.Append(Scenes.Focus).Append(Scenes.Sit))
            {
                if (string.IsNullOrWhiteSpace(sc.Doing) || char.IsUpper(sc.Doing[0]) || sc.Doing == sc.Name)
                    throw new Exception($"„{sc.Id}“ няма какво прави в първо лице");
                if (sc != Scenes.Focus && sc != Scenes.Sit && sc != Scenes.Trampoline && !R.Has($"scene.{sc.Id}.intent"))
                    throw new Exception("няма намерение за " + sc.Id);
            }
            if (Scenes.All.Select(x => x.Name).Distinct().Count() != Scenes.All.Length) throw new Exception("две занимания с едно име");
            foreach (var (id, group) in ids)
            {
                if (!R.Has("poke." + id)) throw new Exception("няма реплики за " + id);
                foreach (var (e, f) in new[] { (80.0, 80.0), (10.0, 80.0), (80.0, 10.0), (10.0, 10.0) })
                {
                    _save.Energy = e;
                    _save.Fullness = f;
                    if (string.IsNullOrWhiteSpace(PokeLine(id, group))) throw new Exception($"празна реплика за {id} ({e}/{f})");
                }
            }
            _save.Energy = energy;
            _save.Fullness = full;
        }, 200);
        await step("цъкнат: първо казва нещо, второто пита дали да спре", () =>
        {
            _offerUntil = 0;
            _lunchAskUntil = 0;
            _heaterAskUntil = 0;
            _napAskUntil = 0;
            _lastActionPoke = -100;
            _chatOpenAtPress = false;
            if (ChatIsOpen) _chat!.Hide();
            _save.Energy = 60;
            // Нищо от предишните стъпки да не пречи (яде, пие, работи, на разходка…).
            if (_eating != null) { _eating = null; _held.Source = null; }
            if (_inScene) LeaveScene();
            if (_save.PomodoroPhase != PomodoroPhase.Off) StopPomodoro();
            if (TimerRunning) StopTimer();
            if (OnWalk) ReturnFromWalk(called: true);
            DevClearFood();
            // Доставка от покупките в предишните стъпки: кашонът пада в случаен момент и
            // Relax → OpenParcelFirst го праща към него (затова понякога „върви“, а не чете).
            ClearDeliveries();
            HideBubble();
            // Без плавния преход и без недовършена разходка/връщане от предишните стъпки
            // (иначе понякога още върви, когато се проверява – нестабилен тест).
            bool instant = InstantModes;
            InstantModes = true;
            _walkingOut = _walkingBack = _peeking = false;
            SetIdle();
            Relax(Scenes.Reading);
            InstantModes = instant;
            if (!InAction)
                throw new Exception($"не седна да чете: фокус={InFocus}, кътче={_inScene}/{_scene?.Id}, състояние={_state}/{_anim}, яде={_eating != null}, храна={WaitingForFood}, разходка={OnWalk}, спи={IsAsleep}");
            OnLeftClick();
            if (ChatIsOpen) throw new Exception("първото цъкване пита, вместо да каже нещо");
            OnLeftClick();
            if (!ChatIsOpen) throw new Exception("второто цъкване не пита дали да спре");
            _chat!.Hide();
            GetUp();
        }, 500);
        await step("цъкнат, докато работи си", () =>
        {
            Ask(Conversations.StopAction(this, CurrentActionName));
            _chat?.UpdateLayout();
            _chat?.Hide();
            GetUp();
        }, 500);
        await step("обяд: въпрос", () => { Ask(Conversations.Lunch()); _chat?.UpdateLayout(); _chat?.Hide(); }, 300);
        await step("студено: печица", () =>
        {
            bool had = Kitchen.Owns(_save, Kitchen.Heater);
            _save.Owned.Remove(Kitchen.Heater);
            _tempC = 3;
            TalkAboutWeather();
            if (WeatherText == null) throw new Exception("няма текст за времето");
            _save.Owned.Add(Kitchen.Heater);
            SetHeater(false);
            TalkAboutWeather();
            if (!TakeHeaterQuestion()) throw new Exception("не пита за печицата");
            _chat?.UpdateLayout();
            _chat?.Hide();
            SetHeater(true);
            UpdateHeater(Now);
            if (_heaterWindow == null) throw new Exception("печицата не се показа");
            TalkAboutWeather();
            WarmUpByHeater();
            _nextCloserLine = 0;
            AskHeaterCloser(Now);
            if (!had) _save.Owned.Remove(Kitchen.Heater);
        }, 1500);
        await step("горещо: изгася печицата", () =>
        {
            _tempC = 30;
            _save.Owned.Add(Kitchen.Heater);
            TalkAboutWeather();
            _ = Warmth;
            SetHeater(false);
            UpdateHeater(Now);
            if (_heaterWindow != null) throw new Exception("печицата не изчезна");
            _save.Owned.Remove(Kitchen.Heater);
            _tempC = null;
            GetUp();
            SetIdle();
        }, 800);
        await step("реплики без повторение", () =>
        {
            var deck = Enumerable.Range(0, 8).Select(i => $"тест {i} {{0}}").ToArray();
            var seen = new HashSet<string>();
            for (int i = 0; i < deck.Length; i++) seen.Add(Lines.Pick(deck, "Тут"));
            if (seen.Count != deck.Length) throw new Exception("репликите се повтарят");
            for (int i = 0; i < 40; i++) if (string.IsNullOrEmpty(Lines.Chat("Тут"))) throw new Exception("празна приказка");
            for (int i = 0; i < 40; i++) Conversations.Next(this);
        }, 200);
        await step("студ по нива", () =>
        {
            foreach (var (t, lvl) in new[] { (22.0, 0), (16.0, 0), (15.0, 1), (11.0, 1), (10.0, 2), (6.0, 2), (5.0, 3), (1.0, 3), (0.0, 4), (-5.0, 4) })
            {
                _tempC = t;
                if (ColdLevel != lvl) throw new Exception($"{t}°C: ниво {ColdLevel}, а трябва {lvl}");
                TalkAboutWeather();
                if (WeatherText == null) throw new Exception("няма текст");
            }
            _tempC = null;
        }, 300);
        await step("хълца", Hiccup, 9000);
        await step("киха", Sneeze, 4000);
        await step("намира нещо", FindSomething, 3500);
        await step("любимото място", () => { _nextFavSpot = 0; GoToFavSpot(); }, 6000);
        await step("сутрешен ритуал", () => { _save.RitualDay = ""; MorningRitual(); }, 6000);
        await step("гост: пчеличка", () => { SetIdle(); StartVisitor(); _visitorX = _x - _visitorDir * 150; }, 2500);
        await step("пчеличката кръжи", () =>
        {
            if (_visitor == null) throw new Exception("пчеличката я няма");
            if (!_visitorGreeted) throw new Exception("не дойде до мечока");
        }, 6000);
        await step("пчеличката отлетя", () => { if (_visitor != null) EndVisitor(); }, 300);
        await step("времето и настроението", () =>
        {
            foreach (var k in new[] { "rain", "heavyrain", "snow", "storm", "fog", "windy", "frost", "heat", "cold", "cool", "sunny", "cloudy" })
                if (R.Get("weather." + k).Length == 0) throw new Exception("няма реплики за " + k);
            Joy(JoyPetted); Joy(JoyFed); Joy(JoyWorked);
            if (JoyToday != 3) throw new Exception("настроението не се брои");
            for (int i = 0; i < 12; i++) MoodLine();
            Say(R.Pick("away.echo", _save.OwnerName), 1);
            Say(R.Pick("welcome.back", _save.OwnerName), 1);
            SetIdle();
        }, 500);
        await step("желания и какво му се яде", () =>
        {
            _nextWish = 0;
            UpdateWishes(Now);
            var c = Kitchen.Cravings(_save);
            if (c.Any(f => !Kitchen.CanCook(f, _save))) throw new Exception("иска нещо, което не може да се сготви");
            double full = _save.Fullness;
            _save.Fullness = 10;
            Ask(Conversations.Next(this));
            _chat?.UpdateLayout();
            _chat?.Hide();
            _save.Fullness = full;
            for (int i = 0; i < 20; i++) MoodLine();
            if (c.Length > 0) DoneLine(c[0]);
            var plain = new System.Windows.Controls.TextBlock();
            Ui.SetRichText(plain, "*прозява се* Добре… *търка очи*");
        }, 300);
        await step("целта в магазина", () =>
        {
            var removed = new[] { Kitchen.Wardrobe, Kitchen.Pot, Kitchen.Pan, Kitchen.Radio, Kitchen.Easel, Kitchen.YogaMat }
                .Where(id => _save.Owned.Remove(id)).ToList();
            _save.GoalId = null;
            _save.GoalChoices.Clear();
            if (Goal != null || GoalChoices.Count != 1 || GoalChoices[0].Id != Kitchen.Wardrobe)
                throw new Exception("в началото изборът не е само гардеробът");
            RerollGoal(Kitchen.Wardrobe);
            if (GoalRerollUsed || GoalChoices[0].Id != Kitchen.Wardrobe) throw new Exception("гардеробът се смени с „Друго“");
            OpenMenu(MenuWindow.ShopTab);
            _menu!.UpdateLayout();
            ChooseGoal(Kitchen.Wardrobe);
            if (Goal?.Id != Kitchen.Wardrobe) throw new Exception("гардеробът не стана цел");
            _save.Hazelnuts += Goal.Price;
            TodayRec.UpgradesBought = 0;
            TakeGoal();
            if (Kitchen.Owns(_save, Kitchen.Wardrobe)) throw new Exception("гардеробът е отключен преди доставката");
            if (GoalChoices.Any(u => u.Id == Kitchen.Wardrobe)) throw new Exception("поръчаният гардероб пак е в избора");
            ReceiveOrdersNow();
            if (!Kitchen.Owns(_save, Kitchen.Wardrobe)) throw new Exception("гардеробът не се купи");
            if (WardrobeBonus.Any(id => !_save.OwnedClothes.Contains(id))) throw new Exception("гардеробът дойде без тениската и дънките");
            var c = GoalChoices;
            if (c.Count != 3) throw new Exception("изборът не е от три");
            if (!c.Any(u => u.Id is Kitchen.Pot or Kitchen.Pan)) throw new Exception("няма тенджера/тиган в избора");
            RerollGoal(c[0].Id);
            if (!GoalRerollUsed) throw new Exception("„Друго“ не сработи");
            ChooseGoal(GoalChoices[1].Id);
            var d = Goal ?? throw new Exception("не се избра цел");
            _menu!.SelfTestShop();
            _save.Hazelnuts += d.Price;
            TodayRec.UpgradesBought = 0;
            TakeGoal();
            ReceiveOrdersNow();
            if (!Kitchen.Owns(_save, d.Id)) throw new Exception("целта не се купи");
            if (GoalChoices.Count == 0 && Goal == null && !AllGoalsDone) throw new Exception("няма нов избор");
            // Дневният лимит: след 2–3 покупки „Купи“ не купува, до утре.
            TodayRec.UpgradesBought = UpgradeLimitToday;
            if (!ShopSoldOut) throw new Exception("лимитът за деня не сработи");
            if (GoalChoices.FirstOrDefault() is { } extra)
            {
                int nuts = _save.Hazelnuts;
                Buy(extra);
                if (_save.Hazelnuts != nuts || _save.Ordered.Contains(extra.Id)) throw new Exception("купи над дневния лимит");
            }
            TodayRec.UpgradesBought = 0;
            _menu!.Hide();
            foreach (var id in removed) if (!_save.Owned.Contains(id)) _save.Owned.Add(id);
            _save.GoalId = null;
            _save.GoalChoices.Clear();
            ClearDeliveries();
        }, 800);
        await step("предложение за работа", () => { Ask(Conversations.WorkOffer(this)); _chat?.UpdateLayout(); _chat?.Hide(); }, 500);
        await step("обяд", () => { UpdateMeals(); ThankForMeal(); }, 300);
        await step("сън в леглото", GoToSleep, 1500);
        await step("събуди", WakeUp, 1000);
        await step("сън: за определено време", () =>
        {
            _save.Energy = 50;
            Sleep(90);
            if (_save.NapUntil < DateTime.Now.AddMinutes(89)) throw new Exception("не легна за 90 мин");
        }, 3500);
        await step("сън: казва кога ще стане", () =>
        {
            if (!IsAsleep) throw new Exception("не заспа");
            if (!WakeText.StartsWith("след")) throw new Exception("не казва кога ще стане: " + WakeText);
            Ask(Conversations.Next(this));
            _chat?.UpdateLayout();
            _chat?.Hide();
            WakeUp();
        }, 1000);
        await step("сън: лягай за деня", () =>
        {
            if (IsAsleep || _save.NapUntil != DateTime.MinValue) throw new Exception("не стана");
            SleepForDay();
            var until = _save.NapUntil;
            if (!_save.SleepForDay || until.Hour < 8 || until.Hour > 10 || until <= DateTime.Now) throw new Exception("за деня: става в " + until);
            WakeUp();
            if (_save.SleepForDay || _save.SleepingByChoice) throw new Exception("не се събуди");
            SetIdle();
        }, 1500);
        await step("спри: спира каквото прави", () =>
        {
            Relax(Scenes.Reading);
            StopDoing();
            if (_inScene || _save.StayPut) throw new Exception("не спря");
        }, 300);
        await step("мечо-доро подред", () =>
        {
            SetPomodoroSeries(2);
            int done = 0, today = _save.PomodorosToday;
            var listener = new object();
            CoreEngine.Bus.Subscribe<Mecho.Core.PomodoroFocusDone>(listener, _ => done++);
            StartFocus();
            if (PomodoroNumber != 1) throw new Exception("не е първото");
            _save.PomodoroEndsAt = DateTime.Now;
            UpdatePomodoro();
            if (_save.PomodoroPhase != PomodoroPhase.Break) throw new Exception("няма почивка");
            UpdateTimerTag();
            _save.PomodoroEndsAt = DateTime.Now;
            UpdatePomodoro();
            if (_save.PomodoroPhase != PomodoroPhase.Focus || PomodoroNumber != 2) throw new Exception("второто не почна само");
            _save.PomodoroEndsAt = DateTime.Now;
            UpdatePomodoro();
            _save.PomodoroEndsAt = DateTime.Now;
            UpdatePomodoro();
            if (_save.PomodoroPhase != PomodoroPhase.Off) throw new Exception("поредицата не свърши");
            CoreEngine.Bus.Unsubscribe(listener);
            // Вълна 4: краят го засича ядрото (WorkModule) и брои там.
            if (done != 2) throw new Exception($"ядрото пусна PomodoroFocusDone {done} пъти, а не 2");
            if (_save.PomodorosToday != today + 2) throw new Exception("броячът за днес не е от ядрото");
            SetPomodoroSeries(1);
            GetUp();
            SetIdle();
        }, 500);
        await step("таймер: почивка", () =>
        {
            int nuts = _save.Hazelnuts;
            StartTimer(1, "", "rest");
            if (!RestTimer || !RelaxesRandomly) throw new Exception("не почива");
            UpdateTimerTag();
            _save.TimerEndsAt = DateTime.Now;
            UpdateTimer();
            if (TimerRunning || RelaxesRandomly) throw new Exception("не стана след почивката");
            if (_save.Hazelnuts != nuts) throw new Exception("почивката даде лешници");
            StartTimer(1, "", "work");
            if (!WorkTimer) throw new Exception("работният не работи");
            StopTimer();
            _save.TimerKind = "work";
            GetUp();
            SetIdle();
        }, 500);
        await step("не заспива сам", () =>
        {
            if (IsAsleep) throw new Exception("още спи");
            _save.Energy = 2;
            SetIdle();
        }, 2500);
        await step("уморен, но буден", () =>
        {
            if (IsAsleep) throw new Exception("заспа сам");
        }, 200);
        await step("дреме над книжката", () =>
        {
            Relax(Scenes.Reading);
            _save.Energy = 10;
        }, 1200);
        double dozeEnergy = 0;
        await step("дреме: енергията не се вдига", () =>
        {
            if (_anim != "read_sleep") throw new Exception("не дреме над книжката, а " + _anim);
            dozeEnergy = _save.Energy;
        }, 1500);
        await step("дреме: стряска се", () =>
        {
            if (_save.Energy > dozeEnergy + 0.001) throw new Exception("енергията се вдига от дрямката");
            if (!StartleFromDoze() || _anim != "read") throw new Exception("не се стресна");
        }, 500);
        await step("вдигнат продължава да чете", () =>
        {
            LiftUp();
            if (!_inScene) throw new Exception("спря да чете");
            _vy = 0;
            SetState(BearState.Falling, _anim);
        }, 1500);
        await step("пуснат: пак чете", () =>
        {
            if (!_inScene) throw new Exception("не се върна към книжката");
            GetUp();
        }, 500);
        await step("сутрешна енергия", () =>
        {
            _save.MorningRefill = "";
            _save.Energy = 10;
            MorningRefresh(fromStart: true);
            int hour = DateTime.Now.Hour;
            if (hour is >= 5 and < 12 ? _save.Energy != MorningEnergy() : _save.Energy != 10) throw new Exception("сутрешната енергия е " + _save.Energy);
            _save.WentToBed = DateTime.Today.AddHours(2);
            if (MorningEnergy() > 65) throw new Exception("легнал в 2 ч., а сутрин е бодър");
            _save.WentToBed = default;
            SetDayKind(DayKind.BadNight);
            if (MorningEnergy() > 70) throw new Exception("не е спал добре, а е бодър");
            SetDayKind(DayKind.Normal);
            _save.Energy = 80;
        }, 200);
        await step("умора: видът на деня", () =>
        {
            var seen = new HashSet<DayKind>();
            for (int i = 0; i < 400; i++) seen.Add(PickDayKind(DateTime.Today));
            if (seen.Count < 5) throw new Exception("малко видове дни: " + seen.Count);
            _save.LastLazy = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd");
            for (int i = 0; i < 300; i++)
                if (PickDayKind(DateTime.Today) == DayKind.Lazy) throw new Exception("мързелив два дни подред");
            _save.LastLazy = "";
            _save.DayKindDate = "";
            EnsureDayKind();
            if (_save.DayKindDate != DateTime.Today.ToString("yyyy-MM-dd")) throw new Exception("денят не се избра");
            SetDayKind(DayKind.Normal);
        }, 200);
        await step("умора: часът", () =>
        {
            var d = new DateTime(2026, 10, 14);   // сряда
            if (TimeFactor(d.AddHours(10), DayKind.Normal) >= 1) throw new Exception("сутрин не е свеж");
            if (TimeFactor(d.AddHours(14), DayKind.Normal) <= 1) throw new Exception("следобед не отпада");
            if (TimeFactor(d.AddHours(20), DayKind.Normal) <= 1) throw new Exception("вечер не му се доспива");
            if (TimeFactor(d.AddHours(23), DayKind.Normal) < 4) throw new Exception("късно не е изморен");
            if (TimeFactor(d.AddHours(22.5), DayKind.NightOwl) >= 1) throw new Exception("нощната птица е уморена в 22:30");
        }, 100);
        await step("умора: работата уморява, почивката помага", () =>
        {
            _save.Energy = 60;
            StartFocus();
            if (EnergyRate() >= 0) throw new Exception("работата не уморява");
            double normal = EnergyRate();
            SetDayKind(DayKind.Energetic);
            if (EnergyRate() <= normal) throw new Exception("енергичният се уморява колкото обикновения");
            SetDayKind(DayKind.Lazy);
            if (EnergyRate() >= normal) throw new Exception("мързеливият се уморява по-малко");
            if (!InFocus || _scene != Scenes.Focus) throw new Exception("мързеливият отказа работа");
            SetDayKind(DayKind.Normal);
            StopPomodoro();
            GetUp();
            StartScene(Scenes.Reading, quiet: true);
            _save.Energy = 40;
            double tired = EnergyRate();
            _save.Energy = 70;
            double ok = EnergyRate();
            _save.Energy = 85;
            double fresh = EnergyRate();
            if (!(tired > ok && ok > 0 && fresh == 0)) throw new Exception($"почивката: {tired:0.0} / {ok:0.0} / {fresh:0.0}");
            _save.Energy = 40;
            DevOverload();
            GetUp();
            StartFocus();
            double over = EnergyRate();
            _workStreak = 0;
            if (over >= EnergyRate()) throw new Exception("претовареният не се уморява повече");
            StopPomodoro();
            GetUp();
            _save.Energy = 80;
        }, 300);
        await step("умора: почивка при мечо-доро", () =>
        {
            StartFocus();
            _save.PomodoroEndsAt = DateTime.Now;
            UpdatePomodoro();
            if (_save.PomodoroPhase != PomodoroPhase.Break) throw new Exception("няма почивка");
        }, 4000);
        await step("умора: почива си в почивката", () =>
        {
            if (!(_onTrampoline || (_inScene && _scene != null && Scenes.Fun.Contains(_scene))))
                throw new Exception("не седна да почива");
            _save.PomodoroEndsAt = DateTime.Now;
            UpdatePomodoro();
            if (_inScene || _onTrampoline) throw new Exception("не стана след почивката");
            SetIdle();
        }, 300);
        await step("умора: пита за дрямка", () =>
        {
            AskNap(20);
            if (!TakeNapQuestion()) throw new Exception("няма въпрос");
            _chat?.UpdateLayout();
            _chat?.Hide();
            _nextNapAsk = double.MaxValue;
            _save.Energy = 50;   // не „уморен“: иначе прозявката е по-бавна
            TakeNap(1);
            if (_save.NapUntil == DateTime.MinValue) throw new Exception("не легна да дремне");
        }, 4000);
        await step("умора: дреме", () =>
        {
            bool asleep = IsAsleep;
            _save.NapUntil = DateTime.Now;
            if (!asleep) throw new Exception("не заспа");
        }, 1000);
        await step("умора: събуди се от дрямката", () =>
        {
            if (IsAsleep || _save.NapUntil != DateTime.MinValue) throw new Exception("не стана от дрямката");
            _save.Energy = 80;
            SetIdle();
        }, 200);
        await step("вдигнат по време на работа", () =>
        {
            StartFocus();
        }, 2500);
        await step("вдигнат: работата тече", () =>
        {
            if (_scene != Scenes.Focus) throw new Exception("не работи");
            LiftUp();
            if (!InFocus) throw new Exception("вдигането спря работата");
            _vy = 0;
            SetState(BearState.Falling, _anim);
        }, 8000);
        await step("пуснат: пак работи", () =>
        {
            if (_scene != Scenes.Focus) throw new Exception("не се върна на бюрото");
            StopPomodoro();
        }, 1000);
        await step("всички реплики са във файла", () =>
        {
            if (R.Keys.Count < 200) throw new Exception("repliki.txt не се зареди");
            if (R.MissingKeys.Count > 0) throw new Exception("липсват: " + string.Join(", ", R.MissingKeys));
        }, 100);
    }

    /// <summary>Developer: всичко купено (уреди, рецепти, дрехи, фонове).</summary>
    internal void DevUnlockAll()
    {
        foreach (var u in Kitchen.Upgrades) if (!_save.Owned.Contains(u.Id)) _save.Owned.Add(u.Id);
        foreach (var f in Kitchen.Foods) if (!_save.Owned.Contains(Kitchen.RecipeId(f))) _save.Owned.Add(Kitchen.RecipeId(f));
        foreach (var b in Backdrops.All) if (!_save.Owned.Contains("bg_" + b.Id)) _save.Owned.Add("bg_" + b.Id);
        foreach (var g in Clothes.All) if (!_save.OwnedClothes.Contains(g.Id)) _save.OwnedClothes.Add(g.Id);
        foreach (var k in Furniture.ByItem.Values.SelectMany(x => x))
            if (!_save.Owned.Contains($"skin_{k.Item}_{k.Id}")) _save.Owned.Add($"skin_{k.Item}_{k.Id}");
        foreach (var p in Clothes.PajamaSets) if (!_save.OwnedClothes.Contains(p.Key)) _save.OwnedClothes.Add(p.Key);
        Persist();
        NotifyCare();
    }

    /// <summary>Developer: махни храната от екрана.</summary>
    /// <summary>Self-test: готви (на CI никой не пипа мишката и мечокът заспива: първо го буди).</summary>
    private void TestCook(Food food)
    {
        DevClearFood();
        if (_cook != null) EndCooking(food: null);
        if (IsAsleep) WakeUp();
        Cook(food);
        StartPendingCook();
        if (_cook == null)
            throw new Exception("не готви: " + string.Concat(_bubbleText.Inlines.OfType<System.Windows.Documents.Run>().Select(r => r.Text)));
    }

    internal void DevClearFood()
    {
        foreach (var f in _food.ToList()) f.Close();
        _food.Clear();
    }
}
