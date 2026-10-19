using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Mecho;

// НАПОМНЯНИЯ: част от бележката – тикче „⏰ Напомни“ (или ⏰ до записана бележка): след
// малко (5 мин… 3 ч, свои минути) или ден и точен час, по желание с повторение (всеки ден,
// делнични, седмица, месец, година). Всички се виждат в „Бележки“ → „Напомняния“. Като дойде
// часът, мечокът пита с балонче: Готово / След 10 мин / След 1 ч / Утре – и звъни (динг).
// Скрит е – появява се, напомня и пак се скрива; на разходка – идва, напомня и се връща на
// разходката. Пропуснатото (програмата е била спряна) – казва го, щом се пусне.
public sealed partial class PetWindow
{
    public static readonly (string Id, string Name)[] ReminderRepeats =
    {
        ("none", "Веднъж"), ("daily", "Всеки ден"), ("weekdays", "Делнични"),
        ("weekly", "Всяка седмица"), ("monthly", "Всеки месец"), ("yearly", "Всяка година"),
    };

    private static readonly CultureInfo Bg = new("bg-BG");

    private readonly Dictionary<Guid, double> _reminderAsked = new();   // кога е питал (затвори ли балончето – пак след 10 мин)
    private double _nextReminderCheck;

    public void AddReminder(string text, DateTime due, string repeat, Guid? noteId = null)
    {
        text = text.Trim();
        if (text.Length == 0) return;
        _save.Reminders.Add(new ReminderItem { Text = text, Due = due, Repeat = repeat, NoteId = noteId });
        Say(R.Pick("reminder.added", _save.OwnerName).Replace("{кога}", WhenText(due)), 3);
        NotifyCare();
        Persist();
    }

    /// <summary>Напомнянето на бележка (едно на бележка – новото сменя старото).</summary>
    public void SetNoteReminder(NoteItem note, DateTime due, string repeat)
    {
        _save.Reminders.RemoveAll(r => r.NoteId == note.Id);
        AddReminder(note.Label, due, repeat, note.Id);
    }

    public void ClearNoteReminder(NoteItem note)
    {
        _save.Reminders.RemoveAll(r => r.NoteId == note.Id);
        NotifyCare();
        Persist();
    }

    /// <summary>Бележка с напомняне наведнъж (напр. „Отговори на писмото“ от пощата).</summary>
    public void AddNoteReminder(string text, DateTime due, string repeat = "none")
    {
        if (AddNote(text, "", pinned: true, quiet: true) is { } note) SetNoteReminder(note, due, repeat);
    }

    /// <summary>„30 мин“, „1 ч“, „1 ч 30 мин“.</summary>
    public static string MinutesText(double minutes)
    {
        int m = (int)Math.Round(minutes);
        if (m < 60) return $"{m} мин";
        return m % 60 == 0 ? $"{m / 60} ч" : $"{m / 60} ч {m % 60} мин";
    }

    public void DeleteReminder(ReminderItem r)
    {
        _save.Reminders.Remove(r);
        NotifyCare();
        Persist();
    }

    /// <summary>Готово: повтарящото се – следващия път; еднократното – маха се.</summary>
    public void CompleteReminder(ReminderItem r)
    {
        if (r.Repeat == "none") _save.Reminders.Remove(r);
        else r.Due = NextDue(r.Due, r.Repeat, DateTime.Now);
        _reminderAsked.Remove(r.Id);
        NotifyCare();
        Persist();
    }

    /// <summary>Отлага (след 10 мин, 1 ч, утре по същото време).</summary>
    public void SnoozeReminder(ReminderItem r, TimeSpan by)
    {
        r.Due = DateTime.Now + by;
        _reminderAsked.Remove(r.Id);
        NotifyCare();
        Persist();
    }

    /// <summary>Следващият път на повтарящо се напомняне (след now).</summary>
    public static DateTime NextDue(DateTime due, string repeat, DateTime now)
    {
        var next = due;
        for (int guard = 0; next <= now && guard < 5000; guard++)
        {
            next = repeat switch
            {
                "daily" => next.AddDays(1),
                "weekdays" => next.AddDays(next.DayOfWeek == DayOfWeek.Friday ? 3 : next.DayOfWeek == DayOfWeek.Saturday ? 2 : 1),
                "weekly" => next.AddDays(7),
                "monthly" => next.AddMonths(1),
                "yearly" => next.AddYears(1),
                _ => now.AddMinutes(1),
            };
        }
        return next;
    }

    /// <summary>„днес в 14:30“, „утре в 9:00“, „пн, 21 окт. в 9:00“.</summary>
    public static string WhenText(DateTime t)
    {
        var day = t.Date == DateTime.Today ? "днес"
            : t.Date == DateTime.Today.AddDays(1) ? "утре"
            : t.Date == DateTime.Today.AddDays(-1) ? "вчера"
            : t.ToString(t.Year == DateTime.Today.Year ? "ddd, d MMM" : "d MMM yyyy", Bg);
        return $"{day} в {t:H:mm}";
    }

    public static string RepeatName(string id) => ReminderRepeats.FirstOrDefault(r => r.Id == id).Name ?? "";

    /// <summary>На кадър (на 5 с): дошъл ли е часът на някое напомняне – пита (или съобщава до часовника).</summary>
    private void UpdateReminders(double now)
    {
        if (now < _nextReminderCheck || SelfTest.Running) return;
        _nextReminderCheck = now + 5;
        var due = _save.Reminders.Where(r => r.Due <= DateTime.Now
                && !(_reminderAsked.TryGetValue(r.Id, out var asked) && now - asked < 10 * 60))
            .OrderBy(r => r.Due).FirstOrDefault();
        if (due == null || _walkingOut || _walkingBack) return;   // тръгва/идва – след малко
        if (!IsVisible || OnWalk)
        {
            PopInForReminder(due);
            return;
        }
        if (ChatIsOpen) return;   // друг въпрос е отворен – след него
        AskReminder(due);
    }

    // Появил се е само заради напомнянето: после пак се скрива („hidden“) или се връща на разходката („walk“).
    private string? _popIn;
    private double _popWalkLeft;
    private bool _popWalkNap, _popWalkSolo;

    /// <summary>Скрит – появява се и пита; на разходка – идва отстрани, пита и после пак тръгва.</summary>
    internal void PopInForReminder(ReminderItem r)
    {
        if (OnWalk)
        {
            _popIn = "walk";
            _popWalkLeft = WalkLeft.TotalMinutes;
            _popWalkNap = _save.WalkNap;
            _popWalkSolo = _save.WalkSolo;
            _reminderAsked[r.Id] = Now;   // докато идва, да не го вика пак
            ReturnFromWalk(called: true, arrived: () => AskReminder(r));
            return;
        }
        _popIn = "hidden";
        ShowBear();
        AskReminder(r);
    }

    /// <summary>Балончето се затвори: ако е дошъл само за напомнянето – след малко пак си отива.</summary>
    private void AfterReminderChat()
    {
        if (_popIn == null) return;
        Later(3, () =>
        {
            if (_popIn == null || ChatIsOpen) return;   // нов въпрос – отива си след него
            string mode = _popIn;
            _popIn = null;
            if (MenuIsOpen || _state == BearState.Drag) return;   // Тут се занимава с него – остава
            if (mode == "hidden")
            {
                HideBubble();
                Hide();
            }
            else if (mode == "walk" && _popWalkLeft >= 1 && !OnWalk)
            {
                GoForWalk(_popWalkLeft, solo: _popWalkSolo);
                if (OnWalk) _save.WalkNap = _popWalkNap;
            }
        });
    }

    /// <summary>Балончето с напомнянето.</summary>
    internal void AskReminder(ReminderItem r)
    {
        bool missed = DateTime.Now - r.Due > TimeSpan.FromMinutes(10);
        string head = missed ? R.Pick("reminder.missed", _save.OwnerName).Replace("{кога}", WhenText(r.Due))
            : R.Pick("reminder.now", _save.OwnerName);
        Sounds.Play(_save);
        Emote("exclaim", 3);
        Ask(new ChatQuestion($"{head}\n⏰ {r.Text}",
            new ChatOption("✅ Готово", p => { p.CompleteReminder(r); p.Say(R.Pick("reminder.done", p._save.OwnerName), 3); }),
            new ChatOption("След 10 мин", p => p.SnoozeReminder(r, TimeSpan.FromMinutes(10))),
            new ChatOption("След 1 ч", p => p.SnoozeReminder(r, TimeSpan.FromHours(1))),
            new ChatOption("Утре", p => p.SnoozeReminder(r, TimeSpan.FromDays(1)))));
        // Затвори ли балончето, без да избере – пита пак след 10 мин (часът му не се мести).
        _reminderAsked[r.Id] = Now;
    }

    /// <summary>Напомнянето за бележка (ако има).</summary>
    public ReminderItem? ReminderFor(NoteItem note) => _save.Reminders.Where(r => r.NoteId == note.Id).OrderBy(r => r.Due).FirstOrDefault();
}
