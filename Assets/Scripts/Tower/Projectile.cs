using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TowerDefense
{
    public enum ProjectileMode
    {
        Default, //single shot
        AOE,
        DoT,
        ChainLightning,
        Knockback,
        Splitter,
        Pierce,
        BlackHole,
        Sticky, //Semtex
        Chainslow //Slow
    }

    public class Projectile : MonoBehaviour
    {
        [Header("Basis")] public int baseDmg = 1;
        public float rotationAngleOffset;
        public bool doRotation = true;
        public bool lookAtTarget = true;
        public bool hideTilCollision;

        [Tooltip("Anzahl der möglichen Bounces")]
        public int canFindNewTarget;

        public float speed;

        [Header("Projektil-Modus")] [SerializeField]
        private ProjectileMode mode = ProjectileMode.Default;

        public ProjectileMode Mode => mode;

        [Header("AOE")] [HideInInspector] public float aoeRadius = 1.5f;
        [HideInInspector] public GameObject aoeVfxPrefab;

        [Header("DoT (Damage over Time)")] [HideInInspector]
        public float dotDuration = 3f;

        [HideInInspector] public float dotInterval = 0.5f;
        [HideInInspector] public int dotDamagePerTick = 5;

        [Header("Chain Lightning")] [HideInInspector]
        public int chainDamage = 10;

        [HideInInspector] public int chainCount = 3;
        [HideInInspector] [Range(0f, 1f)] public float chainDamageFalloff = 0.2f;
        [HideInInspector] public float chainRadius = 3f;

        [Header("Knockback")] [HideInInspector]
        public float knockbackDistance = 1.5f;

        [HideInInspector] public float knockbackDuration = 0.2f;

        [Header("Splitter")] [HideInInspector] public int splitterCount = 5;
        [HideInInspector] public GameObject splitterPrefab;
        [HideInInspector] public int splitterDamage = 10;

        [Header("Pierce")] [HideInInspector] public int pierceCount = 5;

        [Header("Black Hole")] [HideInInspector]
        public float blackHolePullDuration = 2f;

        [HideInInspector] public float blackHoleRadius = 3f;
        [HideInInspector] public GameObject blackHoleVfxPrefab;

        [Header("Sticky")] [HideInInspector] public float stickyDelay = 2f;
        [HideInInspector] public float stickyAoeRadius = 1.5f;
        [HideInInspector] public int stickyAoeDamage = 5;

        [Header("Chain (Verbindung)")] [HideInInspector]
        public float chainLinkRadius = 4f;

        [HideInInspector] public float chainSlowAmount = 0.4f;
        [HideInInspector] public int chainLinkDamage = 5;

        // ── Private ──────────────────────────────────────────────
        private int damage;
        private (GameObject, int) target;
        private int targetIndex;
        private bool projectileIsDead;
        private SpriteAnim anim;
        private int remainingPierce;
        private YSort ysort;
        
        private void Awake()
        {
            anim = GetComponent<SpriteAnim>();
            ysort = GetComponent<YSort>();
        }

        public void Init((GameObject, int) target, int damage)
        {
            this.target = target;
            this.damage = baseDmg + damage;
            this.targetIndex = target.Item2;
            remainingPierce = pierceCount;
        }

        [SerializeField] private float turnSpeed = 360f;
        [SerializeField] private float maxRedirectAngle = 90f;
        private Vector3 projectileDirection;

        private void Start()
        {
            if (target.Item1 != null)
            {
                projectileDirection =
                    (target.Item1.transform.position - transform.position).normalized;
            }
        }


        private void UpdateLookDirection(Vector3 targetPos)
        {
            transform.rotation = targetPos.x < transform.position.x
                ? new Quaternion(0, 180, 0, 1)
                : new Quaternion(0, 0, 0, 1);
        }


        void Update()
        {
            if (projectileIsDead)
            {
                Destroy(gameObject);
                return;
            }

            if (target.Item1 != null)
            {
                if (lookAtTarget)
                    UpdateLookDirection(target.Item1.transform.position);

                if (Vector3.Distance(
                        transform.position,
                        target.Item1.transform.position) < 0.3f)
                {
                    OnProjectileHitTarget();
                }
                else
                {
                    MoveProjectileTowardsTarget();

                }
            }
            else
            {
                if (canFindNewTarget > 0)
                {
                    try
                    {
                        var enemies = TowerHeroManager.instance.enemies;

                        if (enemies.Count > 0)
                        {
                            int newIndex = Mathf.Clamp(
                                targetIndex - 1,
                                0,
                                enemies.Count - 1
                            );

                            var newTarget = enemies[newIndex];

                            if (newTarget != null)
                            {
                                Vector3 newDirection =
                                    (newTarget.transform.position - transform.position)
                                    .normalized;

                                float angle = Vector3.Angle(
                                    projectileDirection,
                                    newDirection
                                );

                                // Nur übernehmen, wenn der neue Gegner
                                // nicht zu weit von der aktuellen Flugrichtung entfernt ist.
                                if (angle <= maxRedirectAngle)
                                {
                                    target = (newTarget, newIndex);
                                    targetIndex = newIndex;
                                }
                                else
                                {
                                    OnProjectileHitTarget();
                                }
                            }
                            else
                            {
                                OnProjectileHitTarget();
                            }
                        }
                        else
                        {
                            OnProjectileHitTarget();
                        }
                    }
                    catch
                    {
                        OnProjectileHitTarget();
                    }
                }
                else
                {
                    OnProjectileHitTarget();
                }
            }
        }


        private void MoveProjectileTowardsTarget()
        {
            Vector3 targetDirection =
                (target.Item1.transform.position - transform.position).normalized;
            
            ysort.UpdateSorting();

            // Projektilrichtung langsam in Richtung des Targets drehen.
            projectileDirection = Vector3.RotateTowards(
                projectileDirection,
                targetDirection,
                turnSpeed * Mathf.Deg2Rad * Time.deltaTime,
                0f
            ).normalized;

            transform.position += projectileDirection * speed * Time.deltaTime;

            if (doRotation)
            {
                float angle =
                    Mathf.Atan2(
                        projectileDirection.y,
                        projectileDirection.x
                    ) * Mathf.Rad2Deg;

                transform.rotation = Quaternion.AngleAxis(
                    angle + rotationAngleOffset,
                    Vector3.forward
                );
            }
        }

        private void OnProjectileHitTarget()
        {
            if (hideTilCollision)
                foreach (Transform child in transform)
                    child.gameObject.SetActive(true);

            Vector3 hitPos = target.Item1 != null
                ? target.Item1.transform.position
                : transform.position;

            switch (Mode)
            {
                case ProjectileMode.Default:
                    HitSingle();
                    break;
                case ProjectileMode.AOE:
                    ApplyAoeDamage(hitPos, aoeRadius, damage, aoeVfxPrefab);
                    break;
                case ProjectileMode.DoT:
                    HitSingle();

                    if (target.Item1 != null)
                    {
                        StartCoroutine(ApplyDoT(target.Item1));
                        return; // Projektil lebt weiter
                    }

                    break;

                case ProjectileMode.ChainLightning:
                    if (target.Item1 != null)
                    {
                        StartCoroutine(ApplyChainLightning(target.Item1, chainDamage, chainCount));
                        return;
                    }

                    break;
                case ProjectileMode.Knockback:
                    HitSingle();
                    if (target.Item1 != null)
                        StartCoroutine(ApplyKnockback(target.Item1, hitPos));
                    break;
                case ProjectileMode.Splitter:
                    HitSingle();
                    SpawnSplitter(hitPos);
                    break;
                case ProjectileMode.Pierce:
                    ApplyPierce(hitPos);
                    if (remainingPierce > 0) return; // noch nicht sterben
                    break;

                case ProjectileMode.BlackHole:
                    StartCoroutine(ApplyBlackHole(hitPos));
                    break;
                case ProjectileMode.Sticky:
                    if (target.Item1 != null)
                        StartCoroutine(ApplySticky(target.Item1));
                    return; // Projektil stirbt erst nach Delay
                case ProjectileMode.Chainslow:
                    HitSingle();
                    if (target.Item1 != null)
                        ApplyChainLink(target.Item1, hitPos);
                    break;
            }

            // Bounce-Logik (nur Default)
            if (Mode == ProjectileMode.Default && canFindNewTarget > 0)
            {
                canFindNewTarget--;
                target = (null, -1);
                return;
            }

            anim.TriggerDeadAnimation(true);
            projectileIsDead = true;
        }

        // ── Effekt-Implementierungen ──────────────────────────────

        private void HitSingle()
        {
            if (target.Item1 != null)
            {
                target.Item1.GetComponent<Enemy>()?.TakeDamage(damage);
                target.Item1.GetComponent<WallSegment>()?.TakeDamage(damage);
            }
        }

        private void ApplyAoeDamage(Vector3 center, float radius, int dmg, GameObject vfx)
        {
            if (vfx != null)
            {
                GameObject aoeVfx = Instantiate(vfx, center, Quaternion.Euler(0, 0, 90));
                SpriteAnim sa = aoeVfx.GetComponent<SpriteAnim>();

                if (sa != null)
                    sa.OnIdleAnimationComplete = () => Destroy(aoeVfx);
                else
                    Destroy(aoeVfx, 1f); // Fallback, falls kein SpriteAnim vorhanden ist
            }

            foreach (var go in TowerHeroManager.instance.enemies.ToArray())
            {
                if (go == null) continue;
                if (Vector3.Distance(center, go.transform.position) <= radius)
                {
                    go.GetComponent<Enemy>()?.TakeDamage(dmg);
                    go.GetComponent<WallSegment>()?.TakeDamage(dmg);
                }
            }
        }

        private IEnumerator ApplyDoT(GameObject enemy)
        {
            float elapsed = 0f;
            float nextTick = 0f;

            while (elapsed < dotDuration)
            {
                if (enemy == null)
                    break;

                // Projektil am Gegner halten
                transform.position = enemy.transform.position;

                if (elapsed >= nextTick)
                {
                    enemy.GetComponent<Enemy>()?.TakeDamage(dotDamagePerTick);
                    enemy.GetComponent<WallSegment>()?.TakeDamage(dotDamagePerTick);

                    nextTick += dotInterval;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            anim.TriggerDeadAnimation(true);
            projectileIsDead = true;
        }


        private IEnumerator ApplyChainLightning(
            GameObject first,
            int dmg,
            int remaining)
        {
            if (first == null)
            {
                projectileIsDead = true;
                yield break;
            }

            HashSet<GameObject> hitEnemies = new HashSet<GameObject>();

            GameObject current = first;
            int currentDamage = dmg;

            // Erstes Ziel direkt als getroffen markieren
            hitEnemies.Add(current);

            while (current != null && remaining > 0)
            {
                // Schaden
                if (current != null)
                {
                    Enemy enemy = current.GetComponent<Enemy>();
                    if (enemy != null)
                        enemy.TakeDamage(currentDamage);

                    WallSegment wallSegment = current.GetComponent<WallSegment>();
                    if (wallSegment != null)
                        wallSegment.TakeDamage(currentDamage);
                }

                // Wenn das aktuelle Ziel durch den Schaden zerstört wurde,
                // trotzdem anhand des Snapshots nach dem nächsten Ziel suchen.
                GameObject nearest = null;
                float minDist = chainRadius;

                foreach (var go in TowerHeroManager.instance.enemies.ToArray())
                {
                    if (go == null)
                        continue;

                    if (go == current)
                        continue;

                    if (hitEnemies.Contains(go))
                        continue;

                    float dist = Vector3.Distance(
                        current.transform.position,
                        go.transform.position
                    );

                    if (dist <= minDist)
                    {
                        minDist = dist;
                        nearest = go;
                    }
                }

                // Kein weiteres Ziel -> Kette fertig
                if (nearest == null)
                    break;

                // Blitz anzeigen
                yield return StartCoroutine(
                    ShowLightning(
                        current.transform.position,
                        nearest.transform.position
                    )
                );

                // Nächstes Ziel markieren
                hitEnemies.Add(nearest);

                current = nearest;

                // Schaden reduzieren
                currentDamage = Mathf.RoundToInt(
                    currentDamage * (1f - chainDamageFalloff)
                );

                remaining--;
            }

            // WICHTIG:
            // Chain-Coroutine ist fertig -> Projectile definitiv beenden
            projectileIsDead = true;

            if (anim != null)
                anim.TriggerDeadAnimation(true);
        }

        private IEnumerator ShowLightning(Vector3 start, Vector3 end)
        {
            GameObject go = new GameObject("Lightningeffect");

            LineRenderer lr = go.AddComponent<LineRenderer>();

            lr.sortingLayerName = "Player";
            lr.sortingOrder = 0;
            
            lr.positionCount = 8;
            lr.startWidth = 0.08f;
            lr.endWidth = 0.08f;

            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = Color.white;
            lr.endColor = new Color(0.6f, 1f, 1f);

            Vector3 dir = end - start;
            Vector3 normal = Vector3.Cross(dir.normalized, Vector3.forward);

            for (int i = 0; i < lr.positionCount; i++)
            {
                float t = i / (float)(lr.positionCount - 1);

                Vector3 p = Vector3.Lerp(start, end, t);

                if (i != 0 && i != lr.positionCount - 1)
                    p += normal * UnityEngine.Random.Range(-0.18f, 0.18f);

                lr.SetPosition(i, p);
            }
            
            Destroy(go, .3f);
            yield return null;
        }

        private IEnumerator ApplyKnockback(GameObject enemy, Vector3 hitPos)
        {
            Vector3 dir = (enemy.transform.position - hitPos).normalized;
            Vector3 destination = enemy.transform.position + dir * knockbackDistance;
            float elapsed = 0f;
            Vector3 start = enemy.transform.position;

            while (elapsed < knockbackDuration && enemy != null)
            {
                elapsed += Time.deltaTime;
                enemy.transform.position = Vector3.Lerp(start, destination, elapsed / knockbackDuration);
                yield return null;
            }
        }

        private void SpawnSplitter(Vector3 hitPos)
        {
            if (splitterPrefab == null) return;
            float angleStep = 360f / splitterCount;
            var enemies = TowerHeroManager.instance.enemies;

            for (int i = 0; i < splitterCount; i++)
            {
                if (enemies.Count == 0) break;
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

                // Nächsten Gegner in dieser Richtung suchen
                GameObject splitterTarget = enemies[UnityEngine.Random.Range(0, enemies.Count)];
                var proj = Instantiate(splitterPrefab, hitPos, Quaternion.identity)
                    .GetComponent<Projectile>();
                if (proj != null)
                    proj.Init((splitterTarget, 0), splitterDamage);
            }
        }

        private void ApplyPierce(Vector3 hitPos)
        {
            if (target.Item1 != null)
            {
                target.Item1.GetComponent<Enemy>()?.TakeDamage(damage);
                target.Item1.GetComponent<WallSegment>()?.TakeDamage(damage);

                remainingPierce--;
            }

            // Nächstes Ziel auf dem Weg suchen
            if (remainingPierce > 0)
            {
                GameObject next = null;
                float minDist = float.MaxValue;
                foreach (var go in TowerHeroManager.instance.enemies.ToArray())
                {
                    if (go == null || go == target.Item1) continue;
                    float d = Vector3.Distance(transform.position, go.transform.position);
                    if (d < minDist)
                    {
                        minDist = d;
                        next = go;
                    }
                }

                target = next != null ? (next, 0) : (null, -1);
            }
        }


        private IEnumerator ApplyBlackHole(Vector3 center)
        {
            GameObject blackHoleVfx = null;

            // Black-Hole-VFX erzeugen
            if (blackHoleVfxPrefab != null)
            {
                blackHoleVfx = Instantiate(
                    blackHoleVfxPrefab,
                    center,
                    Quaternion.identity
                );
                blackHoleVfx.GetComponent<SpriteRenderer>().sortingLayerName= "Player";

            }

            float elapsed = 0f;

            while (elapsed < blackHolePullDuration)
            {
                elapsed += Time.deltaTime;

                // Snapshot verwenden, da TakeDamage / andere Systeme
                // die Enemy-Liste verändern können.
                foreach (var go in TowerHeroManager.instance.enemies.ToArray())
                {
                    if (go == null)
                        continue;

                    float distance = Vector3.Distance(
                        center,
                        go.transform.position
                    );

                    if (distance <= blackHoleRadius)
                    {
                        go.transform.position = Vector3.MoveTowards(
                            go.transform.position,
                            center,
                            3f * Time.deltaTime
                        );
                    }
                }

                yield return null;
            }

            // Black-Hole-VFX explizit zerstören
            if (blackHoleVfx != null)
            {
                Destroy(blackHoleVfx);
            }

            // Abschließender Schaden
            ApplyAoeDamage(
                center,
                blackHoleRadius,
                damage,
                aoeVfxPrefab
            );

            // Projectile beenden
            projectileIsDead = true;

            if (anim != null)
                anim.TriggerDeadAnimation(true);
        }

        private IEnumerator ApplySticky(GameObject enemy)
        {
            // Projektil bleibt am Gegner kleben
            float elapsed = 0f;
            while (elapsed < stickyDelay)
            {
                elapsed += Time.deltaTime;
                if (enemy == null) break;
                transform.position = enemy.transform.position;
                yield return null;
            }

            Vector3 explodePos = enemy != null ? enemy.transform.position : transform.position;
            // Sticky bekommt eigenen AOE-Schaden
            ApplyAoeDamage(
                explodePos,
                stickyAoeRadius,
                stickyAoeDamage,
                aoeVfxPrefab
            );
            anim.TriggerDeadAnimation(true);
            projectileIsDead = true;
        }

        private void ApplyChainLink(GameObject enemyA, Vector3 hitPos)
        {
            GameObject nearest = null;
            float minDist = chainLinkRadius;
            foreach (var go in TowerHeroManager.instance.enemies.ToArray())
            {
                if (go == null || go == enemyA) continue;
                float d = Vector3.Distance(hitPos, go.transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    nearest = go;
                }
            }

            if (nearest == null) return;

            // Beide verlangsamen & Schaden
            enemyA.GetComponent<Enemy>()?.ApplySlow(chainSlowAmount, 99f);
            nearest.GetComponent<Enemy>()?.ApplySlow(chainSlowAmount, 99f);
            StartCoroutine(ChainLinkDamageLoop(enemyA, nearest));
        }

        private IEnumerator ChainLinkDamageLoop(GameObject a, GameObject b)
        {
            while (a != null && b != null)
            {
                float dist = Vector3.Distance(a.transform.position, b.transform.position);
                if (dist > chainLinkRadius * 1.5f) // Kette reißt wenn zu weit
                {
                    a.GetComponent<Enemy>()?.ApplySlow(0f, 0f); // Slow aufheben
                    b.GetComponent<Enemy>()?.ApplySlow(0f, 0f);
                    yield break;
                }

                a.GetComponent<Enemy>()?.TakeDamage(chainLinkDamage);
                b.GetComponent<Enemy>()?.TakeDamage(chainLinkDamage);

                yield return new WaitForSeconds(0.5f);
            }
        }

        // ── Stat-Anzeige für Upgrade-Diffs ──────────────────────
        [Serializable]
        public struct StatEntry
        {
            public string label;
            public float value;
            public Color color;

            public StatEntry(string label, float value, Color color)
            {
                this.label = label;
                this.value = value;
                this.color = color;
            }
        }

        /// <summary>
        /// Liefert Basisschaden + alle modusabhängigen Effekt-Werte dieses Projektils
        /// (z.B. AOE-Radius, DoT-Schaden, Chain-Anzahl, ...), damit sie im
        /// Upgrade-Diff (StatDiffDisplay) angezeigt werden können.
        /// Funktioniert auch direkt auf einem nicht instanziierten Prefab,
        /// da nur die serialisierten Inspector-Werte gelesen werden.
        /// </summary>
        public List<StatEntry> GetEffectStats()
        {
            var stats = new List<StatEntry>
            {
                new StatEntry("Projektilschaden", baseDmg, Color.red)
            };

            switch (mode)
            {
                case ProjectileMode.Default:
                    if (canFindNewTarget > 0)
                        stats.Add(new StatEntry("Bounces", canFindNewTarget, new Color(0.7f, 0.7f, 0.7f)));
                    break;

                case ProjectileMode.AOE:
                    stats.Add(new StatEntry("AOE Radius", aoeRadius, new Color(1f, 0.5f, 0f)));
                    break;

                case ProjectileMode.DoT:
                    stats.Add(new StatEntry("DoT Schaden/Tick", dotDamagePerTick, new Color(0.6f, 0.2f, 0.8f)));
                    stats.Add(new StatEntry("DoT Dauer", dotDuration, new Color(0.6f, 0.2f, 0.8f)));
                    stats.Add(new StatEntry("DoT Intervall", dotInterval, new Color(0.6f, 0.2f, 0.8f)));
                    break;

                case ProjectileMode.ChainLightning:
                    stats.Add(new StatEntry("Chain Anzahl", chainCount, Color.yellow));
                    stats.Add(new StatEntry("Chain Radius", chainRadius, Color.yellow));
                    stats.Add(new StatEntry("Chain Falloff", chainDamageFalloff, Color.yellow));
                    break;

                case ProjectileMode.Knockback:
                    stats.Add(new StatEntry("Knockback Distanz", knockbackDistance, new Color(0.8f, 0.5f, 0.2f)));
                    stats.Add(new StatEntry("Knockback Dauer", knockbackDuration, new Color(0.8f, 0.5f, 0.2f)));
                    break;

                case ProjectileMode.Splitter:
                    stats.Add(new StatEntry("Splitter Anzahl", splitterCount, new Color(0.2f, 0.8f, 0.4f)));
                    stats.Add(new StatEntry("Splitter Schaden", splitterDamage, new Color(0.2f, 0.8f, 0.4f)));
                    break;

                case ProjectileMode.Pierce:
                    stats.Add(new StatEntry("Pierce Anzahl", pierceCount, new Color(0.4f, 0.7f, 1f)));
                    break;

                case ProjectileMode.BlackHole:
                    stats.Add(new StatEntry("BlackHole Radius", blackHoleRadius, new Color(0.5f, 0f, 1f)));
                    stats.Add(new StatEntry("BlackHole Dauer", blackHolePullDuration, new Color(0.5f, 0f, 1f)));
                    break;

                case ProjectileMode.Sticky:
                    stats.Add(new StatEntry("Sticky Radius", stickyAoeRadius, new Color(1f, 0f, 0.5f)));
                    stats.Add(new StatEntry("Sticky Delay", stickyDelay, new Color(1f, 0f, 0.5f)));
                    break;

                case ProjectileMode.Chainslow:
                    stats.Add(new StatEntry("Chain Radius", chainLinkRadius, new Color(0f, 0.8f, 1f)));
                    stats.Add(new StatEntry("Chain Schaden", chainLinkDamage, new Color(0f, 0.8f, 1f)));
                    stats.Add(new StatEntry("Chain Slow", chainSlowAmount, new Color(0f, 0.8f, 1f)));
                    break;
            }

            return stats;
        }

        // ── Gizmos ───────────────────────────────────────────────
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            switch (Mode)
            {
                case ProjectileMode.AOE:
                    DrawGizmoSphere(aoeRadius, new Color(1f, 0.3f, 0f));
                    break;
                case ProjectileMode.ChainLightning:
                    DrawGizmoSphere(chainRadius, new Color(1f, 1f, 0f));
                    break;
                case ProjectileMode.BlackHole:
                    DrawGizmoSphere(blackHoleRadius, new Color(0.5f, 0f, 1f));
                    break;
                case ProjectileMode.Sticky:
                    DrawGizmoSphere(stickyAoeRadius, new Color(1f, 0f, 0.5f));
                    break;
                case ProjectileMode.Chainslow:
                    DrawGizmoSphere(chainLinkRadius, new Color(0f, 0.8f, 1f));
                    break;
            }
        }

        private void DrawGizmoSphere(float radius, Color color)
        {
            Gizmos.color = new Color(color.r, color.g, color.b, 0.2f);
            Gizmos.DrawSphere(transform.position, radius);
            Gizmos.color = new Color(color.r, color.g, color.b, 0.9f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
#endif
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(Projectile))]
    public class ProjectileEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // HideInInspector-Felder aus der normalen Inspector-Darstellung ausschließen
            DrawPropertiesExcluding(
                serializedObject,

                // AOE
                "aoeRadius",
                "aoeVfxPrefab",

                // DoT
                "dotDuration",
                "dotInterval",
                "dotDamagePerTick",

                // Slow
                "slowDuration",
                "slowAmount",

                // Chain Lightning
                "chainCount",
                "chainDamage",
                "chainDamageFalloff",
                "chainRadius",

                // Knockback
                "knockbackDistance",
                "knockbackDuration",

                // Splitter
                "splitterCount",
                "splitterPrefab",
                "splitterDamage",

                // Pierce
                "pierceCount",

                // Orbit
                "orbitProjectilePrefab",
                "orbitDuration",
                "orbitDamagePerSecond",

                // Black Hole
                "blackHolePullDuration",
                "blackHoleRadius",
                "blackHoleVfxPrefab",

                // Sticky
                "stickyDelay",
                "stickyAoeRadius",
                "stickyAoeDamage",

                // Chain Slow
                "chainLinkRadius",
                "chainSlowAmount",
                "chainLinkDamage"
            );

            Projectile p = (Projectile)target;

            EditorGUILayout.Space(10);

            // ---------------------------------------------------------
            // MODE-SPEZIFISCHE PARAMETER
            // ---------------------------------------------------------

            switch (p.Mode)
            {
                // =====================================================
                // AOE
                // =====================================================
                case ProjectileMode.AOE:

                    EditorGUILayout.LabelField(
                        "AOE Parameter",
                        EditorStyles.boldLabel
                    );

                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("aoeRadius"),
                        new GUIContent("AOE Radius")
                    );

                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("aoeVfxPrefab"),
                        new GUIContent("AOE VFX")
                    );

                    break;


                // =====================================================
                // DOT
                // =====================================================
                case ProjectileMode.DoT:

                    EditorGUILayout.LabelField(
                        "DoT Parameter",
                        EditorStyles.boldLabel
                    );

                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("dotDuration"),
                        new GUIContent("Duration")
                    );

                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("dotInterval"),
                        new GUIContent("Tick Interval")
                    );

                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("dotDamagePerTick"),
                        new GUIContent("Damage per Tick")
                    );

                    break;


                // =====================================================
                // CHAIN LIGHTNING
                // =====================================================
                case ProjectileMode.ChainLightning:

                    EditorGUILayout.LabelField(
                        "Chain Lightning Parameter",
                        EditorStyles.boldLabel
                    );

                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("chainDamage"),
                        new GUIContent("Chain Damage")
                    );

                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("chainCount"),
                        new GUIContent("Chain Count")
                    );

                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("chainDamageFalloff"),
                        new GUIContent("Damage Falloff")
                    );

                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("chainRadius"),
                        new GUIContent("Chain Radius")
                    );

                    break;


                // =====================================================
                // KNOCKBACK
                // =====================================================
                case ProjectileMode.Knockback:

                    EditorGUILayout.LabelField(
                        "Knockback Parameter",
                        EditorStyles.boldLabel
                    );

                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("knockbackDistance"),
                        new GUIContent("Distance")
                    );

                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("knockbackDuration"),
                        new GUIContent("Duration")
                    );

                    break;


                // =====================================================
                // SPLITTER
                // =====================================================
                case ProjectileMode.Splitter:

                    EditorGUILayout.LabelField(
                        "Splitter Parameter",
                        EditorStyles.boldLabel
                    );

                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("splitterCount"),
                        new GUIContent("Splitter Count")
                    );

                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("splitterPrefab"),
                        new GUIContent("Splitter Prefab")
                    );

                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("splitterDamage"),
                        new GUIContent("Splitter Damage")
                    );

                    break;


                // =====================================================
                // PIERCE
                // =====================================================
                case ProjectileMode.Pierce:

                    EditorGUILayout.LabelField(
                        "Pierce Parameter",
                        EditorStyles.boldLabel
                    );

                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("pierceCount"),
                        new GUIContent("Pierce Count")
                    );

                    break;


                // =====================================================
                // BLACK HOLE
                // =====================================================
                case ProjectileMode.BlackHole:

                    EditorGUILayout.LabelField(
                        "Black Hole Parameter",
                        EditorStyles.boldLabel
                    );

                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("blackHolePullDuration"),
                        new GUIContent("Pull Duration")
                    );

                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("blackHoleRadius"),
                        new GUIContent("Radius")
                    );

                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("blackHoleVfxPrefab"),
                        new GUIContent("Black Hole VFX")
                    );

                    break;


                // =====================================================
                // STICKY
                // =====================================================
                case ProjectileMode.Sticky:

                    EditorGUILayout.LabelField(
                        "Sticky Parameter",
                        EditorStyles.boldLabel
                    );

                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("stickyDelay"),
                        new GUIContent("Sticky Delay")
                    );

                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("stickyAoeRadius"),
                        new GUIContent("AOE Radius")
                    );

                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("stickyAoeDamage"),
                        new GUIContent("AOE Damage")
                    );

                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("aoeVfxPrefab"),
                        new GUIContent("AOE VFX")
                    );

                    break;


                // =====================================================
                // CHAIN SLOW
                // =====================================================
                case ProjectileMode.Chainslow:

                    EditorGUILayout.LabelField(
                        "Chain (Verbindung) Parameter",
                        EditorStyles.boldLabel
                    );

                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("chainLinkRadius"),
                        new GUIContent("Link Radius")
                    );

                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("chainSlowAmount"),
                        new GUIContent("Slow Amount")
                    );

                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("chainLinkDamage"),
                        new GUIContent("Link Damage")
                    );

                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif