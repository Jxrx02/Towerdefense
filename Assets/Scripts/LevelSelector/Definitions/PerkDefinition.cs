using UnityEngine;

namespace LevelSelector.Definitions
{
    public abstract class PerkDefinition : ScriptableObject
    {
        public string perkName;

        [TextArea(2, 5)]
        public string description;

        public Sprite icon;
        
        [Header("Score")]
        [SerializeField]
        private float scoreMultiplier = 1f;
        public float ScoreMultiplier => scoreMultiplier;
        
        public abstract void Apply(LevelLoadout loadout);
    }
}