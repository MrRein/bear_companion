using System;
using System.Collections.Generic;
using System.Linq;

namespace Mecho;

// Доставка: купиш ли уред или мебел от магазина, след малко отгоре пада кашон
// (малък, среден или голям) до мечока. Той отива и го отваря; цъкнеш ли кашона –
// отваря се веднага. Нещото е твое още при купуването; доставката е за радост
// (и чак тогава мечокът го пробва). Дрехите и дамаските са без доставка.
public sealed partial class PetWindow
{
    private sealed record Delivery(string Icon, string Name, Action? OnOpen, int Size);

    private readonly Queue<Delivery> _deliveries = new();
    private CookerWindow? _parcel;
    private Delivery? _parcelItem;
    private double _nextDelivery, _parcelLast;
    private bool _parcelLanded, _parcelOpening;

    /// <summary>Колко голямо е: 0 – малко, 1 – средно, 2 – голямо (кашонът е такъв).</summary>
    private static int SizeOf(string upgradeId) => upgradeId switch
    {
        Kitchen.Oven or Kitchen.Grill or Kitchen.Couch or Kitchen.Bed or Kitchen.Wardrobe or Kitchen.Trampoline
            or Kitchen.Computer or Kitchen.Easel or Kitchen.Fridge or Kitchen.BoardGame => 2,
        Kitchen.PlantPot or Kitchen.Headphones or Kitchen.Cookbook or Kitchen.Kettle => 0,
        _ => 1,
    };

    private static string BoxArt(int size, bool open) => (size switch { 0 => "parcel_s", 2 => "parcel_l", _ => "parcel" }) + (open ? "_open" : "");

    private static (int W, int H) BoxSize(int size) => size switch { 0 => (12, 10), 2 => (28, 20), _ => (18, 16) };

    /// <summary>Поръчва доставка: идва след 3–6 с (една по една).</summary>
    private void Deliver(string icon, string name, Action? onOpen = null, int size = 1)
    {
        _deliveries.Enqueue(new Delivery(icon, name, onOpen, size));
        if (_parcel == null) _nextDelivery = Math.Max(_nextDelivery, Now + 3 + _rng.NextDouble() * 3);
    }

    private void UpdateDelivery(double now)
    {
        double dt = Math.Clamp(now - _parcelLast, 0, 0.1);
        _parcelLast = now;
        if (_parcel == null)
        {
            if (_deliveries.Count == 0 || now < _nextDelivery || !IsVisible) return;
            DropParcel(_deliveries.Dequeue());
            return;
        }
        if (_parcel.Step(dt) && !_parcelLanded)
        {
            _parcelLanded = true;
            // Кацна: мечокът отива до кашона и го отваря (ако може).
            if (CanAnimateFreely && !_inScene && !InFocus && _eating == null && _state is BearState.Idle or BearState.Walk)
            {
                double cx = _parcel.CenterX;
                int side = _x < cx ? -1 : 1;
                var (w, _) = BoxSize(_parcelItem?.Size ?? 1);
                WalkTo(cx + side * (SpriteWidth * 0.45 + w * PixelSize / 2), () =>
                {
                    _facingLeft = side > 0;
                    OpenParcel();
                });
            }
            else Later(4, OpenParcel);
        }
    }

    private void DropParcel(Delivery d)
    {
        var area = Area;
        _parcelItem = d;
        _parcelLanded = _parcelOpening = false;
        _parcelLast = Now;
        var (w, h) = BoxSize(d.Size);
        double x = Math.Clamp(_x + (_rng.Next(2) == 0 ? -1 : 1) * (SpriteWidth * 1.2 + _rng.Next(60)), area.Left + 40, area.Right - 40);
        _parcel = new CookerWindow("Доставка") { Hint = "Доставка! Цъкни, за да я отвориш." };
        _parcel.SetTime(null);
        _parcel.Clicked += OpenParcel;
        _parcel.SetAway(!IsVisible);
        _parcel.Place(_lib.GetProp(BoxArt(d.Size, false), w, h, 0).Image, PixelSize, x, area.Top + 40);
        _parcel.FallTo(x, area.Bottom);
        if (!IsQuiet) Say(R.Pick("delivery.coming", _save.OwnerName), 3);
    }

    private void OpenParcel()
    {
        if (_parcel == null || _parcelItem == null || _parcelOpening) return;
        _parcelOpening = true;
        var d = _parcelItem;
        var (w, h) = BoxSize(d.Size);
        _parcel.SetArt(_lib.GetProp(BoxArt(d.Size, true), w, h, 0).Image, PixelSize);
        Say($"{R.Pick("delivery.opened", _save.OwnerName)} {d.Icon} {d.Name}!", 5);
        if (CanAnimateFreely && !_inScene && !InFocus && _state != BearState.Busy) Play("happy");
        Later(1.5, () =>
        {
            _parcel?.Close();
            _parcel = null;
            _parcelItem = null;
            _nextDelivery = Now + 2;
            d.OnOpen?.Invoke();
        });
    }

    /// <summary>Купен уред идва със своята първа рецепта (каничката – с портокаловия сок от самото начало).</summary>
    private void LearnDefaultRecipe(string tool)
    {
        if (Kitchen.DefaultRecipe(tool) is not { } id) return;
        var food = Kitchen.Foods.FirstOrDefault(f => f.Id == id);
        if (food == null || Kitchen.HasRecipe(food, _save)) return;
        _save.Owned.Add(Kitchen.RecipeId(food));
        // Ако тъкмо събираше страници за нея, почва следващата.
        if (_save.NextRecipe == id)
        {
            _save.NextRecipe = null;
            _save.RecipePages = 0;
        }
    }

    /// <summary>При пускане: рецептите на вече купените уреди (за стари записи).</summary>
    private void LearnOwnedDefaultRecipes()
    {
        LearnDefaultRecipe(Kitchen.Jug);
        foreach (var u in Kitchen.Upgrades)
            if (Kitchen.Owns(_save, u.Id)) LearnDefaultRecipe(u.Id);
    }
}
