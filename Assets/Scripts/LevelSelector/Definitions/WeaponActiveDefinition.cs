using UnityEngine;

namespace LevelSelector.Definitions
{
    public abstract class WeaponActiveDefinition : ScriptableObject
    {
        public string activeName;

        [TextArea(2, 5)]
        public string description;

        public Sprite icon;

        public abstract void Execute(GameObject user);
    }
}