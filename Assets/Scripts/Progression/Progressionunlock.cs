using UnityEngine;

namespace LevelSelector
{
    public enum UnlockKind
    {
        Perk,
        Mutator,
        Weapon
    }

    /// <summary>
    /// Zustand eines Inhalts. Freischaltung (Heldenlevel) und Besitz (Kauf) sind getrennt:
    ///   Locked        Heldenlevel noch nicht erreicht
    ///   Unlocked      Perk/Mutator: freigeschaltet und nutzbar
    ///   Purchasable   Waffe: freigeschaltet, aber noch nicht gekauft
    ///   Owned         Waffe: gekauft (oder kostenlos) und nutzbar
    /// </summary>
    public enum UnlockState
    {
        Locked,
        Unlocked,
        Purchasable,
        Owned
    }

    public enum PurchaseResult
    {
        Success,
        Locked,
        AlreadyOwned,
        NotEnoughPoints,
        Unavailable
    }

    /// <summary>
    /// Beschreibung eines freischaltbaren Inhalts für die UI (Progressionsleiste, Victory-Popup, Loadout).
    /// Reine Ansicht auf PerkDefinition / MutatorDefinition / WeaponDefinition, es werden keine Daten dupliziert.
    /// </summary>
    public readonly struct ProgressionUnlock
    {
        public readonly UnlockKind kind;
        public readonly ScriptableObject definition;
        public readonly string name;
        public readonly string description;
        public readonly Sprite icon;

        /// <summary>Heldenlevel, ab dem der Inhalt freigeschaltet ist (0 = von Beginn an).</summary>
        public readonly int unlockLevel;

        /// <summary>Score-Multiplikator (Perk, Mutator), bei Waffen 1.</summary>
        public readonly float scoreMultiplier;

        /// <summary>Preis in Forschungspunkten (nur Waffen), 0 = kostenlos.</summary>
        public readonly int purchaseCost;

        public ProgressionUnlock(UnlockKind kind, ScriptableObject definition, string name, string description,
            Sprite icon, int unlockLevel, float scoreMultiplier, int purchaseCost)
        {
            this.kind = kind;
            this.definition = definition;
            this.name = name;
            this.description = description;
            this.icon = icon;
            this.unlockLevel = unlockLevel;
            this.scoreMultiplier = scoreMultiplier;
            this.purchaseCost = purchaseCost;
        }
    }
}