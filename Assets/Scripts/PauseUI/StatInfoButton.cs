using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense
{
    /// <summary>Der kleine "i"-Button. Einzige Implementierung: InteractionPopup, Gegner-Panel usw. nutzen sie.</summary>
    public static class StatInfoButton
    {
        public const float Size = 20f;

        public static Button Create(Transform parent, Font font, Action onClick)
        {
            if (font == null) return null;

            GameObject go = new GameObject("Info", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            Image background = go.AddComponent<Image>();
            background.color = new Color(0.17f, 0.13f, 0.10f, 1f);

            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.62f, 0.46f, 0.27f, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);

            LayoutElement element = go.AddComponent<LayoutElement>();
            element.minWidth = element.preferredWidth = Size;
            element.minHeight = element.preferredHeight = Size;
            element.flexibleWidth = 0f;

            GameObject labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(go.transform, false);
            RectTransform labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;

            Text label = labelObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = 13;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = StatPalette.Neutral;
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

            if (onClick != null)
                button.onClick.AddListener(() => onClick());

            return button;
        }

        /// <summary>Leerer Platzhalter in Button-Größe, damit Werte in Zeilen ohne Info-Button bündig bleiben.</summary>
        public static void CreateSpacer(Transform parent)
        {
            GameObject go = new GameObject("InfoSpacer", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            LayoutElement element = go.AddComponent<LayoutElement>();
            element.minWidth = element.preferredWidth = Size;
            element.minHeight = element.preferredHeight = Size;
            element.flexibleWidth = 0f;
        }
    }

    /// <summary>
    /// Kompakte Chips ("i  Damage") unter einem Beschreibungstext, ein Chip pro erwähntem Stat.
    /// Klick öffnet dieselbe Erklärung wie der Info-Button im InteractionPopup (StatInfoPanel).
    /// Zwei Spalten mit fester Zellgröße: braucht keine Layout-Tricks und wächst nur bei Bedarf.
    /// </summary>
    public class StatChipBar : MonoBehaviour
    {
        private const float ChipWidth = 196f;
        private const float ChipHeight = 26f;

        private Font _font;
        private readonly List<GameObject> _chips = new List<GameObject>();
        private readonly HashSet<string> _shownNames = new HashSet<string>();

        public static StatChipBar Create(Transform parent, Font font, int siblingIndex = -1)
        {
            GameObject go = new GameObject("StatChips", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            if (siblingIndex >= 0)
                go.transform.SetSiblingIndex(siblingIndex);

            GridLayoutGroup grid = go.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(ChipWidth, ChipHeight);
            grid.spacing = new Vector2(4f, 4f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.UpperLeft;

            StatChipBar bar = go.AddComponent<StatChipBar>();
            bar._font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            go.SetActive(false);
            return bar;
        }

        public void Hide()
        {
            Clear();
            gameObject.SetActive(false);
        }

        public void Show(IList<StatId> ids, StatSubject subject, IReadOnlyList<InspectStat> source = null)
        {
            Clear();

            if (ids != null)
            {
                foreach (StatId id in ids)
                {
                    // Gleicher Anzeigename (z. B. Magic pen fest/prozentual) nur einmal
                    if (_shownNames.Add(Texts.Stats.Of(id)))
                        AddChip(id, subject, source);
                }
            }

            gameObject.SetActive(_chips.Count > 0);

            if (transform.parent is RectTransform parent)
                LayoutRebuilder.MarkLayoutForRebuild(parent);
        }

        private void Clear()
        {
            _shownNames.Clear();

            foreach (GameObject chip in _chips)
            {
                if (chip == null) continue;
                chip.transform.SetParent(null);   // zählt sofort nicht mehr im Layout
                Destroy(chip);
            }

            _chips.Clear();
        }

        private void AddChip(StatId id, StatSubject subject, IReadOnlyList<InspectStat> source)
        {
            GameObject chip = new GameObject("Chip_" + id, typeof(RectTransform));
            chip.transform.SetParent(transform, false);

            Image background = chip.AddComponent<Image>();
            background.color = new Color(0.17f, 0.13f, 0.10f, 1f);

            Outline outline = chip.AddComponent<Outline>();
            outline.effectColor = new Color(0.62f, 0.46f, 0.27f, 0.7f);
            outline.effectDistance = new Vector2(1f, -1f);

            HorizontalLayoutGroup row = chip.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(4, 6, 3, 3);
            row.spacing = 6f;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;

            // "i"-Kästchen (nur Optik, der Klick liegt auf dem ganzen Chip)
            GameObject glyph = new GameObject("Glyph", typeof(RectTransform));
            glyph.transform.SetParent(chip.transform, false);
            Image glyphBack = glyph.AddComponent<Image>();
            glyphBack.color = new Color(0.27f, 0.20f, 0.14f, 1f);
            glyphBack.raycastTarget = false;
            LayoutElement glyphSize = glyph.AddComponent<LayoutElement>();
            glyphSize.minWidth = glyphSize.preferredWidth = 18f;
            glyphSize.minHeight = glyphSize.preferredHeight = 18f;
            glyphSize.flexibleWidth = 0f;

            Text i = NewText("i", glyph.transform, 12, FontStyle.Bold, TextAnchor.MiddleCenter, StatPalette.Neutral, "i");
            Stretch(i.rectTransform);

            Text label = NewText("Label", chip.transform, 14, FontStyle.Bold, TextAnchor.MiddleLeft,
                StatPalette.ColorOf(id), Texts.Stats.Of(id));
            label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            Button button = chip.AddComponent<Button>();
            button.targetGraphic = background;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;

            Font font = _font;
            button.onClick.AddListener(() => StatInfoPanel.Show(id, source, subject, font));

            _chips.Add(chip);
        }

        private Text NewText(string name, Transform parent, int size, FontStyle style, TextAnchor anchor,
            Color color, string value)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            Text text = go.AddComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = color;
            text.text = value;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}