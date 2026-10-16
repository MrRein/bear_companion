using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Mecho;

/// <summary>
/// Обикновените разговори от assets/razgovori.txt (без програмиране – само текст):
///
///   === id | описание
///   ? Въпросът на мечока ({име} е Тут)
///   - 😊 Отговор | Какво казва мечокът на това
///   - 🙈 Друг отговор | Какво казва мечокът >> друго_id   (после пита и „друго_id“)
///
/// Разговор с id, което почва с „_“, не се пита сам – само като продължение.
/// </summary>
public static class Talks
{
    public sealed record Option(string Label, string Reply, string? Next);

    public sealed record Talk(string Id, string Question, Option[] Options);

    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "assets", "razgovori.txt");

    private static Dictionary<string, Talk>? _all;

    public static IReadOnlyDictionary<string, Talk> All => _all ??= Load(FilePath);

    /// <summary>Тези, които се питат сами (без „_“ отпред).</summary>
    public static Talk[] TopLevel => All.Values.Where(t => !t.Id.StartsWith('_')).ToArray();

    public static Dictionary<string, Talk> Load(string path)
    {
        var result = new Dictionary<string, Talk>();
        if (!File.Exists(path)) return result;
        string? id = null, question = null;
        var options = new List<Option>();
        void Flush()
        {
            if (id != null && question != null && options.Count >= 2) result[id] = new Talk(id, question, options.ToArray());
            id = question = null;
            options.Clear();
        }
        foreach (var raw in File.ReadAllLines(path))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("//")) continue;
            if (line.StartsWith("==="))
            {
                Flush();
                id = line[3..].Split('|')[0].Trim();
            }
            else if (line.StartsWith('?')) question = line[1..].Trim();
            else if (line.StartsWith('-') && line.Contains('|'))
            {
                int bar = line.IndexOf('|');
                string label = line[1..bar].Trim(), reply = line[(bar + 1)..].Trim();
                string? next = null;
                int arrow = reply.LastIndexOf(">>", StringComparison.Ordinal);
                if (arrow >= 0)
                {
                    next = reply[(arrow + 2)..].Trim();
                    reply = reply[..arrow].Trim();
                }
                options.Add(new Option(label, reply, next));
            }
        }
        Flush();
        return result;
    }

    /// <summary>Въпросът с бутоните; избраният отговор: мечокът казва своето (и евентуално пита следващото).</summary>
    public static ChatQuestion Ask(PetWindow pet, Talk talk)
    {
        string owner = pet.Save.OwnerName;
        return new ChatQuestion(talk.Question.Replace("{име}", owner), talk.Options.Select(o => new ChatOption(o.Label, p =>
        {
            string reply = o.Reply.Replace("{име}", owner);
            p.Say(reply, 4 + reply.Length * 0.03);
            if (o.Next != null && All.TryGetValue(o.Next, out var next)) p.AskAfter(3.5 + reply.Length * 0.04, Ask(p, next));
        })).ToArray());
    }
}
