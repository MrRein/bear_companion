using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace Mecho;

/// <summary>
/// Master loot_list: assets/loot_list.xlsx. Оттам играта чете какво може да се падне
/// от всеки източник (цел, подарък, всеки вид сандъче) и с какво тегло. Лист „Предмети“ –
/// всеки предмет в играта на ред, колоните с източниците са тегла (сумата в колона не е
/// задължително 100: нещата, които вече имаш, се пропускат и останалите се преразпределят).
/// Лист „Настройки“ – числата (колко лешника, колко неща в сандъче, шансове…).
/// Чете се сам (xlsx е zip с xml) – без допълнителни библиотеки.
/// </summary>
public static class Loot
{
    public const string Mission = "Цел", Gift = "Подарък", Wood = "Дървено", Silver = "Сребърно", Gold = "Златно", Legend = "Лилаво";
    /// <summary>Офертата на деня в магазина (няма колона – само рядкостта решава).</summary>
    public const string Offer = "Оферта";
    public static readonly string[] Sources = { Mission, Gift, Wood, Silver, Gold, Legend };

    public sealed record Row(string Code, string Name, string Kind, IReadOnlyDictionary<string, double> Weights, Rarity? Rarity = null)
    {
        public double W(string source) => Weights.TryGetValue(source, out var w) ? w : 0;
    }

    private static List<Row>? _rows;
    private static Dictionary<string, double>? _settings;

    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "assets", "loot_list.xlsx");

    /// <summary>Грешка при четене (за самопроверката), иначе null.</summary>
    public static string? LoadError { get; private set; }

    public static IReadOnlyList<Row> Rows { get { Load(); return _rows!; } }

    private static Dictionary<string, Row>? _byCode;

    public static Row? Find(string code)
    {
        Load();
        _byCode ??= _rows!.GroupBy(r => r.Code).ToDictionary(g => g.Key, g => g.First());
        return _byCode.TryGetValue(code, out var r) ? r : null;
    }

    /// <summary>Число от „Настройки“ (ако го няма – def).</summary>
    public static double Setting(string key, double def)
    {
        Load();
        return _settings!.TryGetValue(key, out var v) ? v : def;
    }

    public static int SettingInt(string key, int def) => (int)Math.Round(Setting(key, def));

    /// <summary>Чете наново (след редакция на файла).</summary>
    public static void Reload() { _rows = null; _settings = null; _byCode = null; Load(); }

    /// <summary>Рядкостта на предмета (null – няма, напр. лешници, купони, уреди).</summary>
    public static Rarity? RarityOf(string code) => Find(code)?.Rarity;

    /// <summary>
    /// Цената в магазина по рядкостта („Настройки“ → „цена дрехи Хубаво“…). Безплатните
    /// (началните) остават безплатни; без рядкост – цената от кода.
    /// </summary>
    public static int PriceFor(string code, int fallback)
    {
        if (fallback <= 0 || RarityOf(code) is not { } r || Rarities.PriceGroup(code) is not { } g) return fallback;
        return Math.Max(1, SettingInt($"цена {g} {Rarities.Name(r)}", Rarities.DefaultPrice(g, r)));
    }

    private static readonly Dictionary<string, double[]> RarityDefaults = new()
    {
        [Offer] = new double[] { 62, 28, 9, 1 },
        [Gift] = new double[] { 60, 30, 9, 1 },
        [Mission] = new double[] { 50, 35, 13, 2 },
        [Wood] = new double[] { 72, 24, 4, 0 },
        [Silver] = new double[] { 50, 38, 11, 1 },
        [Gold] = new double[] { 25, 45, 27, 3 },
        [Legend] = new double[] { 5, 35, 48, 12 },
    };

    /// <summary>Колко често излиза дадена рядкост от източника („Настройки“ → „рядкост Златно Безценно“).</summary>
    public static double RarityWeight(string source, Rarity r) =>
        Math.Max(0, Setting($"рядкост {source} {Rarities.Name(r)}", RarityDefaults.TryGetValue(source, out var d) ? d[(int)r] : 1));

    private static string Category(string code) => code.StartsWith("skin:") ? "skin" : code.Split(':')[0];

    /// <summary>
    /// Тегли едно нещо от източника: само от редовете с тегло &gt; 0, за които available казва „да“.
    /// За нещата с рядкост: видът (дрехи, тапети…) взима общото си тегло от колоната, а вътре
    /// рядкостта се тегли по таблицата „рядкост …“ (каквито рядкости има още), после – предметът.
    /// null – няма какво.
    /// </summary>
    public static string? Roll(string source, Random rng, Func<string, bool> available)
    {
        var pool = Rows.Where(r => r.W(source) > 0 && available(r.Code)).ToList();
        if (pool.Count == 0) return null;
        var eff = Effective(pool, source, r => r.W(source));
        return Pick(pool, eff, rng)?.Code;
    }

    /// <summary>Тегли между дадени предмети само по рядкостта им (офертата на деня).</summary>
    public static string? RollByRarity(string source, IReadOnlyList<string> codes, Random rng)
    {
        if (codes.Count == 0) return null;
        var pool = codes.Select(c => Find(c) ?? new Row(c, c, "", new Dictionary<string, double>())).ToList();
        var eff = Effective(pool, source, _ => 1);
        return Pick(pool, eff, rng)?.Code ?? codes[rng.Next(codes.Count)];
    }

    private static double[] Effective(List<Row> pool, string source, Func<Row, double> w)
    {
        var eff = new double[pool.Count];
        foreach (var cat in pool.Select((r, i) => (r, i)).GroupBy(x => Category(x.r.Code)))
        {
            var withR = cat.Where(x => x.r.Rarity != null).ToList();
            foreach (var x in cat.Where(x => x.r.Rarity == null)) eff[x.i] = w(x.r);
            if (withR.Count == 0) continue;
            double mass = withR.Sum(x => w(x.r));
            var byR = withR.GroupBy(x => x.r.Rarity!.Value).ToList();
            double rsum = byR.Sum(g => RarityWeight(source, g.Key));
            if (rsum <= 0) continue;
            foreach (var g in byR)
            {
                double share = mass * RarityWeight(source, g.Key) / rsum, inner = g.Sum(x => w(x.r));
                foreach (var x in g) eff[x.i] = inner > 0 ? share * w(x.r) / inner : 0;
            }
        }
        return eff;
    }

    private static Row? Pick(List<Row> pool, double[] eff, Random rng)
    {
        double total = eff.Sum();
        if (total <= 0) return null;
        double x = rng.NextDouble() * total;
        for (int i = 0; i < pool.Count; i++)
        {
            x -= eff[i];
            if (x < 0) return pool[i];
        }
        return pool[^1];
    }

    private static void Load()
    {
        if (_rows != null) return;
        _rows = new List<Row>();
        _settings = new Dictionary<string, double>();
        try
        {
            var sheets = ReadWorkbook(FilePath);
            if (sheets.TryGetValue("Предмети", out var items) && items.Count > 0)
            {
                int head = items.FindIndex(r => r.Count > 0 && r[0].Trim() == "Код");
                if (head < 0) throw new InvalidDataException("лист „Предмети“ няма ред „Код“");
                var cols = items[head];
                int Col(string name) => cols.FindIndex(c => c.Trim() == name);
                int name = Col("Име"), kind = Col("Вид"), rarity = Col("Рядкост");
                var src = Sources.ToDictionary(s => s, Col);
                foreach (var r in items.Skip(head + 1))
                {
                    string code = Cell(r, 0).Trim();
                    if (code.Length == 0 || code.StartsWith("#")) continue;
                    var w = new Dictionary<string, double>();
                    foreach (var (s, c) in src)
                        if (c >= 0 && Num(Cell(r, c)) is double v && v > 0) w[s] = v;
                    _rows.Add(new Row(code, Cell(r, name), Cell(r, kind), w, Rarities.Parse(Cell(r, rarity))));
                }
            }
            else throw new InvalidDataException("няма лист „Предмети“");
            if (sheets.TryGetValue("Настройки", out var set))
                foreach (var r in set)
                    if (Cell(r, 0).Trim() is { Length: > 0 } key && Num(Cell(r, 1)) is double v) _settings[key] = v;
            LoadError = null;
        }
        catch (Exception e)
        {
            LoadError = e.Message;
        }
    }

    private static string Cell(List<string> r, int i) => i >= 0 && i < r.Count ? r[i] : "";

    private static double? Num(string s)
    {
        s = s.Trim().TrimEnd('%').Replace(',', '.');
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    // ---- минимален xlsx четец ----

    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace Pkg = "http://schemas.openxmlformats.org/package/2006/relationships";

    /// <summary>Лист → редове → клетки (текст). Празните клетки са "".</summary>
    public static Dictionary<string, List<List<string>>> ReadWorkbook(string path)
    {
        // FileShare.ReadWrite – да се чете и докато е отворен в Excel.
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Read);
        XDocument Doc(string name) => XDocument.Load(zip.GetEntry(name)!.Open());
        var shared = new List<string>();
        if (zip.GetEntry("xl/sharedStrings.xml") != null)
            foreach (var si in Doc("xl/sharedStrings.xml").Root!.Elements(S + "si"))
                shared.Add(string.Concat(si.Descendants(S + "t").Select(t => t.Value)));
        var rels = Doc("xl/_rels/workbook.xml.rels").Root!.Elements(Pkg + "Relationship")
            .ToDictionary(e => (string)e.Attribute("Id")!, e => (string)e.Attribute("Target")!);
        var result = new Dictionary<string, List<List<string>>>();
        foreach (var sh in Doc("xl/workbook.xml").Root!.Element(S + "sheets")!.Elements(S + "sheet"))
        {
            string target = rels[(string)sh.Attribute(Rel + "id")!].TrimStart('/');
            if (!target.StartsWith("xl/")) target = "xl/" + target;
            var rows = new List<List<string>>();
            foreach (var row in Doc(target).Descendants(S + "row"))
            {
                int rn = (int?)row.Attribute("r") ?? rows.Count + 1;
                while (rows.Count < rn - 1) rows.Add(new List<string>());
                var cells = new List<string>();
                foreach (var c in row.Elements(S + "c"))
                {
                    int ci = ColIndex((string?)c.Attribute("r"), cells.Count);
                    while (cells.Count < ci) cells.Add("");
                    string t = (string?)c.Attribute("t") ?? "";
                    string v = c.Element(S + "v")?.Value ?? "";
                    cells.Add(t switch
                    {
                        "s" when int.TryParse(v, out int k) && k < shared.Count => shared[k],
                        "inlineStr" => string.Concat(c.Descendants(S + "t").Select(x => x.Value)),
                        _ => v,
                    });
                }
                rows.Add(cells);
            }
            result[(string)sh.Attribute("name")!] = rows;
        }
        return result;
    }

    private static int ColIndex(string? cellRef, int fallback)
    {
        if (string.IsNullOrEmpty(cellRef)) return fallback;
        int n = 0;
        foreach (char ch in cellRef)
        {
            if (ch < 'A' || ch > 'Z') break;
            n = n * 26 + (ch - 'A' + 1);
        }
        return n > 0 ? n - 1 : fallback;
    }
}
