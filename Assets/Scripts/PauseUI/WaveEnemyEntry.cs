using System;
using System.Globalization;
using ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense
{
    /// <summary>
    /// Eine Zeile im Wave-Browser: Icon, "5x Goblin", Gold, Info-Symbol.
    /// Ein Klick auf die ganze Zeile öffnet das Gegner-Panel.
    /// </summary>
    public class WaveEnemyEntry : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private Text txtLabel;
        [SerializeField] private Text txtGold;

        public void Setup(EnemyConfig config, int count, Action<EnemyConfig> onClick)
        {
            if (icon != null)
            {
                Sprite sprite = config.walkAnim != null && config.walkAnim.Length > 0 ? config.walkAnim[0] : null;
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }

            txtLabel.text = $"{count}x {config.name}";

            int gold = count * config.goldReward;
            txtGold.text = gold > 0 ? $"{gold} Gold" : "";

            Button button = GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke(config));
        }
    }
}