using System;
using System.Collections.Generic;

namespace Mecho.Core;

/// <summary>
/// Къде ядрото пише какво става. Приложението дава своя (грешките отиват в
/// crash.log); тестовете ползват MemoryLog. Ядрото никога не хвърля заради лог.
/// </summary>
public interface ICoreLog
{
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? ex = null);
}

/// <summary>Пази последните записи в паметта (тестове, Developer панел).</summary>
public sealed class MemoryLog : ICoreLog
{
    private readonly int _capacity;
    private readonly LinkedList<string> _lines = new();

    public MemoryLog(int capacity = 500) => _capacity = Math.Max(10, capacity);

    public IReadOnlyCollection<string> Lines
    {
        get
        {
            lock (_lines) return new List<string>(_lines);
        }
    }

    public int Errors { get; private set; }

    public int Warnings { get; private set; }

    public void Info(string message) => Add("info", message);

    public void Warn(string message)
    {
        Warnings++;
        Add("warn", message);
    }

    public void Error(string message, Exception? ex = null)
    {
        Errors++;
        Add("error", ex == null ? message : $"{message}: {ex.GetType().Name}: {ex.Message}");
    }

    private void Add(string level, string message)
    {
        lock (_lines)
        {
            _lines.AddLast($"{DateTime.Now:HH:mm:ss} [{level}] {message}");
            while (_lines.Count > _capacity) _lines.RemoveFirst();
        }
    }
}

/// <summary>Праща всяко писане и към друг лог (например MemoryLog за Developer + crash.log).</summary>
public sealed class TeeLog : ICoreLog
{
    private readonly ICoreLog[] _logs;

    public TeeLog(params ICoreLog[] logs) => _logs = logs ?? Array.Empty<ICoreLog>();

    public void Info(string message)
    {
        foreach (var l in _logs) Safe(() => l.Info(message));
    }

    public void Warn(string message)
    {
        foreach (var l in _logs) Safe(() => l.Warn(message));
    }

    public void Error(string message, Exception? ex = null)
    {
        foreach (var l in _logs) Safe(() => l.Error(message, ex));
    }

    private static void Safe(Action a)
    {
        try { a(); }
        catch (Exception) { /* логът не бива да чупи нищо */ }
    }
}
