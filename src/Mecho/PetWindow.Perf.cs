using System;
using System.Diagnostics;
using System.IO;

namespace Mecho;

// Колко бързо върви мечокът: всеки цикъл се мери на части (поведение, грижи,
// храна, хранилка, живот, времето, кадърът с дрехите, подреждането на прозореца…),
// както и строенето на менюто. В Developer → „Скорост“: „Покажи скоростта“ казва
// средното и най-бавното; „Записвай бавните моменти“ пише в perf.log всеки цикъл
// над 25 мс и всяко строене на меню над 40 мс (какво е било бавно и колко).
public sealed partial class PetWindow
{
    // Частите на цикъла: име и колко мс (само по-дългите от 0,3 мс).
    private readonly System.Collections.Generic.List<(string Name, double Ms)> _perfSteps = new();
    private long _perfLast;

    /// <summary>Край на една част от цикъла (времето от предишната отметка).</summary>
    private void P(string name)
    {
        long t = Stopwatch.GetTimestamp();
        double ms = (t - _perfLast) * 1000.0 / Stopwatch.Frequency;
        if (ms > 0.3) _perfSteps.Add((name, ms));
        _perfLast = t;
    }
    private double _perfSum, _perfMax, _perfMenuMax;
    private int _perfCount;
    private string _perfMaxWhat = "", _perfMenuWhat = "";
    private DateTime _perfSince = DateTime.Now;

    /// <summary>„Записвай бавните моменти“ (Developer): в perf.log до записа.</summary>
    public bool PerfLogging { get; set; }

    public static string PerfLogPath => Path.Combine(SaveData.Folder, "perf.log");

    private static double Ms(long from) => (Stopwatch.GetTimestamp() - from) * 1000.0 / Stopwatch.Frequency;

    private void PerfTick(long start)
    {
        double total = Ms(start);
        _perfSum += total;
        _perfCount++;
        // Най-бавните части (до 4) и какво прави мечокът в момента.
        _perfSteps.Sort((a, b) => b.Ms.CompareTo(a.Ms));
        string parts = string.Join(" · ", _perfSteps.GetRange(0, Math.Min(4, _perfSteps.Count)).ConvertAll(x => $"{x.Name} {x.Ms:0.0}"));
        string what = $"{parts} мс | {_state}/{_anim}{(_inScene ? ", кътче " + _scene?.Id : "")}{(InFocus ? ", работи" : "")}{(MenuIsOpen ? ", менюто е отворено" : "")}";
        if (total > _perfMax)
        {
            _perfMax = total;
            _perfMaxWhat = what;
        }
        if (PerfLogging && total > 25) PerfWrite($"цикъл {total:0.0} мс: {what}");
    }

    /// <summary>Нещо извън цикъла (строене на менюто и т.н.): пази най-бавното, записва бавното.</summary>
    public void PerfNote(string what, double ms)
    {
        if (ms > _perfMenuMax)
        {
            _perfMenuMax = ms;
            _perfMenuWhat = what;
        }
        if (PerfLogging && ms > 40) PerfWrite($"{what}: {ms:0.0} мс");
    }

    private void PerfWrite(string line)
    {
        try { File.AppendAllText(PerfLogPath, $"{DateTime.Now:HH:mm:ss.fff}  {line}{Environment.NewLine}"); }
        catch (IOException) { }
    }

    /// <summary>Кратко: средно/най-бавно за цикъла и менюто, памет, колко пъти в секунда.</summary>
    public string PerfSummary()
    {
        double secs = Math.Max(1, (DateTime.Now - _perfSince).TotalSeconds);
        double avg = _perfCount == 0 ? 0 : _perfSum / _perfCount;
        long mem = GC.GetTotalMemory(false) / (1024 * 1024);
        long ws = Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024);
        return $"Цикъл: {_perfCount / secs:0.0} пъти/с, средно {avg:0.00} мс, най-бавно {_perfMax:0.0} мс ({_perfMaxWhat}). " +
               $"Меню: най-бавно {_perfMenuMax:0.0} мс ({_perfMenuWhat}). Памет: {mem} MB (процесът {ws} MB).";
    }

    public void PerfReset()
    {
        _perfSum = _perfMax = _perfMenuMax = 0;
        _perfCount = 0;
        _perfMaxWhat = _perfMenuWhat = "";
        _perfSince = DateTime.Now;
    }
}
