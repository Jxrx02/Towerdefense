using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>Eine Animation des Helden in 8 Richtungen.</summary>
    [Serializable]
    public class HeroClip
    {
        public HeroAnim anim;

        [Tooltip("Ganzkörper-Animation")] public DirectionalFrames frames;

        [Tooltip("Optional: nur der Oberkörper. Wird zusammen mit den Bein-Animationen benutzt, " +
                 "wenn sich der Held während der Aktion bewegt. Fehlt sie, wird die Ganzkörper-Animation benutzt.")]
        public DirectionalFrames upperBodyFrames;

        public bool loop;
        [Tooltip("Frame, zu dem eine Schleife zurückspringt. Frames davor laufen nur einmal beim Start. " +
                 "Beispiel Run: die ersten 2 Frames sind das Anlaufen (run-in), der Rest die Schleife, also Wert 2.")]
        public int loopStartFrame;
        
        [Tooltip("Sekunden pro Frame. 0 = Standardwert des Sets")]
        public float timePerFrame;

        [Tooltip("Frame, in dem der Treffer bzw. der Schuss ausgelöst wird. -1 = Mitte der Animation")]
        public int eventFrame = -1;

        [Header("Aufladen (aufgeladene Angriffe)")]
        public bool isChargeable;

        [Tooltip("Frame, ab dem die Aufladeschleife beginnt")]
        public int chargeLoopStartFrame;

        [Tooltip("Letzter Frame der Aufladeschleife")]
        public int chargeLoopEndFrame;

        public bool HasFrames => HasAnyFrames(frames);
        public bool HasUpperBody => HasAnyFrames(upperBodyFrames);

        private static bool HasAnyFrames(DirectionalFrames directional)
        {
            if (directional == null)
                return false;

            for (int i = 0; i < 8; i++)
            {
                Sprite[] sprites = directional.Get((FacingDirection)i);
                if (sprites != null && sprites.Length > 0)
                    return true;
            }

            return false;
        }
        
        
        /// <summary>Ist die Aufladeschleife angegeben?</summary>
        public bool HasChargeLoop => isChargeable && (chargeLoopStartFrame > 0 || chargeLoopEndFrame > 0);

        public int GetEventFrame(int frameCount)
        {
            return eventFrame >= 0 ? eventFrame : frameCount / 2;
        }

        public int GetLoopStart(int frameCount)
        {
            return loop ? Mathf.Clamp(loopStartFrame, 0, Mathf.Max(0, frameCount - 1)) : 0;
        }

        /// <summary>Frames der Aufladeschleife. Nicht angegeben: auf dem Frame vor dem Schlag anhalten.</summary>
        public void GetChargeRange(int frameCount, out int start, out int end)
        {
            if (HasChargeLoop)
            {
                start = chargeLoopStartFrame;
                end = chargeLoopEndFrame;
            }
            else
            {
                start = end = Mathf.Max(0, GetEventFrame(frameCount) - 1);
            }

            int last = Mathf.Max(0, frameCount - 1);
            start = Mathf.Clamp(start, 0, last);
            end = Mathf.Clamp(end, start, last);
        }

        /// <summary>Erster Frame des Schlags nach dem Loslassen</summary>
        public int GetReleaseFrame(int frameCount)
        {
            GetChargeRange(frameCount, out _, out int end);
            return Mathf.Min(end + 1, Mathf.Max(0, frameCount - 1));
        }
    }

}