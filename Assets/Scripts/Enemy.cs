using System;
using System.Collections;
using System.Collections.Generic;
using ScriptableObjects;
using TowerDefense.GridMovement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;

namespace TowerDefense
{
    public class Enemy : MonoBehaviour
    {
        public EnemyConfig enemyConfig;

        public int currentHealth;
        private bool isDead;

        // ── Pathfinding ─────────────────────────────────────
        private List<Vector3> currentPath;
        private int pathIndex;
        private Transform nextPathTarget;
        private Transform lvlPathEnd;

        private GameObject attackTarget;
        private bool reachedFinalTarget;

        // ── Visual / Animation ──────────────────────────────
        private SpriteAnim _spriteAnim;
        private YSort ySort;

        // ── Status Effects ───────────────────────────────────
        private float slowMultiplier = 1f;
        private Coroutine slowCoroutine;

        private bool isKnockbacking;
        private bool isBeingPulled;

        // ── AI Type Control ─────────────────────────────────
        private float repathTimer;
        private const float cleverRepathInterval = 1.5f;
        private bool isAttacking;
        public bool IsBeingPulled
        {
            get => isBeingPulled;
            set => isBeingPulled = value;
        }

        // ───────────────── INIT ─────────────────────────────

        private void Start()
        {
            _spriteAnim = GetComponent<SpriteAnim>();
            ySort = GetComponent<YSort>();

            ApplyConfig();
            CalculatePath();
            ySort.UpdateSorting();
        }

        public void SetLevelEnd(Transform newTarget)
        {
            nextPathTarget = newTarget;
            lvlPathEnd = newTarget;
        }

        private void ApplyConfig()
        {
            if (enemyConfig == null)
                return;

            Light2D light = GetComponentInChildren<Light2D>();
            if (enemyConfig.hasLight)
            {
                light.color = enemyConfig.lightColor;
                light.gameObject.SetActive(true);
            }
            else
            {
                light.gameObject.SetActive(false);
            }

            if (_spriteAnim != null)
            {
                _spriteAnim.walk_sprites = enemyConfig.walkAnim;
                _spriteAnim.dead_sprites = enemyConfig.deadAnim;
                _spriteAnim.attack_sprites = enemyConfig.attackAnim;
                _spriteAnim.animState = AnimationState.Walk_Animation;
            }

            currentHealth = enemyConfig.health;
            isDead = false;

            Actions.onEnemySpawn?.Invoke(gameObject);
        }

        // ───────────────── PATH ─────────────────────────────

        private void CalculatePath()
        {
            if (nextPathTarget == null) return;

            nextPathTarget = lvlPathEnd;
            


            PathResult result = PathfindingManager.Instance.FindPath(
                transform.position,
                nextPathTarget.position
            );
            //Debug.Log("Result: " + "\n" + result.attackTarget + "\n" + result.targetNode.worldPosition );

            pathIndex = 0;
            currentPath = result?.path;

            attackTarget = result?.attackTarget;
            
            reachedFinalTarget = Vector2.Distance(
                transform.position,
                lvlPathEnd.position
            ) < 0.6f;
                
            if (attackTarget == null && currentPath == null)
            {
                // kompletter Fail → nichts erreichbar
                Debug.Log("Enemy stuck: no path and no attack target");
            }
        }

        // ───────────────── UPDATE ───────────────────────────

        private void Update()
        {
            if (isDead || isKnockbacking || isBeingPulled || isAttacking)
                return;
            
            UpdatePathing();

            // Ziel erreicht -> jetzt angreifen

            
            if (attackTarget != null)
            {

                float dist = Vector2.Distance(
                    transform.position,
                    attackTarget.transform.position
                );

                if (dist <= enemyConfig.attackRange)
                {
                    Attack();
                    return;
                }
            }
            
            // Erst laufen, solange noch Wegpunkte vorhanden sind
            if (currentPath != null && pathIndex < currentPath.Count)
            {
                if (enemyConfig != null && enemyConfig.movementSpeed > 0)
                    MoveAlongPath();
                
                ySort.UpdateSorting();

                return;
            }



            // Endziel erreicht
            if (reachedFinalTarget)
            {
                Debug.Log("Reched end");

                ReachEnd();
            }
        
        }

        private void UpdatePathing()
        {
            if (enemyConfig == null) return;

            switch (enemyConfig.enemyType)
            {
                case EnemyType.IgnoreWalls:
                    return;

                case EnemyType.Adaptive:
                    CalculatePath();
                    break;

                case EnemyType.Clever:
                    repathTimer += Time.deltaTime;
                    if (repathTimer >= cleverRepathInterval)
                    {
                        repathTimer = 0;
                        CalculatePath();
                    }
                    break;
            }
        }

        // ───────────────── ATTACK ─────────────────────────
        public void Attack()
        {
            if (attackTarget == null)
                return;

            if (isAttacking)
                return;

            WallSegment wallSegment = attackTarget.GetComponent<WallSegment>();

            if (wallSegment == null ||wallSegment.WallGroup.HP<=0 ||wallSegment.WallGroup.IsDestroyed)
            {
                attackTarget = null;
                CalculatePath();
                return;
            }

            Debug.Log("attack0" + attackTarget);

            isAttacking = true;
            StartCoroutine(BaseAttackCoroutine(Shoot));
        }


        protected IEnumerator BaseAttackCoroutine(Action onShoot = null)
        {
            if (attackTarget != null)
            {
                Rotate(attackTarget.transform.position);

                _spriteAnim.SetAttackSpeed(1);
                _spriteAnim.animState = AnimationState.Attack_Animation;


                bool animationComplete = false;
                void AnimationFinished() => animationComplete = true;

                _spriteAnim.OnAttackAnimationComplete += AnimationFinished;
                yield return new WaitUntil(() => animationComplete);
                _spriteAnim.OnAttackAnimationComplete -= AnimationFinished;


                onShoot?.Invoke();

                yield return new WaitForSeconds(enemyConfig.attackCooldown);
            }
            
            isAttacking = false;

        }

        protected virtual void Shoot()
        { 
            if (attackTarget == null)
                return;
            
            WallSegment wallSegment = attackTarget.GetComponent<WallSegment>();

            if (wallSegment == null)
                return;
            

            if (enemyConfig.isMeele)
            {
                wallSegment.TakeDamage(enemyConfig.attackDamage);
                Debug.Log("meeleattack: " + wallSegment + " dmg " + enemyConfig.attackDamage);
            }
            else
            {
                Instantiate(enemyConfig.projectile, transform.position, Quaternion.identity)
                    .GetComponent<Projectile>()
                    .Init((attackTarget, 0), enemyConfig.attackDamage);
            }

            if (wallSegment.WallGroup.HP <= 0)
            {
                attackTarget = null;
                currentPath = null;
                pathIndex = 0;

                CalculatePath();

                _spriteAnim.animState = AnimationState.Walk_Animation;
            }
        }

        // ───────────────── MOVEMENT ─────────────────────────

        private void MoveAlongPath()
        {
            if (currentPath == null || pathIndex >= currentPath.Count)
                return;

            float speed = enemyConfig.movementSpeed * slowMultiplier;
            Vector3 targetPos = currentPath[pathIndex];

            transform.position = Vector2.MoveTowards(
                transform.position,
                targetPos,
                speed * Time.deltaTime
            );

            Rotate(targetPos);

            if (Vector2.Distance(transform.position, targetPos) < 0.1f)
            {
                pathIndex++;
            }
        }

        private void ReachEnd()
        {
            Actions.onEnemyReachedEnd?.Invoke(gameObject);
            Destroy(gameObject);
        }

        // ───────────────── DAMAGE ───────────────────────────

        public void TakeDamage(int damage, string type = "physical")
        {
            if (isDead) return;

            float final = damage;

            if (type == "fire")
                final *= 1 - enemyConfig.fireResistance;
            else if (type == "ice")
                final *= 1 + enemyConfig.iceWeakness;
            else if (type == "poison" && enemyConfig.poisonImmunity > 0)
                final = 0;

            if (Random.Range(0f, 100f) < enemyConfig.evasionChance)
            {
                if (enemyConfig.evasionEffect != null)
                    Instantiate(enemyConfig.evasionEffect, transform.position, Quaternion.identity);
                return;
            }

            currentHealth -= Mathf.RoundToInt(final);

            if (enemyConfig.hitEffect != null)
                Instantiate(enemyConfig.hitEffect, transform.position, Quaternion.identity);

            if (currentHealth <= 0)
                Die();
        }

        private void Die()
        {
            if (isDead) return;

            isDead = true;

            if (enemyConfig.deathEffect != null)
                Instantiate(enemyConfig.deathEffect, transform.position, Quaternion.identity);

            Actions.onEnemyDeath?.Invoke(gameObject);

            if (_spriteAnim != null)
                _spriteAnim.TriggerDeadAnimation(true);
        }

        // ───────────────── STATUS EFFECTS ───────────────────

        public void ApplySlow(float amount, float duration)
        {
            if (slowCoroutine != null)
                StopCoroutine(slowCoroutine);

            if (duration <= 0)
            {
                slowMultiplier = 1f;
                return;
            }

            slowMultiplier = 1f - Mathf.Clamp01(amount);
            slowCoroutine = StartCoroutine(SlowTimer(duration));
        }

        private IEnumerator SlowTimer(float duration)
        {
            yield return new WaitForSeconds(duration);
            slowMultiplier = 1f;
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

        // ───────────────── ROTATION ─────────────────────────

        private void Rotate(Vector3 targetPos)
        {
            Vector3 dir = targetPos - transform.position;

            transform.rotation = dir.x > 0
                ? Quaternion.identity
                : Quaternion.Euler(0, 180, 0);
        }

        private void OnDestroy()
        {
            Actions.onGridChanged -= OnGridChanged;
        }

        public void OnGridChanged()
        {
            if (enemyConfig != null &&
                enemyConfig.enemyType == EnemyType.Adaptive)
            {
                CalculatePath();
            }
        }
    }
}