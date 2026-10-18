using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Mecho.Core;

// ─────────────────────────────────────────────────────────────────────────────
// ИКОНОМИКА: лешниците (портфейл) и всичко притежавано (инвентар).
// Единственият, който ги мени, е EconomyModule – останалите пращат команди:
//
//     bus.Send(new EarnNuts(5, "задача"));
//     var r = bus.Send(new Purchase("radio", 40, Clothes: false, "магазин"));
//     if (!r.Ok) Say(r.Reason);                       // „Трябват още 12 лешника.“
//     bool has = store.Get<InventoryState>().Has("radio");
//
// Слуша се: HazelnutsChanged, ItemAcquired, ItemRevoked, StateChanged<WalletState>…
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Портфейлът: колко лешника има.</summary>
public sealed record WalletState(int Nuts) : IStateSlice;

/// <summary>
/// Притежаваното. Items – уреди, мебели, скинове, тапети, менюта, рецепти (id-та като
/// „radio“, „skin_bed_star“, „bg_forest“, „recipe_soup“); Clothes – дрехите и пижамите.
/// </summary>
public sealed record InventoryState(ImmutableSortedSet<string> Items, ImmutableSortedSet<string> Clothes) : IStateSlice
{
    public static InventoryState Empty { get; } = new(ImmutableSortedSet<string>.Empty, ImmutableSortedSet<string>.Empty);

    public bool Has(string id) => Items.Contains(id);

    public bool HasClothes(string id) => Clothes.Contains(id);
}

// ── Команди ──
public sealed record EarnNuts(int Amount, string Reason) : IGameCommand;
public sealed record SpendNuts(int Amount, string Reason) : IGameCommand;
/// <summary>Слага точна стойност (Developer, стари записи). Обикновеният код ползва Earn/Spend.</summary>
public sealed record SetNuts(int Value, string Reason) : IGameCommand;
public sealed record GrantItem(string ItemId, string Source, bool Clothes = false) : IGameCommand;
public sealed record RevokeItem(string ItemId, bool Clothes = false) : IGameCommand;
/// <summary>Заменя целия инвентар (Developer: отключи/заключи всичко, Dev Mode).</summary>
public sealed record SetInventory(IEnumerable<string> Items, IEnumerable<string> Clothes, string Reason) : IGameCommand;
/// <summary>Купува: проверява лешниците, взима ги и дава предмета – наведнъж (или нищо).</summary>
public sealed record Purchase(string ItemId, int Price, bool Clothes, string Source) : IGameCommand;

// ── Сигнали (HazelnutsChanged и ItemAcquired са в GameSignals.cs) ──
public sealed record ItemRevoked(string ItemId, bool Clothes) : ISignal;

/// <summary>Модулът „икономика“. Id: economy (други модули, които четат инвентара, зависят от него).</summary>
public sealed class EconomyModule : GameModule
{
    public const string ModuleId = "economy";
    public const string WalletKey = "economy.wallet";
    public const string InventoryKey = "economy.inventory";

    public override string Id => ModuleId;

    protected override void OnRegister()
    {
        Store.Register(this, WalletKey, new WalletState(0));
        Store.Register(this, InventoryKey, InventoryState.Empty);

        Handle<EarnNuts>(c =>
        {
            if (c.Amount <= 0) return CommandResult.Rejected("сумата трябва да е положителна");
            ChangeNuts(State<WalletState>().Nuts + c.Amount, c.Reason);
            return CommandResult.Done;
        });
        Handle<SpendNuts>(c =>
        {
            if (c.Amount <= 0) return CommandResult.Rejected("сумата трябва да е положителна");
            int have = State<WalletState>().Nuts;
            if (have < c.Amount) return CommandResult.Rejected($"Трябват още {c.Amount - have} лешника.");
            ChangeNuts(have - c.Amount, c.Reason);
            return CommandResult.Done;
        });
        Handle<SetNuts>(c =>
        {
            ChangeNuts(Math.Max(0, c.Value), c.Reason);
            return CommandResult.Done;
        });
        Handle<GrantItem>(c => Grant(c.ItemId, c.Source, c.Clothes) ? CommandResult.Done : CommandResult.Rejected("вече го има"));
        Handle<RevokeItem>(c =>
        {
            if (string.IsNullOrWhiteSpace(c.ItemId)) return CommandResult.Rejected("празен предмет");
            var inv = State<InventoryState>();
            var set = c.Clothes ? inv.Clothes : inv.Items;
            if (!set.Contains(c.ItemId)) return CommandResult.Rejected("няма го");
            Store.Set(c.Clothes ? inv with { Clothes = set.Remove(c.ItemId) } : inv with { Items = set.Remove(c.ItemId) });
            Emit(new ItemRevoked(c.ItemId, c.Clothes));
            return CommandResult.Done;
        });
        Handle<SetInventory>(c =>
        {
            var old = State<InventoryState>();
            var items = Clean(c.Items);
            var clothes = Clean(c.Clothes);
            Store.Set(new InventoryState(items, clothes));
            foreach (var id in items.Except(old.Items)) Emit(new ItemAcquired(id, c.Reason));
            foreach (var id in old.Items.Except(items)) Emit(new ItemRevoked(id, false));
            foreach (var id in clothes.Except(old.Clothes)) Emit(new ItemAcquired(id, c.Reason));
            foreach (var id in old.Clothes.Except(clothes)) Emit(new ItemRevoked(id, true));
            return CommandResult.Done;
        });
        Handle<Purchase>(c =>
        {
            if (string.IsNullOrWhiteSpace(c.ItemId)) return CommandResult.Rejected("празен предмет");
            if (c.Price < 0) return CommandResult.Rejected("отрицателна цена");
            var inv = State<InventoryState>();
            if (c.Clothes ? inv.HasClothes(c.ItemId) : inv.Has(c.ItemId)) return CommandResult.Rejected("Вече го имаш.");
            int have = State<WalletState>().Nuts;
            if (have < c.Price) return CommandResult.Rejected($"Трябват още {c.Price - have} лешника.");
            if (c.Price > 0) ChangeNuts(have - c.Price, "покупка: " + c.ItemId);
            Grant(c.ItemId, c.Source, c.Clothes);
            return CommandResult.Done;
        });
    }

    private static ImmutableSortedSet<string> Clean(IEnumerable<string>? ids) =>
        (ids ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).ToImmutableSortedSet(StringComparer.Ordinal);

    private void ChangeNuts(int value, string reason)
    {
        int old = State<WalletState>().Nuts;
        if (old == value) return;
        Store.Set(new WalletState(value));
        Emit(new HazelnutsChanged(old, value, reason));
    }

    private bool Grant(string id, string source, bool clothes)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        var inv = State<InventoryState>();
        var set = clothes ? inv.Clothes : inv.Items;
        if (set.Contains(id)) return false;
        Store.Set(clothes ? inv with { Clothes = set.Add(id) } : inv with { Items = set.Add(id) });
        Emit(new ItemAcquired(id, source));
        return true;
    }
}
