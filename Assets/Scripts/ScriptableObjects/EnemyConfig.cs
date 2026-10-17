using System;
using UnityEngine;

namespace ScriptableObjects
{
    public enum EnemyType
    {
        IgnoreWalls,   // berechnet einmal, ignoriert Änderungen
        Adaptive,   // recalculates bei Blockänderung
        Clever      // rechnet regelmäßig + optional optimiert
    }
    
    [CreateAssetMenu(fileName = "New Enemy", menuName = "ScriptableObjects/Enemy")]
    public class EnemyConfig : ScriptableObject
    {
        [Header("AI Type")]
        public EnemyType enemyType;
        
        [Header("Basic Info")]
        public string description; // Description of the enemy
        public Sprite[] walkAnim; // Walk animation sprites
        public Sprite[] deadAnim; // Walk animation sprites
        public Sprite[] attackAnim; // Walk animation sprites

        [Header("Core Stats")]
        public int health; // Health points
        public int tenacity; // Resistance to crowd control effects (percentage reduction)
        public int movementSpeed; // Movement speed (units per second)
        public int xpDrop;

        [Header("Combat Stats")] 
        public int attackRange;
        public int attackCooldown;
        public int attackDamage;
        public Boolean isMeele=true;
        public GameObject projectile;
        
        [Header("Special Attributes")]
        public int evasionChance; // Chance to dodge attacks (percentage)
        //armor, healthregen,...

        [Header("Damage Interaction")]
        public float fireResistance; // Modifier for fire-based attacks (percentage reduction)
        public float iceWeakness; // Modifier for ice-based attacks (percentage increase)
        public float poisonImmunity; // Boolean-like value (0 = none, 1 = immune)
    
        [Header("Abilities")]
        public bool canFly; // If the enemy can fly

        [Header("Economic Impact")]
        public int goldReward; // Gold rewarded when killed
        public int penaltyOnReachingEnd; // Player's penalty if the enemy reaches the end (e.g., health or score loss)
    
        [Header("Visual Effects")]
        public Color lightColor;
        public Boolean hasLight;
        public GameObject deathEffect; // Effect shown on death
        public GameObject hitEffect; // Effect shown when hit
        public GameObject evasionEffect; // Effect shown when hit

    }
}