using System;
using System.Collections.Generic;
using System.Linq;

namespace Mecho;

// Закачените прозорчета (задачи, бележки) и задачата, по която се работи.
public sealed partial class PetWindow
{
    private readonly Dictionary<string, PinWindow> _pins = new();

    public bool IsPinned(string kind) => _pins.ContainsKey(kind);

    /// <summary>Закача/откача прозорчето (📌 в таба „Задачи“ или „Бележки“).</summary>
    public void SetPinned(string kind, bool on)
    {
        var state = kind == PinWindow.Tasks ? _save.PinTasks : _save.PinNotes;
        state.Open = on;
        if (on && !_pins.ContainsKey(kind))
        {
            var w = new PinWindow(this, kind);
            w.Closed += (_, _) => _pins.Remove(kind);
            _pins[kind] = w;
            w.Show();
        }
        else if (!on && _pins.TryGetValue(kind, out var open))
        {
            _pins.Remove(kind);
            open.Close();
        }
        SavePins();
        NotifyCare();
    }

    public void SavePins() => Persist();

    /// <summary>При пускане: закачените от миналия път пак се показват.</summary>
    private void RestorePins()
    {
        CareChanged += (_, _) =>
        {
            foreach (var p in _pins.Values) p.Refresh();
        };
        if (_save.PinTasks.Open) SetPinned(PinWindow.Tasks, true);
        if (_save.PinNotes.Open) SetPinned(PinWindow.Notes, true);
    }

    // ── Задачата, по която се работи ──

    /// <summary>Задачата, по която работим в мечо-дорото или работния таймер (или null).</summary>
    public TaskItem? FocusTask => _save.FocusTask is { } id ? _save.Tasks.FirstOrDefault(t => t.Id == id && !t.Done) : null;

    public void SetFocusTask(TaskItem? task)
    {
        _save.FocusTask = task?.Id;
        NotifyCare();
        Persist();
    }

    /// <summary>▶ до задачата: мечо-доро по нея.</summary>
    public void StartFocusOn(TaskItem task)
    {
        _save.FocusTask = task.Id;
        if (!InFocus) StartFocus();
        else
        {
            NotifyCare();
            Persist();
        }
    }

    /// <summary>Работата свърши, а имаше задача: „Свърши ли я?“ (след малко, след репликата).</summary>
    private void AskAboutFocusTask()
    {
        if (FocusTask is not { } task) return;
        Later(3, () =>
        {
            if (task.Done) return;
            string title = task.Title.Length > 40 ? task.Title[..40] + "…" : task.Title;
            Ask(new ChatQuestion($"Свърши ли „{title}“?",
                new ChatOption("✓ Да!", p => { p.SetTaskDone(task, true); p.SetFocusTask(null); }),
                new ChatOption("Още не", p => p.Say(R.Pick("focustask.notyet", p.Save.OwnerName), 3))));
        });
    }

    /// <summary>Кратко име на задачата за табелката над мечока.</summary>
    private string FocusTaskShort => FocusTask is { } t ? (t.Title.Length > 18 ? t.Title[..17] + "…" : t.Title) : "";
}
