using System.Collections.Generic;
using LevelSelector.Definitions;
using UnityEngine;
using UnityEngine.UI;

namespace LevelSelector
{
    public class LevelLoadoutUI : MonoBehaviour
    {
        public static LevelLoadoutUI Instance { get; private set; }

        [Header("Panel")]
        [SerializeField] private GameObject panel;

        [Header("Containers")]
        [SerializeField] private Transform weaponContainer;
        [SerializeField] private Transform perkContainer;
        [SerializeField] private Transform mutatorContainer;
        [SerializeField] private Transform questContainer;

        [Header("Level Details")]
        [SerializeField] private Text textTitel;
        [SerializeField] private Text textDescription;

        [Header("Selection Counters")]
        [SerializeField] private Text textWeaponsSelected;
        [SerializeField] private Text textPerksSelected;
        [SerializeField] private Text textMutatorsSelected;

        [Header("Prefabs")]
        [SerializeField] private GameObject questPrefab;
        [SerializeField] private GameObject btnPrefab;

        [Header("Colors")]
        [SerializeField] private Color colorWeapon;
        [SerializeField] private Color colorPerk;
        [SerializeField] private Color colorMutator;

        private LevelDefinition currentLevel;

        private readonly List<LoadoutSelectionButton> weaponButtons = new();
        private readonly List<LoadoutSelectionButton> perkButtons = new();
        private readonly List<LoadoutSelectionButton> mutatorButtons = new();

        private const int MaxWeapons = 1;
        private const int MaxPerks = 5;
        private const int MaxMutators = 3;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (panel != null)
                panel.SetActive(false);
        }

        public void Show(LevelDefinition level)
        {
            if (level == null)
            {
                Debug.LogError("LevelLoadoutUI.Show(): LevelDefinition ist null.");
                return;
            }

            currentLevel = level;

            panel.SetActive(true);

            textTitel.text = level.levelName;
            textDescription.text = level.description;

            ClearAllContainers();

            BuildWeaponUI();
            BuildPerkUI();
            BuildMutatorUI();
            BuildQuestUI();

            UpdateCounters();

            LevelLoadoutManager.Instance.CreateLoadout(currentLevel);
        }

        private void BuildWeaponUI()
        {
            if (currentLevel.availableWeapons == null)
                return;

            for (int i = 0; i < currentLevel.availableWeapons.Count; i++)
            {
                WeaponDefinition weapon = currentLevel.availableWeapons[i];

                GameObject obj = Instantiate(btnPrefab, weaponContainer);

                LoadoutSelectionButton button =
                    obj.GetComponent<LoadoutSelectionButton>();

                if (button == null)
                {
                    Debug.LogError(
                        "btnPrefab besitzt kein LoadoutSelectionButton-Script.");
                    Destroy(obj);
                    continue;
                }

                int index = i;

                button.SetupWeapon(
                    weapon,
                    index,
                    colorWeapon,
                    OnWeaponSelected);

                weaponButtons.Add(button);
            }
        }

        private void BuildPerkUI()
        {
            if (currentLevel.availablePerks == null)
                return;

            for (int i = 0; i < currentLevel.availablePerks.Count; i++)
            {
                PerkDefinition perk = currentLevel.availablePerks[i];

                GameObject obj = Instantiate(btnPrefab, perkContainer);

                LoadoutSelectionButton button =
                    obj.GetComponent<LoadoutSelectionButton>();

                if (button == null)
                {
                    Debug.LogError(
                        "btnPrefab besitzt kein LoadoutSelectionButton-Script.");
                    Destroy(obj);
                    continue;
                }

                int index = i;

                button.SetupPerk(
                    perk,
                    index,
                    colorPerk,
                    OnPerkSelected);

                perkButtons.Add(button);
            }
        }

        private void BuildMutatorUI()
        {
            if (currentLevel.availableMutators == null)
                return;

            for (int i = 0; i < currentLevel.availableMutators.Count; i++)
            {
                MutatorDefinition mutator =
                    currentLevel.availableMutators[i];

                GameObject obj = Instantiate(btnPrefab, mutatorContainer);

                LoadoutSelectionButton button =
                    obj.GetComponent<LoadoutSelectionButton>();

                if (button == null)
                {
                    Debug.LogError(
                        "btnPrefab besitzt kein LoadoutSelectionButton-Script.");
                    Destroy(obj);
                    continue;
                }

                int index = i;

                button.SetupMutator(
                    mutator,
                    index,
                    colorMutator,
                    OnMutatorSelected);

                mutatorButtons.Add(button);
            }
        }

        private void OnWeaponSelected(int index)
        {
            if (index < 0 || index >= weaponButtons.Count)
                return;

            WeaponDefinition selectedWeapon =
                weaponButtons[index].GetWeapon();

            if (selectedWeapon == null)
                return;

            LevelLoadoutManager.Instance.SetWeapon(selectedWeapon);

            for (int i = 0; i < weaponButtons.Count; i++)
            {
                weaponButtons[i].SetSelected(i == index);
            }

            ShowWeaponDetails(selectedWeapon);

            UpdateCounters();
        }

        private void OnPerkSelected(int index)
        {
            if (index < 0 || index >= perkButtons.Count)
                return;

            PerkDefinition perk =
                perkButtons[index].GetPerk();

            if (perk == null)
                return;

            bool alreadySelected =
                LevelLoadoutManager.Instance.CurrentLoadout
                    .HasPerk(perk);

            if (alreadySelected)
            {
                LevelLoadoutManager.Instance.RemovePerk(perk);
            }
            else
            {
                int currentCount =
                    LevelLoadoutManager.Instance.CurrentLoadout
                        .perks.Count;

                if (currentCount >= MaxPerks)
                    return;

                LevelLoadoutManager.Instance.AddPerk(perk);
            }

            RefreshPerkSelection();

            ShowPerkDetails(perk);

            UpdateCounters();
        }

        private void OnMutatorSelected(int index)
        {
            if (index < 0 || index >= mutatorButtons.Count)
                return;

            MutatorDefinition mutator =
                mutatorButtons[index].GetMutator();

            if (mutator == null)
                return;

            bool alreadySelected =
                LevelLoadoutManager.Instance.CurrentLoadout
                    .HasMutator(mutator);

            if (alreadySelected)
            {
                LevelLoadoutManager.Instance.RemoveMutator(mutator);
            }
            else
            {
                int currentCount =
                    LevelLoadoutManager.Instance.CurrentLoadout
                        .mutators.Count;

                if (currentCount >= MaxMutators)
                    return;

                LevelLoadoutManager.Instance.AddMutator(mutator);
            }

            RefreshMutatorSelection();

            ShowMutatorDetails(mutator);

            UpdateCounters();
        }

        private void RefreshPerkSelection()
        {
            foreach (LoadoutSelectionButton button in perkButtons)
            {
                bool selected =
                    LevelLoadoutManager.Instance.CurrentLoadout
                        .HasPerk(button.GetPerk());

                button.SetSelected(selected);
            }
        }

        private void RefreshMutatorSelection()
        {
            foreach (LoadoutSelectionButton button in mutatorButtons)
            {
                bool selected =
                    LevelLoadoutManager.Instance.CurrentLoadout
                        .HasMutator(button.GetMutator());

                button.SetSelected(selected);
            }
        }

        private void ShowWeaponDetails(WeaponDefinition weapon)
        {
            textTitel.text = weapon.weaponName;

            textDescription.text =
                $"PASSIVE\n{weapon.passiveDescription}\n\n" +
                $"ACTIVE\n{weapon.activeDescription}";
        }

        private void ShowPerkDetails(PerkDefinition perk)
        {
            textTitel.text = perk.perkName;
            textDescription.text = perk.description;
        }

        private void ShowMutatorDetails(MutatorDefinition mutator)
        {
            textTitel.text = mutator.mutatorName;
            textDescription.text = mutator.description;
        }

        private void UpdateCounters()
        {
            if (LevelLoadoutManager.Instance == null ||
                LevelLoadoutManager.Instance.CurrentLoadout == null)
                return;

            var loadout =
                LevelLoadoutManager.Instance.CurrentLoadout;

            textWeaponsSelected.text =
                $"Weapons ({(loadout.weapon != null ? 1 : 0)}/{MaxWeapons})";

            textPerksSelected.text =
                $"Perks ({loadout.perks.Count}/{MaxPerks})";

            textMutatorsSelected.text =
                $"Mutators ({loadout.mutators.Count}/{MaxMutators})";
        }

        private void BuildQuestUI()
        {
            if (currentLevel.quests == null)
                return;

            foreach (LevelQuestDefinition quest in currentLevel.quests)
            {
                GameObject obj =
                    Instantiate(questPrefab, questContainer);

                QuestUI questUI =
                    obj.GetComponent<QuestUI>();

                if (questUI == null)
                {
                    Debug.LogError(
                        "questPrefab besitzt kein QuestUI-Script.");

                    Destroy(obj);
                    continue;
                }

                bool completed =
                    LevelProgressManager.Instance
                        .IsQuestCompleted(
                            currentLevel.levelIndex,
                            quest);

                questUI.Setup(quest, completed);
            }
        }

        private void ClearAllContainers()
        {
            ClearContainer(weaponContainer);
            ClearContainer(perkContainer);
            ClearContainer(mutatorContainer);
            ClearContainer(questContainer);

            weaponButtons.Clear();
            perkButtons.Clear();
            mutatorButtons.Clear();
        }

        private void ClearContainer(Transform container)
        {
            if (container == null)
                return;

            foreach (Transform child in container)
            {
                Destroy(child.gameObject);
            }
        }

        public void Close()
        {
            panel.SetActive(false);
        }
    }
}