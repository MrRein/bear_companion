using System;
using System.Linq;
using Mecho.Core;

namespace Mecho;

/// <summary>
/// Събитията (ядрото: EventDirector) – какво прави мечокът, когато почне или свърши
/// събитие. Самото събитие (кога, колко, какво сменя) е данни в Mecho.Core.GameEvents;
/// тук са само репликите и картинките. Ново събитие без нужда от нова картинка:
/// запис в GameEvents.All и реплики „event.&lt;id&gt;.start/.end/.chatter“ в repliki.txt.
/// </summary>
public partial class PetWindow
{
    /// <summary>Лекарството в „Храна“ (виж Kitchen.Medicine): само докато е болен.</summary>
    public bool EventFoodAvailable(Food food) =>
        Kitchen.EventTag(food) is not { } tag || CoreEngine.Modifiers.Get(Mods.ShopExtras).Contains(tag);

    /// <summary>Пижамен ден: по пижама цял ден (каквото и да е облечено).</summary>
    private bool PajamaDay => WardrobeModule.WantsPajamas(CoreEngine.Modifiers);

    /// <summary>Болен е днес (докато не вземе лекарството).</summary>
    public bool IsSick => CoreEngine.Store.Get<EventsState>().IsActive(GameEvents.SickDay);

    /// <summary>Какво става днес (за менюто и Developer): „Пижамен ден“…</summary>
    public string[] ActiveEventNames()
    {
        var active = CoreEngine.Store.Get<EventsState>().Active.Keys;
        return active.Select(id => GameEvents.All.FirstOrDefault(d => d.Id == id)?.Name ?? id).ToArray();
    }

    private void WireEvents()
    {
        var bus = CoreEngine.Bus;
        bus.Subscribe<GameEventStarted>(this, s => EventStarted(s.EventId));
        bus.Subscribe<GameEventEnded>(this, s => EventEnded(s.EventId, s.Reason));
        // Събитие, почнало още при пускането (преди да слушаме): казва го след малко.
        foreach (var id in CoreEngine.Store.Get<EventsState>().Active.Keys.Where(id => id != GameEvents.Butterfly).ToList())
            if (R.Has($"event.{id}.start")) Later(12, () => { if (!IsAsleep) Say(R.Pick($"event.{id}.start", _save.OwnerName), 6); });
    }

    private void EventStarted(string id)
    {
        switch (id)
        {
            case GameEvents.Butterfly:
                // Мигът е само ако е свободен (не работи, не спи, не яде…).
                if (_state == BearState.Idle && CanAnimateFreely && !InFocus && !_inScene && _eating == null && !OnWalk) StartChase();
                return;
            case GameEvents.SickDay:
                Emote("sad", 4);
                break;
            case GameEvents.Christmas:
            case GameEvents.PajamaDay:
                Emote("sparkle", 3);
                break;
        }
        if (R.Has($"event.{id}.start") && !IsAsleep) Later(3, () => Say(R.Pick($"event.{id}.start", _save.OwnerName), 6));
        NotifyCare();
    }

    private void EventEnded(string id, string reason)
    {
        string key = id == GameEvents.SickDay && reason == "лекарство" ? "event.sick_day.cured" : $"event.{id}.end";
        if (reason != "свърши" || id != GameEvents.Butterfly)
            if (R.Has(key) && !IsAsleep) Say(R.Pick(key, _save.OwnerName), 5);
        if (id == GameEvents.SickDay && reason == "лекарство") Emote("heart", 3);
        NotifyCare();
    }

    /// <summary>Изпи лекарството: болният ден свършва.</summary>
    private void TookMedicine(Food food)
    {
        if (food.Id != Kitchen.Medicine) return;
        CoreEngine.Bus.Send(new EndGameEvent(GameEvents.SickDay, "лекарство"));
    }

    /// <summary>Реплика от събитието (понякога вместо обикновената), ако има такива.</summary>
    private string? EventChatter()
    {
        foreach (var pack in CoreEngine.Modifiers.Get(Mods.LinePacks))
            if (R.Has(pack + ".chatter") && _rng.Next(3) == 0) return R.Pick(pack + ".chatter", _save.OwnerName);
        return null;
    }

    /// <summary>Хоби, което събитие днес забранява (болен – без батут) или предпочита.</summary>
    private bool HobbyBlocked(Scene s) => CoreEngine.Modifiers.Get(Mods.BlockedHobbies).Contains(s.Id);

    private bool HobbyPreferred(Scene s) => CoreEngine.Modifiers.Get(Mods.PreferredHobbies).Contains(s.Id);
}
