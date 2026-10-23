using System;
using UnityEngine;

namespace TowerDefense
{
    [Serializable]
    public class HeroMotorSettings
    {
        [Header("Tempo (Faktoren auf das Lauftempo des Helden)")]
        public float walkFactor = 0.5f;
        public float sprintFactor = 1.5f;
        public float walkBackFactor = 0.45f;

        [Header("Dash")]
        public float dashDistance = 3f;
        public float dashDuration = 0.25f;
        public float dashCooldown = 1f;
        public float dashInvulnerability = 0.15f;

        [Header("Ausweichen")]
        public float dodgeDistance = 2.2f;
        public float dodgeBackDistance = 1.8f;
        public float dodgeDuration = 0.35f;
        public float dodgeCooldown = 0.8f;
        public float dodgeInvulnerability = 0.3f;

        [Header("Sprung")]
        public float jumpDistance = 1.2f;
        public float jumpDuration = 0.5f;
        public float jumpCooldown = 0.8f;
        public float jumpInvulnerability = 0.2f;

        
        [Header("Ausdauer-Kosten (0 = kostenlos)")]
        public float dashStamina;
        public float dodgeStamina;
        public float jumpStamina;
        
        public float SpeedFactor(HeroAnim locomotion)
        {
            switch (locomotion)
            {
                case HeroAnim.Walk: return walkFactor;
                case HeroAnim.Sprint: return sprintFactor;
                case HeroAnim.WalkBack: return walkBackFactor;
                default: return 1f;
            }
        }
    }

    /// <summary>
    /// Bewegt den Helden: normale Schritte und kurze Impulse (Dash, Ausweichen, Sprung, Ausfallschritt).
    /// Die Kollisionsprüfung (Mauern) kommt vom Helden.
    /// </summary>
    public sealed class HeroMotor
    {
        private readonly Transform transform;
        private readonly Func<Vector3, bool> canMoveTo;

        private Vector2 impulseDirection;
        private float impulseDistance;
        private float impulseDuration;
        private float impulseElapsed;
        private float impulseProgress;

        public bool IsImpulsing { get; private set; }

        public HeroMotor(Transform transform, Func<Vector3, bool> canMoveTo)
        {
            this.transform = transform;
            this.canMoveTo = canMoveTo;
        }

        public void Move(Vector2 direction, float speed, float deltaTime)
        {
            if (direction.sqrMagnitude < 0.0001f || speed <= 0f)
                return;

            TryStep(direction.normalized * speed * deltaTime);
        }

        public void StartImpulse(Vector2 direction, float distance, float duration)
        {
            if (direction.sqrMagnitude < 0.0001f || distance <= 0f || duration <= 0f)
            {
                IsImpulsing = false;
                return;
            }

            impulseDirection = direction.normalized;
            impulseDistance = distance;
            impulseDuration = duration;
            impulseElapsed = 0f;
            impulseProgress = 0f;
            IsImpulsing = true;
        }

        public void CancelImpulse()
        {
            IsImpulsing = false;
        }

        /// <summary>Jeden Frame aufrufen. Schnell am Anfang, abbremsend zum Ende.</summary>
        public void Tick(float deltaTime)
        {
            if (!IsImpulsing)
                return;

            impulseElapsed += deltaTime;
            float t = Mathf.Clamp01(impulseElapsed / impulseDuration);
            float eased = 1f - (1f - t) * (1f - t);

            TryStep(impulseDirection * ((eased - impulseProgress) * impulseDistance));
            impulseProgress = eased;

            if (t >= 1f)
                IsImpulsing = false;
        }

        private void TryStep(Vector2 delta)
        {
            Vector3 next = transform.position + (Vector3)delta;

            if (canMoveTo == null || canMoveTo(next))
                transform.position = next;
        }
    }
}