using System;
using TowerDefense;
using UnityEngine;
using UnityEngine.UI;

namespace LevelSelector
{
    /// <summary>
    /// Ein freischaltbarer Inhalt (Perk, Mutator, Waffe) auf der Levelachse.
    /// Zeigt Icon, Name und Zustand: gesperrt / freigeschaltet / kaufbar (Preis) / im Besitz.
    /// Der Zustand wird immer aus PlayerProgress gelesen, die Ansicht hält keine eigenen Daten.
    /// </summary>
    public class ProgressionItemView : MonoBehaviour
    {
        [SerializeField] private Image border;
        [SerializeField] private Image iconBackground;
        [SerializeField] private Image icon;
        [SerializeField] private GameObject lockOverlay;
        [SerializeField] private GameObject selectedFrame;
        [SerializeField] private Text txtName;
        [SerializeField] private Text txtState;
        [SerializeField] private Button button;

        [Header("Farben je Art (wie im Loadout)")]
        [SerializeField] private Color perkColor = new Color(0.25f, 0.38f, 0.55f, 1f);
        [SerializeField] private Color mutatorColor = new Color(0.40f, 0.25f, 0.50f, 1f);
        [SerializeField] private Color weaponColor = new Color(0.55f, 0.25f, 0.20f, 1f);

        [Header("Farben je Zustand")]
        [SerializeField] private Color lockedBorder = new Color(0.30f, 0.24f, 0.17f, 1f);
        [SerializeField] private Color unlockedBorder = new Color(0.62f, 0.46f, 0.27f, 1f);
        [SerializeField] private Color purchasableBorder = new Color(1f, 0.78f, 0.29f, 1f);
        [SerializeField] private Color lockedText = new Color(0.55f, 0.50f, 0.42f, 1f);
        [SerializeField] private Color unlockedText = new Color(0.62f, 0.80f, 0.38f, 1f);
        [SerializeField] private Color purchasableText = new Color(1f, 0.78f, 0.29f, 1f);

        public ProgressionUnlock Unlock { get; private set; }

        public void Setup(ProgressionUnlock unlock, Action<ProgressionItemView> onClick)
        {
            Unlock = unlock;

            iconBackground.color = ColorOf(unlock.kind);
            icon.sprite = unlock.icon;
            icon.enabled = unlock.icon != null;
            txtName.text = unlock.name;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke(this));

            SetSelected(false);
            Refresh();
        }

        /// <summary>Liest den aktuellen Zustand aus PlayerProgress.</summary>
        public void Refresh()
        {
            UnlockState state = PlayerProgress.GetState(Unlock);
            bool locked = state == UnlockState.Locked;

            lockOverlay.SetActive(locked);
            icon.color = locked ? new Color(0.6f, 0.6f, 0.6f, 1f) : Color.white;

            txtState.text = Texts.Progression.State(state, Unlock.purchaseCost);

            switch (state)
            {
                case UnlockState.Locked:
                    border.color = lockedBorder;
                    txtState.color = lockedText;
                    break;

                case UnlockState.Purchasable:
                    border.color = purchasableBorder;
                    txtState.color = purchasableText;
                    break;

                default:
                    border.color = unlockedBorder;
                    txtState.color = unlockedText;
                    break;
            }
        }

        public void SetSelected(bool selected)
        {
            if (selectedFrame != null)
                selectedFrame.SetActive(selected);
        }

        private Color ColorOf(UnlockKind kind)
        {
            switch (kind)
            {
                case UnlockKind.Weapon: return weaponColor;
                case UnlockKind.Mutator: return mutatorColor;
                default: return perkColor;
            }
        }
    }
}