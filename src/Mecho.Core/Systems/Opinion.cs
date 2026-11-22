using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Mecho.Core;

// ─────────────────────────────────────────────────────────────────────────────
// МНЕНИЕТО В МОМЕНТА: какво мисли мечокът за действие (сготви пица, облечи пуловер…).
//
//     var o = opinion.Evaluate(new OpinionInput("cook", "pizza", tags, now, fullness: 60, energy: 70,
//                                               wished: false, doneToday: false, doneYesterday: true));
//     o.Stance  → Grumbles;  o.Reason.Key → "opinion.ate.yesterday"
//     bus.Send(new OpinionShown(o.Stance));   // след като е показано (броят се отказите)
//
// Причините („за“ и „против“) са малки класове IOpinionFactor – нов вид причина е нов
// клас, добавен с opinion.Add(...). Всяка причина носи ключ за реплика и точки.
// Сбор ≥ Loves → обожава; ≥ Likes → иска; > Grumble → нищо особено; > Refuse → мрънка;
// иначе отказва – но най-много веднъж на ден, не два пъти подред и никога, ако е много
// гладен (тогава само мрънка).
// ─────────────────────────────────────────────────────────────────────────────

public enum Stance { Loves, Likes, Neutral, Grumbles, Refuses }

/// <summary>Какво се прави и в каква обстановка. Tags – „heavy“, „complex“, „warm“, „light“…</summary>
public sealed record OpinionInput(
    string Verb, string ObjectId, IReadOnlySet<string> Tags, DateTime Now,
    double Fullness, double Energy, bool Wished, bool DoneToday, bool DoneYesterday,
    double? Temperature = null, string ObjectName = "");

/// <summary>Една причина: ключът на репликата, точките (+ за, − против) и полетата за репликата.</summary>
public sealed record OpinionReason(string Key, double Score, ImmutableDictionary<string, string> Facts);

public sealed record OpinionResult(Stance Stance, OpinionReason? Reason, double Score, IReadOnlyList<OpinionReason> All);

/// <summary>Една причина „за“ или „против“.</summary>
public interface IOpinionFactor
{
    OpinionReason? Evaluate(OpinionInput input, OpinionModule opinion);
}

/// <summary>Числата на мнението (в настройките на приложението).</summary>
public sealed record OpinionSettings(
    double Loves = 2.5, double Likes = 1, double Grumble = -1, double Refuse = -2.4,
    double StarvingFullness = 25, int RefusalsPerDay = 1, double WhimChance = 0.3);

public sealed record OpinionState(string RefusalDay, int RefusalsToday, bool LastWasRefusal, string WhimDay, string Whim) : IStateSlice
{
    public static OpinionState Calm { get; } = new("", 0, false, "", "");
}

/// <summary>Мнението е показано (или действието е минало без диалог).</summary>
public sealed record OpinionShown(Stance Stance) : IGameCommand;
/// <summary>Каприз за днес (Developer/тестове). Празно – без каприз.</summary>
public sealed record SetWhim(string ObjectId) : IGameCommand;

/// <summary>Модулът „мнение“ (Id: opinion).</summary>
public sealed class OpinionModule : GameModule
{
    public const string ModuleId = "opinion";
    public const string Key = "opinion";

    private readonly List<IOpinionFactor> _factors = new();

    public OpinionSettings Settings { get; set; } = new();

    public override string Id => ModuleId;

    public OpinionModule(bool standardFactors = true)
    {
        if (!standardFactors) return;
        Add(new WishedFactor());
        Add(new DoneTodayFactor());
        Add(new DoneYesterdayFactor());
        Add(new HeavyBreakfastFactor());
        Add(new TiredCookFactor());
        Add(new WhimFactor());
        Add(new HotWarmFactor());
        Add(new ColdLightFactor());
    }

    /// <summary>Добавя причина (нов вид мнение – нов клас).</summary>
    public OpinionModule Add(IOpinionFactor factor)
    {
        _factors.Add(factor);
        return this;
    }

    /// <summary>Кандидатите за каприз днес (приложението ги дава – напр. всички ястия, които знае).</summary>
    public Func<IReadOnlyList<string>> WhimCandidates { get; set; } = () => Array.Empty<string>();

    protected override void OnRegister()
    {
        Store.Register(this, Key, OpinionState.Calm);
        Handle<OpinionShown>(c =>
        {
            var s = State<OpinionState>();
            string today = Clock.DayKey();
            int refusals = s.RefusalDay == today ? s.RefusalsToday : 0;
            if (c.Stance == Stance.Refuses) refusals++;
            Store.Set(s with { RefusalDay = today, RefusalsToday = refusals, LastWasRefusal = c.Stance == Stance.Refuses });
            return CommandResult.Done;
        });
        Handle<SetWhim>(c =>
        {
            Store.Update<OpinionState>(s => s with { WhimDay = Clock.DayKey(), Whim = c.ObjectId ?? "" });
            return CommandResult.Done;
        });
    }

    /// <summary>Капризът днес (решава се веднъж на ден; празно – няма).</summary>
    public string WhimToday
    {
        get
        {
            var s = State<OpinionState>();
            string today = Clock.DayKey();
            if (s.WhimDay == today) return s.Whim;
            var list = WhimCandidates();
            string whim = list.Count > 0 && Random.Chance(Settings.WhimChance) ? list[Random.Next(list.Count)] : "";
            Store.Set(s with { WhimDay = today, Whim = whim });
            return whim;
        }
    }

    /// <summary>Какво мисли за това. Не мени нищо (освен капризът за деня).</summary>
    public OpinionResult Evaluate(OpinionInput input)
    {
        var reasons = _factors.Select(f => f.Evaluate(input, this)).Where(r => r != null).Select(r => r!).ToList();
        bool starving = input.Fullness < Settings.StarvingFullness;
        // Много гладен: яде всичко – недоволствата почти не се броят.
        double score = reasons.Sum(r => r.Score < 0 && starving ? r.Score * 0.2 : r.Score);
        var worst = reasons.Where(r => r.Score < 0).OrderBy(r => r.Score).FirstOrDefault();
        var best = reasons.Where(r => r.Score > 0).OrderByDescending(r => r.Score).FirstOrDefault();
        Stance stance =
            score >= Settings.Loves ? Stance.Loves
            : score >= Settings.Likes ? Stance.Likes
            : score > Settings.Grumble ? Stance.Neutral
            : score > Settings.Refuse ? Stance.Grumbles
            : CanRefuse(starving) ? Stance.Refuses : Stance.Grumbles;
        var reason = stance is Stance.Loves or Stance.Likes ? best : stance is Stance.Grumbles or Stance.Refuses ? worst : null;
        return new OpinionResult(stance, reason, score, reasons);
    }

    private bool CanRefuse(bool starving)
    {
        if (starving) return false;
        var s = State<OpinionState>();
        int today = s.RefusalDay == Clock.DayKey() ? s.RefusalsToday : 0;
        return today < Settings.RefusalsPerDay && !s.LastWasRefusal;
    }

    // ── Стандартните причини ──

    private static OpinionReason R(string key, double score, OpinionInput i) =>
        new(key, score, Facts.Of(("храна", i.ObjectName.ToLowerInvariant()), ("нещо", i.ObjectName.ToLowerInvariant())));

    private static bool Food(OpinionInput i) => i.Verb is "cook" or "eat";

    /// <summary>Точно това му се иска.</summary>
    public sealed class WishedFactor : IOpinionFactor
    {
        public OpinionReason? Evaluate(OpinionInput i, OpinionModule o) => i.Wished ? R("opinion.loves", 3, i) : null;
    }

    public sealed class DoneTodayFactor : IOpinionFactor
    {
        public OpinionReason? Evaluate(OpinionInput i, OpinionModule o) => Food(i) && i.DoneToday && !i.Wished ? R("opinion.ate.today", -2, i) : null;
    }

    public sealed class DoneYesterdayFactor : IOpinionFactor
    {
        public OpinionReason? Evaluate(OpinionInput i, OpinionModule o) => Food(i) && !i.DoneToday && i.DoneYesterday && !i.Wished ? R("opinion.ate.yesterday", -1.2, i) : null;
    }

    /// <summary>Манджа (тежко) на закуска: 5–10 ч.</summary>
    public sealed class HeavyBreakfastFactor : IOpinionFactor
    {
        public OpinionReason? Evaluate(OpinionInput i, OpinionModule o) =>
            Food(i) && i.Tags.Contains("heavy") && i.Now.Hour is >= 5 and < 10 ? R("opinion.breakfast.heavy", -2.6, i) : null;
    }

    /// <summary>Изморен е за сложно готвене.</summary>
    public sealed class TiredCookFactor : IOpinionFactor
    {
        public OpinionReason? Evaluate(OpinionInput i, OpinionModule o) =>
            i.Verb == "cook" && i.Tags.Contains("complex") && i.Energy < 25 ? R("opinion.tired.cook", -1.5, i) : null;
    }

    /// <summary>Днешният каприз: точно това днес не му харесва.</summary>
    public sealed class WhimFactor : IOpinionFactor
    {
        public OpinionReason? Evaluate(OpinionInput i, OpinionModule o) =>
            Food(i) && !i.Wished && o.WhimToday is { Length: > 0 } w && w == i.ObjectId ? R("opinion.whim", -2.6, i) : null;
    }

    /// <summary>Жега, а топла дреха.</summary>
    public sealed class HotWarmFactor : IOpinionFactor
    {
        public OpinionReason? Evaluate(OpinionInput i, OpinionModule o) =>
            i.Verb == "wear" && i.Tags.Contains("warm") && i.Temperature >= 24 ? R("opinion.hot.warm", -1.5, i) : null;
    }

    /// <summary>Студ, а лятна дреха.</summary>
    public sealed class ColdLightFactor : IOpinionFactor
    {
        public OpinionReason? Evaluate(OpinionInput i, OpinionModule o) =>
            i.Verb == "wear" && i.Tags.Contains("light") && i.Temperature <= 5 ? R("opinion.cold.light", -1.5, i) : null;
    }
}
