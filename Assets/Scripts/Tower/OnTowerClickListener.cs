using UnityEngine;

namespace TowerDefense
{
    [RequireComponent(typeof(Tower))]
    public class OnTowerClickListener : MonoBehaviour
    {
        public Vector2 size = new Vector2(1.3f, 1.3f);
        public Vector2 offset = new Vector2(0, .5f);
        [HideInInspector] public bool _enabled = true;

        /// <summary>Klickbox in Weltkoordinaten (wird vom InspectClickHandler ausgewertet).</summary>
        public Bounds WorldBounds =>
            new Bounds(transform.position + (Vector3)offset, new Vector3(size.x, size.y, 1f));

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position + (Vector3)offset, size);
        }
    }
}