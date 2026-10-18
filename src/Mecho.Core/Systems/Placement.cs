using System;
using System.Collections.Immutable;

namespace Mecho.Core;

// ─────────────────────────────────────────────────────────────────────────────
// ПРЕДМЕТИ НА ЗЕМЯТА: печица, лампа, радио, хранилка, саксия (и всеки бъдещ).
// Правила, пазени ТУК (не в прозорците):
//  • от всеки вид най-много един (ключът е видът);
//  • слага се само притежаван предмет (InventoryState);
//  • сложеното от Тут (Owner = Player) стои, докато Тут не го махне;
//  • сложеното от мечока (Owner = Bear) се маха с ReleaseByBear, щом свърши заниманието;
//    ако Тут сложи вече сложеното от мечока – става нейно;
//  • продаден/отнет предмет се маха от земята сам (слуша ItemRevoked).
//
//     bus.Send(new PlaceItem("radio", PlacedBy.Bear, x));     // мечокът ще танцува
//     bus.Send(new SwitchItem("radio", On: true, ByBear: true));
//     bus.Send(new ReleaseByBear("radio"));                   // танцът свърши
//     var radio = store.Get<PlacementState>().Get("radio");   // Placed, Owner, On, X, Supply
//
// Нов предмет за слагане: нищо в ядрото – само id-то му в каталога (Kitchen) и изглед.
// ─────────────────────────────────────────────────────────────────────────────

public enum PlacedBy
{
    Player,
    Bear,
}

/// <summary>
/// Един предмет на земята. On – включен (радио свири, лампа свети); OnByBear – мечокът
/// го е включил (изключва го после); Supply – запас (семенца в хранилката);
/// OffDay – денят, в който Тут го е изгасила (лампата не светва пак същия ден).
/// </summary>
public sealed record Placement(bool Placed, PlacedBy Owner, bool On, bool OnByBear, double X, int Supply, string OffDay)
{
    public static Placement None { get; } = new(false, PlacedBy.Player, false, false, double.NaN, 0, "");
}

/// <summary>Всичко на земята, по вид.</summary>
public sealed record PlacementState(ImmutableDictionary<string, Placement> Items) : IStateSlice
{
    public static PlacementState Empty { get; } = new(ImmutableDictionary<string, Placement>.Empty);

    public Placement Get(string kind) => Items.TryGetValue(kind, out var p) ? p : Placement.None;

    public bool IsPlaced(string kind) => Get(kind).Placed;
}

// ── Команди ──
public sealed record PlaceItem(string Kind, PlacedBy By, double X = double.NaN) : IGameCommand;
public sealed record RemoveItem(string Kind) : IGameCommand;
/// <summary>Маха го, само ако го е сложил мечокът (и изключва каквото той е включил).</summary>
public sealed record ReleaseByBear(string Kind) : IGameCommand;
public sealed record SwitchItem(string Kind, bool On, bool ByBear = false) : IGameCommand;
public sealed record MoveItem(string Kind, double X) : IGameCommand;
public sealed record SetSupply(string Kind, int Supply) : IGameCommand;
public sealed record SetOffDay(string Kind, string Day) : IGameCommand;
/// <summary>Пряко записване (миграция от стари записи, Developer). Не проверява инвентара.</summary>
public sealed record SetPlacement(string Kind, Placement Value) : IGameCommand;

// ── Сигнали ──
public sealed record ItemPlaced(string Kind, PlacedBy By) : ISignal;
public sealed record ItemRemoved(string Kind) : ISignal;
public sealed record ItemSwitched(string Kind, bool On) : ISignal;
public sealed record ItemMoved(string Kind, double X) : ISignal;

/// <summary>Модулът „предмети на земята“. Зависи от „economy“ (кое е притежавано).</summary>
public sealed class PlacementModule : GameModule
{
    public const string ModuleId = "placement";
    public const string Key = "placement";

    public override string Id => ModuleId;

    public override System.Collections.Generic.IReadOnlyCollection<string> DependsOn { get; } = new[] { EconomyModule.ModuleId };

    protected override void OnRegister()
    {
        Store.Register(this, Key, PlacementState.Empty);

        Handle<PlaceItem>(c =>
        {
            if (!Valid(c.Kind)) return CommandResult.Rejected("празен вид");
            if (!Store.Get<InventoryState>().Has(c.Kind)) return CommandResult.Rejected("Нямаш такъв предмет.");
            var p = Get(c.Kind);
            if (p.Placed)
            {
                // Вече е сложено: от мечока → Тут го взима за себе си; иначе нищо.
                if (p.Owner == PlacedBy.Bear && c.By == PlacedBy.Player) Put(c.Kind, p with { Owner = PlacedBy.Player, OnByBear = false });
                return CommandResult.Done;
            }
            Put(c.Kind, p with { Placed = true, Owner = c.By, X = double.IsNaN(c.X) ? p.X : c.X });
            Emit(new ItemPlaced(c.Kind, c.By));
            return CommandResult.Done;
        });
        Handle<RemoveItem>(c =>
        {
            if (!Valid(c.Kind)) return CommandResult.Rejected("празен вид");
            Remove(c.Kind);
            return CommandResult.Done;
        });
        Handle<ReleaseByBear>(c =>
        {
            if (!Valid(c.Kind)) return CommandResult.Rejected("празен вид");
            var p = Get(c.Kind);
            if (p.Placed && p.Owner == PlacedBy.Bear) Remove(c.Kind);
            else if (p.OnByBear) Switch(c.Kind, false, byBear: false);
            return CommandResult.Done;
        });
        Handle<SwitchItem>(c =>
        {
            if (!Valid(c.Kind)) return CommandResult.Rejected("празен вид");
            Switch(c.Kind, c.On, c.ByBear);
            return CommandResult.Done;
        });
        Handle<MoveItem>(c =>
        {
            if (!Valid(c.Kind) || double.IsNaN(c.X) || double.IsInfinity(c.X)) return CommandResult.Rejected("невалидно място");
            var p = Get(c.Kind);
            if (p.X.Equals(c.X)) return CommandResult.Done;
            Put(c.Kind, p with { X = c.X });
            Emit(new ItemMoved(c.Kind, c.X));
            return CommandResult.Done;
        });
        Handle<SetSupply>(c =>
        {
            if (!Valid(c.Kind)) return CommandResult.Rejected("празен вид");
            Put(c.Kind, Get(c.Kind) with { Supply = Math.Max(0, c.Supply) });
            return CommandResult.Done;
        });
        Handle<SetOffDay>(c =>
        {
            if (!Valid(c.Kind)) return CommandResult.Rejected("празен вид");
            Put(c.Kind, Get(c.Kind) with { OffDay = c.Day ?? "" });
            return CommandResult.Done;
        });
        Handle<SetPlacement>(c =>
        {
            if (!Valid(c.Kind) || c.Value == null) return CommandResult.Rejected("празно");
            Put(c.Kind, c.Value);
            return CommandResult.Done;
        });

        // Продаден/отнет предмет не стои на земята.
        On<ItemRevoked>(r =>
        {
            if (!r.Clothes && Get(r.ItemId).Placed) Remove(r.ItemId);
        });
    }

    /// <summary>
    /// При пускане на програмата: каквото мечокът беше сложил/пуснал за занимание
    /// (заниманието вече го няма) се прибира/изключва.
    /// </summary>
    public override void Start()
    {
        foreach (var (kind, p) in State<PlacementState>().Items)
        {
            if (p.Placed && p.Owner == PlacedBy.Bear) Remove(kind);
            else if (p.OnByBear) Switch(kind, false, byBear: false);
        }
    }

    private static bool Valid(string? kind) => !string.IsNullOrWhiteSpace(kind);

    private Placement Get(string kind) => State<PlacementState>().Get(kind);

    private void Put(string kind, Placement p) => Store.Update<PlacementState>(s => s with { Items = s.Items.SetItem(kind, p) });

    private void Remove(string kind)
    {
        var p = Get(kind);
        if (!p.Placed && !p.On) return;
        Put(kind, p with { Placed = false, On = false, OnByBear = false, Owner = PlacedBy.Player });
        Emit(new ItemRemoved(kind));
    }

    private void Switch(string kind, bool on, bool byBear)
    {
        var p = Get(kind);
        if (p.On == on)
        {
            // Тут включи вече включеното от мечока – вече е „нейно“ включване.
            if (on && !byBear && p.OnByBear) Put(kind, p with { OnByBear = false });
            return;
        }
        Put(kind, p with { On = on, OnByBear = on && byBear });
        Emit(new ItemSwitched(kind, on));
    }
}
