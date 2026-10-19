using System.Collections;
using System.Collections.Generic;
using ScriptableObjects;
using TowerDefense.GridMovement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;

namespace TowerDefense
{
    /// <summary>
    /// Gegner mit einer einzigen KI (die frühere "Clever"-Variante):
    ///  - läuft zum Levelende und berechnet den Weg regelmäßig neu,
    ///  - greift Mauern an, die den Weg versperren (pathBlocker, kommt vom PathfindingManager),
    ///  - verfolgt und greift Türme und Helden an, die in Aggro-Reichweite kommen (aggroTarget).
    /// Das Aggro-Ziel hat Vorrang. Der Weg zu beiden Zielarten läuft über das Grid.
    /// </summary>
    [RequireComponent(typeof(Vitals))]
    public class Enemy : MonoBehaviour, IDamageable
    {
        public EnemyConfig enemyConfig;
        [SerializeField] private float spreadRadius = 0.3f;

        private const float RetargetInterval = 0.1f;
        private const float UnreachableCooldown = 3f;

        // ── Komponenten ─────────────────────────────────────
        private Vitals vitals;
        private SpriteAnim _spriteAnim;
        private YSort ySort;

        public Vitals Vitals => vitals;

        /// <summary>Nur lesbar, die HP liegen in Vitals.</summary>
        public int currentHealth => vitals != null ? Mathf.CeilToInt(vitals.Health) : 0;

        // ── Pfad ────────────────────────────────────────────
        private Transform levelEnd;
        private List<Vector3> currentPath;
        private int pathIndex;
        private GridNode reservedSlot;
        private Vector3 spreadOffset;
        private bool reachedFinalTarget;
        private float repathTimer;

        // ── Ziele ───────────────────────────────────────────
        private Tower aggroTarget;      // Turm/Held, den wir aktiv verfolgen
        private Tower pathBlocker;      // Mauer, die den Weg zum Levelende versperrt
        private float retargetTimer;
        private readonly Dictionary<Tower, float> unreachable = new();

        private Tower CurrentTarget => aggroTarget != null ? aggroTarget : pathBlocker;

        // ── Zustand ─────────────────────────────────────────
        private bool isDead;
        private bool isAttacking;
        private bool isKnockbacking;
        private bool isBeingPulled;

        private float slowMultiplier = 1f;
        private Coroutine slowCoroutine;
        private Coroutine blockRechargeRoutine;

        public bool IsBeingPulled
        {
            get => isBeingPulled;
            set => isBeingPulled = value;
        }

        private bool IsCrowdControlled => isKnockbacking || isBeingPulled;

        // ───────────────── INIT ─────────────────────────────

        private void Awake()
        {
            if (!TryGetComponent(out vitals))
                vitals = gameObject.AddComponent<Vitals>();

            _spriteAnim = GetComponent<SpriteAnim>();
            ySort = GetComponent<YSort>();

            vitals.Died += HandleDied;
            vitals.Blocked += HandleBlocked;
        }

        private void OnEnable()
        {
            Actions.onGridChanged += OnGridChanged;
        }

        private void OnDisable()
        {
            Actions.onGridChanged -= OnGridChanged;
        }

        private void Start()
        {
            spreadOffset = Random.insideUnitCircle * spreadRadius;
            retargetTimer = Random.value * RetargetInterval;   // Gegner nicht im Gleichtakt suchen lassen

            ApplyConfig();
            CalculatePath();
            UpdateSorting();
        }

        private void OnDestroy()
        {
            if (vitals != null)
            {
                vitals.Died -= HandleDied;
                vitals.Blocked -= HandleBlocked;
            }
            ReleaseAttackSlot();
        }

        public void SetLevelEnd(Transform newTarget)
        {
            levelEnd = newTarget;
        }

        private void ApplyConfig()
        {
            if (enemyConfig == null)
                return;

            vitals.Configure(enemyConfig.health, enemyConfig.armor, enemyConfig.magicResist, enemyConfig.healthRegenPerSecond);
            isDead = false;

            // Schilde beim Spawn
            if (enemyConfig.startShield > 0f)
                vitals.AddShield(enemyConfig.startShield, enemyConfig.shieldDuration);

            if (enemyConfig.blockCharges > 0)
                vitals.AddBlockShield(enemyConfig.blockCharges, enemyConfig.blockDuration);

            Light2D light = GetComponentInChildren<Light2D>();
            if (light != null)
            {
                if (enemyConfig.hasLight)
                    light.color = enemyConfig.lightColor;
                light.gameObject.SetActive(enemyConfig.hasLight);
            }

            if (_spriteAnim != null)
            {
                _spriteAnim.walk_sprites = enemyConfig.walkAnim;
                _spriteAnim.dead_sprites = enemyConfig.deadAnim;
                _spriteAnim.attack_sprites = enemyConfig.attackAnim;
                _spriteAnim.animState = AnimationState.Walk_Animation;
            }

            Actions.onEnemySpawn?.Invoke(gameObject);
        }

        // ───────────────── UPDATE ───────────────────────────

        private void Update()
        {
            if (isDead || enemyConfig == null || IsCrowdControlled || isAttacking)
                return;

            ValidateTargets();
            TickTimers(Time.deltaTime);

            Tower target = CurrentTarget;

            // In Reichweite -> angreifen (auch ohne das Wegende erreicht zu haben, z. B. Fernkämpfer)
            if (target != null && DistanceTo(target) <= enemyConfig.attackRange)
            {
                StartAttack(target);
                return;
            }

            if (FollowPath())
                return;

            // Pfad zu Ende, Ziel aber noch nicht in Reichweite (z. B. Feld daneben zu weit weg): direkt annähern
            if (target != null)
            {
                ApproachTarget(target);
                return;
            }

            if (reachedFinalTarget)
                ReachEnd();
        }

        private void TickTimers(float dt)
        {
            float interval = aggroTarget != null ? enemyConfig.chaseRepathInterval : enemyConfig.repathInterval;

            repathTimer += dt;
            if (repathTimer >= interval)
            {
                repathTimer = 0f;
                CalculatePath();
            }

            retargetTimer += dt;
            if (retargetTimer >= RetargetInterval)
            {
                retargetTimer = 0f;
                UpdateAggroTarget();
            }
        }

        // ───────────────── TARGETING ────────────────────────

        /// <summary>Ein Ziel, das wir einmal hatten, aber das zerstört wurde oder nicht mehr angreifbar ist.</summary>
        private static bool IsGone(Tower t)
        {
            return !ReferenceEquals(t, null) && (t == null || !t.IsTargetable);
        }

        private void ValidateTargets()
        {
            bool recalc = false;

            if (IsGone(aggroTarget))
            {
                aggroTarget = null;
                recalc = true;
            }

            if (IsGone(pathBlocker))
            {
                pathBlocker = null;
                recalc = true;
            }

            if (recalc)
                CalculatePath();
        }

        private void UpdateAggroTarget()
        {
            Tower found = EnemyTargeting.FindAggroTarget(transform.position, enemyConfig, unreachable);

            // Aktuelles Ziel behalten (Leash), außer ein höher priorisiertes taucht auf -> kein Hin-und-Her
            if (aggroTarget != null && aggroTarget.IsTargetable && DistanceTo(aggroTarget) <= enemyConfig.leashRange)
            {
                if (found == null ||
                    enemyConfig.GetPriorityRank(found.Category) >= enemyConfig.GetPriorityRank(aggroTarget.Category))
                    found = aggroTarget;
            }

            if (found == aggroTarget)
                return;

            aggroTarget = found;
            CalculatePath();   // neues Ziel (oder Ziel verloren -> zurück auf den Weg zum Levelende)
        }

        private void MarkUnreachable(Tower t)
        {
            if (t == null) return;

            if (unreachable.Count > 8)
            {
                float now = Time.time;
                var expired = new List<Tower>();
                foreach (var kv in unreachable)
                    if (kv.Key == null || kv.Value <= now) expired.Add(kv.Key);
                foreach (var key in expired) unreachable.Remove(key);
            }

            unreachable[t] = Time.time + UnreachableCooldown;
        }

        private float DistanceTo(Tower t)
        {
            return Vector2.Distance(transform.position, t.transform.position);
        }

        // ───────────────── PATH ─────────────────────────────

        private void CalculatePath()
        {
            if (levelEnd == null || PathfindingManager.Instance == null)
                return;

            PathfindingManager pm = PathfindingManager.Instance;
            PathResult result = null;

            // 1) Aggro-Ziel: Weg zum besten freien Feld daneben
            if (aggroTarget != null)
            {
                result = pm.FindPathToTarget(transform.position, aggroTarget.gameObject, reservedSlot);

                if (result == null)
                {
                    // Nicht erreichbar (z. B. hinter Mauern). Kurz ignorieren, außer wir können es schon beschießen.
                    MarkUnreachable(aggroTarget);
                    if (DistanceTo(aggroTarget) > enemyConfig.attackRange)
                        aggroTarget = null;
                }
                else
                {
                    pathBlocker = null;
                }
            }

            // 2) Sonst: Weg zum Levelende, bei versperrtem Weg zur besten Mauer
            if (result == null)
            {
                result = pm.FindPath(transform.position, levelEnd.position, reservedSlot);

                GameObject blocker = result?.attackTarget;
                pathBlocker = blocker != null ? blocker.GetComponent<Tower>() : null;
            }

            pathIndex = 0;
            currentPath = result?.path;

            // Angriffsplatz neu reservieren, falls er sich geändert hat
            GridNode newSlot = result?.attackTarget != null ? result.targetNode : null;
            if (newSlot != reservedSlot)
            {
                pm.ReleaseSlot(reservedSlot);
                pm.ReserveSlot(newSlot);
                reservedSlot = newSlot;
            }

            reachedFinalTarget = Vector2.Distance(transform.position, levelEnd.position) < 0.9f;
        }

        private void ReleaseAttackSlot()
        {
            if (reservedSlot == null || PathfindingManager.Instance == null)
                return;

            PathfindingManager.Instance.ReleaseSlot(reservedSlot);
            reservedSlot = null;
        }

        /// <summary>Grid hat sich geändert (Mauer gebaut/zerstört). Berechnet bald neu, leicht versetzt, damit nicht alle im selben Frame suchen.</summary>
        public void OnGridChanged()
        {
            if (enemyConfig == null) return;

            float interval = aggroTarget != null ? enemyConfig.chaseRepathInterval : enemyConfig.repathInterval;
            repathTimer = Mathf.Max(repathTimer, interval - Random.value * 0.4f);
        }

        // ───────────────── MOVEMENT ─────────────────────────

        /// <summary>Folgt dem Pfad. Gibt false zurück, wenn kein Weg mehr übrig ist.</summary>
        private bool FollowPath()
        {
            if (currentPath == null || pathIndex >= currentPath.Count)
                return false;

            if (enemyConfig.movementSpeed > 0f)
                MoveAlongPath();

            UpdateSorting();
            return true;
        }

        private void MoveAlongPath()
        {
            float speed = enemyConfig.movementSpeed * slowMultiplier;
            Vector3 targetPos = currentPath[pathIndex];

            // Auf dem letzten Wegpunkt etwas streuen, damit sich Angreifer nicht stapeln
            if (CurrentTarget != null && pathIndex == currentPath.Count - 1)
                targetPos += spreadOffset;

            transform.position = Vector2.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);
            Rotate(targetPos);

            if (Vector2.Distance(transform.position, targetPos) < 0.1f)
                pathIndex++;
        }

        private void ApproachTarget(Tower target)
        {
            float speed = enemyConfig.movementSpeed * slowMultiplier;
            if (speed <= 0f) return;

            Vector3 dest = target.transform.position;
            transform.position = Vector2.MoveTowards(transform.position, dest, speed * Time.deltaTime);

            Rotate(dest);
            UpdateSorting();
        }

        private void ReachEnd()
        {
            Actions.onEnemyReachedEnd?.Invoke(gameObject);
            Destroy(gameObject);
        }

        // ───────────────── ATTACK ───────────────────────────

        private void StartAttack(Tower target)
        {
            if (isAttacking) return;

            isAttacking = true;
            StartCoroutine(AttackRoutine(target));
        }

        private IEnumerator AttackRoutine(Tower target)
        {
            Rotate(target.transform.position);

            if (_spriteAnim != null)
            {
                _spriteAnim.SetAttackSpeed(1);
                _spriteAnim.animState = AnimationState.Attack_Animation;

                bool animationComplete = false;
                void AnimationFinished() => animationComplete = true;

                _spriteAnim.OnAttackAnimationComplete += AnimationFinished;
                yield return new WaitUntil(() => animationComplete || isDead);
                _spriteAnim.OnAttackAnimationComplete -= AnimationFinished;
            }

            if (!isDead)
            {
                PerformAttack(target);
                yield return new WaitForSeconds(enemyConfig.attackCooldown);
            }

            isAttacking = false;
        }

        protected virtual void PerformAttack(Tower target)
        {
            if (target == null || !target.IsTargetable)
            {
                OnTargetLost();
                return;
            }

            DamageInfo info = enemyConfig.offense.CreateDamage(
                enemyConfig.attackDamage, enemyConfig.damageType, gameObject);

            if (enemyConfig.isMeele)
            {
                target.TakeDamage(info);
            }
            else if (enemyConfig.projectile != null)
            {
                Instantiate(enemyConfig.projectile, transform.position, Quaternion.identity)
                    .GetComponent<Projectile>()
                    .Init((target.gameObject, 0), info);
            }

            if (!target.IsTargetable)
                OnTargetLost();
        }

        private void OnTargetLost()
        {
            ValidateTargets();

            if (_spriteAnim != null)
                _spriteAnim.animState = AnimationState.Walk_Animation;
        }

        // ───────────────── DAMAGE ───────────────────────────

        public DamageResult TakeDamage(DamageInfo info)
        {
            if (isDead) return default;

            // Ausweichen (DoT-Ticks werden nicht ausgewichen)
            if (!info.isDot && enemyConfig != null && enemyConfig.evasionChance > 0 &&
                Random.Range(0f, 100f) < enemyConfig.evasionChance)
            {
                if (enemyConfig.evasionEffect != null)
                    Instantiate(enemyConfig.evasionEffect, transform.position, Quaternion.identity);
                return default;
            }

            DamageResult result = vitals.TakeDamage(info);

            if (!result.blocked && enemyConfig != null && enemyConfig.hitEffect != null)
                Instantiate(enemyConfig.hitEffect, transform.position, Quaternion.identity);

            return result;
        }

        /// <summary>Für alten Code mit int-Schaden.</summary>
        public void TakeDamage(int damage)
        {
            TakeDamage(DamageInfo.Legacy(damage));
        }

        /// <summary>Ein Treffer wurde vom Block-Schild abgefangen.</summary>
        private void HandleBlocked()
        {
            if (enemyConfig == null) return;

            if (enemyConfig.blockEffect != null)
                Instantiate(enemyConfig.blockEffect, transform.position, Quaternion.identity);

            // Letzte Ladung verbraucht -> Block-Schild erneuert sich nach der eingestellten Zeit
            if (enemyConfig.blockRechargeTime > 0f && !vitals.HasBlock && blockRechargeRoutine == null)
                blockRechargeRoutine = StartCoroutine(RechargeBlock());
        }

        private IEnumerator RechargeBlock()
        {
            yield return new WaitForSeconds(enemyConfig.blockRechargeTime);

            if (!isDead)
                vitals.AddBlockShield(enemyConfig.blockCharges, enemyConfig.blockDuration);

            blockRechargeRoutine = null;
        }

        private void HandleDied(Vitals v)
        {
            Die();
        }

        private void Die()
        {
            if (isDead) return;

            isDead = true;
            aggroTarget = null;
            pathBlocker = null;
            ReleaseAttackSlot();

            if (enemyConfig != null && enemyConfig.deathEffect != null)
                Instantiate(enemyConfig.deathEffect, transform.position, Quaternion.identity);

            Actions.onEnemyDeath?.Invoke(gameObject);

            if (_spriteAnim != null)
                _spriteAnim.TriggerDeadAnimation(true);
        }

        // ───────────────── STATUS EFFECTS ───────────────────

        public void ApplySlow(float amount, float duration)
        {
            // duration <= 0 hebt den Slow auf (z. B. wenn eine Kette reißt)
            if (duration <= 0)
            {
                if (slowCoroutine != null)
                    StopCoroutine(slowCoroutine);

                slowCoroutine = null;
                slowMultiplier = 1f;
                return;
            }

            float newMultiplier = 1f - Mathf.Clamp01(amount);

            // Ein stärkerer Slow, der noch läuft, wird nicht von einem schwächeren überschrieben
            if (slowCoroutine != null && newMultiplier > slowMultiplier)
                return;

            if (slowCoroutine != null)
                StopCoroutine(slowCoroutine);

            slowMultiplier = newMultiplier;
            slowCoroutine = StartCoroutine(SlowTimer(duration));
        }

        private IEnumerator SlowTimer(float duration)
        {
            yield return new WaitForSeconds(duration);
            slowMultiplier = 1f;
            slowCoroutine = null;
        }

        public void ApplyPositionOffset(Vector3 offset, float duration)
        {
            if (!isDead)
                StartCoroutine(Knockback(offset, duration));
        }

        private IEnumerator Knockback(Vector3 offset, float duration)
        {
            isKnockbacking = true;

            Vector3 start = transform.position;
            Vector3 end = start + offset;
            float t = 0;

            while (t < duration)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(start, end, t / duration);
                yield return null;
            }

            isKnockbacking = false;
        }

        // ───────────────── HELPERS ──────────────────────────

        private void Rotate(Vector3 targetPos)
        {
            Vector3 dir = targetPos - transform.position;

            transform.rotation = dir.x > 0
                ? Quaternion.identity
                : Quaternion.Euler(0, 180, 0);
        }

        private void UpdateSorting()
        {
            if (ySort != null)
                ySort.UpdateSorting();
        }
    }
}