using System.Collections.Generic;
using ScriptableObjects;
using UnityEngine;

namespace TowerDefense
{
    public enum StatId
    {
        Level, Health, Shield, Block, AntiHeal, Armor, MagicResist,
        Damage, AttackSpeed, Range, Crit, CritDamage, Lethality, ArmorPen, MagicPen, MagicPenPercent,
        Omnivamp, Regen, MoveSpeed, Evasion, Gold,
        GiveRange, GiveDamage, GiveAttackSpeed, GiveSlow
    }

    /// <summary>Eine Zeile im Inspektions-Popup. baseValue ist der Grundwert, value der aktuelle (z. B. nach Shred oder Aura).</summary>
    public struct InspectStat
    {
        public StatId id;
        public string name;          // wird nur gezeigt, wenn kein Icon vorhanden ist
        public string extraLabel;    // fester Text hinter dem Wert, z. B. " / 110 HP"
        public string valueSuffix;   // "%", "/s"
        public float baseValue;
        public float value;
        public int decimals;

        public bool Changed =>
            Mathf.Abs(value - baseValue) >= 0.5f * Mathf.Pow(10f, -Mathf.Max(0, decimals));

        public static InspectStat Make(StatId id, string name, float value, float baseValue,
            string suffix = "", int decimals = 0, string extra = "")
        {
            return new InspectStat
            {
                id = id, name = name, value = value, baseValue = baseValue,
                valueSuffix = suffix, decimals = decimals, extraLabel = extra
            };
        }
    }

    /// <summary>Sammelt alle aktuellen Werte eines Turms, Helden oder Gegners für das InteractionPopup.</summary>
    public static class InspectStats
    {
        // ------------------------------------------------------------------ Turm und Held
        public static void Collect(Tower t, List<InspectStat> list)
        {
            if (t == null) return;
            
            if (t is WallSegment wallSegment)
            {
                CollectWall(wallSegment, list);
                return;
            }
            
            Vitals v = t.Vitals;

            // Leben
            if (v != null)
                AddHealth(list, v);
            else
                list.Add(InspectStat.Make(StatId.Health, "", t.currentHealth, t.currentHealth, "", 0,
                    $" / {t.statHealthPoints} HP"));

            AddDefense(list, v, v != null ? v.BaseArmor : t.Armor, v != null ? v.BaseMagicResist : t.MagicResist);

            list.Add(InspectStat.Make(StatId.Level, "Level", t.level, t.level));

            // Angriff (inkl. Aura-Bonus, die Aura erscheint als grüne Änderung)
            float cooldown = Mathf.Max(0.1f, t.timeInBetweenShots);
            list.Add(InspectStat.Make(StatId.Damage, "Damage",
                t.damage * (1f + t.AuraDamage), t.damage));
            list.Add(InspectStat.Make(StatId.AttackSpeed, "Attack Speed",
                (1f + t.AuraAttackSpeed) / cooldown, 1f / cooldown, "/s", 1));
            list.Add(InspectStat.Make(StatId.Range, "Range", t.EffectiveRange, t.range, "", 1));

            AddOffense(list, t.offense);

            AddIfNonZero(list, StatId.Regen, "Regen/sec", t.statHealthRegenPerSecond, "/s", 1);
            AddIfNonZero(list, StatId.Gold, "Gold/sec", t.statCoinsEarnedPerSecond, "/s", 1);

            // Aura, die dieser Turm an andere abgibt
            AddIfNonZero(list, StatId.GiveRange, "Aura Range", t.statRangeMultiplier * 100f, "%", 0);
            AddIfNonZero(list, StatId.GiveDamage, "Aura Damage", t.statDmgMultiplier * 100f, "%", 0);
            AddIfNonZero(list, StatId.GiveAttackSpeed, "Aura Atk Speed", t.statAttackSpeedMultiplier * 100f, "%", 0);
            AddIfNonZero(list, StatId.GiveSlow, "Aura Slow", t.statSlowMultiplier * 100f, "%", 0);
        }
        private static void CollectWall(WallSegment wall, List<InspectStat> list)
        {
            WallGroup group = wall.WallGroup;
            if (group == null) return;

            // Leben, Schild und Rüstung gehören der ganzen Mauergruppe, nicht dem einzelnen Segment
            Vitals v = group.healthBarVitals;
        
            AddHealth(list, v);
            AddDefense(list, v, v.BaseArmor, v.BaseMagicResist);
        
            list.Add(InspectStat.Make(StatId.Level, "Level", group.WallLevel + 1, group.WallLevel + 1));
        }
        // ------------------------------------------------------------------ Gegner
        public static void Collect(Enemy e, List<InspectStat> list)
        {
            if (e == null) return;

            EnemyConfig cfg = e.enemyConfig;
            Vitals v = e.Vitals;

            if (v != null)
                AddHealth(list, v);

            if (v != null)
                AddDefense(list, v, v.BaseArmor, v.BaseMagicResist);

            if (cfg == null) return;

            float cooldown = Mathf.Max(0.1f, cfg.attackCooldown);
            list.Add(InspectStat.Make(StatId.Damage, "Damage", cfg.attackDamage, cfg.attackDamage));
            list.Add(InspectStat.Make(StatId.AttackSpeed, "Attack Speed", 1f / cooldown, 1f / cooldown, "/s", 1));
            list.Add(InspectStat.Make(StatId.Range, "Range", cfg.attackRange, cfg.attackRange, "", 1));
            list.Add(InspectStat.Make(StatId.MoveSpeed, "Move Speed",
                cfg.movementSpeed * e.SlowMultiplier, cfg.movementSpeed, "", 1));

            AddOffense(list, cfg.offense);

            AddIfNonZero(list, StatId.Evasion, "Evasion", cfg.evasionChance, "%", 0);
            AddIfNonZero(list, StatId.Regen, "Regen/sec", cfg.healthRegenPerSecond, "/s", 1);
            AddIfNonZero(list, StatId.Gold, "Gold", cfg.goldReward, "", 0);
        }

        // ------------------------------------------------------------------ Bausteine
        private static void AddHealth(List<InspectStat> list, Vitals v)
        {
            list.Add(InspectStat.Make(StatId.Health, "", v.Health, v.Health, "", 0,
                $" / {Mathf.CeilToInt(v.MaxHealth)} HP"));

            if (v.Shield > 0.5f)
                list.Add(InspectStat.Make(StatId.Shield, "Shield", v.Shield, v.Shield));

            if (v.HasBlock)
            {
                // int.MaxValue = zeitlicher Block ohne Ladungen
                bool timed = v.BlockCharges > 999;
                list.Add(InspectStat.Make(StatId.Block, "Block", timed ? 1f : v.BlockCharges, timed ? 1f : v.BlockCharges,
                    "", 0, timed ? " Block aktiv" : " Block-Ladung(en)"));
            }

            if (v.AntiHealPercent > 0f)
                list.Add(InspectStat.Make(StatId.AntiHeal, "Anti-Heal", v.AntiHealPercent * 100f, v.AntiHealPercent * 100f, "%", 0));
        }

        /// <summary>Rüstung und MR: Grundwert gegen aktuellen Wert (Shred senkt, Buffs erhöhen).</summary>
        private static void AddDefense(List<InspectStat> list, Vitals v, float baseArmor, float baseMr)
        {
            float armor = v != null ? v.EffectiveArmor : baseArmor;
            float mr = v != null ? v.EffectiveMagicResist : baseMr;

            if (!Mathf.Approximately(baseArmor, 0f) || !Mathf.Approximately(armor, baseArmor))
                list.Add(InspectStat.Make(StatId.Armor, "Armor", armor, baseArmor));

            if (!Mathf.Approximately(baseMr, 0f) || !Mathf.Approximately(mr, baseMr))
                list.Add(InspectStat.Make(StatId.MagicResist, "Magic Resist", mr, baseMr));
        }

        private static void AddOffense(List<InspectStat> list, OffenseStats o)
        {
            if (o == null) return;

            if (o.critChance > 0f)
            {
                list.Add(InspectStat.Make(StatId.Crit, "Crit", o.critChance * 100f, o.critChance * 100f, "%", 0));
                list.Add(InspectStat.Make(StatId.CritDamage, "Crit Damage", o.critDamage * 100f, o.critDamage * 100f, "%", 0));
            }

            AddIfNonZero(list, StatId.Lethality, "Lethality", o.lethality, "", 0);
            AddIfNonZero(list, StatId.ArmorPen, "Armor Pen", o.armorPenPercent * 100f, "%", 0);
            AddIfNonZero(list, StatId.MagicPen, "Magic Pen", o.magicPenFlat, "", 0);
            AddIfNonZero(list, StatId.MagicPenPercent, "Magic Pen", o.magicPenPercent * 100f, "%", 0);
            AddIfNonZero(list, StatId.Omnivamp, "Omnivamp", o.omnivamp * 100f, "%", 0);
        }

        private static void AddIfNonZero(List<InspectStat> list, StatId id, string name, float value,
            string suffix, int decimals)
        {
            if (Mathf.Approximately(value, 0f)) return;
            list.Add(InspectStat.Make(id, name, value, value, suffix, decimals));
        }
    }
}