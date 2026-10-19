using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Mecho;

// ПРОГРАМИТЕ НА ТУТ: мечокът поглежда коя програма е отпред (веднъж на 2 часа) и понякога (20%) казва
// нещо за нея – различно за Photoshop, Illustrator, браузъра, игрите… Засега само
// коментари (никакви специални действия).
//
//  • Кое какво е: assets/programi.txt (вграденият списък) + programi.txt в папката на
//    записа (%AppData%\Mecho) – там Тут дописва свои; нейният ред печели.
//    Ред: „име на програмата (без .exe) = вид“, например „photoshop = adobe.photoshop“.
//    Репликите за вида са „app.<вид>“ в repliki.txt (няма ли – тези на „app.<първата част>“).
//  • Непознатите се записват в nepoznati_programi.txt (до записа): име, описание и
//    „= ?“ – копира се в programi.txt и „?“ се сменя с вида.
//  • Нищо не излиза от компютъра; заглавията на прозорците не се записват никъде
//    (ползват се само за сайтовете в браузъра: YouTube, Pinterest…).
public sealed partial class PetWindow
{
    private Dictionary<string, string>? _appKinds;
    private double _nextAppCheck = 20 * 60;   // първия път – 20 мин след пускането
    private readonly HashSet<string> _unknownApps = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Файлът, в който Тут дописва свои програми.</summary>
    public static string UserAppsPath => Path.Combine(SaveData.Folder, "programi.txt");

    /// <summary>Непознатите програми (за дописване).</summary>
    public static string UnknownAppsPath => Path.Combine(SaveData.Folder, "nepoznati_programi.txt");

    private static string BuiltInAppsPath => Path.Combine(AppContext.BaseDirectory, "assets", "programi.txt");

    /// <summary>Неща на Windows, които не са „програма, с която работи“ (не се коментират, не се записват).</summary>
    private static readonly HashSet<string> IgnoredApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer", "mecho", "searchhost", "searchapp", "shellexperiencehost", "startmenuexperiencehost", "lockapp",
        "applicationframehost", "textinputhost", "systemsettings", "taskmgr", "idle", "dwm", "ctfmon", "rundll32",
        "openwith", "pickerhost", "consent", "winlogon", "credentialuibroker", "shellhost",
    };

    /// <summary>Чете двата списъка (вградения и този на Тут). Вика се отново с „Презареди“.</summary>
    internal Dictionary<string, string> LoadAppKinds()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in new[] { BuiltInAppsPath, UserAppsPath })
        {
            if (!File.Exists(path)) continue;
            foreach (var raw in File.ReadAllLines(path))
            {
                string line = raw;
                int hash = line.IndexOf('#');
                if (hash >= 0) line = line[..hash];
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string name = line[..eq].Trim().ToLowerInvariant().Replace(".exe", "");
                string kind = line[(eq + 1)..].Trim().ToLowerInvariant();
                if (name.Length == 0 || kind.Length == 0 || kind == "?") continue;
                map[name] = kind;
            }
        }
        // Файлът на Тут: щом го няма – правим го с обяснение, за да знае как се пише.
        try
        {
            if (!File.Exists(UserAppsPath))
                File.WriteAllText(UserAppsPath,
                    "# Тук дописваш програми, които мечокът не познава (виж nepoznati_programi.txt).\n" +
                    "# Ред: име на програмата (без .exe) = вид. Видовете са в assets/programi.txt, например:\n" +
                    "#   photoshop = adobe.photoshop\n#   chrome = browser\n#   mygame = game\n" +
                    "# Нов вид (например „knitting“) иска реплики „app.knitting“ в repliki.txt.\n\n");
        }
        catch (Exception)
        {
        }
        _appKinds = map;
        return map;
    }

    /// <summary>Видът на програмата (adobe.photoshop, browser.youtube…), или null – непозната.</summary>
    internal string? AppKind(string process, string title)
    {
        var map = _appKinds ?? LoadAppKinds();
        if (!map.TryGetValue(process.ToLowerInvariant(), out var kind)) return null;
        // В браузъра – по сайта (само от заглавието, нищо не се пази).
        if (kind == "browser")
        {
            string t = title.ToLowerInvariant();
            if (t.Contains("youtube")) return "browser.youtube";
            if (t.Contains("pinterest")) return "browser.pinterest";
            if (t.Contains("artstation") || t.Contains("behance") || t.Contains("dribbble") || t.Contains("deviantart")) return "browser.art";
            if (t.Contains("gmail") || t.Contains("outlook") || t.Contains("поща")) return "browser.mail";
            if (t.Contains("netflix") || t.Contains("hbo") || t.Contains("disney+") || t.Contains("prime video")) return "browser.video";
            if (t.Contains("canva") || t.Contains("figma")) return "design";
            if (t.Contains("instagram") || t.Contains("facebook")) return "browser.social";
        }
        return kind;
    }

    /// <summary>
    /// Веднъж на 2 часа поглежда коя програма е отпред: непозната – записва я; позната –
    /// с шанс 20% казва нещо за нея (ако е свободен: не работите, не спи, не е тихо, вижда се).
    /// </summary>
    private void UpdateApps(double now)
    {
        if (now < _nextAppCheck || !_save.WatchApps || SelfTest.Running) return;
        _nextAppCheck = now + AppCheckEvery;
        try
        {
            var (pid, title) = NativeMethods.ForegroundWindow();
            if (pid == 0) return;
            using var p = Process.GetProcessById((int)pid);
            if (IgnoredApps.Contains(p.ProcessName)) return;
            string? kind = AppKind(p.ProcessName, title);
            if (kind == null)
            {
                NoteUnknownApp(p);
                return;
            }
            if (_rng.NextDouble() < AppTalkChance) TalkAboutApp(kind);
        }
        catch (Exception)
        {
            // Процесът е свършил междувременно, нямаме права… – нищо.
        }
    }

    /// <summary>На колко време поглежда (секунди) и с какъв шанс казва нещо.</summary>
    private const double AppCheckEvery = 2 * 3600, AppTalkChance = 0.2;

    private void TalkAboutApp(string kind)
    {
        if (IsQuiet || IsAsleep || InFocus || OnWalk || !IsVisible || ChatIsOpen) return;
        if (AppLinesKey(kind) is { } key) Mutter(R.Pick(key, _save.OwnerName), 5);
    }

    /// <summary>Репликите за вида: „app.adobe.photoshop“, а ако ги няма – „app.adobe“.</summary>
    internal static string? AppLinesKey(string kind)
    {
        for (string k = kind; k.Length > 0; k = k.Contains('.') ? k[..k.LastIndexOf('.')] : "")
            if (R.Has("app." + k)) return "app." + k;
        return null;
    }

    /// <summary>Непозната програма: записва я (веднъж) – „име = ?  # описание“.</summary>
    private void NoteUnknownApp(Process p)
    {
        string name = p.ProcessName.ToLowerInvariant();
        if (!_unknownApps.Add(name)) return;
        try
        {
            string known = File.Exists(UnknownAppsPath) ? File.ReadAllText(UnknownAppsPath) : "";
            if (known.Contains("\n" + name + " =") || known.StartsWith(name + " =")) return;
            string about = "";
            try { about = p.MainModule?.FileVersionInfo.FileDescription ?? ""; }
            catch (Exception) { }
            if (known.Length == 0)
                File.WriteAllText(UnknownAppsPath,
                    "# Програми, които мечокът не познава. Копирай реда в programi.txt (същата папка)\n" +
                    "# и смени „?“ с вида (виж assets/programi.txt: adobe.photoshop, browser, game, chat…).\n\n");
            File.AppendAllText(UnknownAppsPath, $"{name} = ?{(about.Length > 0 ? "    # " + about : "")}\n");
        }
        catch (Exception)
        {
        }
    }

    /// <summary>Developer: казва нещо за програмата отпред сега (без чакане).</summary>
    internal void DevAppTalk()
    {
        LoadAppKinds();
        var (pid, title) = NativeMethods.ForegroundWindow();
        try
        {
            using var p = Process.GetProcessById((int)pid);
            string? kind = AppKind(p.ProcessName, title);
            string? key = kind == null ? null : AppLinesKey(kind);
            Say(key == null ? $"Не познавам „{p.ProcessName}“ – записах го." : R.Pick(key, _save.OwnerName), 5);
            if (kind == null) NoteUnknownApp(p);
        }
        catch (Exception)
        {
            Say("Не виждам коя програма е отпред.", 3);
        }
    }
}
