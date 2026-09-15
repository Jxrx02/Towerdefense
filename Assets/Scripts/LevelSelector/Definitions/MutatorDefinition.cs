using UnityEngine;

namespace LevelSelector.Definitions
{
    public abstract class MutatorDefinition : ScriptableObject
    {
        public string mutatorName;

        [TextArea(2, 5)]
        public string description;

        public Sprite icon;

        public abstract void Apply(LevelLoadout loadout);
    }
}