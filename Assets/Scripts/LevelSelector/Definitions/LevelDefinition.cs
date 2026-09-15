using System.Collections.Generic;
using UnityEngine;

namespace LevelSelector.Definitions
{
    [CreateAssetMenu(
        fileName = "LevelDefinition",
        menuName = "TowerDefense/Levels/Level Definition")]
    public class LevelDefinition : ScriptableObject
    {
        [Header("Level")]
        public int levelIndex;
        public string levelName;
        [TextArea]
        public string description;

        [Header("Loadout")]
        public List<WeaponDefinition> availableWeapons = new();
        public List<PerkDefinition> availablePerks = new();
        public List<MutatorDefinition> availableMutators = new();

        [Header("Quests")]
        public List<LevelQuestDefinition> quests = new();
    }


}