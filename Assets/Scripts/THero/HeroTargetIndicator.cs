
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// Zeigt das aktuell anvisierte Ziel des Helden mit einem Fadenkreuz an.
    /// </summary>
    public sealed class HeroTargetIndicator : MonoBehaviour
    {
        [Header("Indicator")]
        [SerializeField] private SpriteRenderer crosshairPrefab;
        [SerializeField] private float verticalOffset = 0.2f;

        private IHeroCombatHost host;
        private SpriteRenderer crosshair;
        private GameObject currentTarget;

        public void Initialize(IHeroCombatHost combatHost)
        {
            host = combatHost;

            if (crosshair == null && crosshairPrefab != null)
            {
                crosshair = Instantiate(crosshairPrefab);
                crosshair.gameObject.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            if (host == null || crosshair == null)
                return;

            if (!host.TryFindTarget(out GameObject target, out _) ||
                target == null)
            {
                Hide();
                return;
            }

            currentTarget = target;
            UpdatePosition();
        }

        private void UpdatePosition()
        {
            if (currentTarget == null)
            {
                Hide();
                return;
            }
            Vector3 position = currentTarget.transform.position;
            
            position.y += verticalOffset;

            crosshair.transform.position = position;
            crosshair.gameObject.SetActive(true);
        }

        private void Hide()
        {
            currentTarget = null;

            if (crosshair != null)
                crosshair.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (crosshair != null)
                Destroy(crosshair.gameObject);
        }
    }
}
