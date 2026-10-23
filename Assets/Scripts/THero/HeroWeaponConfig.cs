using System;
using System.Collections.Generic;
using LevelSelector.Definitions;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>Werte eines einzelnen Angriffs (eine Animation).</summary>
    [Serializable]
    public class HeroAttackData
    {
        public HeroAnim anim;

        [Min(0f)] public float damageMultiplier = 1f;
        public DamageType damageType = DamageType.Physical;

        [Header("Treffer (Nahkampf)")]
        [Tooltip("Reichweite des Treffers in Welteinheiten")]
        public float range = 1.4f;
        [Range(10f, 360f)] public float arcDegrees = 110f;
        public int maxTargets = 3;

        [Header("Ablauf")]
        public float animationSpeed = 1f;
        [Tooltip("Kleiner Schritt nach vorn beim Zuschlagen")]
        public float lungeDistance = 0.25f;
        [Tooltip("Tempo beim Laufen während des Angriffs (0 = steht still)")]
        [Range(0f, 1f)] public float moveSpeedMultiplier = 0.6f;
        [Tooltip("Pause nach der Animation, bevor die nächste Aktion möglich ist")]
        public float recovery = 0.08f;
        [Tooltip("Zeit nach dem Angriff, in der der nächste Kombo-Schlag möglich ist")]
        public float comboWindow = 0.4f;
        [Tooltip("Kann während dieses Angriffs nicht von einem Treffer unterbrochen werden")]
        public bool superArmor;
        
        [Header("Ausdauer")]
        [Tooltip("Wird beim Beginn des Angriffs verbraucht (aufgeladene Angriffe: beim Beginn des Aufladens)")]
        [Min(0f)] public float staminaCost = 12f;
        [Header("Aufgeladen")]
        [Tooltip("Schadensbonus bei voller Aufladung (1 = kein Bonus)")]
        public float maxChargeBonus = 1f;

        [Header("Feedback")]
        public float cameraShakeStrength;
        public float cameraShakeDuration = 0.1f;
    }

    /// <summary>
    /// Alles, was eine Waffe für den Helden ausmacht: Waffenart, Animationen, Kombos, Aufladen, Block, Fernkampf.
    /// Wird über WeaponDefinition.heroConfig an den LevelLoadoutManager gehängt.
    /// </summary>
    [CreateAssetMenu(fileName = "HeroWeapon", menuName = "ScriptableObjects/Hero/Weapon Config")]
    public class HeroWeaponConfig : ScriptableObject
    {
        [Header("Waffe")]
        public HeroWeaponType weaponType = HeroWeaponType.OneHand;
        public HeroAnimationSet animationSet;

        [Header("Werte des Helden (überschreiben ihn, wenn größer 0)")]
        public int baseDamage;
        [Tooltip("Reichweite für Zielsuche und automatische Angriffe")]
        public float targetingRange;
        [Tooltip("Pause zwischen zwei Angriffen bzw. nach dem letzten Kombo-Schlag")]
        public float attackCooldown;
        [Tooltip("Zweihänder können langsamer sein, Bogen schneller")]
        public float moveSpeedMultiplier = 1f;

        [Header("Fernkampf")]
        public GameObject projectilePrefab;
        [Tooltip("Der Held greift automatisch an, sobald ein Gegner in Reichweite ist (bisheriges Bogenverhalten)")]
        public bool autoAttack;
        [Tooltip("Bei Zielen nach oben UpAttack bzw. ChargedUpAttack benutzen")]
        public bool useUpAttack = true;
        public HeroAnim upAttack = HeroAnim.UpAttack;
        public HeroAnim chargedUpAttack = HeroAnim.ChargedUpAttack;

        [Header("Kombos (Reihenfolge der Schläge)")]
        public HeroAnim[] lightCombo = { HeroAnim.Attack1, HeroAnim.Attack2, HeroAnim.Attack3 };
        public HeroAnim[] heavyCombo = { HeroAnim.HeavyAttack1, HeroAnim.HeavyAttack2, HeroAnim.HeavyAttack3 };

        [Header("Aufgeladene Angriffe (Stufe 1, Stufe 2 bei voller Aufladung)")]
        public HeroAnim[] chargedLight = { HeroAnim.ChargedAttack1, HeroAnim.ChargedAttack2 };
        public HeroAnim[] chargedHeavy = { HeroAnim.ChargedHeavyAttack1, HeroAnim.ChargedHeavyAttack2 };
        [Tooltip("So lange muss die Angriffstaste gehalten werden, bis das Aufladen beginnt (kürzer = normaler Angriff)")]
        public float chargeThreshold = 0.22f;
        [Tooltip("Sekunden bis zur vollen Aufladung")]
        public float fullChargeTime = 1.0f;
        [Range(0f, 1f)] public float chargeMoveMultiplier = 0.4f;

        [Header("Block")]
        public bool canBlock = true;
        [Range(0f, 1f)] public float blockDamageReduction = 0.6f;
        [Tooltip("Nur Angriffe aus diesem Winkel vor dem Helden werden geblockt")]
        [Range(30f, 360f)] public float blockArc = 140f;
        [Range(0f, 1f)] public float blockMoveMultiplier = 0.5f;

        [Header("Angriffsdaten")]
        public List<HeroAttackData> attacks = new List<HeroAttackData>();
        
        [HideInInspector] public WeaponActiveDefinition activeAbility;
        private static readonly HeroAttackData Fallback = new HeroAttackData();

        public bool IsRanged => weaponType == HeroWeaponType.Bow;
        public bool HasHeavy => heavyCombo != null && heavyCombo.Length > 0;

        public HeroAttackData GetAttack(HeroAnim anim)
        {
            foreach (HeroAttackData attack in attacks)
            {
                if (attack.anim == anim)
                    return attack;
            }

            return Fallback;
        }

        // ------------------------------------------------------------------
        // Startwerte
        // ------------------------------------------------------------------
        private void Reset()
        {
            ApplyDefaults();
        }

        [ContextMenu("Standardwerte für den Waffentyp setzen")]
        public void ApplyDefaults()
        {
            attacks.Clear();

            if (IsRanged)
            {
                lightCombo = new[] { HeroAnim.RangedAttack1, HeroAnim.RangedAttack2, HeroAnim.RangedAttack3 };
                heavyCombo = new HeroAnim[0];
                chargedLight = new[] { HeroAnim.ChargedAttack };
                chargedHeavy = new HeroAnim[0];
                autoAttack = true;
                canBlock = false;
                moveSpeedMultiplier = 1f;

                AddAttack(HeroAnim.RangedAttack1, 1f, move: 0.7f, lunge: 0f);
                AddAttack(HeroAnim.RangedAttack2, 1f, move: 0.7f, lunge: 0f);
                AddAttack(HeroAnim.RangedAttack3, 1.2f, move: 0.7f, lunge: 0f);
                AddAttack(HeroAnim.UpAttack, 1f, move: 0.7f, lunge: 0f);
                AddAttack(HeroAnim.ChargedAttack, 2f, move: 0.5f, lunge: 0f, chargeBonus: 1.5f);
                AddAttack(HeroAnim.ChargedUpAttack, 2f, move: 0.5f, lunge: 0f, chargeBonus: 1.5f);
                return;
            }

            lightCombo = new[] { HeroAnim.Attack1, HeroAnim.Attack2, HeroAnim.Attack3 };
            heavyCombo = new[] { HeroAnim.HeavyAttack1, HeroAnim.HeavyAttack2, HeroAnim.HeavyAttack3 };
            chargedLight = new[] { HeroAnim.ChargedAttack1, HeroAnim.ChargedAttack2 };
            chargedHeavy = new[] { HeroAnim.ChargedHeavyAttack1, HeroAnim.ChargedHeavyAttack2 };
            autoAttack = false;
            canBlock = true;
            blockDamageReduction = weaponType == HeroWeaponType.Shield ? 0.9f : 0.6f;
            moveSpeedMultiplier = weaponType == HeroWeaponType.TwoHand ? 0.85f : 1f;

            float heavyScale = weaponType == HeroWeaponType.TwoHand ? 1.2f : 1f;

            AddAttack(HeroAnim.Attack1, 1f);
            AddAttack(HeroAnim.Attack2, 1f);
            AddAttack(HeroAnim.Attack3, 1.3f, recovery: 0.15f);

            AddAttack(HeroAnim.HeavyAttack1, 1.8f * heavyScale, move: 0.3f, armor: true, shake: 0.1f, range: 1.6f, recovery: 0.15f);
            AddAttack(HeroAnim.HeavyAttack2, 2.0f * heavyScale, move: 0.3f, armor: true, shake: 0.1f, range: 1.6f, recovery: 0.15f);
            AddAttack(HeroAnim.HeavyAttack3, 2.6f * heavyScale, move: 0.25f, armor: true, shake: 0.18f, range: 1.8f, arc: 140f, recovery: 0.25f);

            AddAttack(HeroAnim.ChargedAttack1, 1.6f, move: 0.4f, armor: true, chargeBonus: 1.3f);
            AddAttack(HeroAnim.ChargedAttack2, 2.4f, move: 0.4f, armor: true, chargeBonus: 1.5f, shake: 0.12f);
            AddAttack(HeroAnim.ChargedHeavyAttack1, 2.8f * heavyScale, move: 0.25f, armor: true, chargeBonus: 1.3f, shake: 0.15f, range: 1.8f, arc: 140f);
            AddAttack(HeroAnim.ChargedHeavyAttack2, 4.0f * heavyScale, move: 0.2f, armor: true, chargeBonus: 1.5f, shake: 0.25f, range: 2.0f, arc: 160f, recovery: 0.3f);
        }
        private static float DefaultStaminaCost(HeroAnim anim)
        {
            switch (anim)
            {
                case HeroAnim.Attack1:
                case HeroAnim.Attack2:
                case HeroAnim.Attack3: return 12f;
                case HeroAnim.HeavyAttack1: 
                case HeroAnim.HeavyAttack2: 
                case HeroAnim.HeavyAttack3: return 33f;
                case HeroAnim.ChargedAttack1: return 70f;
                case HeroAnim.ChargedAttack2: return 70f;
                case HeroAnim.ChargedHeavyAttack1: return 70f;
                case HeroAnim.ChargedHeavyAttack2: return 70f;
                case HeroAnim.RangedAttack1:
                case HeroAnim.RangedAttack2:
                case HeroAnim.UpAttack: 
                case HeroAnim.RangedAttack3: return 12f;
                case HeroAnim.ChargedAttack: return 70f;
                case HeroAnim.ChargedUpAttack: return 70f;
                default: return 12;
            }
        }
        private void AddAttack(HeroAnim anim, float damage, float move = 0.6f, bool armor = false,
            float chargeBonus = 1f, float shake = 0f, float range = 1.4f, float arc = 110f,
            float lunge = 0.25f, float recovery = 0.08f)
        {
            attacks.Add(new HeroAttackData
            {
                anim = anim,
                damageMultiplier = damage,
                moveSpeedMultiplier = move,
                superArmor = armor,
                maxChargeBonus = chargeBonus,
                cameraShakeStrength = shake,
                range = range,
                arcDegrees = arc,
                lungeDistance = lunge,
                recovery = recovery,
                staminaCost = DefaultStaminaCost(anim)
            });
        }
    }
}