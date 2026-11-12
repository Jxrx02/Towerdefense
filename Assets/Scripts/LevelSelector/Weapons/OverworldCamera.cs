using UnityEngine;

namespace LevelSelector
{
    /// <summary>Folgt dem Helden auf der Übersichtskarte sanft.</summary>
    public class OverworldCamera : MonoBehaviour
    {
        [Tooltip("Leer = OverworldHero.Instance")]
        [SerializeField] private Transform target;
        [SerializeField] private float smoothTime = 0.18f;
        [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);

        private Vector3 _velocity;

        private Transform Target => target != null ? target : (OverworldHero.Instance != null ? OverworldHero.Instance.transform : null);

        private void Start()
        {
            // Sofort auf den Helden springen (auch nach TeleportTo in Start)
            Transform t = Target;
            if (t != null)
                transform.position = t.position + offset;
        }

        private void LateUpdate()
        {
            Transform t = Target;
            if (t == null) return;

            transform.position = Vector3.SmoothDamp(transform.position, t.position + offset, ref _velocity, smoothTime);
        }
    }
}