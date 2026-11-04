using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense
{
    public sealed class TargetTypeDropdown : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Button toggleButton;
        [SerializeField] private Text currentTypeText;
        [SerializeField] private GameObject optionsRoot;
        [SerializeField] private Transform optionsContainer;
        [SerializeField] private Button optionButtonTemplate;
        [SerializeField, Min(1)] private int maxOptions = 12;

        [Header("Colors")]
        [SerializeField] private Color optionColor = new Color(0.15f, 0.17f, 0.22f, 1f);
        [SerializeField] private Color selectedColor = new Color(0.20f, 0.38f, 0.55f, 1f);
        [SerializeField] private Color selectedTextColor = Color.white;
        [SerializeField] private Color normalTextColor = new Color(0.78f, 0.81f, 0.88f);

        private readonly List<Button> optionButtons = new List<Button>();
        private Tower currentTower;
        private TargetType shownTargetType;
        private bool hasShownTargetType;

        private void Awake()
        {
            if (toggleButton != null)
                toggleButton.onClick.AddListener(ToggleOptions);

            if (optionButtonTemplate != null)
                optionButtonTemplate.gameObject.SetActive(false);

            SetExpanded(false);
        }

        private void Update()
        {
            Tower activeTower = InteractionPopup.instance != null
                ? InteractionPopup.instance.CurrentUpgradeTarget
                : null;

            if (activeTower != currentTower)
            {
                SetTower(activeTower);
                return;
            }

            RefreshSelection();
        }

        private void OnDisable()
        {
            SetExpanded(false);
        }

        private void SetTower(Tower tower)
        {
            if (currentTower == tower)
            {
                RefreshSelection();
                return;
            }

            currentTower = tower;
            hasShownTargetType = false;
            SetExpanded(false);

            if (currentTower == null)
            {
                if (currentTypeText != null)
                    currentTypeText.text = "Zieltyp auswählen";

                return;
            }

            BuildOptions();
            RefreshSelection();
        }

        private void BuildOptions()
        {
            ClearOptions();

            if (optionsContainer == null || optionButtonTemplate == null)
                return;

            Array targetTypes = Enum.GetValues(typeof(TargetType));
            int optionCount = Mathf.Min(targetTypes.Length, maxOptions);

            for (int i = 0; i < optionCount; i++)
            {
                TargetType targetType = (TargetType)targetTypes.GetValue(i);
                Button optionButton = Instantiate(optionButtonTemplate, optionsContainer, false);
                optionButton.gameObject.name = $"TargetType_{targetType}";
                optionButton.transition = Selectable.Transition.None;
                optionButton.gameObject.SetActive(true);

                Text optionText = optionButton.GetComponentInChildren<Text>(true);
                if (optionText != null)
                    optionText.text = DisplayName(targetType);

                optionButton.onClick.AddListener(() => SelectTargetType(targetType));
                optionButtons.Add(optionButton);
            }
        }

        private void ClearOptions()
        {
            for (int i = optionButtons.Count - 1; i >= 0; i--)
            {
                if (optionButtons[i] != null)
                    Destroy(optionButtons[i].gameObject);
            }

            optionButtons.Clear();
        }

        private void ToggleOptions()
        {
            if (currentTower == null)
                return;

            SetExpanded(optionsRoot == null || !optionsRoot.activeSelf);
        }

        private void SetExpanded(bool expanded)
        {
            if (optionsRoot != null)
                optionsRoot.SetActive(expanded && currentTower != null);
        }

        private void SelectTargetType(TargetType targetType)
        {
            if (currentTower == null)
                return;

            currentTower.SetTargetType(targetType);
            RefreshSelection(true);
            SetExpanded(false);
        }

        private void RefreshSelection(bool force = false)
        {
            if (currentTower == null)
                return;

            TargetType selectedType = currentTower.targetPreference;
            if (!force && hasShownTargetType && shownTargetType == selectedType)
                return;

            shownTargetType = selectedType;
            hasShownTargetType = true;

            if (currentTypeText != null)
                currentTypeText.text = $"Zieltyp: {DisplayName(selectedType)}  ▾";

            foreach (Button optionButton in optionButtons)
            {
                if (optionButton == null)
                    continue;

                bool selected = optionButton.name == $"TargetType_{selectedType}";
                Image image = optionButton.targetGraphic as Image;
                if (image != null)
                    image.color = selected ? selectedColor : optionColor;

                Text label = optionButton.GetComponentInChildren<Text>(true);
                if (label != null)
                    label.color = selected ? selectedTextColor : normalTextColor;
            }
        }

        private static string DisplayName(TargetType targetType)
        {
            switch (targetType)
            {
                case TargetType.First: return "Erstes Ziel";
                case TargetType.Last: return "Letztes Ziel";
                case TargetType.Strongest: return "Stärkstes Ziel";
                case TargetType.Nearest: return "Nächstes Ziel";
                default: return targetType.ToString();
            }
        }
    }
}
