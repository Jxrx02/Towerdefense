using LevelSelector.Definitions;
using UnityEngine;

namespace LevelSelector.Quest
{
    [CreateAssetMenu(
        fileName = "QuestConditionMutator",
        menuName = "TowerDefense/Levels/Conditions/Mutator")]
    public class MutatorQuestCondition : QuestCondition
    {
        public MutatorDefinition requiredMutator;

        public override bool Evaluate(
            LevelLoadout loadout,
            LevelResult result)
        {
            return loadout.HasMutator(requiredMutator);
        }
    }
}