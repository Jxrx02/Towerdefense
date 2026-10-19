using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// HP, Rüstung, MR, zwei Schildarten, Heilung und Anti-Heal in einer Komponente.
    /// Für Gegner, Türme, Mauern – alles, was Schaden nehmen kann.
    ///
    /// Reihenfolge bei einem Treffer:
    ///   1. Block-Schild   -> Treffer komplett abgefangen (verbraucht 1 Ladung, DoT-Ticks verbrauchen keine)
    ///   2. Crit           -> Schaden x Crit-Multiplikator
    ///   3. Rüstung/MR     -> inkl. Lethality / Pen / Shred
    ///   4. HP-Schild      -> absorbiert wie zusätzliche HP
    ///   5. HP
    ///   6. Omnivamp       -> Angreifer heilt sich (reduziert durch dessen Anti-Heal)
    /// </summary>
    [DisallowMultipleComponent]
    public class Vitals : MonoBehaviour, IDamageable
    {
        [Header("Leben")]
        [SerializeField] float maxHealth = 100f;
        [SerializeField] float healthRegenPerSecond;




        // ---------- Laufzeit ----------
        float currentHealth;
        bool isDead;
        
        float armor;
        float magicResist;
        // HP-Schild (zählt wie zusätzliche HP)
        float shield;
        float shieldTimeLeft = float.PositiveInfinity;

        // Block-Schild (blockt komplette Treffer)
        int blockCharges;
        float blockTimeLeft = float.PositiveInfinity;

        // Anti-Heal
        float antiHealPercent;
        float antiHealTimeLeft;

        class ShredStack
        {
            public string key;
            public int stacks;
            public int maxStacks;
            public float armorFlat, mrFlat, percent;
            public float timeLeft;
        }
        readonly List<ShredStack> shreds = new List<ShredStack>();

        // ---------- Events ----------
        public event Action<DamageResult> Damaged;
        public event Action<float> Healed;
        public event Action Blocked;
        public event Action<Vitals> Died;
        public event Action<float> ShieldChanged;
        public event Action<int> BlockShieldChanged;
        // ---------- Properties ----------
        public float Health => currentHealth;
        public float MaxHealth => maxHealth;


        public float HealthPercent => maxHealth > 0f ? currentHealth / maxHealth : 0f;
        
        public bool IsDead => isDead;

        public float Shield => shield;
        public bool HasBlock => blockCharges > 0 && blockTimeLeft > 0f;
        public int BlockCharges => HasBlock ? blockCharges : 0;
        public float AntiHealPercent => antiHealPercent;

        public float BaseArmor { get => armor; set => armor = value; }
        public float BaseMagicResist { get => magicResist; set => magicResist = value; }

        public float EffectiveArmor => ApplyShred(armor, s => s.armorFlat);
        public float EffectiveMagicResist => ApplyShred(magicResist, s => s.mrFlat);

        // =====================================================================
        void Awake()
        {
            currentHealth = maxHealth;
        }

        void Update()
        {
            if (isDead) return;
            float dt = Time.deltaTime;

            if (shield > 0f && !float.IsPositiveInfinity(shieldTimeLeft))
            {
                shieldTimeLeft -= dt;

                if (shieldTimeLeft <= 0f)
                {
                    shield = 0f;
                    shieldTimeLeft = float.PositiveInfinity;
                    ShieldChanged?.Invoke(shield);
                }
            }

            if (blockCharges > 0 && !float.IsPositiveInfinity(blockTimeLeft))
            {
                blockTimeLeft -= dt;

                if (blockTimeLeft <= 0f)
                {
                    blockCharges = 0;
                    blockTimeLeft = float.PositiveInfinity;
                    BlockShieldChanged?.Invoke(0);
                }
            }

            if (antiHealTimeLeft > 0f)
            {
                antiHealTimeLeft -= dt;
                if (antiHealTimeLeft <= 0f) antiHealPercent = 0f;
            }

            for (int i = shreds.Count - 1; i >= 0; i--)
            {
                shreds[i].timeLeft -= dt;
                if (shreds[i].timeLeft <= 0f) shreds.RemoveAt(i);
            }

            if (healthRegenPerSecond > 0f && currentHealth < maxHealth)
                Heal(healthRegenPerSecond * dt, null, false, true);
        }

        // =====================================================================
        //  Schaden
        // =====================================================================
        public DamageResult TakeDamage(DamageInfo info)
        {
            if (isDead) return default;

            var damage = new DamageResult { isCrit = info.isCrit && !info.isDot, type = info.type };

            // 1) Block-Schild
            if (!info.bypassBlock && HasBlock)
            {
                if (!info.isDot)
                {
                    blockCharges--;
                    if (blockCharges <= 0) blockTimeLeft = float.PositiveInfinity;
                }

                damage.blocked = true;
                BlockShieldChanged?.Invoke(HasBlock ? blockCharges : 0);
                Blocked?.Invoke();
                Damaged?.Invoke(damage);
                return damage;
            }

            // 2) Crit
            if (info.isDot) info.isCrit = false;
            float raw = info.FinalAmount;
            if (raw <= 0f) return damage;
            damage.rawDamage = raw;

            // 3) Rüstung / MR
            float afterMit = raw * DamageCalculator.GetMultiplier(info, this);
            damage.afterMitigation = afterMit;
            damage.mitigated = raw - afterMit;

            // 4) HP-Schild
            float toHealth = afterMit;
            if (!info.bypassShield && shield > 0f)
            {
                float absorbed = Mathf.Min(shield, toHealth);
                shield -= absorbed;
                toHealth -= absorbed;
                damage.shieldDamage = absorbed;

                if (shield <= 0.0001f)
                    shield = 0f;

                ShieldChanged?.Invoke(shield);
            }

            // 5) HP
            damage.healthDamage = Mathf.Min(currentHealth, toHealth);
            currentHealth = Mathf.Max(0f, currentHealth - toHealth);
            damage.killed = currentHealth <= 0f;

            Damaged?.Invoke(damage);

            // 6) Omnivamp (auf Schaden nach Rüstung, inkl. Schildschaden)
            if (info.omnivamp > 0f && info.sourceVitals != null && info.sourceVitals != this)
                info.sourceVitals.Heal(afterMit * info.omnivamp * info.omnivampScale, gameObject);

            if (damage.killed)
            {
                isDead = true;
                Died?.Invoke(this);
            }

            return damage;
        }

        /// <summary>Für alten Code mit int-Schaden (Enemy.TakeDamage(int) etc.).</summary>
        public void TakeDamage(int amount)
        {
            TakeDamage(DamageInfo.Legacy(amount));
        }

        // =====================================================================
        //  Heilung / Anti-Heal
        // =====================================================================

        /// <summary>Heilt (reduziert durch Anti-Heal). Gibt die tatsächlich geheilte Menge zurück.</summary>
        public float Heal(float amount, GameObject source = null, bool ignoreAntiHeal = false, bool silent = false)
        {
            if (isDead || amount <= 0f) return 0f;

            if (!ignoreAntiHeal)
                amount *= 1f - antiHealPercent;

            float healed = Mathf.Min(amount, maxHealth - currentHealth);
            if (healed <= 0f) return 0f;

            currentHealth += healed;
            if (!silent) Healed?.Invoke(healed);
            return healed;
        }

        /// <summary>Anti-Heal: der stärkste Wert gewinnt, die Dauer wird aufgefrischt.</summary>
        public void ApplyAntiHeal(float percent, float duration)
        {
            percent = Mathf.Clamp01(percent);
            if (percent > antiHealPercent || antiHealTimeLeft <= 0f)
                antiHealPercent = percent;
            antiHealTimeLeft = Mathf.Max(antiHealTimeLeft, duration);
        }

        // =====================================================================
        //  Schilde
        // =====================================================================

        /// <summary>
        /// HP-Schild. duration 0 = bleibt bis aufgebraucht.
        /// additive false = Wert wird nicht gestapelt, sondern nur aufgefrischt (für Pulse).
        /// </summary>
        public void AddShield(float amount, float duration = 0f, bool additive = true)
        {
            if (isDead || amount <= 0f)
                return;

            shield = additive ? shield + amount : Mathf.Max(shield, amount);

            float newTime = duration > 0f ? duration : float.PositiveInfinity;

            shieldTimeLeft = float.IsPositiveInfinity(shieldTimeLeft) ||
                             float.IsPositiveInfinity(newTime)
                ? newTime
                : Mathf.Max(shieldTimeLeft, newTime);

            ShieldChanged?.Invoke(shield);
        }


        /// <summary>Block-Schild: blockt die nächsten 'charges' Treffer. duration 0 = bis verbraucht.</summary>
        public void AddBlockShield(int charges, float duration = 0f)
        {
            if (isDead || charges <= 0)
                return;

            blockCharges = Mathf.Max(HasBlock ? blockCharges : 0, charges);

            float newTime = duration > 0f ? duration : float.PositiveInfinity;

            blockTimeLeft = !HasBlock ||
                            float.IsPositiveInfinity(blockTimeLeft) ||
                            float.IsPositiveInfinity(newTime)
                ? newTime
                : Mathf.Max(blockTimeLeft, newTime);

            BlockShieldChanged?.Invoke(HasBlock ? blockCharges : 0);
        }

        /// <summary>Block-Schild, das alle Treffer für eine Dauer abfängt.</summary>
        public void AddTimedBlock(float duration)
        {
            AddBlockShield(int.MaxValue, duration);
        }
        private void NotifyShieldStateChanged()
        {
            ShieldChanged?.Invoke(shield);
            BlockShieldChanged?.Invoke(HasBlock ? blockCharges : 0);
        }
        // =====================================================================
        //  Shred
        // =====================================================================
        public void ApplyShred(string key, float armorFlat, float mrFlat, float percent, float duration, int maxStacks)
        {
            if (isDead) return;

            ShredStack s = shreds.Find(x => x.key == key);
            if (s == null)
            {
                s = new ShredStack { key = key };
                shreds.Add(s);
            }

            s.maxStacks = Mathf.Max(1, maxStacks);
            s.stacks = Mathf.Min(s.maxStacks, s.stacks + 1);
            s.armorFlat = armorFlat;
            s.mrFlat = mrFlat;
            s.percent = percent;
            s.timeLeft = duration;
        }

        float ApplyShred(float baseValue, Func<ShredStack, float> flatSelector)
        {
            if (shreds.Count == 0) return baseValue;

            float flat = 0f, pct = 0f;
            foreach (var s in shreds)
            {
                flat += flatSelector(s) * s.stacks;
                pct += s.percent * s.stacks;
            }

            float value = baseValue - flat;                       // flat zuerst, darf negativ werden
            if (value > 0f) value *= 1f - Mathf.Min(pct, 0.9f);   // % nur auf positive Werte
            return value;
        }

        // =====================================================================
        //  Upgrades / Pooling
        // =====================================================================
        public void AddMaxHealth(float amount)
        {
            maxHealth += amount;
            currentHealth = Mathf.Clamp(currentHealth + amount, 0f, maxHealth);
        }

        /// <summary>Setzt alle Basiswerte auf einmal (z. B. aus EnemyConfig) und füllt die HP auf.</summary>
        public void Configure(float maxHealth, float armor, float magicResist, float regenPerSecond)
        {
            this.maxHealth = Mathf.Max(1f, maxHealth);
            this.armor = armor;
            this.magicResist = magicResist;
            healthRegenPerSecond = regenPerSecond;
            currentHealth = this.maxHealth;
            isDead = false;
            NotifyShieldStateChanged();
        }

        public void SetMaxHealth(float value, bool refill = false)
        {
            maxHealth = Mathf.Max(1f, value);
            currentHealth = refill ? maxHealth : Mathf.Min(currentHealth, maxHealth);
        }
        /// <summary>
        /// Setzt die HP für eine rein aggregierte Anzeige, z. B. die
        /// Gesamt-HP einer WallGroup. Führt keine Schadensberechnung aus.
        /// </summary>
        public void SetHealthForDisplay(float current, float maximum)
        {
            maxHealth = Mathf.Max(1f, maximum);
            currentHealth = Mathf.Clamp(current, 0f, maxHealth);

            // Die Anzeige-Komponente soll nicht aufgrund ihrer eigenen
            // HP-Verwaltung als zerstört gelten.
            isDead = false;
        }
        public void SetRegen(float regenPerSecond)
        {
            healthRegenPerSecond = regenPerSecond;
        }

        public void ResetState()
        {
            isDead = false;
            currentHealth = maxHealth;
            shield = 0f;
            shieldTimeLeft = float.PositiveInfinity;
            blockCharges = 0;
            blockTimeLeft = float.PositiveInfinity;
            antiHealPercent = 0f;
            antiHealTimeLeft = 0f;
            shreds.Clear();
            NotifyShieldStateChanged();
        }
    }
}