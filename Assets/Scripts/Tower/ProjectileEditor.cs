#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// Inspector für Projectile: zeigt nur die Parameter des gewählten Modus.
    /// Liegt jetzt in einer eigenen Datei, weil die schließende Klammer des Namespace vorher innerhalb von
    /// #if UNITY_EDITOR stand (das hätte Builds ohne Editor nicht kompilieren lassen).
    /// Verschiebe die Datei am besten in einen Ordner namens "Editor".
    /// </summary>
    [CustomEditor(typeof(Projectile))]
    public class ProjectileEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Modus-spezifische Felder sind HideInInspector und werden unten gezielt gezeichnet
            DrawPropertiesExcluding(
                serializedObject,
                "aoeRadius", "aoeVfxPrefab",
                "dotDuration", "dotInterval", "dotDamagePerTick",
                "slowDuration", "slowAmount",
                "chainCount", "chainDamage", "chainDamageFalloff", "chainRadius",
                "knockbackDistance", "knockbackDuration",
                "splitterCount", "splitterPrefab", "splitterDamage",
                "pierceCount",
                "orbitProjectilePrefab", "orbitDuration", "orbitDamagePerSecond",
                "blackHolePullDuration", "blackHoleRadius", "blackHoleVfxPrefab",
                "stickyDelay", "stickyAoeRadius", "stickyAoeDamage",
                "chainLinkRadius", "chainSlowAmount", "chainLinkDamage"
            );

            Projectile p = (Projectile)target;

            EditorGUILayout.Space(10);

            switch (p.Mode)
            {
                case ProjectileMode.AOE:
                    Title("AOE Parameter");
                    Field("aoeRadius", "AOE Radius");
                    Field("aoeVfxPrefab", "AOE VFX");
                    break;

                case ProjectileMode.DoT:
                    Title("DoT Parameter");
                    Field("dotDuration", "Duration");
                    Field("dotInterval", "Tick Interval");
                    Field("dotDamagePerTick", "Damage per Tick");
                    break;

                case ProjectileMode.ChainLightning:
                    Title("Chain Lightning Parameter");
                    Field("chainDamage", "Chain Damage");
                    Field("chainCount", "Chain Count");
                    Field("chainDamageFalloff", "Damage Falloff");
                    Field("chainRadius", "Chain Radius");
                    break;

                case ProjectileMode.Knockback:
                    Title("Knockback Parameter");
                    Field("knockbackDistance", "Distance");
                    Field("knockbackDuration", "Duration");
                    break;

                case ProjectileMode.Splitter:
                    Title("Splitter Parameter");
                    Field("splitterCount", "Splitter Count");
                    Field("splitterPrefab", "Splitter Prefab");
                    Field("splitterDamage", "Splitter Damage");
                    break;

                case ProjectileMode.Pierce:
                    Title("Pierce Parameter");
                    Field("pierceCount", "Pierce Count");
                    break;

                case ProjectileMode.BlackHole:
                    Title("Black Hole Parameter");
                    Field("blackHolePullDuration", "Pull Duration");
                    Field("blackHoleRadius", "Radius");
                    Field("blackHoleVfxPrefab", "Black Hole VFX");
                    break;

                case ProjectileMode.Sticky:
                    Title("Sticky Parameter");
                    Field("stickyDelay", "Sticky Delay");
                    Field("stickyAoeRadius", "AOE Radius");
                    Field("stickyAoeDamage", "AOE Damage");
                    Field("aoeVfxPrefab", "AOE VFX");
                    break;

                case ProjectileMode.Chainslow:
                    Title("Chain (Verbindung) Parameter");
                    Field("chainLinkRadius", "Link Radius");
                    Field("chainSlowAmount", "Slow Amount");
                    Field("chainLinkDamage", "Link Damage");
                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void Title(string text)
        {
            EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
        }

        private void Field(string propertyName, string label)
        {
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty(propertyName),
                new GUIContent(label)
            );
        }
    }
}
#endif