using System;
using System.Collections.Generic;
using System.Linq;

namespace Mecho;

// Доставка: купиш ли уред или мебел от магазина, след малко отгоре пада кашон
// (малък, среден или голям) до мечока. Той спира каквото прави, отива до кашона,
// отваря го и се връща на мястото си да продължи (кътче, работа, батут). Цъкнеш
// ли кашона – отваря се веднага. Нещото е твое още при купуването; доставката е
// за радост. Дрехите и дамаските са без доставка.
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
        if (_parcel.Step(dt)) _parcelLanded = true;
        // Кацна: щом може, мечокът спира каквото прави и отива до кашона.
        if (_parcelLanded && !_parcelOpening && _trip == null && CanGoToParcel) StartParcelTrip();
    }

    /// <summary>Какво правеше, преди да тръгне към кашона (за да продължи после).</summary>
    private sealed record ParcelTrip(double At, double X, bool FacingLeft, Scene? Scene, double SceneLeft, bool Auto, bool StayPut);

    private ParcelTrip? _trip;

    private bool CanGoToParcel =>
        !IsAsleep && _eating == null && !WaitingForFood && !CookFinale
        && _state is BearState.Idle or BearState.Walk or BearState.Couch or BearState.Bounce;

    private void StartParcelTrip()
    {
        if (_parcel == null) return;
        var scene = _onTrampoline ? Scenes.Trampoline : _inScene ? _scene : null;
        double left = _onTrampoline ? (_bounceUntil > 0 ? Math.Max(1, _bounceUntil - Now) : 0) : _sceneUntil > 0 ? Math.Max(1, _sceneUntil - Now) : 0;
        _trip = new ParcelTrip(Now, _x, _facingLeft, scene, left, _autoBlock, _save.StayPut);
        // Спира временно: без LeaveScene, за да не почне нов свободен ритъм.
        if (_onTrampoline) EndBounce(null);
        if (_inScene)
        {
            _cup = null;
            _inScene = false;
            _scene = null;
            _sceneUntil = 0;
            _autoBlock = false;
        }
        GoToParcel();
    }

    private void GoToParcel()
    {
        if (_parcel == null) return;
        double cx = _parcel.CenterX;
        int side = _x < cx ? -1 : 1;
        var (w, _) = BoxSize(_parcelItem?.Size ?? 1);
        WalkTo(cx + side * (SpriteWidth * 0.45 + w * PixelSize / 2), () =>
        {
            _facingLeft = side > 0;
            OpenParcel();
        });
    }

    /// <summary>
    /// Пътят до кашона се е прекъснал (стои, а още не е свършил): продължава –
    /// до кашона, ако още е там, иначе обратно на мястото си.
    /// </summary>
    private void ContinueParcelTrip()
    {
        if (_parcel == null) ReturnFromParcel();
        else if (!_parcelOpening) GoToParcel();
    }

    /// <summary>Отвори кашона: връща се на мястото си и продължава каквото правеше.</summary>
    private void ReturnFromParcel()
    {
        if (_trip is not { } t) return;
        // Спал, вдигнат, или пък тъкмо пробва новото нещо: не се връща.
        if (IsAsleep || _inScene || _onTrampoline || _state is BearState.Drag or BearState.Falling or BearState.Jump)
        {
            _trip = null;
            return;
        }
        WalkTo(t.X, () =>
        {
            _trip = null;
            _facingLeft = t.FacingLeft;
            _save.StayPut = t.StayPut;
            if (t.Scene == null || (t.Scene == Scenes.Focus && !InFocus)) return;
            if (t.Scene == Scenes.Trampoline)
            {
                StartTrampoline(t.SceneLeft, quiet: true);
                return;
            }
            StartScene(t.Scene, quiet: true);
            if (t.SceneLeft > 0) _sceneUntil = Now + t.SceneLeft;
            _autoBlock = t.Auto;
        });
    }

    /// <summary>Самопроверка: маха чакащите доставки и кашона.</summary>
    private void ClearDeliveries()
    {
        _deliveries.Clear();
        _parcel?.Close();
        _parcel = null;
        _parcelItem = null;
        _trip = null;
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
        if (CanAnimateFreely && !_inScene && _state != BearState.Busy) Play("happy");
        Later(1.5, () =>
        {
            _parcel?.Close();
            _parcel = null;
            _parcelItem = null;
            _nextDelivery = Now + 2;
            d.OnOpen?.Invoke();
            ReturnFromParcel();
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
