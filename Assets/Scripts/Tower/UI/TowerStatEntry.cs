using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense
{
    public class TowerStatEntry : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Image icon;
        [SerializeField] private Text valueText;


        // =========================================================
        // SETUP
        // =========================================================

        public void Setup(
            Sprite statIcon,
            float value,
            int decimals = 0,
            string suffix = "")
        {
            if (icon != null)
                icon.sprite = statIcon;

            if (valueText == null)
                return;

            string format;

            if (decimals > 0)
            {
                format = value.ToString($"F{decimals}");
            }
            else
            {
                format = value.ToString("0");
            }

            valueText.text = $"{format}{suffix}";
        }
    }
}