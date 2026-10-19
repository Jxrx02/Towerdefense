using UnityEngine;
using UnityEngine.EventSystems;

namespace TowerDefense
{
    /// <summary>
    /// Klick auf einen Turm, Helden oder Gegner öffnet das InteractionPopup mit den aktuellen Stats.
    /// Klick ins Leere schließt es wieder. Ein Drag (Kamera verschieben) zählt nicht als Klick.
    /// Einmal in der Szene platzieren.
    ///
    /// Es werden keine Collider benutzt (die Turm-Collider sind Reichweiten-Kreise), sondern die Sprite-Grenzen.
    /// </summary>
    public class InspectClickHandler : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [Tooltip("Maximale Mausbewegung in Pixeln, die noch als Klick gilt")]
        [SerializeField] private float maxClickPixels = 8f;
        [Tooltip("Vergrößert die klickbare Fläche, damit kleine Gegner leichter zu treffen sind")]
        [SerializeField] private float clickPadding = 0.15f;

        /// <summary>Andere Systeme (z. B. Klick-zum-Bewegen) können Inspektion kurz sperren.</summary>
        public static bool Suppressed;

        private Vector3 downPosition;
        private bool downValid;

        private void Awake()
        {
            if (worldCamera == null)
                worldCamera = Camera.main;
        }

        private void Update()
        {
            if (Suppressed || InteractionPopup.instance == null)
                return;

            if (Input.GetMouseButtonDown(0))
            {
                downValid = !IsOverUI();
                downPosition = Input.mousePosition;
            }
            else if (Input.GetMouseButtonUp(0) && downValid)
            {
                downValid = false;

                if ((Input.mousePosition - downPosition).sqrMagnitude > maxClickPixels * maxClickPixels)
                    return;   // war ein Drag

                HandleClick();
            }
        }

        private void HandleClick()
        {
            InteractionPopup popup = InteractionPopup.instance;

            // Kauf-/Upgrade-/Mauer-Popup hat Vorrang
            if (popup.IsBusyWithInteraction)
                return;

            if (worldCamera == null)
                worldCamera = Camera.main;
            if (worldCamera == null)
                return;

            Vector3 world = worldCamera.ScreenToWorldPoint(Input.mousePosition);

            Tower hitTower = null;
            Enemy hitEnemy = null;
            float bestArea = float.MaxValue;

            // Türme und Held (Mauern nicht)
            foreach (Tower t in Tower.Active)
            {
                if (t == null || !t.gameObject.activeInHierarchy)
                    continue;

                // Mauern nur, wenn sie gebaut sind (Blaupausen und zerstörte Mauern sind nicht klickbar)
                if (t is WallSegment wall && !wall.IsBuilt)
                    continue;

                if (TryHit(t.gameObject, world, out float area) && area < bestArea)
                {
                    bestArea = area;
                    hitTower = t;
                    hitEnemy = null;
                }
            }

            // Gegner
            if (TowerHeroManager.instance != null)
            {
                foreach (GameObject go in TowerHeroManager.instance.enemies)
                {
                    if (go == null) continue;

                    Enemy e = go.GetComponent<Enemy>();
                    if (e == null || (e.Vitals != null && e.Vitals.IsDead)) continue;

                    if (TryHit(go, world, out float area) && area < bestArea)
                    {
                        bestArea = area;
                        hitEnemy = e;
                        hitTower = null;
                    }
                }
            }

            if (hitEnemy != null)
                popup.ShowInspect(hitEnemy);
            else if (hitTower != null)
                popup.ShowInspect(hitTower);
            else if (popup.IsInspecting)
                popup.Hide();
        }

        private bool TryHit(GameObject go, Vector3 world, out float area)
        {
            area = 0f;

            // Nur der Sprite am Objekt selbst (Kinder wie Reichweiten-Anzeigen würden die Fläche sprengen)
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = go.GetComponentInChildren<SpriteRenderer>();
            if (sr == null || sr.sprite == null)
                return false;

            Bounds b = sr.bounds;
            b.Expand(clickPadding * 2f);

            if (!b.Contains(new Vector3(world.x, world.y, b.center.z)))
                return false;

            area = b.size.x * b.size.y;
            return true;
        }

        private static bool IsOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }
}