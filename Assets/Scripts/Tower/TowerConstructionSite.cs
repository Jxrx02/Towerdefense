using System.Collections;
using UnityEngine;

namespace TowerDefense
{
    public class TowerConstructionSite : MonoBehaviour
    {
        [Header("State")]
        [SerializeField] private bool isBuilt;
        [SerializeField] private bool isDestroyed;

        public bool IsBuilt
        {
            get => isBuilt;
            set => isBuilt = value;
        }

        public bool IsDestroyed
        {
            get => isDestroyed;
            set => isDestroyed = value;
        }

        [Header("Tower")]
        [SerializeField] private Transform towerSpawnPoint;

        [Header("Visual")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        private GameObject activeTower;

        private Coroutine interactionCoroutine;

        private const float interactionHoldDuration = 4f;

        private Vector3Int cell;

        public Vector3Int Cell => cell;


        // =========================================================
        // UNITY
        // =========================================================

        private void Awake()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            isBuilt = false;
            isDestroyed = false;

            SetUnbuiltVisual();
        }


        // =========================================================
        // INITIALIZE
        // =========================================================

        public void Initialize(
            Vector3Int cell,
            Vector3 worldPosition)
        {
            this.cell = cell;

            transform.position = worldPosition;

            isBuilt = false;
            isDestroyed = false;

            activeTower = null;

            SetUnbuiltVisual();
        }


        // =========================================================
        // INTERACTION
        // =========================================================

        public void EnterInteractionRange(Hero hero)
        {
            if (!LevelManager.instance.isDay)
                return;

            // Bereits ein funktionierender Tower vorhanden
            if (isBuilt && !isDestroyed)
                return;

            // Später für Repair
            if (isDestroyed)
                return;

            // Tower-Auswahl direkt anzeigen
            ShowTowerSelection();

            // Falls bereits eine Coroutine läuft
            if (interactionCoroutine != null)
                StopCoroutine(interactionCoroutine);

            interactionCoroutine = StartCoroutine(HoldInteraction());
        }


        public void ExitInteractionRange(Hero hero)
        {
            if (interactionCoroutine != null)
            {
                StopCoroutine(interactionCoroutine);
                interactionCoroutine = null;
            }

            if (InteractionPopup.instance != null)
                InteractionPopup.instance.Hide();

            if (InteractionUISelectTower.instance != null)
                InteractionUISelectTower.instance.Hide();
        }


        // =========================================================
        // HOLD E
        // =========================================================

        private IEnumerator HoldInteraction()
        {
            while (true)
            {
                yield return new WaitUntil(
                    () => Input.GetKeyDown(KeyCode.E)
                );

                float timer = 0f;

                while (Input.GetKey(KeyCode.E))
                {
                    timer += Time.deltaTime;

                    float progress =
                        Mathf.Clamp01(
                            timer / interactionHoldDuration
                        );

                    if (InteractionPopup.instance != null)
                        InteractionPopup.instance.SetProgress(progress);

                    if (timer >= interactionHoldDuration)
                    {
                        interactionCoroutine = null;

                        ConfirmSelectedTower();

                        yield break;
                    }

                    yield return null;
                }

                if (InteractionPopup.instance != null)
                    InteractionPopup.instance.ResetProgress();
            }
        }


        // =========================================================
        // TOWER SELECTION
        // =========================================================

        private void ShowTowerSelection()
        {
            if (InteractionUISelectTower.instance == null)
                return;

            InteractionUISelectTower.instance.Show(this);
        }


        /// <summary>
        /// Wird von InteractionUISelectTower aufgerufen,
        /// sobald der Spieler E bestätigt.
        /// </summary>
        private void ConfirmSelectedTower()
        {
            Debug.LogWarning("ConfirmSelectedTower.");

            if (isBuilt)// || isDestroyed)
                return;

            if (activeTower != null)
                return;

            if (InteractionUISelectTower.instance == null)
                return;

            GameObject selectedTower = InteractionUISelectTower.instance.GetSelectedTower();

            if (selectedTower == null)
            {
                Debug.LogWarning("Kein Tower ausgewählt.");
                return;
            }
            
            BuyTower(selectedTower);
            
            gameObject.SetActive(false);
        }


        // =========================================================
        // SPAWN TOWER
        // =========================================================

        public void BuyTower(GameObject towerPrefab)
        {
            if (towerPrefab == null)
                return;

            if (isDestroyed)
                return;

            Tower towerPrefabComponent = towerPrefab.GetComponent<Tower>();

            if (towerPrefabComponent == null)
            {
                Debug.LogError(
                    $"Das Prefab {towerPrefab.name} besitzt keine Tower-Komponente!",
                    towerPrefab
                );

                return;
            }

            // Preis des ausgewählten Towers
            int cost = towerPrefabComponent.towerInitPrice;

            // Kauf durchführen
            if (!LevelManager.instance.DoPurchase(cost))
            {
                // Nicht genug Gold:
                // Auswahl bleibt offen
                return;
            }

            Vector3 spawnPosition =
                towerSpawnPoint != null
                    ? towerSpawnPoint.position
                    : transform.position;

            // Tower erzeugen
            activeTower = Instantiate(
                towerPrefab,
                spawnPosition,
                Quaternion.identity
            );

            Tower tower = activeTower.GetComponent<Tower>();
            tower.towerConstructionSite = this;
            
            if (tower != null)
            {
                tower.currentHealth = tower.statHealthPoints;

                tower.SetIsSelected(false);
            }

            // Tower registrieren
            if (TowerHeroManager.instance != null)
            {
                TowerHeroManager.instance.RegisterTower(activeTower);
            }

            // Baustelle ist jetzt gebaut
            isBuilt = true;
            isDestroyed = false;


            // UI schließen
            if (InteractionUISelectTower.instance != null)
            {
                InteractionUISelectTower.instance.Hide();
            }

            if (InteractionPopup.instance != null)
            {
                InteractionPopup.instance.Hide();
            }
            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            activeTower = null;
        }
        // =========================================================
        // VISUALS
        // =========================================================

        private void SetUnbuiltVisual()
        {
            if (spriteRenderer != null)
                spriteRenderer.enabled = true;
        }


        private void SetBuiltVisual()
        {
            if (spriteRenderer != null)
                spriteRenderer.enabled = false;
        }
    }
}