using System;
using System.Collections.Generic;
using LevelSelector.Definitions;
using TowerDefense;
using TowerDefense.Research;
using UnityEngine;

namespace LevelSelector
{
    /// <summary>
    /// Laufzeit-Zustand der Spielerprogression.
    ///
    ///   XP / Heldenlevel   gespeichert im Savegame des LevelProgressManager (playerXp)
    ///   Freischaltung      abgeleitet aus dem Heldenlevel und der PlayerProgressionDefinition (keine zweite Datenhaltung)
    ///   Besitz (Waffen)    gespeichert im Savegame (ownedWeapons), bezahlt mit Forschungspunkten
    ///
    /// Perks, Waffen und Mutatoren, die in keiner Stufe der Progression stehen, sind immer freigeschaltet.
    /// Eine Waffe ist nutzbar (Owned), wenn sie freigeschaltet ist UND (kostenlos oder gekauft).
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

        /// <summary>XP oder Besitz haben sich geändert (XP erhalten, Waffe gekauft, Reset). UIs aktualisieren sich daraufhin.</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()   // nötig, falls "Domain Reload" aus ist
        {
            _definition = null;
            _loaded = false;
            Changed = null;
        }

        public static PlayerProgressionDefinition Definition
        {
            get
            {
                if (!_loaded)
                {
                    _definition = Resources.Load<PlayerProgressionDefinition>("PlayerProgression");
                    _loaded = true;
                    Debug.Log("PlayerProgressionDefinition geladen: " + (_definition != null ? _definition.name : "NICHT GEFUNDEN"));
                }

                return _definition;
            }
        }

        public static bool HasProgression =>
            Definition != null && Definition.levels != null && Definition.levels.Count > 0;

        // ── XP / Level ────────────────────────────────────────────

        public static int TotalXp => LevelProgressManager.GetPlayerXp();

        public static void AddXp(int amount)
        {
            LevelProgressManager.AddPlayerXp(amount);
            Changed?.Invoke();
        }

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

        // ── Freischaltung (aus dem Level abgeleitet) ──────────────

        /// <summary>Stufe, ab der der Inhalt verfügbar ist. 0 = steht in keiner Stufe (immer verfügbar).</summary>
        public static int GetUnlockLevel(PerkDefinition perk) => FindUnlockLevel(perk);
        public static int GetUnlockLevel(MutatorDefinition mutator) => FindUnlockLevel(mutator);
        public static int GetUnlockLevel(WeaponDefinition weapon) => FindUnlockLevel(weapon);

        public static bool IsPerkUnlocked(PerkDefinition perk) => IsUnlockedAt(FindUnlockLevel(perk));
        public static bool IsMutatorUnlocked(MutatorDefinition mutator) => IsUnlockedAt(FindUnlockLevel(mutator));
        public static bool IsWeaponUnlocked(WeaponDefinition weapon) => IsUnlockedAt(FindUnlockLevel(weapon));

        private static bool IsUnlockedAt(int unlockLevel) => unlockLevel == 0 || unlockLevel <= CurrentLevel;

        private static int FindUnlockLevel(UnityEngine.Object content)
        {
            PlayerProgressionDefinition def = Definition;
            if (def == null || content == null) return 0;

            for (int i = 0; i < def.levels.Count; i++)
            {
                ProgressionLevel level = def.levels[i];

                if (level.unlockPerk == content ||
                    ContainsReference(level.unlockWeapons, content) ||
                    ContainsReference(level.unlockMutators, content))
                    return i + 1;
            }

            return 0;
        }

        private static bool ContainsReference<T>(List<T> list, UnityEngine.Object content) where T : UnityEngine.Object
        {
            if (list == null) return false;

            foreach (T item in list)
                if (item == content) return true;

            return false;
        }

        // ── Besitz (Waffen) ───────────────────────────────────────

        public static bool IsWeaponOwned(WeaponDefinition weapon)
        {
            if (weapon == null || !IsWeaponUnlocked(weapon))
                return false;

            return weapon.purchaseCost <= 0 || LevelProgressManager.IsWeaponPurchased(weapon.name);
        }

        public static UnlockState GetWeaponState(WeaponDefinition weapon)
        {
            if (!IsWeaponUnlocked(weapon)) return UnlockState.Locked;
            return IsWeaponOwned(weapon) ? UnlockState.Owned : UnlockState.Purchasable;
        }

        /// <summary>Forschungspunkte, mit denen Waffen bezahlt werden (dasselbe Konto wie die Forschung).</summary>
        public static int PointsBalance =>
            ResearchManager.Instance != null ? ResearchManager.Instance.researchPoints : 0;

        public static bool CanAffordWeapon(WeaponDefinition weapon) =>
            weapon != null && PointsBalance >= weapon.purchaseCost;

        /// <summary>
        /// Kauft eine freigeschaltete Waffe. Der Besitz wird zuerst gespeichert, dann werden die Punkte abgezogen,
        /// damit bei einem Abbruch dazwischen nie bezahlt wird, ohne die Waffe zu besitzen.
        /// </summary>
        public static PurchaseResult TryPurchaseWeapon(WeaponDefinition weapon)
        {
            if (weapon == null) return PurchaseResult.Unavailable;
            if (!IsWeaponUnlocked(weapon)) return PurchaseResult.Locked;
            if (IsWeaponOwned(weapon)) return PurchaseResult.AlreadyOwned;

            ResearchManager research = ResearchManager.Instance;
            if (research == null || LevelProgressManager.Instance == null) return PurchaseResult.Unavailable;

            if (!research.CanAffordResearch(weapon.purchaseCost)) return PurchaseResult.NotEnoughPoints;

            LevelProgressManager.MarkWeaponPurchased(weapon.name);
            research.SpendResearchPoints(weapon.purchaseCost);

            Changed?.Invoke();
            return PurchaseResult.Success;
        }

        // ── Beschreibung für die UI ───────────────────────────────

        public static ProgressionUnlock Describe(PerkDefinition perk) =>
            new ProgressionUnlock(UnlockKind.Perk, perk, perk.perkName, perk.description, perk.icon,
                FindUnlockLevel(perk), perk.ScoreMultiplier, 0);

        public static ProgressionUnlock Describe(MutatorDefinition mutator) =>
            new ProgressionUnlock(UnlockKind.Mutator, mutator, mutator.mutatorName, mutator.description, mutator.icon,
                FindUnlockLevel(mutator), mutator.ScoreMultiplier, 0);

        public static ProgressionUnlock Describe(WeaponDefinition weapon) =>
            new ProgressionUnlock(UnlockKind.Weapon, weapon, weapon.weaponName,
                Texts.Progression.WeaponDescription(weapon.passiveDescription, weapon.activeDescription), weapon.icon,
                FindUnlockLevel(weapon), 1f, weapon.purchaseCost);

        /// <summary>Zustand eines beliebigen Inhalts (Perk/Mutator: Locked oder Unlocked, Waffe: Locked, Purchasable, Owned).</summary>
        public static UnlockState GetState(ProgressionUnlock unlock)
        {
            switch (unlock.kind)
            {
                case UnlockKind.Weapon: return GetWeaponState((WeaponDefinition)unlock.definition);
                case UnlockKind.Mutator: return IsMutatorUnlocked((MutatorDefinition)unlock.definition) ? UnlockState.Unlocked : UnlockState.Locked;
                default: return IsPerkUnlocked((PerkDefinition)unlock.definition) ? UnlockState.Unlocked : UnlockState.Locked;
            }
        }

        /// <summary>Alles, was mit genau dieser Stufe freigeschaltet wird (Perk, Mutatoren, Waffen).</summary>
        public static List<ProgressionUnlock> GetUnlocksAtLevel(int level)
        {
            var result = new List<ProgressionUnlock>();
            PlayerProgressionDefinition def = Definition;

            if (def == null || level < 1 || level > def.levels.Count)
                return result;

            ProgressionLevel entry = def.levels[level - 1];

            if (entry.unlockPerk != null)
                result.Add(Describe(entry.unlockPerk));

            if (entry.unlockMutators != null)
                foreach (MutatorDefinition mutator in entry.unlockMutators)
                    if (mutator != null) result.Add(Describe(mutator));

            if (entry.unlockWeapons != null)
                foreach (WeaponDefinition weapon in entry.unlockWeapons)
                    if (weapon != null) result.Add(Describe(weapon));

            return result;
        }

        /// <summary>Nächster Inhalt oberhalb der aktuellen Stufe (für die Vorschau neben dem Balken).</summary>
        public static bool GetNextUnlock(int currentLevel, out ProgressionUnlock unlock)
        {
            unlock = default;

            PlayerProgressionDefinition def = Definition;
            if (def == null) return false;

            for (int level = currentLevel + 1; level <= def.levels.Count; level++)
            {
                List<ProgressionUnlock> unlocks = GetUnlocksAtLevel(level);

                if (unlocks.Count > 0)
                {
                    unlock = unlocks[0];
                    return true;
                }
            }

            return false;
        }

        /// <summary>Zum Testen: XP zurücksetzen. Gekaufte Waffen bleiben erhalten.</summary>
        public static void ResetXp()
        {
            LevelProgressManager.ResetPlayerXp();
            Changed?.Invoke();
        }
    }
}