using UnityEngine;
using LevelSelector.Definitions;

namespace LevelSelector.Mutators
{
    [CreateAssetMenu(
        fileName = "NoHealing",
        menuName = "TowerDefense/Loadout/Mutators/No Healing")]
    public class NoHealingMutator : MutatorDefinition
    {
        public override void Apply(LevelLoadout loadout)
        {
            loadout.healingEnabled = false;
        }
    }
}