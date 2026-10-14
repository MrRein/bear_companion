using System;
using System.Collections.Generic;
using System.Linq;

namespace Mecho;

// Мечтата на Мечо: винаги има една цел с лента, която се пълни с лешниците.
// Първата мечта е гардеробът. После мечокът дава избор между 3 случайни неща
// (в началото винаги поне един от основните уреди – тенджера, тиган); едно от
// трите може веднъж да се „презареди“. Избереш ли – лентата почва да се пълни
// според лешниците; стигнат ли, „Вземи“ и нещото идва с доставка.
public sealed partial class PetWindow
{
    /// <summary>Основните уреди: в началото поне един от тях е в избора.</summary>
    private static readonly string[] FirstTools = { Kitchen.Pot, Kitchen.Pan };

    /// <summary>Сегашната мечта (или null, ако се избира между три).</summary>
    public Upgrade? Dream
    {
        get
        {
            EnsureDream();
            return Kitchen.Find(_save.DreamId ?? "");
        }
    }

    /// <summary>Трите неща за избор (празно, ако вече има мечта).</summary>
    public IReadOnlyList<Upgrade> DreamChoices
    {
        get
        {
            EnsureDream();
            return _save.DreamChoices.Select(Kitchen.Find).Where(u => u != null).Select(u => u!).ToList();
        }
    }

    public bool DreamRerollUsed => _save.DreamRerollUsed;

    public int DreamsDone => _save.DreamsDone;

    /// <summary>Всичко е сбъднато (няма какво повече да се мечтае).</summary>
    public bool AllDreamsDone => Dream == null && DreamChoices.Count == 0;

    private IEnumerable<Upgrade> DreamPool(IEnumerable<string> except) =>
        Kitchen.Upgrades.Where(u => !Kitchen.Owns(_save, u.Id) && u.Id != Kitchen.Wardrobe && !except.Contains(u.Id));

    /// <summary>Подрежда мечтата: първо гардеробът; после избор от три.</summary>
    private void EnsureDream()
    {
        if (_save.DreamId != null && Kitchen.Owns(_save, _save.DreamId)) _save.DreamId = null;
        _save.DreamChoices.RemoveAll(id => Kitchen.Owns(_save, id) || Kitchen.Find(id) == null);
        if (_save.DreamId != null) return;
        if (!Kitchen.Owns(_save, Kitchen.Wardrobe))
        {
            _save.DreamId = Kitchen.Wardrobe;
            _save.DreamChoices.Clear();
            return;
        }
        if (_save.DreamChoices.Count == 0) NewDreamChoices();
    }

    /// <summary>Нови три неща за избор (поне един основен уред, ако още ги няма).</summary>
    private void NewDreamChoices()
    {
        var choices = new List<string>();
        var tools = FirstTools.Where(t => !Kitchen.Owns(_save, t)).ToList();
        if (tools.Count > 0) choices.Add(tools[_rng.Next(tools.Count)]);
        var pool = DreamPool(choices).OrderBy(_ => _rng.Next()).ToList();
        foreach (var u in pool)
        {
            if (choices.Count >= 3) break;
            choices.Add(u.Id);
        }
        // Основният уред да не е винаги първи.
        _save.DreamChoices = choices.OrderBy(_ => _rng.Next()).ToList();
        _save.DreamRerollUsed = false;
        Persist();
    }

    /// <summary>Избира мечта от трите: лентата почва да се пълни.</summary>
    public void ChooseDream(string id)
    {
        if (!_save.DreamChoices.Contains(id)) return;
        _save.DreamId = id;
        _save.DreamChoices.Clear();
        var u = Kitchen.Find(id);
        if (u != null) Say(R.Pick("dream.chosen", _save.OwnerName).Replace("{нещо}", $"{u.Icon} {u.Name.ToLowerInvariant()}"), 4);
        _dreamReadySaid = false;
        Persist();
        NotifyCare();
    }

    /// <summary>„Презареди“ едно от трите (веднъж на избор).</summary>
    public void RerollDream(string id)
    {
        if (_save.DreamRerollUsed) return;
        int i = _save.DreamChoices.IndexOf(id);
        if (i < 0) return;
        var pool = DreamPool(_save.DreamChoices).ToList();
        if (pool.Count == 0) return;
        _save.DreamChoices[i] = pool[_rng.Next(pool.Count)].Id;
        _save.DreamRerollUsed = true;
        Persist();
        NotifyCare();
    }

    /// <summary>„Вземи“: купува мечтата (идва с доставка).</summary>
    public void TakeDream()
    {
        if (Dream is not { } u || _save.Hazelnuts < u.Price) return;
        Buy(u);
    }

    /// <summary>Вика се от Buy: купеното е мечтата – сбъдната е, и почва нов избор.</summary>
    private void DreamBought(string id)
    {
        if (_save.DreamId != id) return;
        _save.DreamId = null;
        _save.DreamsDone++;
        _dreamReadySaid = false;
        EnsureDream();
    }

    private bool _dreamReadySaid;

    /// <summary>Лешниците стигнаха за мечтата: казва го веднъж (и рядко пак).</summary>
    private bool TalkAboutDream()
    {
        if (Dream is not { } u) return false;
        string thing = $"{u.Icon} {u.Name.ToLowerInvariant()}";
        if (_save.Hazelnuts >= u.Price)
        {
            Mutter(R.Pick("dream.ready", _save.OwnerName).Replace("{нещо}", thing), 6);
            _dreamReadySaid = true;
            return true;
        }
        if (_rng.Next(2) == 0) Mutter(R.Pick("dream.wish", _save.OwnerName).Replace("{нещо}", thing), 5);
        return true;
    }
}
