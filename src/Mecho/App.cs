using System;
using System.Linq;
using System.Threading;
using System.Windows;

namespace Mecho;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--selftest"))
        {
            // Самопроверката не бива да пипа истинския запис (save.json, отговорите, perf.log…).
            SaveData.UseFolder(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MechoSelfTest"));
            return SelfTest.Run();
        }

        // Само един мечок наведнъж.
        using var mutex = new Mutex(true, "Mecho.SingleInstance", out bool first);
        if (!first) return 0;

        // Мечокът почти не товари процесора (0,1%), но когато компютърът е зает (пишеш,
        // браузър, игра), Windows дава кадрите му накрая и анимацията накъсва. С малко
        // по-висок приоритет кадрите идват навреме; програмата пак е лека.
        try { System.Diagnostics.Process.GetCurrentProcess().PriorityClass = System.Diagnostics.ProcessPriorityClass.AboveNormal; }
        catch (Exception) { /* няма права – нищо */ }

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        // Неочаквана грешка: записва се в crash.log и мечокът продължава, вместо да изчезне.
        app.DispatcherUnhandledException += (_, e) =>
        {
            CrashLog.Write(e.Exception);
            e.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => CrashLog.Write(e.ExceptionObject as Exception);
        var save = SaveData.Load();
        var window = new PetWindow(save);
        using var tray = new TrayIcon(window, save);
        // Изключване/излизане от Windows: записва веднага (обикновено записът е отложен с 2 с).
        app.SessionEnding += (_, _) => window.PersistNow();
        window.Show();
        app.Run();
        save.Save();
        return 0;
    }
}

/// <summary>
/// Записва грешките в %AppData%\Mecho\crash.log (за да ги прати Тут при проблем).
/// Една и съща грешка се записва веднъж на 10 мин (иначе грешка в цикъла би писала
/// по 30 пъти в секунда), а файлът се отрязва над 1 MB.
/// </summary>
public static class CrashLog
{
    public static string Path => System.IO.Path.Combine(SaveData.Folder, "crash.log");

    private static readonly System.Collections.Generic.Dictionary<string, DateTime> Seen = new();

    public static void Write(Exception? ex)
    {
        string key = ex == null ? "null" : ex.GetType().Name + ex.Message + ex.StackTrace?.Split('\n').FirstOrDefault();
        lock (Seen)
        {
            if (Seen.TryGetValue(key, out var last) && DateTime.Now - last < TimeSpan.FromMinutes(10)) return;
            Seen[key] = DateTime.Now;
        }
        Append($"── {DateTime.Now:yyyy-MM-dd HH:mm:ss}  версия {Updater.CurrentName}\n{ex}\n\n");
    }

    /// <summary>Бележка (не грешка) – например „заредено е резервното копие“.</summary>
    public static void Note(string text) => Append($"── {DateTime.Now:yyyy-MM-dd HH:mm:ss}  {text}\n\n");

    private static void Append(string text)
    {
        try
        {
            System.IO.Directory.CreateDirectory(SaveData.Folder);
            var file = new System.IO.FileInfo(Path);
            if (file.Exists && file.Length > 1_000_000) System.IO.File.Move(Path, Path + ".old", true);
            System.IO.File.AppendAllText(Path, text);
        }
        catch (Exception)
        {
            // Дори дневникът да не може да се запише, мечокът продължава.
        }
    }
}
