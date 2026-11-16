using LevelSelector.Definitions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LevelSelector
{
    public class LoadoutSelectionButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI")]
        [SerializeField] private Image border;
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private GameObject selectedObject;

        private Button button;

        private int index;
        private LoadoutSelectionType selectionType;

        private WeaponDefinition weapon;
        private PerkDefinition perk;
        private MutatorDefinition mutator;

        // Darstellung
        private Color _baseColor = Color.white;
        private GameObject _overlay;
        private Image _dim;
        private Text _badge;
        private bool _selected;
        private bool _hovered;
        private bool _locked;
        private float _hover;   // 0..1
        private float _pop;     // 1 -> 0 nach dem Auswählen

        private void Awake()
        {
            button = GetComponent<Button>();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;   // läuft auch im Pausemenü

            _hover = Mathf.MoveTowards(_hover, _hovered ? 1f : 0f, dt * 8f);
            _pop = Mathf.MoveTowards(_pop, 0f, dt * 3.5f);

            float hoverScale = _locked ? 0.03f : 0.07f;
            transform.localScale = Vector3.one * (1f + hoverScale * _hover + 0.18f * Mathf.Sin(_pop * Mathf.PI));
        }

        public void OnPointerEnter(PointerEventData eventData) => _hovered = true;
        public void OnPointerExit(PointerEventData eventData) => _hovered = false;

        public void SetupWeapon(
            WeaponDefinition definition,
            int index,
            Color color,
            System.Action<int> onClick)
        {
            this.weapon = definition;
            this.perk = null;
            this.mutator = null;

            this.index = index;
            selectionType = LoadoutSelectionType.Weapon;

            SetupVisual(definition.icon, color);

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick(this.index));

            SetSelected(false);
        }

        public void SetupPerk(
            PerkDefinition definition,
            int index,
            Color color,
            System.Action<int> onClick)
        {
            this.weapon = null;
            this.perk = definition;
            this.mutator = null;

            this.index = index;
            selectionType = LoadoutSelectionType.Perk;

            SetupVisual(definition.icon, color);

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick(this.index));

            SetSelected(false);
        }

        public void SetupMutator(
            MutatorDefinition definition,
            int index,
            Color color,
            System.Action<int> onClick)
        {
            this.weapon = null;
            this.perk = null;
            this.mutator = definition;

            this.index = index;
            selectionType = LoadoutSelectionType.Mutator;

            SetupVisual(definition.icon, color);

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick(this.index));

            SetSelected(false);
        }

        private void SetupVisual(Sprite sprite, Color color)
        {
            _baseColor = color;

            if (icon != null)
                icon.sprite = sprite;

            if (background != null)
                background.color = color;
        }

        /// <summary>
        /// Zustand des Inhalts. Locked: ausgegraut mit Level-Badge. Purchasable: normal, Preis als Badge.
        /// Unlocked/Owned: normal ohne Badge. Das Klicken bleibt immer möglich (zeigt dann die Details).
        /// </summary>
        public void SetAvailability(UnlockState state, string badge, Font font)
        {
            _locked = state == UnlockState.Locked;
            bool showBadge = (_locked || state == UnlockState.Purchasable) && !string.IsNullOrEmpty(badge);

            if (!_locked && !showBadge && _overlay == null)
                return;   // nichts zu zeigen, nichts anzulegen

            EnsureOverlay(font);

            _dim.enabled = _locked;
            _badge.gameObject.SetActive(showBadge);
            _badge.text = badge ?? "";
            _badge.color = _locked ? new Color(0.96f, 0.92f, 0.82f) : new Color(1f, 0.78f, 0.29f);

            if (background != null)
                background.color = _locked ? Color.Lerp(_baseColor, new Color(0.22f, 0.20f, 0.18f), 0.75f) : _baseColor;

            if (icon != null)
                icon.color = _locked ? new Color(0.6f, 0.6f, 0.6f, 1f) : Color.white;
        }

        private void EnsureOverlay(Font font)
        {
            if (_overlay != null) return;

            _overlay = new GameObject("Availability", typeof(RectTransform));
            _overlay.transform.SetParent(transform, false);

            RectTransform rect = (RectTransform)_overlay.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            _dim = _overlay.AddComponent<Image>();
            _dim.color = new Color(0f, 0f, 0f, 0.38f);
            _dim.raycastTarget = false;

            GameObject badgeObject = new GameObject("Badge", typeof(RectTransform));
            badgeObject.transform.SetParent(_overlay.transform, false);

            RectTransform badgeRect = (RectTransform)badgeObject.transform;
            badgeRect.anchorMin = new Vector2(0f, 0f);
            badgeRect.anchorMax = new Vector2(1f, 0f);
            badgeRect.pivot = new Vector2(0.5f, 0f);
            badgeRect.anchoredPosition = new Vector2(0f, 4f);
            badgeRect.sizeDelta = new Vector2(0f, 20f);

            _badge = badgeObject.AddComponent<Text>();
            _badge.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _badge.fontSize = 14;
            _badge.fontStyle = FontStyle.Bold;
            _badge.alignment = TextAnchor.MiddleCenter;
            _badge.horizontalOverflow = HorizontalWrapMode.Overflow;
            _badge.verticalOverflow = VerticalWrapMode.Overflow;
            _badge.raycastTarget = false;

            Outline outline = badgeObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);
        }

        public void SetSelected(bool selected)
        {
            if (selected && !_selected)
                _pop = 1f;

            _selected = selected;

            if (selectedObject != null)
                selectedObject.SetActive(selected);

            if (border != null)
                border.enabled = selected;
        }

        public WeaponDefinition GetWeapon()
        {
            return weapon;
        }

        public PerkDefinition GetPerk()
        {
            return perk;
        }

        public MutatorDefinition GetMutator()
        {
            return mutator;
        }

        private enum LoadoutSelectionType
        {
            Weapon,
            Perk,
            Mutator
        }
    }
}