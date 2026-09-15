using UnityEngine;

namespace LevelSelector.Quest
{
    public abstract class QuestCondition : ScriptableObject
    {
        public abstract bool Evaluate(
            LevelLoadout loadout,
            LevelResult result);
    }
}