#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TowerDefense.EditorTools
{
    /// <summary>
    /// Befüllt Turm-Prefabs und erzeugt Projektil-Prefabs aus SlimeTowers.json.
    /// Setup:
    ///  1) SlimeTowers.json irgendwo ins Projekt legen (Assets/...).
    ///  2) Dieses Skript in einen Ordner "Editor" legen.
    ///  3) Ein Projektil-Prefab (mit Projectile, SpriteAnim, YSort) als Vorlage im Project-Fenster auswählen
    ///     und "Tools > Slime Towers > 1) Projektil-Prefabs erzeugen" ausführen.
    ///  4) Einen Turm (Prefab oder Szenenobjekt mit einer Tower-Unterklasse) auswählen und
    ///     "Tools > Slime Towers > 2) Preset anwenden > ..." ausführen.
    /// Sprites, Spawnpunkt, Indikatoren, LevelConfig und VFX-Prefabs werden nicht angefasst.
    /// </summary>
    public static class SlimeTowerSetup
    {
        private const string ProjectileFolder = "Assets/Prefabs/Projectiles/Slime";

        [Serializable] public class Entry { public string path; public string kind; public float num; public string str; }
        [Serializable] public class TowerPreset { public string id; public List<Entry> entries; }
        [Serializable] public class ProjectilePreset { public string name; public List<Entry> entries; }
        [Serializable] public class PresetFile { public List<TowerPreset> towers; public List<ProjectilePreset> projectiles; }

        private static readonly HashSet<string> missingPrefabs = new HashSet<string>();

        // ------------------------------------------------------------------ Menü
        [MenuItem("Tools/Slime Towers/1) Projektil-Prefabs erzeugen (Vorlage auswählen)")]
        public static void CreateProjectiles()
        {
            GameObject template = Selection.activeObject as GameObject;
            string templatePath = template != null ? AssetDatabase.GetAssetPath(template) : null;

            if (string.IsNullOrEmpty(templatePath) || template.GetComponent<Projectile>() == null)
            {
                EditorUtility.DisplayDialog("Slime Towers",
                    "Wähle im Project-Fenster ein Projektil-Prefab als Vorlage aus (mit Projectile-Komponente).", "OK");
                return;
            }

            PresetFile file = LoadPresets();
            if (file == null) return;

            EnsureFolder(ProjectileFolder);
            missingPrefabs.Clear();

            int created = 0, applied = 0, missing = 0;

            foreach (ProjectilePreset preset in file.projectiles)
            {
                string path = $"{ProjectileFolder}/{preset.name}.prefab";

                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    AssetDatabase.CopyAsset(templatePath, path);
                    created++;
                }

                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Projectile projectile = root.GetComponent<Projectile>();
                    if (projectile == null)
                    {
                        Debug.LogWarning($"SlimeTowerSetup: {path} hat keine Projectile-Komponente.");
                        continue;
                    }

                    var so = new SerializedObject(projectile);
                    ApplyEntries(so, preset.entries, false, ref applied, ref missing);
                    so.ApplyModifiedProperties();

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Report("Projektile", $"{file.projectiles.Count} Prefabs in {ProjectileFolder} ({created} neu erzeugt).", applied, missing);
        }

        [MenuItem("Tools/Slime Towers/2) Preset anwenden/Glibberwerfer")] private static void Glibberwerfer() => ApplyTower("Glibberwerfer");
        [MenuItem("Tools/Slime Towers/2) Preset anwenden/Säuretümpel")] private static void Saeuretuempel() => ApplyTower("Säuretümpel");
        [MenuItem("Tools/Slime Towers/2) Preset anwenden/Spaltschleim")] private static void Spaltschleim() => ApplyTower("Spaltschleim");
        [MenuItem("Tools/Slime Towers/2) Preset anwenden/Klebfadenturm")] private static void Klebfadenturm() => ApplyTower("Klebfadenturm");
        [MenuItem("Tools/Slime Towers/2) Preset anwenden/Quallenturm")] private static void Quallenturm() => ApplyTower("Quallenturm");
        [MenuItem("Tools/Slime Towers/2) Preset anwenden/Schleimabfluss")] private static void Schleimabfluss() => ApplyTower("Schleimabfluss");

        // ------------------------------------------------------------------ Turm anwenden
        private static void ApplyTower(string id)
        {
            GameObject go = Selection.activeGameObject;
            Tower tower = go != null ? go.GetComponent<Tower>() : null;

            if (tower == null)
            {
                EditorUtility.DisplayDialog("Slime Towers",
                    "Wähle einen Turm (Prefab oder Szenenobjekt mit einer Tower-Komponente) aus.", "OK");
                return;
            }

            PresetFile file = LoadPresets();
            if (file == null) return;

            TowerPreset preset = file.towers.Find(t => t.id == id);
            if (preset == null)
            {
                EditorUtility.DisplayDialog("Slime Towers", $"Kein Preset '{id}' in SlimeTowers.json.", "OK");
                return;
            }

            missingPrefabs.Clear();
            int applied = 0, missing = 0;

            var so = new SerializedObject(tower);

            // Array-Größen zuerst, sonst gibt es die Elemente noch nicht
            ApplyEntries(so, preset.entries, true, ref applied, ref missing);
            so.ApplyModifiedProperties();
            so.Update();

            ApplyEntries(so, preset.entries, false, ref applied, ref missing);
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(tower);
            if (PrefabUtility.IsPartOfPrefabAsset(tower))
                AssetDatabase.SaveAssets();

            Report(id, $"Preset auf '{tower.name}' angewendet.", applied, missing);
        }

        // ------------------------------------------------------------------ Hilfen
        private static void ApplyEntries(SerializedObject so, List<Entry> entries, bool sizesPass, ref int applied, ref int missing)
        {
            foreach (Entry e in entries)
            {
                if ((e.kind == "size") != sizesPass) continue;

                SerializedProperty p = so.FindProperty(e.path);
                if (p == null)
                {
                    Debug.LogWarning($"SlimeTowerSetup: Feld '{e.path}' nicht gefunden auf {so.targetObject.GetType().Name}.");
                    missing++;
                    continue;
                }

                if (SetValue(p, e)) applied++;
                else missing++;
            }
        }

        private static bool SetValue(SerializedProperty p, Entry e)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.Float:
                    p.floatValue = e.num;
                    return true;

                case SerializedPropertyType.Integer:
                case SerializedPropertyType.ArraySize:
                    p.intValue = Mathf.RoundToInt(e.num);
                    return true;

                case SerializedPropertyType.Boolean:
                    p.boolValue = e.num != 0f;
                    return true;

                case SerializedPropertyType.String:
                    p.stringValue = e.str;
                    return true;

                case SerializedPropertyType.Enum:
                    int value = Mathf.RoundToInt(e.num);
                    if (e.kind == "flags")
                    {
#if UNITY_2021_2_OR_NEWER
                        p.enumValueFlag = value;
#else
                        p.intValue = value;
#endif
                    }
                    else
                    {
                        p.enumValueIndex = value;
                    }
                    return true;

                case SerializedPropertyType.ObjectReference:
                    if (string.IsNullOrEmpty(e.str))
                    {
                        p.objectReferenceValue = null;
                        return true;
                    }

                    GameObject prefab = ResolvePrefab(e.str);
                    if (prefab == null)
                    {
                        missingPrefabs.Add(e.str);
                        return false;
                    }

                    p.objectReferenceValue = prefab;
                    return true;
            }

            Debug.LogWarning($"SlimeTowerSetup: Typ {p.propertyType} von '{p.propertyPath}' wird nicht unterstützt.");
            return false;
        }

        private static GameObject ResolvePrefab(string prefabName)
        {
            GameObject direct = AssetDatabase.LoadAssetAtPath<GameObject>($"{ProjectileFolder}/{prefabName}.prefab");
            if (direct != null) return direct;

            foreach (string guid in AssetDatabase.FindAssets($"{prefabName} t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == prefabName)
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            return null;
        }

        private static PresetFile LoadPresets()
        {
            foreach (string guid in AssetDatabase.FindAssets("SlimeTowers t:TextAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) != "SlimeTowers" || !path.EndsWith(".json"))
                    continue;

                TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                return JsonUtility.FromJson<PresetFile>(asset.text);
            }

            EditorUtility.DisplayDialog("Slime Towers", "SlimeTowers.json wurde im Projekt nicht gefunden.", "OK");
            return null;
        }

        private static void EnsureFolder(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void Report(string title, string message, int applied, int missing)
        {
            string text = $"{message}\n\n{applied} Felder gesetzt, {missing} nicht gesetzt.";

            if (missingPrefabs.Count > 0)
                text += "\n\nPrefabs nicht gefunden (erst Schritt 1 ausführen oder zuweisen):\n- " +
                        string.Join("\n- ", missingPrefabs);

            Debug.Log("SlimeTowerSetup: " + text);
            EditorUtility.DisplayDialog("Slime Towers: " + title, text, "OK");
        }
    }
}
#endif