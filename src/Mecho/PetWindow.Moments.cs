using System;
using System.Collections.Generic;
using System.Linq;

namespace Mecho;

// Фаза 1 – паметта се усеща:
//   • сутрешните/следобедните спомени и желанието минават през режисьора на мислите;
//   • връщане след седмица; годишнини (дни заедно, мечо-дора, първият проект);
//   • спомен за завършен проект от време на време;
//   • откъде е всяко нещо (сандъче, подарък, цел) – и Мечо понякога го казва, когато го сложи.
public sealed partial class PetWindow
{
    /// <summary>Дни заедно, които се празнуват.</summary>
    private static readonly int[] DayMilestones = { 7, 30, 100, 200, 365, 500, 730, 1000 };

    /// <summary>Мечо-дора, които се празнуват.</summary>
    private static readonly int[] PomodoroMilestones = { 100, 250, 500, 1000, 2000, 5000 };

    /// <summary>Показвачите на спомените (вика се от SetupAlive).</summary>
    private void SetupMoments()
    {
        OnThought("memory.morning", _ => MorningMemory());
        OnThought("memory.later", _ => LaterMemory());
        OnThought("wish.favorite", _ => SayFavoriteWish());
    }

    /// <summary>Сутринта (новият ден): спомен, връщане след отсъствие, годишнина.</summary>
    private void ThinkMorning()
    {
        var (y, gap) = LastDay();
        int days = Journal.DaysTogether(DateTime.Today);
        if (DayMilestones.Contains(days) || days > 1000 && days % 365 == 0)
        {
            Remember("together.days", 8, ("дни", days.ToString()));
            Think("anniversary.days", 9, cooldownHours: 20, ("дни", days.ToString()));
        }
        if (gap >= 7 && R.Has("absence.week"))
        {
            Think("absence.week", 10, cooldownHours: 20, ("дни", gap.ToString()));
            return;
        }
        if (y != null) Think("memory.morning", gap >= 2 ? 9 : 5);
    }

    /// <summary>Следобед: спомен за днес, желание или (рядко) спомен за завършен проект.</summary>
    private void ThinkAfternoon()
    {
        var done = _save.HallOfFame;
        if (done.Count > 0 && _rng.Next(4) == 0)
        {
            var f = done[_rng.Next(done.Count)];
            if (Think("memory.project", 4, cooldownHours: 24 * 7, ("проект", f.Name), ("тема", f.Theme.ToLowerInvariant()))) return;
        }
        if (_rng.Next(2) == 0) Think("memory.later", 3);
        else Think("wish.favorite", 4);
    }

    /// <summary>След всяко мечо-доро: кръгъл брой – празник.</summary>
    private void CheckPomodoroMilestone()
    {
        int total = _save.PomodorosTotal;
        if (!PomodoroMilestones.Contains(total)) return;
        Remember("pomodoro.milestone", 7, ("брой", total.ToString()));
        Think("anniversary.pomodoro", 8, cooldownHours: 1, ("брой", total.ToString()));
    }

    /// <summary>Проектът е завършен: запомня го (и първият – празник).</summary>
    private void RememberProjectDone()
    {
        string theme = _save.ProjectAnswers.GetValueOrDefault("theme", "");
        Remember("project.done", 9, ("проект", _save.GameName), ("тема", theme), ("вид", CurrentProject.Kind));
        if (CoreEngine.Started) CoreEngine.Bus.Send(new Mecho.Core.Tally("projects"));
        if (Journal.Counter("projects") == 1) Think("anniversary.project", 9, cooldownHours: 0, ("проект", _save.GameName));
    }

    // ── Откъде е всяко нещо ──

    /// <summary>Id в инвентара за код от loot_list (cloth:x → x, bg:x → bg_x…).</summary>
    internal static string InventoryId(string code)
    {
        int i = code.IndexOf(':');
        if (i < 0) return code;
        string kind = code[..i], id = code[(i + 1)..];
        return kind switch
        {
            "cloth" => id,
            "pajama" => "pajama_" + id,
            "bg" => "bg_" + id,
            "theme" => "ui_" + id,
            "skin" => "skin_" + id.Replace(':', '_'),
            _ => id,   // up:…
        };
    }

    /// <summary>Откъде е наградата от сандъчето на проекта.</summary>
    private string ChestOrigin => _save.GameName.Length > 0 ? $"от сандъчето на „{_save.GameName}“" : "от сандъчето на проекта";

    /// <summary>Слага нещо (дреха, тапет, облик…): ако е от сандъче/подарък/цел – понякога си спомня.</summary>
    private void MentionOrigin(string inventoryId, string name)
    {
        if (!CoreEngine.Started || Journal.OriginOf(inventoryId) is not { } o) return;
        if (!(o.From.StartsWith("от ") || o.From.StartsWith("награда"))) return;   // купеното от магазина – без спомен
        if (_rng.Next(3) != 0) return;
        Think("memory.item", 3, cooldownHours: 6, ("нещо", name.ToLowerInvariant()), ("откъде", o.From));
    }

    /// <summary>Самопроверка: дава нещо от loot_list с произход.</summary>
    internal string? SelfTestGrant(string code, string from) => GrantLoot(code, from);

    /// <summary>Откъде е (за подсказката в гардероба): „От сандъчето на „Горски тайни“, 22.11.“</summary>
    public string? OriginNote(string inventoryId)
    {
        if (!CoreEngine.Started || Journal.OriginOf(inventoryId) is not { } o) return null;
        if (!(o.From.StartsWith("от ") || o.From.StartsWith("награда"))) return null;
        return char.ToUpper(o.From[0]) + o.From[1..] + ", " + o.When.ToString("d.MM.yyyy") + ".";
    }
}
