using TowerDefense;
using UnityEngine;

namespace ScriptableObjects
{
    [CreateAssetMenu(fileName = "New Enemy", menuName = "ScriptableObjects/Enemy")]
    public class EnemyConfig : ScriptableObject
    {
        [Header("Basic Info")]
        public string description;
        public Sprite[] walkAnim;
        public Sprite[] deadAnim;
        public Sprite[] attackAnim;

        [Header("Core Stats")]
        public int health;
        public int tenacity;            // Resistenz gegen CC (Prozent)
        public float movementSpeed;     // Einheiten pro Sekunde
        public int xpDrop;

        [Header("Defense (LoL-Stats)")]
        public float armor;
        public float magicResist;
        public float healthRegenPerSecond;

        [Header("Schilde")]
        [Tooltip("HP-Schild beim Spawn: zählt wie zusätzliche HP und absorbiert Schaden nach der Rüstung")]
        public float startShield;
        [Tooltip("Sekunden, bis das HP-Schild verfällt. 0 = bleibt, bis es aufgebraucht ist")]
        public float shieldDuration;
        [Tooltip("Block-Schild beim Spawn: blockt die nächsten Treffer komplett (0 = keins)")]
        public int blockCharges;
        [Tooltip("Sekunden, bis das Block-Schild verfällt. 0 = bis alle Ladungen verbraucht sind")]
        public float blockDuration;
        [Tooltip("Sekunden, bis sich das Block-Schild erneuert, nachdem die letzte Ladung verbraucht wurde. 0 = nie")]
        public float blockRechargeTime;

        [Header("Combat Stats")]
        public float attackRange;
        public float attackCooldown;
        public int attackDamage;
        public DamageType damageType = DamageType.Physical;
        [Tooltip("Crit, Lethality, Pen, Omnivamp dieses Gegners")]
        public OffenseStats offense = new OffenseStats();
        public bool isMeele = true;
        public GameObject projectile;   // nur für Fernkämpfer (isMeele = false)

        [Header("Pathing")]
        [Tooltip("Sekunden zwischen zwei Wegberechnungen zum Levelende")]
        public float repathInterval = 1.5f;
        [Tooltip("Sekunden zwischen zwei Wegberechnungen, solange ein Turm/Held verfolgt wird (Helden bewegen sich)")]
        public float chaseRepathInterval = 0.5f;

        [Header("Targeting")]
        [Tooltip("Türme/Helden innerhalb dieser Reichweite werden aktiv verfolgt und angegriffen")]
        public float aggroRange = 4f;
        [Tooltip("Außerhalb dieser Reichweite lässt der Gegner sein Ziel wieder los")]
        public float leashRange = 6f;
        [Tooltip("Reihenfolge = Priorität. Kategorien, die hier fehlen, werden nicht aktiv verfolgt. " +
                 "Mauern, die den Weg zum Levelende versperren, werden immer angegriffen.")]
        public TargetCategory[] targetPriority = { TargetCategory.Hero, TargetCategory.Tower };

        [Header("Special Attributes")]
        public int evasionChance;       // Prozent

        [Header("Abilities")]
        public bool canFly;

        [Header("Economic Impact")]
        public int goldReward;
        public int penaltyOnReachingEnd;

        [Header("Visual Effects")]
        public Color lightColor;
        public bool hasLight;
        public GameObject deathEffect;
        public GameObject hitEffect;
        public GameObject evasionEffect;
        [Tooltip("Effekt, wenn ein Treffer vom Block-Schild abgefangen wird")]
        public GameObject blockEffect;

        /// <summary>0 = höchste Priorität, -1 = darf nicht aktiv verfolgt werden.</summary>
        public int GetPriorityRank(TargetCategory category)
        {
            if (targetPriority == null) return -1;
            for (int i = 0; i < targetPriority.Length; i++)
                if (targetPriority[i] == category) return i;
            return -1;
        }

        private void OnValidate()
        {
            leashRange = Mathf.Max(leashRange, aggroRange);
        }
    }
}