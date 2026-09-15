using LevelSelector.Definitions;
using UnityEngine;

namespace LevelSelector.Quest
{
    [CreateAssetMenu(
        fileName = "QuestConditionPerk",
        menuName = "TowerDefense/Levels/Conditions/Perk")]
    public class PerkQuestCondition : QuestCondition
    {
        public PerkDefinition requiredPerk;

        public override bool Evaluate(
            LevelLoadout loadout,
            LevelResult result)
        {
            return loadout.HasPerk(requiredPerk);
        }
    }
}