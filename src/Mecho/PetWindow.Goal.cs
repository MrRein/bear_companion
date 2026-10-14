using System;
using System.Collections.Generic;
using System.Linq;

namespace Mecho;

// Целта в магазина („Събираме за…“): избираш едно от 3 неща и лентата се пълни
// с лешниците; стигнат ли – „Купи“ и нещото идва с доставка. Едно от трите може
// веднъж да се смени („Друго“). В самото начало изборът е само гардеробът (в
// средата; другите две са заключени „?“). После – 3 случайни неща, в началото
// винаги поне един от основните уреди (тенджера, тиган).
public sealed partial class PetWindow
{
    /// <summary>Основните уреди: в началото поне един от тях е в избора.</summary>
    private static readonly string[] FirstTools = { Kitchen.Pot, Kitchen.Pan };

    /// <summary>Сегашната цел (или null, ако се избира между три).</summary>
    public Upgrade? Goal
    {
        get
        {
            EnsureGoal();
            return Kitchen.Find(_save.GoalId ?? "");
        }
    }

    /// <summary>Трите неща за избор (празно, ако вече има цел; в началото – само гардеробът).</summary>
    public IReadOnlyList<Upgrade> GoalChoices
    {
        get
        {
            EnsureGoal();
            return _save.GoalChoices.Select(Kitchen.Find).Where(u => u != null).Select(u => u!).ToList();
        }
    }

    public bool GoalRerollUsed => _save.GoalRerollUsed;

    public int GoalsDone => _save.GoalsDone;

    /// <summary>Всичко е купено (няма за какво повече да се събира).</summary>
    public bool AllGoalsDone => Goal == null && GoalChoices.Count == 0;

    private IEnumerable<Upgrade> GoalPool(IEnumerable<string> except) =>
        Kitchen.Upgrades.Where(u => !OwnsOrOrdered(u.Id) && u.Id != Kitchen.Wardrobe && !except.Contains(u.Id));

    /// <summary>Подрежда целта: първо гардеробът; после избор от три.</summary>
    private void EnsureGoal()
    {
        if (_save.GoalId != null && OwnsOrOrdered(_save.GoalId)) _save.GoalId = null;
        _save.GoalChoices.RemoveAll(id => OwnsOrOrdered(id) || Kitchen.Find(id) == null);
        if (_save.GoalId != null) return;
        if (!OwnsOrOrdered(Kitchen.Wardrobe))
        {
            // Началото: само гардеробът може да се избере.
            if (_save.GoalChoices.Count != 1 || _save.GoalChoices[0] != Kitchen.Wardrobe)
                _save.GoalChoices = new List<string> { Kitchen.Wardrobe };
            return;
        }
        if (_save.GoalChoices.Count == 0) NewGoalChoices();
    }

    /// <summary>Нови три неща за избор (поне един основен уред, ако още ги няма).</summary>
    private void NewGoalChoices()
    {
        var choices = new List<string>();
        var tools = FirstTools.Where(t => !OwnsOrOrdered(t)).ToList();
        if (tools.Count > 0) choices.Add(tools[_rng.Next(tools.Count)]);
        var pool = GoalPool(choices).OrderBy(_ => _rng.Next()).ToList();
        foreach (var u in pool)
        {
            if (choices.Count >= 3) break;
            choices.Add(u.Id);
        }
        // Основният уред да не е винаги първи.
        _save.GoalChoices = choices.OrderBy(_ => _rng.Next()).ToList();
        _save.GoalRerollUsed = false;
        Persist();
    }

    /// <summary>Избира цел от трите: лентата почва да се пълни.</summary>
    public void ChooseGoal(string id)
    {
        if (!_save.GoalChoices.Contains(id)) return;
        _save.GoalId = id;
        _save.GoalChoices.Clear();
        var u = Kitchen.Find(id);
        if (u != null) Say(R.Pick("goal.chosen", _save.OwnerName).Replace("{нещо}", $"{u.Icon} {u.Name.ToLowerInvariant()}"), 4);
        _goalReadySaid = false;
        Persist();
        NotifyCare();
    }

    /// <summary>„Презареди“ едно от трите (веднъж на избор).</summary>
    public void RerollGoal(string id)
    {
        if (_save.GoalRerollUsed || id == Kitchen.Wardrobe) return;
        int i = _save.GoalChoices.IndexOf(id);
        if (i < 0) return;
        var pool = GoalPool(_save.GoalChoices).ToList();
        if (pool.Count == 0) return;
        _save.GoalChoices[i] = pool[_rng.Next(pool.Count)].Id;
        _save.GoalRerollUsed = true;
        Persist();
        NotifyCare();
    }

    /// <summary>„Купи“: купува целта (идва с доставка).</summary>
    public void TakeGoal()
    {
        if (Goal is not { } u || _save.Hazelnuts < u.Price) return;
        Buy(u);
    }

    /// <summary>Вика се от Buy: купеното е целта – почва нов избор.</summary>
    private void GoalBought(string id)
    {
        if (_save.GoalId != id) return;
        _save.GoalId = null;
        _save.GoalsDone++;
        _goalReadySaid = false;
        EnsureGoal();
    }

    private bool _goalReadySaid;

    /// <summary>Лешниците стигнаха за целта: казва го веднъж (и рядко пак).</summary>
    private bool TalkAboutGoal()
    {
        if (Goal is not { } u) return false;
        string thing = $"{u.Icon} {u.Name.ToLowerInvariant()}";
        if (_save.Hazelnuts >= u.Price)
        {
            Mutter(R.Pick("goal.ready", _save.OwnerName).Replace("{нещо}", thing), 6);
            _goalReadySaid = true;
            return true;
        }
        if (_rng.Next(2) == 0) Mutter(R.Pick("goal.wish", _save.OwnerName).Replace("{нещо}", thing), 5);
        return true;
    }
}
