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
        if (args.Contains("--selftest")) return SelfTest.Run();

        // Само един мечок наведнъж.
        using var mutex = new Mutex(true, "Mecho.SingleInstance", out bool first);
        if (!first) return 0;

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
        window.Show();
        app.Run();
        save.Save();
        return 0;
    }
}

/// <summary>Записва грешките в %AppData%\Mecho\crash.log (за да ги прати Тут при проблем).</summary>
public static class CrashLog
{
    public static string Path => System.IO.Path.Combine(SaveData.Folder, "crash.log");

    public static void Write(Exception? ex)
    {
        try
        {
            System.IO.Directory.CreateDirectory(SaveData.Folder);
            System.IO.File.AppendAllText(Path,
                $"── {DateTime.Now:yyyy-MM-dd HH:mm:ss}  версия {Updater.CurrentVersion}\n{ex}\n\n");
        }
        catch (Exception)
        {
            // Дори дневникът да не може да се запише, мечокът продължава.
        }
    }
}
