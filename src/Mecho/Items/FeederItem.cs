using System.Collections.Generic;

namespace Mecho;

public sealed partial class PetWindow
{
    /// <summary>
    /// ХРАНИЛКАТА. Празна → отива и я пълни сам (понякога). Има птичка → радва се и казва
    /// нещо за нея. Обича да гледа птичките.
    /// </summary>
    private sealed class FeederItem : PlacedItem
    {
        public FeederItem(PetWindow pet) : base(pet, Kitchen.Feeder) { }

        public override bool Present => P.FeederPlaced && P._feederWindow != null;

        public override IEnumerable<ItemAction> Actions()
        {
            bool empty = P._save.FeederFood <= 0;
            yield return Act("fill", empty ? 3 : 0, () =>
            {
                Line("fill_go", 3);
                P.FillFeeder();
            });
            yield return Act("bird", P._birdPhase > 0 ? 3 : 0, () =>
            {
                Line("bird");
                P.Emote("heart", 2.5);
            });
            yield return Act("love", 1, () => Line(empty ? "empty" : "love"));
        }
    }
}
