using System.Collections.Generic;
using LevelSelector;
using LevelSelector.Definitions;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense
{
    /// <summary>
    /// Baut die Score-Aufschlüsselung auf:
    ///   Basis  (Kills, Waves, Gold, Zeitbonus, Basis-Score)
    ///   ─────  (Trennlinie)
    ///   Multiplikatoren pro Perk und pro Mutator, mit laufendem Score nach jedem Faktor
    ///   ─────
    ///   Gesamt-Multiplikator und Gesamt-Score
    /// Die Werte stehen beim Pausieren fest (Time.time steht), deshalb reicht ein Aufbau beim Öffnen.
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

        // Perk/Mutator-Definition -> zugehörige Zeile (für das Highlight)
        private readonly Dictionary<ScriptableObject, ScoreRow> _multiplierRows = new();

        /// <summary>Live-Werte (Pausemenü).</summary>
        public void Rebuild()
        {
            if (LevelManager.instance == null) return;
            Rebuild(LevelManager.instance.GetScoreBreakdown());
        }

        /// <summary>Eingefrorene Werte (Victory-Screen).</summary>
        public void Rebuild(LevelManager.ScoreBreakdown s)
        {
            if (container == null || rowPrefab == null || headerPrefab == null || dividerPrefab == null)
            {
                Debug.LogError("ScorePanelUI: container/rowPrefab/headerPrefab/dividerPrefab fehlt. " +
                               "Pausemenü-Builder erneut ausführen und prüfen, ob am Prefab 'ScoreRow' das Script ScoreRow hängt.", this);
                return;
            }

            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Transform child = container.GetChild(i);
                child.SetParent(null);
                Destroy(child.gameObject);
            }

            _multiplierRows.Clear();

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

            LevelLoadout loadout = LevelLoadoutManager.Instance != null
                ? LevelLoadoutManager.Instance.CurrentLoadout
                : null;

            float running = s.baseScore;

            Header("Perks", small: true);
            bool any = false;
            if (loadout != null)
            {
                foreach (PerkDefinition perk in loadout.perks)
                {
                    if (perk == null) continue;
                    any = true;
                    running *= perk.ScoreMultiplier;
                    _multiplierRows[perk] = MultiplierRow(perk.perkName, perk.ScoreMultiplier, running);
                }
            }
            if (!any) Row("None", "", "", dimColor, dimColor, dimColor);

            Header("Mutators", small: true);
            any = false;
            if (loadout != null)
            {
                foreach (MutatorDefinition mutator in loadout.mutators)
                {
                    if (mutator == null) continue;
                    any = true;
                    running *= mutator.ScoreMultiplier;
                    _multiplierRows[mutator] = MultiplierRow(mutator.mutatorName, mutator.ScoreMultiplier, running);
                }
            }
            if (!any) Row("None", "", "", dimColor, dimColor, dimColor);

            Divider();

            // ── Ergebnis ──
            Row("Total multiplier", $"x{s.multiplier:0.00}", "", textColor, MultiplierColor(s.multiplier), textColor, bold: true);
            Row("Total score", "", s.total.ToString(), goldColor, dimColor, goldColor, bold: true);
        }

        /// <summary>Hebt die Zeile dieses Perks/Mutators hervor. null oder unbekannt (z. B. Waffe) = nichts hervorheben.</summary>
        public void Highlight(ScriptableObject definition)
        {
            foreach (var pair in _multiplierRows)
                pair.Value.SetHighlight(definition != null && pair.Key == definition);
        }

        // ── Bausteine ─────────────────────────────────────────────

        private ScoreRow MultiplierRow(string label, float multiplier, float runningScore)
        {
            return Row(label, $"x{multiplier:0.00}", Mathf.RoundToInt(runningScore).ToString(),
                textColor, MultiplierColor(multiplier), textColor);
        }

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