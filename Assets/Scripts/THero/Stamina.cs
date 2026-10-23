using System;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// Ausdauer: lädt sich selbst auf, jeder Angriff (und optional Dash, Ausweichen, Sprung) verbraucht sie.
    ///
    ///   Nach dem Verbrauch pausiert die Aufladung kurz (regenDelay).
    ///   Ist die Ausdauer leer, ist der Held erschöpft: Aktionen sind gesperrt,
    ///   bis wieder ein Teil (exhaustionRecoverFraction) aufgeladen ist.
    ///   Die letzte Aktion darf die Ausdauer auf 0 ziehen, auch wenn sie mehr kostet als noch da ist.
    /// </summary>
    [DisallowMultipleComponent]
    public class Stamina : MonoBehaviour
    {
        [Header("Menge")]
        [SerializeField] private float maxStamina = 100f;

        [Header("Aufladung")]
        [SerializeField] private float regenPerSecond = 28f;
        [Tooltip("Sekunden nach einem Verbrauch, bevor die Aufladung beginnt")]
        [SerializeField] private float regenDelay = 0.7f;

        [Header("Erschöpfung")]
        [Tooltip("Ist die Ausdauer leer, bleibt der Held erschöpft, bis dieser Anteil wieder erreicht ist")]
        [Range(0f, 1f)] [SerializeField] private float exhaustionRecoverFraction = 0.25f;
        [Tooltip("Aufladetempo während der Erschöpfung (1 = normal)")]
        [SerializeField] private float exhaustedRegenMultiplier = 0.8f;

        private const float DeniedEventInterval = 0.3f;

        private float current;
        private float regenTimer;
        private float lastDeniedTime = -999f;

        public event Action<float> Changed;
        public event Action<float> Spent;
        public event Action<bool> ExhaustedChanged;

        /// <summary>Aktion wurde abgelehnt (zu wenig Ausdauer). Höchstens alle 0,3 s, damit die Anzeige nicht flackert.</summary>
        public event Action Denied;

        public float Current => current;
        public float Max => maxStamina;
        public float Percent => maxStamina > 0f ? current / maxStamina : 0f;
        public bool IsExhausted { get; private set; }

        /// <summary>Z. B. während des Aufladens eines Angriffs, damit sich die Ausdauer dabei nicht füllt</summary>
        public bool RegenPaused { get; set; }

        private void Awake()
        {
            current = maxStamina;
        }

        private void Update()
        {
            if (current >= maxStamina)
                return;

            float dt = Time.deltaTime;

            if (regenTimer > 0f)
            {
                regenTimer -= dt;
                return;
            }

            if (RegenPaused)
                return;

            float rate = regenPerSecond * (IsExhausted ? exhaustedRegenMultiplier : 1f);
            current = Mathf.Min(maxStamina, current + rate * dt);

            if (IsExhausted && current >= maxStamina * exhaustionRecoverFraction)
                SetExhausted(false);

            Changed?.Invoke(current);
        }

        /// <summary>Kann eine Aktion mit diesen Kosten begonnen werden? Verbraucht nichts.</summary>
        public bool CanAfford(float amount, bool requireFullCost = false)
        {
            if (amount <= 0f)
                return true;

            return !IsExhausted && current > 0f && (!requireFullCost || current >= amount);
        }

        /// <summary>
        /// Verbraucht Ausdauer. Kosten 0 sind immer erlaubt (auch bei Erschöpfung).
        /// Ohne requireFullCost darf die letzte Aktion die Ausdauer auf 0 ziehen.
        /// </summary>
        public bool TrySpend(float amount, bool requireFullCost = false)
        {
            if (amount <= 0f)
                return true;

            if (!CanAfford(amount, requireFullCost))
            {
                RaiseDenied();
                return false;
            }

            current = Mathf.Max(0f, current - amount);
            regenTimer = regenDelay;

            Spent?.Invoke(amount);
            Changed?.Invoke(current);

            if (current <= 0f)
                SetExhausted(true);

            return true;
        }

        public void Refill()
        {
            current = maxStamina;
            regenTimer = 0f;
            SetExhausted(false);
            Changed?.Invoke(current);
        }

        public void SetMax(float value, bool refill = false)
        {
            maxStamina = Mathf.Max(1f, value);
            current = refill ? maxStamina : Mathf.Min(current, maxStamina);
            Changed?.Invoke(current);
        }

        private void SetExhausted(bool value)
        {
            if (IsExhausted == value)
                return;

            IsExhausted = value;
            ExhaustedChanged?.Invoke(value);
        }

        private void RaiseDenied()
        {
            if (Time.unscaledTime - lastDeniedTime < DeniedEventInterval)
                return;

            lastDeniedTime = Time.unscaledTime;
            Denied?.Invoke();
        }
    }
}