using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Mecho;

// Леглото (истинско, когато е купено), скиновете на мебелите, пижамата вечер и
// размерът на мечока на екрана.
public sealed partial class PetWindow
{
    private readonly Image _bed = new() { IsHitTestVisible = false, Visibility = Visibility.Collapsed };
    private Prop? _bedProp;

    // ── Леглото ──

    /// <summary>Купено легло: заспи ли (не на дивана), ляга в него.</summary>
    private bool BedOn => _state == BearState.Sleep && !_inScene && Kitchen.Owns(_save, Kitchen.Bed);

    private double BedLift => BedOn ? BedProp.Seat * PixelSize : 0;

    private Prop BedProp => _bedProp ??= _lib.GetProp(Furniture.Current(_save, Kitchen.Bed).Prop, 46, 16, 7);

    /// <summary>Къде е леглото (центрирано под мечока, на земята), или празно.</summary>
    private Rect BedRect(double xs, double ys)
    {
        bool on = BedOn;
        _bed.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        if (!on) return Rect.Empty;
        var img = BedProp.Image;
        if (!ReferenceEquals(_bed.Source, img))
        {
            _bed.Source = img;
            RenderOptions.SetBitmapScalingMode(_bed, BitmapScalingMode.NearestNeighbor);
        }
        _bed.Width = img.PixelWidth * PixelSize;
        _bed.Height = img.PixelHeight * PixelSize;
        return new Rect(xs - _bed.Width / 2, ys - _bed.Height, _bed.Width, _bed.Height);
    }

    // ── Скинове ──

    public void BuySkin(Skin skin)
    {
        if (Furniture.Owns(_save, skin) || _save.Hazelnuts < skin.Price) return;
        _save.Hazelnuts -= skin.Price;
        _save.Owned.Add($"skin_{skin.Item}_{skin.Id}");
        // Новата дамаска идва с доставка; слага я, като отвори кашончето.
        Deliver("🎁", skin.Name, () => UseSkin(skin), size: 1);
        Persist();
        NotifyCare();
    }

    public void UseSkin(Skin skin)
    {
        if (!Furniture.Owns(_save, skin)) return;
        if (skin.Item == Kitchen.Bed) _save.BedSkin = skin.Id;
        else _save.CouchSkin = skin.Id;
        _bedProp = null;
        // Седи на дивана: сменя се веднага.
        if (_inScene && _scene != null && _scene.Seat == "couch") LoadSceneArt(_scene);
        Persist();
        NotifyCare();
    }

    public System.Windows.Media.Imaging.BitmapSource SkinImage(Skin skin) =>
        _lib.GetProp(skin.Prop, skin.Item == Kitchen.Bed ? 46 : 48, skin.Item == Kitchen.Bed ? 16 : 24, 0).Image;

    // ── Пижамата ──

    private bool _wasPajamas;

    /// <summary>Вечер (22:00–7:00) или заспал след 20:00: облича пижамата.</summary>
    public bool PajamaTime
    {
        get
        {
            if (!_save.PajamasAtNight) return false;
            int h = DateTime.Now.Hour;
            return h >= 22 || h < 7 || (IsAsleep && (h >= 20 || h < 9));
        }
    }

    /// <summary>Каквото носи сега: облеклото от гардероба, а вечер пижамата отгоре.</summary>
    private IReadOnlyDictionary<string, string> EffectiveOutfit
    {
        get
        {
            if (!PajamaTime) return WithCup(_save.Outfit);
            var night = new Dictionary<string, string>(_save.Outfit);
            night.Remove(Clothes.Glasses.Id);
            night.Remove(Clothes.Neck.Id);
            night.Remove(Clothes.Gloves.Id);
            foreach (var (slot, id) in Clothes.Pajamas) night[slot] = id;
            return WithCup(night);
        }
    }

    /// <summary>Казва нещо, когато облече или съблече пижамата.</summary>
    private void UpdatePajamas()
    {
        bool now = PajamaTime;
        if (now == _wasPajamas) return;
        _wasPajamas = now;
        if (!IsAsleep) Mutter(now ? "*облича пижамата* Време е за сън скоро…" : "*съблича пижамата* Добро утро!", 4);
        NotifyCare();
    }

    public void SetPajamas(bool on)
    {
        _save.PajamasAtNight = on;
        Persist();
        NotifyCare();
    }

    // ── Размер ──

    /// <summary>Колко DIP е пиксел от рисунката (от „Още“ → „Размер“, иначе от anim.json).</summary>
    public int BearScale => _save.BearScale is >= 1 and <= 8 ? _save.BearScale : _lib.Scale;

    public void SetBearScale(int scale)
    {
        _save.BearScale = Math.Clamp(scale, 1, 8);
        _bedProp = null;
        ApplySpriteSize();
        ClampX();
        UpdateLayoutAndWindow();
        Persist();
    }
}
