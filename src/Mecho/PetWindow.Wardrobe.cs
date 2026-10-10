using System;

namespace Mecho;

// Гардеробът: дрехите се купуват с лешници и се обличат от таба „Гардероб“.
// Докато табът е отворен, гардеробът стои до мечока; когато смени дреха,
// мечокът се скрива зад вратата, гардеробът се клати и той излиза облечен.
public sealed partial class PetWindow
{
    private const double DressTime = 0.9;

    private double _dressUntil;     // докато е зад вратата
    private double _twirlUntil;     // докато се показва с новата дреха
    private string? _dressLine;

    private bool AtWardrobe => _inScene && _scene == Scenes.Wardrobe;

    private bool Dressing => AtWardrobe && Now < _dressUntil;

    private bool WearsHat => Clothes.Worn(_save, Clothes.Hat) != null;

    /// <summary>Гардеробът се клати, докато мечокът се преоблича вътре.</summary>
    private double WardrobeShake => Dressing ? Math.Round(Math.Sin(Now * 45)) * PixelSize : 0;

    /// <summary>Мечокът с това облекло (за плочките в гардероба).</summary>
    public System.Windows.Media.Imaging.BitmapSource DressedPreview(System.Collections.Generic.IReadOnlyDictionary<string, string> outfit) =>
        Dresser.Preview(_lib, outfit);

    /// <summary>Табът „Гардероб“ е отворен: гардеробът идва до мечока.</summary>
    public void OpenWardrobe()
    {
        if (AtWardrobe || IsAsleep || InFocus || _onTrampoline) return;
        if (_state is BearState.Drag or BearState.Falling or BearState.Jump or BearState.Bounce) return;
        if (_inScene) LeaveScene();
        StartScene(Scenes.Wardrobe, quiet: false);
    }

    /// <summary>Табът е затворен: гардеробът изчезва (ако Тут го е оставила в кътче, се връща там).</summary>
    public void CloseWardrobe()
    {
        if (!AtWardrobe) return;
        _dressUntil = _twirlUntil = 0;
        Say(Lines.Pick(Scenes.Wardrobe.End, _save.OwnerName), 3);
        LeaveScene();
    }

    public void BuyGarment(Garment g)
    {
        if (Clothes.Owns(_save, g)) return;
        if (_save.Hazelnuts < g.Price)
        {
            Say($"Трябват ми още {g.Price - _save.Hazelnuts} 🌰 за {g.Name.ToLowerInvariant()}.", 3);
            return;
        }
        _save.Hazelnuts -= g.Price;
        _save.OwnedClothes.Add(g.Id);
        Wear(g, Lines.Pick(Lines.NewClothes, _save.OwnerName));
    }

    public void Wear(Garment g, string? line = null)
    {
        if (!Clothes.Owns(_save, g)) return;
        _save.Outfit[g.SlotId] = g.Id;
        Dress(line ?? Lines.Pick(g.SlotId == Clothes.Hat.Id ? Lines.PutOnHat : Lines.PutOn, _save.OwnerName));
    }

    public void TakeOff(Slot slot)
    {
        if (!_save.Outfit.Remove(slot.Id)) return;
        Dress(Lines.Pick(Lines.TookOff, _save.OwnerName));
    }

    /// <summary>Свали всичко.</summary>
    public void Undress()
    {
        if (_save.Outfit.Count == 0) return;
        _save.Outfit.Clear();
        Dress(Lines.Pick(Lines.TookOff, _save.OwnerName));
    }

    private void Dress(string line)
    {
        Persist();
        NotifyCare();
        if (AtWardrobe)
        {
            // Зад вратата: шумоли, после излиза и се върти.
            _dressUntil = Now + DressTime;
            _twirlUntil = 0;
            _dressLine = line;
            Say("*шъл-шъл* Секунда…", DressTime);
            return;
        }
        Say(line, 3);
        if (CanAnimateFreely && _state is BearState.Idle or BearState.Walk) Play("happy");
    }

    private void UpdateWardrobe(double now)
    {
        if (_dressLine != null && now >= _dressUntil)
        {
            Say(_dressLine, 3);
            _dressLine = null;
            _anim = "dance";
            _animTime = 0;
            _twirlUntil = now + 1.4;
        }
        else if (_twirlUntil > 0 && now > _twirlUntil)
        {
            _twirlUntil = 0;
            _anim = Scenes.Wardrobe.Anim;
            _animTime = 0;
        }
    }
}
