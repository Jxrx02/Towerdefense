using LevelSelector.Definitions;
using UnityEngine;

namespace LevelSelector
{
    /// <summary>
    /// Laufzeit-Zustand des Spieler-Levels. Die XP liegen im Savegame des LevelProgressManager.
    /// Perks, die in keiner Stufe der Progression stehen, sind immer freigeschaltet.
    /// </summary>
    public static class PlayerProgress
    {
        public struct LevelState
        {
            public int level;          // 1-basiert
            public int xpIntoLevel;    // XP innerhalb der aktuellen Stufe
            public int xpForNext;      // XP bis zur nächsten Stufe (0 bei Max)
            public bool isMax;

            public float Fraction => isMax ? 1f : (xpForNext > 0 ? Mathf.Clamp01((float)xpIntoLevel / xpForNext) : 1f);
        }

        private static PlayerProgressionDefinition _definition;
        private static bool _loaded;

        public static PlayerProgressionDefinition Definition
        {
            get
            {
                if (!_loaded)
                {
                    _definition = Resources.Load<PlayerProgressionDefinition>("PlayerProgression");
                    _loaded = true;
                }
                return _definition;
            }
        }

        // ── XP / Level ────────────────────────────────────────────

        // Gespeichert im Savegame des LevelProgressManager (levelProgress.json)
        public static int TotalXp => LevelProgressManager.GetPlayerXp();

        public static void AddXp(int amount) => LevelProgressManager.AddPlayerXp(amount);

        public static LevelState Evaluate(int totalXp)
        {
            PlayerProgressionDefinition def = Definition;

            if (def == null || def.levels == null || def.levels.Count == 0)
                return new LevelState { level = 1, isMax = true };

            int remaining = Mathf.Max(0, totalXp);
            int level = 1;

            // levels[i] = Stufe i+1. levels[level].xpRequired = Kosten für Stufe level+1
            while (level < def.levels.Count)
            {
                int need = Mathf.Max(1, def.levels[level].xpRequired);

                if (remaining < need)
                    return new LevelState { level = level, xpIntoLevel = remaining, xpForNext = need };

                remaining -= need;
                level++;
            }

            return new LevelState { level = level, isMax = true };
        }

        public static int CurrentLevel => Evaluate(TotalXp).level;

        // ── Perks ─────────────────────────────────────────────────

        /// <summary>Stufe, ab der der Perk verfügbar ist. 0 = steht in keiner Stufe (immer verfügbar).</summary>
        public static int GetUnlockLevel(PerkDefinition perk)
        {
            PlayerProgressionDefinition def = Definition;
            if (def == null || perk == null) return 0;

            for (int i = 0; i < def.levels.Count; i++)
                if (def.levels[i].unlockPerk == perk)
                    return i + 1;

            return 0;
        }

        public static bool IsPerkUnlocked(PerkDefinition perk)
        {
            int unlockLevel = GetUnlockLevel(perk);
            return unlockLevel == 0 || unlockLevel <= CurrentLevel;
        }

        /// <summary>Perk, der mit genau dieser Stufe freigeschaltet wird (oder null).</summary>
        public static PerkDefinition GetPerkUnlockedAtLevel(int level)
        {
            PlayerProgressionDefinition def = Definition;
            if (def == null || level < 1 || level > def.levels.Count) return null;
            return def.levels[level - 1].unlockPerk;
        }

        /// <summary>Nächster Perk oberhalb der aktuellen Stufe (für die Vorschau neben dem Balken).</summary>
        public static bool GetNextUnlock(int currentLevel, out PerkDefinition perk, out int atLevel)
        {
            perk = null;
            atLevel = 0;

            PlayerProgressionDefinition def = Definition;
            if (def == null) return false;

            for (int i = currentLevel; i < def.levels.Count; i++)
            {
                if (def.levels[i].unlockPerk == null) continue;

                perk = def.levels[i].unlockPerk;
                atLevel = i + 1;
                return true;
            }

            return false;
        }

        /// <summary>Zum Testen: XP zurücksetzen.</summary>
        public static void ResetXp() => LevelProgressManager.ResetPlayerXp();
    }
}