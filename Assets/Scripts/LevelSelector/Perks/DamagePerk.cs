using UnityEngine;
using LevelSelector.Definitions;

namespace LevelSelector.Perks
{
    [CreateAssetMenu(
        fileName = "DamagePerk",
        menuName = "TowerDefense/Loadout/Perks/Damage")]
    public class DamagePerk : PerkDefinition
    {
        [Header("PerkEffect")]
        public float damageMultiplier = 1.1f;

        public override void Apply(LevelLoadout loadout)
        {
            loadout.damageMultiplier *= damageMultiplier;
        }
    }
}