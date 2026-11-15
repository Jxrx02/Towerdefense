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
        public HeroWeaponConfig heroConfig;
        [Header("Weapon")]
        public Projectile projectile;

        [Header("Active")]
        public WeaponActiveDefinition active;

        [Header("Visuals")]
        public Sprite icon;

        [Header("Unlock & Purchase")]
        [Tooltip("Preis in Forschungspunkten. Die Freischaltung (Heldenlevel) steht in der PlayerProgressionDefinition. " +
                 "0 = kostenlos: die Waffe ist sofort nutzbar, sobald sie freigeschaltet ist.")]
        [Min(0)] public int purchaseCost;
    }
}