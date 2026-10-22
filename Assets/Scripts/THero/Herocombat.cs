using System.Collections.Generic;
using UnityEngine;
using System;

namespace TowerDefense
{
    /// <summary>Was HeroCombat vom Helden braucht. Der Held implementiert das, damit HeroCombat nichts über Tower weiß.</summary>
    public interface IHeroCombatHost
    {
        Transform HostTransform { get; }

        /// <summary>Basisschaden inklusive Aura und Loadout</summary>
        float AttackDamage { get; }

        float AttackSpeedMultiplier { get; }

        DamageInfo CreateDamage(float amount, DamageType type);
        bool TryFindTarget(out GameObject enemy, out int index);
        void FireProjectile(GameObject enemy, int index, float damageMultiplier);
        void NotifyCombatActivity();
    }
    /// <summary>Was die Aufladeanzeige braucht.</summary>
    public interface IChargeSource
    {
        bool IsCharging { get; }

        /// <summary>0 bis 1, bei 1 ist der Angriff voll aufgeladen</summary>
        float ChargeProgress { get; }

        event Action FullyCharged;
    }
    /// <summary>
    /// Angriffslogik des Helden: Kombos (leicht und schwer), Aufladen, Bogenschüsse, Treffererkennung.
    ///
    ///   Taste kurz   -> nächster Schlag der Kombo (Attack1-3, HeavyAttack1-3, RangedAttack1-3, UpAttack)
    ///   Taste halten -> aufladen, beim Loslassen aufgeladener Angriff (Stufe 2 bei voller Aufladung)
    ///   Taste während des Angriffs -> wird gemerkt und direkt danach ausgeführt
    /// </summary>
    public sealed class HeroCombat : IChargeSource
    {
        private enum Phase { None, Pressing, Charging, Swinging, Recovering }

        private const float LungeDuration = 0.12f;
        private const float MinHitDistance = 0.35f;
        
        private readonly IHeroCombatHost host;
        private readonly HeroAnimator animator;
        private readonly HeroMotor motor;
        private readonly List<KeyValuePair<float, GameObject>> hitBuffer = new List<KeyValuePair<float, GameObject>>();

        private HeroWeaponConfig weapon;
        private Phase phase;

        private HeroAttackKind pressKind;
        private float pressTime;

        private HeroAttackKind chargeKind;
        private HeroAnim chargeAnim;
        private float chargeStart;
        private float chargeFraction;

        private HeroAnim currentAnim;
        private HeroAttackData currentData;
        private bool strikePending;

        private Vector2 lockedAim = Vector2.down;
        private FacingDirection lockedFacing = FacingDirection.Down;

        private HeroAttackKind lastKind;
        private int lastComboIndex = -1;
        private int lastComboLength;
        private float comboWindowEnd;
        private float cooldownEnd;
        private float recoveryEnd;
        private HeroAttackKind? buffered;

        // ------------------------------------------------------------------
        public bool IsBusy => phase == Phase.Charging || phase == Phase.Swinging || phase == Phase.Recovering;
        public bool IsRecovering => phase == Phase.Recovering;
        public bool HasSuperArmor => phase == Phase.Swinging && currentData != null && currentData.superArmor;
        public Vector2 LockedAim => lockedAim;
        public FacingDirection LockedFacing => lockedFacing;
        
        // Aufladeanzeige
        private HeroChargeIndicator chargeIndicator;
        private static readonly Color FullChargeFlash = new Color(1f, 0.9f, 0.5f);
        private bool fullyChargedRaised;
        
        public event Action FullyCharged;
        public bool IsCharging => phase == Phase.Charging;

        public float ChargeProgress =>
            phase == Phase.Charging && weapon != null
                ? Mathf.Clamp01((Time.time - chargeStart) / Mathf.Max(0.05f, weapon.fullChargeTime))
                : 0f;
        /// <summary>Tempo-Faktor für die Bewegung während der aktuellen Phase</summary>
        public float MoveSpeedMultiplier
        {
            get
            {
                if (phase == Phase.Swinging && currentData != null) return currentData.moveSpeedMultiplier;
                if (phase == Phase.Charging && weapon != null) return weapon.chargeMoveMultiplier;
                return 1f;
            }
        }

        public HeroCombat(IHeroCombatHost host, HeroAnimator animator, HeroMotor motor)
        {
            this.host = host;
            this.animator = animator;
            this.motor = motor;
            
            chargeIndicator = host.HostTransform.GetComponent<HeroChargeIndicator>();
            if (chargeIndicator == null)
            {
                chargeIndicator = host.HostTransform.gameObject
                    .AddComponent<HeroChargeIndicator>();
            }
            chargeIndicator.Bind(this);

            animator.BodyFinished += OnBodyFinished;
            animator.BodyEvent += OnBodyEvent;
        }

        public void Dispose()
        {
            animator.BodyFinished -= OnBodyFinished;
            animator.BodyEvent -= OnBodyEvent;
            
            if (chargeIndicator != null)
            {
                chargeIndicator.Bind(null);
                UnityEngine.Object.Destroy(chargeIndicator);
            }
        }

        public void SetWeapon(HeroWeaponConfig newWeapon)
        {
            Cancel();
            weapon = newWeapon;
            lastComboIndex = -1;
            comboWindowEnd = 0f;
            cooldownEnd = 0f;
        }

        public void Cancel()
        {
            phase = Phase.None;
            strikePending = false;
            buffered = null;
            chargeFraction = 0f;
            animator.SetCharging(false);
        }

        // ------------------------------------------------------------------
        // Jeden Frame
        // ------------------------------------------------------------------

        /// <param name="aim">Zielrichtung (Gegner, sonst Bewegung, sonst Blickrichtung)</param>
        /// <param name="locomotion">Walk/Run/Sprint/WalkBack, wenn der Held sich bewegt, sonst null</param>
        /// <param name="canStart">false, wenn gerade kein neuer Angriff begonnen werden darf</param>
        public void Tick(in HeroInputState input, Vector2 aim, HeroAnim? locomotion, bool canStart)
        {
            if (weapon == null)
                return;

            if (phase == Phase.None && canStart)
                TryBeginPress(input);

            switch (phase)
            {
                case Phase.Pressing: UpdatePressing(input, aim, locomotion); break;
                case Phase.Charging: UpdateCharging(input, aim, locomotion); break;
                case Phase.Swinging: BufferInput(input); break;
                case Phase.Recovering: UpdateRecovering(input, aim, locomotion); break;
            }
        }

        /// <summary>Automatischer Angriff (Bogen): Ziel in Reichweite, Held ist frei.</summary>
        public bool TryAutoAttack(Vector2 aim, HeroAnim? locomotion)
        {
            if (weapon == null || !weapon.autoAttack || phase != Phase.None || Time.time < cooldownEnd)
                return false;

            StartTap(HeroAttackKind.Light, aim, locomotion);
            return phase == Phase.Swinging;
        }

        // ------------------------------------------------------------------
        // Eingabe: kurz oder halten
        // ------------------------------------------------------------------
        private bool TryBeginPress(in HeroInputState input)
        {
            HeroAttackKind kind;

            if (input.lightPressed) kind = HeroAttackKind.Light;
            else if (input.heavyPressed && weapon.HasHeavy) kind = HeroAttackKind.Heavy;
            else return false;

            if (!CanStart())
                return false;

            pressKind = kind;
            pressTime = Time.time;
            phase = Phase.Pressing;
            return true;
        }

        private bool CanStart()
        {
            return Time.time >= cooldownEnd || Time.time <= comboWindowEnd;
        }

        private void UpdatePressing(in HeroInputState input, Vector2 aim, HeroAnim? locomotion)
        {
            bool held = pressKind == HeroAttackKind.Light ? input.lightHeld : input.heavyHeld;

            // Ohne Aufladen sofort zuschlagen (keine Verzögerung durch das Warten auf Loslassen)
            if (!held || !CanCharge(pressKind))
            {
                StartTap(pressKind, aim, locomotion);
                return;
            }

            if (Time.time - pressTime >= weapon.chargeThreshold)
                StartCharge(pressKind, aim, locomotion);
        }

        private void BufferInput(in HeroInputState input)
        {
            if (input.lightPressed) buffered = HeroAttackKind.Light;
            else if (input.heavyPressed && weapon.HasHeavy) buffered = HeroAttackKind.Heavy;
        }

        // ------------------------------------------------------------------
        // Normaler Angriff
        // ------------------------------------------------------------------
        private void StartTap(HeroAttackKind kind, Vector2 aim, HeroAnim? locomotion)
        {
            HeroAnim? anim = PickTapAnimation(kind, aim);

            if (anim == null)
            {
                phase = Phase.None;
                return;
            }

            chargeFraction = 0f;
            BeginSwing(anim.Value, kind, aim, locomotion, 0);
        }

        private HeroAnim? PickTapAnimation(HeroAttackKind kind, Vector2 aim)
        {
            // Bogen: bei Zielen nach oben die Aufwärts-Animation
            if (weapon.IsRanged && weapon.useUpAttack && DirectionUtility.FromVector(aim).IsUpward() &&
                animator.HasClip(weapon.upAttack))
                return weapon.upAttack;

            HeroAnim[] combo = kind == HeroAttackKind.Light ? weapon.lightCombo : weapon.heavyCombo;
            if (combo == null || combo.Length == 0)
                return null;

            bool inWindow = lastKind == kind && lastComboIndex >= 0 && Time.time <= comboWindowEnd;
            int start = inWindow ? (lastComboIndex + 1) % combo.Length : 0;

            // Fehlende Clips überspringen
            for (int i = 0; i < combo.Length; i++)
            {
                int index = (start + i) % combo.Length;

                if (animator.HasClip(combo[index]))
                {
                    lastComboIndex = index;
                    lastComboLength = combo.Length;
                    return combo[index];
                }
            }

            return null;
        }

        private void BeginSwing(HeroAnim anim, HeroAttackKind kind, Vector2 aim, HeroAnim? locomotion, int startFrame)
        {
            currentAnim = anim;
            currentData = weapon.GetAttack(anim);
            lastKind = kind;
            buffered = null;
            strikePending = true;

            LockAim(aim);

            animator.Play(anim, currentData.animationSpeed * host.AttackSpeedMultiplier, locomotion, startFrame);
            animator.SetCharging(false);

            if (currentData.lungeDistance > 0f)
                motor.StartImpulse(lockedAim, currentData.lungeDistance, LungeDuration);

            phase = Phase.Swinging;
            host.NotifyCombatActivity();
        }

        // ------------------------------------------------------------------
        // Aufladen
        // ------------------------------------------------------------------
        private bool CanCharge(HeroAttackKind kind)
        {
            HeroAnim[] list = kind == HeroAttackKind.Light ? weapon.chargedLight : weapon.chargedHeavy;
            return list != null && list.Length > 0 && animator.HasClip(list[0]);
        }

        private HeroAnim? PickChargeAnimation(HeroAttackKind kind, Vector2 aim, int tier)
        {
            if (weapon.IsRanged && kind == HeroAttackKind.Light && weapon.useUpAttack &&
                DirectionUtility.FromVector(aim).IsUpward() && animator.HasClip(weapon.chargedUpAttack))
                return weapon.chargedUpAttack;

            HeroAnim[] list = kind == HeroAttackKind.Light ? weapon.chargedLight : weapon.chargedHeavy;
            if (list == null || list.Length == 0)
                return null;

            HeroAnim anim = list[Mathf.Clamp(tier, 0, list.Length - 1)];
            return animator.HasClip(anim) ? anim : (HeroAnim?)null;
        }

        private void StartCharge(HeroAttackKind kind, Vector2 aim, HeroAnim? locomotion)
        {
            HeroAnim? anim = PickChargeAnimation(kind, aim, 0);

            if (anim == null)
            {
                StartTap(kind, aim, locomotion);
                return;
            }

            chargeKind = kind;
            chargeAnim = anim.Value;
            chargeStart = Time.time;
            fullyChargedRaised = false;
            
            LockAim(aim);
            animator.Play(chargeAnim, 1f, locomotion);
            animator.SetCharging(true);

            phase = Phase.Charging;
            host.NotifyCombatActivity();
        }

        private void UpdateCharging(in HeroInputState input, Vector2 aim, HeroAnim? locomotion)
        {
            bool held = chargeKind == HeroAttackKind.Light ? input.lightHeld : input.heavyHeld;

            // Beim Aufladen kann weiter gezielt werden
            LockAim(aim);

            // Volle Aufladung: einmal aufblitzen
            if (!fullyChargedRaised && ChargeProgress >= 1f)
            {
                fullyChargedRaised = true;
                animator.Flash(FullChargeFlash, 0.25f);
                FullyCharged?.Invoke();
            }

            if (!held)
                ReleaseCharge(locomotion);
        }

        private void ReleaseCharge(HeroAnim? locomotion)
        {
            float full = Mathf.Max(0.05f, weapon.fullChargeTime);
            chargeFraction = Mathf.Clamp01((Time.time - chargeStart) / full);

            int tier = chargeFraction >= 0.99f ? 1 : 0;
            HeroAnim? anim = PickChargeAnimation(chargeKind, lockedAim, tier);
            if (anim == null)
                anim = chargeAnim;

            lastKind = chargeKind;

            // Gleiche Animation: weiterlaufen lassen, aber aus der Aufladeschleife direkt in den Schlag springen
            if (anim.Value == chargeAnim)
            {
                currentAnim = chargeAnim;
                currentData = weapon.GetAttack(chargeAnim);
                strikePending = true;
                buffered = null;

                // Phase zuerst setzen: ReleaseCharge kann den Schlag-Event sofort auslösen
                phase = Phase.Swinging;
                animator.ReleaseCharge();
                return;
            }

            // Stufe 2: Clip wechseln und hinter der Aufladeschleife einsteigen
            BeginSwing(anim.Value, chargeKind, lockedAim, locomotion, animator.GetReleaseFrame(anim.Value));
        }

        // ------------------------------------------------------------------
        // Ablauf und Erholung
        // ------------------------------------------------------------------
        private void OnBodyEvent(HeroAnim anim, int frame)
        {
            if (phase != Phase.Swinging || !strikePending || anim != currentAnim)
                return;

            strikePending = false;
            Strike();
        }

        private void OnBodyFinished(HeroAnim anim)
        {
            if (phase != Phase.Swinging || anim != currentAnim)
                return;

            // Sicherheitsnetz: Hat der Clip keinen Event-Frame ausgelöst, jetzt zuschlagen
            if (strikePending)
            {
                strikePending = false;
                Strike();
            }

            BeginRecovery();
        }

        private void BeginRecovery()
        {
            phase = Phase.Recovering;
            recoveryEnd = Time.time + currentData.recovery;
            comboWindowEnd = recoveryEnd + currentData.comboWindow;

            // Abklingzeit nach dem letzten Kombo-Schlag bzw. nach jedem Schuss
            bool endsCombo = weapon.IsRanged || lastComboIndex >= lastComboLength - 1;
            if (endsCombo)
                cooldownEnd = Time.time + weapon.attackCooldown;
        }

        private void UpdateRecovering(in HeroInputState input, Vector2 aim, HeroAnim? locomotion)
        {
            BufferInput(input);

            if (Time.time < recoveryEnd)
                return;

            phase = Phase.None;

            if (buffered.HasValue)
            {
                HeroAttackKind kind = buffered.Value;
                buffered = null;

                if (CanStart())
                    StartTap(kind, aim, locomotion);
            }
        }

        private void LockAim(Vector2 aim)
        {
            if (aim.sqrMagnitude < 0.001f)
                return;

            lockedAim = aim.normalized;
            lockedFacing = DirectionUtility.FromVector(lockedAim);
            animator.SetFacing(lockedFacing);
        }

        // ------------------------------------------------------------------
        // Treffer
        // ------------------------------------------------------------------
        private void Strike()
        {
            float multiplier = currentData.damageMultiplier * Mathf.Lerp(1f, currentData.maxChargeBonus, chargeFraction);
            chargeFraction = 0f;

            if (weapon.IsRanged)
            {
                if (host.TryFindTarget(out GameObject enemy, out int index))
                    host.FireProjectile(enemy, index, multiplier);
            }
            else
            {
                MeleeHit(multiplier);
            }

            if (currentData.cameraShakeStrength > 0f && CameraZoom.Instance != null)
                CameraZoom.Instance.ShakeCamera(currentData.cameraShakeDuration, currentData.cameraShakeStrength);
        }

        private void MeleeHit(float multiplier)
        {
            TowerHeroManager manager = TowerHeroManager.instance;
            if (manager == null)
                return;

            Vector2 origin = host.HostTransform.position;
            hitBuffer.Clear();

            foreach (GameObject enemy in manager.enemies)
            {
                if (enemy == null) continue;

                Vector2 offset = (Vector2)enemy.transform.position - origin;
                float distance = offset.magnitude;

                if (distance > currentData.range) continue;
                if (distance > MinHitDistance && Vector2.Angle(lockedAim, offset) > currentData.arcDegrees * 0.5f) continue;

                hitBuffer.Add(new KeyValuePair<float, GameObject>(distance, enemy));
            }

            if (hitBuffer.Count == 0)
                return;

            hitBuffer.Sort((a, b) => a.Key.CompareTo(b.Key));

            // Ein Crit-Wurf pro Schlag, alle getroffenen Gegner teilen ihn
            DamageInfo info = host.CreateDamage(host.AttackDamage * multiplier, currentData.damageType);
            int hits = Mathf.Min(currentData.maxTargets, hitBuffer.Count);

            for (int i = 0; i < hits; i++)
            {
                IDamageable damageable = hitBuffer[i].Value.GetComponent<IDamageable>();
                if (damageable != null)
                    damageable.TakeDamage(info);
            }
        }
    }
}