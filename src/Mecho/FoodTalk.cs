using System;
using System.Collections.Generic;

namespace Mecho;

/// <summary>
/// Какво казва мечокът за всяко ядене поотделно: докато се готви (започва,
/// наглежда, сяда до уреда, тръгва за края, готово), като му го дадат, докато
/// дъвче или отпива и като свърши. Сокът не „загаря“, а пуканките не „кипят“.
/// </summary>
public sealed record FoodTalk(
    string[] Start, string[] During, string[] Check, string[] Sit, string[] Almost, string[] Pop,
    string[] Receive, string[] Chew, string[] Done)
{
    public static FoodTalk For(Food food) => For(food, food.Tool);

    /// <summary>
    /// За ястие и уред (при рецепти в два уреда първата стъпка е в другия уред).
    /// Старите ястия имат свои реплики за всичко; новите – за уреда (раздели
    /// tool.&lt;уред&gt;.start… във файла, {ястие} е името) и свои за яденето
    /// (food.&lt;ястие&gt;.receive / chew / done).
    /// </summary>
    public static FoodTalk For(Food food, string? tool)
    {
        if (All.TryGetValue(food.Id, out var t)) return t;
        string key = food.Id + "/" + tool;
        if (Built.TryGetValue(key, out var b)) return b;
        string name = food.Name.ToLowerInvariant();
        string[] T(string role) => tool == null ? Array.Empty<string>()
            : Array.ConvertAll(R.Get($"tool.{tool}.{role}"), l => l.Replace("{ястие}", name));
        string[] F(string role, string[] fallback) => R.Has($"food.{food.Id}.{role}") ? R.Get($"food.{food.Id}.{role}") : fallback;
        var plain = Plain(food);
        b = new FoodTalk(T("start"), T("during"), T("check"), T("sit"), T("almost"), T("pop"),
            F("receive", plain.Receive), F("chew", plain.Chew), F("done", plain.Done));
        Built[key] = b;
        return b;
    }

    private static readonly Dictionary<string, FoodTalk> Built = new();

    private static FoodTalk Plain(Food food) => new(
        new[] { "Готвя!", "Започвам!", "*запретва ръкави*" }, new[] { "*чака*", "Мирише хубаво.", "Скоро, скоро…" },
        new[] { "Да видя как върви.", "*наднича*" }, new[] { "Ще почакам тук.", "*сяда до уреда*" },
        new[] { "Почти е готово!", "Идвам!" }, new[] { "Готово!" }, food.Lines, new[] { "*ням*", "Мм!" }, new[] { "Мерси!", "Вкусно беше!" });

    private static readonly Dictionary<string, FoodTalk> All = new()
    {
        ["berries"] = new(
            Start: Array.Empty<string>(), During: Array.Empty<string>(), Check: Array.Empty<string>(),
            Sit: Array.Empty<string>(), Almost: Array.Empty<string>(), Pop: Array.Empty<string>(),
            Receive: R.Get("food.berries.receive"),
            Chew: R.Get("food.berries.chew"),
            Done: R.Get("food.berries.done")),

        ["popcorn"] = new(
            Start: R.Get("food.popcorn.start"),
            During: R.Get("food.popcorn.during"),
            Check: R.Get("food.popcorn.check"),
            Sit: R.Get("food.popcorn.sit"),
            Almost: R.Get("food.popcorn.almost"),
            Pop: R.Get("food.popcorn.pop"),
            Receive: R.Get("food.popcorn.receive"),
            Chew: R.Get("food.popcorn.chew"),
            Done: R.Get("food.popcorn.done")),

        ["meatballs"] = new(
            Start: R.Get("food.meatballs.start"),
            During: R.Get("food.meatballs.during"),
            Check: R.Get("food.meatballs.check"),
            Sit: R.Get("food.meatballs.sit"),
            Almost: R.Get("food.meatballs.almost"),
            Pop: R.Get("food.meatballs.pop"),
            Receive: R.Get("food.meatballs.receive"),
            Chew: R.Get("food.meatballs.chew"),
            Done: R.Get("food.meatballs.done")),

        ["potatoes"] = new(
            Start: R.Get("food.potatoes.start"),
            During: R.Get("food.potatoes.during"),
            Check: R.Get("food.potatoes.check"),
            Sit: R.Get("food.potatoes.sit"),
            Almost: R.Get("food.potatoes.almost"),
            Pop: R.Get("food.potatoes.pop"),
            Receive: R.Get("food.potatoes.receive"),
            Chew: R.Get("food.potatoes.chew"),
            Done: R.Get("food.potatoes.done")),

        ["coffee"] = new(
            Start: R.Get("food.coffee.start"),
            During: R.Get("food.coffee.during"),
            Check: R.Get("food.coffee.check"),
            Sit: R.Get("food.coffee.sit"),
            Almost: R.Get("food.coffee.almost"),
            Pop: R.Get("food.coffee.pop"),
            Receive: R.Get("food.coffee.receive"),
            Chew: R.Get("food.coffee.chew"),
            Done: R.Get("food.coffee.done")),

        ["tea"] = new(
            Start: R.Get("food.tea.start"),
            During: R.Get("food.tea.during"),
            Check: R.Get("food.tea.check"),
            Sit: R.Get("food.tea.sit"),
            Almost: R.Get("food.tea.almost"),
            Pop: R.Get("food.tea.pop"),
            Receive: R.Get("food.tea.receive"),
            Chew: R.Get("food.tea.chew"),
            Done: R.Get("food.tea.done")),

        ["juice_orange"] = new(
            Start: R.Get("food.juice_orange.start"),
            During: R.Get("food.juice_orange.during"),
            Check: R.Get("food.juice_orange.check"),
            Sit: R.Get("food.juice_orange.sit"),
            Almost: R.Get("food.juice_orange.almost"),
            Pop: R.Get("food.juice_orange.pop"),
            Receive: R.Get("food.juice_orange.receive"),
            Chew: R.Get("food.juice_orange.chew"),
            Done: R.Get("food.juice_orange.done")),

        ["juice_berry"] = new(
            Start: R.Get("food.juice_berry.start"),
            During: R.Get("food.juice_berry.during"),
            Check: R.Get("food.juice_berry.check"),
            Sit: R.Get("food.juice_berry.sit"),
            Almost: R.Get("food.juice_berry.almost"),
            Pop: R.Get("food.juice_berry.pop"),
            Receive: R.Get("food.juice_berry.receive"),
            Chew: R.Get("food.juice_berry.chew"),
            Done: R.Get("food.juice_berry.done")),
    };
}
