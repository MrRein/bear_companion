using System;
using System.Collections.Generic;
using System.Linq;
using Mecho.Core;

namespace Mecho;

// „Живият“ Мечо – връзката с ядрото (хроника и режисьор на мислите).
//
//   Think("memory.yesterday.work", 5, facts: ("часа", "4"));   // предлага мисъл
//   Remember("project.done", 8, ("проект", "Горски тайни"));   // запомня момент
//   OnThought("project.nudge", t => …);                        // свой показвач за ключ
//
// Ключ без реплики в repliki.txt и без свой показвач – мълчи (мисълта изобщо не чака).
// Всичко, което Мечо казва извън режисьора (Say), също се брои към приказливостта.
public sealed partial class PetWindow
{
    private readonly Dictionary<string, Action<Thought>> _thoughtPresenters = new();
    private bool _speakingThought;

    /// <summary>Свой начин да се покаже мисъл (въпрос с бутони, сцена…), вместо реплика.</summary>
    internal void OnThought(string key, Action<Thought> show, bool needsLines = false)
    {
        _thoughtPresenters[key] = show;
        if (needsLines) _thoughtNeedsLines.Add(key);
    }

    private readonly HashSet<string> _thoughtNeedsLines = new();

    /// <summary>Има ли какво да каже за ключа: свой показвач (и реплики, ако ги иска) или реплики.</summary>
    private bool CanSayThought(string key) =>
        _thoughtPresenters.ContainsKey(key) ? !_thoughtNeedsLines.Contains(key) || R.Has(key) : R.Has(key);

    /// <summary>Предлага мисъл на режисьора. false – няма какво да каже (празен раздел) или скоро е казано.</summary>
    public bool Think(string key, int importance, double cooldownHours = 0, params (string Name, string Value)[] facts)
    {
        if (!CoreEngine.Started) return false;
        if (!CanSayThought(key)) return false;
        return CoreEngine.Bus.Send(new Think(key, importance, Facts.Of(facts), cooldownHours)).Ok;
    }

    /// <summary>Запомня момент в хрониката.</summary>
    public void Remember(string kind, int importance = 3, params (string Name, string Value)[] facts)
    {
        if (CoreEngine.Started) CoreEngine.Bus.Send(new Remember(kind, Facts.Of(facts), importance));
    }

    /// <summary>Уточнява откъде е предмет („от сандъчето на „Горски тайни““).</summary>
    internal void SetOrigin(string itemId, string from)
    {
        if (CoreEngine.Started) CoreEngine.Bus.Send(new SetOrigin(itemId, from));
    }

    /// <summary>Хрониката (за менюто, спомените…).</summary>
    public JournalState Journal => CoreEngine.Store.Get<JournalState>();

    /// <summary>Тут обърна внимание (цъкна на него, отвори менюто…) – по-приказлив е.</summary>
    internal void Attention()
    {
        if (CoreEngine.Started) CoreEngine.Bus.Send(new UserAttention());
    }

    /// <summary>Вика се веднъж след старта на ядрото.</summary>
    private void SetupAlive()
    {
        CoreEngine.Bus.Subscribe<SpeakNow>(this, s => ShowThought(s.Thought));
        if (Mecho.Engine.CoreSetup.Talk is { } talk) talk.CanSay = key => key.StartsWith("dev.") || CanSayThought(key);
        if (Mecho.Engine.CoreSetup.Care is { } care) care.Context = CareContextNow;
        SetupCare();
        // Стари записи: първият ден заедно – най-старият записан ден.
        var first = _save.Days.Select(d => DateTime.TryParse(d.Date, out var x) ? x : DateTime.MaxValue).DefaultIfEmpty(DateTime.MaxValue).Min();
        if (first != DateTime.MaxValue) CoreEngine.Bus.Send(new SetFirstDay(first));
        // Напомнянето за проекта: с бутон към него (вместо проста реплика).
        OnThought("project.nudge", _ => ProjectNudge(force: true));
        OnThought("project.choose", _ => { if (!HasActiveProject) ChooseProject(); });
        SetupMoments();
        OnThought("dev.test", t => Say($"(мисъл с важност {t.Fact("важност")} – режисьорът реши да я кажа сега)", 5));
    }

    private void ShowThought(Thought t)
    {
        if (_thoughtPresenters.TryGetValue(t.Key, out var show))
        {
            show(t);
            return;
        }
        if (IsQuiet) return;
        string line = R.Text(t.Key, _save.OwnerName, t.Facts.Select(kv => (kv.Key, kv.Value)).ToArray());
        if (string.IsNullOrWhiteSpace(line)) return;
        _speakingThought = true;
        try { Say(line, 5); }
        finally { _speakingThought = false; }
    }

    /// <summary>Say извън режисьора: брои се към приказливостта (половин реплика).</summary>
    private void NoteSpokeAside()
    {
        if (!_speakingThought && CoreEngine.Started) CoreEngine.Bus.Send(new SpokeAside(0.5));
    }

    /// <summary>Описание за Developer: прагът и чакащите мисли.</summary>
    internal string TalkReport()
    {
        var talk = Mecho.Engine.CoreSetup.Talk;
        var s = CoreEngine.Store.Get<TalkState>();
        string pending = s.Pending.Count == 0 ? "няма" : string.Join(", ", s.Pending.Select(p => $"{p.Key} ({p.Importance})"));
        var j = Journal;
        return $"Праг: {talk?.Threshold:0.0} · чакат: {pending} · спомени: {j.Moments.Count} · дни заедно: {j.DaysTogether(DateTime.Today)}";
    }
}
