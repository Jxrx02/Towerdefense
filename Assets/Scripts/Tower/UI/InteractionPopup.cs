using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense
{
    public class InteractionPopup : MonoBehaviour
    {
        public static InteractionPopup instance;

        [Header("Coins")]
        [SerializeField] private Transform coinContainer;
        [SerializeField] private CoinUI coinPrefab;

        [Header("Upgrade Info")]
        [SerializeField] private GameObject upgradeInfoPanel;
        [SerializeField] private Text upgradeTitle;
        [SerializeField] private Text upgradeDescription;
        [SerializeField] private Text upgradeCost;
        [Header("Upgrade Stats")]
        [SerializeField] private Transform upgradeStatsContainer;
        [SerializeField] private UpgradeStatEntry upgradeStatPrefab;
        
        [Header("Position")]
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.8f, 0f);
        
        [Header("Stat Icons")]
        [SerializeField] public Sprite damageIcon;
        [SerializeField] public Sprite rangeIcon;
        [SerializeField] public Sprite attackSpeedIcon;
        [SerializeField] public Sprite goldIcon;
        [SerializeField] public Sprite regenIcon;
        [SerializeField] public Sprite healthIcon;
        [SerializeField] public Sprite damageMultiplierIcon;
        [SerializeField] public Sprite rangeMultiplierIcon;
        [SerializeField] public Sprite attackSpeedMultiplierIcon;
        [SerializeField] public Sprite slowMultiplierIcon;

        private readonly List<CoinUI> coins = new List<CoinUI>();

        private RectTransform rectTransform;
        private Canvas canvas;

        private int requiredCoins;
        private int filledCoins;

        private void Awake()
        {
            if (instance == null)
                instance = this;
            else
            {
                Destroy(gameObject);
                return;
            }

            rectTransform = GetComponent<RectTransform>();
            canvas = GetComponentInParent<Canvas>();

            Hide();
        }

        // --------------------------------------------------
        // SHOW
        // --------------------------------------------------

        public void Show(
            Vector3 worldPosition,
            string interactiontext,
            string title,
            int goldCost,
            string description
        )
        {
            requiredCoins = Mathf.CeilToInt(goldCost / 10f);

            CreateCoins(requiredCoins);

            filledCoins = 0;

            SetCoinsProgress(0);

            // Upgrade Info
            if (upgradeInfoPanel != null)
                upgradeInfoPanel.SetActive(true);

            if (upgradeTitle != null)
                upgradeTitle.text = title;

            if (upgradeDescription != null)
                upgradeDescription.text = description;

            if (upgradeCost != null)
                upgradeCost.text = $"{goldCost} Gold";

            Position(worldPosition);

            gameObject.SetActive(true);
        }

        public void ShowUpgradeStats(List<UpgradeStatData> stats)
        {
            if (upgradeStatsContainer == null || upgradeStatPrefab == null)
                return;

            // Alte Einträge entfernen
            for (int i = upgradeStatsContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(upgradeStatsContainer.GetChild(i).gameObject);
            }

            if (stats == null)
                return;

            foreach (UpgradeStatData stat in stats)
            {
                if (stat == null)
                    continue;

                UpgradeStatEntry entry = Instantiate(
                    upgradeStatPrefab,
                    upgradeStatsContainer
                );

                entry.Setup(
                    stat.icon,
                    stat.oldValue,
                    stat.newValue,
                    stat.color,
                    stat.suffixNewValueOnly,
                    stat.suffix,
                    stat.decimals
                );
            }
        }
        // --------------------------------------------------
        // HIDE
        // --------------------------------------------------

        public void Hide()
        {
            gameObject.SetActive(false);

            if (upgradeInfoPanel != null)
                upgradeInfoPanel.SetActive(false);

            ClearUpgradeStats();

            ResetProgress();
        }

        private void ClearUpgradeStats()
        {
            if (upgradeStatsContainer == null)
                return;

            for (int i = upgradeStatsContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(upgradeStatsContainer.GetChild(i).gameObject);
            }
        }

        // --------------------------------------------------
        // COINS
        // --------------------------------------------------

        private void CreateCoins(int amount)
        {
            // Alte Münzen löschen
            for (int i = coinContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(coinContainer.GetChild(i).gameObject);
            }

            coins.Clear();

            // Neue Münzen erstellen
            for (int i = 0; i < amount; i++)
            {
                CoinUI coin = Instantiate(
                    coinPrefab,
                    coinContainer
                );

                coin.ResetCoin();

                coins.Add(coin);
            }
        }

        /// <summary>
        /// progress = 0..1
        /// Jede volle Sekunde entspricht einer vollständig
        /// gefüllten Münze.
        /// </summary>
        public void SetProgress(float progress)
        {
            if (requiredCoins <= 0)
                return;

            progress = Mathf.Clamp01(progress);

            int coinsToFill = Mathf.FloorToInt(
                progress * requiredCoins
            );

            SetCoinsProgress(coinsToFill);
        }

        private void SetCoinsProgress(int amount)
        {
            amount = Mathf.Clamp(
                amount,
                0,
                coins.Count
            );

            for (int i = 0; i < coins.Count; i++)
            {
                coins[i].SetFilled(i < amount);
            }

            filledCoins = amount;
        }

        public bool IsComplete()
        {
            return filledCoins >= requiredCoins;
        }

        public void ResetProgress()
        {
            filledCoins = 0;

            foreach (CoinUI coin in coins)
            {
                coin.ResetCoin();
            }
        }

        // --------------------------------------------------
        // POSITION
        // --------------------------------------------------

        private void Position(Vector3 worldPosition)
        {
            worldPosition += worldOffset;

            if (canvas == null)
                return;

            if (canvas.renderMode == RenderMode.WorldSpace)
            {
                rectTransform.position = worldPosition;
                return;
            }

            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;

            Vector2 screenPosition =
                RectTransformUtility.WorldToScreenPoint(
                    cam,
                    worldPosition
                );

            RectTransform canvasRect =
                canvas.transform as RectTransform;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPosition,
                cam,
                out Vector2 localPosition
            );

            rectTransform.anchoredPosition = localPosition;
        }
    }
}
