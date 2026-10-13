using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Mecho;

/// <summary>
/// Всички реплики на мечока живеят в assets/repliki.txt (обикновен текст, по
/// една реплика на ред). Раздел започва с „=== ключ | описание“. Празните редове
/// и редовете с „//“ се прескачат. {име} е името на Тут.
/// </summary>
public static class R
{
    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "assets", "repliki.txt");

    private static Dictionary<string, string[]>? _all;
    private static readonly HashSet<string> Missing = new();

    private static Dictionary<string, string[]> All => _all ??= Load(FilePath);

    /// <summary>Ключовете, които кодът поиска, а във файла ги няма (за самопроверката).</summary>
    public static IReadOnlyCollection<string> MissingKeys => Missing;

    public static IReadOnlyCollection<string> Keys => All.Keys;

    /// <summary>Чете файла наново (след редакция, без рестарт). Вече заредените списъци в кода остават.</summary>
    public static int Reload()
    {
        _all = Load(FilePath);
        Missing.Clear();
        return _all.Count;
    }

    /// <summary>Списъкът реплики за ключа (празен, ако го няма).</summary>
    public static string[] Get(string key)
    {
        if (All.TryGetValue(key, out var lines) && lines.Length > 0) return lines;
        Missing.Add(key);
        return Array.Empty<string>();
    }

    /// <summary>Следващата реплика от тестето за ключа, с името на Тут.</summary>
    public static string Pick(string key, string owner) => Lines.Pick(Get(key), owner);

    /// <summary>Името на Тут за {име} (взима се от записа).</summary>
    public static Func<string> Owner { get; set; } = () => "Тут";

    /// <summary>Следващата реплика от тестето за ключа ({име} = Тут).</summary>
    public static string Any(string key) => Lines.Fill(Lines.PickAny(Get(key)), Owner());

    public static Dictionary<string, string[]> Load(string path)
    {
        var result = new Dictionary<string, string[]>();
        if (!File.Exists(path)) return result;
        string? key = null;
        var current = new List<string>();
        void Flush()
        {
            if (key != null) result[key] = current.ToArray();
            current = new List<string>();
        }
        foreach (var raw in File.ReadAllLines(path))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("//")) continue;
            if (line.StartsWith("==="))
            {
                Flush();
                string head = line[3..];
                int bar = head.IndexOf('|');
                key = (bar >= 0 ? head[..bar] : head).Trim();
                continue;
            }
            if (key == null) continue;
            current.Add(line.Replace("{име}", "{0}").Replace("{брой}", "{1}").Replace("{храна}", "{2}"));
        }
        Flush();
        return result;
    }
}
