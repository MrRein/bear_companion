using System;
using System.Collections.Generic;
using System.Linq;
using Mecho.Core;

namespace Mecho;

/// <summary>
/// ВЗАИМОДЕЙСТВИЯ С ПРЕДМЕТИТЕ (Interactions). Мечокът знае какво е сложено и понякога
/// прави нещо с него: пуска/спира печицата, казва, че му е топло, че я обича, дали му
/// трябва; пълни хранилката; спира радиото вечер… Всеки предмет е отделен файл в тази
/// папка (HeaterItem, LampItem, RadioItem, FeederItem, PlantItem) с:
///   • Present – сложен ли е на земята;
///   • Actions() – какво би направил сега и колко му се иска (тегло; 0 – не сега);
///   • Bonus()   – какво му дава, докато е сложен (модификатори под източник „item.&lt;вид&gt;“).
/// Колко често – решава ядрото (InteractionsModule: поне 15 мин между две, 40 мин за един
/// предмет, 3 ч за едно и също, до 12 на ден), за да не е досаден.
///
/// Нов предмет: файл XItem.cs (клас XItem : PlacedItem), ред в InitItems, реплики
/// „item.x.…“ в repliki.txt. Тестът в SelfTest минава през всички предмети сам.
/// </summary>
public sealed partial class PetWindow
{
    /// <summary>Едно действие с предмет: какво, колко му се иска, и самото действие.</summary>
    private sealed record ItemAction(string Id, double Weight, Action Run, bool Urgent = false);

    /// <summary>Общото за всички предмети на земята.</summary>
    private abstract class PlacedItem
    {
        protected PlacedItem(PetWindow pet, string kind)
        {
            P = pet;
            Kind = kind;
        }

        protected PetWindow P { get; }

        /// <summary>Видът (Kitchen.Heater, Kitchen.Lamp…).</summary>
        public string Kind { get; }

        /// <summary>Сложен ли е на земята сега.</summary>
        public abstract bool Present { get; }

        /// <summary>Какво би направил сега (тегло 0 – не сега).</summary>
        public abstract IEnumerable<ItemAction> Actions();

        /// <summary>Бонусите, докато е сложен (модификатори под „item.&lt;вид&gt;“). Вика се на 5 секунди.</summary>
        public virtual void Bonus() { }

        /// <summary>Тут току-що го сложи: обикновено казва нещо (item.&lt;вид&gt;.placed), понякога не.</summary>
        public virtual void Placed()
        {
            if (P._rng.NextDouble() < PlacedCommentChance) Line("placed", 4);
        }

        /// <summary>Колко често коментира сложеното (не всеки път, за да не е досаден).</summary>
        protected virtual double PlacedCommentChance => 0.75;

        protected string Source => "item." + Kind;

        protected ModifierStack Mods => P.CoreEngine.Modifiers;

        /// <summary>Реплика от repliki.txt („item.&lt;вид&gt;.&lt;какво&gt;“), ако я има.</summary>
        protected void Line(string what, double seconds = 4)
        {
            string key = $"item.{Kind}.{what}";
            if (R.Has(key)) P.Say(R.Pick(key, P._save.OwnerName), seconds);
        }

        protected ItemAction Act(string id, double weight, Action run, bool urgent = false) => new(id, weight, run, urgent);
    }

    private PlacedItem[] _items = Array.Empty<PlacedItem>();
    private double _nextItemCheck = 90, _nextItemBonus;

    private void InitItems()
    {
        // Тут сложи нещо: мечокът го забелязва и (обикновено) казва нещо – след миг, като се появи.
        CoreEngine.Bus.Subscribe<ItemPlaced>(this, s =>
        {
            if (s.By != PlacedBy.Player) return;
            Later(1.2, () =>
            {
                if (IsAsleep || IsQuiet || InFocus) return;
                _items.FirstOrDefault(i => i.Kind == s.Kind && i.Present)?.Placed();
            });
        });
        _items = new PlacedItem[]
        {
            new HeaterItem(this),
            new LampItem(this),
            new RadioItem(this),
            new FeederItem(this),
            new PlantItem(this),
        }.Concat(FurnitureKinds.Select(k => (PlacedItem)new FurnitureItem(this, k))).ToArray();
    }

    /// <summary>Свободен ли е да се занимае с предмет (не работи, не спи, не яде, не е зает…).</summary>
    private bool FreeForItems =>
        _state == BearState.Idle && !_inScene && !_onTrampoline && !InFocus && !IsQuiet && !IsAsleep && !Cooks
        && _eating == null && !OnWalk && !WaitingForFood && !ChatIsOpen && !MenuIsOpen && CanAnimateFreely && !SelfTest.Running;

    /// <summary>На кадър: бонусите (на 5 с) и понякога (на 1–2 мин) едно взаимодействие, ако ядрото разреши.</summary>
    private void UpdateItems(double now)
    {
        if (now >= _nextItemBonus)
        {
            _nextItemBonus = now + 5;
            foreach (var item in _items)
                if (item.Present) item.Bonus();
                else CoreEngine.Modifiers.Clear("item." + item.Kind);
        }
        if (now < _nextItemCheck) return;
        _nextItemCheck = now + 60 + _rng.Next(60);
        if (!FreeForItems) return;
        var options = _items.Where(i => i.Present)
            .SelectMany(i => i.Actions().Where(a => a.Weight > 0).Select(a => (Item: i, Action: a)))
            .OrderByDescending(o => o.Action.Urgent)
            .ToList();
        if (options.Count == 0) return;
        // Спешното (студено, а печицата е изключена) – първо; иначе случайно по тегло.
        var pick = options[0].Action.Urgent ? options[0] : WeightedPick(options);
        RunItem(pick.Item, pick.Action);
    }

    private (PlacedItem Item, ItemAction Action) WeightedPick(List<(PlacedItem Item, ItemAction Action)> options)
    {
        double roll = _rng.NextDouble() * options.Sum(o => o.Action.Weight);
        foreach (var o in options)
        {
            roll -= o.Action.Weight;
            if (roll <= 0) return o;
        }
        return options[^1];
    }

    /// <summary>Прави го, ако ядрото казва, че не е твърде скоро.</summary>
    private bool RunItem(PlacedItem item, ItemAction action)
    {
        if (!CoreEngine.Bus.Send(new TryInteract(item.Kind, action.Id, action.Urgent)).Ok) return false;
        action.Run();
        return true;
    }

    /// <summary>Конкретно действие с предмет (времето вика „печицата: включи“). Само ако сега има смисъл.</summary>
    private bool TryItem(string kind, string actionId)
    {
        if (SelfTest.Running) return false;   // самопроверката проверява действията поотделно (без разходки)
        if (_items.FirstOrDefault(i => i.Kind == kind) is not { Present: true } item) return false;
        var action = item.Actions().FirstOrDefault(a => a.Id == actionId && a.Weight > 0);
        return action != null && RunItem(item, action);
    }

    /// <summary>
    /// Отива до предмета и прави нещо с лапите (включва, пълни…), после done. Ако предметът
    /// го няма – нищо. Анимацията е само до предмета (котва).
    /// </summary>
    private void UseItem(CookerWindow? window, ItemAnchor anchor, string anim, double seconds, Action done)
    {
        if (window == null) return;
        if (_inScene) LeaveScene();
        double cx = window.CenterX;
        int side = _x < cx ? -1 : 1;
        WalkTo(cx + side * (SpriteWidth * 0.45 + 4 * PixelSize), () =>
        {
            _facingLeft = side > 0;
            PlayNear(anchor, anim, length: seconds, after: done);
        });
    }

    /// <summary>Developer: прави сега най-желаното действие с предмета (без чакане между тях).</summary>
    internal void DevItemAction(string kind)
    {
        if (_items.FirstOrDefault(i => i.Kind == kind) is not { Present: true } item)
        {
            Say("Не е сложен на земята.", 2);
            return;
        }
        var best = item.Actions().Where(a => a.Weight > 0).OrderByDescending(a => a.Weight).FirstOrDefault();
        if (best == null) Say("Сега няма какво да правя с него.", 2);
        else best.Run();
    }

    /// <summary>Самопроверка: всички предмети и действията им (без да ги прави).</summary>
    internal IEnumerable<(string Kind, string Action, double Weight)> ItemActionsForTest() =>
        _items.Where(i => i.Present).SelectMany(i => i.Actions().Select(a => (i.Kind, a.Id, a.Weight)));
}
