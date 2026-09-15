using UnityEngine;
using LevelSelector.Definitions;
namespace LevelSelector.Mutators
{
    [CreateAssetMenu(
        fileName = "MoreEnemies",
        menuName = "TowerDefense/Loadout/Mutators/More Enemies")]
    public class MoreEnemiesMutator : MutatorDefinition
    {
        [Range(1f, 5f)]
        public float enemyMultiplier = 2f;

        public override void Apply(LevelLoadout loadout)
        {
            loadout.enemyCountMultiplier *= enemyMultiplier;
        }
    }
}