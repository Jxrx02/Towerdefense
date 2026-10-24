using System;
using System.Collections;
using System.Collections.Generic;
using Tower;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
using LevelSelector;
using LevelSelector.Definitions;

namespace TowerDefense
{
    public class Hero : Tower, IHeroCombatHost
    {
        private static readonly HeroAnim[] IdleOrder = { HeroAnim.Idle };
        private static readonly HeroAnim[] CombatIdleOrder = { HeroAnim.CombatIdle, HeroAnim.Idle };
        private static readonly HeroAnim[] WalkOrder = { HeroAnim.Walk, HeroAnim.Run };
        private static readonly HeroAnim[] RunOrder = { HeroAnim.Run, HeroAnim.Walk };
        private static readonly HeroAnim[] SprintOrder = { HeroAnim.Sprint, HeroAnim.Run, HeroAnim.Walk };
        private const float WalkBackThreshold = -0.35f;

        [Header("Movement")]
        [FormerlySerializedAs("moveSpeed")]
        [Tooltip("Lauftempo. Gehen, Sprinten und Rückwärtsgehen sind Faktoren davon.")]
        [SerializeField] private float runSpeed = 4f;
        [SerializeField] private HeroMotorSettings motorSettings = new HeroMotorSettings();

        [Header("Input")]
        [SerializeField] private HeroInputSettings inputSettings = new HeroInputSettings();

        [Header("Waffe")]
        [Tooltip("Wird benutzt, wenn der LevelLoadoutManager keine Waffe liefert")]
        [SerializeField] private HeroWeaponConfig defaultWeapon;

        [Header("Kampf")]
        [Tooltip("So lange bleibt der Held nach einer Kampfaktion in der Kampfhaltung (CombatIdle)")]
        [SerializeField] private float combatStanceDuration = 4f;
        [SerializeField] private float hitCooldown = 0.5f;
        [SerializeField] private bool showStaminaBar = true;
        
        [Header("Tod / Respawn")]
        [SerializeField] private float respawnTime = 5f;

        [HideInInspector] public Vector2 targetPosition;

        private HeroAnimator animator;
        private HeroMotor motor;
        private HeroCombat combat;
        private HeroWeaponConfig weapon;
        private HeroState state = HeroState.Free;
        private FacingDirection facing = FacingDirection.Down;
        private HeroInputState lastInput;

        private Tower currentTower;
        private bool interactionMode;
        private Vector3 spawnPosition;
        private readonly List<SpriteRenderer> hiddenRenderers = new List<SpriteRenderer>();

        private float loadoutDamageMultiplier = 1f;
        private float loadoutAttackSpeedMultiplier = 1f;
        private Stamina stamina;


        private float invulnerableUntil;
        private float lastCombatTime = -999f;
        private float dashReadyTime, dodgeReadyTime, jumpReadyTime;
        private float moveActionEnd, hitEnd, hitReadyTime;

        private Vector2 FacingVector => facing.ToVector();
        private bool InCombatStance => Time.time - lastCombatTime < combatStanceDuration;


        protected override void Awake()
        {
            base.Awake();

            // Der HeroAnimator ersetzt SpriteAnim für den Helden (beide würden denselben SpriteRenderer beschreiben)
            if (TryGetComponent(out SpriteAnim legacyAnimation))
                legacyAnimation.enabled = false;

            animator = GetComponent<HeroAnimator>();
            if (animator == null)
                animator = gameObject.AddComponent<HeroAnimator>();

            motor = new HeroMotor(transform, CanMoveTo);
            combat = new HeroCombat(this, animator, motor);
            
            stamina = GetComponent<Stamina>();
            if (stamina == null)
                stamina = gameObject.AddComponent<Stamina>();

            if (showStaminaBar)
                gameObject.AddComponent<StaminaBar>().Bind(stamina);
        }

        private void Start()
        {
            TowerHeroManager.instance.RegisterTower(gameObject);

            spawnPosition = transform.position;
            targetPosition = transform.position;

            if (Vitals != null)
                Vitals.Damaged += OnVitalsDamaged;
            
            if(LevelLoadoutManager.Instance != null)
                EquipFromLoadout();
            else
                EquipWeapon(defaultWeapon);

            SetFacing(FacingDirection.Down);
        }
        private void Update()
        {
            if (state == HeroState.Dead || weapon == null)
                return;

            float dt = Time.deltaTime;
            lastInput = inputSettings.Read();

            HandleInteractionKeys();
            motor.Tick(dt);
            stamina.RegenPaused = combat.IsCharging;   // beim Aufladen füllt sich die Ausdauer nicht
            switch (state)
            {
                case HeroState.Free:       TickFree(lastInput, dt); break;
                case HeroState.Combat:     TickCombat(lastInput, dt); break;
                case HeroState.Blocking:   TickBlocking(lastInput, dt); break;
                case HeroState.MoveAction: TickMoveAction(); break;
                case HeroState.Hit:        TickHit(); break;
            }

            isAttacking = state == HeroState.Combat;
        }

        /// <summary>E (Upgrade-Menü) und Leertaste (Interaktion): unverändert aus der alten Update-Methode.</summary>
        private void HandleInteractionKeys()
        {
            if (Input.GetKeyDown(KeyCode.E) && currentTower != null && currentTower is not WallSegment)
                TowerHeroManager.instance.SelectTower(currentTower);

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

        private void TickFree(HeroInputState input, float dt)
        {
            if (TryStartMoveAction(input) || TryStartBlocking(input))
                return;

            GameObject enemy = FindEnemy();
            Vector2 aim = ComputeAim(input.move, enemy);

            combat.Tick(input, aim, GetLocomotion(input, aim, true, false), true);

            if (combat.IsBusy)
            {
                state = HeroState.Combat;
                return;
            }

            bool moving = input.move.sqrMagnitude > 0.01f;

            if (moving)
                SetFacing(DirectionUtility.FromVector(input.move));

            Move(input, 1f, FacingVector, false, false, dt);
            PlayFreeAnimation(input, moving);
        }

        private void TickCombat(HeroInputState input, float dt)
        {
            GameObject enemy = FindEnemy();
            Vector2 aim = ComputeAim(input.move, enemy);
            HeroAnim? locomotion = GetLocomotion(input, combat.LockedAim, true, false);

            combat.Tick(input, aim, locomotion, false);

            if (!combat.IsBusy)
            {
                state = HeroState.Free;
                return;
            }

            // Blickrichtung bleibt auf dem Ziel, die Beine folgen der Bewegung (Oberkörper + Beine)
            SetFacing(combat.LockedFacing);
            animator.SetLocomotion(locomotion);
            Move(input, combat.MoveSpeedMultiplier, combat.LockedAim, true, false, dt);

            // Dash/Ausweichen bricht die Erholungsphase ab
            if (combat.IsRecovering)
                TryStartMoveAction(input);
        }

        private void TickBlocking(HeroInputState input, float dt)
        {
            if (!input.blockHeld)
            {
                state = HeroState.Free;
                return;
            }

            lastCombatTime = Time.time;

            animator.SetLocomotion(GetLocomotion(input, FacingVector, true, true));
            Move(input, weapon.blockMoveMultiplier, FacingVector, true, true, dt);
        }

        private void TickMoveAction()
        {
            ysort.UpdateSorting();

            if (!motor.IsImpulsing && Time.time >= moveActionEnd)
                state = HeroState.Free;
        }

        private void TickHit()
        {
            if (Time.time >= hitEnd)
                state = HeroState.Free;
        }
        protected override void OnDestroy()
        {
            base.OnDestroy();

            combat?.Dispose();

            if (Vitals != null)
                Vitals.Damaged -= OnVitalsDamaged;
        }
        
        // ------------------------------------------------------------------
        // WEAPON
        // ------------------------------------------------------------------
        private void EquipFromLoadout()
        {
            LevelLoadout loadout = LevelLoadoutManager.Instance != null
                ? LevelLoadoutManager.Instance.CurrentLoadout
                : null;

            // Multiplikatoren aus Perks und Mutatoren (0 = Loadout nicht berechnet, dann neutral)
            loadoutDamageMultiplier = loadout != null && loadout.damageMultiplier > 0f ? loadout.damageMultiplier : 1f;
            loadoutAttackSpeedMultiplier = loadout != null && loadout.attackSpeedMultiplier > 0f ? loadout.attackSpeedMultiplier : 1f;

            EquipWeapon(loadout != null ? loadout.weapon : null);
        }

        /// <summary>Kann jederzeit aufgerufen werden (auch mitten im Level).</summary>
        public void EquipWeapon(WeaponDefinition definition)
        {
            EquipWeapon(definition != null ? definition.heroConfig : null);
            weapon.activeAbility = definition.active;
        }

        public void EquipWeapon(HeroWeaponConfig config)
        {
            if (config == null)
                config = defaultWeapon;

            if (config == null)
            {
                Debug.LogWarning("Hero: Keine Waffe vom Loadout und keine Default-Waffe zugewiesen.", this);
                return;
            }

            weapon = config;
            combat.SetWeapon(config);
            animator.SetAnimationSet(config.animationSet);

            // Werte des Turms (Zielsuche, Schaden, Abklingzeit, Projektil) aus der Waffe übernehmen
            if (config.targetingRange > 0f) range = config.targetingRange;
            if (config.baseDamage > 0) damage = config.baseDamage;
            if (config.attackCooldown > 0f) timeInBetweenShots = config.attackCooldown;
            if (config.projectilePrefab != null) projectilePrefab = config.projectilePrefab;

            motor.CancelImpulse();
            state = HeroState.Free;
        }

        public void ExecuteActive()
        {
            weapon.activeAbility.Execute(this.gameObject);
        }
        
        public HeroWeaponConfig Weapon => weapon;
        
        // ------------------------------------------------------------------
        // 2.6 Hilfsmethoden: Zielrichtung, Laufanimation, Bewegung
        // ------------------------------------------------------------------
        private GameObject FindEnemy()
        {
            return TowerHeroManager.instance != null
                ? FindTargetInRange(TowerHeroManager.instance.enemies).Item1
                : null;
        }
        
        /// <summary>Gegner, sonst Bewegungsrichtung, sonst Blickrichtung</summary>
        private Vector2 ComputeAim(Vector2 move, GameObject enemy)
        {
            if (enemy != null)
                return enemy.transform.position - transform.position;
        
            return move.sqrMagnitude > 0.01f ? move : FacingVector;
        }
        
        private void SetFacing(FacingDirection direction)
        {
            facing = direction;
            animator.SetFacing(direction);
        }
        
        /// <summary>Walk/Run/Sprint/WalkBack. WalkBack gibt es nur, wenn die Blickrichtung festgehalten wird.</summary>
        private HeroAnim ChooseLocomotion(HeroInputState input, Vector2 reference, bool locked, bool slow)
        {
            if (locked && reference.sqrMagnitude > 0.001f &&
                Vector2.Dot(input.move.normalized, reference.normalized) < WalkBackThreshold)
                return HeroAnim.WalkBack;
        
            if (input.walk || slow) return HeroAnim.Walk;
            
            bool canSprint = vitals != null && vitals.Health >= vitals.MaxHealth;
            if (input.sprint && !locked && canSprint) return HeroAnim.Sprint;
            return HeroAnim.Run;
        }
        
        private HeroAnim? GetLocomotion(HeroInputState input, Vector2 reference, bool locked, bool slow)
        {
            if (input.move.sqrMagnitude < 0.01f)
                return null;
        
            return ChooseLocomotion(input, reference, locked, slow);
        }
        
        private void Move(HeroInputState input, float multiplier, Vector2 reference, bool locked, bool slow, float dt)
        {
            if (input.move.sqrMagnitude < 0.01f)
                return;
        
            HeroAnim locomotion = ChooseLocomotion(input, reference, locked, slow);
            float speed = runSpeed * motorSettings.SpeedFactor(locomotion) * multiplier * weapon.moveSpeedMultiplier;
        
            motor.Move(input.move, speed, dt);
            ysort.UpdateSorting();
        }
        
        private void PlayFreeAnimation(HeroInputState input, bool moving)
        {
            if (!moving)
            {
                animator.PlayLooped(InCombatStance ? CombatIdleOrder : IdleOrder);
                return;
            }
        
            switch (ChooseLocomotion(input, FacingVector, false, false))
            {
                case HeroAnim.Sprint: animator.PlayLooped(SprintOrder); break;
                case HeroAnim.Walk:   animator.PlayLooped(WalkOrder); break;
                default:              animator.PlayLooped(RunOrder); break;
            }
        }
        // ------------------------------------------------------------------
        // 2.7 Block, Dash, Ausweichen, Sprung, Treffer-Reaktion
        // ------------------------------------------------------------------
        private bool TryStartBlocking(HeroInputState input)
        {
            if (!weapon.canBlock || !input.blockHeld || !animator.HasClip(HeroAnim.Block))
                return false;

            GameObject enemy = FindEnemy();
            if (enemy != null)
                SetFacing(DirectionUtility.FromVector(enemy.transform.position - transform.position));

            state = HeroState.Blocking;
            lastCombatTime = Time.time;
            animator.Play(HeroAnim.Block, 1f, GetLocomotion(input, FacingVector, true, true));
            return true;
        }

        private bool TryStartMoveAction(HeroInputState input)
        {
            float now = Time.time;
            bool moving = input.move.sqrMagnitude > 0.01f;
            Vector2 forward = FacingVector;
            
            if (input.dashPressed && now >= dashReadyTime && animator.HasClip(HeroAnim.Dash) &&
                stamina.TrySpend(motorSettings.dashStamina))
            {
                dashReadyTime = now + motorSettings.dashCooldown;
                BeginMoveAction(HeroAnim.Dash, moving ? input.move : forward,
                    motorSettings.dashDistance, motorSettings.dashDuration, motorSettings.dashInvulnerability, true);
                return true;
            }
            
            // beim Ausweichen:
            if (input.dodgePressed && now >= dodgeReadyTime
                &&(animator.HasClip(HeroAnim.Dodge) || animator.HasClip(HeroAnim.DodgeBackwards)) 
                && stamina.TrySpend(motorSettings.dodgeStamina) )
            {
                // Ohne Eingabe oder entgegen der Blickrichtung: nach hinten ausweichen
                bool backwards = !moving || Vector2.Dot(input.move.normalized, forward) < -0.5f;
                HeroAnim anim = backwards && animator.HasClip(HeroAnim.DodgeBackwards) ? HeroAnim.DodgeBackwards : HeroAnim.Dodge;

                if (animator.HasClip(anim))
                {
                    dodgeReadyTime = now + motorSettings.dodgeCooldown;
                    BeginMoveAction(anim, backwards ? -forward : input.move,
                        backwards ? motorSettings.dodgeBackDistance : motorSettings.dodgeDistance,
                        motorSettings.dodgeDuration, motorSettings.dodgeInvulnerability, !backwards);
                    return true;
                }
            }
            if (input.jumpPressed && now >= jumpReadyTime && animator.HasClip(HeroAnim.Jump) &&
                stamina.TrySpend(motorSettings.jumpStamina))
            {
                jumpReadyTime = now + motorSettings.jumpCooldown;
                BeginMoveAction(HeroAnim.Jump, moving ? input.move : forward,
                    motorSettings.jumpDistance, motorSettings.jumpDuration, motorSettings.jumpInvulnerability, true);
                return true;
            }

            return false;
        }

        private void BeginMoveAction(HeroAnim anim, Vector2 direction, float distance, float duration,
            float invulnerability, bool faceDirection)
        {
            combat.Cancel();

            state = HeroState.MoveAction;
            moveActionEnd = Time.time + duration;
            invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + invulnerability);
            lastCombatTime = Time.time;

            if (faceDirection)
                SetFacing(DirectionUtility.FromVector(direction));

            motor.StartImpulse(direction, distance, duration);

            // Animation so beschleunigen oder bremsen, dass sie genau zur Dauer der Bewegung passt
            float clipDuration = animator.GetClipDuration(anim);
            animator.Play(anim, clipDuration > 0f ? clipDuration / duration : 1f);
        }

        private void OnVitalsDamaged(DamageResult result)
        {
            if (state == HeroState.Dead || state == HeroState.MoveAction || result.blocked)
                return;

            if (result.healthDamage <= 0f && result.shieldDamage <= 0f)
                return;

            lastCombatTime = Time.time;

            // Schwere Angriffe haben Super Armor, außerdem eine Pause zwischen zwei Reaktionen
            if (combat.IsCharging || combat.HasSuperArmor || Time.time < hitReadyTime || !animator.HasClip(HeroAnim.Hit))
                return;
            

            combat.Cancel();
            motor.CancelImpulse();

            state = HeroState.Hit;
            hitReadyTime = Time.time + hitCooldown;
            hitEnd = Time.time + animator.GetClipDuration(HeroAnim.Hit);
            animator.Play(HeroAnim.Hit);
        }
        // ------------------------------------------------------------------
        // 2.8 Schaden: Ausweichphasen und Block 
        // ------------------------------------------------------------------
        public override DamageResult TakeDamage(DamageInfo info)
        {
            if (state == HeroState.Dead || Time.time < invulnerableUntil)
                return default;

            // Block: Nur Angriffe von vorn werden gemindert.
            if (state == HeroState.Blocking &&
                weapon != null &&
                weapon.canBlock &&
                IsFromFront(info.source))
            {
                info.amount *= 1f - Mathf.Clamp01(weapon.blockDamageReduction);
            }
            PlayDamageFlash();
            
            return base.TakeDamage(info);
        }


        private bool IsFromFront(GameObject source)
        {
            if (source == null)
                return true;

            Vector2 toSource = source.transform.position - transform.position;
            return Vector2.Angle(FacingVector, toSource) <= weapon.blockArc * 0.5f;
        }
        
        // ------------------------------------------------------------------
        // 2.9 Tod und Respawn
        // ------------------------------------------------------------------
        protected override void BreakTower()
        {
            if (state == HeroState.Dead)
                return;

            StartCoroutine(DownedRoutine());
        }

        private IEnumerator DownedRoutine()
        {
            state = HeroState.Dead;
            combat.Cancel();
            motor.CancelImpulse();
            currentTower = null;
            TowerHeroManager.instance.DeselectTower();

            // Todesanimation abspielen, dann ausblenden
            animator.Play(HeroAnim.Dead);
            float deathTime = animator.GetClipDuration(HeroAnim.Dead);
            if (deathTime > 0f)
                yield return new WaitForSeconds(deathTime);

            SetVisible(false);
            yield return new WaitForSeconds(respawnTime);

            transform.position = spawnPosition;
            targetPosition = spawnPosition;
            RestoreFullHealth();
            stamina.Refill();
            SetVisible(true);

            Actions.onHeroDeath?.Invoke(gameObject);

            SetFacing(FacingDirection.Down);
            animator.PlayLooped(IdleOrder);
            state = HeroState.Free;
        }

        /// <summary>Blendet Körper und Beine über den Animator aus, dazu alle anderen sichtbaren Sprites des Helden.</summary>
        private void SetVisible(bool visible)
        {
            animator.SetVisible(visible);

            if (!visible)
            {
                hiddenRenderers.Clear();

                foreach (var r in GetComponentsInChildren<SpriteRenderer>())
                {
                    if (!r.enabled || r == animator.BodyRenderer || r == animator.LegsRenderer)
                        continue;

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
        // 2.10 Automatischer Angriff, Klick-zum-Bewegen, Blickrichtung
        // ------------------------------------------------------------------
        /// <summary>Wird vom TowerHeroManager aufgerufen. Nur Waffen mit autoAttack (Bogen) greifen von allein an.</summary>
        public override void Attack((GameObject, int) _target)
        {
            if (weapon == null || !weapon.autoAttack || state != HeroState.Free)
                return;

            if (_target.Item1 == null || !IsObjectInRange(_target.Item1))
                return;

            target = _target;
            Vector2 aim = _target.Item1.transform.position - transform.position;

            if (combat.TryAutoAttack(aim, GetLocomotion(lastInput, aim, true, false)))
                state = HeroState.Combat;
        }

        public void OnHeroMoveButtonPressed()
        {
            OneClickInWorldListener.ListenOnce((Vector3 pos) =>
            {
                if (!isAttacking)
                {
                    targetPosition = pos;

                    Vector2 direction = targetPosition - (Vector2)transform.position;
                    if (direction.sqrMagnitude > 0.001f)
                        SetFacing(DirectionUtility.FromVector(direction));
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

        protected override void UpdateLookDirection(Vector3 targetPos)
        {
            Vector2 direction = targetPos - transform.position;

            if (direction.sqrMagnitude >= 0.001f)
                SetFacing(DirectionUtility.FromVector(direction));
        }
        
        // ------------------------------------------------------------------
        // 2.11 IHeroCombatHost
        // ------------------------------------------------------------------
        Transform IHeroCombatHost.HostTransform => transform;

        float IHeroCombatHost.AttackDamage => damage * (1f + AuraDamage) * loadoutDamageMultiplier;

        float IHeroCombatHost.AttackSpeedMultiplier =>
            attackSpeedMultiplier * (1f + AuraAttackSpeed) * loadoutAttackSpeedMultiplier;

        DamageInfo IHeroCombatHost.CreateDamage(float amount, DamageType type) =>
            offense.CreateDamage(amount, type, gameObject);

        bool IHeroCombatHost.TrySpendStamina(float amount) => stamina.TrySpend(amount);
        bool IHeroCombatHost.TryFindTarget(out GameObject enemy, out int index)
        {
            enemy = null;
            index = 0;

            if (TowerHeroManager.instance == null)
                return false;

            (GameObject, int) found = FindTargetInRange(TowerHeroManager.instance.enemies);
            enemy = found.Item1;
            index = found.Item2;
            return enemy != null;
        }

        void IHeroCombatHost.FireProjectile(GameObject enemy, int index, float damageMultiplier)
        {
            target = (enemy, index);
            Shoot(damageMultiplier * loadoutDamageMultiplier);
        }

        void IHeroCombatHost.NotifyCombatActivity() => lastCombatTime = Time.time;
        
        

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

                /*if (!wallGroup.IsBuilt && !wallGroup.IsDestroyed)
                {
                    wallGroup.SetUnbuiltVisual();
                }
                */
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

                /*if (!wall.WallGroup.IsBuilt)
                {
                    wall.WallGroup.SetUnbuiltVisual();
                }*/
            }

            tower.SetHighlighted(false);
            tower.SetInteraction(false);
            tower.SetIsSelected(false);

            SetInteraction(false);

            TowerHeroManager.instance.DeselectTower();
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
        
        
    }
    
}