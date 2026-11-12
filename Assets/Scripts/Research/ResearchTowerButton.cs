using System;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense.Research
{
    /// <summary>Eintrag in der Turm-Liste links: Icon, Name, Status (gesperrt / x von y Upgrades).</summary>
    public class ResearchTowerButton : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private Text txtName;
        [SerializeField] private Text txtStatus;
        [SerializeField] private GameObject selectedMarker;
        [SerializeField] private Button button;

        [SerializeField] private Color nameColor = new Color(0.96f, 0.92f, 0.82f);
        [SerializeField] private Color nameLockedColor = new Color(0.60f, 0.55f, 0.46f);
        [SerializeField] private Color statusColor = new Color(0.74f, 0.67f, 0.55f);
        [SerializeField] private Color affordableColor = new Color(1f, 0.78f, 0.29f);
        [SerializeField] private Color tooExpensiveColor = new Color(0.90f, 0.35f, 0.30f);

        public void Setup(Sprite sprite, string towerName, Action onClick)
        {
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            txtName.text = towerName;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke());

            SetSelected(false);
        }

        public void Refresh(bool unlocked, int researched, int total, int unlockCost, bool affordable)
        {
            icon.color = unlocked ? Color.white : new Color(0.55f, 0.55f, 0.55f, 1f);
            txtName.color = unlocked ? nameColor : nameLockedColor;

            if (unlocked)
            {
                txtStatus.text = total > 0 ? Texts.Research.UpgradesProgress(researched, total) : Texts.Common.Unlocked;
                txtStatus.color = researched >= total && total > 0 ? affordableColor : statusColor;
            }
            else
            {
                txtStatus.text = Texts.Research.TowerLocked(unlockCost);
                txtStatus.color = affordable ? affordableColor : tooExpensiveColor;
            }
        }

        public void SetSelected(bool selected)
        {
            if (selectedMarker != null)
                selectedMarker.SetActive(selected);
        }
    }
}