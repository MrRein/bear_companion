using System;
using System.Collections.Generic;
using System.Linq;

namespace Mecho.Core;

// ─────────────────────────────────────────────────────────────────────────────
// МЕЧО ЗАБЕЛЯЗВА ТЕБ (фаза 3). Всяко наблюдение е малък клас IObserver: гледа
// обстановката (CareContext + задачите) и предлага мисъл на режисьора. Режисьорът
// решава кога да се каже (приказливост); повторенията ги спира паузата (Cooldown).
//
//     care.Add(new MyObserver());            // ново наблюдение – нов клас
//     care.Context = () => new CareContext(…); // приложението казва какво става
//
// Наблюденията се проверяват веднъж в минута (и ръчно с CheckCare).
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Какво става (дава го приложението). SinceBreakMinutes – колко работи без истинска почивка;
/// Working – сега работите (мечо-доро/таймер); GapBeforeToday – колко дни преди днес не е работено
/// (0 – вчера е работено); Streak – дни подред с работа до вчера.
/// </summary>
public sealed record CareContext(
    DateTime Now, bool Working, double SinceBreakMinutes, int PomodorosToday, int TasksDoneToday,
    int Streak, int GapBeforeToday, bool Awake = true);

/// <summary>Предложение за мисъл от наблюдение.</summary>
public sealed record CareIdea(string Key, int Importance, double CooldownHours, params (string Name, string Value)[] Facts);

public interface IObserver
{
    /// <summary>null – няма какво да каже.</summary>
    CareIdea? Look(CareContext c, TasksState tasks);
}

/// <summary>Провери наблюденията сега (Developer/тестове).</summary>
public sealed record CheckCare : IGameCommand;

/// <summary>Модулът „грижа“ (Id: care, зависи от thoughts).</summary>
public sealed class CareModule : GameModule
{
    public const string ModuleId = "care";

    private readonly List<IObserver> _observers = new();

    public CareModule(bool standard = true)
    {
        if (!standard) return;
        Add(new OverworkObserver());
        Add(new LateObserver());
        Add(new ProcrastinationObserver());
        Add(new BadDayObserver());
        Add(new StreakObserver());
        Add(new ComebackObserver());
    }

    public CareModule Add(IObserver observer)
    {
        _observers.Add(observer);
        return this;
    }

    /// <summary>Какво става (приложението). null – наблюденията мълчат.</summary>
    public Func<CareContext>? Context { get; set; }

    public override string Id => ModuleId;

    public override IReadOnlyCollection<string> DependsOn { get; } = new[] { ThoughtsModule.ModuleId };

    protected override void OnRegister()
    {
        Handle<CheckCare>(_ =>
        {
            Look();
            return CommandResult.Done;
        });
        // Голяма задача е свършена: празник (с името ѝ).
        On<TaskCompleted>(t =>
        {
            if (t.Size < 3) return;
            string title = Store.TryGet<TasksState>(out var s) && s != null ? s.Open.Concat(s.History).FirstOrDefault(x => x.Id == t.Id)?.Title ?? "" : "";
            Send(new Think("care.bigtask", 8, Facts.Of(("задача", title))));
        });
        Every("care", 60, 60, Look);
    }

    private void Look()
    {
        if (Context?.Invoke() is not { } c || !c.Awake) return;
        var tasks = Store.TryGet<TasksState>(out var s) && s != null ? s : TasksState.Empty;
        foreach (var o in _observers)
        {
            CareIdea? idea;
            try { idea = o.Look(c, tasks); }
            catch (Exception ex)
            {
                Log.Error($"наблюдението {o.GetType().Name}: {ex.Message}");
                continue;
            }
            if (idea != null) Send(new Think(idea.Key, idea.Importance, Facts.Of(idea.Facts), idea.CooldownHours));
        }
    }

    // ── Стандартните наблюдения ──

    /// <summary>Работиш 2+ часа без истинска почивка.</summary>
    public sealed class OverworkObserver : IObserver
    {
        public const double Minutes = 120;

        public CareIdea? Look(CareContext c, TasksState t) =>
            c.Working && c.SinceBreakMinutes >= Minutes
                ? new("care.overwork", 7, 2, ("часа", ((int)(c.SinceBreakMinutes / 60)).ToString()), ("минути", ((int)c.SinceBreakMinutes).ToString()))
                : null;
    }

    /// <summary>Работиш след 23 ч. (до 4 ч.).</summary>
    public sealed class LateObserver : IObserver
    {
        public CareIdea? Look(CareContext c, TasksState t) =>
            c.Working && (c.Now.Hour >= 23 || c.Now.Hour < 4) ? new("care.late", 7, 3, ("час", c.Now.ToString("H:mm"))) : null;
    }

    /// <summary>Задача, пренасяна 4+ дни.</summary>
    public sealed class ProcrastinationObserver : IObserver
    {
        public const int Days = 4;

        public CareIdea? Look(CareContext c, TasksState t)
        {
            if (c.Working || c.Now.Hour < 9 || c.Now.Hour >= 20) return null;
            var task = t.Open.Where(x => !x.Done && x.Rolled >= Days && x.Day is { } d && d.Date <= c.Now.Date)
                .OrderByDescending(x => x.Rolled).FirstOrDefault();
            return task == null ? null
                : new("care.procrastinate", 6, 24, ("задача", task.Title), ("дни", task.Rolled.ToString()), ("id", task.Id.ToString()));
        }
    }

    /// <summary>Вечер, а днес почти нищо не е свършено: утешава, без укор.</summary>
    public sealed class BadDayObserver : IObserver
    {
        public CareIdea? Look(CareContext c, TasksState t) =>
            !c.Working && c.Now.Hour is >= 18 and < 22 && c.PomodorosToday == 0 && c.TasksDoneToday == 0
                ? new("care.badday", 4, 20) : null;
    }

    /// <summary>3+ дни подред с работа – гордее се (след първото мечо-доро днес).</summary>
    public sealed class StreakObserver : IObserver
    {
        public CareIdea? Look(CareContext c, TasksState t) =>
            c.PomodorosToday >= 1 && c.Streak >= 3 ? new("care.streak", 6, 20, ("дни", (c.Streak + 1).ToString())) : null;
    }

    /// <summary>Първата работа след 3+ дни пауза – радва се.</summary>
    public sealed class ComebackObserver : IObserver
    {
        public CareIdea? Look(CareContext c, TasksState t) =>
            c.PomodorosToday >= 1 && c.GapBeforeToday >= 3 ? new("care.comeback", 7, 20, ("дни", c.GapBeforeToday.ToString())) : null;
    }
}
