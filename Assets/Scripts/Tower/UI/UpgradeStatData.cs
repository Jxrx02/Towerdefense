using UnityEngine;

namespace TowerDefense
{
    public class UpgradeStatData
    {
        public string statName;
        public float oldValue;
        public float newValue;

        public Sprite icon;
        public Color color;

        public string suffix;
        public string suffixNewValueOnly;

        public int decimals;

        public UpgradeStatData(
            string statName,
            float oldValue,
            float newValue,
            Sprite icon,
            Color color,
            string suffixNewValueOnly,
            string suffix = "",
            int decimals = 0
        )
        {
            this.statName = statName;
            this.oldValue = oldValue;
            this.newValue = newValue;
            this.icon = icon;
            this.color = color;
            this.suffix = suffix;
            this.suffixNewValueOnly = suffixNewValueOnly;

            this.decimals = decimals;
        }
    }
}