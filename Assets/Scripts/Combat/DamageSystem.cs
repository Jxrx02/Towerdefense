using System;
using UnityEngine;

namespace TowerDefense
{
    public enum DamageType
    {
        Physical,   // Rüstung, Lethality, Armor Pen %
        Magic,      // Magieresistenz, Magic Pen flat/%
        True        // ignoriert alles außer Block-Schild
    }

    public interface IDamageable
    {
        DamageResult TakeDamage(DamageInfo info);
    }

    /// <summary>
    /// Alles, was für einen Treffer nötig ist. Wird vom Angreifer (Turm) erzeugt und vom Ziel (Vitals) ausgewertet.
    /// WICHTIG: 'amount' ist der Schaden VOR Crit. Der Crit-Multiplikator wird erst im Ziel angewendet,
    /// damit Projektile (AOE, Chain, Splitter) pro Treffer einen anderen 'amount' setzen können, ohne den Crit zu verlieren.
    /// </summary>
    public struct DamageInfo
    {
        public float amount;
        public DamageType type;
        public GameObject source;
        public Vitals sourceVitals;      // für Omnivamp

        // Crit (einmal pro Projektil gewürfelt)
        public bool isCrit;
        public float critMultiplier;     // 1.75 = 175 %

        // Penetration
        public float lethality;          // flat, nur Physisch
        public float armorPenPercent;    // 0..1, nur Physisch
        public float magicPenFlat;       // nur Magisch
        public float magicPenPercent;    // 0..1, nur Magisch

        // Sustain
        public float omnivamp;           // 0..1
        public float omnivampScale;      // z. B. 0.33 bei AOE/DoT/Chain

        // Verlangsamung (z. B. durch eine Slow-Aura): wird vom Projektil bei Treffer auf Gegner gelegt
        public float slowAmount;         // 0..1, 0.3 = 30 % langsamer
        public float slowDuration;

        // Flags
        public bool isDot;               // Tick: kein Crit, verbraucht keine Block-Ladung
        public bool bypassBlock;
        public bool bypassShield;

        public float FinalAmount => isCrit ? amount * Mathf.Max(1f, critMultiplier) : amount;

        /// <summary>Für alten Code, der nur einen int-Schaden kennt (keine Crit/Pen/Omnivamp).</summary>
        public static DamageInfo Legacy(float amount, DamageType type = DamageType.Physical)
        {
            return new DamageInfo
            {
                amount = amount,
                type = type,
                critMultiplier = 1f,
                omnivampScale = 1f
            };
        }
    }

    public struct DamageResult
    {
        public bool blocked;          // komplett vom Block-Schild abgefangen
        public bool isCrit;
        public bool killed;
        public DamageType type;

        public float rawDamage;       // nach Crit, vor Rüstung/MR
        public float afterMitigation; // nach Rüstung/MR
        public float mitigated;       // rawDamage - afterMitigation
        public float shieldDamage;    // vom Schild absorbiert
        public float healthDamage;    // echter HP-Verlust
    }

    /// <summary>
    /// Offensive LoL-Stats eines Turms/Angreifers. Upgrades addieren per Add().
    /// </summary>
    [Serializable]
    public class OffenseStats
    {
        [Header("Crit")]
        [Range(0f, 1f)] public float critChance;
        [Tooltip("Multiplikator bei Crit: 1.75 = 175 % Schaden. Bonus-Crit-Schaden einfach addieren (+0.25).")]
        public float critDamage = 1.75f;

        [Header("Penetration")]
        [Tooltip("Flat Rüstungs-Pen (wirkt nach Armor Pen %)")]
        public float lethality;
        [Range(0f, 1f)] public float armorPenPercent;
        public float magicPenFlat;
        [Range(0f, 1f)] public float magicPenPercent;

        [Header("Sustain")]
        [Range(0f, 1f)] public float omnivamp;

        public void Add(OffenseStats o)
        {
            critChance = Mathf.Clamp01(critChance + o.critChance);
            critDamage += o.critDamage;
            lethality += o.lethality;
            armorPenPercent = Mathf.Clamp01(armorPenPercent + o.armorPenPercent);
            magicPenFlat += o.magicPenFlat;
            magicPenPercent = Mathf.Clamp01(magicPenPercent + o.magicPenPercent);
            omnivamp = Mathf.Clamp01(omnivamp + o.omnivamp);
        }

        /// <summary>Addiert die Offense-Anteile eines Upgrades oder Level-Ups.</summary>
        public void Apply(StatBonus b)
        {
            critChance = Mathf.Clamp01(critChance + b.critChance);
            critDamage += b.critDamage;
            lethality += b.lethality;
            armorPenPercent = Mathf.Clamp01(armorPenPercent + b.armorPenPercent);
            magicPenFlat += b.magicPenFlat;
            magicPenPercent = Mathf.Clamp01(magicPenPercent + b.magicPenPercent);
            omnivamp = Mathf.Clamp01(omnivamp + b.omnivamp);
        }

        /// <summary>Erzeugt einen Treffer inkl. Crit-Wurf. Einmal pro Schuss aufrufen.</summary>
        public DamageInfo CreateDamage(float amount, DamageType type, GameObject source,
            bool canCrit = true, float omnivampScale = 1f)
        {
            return new DamageInfo
            {
                amount = amount,
                type = type,
                source = source,
                sourceVitals = source != null ? source.GetComponent<Vitals>() : null,

                isCrit = canCrit && critChance > 0f && UnityEngine.Random.value < critChance,
                critMultiplier = critDamage,

                lethality = lethality,
                armorPenPercent = armorPenPercent,
                magicPenFlat = magicPenFlat,
                magicPenPercent = magicPenPercent,

                omnivamp = omnivamp,
                omnivampScale = omnivampScale
            };
        }
    }

    /// <summary>
    /// Stat-Zuwachs aus einem Upgrade oder einem Level-Up. Alles additiv, 0 = keine Änderung.
    /// Wird in TowerUpgradeLevel.bonus (pro Upgrade) und Tower.bonusPerLevel (pro Level-Up) eingetragen.
    /// </summary>
    [Serializable]
    public class StatBonus
    {
        [Header("Allgemein")]
        public int health;
        public float healthRegen;
        public int damage;
        public float range;
        [Tooltip("Sekunden, die von der Zeit zwischen zwei Schüssen abgezogen werden")]
        public float cooldownDecrease;

        [Header("Verteidigung")]
        public float armor;
        public float magicResist;

        [Header("Crit")]
        [Range(0f, 1f)] public float critChance;
        [Tooltip("Bonus auf den Crit-Multiplikator: 0.25 = +25 % Crit-Schaden")]
        public float critDamage;

        [Header("Penetration")]
        public float lethality;
        [Range(0f, 1f)] public float armorPenPercent;
        public float magicPenFlat;
        [Range(0f, 1f)] public float magicPenPercent;

        [Header("Sustain")]
        [Range(0f, 1f)] public float omnivamp;
    }

    /// <summary>Effekte, die ein Projektil zusätzlich zum Schaden auf das Ziel legt.</summary>
    [Serializable]
    public class HitEffects
    {
        [Header("Anti-Heal (Grievous Wounds)")]
        [Range(0f, 1f)] public float antiHealPercent;
        public float antiHealDuration = 3f;

        [Header("Rüstungs-/MR-Shred")]
        public float armorShredFlat;
        public float mrShredFlat;
        [Range(0f, 0.9f)] public float shredPercent;
        public float shredDuration = 4f;
        public int shredMaxStacks = 3;
        [Tooltip("Gleicher Key = Stacks addieren sich")]
        public string shredKey = "shred";

        [Header("Verlangsamung (\"Verschleimt\")")]
        [Range(0f, 1f)] public float slowAmount;
        public float slowDuration = 2f;

        public void Apply(Vitals target)
        {
            if (target == null || target.IsDead) return;

            if (slowAmount > 0f && target.TryGetComponent(out Enemy enemy))
                enemy.ApplySlow(slowAmount, slowDuration);

            if (antiHealPercent > 0f)
                target.ApplyAntiHeal(antiHealPercent, antiHealDuration);

            if (armorShredFlat > 0f || mrShredFlat > 0f || shredPercent > 0f)
                target.ApplyShred(shredKey, armorShredFlat, mrShredFlat, shredPercent, shredDuration, shredMaxStacks);
        }
    }

    public static class DamageCalculator
    {
        /// <summary>
        /// Schadensmultiplikator nach Rüstung/MR. Reihenfolge wie in LoL:
        /// Armor Pen % zuerst, danach Lethality / flat Magic Pen. Pen wirkt nur auf positive Werte.
        /// Negative Rüstung (durch Shred) erhöht den Schaden: 2 - 100 / (100 - Wert).
        /// </summary>
        public static float GetMultiplier(in DamageInfo info, Vitals target)
        {
            switch (info.type)
            {
                case DamageType.Physical:
                    return ResistMultiplier(target.EffectiveArmor, info.armorPenPercent, info.lethality);
                case DamageType.Magic:
                    return ResistMultiplier(target.EffectiveMagicResist, info.magicPenPercent, info.magicPenFlat);
                default:
                    return 1f;
            }
        }

        public static float ResistMultiplier(float resist, float penPercent, float penFlat)
        {
            if (resist > 0f)
            {
                resist *= 1f - Mathf.Clamp01(penPercent);
                resist = Mathf.Max(0f, resist - penFlat);
            }

            return resist >= 0f
                ? 100f / (100f + resist)
                : 2f - 100f / (100f - resist);
        }
    }
}