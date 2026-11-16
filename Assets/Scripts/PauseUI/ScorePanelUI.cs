using System.Collections.Generic;
using LevelSelector;
using LevelSelector.Definitions;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense
{
    /// <summary>
    /// Baut Score- und Multiplikator-Zeilen auf. Zwei Ansichten, eine Logik (AppendMultiplierRows, Highlight):
    ///
    ///   Rebuild(...)                    Pausemenü / Victory: Basis | Multiplikatoren mit laufendem Score | Gesamt
    ///   RebuildLoadoutMultipliers(...)  Loadout: Waffe | ein Multiplikator pro Perk/Mutator | Score-Multiplikator
    /// </summary>
    public class ScorePanelUI : MonoBehaviour
    {
        [SerializeField] private Transform container;
        [SerializeField] private ScoreRow rowPrefab;
        [SerializeField] private GameObject headerPrefab;    // Kind "Label" (Text)
        [SerializeField] private GameObject dividerPrefab;

        [Header("Farben")]
        [SerializeField] private Color textColor = new Color(0.96f, 0.92f, 0.82f);
        [SerializeField] private Color dimColor = new Color(0.74f, 0.67f, 0.55f);
        [SerializeField] private Color goldColor = new Color(1f, 0.78f, 0.29f);
        [SerializeField] private Color positiveColor = new Color(0.62f, 0.80f, 0.38f);   // Multiplikator > 1
        [SerializeField] private Color negativeColor = new Color(0.90f, 0.35f, 0.30f);   // Multiplikator < 1
        [Tooltip("Dauerhafte Hintergrundfarbe der Gesamtzeile im Loadout")]
        [SerializeField] private Color totalRowColor = new Color(1f, 0.78f, 0.29f, 0.12f);

        // Perk/Mutator-Definition -> zugehörige Zeile (für das Highlight)
        private readonly Dictionary<ScriptableObject, ScoreRow> _multiplierRows = new();
        private ScoreRow _totalRow;

        // ── Pausemenü / Victory ───────────────────────────────────

        /// <summary>Live-Werte (Pausemenü).</summary>
        public void Rebuild()
        {
            if (LevelManager.instance == null) return;
            Rebuild(LevelManager.instance.GetScoreBreakdown());
        }

        /// <summary>Eingefrorene Werte (Victory-Screen).</summary>
        public void Rebuild(LevelManager.ScoreBreakdown s)
        {
            if (!BeginRebuild()) return;

            // ── Basis ──
            Header("Base");
            Row($"Kills ({s.kills} x 10)", "", s.killScore.ToString(), dimColor, dimColor, textColor);
            Row($"Waves ({s.waves} x 100)", "", s.waveScore.ToString(), dimColor, dimColor, textColor);
            Row($"Gold ({s.gold} x 2)", "", s.goldScore.ToString(), dimColor, dimColor, textColor);
            Row($"Time bonus ({s.timeSeconds}s)", "", s.timeBonus.ToString(), dimColor, dimColor, textColor);
            Row("Base score", "", s.baseScore.ToString(), textColor, dimColor, textColor, bold: true);

            Divider();

            // ── Multiplikatoren ──
            Header("Multipliers");
            Row("", "Mult.", "Score", dimColor, dimColor, dimColor);

            AppendMultiplierRows(CurrentLoadout(), s.baseScore, compact: false);

            Divider();

            // ── Ergebnis ──
            Row("Total multiplier", $"x{s.multiplier:0.00}", "", textColor, MultiplierColor(s.multiplier), textColor, bold: true);
            Row("Total score", "", s.total.ToString(), goldColor, dimColor, goldColor, bold: true);
        }

        // ── Loadout ───────────────────────────────────────────────

        /// <summary>Waffe, Multiplikator je Perk/Mutator und die Gesamtzeile. Gleiche Zeilen und Farben wie im Pausemenü.</summary>
        public void RebuildLoadoutMultipliers(LevelLoadout loadout)
        {
            if (!BeginRebuild()) return;

            string weapon = loadout != null && loadout.weapon != null ? loadout.weapon.weaponName : "-";
            Row(Texts.Progression.Kind(UnlockKind.Weapon), "", weapon, dimColor, dimColor, textColor);

            float total = AppendMultiplierRows(loadout, null, compact: true);

            Divider();

            _totalRow = Row(Texts.Loadout.ScoreMultiplier, "", $"x{total:0.00}",
                goldColor, dimColor, MultiplierColor(total), bold: true);
            _totalRow.SetRestColor(totalRowColor);
        }

        /// <summary>Lässt die Gesamtzeile kurz aufleuchten (Auswahl im Loadout hat sich geändert).</summary>
        public void PulseTotal()
        {
            if (_totalRow != null)
                _totalRow.Pulse();
        }

        /// <summary>Hebt die Zeile dieses Perks/Mutators hervor. null oder unbekannt (z. B. Waffe) = nichts hervorheben.</summary>
        public void Highlight(ScriptableObject definition)
        {
            foreach (var pair in _multiplierRows)
                pair.Value.SetHighlight(definition != null && pair.Key == definition);
        }

        // ── Gemeinsame Logik ──────────────────────────────────────

        /// <summary>
        /// Eine Zeile je Perk und Mutator. baseScore != null: laufender Score nach jedem Faktor (Pausemenü).
        /// compact: ohne Zwischenüberschriften, Multiplikator rechts (Loadout). Gibt das Produkt aller Faktoren zurück.
        /// </summary>
        private float AppendMultiplierRows(LevelLoadout loadout, float? baseScore, bool compact)
        {
            float multiplier = 1f;
            float running = baseScore ?? 0f;
            bool any = false;

            if (!compact) Header("Perks", small: true);
            bool anyPerk = false;

            if (loadout != null)
            {
                foreach (PerkDefinition perk in loadout.perks)
                {
                    if (perk == null) continue;
                    anyPerk = any = true;
                    multiplier *= perk.ScoreMultiplier;
                    running *= perk.ScoreMultiplier;
                    _multiplierRows[perk] = MultiplierRow(perk.perkName, perk.ScoreMultiplier,
                        baseScore.HasValue ? running : (float?)null, compact);
                }
            }

            if (!compact && !anyPerk) Row("None", "", "", dimColor, dimColor, dimColor);

            if (!compact) Header("Mutators", small: true);
            bool anyMutator = false;

            if (loadout != null)
            {
                foreach (MutatorDefinition mutator in loadout.mutators)
                {
                    if (mutator == null) continue;
                    anyMutator = any = true;
                    multiplier *= mutator.ScoreMultiplier;
                    running *= mutator.ScoreMultiplier;
                    _multiplierRows[mutator] = MultiplierRow(mutator.mutatorName, mutator.ScoreMultiplier,
                        baseScore.HasValue ? running : (float?)null, compact);
                }
            }

            if (!compact && !anyMutator) Row("None", "", "", dimColor, dimColor, dimColor);
            if (compact && !any) Row(Texts.Loadout.NoMultipliers, "", "", dimColor, dimColor, dimColor);

            return multiplier;
        }

        private ScoreRow MultiplierRow(string label, float multiplier, float? runningScore, bool compact)
        {
            if (compact)
                return Row(label, "", $"x{multiplier:0.00}", textColor, dimColor, MultiplierColor(multiplier));

            return Row(label, $"x{multiplier:0.00}",
                runningScore.HasValue ? Mathf.RoundToInt(runningScore.Value).ToString() : "",
                textColor, MultiplierColor(multiplier), textColor);
        }

        private static LevelLoadout CurrentLoadout()
        {
            return LevelLoadoutManager.Instance != null ? LevelLoadoutManager.Instance.CurrentLoadout : null;
        }

        private bool BeginRebuild()
        {
            if (container == null || rowPrefab == null || headerPrefab == null || dividerPrefab == null)
            {
                Debug.LogError("ScorePanelUI: container/rowPrefab/headerPrefab/dividerPrefab fehlt. " +
                               "Builder erneut ausführen und prüfen, ob am Prefab 'ScoreRow' das Script ScoreRow hängt.", this);
                return false;
            }

            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Transform child = container.GetChild(i);
                child.SetParent(null);
                Destroy(child.gameObject);
            }

            _multiplierRows.Clear();
            _totalRow = null;
            return true;
        }

        // ── Bausteine ─────────────────────────────────────────────

        private Color MultiplierColor(float multiplier)
        {
            if (multiplier > 1.0001f) return positiveColor;
            if (multiplier < 0.9999f) return negativeColor;
            return dimColor;
        }

        private ScoreRow Row(string label, string middle, string value,
            Color labelColor, Color middleColor, Color valueColor, bool bold = false)
        {
            ScoreRow row = Instantiate(rowPrefab, container);
            row.Set(label, middle, value, labelColor, middleColor, valueColor, bold);
            return row;
        }

        private void Header(string text, bool small = false)
        {
            GameObject header = Instantiate(headerPrefab, container);
            Text label = header.transform.Find("Label").GetComponent<Text>();
            label.text = small ? text : text.ToUpperInvariant();
            label.color = small ? textColor : dimColor;
        }

        private void Divider() => Instantiate(dividerPrefab, container);
    }
}