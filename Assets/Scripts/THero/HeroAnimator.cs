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
                PlayLegs(locomotion.Value);

            SetLayered(useLayers);
            return true;
        }

        /// <summary>
        /// Spielt die erste vorhandene Animation der Liste, falls sie nicht schon läuft.
        /// Für Dauerzustände wie Idle und Laufen, die jeden Frame aufgerufen werden dürfen.
        /// </summary>
        public bool PlayLooped(HeroAnim[] fallbackOrder)
        {
            foreach (HeroAnim anim in fallbackOrder)
            {
                if (IsCurrent(anim))
                    return true;

                if (Play(anim))
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
                    PlayLegs(locomotion.Value);

                SetLayered(wantLayers);
            }
            else if (wantLayers && legs.Current != locomotion.Value)
            {
                PlayLegs(locomotion.Value);
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

        private void PlayLegs(HeroAnim locomotion)
        {
            HeroClip legClip = set.GetLegs(locomotion);
            legs.Play(legClip, legClip.frames, FrameTime(legClip), 1f);
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