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
    GiveRange, GiveDamage, GiveAttackSpeed, GiveSlow,
    GiveRegen, GiveOmnivamp, XpDrop,
    Stamina   // neu: nur am Ende angefügt, damit bestehende Werte gleich bleiben
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

    /// <summary>
    /// Sammelt alle aktuellen Werte eines Turms, Helden oder Gegners für das InteractionPopup.
    /// Die Werte entsprechen den Formeln im Spiel, die Erklärungen dazu stehen in Texts.StatInfo.
    /// Anzeigenamen kommen aus Texts.Stats.Of.
    /// </summary>
    public static class InspectStats
    {
        private static string N(StatId id) => Texts.Stats.Of(id);

        // ------------------------------------------------------------------ Turm und Held
        public static void Collect(Tower t, List<InspectStat> list)
        {
            if (t == null) return;
            
            if (t is WallSegment wallSegment)
            {
                CollectWall(wallSegment, list);
                return;
            }

            if (t is Hero hero)
            {
                CollectHero(hero, list);
                return;
            }
            
            Vitals v = t.Vitals;

            // Leben
            if (v != null)
                AddHealth(list, v);
            else
                list.Add(InspectStat.Make(StatId.Health, "", t.currentHealth, t.currentHealth, "", 0,
                    Texts.Stats.HealthSuffix(t.statHealthPoints)));

            AddDefense(list, v, v != null ? v.BaseArmor : t.Armor, v != null ? v.BaseMagicResist : t.MagicResist);

            list.Add(InspectStat.Make(StatId.Level, N(StatId.Level), t.level, t.level));

            // Angriff (inkl. Aura-Bonus, die Aura erscheint als grüne Änderung)
            float cooldown = Mathf.Max(0.1f, t.timeInBetweenShots);

            // Schaden und Tempo wie im Schuss: Aura wirkt auf den Schaden, Tempo = Multiplikator x (1 + Aura) / Abklingzeit
            list.Add(InspectStat.Make(StatId.Damage, N(StatId.Damage),
                t.damage * (1f + t.AuraDamage), t.damage));
            list.Add(InspectStat.Make(StatId.AttackSpeed, N(StatId.AttackSpeed),
                t.attackSpeedMultiplier * (1f + t.AuraAttackSpeed) / cooldown,
                t.attackSpeedMultiplier / cooldown, "/s", 1));
            list.Add(InspectStat.Make(StatId.Range, N(StatId.Range), t.EffectiveRange, t.range, "", 1));

            AddOffense(list, t.offense);

            // Regen: Grundwert gegen Wert mit Aura (zeigt auch Türme, die nur durch die Aura Regen bekommen)
            if (t.statHealthPoints > 0 &&
                (!Mathf.Approximately(t.statHealthRegenPerSecond, 0f) || !Mathf.Approximately(t.EffectiveRegen, 0f)))
            {
                list.Add(InspectStat.Make(StatId.Regen, N(StatId.Regen),
                    t.EffectiveRegen, t.statHealthRegenPerSecond, "/s", 1));
            }
            AddIfNonZero(list, StatId.Gold, Texts.Stats.GoldPerSecond, t.statCoinsEarnedPerSecond, "/s", 1);

            // Aura, die dieser Turm an andere abgibt
            AddIfNonZero(list, StatId.GiveRange, N(StatId.GiveRange), t.statRangeMultiplier * 100f, "%", 0);
            AddIfNonZero(list, StatId.GiveDamage, N(StatId.GiveDamage), t.statDmgMultiplier * 100f, "%", 0);
            AddIfNonZero(list, StatId.GiveAttackSpeed, N(StatId.GiveAttackSpeed), t.statAttackSpeedMultiplier * 100f, "%", 0);
            AddIfNonZero(list, StatId.GiveSlow, N(StatId.GiveSlow), t.statSlowMultiplier * 100f, "%", 0);
            AddIfNonZero(list, StatId.GiveRegen, N(StatId.GiveRegen), t.statRegenMultiplier * 100f, "%", 0);
            AddIfNonZero(list, StatId.GiveOmnivamp, N(StatId.GiveOmnivamp), t.statOmnivampMultiplier * 100f, "%", 0);
        }

        /// <summary>
        /// Held: Schaden und Angriffstempo wie im Kampf (inklusive Aura und Perks aus dem Loadout), dazu Ausdauer und Lauftempo.
        /// Das Angriffstempo ist beim Helden ein Faktor auf die Animationen ("x1.10"), keine Schüsse pro Sekunde.
        /// </summary>
        private static void CollectHero(Hero hero, List<InspectStat> list)
        {
            Vitals v = hero.Vitals;

            if (v != null)
                AddHealth(list, v);

            AddDefense(list, v, v != null ? v.BaseArmor : hero.Armor, v != null ? v.BaseMagicResist : hero.MagicResist);

            Stamina stamina = hero.StaminaComponent;
            if (stamina != null)
                list.Add(InspectStat.Make(StatId.Stamina, N(StatId.Stamina), stamina.Current, stamina.Current, "", 0,
                    $" / {Mathf.CeilToInt(stamina.Max)}"));

            list.Add(InspectStat.Make(StatId.Damage, N(StatId.Damage), hero.AttackDamageEffective, hero.AttackDamageBase));
            list.Add(InspectStat.Make(StatId.AttackSpeed, N(StatId.AttackSpeed),
                hero.AttackSpeedEffective, hero.AttackSpeedBase, "x", 2));
            list.Add(InspectStat.Make(StatId.Range, N(StatId.Range), hero.EffectiveRange, hero.range, "", 1));

            AddOffense(list, hero.offense);

            if (!Mathf.Approximately(hero.statHealthRegenPerSecond, 0f) || !Mathf.Approximately(hero.EffectiveRegen, 0f))
                list.Add(InspectStat.Make(StatId.Regen, N(StatId.Regen),
                    hero.EffectiveRegen, hero.statHealthRegenPerSecond, "/s", 1));

            list.Add(InspectStat.Make(StatId.MoveSpeed, N(StatId.MoveSpeed), hero.RunSpeedEffective, hero.RunSpeedBase, "", 1));
        }

        private static void CollectWall(WallSegment wall, List<InspectStat> list)
        {
            WallGroup group = wall.WallGroup;
            if (group == null) return;

            // Leben, Schild und Rüstung gehören der ganzen Mauergruppe, nicht dem einzelnen Segment
            Vitals v = group.healthBarVitals;
        
            AddHealth(list, v);
            AddDefense(list, v, v.BaseArmor, v.BaseMagicResist);
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
            list.Add(InspectStat.Make(StatId.Damage, N(StatId.Damage), cfg.attackDamage, cfg.attackDamage));
            list.Add(InspectStat.Make(StatId.AttackSpeed, N(StatId.AttackSpeed), 1f / cooldown, 1f / cooldown, "/s", 1));
            list.Add(InspectStat.Make(StatId.Range, N(StatId.Range), cfg.attackRange, cfg.attackRange, "", 1));
            list.Add(InspectStat.Make(StatId.MoveSpeed, N(StatId.MoveSpeed),
                cfg.movementSpeed * e.SlowMultiplier, cfg.movementSpeed, "", 1));

            AddOffense(list, cfg.offense);

            AddIfNonZero(list, StatId.Evasion, N(StatId.Evasion), cfg.evasionChance, "%", 0);
            AddIfNonZero(list, StatId.Regen, N(StatId.Regen), cfg.healthRegenPerSecond, "/s", 1);
            AddIfNonZero(list, StatId.Gold, N(StatId.Gold), cfg.goldReward, "", 0);
            AddIfNonZero(list, StatId.XpDrop, N(StatId.XpDrop), cfg.xpDrop, "", 0);
        }

        // ------------------------------------------------------------------ Bausteine
        private static void AddHealth(List<InspectStat> list, Vitals v)
        {
            list.Add(InspectStat.Make(StatId.Health, "", v.Health, v.Health, "", 0,
                Texts.Stats.HealthSuffix(Mathf.CeilToInt(v.MaxHealth))));

            if (v.Shield > 0.5f)
                list.Add(InspectStat.Make(StatId.Shield, N(StatId.Shield), v.Shield, v.Shield));

            if (v.HasBlock)
            {
                // int.MaxValue = zeitlicher Block ohne Ladungen
                bool timed = v.BlockCharges > 999;
                list.Add(InspectStat.Make(StatId.Block, N(StatId.Block), timed ? 1f : v.BlockCharges, timed ? 1f : v.BlockCharges,
                    "", 0, timed ? Texts.Stats.BlockActive : Texts.Stats.BlockCharges));
            }

            if (v.AntiHealPercent > 0f)
                list.Add(InspectStat.Make(StatId.AntiHeal, N(StatId.AntiHeal), v.AntiHealPercent * 100f, v.AntiHealPercent * 100f, "%", 0));
        }

        /// <summary>Rüstung und MR: Grundwert gegen aktuellen Wert (Shred senkt, Buffs erhöhen).</summary>
        private static void AddDefense(List<InspectStat> list, Vitals v, float baseArmor, float baseMr)
        {
            float armor = v != null ? v.EffectiveArmor : baseArmor;
            float mr = v != null ? v.EffectiveMagicResist : baseMr;

            if (!Mathf.Approximately(baseArmor, 0f) || !Mathf.Approximately(armor, baseArmor))
                list.Add(InspectStat.Make(StatId.Armor, N(StatId.Armor), armor, baseArmor));

            if (!Mathf.Approximately(baseMr, 0f) || !Mathf.Approximately(mr, baseMr))
                list.Add(InspectStat.Make(StatId.MagicResist, N(StatId.MagicResist), mr, baseMr));
        }

        private static void AddOffense(List<InspectStat> list, OffenseStats o, float auraOmnivamp = 0f)
        {
            if (o == null) return;

            if (o.critChance > 0f)
            {
                list.Add(InspectStat.Make(StatId.Crit, N(StatId.Crit), o.critChance * 100f, o.critChance * 100f, "%", 0));
                list.Add(InspectStat.Make(StatId.CritDamage, N(StatId.CritDamage), o.critDamage * 100f, o.critDamage * 100f, "%", 0));
            }

            AddIfNonZero(list, StatId.Lethality, N(StatId.Lethality), o.lethality, "", 0);
            AddIfNonZero(list, StatId.ArmorPen, N(StatId.ArmorPen), o.armorPenPercent * 100f, "%", 0);
            AddIfNonZero(list, StatId.MagicPen, N(StatId.MagicPen), o.magicPenFlat, "", 0);
            AddIfNonZero(list, StatId.MagicPenPercent, N(StatId.MagicPenPercent), o.magicPenPercent * 100f, "%", 0);

            float omni = o.omnivamp * 100f;
            if (!Mathf.Approximately(omni, 0f))
                list.Add(InspectStat.Make(StatId.Omnivamp, N(StatId.Omnivamp),
                    omni * (1f + auraOmnivamp), omni, "%", 0));
        }

        private static void AddIfNonZero(List<InspectStat> list, StatId id, string name, float value,
            string suffix, int decimals)
        {
            if (Mathf.Approximately(value, 0f)) return;
            list.Add(InspectStat.Make(id, name, value, value, suffix, decimals));
        }
    }
}