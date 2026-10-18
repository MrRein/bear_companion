using System.Collections.Generic;

namespace Mecho;

public sealed partial class PetWindow
{
    /// <summary>
    /// РАСТЕНИЕТО (саксията, сложена от Тут). Отива да се погрижи за него (кътчето „Грижи се
    /// за растението“), гордее се колко е пораснало, радва се, когато цъфне.
    /// Бонус: цъфналото растение носи късмет – малко по-чести подаръци.
    /// </summary>
    private sealed class PlantItem : PlacedItem
    {
        public PlantItem(PetWindow pet) : base(pet, Kitchen.PlantPot) { }

        public override bool Present => P._save.PlantPlaced && Kitchen.Owns(P._save, Kitchen.PlantPot);

        private bool Bloomed => P._save.PlantStage >= PlantStages - 1;

        public override IEnumerable<ItemAction> Actions()
        {
            yield return Act("care", P.CanDo(Scenes.Plant) && !Bloomed ? 2 : 0, () =>
            {
                Line("care_go", 3);
                P.StartScene(Scenes.Plant, 120 + P._rng.Next(120));
            });
            yield return Act("proud", P._save.PlantStage >= 3 ? 1 : 0, () => Line(Bloomed ? "bloom" : "proud"));
            yield return Act("love", 1, () => Line("love"));
        }

        public override void Bonus()
        {
            if (Bloomed) Mods.Set(Source, Mecho.Core.Mods.GiftChance, 1.1);
            else Mods.Clear(Source);
        }
    }
}
