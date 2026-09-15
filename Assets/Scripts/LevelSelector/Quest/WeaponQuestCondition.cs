using LevelSelector.Definitions;
using UnityEngine;

namespace LevelSelector.Quest
{
    [CreateAssetMenu(
        fileName = "QuestConditionWeapon",
        menuName = "TowerDefense/Levels/Conditions/Weapon")]
    public class WeaponQuestCondition : QuestCondition
    {
        public WeaponDefinition requiredWeapon;

        public override bool Evaluate(
            LevelLoadout loadout,
            LevelResult result)
        {
            return loadout.weapon == requiredWeapon;
        }
    }
}