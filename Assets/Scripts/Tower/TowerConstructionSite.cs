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

        private void Awake()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            isBuilt = false;
            isDestroyed = false;

            SetUnbuiltVisual();
        }

        public void Initialize(Vector3Int cell, Vector3 worldPosition)
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

            if (isBuilt && !isDestroyed)
                return;

            // Später für Repair
            if (isDestroyed)
                return;

            if (InteractionPopup.instance == null)
                return;

            Vector3 popupPos = towerSpawnPoint != null
                ? towerSpawnPoint.position
                : transform.position;

            if (!InteractionPopup.instance.ShowTowerPurchase(this, popupPos))
                return;

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
                InteractionPopup.instance.HideFor(this);
        }

        // =========================================================
        // HOLD E
        // =========================================================

        private IEnumerator HoldInteraction()
        {
            while (true)
            {
                yield return new WaitUntil(() => Input.GetKeyDown(KeyCode.E));

                float timer = 0f;
                bool completed = false;

                while (Input.GetKey(KeyCode.E))
                {
                    timer += Time.deltaTime;

                    InteractionPopup.instance.SetProgress(
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
                    InteractionPopup.instance.ResetProgress();
                    continue;
                }

                if (TryBuySelectedTower())
                {
                    interactionCoroutine = null;
                    yield break;
                }

                // Nicht genug Gold o. Ä.: Fortschritt zurücksetzen, Auswahl bleibt offen
                InteractionPopup.instance.ResetProgress();
                yield return new WaitUntil(() => !Input.GetKey(KeyCode.E));
            }
        }

        private bool TryBuySelectedTower()
        {
            if (isBuilt || isDestroyed || activeTower != null)
                return false;

            if (InteractionPopup.instance == null)
                return false;

            GameObject selected = InteractionPopup.instance.GetSelectedTowerPrefab();

            if (selected == null)
            {
                Debug.LogWarning("Kein Tower ausgewählt.");
                return false;
            }

            return BuyTower(selected);
        }

        // =========================================================
        // SPAWN TOWER
        // =========================================================

        public bool BuyTower(GameObject towerPrefab)
        {
            if (towerPrefab == null || isDestroyed)
                return false;

            Tower towerPrefabComponent = towerPrefab.GetComponent<Tower>();

            if (towerPrefabComponent == null)
            {
                Debug.LogError(
                    $"Das Prefab {towerPrefab.name} besitzt keine Tower-Komponente!",
                    towerPrefab);
                return false;
            }

            if (!LevelManager.instance.DoPurchase(towerPrefabComponent.towerInitPrice))
                return false;

            Vector3 spawnPosition =
                towerSpawnPoint != null
                    ? towerSpawnPoint.position
                    : transform.position;

            activeTower = Instantiate(towerPrefab, spawnPosition, Quaternion.identity);

            Tower tower = activeTower.GetComponent<Tower>();
            tower.towerConstructionSite = this;
            tower.currentHealth = tower.statHealthPoints;
            tower.SetIsSelected(false);

            if (TowerHeroManager.instance != null)
                TowerHeroManager.instance.RegisterTower(activeTower);

            isBuilt = true;
            isDestroyed = false;

            if (InteractionPopup.instance != null)
                InteractionPopup.instance.Hide();

            gameObject.SetActive(false);
            return true;
        }

        private void OnEnable()
        {
            activeTower = null;
        }

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