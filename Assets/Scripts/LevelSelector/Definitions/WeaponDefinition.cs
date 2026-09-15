using TowerDefense;
using UnityEngine;

namespace LevelSelector.Definitions
{
    [CreateAssetMenu(
        fileName = "Weapon",
        menuName = "TowerDefense/Loadout/Weapon")]
    public class WeaponDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string weaponName;

        [TextArea(2, 5)]
        public string passiveDescription;

        [TextArea(2, 5)]
        public string activeDescription;

        [Header("Weapon")]
        public Projectile projectile;

        [Header("Active")]
        public WeaponActiveDefinition active;

        [Header("Visuals")]
        public Sprite icon;
    }
}