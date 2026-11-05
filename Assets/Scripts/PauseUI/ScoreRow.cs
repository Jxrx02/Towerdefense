using System.Collections.Generic;
using LevelSelector;
using LevelSelector.Definitions;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense
{
    /// <summary>Eine Zeile der Score-Aufschlüsselung: Label | Mitte (z. B. "x0.90") | Wert.</summary>
    public class ScoreRow : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Text txtLabel;
        [SerializeField] private Text txtMiddle;
        [SerializeField] private Text txtValue;
        [SerializeField] private Color highlightColor = new Color(0.82f, 0.55f, 0.20f, 0.35f);

        public void Set(string label, string middle, string value,
            Color labelColor, Color middleColor, Color valueColor, bool bold = false)
        {
            txtLabel.text = label;
            txtMiddle.text = middle;
            txtValue.text = value;

            txtLabel.color = labelColor;
            txtMiddle.color = middleColor;
            txtValue.color = valueColor;

            FontStyle style = bold ? FontStyle.Bold : FontStyle.Normal;
            txtLabel.fontStyle = style;
            txtMiddle.fontStyle = FontStyle.Bold;
            txtValue.fontStyle = style;

            SetHighlight(false);
        }

        public void SetHighlight(bool on)
        {
            if (background != null)
                background.color = on ? highlightColor : new Color(0f, 0f, 0f, 0f);
        }
    }
}