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
        // Ако нещо увисне, след 4 минути спираме с грешка.
        var watchdog = new DispatcherTimer { Interval = TimeSpan.FromMinutes(4) };
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

        foreach (var scene in Scenes.Fun)
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
            }, 7000);
            await step("изчисти храната", DevClearFood, 300);
        }

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
            foreach (var anim in new[] { "idle", "walk", "sleep", "eat", "happy", "dance", "read", "focus", "cook", "bounce" })
                for (int i = 0; i < 8; i++) _dresser.Frame(_lib, anim, i, _save.Outfit);
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
        await step("хоби само", () => { LeaveScene(); StartAutoHobby(); }, 1000);
        await step("след хобито пак седи", () => { if (_onTrampoline) EndBounce(null); else LeaveScene(); }, 500);
        await step("предложение за работа", () => { Ask(Conversations.WorkOffer(this)); _chat?.UpdateLayout(); _chat?.Hide(); }, 500);
        await step("обяд", () => { UpdateMeals(); ThankForMeal(); }, 300);
        await step("сън в леглото", GoToSleep, 1500);
        await step("събуди", WakeUp, 1000);
    }

    /// <summary>Developer: всичко купено (уреди, рецепти, дрехи, фонове).</summary>
    internal void DevUnlockAll()
    {
        foreach (var u in Kitchen.Upgrades) if (!_save.Owned.Contains(u.Id)) _save.Owned.Add(u.Id);
        foreach (var f in Kitchen.Foods) if (!_save.Owned.Contains(Kitchen.RecipeId(f))) _save.Owned.Add(Kitchen.RecipeId(f));
        foreach (var b in Backdrops.All) if (!_save.Owned.Contains("bg_" + b.Id)) _save.Owned.Add("bg_" + b.Id);
        foreach (var g in Clothes.All) if (!_save.OwnedClothes.Contains(g.Id)) _save.OwnedClothes.Add(g.Id);
        Persist();
        NotifyCare();
    }

    /// <summary>Developer: махни храната от екрана.</summary>
    internal void DevClearFood()
    {
        foreach (var f in _food.ToList()) f.Close();
        _food.Clear();
    }
}
