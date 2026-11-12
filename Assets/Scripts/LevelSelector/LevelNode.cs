using System.Collections.Generic;
using LevelSelector.Definitions;
using UnityEngine;

namespace LevelSelector
{
    /// <summary>
    /// Ein Level auf der Übersichtskarte. Läuft der Held heran, zeigt das LevelNodePopup Highscore und Quests.
    /// Mit E öffnet sich der Loadout-Screen.
    /// </summary>
    public class LevelNode : MonoBehaviour
    {
        public static readonly List<LevelNode> All = new();

        [SerializeField] private LevelDefinition level;
        [SerializeField] private float interactRadius = 1.8f;
        [SerializeField] private Vector3 popupOffset = new Vector3(0f, 1.6f, 0f);
        [SerializeField] private Vector3 heroSpawnOffset = new Vector3(0f, -1.4f, 0f);

        [Header("Darstellung")]
        [SerializeField] private SpriteRenderer marker;
        [SerializeField] private Color lockedColor = new Color(0.30f, 0.27f, 0.25f, 1f);
        [SerializeField] private Color availableColor = new Color(0.82f, 0.55f, 0.20f, 1f);
        [SerializeField] private Color completedColor = new Color(0.62f, 0.80f, 0.38f, 1f);
        [SerializeField] private float pulseSpeed = 2.2f;
        [SerializeField] private float pulseAmount = 0.08f;

        private Vector3 _baseScale = Vector3.one;

        public LevelDefinition Level => level;
        public float InteractRadius => interactRadius;
        public Vector3 PopupAnchor => transform.position + popupOffset;
        public Vector3 HeroSpawnPosition => transform.position + heroSpawnOffset;

        public bool IsUnlocked =>
            level != null && level.levelIndex <= LevelProgressManager.GetUnlockedLevel();

        public int Highscore
        {
            get
            {
                LevelProgressManager progress = LevelProgressManager.Instance;
                return level != null && progress != null ? progress.GetHighscore(level.levelIndex) : 0;
            }
        }

        public bool IsCompleted => Highscore > 0;

        private void Awake()
        {
            if (marker != null)
                _baseScale = marker.transform.localScale;
        }

        private void OnEnable() => All.Add(this);

        private void OnDisable() => All.Remove(this);

        private void Start() => Refresh();

        private void Update()
        {
            if (marker == null) return;

            // Freigeschaltete, noch nicht abgeschlossene Level pulsieren leicht
            bool pulse = IsUnlocked && !IsCompleted;
            float scale = pulse ? 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount : 1f;
            marker.transform.localScale = _baseScale * scale;
        }

        public void Refresh()
        {
            if (marker == null) return;

            marker.color = !IsUnlocked ? lockedColor : (IsCompleted ? completedColor : availableColor);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.78f, 0.29f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, interactRadius);
        }
    }
}