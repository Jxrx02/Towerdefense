using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// Kurze globale Spielpause (Time.timeScale = 0) für ein paar Frames, z. B. bei kritischen Treffern.
    /// Gezählt wird in gerenderten Frames, nicht in Zeit. Update läuft auch bei timeScale 0 weiter.
    ///
    /// Wichtig für ein Pause-Menü: vor dem Pausieren HitStop.Cancel() aufrufen. Sonst kann ein laufender
    /// Hit Stop die Pause nach seinem Ende wieder aufheben.
    /// </summary>
    public class HitStop : MonoBehaviour
    {
        public static HitStop Instance { get; private set; }

        [Header("Hit Stop")]
        [SerializeField, Range(0, 25)] private int defaultFrames = 4;

        [Tooltip("Mindestabstand in Echtzeit-Sekunden zwischen zwei Stopps. Verhindert Dauerruckeln bei vielen Treffern.")]
        [SerializeField] private float minInterval = 0.12f;

        private int remainingFrames;
        private float previousTimeScale = 1f;
        private bool isStopping;
        private int startFrame;
        private float lastStopEnd = -999f;

        public static bool IsActive => Instance != null && Instance.isStopping;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);   // nur die Komponente, das GameObject kann anderes enthalten
                return;
            }

            Instance = this;
        }

        public static void Request(int frames = 0)
        {
            if (Instance == null)
                return;

            Instance.StartHitStop(frames);
        }

        /// <summary>Beendet einen laufenden Hit Stop sofort (z. B. bevor das Pause-Menü die Zeit anhält).</summary>
        public static void Cancel()
        {
            if (Instance != null)
                Instance.EndHitStop();
        }

        private void StartHitStop(int frames)
        {
            if (!isActiveAndEnabled)
                return;

            int requested = frames > 0 ? frames : defaultFrames;
            if (requested <= 0)
                return;

            // Läuft schon: nur verlängern
            if (isStopping)
            {
                remainingFrames = Mathf.Max(remainingFrames, requested);
                return;
            }

            // Spiel ist ohnehin pausiert: nichts tun, sonst würde die Pause später aufgehoben
            if (Time.timeScale <= 0f)
                return;

            if (Time.unscaledTime - lastStopEnd < minInterval)
                return;

            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            isStopping = true;
            remainingFrames = requested;
            startFrame = Time.frameCount;
        }

        private void Update()
        {
            if (!isStopping)
                return;

            // Der Frame, in dem angefordert wurde, zählt nicht mit
            if (Time.frameCount == startFrame)
                return;

            remainingFrames--;

            if (remainingFrames <= 0)
                EndHitStop();
        }

        private void EndHitStop()
        {
            if (!isStopping)
                return;

            isStopping = false;
            remainingFrames = 0;
            lastStopEnd = Time.unscaledTime;

            // Nur zurücksetzen, wenn niemand sonst die Zeit verändert hat
            if (Mathf.Approximately(Time.timeScale, 0f))
                Time.timeScale = previousTimeScale;
        }

        private void OnDisable()
        {
            EndHitStop();

            if (Instance == this)
                Instance = null;
        }
    }
}