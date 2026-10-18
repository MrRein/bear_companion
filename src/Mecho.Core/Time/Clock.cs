using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Mecho.Core;

/// <summary>
/// Времето за ядрото. Mono – секунди от пускането (за „след 5 мин“; не скача при смяна
/// на часовника); Now – истинската дата и час (за „сутрин“, „Коледа“, „нов ден“).
/// В тестовете (и в Developer „симулирай дата“) се подменя с ManualClock.
/// </summary>
public interface IClock
{
    double Mono { get; }

    DateTime Now { get; }
}

public static class ClockExtensions
{
    public static DateTime Today(this IClock clock) => clock.Now.Date;

    /// <summary>Денят като ключ („2026-10-18“) – един и същ навсякъде.</summary>
    public static string DayKey(this IClock clock) => clock.Now.ToString("yyyy-MM-dd");
}

/// <summary>Истинското време.</summary>
public sealed class SystemClock : IClock
{
    private readonly Stopwatch _watch = Stopwatch.StartNew();

    public double Mono => _watch.Elapsed.TotalSeconds;

    /// <summary>Отместване на датата (Developer: „симулирай Коледа“). Обикновено 0.</summary>
    public TimeSpan Shift { get; set; }

    public DateTime Now => DateTime.Now + Shift;
}

/// <summary>Време на ръка (тестове): Advance(секунди) мести и двата часовника.</summary>
public sealed class ManualClock : IClock
{
    public ManualClock(DateTime start) => Now = start;

    public double Mono { get; private set; }

    public DateTime Now { get; set; }

    public void Advance(double seconds)
    {
        if (seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        Mono += seconds;
        Now = Now.AddSeconds(seconds);
    }
}

/// <summary>Случайността за ядрото – подменя се в тестовете с фиксирано зърно.</summary>
public interface IRandom
{
    int Next(int maxExclusive);

    int Next(int min, int maxExclusive);

    double NextDouble();
}

public sealed class SystemRandom : IRandom
{
    private readonly Random _rng;

    public SystemRandom(int? seed = null) => _rng = seed is int s ? new Random(s) : new Random();

    public int Next(int maxExclusive) => _rng.Next(maxExclusive);

    public int Next(int min, int maxExclusive) => _rng.Next(min, maxExclusive);

    public double NextDouble() => _rng.NextDouble();
}

public static class RandomExtensions
{
    /// <summary>true с вероятност p (0…1).</summary>
    public static bool Chance(this IRandom rng, double p) => p > 0 && (p >= 1 || rng.NextDouble() < p);

    /// <summary>Число между min и max (за интервали: 40–70 мин).</summary>
    public static double Between(this IRandom rng, double min, double max) => min + rng.NextDouble() * Math.Max(0, max - min);

    /// <summary>Случаен елемент; празен списък → InvalidOperationException (ясна грешка, не IndexOutOfRange).</summary>
    public static T Pick<T>(this IRandom rng, IReadOnlyList<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0) throw new InvalidOperationException("Pick от празен списък.");
        return items[rng.Next(items.Count)];
    }
}
