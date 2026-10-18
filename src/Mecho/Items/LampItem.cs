using System.Collections.Generic;
using Mecho.Core;

namespace Mecho;

public sealed partial class PetWindow
{
    /// <summary>
    /// ЛАМПАТА. Вечер на светлината му е уютно (казва го понякога, обича я). Денем му е
    /// излишна – пита дали да я прибере (сложена е от Тут – сам не я маха).
    /// Бонус: вечер до светещата лампа – по-малко се уморява.
    /// </summary>
    private sealed class LampItem : PlacedItem
    {
        public LampItem(PetWindow pet) : base(pet, Kitchen.Lamp) { }

        public override bool Present => P.LampOn && P._lampWindow != null;

        private static bool Dark => IsDark(System.DateTime.Now);

        public override IEnumerable<ItemAction> Actions()
        {
            yield return Act("cozy", Dark ? 2 : 0, () => Line("cozy"));
            yield return Act("love", 1, () => Line("love"));
            // Денем: „Да я прибера ли?“ – решава Тут.
            yield return Act("day", !Dark ? 1 : 0, AskPutAway);
        }

        private void AskPutAway()
        {
            if (!R.Has("item.lamp.day")) return;
            P.Ask(new ChatQuestion(R.Pick("item.lamp.day", P._save.OwnerName),
                new ChatOption("Прибери я", p => { p.SetLamp(false); p.Say(R.Pick("lamp.off", p.Save.OwnerName), 3); }),
                new ChatOption("Нека стои", p => p.Say(R.Pick("lamp.keep", p.Save.OwnerName), 3))));
        }

        public override void Bonus()
        {
            if (Dark) Mods.Set(Source, Mecho.Core.Mods.EnergyDrain, 0.95);
            else Mods.Clear(Source);
        }
    }
}
