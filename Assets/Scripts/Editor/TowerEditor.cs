using UnityEditor;
using UnityEngine;

namespace TowerDefense
{
    [CustomEditor(typeof(Tower), true)]
    public class TowerEditor : Editor
    {
        // ───────────────── Identity ─────────────────

        private SerializedProperty towerName;
        private SerializedProperty towerDesc;
        private SerializedProperty towerInitPrice;

        // ───────────────── Placement ─────────────────

        private SerializedProperty placementType;
        private SerializedProperty blocksPath;

        // ───────────────── Health ─────────────────

        private SerializedProperty statHealthPoints;
        private SerializedProperty currentHealth;

        // ───────────────── Combat ─────────────────

        private SerializedProperty range;
        private SerializedProperty interactionrange;
        private SerializedProperty damage;
        private SerializedProperty timeInBetweenShots;
        private SerializedProperty attackSpeedMultiplier;

        // ───────────────── Visuals ─────────────────

        private SerializedProperty rangeIndicator;
        private SerializedProperty rangeIndicatorOffset;
        private SerializedProperty interactionIndicator;
        private SerializedProperty interactionIndicatorOffset;
        private SerializedProperty outlineMaterial;

        // ───────────────── Targeting ─────────────────

        private SerializedProperty targetPreference;

        // ───────────────── Economy ─────────────────

        private SerializedProperty statCoinsEarnedPerSecond;
        private SerializedProperty statHealthRegenPerSecond;

        // ───────────────── Multipliers ─────────────────

        private SerializedProperty statRangeMultiplier;
        private SerializedProperty statDmgMultiplier;
        private SerializedProperty statAttackSpeedMultiplier;
        private SerializedProperty statSlowMultiplier;

        // ───────────────── Upgrades ─────────────────

        private SerializedProperty spawnProjectileOffsetPoint;
        private SerializedProperty upgradePaths;
        private SerializedProperty pathLevels;

        // ───────────────── Progression ─────────────────

        private SerializedProperty levelConfig;

        // ───────────────── Projectile ─────────────────

        private SerializedProperty projectilePrefab;


        // ============================================================
        // ENABLE
        // ============================================================

        protected virtual void OnEnable()
        {
            towerName = serializedObject.FindProperty("towerName");
            towerDesc = serializedObject.FindProperty("towerDesc");
            towerInitPrice = serializedObject.FindProperty("towerInitPrice");

            placementType = serializedObject.FindProperty("placementType");
            blocksPath = serializedObject.FindProperty("blocksPath");

            statHealthPoints =
                serializedObject.FindProperty("statHealthPoints");

            currentHealth =
                serializedObject.FindProperty("currentHealth");

            range =
                serializedObject.FindProperty("range");

            interactionrange =
                serializedObject.FindProperty("interactionrange");

            damage =
                serializedObject.FindProperty("damage");

            timeInBetweenShots =
                serializedObject.FindProperty("timeInBetweenShots");

            attackSpeedMultiplier =
                serializedObject.FindProperty("attackSpeedMultiplier");

            rangeIndicator =
                serializedObject.FindProperty("rangeIndicator");

            rangeIndicatorOffset =
                serializedObject.FindProperty("rangeIndicatorOffset");

            interactionIndicator =
                serializedObject.FindProperty("interactionIndicator");

            interactionIndicatorOffset =
                serializedObject.FindProperty("interactionIndicatorOffset");

            outlineMaterial =
                serializedObject.FindProperty("outlineMaterial");

            targetPreference =
                serializedObject.FindProperty("targetPreference");

            statCoinsEarnedPerSecond =
                serializedObject.FindProperty("statCoinsEarnedPerSecond");

            statHealthRegenPerSecond =
                serializedObject.FindProperty("statHealthRegenPerSecond");

            statRangeMultiplier =
                serializedObject.FindProperty("statRangeMultiplier");

            statDmgMultiplier =
                serializedObject.FindProperty("statDmgMultiplier");

            statAttackSpeedMultiplier =
                serializedObject.FindProperty("statAttackSpeedMultiplier");

            statSlowMultiplier =
                serializedObject.FindProperty("statSlowMultiplier");

            spawnProjectileOffsetPoint =
                serializedObject.FindProperty("spawnProjectileOffsetPoint");

            upgradePaths =
                serializedObject.FindProperty("upgradePaths");

            pathLevels =
                serializedObject.FindProperty("pathLevels");

            levelConfig =
                serializedObject.FindProperty("levelConfig");

            projectilePrefab =
                serializedObject.FindProperty("projectilePrefab");
        }


        // ============================================================
        // INSPECTOR
        // ============================================================

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawIdentity();
            DrawPlacement();

            PlacementType type =
                (PlacementType)placementType.enumValueIndex;

            EditorGUILayout.Space();

            if (type == PlacementType.Wall)
            {
                DrawWallInspector();

                // Wenn es ein WallSegment ist:
                if (target is WallSegment)
                {
                    DrawWallSegmentInspector();
                }
            }
            else
            {
                DrawTowerInspector();
            }

            serializedObject.ApplyModifiedProperties();
        }


        // ============================================================
        // IDENTITY
        // ============================================================

        private void DrawIdentity()
        {
            EditorGUILayout.LabelField(
                "Identity",
                EditorStyles.boldLabel
            );

            EditorGUILayout.PropertyField(towerName);
            EditorGUILayout.PropertyField(towerDesc);
            EditorGUILayout.PropertyField(towerInitPrice);
        }


        // ============================================================
        // PLACEMENT
        // ============================================================

        private void DrawPlacement()
        {
            EditorGUILayout.Space();

            EditorGUILayout.LabelField(
                "Placement / World Interaction",
                EditorStyles.boldLabel
            );

            EditorGUILayout.PropertyField(placementType);
        }


        // ============================================================
        // WALL
        // ============================================================

        private void DrawWallInspector()
        {
            EditorGUILayout.LabelField(
                "Wall Stats",
                EditorStyles.boldLabel
            );

            EditorGUILayout.PropertyField(interactionrange);
            EditorGUILayout.PropertyField(range);
            EditorGUILayout.PropertyField(damage);
            EditorGUILayout.PropertyField(timeInBetweenShots);
            EditorGUILayout.PropertyField(attackSpeedMultiplier);

            EditorGUILayout.PropertyField(statHealthPoints);
            EditorGUILayout.PropertyField(currentHealth);
            EditorGUILayout.PropertyField(levelConfig);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField(
                "Visuals",
                EditorStyles.boldLabel
            );

            EditorGUILayout.PropertyField(interactionIndicator);
            EditorGUILayout.PropertyField(outlineMaterial);
            EditorGUILayout.PropertyField(interactionIndicatorOffset);

            EditorGUILayout.PropertyField(rangeIndicator);
            EditorGUILayout.PropertyField(rangeIndicatorOffset);
        }


        // ============================================================
        // WALL SEGMENT
        // ============================================================

        private void DrawWallSegmentInspector()
        {
            EditorGUILayout.Space();

            EditorGUILayout.LabelField(
                "Wall Visual",
                EditorStyles.boldLabel
            );

            SerializedProperty wallSprites =
                serializedObject.FindProperty("wallSprites");

            SerializedProperty unbuiltMaterial =
                serializedObject.FindProperty("unbuiltMaterial");

            SerializedProperty builtMaterial =
                serializedObject.FindProperty("builtMaterial");

            EditorGUILayout.PropertyField(
                wallSprites,
                new GUIContent(
                    "Wall Sprites",
                    "16 Sprites entsprechend der 4-Bit-Nachbarschaft."
                ),
                true
            );

            EditorGUILayout.PropertyField(
                unbuiltMaterial,
                new GUIContent(
                    "Unbuilt Material",
                    "Material für eine noch nicht gebaute Wall."
                )
            );

            EditorGUILayout.PropertyField(
                builtMaterial,
                new GUIContent(
                    "Built Material",
                    "Material für eine gebaute Wall."
                )
            );


            EditorGUILayout.Space();

            EditorGUILayout.LabelField(
                "Sorting",
                EditorStyles.boldLabel
            );


            
        }


        // ============================================================
        // TOWER
        // ============================================================

        private void DrawTowerInspector()
        {
            // Placement / World Interaction
            EditorGUILayout.LabelField(
                "Placement / World Interaction",
                EditorStyles.boldLabel
            );

            EditorGUILayout.PropertyField(blocksPath);


            // Health
            EditorGUILayout.Space();

            EditorGUILayout.LabelField(
                "Health",
                EditorStyles.boldLabel
            );

            EditorGUILayout.PropertyField(statHealthPoints);
            EditorGUILayout.PropertyField(currentHealth);


            // Combat
            EditorGUILayout.Space();

            EditorGUILayout.LabelField(
                "Combat Stats",
                EditorStyles.boldLabel
            );

            EditorGUILayout.PropertyField(range);
            EditorGUILayout.PropertyField(interactionrange);
            EditorGUILayout.PropertyField(damage);
            EditorGUILayout.PropertyField(timeInBetweenShots);
            EditorGUILayout.PropertyField(attackSpeedMultiplier);


            // Visuals
            EditorGUILayout.Space();

            EditorGUILayout.LabelField(
                "Visuals",
                EditorStyles.boldLabel
            );

            EditorGUILayout.PropertyField(rangeIndicator);
            EditorGUILayout.PropertyField(rangeIndicatorOffset);

            EditorGUILayout.PropertyField(interactionIndicator);
            EditorGUILayout.PropertyField(interactionIndicatorOffset);

            EditorGUILayout.PropertyField(outlineMaterial);


            // Targeting
            EditorGUILayout.Space();

            EditorGUILayout.LabelField(
                "Targeting",
                EditorStyles.boldLabel
            );

            EditorGUILayout.PropertyField(targetPreference);


            // Economy
            EditorGUILayout.Space();

            EditorGUILayout.LabelField(
                "Economic / Utility Stats",
                EditorStyles.boldLabel
            );

            EditorGUILayout.PropertyField(statCoinsEarnedPerSecond);
            EditorGUILayout.PropertyField(statHealthRegenPerSecond);


            // Multipliers
            EditorGUILayout.Space();

            EditorGUILayout.LabelField(
                "Multipliers",
                EditorStyles.boldLabel
            );

            EditorGUILayout.PropertyField(statRangeMultiplier);
            EditorGUILayout.PropertyField(statDmgMultiplier);
            EditorGUILayout.PropertyField(statAttackSpeedMultiplier);
            EditorGUILayout.PropertyField(statSlowMultiplier);


            // Upgrades
            EditorGUILayout.Space();

            EditorGUILayout.LabelField(
                "Upgrades",
                EditorStyles.boldLabel
            );

            EditorGUILayout.PropertyField(
                spawnProjectileOffsetPoint
            );

            EditorGUILayout.PropertyField(
                upgradePaths,
                true
            );

            EditorGUILayout.PropertyField(
                pathLevels,
                true
            );


            // Progression
            EditorGUILayout.Space();

            EditorGUILayout.LabelField(
                "Progression",
                EditorStyles.boldLabel
            );

            EditorGUILayout.PropertyField(levelConfig);


            // Projectile
            EditorGUILayout.Space();

            EditorGUILayout.LabelField(
                "Projectile",
                EditorStyles.boldLabel
            );

            EditorGUILayout.PropertyField(projectilePrefab);
        }
    }
}