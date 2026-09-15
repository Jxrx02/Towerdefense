using LevelSelector.Definitions;
using UnityEngine;

namespace TowerDefense
{
    [CreateAssetMenu(
        fileName = "LightningActive",
        menuName = "TowerDefense/Weapons/Actives/Lightning")]
    public class LightningActive : WeaponActiveDefinition
    {
        public float damage = 100f;
        public float radius = 2f;

        public override void Execute(GameObject user)
        {
            // Lightning-Logik
        }
    }
}