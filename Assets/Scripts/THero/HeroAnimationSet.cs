using System;
using System.Collections.Generic;
using TowerDefense;
using UnityEngine;


    /// <summary>
    /// Alle Animationen einer Waffenart: Ganzkörper-Clips (Idle, Angriffe, Block, ...) und
    /// reine Bein-Clips (Walk, Run, Sprint, WalkBack) für Angriffe während der Bewegung.
    /// Schwert, Zweihänder und Schild teilen sich meist ein Set mit Schwert-Beinen, der Bogen hat ein eigenes.
    /// </summary>

    [CreateAssetMenu(
        fileName = "HeroAnimationSet",
        menuName = "ScriptableObjects/Hero/Animation Set"
    )]
    public class HeroAnimationSet : ScriptableObject
    {
        [SerializeField] private float defaultTimePerFrame = 0.1f;
        [SerializeField] public List<HeroClip> bodyClips = new List<HeroClip>();

        [Tooltip("Nur Beine: Walk, Run, Sprint, WalkBack")]
        [SerializeField] public List<HeroClip> legClips = new List<HeroClip>();

        private Dictionary<HeroAnim, HeroClip> bodyLookup;
        private Dictionary<HeroAnim, HeroClip> legLookup;

        public float DefaultTimePerFrame => defaultTimePerFrame;

        public HeroClip GetBody(HeroAnim anim)
        {
            return Find(ref bodyLookup, bodyClips, anim);
        }

        public HeroClip GetLegs(HeroAnim anim)
        {
            HeroClip clip = Find(ref legLookup, legClips, anim);
            return clip != null && clip.HasFrames ? clip : null;
        }

        private static HeroClip Find(ref Dictionary<HeroAnim, HeroClip> lookup, List<HeroClip> clips, HeroAnim anim)
        {
            if (lookup == null)
            {
                lookup = new Dictionary<HeroAnim, HeroClip>();

                foreach (HeroClip clip in clips)
                {
                    if (clip != null && !lookup.ContainsKey(clip.anim))
                        lookup.Add(clip.anim, clip);
                }
            }

            lookup.TryGetValue(anim, out HeroClip found);
            return found;
        }

        private void OnValidate()
        {
            bodyLookup = null;
            legLookup = null;

            foreach (HeroClip clip in bodyClips)
            {
                if (clip == null) continue;

                if (clip.anim.IsChargedAttack())
                    clip.isChargeable = true;

                clip.loopStartFrame = Mathf.Max(0, clip.loopStartFrame);
                clip.chargeLoopStartFrame = Mathf.Max(0, clip.chargeLoopStartFrame);
                clip.chargeLoopEndFrame = Mathf.Max(clip.chargeLoopStartFrame, clip.chargeLoopEndFrame);
            }

            foreach (HeroClip clip in legClips)
            {
                if (clip != null)
                    clip.loopStartFrame = Mathf.Max(0, clip.loopStartFrame);
            }
        }

#if UNITY_EDITOR
        /// <summary>Nur für Editor-Werkzeuge (z. B. Migration aus SpriteAnim).</summary>
        public void EditorSetBodyClips(List<HeroClip> clips)
        {
            bodyClips = clips;
            bodyLookup = null;
        }
        
        public void EditorUpsertBodyClip(HeroClip newClip)
        {
            UpsertClip(bodyClips, newClip);
            bodyLookup = null;
        }

        public void EditorUpsertLegClip(HeroClip newClip)
        {
            UpsertClip(legClips, newClip);
            legLookup = null;
        }

        private static void UpsertClip(List<HeroClip> clips, HeroClip newClip)
        {
            if (newClip == null)
                return;

            int index = clips.FindIndex(
                clip => clip != null && clip.anim == newClip.anim
            );

            if (index >= 0)
                clips[index] = newClip;
            else
                clips.Add(newClip);
        }
#endif
    
}
