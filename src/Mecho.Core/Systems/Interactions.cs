using System;
using System.Collections.Immutable;

namespace Mecho.Core;

// ─────────────────────────────────────────────────────────────────────────────
// ВЗАИМОДЕЙСТВИЯ С ПРЕДМЕТИТЕ: мечокът забелязва сложеното и понякога прави нещо с
// него (пуска печицата, казва, че му е топло, пълни хранилката…).
//
// Тук са само правилата „колко често“, за да е интересен, а не досаден:
//   • между две взаимодействия – поне MinGap (каквото и да е);
//   • с един и същ предмет – поне ItemGap;
//   • едно и също действие (напр. „обичам печицата“) – поне SameActionGap;
//   • най-много MaxPerDay на ден.
// Важните (студено е, а печицата е изключена) минават с Urgent: само MinUrgentGap.
// Какво точно прави мечокът е в приложението (src/Mecho/Items/*.cs – по файл за предмет).
//
//     if (bus.Send(new TryInteract("heater", "turn_on")).Ok) …   // може – прави го
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Кога за последно с кой предмет и кое действие; колко за днес.</summary>
public sealed record InteractionsState(DateTime LastAny, ImmutableSortedDictionary<string, DateTime> LastByItem,
    ImmutableSortedDictionary<string, DateTime> LastByAction, string Day, int Today) : IStateSlice
{
    public static InteractionsState Empty { get; } = new(DateTime.MinValue, ImmutableSortedDictionary<string, DateTime>.Empty,
        ImmutableSortedDictionary<string, DateTime>.Empty, "", 0);
}

/// <summary>Може ли сега това действие с този предмет. Ok – записано е, прави го.</summary>
public sealed record TryInteract(string Item, string Action, bool Urgent = false) : IGameCommand;

public sealed record ItemInteracted(string Item, string Action) : ISignal;

/// <summary>Модулът „взаимодействия“ (Id: interactions).</summary>
public sealed class InteractionsModule : GameModule
{
    public const string ModuleId = "interactions";
    public const string Key = "interactions";

    public static readonly TimeSpan MinGap = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan MinUrgentGap = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan ItemGap = TimeSpan.FromMinutes(40);
    public static readonly TimeSpan SameActionGap = TimeSpan.FromHours(3);
    public const int MaxPerDay = 12;

    public override string Id => ModuleId;

    protected override void OnRegister()
    {
        Store.Register(this, Key, InteractionsState.Empty);
        Handle<TryInteract>(c =>
        {
            if (string.IsNullOrWhiteSpace(c.Item) || string.IsNullOrWhiteSpace(c.Action)) return CommandResult.Rejected("няма предмет/действие");
            var s = State<InteractionsState>();
            var now = Clock.Now;
            string today = Clock.DayKey();
            int count = s.Day == today ? s.Today : 0;
            string action = c.Item + "." + c.Action;
            if (c.Urgent)
            {
                // Важното (студено, а печицата е изключена) – само да не е току-що.
                if (now - s.LastAny < MinUrgentGap) return CommandResult.Rejected("току-що");
            }
            else
            {
                if (count >= MaxPerDay) return CommandResult.Rejected("стига за днес");
                if (now - s.LastAny < MinGap) return CommandResult.Rejected("скоро беше друго");
                if (s.LastByItem.TryGetValue(c.Item, out var item) && now - item < ItemGap) return CommandResult.Rejected("скоро беше със същото");
                if (s.LastByAction.TryGetValue(action, out var same) && now - same < SameActionGap) return CommandResult.Rejected("скоро го каза");
            }
            Store.Set(new InteractionsState(now, s.LastByItem.SetItem(c.Item, now), s.LastByAction.SetItem(action, now), today, count + 1));
            Emit(new ItemInteracted(c.Item, c.Action));
            return CommandResult.Done;
        });
    }
}
