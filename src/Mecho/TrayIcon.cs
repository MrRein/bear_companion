using System;
using System.Drawing;
using System.Windows;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace Mecho;

/// <summary>Иконката до часовника: показва/скрива мечока, настройки, изход.</summary>
public sealed class TrayIcon : IDisposable
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly PetWindow _pet;
    private readonly SaveData _save;
    private readonly Forms.NotifyIcon _icon;

    /// <summary>Иконката (за съобщенията до часовника – напомнянията, докато мечокът е скрит).</summary>
    public static TrayIcon? Current { get; private set; }

    /// <summary>Съобщение до часовника.</summary>
    public void Balloon(string title, string text)
    {
        try { _icon.ShowBalloonTip(10000, title, text, Forms.ToolTipIcon.None); }
        catch (Exception) { }
    }

    public TrayIcon(PetWindow pet, SaveData save)
    {
        Current = this;
        _pet = pet;
        _save = save;

        if (!save.FirstRunDone)
        {
            save.FirstRunDone = true;
            SetStartWithWindows(save.StartWithWindows);
            save.Save();
        }

        var menu = new Forms.ContextMenuStrip();
        // Менюто на мечока (и на разходка – той не се връща само от това).
        var panel = menu.Items.Add("Отвори меню");
        panel.Click += (_, _) => OpenMenu();
        // Точно под него: Скрий (показан е) / Покажи (скрит е) / Повикай (на разходка – връща се).
        var visibility = menu.Items.Add("");
        visibility.Click += (_, _) => Toggle();
        // Таймери: табелката до часовника, докато мечокът е скрит.
        var timers = new Forms.ToolStripMenuItem("Таймери");
        timers.Click += (_, _) =>
        {
            _save.ShowTimers = !_save.ShowTimers;
            _save.Save();
        };
        menu.Items.Add(timers);
        menu.Items.Add(new Forms.ToolStripSeparator());
        var update = menu.Items.Add("");
        update.Click += (_, _) =>
        {
            if (_pet.Updater.IsAvailable) _ = _pet.InstallUpdate();
            else _ = _pet.CheckForUpdates(manual: true);
        };
        var autostart = new Forms.ToolStripMenuItem("Пускай се с Windows");
        autostart.Click += (_, _) =>
        {
            _save.StartWithWindows = !_save.StartWithWindows;
            SetStartWithWindows(_save.StartWithWindows);
            _save.Save();
        };
        menu.Items.Add(autostart);
        menu.Items.Add(new Forms.ToolStripSeparator());
        var exit = menu.Items.Add("Изход");

        // Пикселните иконки и тук (вместо празно място отляво).
        menu.ImageScalingSize = new System.Drawing.Size(24, 24);
        (Forms.ToolStripItem Item, string Icon)[] icons =
        {
            (panel, "bear"), (update, "update"), (exit, "close"),
        };
        foreach (var (item, icon) in icons) item.Image = PixelIcon(icon);
        exit.Click += (_, _) =>
        {
            _pet.PersistNow();
            System.Windows.Application.Current.Shutdown();
        };

        menu.Opening += (_, _) =>
        {
            timers.Checked = _save.ShowTimers;
            // Окото – скрий/покажи; човечето от „Разходи се“ – повикай го от разходката.
            visibility.Text = _pet.OnWalk ? "Повикай" : _pet.IsVisible ? "Скрий" : "Покажи";
            visibility.Image = _pet.OnWalk ? _walkIcon : _eyeIcon;
            autostart.Checked = _save.StartWithWindows;
            update.Text = _pet.Updater.IsAvailable
                ? "Обнови"
                : "Провери за обновление";
            update.Enabled = !_pet.Updater.IsBusy;
        };

        _icon = new Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "Мечо",
            ContextMenuStrip = menu,
            Visible = true,
        };
        // Цъкнеш ли съобщението (напомняне) – отваря бележките.
        _icon.BalloonTipClicked += (_, _) => _pet.OpenMenu(MenuWindow.NotesTab);
        // При посочване: таймерите (мечо-доро, таймер, разходка) – до 63 знака.
        _icon.MouseMove += (_, _) =>
        {
            string t = _pet.TimersText;
            _icon.Text = t.Length > 63 ? t[..63] : t;
        };
        // Цъкане: менюто на мечока (Developer може да го върне на „скрий/покажи“, както беше).
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button != Forms.MouseButtons.Left) return;
            if (_save.TrayClickHides) Toggle();
            else OpenMenu();
        };
    }

    private readonly System.Drawing.Image? _eyeIcon = PixelIcon("eye"), _walkIcon = PixelIcon("walk");

    /// <summary>Само отваря менюто на мечока: не го показва (скрит си остава скрит), не го вика от разходка.</summary>
    private void OpenMenu() => _pet.OpenMenu(MenuWindow.BearTab);

    /// <summary>Скрий / Покажи (мечока и рисунките заедно) или – на разходка – Повикай.</summary>
    private void Toggle()
    {
        if (_pet.OnWalk)
        {
            _pet.ReturnFromWalk(called: true);
            return;
        }
        if (_pet.IsVisible)
        {
            _pet.Hide();
            _pet.SetDrawingsHidden(true);
        }
        else Show();
    }

    private void Show()
    {
        _pet.ComeBackFromEdge();   // влиза с ходене иззад ръба (спи/в кътче – направо там)
        if (_pet.DrawingsHidden) _pet.SetDrawingsHidden(false);
    }

    /// <summary>Пикселна иконка от assets/ui, увеличена 2 пъти без размазване.</summary>
    private static System.Drawing.Image? PixelIcon(string name)
    {
        try
        {
            string path = System.IO.Path.Combine(AppContext.BaseDirectory, "assets", "ui", name + ".png");
            if (!System.IO.File.Exists(path)) return null;
            using var src = new Bitmap(path);
            var big = new Bitmap(src.Width * 2, src.Height * 2);
            using var g = Graphics.FromImage(big);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            g.DrawImage(src, 0, 0, big.Width, big.Height);
            return big;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static Icon LoadIcon()
    {
        try
        {
            var res = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/mecho.ico"));
            if (res != null) return new Icon(res.Stream);
        }
        catch (Exception)
        {
        }
        return SystemIcons.Application;
    }

    private static void SetStartWithWindows(bool on)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (on && Environment.ProcessPath is string exe) key.SetValue("Mecho", $"\"{exe}\"");
            else key.DeleteValue("Mecho", false);
        }
        catch (Exception)
        {
        }
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
