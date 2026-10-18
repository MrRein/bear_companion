using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Mecho.Core;

// ─────────────────────────────────────────────────────────────────────────────
// ВИДЪТ: какво е облечено, скиновете на мебелите, пижамата, менюто и фонът.
//
//     bus.Send(new Wear("hat", "beanie"));
//     bus.Send(new TakeOff("hat"));
//     bus.Send(new UseSkin("couch", "velvet"));
//     bus.Send(new SetLook(Pajama: "stars"));      // null – без промяна
//
// Кое е притежавано се проверява в приложението (каталогът е там); тук – само кое
// е избрано. Събитие „пижамен ден“ НЕ пипа облеклото: вдига Mods.WantedOutfit
// („pajama“), а изгледът решава как да покаже мечока (WardrobeModule.WantsPajamas).
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Outfit – слот → дреха; Skins – предмет → скин.</summary>
public sealed record LooksState(ImmutableSortedDictionary<string, string> Outfit, ImmutableSortedDictionary<string, string> Skins,
    string Pajama, bool PajamasAtNight, string UiTheme, string Background) : IStateSlice
{
    public static LooksState Default { get; } = new(ImmutableSortedDictionary<string, string>.Empty, ImmutableSortedDictionary<string, string>.Empty,
        "classic", true, "classic", "meadow");

    public string? Worn(string slot) => Outfit.TryGetValue(slot, out var id) ? id : null;

    public string? SkinOf(string item) => Skins.TryGetValue(item, out var id) ? id : null;

    public bool Equals(LooksState? o) => o is not null && Pajama == o.Pajama && PajamasAtNight == o.PajamasAtNight && UiTheme == o.UiTheme
        && Background == o.Background && Outfit.SequenceEqual(o.Outfit) && Skins.SequenceEqual(o.Skins);

    public override int GetHashCode() => HashCode.Combine(Pajama, UiTheme, Background, Outfit.Count, Skins.Count);
}

// ── Команди ──
public sealed record Wear(string Slot, string ItemId) : IGameCommand;
public sealed record TakeOff(string Slot) : IGameCommand;
public sealed record TakeOffAll : IGameCommand;
/// <summary>Цялото облекло наведнъж (Developer, стари записи).</summary>
public sealed record SetOutfit(IReadOnlyDictionary<string, string> Outfit) : IGameCommand;
/// <summary>Скин за предмет (null/"" – класическият).</summary>
public sealed record UseSkin(string Item, string? SkinId) : IGameCommand;
public sealed record SetSkins(IReadOnlyDictionary<string, string> Skins) : IGameCommand;
/// <summary>Останалото по вида (null – без промяна).</summary>
public sealed record SetLook(string? Pajama = null, bool? PajamasAtNight = null, string? UiTheme = null, string? Background = null) : IGameCommand;

// ── Сигнали ──
public sealed record OutfitChanged(string? Slot) : ISignal;     // null – много слотове наведнъж
public sealed record SkinChanged(string Item, string? SkinId) : ISignal;

/// <summary>Модулът „гардероб“ (Id: wardrobe).</summary>
public sealed class WardrobeModule : GameModule
{
    public const string ModuleId = "wardrobe";
    public const string Key = "wardrobe.looks";
    public const string PajamaTag = "pajama";

    public override string Id => ModuleId;

    /// <summary>Събитие иска пижама (пижамен ден) – каквото и да е облечено.</summary>
    public static bool WantsPajamas(ModifierStack mods) => mods.Get(Mods.WantedOutfit).Contains(PajamaTag);

    protected override void OnRegister()
    {
        Store.Register(this, Key, LooksState.Default);

        Handle<Wear>(c =>
        {
            if (string.IsNullOrWhiteSpace(c.Slot) || string.IsNullOrWhiteSpace(c.ItemId)) return CommandResult.Rejected("няма слот или дреха");
            var s = State<LooksState>();
            if (s.Worn(c.Slot) == c.ItemId) return CommandResult.Done;
            Store.Set(s with { Outfit = s.Outfit.SetItem(c.Slot, c.ItemId) });
            Emit(new OutfitChanged(c.Slot));
            return CommandResult.Done;
        });
        Handle<TakeOff>(c =>
        {
            var s = State<LooksState>();
            if (c.Slot == null || !s.Outfit.ContainsKey(c.Slot)) return CommandResult.Rejected("не е облечено");
            Store.Set(s with { Outfit = s.Outfit.Remove(c.Slot) });
            Emit(new OutfitChanged(c.Slot));
            return CommandResult.Done;
        });
        Handle<TakeOffAll>(_ =>
        {
            var s = State<LooksState>();
            if (s.Outfit.IsEmpty) return CommandResult.Rejected("нищо не е облечено");
            Store.Set(s with { Outfit = ImmutableSortedDictionary<string, string>.Empty });
            Emit(new OutfitChanged(null));
            return CommandResult.Done;
        });
        Handle<SetOutfit>(c =>
        {
            ArgumentNullException.ThrowIfNull(c.Outfit);
            Store.Update<LooksState>(s => s with { Outfit = Clean(c.Outfit) });
            Emit(new OutfitChanged(null));
            return CommandResult.Done;
        });
        Handle<UseSkin>(c =>
        {
            if (string.IsNullOrWhiteSpace(c.Item)) return CommandResult.Rejected("няма предмет");
            var s = State<LooksState>();
            var skins = string.IsNullOrWhiteSpace(c.SkinId) ? s.Skins.Remove(c.Item) : s.Skins.SetItem(c.Item, c.SkinId);
            Store.Set(s with { Skins = skins });
            Emit(new SkinChanged(c.Item, string.IsNullOrWhiteSpace(c.SkinId) ? null : c.SkinId));
            return CommandResult.Done;
        });
        Handle<SetSkins>(c =>
        {
            ArgumentNullException.ThrowIfNull(c.Skins);
            Store.Update<LooksState>(s => s with { Skins = Clean(c.Skins) });
            return CommandResult.Done;
        });
        Handle<SetLook>(c =>
        {
            Store.Update<LooksState>(s => s with
            {
                Pajama = string.IsNullOrWhiteSpace(c.Pajama) ? s.Pajama : c.Pajama,
                PajamasAtNight = c.PajamasAtNight ?? s.PajamasAtNight,
                UiTheme = string.IsNullOrWhiteSpace(c.UiTheme) ? s.UiTheme : c.UiTheme,
                Background = string.IsNullOrWhiteSpace(c.Background) ? s.Background : c.Background,
            });
            return CommandResult.Done;
        });
    }

    private static ImmutableSortedDictionary<string, string> Clean(IReadOnlyDictionary<string, string> d) =>
        d.Where(kv => !string.IsNullOrWhiteSpace(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value)).ToImmutableSortedDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
}
