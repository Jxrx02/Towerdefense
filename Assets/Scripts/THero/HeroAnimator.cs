using System;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// Zeichnet den Helden: ein Körper-Renderer (der am Helden) und ein Bein-Renderer (Kind "Legs").
    ///
    ///   Stehen oder Laufen ohne Angriff   Ganzkörper-Clip (Idle, Walk, Run, ...)
    ///   Aktion im Stand                   Ganzkörper-Clip der Aktion
    ///   Aktion während der Bewegung       Oberkörper-Clip der Aktion + Bein-Clip (Walk/Run/Sprint/WalkBack)
    ///
    /// Die Oberkörper-Variante wird nur benutzt, wenn der Clip sie hat und das Set passende Beine kennt.
    /// Ersetzt SpriteAnim für den Helden. Towers und Gegner benutzen SpriteAnim weiter.
    /// </summary>
    [DisallowMultipleComponent]
    public class HeroAnimator : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer bodyRenderer;
        [Tooltip("Wird bei Bedarf als Kind 'Legs' erzeugt")]
        [SerializeField] private SpriteRenderer legsRenderer;

        private HeroAnimationSet set;
        private DirectionalClipPlayer body;
        private DirectionalClipPlayer legs;
        private HeroClip currentClip;
        private FacingDirection facing = FacingDirection.Down;
        private bool layered;
        private bool visible = true;

        private Color flashColor = Color.white;
        private float flashDuration = 0.2f;
        private float flashTimer;
        
        public event Action<HeroAnim> BodyFinished;
        public event Action<HeroAnim, int> BodyEvent;

        public SpriteRenderer BodyRenderer => bodyRenderer;
        public SpriteRenderer LegsRenderer => legsRenderer;
        public bool IsLayered => layered;

        // ------------------------------------------------------------------
        private void Awake()
        {
            EnsureRenderers();

            body = new DirectionalClipPlayer(bodyRenderer);
            legs = new DirectionalClipPlayer(legsRenderer);

            body.Finished += anim => BodyFinished?.Invoke(anim);
            body.EventFrameReached += (anim, frame) => BodyEvent?.Invoke(anim, frame);
        }

        private void EnsureRenderers()
        {
            if (bodyRenderer == null) bodyRenderer = GetComponent<SpriteRenderer>();
            if (bodyRenderer == null) bodyRenderer = gameObject.AddComponent<SpriteRenderer>();

            if (legsRenderer == null)
            {
                Transform child = transform.Find("Legs");
                GameObject legsObject = child != null ? child.gameObject : new GameObject("Legs");
                legsObject.transform.SetParent(transform, false);

                legsRenderer = legsObject.GetComponent<SpriteRenderer>();
                if (legsRenderer == null) legsRenderer = legsObject.AddComponent<SpriteRenderer>();
            }

            legsRenderer.sharedMaterial = bodyRenderer.sharedMaterial;
            legsRenderer.enabled = false;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;

            body.Tick(dt);

            if (layered)
            {
                legs.Tick(dt);

                // Beine gehören immer direkt hinter den Körper (YSort verändert die Reihenfolge laufend)
                legsRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
                legsRenderer.sortingOrder = bodyRenderer.sortingOrder - 1;
            }

            UpdateFlash();
        }

        // ------------------------------------------------------------------
        // Konfiguration
        // ------------------------------------------------------------------
        public void SetAnimationSet(HeroAnimationSet newSet)
        {
            set = newSet;
            currentClip = null;
            SetLayered(false);
        }

        public void SetFacing(FacingDirection direction)
        {
            facing = direction;
            body.SetDirection(direction);
            legs.SetDirection(direction);
        }

        public void SetVisible(bool value)
        {
            visible = value;
            bodyRenderer.enabled = value;
            UpdateLegsVisibility();
        }

        public void SetCharging(bool value)
        {
            body.SetCharging(value);
        }
        /// <summary>Taste losgelassen: aus der Aufladeschleife direkt in den Schlag springen.</summary>
        public void ReleaseCharge()
        {
            body.ReleaseCharge();
        }
        /// <summary>Kurzes Aufblitzen des Helden, z. B. bei voller Aufladung.</summary>
        public void Flash(Color color, float duration)
        {
            flashColor = color;
            flashDuration = Mathf.Max(0.01f, duration);
            flashTimer = flashDuration;
        }
        /// <summary>Erster Frame des Schlags nach der Aufladeschleife (für den Einstieg in Stufe-2-Clips)</summary>
        public int GetReleaseFrame(HeroAnim anim)
        {
            HeroClip clip = GetClip(anim);
            return clip != null && clip.isChargeable ? clip.GetReleaseFrame(FrameCount(clip)) : 0;
        }
        // ------------------------------------------------------------------
        // Abfragen
        // ------------------------------------------------------------------
        public HeroClip GetClip(HeroAnim anim)
        {
            return set != null ? set.GetBody(anim) : null;
        }

        public bool HasClip(HeroAnim anim)
        {
            HeroClip clip = GetClip(anim);
            return clip != null && clip.HasFrames;
        }

        public float GetClipDuration(HeroAnim anim)
        {
            HeroClip clip = GetClip(anim);
            if (clip == null)
                return 0f;

            Sprite[] sprites = clip.frames.GetWithFallback(facing);
            return sprites != null ? sprites.Length * FrameTime(clip) : 0f;
        }

        // ------------------------------------------------------------------
        // Abspielen
        // ------------------------------------------------------------------

        /// <summary>
        /// Spielt einen Clip. Mit 'locomotion' (Walk/Run/Sprint/WalkBack) und passenden Beinen wird die
        /// Oberkörper-Variante plus Bein-Animation benutzt. Gibt false zurück, wenn der Clip fehlt.
        /// </summary>
        public bool Play(HeroAnim anim, float speed = 1f, HeroAnim? locomotion = null, int startFrame = 0)
        {
            HeroClip clip = GetClip(anim);
            if (clip == null || !clip.HasFrames)
                return false;

            currentClip = clip;

            bool useLayers = CanLayer(clip, locomotion);
            body.Play(clip, useLayers ? clip.upperBodyFrames : clip.frames, FrameTime(clip), speed, startFrame);

            if (useLayers)
                PlayLegs(locomotion.Value, true);   

            SetLayered(useLayers);
            return true;
        }

        /// <param name="skipIntro">
        /// true, wenn der Held schon in Bewegung war (z. B. nach einem Angriff): bei Clips mit Schleifenstart
        /// (Run: run-in, run-loop) wird das Anlaufen übersprungen.
        /// </param>
        public bool PlayLooped(HeroAnim[] fallbackOrder, bool skipIntro = false)
        {
            foreach (HeroAnim anim in fallbackOrder)
            {
                if (IsCurrent(anim))
                    return true;

                HeroClip clip = GetClip(anim);
                if (clip == null || !clip.HasFrames)
                    continue;

                int start = skipIntro ? clip.GetLoopStart(FrameCount(clip)) : 0;

                if (Play(anim, 1f, null, start))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Während eine Aktion läuft: Beine zuschalten (Bewegung) oder wieder auf Ganzkörper wechseln (Stillstand).
        /// Die Position innerhalb der Animation bleibt erhalten.
        /// </summary>
        public void SetLocomotion(HeroAnim? locomotion)
        {
            if (currentClip == null)
                return;

            bool wantLayers = CanLayer(currentClip, locomotion);

            if (wantLayers != layered)
            {
                body.SetFrames(wantLayers ? currentClip.upperBodyFrames : currentClip.frames);

                if (wantLayers)
                    PlayLegs(locomotion.Value, false); 

                SetLayered(wantLayers);
            }
            else if (wantLayers && legs.Current != locomotion.Value)
            {
                PlayLegs(locomotion.Value, true);
            }
        }

        // ------------------------------------------------------------------
        private bool IsCurrent(HeroAnim anim)
        {
            return currentClip != null && currentClip.anim == anim && !body.IsFinished;
        }

        private bool CanLayer(HeroClip clip, HeroAnim? locomotion)
        {
            return locomotion.HasValue &&
                   set != null &&
                   clip.HasUpperBody &&
                   set.GetLegs(locomotion.Value) != null;
        }

        private void PlayLegs(HeroAnim locomotion, bool skipIntro)
        {
            HeroClip legClip = set.GetLegs(locomotion);
            int start = skipIntro ? legClip.GetLoopStart(FrameCount(legClip)) : 0;
            legs.Play(legClip, legClip.frames, FrameTime(legClip), 1f, start);
        }
        private int FrameCount(HeroClip clip)
        {
            Sprite[] sprites = clip.frames.GetWithFallback(facing);
            return sprites != null ? sprites.Length : 0;
        }
        /// <summary>Flash über Körper und Beine, blendet zurück auf Weiß.</summary>
        private void UpdateFlash()
        {
            if (flashTimer <= 0f)
                return;

            flashTimer -= Time.unscaledDeltaTime;

            Color color = Color.Lerp(Color.white, flashColor, Mathf.Clamp01(flashTimer / flashDuration));
            bodyRenderer.color = color;
            legsRenderer.color = color;
        }
        private void SetLayered(bool value)
        {
            layered = value;
            UpdateLegsVisibility();
        }

        private void UpdateLegsVisibility()
        {
            if (legsRenderer != null)
                legsRenderer.enabled = visible && layered;
        }

        private float FrameTime(HeroClip clip)
        {
            return clip.timePerFrame > 0f ? clip.timePerFrame : set.DefaultTimePerFrame;
        }
    }
}