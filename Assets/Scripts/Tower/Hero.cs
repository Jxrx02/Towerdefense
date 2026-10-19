using System;
using System.Collections;
using System.Collections.Generic;
using Tower;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace TowerDefense
{
    public class Hero : Tower
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 4f;

        private Vector2 movementInput;

        private Tower currentTower;

        private bool interactionMode = false;
        //private bool hasReachedTargetPosition;

        private Coroutine interactionCoroutine;

        [Header("Weapons")]
        [SerializeField] private Projectile[] equippedProjectiles;
        [SerializeField] private int activeWeaponIndex;

        [Header("Tod / Respawn")]
        [SerializeField] private float respawnTime = 5f;

        private bool isDowned;
        private Vector3 spawnPosition;
        private readonly List<SpriteRenderer> hiddenRenderers = new List<SpriteRenderer>();

        [HideInInspector]
        public Vector2 targetPosition;

        /// <summary>Gegner dürfen den Helden nur angreifen, solange er nicht außer Gefecht ist.</summary>
        public override bool IsTargetable => base.IsTargetable && !isDowned;

        private void Start()
        {
            TowerHeroManager.instance.RegisterTower(this.gameObject);

            spawnPosition = transform.position;
            targetPosition = transform.position;

            // Start-Richtung
            spriteAnim.SetDirection(FacingDirection.Down);
            spriteAnim.SetState(AnimationState.Idle);
        }

        private void Update()
        {
            if (isDowned)
                return;

            HandleMovement();
            HandleAnimation();

            if (Input.GetKeyDown(KeyCode.E) && currentTower != null && currentTower is not WallSegment)
            {
                TowerHeroManager.instance.SelectTower(currentTower);
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                interactionMode = !interactionMode;
                SetInteraction(true);
            }

            if (Input.GetKeyUp(KeyCode.Space))
            {
                interactionMode = !interactionMode;
                SetInteraction(false);
            }
        }

        // ------------------------------------------------------------------
        // DAMAGE / TOD
        // ------------------------------------------------------------------

        /// <summary>
        /// Der Held hat keine Baustelle (towerConstructionSite), deshalb eigene Variante statt Tower.BreakTower:
        /// kurz außer Gefecht, dann Respawn am Startpunkt.
        /// </summary>
        protected override void BreakTower()
        {
            if (isDowned)
                return;

            StartCoroutine(DownedRoutine());
        }

        private IEnumerator DownedRoutine()
        {
            isDowned = true;
            isAttacking = false;
            movementInput = Vector2.zero;
            currentTower = null;

            TowerHeroManager.instance.DeselectTower();
            SetVisible(false);

            yield return new WaitForSeconds(respawnTime);

            transform.position = spawnPosition;
            targetPosition = spawnPosition;
            RestoreFullHealth();
            SetVisible(true);
            
            Actions.onHeroDeath.Invoke(this.gameObject);

            spriteAnim.SetDirection(FacingDirection.Down);
            spriteAnim.SetState(AnimationState.Idle);

            isDowned = false;
        }

        /// <summary>Blendet nur Renderer aus, die gerade sichtbar sind (Reichweiten-Anzeigen bleiben unberührt).</summary>
        private void SetVisible(bool visible)
        {
            if (!visible)
            {
                hiddenRenderers.Clear();

                foreach (var r in GetComponentsInChildren<SpriteRenderer>())
                {
                    if (!r.enabled) continue;

                    r.enabled = false;
                    hiddenRenderers.Add(r);
                }
            }
            else
            {
                foreach (var r in hiddenRenderers)
                {
                    if (r != null)
                        r.enabled = true;
                }

                hiddenRenderers.Clear();
            }
        }

        // ------------------------------------------------------------------
        // TOWER INTERACTION
        // ------------------------------------------------------------------

        public new void EnterTowerRange(Tower tower)
        {
            currentTower = tower;

            if (currentTower != null && LevelManager.instance.isDay)
            {
                currentTower.SetHighlighted(false);
                currentTower.SetInteraction(false);
                currentTower.SetIsSelected(false);
            }

            if (currentTower is not WallSegment && LevelManager.instance.isDay)
            {
                currentTower.SetHighlighted(true);
                currentTower.SetInteraction(true);
            }

            if (currentTower is WallSegment wall)
            {
                WallGroup wallGroup = wall.WallGroup;

                if (!wallGroup.IsBuilt && !wallGroup.IsDestroyed)
                {
                    wallGroup.SetUnbuiltVisual();
                }

                wallGroup.EnterInteractionRange(this);
            }

            Debug.Log(tower.towerName + " ist in Range");
        }

        public new void ExitTowerRange(Tower tower)
        {
            if (tower != currentTower)
                return;

            if (tower is WallSegment wall)
            {
                wall.WallGroup.ExitInteractionRange(this);

                if (!wall.WallGroup.IsBuilt)
                {
                    wall.WallGroup.SetUnbuiltVisual();
                }
            }

            tower.SetHighlighted(false);
            tower.SetInteraction(false);
            tower.SetIsSelected(false);

            SetInteraction(false);

            TowerHeroManager.instance.DeselectTower();
        }

        // ------------------------------------------------------------------
        // ANIMATION
        // ------------------------------------------------------------------

        private void HandleAnimation()
        {
            if (isAttacking)
                return;

            bool moving = movementInput.sqrMagnitude > 0.01f;

            if (moving)
            {
                spriteAnim.SetState(AnimationState.Run);
            }
            else
            {
                spriteAnim.SetState(AnimationState.Idle);
            }
        }

        // ------------------------------------------------------------------
        // MOVEMENT
        // ------------------------------------------------------------------

        private void HandleMovement()
        {
            movementInput = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical")
            );

            // Wichtig:
            // Dadurch sind diagonale Bewegungen nicht schneller.
            movementInput = movementInput.normalized;

            if (movementInput.sqrMagnitude > 0.01f)
            {
                // Position bewegen
                Vector3 movement = (movementInput * moveSpeed * Time.deltaTime);

                Vector3 newPosition = transform.position + movement;

                if (CanMoveTo(newPosition))
                {
                    transform.position = newPosition;
                }

                // Bewegungsrichtung in eine der 8 Richtungen umwandeln
                FacingDirection direction =
                    DirectionUtility.FromVector(movementInput);

                spriteAnim.SetDirection(direction);

                ysort.UpdateSorting();
            }
        }

        private bool CanMoveTo(Vector3 newPosition)
        {
            foreach (GameObject wallObject in TowerHeroManager.instance.walls)
            {
                if (wallObject == null)
                    continue;

                WallSegment wallSegment = wallObject.GetComponent<WallSegment>();

                if (wallSegment == null || wallSegment.WallGroup.IsGateOpen())
                    continue;

                float distance =
                    Vector2.Distance(
                        newPosition,
                        wallSegment.transform.position
                    );

                if (distance > 0.4f)
                {
                    continue;
                }

                // Das mittlere Tile ist ein Gate
                if (wallSegment is WallGate gate)
                {
                    // Geöffnet -> Hero darf durch
                    if (gate.IsOpen)
                    {
                        Debug.Log("Distance: " + distance);

                        continue;
                    }

                    // Geschlossen -> blockieren
                    return false;
                }

                // Normale Wand -> immer blockieren
                return false;
            }

            return true;
        }

        // ------------------------------------------------------------------
        // CLICK-TO-MOVE
        // ------------------------------------------------------------------

        public void OnHeroMoveButtonPressed()
        {
            OneClickInWorldListener.ListenOnce((Vector3 pos) =>
            {
                if (!isAttacking)
                {
                    targetPosition = pos;

                    if (Vector2.Distance(
                            transform.position,
                            targetPosition) > 0.15f)
                    {
                        //hasReachedTargetPosition = false;

                        Vector2 direction =
                            targetPosition - (Vector2)transform.position;

                        if (direction.sqrMagnitude > 0.001f)
                        {
                            spriteAnim.SetDirection(
                                DirectionUtility.FromVector(direction)
                            );
                        }

                        spriteAnim.SetState(
                            AnimationState.Run,
                            true
                        );
                    }
                }
                else
                {
                    DialogCanvas.instance.TriggerDialog(
                        "I HAVE TO FOCUS FIRST. Otherwise I might miss :(",
                        transform.position
                    );
                }

                TowerHeroManager.instance.DeselectTower();
            });
        }

        // ------------------------------------------------------------------
        // ATTACK
        // ------------------------------------------------------------------

        public override void Attack((GameObject, int) _target)
        {
            if (isDowned || isAttacking)
                return;

            target = _target;

            if (target.Item1 == null)
                return;

            if (!IsObjectInRange(target.Item1))
                return;

            // Angriffsrichtung bestimmen
            Vector2 attackDirection =
                target.Item1.transform.position - transform.position;

            if (attackDirection.sqrMagnitude > 0.001f)
            {
                FacingDirection direction =
                    DirectionUtility.FromVector(attackDirection);

                spriteAnim.SetDirection(direction);
            }

            isAttacking = true;

            spriteAnim.SetState(
                AnimationState.BowRangedAttack,
                true
            );

            StartCoroutine(HeroAttackCoroutine());
        }

        private IEnumerator HeroAttackCoroutine()
        {
            // Kleine Verzögerung bis zum eigentlichen Schuss.
            // Diesen Wert später passend zum Schuss-Frame deiner Animation einstellen.
            yield return new WaitForSeconds(0.15f);

            // Ziel könnte während der Animation verschwunden sein
            if (target.Item1 != null)
            {
                Shoot();
            }

            // Warten, bis die Angriffsdauer vorbei ist
            yield return new WaitForSeconds(0.2f);

            isAttacking = false;

            // Danach wieder Idle/Run
            bool moving = movementInput.sqrMagnitude > 0.01f;

            spriteAnim.SetState(
                moving
                    ? AnimationState.Run
                    : AnimationState.Idle,
                true
            );
        }

        // ------------------------------------------------------------------
        // LOOK DIRECTION
        // ------------------------------------------------------------------

        protected override void UpdateLookDirection(Vector3 targetPos)
        {
            Vector2 direction =
                targetPos - transform.position;

            if (direction.sqrMagnitude < 0.001f)
                return;

            FacingDirection facing =
                DirectionUtility.FromVector(direction);

            spriteAnim.SetDirection(facing);

            // KEIN transform.rotation mehr!
            //
            // Die 8 Richtungen werden über die Sprites dargestellt.
            // Dadurch werden Up/Down/Diagonal nicht zerstört.
        }
    }
}