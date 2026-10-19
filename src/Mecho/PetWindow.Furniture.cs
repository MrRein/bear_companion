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

    /// <summary>
    /// Спи в леглото: то е сложено на земята и мечокът е върху него (отиде до него или
    /// сам го сложи, преди да легне – GoToBed). Тогава леглото се рисува под него.
    /// </summary>
    private bool BedOn => _state == BearState.Sleep && !_inScene && FurniturePlaced(Kitchen.Bed) && Math.Abs(_x - BedX) <= SpriteWidth * 0.3;

    private double BedX => FurnishingOf(Kitchen.Bed)?.Window?.CenterX ?? PlacementOf(Kitchen.Bed).X;

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
        UseSkin(skin);
    }

    public void UseSkin(Skin skin)
    {
        if (!Furniture.Owns(_save, skin)) return;
        _save.Skins[skin.Item] = skin.Id;
        if (skin.Item == Kitchen.Bed) _save.BedSkin = skin.Id;
        if (skin.Item == Kitchen.Couch) _save.CouchSkin = skin.Id;
        _bedProp = null;
        _nextHeaterFrame = 0;                       // печицата – веднага с новата рисунка
        _lampWindow?.Close();                       // лампата се показва наново
        _lampWindow = null;
        // Седи на дивана: сменя се веднага.
        if (_inScene && _scene != null && _scene.Seat == "couch") LoadSceneArt(_scene);
        Persist();
        NotifyCare();
    }

    /// <summary>Картинката на скина (за плочката в магазина и гардероба).</summary>
    public System.Windows.Media.Imaging.BitmapSource SkinImage(Skin skin) => skin.Item switch
    {
        Kitchen.Bed => _lib.GetProp(skin.Prop, 46, 16, 0).Image,
        Kitchen.Couch => _lib.GetProp(skin.Prop, 48, 24, 0).Image,
        Kitchen.Heater => _lib.GetProp(skin.Prop, 20, 22, 0).Image,
        Kitchen.Lamp => _lib.GetProp(skin.Prop, 14, 30, 0).Image,
        Kitchen.Guitar => _lib.Clothes("tool_" + skin.Prop) ?? _lib.GetProp("shop_guitar", 16, 16, 0).Image,
        _ => _lib.GetProp(skin.Prop, 22, 24, 0).Image,
    };

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
            // На път към гардероба: още със старите дрехи (сменя ги зад вратата).
            IReadOnlyDictionary<string, string> outfit = (IReadOnlyDictionary<string, string>?)_shownOutfit ?? _save.Outfit;
            if (!PajamaTime && !PajamaDay) return WithCup(outfit);
            var night = new Dictionary<string, string>(outfit);
            night.Remove(Clothes.Glasses.Id);
            night.Remove(Clothes.Neck.Id);
            night.Remove(Clothes.Gloves.Id);
            foreach (var (slot, id) in Clothes.PajamaOutfit(Clothes.CurrentPajama(_save))) night[slot] = id;
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

    public void BuyPajama(PajamaSet p)
    {
        if (Clothes.Owns(_save, p) || _save.Hazelnuts < p.Price) return;
        _save.Hazelnuts -= p.Price;
        _save.OwnedClothes.Add(p.Key);
        UsePajama(p);
        Say($"Нова пижама! {p.Name}. Чакам вечерта!", 4);
    }

    public void UsePajama(PajamaSet p)
    {
        if (!Clothes.Owns(_save, p)) return;
        _save.Pajama = p.Id;
        Persist();
        NotifyCare();
    }

    /// <summary>Мечокът с пижамата (за плочката в гардероба и магазина).</summary>
    public System.Windows.Media.Imaging.BitmapSource PajamaPreview(PajamaSet p)
    {
        // Пази се отделно: иначе всяка пижама би изтрила от кеша облеклото, което носи сега.
        if (_pajamaPreviews.TryGetValue(p.Id, out var img)) return img;
        return _pajamaPreviews[p.Id] = new Dresser().Frame(_lib, "idle", 0, Clothes.PajamaOutfit(p));
    }

    private readonly Dictionary<string, System.Windows.Media.Imaging.BitmapSource> _pajamaPreviews = new();

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
        // Мащабът е за всичко нарисувано: мебели, уреди, радио, лампа, растение, хранилка, храна, птичка.
        foreach (Window w in Application.Current.Windows)
        {
            if (w is CookerWindow c) c.Rescale(PixelSize);
            else if (w is FoodWindow f) f.Rescale(PixelSize);
        }
        _bird.Width = _bird.Height = BirdArt * PixelSize;
        ClampX();
        UpdateLayoutAndWindow();
        Persist();
    }
}
