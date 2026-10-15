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

    public static int Run()
    {
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
            app.Shutdown(errors.Count == 0 ? 0 : 1);
        });
        // Ако нещо увисне, след 10 минути спираме с грешка.
        var watchdog = new DispatcherTimer { Interval = TimeSpan.FromMinutes(10) };
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
                errors.Add(ex.ToString());
                Log($"ГРЕШКА в „{name}“: {ex}");
            }
            await Task.Delay(waitMs);
            if (errors.Count > before) Log($"  ↑ грешка по време на „{name}“");
        }
    }
}

public sealed partial class PetWindow
{
    /// <summary>Стъпките на самопроверката (имат достъп до всичко вътре в мечока).</summary>
    internal async Task SelfTestSteps(Func<string, Action, int, Task> step)
    {
        await step("умора: обикновен ден за теста", () =>
        {
            _nextNapAsk = double.MaxValue;
            SetDayKind(DayKind.Normal);
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
            bool dark = IsDark(DateTime.Now);
            UpdateLamp();
            if (dark != (_lampWindow != null)) throw new Exception("лампата не следва тъмното");
            if (dark)
            {
                SetLamp(false);
                UpdateLamp();
                if (_lampWindow != null) throw new Exception("изгасената лампа свети");
                SetLamp(true);
            }
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
            _save.Owned.Remove(Kitchen.RecipeId(Kitchen.Foods.First(f => f.Id == "popcorn")));
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
            if (!Kitchen.HasRecipe(Kitchen.Foods.First(f => f.Id == "popcorn"), _save)) throw new Exception("тенджерата дойде без рецепта");
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
            AddTask("Тестова задача", 2);
            SetTaskDone(_save.Tasks[^1], true);
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
            if (!_bubbleWindow.IsVisible) throw new Exception("балончето не се показа");
            double bottom = _bubbleWindow.Top + _bubbleWindow.ActualHeight;
            double mid = _bubbleWindow.Left + _bubbleWindow.ActualWidth / 2;
            if (bottom > _bearRect.Top + 1 || Math.Abs(mid - (_bearRect.Left + _bearRect.Width / 2)) > _bearRect.Width + 300)
                throw new Exception($"балончето не е над мечока ({_bubbleWindow.Left:0},{_bubbleWindow.Top:0} спрямо {_bearRect})");
            if (Width > 400 * Math.Max(1, BearScale / 3.0)) throw new Exception("прозорецът на мечока е твърде голям: " + Width);
        }, 300);
        await step("балончето: скрива се с мечока", () =>
        {
            Hide();
            if (_bubbleWindow.IsVisible) throw new Exception("балончето остана без мечока");
            Show();
            HideBubble();
            if (_bubbleWindow.IsVisible) throw new Exception("балончето не се скри");
        }, 300);
        await step("пеперуда: в свой прозорец", () =>
        {
            SetIdle();
            StartChase();
        }, 600);
        await step("пеперуда: лети", () =>
        {
            if (!_butterflyWindow.IsVisible) throw new Exception("пеперудата не се вижда");
            _bfActive = false;
            SetIdle();
        }, 300);
        await step("пеперуда: отлетя", () =>
        {
            if (_butterflyWindow.IsVisible) throw new Exception("пеперудата остана");
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
            _save.Energy = 50;
            GoToSleep();
            if (IsAsleep || _save.SleepingByChoice) throw new Exception("заспа, докато се готви");
            AskStopCooking();
            _chat?.Hide();
            StopCooking();
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
        await step("гардероб", () => { OpenMenu(MenuWindow.WardrobeTab); _menu!.SelfTestWardrobe(); }, 800);
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
        await step("developer панел", () =>
        {
            var dev = new DevWindow(this);
            dev.Show();
            dev.UpdateLayout();
            dev.Close();
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
            _save.Energy = 60;
            Relax(Scenes.Reading);
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
            foreach (var (t, lvl) in new[] { (22.0, 0), (17.0, 1), (12.0, 2), (5.0, 3), (-5.0, 4) })
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
            TakeGoal();
            ReceiveOrdersNow();
            if (!Kitchen.Owns(_save, d.Id)) throw new Exception("целта не се купи");
            if (GoalChoices.Count == 0 && Goal == null && !AllGoalsDone) throw new Exception("няма нов избор");
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
