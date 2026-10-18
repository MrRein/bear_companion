using Mecho.Core;
using Xunit;

namespace Mecho.Core.Tests;

public class EconomyAndPlacementTests
{
    private static (GameEngine E, List<ISignal> Signals) Started()
    {
        var (e, _, _) = Kit.Engine();
        e.Modules.Add(new PlacementModule());   // редът на добавяне няма значение – зависимостите се подреждат
        e.Modules.Add(new EconomyModule());
        var signals = new List<ISignal>();
        e.Bus.SubscribeAll(new object(), signals.Add);
        e.Start();
        return (e, signals);
    }

    [Fact]
    public void Earn_and_spend_change_wallet_and_signal()
    {
        var (e, signals) = Started();
        Assert.True(e.Bus.Send(new EarnNuts(10, "задача")).Ok);
        var spend = e.Bus.Send(new SpendNuts(15, "тест"));
        Assert.False(spend.Ok);
        Assert.Contains("5", spend.Reason);
        Assert.True(e.Bus.Send(new SpendNuts(4, "тест")).Ok);
        Assert.Equal(6, e.Store.Get<WalletState>().Nuts);
        Assert.Equal(new[] { (0, 10), (10, 6) }, signals.OfType<HazelnutsChanged>().Select(h => (h.Old, h.New)));
        Assert.False(e.Bus.Send(new EarnNuts(-1, "x")).Ok);
    }

    [Fact]
    public void Purchase_is_all_or_nothing()
    {
        var (e, signals) = Started();
        e.Bus.Send(new SetNuts(30, "тест"));
        Assert.False(e.Bus.Send(new Purchase("radio", 40, false, "магазин")).Ok);
        Assert.False(e.Store.Get<InventoryState>().Has("radio"));
        Assert.Equal(30, e.Store.Get<WalletState>().Nuts);
        Assert.True(e.Bus.Send(new Purchase("radio", 20, false, "магазин")).Ok);
        Assert.True(e.Store.Get<InventoryState>().Has("radio"));
        Assert.Equal(10, e.Store.Get<WalletState>().Nuts);
        Assert.False(e.Bus.Send(new Purchase("radio", 0, false, "магазин")).Ok);   // вече го има
        Assert.Single(signals.OfType<ItemAcquired>());
    }

    [Fact]
    public void Inventory_grant_revoke_and_bulk_set()
    {
        var (e, signals) = Started();
        e.Bus.Send(new GrantItem("scarf", "подарък", Clothes: true));
        e.Bus.Send(new GrantItem("lamp", "dev"));
        Assert.True(e.Store.Get<InventoryState>().HasClothes("scarf"));
        Assert.True(e.Bus.Send(new RevokeItem("lamp")).Ok);
        Assert.False(e.Bus.Send(new RevokeItem("lamp")).Ok);
        e.Bus.Send(new SetInventory(new[] { "a", "b", " " }, Array.Empty<string>(), "dev"));
        var inv = e.Store.Get<InventoryState>();
        Assert.Equal(new[] { "a", "b" }, inv.Items);
        Assert.Empty(inv.Clothes);
        Assert.Contains(signals.OfType<ItemRevoked>(), r => r.ItemId == "scarf" && r.Clothes);
    }

    [Fact]
    public void Only_owned_items_can_be_placed_one_per_kind()
    {
        var (e, signals) = Started();
        Assert.False(e.Bus.Send(new PlaceItem("radio", PlacedBy.Player, 100)).Ok);
        e.Bus.Send(new GrantItem("radio", "dev"));
        Assert.True(e.Bus.Send(new PlaceItem("radio", PlacedBy.Player, 100)).Ok);
        Assert.True(e.Bus.Send(new PlaceItem("radio", PlacedBy.Player, 500)).Ok);   // второ слагане – нищо ново
        var radio = e.Store.Get<PlacementState>().Get("radio");
        Assert.True(radio.Placed);
        Assert.Equal(100, radio.X);
        Assert.Single(signals.OfType<ItemPlaced>());
    }

    [Fact]
    public void Bear_placed_item_is_released_owner_item_stays()
    {
        var (e, _) = Started();
        e.Bus.Send(new GrantItem("radio", "dev"));
        e.Bus.Send(new PlaceItem("radio", PlacedBy.Bear, 50));
        e.Bus.Send(new SwitchItem("radio", true, ByBear: true));
        e.Bus.Send(new ReleaseByBear("radio"));
        Assert.False(e.Store.Get<PlacementState>().IsPlaced("radio"));

        e.Bus.Send(new PlaceItem("radio", PlacedBy.Player, 50));
        e.Bus.Send(new SwitchItem("radio", true, ByBear: true));       // мечокът го пуска за танц
        e.Bus.Send(new ReleaseByBear("radio"));                        // танцът свърши
        var radio = e.Store.Get<PlacementState>().Get("radio");
        Assert.True(radio.Placed);                                     // радиото на Тут остава…
        Assert.False(radio.On);                                        // …и пак е изключено
    }

    [Fact]
    public void Player_claims_item_the_bear_placed()
    {
        var (e, _) = Started();
        e.Bus.Send(new GrantItem("plant", "dev"));
        e.Bus.Send(new PlaceItem("plant", PlacedBy.Bear));
        e.Bus.Send(new PlaceItem("plant", PlacedBy.Player));
        e.Bus.Send(new ReleaseByBear("plant"));
        Assert.True(e.Store.Get<PlacementState>().IsPlaced("plant"));
    }

    [Fact]
    public void Revoked_item_leaves_the_floor()
    {
        var (e, signals) = Started();
        e.Bus.Send(new GrantItem("heater", "dev"));
        e.Bus.Send(new PlaceItem("heater", PlacedBy.Player));
        e.Bus.Send(new RevokeItem("heater"));
        Assert.False(e.Store.Get<PlacementState>().IsPlaced("heater"));
        Assert.Contains(signals.OfType<ItemRemoved>(), r => r.Kind == "heater");
    }

    [Fact]
    public void Placement_without_economy_is_skipped_not_crashing()
    {
        var (e, _, _) = Kit.Engine();
        e.Modules.Add(new PlacementModule());
        e.Start();
        Assert.Equal(ModuleStatus.Skipped, e.Modules.StatusOf(PlacementModule.ModuleId));
        Assert.False(e.Bus.Send(new PlaceItem("radio", PlacedBy.Player)).Ok);   // няма обработчик → отказ, не срив
    }

    [Fact]
    public void State_roundtrips_through_save()
    {
        var (a, _) = Started();
        a.Bus.Send(new SetNuts(77, "x"));
        a.Bus.Send(new GrantItem("radio", "x"));
        a.Bus.Send(new PlaceItem("radio", PlacedBy.Player, 123));
        var json = a.Store.Export().ToJsonString();

        var (b, _, _) = Kit.Engine();
        b.Store.Import(System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject());
        b.Modules.Add(new EconomyModule());
        b.Modules.Add(new PlacementModule());
        b.Start();
        Assert.Equal(77, b.Store.Get<WalletState>().Nuts);
        Assert.Equal(123, b.Store.Get<PlacementState>().Get("radio").X);
        Assert.True(b.Store.WasLoaded(EconomyModule.WalletKey));
    }

    [Fact]
    public void Bear_items_do_not_survive_a_restart()
    {
        var (a, _) = Started();
        a.Bus.Send(new GrantItem("radio", "x"));
        a.Bus.Send(new GrantItem("lamp", "x"));
        a.Bus.Send(new PlaceItem("radio", PlacedBy.Bear, 10));
        a.Bus.Send(new PlaceItem("lamp", PlacedBy.Player, 20));
        a.Bus.Send(new SwitchItem("lamp", true, ByBear: true));
        var json = a.Store.Export().ToJsonString();

        var (b, _, _) = Kit.Engine();
        b.Store.Import(System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject());
        b.Modules.Add(new EconomyModule());
        b.Modules.Add(new PlacementModule());
        b.Start();
        var st = b.Store.Get<PlacementState>();
        Assert.False(st.IsPlaced("radio"));          // мечокът го беше сложил – прибрано
        Assert.True(st.IsPlaced("lamp"));            // на Тут – стои…
        Assert.False(st.Get("lamp").On);             // …но включеното от мечока е изключено
    }
}
