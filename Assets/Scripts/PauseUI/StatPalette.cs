using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// Einzige Quelle für die Farbe eines Stats. Inspektion (InteractionPopup), Beschreibungen,
    /// Info-Chips, Gegner-Panel und Info-Fenster lesen alle von hier.
    /// </summary>
    public static class StatPalette
    {
        public static readonly Color Neutral = new Color(0.96f, 0.92f, 0.82f);

        public static Color ColorOf(StatId id)
        {
            switch (id)
            {
                case StatId.Health: return new Color(0.30f, 0.85f, 0.35f);
                case StatId.Shield: return Color.white;
                case StatId.Block: return new Color(0.75f, 0.45f, 1.00f);
                case StatId.AntiHeal: return new Color(0.75f, 0.20f, 0.30f);
                case StatId.Armor: return new Color(0.95f, 0.78f, 0.35f);
                case StatId.MagicResist: return new Color(0.55f, 0.68f, 0.95f);

                case StatId.Damage:
                case StatId.GiveDamage: return new Color(1.00f, 0.32f, 0.28f);

                case StatId.AttackSpeed:
                case StatId.GiveAttackSpeed: return new Color(1.00f, 0.35f, 0.60f);

                case StatId.Range:
                case StatId.GiveRange: return new Color(0.20f, 0.80f, 1.00f);

                case StatId.Crit:
                case StatId.CritDamage: return new Color(1.00f, 0.58f, 0.22f);

                case StatId.Lethality:
                case StatId.ArmorPen: return new Color(0.90f, 0.35f, 0.30f);

                case StatId.MagicPen:
                case StatId.MagicPenPercent: return new Color(0.72f, 0.45f, 0.95f);

                case StatId.Omnivamp:
                case StatId.GiveOmnivamp: return new Color(0.90f, 0.30f, 0.55f);

                case StatId.Regen:
                case StatId.GiveRegen: return new Color(0.30f, 1.00f, 0.30f);

                case StatId.GiveSlow: return new Color(0.60f, 0.40f, 1.00f);
                case StatId.Gold: return new Color(1.00f, 0.84f, 0.00f);
                case StatId.MoveSpeed: return new Color(0.90f, 0.90f, 0.50f);
                case StatId.XpDrop: return new Color(0.62f, 0.80f, 0.38f);
                case StatId.Stamina: return new Color(1.00f, 0.82f, 0.25f);
                default: return Neutral;
            }
        }

        public static string Hex(StatId id) => "#" + ColorUtility.ToHtmlStringRGB(ColorOf(id));
    }

    /// <summary>
    /// Textaufbereitung für Beschreibungen: Absatzabstand und Einfärben erwähnter Stats.
    /// Erkannt werden die Anzeigenamen aus Texts.Stats (beide Sprachen) plus gängige Kurzformen aus den Assets
    /// ("dmg", "attackspeed", ...). Vorhandene Rich-Text-Tags bleiben unberührt.
    /// </summary>
    public static class StatMarkup
    {
        private static readonly (string text, StatId id)[] Aliases =
        {
            ("damage", StatId.Damage), ("dmg", StatId.Damage), ("schaden", StatId.Damage),
            ("attack speed", StatId.AttackSpeed), ("attackspeed", StatId.AttackSpeed),
            ("atk speed", StatId.AttackSpeed), ("angriffstempo", StatId.AttackSpeed),
            ("range", StatId.Range), ("reichweite", StatId.Range),
            ("health", StatId.Health), ("hp", StatId.Health), ("leben", StatId.Health),
            ("armor", StatId.Armor), ("rüstung", StatId.Armor),
            ("magic resist", StatId.MagicResist), ("magic resistance", StatId.MagicResist),
            ("magieresistenz", StatId.MagicResist),
            ("crit chance", StatId.Crit), ("crit", StatId.Crit), ("krit-chance", StatId.Crit),
            ("crit damage", StatId.CritDamage), ("krit-schaden", StatId.CritDamage),
            ("lethality", StatId.Lethality), ("letalität", StatId.Lethality),
            ("armor pen", StatId.ArmorPen), ("armor penetration", StatId.ArmorPen),
            ("rüstungsdurchdringung", StatId.ArmorPen),
            ("magic pen", StatId.MagicPen), ("magic penetration", StatId.MagicPen),
            ("omnivamp", StatId.Omnivamp),
            ("regen", StatId.Regen), ("regeneration", StatId.Regen),
            ("move speed", StatId.MoveSpeed), ("movement speed", StatId.MoveSpeed), ("lauftempo", StatId.MoveSpeed),
            ("evasion", StatId.Evasion),
            ("stamina", StatId.Stamina), ("ausdauer", StatId.Stamina),
            ("shield", StatId.Shield), ("schild", StatId.Shield),
            ("block", StatId.Block),
            ("anti-heal", StatId.AntiHeal),
            ("gold", StatId.Gold)
        };

        private static Regex _regex;
        private static Dictionary<string, StatId> _lookup;
        private static GameLanguage _builtFor;
        private static bool _built;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _regex = null;
            _lookup = null;
            _built = false;
        }

        private static void EnsureBuilt()
        {
            if (_built && _builtFor == Texts.Language)
                return;

            var lookup = new Dictionary<string, StatId>();

            void Add(string text, StatId id)
            {
                text = (text ?? "").Trim().TrimEnd('.').ToLowerInvariant();
                if (text.Length < 2 || lookup.ContainsKey(text)) return;
                lookup.Add(text, id);
            }

            foreach ((string text, StatId id) in Aliases)
                Add(text, id);

            foreach (StatId id in Enum.GetValues(typeof(StatId)))
            {
                if (id == StatId.Level) continue;   // "Level" kommt in normalem Text zu oft vor
                Add(Texts.Stats.Of(id), id);
            }

            // Längste Begriffe zuerst: "magic resist" gewinnt gegen "magic"
            var keys = new List<string>(lookup.Keys);
            keys.Sort((a, b) => b.Length.CompareTo(a.Length));
            string alternatives = string.Join("|", keys.ConvertAll(Regex.Escape));

            // 1. Alternative: Rich-Text-Tags überspringen. 2. Alternative: Stat-Begriff (optional im Plural).
            const string word = @"[\p{L}\p{N}]";
            _regex = new Regex(
                $@"<\/?[a-zA-Z][^<>]*>|(?<!{word})(?<kw>{alternatives})s?(?!{word})",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            _lookup = lookup;
            _builtFor = Texts.Language;
            _built = true;
        }

        /// <param name="spacedParagraphs">Jeder Zeilenumbruch wird zu einem Absatz (eine Leerzeile dazwischen).</param>
        public static string Format(string text, bool spacedParagraphs = true, bool colorize = true)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            string result = text.Replace("\r\n", "\n").Trim();

            result = spacedParagraphs
                ? Regex.Replace(result, @"\n+", "\n\n")
                : Regex.Replace(result, @"\n{3,}", "\n\n");

            return colorize ? Colorize(result) : result;
        }

        public static string Colorize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            EnsureBuilt();

            return _regex.Replace(text, match =>
            {
                Group keyword = match.Groups["kw"];

                if (!keyword.Success || !_lookup.TryGetValue(keyword.Value.ToLowerInvariant(), out StatId id))
                    return match.Value;

                return "<color=" + StatPalette.Hex(id) + ">" + match.Value + "</color>";
            });
        }

        /// <summary>Fügt alle in 'text' erwähnten Stats (ohne Duplikate) zu 'into' hinzu.</summary>
        public static void FindStats(string text, List<StatId> into)
        {
            if (string.IsNullOrEmpty(text) || into == null)
                return;

            EnsureBuilt();

            foreach (Match match in _regex.Matches(text))
            {
                Group keyword = match.Groups["kw"];

                if (keyword.Success &&
                    _lookup.TryGetValue(keyword.Value.ToLowerInvariant(), out StatId id) &&
                    !into.Contains(id))
                    into.Add(id);
            }
        }
    }
}