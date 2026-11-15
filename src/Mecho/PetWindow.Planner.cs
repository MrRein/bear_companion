using System;
using System.Linq;
using Mecho.Core;

namespace Mecho;

// Задачите по дни: седмицата (PlannerWindow), местене от ден в ден, етикети, важни, време,
// и пренасянето на недовършените за днес.
public sealed partial class PetWindow
{
    private PlannerWindow? _planner;

    /// <summary>Днешният ден за задачите (по часовника на ядрото – Developer може да го мести).</summary>
    public DateTime TaskToday => CoreEngine.Clock.Today();

    public TasksState TaskState => CoreEngine.Store.Get<TasksState>();

    public bool PlannerIsOpen => _planner != null;

    /// <summary>Отваря седмицата (или я вдига отпред, ако е отворена).</summary>
    public void OpenPlanner()
    {
        if (_menu is { IsOpen: true }) _menu.Hide();
        if (_planner == null)
        {
            _planner = new PlannerWindow(this);
            _planner.Closed += (_, _) =>
            {
                _planner = null;
                _save.Planner.Open = false;
                Persist();
            };
            _save.Planner.Open = true;
            _planner.Show();
        }
        else
        {
            if (_planner.WindowState == System.Windows.WindowState.Minimized) _planner.WindowState = System.Windows.WindowState.Normal;
            _planner.Activate();
        }
        Persist();
    }

    public void ClosePlanner() => _planner?.Close();

    /// <summary>Мести задачата в ден (null – „Някога“), преди before (или накрая на деня).</summary>
    public void MoveTask(TaskItem task, DateTime? day, Guid? before = null)
    {
        if (!CoreEngine.Bus.Send(new MoveTask(task.Id, day, before)).Ok) return;
        NotifyCare();
        Persist();
    }

    /// <summary>Маркира: етикет ("" – без), важна, време (0 – не е казано), големина.</summary>
    public void MarkTask(TaskItem task, string? tag = null, bool? star = null, int? minutes = null, int? size = null)
    {
        if (!CoreEngine.Bus.Send(new MarkTask(task.Id, tag, star, minutes, size)).Ok) return;
        if (star == true) Emote("sparkle", 1);
        NotifyCare();
        Persist();
    }

    /// <summary>Вика се веднъж от конструктора (преди ядрото да тръгне – пренасянето при пускане също се чува).</summary>
    private void WirePlanner()
    {
        CoreEngine.Bus.Subscribe<TasksRolledOver>(this, s =>
        {
            NotifyCare();
            // След малко (при пускане – след поздрава); заспал – не го буди.
            Later(9, () =>
            {
                if (IsAsleep || OnWalk || SelfTest.Running) return;
                Say(R.Text("tasks.rolled", _save.OwnerName, ("брой", s.Count.ToString())), 5);
            });
        });
        TaskCards.DragEnded += () => NotifyCare();
    }

    /// <summary>При пускане: седмицата беше отворена – пак се показва.</summary>
    private void RestorePlanner()
    {
        if (_save.Planner.Open && !SelfTest.Running) OpenPlanner();
    }

    /// <summary>Менюто е отворено или току-що се затвори (цъкането другаде го затваря, преди копчето да разбере).</summary>
    public bool MenuOpenOrJustClosed => MenuIsOpen || Now - _menuClosedAt < 0.3;

    /// <summary>Копчето с мечока в таблото: отваря менюто, а ако е било отворено – затваря го.</summary>
    public void ToggleMenuFrom(int tab, bool wasOpen)
    {
        if (wasOpen)
        {
            if (MenuIsOpen) _menu!.Hide();
            return;
        }
        OpenMenu(tab);
    }

    /// <summary>„Редактирай…“ от таблото: менюто, направо на задачата.</summary>
    public void EditTaskInMenu(TaskItem task)
    {
        OpenMenu(MenuWindow.TasksTab);
        _menu!.ShowNewTask(true, task);
    }

    /// <summary>Колко задачи са за днес и колко са свършени, и колко време са (за обобщенията).</summary>
    public (int Done, int All, int Minutes) TodayLoad()
    {
        var today = TaskState.OnDay(TaskToday).ToList();
        return (today.Count(t => t.Done), today.Count, today.Where(t => !t.Done).Sum(t => t.Minutes));
    }
}
