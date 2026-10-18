using System;
using System.Windows;

namespace Mecho;

/// <summary>
/// Предмет, до който трябва да е мечокът, за да прави нещо с него (бърка в
/// тенджерата, поли растението, танцува до радиото, топли се до печицата).
/// Target – къде е предметът сега (null = няма го вече); Gap – колко встрани от
/// ръба му застава мечокът (средата на мечока).
/// </summary>
internal sealed record ItemAnchor(string Kind, Func<Rect?> Target, double Gap);

// Занимания, вързани за предмет. Правилото е едно за всички:
//  • анимацията за предмета (поливане, бъркане, танц…) върви само докато мечокът е
//    до предмета;
//  • вдигнат ли е – виси с „drag“ (не полива във въздуха); пуснат далече – отива
//    обратно до предмета и там продължава;
//  • преместен ли е предметът (Тут влачи печицата/радиото/уреда) – мечокът отива при него;
//  • изчезне ли предметът – заниманието свършва.
// Кътчетата (растение, радио, печица, сяда до уреда) пазят котвата в _sceneNear;
// кратките неща (бъркане, пълнене на хранилката) – в _busyNear (свършват, ако се отдалечи).
public sealed partial class PetWindow
{
    private ItemAnchor? _sceneNear;
    private ItemAnchor? _busyNear;
    private bool _anchorWalk;
    private double _tickDt = 0.033;

    // ── Котвите на предметите ──

    /// <summary>
    /// Къде е предмет на земята: прозорецът му, а ако още не е направен (току-що
    /// сложен) – запомненото място. null – предметът го няма.
    /// </summary>
    private Rect? ItemBounds(CookerWindow? w, bool placed, double savedX, double artW, double artH)
    {
        if (!placed) return null;
        if (w != null) return w.Bounds;
        if (double.IsNaN(savedX)) return null;
        double ww = artW * PixelSize, hh = artH * PixelSize;
        return new Rect(savedX - ww / 2, Area.Bottom - hh, ww, hh);
    }

    private ItemAnchor RadioAnchor => new("radio", () => ItemBounds(_radioWindow, RadioPlaced, _save.RadioX, 14, 12), SpriteWidth * 0.45);

    private ItemAnchor HeaterAnchor => new("heater", () => ItemBounds(_heaterWindow, _save.HeaterOn && Kitchen.Owns(_save, Kitchen.Heater), _save.HeaterX, 20, 22), SpriteWidth * 0.4);

    private ItemAnchor CookerAnchor => new("cooker", () => _cook?.Window.Bounds, SpriteWidth * 0.35);

    private ItemAnchor FeederAnchor => new("feeder", () => ItemBounds(_feederWindow, FeederPlaced, _save.FeederX, 16, 28), SpriteWidth * 0.35);

    /// <summary>Саксията (на мястото си до ръба или там, където Тут я е сложила).</summary>
    private ItemAnchor PlantAnchor => new("plant", () =>
    {
        double w = PlantW * PixelSize;
        return new Rect(PlantSpot - w / 2, _y - PlantH * PixelSize, w, PlantH * PixelSize);
    }, PlantGap - PlantW * PixelSize / 2);

    /// <summary>Котвата на кътчето, ако то е за предмет (иначе null – кътчето е „в него“).</summary>
    private ItemAnchor? AnchorFor(Scene scene) =>
        scene == Scenes.Music && Kitchen.Owns(_save, Kitchen.Radio) ? RadioAnchor
        : scene == Scenes.Plant ? PlantAnchor
        : null;

    /// <summary>Къде застава до предмета: от страната, от която е (саксията до ръба – само откъм екрана).</summary>
    private (double Spot, bool FaceLeft) AnchorSpot(ItemAnchor a, Rect r)
    {
        if (a.Kind == "plant") return (CareSpot, _plantSide < 0);
        double cx = r.Left + r.Width / 2;
        int side = _x < cx ? -1 : 1;
        var area = AreaAt(cx, r.Bottom - 1);
        double spot = cx + side * (r.Width / 2 + a.Gap);
        if (spot - HalfWidth < area.Left || spot + HalfWidth > area.Right) spot = cx - side * (r.Width / 2 + a.Gap);
        return (spot, spot > cx);
    }

    /// <summary>
    /// До предмета ли е: средата на мечока е най-много на Gap + половин мечок от ръба
    /// на предмета (от която и да е страна) – колкото да го стигне с лапа.
    /// </summary>
    private bool NearAnchor(ItemAnchor a)
    {
        if (a.Target() is not { } r) return false;
        double fromEdge = Math.Max(0, Math.Abs(_x - (r.Left + r.Width / 2)) - r.Width / 2);
        return fromEdge <= Math.Max(0, a.Gap) + SpriteWidth * 0.45;
    }

    /// <summary>
    /// Кътче за предмет: далече ли е – върви към него (без анимацията за предмета) и
    /// връща true; стигнал е – продължава заниманието. Предметът го няма – свършва.
    /// </summary>
    private bool FollowSceneAnchor(Scene scene, double dt)
    {
        if (_sceneNear is not { } a) return false;
        if (scene == Scenes.Plant && _gardenPhase < 2) return false;   // още изнася саксията
        if (a.Target() is not { } r)
        {
            _anchorWalk = false;
            LeaveScene();
            return true;
        }
        var (spot, faceLeft) = AnchorSpot(a, r);
        // Достатъчно близо е – прави си нещото; иначе отива точно до мястото.
        if (!_anchorWalk && (a.Kind == "plant" ? Math.Abs(_x - spot) <= SpriteWidth * 0.35 : NearAnchor(a))) return false;
        if (Math.Abs(_x - spot) > PixelSize)
        {
            if (!_anchorWalk)
            {
                _anchorWalk = true;
                _animTime = 0;
            }
            double dir = Math.Sign(spot - _x);
            _facingLeft = dir < 0;
            _x += dir * Math.Min(Math.Abs(spot - _x), (IsExhausted ? WalkSpeed * 0.6 : WalkSpeed) * dt);
            string walk = IsExhausted ? "walk_tired" : "walk";
            if (_anim != walk)
            {
                _anim = walk;
                _animTime = 0;
            }
            return true;
        }
        if (_anchorWalk)
        {
            // Стигна: продължава с предмета.
            _anchorWalk = false;
            _facingLeft = faceLeft;
            if (scene == Scenes.Plant) NextCare(Now);
            else
            {
                _anim = SceneAnim(scene);
                _animTime = 0;
            }
        }
        return false;
    }

    /// <summary>Кратко нещо с предмет (бърка, пълни хранилката): отдалечи ли се – спира.</summary>
    private bool BusyLostAnchor() => _busyNear is { } a && !NearAnchor(a);

    /// <summary>Като Play, но докато е до предмета (иначе спира и прави нещо друго).</summary>
    private void PlayNear(ItemAnchor anchor, string anim, double length = 0, Action? after = null)
    {
        Play(anim, length: length, after: after);
        _busyNear = anchor;
    }
}
