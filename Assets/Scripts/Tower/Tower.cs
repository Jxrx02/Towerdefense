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
    [System.Flags]
    public enum TowerDisplayedStats
    {
        None = 0,
        Damage = 1 << 0,
        Range = 1 << 1,
        AttackSpeed = 1 << 2,
        Health = 1 << 3,
        Gold = 1 << 4,
        Regen = 1 << 5,
        DamageMultiplier = 1 << 6,
        RangeMultiplier = 1 << 7,
        AttackSpeedMultiplier = 1 << 8,
        SlowMultiplier = 1 << 9,
        Armor = 1 << 10,
        MagicResist = 1 << 11,
        CritChance = 1 << 12,
        Penetration = 1 << 13,      // zeigt Lethality, Armor Pen %, Magic Pen flat und Magic Pen %
        Omnivamp = 1 << 14,
        CritDamage = 1 << 15
    }

    public enum TargetType
    {
        First,
        Last,
        Strongest,
        Nearest
    }

    public abstract class Tower : MonoBehaviour, IDamageable
    {
        /// <summary>Alle Türme, Mauersegmente und Helden in der Szene (Grundlage fürs Gegner-Targeting).</summary>
        public static readonly List<Tower> Active = new List<Tower>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()   // nötig, falls "Domain Reload" aus ist
        {
            Active.Clear();
            Supporters.Clear();
        }

        /// <summary>Türme, die benachbarte Türme verstärken (Reichweite/Schaden/Angriffstempo/Slow). Nur diese müssen Empfänger durchsuchen.</summary>
        public static readonly List<Tower> Supporters = new List<Tower>();

        [Header("Identity")]
        [SerializeField] public string towerName;
        [SerializeField] public string towerDesc;
        [SerializeField] public int towerInitPrice;
        [SerializeField] private TowerDisplayedStats displayedStats;
        public TowerDisplayedStats DisplayedStats => displayedStats;

        [Header("Health")]
        [SerializeField] public int statHealthPoints;
        public int currentHealth;

        [Header("Defense (LoL-Stats)")]
        [Tooltip("Wird auf eine vorhandene Vitals-Komponente addiert")]
        [SerializeField]
        protected float baseArmor;
        [SerializeField] protected float baseMagicResist;

        [Header("Level-Up Bonus (pro Level)")]
        [SerializeField] private StatBonus bonusPerLevel = new StatBonus();

        [Header("Combat Stats")]
        [SerializeField] public float range;
        [SerializeField] public float interactionrange;

        [SerializeField] public int damage;

        [Range(0.1f, 50f)]
        [SerializeField] public float timeInBetweenShots;

        [SerializeField] public float attackSpeedMultiplier = 1f;

        [Header("Offense (LoL-Stats)")]
        public OffenseStats offense = new OffenseStats();

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
        protected YSort ysort;

        protected (GameObject, int) target;
        private List<GameObject> _enemiesInRange = new List<GameObject>();
        [HideInInspector] public TowerConstructionSite towerConstructionSite;

        // ── Gegner-Targeting / Vitals ───────────────────────
        private bool isBroken;
        private TargetCategory category;
        
        protected Vitals vitals;
        public Vitals Vitals => vitals;
        public TargetCategory Category => category;

        /// <summary>
        /// Rüstung und MR für die Anzeige. Funktionieren auch auf einem Prefab (Awake ist dort nie gelaufen).
        /// </summary>
        public float Armor =>
            vitals != null ? vitals.BaseArmor : baseArmor + (TryGetComponent(out Vitals v) ? v.BaseArmor : 0f);

        public float MagicResist =>
            vitals != null ? vitals.BaseMagicResist : baseMagicResist + (TryGetComponent(out Vitals v) ? v.BaseMagicResist : 0f);

        /// <summary>Darf ein Gegner dieses Ziel gerade angreifen?</summary>
        public virtual bool IsTargetable => !isBroken && isActiveAndEnabled;

        // ── Aura: Verstärkung durch Support-Türme in der Nähe ──
        // statRangeMultiplier / statDmgMultiplier / statAttackSpeedMultiplier / statSlowMultiplier sind die Werte,
        // die dieser Turm an andere Türme in seiner Reichweite abgibt. Hier stehen die Werte, die er selbst erhält.
        private const float AuraInterval = 0.5f;
        private const float MaxAuraBonus = 1.5f;      // Obergrenze (+150 %) für Reichweite, Schaden und Angriffstempo
        private const float MaxAuraSlow = 0.8f;
        private const float AuraSlowDuration = 2f;    // Sekunden, die der Slow nach einem Treffer anhält

        public float AuraRange { get; private set; }
        public float AuraDamage { get; private set; }
        public float AuraAttackSpeed { get; private set; }
        public float AuraSlow { get; private set; }

        /// <summary>Reichweite inklusive Aura-Bonus.</summary>
        public float EffectiveRange => range * (1f + AuraRange);

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

            category = this is Hero ? TargetCategory.Hero
                     : this is WallSegment ? TargetCategory.Wall
                     : TargetCategory.Tower;

            Active.Add(this);

            // currentHealth wurde bisher nirgends initialisiert
            if (currentHealth <= 0) currentHealth = statHealthPoints;

            // Vitals = Rüstung, MR, Schilde, Block, Anti-Heal.
            if (!TryGetComponent(out vitals) && this is not WallSegment)
                vitals = gameObject.AddComponent<Vitals>();

            if (vitals != null)
            {
                vitals.SetMaxHealth(statHealthPoints, true);
                vitals.SetRegen(statHealthRegenPerSecond);
                vitals.BaseArmor += baseArmor;
                vitals.BaseMagicResist += baseMagicResist;

                vitals.Damaged += _ => SyncHealth();
                vitals.Healed += _ => SyncHealth();
                vitals.Died += _ => BreakTower();
            }

            RefreshSupporterRegistration();
        }

        protected virtual void OnEnable()
        {
            // Coroutinen enden, sobald das Objekt deaktiviert wird (z. B. wenn die Baustelle den Turm ausblendet).
            // Deshalb startet die Aura hier statt in Awake und läuft nach einem Wiederaufbau weiter.
            if (this is not WallSegment)
                StartCoroutine(AuraRoutine());
        }

        protected virtual void OnDestroy()
        {
            Active.Remove(this);
            Supporters.Remove(this);
            Actions.onEnemyDeath -= this.IncreaseExp;
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
        }

        private void LevelUp()
        {
            level++;
            CalculateRequiredExp();

            ApplyBonus(bonusPerLevel);
            ShowBonusDiff(bonusPerLevel, true);
        }

        // ───────────────── AURA (Support-Türme) ─────────────────

        private bool HasSupportStats =>
            statRangeMultiplier != 0f || statDmgMultiplier != 0f ||
            statAttackSpeedMultiplier != 0f || statSlowMultiplier != 0f;

        /// <summary>Trägt diesen Turm als Support-Turm ein oder aus, je nachdem ob er Aura-Werte hat.</summary>
        private void RefreshSupporterRegistration()
        {
            bool shouldSupport = HasSupportStats && !isBroken;

            if (shouldSupport && !Supporters.Contains(this))
                Supporters.Add(this);
            else if (!shouldSupport)
                Supporters.Remove(this);
        }

        private IEnumerator AuraRoutine()
        {
            // Versetzt starten, damit nicht alle Türme im selben Frame rechnen
            yield return new WaitForSeconds(UnityEngine.Random.value * AuraInterval);

            var wait = new WaitForSeconds(AuraInterval);
            while (true)
            {
                RecalculateAura();
                yield return wait;
            }
        }

        /// <summary>
        /// Summiert die Aura-Werte aller Support-Türme, in deren Reichweite dieser Turm steht.
        /// Radius der Aura = Reichweite (range) des Support-Turms.
        /// </summary>
        private void RecalculateAura()
        {
            float rangeBonus = 0f, damageBonus = 0f, attackSpeedBonus = 0f, slow = 0f;

            for (int i = 0; i < Supporters.Count; i++)
            {
                Tower support = Supporters[i];
                if (support == null || support == this) continue;

                if (Vector2.Distance(transform.position, support.transform.position) > support.range)
                    continue;

                rangeBonus += support.statRangeMultiplier;
                damageBonus += support.statDmgMultiplier;
                attackSpeedBonus += support.statAttackSpeedMultiplier;
                slow += support.statSlowMultiplier;
            }

            rangeBonus = Mathf.Clamp(rangeBonus, 0f, MaxAuraBonus);
            damageBonus = Mathf.Clamp(damageBonus, 0f, MaxAuraBonus);
            attackSpeedBonus = Mathf.Clamp(attackSpeedBonus, 0f, MaxAuraBonus);
            slow = Mathf.Clamp(slow, 0f, MaxAuraSlow);

            bool rangeChanged = !Mathf.Approximately(rangeBonus, AuraRange);

            ShowAuraDiff("Aura Range %", AuraRange, rangeBonus, new Color(0.5f, 0.9f, 1f));
            ShowAuraDiff("Aura Dmg %", AuraDamage, damageBonus, new Color(1f, 0.4f, 0.4f));
            ShowAuraDiff("Aura AtkSpeed %", AuraAttackSpeed, attackSpeedBonus, new Color(1f, 0.7f, 0.2f));
            ShowAuraDiff("Aura Slow %", AuraSlow, slow, new Color(0.6f, 0.4f, 1f));

            AuraRange = rangeBonus;
            AuraDamage = damageBonus;
            AuraAttackSpeed = attackSpeedBonus;
            AuraSlow = slow;

            if (rangeChanged)
                DrawRangeIndicatior();
        }

        private void ShowAuraDiff(string label, float oldValue, float newValue, Color color)
        {
            if (statDiffDisplay == null || Mathf.Approximately(oldValue, newValue)) return;
            statDiffDisplay.ShowDiff(label, oldValue * 100f, newValue * 100f, color);
        }

        // ───────────────── STAT-BONI (Upgrades und Level-Ups) ─────────────────

        /// <summary>Addiert einen Stat-Zuwachs auf den Turm, seine Offense-Stats und seine Vitals.</summary>
        private void ApplyBonus(StatBonus b)
        {
            if (b == null) return;

            damage += b.damage;
            range += b.range;
            timeInBetweenShots = Mathf.Max(0.1f, timeInBetweenShots - b.cooldownDecrease);

            statHealthPoints += b.health;
            statHealthRegenPerSecond += b.healthRegen;

            offense.Apply(b);

            if (vitals != null)
            {
                if (b.health != 0) vitals.AddMaxHealth(b.health);
                vitals.BaseArmor += b.armor;
                vitals.BaseMagicResist += b.magicResist;
                vitals.SetRegen(statHealthRegenPerSecond);
            }

            DrawRangeIndicatior();
        }

        /// <summary>Zeigt nur die Werte, die sich geändert haben. includeBasics = false, wenn Schaden/Reichweite/HP schon anderswo angezeigt werden.</summary>
        private void ShowBonusDiff(StatBonus b, bool includeBasics)
        {
            if (statDiffDisplay == null || b == null) return;

            if (includeBasics)
            {
                ShowStatDiff("Damage", damage, b.damage, Color.red);
                ShowStatDiff("Range", range, b.range, new Color(0.2f, 0.8f, 1f));
                ShowStatDiff("Health", statHealthPoints, b.health, new Color(0f, 0.9f, 0f));
                ShowStatDiff("Regen/sec", statHealthRegenPerSecond, b.healthRegen, new Color(0.3f, 1f, 0.3f));
            }

            ShowStatDiff("Armor", vitals != null ? vitals.BaseArmor : 0f, b.armor, new Color(1f, 0.8f, 0.3f));
            ShowStatDiff("Magic Resist", vitals != null ? vitals.BaseMagicResist : 0f, b.magicResist, new Color(0.5f, 0.6f, 1f));
            ShowStatDiff("Crit %", offense.critChance * 100f, b.critChance * 100f, new Color(1f, 0.5f, 0.1f));
            ShowStatDiff("Crit Damage", offense.critDamage * 100f, b.critDamage * 100f, new Color(1f, 0.4f, 0.1f));
            ShowStatDiff("Lethality", offense.lethality, b.lethality, new Color(0.9f, 0.2f, 0.2f));
            ShowStatDiff("Armor Pen %", offense.armorPenPercent * 100f, b.armorPenPercent * 100f, new Color(0.9f, 0.3f, 0.3f));
            ShowStatDiff("Magic Pen", offense.magicPenFlat, b.magicPenFlat, new Color(0.7f, 0.3f, 1f));
            ShowStatDiff("Magic Pen %", offense.magicPenPercent * 100f, b.magicPenPercent * 100f, new Color(0.7f, 0.3f, 1f));
            ShowStatDiff("Omnivamp %", offense.omnivamp * 100f, b.omnivamp * 100f, new Color(0.9f, 0.1f, 0.5f));
        }

        private void ShowStatDiff(string label, float current, float added, Color color)
        {
            if (Mathf.Approximately(added, 0f)) return;
            statDiffDisplay.ShowDiff(label, current - added, current, color);
        }

        private void CalculateRequiredExp()
        {
            requiredExperience = levelConfig.GetRequiredExp(level);
        }



        /// <summary>
        /// Neuer Weg (Gegner, Projektile). Ohne Vitals wird auf TakeDamage(int) gerundet,
        /// damit Overrides wie WallSegment weiter greifen.
        /// </summary>
        public virtual DamageResult TakeDamage(DamageInfo info)
        {
            if (vitals != null)
                return vitals.TakeDamage(info);

            return default;
        }

        protected void SyncHealth() => currentHealth = Mathf.CeilToInt(vitals.Health);

        /// <summary>Für Respawn (Hero) oder Reparatur.</summary>
        protected void RestoreFullHealth()
        {
            currentHealth = statHealthPoints;
            if (vitals != null) vitals.ResetState();
        }

        // ───────────────── ATTACK CORE ─────────────────

        public abstract void Attack((GameObject, int) _target);

        protected IEnumerator BaseAttackCoroutine(AnimationState attackanimation, Action onShoot = null)
        {
            if (target.Item1 != null)
            {
                UpdateLookDirection(target.Item1.transform.position);

                spriteAnim.SetAttackSpeed(attackSpeedMultiplier * (1f + AuraAttackSpeed));
                spriteAnim.animState = attackanimation;

                bool animationComplete = false;
                void AnimationFinished() => animationComplete = true;
                spriteAnim.OnAttackAnimationComplete += AnimationFinished;
                yield return new WaitUntil(() => animationComplete);
                spriteAnim.OnAttackAnimationComplete -= AnimationFinished;

                onShoot?.Invoke();

                yield return new WaitForSeconds(
                    timeInBetweenShots /
                    (attackSpeedMultiplier * (1f + AuraAttackSpeed))
                );
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
        /// Spawnt ein Projektil und überträgt Schaden (inkl. Crit, Penetration, Omnivamp) vom Turm.
        /// </summary>
        /// 
        protected void Shoot() => Shoot(1f);

        protected void Shoot(float damageMultiplier)
        {
            if (target.Item1 == null)
                return;

            if (projectilePrefab == null)
                return;

            GameObject go = Instantiate(
                projectilePrefab,
                spawnProjectileOffsetPoint.position,
                Quaternion.identity
            );

            var proj = go.GetComponent<Projectile>();

            if (proj == null)
                return;

            // Schaden inklusive Aura-Bonus von Support-Türmen in der Nähe
            int finalTowerDamage = Mathf.RoundToInt(damage * (1f + AuraDamage) * damageMultiplier);

            // Die Schadensart kommt vom Projektil-Prefab (Projectile.damageType)
            DamageInfo info = offense.CreateDamage(finalTowerDamage, DamageType.Physical, gameObject);

            // Slow-Aura: Treffer verlangsamen den Gegner
            if (AuraSlow > 0f)
            {
                info.slowAmount = AuraSlow;
                info.slowDuration = AuraSlowDuration;
            }

            proj.Init(target, info);
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
            return Vector3.Distance(transform.position, obj.transform.position) <= EffectiveRange;
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
                // Lebenspunkte plus Schild
                Vitals v = enemies[i] != null ? enemies[i].GetComponent<Enemy>()?.Vitals : null;
                float hp = v != null ? v.Health + v.Shield : 0f;

                if (hp > maxHealth)
                {
                    strongest = enemies[i];
                    maxHealth = hp;
                    bestPos = i;
                }
            }
            return (strongest, bestPos);
        }

        private (GameObject, int) FindNearestEnemy(List<GameObject> enemies)
        {
            GameObject nearest = null;
            float shortest = EffectiveRange;
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
        }

        public void PreviousTargetType()
        {
            int count = System.Enum.GetValues(typeof(TargetType)).Length;
            targetPreference = (TargetType)(((int)targetPreference - 1 + count) % count);
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
                float scaleFactor = (EffectiveRange * 2) / spriteDiameter;
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

        // ───────────────── UPGRADES ─────────────────

        public virtual void UpgradePath(int pathIndex)
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
                float oldHealth           = statHealthPoints;
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
                if (vitals != null) vitals.AddMaxHealth(lvl.healthpoints);

                statRangeMultiplier      += lvl.rangeMultiplier;
                statDmgMultiplier        += lvl.dmgMultiplier;
                statAttackSpeedMultiplier+= lvl.attackSpeedMultiplier;
                statSlowMultiplier       += lvl.slowMultiplier;

                // Neue Stats (Rüstung, MR, Crit, Penetration, Omnivamp ...) und optionale Zusatzwerte
                ApplyBonus(lvl.bonus);
                RefreshSupporterRegistration();

                pathLevels[pathIndex]++;

                // Nur ersetzen, wenn das Upgrade eigene Sprites/Projektile hat (sonst würde die Animation bzw. der Schuss leer)
                if (lvl.idle_sprites != null && lvl.idle_sprites.Length > 0)
                    spriteAnim.idle_sprites = lvl.idle_sprites;
                if (lvl.attack_sprites != null && lvl.attack_sprites.Length > 0)
                    spriteAnim.attack_sprites = lvl.attack_sprites;

                GameObject oldProjectilePrefab = projectilePrefab;
                if (lvl.projectile != null)
                    projectilePrefab = lvl.projectile;

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
                    ShowBonusDiff(lvl.bonus, false);
                }

                Debug.Log($"Pfad {path} auf Level {pathLevels[pathIndex]} verbessert!");
            }
            else
            {
                Debug.Log($"Pfad {path} ist bereits maximal verbessert!");
            }

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

        // ───────────────── UPGRADE INTERACTION (Hold E) ─────────────────
        private Coroutine upgradeInteractionCoroutine;
        private const float UpgradeHoldDuration = 4f;

        /// <summary>Index des Pfads, der bereits gekauft wurde, sonst -1.</summary>
        public int GetActivePathIndex()
        {
            for (int i = 0; i < upgradePaths.Length && i < pathLevels.Length; i++)
            {
                if (pathLevels[i] > 0)
                    return i;
            }
            return -1;
        }

        /// <summary>Gesperrt, wenn bereits ein anderer Pfad gewählt wurde.</summary>
        public bool IsPathLocked(int pathIndex)
        {
            int active = GetActivePathIndex();
            return active >= 0 && active != pathIndex;
        }

        public virtual void EnterInteractionRange(Hero hero)
        {
            if (this is WallSegment || this is Hero)
                return;

            if (LevelManager.instance == null || !LevelManager.instance.isDay)
                return;

            if (InteractionPopup.instance == null)
                return;

            if (!InteractionPopup.instance.ShowTowerUpgrade(this, transform.position))
                return;

            if (upgradeInteractionCoroutine != null)
                StopCoroutine(upgradeInteractionCoroutine);

            upgradeInteractionCoroutine = StartCoroutine(HoldUpgradeInteraction());
        }

        public virtual void ExitInteractionRange(Hero hero)
        {
            StopUpgradeInteraction();
        }

        protected void StopUpgradeInteraction()
        {
            if (upgradeInteractionCoroutine != null)
            {
                StopCoroutine(upgradeInteractionCoroutine);
                upgradeInteractionCoroutine = null;
            }

            if (InteractionPopup.instance != null)
                InteractionPopup.instance.HideFor(this);
        }

        private IEnumerator HoldUpgradeInteraction()
        {
            InteractionPopup popup = InteractionPopup.instance;

            while (true)
            {
                yield return new WaitUntil(() => Input.GetKeyDown(KeyCode.E));

                float timer = 0f;
                bool completed = false;

                while (Input.GetKey(KeyCode.E))
                {
                    timer += Time.deltaTime;
                    popup.SetProgress(Mathf.Clamp01(timer / UpgradeHoldDuration));

                    if (timer >= UpgradeHoldDuration)
                    {
                        completed = true;
                        break;
                    }

                    yield return null;
                }

                if (!completed)
                {
                    popup.ResetProgress();
                    continue;
                }

                if (TryUpgradePath(popup.SelectedIndex))
                {
                    // Popup mit neuen Werten aktualisieren; false = alles maximal
                    if (!popup.ShowTowerUpgrade(this, transform.position))
                    {
                        upgradeInteractionCoroutine = null;
                        yield break;
                    }
                }
                else
                {
                    popup.ResetProgress();
                }

                yield return new WaitUntil(() => !Input.GetKey(KeyCode.E));
            }
        }

        public bool TryUpgradePath(int pathIndex)
        {
            if (pathIndex < 0 || pathIndex >= upgradePaths.Length) return false;
            if (IsPathLocked(pathIndex)) return false;
            if (upgradePaths[pathIndex] == null) return false;

            int level = pathLevels[pathIndex];
            if (level >= upgradePaths[pathIndex].levels.Length) return false;
            if (!IsUpgradeUnlocked(pathIndex)) return false;

            int cost = upgradePaths[pathIndex].levels[level].upgradeCost;
            if (!LevelManager.instance.DoPurchase(cost)) return false;

            UpgradePath(pathIndex);
            return true;
        }

        public bool IsUpgradeUnlocked(int pathIndex)
        {
            var path = upgradePaths[pathIndex];
            int level = pathLevels[pathIndex];

            if (path == null || level >= path.levels.Length)
                return false;

            string pathName = path.towerName;
            string upgradeName = path.levels[level].upgradeName;

            var researchManager = TowerDefense.Research.ResearchManager.Instance;

            return researchManager == null || researchManager.unlockedResearch.Any(t =>
                t.researchedPaths.Any(p => p.pathName.Contains(pathName)) &&
                t.researchedPaths.Any(p => p.unlockedUpgrades.Contains(upgradeName)));
        }

        // ───────────────── BREAK / SELL ─────────────────
        protected virtual void BreakTower()
        {
            isBroken = true;
            Active.Remove(this);
            Supporters.Remove(this);

            StopUpgradeInteraction();
            Actions.onEnemyDeath -= this.IncreaseExp;
            TowerHeroManager.instance.UnRegisterTower(this.gameObject);
            TowerHeroManager.instance.DeselectTower();

            towerConstructionSite.IsDestroyed = true;
            towerConstructionSite.gameObject.SetActive(true);
        }

        /// <summary>Aufrufen, wenn ein zerstörter Turm wieder aufgebaut/repariert wird.</summary>
        public void ResetBroken()
        {
            isBroken = false;
            RestoreFullHealth();
            if (!Active.Contains(this)) Active.Add(this);
            RefreshSupporterRegistration();
        }

        protected virtual void DestroyTower()
        {
            Active.Remove(this);
            Supporters.Remove(this);

            StopUpgradeInteraction();
            Actions.onEnemyDeath -= this.IncreaseExp;
            TowerHeroManager.instance.UnRegisterTower(this.gameObject);
            TowerHeroManager.instance.DeselectTower();
            towerConstructionSite.gameObject.SetActive(true);
            towerConstructionSite.IsBuilt = false;
            Destroy(this.gameObject);
        }

        public void SellTower()
        {
            LevelManager.instance.cur_coins += CalculateSellPrice();

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