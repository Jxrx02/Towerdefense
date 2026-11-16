using System;
using System.Globalization;
using UnityEngine;
using System.Collections.Generic;
using LevelSelector;

namespace TowerDefense
{
    public enum GameLanguage
    {
        English = 0,
        German = 1
    }

    /// <summary>
    /// Zentrale Verwaltung aller UI-Texte.
    ///
    ///   - Jeder Text steht genau einmal hier, beide Sprachen nebeneinander: T("English", "Deutsch").
    ///   - Zugriff typsicher per Property oder Methode: Texts.Research.Title, Texts.Research.UnlockButton(40).
    ///     Kein String-Schlüssel, ein Tippfehler ist ein Compile-Fehler.
    ///   - Die Sprache wird in PlayerPrefs gespeichert. Texts.Language = ... löst Texts.Changed aus.
    ///   - Statische Beschriftungen aktualisieren sich über LocalizedText, dynamische UIs (z. B. ResearchTreeUI)
    ///     bauen sich bei Texts.Changed neu auf.
    ///
    /// Neuen Text hinzufügen: Property im passenden Abschnitt ergänzen, in der UI Texts.X.Y verwenden.
    /// Neuer Abschnitt (z. B. Pause, Loadout): eigene verschachtelte static class, gleiches Muster.
    ///
    /// Nicht enthalten: Namen und Beschreibungen aus Daten-Assets (Turm-, Upgrade-, Gegnernamen). Sie stehen in den
    /// ScriptableObjects/Prefabs und müssten dort oder über eine eigene Tabelle übersetzt werden.
    /// </summary>
    public static partial class Texts
    {
        private const string PrefsKey = "language";

        private static GameLanguage _language;
        private static bool _loaded;

        /// <summary>Wird ausgelöst, wenn sich die Sprache ändert. UIs aktualisieren daraufhin ihre Texte.</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()   // nötig, falls "Domain Reload" aus ist
        {
            _loaded = false;
            Changed = null;
        }

        public static GameLanguage Language
        {
            get
            {
                if (!_loaded)
                {
                    _language = (GameLanguage)PlayerPrefs.GetInt(PrefsKey, (int)GameLanguage.English);
                    _loaded = true;
                }

                return _language;
            }
            set
            {
                if (_loaded && _language == value)
                    return;

                _language = value;
                _loaded = true;

                PlayerPrefs.SetInt(PrefsKey, (int)value);
                PlayerPrefs.Save();

                Changed?.Invoke();
            }
        }

        private static string T(string english, string german)
        {
            return Language == GameLanguage.German ? german : english;
        }

        private static string F(string english, string german, params object[] args)
        {
            return string.Format(CultureInfo.InvariantCulture, T(english, german), args);
        }

        // ══════════════════════════════════════════════════════════
        // Allgemein
        // ══════════════════════════════════════════════════════════
        public static class Common
        {
            public static string Back => T("Back", "Zurück");
            public static string Locked => T("Locked", "Gesperrt");
            public static string Unlocked => T("Unlocked", "Freigeschaltet");
        }

        // ══════════════════════════════════════════════════════════
        // Stat-Namen (Forschung, später auch InteractionPopup / Inspektion)
        // ══════════════════════════════════════════════════════════
        public static class Stats
        {
            public static string Damage => T("Damage", "Schaden");
            public static string Range => T("Range", "Reichweite");
            public static string AttackSpeed => T("Attack speed", "Angriffstempo");
            public static string Cooldown => T("Cooldown", "Abklingzeit");
            public static string Health => T("Health", "Leben");
            public static string Regen => T("Regen", "Regeneration");
            public static string GoldPerSecond => T("Gold per second", "Gold pro Sekunde");

            public static string AuraRange => T("Aura range", "Aura-Reichweite");
            public static string AuraDamage => T("Aura damage", "Aura-Schaden");
            public static string AuraAttackSpeed => T("Aura attack speed", "Aura-Angriffstempo");
            public static string AuraSlow => T("Aura slow", "Aura-Verlangsamung");
            public static string AuraRegen => T("Aura regen", "Aura-Regeneration");
            public static string AuraOmnivamp => T("Aura omnivamp", "Aura-Omnivamp");

            public static string Armor => T("Armor", "Rüstung");
            public static string MagicResist => T("Magic resist", "Magieresistenz");
            public static string CritChance => T("Crit chance", "Krit-Chance");
            public static string CritDamage => T("Crit damage", "Krit-Schaden");
            public static string Lethality => T("Lethality", "Letalität");
            public static string ArmorPen => T("Armor pen", "Rüstungsdurchdr.");
            public static string MagicPen => T("Magic pen", "Magiedurchdr.");
            public static string Omnivamp => T("Omnivamp", "Omnivamp");

            public static string MoveSpeed => T("Move speed", "Lauftempo");
            public static string Evasion => T("Evasion", "Ausweichen");
            public static string Stamina => T("Stamina", "Ausdauer");
            public static string Level => T("Level", "Stufe");

            // Block-/Lebens-Zusatztexte der Inspektion
            public static string HealthSuffix(int max) => F(" / {0} HP", " / {0} LP", max);
            public static string BlockActive => T(" Block active", " Block aktiv");
            public static string BlockCharges => T(" block charge(s)", " Block-Ladung(en)");

            /// <summary>Anzeigename eines Stats in der Inspektion (eine Quelle für Zeilen und Erklärung).</summary>
            public static string Of(StatId id)
            {
                switch (id)
                {
                    case StatId.Level: return Level;
                    case StatId.Health: return Health;
                    case StatId.Shield: return T("Shield", "Schild");
                    case StatId.Block: return "Block";
                    case StatId.AntiHeal: return "Anti-Heal";
                    case StatId.Armor: return Armor;
                    case StatId.MagicResist: return MagicResist;
                    case StatId.Damage: return Damage;
                    case StatId.AttackSpeed: return AttackSpeed;
                    case StatId.Range: return Range;
                    case StatId.Crit: return CritChance;
                    case StatId.CritDamage: return CritDamage;
                    case StatId.Lethality: return Lethality;
                    case StatId.ArmorPen: return ArmorPen;
                    case StatId.MagicPen:
                    case StatId.MagicPenPercent: return MagicPen;
                    case StatId.Omnivamp: return Omnivamp;
                    case StatId.Regen: return Regen;
                    case StatId.MoveSpeed: return MoveSpeed;
                    case StatId.Evasion: return Evasion;
                    case StatId.Gold: return "Gold";
                    case StatId.GiveRange: return AuraRange;
                    case StatId.GiveDamage: return AuraDamage;
                    case StatId.GiveAttackSpeed: return AuraAttackSpeed;
                    case StatId.GiveSlow: return AuraSlow;
                    case StatId.GiveRegen: return AuraRegen;
                    case StatId.GiveOmnivamp: return AuraOmnivamp;
                    case StatId.XpDrop: return T("XP drop", "XP-Drop");
                    case StatId.Stamina: return Stamina;
                    default: return id.ToString();
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        // Forschung (Skilltree)
        // ══════════════════════════════════════════════════════════
        public static class Research
        {
            public static string Title => T("Research", "Forschung");
            public static string TowersHeader => T("Towers", "Türme");
            public static string UpgradesHeader => T("Upgrades", "Upgrades");

            public static string Points(int points) =>
                F("Research points: {0}", "Forschungspunkte: {0}", points);

            public static string KeyHints => T(
                "Arrows: select   Enter: research   Q/E: tower   Esc: back",
                "Pfeile: wählen   Enter: erforschen   Q/E: Turm   Esc: zurück");

            // Kosten-Anzeige (RP = Research Points, FP = Forschungspunkte)
            public static string Cost(int cost) => F("{0} RP", "{0} FP", cost);

            // Turm-Liste
            public static string TowerLocked(int cost) => F("Locked - {0} RP", "Gesperrt - {0} FP", cost);

            public static string UpgradesProgress(int researched, int total) =>
                F("{0} / {1} upgrades", "{0} / {1} Upgrades", researched, total);

            // Details
            public static string StarterHint => T("Available from the start.", "Von Anfang an verfügbar.");

            public static string UpgradeSubtitle(string tower, string path, int level, int maxLevel) =>
                F("{0}  /  {1}  (Lv {2}/{3})", "{0}  /  {1}  (Stufe {2}/{3})", tower, path, level, maxLevel);

            public static string NoStatChanges => T("No stat changes.", "Keine Wertänderungen.");

            public static string MorePointsNeeded(int missing) =>
                F("{0} more research points needed.", "Noch {0} Forschungspunkte nötig.", missing);

            public static string UnlockTowerFirst(string tower) =>
                F("Unlock {0} first.", "Schalte zuerst {0} frei.", tower);

            public static string ResearchPreviousFirst =>
                T("Research the previous upgrade first.", "Erforsche zuerst das vorherige Upgrade.");

            // Aktions-Button
            public static string UnlockButton(int cost) => F("Unlock  -  {0} RP", "Freischalten  -  {0} FP", cost);
            public static string ResearchButton(int cost) => F("Research  -  {0} RP", "Erforschen  -  {0} FP", cost);
            public static string ResearchedButton => T("Researched", "Erforscht");

            // Zurücksetzen
            public static string ResetButton => T("Reset research", "Forschung zurücksetzen");

            public static string ResetConfirm => T(
                "Reset all research? Spent points are refunded.",
                "Gesamte Forschung zurücksetzen? Ausgegebene Punkte werden erstattet.");
        }
    }
}


namespace TowerDefense
{
    /// <summary>Texte der Progression (Leiste im Hauptmenü, Victory-Popup, Loadout-Kauf).</summary>
    public static partial class Texts
    {
        public static class Progression
        {
            public static string BarTitle => T("Progression", "Fortschritt");

            public static string Level(int level) => F("LEVEL {0}", "STUFE {0}", level);
            public static string Xp(int into, int need) => F("{0} / {1} XP", "{0} / {1} XP", into, need);
            public static string MaxLevel => T("MAX", "MAX");

            public static string ExpandHint => T("Click to open", "Klicken zum Öffnen");
            public static string CollapseHint => T("Click to close", "Klicken zum Schließen");
            public static string SelectItem => T("Select an item for details.", "Wähle einen Eintrag für Details.");

            // ── Art und Zustand ───────────────────────────────────
            public static string Kind(UnlockKind kind)
            {
                switch (kind)
                {
                    case UnlockKind.Weapon: return T("Weapon", "Waffe");
                    case UnlockKind.Mutator: return T("Mutator", "Mutator");
                    default: return T("Perk", "Perk");
                }
            }

            public static string NewUnlockHeader(UnlockKind kind)
            {
                switch (kind)
                {
                    case UnlockKind.Weapon: return T("NEW WEAPON", "NEUE WAFFE");
                    case UnlockKind.Mutator: return T("NEW MUTATOR", "NEUER MUTATOR");
                    default: return T("NEW PERK", "NEUER PERK");
                }
            }

            public static string NextUnlock(UnlockKind kind, string name, int level)
            {
                switch (kind)
                {
                    case UnlockKind.Weapon: return F("Next weapon: {0} (level {1})", "Nächste Waffe: {0} (Stufe {1})", name, level);
                    case UnlockKind.Mutator: return F("Next mutator: {0} (level {1})", "Nächster Mutator: {0} (Stufe {1})", name, level);
                    default: return F("Next perk: {0} (level {1})", "Nächster Perk: {0} (Stufe {1})", name, level);
                }
            }

            public static string State(UnlockState state, int cost)
            {
                switch (state)
                {
                    case UnlockState.Locked: return Common.Locked;
                    case UnlockState.Purchasable: return Research.Cost(cost);
                    case UnlockState.Owned: return T("Owned", "Im Besitz");
                    default: return Common.Unlocked;
                }
            }

            // ── Details ───────────────────────────────────────────
            public static string UnlocksAtLevel(int level) =>
                F("Unlocks at hero level {0}.", "Wird auf Heldenstufe {0} freigeschaltet.", level);

            public static string AvailableFromStart => T("Available from the start.", "Von Anfang an verfügbar.");

            public static string ScoreMultiplier(float multiplier) =>
                F("Score x{0:0.00}", "Punkte x{0:0.00}", multiplier);

            public static string WeaponDescription(string passive, string active) =>
                F("{2}\n{0}\n\n{3}\n{1}", "{2}\n{0}\n\n{3}\n{1}", passive, active, PassiveHeader, ActiveHeader);

            public static string LevelBadge(int level) => F("LV {0}", "ST. {0}", level);
            public static string PassiveHeader => T("PASSIVE", "PASSIV");
            public static string ActiveHeader => T("ACTIVE", "AKTIV");

            public static string WeaponPurchaseHint =>
                T("Unlocked. Buy it to use it in the loadout.", "Freigeschaltet. Kaufe sie, um sie im Loadout zu nutzen.");

            // ── Kauf ──────────────────────────────────────────────
            public static string BuyButton(int cost) => F("Buy  -  {0} RP", "Kaufen  -  {0} FP", cost);
            public static string OwnedButton => T("Owned", "Im Besitz");
            public static string LockedButton(int level) => F("Unlocks at level {0}", "Ab Stufe {0}", level);

            public static string NotEnoughPoints(int missing) =>
                F("{0} more research points needed.", "Noch {0} Forschungspunkte nötig.", missing);

            public static string PurchaseFailed => T("Purchase not possible.", "Kauf nicht möglich.");
        }

        public static class Loadout
        {
            public static string NoLevel => T("No level selected.", "Kein Level gewählt.");
            public static string SelectWeapon => T("Select a weapon.", "Wähle eine Waffe.");
            public static string WeaponNotOwned => T("Buy this weapon first.", "Kaufe diese Waffe zuerst.");
            public static string ReadyToStart => T("Ready to start.", "Bereit zum Start.");
            
            public static string ScoreMultiplier => T("Score multiplier", "Score-Multiplikator");
            public static string NoMultipliers => T("No multipliers selected", "Keine Multiplikatoren gewählt");
        }
    }
}


namespace TowerDefense
{
    /// <summary>Wessen Werte gerade angesehen werden (manche Stats funktionieren je nach Einheit anders).</summary>
    public enum StatSubject
    {
        Tower,
        Hero,
        Enemy
    }

    /// <summary>
    /// Erklärungen zu den Stats der Inspektion (Info-Button neben jedem Wert).
    ///
    /// Jede Aussage stammt aus dem tatsächlichen Code und nennt die Quelle:
    ///   Vitals            Schaden, Schilde, Block, Heilung, Anti-Heal, Shred, Omnivamp
    ///   DamageCalculator  Rüstung / Magieresistenz, Penetration (die Live-Zeilen rufen die echte Funktion auf)
    ///   DamageInfo        Crit
    ///   Tower             Aura, Schuss, Reichweite, Erfahrung
    ///   Hero, Stamina     Held
    ///   Enemy             Ausweichen, Verlangsamung
    /// Ändert sich eine Formel im Code, muss der Text hier mitgezogen werden.
    /// </summary>
    public static partial class Texts
    {
        public static class StatInfo
        {
            public struct Entry
            {
                public string title;
                public string effect;
                public string interactions;
                public string formula;
                public string modifiers;
            }

            // ── Abschnitte der Erklärung ──────────────────────────
            public static string SectionEffect => T("What it does", "Was es bewirkt");
            public static string SectionInteractions => T("How it affects other values", "Wie es andere Werte beeinflusst");
            public static string SectionFormula => T("Formula", "Formel");
            public static string SectionModifiers => T("What changes this value", "Was diesen Wert verändert");
            public static string SectionLive => T("Right now", "Aktuell");
            public static string Close => T("Close", "Schließen");
            public static string InfoTooltip => T("Explain this stat", "Diesen Wert erklären");

            private static Entry E(string title, string effect, string interactions, string formula, string modifiers)
            {
                return new Entry { title = title, effect = effect, interactions = interactions, formula = formula, modifiers = modifiers };
            }

            // Aura-Obergrenzen (Tower.MaxAuraBonus = +150 %, Tower.MaxAuraSlow = 80 %, Tower.AuraSlowDuration = 2 s)
            private static string AuraInteractions(string capEn, string capDe) => T(
                "The bonuses of all support towers whose range covers this tower add up. " + capEn +
                " Recalculated every 0.5 seconds. A tower never buffs itself and walls receive no auras.",
                "Die Boni aller Support-Türme, in deren Reichweite dieser Turm steht, addieren sich. " + capDe +
                " Wird alle 0,5 Sekunden neu berechnet. Ein Turm verstärkt sich nie selbst und Mauern erhalten keine Auren.");

            private static string AuraEffect => T(
                "Bonus this tower grants to other towers and the hero within its range (its range is the aura radius).",
                "Bonus, den dieser Turm anderen Türmen und dem Helden in seiner Reichweite gibt (seine Reichweite ist der Aura-Radius).");

            private static string AuraModifiers => T(
                "Upgrade multipliers of the support tower itself. The recipients' own stats are not changed permanently.",
                "Upgrade-Multiplikatoren des Support-Turms selbst. Die eigenen Werte der Empfänger werden nicht dauerhaft verändert.");

            public static Entry Get(StatId id, StatSubject subject)
            {
                bool hero = subject == StatSubject.Hero;
                bool enemy = subject == StatSubject.Enemy;

                switch (id)
                {
                    case StatId.Health:
                        return E(Stats.Health,
                            T("Hit points. At 0 the unit dies: enemies disappear and pay out gold and XP, towers and walls are destroyed and have to be repaired, the hero is downed and returns after the respawn time.",
                              "Trefferpunkte. Bei 0 stirbt die Einheit: Gegner verschwinden und geben Gold und XP, Türme und Mauern werden zerstört und müssen repariert werden, der Held geht zu Boden und kehrt nach der Respawn-Zeit zurück."),
                            T("Damage hits the shield first, then health. Healing (regeneration, omnivamp) is reduced by Anti-Heal and never exceeds the maximum. At the start of each day intact towers and walls are healed to full, the hero is not." +
                              (hero ? " The hero can only sprint at full health." : ""),
                              "Schaden trifft zuerst das Schild, dann das Leben. Heilung (Regeneration, Omnivamp) wird durch Anti-Heal verringert und überschreitet nie das Maximum. Zu Beginn jedes Tages werden intakte Türme und Mauern vollständig geheilt, der Held nicht." +
                              (hero ? " Der Held kann nur bei vollem Leben sprinten." : "")),
                            T("Health after a hit = health - max(0, damage x resist multiplier - shield)",
                              "Leben nach einem Treffer = Leben - max(0, Schaden x Resistenz-Multiplikator - Schild)"),
                            T("Base health, health from upgrades and level-ups, regeneration, shields, armor / magic resist, Anti-Heal.",
                              "Basisleben, Leben aus Upgrades und Level-Ups, Regeneration, Schilde, Rüstung / Magieresistenz, Anti-Heal."));

                    case StatId.Shield:
                        return E(T("Shield", "Schild"),
                            T("Extra hit points. Absorbs damage after armor / magic resist and before health. Some shields expire after a duration.",
                              "Zusätzliche Trefferpunkte. Absorbiert Schaden nach Rüstung / Magieresistenz und vor dem Leben. Manche Schilde verfallen nach einer Dauer."),
                            T("The health bar is extended by the shield. Shields from pulses do not stack (the larger value is kept), other shields add up. Hits that bypass shields ignore it.",
                              "Die Lebensleiste wird um das Schild verlängert. Schilde aus Pulsen stapeln sich nicht (der größere Wert bleibt), andere Schilde addieren sich. Treffer, die Schilde umgehen, ignorieren es."),
                            T("Absorbed = min(shield, damage after armor / magic resist). The rest of the damage goes to health.",
                              "Absorbiert = min(Schild, Schaden nach Rüstung / Magieresistenz). Der Rest des Schadens geht ans Leben."),
                            T("Shield effects on the unit, for example the start shield of an enemy or healer pulses, and their duration.",
                              "Schild-Effekte auf der Einheit, z. B. das Start-Schild eines Gegners oder Heiler-Pulse, und deren Dauer."));

                    case StatId.Block:
                        return E("Block",
                            T("Blocks incoming hits completely while it lasts.",
                              "Fängt eingehende Treffer vollständig ab, solange er besteht."),
                            T("Each blocked hit uses one charge. A timed block has no charges and ends after its duration. Damage-over-time ticks are blocked too but use no charge. Effects of a blocked hit (slow, armor shred, anti-heal) are not applied. Some enemies renew their block after a delay.",
                              "Jeder geblockte Treffer verbraucht eine Ladung. Ein zeitlicher Block hat keine Ladungen und endet nach seiner Dauer. Schadens-Ticks über Zeit werden ebenfalls geblockt, verbrauchen aber keine Ladung. Effekte eines geblockten Treffers (Verlangsamung, Rüstungs-Shred, Anti-Heal) wirken nicht. Manche Gegner erneuern ihren Block nach einer Wartezeit."),
                            T("Charges after a hit = charges - 1 (not for damage-over-time ticks)",
                              "Ladungen nach einem Treffer = Ladungen - 1 (nicht bei Schadens-Ticks über Zeit)"),
                            T("Charges and duration set by the effect that grants the block (enemy configuration, support pulses).",
                              "Ladungen und Dauer legt der Effekt fest, der den Block gibt (Gegner-Konfiguration, Support-Pulse)."));

                    case StatId.AntiHeal:
                        return E("Anti-Heal",
                            T("Reduces all healing the unit receives.", "Verringert jede Heilung, die die Einheit erhält."),
                            T("Applies to regeneration and omnivamp. Healing at the start of the day ignores it. The strongest anti-heal wins, applying it again refreshes the duration.",
                              "Gilt für Regeneration und Omnivamp. Die Heilung zu Beginn des Tages ignoriert ihn. Der stärkste Anti-Heal gewinnt, erneutes Anwenden frischt die Dauer auf."),
                            T("Healing received = healing x (1 - anti-heal)", "Erhaltene Heilung = Heilung x (1 - Anti-Heal)"),
                            T("Hit effects of projectiles (anti-heal percent and duration).",
                              "Treffer-Effekte von Projektilen (Anti-Heal-Prozent und Dauer)."));

                    case StatId.Armor:
                        return E(Stats.Armor,
                            T("Reduces physical damage taken.", "Verringert erlittenen physischen Schaden."),
                            T("Magic damage uses magic resist instead, true damage ignores both. Attackers weaken it with armor penetration and lethality. Armor shred lowers it for a while and can push it below 0, which increases the damage taken.",
                              "Magischer Schaden nutzt stattdessen die Magieresistenz, wahrer Schaden ignoriert beides. Angreifer schwächen sie mit Rüstungsdurchdringung und Letalität. Rüstungs-Shred senkt sie zeitweise und kann sie unter 0 drücken, was den erlittenen Schaden erhöht."),
                            T("Damage multiplier = 100 / (100 + armor). Below 0: 2 - 100 / (100 - armor).\nPenetration first: armor x (1 - armor pen %), then - lethality (not below 0). Penetration only works on positive armor.",
                              "Schadens-Multiplikator = 100 / (100 + Rüstung). Unter 0: 2 - 100 / (100 - Rüstung).\nPenetration zuerst: Rüstung x (1 - Rüstungsdurchdr. %), dann - Letalität (nicht unter 0). Penetration wirkt nur auf positive Rüstung."),
                            T("Base armor (prefab or config plus upgrades and level-ups). Shred: flat reduction per stack, then a percentage per stack (at most 90 %, only on positive values). The shown value is the current effective armor.",
                              "Basisrüstung (Prefab oder Konfiguration plus Upgrades und Level-Ups). Shred: feste Senkung pro Stapel, danach ein Prozentwert pro Stapel (höchstens 90 %, nur bei positiven Werten). Der angezeigte Wert ist die aktuelle effektive Rüstung."));

                    case StatId.MagicResist:
                        return E(Stats.MagicResist,
                            T("Reduces magic damage taken.", "Verringert erlittenen magischen Schaden."),
                            T("Physical damage uses armor instead, true damage ignores both. Attackers weaken it with magic penetration (flat and percent). Shred can lower it below 0, which increases the damage taken.",
                              "Physischer Schaden nutzt stattdessen die Rüstung, wahrer Schaden ignoriert beides. Angreifer schwächen sie mit Magiedurchdringung (fest und prozentual). Shred kann sie unter 0 senken, was den erlittenen Schaden erhöht."),
                            T("Damage multiplier = 100 / (100 + magic resist). Below 0: 2 - 100 / (100 - magic resist).\nPenetration first: magic resist x (1 - magic pen %), then - flat magic pen (not below 0). Penetration only works on positive values.",
                              "Schadens-Multiplikator = 100 / (100 + Magieresistenz). Unter 0: 2 - 100 / (100 - Magieresistenz).\nPenetration zuerst: Magieresistenz x (1 - Magiedurchdr. %), dann - feste Magiedurchdr. (nicht unter 0). Penetration wirkt nur auf positive Werte."),
                            T("Base magic resist (prefab or config plus upgrades and level-ups). Shred: flat reduction per stack, then a percentage per stack (at most 90 %). The shown value is the current effective magic resist.",
                              "Basis-Magieresistenz (Prefab oder Konfiguration plus Upgrades und Level-Ups). Shred: feste Senkung pro Stapel, danach ein Prozentwert pro Stapel (höchstens 90 %). Der angezeigte Wert ist die aktuelle effektive Magieresistenz."));

                    case StatId.Damage:
                        if (enemy)
                            return E(Stats.Damage,
                                T("Damage of each attack.", "Schaden jedes Angriffs."),
                                T("Crits multiply it, armor or magic resist of the target reduce it depending on the damage type (physical, magic, true). Omnivamp heals for a share of the damage that gets through.",
                                  "Crits vervielfachen ihn, Rüstung oder Magieresistenz des Ziels verringern ihn je nach Schadensart (physisch, magisch, wahr). Omnivamp heilt einen Anteil des Schadens, der durchkommt."),
                                T("Hit = attack damage (x crit damage on a crit), then x resist multiplier of the target.",
                                  "Treffer = Angriffsschaden (x Krit-Schaden bei einem Crit), danach x Resistenz-Multiplikator des Ziels."),
                                T("The enemy's configuration (damage and damage type).", "Die Konfiguration des Gegners (Schaden und Schadensart)."));

                        if (hero)
                            return E(Stats.Damage,
                                T("Base damage of the hero's attacks, including aura and perks.",
                                  "Basisschaden der Angriffe des Helden, inklusive Aura und Perks."),
                                T("Each attack multiplies it by its own factor, charged attacks by up to their charge bonus. Crits multiply it, armor or magic resist of the target reduce it depending on the damage type.",
                                  "Jeder Angriff multipliziert ihn mit seinem eigenen Faktor, aufgeladene Angriffe mit bis zu ihrem Aufladebonus. Crits vervielfachen ihn, Rüstung oder Magieresistenz des Ziels verringern ihn je nach Schadensart."),
                                T("Shown value = damage x (1 + aura damage) x perk multiplier.\nHit = shown value x attack factor x (charge bonus when charged), x crit damage on a crit, then x resist multiplier of the target.",
                                  "Angezeigter Wert = Schaden x (1 + Aura-Schaden) x Perk-Multiplikator.\nTreffer = angezeigter Wert x Angriffsfaktor x (Aufladebonus bei aufgeladenen Angriffen), x Krit-Schaden bei einem Crit, danach x Resistenz-Multiplikator des Ziels."),
                                T("Damage of the equipped weapon (replaces the hero's base damage when above 0), level-up bonuses, aura damage (at most +150 %), the damage multiplier from the loadout perks.",
                                  "Schaden der ausgerüsteten Waffe (ersetzt den Basisschaden des Helden, wenn über 0), Level-Up-Boni, Aura-Schaden (höchstens +150 %), der Schadens-Multiplikator aus den Perks des Loadouts."));

                        return E(Stats.Damage,
                            T("Damage dealt per shot before the target's defenses.", "Schaden pro Schuss vor den Verteidigungswerten des Ziels."),
                            T("Crits multiply it, armor or magic resist of the target reduce it depending on the damage type of the projectile. Support towers add a percentage. Omnivamp heals for a share of the damage that gets through.",
                              "Crits vervielfachen ihn, Rüstung oder Magieresistenz des Ziels verringern ihn je nach Schadensart des Projektils. Support-Türme addieren einen Prozentwert. Omnivamp heilt einen Anteil des Schadens, der durchkommt."),
                            T("Shot damage = round(damage x (1 + aura damage)) + base damage of the projectile; x crit damage on a crit; then x resist multiplier.\nSpecial projectile modes (chain, damage over time, splitters, sticky bombs) use their own damage values.",
                              "Schussschaden = gerundet(Schaden x (1 + Aura-Schaden)) + Basisschaden des Projektils; x Krit-Schaden bei einem Crit; danach x Resistenz-Multiplikator.\nBesondere Projektil-Modi (Kette, Schaden über Zeit, Splitter, Haftbomben) nutzen eigene Schadenswerte."),
                            T("Base damage, damage from upgrades and level-ups, aura damage (at most +150 %), base damage of the projectile.",
                              "Basisschaden, Schaden aus Upgrades und Level-Ups, Aura-Schaden (höchstens +150 %), Basisschaden des Projektils."));

                    case StatId.AttackSpeed:
                        if (enemy)
                            return E(Stats.AttackSpeed,
                                T("How often the enemy attacks.", "Wie oft der Gegner angreift."),
                                T("The enemy attacks as soon as its target is in attack range. The attack animation plays first, the cooldown starts after the hit.",
                                  "Der Gegner greift an, sobald sein Ziel in Angriffsreichweite ist. Zuerst läuft die Angriffsanimation, die Abklingzeit beginnt nach dem Treffer."),
                                T("Attacks per second = 1 / attack cooldown (seconds)", "Angriffe pro Sekunde = 1 / Abklingzeit (Sekunden)"),
                                T("The enemy's configuration (attack cooldown).", "Die Konfiguration des Gegners (Abklingzeit)."));

                        if (hero)
                            return E(Stats.AttackSpeed,
                                T("Speed factor of the hero's attack animations.", "Tempo-Faktor der Angriffsanimationen des Helden."),
                                T("Faster animations shorten every swing and shot. Recovery time, combo window, the charge time and the cooldown after a finished combo are not scaled.",
                                  "Schnellere Animationen verkürzen jeden Schlag und Schuss. Erholungszeit, Kombo-Fenster, Aufladezeit und die Abklingzeit nach einer fertigen Kombo werden nicht skaliert."),
                                T("Factor = attack speed multiplier x (1 + aura attack speed) x perk multiplier.\nAnimation playback speed = animation speed of the attack x factor.",
                                  "Faktor = Angriffstempo-Multiplikator x (1 + Aura-Angriffstempo) x Perk-Multiplikator.\nAbspielgeschwindigkeit der Animation = Animationstempo des Angriffs x Faktor."),
                                T("Attack speed multiplier from upgrades and level-ups, aura attack speed (at most +150 %), the attack speed multiplier from the loadout perks.",
                                  "Angriffstempo-Multiplikator aus Upgrades und Level-Ups, Aura-Angriffstempo (höchstens +150 %), der Angriffstempo-Multiplikator aus den Perks des Loadouts."));

                        return E(Stats.AttackSpeed,
                            T("How often the tower attacks (attacks per second, without the attack animation).",
                              "Wie oft der Turm angreift (Angriffe pro Sekunde, ohne die Angriffsanimation)."),
                            T("The attack animation plays faster by the same factor. Support towers can raise it.",
                              "Die Angriffsanimation läuft um denselben Faktor schneller. Support-Türme können ihn erhöhen."),
                            T("Attacks per second = attack speed multiplier x (1 + aura attack speed) / cooldown.\nWait after a shot = cooldown / (attack speed multiplier x (1 + aura attack speed)). The cooldown is at least 0.1 s.",
                              "Angriffe pro Sekunde = Angriffstempo-Multiplikator x (1 + Aura-Angriffstempo) / Abklingzeit.\nWartezeit nach einem Schuss = Abklingzeit / (Angriffstempo-Multiplikator x (1 + Aura-Angriffstempo)). Die Abklingzeit beträgt mindestens 0,1 s."),
                            T("Cooldown (time between shots) and attack speed multiplier from upgrades and level-ups, aura attack speed (at most +150 %).",
                              "Abklingzeit (Zeit zwischen Schüssen) und Angriffstempo-Multiplikator aus Upgrades und Level-Ups, Aura-Angriffstempo (höchstens +150 %)."));

                    case StatId.Range:
                        if (enemy)
                            return E(Stats.Range,
                                T("Distance from which the enemy attacks its target.", "Entfernung, aus der der Gegner sein Ziel angreift."),
                                T("Melee enemies need to get close, ranged enemies shoot from afar. Towers and the hero within the aggro range are chased and attacked.",
                                  "Nahkämpfer müssen nah herankommen, Fernkämpfer schießen aus der Ferne. Türme und der Held in Aggro-Reichweite werden verfolgt und angegriffen."),
                                T("The enemy attacks when distance to the target <= attack range.",
                                  "Der Gegner greift an, wenn die Entfernung zum Ziel <= Angriffsreichweite."),
                                T("The enemy's configuration.", "Die Konfiguration des Gegners."));

                        return E(Stats.Range,
                            T("Distance within which targets are attacked. For support towers it is also the radius of their aura.",
                              "Entfernung, in der Ziele angegriffen werden. Bei Support-Türmen ist es zugleich der Radius ihrer Aura."),
                            T("The targeting preference (first, strongest, nearest ...) chooses among the enemies in range. Towers and the hero also gain XP from enemies that die inside their range.",
                              "Die Zielpriorität (erstes, stärkstes, nächstes ...) wählt unter den Gegnern in Reichweite. Türme und der Held erhalten außerdem XP von Gegnern, die in ihrer Reichweite sterben."),
                            T("Effective range = range x (1 + aura range). A target is in range when its distance <= effective range.",
                              "Effektive Reichweite = Reichweite x (1 + Aura-Reichweite). Ein Ziel ist in Reichweite, wenn seine Entfernung <= effektive Reichweite."),
                            T("Base range and range from upgrades and level-ups, aura range (at most +150 %)." + (hero ? " The hero's range comes from the equipped weapon." : ""),
                              "Basisreichweite und Reichweite aus Upgrades und Level-Ups, Aura-Reichweite (höchstens +150 %)." + (hero ? " Die Reichweite des Helden stammt von der ausgerüsteten Waffe." : "")));

                    case StatId.Crit:
                        return E(Stats.CritChance,
                            T("Chance that an attack is a critical hit.", "Chance, dass ein Angriff ein kritischer Treffer ist."),
                            T("A crit multiplies the damage by the crit damage. The roll happens once per attack (shot or swing): all targets of one swing share it. Damage-over-time ticks never crit.",
                              "Ein Crit multipliziert den Schaden mit dem Krit-Schaden. Gewürfelt wird einmal pro Angriff (Schuss oder Schlag): alle Ziele eines Schlags teilen sich das Ergebnis. Schadens-Ticks über Zeit critten nie."),
                            T("Crit if a random number (0 to 1) < crit chance. The chance is limited to 0 - 100 %.\nAverage damage factor = 1 + chance x (crit damage - 1)",
                              "Crit, wenn eine Zufallszahl (0 bis 1) < Krit-Chance. Die Chance ist auf 0 - 100 % begrenzt.\nDurchschnittlicher Schadensfaktor = 1 + Chance x (Krit-Schaden - 1)"),
                            T("Base crit chance and bonuses from upgrades and level-ups (added up, at most 100 %).",
                              "Basis-Krit-Chance und Boni aus Upgrades und Level-Ups (addiert, höchstens 100 %)."));

                    case StatId.CritDamage:
                        return E(Stats.CritDamage,
                            T("How much stronger a critical hit is.", "Wie viel stärker ein kritischer Treffer ist."),
                            T("Only matters when the crit chance is above 0.", "Spielt nur eine Rolle, wenn die Krit-Chance über 0 liegt."),
                            T("Damage on a crit = damage x crit multiplier (never below x1). Shown in percent: 175 % = x1.75.",
                              "Schaden bei einem Crit = Schaden x Krit-Multiplikator (nie unter x1). Angezeigt in Prozent: 175 % = x1,75."),
                            T("Base crit damage (175 % by default) and bonuses from upgrades and level-ups, added to the multiplier (+25 % = +0.25).",
                              "Basis-Krit-Schaden (standardmäßig 175 %) und Boni aus Upgrades und Level-Ups, die auf den Multiplikator addiert werden (+25 % = +0,25)."));

                    case StatId.Lethality:
                        return E(Stats.Lethality,
                            T("Flat armor penetration against physical damage.", "Feste Rüstungsdurchdringung gegen physischen Schaden."),
                            T("Applied after armor penetration in percent. It only works on positive armor and never pushes armor below 0, so it cannot increase damage beyond the unarmored value. No effect on magic damage.",
                              "Wird nach der prozentualen Rüstungsdurchdringung angewendet. Wirkt nur auf positive Rüstung und drückt sie nie unter 0, erhöht den Schaden also nie über den Wert ohne Rüstung. Kein Effekt auf magischen Schaden."),
                            T("Effective armor = max(0, armor x (1 - armor pen %) - lethality)",
                              "Effektive Rüstung = max(0, Rüstung x (1 - Rüstungsdurchdr. %) - Letalität)"),
                            T("Base lethality and bonuses from upgrades and level-ups.", "Basis-Letalität und Boni aus Upgrades und Level-Ups."));

                    case StatId.ArmorPen:
                        return E(Stats.ArmorPen,
                            T("Percentage armor penetration against physical damage.", "Prozentuale Rüstungsdurchdringung gegen physischen Schaden."),
                            T("Applied before lethality. Only works on positive armor. No effect on magic damage.",
                              "Wird vor der Letalität angewendet. Wirkt nur auf positive Rüstung. Kein Effekt auf magischen Schaden."),
                            T("Effective armor = armor x (1 - armor pen %), then - lethality (not below 0). Limited to 0 - 100 %.",
                              "Effektive Rüstung = Rüstung x (1 - Rüstungsdurchdr. %), dann - Letalität (nicht unter 0). Begrenzt auf 0 - 100 %."),
                            T("Base value and bonuses from upgrades and level-ups (added up, at most 100 %).",
                              "Basiswert und Boni aus Upgrades und Level-Ups (addiert, höchstens 100 %)."));

                    case StatId.MagicPen:
                        return E(T("Magic pen (flat)", "Magiedurchdr. (fest)"),
                            T("Flat magic resist penetration against magic damage.", "Feste Magiewiderstands-Durchdringung gegen magischen Schaden."),
                            T("Applied after magic penetration in percent. Only works on positive magic resist and never pushes it below 0. No effect on physical damage.",
                              "Wird nach der prozentualen Magiedurchdringung angewendet. Wirkt nur auf positive Magieresistenz und drückt sie nie unter 0. Kein Effekt auf physischen Schaden."),
                            T("Effective magic resist = max(0, magic resist x (1 - magic pen %) - flat magic pen)",
                              "Effektive Magieresistenz = max(0, Magieresistenz x (1 - Magiedurchdr. %) - feste Magiedurchdr.)"),
                            T("Base value and bonuses from upgrades and level-ups.", "Basiswert und Boni aus Upgrades und Level-Ups."));

                    case StatId.MagicPenPercent:
                        return E(T("Magic pen (percent)", "Magiedurchdr. (prozentual)"),
                            T("Percentage magic resist penetration against magic damage.", "Prozentuale Magiewiderstands-Durchdringung gegen magischen Schaden."),
                            T("Applied before flat magic penetration. Only works on positive magic resist. No effect on physical damage.",
                              "Wird vor der festen Magiedurchdringung angewendet. Wirkt nur auf positive Magieresistenz. Kein Effekt auf physischen Schaden."),
                            T("Effective magic resist = magic resist x (1 - magic pen %), then - flat magic pen (not below 0). Limited to 0 - 100 %.",
                              "Effektive Magieresistenz = Magieresistenz x (1 - Magiedurchdr. %), dann - feste Magiedurchdr. (nicht unter 0). Begrenzt auf 0 - 100 %."),
                            T("Base value and bonuses from upgrades and level-ups (added up, at most 100 %).",
                              "Basiswert und Boni aus Upgrades und Level-Ups (addiert, höchstens 100 %)."));

                    case StatId.Omnivamp:
                        return E(Stats.Omnivamp,
                            T("Heals the attacker for a share of the damage it deals, for every damage type.",
                              "Heilt den Angreifer um einen Anteil des verursachten Schadens, bei jeder Schadensart."),
                            T("Based on the damage after armor / magic resist, including the part absorbed by shields. The heal is reduced by the attacker's own Anti-Heal and cannot exceed its maximum health. No heal for blocked hits or against itself. Projectiles of towers multiply it by (1 + aura omnivamp); the shown value is without the aura.",
                              "Basiert auf dem Schaden nach Rüstung / Magieresistenz, einschließlich des von Schilden absorbierten Teils. Die Heilung wird durch den eigenen Anti-Heal verringert und überschreitet nie das Maximalleben. Keine Heilung bei geblockten Treffern oder gegen sich selbst. Projektile von Türmen multiplizieren ihn mit (1 + Aura-Omnivamp); der angezeigte Wert gilt ohne Aura."),
                            T("Heal = damage after mitigation x omnivamp x scale (scale is 1 unless an attack lowers it)",
                              "Heilung = Schaden nach Abzug x Omnivamp x Skalierung (Skalierung ist 1, außer ein Angriff senkt sie)"),
                            T("Base omnivamp and bonuses from upgrades and level-ups (added up, at most 100 %), aura omnivamp.",
                              "Basis-Omnivamp und Boni aus Upgrades und Level-Ups (addiert, höchstens 100 %), Aura-Omnivamp."));

                    case StatId.Regen:
                        return E(Stats.Regen,
                            T("Health restored per second.", "Pro Sekunde wiederhergestelltes Leben."),
                            T("Only while health is below the maximum. Reduced by Anti-Heal." + (enemy ? "" : " Support towers can raise it with a regeneration aura."),
                              "Nur solange das Leben unter dem Maximum liegt. Wird durch Anti-Heal verringert." + (enemy ? "" : " Support-Türme können es mit einer Regenerations-Aura erhöhen.")),
                            enemy
                                ? T("Healing per second = regeneration x (1 - anti-heal)", "Heilung pro Sekunde = Regeneration x (1 - Anti-Heal)")
                                : T("Regeneration = base regeneration x (1 + aura regen)\nHealing per second = regeneration x (1 - anti-heal)",
                                    "Regeneration = Basis-Regeneration x (1 + Aura-Regeneration)\nHeilung pro Sekunde = Regeneration x (1 - Anti-Heal)"),
                            enemy
                                ? T("The enemy's configuration.", "Die Konfiguration des Gegners.")
                                : T("Base regeneration and bonuses from upgrades and level-ups, aura regen (at most +150 %).",
                                    "Basis-Regeneration und Boni aus Upgrades und Level-Ups, Aura-Regeneration (höchstens +150 %)."));

                    case StatId.MoveSpeed:
                        if (hero)
                            return E(Stats.MoveSpeed,
                                T("How fast the hero runs (world units per second).", "Wie schnell der Held läuft (Welteinheiten pro Sekunde)."),
                                T("Walking, sprinting and walking backwards use their own factors. Moving while attacking, charging or blocking is slower. Dash, dodge and jump cover a fixed distance and are not affected. Sprinting is only possible at full health.",
                                  "Gehen, Sprinten und Rückwärtsgehen haben eigene Faktoren. Bewegung beim Angreifen, Aufladen oder Blocken ist langsamer. Dash, Ausweichen und Sprung legen eine feste Strecke zurück und sind nicht betroffen. Sprinten ist nur bei vollem Leben möglich."),
                                T("Speed = run speed x locomotion factor x action multiplier x weapon factor.\nShown: run speed x weapon factor (running, no action).",
                                  "Tempo = Lauftempo x Bewegungsfaktor x Aktions-Multiplikator x Waffenfaktor.\nAngezeigt: Lauftempo x Waffenfaktor (Laufen, ohne Aktion)."),
                                T("Run speed of the hero, locomotion factors in the hero's motor settings, speed multipliers of attacks, charging and blocking, move speed factor of the equipped weapon.",
                                  "Lauftempo des Helden, Bewegungsfaktoren in den Motor-Einstellungen des Helden, Tempo-Multiplikatoren von Angriffen, Aufladen und Blocken, Lauftempo-Faktor der ausgerüsteten Waffe."));

                        return E(Stats.MoveSpeed,
                            T("Movement speed of the enemy (world units per second).", "Bewegungstempo des Gegners (Welteinheiten pro Sekunde)."),
                            T("Slows reduce it. A running stronger slow is not replaced by a weaker one. Knockback and pull interrupt walking.",
                              "Verlangsamungen senken es. Eine laufende stärkere Verlangsamung wird nicht durch eine schwächere ersetzt. Rückstoß und Sog unterbrechen das Laufen."),
                            T("Speed = base speed x slow multiplier, slow multiplier = 1 - slow (0 - 100 %)",
                              "Tempo = Basistempo x Verlangsamungs-Multiplikator, Multiplikator = 1 - Verlangsamung (0 - 100 %)"),
                            T("The enemy's configuration, slows from projectile hits, slow auras of towers (at most 80 %) and chain links.",
                              "Die Konfiguration des Gegners, Verlangsamungen durch Projektiltreffer, Slow-Auren von Türmen (höchstens 80 %) und Kettenverbindungen."));

                    case StatId.Stamina:
                        return E(Stats.Stamina,
                            T("Energy for attacks and movement actions. Every attack costs stamina, dash, dodge and jump can cost stamina too.",
                              "Energie für Angriffe und Bewegungsaktionen. Jeder Angriff kostet Ausdauer, Dash, Ausweichen und Sprung können ebenfalls Ausdauer kosten."),
                            T("At 0 the hero is exhausted: no new actions until stamina has recovered to a part of the maximum. The last action may take stamina to 0 even if it costs more than is left. Regeneration pauses shortly after spending and while an attack is charged.",
                              "Bei 0 ist der Held erschöpft: keine neuen Aktionen, bis sich die Ausdauer auf einen Teil des Maximums erholt hat. Die letzte Aktion darf die Ausdauer auf 0 ziehen, auch wenn sie mehr kostet als noch da ist. Die Aufladung pausiert kurz nach einem Verbrauch und während ein Angriff aufgeladen wird."),
                            T("Regeneration per second applies after the regeneration delay since the last spending. While exhausted it is multiplied by the exhausted factor until stamina >= maximum x recovery fraction.",
                              "Aufladung pro Sekunde gilt nach der Aufladeverzögerung seit dem letzten Verbrauch. Während der Erschöpfung wird sie mit dem Erschöpfungsfaktor multipliziert, bis Ausdauer >= Maximum x Erholungsanteil."),
                            T("Maximum, regeneration, delay and exhaustion settings of the hero, stamina costs of the attacks (weapon) and of dash, dodge and jump (motor settings).",
                              "Maximum, Aufladung, Verzögerung und Erschöpfungs-Einstellungen des Helden, Ausdauerkosten der Angriffe (Waffe) sowie von Dash, Ausweichen und Sprung (Motor-Einstellungen)."));

                    case StatId.Evasion:
                        return E(Stats.Evasion,
                            T("Chance to avoid a hit completely.", "Chance, einem Treffer vollständig auszuweichen."),
                            T("An evaded hit deals no damage. Damage-over-time ticks cannot be evaded. The roll happens for every single hit.",
                              "Ein ausgewichener Treffer verursacht keinen Schaden. Schadens-Ticks über Zeit können nicht ausgewichen werden. Gewürfelt wird für jeden einzelnen Treffer."),
                            T("Evaded if a random number (0 to 100) < evasion %", "Ausgewichen, wenn eine Zufallszahl (0 bis 100) < Ausweichen %"),
                            T("The enemy's configuration.", "Die Konfiguration des Gegners."));

                    case StatId.Gold:
                        return E("Gold",
                            enemy
                                ? T("Gold paid out when the enemy dies.", "Gold, das beim Tod des Gegners ausgezahlt wird.")
                                : T("Gold income of this building per second.", "Goldeinkommen dieses Gebäudes pro Sekunde."),
                            T("Gold buys towers, walls and upgrades. Gold left at the end of a level counts x2 towards the score.",
                              "Mit Gold werden Türme, Mauern und Upgrades gekauft. Gold, das am Ende eines Levels übrig ist, zählt x2 für den Score."),
                            enemy
                                ? T("Gold += reward when the enemy dies", "Gold += Belohnung, wenn der Gegner stirbt")
                                : T("Income per second as set by the upgrades of the building", "Einkommen pro Sekunde laut den Upgrades des Gebäudes"),
                            enemy
                                ? T("The enemy's configuration.", "Die Konfiguration des Gegners.")
                                : T("Upgrades of the building.", "Upgrades des Gebäudes."));

                    case StatId.XpDrop:
                        return E(T("XP drop", "XP-Drop"),
                            T("Experience that towers and the hero gain when this enemy dies.", "Erfahrung, die Türme und der Held erhalten, wenn dieser Gegner stirbt."),
                            T("Every tower or hero that has the enemy inside its effective range receives the full amount. Level-ups give the tower its bonus per level.",
                              "Jeder Turm und der Held, der den Gegner in seiner effektiven Reichweite hat, erhält die volle Menge. Level-Ups geben dem Turm seinen Bonus pro Stufe."),
                            T("XP gained = XP drop, if distance to the enemy <= effective range", "Erhaltene XP = XP-Drop, wenn Entfernung zum Gegner <= effektive Reichweite"),
                            T("The enemy's configuration.", "Die Konfiguration des Gegners."));

                    case StatId.Level:
                        return E(Stats.Level,
                            T("Experience level of the tower. Gains XP from enemies that die inside its range.", "Erfahrungsstufe des Turms. Erhält XP von Gegnern, die in seiner Reichweite sterben."),
                            T("Every level-up adds the bonus per level of the tower (damage, range, health, armor and more).",
                              "Jeder Level-Up addiert den Bonus pro Stufe des Turms (Schaden, Reichweite, Leben, Rüstung und mehr)."),
                            T("Required XP = round(curve(level / max level) x max required XP), from the level configuration.",
                              "Benötigte XP = gerundet(Kurve(Stufe / Maximalstufe) x maximal benötigte XP), aus der Level-Konfiguration."),
                            T("Level configuration (curve, max level, max required XP), XP drop of the enemies.",
                              "Level-Konfiguration (Kurve, Maximalstufe, maximal benötigte XP), XP-Drop der Gegner."));

                    case StatId.GiveRange:
                        return E(Stats.AuraRange, AuraEffect,
                            AuraInteractions("The total is capped at +150 %.", "Die Summe ist auf +150 % begrenzt."),
                            T("Effective range of a recipient = range x (1 + total aura range)", "Effektive Reichweite eines Empfängers = Reichweite x (1 + Summe Aura-Reichweite)"),
                            AuraModifiers);

                    case StatId.GiveDamage:
                        return E(Stats.AuraDamage, AuraEffect,
                            AuraInteractions("The total is capped at +150 %.", "Die Summe ist auf +150 % begrenzt."),
                            T("Shot damage of a recipient = round(damage x (1 + total aura damage)) + projectile base damage",
                              "Schussschaden eines Empfängers = gerundet(Schaden x (1 + Summe Aura-Schaden)) + Basisschaden des Projektils"),
                            AuraModifiers);

                    case StatId.GiveAttackSpeed:
                        return E(Stats.AuraAttackSpeed, AuraEffect,
                            AuraInteractions("The total is capped at +150 %.", "Die Summe ist auf +150 % begrenzt."),
                            T("Attack speed of a recipient = multiplier x (1 + total aura attack speed) / cooldown",
                              "Angriffstempo eines Empfängers = Multiplikator x (1 + Summe Aura-Angriffstempo) / Abklingzeit"),
                            AuraModifiers);

                    case StatId.GiveSlow:
                        return E(Stats.AuraSlow, AuraEffect,
                            AuraInteractions("The total is capped at 80 %.", "Die Summe ist auf 80 % begrenzt.") +
                            T(" Projectile hits of the recipients slow enemies for 2 seconds.", " Projektiltreffer der Empfänger verlangsamen Gegner für 2 Sekunden."),
                            T("Enemy speed after a hit = speed x (1 - total aura slow), for 2 seconds", "Gegnertempo nach einem Treffer = Tempo x (1 - Summe Aura-Verlangsamung), für 2 Sekunden"),
                            AuraModifiers);

                    case StatId.GiveRegen:
                        return E(Stats.AuraRegen, AuraEffect,
                            AuraInteractions("The total is capped at +150 %.", "Die Summe ist auf +150 % begrenzt."),
                            T("Regeneration of a recipient = base regeneration x (1 + total aura regen)", "Regeneration eines Empfängers = Basis-Regeneration x (1 + Summe Aura-Regeneration)"),
                            AuraModifiers);

                    case StatId.GiveOmnivamp:
                        return E(Stats.AuraOmnivamp, AuraEffect,
                            AuraInteractions("The total is capped at +150 %.", "Die Summe ist auf +150 % begrenzt.") +
                            T(" It applies to projectile attacks of the recipients.", " Er gilt für Projektil-Angriffe der Empfänger."),
                            T("Omnivamp of a recipient's projectile = omnivamp x (1 + total aura omnivamp)", "Omnivamp eines Projektils eines Empfängers = Omnivamp x (1 + Summe Aura-Omnivamp)"),
                            AuraModifiers);

                    default:
                        return E(id.ToString(), "", "", "", "");
                }
            }

            /// <summary>
            /// Zeile "Aktuell": rechnet mit den echten Werten. Leer, wenn es nichts Sinnvolles zu berechnen gibt.
            /// Armor und Magic Resist rufen dieselbe Funktion auf wie der Schaden im Spiel (DamageCalculator).
            /// </summary>
            public static string Live(StatId id, StatSubject subject, IReadOnlyList<InspectStat> all)
            {
                if (!TryFind(all, id, out InspectStat stat))
                    return "";

                switch (id)
                {
                    case StatId.Armor:
                    case StatId.MagicResist:
                    {
                        float multiplier = DamageCalculator.ResistMultiplier(stat.value, 0f, 0f);
                        return F("Value {0:0.#}: you take x{1:0.00} of the damage ({2:0}% {3}).",
                                 "Wert {0:0.#}: Du erleidest x{1:0.00} des Schadens ({2:0} % {3}).",
                                 stat.value, multiplier, Mathf.Abs(1f - multiplier) * 100f,
                                 multiplier <= 1f ? T("less", "weniger") : T("more", "mehr"));
                    }

                    case StatId.Crit:
                    case StatId.CritDamage:
                    {
                        if (!TryFind(all, StatId.Crit, out InspectStat chance) ||
                            !TryFind(all, StatId.CritDamage, out InspectStat damage))
                            return "";

                        float average = 1f + Mathf.Clamp01(chance.value / 100f) * (Mathf.Max(1f, damage.value / 100f) - 1f);
                        return F("Crit chance {0:0}%, crit damage {1:0}%: on average x{2:0.00} damage.",
                                 "Krit-Chance {0:0} %, Krit-Schaden {1:0} %: im Schnitt x{2:0.00} Schaden.",
                                 chance.value, damage.value, average);
                    }

                    case StatId.AttackSpeed:
                        if (stat.value <= 0f) return "";
                        return subject == StatSubject.Hero
                            ? F("Attack animations play x{0:0.00} as fast.", "Angriffsanimationen laufen x{0:0.00} so schnell.", stat.value)
                            : F("About one attack every {0:0.00} seconds.", "Etwa ein Angriff alle {0:0.00} Sekunden.", 1f / stat.value);

                    case StatId.Regen:
                        return F("About {0:0.#} health per minute.", "Etwa {0:0.#} Leben pro Minute.", stat.value * 60f);

                    case StatId.Evasion:
                        return F("About {0:0}% of all hits are avoided.", "Etwa {0:0} % aller Treffer werden vermieden.", stat.value);

                    case StatId.Stamina:
                    {
                        Hero hero = Hero.Instance;
                        Stamina stamina = hero != null ? hero.StaminaComponent : null;
                        if (stamina == null) return "";

                        return F("Regeneration {0:0.#}/s after {1:0.#} s without spending. Exhausted until {2:0}% ({3:0.#}).",
                                 "Aufladung {0:0.#}/s nach {1:0.#} s ohne Verbrauch. Erschöpft bis {2:0} % ({3:0.#}).",
                                 stamina.RegenPerSecond, stamina.RegenDelay,
                                 stamina.ExhaustionRecoverFraction * 100f, stamina.Max * stamina.ExhaustionRecoverFraction);
                    }

                    default:
                        return "";
                }
            }

            private static bool TryFind(IReadOnlyList<InspectStat> all, StatId id, out InspectStat stat)
            {
                if (all != null)
                {
                    for (int i = 0; i < all.Count; i++)
                    {
                        if (all[i].id != id) continue;

                        stat = all[i];
                        return true;
                    }
                }

                stat = default;
                return false;
            }
        }
    }
}