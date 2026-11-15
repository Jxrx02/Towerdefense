using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense
{
    /// <summary>
    /// Erklärung zu einem Stat der Inspektion (Info-Button neben dem Wert).
    ///
    ///   Was es bewirkt | Wie es andere Werte beeinflusst | Formel | Was diesen Wert verändert | Aktuell (Live-Rechnung)
    ///
    /// Die Texte stehen in Texts.StatInfo und stammen aus dem tatsächlichen Code. Die Live-Zeile wird alle 0,25 s
    /// aus der Werteliste der Inspektion neu berechnet, solange das Fenster offen ist.
    /// Das Fenster baut sich beim ersten Aufruf selbst auf (Overlay-Canvas, Sorting Order 450, unter dem Pausemenü).
    /// Schließen: X, Klick daneben oder Esc (Esc öffnet dabei nicht zusätzlich das Pausemenü, siehe PauseHotkey).
    /// </summary>
    public class StatInfoPanel : MonoBehaviour
    {
        private const int SortingOrder = 450;
        private const float RefreshInterval = 0.25f;

        private static readonly Color DimBack = new Color(0.04f, 0.03f, 0.02f, 0.70f);
        private static readonly Color Border = new Color(0.62f, 0.46f, 0.27f, 0.90f);
        private static readonly Color Panel = new Color(0.12f, 0.09f, 0.07f, 0.98f);
        private static readonly Color TextColor = new Color(0.96f, 0.92f, 0.82f);
        private static readonly Color Dim = new Color(0.74f, 0.67f, 0.55f);
        private static readonly Color Gold = new Color(1f, 0.78f, 0.29f);
        private static readonly Color ButtonColor = new Color(0.27f, 0.20f, 0.14f, 1f);

        private static StatInfoPanel _instance;

        /// <summary>Das Fenster ist offen. PauseHotkey öffnet dann das Pausemenü nicht.</summary>
        public static bool IsOpen => _instance != null && _instance.gameObject.activeSelf;

        /// <summary>Frame, in dem das Fenster geschlossen wurde (verhindert, dass dasselbe Esc das Pausemenü öffnet).</summary>
        public static int LastClosedFrame { get; private set; } = -1;

        private Font _font;
        private Text _title;
        private Text _liveHeader;
        private Text _live;
        private Text _closeLabel;
        private readonly Text[] _headers = new Text[4];
        private readonly Text[] _bodies = new Text[4];

        private StatId _id;
        private StatSubject _subject;
        private IReadOnlyList<InspectStat> _source;
        private float _timer;

        // ── Öffentlich ────────────────────────────────────────────

        /// <param name="source">Aktuelle Werteliste der Inspektion (für die Live-Zeile).</param>
        /// <param name="font">Schrift der aufrufenden UI (Pixel-Font). Null = Standardschrift.</param>
        public static void Show(StatId id, IReadOnlyList<InspectStat> source, StatSubject subject, Font font)
        {
            if (_instance == null)
                _instance = Build(font);

            _instance.Open(id, source, subject);
        }

        public static void Hide()
        {
            if (IsOpen)
                _instance.Close();
        }

        // ── Anzeige ───────────────────────────────────────────────

        private void Open(StatId id, IReadOnlyList<InspectStat> source, StatSubject subject)
        {
            _id = id;
            _source = source;
            _subject = subject;
            _timer = 0f;

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            ApplyTexts();
            RefreshLive();
        }

        private void Close()
        {
            LastClosedFrame = Time.frameCount;
            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            Texts.Changed += ApplyTexts;
        }

        private void OnDisable()
        {
            Texts.Changed -= ApplyTexts;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }

            _timer += Time.unscaledDeltaTime;
            if (_timer >= RefreshInterval)
            {
                _timer = 0f;
                RefreshLive();
            }
        }

        private void ApplyTexts()
        {
            if (_title == null) return;

            Texts.StatInfo.Entry entry = Texts.StatInfo.Get(_id, _subject);

            _title.text = Texts.Stats.Of(_id);
            _closeLabel.text = Texts.StatInfo.Close;

            SetSection(0, Texts.StatInfo.SectionEffect, entry.effect);
            SetSection(1, Texts.StatInfo.SectionInteractions, entry.interactions);
            SetSection(2, Texts.StatInfo.SectionFormula, entry.formula);
            SetSection(3, Texts.StatInfo.SectionModifiers, entry.modifiers);

            _liveHeader.text = Texts.StatInfo.SectionLive.ToUpperInvariant();
        }

        private void SetSection(int index, string header, string body)
        {
            bool show = !string.IsNullOrEmpty(body);

            _headers[index].gameObject.SetActive(show);
            _bodies[index].gameObject.SetActive(show);

            _headers[index].text = header.ToUpperInvariant();
            _bodies[index].text = body;
        }

        private void RefreshLive()
        {
            if (_live == null) return;

            string text = Texts.StatInfo.Live(_id, _subject, _source);
            bool show = !string.IsNullOrEmpty(text);

            _liveHeader.gameObject.SetActive(show);
            _live.gameObject.SetActive(show);
            _live.text = text;
        }

        // ── Aufbau (zur Laufzeit, kein Prefab nötig) ──────────────

        private static StatInfoPanel Build(Font font)
        {
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject rootObject = new GameObject("StatInfoPanel", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));

            Canvas canvas = rootObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            CanvasScaler scaler = rootObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            StatInfoPanel panel = rootObject.AddComponent<StatInfoPanel>();
            panel._font = font;

            // Abdunklung: Klick darauf schließt das Fenster
            Image dim = NewImage("Dim", rootObject.transform, DimBack, true);
            Stretch(dim.rectTransform);
            Button dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.navigation = new Navigation { mode = Navigation.Mode.None };
            dimButton.onClick.AddListener(panel.Close);

            // Karte (fängt Klicks ab, damit sie die Abdunklung nicht erreichen)
            Image border = NewImage("Card", rootObject.transform, Border, true);
            RectTransform cardRect = border.rectTransform;
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(820f, 700f);
            AddVertical(border.gameObject, new RectOffset(2, 2, 2, 2), 0f);

            Image inner = NewImage("Content", border.transform, Panel, true);
            AddVertical(inner.gameObject, new RectOffset(24, 24, 16, 18), 8f);
            inner.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;

            // Kopf: Titel + Schließen
            RectTransform head = NewRect("Head", inner.transform);
            HorizontalLayoutGroup headLayout = head.gameObject.AddComponent<HorizontalLayoutGroup>();
            headLayout.spacing = 12f;
            headLayout.childAlignment = TextAnchor.MiddleLeft;
            headLayout.childControlWidth = true;
            headLayout.childControlHeight = true;
            headLayout.childForceExpandWidth = false;
            headLayout.childForceExpandHeight = false;
            head.gameObject.AddComponent<LayoutElement>().minHeight = 44f;

            panel._title = NewText("Title", head, font, 30, Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            panel._title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            Button close = NewButton("Close", head, ButtonColor);
            LayoutElement closeElement = close.gameObject.AddComponent<LayoutElement>();
            closeElement.minWidth = 130f;
            closeElement.minHeight = 38f;
            panel._closeLabel = NewText("Label", close.transform, font, 17, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(panel._closeLabel.rectTransform);
            close.onClick.AddListener(panel.Close);

            Image accent = NewImage("Accent", inner.transform, Border, false);
            accent.gameObject.AddComponent<LayoutElement>().minHeight = 2f;
            accent.GetComponent<LayoutElement>().preferredHeight = 2f;

            // Live-Zeile (Rechnung mit den aktuellen Werten)
            panel._liveHeader = NewText("LiveHeader", inner.transform, font, 15, Dim, TextAnchor.MiddleLeft, FontStyle.Bold);
            panel._live = NewText("Live", inner.transform, font, 18, TextColor, TextAnchor.UpperLeft, FontStyle.Bold, true);

            // Erklärung (scrollbar)
            Image scrollBack = NewImage("Scroll", inner.transform, new Color(0f, 0f, 0f, 0.18f), true);
            scrollBack.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            scrollBack.GetComponent<LayoutElement>().minHeight = 200f;

            RectTransform viewport = NewRect("Viewport", scrollBack.transform);
            Stretch(viewport);
            viewport.offsetMin = new Vector2(12f, 8f);
            viewport.offsetMax = new Vector2(-12f, -8f);
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            AddVertical(content.gameObject, new RectOffset(0, 0, 0, 0), 4f);
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = scrollBack.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            for (int i = 0; i < 4; i++)
            {
                panel._headers[i] = NewText("Header" + i, content, font, 15, Dim, TextAnchor.MiddleLeft, FontStyle.Bold);
                panel._headers[i].gameObject.AddComponent<LayoutElement>().minHeight = 26f;

                panel._bodies[i] = NewText("Body" + i, content, font, 17, TextColor, TextAnchor.UpperLeft, FontStyle.Normal, true);
                panel._bodies[i].lineSpacing = 1.25f;
            }

            rootObject.SetActive(false);
            return panel;
        }

        // ── Bausteine ─────────────────────────────────────────────

        private static RectTransform NewRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image NewImage(string name, Transform parent, Color color, bool raycast)
        {
            RectTransform rect = NewRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        private static Text NewText(string name, Transform parent, Font font, int size, Color color,
            TextAnchor anchor, FontStyle style, bool wrap = false)
        {
            RectTransform rect = NewRect(name, parent);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.fontStyle = style;
            text.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;
            text.raycastTarget = false;
            return text;
        }

        private static Button NewButton(string name, Transform parent, Color color)
        {
            Image image = NewImage(name, parent, color, true);
            Outline outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = Border;
            outline.effectDistance = new Vector2(1f, -1f);

            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;
            return button;
        }

        private static void AddVertical(GameObject go, RectOffset padding, float spacing)
        {
            VerticalLayoutGroup layout = go.AddComponent<VerticalLayoutGroup>();
            layout.padding = padding;
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}