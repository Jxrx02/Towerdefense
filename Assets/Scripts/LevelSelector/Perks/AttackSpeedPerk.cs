using UnityEngine;
using LevelSelector.Definitions;
namespace LevelSelector.Perks
{
    [CreateAssetMenu(
        fileName = "AttackSpeedPerk",
        menuName = "TowerDefense/Loadout/Perks/Attack Speed")]
    public class AttackSpeedPerk : PerkDefinition
    {
        public float multiplier = 1.1f;

        public override void Apply(LevelLoadout loadout)
        {
            loadout.attackSpeedMultiplier *= multiplier;
        }
    }
}