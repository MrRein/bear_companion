using System.Collections.Generic;
using Mecho.Core;

namespace Mecho;

public sealed partial class PetWindow
{
    /// <summary>
    /// ПЕЧИЦАТА. Студено е, а е изключена → отива и я пуска сам. Стопли ли се → изключва я
    /// („не ми трябва вече“). Докато грее и е студено: сяда до нея, казва, че му е топло и
    /// приятно, че я обича. Бонус: до включената печица в студа се уморява по-малко.
    /// </summary>
    private sealed class HeaterItem : PlacedItem
    {
        public HeaterItem(PetWindow pet) : base(pet, Kitchen.Heater) { }

        public override bool Present => P._save.HeaterPlaced && Kitchen.Owns(P._save, Kitchen.Heater) && P._heaterWindow != null;

        private bool On => P._save.HeaterOn;

        /// <summary>Знаем ли времето (без него не решава дали му трябва).</summary>
        private bool Known => P._tempC != null;

        private bool Near => P._heaterWindow is { } w && System.Math.Abs(w.CenterX - P._x) < P.SpriteWidth * 3;

        public override IEnumerable<ItemAction> Actions()
        {
            bool cold = P.Cold, wants = P.WantsHeater;
            bool warmOutside = Known && (P.Hot || P._tempC >= Weather.Cool + 3);
            // Пуска я: студено е, а е изключена (спешно, ако наистина му трябва).
            yield return Act("turn_on", !On && cold ? (wants ? 6 : 2) : 0, TurnOn, urgent: wants);
            // Спира я: топло е вече.
            yield return Act("turn_off", On && warmOutside ? 5 : 0, TurnOff, urgent: P.Hot);
            // Топли се до нея.
            yield return Act("warm", On && cold && !Near ? 3 : 0, () => P.WarmUpByHeater());
            // Казва колко му е хубаво с нея / че я обича.
            yield return Act("cozy", On && cold && Near ? 2 : 0, () => Line("cozy"));
            yield return Act("love", 1, () => Line(On ? "love" : "love_off"));
            // Трябва ли му днес?
            yield return Act("need", Known && !On && !cold ? 1 : 0, () => Line("not_needed"));
        }

        private void TurnOn() => P.UseItem(P._heaterWindow, P.HeaterAnchor, "doing", 2.5, () =>
        {
            P.SetHeater(true);
            Line("turn_on", 5);
            P.Emote("heart", 2.5);
        });

        private void TurnOff() => P.UseItem(P._heaterWindow, P.HeaterAnchor, "doing", 2.5, () =>
        {
            P.SetHeater(false);
            Line("turn_off", 5);
        });

        public override void Bonus()
        {
            // До включената печица в студа: по-малко се уморява (×0.85), не му е студено.
            if (On && P.Cold && Near) Mods.Set(Source, Mecho.Core.Mods.EnergyDrain, 0.85);
            else Mods.Clear(Source);
        }
    }
}
