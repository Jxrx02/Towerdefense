using System.Collections.Generic;
using TowerDefense;
using UnityEngine;
using UnityEngine.UI;

namespace LevelSelector
{
    /// <summary>
    /// Progressionsleiste am unteren Rand des Hauptmenüs.
    ///
    ///   Eingeklappt   Nur die Kopfzeile schaut leicht ausgeblendet über den Rand: Heldenstufe, XP-Balken, nächste Freischaltung.
    ///   Ausgeklappt   Klick auf die Kopfzeile (oder Esc zum Schließen): die Leiste fährt auf und zeigt die Levelachse.
    ///
    /// Levelachse: alle Heldenstufen der PlayerProgressionDefinition nebeneinander. Pro Stufe die Inhalte, die sie
    /// freischaltet (Perks, Mutatoren, Waffen) mit Zustand gesperrt / freigeschaltet / kaufbar / im Besitz.
    /// Klick auf einen Inhalt zeigt rechts Details, bei Waffen mit Kauf-Button. Die aktuelle Stufe pulsiert,
    /// die Linie bis zur nächsten Stufe füllt sich mit dem XP-Fortschritt.
    ///
    /// Alle Daten kommen aus PlayerProgress, die Leiste hält keinen eigenen Zustand. Das Heldenlevel wird nur
    /// hier angezeigt (die frühere Zeile unter dem Quit-Button entfällt).
    /// Dieses Skript liegt auf dem Wurzelobjekt der Leiste (unten verankert, Pivot unten Mitte). Es wird verschoben.
    /// </summary>
    public class ProgressionBarUI : MonoBehaviour
    {
        /// <summary>Die Leiste ist ausgeklappt. MainMenuUI lässt Esc dann der Leiste (schließt nur sie).</summary>
        public static bool IsExpanded { get; private set; }

        /// <summary>Frame, in dem die Leiste per Esc eingeklappt wurde (verhindert, dass dasselbe Esc das Menü schließt).</summary>
        public static int LastCollapsedFrame { get; private set; } = -1;

        [Header("Leiste")]
        [Tooltip("Alles außer dem Wurzelobjekt. Wird ausgeblendet, wenn es keine Progression gibt.")]
        [SerializeField] private GameObject content;
        [SerializeField] private CanvasGroup panelGroup;
        [Tooltip("So viel der Kopfzeile bleibt im eingeklappten Zustand sichtbar (Pixel)")]
        [SerializeField] private float peekHeight = 68f;
        [SerializeField] private float animationTime = 0.4f;
        [Range(0f, 1f)] [SerializeField] private float collapsedAlpha = 0.72f;
        [Range(0f, 1f)] [SerializeField] private float hoverAlpha = 0.95f;

        [Header("Kopfzeile")]
        [SerializeField] private Button headerButton;
        [SerializeField] private RectTransform chevron;
        [SerializeField] private Text txtLevel;
        [SerializeField] private Text txtXp;
        [SerializeField] private RectTransform barFill;
        [SerializeField] private Text txtHint;
        [SerializeField] private GameObject nextRoot;
        [SerializeField] private Image nextIcon;
        [SerializeField] private Text txtNext;

        [Header("Levelachse")]
        [SerializeField] private CanvasGroup bodyGroup;
        [SerializeField] private ScrollRect timelineScroll;
        [SerializeField] private RectTransform timelineContent;
        [SerializeField] private ProgressionColumnView columnPrefab;
        [SerializeField] private ProgressionItemView itemPrefab;

        [Header("Details")]
        [SerializeField] private GameObject detailEmpty;
        [SerializeField] private Text txtDetailEmpty;
        [SerializeField] private GameObject detailRoot;
        [SerializeField] private Image detailIcon;
        [SerializeField] private Text txtDetailKind;
        [SerializeField] private Text txtDetailName;
        [SerializeField] private Text txtDetailBody;
        [SerializeField] private Text txtDetailLevel;
        [SerializeField] private Text txtDetailMultiplier;
        [SerializeField] private Text txtDetailHint;
        [SerializeField] private Button btnBuy;
        [SerializeField] private Text txtBuy;

        [Header("Farben")]
        [SerializeField] private Color goldColor = new Color(1f, 0.78f, 0.29f);
        [SerializeField] private Color dimColor = new Color(0.74f, 0.67f, 0.55f);
        [SerializeField] private Color goodColor = new Color(0.62f, 0.80f, 0.38f);
        [SerializeField] private Color badColor = new Color(0.90f, 0.35f, 0.30f);

        private RectTransform _rect;
        private bool _expanded;
        private float _t;          // 0 = eingeklappt, 1 = ausgeklappt
        private float _hover;      // 0..1, geglättet
        
        private StatChipBar _chips; 
        private readonly List<StatId> _chipBuffer = new List<StatId>();
        private readonly List<ColumnEntry> _columns = new List<ColumnEntry>();
        private readonly List<ProgressionItemView> _items = new List<ProgressionItemView>();
        private ScriptableObject _selected;
        private int _currentIndex;

        private struct ColumnEntry
        {
            public ProgressionColumnView view;
        }

        // ── Lebenszyklus ──────────────────────────────────────────

        private void Awake()
        {
            _rect = (RectTransform)transform;

            headerButton.onClick.AddListener(Toggle);

            if (btnBuy != null)
                btnBuy.onClick.AddListener(OnBuyClicked);
            if (txtDetailBody != null)
                _chips = StatChipBar.Create(txtDetailBody.transform.parent, txtDetailBody.font, txtDetailBody.transform.GetSiblingIndex() + 1);
            
            LocalizedText.Bind(txtDetailEmpty, () => Texts.Progression.SelectItem);
        }

        private void OnEnable()
        {
            // Das Menü öffnet immer mit eingeklappter Leiste
            _expanded = false;
            _t = 0f;
            IsExpanded = false;
            ApplyPose();

            PlayerProgress.Changed += Rebuild;
            Texts.Changed += Rebuild;

            Rebuild();
        }

        private void OnDisable()
        {
            PlayerProgress.Changed -= Rebuild;
            Texts.Changed -= Rebuild;

            IsExpanded = false;
        }

        // ── Auf- und Zuklappen ────────────────────────────────────

        public void Toggle()
        {
            if (_expanded) Collapse();
            else Expand();
        }

        public void Expand()
        {
            if (_expanded) return;

            _expanded = true;
            IsExpanded = true;
            RefreshHint();

            // Layout steht erst jetzt sicher: zur aktuellen Stufe scrollen
            ScrollToCurrent();
        }

        public void Collapse()
        {
            if (!_expanded) return;

            _expanded = false;
            IsExpanded = false;
            RefreshHint();
        }

        private void Update()
        {
            if (_expanded && Input.GetKeyDown(KeyCode.Escape) && !StatInfoPanel.ConsumesEscape)
            {
                LastCollapsedFrame = Time.frameCount;
                Collapse();
            }

            float dt = Time.unscaledDeltaTime;

            _t = Mathf.MoveTowards(_t, _expanded ? 1f : 0f, dt / Mathf.Max(0.05f, animationTime));

            bool hovered = RectTransformUtility.RectangleContainsScreenPoint(
                (RectTransform)headerButton.transform, Input.mousePosition, null);
            _hover = Mathf.MoveTowards(_hover, hovered ? 1f : 0f, dt / 0.15f);

            ApplyPose();
        }

        private void ApplyPose()
        {
            if (_rect == null)
                _rect = (RectTransform)transform;

            float eased = Mathf.SmoothStep(0f, 1f, _t);
            float hidden = Mathf.Max(0f, _rect.rect.height - peekHeight);

            _rect.anchoredPosition = new Vector2(0f, Mathf.Lerp(-hidden, 0f, eased));

            // Eingeklappt dezent, beim Überfahren der Kopfzeile deutlicher, ausgeklappt voll sichtbar
            float collapsed = Mathf.Lerp(collapsedAlpha, hoverAlpha, _hover);
            panelGroup.alpha = Mathf.Lerp(collapsed, 1f, eased);

            // Der Inhalt blendet erst ein, wenn die Leiste schon ein Stück oben ist
            bodyGroup.alpha = Mathf.Clamp01((eased - 0.3f) / 0.7f);
            bool interactive = _expanded && _t > 0.6f;
            bodyGroup.interactable = interactive;
            bodyGroup.blocksRaycasts = interactive;

            if (chevron != null)
                chevron.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(90f, -90f, eased));
        }

        private void RefreshHint()
        {
            if (txtHint != null)
                txtHint.text = _expanded ? Texts.Progression.CollapseHint : Texts.Progression.ExpandHint;
        }

        // ── Aufbau ────────────────────────────────────────────────

        private void Rebuild()
        {
            bool hasProgression = PlayerProgress.HasProgression;

            if (content != null)
                content.SetActive(hasProgression);

            if (!hasProgression)
                return;

            PlayerProgress.LevelState state = PlayerProgress.Evaluate(PlayerProgress.TotalXp);

            RefreshHeader(state);
            RefreshHint();
            BuildTimeline(state);
            ReselectOrClear();
        }

        private void RefreshHeader(PlayerProgress.LevelState state)
        {
            txtLevel.text = Texts.Progression.Level(state.level);
            txtXp.text = state.isMax ? Texts.Progression.MaxLevel : Texts.Progression.Xp(state.xpIntoLevel, state.xpForNext);

            barFill.anchorMin = new Vector2(0f, 0f);
            barFill.anchorMax = new Vector2(Mathf.Clamp01(state.Fraction), 1f);
            barFill.offsetMin = Vector2.zero;
            barFill.offsetMax = Vector2.zero;

            bool hasNext = PlayerProgress.GetNextUnlock(state.level, out ProgressionUnlock next);
            nextRoot.SetActive(hasNext);

            if (hasNext)
            {
                nextIcon.sprite = next.icon;
                nextIcon.enabled = next.icon != null;
                txtNext.text = Texts.Progression.NextUnlock(next.kind, next.name, next.unlockLevel);
            }
        }

        private void BuildTimeline(PlayerProgress.LevelState state)
        {
            Clear(timelineContent);
            _columns.Clear();
            _items.Clear();

            int count = PlayerProgress.Definition.levels.Count;
            _currentIndex = Mathf.Clamp(state.level - 1, 0, count - 1);

            for (int level = 1; level <= count; level++)
            {
                bool reached = level <= state.level;
                bool current = level == state.level;
                bool hasLeft = level > 1;
                bool hasRight = level < count;

                // Linie zwischen Stufe a und a+1: rechte Hälfte der Spalte a plus linke Hälfte der Spalte a+1
                float leftAmount = hasLeft ? Mathf.Clamp01(SegmentFill(level - 1, state) * 2f - 1f) : 0f;
                float rightAmount = hasRight ? Mathf.Clamp01(SegmentFill(level, state) * 2f) : 0f;

                string xpText = current && !state.isMax ? Texts.Progression.Xp(state.xpIntoLevel, state.xpForNext) : "";

                ProgressionColumnView column = Instantiate(columnPrefab, timelineContent);
                column.Setup(level, reached, current, hasLeft, hasRight, leftAmount, rightAmount, xpText);
                _columns.Add(new ColumnEntry { view = column });

                foreach (ProgressionUnlock unlock in PlayerProgress.GetUnlocksAtLevel(level))
                {
                    ProgressionItemView item = Instantiate(itemPrefab, column.ItemContainer);
                    item.Setup(unlock, OnItemClicked);
                    _items.Add(item);
                }
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(timelineContent);
            ScrollToCurrent();
        }

        /// <summary>Füllgrad der Linie von Stufe 'lower' zu 'lower + 1' (0..1).</summary>
        private static float SegmentFill(int lower, PlayerProgress.LevelState state)
        {
            if (state.isMax || state.level > lower) return 1f;
            return state.level == lower ? state.Fraction : 0f;
        }

        /// <summary>Zentriert die aktuelle Stufe im sichtbaren Bereich.</summary>
        private void ScrollToCurrent()
        {
            if (_columns.Count == 0 || timelineScroll == null || timelineScroll.viewport == null)
                return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(timelineContent);

            RectTransform column = (RectTransform)_columns[_currentIndex].view.transform;
            float center = timelineContent.InverseTransformPoint(column.position).x;

            float viewport = timelineScroll.viewport.rect.width;
            float maxOffset = Mathf.Max(0f, timelineContent.rect.width - viewport);
            float offset = Mathf.Clamp(center - viewport * 0.5f, 0f, maxOffset);

            timelineScroll.velocity = Vector2.zero;
            timelineContent.anchoredPosition = new Vector2(-offset, timelineContent.anchoredPosition.y);
        }

        // ── Auswahl und Details ───────────────────────────────────

        private void OnItemClicked(ProgressionItemView item)
        {
            _selected = item.Unlock.definition;
            ApplySelection(item);
        }

        private void ReselectOrClear()
        {
            foreach (ProgressionItemView item in _items)
            {
                if (_selected != null && item.Unlock.definition == _selected)
                {
                    ApplySelection(item);
                    return;
                }
            }

            _selected = null;
            detailEmpty.SetActive(true);
            detailRoot.SetActive(false);
        }

        private void ApplySelection(ProgressionItemView selectedItem)
        {
            foreach (ProgressionItemView item in _items)
                item.SetSelected(item == selectedItem);

            ShowDetails(selectedItem.Unlock);
        }

        private void ShowDetails(ProgressionUnlock unlock)
        {
            detailEmpty.SetActive(false);
            detailRoot.SetActive(true);

            UnlockState state = PlayerProgress.GetState(unlock);

            detailIcon.sprite = unlock.icon;
            detailIcon.enabled = unlock.icon != null;

            txtDetailKind.text = Texts.Progression.Kind(unlock.kind).ToUpperInvariant();
            txtDetailName.text = unlock.name;
            txtDetailBody.text = StatMarkup.Format(unlock.description, unlock.kind != UnlockKind.Weapon);
            _chipBuffer.Clear();
            StatMarkup.FindStats(txtDetailBody.text, _chipBuffer);
            if (_chips != null) _chips.Show(_chipBuffer, StatSubject.Hero);

            // Freischaltung (Level) und Zustand
            txtDetailLevel.text = unlock.unlockLevel > 0
                ? Texts.Progression.UnlocksAtLevel(unlock.unlockLevel)
                : Texts.Progression.AvailableFromStart;
            txtDetailLevel.color = state == UnlockState.Locked ? badColor : goodColor;

            // Score-Multiplikator (Perk, Mutator)
            bool hasMultiplier = unlock.kind != UnlockKind.Weapon;
            txtDetailMultiplier.gameObject.SetActive(hasMultiplier);
            if (hasMultiplier)
            {
                txtDetailMultiplier.text = Texts.Progression.ScoreMultiplier(unlock.scoreMultiplier);
                txtDetailMultiplier.color = unlock.scoreMultiplier > 1.0001f ? goodColor :
                                            unlock.scoreMultiplier < 0.9999f ? badColor : dimColor;
            }

            ShowPurchase(unlock, state);
        }

        /// <summary>Waffen: Kauf-Button je nach Zustand. Perks und Mutatoren haben keinen.</summary>
        private void ShowPurchase(ProgressionUnlock unlock, UnlockState state)
        {
            bool isWeapon = unlock.kind == UnlockKind.Weapon;

            btnBuy.gameObject.SetActive(isWeapon);
            txtDetailHint.gameObject.SetActive(false);

            if (!isWeapon)
                return;

            switch (state)
            {
                case UnlockState.Locked:
                    txtBuy.text = Texts.Progression.LockedButton(unlock.unlockLevel);
                    btnBuy.interactable = false;
                    break;

                case UnlockState.Purchasable:
                {
                    bool affordable = PlayerProgress.PointsBalance >= unlock.purchaseCost;
                    txtBuy.text = Texts.Progression.BuyButton(unlock.purchaseCost);
                    btnBuy.interactable = affordable;

                    txtDetailHint.gameObject.SetActive(true);
                    txtDetailHint.text = affordable
                        ? Texts.Research.Points(PlayerProgress.PointsBalance)
                        : Texts.Progression.NotEnoughPoints(unlock.purchaseCost - PlayerProgress.PointsBalance);
                    txtDetailHint.color = affordable ? goldColor : badColor;
                    break;
                }

                default:
                    txtBuy.text = Texts.Progression.OwnedButton;
                    btnBuy.interactable = false;
                    break;
            }
        }

        private void OnBuyClicked()
        {
            if (_selected is not LevelSelector.Definitions.WeaponDefinition weapon)
                return;

            PurchaseResult result = PlayerProgress.TryPurchaseWeapon(weapon);

            // Bei Erfolg löst PlayerProgress.Changed den Neuaufbau aus (Zustand, Punkte, Details)
            if (result != PurchaseResult.Success)
            {
                txtDetailHint.gameObject.SetActive(true);
                txtDetailHint.text = Texts.Progression.PurchaseFailed;
                txtDetailHint.color = badColor;
            }
        }

        // ── Hilfen ────────────────────────────────────────────────

        private static void Clear(Transform container)
        {
            if (container == null) return;

            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Transform child = container.GetChild(i);
                child.SetParent(null);   // zählt sofort nicht mehr im Layout
                Destroy(child.gameObject);
            }
        }
    }
}