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
                oldValueText.text = FormatValue(oldValue, suffix, decimals);

            if (newValueText != null)
            {
                newValueText.text = FormatValue(newValue, suffix,decimals);
                newValueText.color = color;
                suffixNewValueText.text = suffixNewValueOnly;
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