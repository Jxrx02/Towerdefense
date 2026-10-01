using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ScriptableObjects;
using TowerDefense.GridMovement;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense
{
    public enum TargetType
    {
        First,
        Last,
        Strongest,
        Nearest
    }

    public enum PlacementType
    {
        Tower,
        Wall
    }
    public abstract class Tower : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] public string towerName;
        [SerializeField] public string towerDesc;
        [SerializeField] public int towerInitPrice;

        [Header("Placement / World Interaction")]
        [SerializeField] public PlacementType placementType = PlacementType.Tower;
        [SerializeField] public bool blocksPath = false;

        [Header("Health")]
        [SerializeField] public int statHealthPoints;
        public int currentHealth;

        [Header("Combat Stats")]
        [SerializeField] public float range;
        [SerializeField] public float interactionrange;

        [SerializeField] public int damage;

        [Range(0.1f, 50f)]
        [SerializeField] public float timeInBetweenShots;

        [SerializeField] public float attackSpeedMultiplier = 1f;

        [Header("Visuals")]
        [SerializeField] public SpriteRenderer rangeIndicator;
        [SerializeField] private Vector3 rangeIndicatorOffset;

        [SerializeField] public SpriteRenderer interactionIndicator;
        [SerializeField] private Vector3 interactionIndicatorOffset;

        [SerializeField] public Material outlineMaterial;

        [SerializeField] public TargetType targetPreference = TargetType.Nearest;

        private Material notOutlinedMaterial;
        protected internal SpriteAnim spriteAnim;
        protected SpriteRenderer sr;
        private StatDiffDisplay statDiffDisplay;

        [Header("Economic / Utility Stats")]
        [SerializeField] public float statCoinsEarnedPerSecond;
        [SerializeField] public float statHealthRegenPerSecond;

        [Header("Multipliers")]
        [SerializeField] public float statRangeMultiplier;
        [SerializeField] public float statDmgMultiplier;
        [SerializeField] public float statAttackSpeedMultiplier;
        [SerializeField] public float statSlowMultiplier;

        [Header("Upgrades")]
        [SerializeField] protected Transform spawnProjectileOffsetPoint;
        public UpgradePath[] upgradePaths = new UpgradePath[3];
        public int[] pathLevels = new int[5];

        [Header("Progression")]
        [HideInInspector] public int level = 1;
        [HideInInspector] public int experience;
        [HideInInspector] public int requiredExperience;

        public LevelConfig levelConfig;

        [HideInInspector] public Boolean isAttacking = false;
        public GameObject projectilePrefab;
    
        [Header("Targeting")]
        protected Boolean isSelected;
        protected Boolean isHighlighted; 
        protected Boolean isInteracted;
        protected Boolean isPlaceable;
        protected YSort ysort;

        protected (GameObject, int) target;
        private List<GameObject> _enemiesInRange = new List<GameObject>();

        // ───────────────── INIT ─────────────────
        protected virtual void Awake()
        {
            spriteAnim = GetComponent<SpriteAnim>();

            if (spriteAnim != null)
                spriteAnim.animState = AnimationState.Idle_Animation;

            sr = GetComponent<SpriteRenderer>();
            ysort = GetComponent<YSort>();
            
            if (sr != null)
                notOutlinedMaterial = sr.material;

            statDiffDisplay = GetComponent<StatDiffDisplay>();
            Actions.onEnemyDeath += this.IncreaseExp;
            DrawRangeIndicatior();
            
            
        }


        // ───────────────── XP / LEVEL / Health ─────────────────
        private void IncreaseExp(GameObject enemy)
        {
            if (this == null || !this.gameObject) return;
            if (!IsObjectInRange(enemy)) return;
            var value = enemy.GetComponent<Enemy>().enemyConfig.xpDrop;
            experience += value;

            if (experience >= requiredExperience)
            {
                while (experience >= requiredExperience)
                {
                    experience -= requiredExperience;
                    LevelUp();
                    statDiffDisplay.ShowDiff("Level", level - 1, level, Color.cornsilk);
                }
            }
            TowerUI.Instance.UpdateUI();
        }

        private void LevelUp()
        {
            level++;
            CalculateRequiredExp();
        }

        private void CalculateRequiredExp()
        {
            requiredExperience = levelConfig.GetRequiredExp(level);
        }
        public virtual void TakeDamage(int damage)
        {
            currentHealth -= damage;

            if (currentHealth <= 0)
                DestroyTower();
        }

        // ───────────────── ATTACK CORE ─────────────────

        public abstract void Attack((GameObject, int) _target);

        protected IEnumerator BaseAttackCoroutine(AnimationState attackanimation, Action onShoot = null)
        {
            if (target.Item1 != null)
            {
                UpdateLookDirection(target.Item1.transform.position);

                spriteAnim.SetAttackSpeed(attackSpeedMultiplier);
                spriteAnim.animState = attackanimation;

                bool animationComplete = false;
                void AnimationFinished() => animationComplete = true;
                spriteAnim.OnAttackAnimationComplete += AnimationFinished;
                yield return new WaitUntil(() => animationComplete);
                spriteAnim.OnAttackAnimationComplete -= AnimationFinished;

                onShoot?.Invoke();

                yield return new WaitForSeconds(timeInBetweenShots);
                isAttacking = false;
            }
            else
            {
                isAttacking = false;
                spriteAnim.animState = AnimationState.Idle_Animation;
            }
            
            ysort.UpdateSorting();
        }


        protected virtual void UpdateLookDirection(Vector3 targetPos)
        {
            Quaternion canvasRot = GetComponentInChildren<Canvas>().transform.rotation;

            bool faceRight = targetPos.x > transform.position.x;

            Quaternion rotation = faceRight
                ? Quaternion.Euler(0, 180, 0)
                : Quaternion.identity;

            transform.rotation = rotation;

            if (statDiffDisplay != null)
                statDiffDisplay.gameObject.transform.rotation = rotation;

            GetComponentInChildren<Canvas>().transform.rotation = canvasRot;
        }
        
        /// <summary>
        /// Spawnt ein Projektil und überträgt Schaden + optionale Projektil-Überschreibungen vom Turm.
        /// </summary>
        protected void Shoot()
        {
            if (target.Item1 == null)
            {
                return;
            }

            if (projectilePrefab == null)
            {
                return;
            }

            GameObject go = Instantiate(
                projectilePrefab,
                spawnProjectileOffsetPoint.position,
                Quaternion.identity
            );

            var proj = go.GetComponent<Projectile>();

            if (proj == null)
            {
                return;
            }

            int finalTowerDamage =
                Mathf.RoundToInt(
                    damage * Mathf.Max(1f, statDmgMultiplier)
                );

            proj.Init(target, finalTowerDamage);
        }

        public (GameObject, int) FindTargetInRange(List<GameObject> enemies)
        {
            _enemiesInRange.Clear();
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] != null && IsObjectInRange(enemies[i]))
                    _enemiesInRange.Add(enemies[i]);
            }
            if (_enemiesInRange.Count == 0) return (null, 0);
            return FindTargetByPreference(_enemiesInRange);
        }

        // ───────────────── TARGETING ─────────────────

        public Boolean IsObjectInRange(GameObject obj)
        {
            return Vector3.Distance(transform.position, obj.transform.position) <= range;
        }

        public (GameObject, int) FindTargetByPreference(List<GameObject> enemies)
        {
            switch (targetPreference)
            {
                case TargetType.First:     return (FindFirstEnemy(enemies), 0);
                case TargetType.Last:      return (FindLastEnemy(enemies), enemies.Count - 1);
                case TargetType.Strongest: return FindStrongestEnemy(enemies);
                case TargetType.Nearest:   return FindNearestEnemy(enemies);
                default:                   return (null, 0);
            }
        }

        private GameObject FindFirstEnemy(List<GameObject> enemies)
            => enemies.Count > 0 ? enemies[0] : null;

        private GameObject FindLastEnemy(List<GameObject> enemies)
            => enemies.Count > 0 ? enemies[enemies.Count - 1] : null;

        private (GameObject, int) FindStrongestEnemy(List<GameObject> enemies)
        {
            GameObject strongest = null;
            int bestPos = 0;
            float maxHealth = 0;

            for (int i = 0; i < enemies.Count; i++)
            {
                var ec = enemies[i].GetComponent<Enemy>();
                if (ec != null && ec.currentHealth > maxHealth)
                {
                    strongest = enemies[i];
                    maxHealth = ec.currentHealth;
                    bestPos = i;
                }
            }
            return (strongest, bestPos);
        }

        private (GameObject, int) FindNearestEnemy(List<GameObject> enemies)
        {
            GameObject nearest = null;
            float shortest = range;
            int bestPos = 0;

            for (int i = 0; i < enemies.Count; i++)
            {
                float d = Vector3.Distance(transform.position, enemies[i].transform.position);
                if (d < shortest)
                {
                    nearest = enemies[i];
                    shortest = d;
                    bestPos = i;
                }
            }
            return (nearest, bestPos);
        }
        public void NextTargetType()
        {
            targetPreference = (TargetType)(((int)targetPreference + 1) %
                                            System.Enum.GetValues(typeof(TargetType)).Length);
            TowerUI.Instance.txtTargetPreference.text = targetPreference.ToString();
        }

        public void PreviousTargetType()
        {
            int count = System.Enum.GetValues(typeof(TargetType)).Length;
            targetPreference = (TargetType)(((int)targetPreference - 1 + count) % count);
            TowerUI.Instance.txtTargetPreference.text = targetPreference.ToString();
        }
        // ───────────────── UI / SELECTION ─────────────────
         
        public void SetIsSelected(Boolean _isSelected)
        {
            ysort.UpdateSorting();
            DrawRangeIndicatior();
            isSelected = _isSelected;

            if (isSelected)
            {
                rangeIndicator.gameObject.SetActive(true);
            }
            else
            {
                rangeIndicator.gameObject.SetActive(false);
            }
        }
        public void SetHighlighted(bool _isHighlighted)
        {
            ysort.UpdateSorting();
            isHighlighted = _isHighlighted;

            if (sr == null)
            {
                Debug.LogError(
                    $"Tower '{name}' hat keinen SpriteRenderer für Highlighting!",
                    this
                );
                return;
            }

            if (isHighlighted)
            {
                if (outlineMaterial == null)
                {
                    Debug.LogError(
                        $"Tower '{name}' hat kein Outline-Material!",
                        this
                    );
                    return;
                }

                sr.material = outlineMaterial;
            }
            else
            {
                if (notOutlinedMaterial != null && this is not WallSegment)
                    sr.material = notOutlinedMaterial;
            }
        }


        public void SetInteraction(Boolean _isInteracted)
        {
            isInteracted = _isInteracted;
            DrawInteractionIndicatior();

            if (isSelected)
            {
                interactionIndicator.enabled = true;
            }
            else
            {
                interactionIndicator.enabled = false;
            }
        }
        
        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position + rangeIndicatorOffset, range);
            GetComponent<CircleCollider2D>().radius = range;
            GetComponent<CircleCollider2D>().offset = rangeIndicatorOffset;
            DrawRangeIndicatior();
            
            //Interactionindicator
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position + rangeIndicatorOffset, interactionrange);
            GetComponent<CircleCollider2D>().radius = interactionrange;
            GetComponent<CircleCollider2D>().offset = rangeIndicatorOffset;

            DrawInteractionIndicatior();
        }

        private void DrawRangeIndicatior()
        {
            if (rangeIndicator != null)
            {
                float spriteDiameter = rangeIndicator.sprite.bounds.size.x;
                float scaleFactor = (range * 2) / spriteDiameter;
                rangeIndicator.transform.position = transform.position + rangeIndicatorOffset;
                rangeIndicator.transform.localScale = new Vector3(scaleFactor, scaleFactor, 1);
            }
        }
        

        private void DrawInteractionIndicatior()
        {
            if (interactionIndicator != null)
            {
                float spriteDiameter = interactionIndicator.sprite.bounds.size.x;
                float scaleFactor = (interactionrange * 2) / spriteDiameter;
                interactionIndicator.transform.position = transform.position + rangeIndicatorOffset;
                interactionIndicator.transform.localScale = new Vector3(scaleFactor, scaleFactor, 1);
            }
        }

        
        
        public void UpgradePath(int pathIndex)
        {
            if (pathIndex < 0 || pathIndex >= upgradePaths.Length) return;

            
            UpgradePath path = upgradePaths[pathIndex];
            int currentLevel = pathLevels[pathIndex];

            if (currentLevel < path.levels.Length)
            {
                TowerUpgradeLevel lvl = path.levels[currentLevel];

                float oldDamage           = damage;
                float oldRange            = range;
                float oldFireRate         = 1f / timeInBetweenShots;
                float oldCoinsPerSecond   = statCoinsEarnedPerSecond;
                float oldRegen            = statHealthRegenPerSecond;
                float oldHealth           = statHealthRegenPerSecond;
                float oldDmgMult          = statDmgMultiplier;
                float oldRangeMult        = statRangeMultiplier;
                float oldAtkSpeedMult     = statAttackSpeedMultiplier;
                float oldSlowMult         = statSlowMultiplier;

                towerName              = lvl.upgradeName;
                towerDesc              = lvl.description;
                damage                += lvl.damageIncrease;
                range                 += lvl.rangeIncrease;
                timeInBetweenShots     = (timeInBetweenShots - lvl.timeInBetweenShotsDecrease) > 0.1f
                                            ? (timeInBetweenShots - lvl.timeInBetweenShotsDecrease)
                                            : 0.1f;
                attackSpeedMultiplier += lvl.attackSpeedIncrease;

                statCoinsEarnedPerSecond += lvl.coinsEarnedPerSecond;
                statHealthRegenPerSecond += lvl.healthRegenPerSecond;
                statHealthPoints  += lvl.healthpoints;

                
                statRangeMultiplier      += lvl.rangeMultiplier;
                statDmgMultiplier        += lvl.dmgMultiplier;
                statAttackSpeedMultiplier+= lvl.attackSpeedMultiplier;
                statSlowMultiplier       += lvl.slowMultiplier;

                pathLevels[pathIndex]++;

                spriteAnim.idle_sprites   = lvl.idle_sprites;
                spriteAnim.attack_sprites = lvl.attack_sprites;
                GameObject oldProjectilePrefab = projectilePrefab;  
                projectilePrefab               = lvl.projectile;
                
               /* if (this is WallGroup wall)
                {
                 //   wall.ApplyWallTile(lvl.wallTile);
                }*/
                
                if (statDiffDisplay != null)
                {
                    statDiffDisplay.ShowDiff("Damage",         oldDamage,         damage,                     Color.red);
                    statDiffDisplay.ShowDiff("Range",          oldRange,          range,                      new Color(0.2f, 0.8f, 1f));
                    statDiffDisplay.ShowDiff("Abklingzeit",    oldFireRate,       1f / timeInBetweenShots,    new Color(1f, 0.35f, .6f));
                    statDiffDisplay.ShowDiff("Gold/sec",       oldCoinsPerSecond, statCoinsEarnedPerSecond,   new Color(1f, 0.84f, 0f));
                    statDiffDisplay.ShowDiff("Regen/sec",      oldRegen,          statHealthRegenPerSecond,   new Color(0.3f, 1f, 0.3f));
                    statDiffDisplay.ShowDiff("Health",         oldHealth,         statHealthPoints,           new Color(0f, 0.9f, 0f));
                    statDiffDisplay.ShowDiff("Dmg Mult.",      oldDmgMult,        statDmgMultiplier,          new Color(1f, 0.4f, 0.4f));
                    statDiffDisplay.ShowDiff("Range Mult.",    oldRangeMult,      statRangeMultiplier,        new Color(0.5f, 0.9f, 1f));
                    statDiffDisplay.ShowDiff("AtkSpeed Mult.", oldAtkSpeedMult,   statAttackSpeedMultiplier,  new Color(1f, 0.7f, 0.2f));
                    statDiffDisplay.ShowDiff("Slow Mult.",     oldSlowMult,       statSlowMultiplier,         new Color(0.6f, 0.4f, 1f));
                    
                    ShowProjectileDiff(oldProjectilePrefab, projectilePrefab);

                }

                Debug.Log($"Pfad {path} auf Level {pathLevels[pathIndex]} verbessert!");
            }
            else
            {
                Debug.Log($"Pfad {path} ist bereits maximal verbessert!");
            }

            TowerUI.Instance.UpdateUI();
            DrawRangeIndicatior();
        }
        private void ShowProjectileDiff(GameObject oldPrefab, GameObject newPrefab)
        {
            if (statDiffDisplay == null || newPrefab == null) return;

            Projectile newProj = newPrefab.GetComponent<Projectile>();
            if (newProj == null) return;

            Projectile oldProj = oldPrefab != null ? oldPrefab.GetComponent<Projectile>() : null;

            var newStats = newProj.GetEffectStats();
            var oldStats = oldProj != null ? oldProj.GetEffectStats() : null;

            foreach (var stat in newStats)
            {
                float oldValue = 0f;
                if (oldStats != null)
                {
                    foreach (var os in oldStats)
                    {
                        if (os.label == stat.label) { oldValue = os.value; break; }
                    }
                }
                statDiffDisplay.ShowDiff(stat.label, oldValue, stat.value, stat.color);
            }
        }


        // ───────────────── SELL ─────────────────

        protected virtual void DestroyTower()
        {

            Actions.onEnemyDeath -= this.IncreaseExp;
            TowerHeroManager.instance.UnRegisterTower(this.gameObject);
            TowerHeroManager.instance.DeselectTower();

            Destroy(this.gameObject);
        }
        public void SellTower()
        {
            LevelManager.instance.cur_coins += CalculateSellPrice();
            if (this is Hero && LevelManager.instance.heroFielded)
                LevelManager.instance.heroFielded = false;

            DestroyTower();

        }
        
        // ───────────────── Towerinteraction among towers ─────────────────

        public void EnterTowerRange(Tower tower)
        {
            ysort.UpdateSorting();
            //TowerSynergy
        }
        public void ExitTowerRange(Tower tower)
        {
            ysort.UpdateSorting();
        }
        public int CalculateSellPrice()
        {
            int coins = towerInitPrice;
            for (int i = 0; i < upgradePaths.Length; i++)
            {
                UpgradePath path = upgradePaths[i];
                int lvl = pathLevels[i];
                for (int j = 0; j < lvl; j++)
                    coins += path.levels[j].upgradeCost;
            }
            return (int)(coins * 0.8f);
        }


    }
}