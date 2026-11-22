using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Mecho.Core;

namespace Mecho;

// Фаза 2 – мнението в момента: преди да сготви/облече нещо, Мечо си има мнение.
// Нищо не се заключва: ако има какво да каже (мрънка или отказва) – диалогче с опции;
// „Хайде де!“/„Моля те!“ го прави, „Добре, друго“ връща към избора. Без реплика за
// причината – просто го прави. Логиката е в ядрото (OpinionModule), таговете – в
// assets/opinion.json, репликите – opinion.* в repliki.txt.
public sealed partial class PetWindow
{
    private static Dictionary<string, HashSet<string>>? _opinionTags;

    /// <summary>Самопроверка: мнението се пита и докато тече (иначе тестовете не го пипат).</summary>
    internal bool OpinionInSelfTest { get; set; }

    /// <summary>Таговете от assets/opinion.json: „food:pizza“ → {heavy, complex}.</summary>
    private static Dictionary<string, HashSet<string>> OpinionTags
    {
        get
        {
            if (_opinionTags != null) return _opinionTags;
            var tags = new Dictionary<string, HashSet<string>>();
            try
            {
                string path = Path.Combine(AppContext.BaseDirectory, "assets", "opinion.json");
                if (File.Exists(path))
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(path),
                        new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                    foreach (var group in doc.RootElement.EnumerateObject())
                    {
                        string prefix = group.Name == "clothes" ? "cloth" : group.Name;
                        foreach (var tag in group.Value.EnumerateObject())
                            foreach (var id in tag.Value.EnumerateArray())
                            {
                                string key = prefix + ":" + id.GetString();
                                if (!tags.TryGetValue(key, out var set)) tags[key] = set = new HashSet<string>();
                                set.Add(tag.Name);
                            }
                    }
                }
            }
            catch (Exception ex) { CrashLog.Write(ex); }
            return _opinionTags = tags;
        }
    }

    internal static bool HasOpinionTag(string code, string tag) => TagsOf(code).Contains(tag);

    private static IReadOnlySet<string> TagsOf(string code) => OpinionTags.TryGetValue(code, out var t) ? t : new HashSet<string>();

    /// <summary>Ястията, които могат да са днешният каприз (знае ги и има уреда; без боровинките).</summary>
    internal IReadOnlyList<string> WhimFoods() =>
        Kitchen.Foods.Where(f => f.Cost > 0 && Kitchen.CanCook(f, _save)).Select(f => f.Id).ToList();

    private Mecho.Core.OpinionModule? Opinion => Mecho.Engine.CoreSetup.Opinion;

    /// <summary>
    /// Пита мнението. true – показа диалогче (действието чака отговора); false – давай направо.
    /// proceed – когато се съгласи; other – „Добре, друго“.
    /// </summary>
    private bool OpinionGate(OpinionInput input, Action proceed, Action other)
    {
        if (Opinion is not { } op || SelfTest.Running && !OpinionInSelfTest) return false;
        var result = op.Evaluate(input);
        if (result.Stance is not (Stance.Grumbles or Stance.Refuses) || result.Reason is not { } why || !R.Has(why.Key))
        {
            CoreEngine.Bus.Send(new OpinionShown(result.Stance is Stance.Refuses ? Stance.Grumbles : result.Stance));
            return false;
        }
        CoreEngine.Bus.Send(new OpinionShown(result.Stance));
        string line = R.Text(why.Key, _save.OwnerName, why.Facts.Select(kv => (kv.Key, kv.Value)).ToArray());
        bool refuses = result.Stance == Stance.Refuses;
        Ask(new ChatQuestion(line,
            new ChatOption(refuses ? "Моля те!" : "Хайде де!", p => p.Persuaded(refuses, proceed)),
            new ChatOption("Добре, друго", _ => other())));
        return true;
    }

    /// <summary>Съгласи се: мрънка и го прави (след отказ – после се цупи малко).</summary>
    private void Persuaded(bool refused, Action proceed)
    {
        if (R.Has("opinion.persuaded")) Say(R.Pick("opinion.persuaded", _save.OwnerName), 3);
        proceed();
        if (refused && R.Has("opinion.sulk")) Later(12, () => Mutter(R.Pick("opinion.sulk", _save.OwnerName), 4));
    }

    /// <summary>Мнението за готвене на ястие.</summary>
    private OpinionInput CookOpinion(Food food)
    {
        var (y, gap) = LastDay();
        bool yesterday = y != null && gap == 1 && y.Foods.Contains(food.Id);
        return new OpinionInput("cook", food.Id, TagsOf("food:" + food.Id), DateTime.Now, _save.Fullness, _save.Energy,
            Wishes().Contains(food), TodayRec.Foods.Contains(food.Id), yesterday, Temperature, food.Name);
    }

    /// <summary>Мнението за обличане на дреха.</summary>
    private OpinionInput WearOpinion(Garment g) =>
        new("wear", g.Id, TagsOf("cloth:" + g.Id), DateTime.Now, _save.Fullness, _save.Energy, false, false, false, Temperature, g.Name);
}
