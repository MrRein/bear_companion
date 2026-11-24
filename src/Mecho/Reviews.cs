using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Mecho;

/// <summary>
/// Ревютата на завършените проекти: assets/reviews.txt (пише ги разработчикът).
///
///   === boardgame-20261124-1 | Горски тайни
///   Зайчето | 5 | Играхме я цяла нощ и пак искам!
///   Бухалът | 4 | Правилата са хитри. Картинките – прекрасни.
///
/// Всеки завършен проект има код (FameEntry.Id); кодовете и всичко за проектите се
/// записват в „Завършени проекти.txt“ до записа (и Developer → „Завършени проекти: копирай“),
/// за да се види какво е направено и какво да се напише.
/// </summary>
public static class Reviews
{
    public sealed record Review(string Author, int Stars, string Text);

    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "assets", "reviews.txt");

    /// <summary>Ревютата за проекта с този код (чете се наново всеки път – файлът е малък).</summary>
    public static IReadOnlyList<Review> For(string id)
    {
        var result = new List<Review>();
        if (string.IsNullOrEmpty(id) || !File.Exists(FilePath)) return result;
        try
        {
            bool mine = false;
            foreach (var raw in File.ReadAllLines(FilePath))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("//")) continue;
                if (line.StartsWith("==="))
                {
                    string head = line[3..];
                    int bar = head.IndexOf('|');
                    mine = (bar >= 0 ? head[..bar] : head).Trim() == id;
                    continue;
                }
                if (!mine) continue;
                var parts = line.Split('|', 3);
                if (parts.Length == 3 && int.TryParse(parts[1].Trim(), out int stars))
                    result.Add(new Review(parts[0].Trim(), Math.Clamp(stars, 0, 5), parts[2].Trim()));
                else result.Add(new Review("", 0, line));
            }
        }
        catch (Exception ex) { CrashLog.Write(ex); }
        return result;
    }

    /// <summary>Нов код за завършен проект: вид-дата-пореден.</summary>
    public static string NewId(string type, IEnumerable<FameEntry> all)
    {
        string stem = $"{type}-{DateTime.Today:yyyyMMdd}";
        int n = 1;
        while (all.Any(f => f.Id == $"{stem}-{n}")) n++;
        return $"{stem}-{n}";
    }

    /// <summary>Всичко за завършените проекти като текст (за разработчика – да напише ревюта).</summary>
    public static string Describe(IEnumerable<FameEntry> all)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// Завършените проекти на Мечо. Ревютата се пишат в assets/reviews.txt под кода (=== код | име).");
        foreach (var f in all)
        {
            sb.AppendLine();
            sb.AppendLine($"=== {f.Id} | {(f.Name.Length > 0 ? f.Name : f.Kind)}");
            sb.AppendLine($"// {f.Kind}, завършен {f.Done}" + (f.Started.Length > 0 ? $" (почнат {f.Started})" : ""));
            foreach (var fact in f.Facts) sb.AppendLine("// " + fact);
            if (f.WorkMinutes > 0 || f.Tasks > 0) sb.AppendLine($"// {f.WorkMinutes / 60:0.#} ч работа, {f.Tasks} задачи, {f.Items} награди ({f.Rare} редки)");
        }
        return sb.ToString();
    }

    /// <summary>Записва „Завършени проекти.txt“ до записа (при всеки завършен проект).</summary>
    public static void Export(IEnumerable<FameEntry> all)
    {
        try { File.WriteAllText(Path.Combine(SaveData.Folder, "Завършени проекти.txt"), Describe(all), Encoding.UTF8); }
        catch (Exception ex) { CrashLog.Write(ex); }
    }
}
