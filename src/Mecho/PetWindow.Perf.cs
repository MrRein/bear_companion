using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

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

    // По части за целия цикъл (за доклада от теста за скорост): брой, сума, най-много.
    private readonly System.Collections.Generic.Dictionary<string, (int N, double Sum, double Max)> _perfParts = new();
    private readonly System.Collections.Generic.List<double> _perfTicks = new();

    private void PerfTick(long start)
    {
        double total = Ms(start);
        if (_perfBench)
        {
            _perfTicks.Add(total);
            foreach (var (name, ms) in _perfSteps)
            {
                _perfParts.TryGetValue(name, out var p);
                _perfParts[name] = (p.N + 1, p.Sum + ms, Math.Max(p.Max, ms));
            }
        }
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

    // ───────────────────────── Тест за скорост (доклад) ─────────────────────────

    private bool _perfBench;

    public static string PerfReportPath => Path.Combine(SaveData.Folder, "perf_report.txt");

    /// <summary>
    /// Developer → „Тест за скорост (доклад)“: ~1,5 мин минава през всичко тежко
    /// (покой, ходене, четене, работа на бюро, игра, статив, растение, радио с танц,
    /// хранилка с птичка, готвене и ядене, пеперуда, менюто – всеки таб) и мери всяко
    /// поотделно. Накрая пише perf_report.txt и го отваря – него ми го пращаш.
    /// </summary>
    public async System.Threading.Tasks.Task RunPerfReport()
    {
        if (_perfBench) return;
        var report = new System.Text.StringBuilder();
        var proc = Process.GetCurrentProcess();
        report.AppendLine($"Мечо – тест за скорост, {DateTime.Now:yyyy-MM-dd HH:mm}");
        report.AppendLine($"Версия {Updater.CurrentName} · Windows {Environment.OSVersion.Version} · .NET {Environment.Version} · {Environment.ProcessorCount} ядра");
        var area = Area;
        double dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this).DpiScaleX;
        report.AppendLine($"Екран (работна площ) {area.Width:0}×{area.Height:0} · DPI ×{dpi:0.##} · пиксел на мечока {PixelSize} · прозорец {Width:0}×{Height:0}");
        report.AppendLine($"Записът (save.json): {(File.Exists(SaveData.FilePath) ? new FileInfo(SaveData.FilePath).Length / 1024 : 0)} KB · дни в паметта {_save.Days.Count} · задачи {_save.Tasks.Count} · бележки {_save.Notes.Count}");
        report.AppendLine($"Отворени прозорци на програмата: {System.Windows.Application.Current.Windows.Count}");
        report.AppendLine();

        // Какво беше преди теста – после се връща.
        var owned = new System.Collections.Generic.List<string>(_save.Owned);
        int nuts = _save.Hazelnuts;
        double full = _save.Fullness, energy = _save.Energy;
        bool radio = _save.RadioPlaced, radioOn = _save.RadioOn, feeder = _save.FeederPlaced;
        int feederFood = _save.FeederFood;
        bool stay = _save.StayPut, auto = _save.AutoMode, calm = _save.CalmWhileTyping;
        string stayScene = _save.StayScene;
        bool wasLogging = PerfLogging;
        PerfLogging = false;
        _save.CalmWhileTyping = false;
        InstantModes = true;
        foreach (var u in Kitchen.Upgrades) if (!_save.Owned.Contains(u.Id)) _save.Owned.Add(u.Id);
        _save.Hazelnuts = 999;
        Say("Тест за скорост: ще пробвам всичко, около минута и половина. Не ме пипай!", 6);

        long gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2);
        var cpu0 = proc.TotalProcessorTime;
        var wall = Stopwatch.StartNew();
        long mem0 = GC.GetTotalMemory(false);

        var phases = new (string Name, Action Start, int Seconds)[]
        {
            ("Покой", () => { CleanForBench(); SetIdle(); }, 6),
            ("Ходене", () => { CleanForBench(); WalkTo(_x > Area.Left + Area.Width / 2 ? Area.Left + 100 : Area.Right - 100); }, 6),
            ("Чете", () => { CleanForBench(); StartScene(Scenes.Reading, 60); }, 6),
            ("Работи на бюро (мечо-доро)", () => { CleanForBench(); StartFocus(); }, 8),
            ("Игра на компютър", () => { CleanForBench(); StartScene(Scenes.Gaming, 60); }, 6),
            ("Рисува на статива", () => { CleanForBench(); StartScene(Scenes.Painting, 60); }, 6),
            ("Растение (издърпва, грижи)", () => { CleanForBench(); StartScene(Scenes.Plant, 60); }, 10),
            ("Радио и танц", () => { CleanForBench(); SetRadio(true); DanceByRadio(); }, 8),
            ("Хранилка и птичка", () => { CleanForBench(); SetFeeder(true); _save.FeederFood = 3; _nextBird = 0; }, 8),
            ("Готвене и ядене", () => { CleanForBench(); TestCook(Kitchen.Foods.First(f => f.Id == "sandwich")); EndCooking(Kitchen.Foods.First(f => f.Id == "sandwich")); }, 8),
            ("Пеперуда", () => { CleanForBench(); StartChase(); }, 6),
            ("Говори и емоции", () => { CleanForBench(); Say("Тест, тест, едно, две, три – балончето с думите.", 5); Emote("heart", 4); }, 5),
        };
        report.AppendLine("ЧАСТИ (по 5–10 с всяка):");
        report.AppendLine("  част | цикли/с | средно мс | 95% мс | най-много мс | най-тежките неща вътре (средно/най-много мс)");
        foreach (var (name, start, seconds) in phases)
        {
            _perfParts.Clear();
            _perfTicks.Clear();
            try { start(); }
            catch (Exception e) { report.AppendLine($"  {name}: не тръгна ({e.Message})"); continue; }
            _perfBench = true;
            var pw = Stopwatch.StartNew();
            await System.Threading.Tasks.Task.Delay(seconds * 1000);
            _perfBench = false;
            report.AppendLine("  " + PhaseLine(name, pw.Elapsed.TotalSeconds));
        }
        CleanForBench();
        SetRadio(radio);
        _save.RadioOn = radioOn;
        SetFeeder(feeder);
        _save.FeederFood = feederFood;

        // Менюто: всеки таб, построен наново.
        report.AppendLine();
        report.AppendLine("МЕНЮТО (строене на таба наново):");
        try
        {
            OpenMenu(MenuWindow.BearTab);
            await System.Threading.Tasks.Task.Delay(300);
            foreach (var (tab, ms) in _menu!.BenchTabs()) report.AppendLine($"  {tab}: {ms:0.0} мс");
            var open = Stopwatch.StartNew();
            _menu.Hide();
            OpenMenu(MenuWindow.FoodTab);
            _menu.UpdateLayout();
            report.AppendLine($"  отваряне на менюто (Храна): {open.Elapsed.TotalMilliseconds:0.0} мс");
            _menu.Hide();
        }
        catch (Exception e) { report.AppendLine("  не можа: " + e.Message); }

        // Кадрите с дрехите: колко струва да се сглоби всеки кадър (с това, което е облечено).
        report.AppendLine();
        var compose = Stopwatch.StartNew();
        int frames = 0;
        foreach (var anim in _lib.AnimationNames)
        {
            var a = _lib.Get(anim);
            for (int i = 0; i < a.Frames.Length; i++) { _dresser.Frame(_lib, anim, i, EffectiveOutfit); frames++; }
        }
        report.AppendLine($"КАДРИ С ДРЕХИТЕ: {frames} кадъра за {compose.Elapsed.TotalMilliseconds:0} мс (вече сглобените са от паметта)");

        // Записът.
        var save = Stopwatch.StartNew();
        PersistNow();
        report.AppendLine($"ЗАПИС НА СЪСТОЯНИЕТО: {save.Elapsed.TotalMilliseconds:0.0} мс");

        report.AppendLine();
        report.AppendLine($"ОБЩО: {wall.Elapsed.TotalSeconds:0} с · процесорно време {(proc.TotalProcessorTime - cpu0).TotalMilliseconds / Math.Max(1, wall.Elapsed.TotalMilliseconds) * 100 / Environment.ProcessorCount:0.0}% от целия процесор" +
            $" · GC {GC.CollectionCount(0) - gc0}/{GC.CollectionCount(1) - gc1}/{GC.CollectionCount(2) - gc2} (поколения 0/1/2)" +
            $" · памет {mem0 / 1048576} → {GC.GetTotalMemory(false) / 1048576} MB (процесът {Process.GetCurrentProcess().WorkingSet64 / 1048576} MB)");
        report.AppendLine();
        report.AppendLine("Най-бавните неща от обикновената работа (от пускането досега): " + PerfSummary());

        // Връща всичко, както беше.
        _save.Owned.Clear();
        _save.Owned.AddRange(owned);
        _save.Hazelnuts = nuts;
        _save.Fullness = full;
        _save.Energy = energy;
        _save.StayPut = stay;
        _save.AutoMode = auto;
        _save.StayScene = stayScene;
        _save.CalmWhileTyping = calm;
        InstantModes = SelfTest.Running;
        PerfLogging = wasLogging;
        SetIdle();
        NotifyCare();
        PersistNow();

        try
        {
            File.WriteAllText(PerfReportPath, report.ToString());
            if (!SelfTest.Running) Process.Start(new ProcessStartInfo(PerfReportPath) { UseShellExecute = true });
        }
        catch (Exception) { }
        Say("Готово! Докладът е в perf_report.txt – прати го.", 6);
    }

    /// <summary>Ред за една част: колко пъти в секунда, средно, 95-и процентил, най-много и най-тежките неща.</summary>
    private string PhaseLine(string name, double seconds)
    {
        if (_perfTicks.Count == 0) return $"{name}: няма цикли";
        _perfTicks.Sort();
        double avg = 0;
        foreach (var t in _perfTicks) avg += t;
        avg /= _perfTicks.Count;
        double p95 = _perfTicks[(int)Math.Min(_perfTicks.Count - 1, _perfTicks.Count * 0.95)];
        var heavy = new System.Collections.Generic.List<(string Name, double Avg, double Max)>();
        foreach (var (k, v) in _perfParts) heavy.Add((k, v.Sum / _perfTicks.Count, v.Max));
        heavy.Sort((a, b) => b.Avg.CompareTo(a.Avg));
        string parts = string.Join(", ", heavy.GetRange(0, Math.Min(5, heavy.Count)).ConvertAll(h => $"{h.Name} {h.Avg:0.00}/{h.Max:0.0}"));
        return $"{name} | {_perfTicks.Count / seconds:0.0} | {avg:0.00} | {p95:0.0} | {_perfTicks[^1]:0.0} | {parts}";
    }

    /// <summary>Чисто начало за всяка част на теста.</summary>
    private void CleanForBench()
    {
        if (_save.PomodoroPhase != PomodoroPhase.Off) StopPomodoro();
        if (_inScene) LeaveScene();
        if (_onTrampoline) EndBounce(null);
        _plantPushing = false;
        _plantOut = 0;
        HideGarden();
        DevClearFood();
        if (_cook != null) EndCooking(food: null);
        _eating = null;
        _held.Source = null;
        _save.StayPut = false;
        if (IsAsleep) WakeUp();
        SetRadio(false);
        SetFeeder(false);
        SetIdle();
    }
}
