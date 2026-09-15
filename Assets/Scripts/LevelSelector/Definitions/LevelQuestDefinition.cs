using System.Collections.Generic;
using LevelSelector.Quest;
using UnityEngine;

namespace LevelSelector.Definitions
{
    [CreateAssetMenu(
        fileName = "Quest",
        menuName = "TowerDefense/Levels/Quest")]
    public class LevelQuestDefinition : ScriptableObject
    {
        public string questName;

        [TextArea(2, 5)]
        public string description;

        public List<QuestCondition> conditions = new();

        public bool IsCompleted(
            LevelLoadout loadout,
            LevelResult result)
        {
            foreach (var condition in conditions)
            {
                if (!condition.Evaluate(loadout, result))
                    return false;
            }

            return true;
        }
    }
}