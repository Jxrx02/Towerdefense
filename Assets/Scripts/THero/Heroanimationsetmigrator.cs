#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TowerDefense.EditorTools
{
    /// <summary>
    /// Übernimmt die bisherigen Hero-Animationen aus SpriteAnim.directionalAnimations in ein neues HeroAnimationSet,
    /// damit die schon zugewiesenen Sprites nicht neu eingetragen werden müssen.
    ///   Idle -> Idle, Run -> Run, LightAttack -> Attack1, HeavyAttack -> HeavyAttack1,
    ///   BowRangedAttack -> RangedAttack1, BowUpAttack -> UpAttack
    /// Aufruf: Rechtsklick auf die SpriteAnim-Komponente des Helden.
    /// </summary>
    public static class HeroAnimationSetMigrator
    {
        [MenuItem("CONTEXT/SpriteAnim/Hero-Animationsset aus diesen Animationen erzeugen")]
        private static void Create(MenuCommand command)
        {
            SpriteAnim source = command.context as SpriteAnim;
            if (source == null)
                return;

            string path = EditorUtility.SaveFilePanelInProject(
                "Hero-Animationsset speichern", "HeroAnimationSet", "asset", "Wohin soll das Set gespeichert werden?");

            if (string.IsNullOrEmpty(path))
                return;

            var clips = new List<HeroClip>();

            foreach (StateAnimationEntry entry in source.directionalAnimations)
            {
                if (entry == null)
                    continue;

                HeroAnim? mapped = Map(entry.state);
                if (mapped == null)
                    continue;

                clips.Add(new HeroClip
                {
                    anim = mapped.Value,
                    frames = entry.frames,
                    loop = entry.loop,
                    eventFrame = -1,
                    isChargeable = entry.isChargeable,
                    chargeLoopStartFrame = entry.chargeLoopStartFrame,
                    chargeLoopEndFrame = entry.chargeLoopEndFrame
                });
            }

            HeroAnimationSet set = ScriptableObject.CreateInstance<HeroAnimationSet>();
            set.EditorSetBodyClips(clips);

            AssetDatabase.CreateAsset(set, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = set;

            Debug.Log($"HeroAnimationSet mit {clips.Count} Animationen erzeugt: {path}");
        }

        private static HeroAnim? Map(AnimationState state)
        {
            switch (state)
            {
                case AnimationState.Idle: return HeroAnim.Idle;
                case AnimationState.Run: return HeroAnim.Run;
                case AnimationState.LightAttack: return HeroAnim.Attack1;
                case AnimationState.HeavyAttack: return HeroAnim.HeavyAttack1;
                case AnimationState.BowRangedAttack: return HeroAnim.RangedAttack1;
                case AnimationState.BowUpAttack: return HeroAnim.UpAttack;
                default: return null;
            }
        }
    }
}
#endif