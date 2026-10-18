using System.Collections.Generic;
using Mecho.Core;

namespace Mecho;

public sealed partial class PetWindow
{
    /// <summary>
    /// РАДИОТО. Сложено, но тихо → понякога отива и пуска музика (денем). Късно вечер
    /// (след 22 ч) го спира – „стига музика за днес“. Обича го и казва коя песен му харесва.
    /// Бонус: докато свири – по-бодър (уморява се малко по-малко).
    /// </summary>
    private sealed class RadioItem : PlacedItem
    {
        public RadioItem(PetWindow pet) : base(pet, Kitchen.Radio) { }

        public override bool Present => P.RadioPlaced && P._radioWindow != null;

        private static bool Late => System.DateTime.Now.Hour is >= 22 or < 7;

        public override IEnumerable<ItemAction> Actions()
        {
            bool playing = P.RadioPlaying;
            yield return Act("turn_on", !playing && !Late ? 2 : 0, TurnOn);
            yield return Act("turn_off", playing && Late ? 4 : 0, TurnOff);
            yield return Act("love", 1, () => Line(playing ? "love" : "love_off"));
            yield return Act("song", playing ? 1 : 0, () => Line("song"));
        }

        private void TurnOn() => P.UseItem(P._radioWindow, P.RadioAnchor, "doing", 2, () =>
        {
            P.SetRadioOn(true);
            Line("turn_on", 4);
        });

        private void TurnOff() => P.UseItem(P._radioWindow, P.RadioAnchor, "doing", 2, () =>
        {
            P.SetRadioOn(false);
            Line("turn_off", 4);
        });

        public override void Bonus()
        {
            if (P.RadioPlaying) Mods.Set(Source, Mecho.Core.Mods.EnergyDrain, 0.95);
            else Mods.Clear(Source);
        }
    }
}
