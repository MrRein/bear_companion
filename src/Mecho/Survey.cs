using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Mecho;

/// <summary>
/// Въпросникът (assets/anketi.txt): мечокът пита нещо за Тут („Как предпочиташ да
/// рисуваш?“), тя избира бутон или пише своя отговор. Мечокът го запомня и после
/// понякога си спомня („Още мисля, че обичаш молива!“). Форматът е като на
/// razgovori.txt:
///
///   === id | за какво е (тема)
///   ? Въпросът ({име} е Тут)
///   - 😊 Отговор | Какво казва мечокът на това
///   * Какво казва на написан отговор ({отговор} е текстът)
///   > Какво казва после, като си спомни ({отговор} – каквото е отговорила)
///   > [Отговор] Само ако е избрала точно този бутон
///
/// Всички отговори се пишат и в otgovori.txt до записа – него може да го пратиш,
/// за да се добавят нови реплики за нещата, които Тут е казала.
/// </summary>
public static class Survey
{
    public sealed record Option(string Label, string Reply);

    public sealed record Remark(string? Only, string Text);

    public sealed record Question(string Id, string Topic, string Text, Option[] Options, string[] FreeReplies, Remark[] Remarks);

    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "assets", "anketi.txt");

    /// <summary>Отговорите на Тут, четими (за да се пратят и да се добавят реплики).</summary>
    public static string AnswersPath => Path.Combine(SaveData.Folder, "otgovori.txt");

    private static Dictionary<string, Question>? _all;

    public static IReadOnlyDictionary<string, Question> All => _all ??= Load(FilePath);

    public static int Reload()
    {
        _all = Load(FilePath);
        return _all.Count;
    }

    public static Dictionary<string, Question> Load(string path)
    {
        var result = new Dictionary<string, Question>();
        if (!File.Exists(path)) return result;
        string? id = null, topic = null, text = null;
        var options = new List<Option>();
        var free = new List<string>();
        var remarks = new List<Remark>();
        void Flush()
        {
            if (id != null && text != null && (options.Count > 0 || free.Count > 0))
                result[id] = new Question(id, topic ?? id, text, options.ToArray(), free.ToArray(), remarks.ToArray());
            id = topic = text = null;
            options.Clear();
            free.Clear();
            remarks.Clear();
        }
        foreach (var raw in File.ReadAllLines(path))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("//")) continue;
            if (line.StartsWith("==="))
            {
                Flush();
                var head = line[3..].Split('|', 2);
                id = head[0].Trim();
                topic = head.Length > 1 ? head[1].Trim() : id;
            }
            else if (id == null) continue;
            else if (line.StartsWith('?')) text = line[1..].Trim();
            else if (line.StartsWith('-') && line.Contains('|'))
            {
                int bar = line.IndexOf('|');
                options.Add(new Option(line[1..bar].Trim(), line[(bar + 1)..].Trim()));
            }
            else if (line.StartsWith('*')) free.Add(line[1..].Trim());
            else if (line.StartsWith('>'))
            {
                string r = line[1..].Trim();
                string? only = null;
                if (r.StartsWith('[') && r.IndexOf(']') is int end and > 0)
                {
                    only = r[1..end].Trim();
                    r = r[(end + 1)..].Trim();
                }
                remarks.Add(new Remark(only, r));
            }
        }
        Flush();
        return result;
    }

    /// <summary>Етикетът без емоджито отпред (за сравнение и за {отговор}).</summary>
    public static string Plain(string label)
    {
        var e = System.Globalization.StringInfo.GetTextElementEnumerator(label);
        var sb = new StringBuilder();
        while (e.MoveNext())
        {
            string el = e.GetTextElement();
            if (sb.Length == 0 && (el.Length > 1 || char.IsSymbol(el[0]) || el[0] == '️' || el == " ")) continue;
            sb.Append(el);
        }
        return sb.ToString().Trim();
    }

    /// <summary>Въпрос, на който още не е отговорила (случаен), или null.</summary>
    public static Question? NextUnanswered(SaveData s, Random rng)
    {
        var open = All.Values.Where(q => !s.Answers.ContainsKey(q.Id)).ToList();
        return open.Count == 0 ? null : open[rng.Next(open.Count)];
    }

    /// <summary>Въпросът с бутоните и полето за свободен отговор.</summary>
    public static ChatQuestion Ask(PetWindow pet, Question q)
    {
        string owner = pet.Save.OwnerName;
        string Fill(string line, string answer) => line.Replace("{име}", owner).Replace("{отговор}", answer);
        var options = q.Options.Select(o => new ChatOption(o.Label, p =>
        {
            p.RememberAnswer(q, Plain(o.Label), free: false);
            string reply = Fill(o.Reply, Plain(o.Label));
            p.Say(reply, 4 + reply.Length * 0.03);
        })).ToList();
        options.Add(new ChatOption("Не сега", p => p.Say("Добре, ще те питам друг път!", 3)));
        return new ChatQuestion(Fill(q.Text, ""), options.ToArray())
        {
            FreeText = (p, text) =>
            {
                p.RememberAnswer(q, text, free: true);
                string reply = q.FreeReplies.Length > 0 ? Fill(Lines.PickAny(q.FreeReplies), text) : $"„{text}“ – запомних!";
                p.Say(reply, 4 + reply.Length * 0.03);
            },
        };
    }

    /// <summary>Нещо, което казва, като си спомни отговора (или null, ако няма подходящо).</summary>
    public static string? RemarkFor(SaveData s, Random rng)
    {
        var pool = new List<(Question Q, Remark R, SurveyAnswer A)>();
        foreach (var (id, a) in s.Answers)
        {
            if (!All.TryGetValue(id, out var q)) continue;
            foreach (var r in q.Remarks)
            {
                // Реплика за конкретен бутон: само ако е избран точно той.
                if (r.Only != null && (a.Free || !string.Equals(Plain(r.Only), a.Text, StringComparison.OrdinalIgnoreCase))) continue;
                pool.Add((q, r, a));
            }
        }
        if (pool.Count == 0) return null;
        var pick = pool[rng.Next(pool.Count)];
        return pick.R.Text.Replace("{име}", s.OwnerName).Replace("{отговор}", pick.A.Text);
    }

    /// <summary>Записва всички отговори в otgovori.txt (въпрос, отговор, кога).</summary>
    public static void Export(SaveData s)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("// Отговорите на Тут от въпросника на Мечо. Прати този файл, за да се добавят");
            sb.AppendLine("// реплики за нещата, които е казала (виж assets/anketi.txt).");
            sb.AppendLine();
            foreach (var (id, a) in s.Answers.OrderBy(x => x.Value.At))
            {
                All.TryGetValue(id, out var q);
                sb.AppendLine($"=== {id} | {q?.Topic ?? id}");
                sb.AppendLine($"? {q?.Text.Replace("{име}", s.OwnerName) ?? ""}");
                sb.AppendLine($"{(a.Free ? "написа" : "избра")}: {a.Text}");
                sb.AppendLine($"кога: {a.At:yyyy-MM-dd HH:mm}");
                sb.AppendLine();
            }
            Directory.CreateDirectory(SaveData.Folder);
            File.WriteAllText(AnswersPath, sb.ToString());
        }
        catch (Exception)
        {
            // Файлът е за удобство – записът (save.json) пази отговорите така или иначе.
        }
    }
}

public sealed partial class PetWindow
{
    /// <summary>Запомня отговора от въпросника (в записа и в otgovori.txt).</summary>
    internal void RememberAnswer(Survey.Question q, string text, bool free)
    {
        _save.Answers[q.Id] = new SurveyAnswer { Text = text, Free = free, At = DateTime.Now };
        Persist();
        Survey.Export(_save);
        Emote("heart", 2.5);
    }

    /// <summary>
    /// При цъкване: понякога (до 2 пъти на ден) пита от въпросника вместо обикновен разговор.
    /// </summary>
    internal ChatQuestion? MaybeSurvey()
    {
        string today = DayKey(DateTime.Today);
        if (_save.SurveyDay != today)
        {
            _save.SurveyDay = today;
            _save.SurveysToday = 0;
        }
        if (_save.SurveysToday >= 2 || _rng.Next(100) >= 30) return null;
        var q = Survey.NextUnanswered(_save, _rng);
        if (q == null) return null;
        _save.SurveysToday++;
        return Survey.Ask(this, q);
    }

    private double _nextRemark = 1800;

    /// <summary>Понякога (до 3 пъти на ден) си спомня какво му е казала Тут.</summary>
    private void UpdateRemarks(double now)
    {
        if (now < _nextRemark) return;
        _nextRemark = now + Minutes(45, 120);
        if (_save.Answers.Count == 0 || IsQuiet || InFocus || IsAsleep || IsResting || ChatIsOpen) return;
        string today = DayKey(DateTime.Today);
        if (_save.RemarkDay != today)
        {
            _save.RemarkDay = today;
            _save.RemarksToday = 0;
        }
        if (_save.RemarksToday >= 3 || _rng.Next(3) > 0) return;
        if (Survey.RemarkFor(_save, _rng) is not { } line) return;
        _save.RemarksToday++;
        Mutter(line, 5);
    }
}
