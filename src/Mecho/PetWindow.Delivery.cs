using System;
using System.Collections.Generic;
using System.Linq;

namespace Mecho;

// Доставка: купиш ли нещо от магазина (уред, мебел, дреха, дамаска), след малко
// отдясно влизат куриерчета – малки жълти пиленца – с кашон над главите (малък,
// среден или голям; колкото по-голям, толкова повече са). На една трета от
// екрана го оставят и си тръгват, а мечокът отива и го отваря. Цъкнеш ли
// кашона – отваря се веднага. Нещото е твое още при купуването; доставката е
// за радост (и чак тогава мечокът го пробва).
public sealed partial class PetWindow
{
    private sealed record Delivery(string Icon, string Name, Action? OnOpen, int Size);

    private readonly Queue<Delivery> _deliveries = new();
    private CourierWindow? _courier;
    private CookerWindow? _parcel;
    private Delivery? _parcelItem;
    private double _nextDelivery, _parcelLast, _courierX, _courierDrop, _courierTime;
    private bool _courierHasBox, _parcelOpening;

    /// <summary>Колко голямо е: 0 – малко (1 куриерче), 1 – средно (2), 2 – голямо (3).</summary>
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
        if (_courier == null && _parcel == null) _nextDelivery = Math.Max(_nextDelivery, Now + 3 + _rng.NextDouble() * 3);
    }

    private void UpdateDelivery(double now)
    {
        double dt = Math.Clamp(now - _parcelLast, 0, 0.1);
        _parcelLast = now;
        if (_courier == null && _parcel == null)
        {
            if (_deliveries.Count == 0 || now < _nextDelivery || !IsVisible) return;
            StartCouriers(_deliveries.Dequeue());
            return;
        }
        _parcel?.Step(dt);
        _parcel?.SetAway(!IsVisible);
        if (_courier == null) return;

        var area = Area;
        _courierTime += dt;
        double speed = 75;
        if (_courierHasBox)
        {
            _courierX = Math.Max(_courierDrop, _courierX - speed * dt);
            if (_courierX <= _courierDrop + 0.5) DropBox();
        }
        else _courierX += speed * 1.3 * dt;
        bool step = (int)(_courierTime * 6) % 2 == 0;
        var box = _courierHasBox && _parcelItem is { } d ? _lib.GetProp(BoxArt(d.Size, false), BoxSize(d.Size).W, BoxSize(d.Size).H, 0).Image : null;
        string kind = _courierHasBox ? "courier" : "courier_free";
        _courier.Place(box, _lib.GetProp(kind, 12, 14, 0).Image, _lib.GetProp(kind + "_b", 12, 14, 0).Image,
            PixelSize, _courierX, area.Bottom, step, faceLeft: _courierHasBox);
        if (!IsVisible) _courier.Hide();
        if (!_courierHasBox && _courierX > area.Right + 80)
        {
            _courier.Close();
            _courier = null;
        }
    }

    private void StartCouriers(Delivery d)
    {
        var area = Area;
        _parcelItem = d;
        _parcelOpening = false;
        _courierHasBox = true;
        _courierTime = 0;
        _courierX = area.Right + 60;
        // Оставят го на една трета от екрана (откъм дясно), но не върху мечока.
        _courierDrop = area.Right - area.Width / 3;
        if (Math.Abs(_courierDrop - _x) < SpriteWidth) _courierDrop = _x + SpriteWidth * 1.3;
        _courierDrop = Math.Clamp(_courierDrop, area.Left + 60, area.Right - 60);
        _courier = new CourierWindow(d.Size + 1);
        if (!IsQuiet) Say(R.Pick("delivery.coming", _save.OwnerName), 3);
    }

    /// <summary>Стигнаха: оставят кашона на земята и си тръгват; мечокът отива да го отвори.</summary>
    private void DropBox()
    {
        if (_parcelItem is not { } d) return;
        _courierHasBox = false;
        var area = Area;
        var (w, h) = BoxSize(d.Size);
        _parcel = new CookerWindow("Доставка") { Hint = "Доставка! Цъкни, за да я отвориш." };
        _parcel.SetTime(null);
        _parcel.Clicked += OpenParcel;
        _parcel.SetAway(!IsVisible);
        _parcel.Place(_lib.GetProp(BoxArt(d.Size, false), w, h, 0).Image, PixelSize, _courierDrop, area.Bottom);
        if (CanAnimateFreely && !_inScene && !InFocus && _eating == null && _state is BearState.Idle or BearState.Walk)
        {
            double cx = _courierDrop;
            int side = _x < cx ? -1 : 1;
            WalkTo(cx + side * (SpriteWidth * 0.45 + w * PixelSize / 2), () =>
            {
                _facingLeft = side > 0;
                Play("happy", after: OpenParcel);
            });
        }
        else Later(6, OpenParcel);
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
