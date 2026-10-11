using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Mecho;

/// <summary>Едно нещо за мотивация: линк с име (бутон) или просто текст (Link = null).</summary>
public sealed record Motivation(string? Link, string Name);

/// <summary>
/// Листът за SOS: motivation.md в GitHub (редактира се от всякъде). Мечо го
/// сваля и пази копие в %AppData%\Mecho\motivation.md, за да работи и без мрежа.
/// </summary>
public static class Motivations
{
    private const string Url = "https://raw.githubusercontent.com/MrRein/bear_companion/HEAD/motivation.md";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static DateTime _fetchedAt = DateTime.MinValue;

    private static string CachePath => Path.Combine(SaveData.Folder, "motivation.md");

    /// <summary>Ако няма нито мрежа, нито копие.</summary>
    private static readonly Motivation[] Fallback =
    {
        new(null, "Не трябва да е перфектно. Трябва да е започнато."),
        new(null, "Направи едно малко нещо. После още едно. Мечо брои."),
        new("https://line-of-action.com/", "Бързи скици на пози (Line of Action)"),
    };

    private static readonly Regex MarkdownLink = new(@"^\[(?<name>.+?)\]\((?<url>https?://[^\s)]+)\)\s*$");

    /// <summary>Само редовете с тире („- …“ или „* …“); всичко друго се пропуска.</summary>
    public static List<Motivation> Parse(string text)
    {
        var list = new List<Motivation>();
        foreach (var raw in text.Split('\n'))
        {
            string line = raw.Trim();
            if (!(line.StartsWith("- ") || line.StartsWith("* "))) continue;
            line = line[2..].Trim();
            if (line.Length == 0) continue;

            var md = MarkdownLink.Match(line);
            if (md.Success)
            {
                list.Add(new Motivation(md.Groups["url"].Value, md.Groups["name"].Value.Trim()));
                continue;
            }
            string first = line.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries)[0].Trim('*', '<', '>');
            if (first.StartsWith("http://") || first.StartsWith("https://"))
            {
                string name = line.Length > line.IndexOf(' ') && line.Contains(' ')
                    ? line[(line.IndexOf(' ') + 1)..].Trim().Trim('*').Trim()
                    : "";
                if (name.Length == 0) name = new Uri(first).Host;
                list.Add(new Motivation(first, name));
            }
            else list.Add(new Motivation(null, line.Trim('*').Trim()));
        }
        return list;
    }

    /// <summary>Каквото има в копието (или резервния лист).</summary>
    public static List<Motivation> Load()
    {
        try
        {
            if (File.Exists(CachePath))
            {
                var list = Parse(File.ReadAllText(CachePath));
                if (list.Count > 0) return list;
            }
        }
        catch (Exception)
        {
            // Счупено копие: резервният лист.
        }
        return Fallback.ToList();
    }

    /// <summary>Сваля листа от GitHub (най-много веднъж на 10 минути). Връща true, ако е нов.</summary>
    public static async Task<bool> RefreshAsync()
    {
        if (DateTime.Now - _fetchedAt < TimeSpan.FromMinutes(10)) return false;
        _fetchedAt = DateTime.Now;
        try
        {
            string text = await Http.GetStringAsync(Url + "?t=" + DateTime.Now.Ticks);
            if (Parse(text).Count == 0) return false;
            Directory.CreateDirectory(SaveData.Folder);
            await File.WriteAllTextAsync(CachePath, text);
            return true;
        }
        catch (Exception)
        {
            return false;   // без мрежа: остава копието
        }
    }
}
