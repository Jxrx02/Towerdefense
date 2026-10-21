
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using UnityEditor;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace TowerDefense.EditorTools
{
    public class HeroAsepriteImporter : EditorWindow
    {
        private UnityEngine.Object sourceAsset;
        private TextAsset jsonFile;
        private HeroAnimationSet targetSet;

        private HeroAnim selectedAnim;
        private string animationTag = "Idle";
        private bool importAsLegs;

        private int frameCount = 1;
        private bool useStartFrame;
        private int startFrame = 1;

        private static readonly Regex FrameRegex = new Regex(
            @"_(?<tag>[^_<>]+)(?:<(?<dir>DR|DL|UR|UL|R|L|U|D)>)?_(?<frame>\d+)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        [MenuItem("Tools/Hero/Aseprite Animation Importer")]
        private static void OpenWindow()
        {
            GetWindow<HeroAsepriteImporter>(
                "Hero Animation Importer"
            );
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField(
                "PNG + JSON → HeroAnimationSet",
                EditorStyles.boldLabel
            );

            EditorGUILayout.Space();

            sourceAsset = EditorGUILayout.ObjectField(
                "Sprite-Sheet PNG",
                sourceAsset,
                typeof(UnityEngine.Object),
                false
            );

            jsonFile = (TextAsset)EditorGUILayout.ObjectField(
                "Aseprite JSON",
                jsonFile,
                typeof(TextAsset),
                false
            );

            targetSet = (HeroAnimationSet)EditorGUILayout.ObjectField(
                "Animation Set",
                targetSet,
                typeof(HeroAnimationSet),
                false
            );

            EditorGUILayout.Space();

            selectedAnim = (HeroAnim)EditorGUILayout.EnumPopup(
                "HeroAnim",
                selectedAnim
            );

            animationTag = EditorGUILayout.TextField(
                "Sprite-Animationstag",
                animationTag
            );

            importAsLegs = EditorGUILayout.Toggle(
                "Als Beinanimation importieren",
                importAsLegs
            );

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Frame-Bereich",
                EditorStyles.boldLabel
            );

            frameCount = Mathf.Max(
                1,
                EditorGUILayout.IntField("Frame Count", frameCount)
            );

            useStartFrame = EditorGUILayout.Toggle(
                "Start Frame angeben",
                useStartFrame
            );

            if (useStartFrame)
            {
                startFrame = Mathf.Max(
                    1,
                    EditorGUILayout.IntField("Start Frame", startFrame)
                );
            }

            EditorGUILayout.HelpBox(
                "Die Sprite-Namen müssen dem Schema " +
                "'PNG-Dateiname_1', 'PNG-Dateiname_2' usw. folgen.\n\n" +
                "Frame Count bestimmt die Anzahl der JSON-Einträge " +
                "im gewählten Bereich. Fehlende Sprites werden " +
                "übersprungen.\n\n" +
                "Vorhandene Clips werden ergänzt, soweit ihre Frames " +
                "über die Clip-Listen zugänglich sind.",
                MessageType.Info
            );

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(
                sourceAsset == null ||
                jsonFile == null ||
                targetSet == null ||
                string.IsNullOrWhiteSpace(animationTag)))
            {
                if (GUILayout.Button("Animation importieren"))
                {
                    ImportAnimation(
                        frameCount,
                        useStartFrame ? startFrame : (int?)null
                    );
                }
            }
        }

        private void ImportAnimation(
            int requestedFrameCount,
            int? requestedStartFrame = null)
        {
            if (sourceAsset == null ||
                jsonFile == null ||
                targetSet == null)
            {
                Debug.LogError(
                    "Bitte PNG, JSON und Animation Set auswählen."
                );
                return;
            }

            if (requestedFrameCount < 1)
            {
                Debug.LogError(
                    "Frame Count muss mindestens 1 sein."
                );
                return;
            }

            string path = AssetDatabase.GetAssetPath(sourceAsset);

            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("Ungültiger Asset-Pfad.");
                return;
            }

            if (!string.Equals(
                Path.GetExtension(path),
                ".png",
                StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError(
                    "Als Quelle muss eine PNG-Datei ausgewählt werden."
                );
                return;
            }

            Sprite[] sprites = AssetDatabase
                .LoadAllAssetsAtPath(path)
                .OfType<Sprite>()
                .ToArray();

            if (sprites.Length == 0)
            {
                Debug.LogWarning(
                    $"Keine Sprite-Subassets gefunden: {path}. " +
                    "Prüfe, ob die PNG im Sprite Mode 'Multiple' " +
                    "importiert wurde."
                );
                return;
            }

            Dictionary<string, Sprite> spriteMapping;

            try
            {
                spriteMapping = BuildSpriteMapping(
                    sprites,
                    jsonFile,
                    requestedFrameCount,
                    requestedStartFrame
                );
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"Fehler beim Erstellen der Sprite-Zuordnung: " +
                    exception.Message
                );
                return;
            }

            var grouped = new Dictionary<
                FacingDirection,
                SortedDictionary<int, Sprite>
            >();

            foreach (KeyValuePair<string, Sprite> entry in spriteMapping)
            {
                // Tag, Richtung und Animationsframe stammen aus
                // dem ursprünglichen JSON-Namen.
                Match match = FrameRegex.Match(entry.Key);

                if (!match.Success)
                {
                    Debug.LogWarning(
                        $"JSON-Frame kann nicht interpretiert werden: " +
                        $"'{entry.Key}'"
                    );
                    continue;
                }

                string tag = match.Groups["tag"].Value;

                if (!string.Equals(
                    tag,
                    animationTag,
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int animationFrameIndex = int.Parse(
                    match.Groups["frame"].Value
                );

                FacingDirection direction = ParseDirection(
                    match.Groups["dir"].Value
                );

                if (!grouped.TryGetValue(
                    direction,
                    out SortedDictionary<int, Sprite> frames))
                {
                    frames = new SortedDictionary<int, Sprite>();
                    grouped.Add(direction, frames);
                }

                if (frames.ContainsKey(animationFrameIndex))
                {
                    Debug.LogWarning(
                        $"Doppelter Frame {animationFrameIndex} " +
                        $"für {tag}/{direction}: '{entry.Key}'"
                    );
                    continue;
                }

                frames.Add(animationFrameIndex, entry.Value);
            }

            if (grouped.Count == 0)
            {
                Debug.LogWarning(
                    $"Keine Frames für Animationstag '{animationTag}' " +
                    "im ausgewählten Frame-Bereich gefunden."
                );
                return;
            }

            // Vorhandenen Clip abrufen, sofern die Clip-Listen
            // in HeroAnimationSet öffentlich verfügbar sind.
            //
            // Falls deine Listen anders heißen oder privat sind,
            // muss der Zugriff an HeroAnimationSet angepasst werden.
            List<HeroClip> clipList = importAsLegs
                ? targetSet.legClips
                : targetSet.bodyClips;

            HeroClip existingClip = clipList?
                .FirstOrDefault(clip => clip.anim == selectedAnim);

            DirectionalFrames directionalFrames =
                existingClip?.frames ?? new DirectionalFrames();

            // Nur Richtungen ergänzen, für die neue Frames
            // gefunden wurden. Nicht importierte Richtungen bleiben
            // unverändert.
            foreach (KeyValuePair<
                FacingDirection,
                SortedDictionary<int, Sprite>> entry in grouped)
            {
                Sprite[] importedSprites = entry.Value.Values.ToArray();

                switch (entry.Key)
                {
                    case FacingDirection.Up:
                        directionalFrames.up = MergeFrames(
                            directionalFrames.up, importedSprites);
                        break;

                    case FacingDirection.UpRight:
                        directionalFrames.upRight = MergeFrames(
                            directionalFrames.upRight, importedSprites);
                        break;

                    case FacingDirection.Right:
                        directionalFrames.right = MergeFrames(
                            directionalFrames.right, importedSprites);
                        break;

                    case FacingDirection.DownRight:
                        directionalFrames.downRight = MergeFrames(
                            directionalFrames.downRight, importedSprites);
                        break;

                    case FacingDirection.Down:
                        directionalFrames.down = MergeFrames(
                            directionalFrames.down, importedSprites);
                        break;

                    case FacingDirection.DownLeft:
                        directionalFrames.downLeft = MergeFrames(
                            directionalFrames.downLeft, importedSprites);
                        break;

                    case FacingDirection.Left:
                        directionalFrames.left = MergeFrames(
                            directionalFrames.left, importedSprites);
                        break;

                    case FacingDirection.UpLeft:
                        directionalFrames.upLeft = MergeFrames(
                            directionalFrames.upLeft, importedSprites);
                        break;
                }
            }

            HeroClip clip = existingClip ?? new HeroClip
            {
                anim = selectedAnim,
                loop = selectedAnim != HeroAnim.Dead,
                timePerFrame = 0f,
                eventFrame = -1
            };

            clip.frames = directionalFrames;

            Undo.RecordObject(targetSet, "Import Hero Animation");

            if (importAsLegs)
                targetSet.EditorUpsertLegClip(clip);
            else
                targetSet.EditorUpsertBodyClip(clip);

            EditorUtility.SetDirty(targetSet);
            AssetDatabase.SaveAssets();

            int importedCount = grouped.Values.Sum(
                frames => frames.Count
            );

            Debug.Log(
                $"Animation '{animationTag}' importiert: " +
                $"{importedCount} Frames in {grouped.Count} Richtungen. " +
                $"Ziel: {targetSet.name}, Animation: {selectedAnim}."
            );
        }

        /// <summary>
        /// Ergänzt Frames positionsbasiert:
        /// Vorhandene Einträge bleiben bestehen, neue Einträge
        /// überschreiben Positionen, die importiert wurden.
        /// </summary>
        private static Sprite[] MergeFrames(
            Sprite[] existingFrames,
            Sprite[] importedFrames)
        {
            if (existingFrames == null || existingFrames.Length == 0)
                return importedFrames;

            if (importedFrames == null || importedFrames.Length == 0)
                return existingFrames;

            var merged = existingFrames.ToList();

            for (int i = 0; i < importedFrames.Length; i++)
            {
                if (i < merged.Count)
                    merged[i] = importedFrames[i];
                else
                    merged.Add(importedFrames[i]);
            }

            return merged.ToArray();
        }

        private Dictionary<string, Sprite> BuildSpriteMapping(
            Sprite[] sprites,
            TextAsset jsonAsset,
            int requestedFrameCount,
            int? requestedStartFrame = null)
        {
            var mapping = new Dictionary<string, Sprite>();

            JObject root = JObject.Parse(jsonAsset.text);
            JObject jsonFrames = root["frames"] as JObject;

            if (jsonFrames == null)
            {
                throw new Exception(
                    "Das JSON enthält kein erwartetes 'frames'-Objekt."
                );
            }

            string pngPath = AssetDatabase.GetAssetPath(sourceAsset);
            string spritePrefix = Path.GetFileNameWithoutExtension(
                pngPath
            );

            var spritesByName = sprites
                .GroupBy(sprite => sprite.name)
                .ToDictionary(
                    group => group.Key,
                    group => group.First()
                );

            int firstFrame = Mathf.Max(1, requestedStartFrame ?? 1);
            int lastFrameExclusive = firstFrame + requestedFrameCount;

            int jsonFrameIndex = 0;

            foreach (JProperty frameEntry in jsonFrames.Properties())
            {
                jsonFrameIndex++;

                if (jsonFrameIndex < firstFrame)
                    continue;

                if (jsonFrameIndex >= lastFrameExclusive)
                    break;

                string expectedSpriteName =
                    $"{spritePrefix}_{jsonFrameIndex}";

                if (spritesByName.TryGetValue(
                    expectedSpriteName,
                    out Sprite sprite))
                {
                    mapping[frameEntry.Name] = sprite;
                }
                else
                {
                    Debug.LogWarning(
                        $"Sprite '{expectedSpriteName}' fehlt. " +
                        $"JSON-Frame '{frameEntry.Name}' " +
                        $"(Index {jsonFrameIndex}) wird übersprungen."
                    );
                }
            }

            return mapping;
        }

        private static FacingDirection ParseDirection(string suffix)
        {
            switch (suffix.ToUpperInvariant())
            {
                case "U":
                    return FacingDirection.Up;
                case "UR":
                    return FacingDirection.UpRight;
                case "R":
                    return FacingDirection.Right;
                case "DR":
                    return FacingDirection.DownRight;
                case "D":
                    return FacingDirection.Down;
                case "DL":
                    return FacingDirection.DownLeft;
                case "L":
                    return FacingDirection.Left;
                case "UL":
                    return FacingDirection.UpLeft;
                default:
                    return FacingDirection.Down;
            }
        }
    }
}
