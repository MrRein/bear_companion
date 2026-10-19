using System;
using System.Collections.Generic;
using System.Linq;

namespace Mecho;

/// <summary>
/// КАТАЛОГЪТ НА ПОВЕДЕНИЕТО: какво изисква всяко занимание и как изглежда.
/// Едно място за правилата „анимация ↔ действие ↔ наличен предмет“, за да не се
/// разминават (мечокът не чете на диван, който го няма; не стои прав върху столче;
/// не прави йога без постелка…). Всички, които избират или рисуват занимание, питат тук:
///
///   Behaviour.Available(scene, save)  – има ли всичко нужно (купено/сложено);
///   Behaviour.PostureOf(anim)         – седнал, прав или на земята (на постелка);
///   Behaviour.SeatFor(seat, posture)  – дали седалката се рисува при тази поза.
///
/// Ново занимание: Scene в Scenes (Unlock = предметът, който го отключва; Needs = още
/// задължителни мебели) и при нужда поза за анимацията му тук. Самопроверката минава
/// през всички кътчета и проверява, че каталогът и рисуваното съвпадат.
/// </summary>
public static class Behaviour
{
    public enum Posture
    {
        /// <summary>Седнал: на диван/столче, ако ги има; иначе на земята.</summary>
        Sit,
        /// <summary>Прав: никаква седалка под него.</summary>
        Stand,
        /// <summary>На земята върху нещо (постелка): само „подови“ седалки.</summary>
        Floor,
    }

    /// <summary>Позите на анимациите. Каквото не е тук – седнал.</summary>
    private static readonly Dictionary<string, Posture> Poses = new(StringComparer.Ordinal)
    {
        ["idle"] = Posture.Stand, ["write"] = Posture.Stand, ["doing"] = Posture.Stand,
        ["dance"] = Posture.Stand, ["walk"] = Posture.Stand, ["happy"] = Posture.Stand, ["think"] = Posture.Stand,
        ["juggle"] = Posture.Stand, ["bounce"] = Posture.Stand, ["hold"] = Posture.Stand, ["hold_walk"] = Posture.Stand,
        ["yoga"] = Posture.Floor,
    };

    /// <summary>
    /// ИЗКЛЮЧЕНИЯ „зад мечока“: обикновено всичко сложено е пред мечока (той е зад всичко),
    /// а тези предмети са ЗАД него – минава пред дивана и пред статива. Друг предмет се
    /// флагва, като се добави тук (видът от Kitchen) – прозорецът му сам застава зад мечока.
    /// </summary>
    public static readonly HashSet<string> BehindBear = new(StringComparer.Ordinal) { Kitchen.Couch, Kitchen.Easel };

    public static bool IsBehindBear(string? kind) => kind != null && BehindBear.Contains(kind);

    /// <summary>Седалки на пода (рисуват се и под прав/легнал мечок).</summary>
    private static readonly HashSet<string> FloorSeats = new(StringComparer.Ordinal) { "mat" };

    /// <summary>Позата на анимацията („write_tired“ е като „write“).</summary>
    public static Posture PostureOf(string? anim)
    {
        if (string.IsNullOrEmpty(anim)) return Posture.Sit;
        if (Poses.TryGetValue(anim, out var p)) return p;
        int tired = anim.IndexOf("_tired", StringComparison.Ordinal);
        return tired > 0 && Poses.TryGetValue(anim[..tired], out p) ? p : Posture.Sit;
    }

    public static bool IsFloorSeat(string? seat) => seat != null && FloorSeats.Contains(seat);

    /// <summary>Рисува ли се седалката при тази поза: подовата – винаги, стол/диван – само седнал.</summary>
    public static string? SeatFor(string? seat, Posture posture) =>
        seat == null ? null : IsFloorSeat(seat) ? seat : posture == Posture.Sit ? seat : null;

    /// <summary>Всички предмети, без които заниманието не става (отключващият + задължителните мебели).</summary>
    public static IEnumerable<string> Needs(Scene scene) =>
        (scene.Unlock == null ? Enumerable.Empty<string>() : new[] { scene.Unlock }).Concat(scene.Needs);

    /// <summary>Има ли всичко нужно за заниманието.</summary>
    public static bool Available(Scene scene, SaveData save) => Needs(scene).All(id => Kitchen.Owns(save, id));

    /// <summary>Първото липсващо (за „Нямам …“), или null.</summary>
    public static string? Missing(Scene scene, SaveData save) => Needs(scene).FirstOrDefault(id => !Kitchen.Owns(save, id));
}
