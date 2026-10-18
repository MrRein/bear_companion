using Mecho.Core;
using Xunit;

namespace Mecho.Core.Tests;

public class WardrobeTests
{
    private static (GameEngine E, List<ISignal> Signals) Started()
    {
        var (e, _, _) = Kit.Engine();
        e.Modules.Add(new WardrobeModule());
        var signals = new List<ISignal>();
        e.Bus.SubscribeAll(new object(), signals.Add);
        e.Start();
        return (e, signals);
    }

    [Fact]
    public void Wear_take_off_and_skins()
    {
        var (e, signals) = Started();
        e.Bus.Send(new Wear("hat", "beanie"));
        e.Bus.Send(new Wear("neck", "scarf_red"));
        Assert.Equal("beanie", e.Store.Get<LooksState>().Worn("hat"));
        Assert.Contains(new OutfitChanged("hat"), signals);
        Assert.True(e.Bus.Send(new TakeOff("hat")).Ok);
        Assert.False(e.Bus.Send(new TakeOff("hat")).Ok);
        e.Bus.Send(new TakeOffAll());
        Assert.Empty(e.Store.Get<LooksState>().Outfit);

        e.Bus.Send(new UseSkin("couch", "velvet"));
        Assert.Equal("velvet", e.Store.Get<LooksState>().SkinOf("couch"));
        e.Bus.Send(new UseSkin("couch", null));
        Assert.Null(e.Store.Get<LooksState>().SkinOf("couch"));
    }

    [Fact]
    public void Set_look_changes_only_given_fields()
    {
        var (e, _) = Started();
        e.Bus.Send(new SetLook(Pajama: "stars"));
        e.Bus.Send(new SetLook(PajamasAtNight: false, Background: "forest"));
        var l = e.Store.Get<LooksState>();
        Assert.Equal(("stars", false, "classic", "forest"), (l.Pajama, l.PajamasAtNight, l.UiTheme, l.Background));
    }

    [Fact]
    public void Pajama_day_is_a_modifier_not_an_outfit_change()
    {
        var (e, _) = Started();
        Assert.False(WardrobeModule.WantsPajamas(e.Modifiers));
        e.Modifiers.Add("event.pajama_day", Mods.WantedOutfit, WardrobeModule.PajamaTag);
        Assert.True(WardrobeModule.WantsPajamas(e.Modifiers));
        e.Modifiers.Clear("event.pajama_day");
        Assert.False(WardrobeModule.WantsPajamas(e.Modifiers));
    }

    [Fact]
    public void Looks_survive_save_and_load()
    {
        var (e, _) = Started();
        e.Bus.Send(new Wear("hat", "beanie"));
        e.Bus.Send(new UseSkin("bed", "royal"));
        var json = e.Store.Export();
        var (e2, _, _) = Kit.Engine();
        e2.Store.Import(json);
        e2.Modules.Add(new WardrobeModule());
        e2.Start();
        Assert.Equal(e.Store.Get<LooksState>(), e2.Store.Get<LooksState>());
    }
}
