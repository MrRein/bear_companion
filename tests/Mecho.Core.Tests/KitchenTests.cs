using Mecho.Core;
using Xunit;

namespace Mecho.Core.Tests;

public class KitchenTests
{
    private static (GameEngine E, List<ISignal> Signals) Started()
    {
        var (e, _, _) = Kit.Engine();
        e.Modules.Add(new KitchenModule());
        var signals = new List<ISignal>();
        e.Bus.SubscribeAll(new object(), signals.Add);
        e.Start();
        return (e, signals);
    }

    [Fact]
    public void Cooking_lifecycle_with_two_stages()
    {
        var (e, signals) = Started();
        var t = new DateTime(2026, 10, 18, 12, 0, 0);
        Assert.True(e.Bus.Send(new StartCook("soup", t, 3, 0, 100)).Ok);
        Assert.Equal(new CookingState("soup", t, 3, 0, 100), e.Store.Get<CookingState>());

        Assert.True(e.Bus.Send(new NextCookStage(t.AddMinutes(5), 200)).Ok);
        Assert.False(e.Bus.Send(new NextCookStage(t.AddMinutes(9))).Ok);   // вече е в главния
        Assert.Equal(1, e.Store.Get<CookingState>().Stage);

        e.Bus.Send(new EndCook(true));
        var s = e.Store.Get<CookingState>();
        Assert.False(s.Cooking);
        Assert.Equal(200, s.X);                                              // мястото се помни
        Assert.Contains(new CookingEnded("soup", true), signals);
        Assert.Equal(2, signals.OfType<CookingStarted>().Count());
    }

    [Fact]
    public void Resuming_same_food_does_not_restart_and_bad_input_is_rejected()
    {
        var (e, signals) = Started();
        var t = DateTime.Now;
        e.Bus.Send(new StartCook("tea", t, 1));
        e.Bus.Send(new StartCook("tea", t.AddSeconds(5), 1));
        Assert.Single(signals.OfType<CookingStarted>());
        Assert.False(e.Bus.Send(new StartCook("", t, 1)).Ok);
        Assert.False(e.Bus.Send(new StartCook("x", t, -1)).Ok);
        Assert.False(e.Bus.Send(new StartCook("x", t, 1, 5)).Ok);

        e.Bus.Send(new StartCook("pie", t, 2));                              // друго ястие – старото е спряно
        Assert.Contains(new CookingEnded("tea", false), signals);
    }

    [Fact]
    public void Wishes_are_fulfilled_once_and_capped()
    {
        var (e, signals) = Started();
        e.Bus.Send(new SetWishes("2026-10-18", new[] { "soup", "soup", "", "pie" }));
        Assert.Equal(new[] { "soup", "pie" }, e.Store.Get<WishesState>().Foods);

        Assert.True(e.Bus.Send(new FulfillWish("soup")).Ok);
        Assert.False(e.Bus.Send(new FulfillWish("soup")).Ok);
        Assert.Contains(new WishFulfilled("soup"), signals);

        Assert.False(e.Bus.Send(new AddWish("pie")).Ok);
        for (int i = 0; i < 10; i++) e.Bus.Send(new AddWish("f" + i));
        Assert.Equal(KitchenModule.MaxWishes, e.Store.Get<WishesState>().Foods.Count);
    }

    [Fact]
    public void Kitchen_survives_save_and_load()
    {
        var (e, _) = Started();
        var t = new DateTime(2026, 10, 18, 12, 0, 0);
        e.Bus.Send(new StartCook("soup", t, 3, 0));
        e.Bus.Send(new SetWishes("d", new[] { "pie" }));
        var json = e.Store.Export();

        var (e2, _, _) = Kit.Engine();
        e2.Store.Import(json);
        e2.Modules.Add(new KitchenModule());
        e2.Start();
        Assert.Equal(e.Store.Get<CookingState>(), e2.Store.Get<CookingState>());
        Assert.Equal(e.Store.Get<WishesState>(), e2.Store.Get<WishesState>());
    }
}
