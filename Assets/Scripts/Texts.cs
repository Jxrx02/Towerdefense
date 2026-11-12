using System;
using System.Globalization;
using UnityEngine;

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
    public static class Texts
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
