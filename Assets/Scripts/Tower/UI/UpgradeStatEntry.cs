using System;
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

        [SerializeField] private Graphic arrow;   

        // Info-Button (nur in der Inspektion): wird bei Bedarf selbst erzeugt, das Prefab bleibt unverändert
        private Button infoButton;

        public bool HasInfo => infoButton != null && infoButton.gameObject.activeSelf;

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

        // ------------------------------------------------------------------
        // Info-Button
        // ------------------------------------------------------------------

        /// <summary>Hängt rechts in der Zeile einen kleinen "i"-Button an, der onClick auslöst.</summary>
        public void SetInfo(Action onClick)
        {
            if (infoButton == null)
                infoButton = StatInfoButton.Create(transform, newValueText != null ? newValueText.font : null, null);

            if (infoButton == null) return;

            infoButton.onClick.RemoveAllListeners();
            infoButton.onClick.AddListener(() => onClick?.Invoke());
            infoButton.gameObject.SetActive(true);
        }

        public void HideInfo()
        {
            if (infoButton != null)
                infoButton.gameObject.SetActive(false);
        }

        private Button CreateInfoButton()
        {
            // Schrift der Zeile übernehmen (Pixel-Font des Popups)
            Font font = newValueText != null ? newValueText.font : null;
            if (font == null) return null;

            GameObject go = new GameObject("Info", typeof(RectTransform));
            go.transform.SetParent(transform, false);

            Image background = go.AddComponent<Image>();
            background.color = new Color(0.17f, 0.13f, 0.10f, 1f);

            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.62f, 0.46f, 0.27f, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);

            LayoutElement element = go.AddComponent<LayoutElement>();
            element.minWidth = 20f;
            element.preferredWidth = 20f;
            element.minHeight = 20f;
            element.preferredHeight = 20f;
            element.flexibleWidth = 0f;

            GameObject labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(go.transform, false);

            RectTransform labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            Text label = labelObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = 13;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.96f, 0.92f, 0.82f);
            label.text = "i";
            label.raycastTarget = false;

            Button button = go.AddComponent<Button>();
            button.targetGraphic = background;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.4f, 1.4f, 1.4f, 1f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;

            return button;
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