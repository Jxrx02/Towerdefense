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

        [Header("Interaction")]
        [SerializeField, Min(0f)]
        private float interactionHoldDuration = 4f;

        [Header("Visual")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        private GameObject activeTower;
        private Coroutine interactionCoroutine;
        private bool repairPopupActive;

        private Vector3Int cell;
        public Vector3Int Cell => cell;

        private void Awake()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            isBuilt = false;
            isDestroyed = false;
            activeTower = null;

            SetUnbuiltVisual();
        }

        public void Initialize(Vector3Int cell, Vector3 worldPosition)
        {
            StopInteraction();
            HideInteractionPopup();

            this.cell = cell;
            transform.position = worldPosition;

            activeTower = null;
            isBuilt = false;
            isDestroyed = false;

            SetUnbuiltVisual();
        }

        // =========================================================
        // INTERACTION
        // =========================================================

        public void EnterInteractionRange(Hero hero)
        {
            if (LevelManager.instance == null ||
                !LevelManager.instance.isDay)
            {
                return;
            }

            if (isBuilt && !isDestroyed)
                return;

            if (isDestroyed && !HasBrokenTower())
            {
                Debug.LogWarning(
                    $"Die Baustelle '{name}' hat keinen reparierbaren Turm.",
                    this);

                return;
            }

            InteractionPopup popup = InteractionPopup.instance;

            if (popup == null)
                return;

            StopInteraction();
            HideInteractionPopup();

            Vector3 popupPosition = towerSpawnPoint != null
                ? towerSpawnPoint.position
                : transform.position;

            if (isDestroyed)
            {
                Tower tower = activeTower.GetComponent<Tower>();

                popup.Show(
                    popupPosition,
                    "to repair",
                    "Repair: " + tower.towerName,
                    tower.CalculateRepairPrice(),
                    "This tower has been destroyed and must be repaired.",
                    true);

                repairPopupActive = true;
            }
            else
            {
                if (!popup.ShowTowerPurchase(this, popupPosition))
                    return;

                repairPopupActive = false;
            }

            interactionCoroutine = StartCoroutine(HoldInteraction());
        }

        public void ExitInteractionRange(Hero hero)
        {
            StopInteraction();
            HideInteractionPopup();
        }

        private void OnDisable()
        {
            StopInteraction();
            HideInteractionPopup();
        }

        private bool HasBrokenTower()
        {
            return activeTower != null &&
                   activeTower.GetComponent<Tower>() != null;
        }

        // =========================================================
        // HOLD E
        // =========================================================

        private IEnumerator HoldInteraction()
        {
            InteractionPopup popup = InteractionPopup.instance;

            if (popup == null)
                yield break;

            while (true)
            {
                yield return new WaitUntil(
                    () => Input.GetKeyDown(KeyCode.E));

                float timer = 0f;
                bool completed = false;

                while (Input.GetKey(KeyCode.E))
                {
                    if (LevelManager.instance == null ||
                        !LevelManager.instance.isDay)
                    {
                        break;
                    }

                    timer += Time.deltaTime;

                    popup.SetProgress(
                        Mathf.Clamp01(timer / interactionHoldDuration));

                    if (timer >= interactionHoldDuration)
                    {
                        completed = true;
                        break;
                    }

                    yield return null;
                }

                if (!completed)
                {
                    popup.ResetProgress();
                    yield return new WaitUntil(
                        () => !Input.GetKey(KeyCode.E));

                    continue;
                }

                bool success = isDestroyed
                    ? TryRepairTower()
                    : TryBuySelectedTower();

                if (success)
                {
                    interactionCoroutine = null;
                    yield break;
                }

                // Fehlgeschlagen, zum Beispiel wegen zu wenig Gold.
                // Die Interaktion bleibt offen.
                popup.ResetProgress();

                yield return new WaitUntil(
                    () => !Input.GetKey(KeyCode.E));
            }
        }

        private void StopInteraction()
        {
            if (interactionCoroutine == null)
                return;

            StopCoroutine(interactionCoroutine);
            interactionCoroutine = null;
        }

        private void HideInteractionPopup()
        {
            InteractionPopup popup = InteractionPopup.instance;

            if (popup == null)
            {
                repairPopupActive = false;
                return;
            }

            if (repairPopupActive)
                popup.Hide();
            else
                popup.HideFor(this);

            repairPopupActive = false;
        }

        // =========================================================
        // BUY TOWER
        // =========================================================

        private bool TryBuySelectedTower()
        {
            if (isBuilt || isDestroyed || activeTower != null)
                return false;

            if (LevelManager.instance == null ||
                !LevelManager.instance.isDay)
            {
                return false;
            }

            InteractionPopup popup = InteractionPopup.instance;

            if (popup == null)
                return false;

            GameObject selectedTowerPrefab =
                popup.GetSelectedTowerPrefab();

            if (selectedTowerPrefab == null)
            {
                Debug.LogWarning("Kein Tower ausgewählt.");
                return false;
            }

            return BuyTower(selectedTowerPrefab);
        }

        public bool BuyTower(GameObject towerPrefab)
        {
            if (towerPrefab == null ||
                isBuilt ||
                isDestroyed ||
                activeTower != null ||
                LevelManager.instance == null)
            {
                return false;
            }

            Tower prefabTower = towerPrefab.GetComponent<Tower>();

            if (prefabTower == null)
            {
                Debug.LogError(
                    $"Das Prefab '{towerPrefab.name}' besitzt keine Tower-Komponente!",
                    towerPrefab);

                return false;
            }

            if (!LevelManager.instance.DoPurchase(
                    prefabTower.towerInitPrice))
            {
                return false;
            }

            Vector3 spawnPosition = towerSpawnPoint != null
                ? towerSpawnPoint.position
                : transform.position;

            GameObject instance = Instantiate(
                towerPrefab,
                spawnPosition,
                Quaternion.identity);

            Tower tower = instance.GetComponent<Tower>();

            if (tower == null)
            {
                Debug.LogError(
                    $"Der erzeugte Turm '{instance.name}' besitzt keine Tower-Komponente!",
                    instance);

                Destroy(instance);
                LevelManager.instance.cur_coins += prefabTower.towerInitPrice;

                return false;
            }

            activeTower = instance;

            tower.towerConstructionSite = this;
            tower.currentHealth = tower.statHealthPoints;
            tower.SetIsSelected(false);

            if (TowerHeroManager.instance != null)
                TowerHeroManager.instance.RegisterTower(instance);

            isBuilt = true;
            isDestroyed = false;

            SetBuiltVisual();

            if (InteractionPopup.instance != null)
                InteractionPopup.instance.Hide();

            repairPopupActive = false;
            interactionCoroutine = null;

            gameObject.SetActive(false);

            return true;
        }

        // =========================================================
        // DESTROYED / REPAIR
        // =========================================================

        /// <summary>
        /// Wird von Tower.BreakTower aufgerufen.
        /// Der Turm bleibt erhalten, wird aber deaktiviert.
        /// </summary>
        public void HandleTowerDestroyed(Tower tower)
        {
            if (tower == null)
                return;

            activeTower = tower.gameObject;

            isBuilt = false;
            isDestroyed = true;

            StopInteraction();
            HideInteractionPopup();

            SetUnbuiltVisual();

            // Die Baustelle wird wieder zum Interaktionspunkt.
            gameObject.SetActive(true);
        }

        private bool TryRepairTower()
        {
            if (!isDestroyed ||
                !HasBrokenTower() ||
                LevelManager.instance == null ||
                !LevelManager.instance.isDay)
            {
                return false;
            }

            Tower tower = activeTower.GetComponent<Tower>();
            int repairCost = tower.CalculateRepairPrice();

            if (!LevelManager.instance.DoPurchase(repairCost))
                return false;

            // Dasselbe Tower-Objekt behalten, damit seine Upgrades
            // und sonstigen gekauften Werte erhalten bleiben.
            tower.ResetBroken();
            tower.gameObject.SetActive(true);

            isBuilt = true;
            isDestroyed = false;

            SetBuiltVisual();

            if (InteractionPopup.instance != null)
                InteractionPopup.instance.Hide();

            repairPopupActive = false;
            interactionCoroutine = null;

            // Nach der Reparatur verschwindet die Baustelle wieder.
            gameObject.SetActive(false);

            return true;
        }

        // =========================================================
        // SELL
        // =========================================================

        /// <summary>
        /// Wird von Tower.DestroyTower vor Destroy(gameObject)
        /// aufgerufen, wenn der Turm verkauft wird.
        /// </summary>
        public void HandleTowerSold(Tower tower)
        {
            if (tower != null &&
                activeTower != null &&
                activeTower != tower.gameObject)
            {
                Debug.LogWarning(
                    $"Der verkaufte Turm gehört nicht zur Baustelle '{name}'.",
                    this);

                return;
            }

            StopInteraction();
            HideInteractionPopup();

            activeTower = null;

            isBuilt = false;
            isDestroyed = false;

            SetUnbuiltVisual();

            // Im Unterschied zur Reparatur ist der Turm jetzt entfernt.
            gameObject.SetActive(true);
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