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

    public TrayIcon(PetWindow pet, SaveData save)
    {
        _pet = pet;
        _save = save;

        if (!save.FirstRunDone)
        {
            save.FirstRunDone = true;
            SetStartWithWindows(save.StartWithWindows);
            save.Save();
        }

        var menu = new Forms.ContextMenuStrip();
        var show = menu.Items.Add("Покажи мечока");
        show.Click += (_, _) => Toggle();
        var panel = menu.Items.Add("Отвори панела");
        panel.Click += (_, _) =>
        {
            if (!_pet.IsVisible) _pet.Show();
            _pet.OpenMenu(MenuWindow.BearTab);
        };
        var stay = menu.Items.Add("");
        stay.Click += (_, _) => { if (_pet.StaysPut) _pet.GetUp(); else _pet.StayHere(); };
        var sleep = menu.Items.Add("");
        sleep.Click += (_, _) => { if (_pet.IsAsleep) _pet.WakeUp(); else _pet.GoToSleep(); };
        var quiet = menu.Items.Add("");
        quiet.Click += (_, _) => _pet.SetQuiet(!_pet.IsQuiet);
        menu.Items.Add(new Forms.ToolStripSeparator());
        var update = menu.Items.Add("");
        update.Click += (_, _) =>
        {
            if (_pet.Updater.IsAvailable) _ = _pet.InstallUpdate();
            else _ = _pet.CheckForUpdates(manual: true);
        };
        var draw = menu.Items.Add("Рисувай платформи");
        draw.Click += (_, _) =>
        {
            if (!_pet.IsVisible) _pet.Show();
            _pet.OpenDrawing();
        };
        var reload = menu.Items.Add("Презареди рисунките");
        reload.Click += (_, _) => _pet.ReloadArt();
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
        exit.Click += (_, _) =>
        {
            _pet.Persist();
            System.Windows.Application.Current.Shutdown();
        };

        menu.Opening += (_, _) =>
        {
            show.Text = _pet.IsVisible ? "Скрий мечока" : "Покажи мечока";
            stay.Text = _pet.StaysPut ? "Стани от дивана" : "Стой тук и почети";
            sleep.Text = _pet.IsAsleep ? "Събуди се" : "Лягай да спиш";
            sleep.Enabled = _pet.CanSleep;
            quiet.Text = _pet.IsQuiet ? "Може да говориш" : "Тихо за 1 час";
            autostart.Checked = _save.StartWithWindows;
            update.Text = _pet.Updater.IsAvailable
                ? $"Обнови до версия {_pet.Updater.LatestVersion}"
                : $"Провери за обновление (сега: {Updater.CurrentVersion})";
            update.Enabled = !_pet.Updater.IsBusy;
        };

        _icon = new Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = $"Мечо, версия {Updater.CurrentVersion}",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) Toggle(); };
    }

    private void Toggle()
    {
        if (_pet.IsVisible) _pet.Hide();
        else _pet.Show();
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
