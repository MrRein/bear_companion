using System;

namespace Mecho.Core;

// ─────────────────────────────────────────────────────────────────────────────
// КАТАЛОГ НА СИГНАЛИТЕ
//
// Всички сигнали на играта на едно място – да се вижда какво може да се слуша.
// Нов сигнал: един ред record тук (или в файла на модула, ако е само негов):
//
//     public sealed record PlantWatered(int Stage) : ISignal;
//
// Имената са в минало време (нещо СТАНА). Данните са неизменими.
// ─────────────────────────────────────────────────────────────────────────────

// ── Ядро ──

/// <summary>Модул спря заради повтарящи се грешки (виж ModuleHost.FaultLimit).</summary>
public sealed record ModuleFaulted(string ModuleId, string Reason) : ISignal;

/// <summary>Сборът на модификаторите се смени (събитие почна/свърши, дреха…).</summary>
public sealed record ModifiersChanged(string Source) : ISignal;

/// <summary>Нов ден (първият цикъл след полунощ или първото пускане за деня).</summary>
public sealed record DayStarted(DateTime Day, DateTime? PreviousDay) : ISignal;

// ── Мечокът и нуждите ──

/// <summary>Ситостта или енергията се смени (с поне 1 точка).</summary>
public sealed record NeedsChanged(double OldFullness, double Fullness, double OldEnergy, double Energy) : ISignal;

/// <summary>Заспа / събуди се.</summary>
public sealed record SleepChanged(bool Asleep) : ISignal;

// ── Предмети и пари ──

/// <summary>Лешниците се смениха (Old → New; Reason: „задача“, „покупка“…, ако се знае).</summary>
public sealed record HazelnutsChanged(int Old, int New, string? Reason = null) : ISignal;

/// <summary>Нов предмет (уред, дреха, скин, тапет, рецепта…). Source: „магазин“, „подарък“, „dev“…</summary>
public sealed record ItemAcquired(string ItemId, string Source) : ISignal;

// ── Събития в играта (пижамен ден, болен ден, Коледа – виж плана, фаза 4б) ──

/// <summary>Събитие в играта почна.</summary>
public sealed record GameEventStarted(string EventId) : ISignal;

/// <summary>Събитие в играта се промени (стъпка, напредък).</summary>
public sealed record GameEventUpdated(string EventId, string What) : ISignal;

/// <summary>Събитие в играта свърши (Reason: „време“, „излекуван“, „dev“…).</summary>
public sealed record GameEventEnded(string EventId, string Reason) : ISignal;
