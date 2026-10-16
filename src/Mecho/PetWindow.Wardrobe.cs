using System;
using System.Collections.Generic;
using System.Linq;

namespace Mecho;

// Гардеробът: дрехите се купуват с лешници и се обличат от таба „Гардероб“.
// Свободен ли е мечокът, гардеробът идва, щом се отвори табът. Прави ли нещо
// (кътче, работа, батут), гардеробът идва чак когато Тут смени дреха: спира за
// малко, отмества се, гардеробът се появява и той се преоблича зад вратата.
// Гардеробът стои, докато табът е отворен; затвори ли се, мечокът се връща на
// мястото си и продължава каквото е правил (със същото оставащо време).
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
    public System.Windows.Media.Imaging.BitmapSource GarmentPreview(Garment g) =>
        _dresser.Preview(_lib, g) ?? _lib.GetProp("garment_" + g.Id, 16, 16, 0).Image;

    private bool _wardrobeTab;              // табът „Гардероб“ е отворен
    private ParcelTrip? _closet;            // какво правеше, преди да отиде до гардероба
    private Dictionary<string, string>? _shownOutfit;   // старите дрехи, докато не влезе зад вратата

    /// <summary>Може да отиде до гардероба (буден, не яде, нищо не го чака).</summary>
    private bool CanUseWardrobe =>
        DailyOffer.HasWardrobe(_save) && !IsAsleep && _eating == null && !WaitingForFood && !CookFinale && _trip == null
        && !BusyEating(say: false) && _state is BearState.Idle or BearState.Walk or BearState.Couch or BearState.Bounce;

    /// <summary>Табът „Гардероб“ е отворен: ако е свободен, гардеробът идва веднага.</summary>
    public void OpenWardrobe()
    {
        _wardrobeTab = true;
        if (AtWardrobe || _closet != null || !CanUseWardrobe || InFocus) return;
        if (_inScene || _onTrampoline || _state != BearState.Idle) return;   // прави нещо: чака да смени дреха
        StartScene(Scenes.Wardrobe, quiet: true);
    }

    /// <summary>Табът е затворен: гардеробът изчезва и мечокът продължава каквото правеше.</summary>
    public void CloseWardrobe()
    {
        _wardrobeTab = false;
        _shownOutfit = null;
        if (_closet is { } t)
        {
            _closet = null;
            if (_dressLine != null) Say(_dressLine, 3);
            _dressUntil = _twirlUntil = 0;
            _dressLine = null;
            if (AtWardrobe)
            {
                _inScene = false;
                _scene = null;
                _sceneUntil = 0;
            }
            if (_state is BearState.Drag or BearState.Falling or BearState.Jump || IsAsleep) return;
            WalkTo(t.X, () => ResumeActivity(t));
            return;
        }
        if (!AtWardrobe) return;
        _dressUntil = _twirlUntil = 0;
        LeaveScene();
    }

    /// <summary>Смени дреха, докато прави нещо: спира, отмества се малко и гардеробът се появява.</summary>
    private void GoToWardrobe()
    {
        _closet ??= PauseActivity();
        var area = Area;
        int dir = _x > area.Left + area.Width / 2 ? -1 : 1;
        double spot = Math.Clamp(_closet.X + dir * 14 * PixelSize, area.Left + SpriteWidth, area.Right - SpriteWidth);
        WalkTo(spot, ArriveAtWardrobe);
    }

    private void ArriveAtWardrobe()
    {
        if (_closet == null) return;
        if (!_wardrobeTab)
        {
            var t = _closet;
            _closet = null;
            WalkTo(t.X, () => ResumeActivity(t));
            return;
        }
        _facingLeft = false;
        StartScene(Scenes.Wardrobe, quiet: true);
        if (_dressLine != null) BeginDressing(_dressLine);
    }

    /// <summary>Отиде към гардероба, но нещо го спря по пътя: пак тръгва (или се връща, ако табът е затворен).</summary>
    private void ContinueToWardrobe()
    {
        if (_closet == null || AtWardrobe) return;
        if (_wardrobeTab) GoToWardrobe();
        else ArriveAtWardrobe();
    }

    /// <summary>Зад вратата: шумоли, после излиза с новите дрехи и се върти.</summary>
    private void BeginDressing(string line)
    {
        _dressUntil = Now + DressTime;
        _twirlUntil = 0;
        _dressLine = line;
        _shownOutfit = null;      // вече е скрит: новите дрехи може да се облекат
        Say("*шъл-шъл* Секунда…", DressTime);
    }

    /// <summary>Преди смяната: помни как изглежда, за да не се преоблече пред очите ни по пътя.</summary>
    private void RememberLook()
    {
        if (!AtWardrobe && _wardrobeTab) _shownOutfit ??= new Dictionary<string, string>(_save.Outfit);
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
        // Купеното отива в гардероба; облича се оттам.
        Say(Lines.Pick(Lines.NewClothes, _save.OwnerName), 3);
        Persist();
        NotifyCare();
    }

    public void Wear(Garment g, string? line = null)
    {
        if (!Clothes.Owns(_save, g)) return;
        RememberLook();
        _save.Outfit[g.SlotId] = g.Id;
        TodayRec.Clothes++;
        Dress(line ?? Lines.Pick(g.SlotId == Clothes.Hat.Id ? Lines.PutOnHat : Lines.PutOn, _save.OwnerName));
    }

    public void TakeOff(Slot slot)
    {
        RememberLook();
        if (!_save.Outfit.Remove(slot.Id)) return;
        Dress(Lines.Pick(Lines.TookOff, _save.OwnerName));
    }

    /// <summary>Свали всичко.</summary>
    public void Undress()
    {
        if (_save.Outfit.Count == 0) return;
        RememberLook();
        _save.Outfit.Clear();
        Dress(Lines.Pick(Lines.TookOff, _save.OwnerName));
    }

    private void Dress(string line)
    {
        Persist();
        NotifyCare();
        if (AtWardrobe)
        {
            BeginDressing(line);
            return;
        }
        // Табът е отворен, а той прави нещо: първо отива до гардероба.
        if (_wardrobeTab && (_closet != null || CanUseWardrobe))
        {
            _dressLine = line;
            if (_closet == null) GoToWardrobe();
            return;
        }
        _shownOutfit = null;
        Say(line, 3);
        if (CanAnimateFreely && _state is BearState.Idle or BearState.Walk) Play("happy");
    }

    private void UpdateWardrobe(double now)
    {
        if (_dressLine != null && now >= _dressUntil)
        {
            Say(_dressLine, 3);
            Emote("sparkle");
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

// Фоновете на портрета: купуват се в магазина, сменят се с клик върху портрета.
public sealed partial class PetWindow
{
    public System.Windows.Media.Imaging.BitmapSource BackdropImage(Backdrop b) =>
        _lib.Background(b.Id) ?? _lib.Background(Backdrops.Default) ?? _lib.GetProp("bg_" + b.Id, 50, 50, 0).Image;

    public Backdrop CurrentBackdrop => Backdrops.Find(_save.Background);

    public void BuyBackdrop(Backdrop b)
    {
        if (Backdrops.Owns(_save, b) || _save.Hazelnuts < b.Price) return;
        _save.Hazelnuts -= b.Price;
        _save.Owned.Add("bg_" + b.Id);
        _save.Background = b.Id;
        Persist();
        NotifyCare();
    }

    // ── Скин на менюто и бутоните ──

    public void BuyUiTheme(UiTheme t)
    {
        if (UiThemes.Owns(_save, t) || _save.Hazelnuts < t.Price) return;
        _save.Hazelnuts -= t.Price;
        _save.Owned.Add(t.Key);
        // Отива в гардероба („Цвят на меню“); слага се оттам.
        Persist();
        NotifyCare();
    }

    public void UseUiTheme(UiTheme t)
    {
        if (!UiThemes.Owns(_save, t)) return;
        _save.UiTheme = t.Id;
        Ui.Apply(t);
        Persist();
        NotifyCare();
    }

    public void UseBackdrop(Backdrop b)
    {
        if (!Backdrops.Owns(_save, b)) return;
        _save.Background = b.Id;
        Persist();
        NotifyCare();
    }

    /// <summary>Следващият купен фон (клик върху портрета).</summary>
    public void NextBackdrop()
    {
        var owned = Backdrops.All.Where(b => Backdrops.Owns(_save, b)).ToList();
        int i = owned.FindIndex(b => b.Id == _save.Background);
        UseBackdrop(owned[(i + 1) % owned.Count]);
    }
}
