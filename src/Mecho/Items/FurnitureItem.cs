using System.Collections.Generic;

namespace Mecho;

public sealed partial class PetWindow
{
    /// <summary>
    /// МЕБЕЛИТЕ (бюро, компютър, статив, игра, прежда, диван, постелка, легло – виж Furnishings.cs).
    /// Сложени от Тут: понякога казва, че ги обича или че му се иска да ги ползва
    /// („Само едно ниво…“), а вечер – че леглото го вика. Само приказки: самото занимание
    /// си го избира той (Почивай си / Работи си) или Тут.
    /// </summary>
    private sealed class FurnitureItem : PlacedItem
    {
        public FurnitureItem(PetWindow pet, string kind) : base(pet, kind) { }

        public override bool Present => P.FurniturePlaced(Kind) && P.FurnishingOf(Kind)?.Window != null
            && P.PlacementOf(Kind).Owner == Mecho.Core.PlacedBy.Player;

        private static bool Evening => System.DateTime.Now.Hour is >= 21 or < 5;

        public override IEnumerable<ItemAction> Actions()
        {
            yield return Act("love", 0.6, () => Line("love"));
            if (Kind == Kitchen.Bed) yield return Act("sleepy", Evening && P.IsExhausted ? 2 : 0, () => Line("sleepy"));
            else yield return Act("invite", 0.8, () => Line("invite"));
        }
    }
}
