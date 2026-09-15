using System.Collections.Generic;
using LevelSelector.Definitions;

namespace LevelSelector
{
    [System.Serializable]
    public class LevelLoadout
    {
        public int levelIndex;

        public LevelDefinition levelDefinition;

        public WeaponDefinition weapon;

        public List<PerkDefinition> perks = new();
        public List<MutatorDefinition> mutators = new();

        // Runtime-Werte
        public float damageMultiplier = 1f;
        public float attackSpeedMultiplier = 1f;
        public float enemyCountMultiplier = 1f;

        public bool healingEnabled = true;

        public bool HasPerk(PerkDefinition perk)
        {
            return perks.Contains(perk);
        }

        public bool HasMutator(MutatorDefinition mutator)
        {
            return mutators.Contains(mutator);
        }
        
        
    }
}