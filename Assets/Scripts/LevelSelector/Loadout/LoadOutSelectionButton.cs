using LevelSelector.Definitions;
using UnityEngine;
using UnityEngine.UI;

namespace LevelSelector
{
    public class LoadoutSelectionButton : MonoBehaviour
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

        private void Awake()
        {
            button = GetComponent<Button>();
        }

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
            if (icon != null)
                icon.sprite = sprite;

            if (background != null)
                background.color = color;
        }

        public void SetSelected(bool selected)
        {
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