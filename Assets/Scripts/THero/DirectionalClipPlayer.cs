using System;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// Spielt einen HeroClip in 8 Richtungen auf einem SpriteRenderer ab.
    /// Eine Instanz für den Körper, eine zweite für die Beine.
    /// </summary>
    public sealed class DirectionalClipPlayer
    {
        private readonly SpriteRenderer target;

        private HeroClip clip;
        private DirectionalFrames frames;
        private Sprite[] sprites;
        private FacingDirection direction = FacingDirection.Down;

        private float timePerFrame = 0.1f;
        private float speed = 1f;
        private float elapsed;
        private int frame;
        private int eventFrame;
        private bool eventRaised;
        private bool charging;

        public event Action<HeroAnim> Finished;
        public event Action<HeroAnim, int> EventFrameReached;

        public HeroAnim Current { get; private set; }
        public bool IsFinished { get; private set; }
        public bool IsPlaying => clip != null && !IsFinished;

        private int FrameCount => sprites != null ? sprites.Length : 0;

        public DirectionalClipPlayer(SpriteRenderer target)
        {
            this.target = target;
        }

        public void Play(HeroClip newClip, DirectionalFrames newFrames, float frameTime, float playSpeed, int startFrame = 0)
        {
            clip = newClip;
            frames = newFrames;
            Current = newClip.anim;

            timePerFrame = Mathf.Max(0.01f, frameTime);
            speed = Mathf.Max(0.05f, playSpeed);
            elapsed = 0f;
            charging = false;
            IsFinished = false;
            eventRaised = false;

            RefreshSprites();
            frame = Mathf.Clamp(startFrame, 0, Mathf.Max(0, FrameCount - 1));
            eventFrame = clip.eventFrame >= 0 ? clip.eventFrame : FrameCount / 2;

            Apply();
        }

        /// <summary>Wechselt die Frames (z. B. Ganzkörper zu Oberkörper) und behält die Position in der Animation.</summary>
        public void SetFrames(DirectionalFrames newFrames)
        {
            frames = newFrames;
            RefreshSprites();
            frame = Mathf.Clamp(frame, 0, Mathf.Max(0, FrameCount - 1));
            Apply();
        }

        public void SetDirection(FacingDirection newDirection)
        {
            if (direction == newDirection)
                return;

            direction = newDirection;
            RefreshSprites();
            frame = Mathf.Clamp(frame, 0, Mathf.Max(0, FrameCount - 1));
            Apply();
        }

        public void SetCharging(bool value)
        {
            charging = value;
        }

        public void Tick(float deltaTime)
        {
            if (clip == null || IsFinished || FrameCount == 0)
                return;

            elapsed += deltaTime * speed;

            while (elapsed >= timePerFrame && !IsFinished)
            {
                elapsed -= timePerFrame;
                Advance();
            }
        }

        private void Advance()
        {
            frame++;

            // Aufladen: zwischen Start- und Endframe der Schleife bleiben
            if (charging && clip.isChargeable && frame > clip.chargeLoopEndFrame)
                frame = Mathf.Clamp(clip.chargeLoopStartFrame, 0, FrameCount - 1);

            if (frame >= FrameCount)
            {
                if (clip.loop)
                {
                    frame = 0;
                    eventRaised = false;
                }
                else
                {
                    frame = FrameCount - 1;
                    Apply();
                    RaiseEventIfDue();

                    IsFinished = true;
                    Finished?.Invoke(Current);
                    return;
                }
            }

            Apply();
            RaiseEventIfDue();
        }

        private void RaiseEventIfDue()
        {
            if (eventRaised || frame < eventFrame)
                return;

            // Beim Aufladen wird noch nicht zugeschlagen
            if (charging && clip.isChargeable)
                return;

            eventRaised = true;
            EventFrameReached?.Invoke(Current, frame);
        }

        private void RefreshSprites()
        {
            sprites = frames.GetWithFallback(direction);
        }

        private void Apply()
        {
            if (target != null && sprites != null && frame >= 0 && frame < sprites.Length)
                target.sprite = sprites[frame];
        }
    }
}