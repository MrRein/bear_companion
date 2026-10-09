using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Mecho;

// Грижа и работа: глад, енергия, храна, задачи и мед-доро.
public sealed partial class PetWindow
{
    // Колко бързо се променят нуждите (точки на час).
    private const double HungerAwake = 20;     // сит → гладен за ~4 часа
    private const double HungerAsleep = 7;
    private const double TiredAwake = 14;      // бодър → сънлив за ~6 часа
    private const double RestAsleep = 300;     // от 20 до 100 за ~16 минути дрямка

    public static readonly TimeSpan FocusLength = TimeSpan.FromMinutes(25);
    public static readonly TimeSpan BreakLength = TimeSpan.FromMinutes(5);

    private double _nextNeedLine;
    private readonly Border _timerTag = new();
    private readonly TextBlock _timerText = new();

    public SaveData Save => _save;

    /// <summary>Вика се, когато нещо в грижата се промени (за панела).</summary>
    public event EventHandler? CareChanged;

    private void NotifyCare() => CareChanged?.Invoke(this, EventArgs.Empty);

    private void InitCare()
    {
        _timerText.FontFamily = new FontFamily("Segoe UI");
        _timerText.FontWeight = FontWeights.Bold;
        _timerText.FontSize = 12;
        _timerText.Foreground = new SolidColorBrush(Color.FromRgb(59, 36, 20));
        _timerTag.Child = _timerText;
        _timerTag.Background = new SolidColorBrush(Color.FromRgb(255, 214, 102));
        _timerTag.BorderBrush = new SolidColorBrush(Color.FromRgb(59, 36, 20));
        _timerTag.BorderThickness = new Thickness(2);
        _timerTag.CornerRadius = new CornerRadius(3);
        _timerTag.Padding = new Thickness(5, 0, 5, 1);
        _timerTag.IsHitTestVisible = false;
        _timerTag.Visibility = Visibility.Collapsed;
        _root.Children.Add(_timerTag);

        ApplyOfflineTime();
        StartNewDay();
    }

    /// <summary>
    /// Докато програмата е спряна, мечокът почива (компютърът спи) и огладнява
    /// бавно. Никога не се връща умрял от глад: най-много „огладнял“.
    /// </summary>
    private void ApplyOfflineTime()
    {
        if (_save.LastSeen == default) return;
        double hours = Math.Max(0, (DateTime.Now - _save.LastSeen).TotalHours);
        if (hours < 0.05) return;
        _save.Energy = Math.Min(100, _save.Energy + hours * RestAsleep);
        _save.Fullness = Math.Max(Math.Min(_save.Fullness, 30), _save.Fullness - hours * HungerAsleep);
    }

    /// <summary>Нов ден: свършените вчера задачи се махат, броячът на мед-дора се нулира.</summary>
    private void StartNewDay()
    {
        string today = DateTime.Today.ToString("yyyy-MM-dd");
        if (_save.PomodoroDay != today)
        {
            _save.PomodoroDay = today;
            _save.PomodorosToday = 0;
        }
        _save.Tasks.RemoveAll(t => t.Done && t.DoneAt?.Date < DateTime.Today);
    }

    private bool IsResting => IsAsleep || _anim == "read_sleep";

    private void UpdateCare(double dt, double now)
    {
        double h = dt / 3600;
        _save.Fullness = Math.Clamp(_save.Fullness - (IsResting ? HungerAsleep : HungerAwake) * h, 0, 100);
        _save.Energy = Math.Clamp(_save.Energy + (IsResting ? RestAsleep : -TiredAwake) * h, 0, 100);

        UpdatePomodoro();

        // Казва, че е гладен или сънлив, но не по-често от веднъж на няколко минути.
        if (!IsResting && !IsQuiet && !InFocus && now > _nextNeedLine && _state != BearState.Drag)
        {
            if (_save.Fullness < 25)
            {
                _nextNeedLine = now + 240 + _rng.Next(120);
                Say(Lines.Pick(Lines.Hungry, _save.OwnerName), 5);
            }
            else if (_save.Energy < 25)
            {
                _nextNeedLine = now + 240 + _rng.Next(120);
                Say(Lines.Pick(Lines.Sleepy, _save.OwnerName), 5);
            }
        }
    }

    /// <summary>Сам ли иска да дремне (вика се, когато стои без работа).</summary>
    private bool WantsNap => _save.Energy < 15 && !InFocus;

    // ───────────────────────── Храна ─────────────────────────

    public void Feed(Food food)
    {
        if (!Kitchen.IsUnlocked(food, _save)) return;
        if (IsResting)
        {
            Say("Ззз… ще ям, като стана.", 3);
            return;
        }
        if (_save.Fullness >= 95)
        {
            Say(Lines.Pick(Lines.Full, _save.OwnerName), 3);
            return;
        }
        _save.Fullness = Math.Min(100, _save.Fullness + food.Fullness);
        Say(food.Lines[_rng.Next(food.Lines.Length)], 4);
        if (CanAnimateFreely) Play("eat");
        NotifyCare();
        Persist();
    }

    public void Pet()
    {
        if (IsResting)
        {
            Say("*мърка насън*", 3);
            return;
        }
        Say(Lines.Pick(Lines.Petted, _save.OwnerName), 3);
        if (CanAnimateFreely) Play("happy");
    }

    /// <summary>Може ли да пусне анимация, без да прекъсне дивана, влаченето или съня.</summary>
    private bool CanAnimateFreely => !OnCouchMission && !IsAsleep && _state is not (BearState.Drag or BearState.Falling);

    // ───────────────────────── Задачи ─────────────────────────

    public void AddTask(string title, int size)
    {
        title = title.Trim();
        if (title.Length == 0) return;
        _save.Tasks.Add(new TaskItem { Title = title, Size = Math.Clamp(size, 1, 3) });
        Say(Lines.Pick(Lines.TaskAdded, _save.OwnerName), 3);
        NotifyCare();
        Persist();
    }

    public void SetTaskDone(TaskItem task, bool done)
    {
        if (task.Done == done) return;
        task.Done = done;
        task.DoneAt = done ? DateTime.Now : null;

        if (!done)
        {
            // Отмяна: връщаме наградата.
            _save.Hazelnuts = Math.Max(0, _save.Hazelnuts - task.Reward);
            _save.TasksDoneTotal = Math.Max(0, _save.TasksDoneTotal - 1);
            NotifyCare();
            Persist();
            return;
        }

        _save.Hazelnuts += task.Reward;
        _save.TasksDoneTotal++;

        var unlocked = Kitchen.UnlockedAt(_save.TasksDoneTotal);
        if (unlocked != null)
            Say($"Ура! Отключи {unlocked.Unlock}! Вече мога да правя {unlocked.Name.ToLowerInvariant()}! {unlocked.Icon}", 8);
        else
            Say(Lines.Pick(task.Size switch { 3 => Lines.BigTaskDone, 2 => Lines.TaskDone, _ => Lines.SmallTaskDone }, _save.OwnerName) +
                $" +{task.Reward} 🌰", 4);

        if (CanAnimateFreely) Play(task.Size >= 3 || unlocked != null ? "dance" : "happy", length: task.Size >= 3 ? 3 : 0);
        NotifyCare();
        Persist();
    }

    public void DeleteTask(TaskItem task)
    {
        _save.Tasks.Remove(task);
        NotifyCare();
        Persist();
    }

    // ───────────────────────── Бележки ─────────────────────────

    public void AddNote(string text, bool quiet = false)
    {
        text = text.Trim();
        if (text.Length == 0) return;
        _save.Notes.Insert(0, new NoteItem { Text = text });
        if (!quiet) Say(Lines.Pick(Lines.NoteAdded, _save.OwnerName), 3);
        NotifyCare();
        Persist();
    }

    public void DeleteNote(NoteItem note)
    {
        _save.Notes.Remove(note);
        NotifyCare();
        Persist();
    }

    // ───────────────────────── Мед-доро ─────────────────────────

    public bool InFocus => _save.PomodoroPhase == PomodoroPhase.Focus;

    public TimeSpan PomodoroLeft =>
        _save.PomodoroPhase == PomodoroPhase.Off ? TimeSpan.Zero : Max(TimeSpan.Zero, _save.PomodoroEndsAt - DateTime.Now);

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

    public void StartFocus()
    {
        _save.PomodoroPhase = PomodoroPhase.Focus;
        _save.PomodoroEndsAt = DateTime.Now + FocusLength;
        Say(Lines.Pick(Lines.FocusStart, _save.OwnerName), 4);
        if (CanAnimateFreely) Play("work", length: 20);
        NotifyCare();
        Persist();
    }

    public void StopPomodoro()
    {
        if (_save.PomodoroPhase == PomodoroPhase.Off) return;
        bool wasFocus = InFocus;
        _save.PomodoroPhase = PomodoroPhase.Off;
        Say(wasFocus ? "Добре, спираме. Ще продължим после." : "Почивката свърши по-рано. Хайде!", 3);
        NotifyCare();
        Persist();
    }

    private void UpdatePomodoro()
    {
        if (_save.PomodoroPhase == PomodoroPhase.Off || DateTime.Now < _save.PomodoroEndsAt) return;

        StartNewDay();
        if (InFocus)
        {
            _save.PomodorosTotal++;
            _save.PomodorosToday++;
            _save.Hazelnuts++;
            _save.PomodoroPhase = PomodoroPhase.Break;
            _save.PomodoroEndsAt = DateTime.Now + BreakLength;
            Say(Lines.Pick(Lines.FocusDone, _save.OwnerName) + " +1 🌰", 8);
            if (CanAnimateFreely) Play("dance", length: 3);
        }
        else
        {
            _save.PomodoroPhase = PomodoroPhase.Off;
            Say(Lines.Pick(Lines.BreakDone, _save.OwnerName), 6);
            if (CanAnimateFreely) Play("happy");
        }
        NotifyCare();
        Persist();
    }

    private void UpdateTimerTag()
    {
        if (_save.PomodoroPhase == PomodoroPhase.Off)
        {
            _timerTag.Visibility = Visibility.Collapsed;
            return;
        }
        var left = PomodoroLeft;
        string icon = InFocus ? "🍯" : "☕";
        _timerText.Text = $"{icon} {(int)left.TotalMinutes:00}:{left.Seconds:00}";
        _timerTag.Visibility = Visibility.Visible;
    }
}
