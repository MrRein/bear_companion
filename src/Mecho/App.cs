using System;
using System.Threading;
using System.Windows;

namespace Mecho;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        // Само един мечок наведнъж.
        using var mutex = new Mutex(true, "Mecho.SingleInstance", out bool first);
        if (!first) return;

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var save = SaveData.Load();
        var window = new PetWindow(save);
        using var tray = new TrayIcon(window, save);
        window.Show();
        app.Run();
        save.Save();
    }
}
