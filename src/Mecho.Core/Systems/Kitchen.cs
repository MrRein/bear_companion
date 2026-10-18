using System;
using System.Collections.Immutable;
using System.Linq;

namespace Mecho.Core;

// ─────────────────────────────────────────────────────────────────────────────
// КУХНЯТА: какво се готви сега и какво му се иска.
//
// Ядрото пази САМО състоянието (какво, докога, в кой уред, къде е уредът; списъкът
// с желания за деня). Каталогът с ястия и анимациите са в приложението (Kitchen.cs,
// PetWindow.Cooking.cs) – модулът не знае нищо за картинки.
//
//     bus.Send(new StartCook("soup", readyAt, cost: 3, stage: 0, x: 420));
//     bus.Send(new NextCookStage(newReadyAt, x));      // рецепта в два уреда
//     bus.Send(new EndCook(Completed: true));           // извади го (или Completed: false – спря)
//     bus.Send(new SetWishes("2026-10-18", new[] { "soup", "pancakes" }));
//     if (bus.Send(new FulfillWish("soup")).Ok) …      // беше желано – махнато
//     bus.Publish(new FoodEaten("soup", Whole: true));  // за всеки, който се интересува
//
// Сигнали: CookingStarted, CookingEnded, WishAdded, WishFulfilled, FoodEaten.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Какво се готви. FoodId == null – нищо. Stage: 0 – първият уред, 1 – главният.</summary>
public sealed record CookingState(string? FoodId, DateTime ReadyAt, int Cost, int Stage, double X) : IStateSlice
{
    public static CookingState None { get; } = new(null, DateTime.MinValue, 0, 1, double.NaN);

    public bool Cooking => FoodId != null;
}

/// <summary>Какво му се иска днес (id-та на ястия, по ред). Day – за кой ден е списъкът.</summary>
public sealed record WishesState(string Day, ImmutableList<string> Foods) : IStateSlice
{
    public static WishesState Empty { get; } = new("", ImmutableList<string>.Empty);

    public bool Wants(string foodId) => Foods.Contains(foodId);

    // Записите са сравними по съдържание (иначе всяко Set би било „промяна“).
    public bool Equals(WishesState? other) => other is not null && Day == other.Day && Foods.SequenceEqual(other.Foods);

    public override int GetHashCode() => HashCode.Combine(Day, Foods.Count);
}

// ── Команди ──
public sealed record StartCook(string FoodId, DateTime ReadyAt, int Cost, int Stage = 1, double X = double.NaN) : IGameCommand;
/// <summary>Първият уред свърши – сега главният (нов срок, може и ново място).</summary>
public sealed record NextCookStage(DateTime ReadyAt, double X = double.NaN) : IGameCommand;
public sealed record MoveCooker(double X) : IGameCommand;
/// <summary>Готвенето чака толкова (мечокът беше вдигнат – нищо не тече скрито).</summary>
public sealed record DelayCook(TimeSpan By) : IGameCommand;
/// <summary>Край на готвенето. Completed – извадено готово; иначе спряно.</summary>
public sealed record EndCook(bool Completed) : IGameCommand;
public sealed record SetWishes(string Day, System.Collections.Generic.IReadOnlyList<string> Foods) : IGameCommand;
public sealed record AddWish(string FoodId) : IGameCommand;
/// <summary>Маха желанието. Rejected – не беше желано (нищо не се сменя).</summary>
public sealed record FulfillWish(string FoodId) : IGameCommand;

// ── Сигнали ──
public sealed record CookingStarted(string FoodId, DateTime ReadyAt, int Stage) : ISignal;
public sealed record CookingEnded(string FoodId, bool Completed) : ISignal;
public sealed record WishAdded(string FoodId) : ISignal;
public sealed record WishFulfilled(string FoodId) : ISignal;
/// <summary>Изяде (или изпи) нещо. Whole – до последната хапка. Пуска го приложението.</summary>
public sealed record FoodEaten(string FoodId, bool Whole) : ISignal;

/// <summary>Модулът „кухня“ (Id: kitchen).</summary>
public sealed class KitchenModule : GameModule
{
    public const string ModuleId = "kitchen";
    public const string CookingKey = "kitchen.cooking";
    public const string WishesKey = "kitchen.wishes";

    /// <summary>Най-много толкова желания наведнъж.</summary>
    public const int MaxWishes = 6;

    public override string Id => ModuleId;

    protected override void OnRegister()
    {
        Store.Register(this, CookingKey, CookingState.None);
        Store.Register(this, WishesKey, WishesState.Empty);

        Handle<StartCook>(c =>
        {
            if (string.IsNullOrWhiteSpace(c.FoodId)) return CommandResult.Rejected("няма ястие");
            if (c.Cost < 0) return CommandResult.Rejected("отрицателна цена");
            if (c.Stage is < 0 or > 1) return CommandResult.Rejected("стъпката е 0 или 1");
            var old = State<CookingState>();
            // Същото ястие пак (продължава след рестарт) – само обновява; друго – старото е спряно.
            if (old.Cooking && old.FoodId != c.FoodId) Emit(new CookingEnded(old.FoodId!, false));
            Store.Set(new CookingState(c.FoodId, c.ReadyAt, c.Cost, c.Stage, c.X));
            if (old.FoodId != c.FoodId || old.Stage != c.Stage) Emit(new CookingStarted(c.FoodId, c.ReadyAt, c.Stage));
            return CommandResult.Done;
        });
        Handle<NextCookStage>(c =>
        {
            var s = State<CookingState>();
            if (!s.Cooking) return CommandResult.Rejected("нищо не се готви");
            if (s.Stage >= 1) return CommandResult.Rejected("вече е в главния уред");
            Store.Set(s with { Stage = 1, ReadyAt = c.ReadyAt, X = double.IsNaN(c.X) ? s.X : c.X });
            Emit(new CookingStarted(s.FoodId!, c.ReadyAt, 1));
            return CommandResult.Done;
        });
        Handle<DelayCook>(c =>
        {
            var s = State<CookingState>();
            if (!s.Cooking) return CommandResult.Rejected("нищо не се готви");
            if (c.By <= TimeSpan.Zero) return CommandResult.Done;
            Store.Set(s with { ReadyAt = s.ReadyAt + c.By });
            return CommandResult.Done;
        });
        Handle<MoveCooker>(c =>
        {
            Store.Update<CookingState>(s => s with { X = c.X });
            return CommandResult.Done;
        });
        Handle<EndCook>(c =>
        {
            var s = State<CookingState>();
            if (!s.Cooking) return CommandResult.Done;    // вече няма нищо – не е грешка
            // Мястото на уреда се помни (следващият път е на същото място).
            Store.Set(CookingState.None with { X = s.X });
            Emit(new CookingEnded(s.FoodId!, c.Completed));
            return CommandResult.Done;
        });

        Handle<SetWishes>(c =>
        {
            ArgumentNullException.ThrowIfNull(c.Foods);
            var foods = c.Foods.Where(f => !string.IsNullOrWhiteSpace(f)).Distinct(StringComparer.Ordinal).Take(MaxWishes).ToImmutableList();
            Store.Set(new WishesState(c.Day ?? "", foods));
            return CommandResult.Done;
        });
        Handle<AddWish>(c =>
        {
            var w = State<WishesState>();
            if (string.IsNullOrWhiteSpace(c.FoodId)) return CommandResult.Rejected("няма ястие");
            if (w.Wants(c.FoodId)) return CommandResult.Rejected("вече го иска");
            if (w.Foods.Count >= MaxWishes) return CommandResult.Rejected("стигат му желания");
            Store.Set(w with { Foods = w.Foods.Add(c.FoodId) });
            Emit(new WishAdded(c.FoodId));
            return CommandResult.Done;
        });
        Handle<FulfillWish>(c =>
        {
            var w = State<WishesState>();
            if (!w.Wants(c.FoodId)) return CommandResult.Rejected("не беше желано");
            Store.Set(w with { Foods = w.Foods.Remove(c.FoodId) });
            Emit(new WishFulfilled(c.FoodId));
            return CommandResult.Done;
        });
    }
}
