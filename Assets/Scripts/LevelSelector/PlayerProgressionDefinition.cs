using System.Collections.Generic;
using UnityEngine;

namespace LevelSelector.Definitions
{
    /// <summary>
    /// Spieler-Level mit Perk-Freischaltungen. Das Asset muss unter Assets/Resources/PlayerProgression.asset liegen
    /// (der Victory-Builder legt es an, falls es fehlt).
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerProgression", menuName = "TowerDefense/Progression/Player Progression")]
    public class PlayerProgressionDefinition : ScriptableObject
    {
        [Tooltip("XP pro Score-Punkt. Beispiel: 0.25 bei 5000 Score = 1250 XP.")]
        public float xpPerScorePoint = 0.25f;

        [Tooltip("Eintrag 0 = Stufe 1, Eintrag 1 = Stufe 2, ...")]
        public List<ProgressionLevel> levels = new();
    }

    [System.Serializable]
    public class ProgressionLevel
    {
        [Tooltip("XP, die von der vorherigen Stufe bis zu dieser Stufe nötig sind. Stufe 1: egal (0).")]
        public int xpRequired = 1500;

        [Tooltip("Perk, der mit dieser Stufe freigeschaltet wird (leer = keiner).")]
        public PerkDefinition unlockPerk;
    }
}