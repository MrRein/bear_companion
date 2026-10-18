using System;
using System.Collections.Generic;
using System.Linq;

namespace Mecho.Core;

/// <summary>
/// Планировчикът: „след 5 мин направи…“, „на всеки 40–70 мин, ако…“. Заменя
/// разпилените полета _nextXxx. Всяка задача има собственик (модул) и име; ново
/// планиране със същото име замества старото; CancelAll(собственик) маха всичко
/// на модула (изключен ли е модулът – нищо негово не изпълзява по-късно).
///
/// Пример (в модул – виж GameModule.Every/After):
/// <code>
/// ctx.Scheduler.Every(this, "chatter", 40 * 60, 70 * 60, Talk, when: () => !asleep);
/// ctx.Scheduler.After(this, "gift.remind", 15 * 60, Remind);
/// </code>
/// Грешка в задача се записва, вдига JobFaulted и не спира другите. Повтарящата се
/// задача продължава (освен ако модулът не бъде изключен заради много грешки).
/// </summary>
public sealed class Scheduler
{
    private sealed class Job
    {
        public required object Owner { get; init; }
        public required string Name { get; init; }
        public required Action Action { get; init; }
        public double Due { get; set; }
        public double Min { get; init; }
        public double Max { get; init; }
        public bool Repeat { get; init; }
        public Func<bool>? When { get; init; }
        public bool Cancelled { get; set; }
    }

    private readonly IClock _clock;
    private readonly IRandom _rng;
    private readonly ICoreLog _log;
    private readonly List<Job> _jobs = new();

    public Scheduler(IClock clock, IRandom rng, ICoreLog log)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>Задача гръмна: (собственик, грешка).</summary>
    public event Action<object, Exception>? JobFaulted;

    /// <summary>Веднъж, след толкова секунди.</summary>
    public void After(object owner, string name, double seconds, Action action) =>
        Add(owner, name, action, Math.Max(0, seconds), 0, 0, repeat: false, when: null);

    /// <summary>
    /// Повтаря на всеки min…max секунди (случайно в интервала). when – ако е false, този
    /// път се пропуска (и пак се планира). Първият път е след min…max.
    /// </summary>
    public void Every(object owner, string name, double minSeconds, double maxSeconds, Action action, Func<bool>? when = null)
    {
        if (minSeconds <= 0 || maxSeconds < minSeconds) throw new ArgumentOutOfRangeException(nameof(minSeconds), "Интервалът трябва да е > 0 и min ≤ max.");
        Add(owner, name, action, 0, minSeconds, maxSeconds, repeat: true, when);
    }

    private void Add(object owner, string name, Action action, double delay, double min, double max, bool repeat, Func<bool>? when)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(action);
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Задачата иска име.", nameof(name));
        Cancel(owner, name);
        var job = new Job { Owner = owner, Name = name, Action = action, Min = min, Max = max, Repeat = repeat, When = when };
        job.Due = _clock.Mono + (repeat ? _rng.Between(min, max) : delay);
        _jobs.Add(job);
    }

    /// <summary>Има ли планирана задача с това име.</summary>
    public bool IsScheduled(object owner, string name) => _jobs.Any(j => !j.Cancelled && ReferenceEquals(j.Owner, owner) && j.Name == name);

    /// <summary>След колко секунди е задачата (null – няма такава).</summary>
    public double? DueIn(object owner, string name) =>
        _jobs.FirstOrDefault(j => !j.Cancelled && ReferenceEquals(j.Owner, owner) && j.Name == name) is { } j ? j.Due - _clock.Mono : null;

    public void Cancel(object owner, string name)
    {
        foreach (var j in _jobs)
            if (ReferenceEquals(j.Owner, owner) && j.Name == name) j.Cancelled = true;
        _jobs.RemoveAll(j => j.Cancelled);
    }

    public void CancelAll(object owner)
    {
        foreach (var j in _jobs)
            if (ReferenceEquals(j.Owner, owner)) j.Cancelled = true;
        _jobs.RemoveAll(j => j.Cancelled);
    }

    /// <summary>Изпълнява наближилите задачи (вика се всеки цикъл от GameEngine.Update).</summary>
    public void Update()
    {
        double now = _clock.Mono;
        // Копие: задача може да планира/отменя други по време на изпълнение.
        foreach (var job in _jobs.Where(j => !j.Cancelled && j.Due <= now).OrderBy(j => j.Due).ToList())
        {
            if (job.Cancelled) continue;
            if (job.Repeat) job.Due = now + _rng.Between(job.Min, job.Max);
            else job.Cancelled = true;
            try
            {
                if (job.When == null || job.When()) job.Action();
            }
            catch (Exception ex)
            {
                _log.Error($"Задача „{job.Name}“ ({SignalBus.OwnerName(job.Owner)}) гръмна", ex);
                try { JobFaulted?.Invoke(job.Owner, ex); }
                catch (Exception inner) { _log.Error("JobFaulted гръмна", inner); }
            }
        }
        _jobs.RemoveAll(j => j.Cancelled);
    }

    /// <summary>Колко задачи има (диагностика).</summary>
    public int Count => _jobs.Count;
}
