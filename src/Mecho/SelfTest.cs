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
        // Ако нещо увисне, след 6 минути спираме с грешка.
        var watchdog = new DispatcherTimer { Interval = TimeSpan.FromMinutes(6) };
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
        }, 100);
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
            if (learned != Kitchen.Foods.Count(f => f.Cost > 0)) throw new Exception("не всички рецепти се научиха");
            if (Recipes.AddPage(t) != null) throw new Exception("страница след всичко");
            FindRecipePage();
            OpenMenu(MenuWindow.FoodTab);
            _menu!.UpdateLayout();
        }, 400);
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
        await step("магазин: всички раздели", () => _menu!.SelfTestShop(), 400);
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

        foreach (var food in Kitchen.Foods)
        {
            var f = food;
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
            foreach (var (slot, id) in Clothes.Pajamas) night[slot] = id;
            foreach (var anim in new[] { "idle", "sleep", "read_sleep" })
                for (int i = 0; i < 4; i++) _dresser.Frame(_lib, anim, i, night);
            SetPajamas(false);
            SetPajamas(true);
        }, 300);
        await step("скинове", () =>
        {
            foreach (var skin in Furniture.CouchSkins) UseSkin(skin);
            Relax(Scenes.Reading);
            foreach (var skin in Furniture.BedSkins) UseSkin(skin);
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
            if (_heaterWindow == null) throw new Exception("изгасената печица изчезна");
            _tempC = 20;
            WarmUpByHeater();
            _save.Owned.Remove(Kitchen.Heater);
            UpdateHeater(Now);
            if (_heaterWindow != null) throw new Exception("печицата не изчезна");
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
            foreach (var (t, lvl) in new[] { (20.0, 0), (12.0, 1), (7.0, 2), (2.0, 3), (-5.0, 4) })
            {
                _tempC = t;
                if (ColdLevel != lvl) throw new Exception($"{t}°C: ниво {ColdLevel}, а трябва {lvl}");
                TalkAboutWeather();
                if (WeatherText == null) throw new Exception("няма текст");
            }
            _tempC = null;
        }, 300);
        await step("предложение за работа", () => { Ask(Conversations.WorkOffer(this)); _chat?.UpdateLayout(); _chat?.Hide(); }, 500);
        await step("обяд", () => { UpdateMeals(); ThankForMeal(); }, 300);
        await step("сън в леглото", GoToSleep, 1500);
        await step("събуди", WakeUp, 1000);
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
            if (hour is >= 5 and < 12 ? _save.Energy != 100 : _save.Energy != 10) throw new Exception("сутрешната енергия е " + _save.Energy);
            _save.Energy = 80;
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
        foreach (var k in Furniture.CouchSkins.Concat(Furniture.BedSkins))
            if (!_save.Owned.Contains($"skin_{k.Item}_{k.Id}")) _save.Owned.Add($"skin_{k.Item}_{k.Id}");
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
