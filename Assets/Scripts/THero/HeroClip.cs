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
    }

}