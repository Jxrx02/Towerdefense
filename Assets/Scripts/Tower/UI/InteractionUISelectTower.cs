using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense
{
    public class InteractionUISelectTower : MonoBehaviour
    {
        public static InteractionUISelectTower instance;


        // =========================================================
        // TOWER OPTION
        // =========================================================

        [System.Serializable]
        public class TowerOption
        {
            [Header("Tower")]
            public GameObject towerPrefab;

            [Header("UI")]
            public TowerSelectionEntry entry;
        }


        // =========================================================
        // INSPECTOR
        // =========================================================

        [Header("Tower Options")]
        [SerializeField]
        private List<TowerOption> towerOptions = new List<TowerOption>();


        [Header("Input")]
        [SerializeField]
        private bool allowMouseSelection = true;


        // =========================================================
        // STATE
        // =========================================================

        private TowerConstructionSite currentSite;

        private int selectedIndex = 0;


        // =========================================================
        // PROPERTIES
        // =========================================================

        public GameObject SelectedTower
        {
            get
            {
                if (towerOptions == null ||
                    towerOptions.Count == 0)
                    return null;

                if (selectedIndex < 0 ||
                    selectedIndex >= towerOptions.Count)
                    return null;

                return towerOptions[selectedIndex].towerPrefab;
            }
        }


        // =========================================================
        // UNITY
        // =========================================================

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            gameObject.SetActive(false);
        }


        private void Update()
        {
            if (!gameObject.activeSelf)
                return;

            HandleKeyboardNavigation();
        }


        // =========================================================
        // SHOW
        // =========================================================

        public void Show(TowerConstructionSite site)
        {
            currentSite = site;

            // Immer beim ersten Tower starten
            selectedIndex = 0;

            gameObject.SetActive(true);

            RefreshAllEntries();
            
            Tower tower = GetSelectedTower().gameObject.GetComponent<Tower>();
            
            InteractionPopup.instance.Show(
                site.gameObject.transform.position,
                "to buy " + tower.towerName,
                tower.towerName,
                tower.towerInitPrice,
                tower.towerDesc,
                false
            );

            SelectIndex(0);
        }


        // =========================================================
        // KEYBOARD
        // =========================================================

        private void HandleKeyboardNavigation()
        {
            // Rechts / D
            if (Input.GetKeyDown(KeyCode.RightArrow) ||
                Input.GetKeyDown(KeyCode.D))
            {
                SelectNext();
            }


            // Links / A
            if (Input.GetKeyDown(KeyCode.LeftArrow) ||
                Input.GetKeyDown(KeyCode.A))
            {
                SelectPrevious();
            }


            // Unten / S
            if (Input.GetKeyDown(KeyCode.DownArrow) ||
                Input.GetKeyDown(KeyCode.S))
            {
                SelectNext();
            }


            // Oben / W
            if (Input.GetKeyDown(KeyCode.UpArrow) ||
                Input.GetKeyDown(KeyCode.W))
            {
                SelectPrevious();
            }
        }


        // =========================================================
        // NEXT
        // =========================================================

        public void SelectNext()
        {
            if (towerOptions == null ||
                towerOptions.Count == 0)
                return;

            int nextIndex =
                selectedIndex + 1;

            if (nextIndex >= towerOptions.Count)
                nextIndex = 0;

            SelectIndex(nextIndex);
        }


        // =========================================================
        // PREVIOUS
        // =========================================================

        public void SelectPrevious()
        {
            if (towerOptions == null ||
                towerOptions.Count == 0)
                return;

            int previousIndex =
                selectedIndex - 1;

            if (previousIndex < 0)
                previousIndex =
                    towerOptions.Count - 1;

            SelectIndex(previousIndex);
        }


        // =========================================================
        // SELECT INDEX
        // =========================================================

        public void SelectIndex(int index)
        {
            if (towerOptions == null ||
                towerOptions.Count == 0)
                return;

            if (index < 0 ||
                index >= towerOptions.Count)
                return;

            selectedIndex = index;

            RefreshSelection();
        }


        // =========================================================
        // MOUSE
        // =========================================================

        public void SelectFromMouse(int index)
        {
            if (!allowMouseSelection)
                return;

            SelectIndex(index);
        }


        // =========================================================
        // REFRESH ENTRIES
        // =========================================================

        private void RefreshAllEntries()
        {
            if (towerOptions == null)
                return;

            for (int i = 0;
                 i < towerOptions.Count;
                 i++)
            {
                TowerOption option =
                    towerOptions[i];

                if (option == null)
                    continue;

                if (option.entry == null)
                    continue;

                if (option.towerPrefab == null)
                    continue;

                Tower tower =
                    option.towerPrefab
                        .GetComponent<Tower>();

                if (tower == null)
                {
                    Debug.LogError(
                        $"Tower-Prefab '{option.towerPrefab.name}' " +
                        $"besitzt keine Tower-Komponente.",
                        option.towerPrefab
                    );

                    continue;
                }

                option.entry.Initialize(
                    i,
                    tower
                );
                
                
            }
        }


        // =========================================================
        // REFRESH SELECTION
        // =========================================================

        private void RefreshSelection()
        {
            if (towerOptions == null)
                return;

            for (int i = 0;
                 i < towerOptions.Count;
                 i++)
            {
                TowerOption option =
                    towerOptions[i];

                if (option == null)
                    continue;

                if (option.entry == null)
                    continue;

                bool selected =
                    i == selectedIndex;

                option.entry.SetSelected(
                    selected
                );
            }
        }


        // =========================================================
        // GET SELECTED TOWER
        // =========================================================

        public GameObject GetSelectedTower()
        {
            return SelectedTower;
        }


        public int GetSelectedIndex()
        {
            return selectedIndex;
        }


        // =========================================================
        // HIDE
        // =========================================================

        public void Hide()
        {
            currentSite = null;

            gameObject.SetActive(false);
        }
    }
}