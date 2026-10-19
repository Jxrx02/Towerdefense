using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense
{
    public class UpgradeStatEntry : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Image statIcon;
        [SerializeField] private Text oldValueText;
        [SerializeField] private Text newValueText;
        [Tooltip("Steht hinter dem neuen Wert. Zeigt den Stat-Namen, wenn kein Icon zugewiesen ist.")]
        [SerializeField] private Text suffixNewValueText;

        [Header("Optional")]
        [SerializeField] private Image arrow;

        public void Setup(
            Sprite icon,
            float oldValue,
            float newValue,
            Color color,
            string suffixNewValueOnly,
            string suffix = "",
            int decimals = 0
        )
        {
            if (statIcon != null)
            {
                statIcon.sprite = icon;
                statIcon.enabled = icon != null;
            }

            if (oldValueText != null)
            {
                oldValueText.gameObject.SetActive(true);
                oldValueText.text = FormatValue(oldValue, suffix, decimals);
            }

            if (arrow != null)
                arrow.gameObject.SetActive(true);

            if (newValueText != null)
            {
                newValueText.text = FormatValue(newValue, suffix, decimals);
                newValueText.color = color;
            }

            if (suffixNewValueText != null)
            {
                suffixNewValueText.text = suffixNewValueOnly;
                suffixNewValueText.color = color;
            }

            gameObject.SetActive(true);
        }

        /// <summary>
        /// Zeigt nur einen aktuellen Wert (ohne alten Wert und Pfeil), z. B. im Inspektions-Popup.
        /// 'label' steht hinter dem Wert (Stat-Name, falls kein Icon da ist, oder z. B. " / 110 HP").
        /// </summary>
        public void SetupSingle(
            Sprite icon,
            float value,
            Color color,
            string label,
            string suffix = "",
            int decimals = 0
        )
        {
            if (statIcon != null)
            {
                statIcon.sprite = icon;
                statIcon.enabled = icon != null;
            }

            if (oldValueText != null)
                oldValueText.gameObject.SetActive(false);

            if (arrow != null)
                arrow.gameObject.SetActive(false);

            if (newValueText != null)
            {
                newValueText.text = FormatValue(value, suffix, decimals);
                newValueText.color = color;
            }

            if (suffixNewValueText != null)
            {
                suffixNewValueText.text = label;
                suffixNewValueText.color = color;
            }

            gameObject.SetActive(true);
        }

        private string FormatValue(
            float value,
            string suffix,
            int decimals
        )
        {
            string format = decimals <= 0
                ? "F0"
                : "F" + decimals;

            return value.ToString(format) + suffix;
        }
    }
}