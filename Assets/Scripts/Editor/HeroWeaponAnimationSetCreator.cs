namespace TowerDefense.EditorTools
{

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;
using UnityEditor.U2D.Sprites;

public class HeroWeaponAnimationSetCreator : EditorWindow
{
    private const int SheetCount = 5;

    [Serializable]
    private class SpriteSheetPair
    {
        public string label;
        public Texture2D source;
        public Texture2D target;

        public SpriteSheetPair(string label)
        {
            this.label = label;
        }
    }

    [SerializeField] private HeroAnimationSet sourceAnimationSet;
    [SerializeField] private string newAssetName = "HeroAnimationSet_NewWeapon";

    [SerializeField]
    private SpriteSheetPair[] sheets =
    {
        new SpriteSheetPair("Movement"),
        new SpriteSheetPair("Move Actions"),
        new SpriteSheetPair("Attacks"),
        new SpriteSheetPair("Charged Attacks"),
        new SpriteSheetPair("Idle / Combat / Block / Dead / Hit")
    };

    [MenuItem("Tools/Hero/Create Weapon Animation Set")]
    private static void OpenWindow()
    {
        GetWindow<HeroWeaponAnimationSetCreator>(
            "Weapon Animation Set");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "Create Weapon Animation Set",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Wähle das bestehende Animation Set und die fünf PNG-Paare. " +
            "Die PNGs müssen dieselbe Auflösung und Sprite-Anordnung haben. " +
            "Das Original-Set wird nicht verändert.",
            MessageType.Info);

        EditorGUILayout.Space();

        sourceAnimationSet =
            (HeroAnimationSet)EditorGUILayout.ObjectField(
                "Vorlagen-Animation-Set",
                sourceAnimationSet,
                typeof(HeroAnimationSet),
                false);

        newAssetName = EditorGUILayout.TextField(
            "Name des neuen Sets",
            newAssetName);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(
            "Sprite-Sheet-Paare",
            EditorStyles.boldLabel);

        foreach (SpriteSheetPair pair in sheets)
        {
            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox);

            EditorGUILayout.LabelField(
                pair.label,
                EditorStyles.boldLabel);

            pair.source = (Texture2D)EditorGUILayout.ObjectField(
                "Original (Two-Hand)",
                pair.source,
                typeof(Texture2D),
                false);

            pair.target = (Texture2D)EditorGUILayout.ObjectField(
                "Neue Waffe",
                pair.target,
                typeof(Texture2D),
                false);

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(!CanCreate()))
        {
            if (GUILayout.Button(
                    "Neues Animation Set erstellen",
                    GUILayout.Height(32)))
            {
                CreateAnimationSet();
            }
        }
    }

    private bool CanCreate()
    {
        if (sourceAnimationSet == null ||
            string.IsNullOrWhiteSpace(newAssetName) ||
            sheets == null ||
            sheets.Length != SheetCount)
        {
            return false;
        }

        foreach (SpriteSheetPair pair in sheets)
        {
            if (pair == null ||
                pair.source == null ||
                pair.target == null ||
                pair.source == pair.target)
            {
                return false;
            }
        }

        return true;
    }

    private void CreateAnimationSet()
    {
        string outputPath = EditorUtility.SaveFilePanelInProject(
            "Neues Hero Animation Set speichern",
            newAssetName,
            "asset",
            "Wähle den Speicherort für das neue Animation Set.");

        if (string.IsNullOrEmpty(outputPath))
            return;

        try
        {
            Dictionary<string, Dictionary<Rect, Sprite>> spriteMaps =
                new Dictionary<string, Dictionary<Rect, Sprite>>();


            foreach (SpriteSheetPair pair in sheets)
            {
                ValidateTextureDimensions(pair);

                // 1. Ziel-PNG anhand der Original-PNG neu slicen.
                CopySpriteMetadata(pair.source, pair.target);

                // 2. Erst nach dem Slicing die Sprite-Zuordnung aufbauen.
                ValidateSlicedSprites(pair);

                spriteMaps.Add(
                    AssetDatabase.GetAssetPath(pair.source),
                    BuildTargetSpriteMap(pair.target));
            }


            HeroAnimationSet newSet =
                CreateInstance<HeroAnimationSet>();

            EditorUtility.CopySerialized(sourceAnimationSet, newSet);

            newSet.name = Path.GetFileNameWithoutExtension(outputPath);

            newSet.EditorSetBodyClips(
                CloneClips(sourceAnimationSet.bodyClips, spriteMaps));

            // Leg-Clips ebenfalls kopieren, falls sie vorhanden sind.
            foreach (HeroClip legClip in sourceAnimationSet.legClips)
            {
                if (legClip == null)
                    continue;

                newSet.EditorUpsertLegClip(
                    CloneClip(legClip, spriteMaps));
            }

            AssetDatabase.CreateAsset(newSet, outputPath);
            EditorUtility.SetDirty(newSet);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = newSet;
            EditorGUIUtility.PingObject(newSet);

            Debug.Log(
                $"Neues HeroAnimationSet erstellt: {outputPath}",
                newSet);
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "Das neue HeroAnimationSet konnte nicht erstellt werden.\n" +
                exception);
        }
    }


    private static void ValidateTextureDimensions(SpriteSheetPair pair)
    {
        string sourcePath = AssetDatabase.GetAssetPath(pair.source);
        string targetPath = AssetDatabase.GetAssetPath(pair.target);

        TextureImporter sourceImporter =
            AssetImporter.GetAtPath(sourcePath) as TextureImporter;

        TextureImporter targetImporter =
            AssetImporter.GetAtPath(targetPath) as TextureImporter;

        if (sourceImporter == null || targetImporter == null)
        {
            throw new InvalidOperationException(
                $"TextureImporter fehlt bei '{pair.label}'.");
        }

        if (sourceImporter.spriteImportMode != SpriteImportMode.Multiple ||
            targetImporter.spriteImportMode != SpriteImportMode.Multiple)
        {
            throw new InvalidOperationException(
                $"Beide PNGs müssen Sprite Mode Multiple verwenden: {pair.label}");
        }

        if (pair.source.width != pair.target.width ||
            pair.source.height != pair.target.height)
        {
            throw new InvalidOperationException(
                $"Die Auflösungen stimmen nicht überein: {pair.label}");
        }
    }

    private static void ValidateSlicedSprites(SpriteSheetPair pair)
    {
        Sprite[] sourceSprites = LoadSprites(pair.source);
        Sprite[] targetSprites = LoadSprites(pair.target);

        if (sourceSprites.Length == 0 ||
            sourceSprites.Length != targetSprites.Length)
        {
            throw new InvalidOperationException(
                $"Sprite-Anzahl stimmt nach dem Slicing nicht überein: " +
                $"{pair.label}. Original: {sourceSprites.Length}, " +
                $"Ziel: {targetSprites.Length}.");
        }

        Dictionary<Rect, Sprite> targetMap =
            BuildTargetSpriteMap(pair.target);

        foreach (Sprite sourceSprite in sourceSprites)
        {
            if (!targetMap.ContainsKey(sourceSprite.rect))
            {
                throw new InvalidOperationException(
                    $"Sprite-Rect fehlt nach dem Slicing: " +
                    $"{sourceSprite.name} in {pair.label}.");
            }
        }
    }

    private static Sprite[] LoadSprites(Texture2D texture)
    {
        string path = AssetDatabase.GetAssetPath(texture);
        UnityEngine.Object[] assets =
            AssetDatabase.LoadAllAssetsAtPath(path);

        List<Sprite> sprites = new List<Sprite>();

        foreach (UnityEngine.Object asset in assets)
        {
            if (asset is Sprite sprite)
                sprites.Add(sprite);
        }

        return sprites.ToArray();
    }

    private static Dictionary<Rect, Sprite> BuildTargetSpriteMap(
        Texture2D targetTexture)
    {
        Dictionary<Rect, Sprite> map =
            new Dictionary<Rect, Sprite>();

        foreach (Sprite sprite in LoadSprites(targetTexture))
        {
            if (map.ContainsKey(sprite.rect))
            {
                throw new InvalidOperationException(
                    $"Mehrere Sprites besitzen dasselbe Rect: " +
                    $"{targetTexture.name} / {sprite.rect}");
            }

            map.Add(sprite.rect, sprite);
        }

        return map;
    }

    private static void CopySpriteMetadata(
        Texture2D sourceTexture,
        Texture2D targetTexture)
    {
        string sourcePath = AssetDatabase.GetAssetPath(sourceTexture);
        string targetPath = AssetDatabase.GetAssetPath(targetTexture);

        TextureImporter sourceImporter =
            AssetImporter.GetAtPath(sourcePath) as TextureImporter;

        TextureImporter targetImporter =
            AssetImporter.GetAtPath(targetPath) as TextureImporter;

        if (sourceImporter == null || targetImporter == null)
        {
            throw new InvalidOperationException(
                "Sprite-Metadaten konnten nicht geladen werden.");
        }

        SpriteMetaData[] sourceMetadata = sourceImporter.spritesheet;

        if (sourceMetadata == null || sourceMetadata.Length == 0)
        {
            throw new InvalidOperationException(
                $"Das Original-Spritesheet enthält keine Slice-Daten: {sourcePath}");
        }

        targetImporter.spriteImportMode = SpriteImportMode.Multiple;

        // Vorhandene Ziel-Slices vollständig durch die Original-Slices ersetzen.
        targetImporter.spritesheet = new SpriteMetaData[0];
        EditorUtility.SetDirty(targetImporter);
        AssetDatabase.WriteImportSettingsIfDirty(targetPath);

        targetImporter.spritesheet = sourceMetadata;
        EditorUtility.SetDirty(targetImporter);
        AssetDatabase.WriteImportSettingsIfDirty(targetPath);

        AssetDatabase.ImportAsset(
            targetPath,
            ImportAssetOptions.ForceUpdate);

        Debug.Log(
            $"Sprite-Slicing übernommen: {sourcePath} -> {targetPath}. " +
            $"Erwartete Sprites: {sourceMetadata.Length}");
    }

    private static List<HeroClip> CloneClips(
        List<HeroClip> sourceClips,
        Dictionary<string, Dictionary<Rect, Sprite>> spriteMaps)
    {
        List<HeroClip> result = new List<HeroClip>();

        foreach (HeroClip clip in sourceClips)
        {
            if (clip != null)
                result.Add(CloneClip(clip, spriteMaps));
        }

        return result;
    }

    private static HeroClip CloneClip(
        HeroClip source,
        Dictionary<string, Dictionary<Rect, Sprite>> spriteMaps)
    {
        return new HeroClip
        {
            anim = source.anim,
            frames = CloneDirectionalFrames(source.frames, spriteMaps),
            upperBodyFrames = CloneDirectionalFrames(
                source.upperBodyFrames,
                spriteMaps),
            loop = source.loop,
            timePerFrame = source.timePerFrame,
            eventFrame = source.eventFrame,
            isChargeable = source.isChargeable,
            chargeLoopStartFrame = source.chargeLoopStartFrame,
            chargeLoopEndFrame = source.chargeLoopEndFrame
        };
    }

    private static DirectionalFrames CloneDirectionalFrames(
        DirectionalFrames source,
        Dictionary<string, Dictionary<Rect, Sprite>> spriteMaps)
    {
        if (source == null)
            return null;

        return new DirectionalFrames
        {
            up = ReplaceSprites(source.up, spriteMaps),
            upRight = ReplaceSprites(source.upRight, spriteMaps),
            right = ReplaceSprites(source.right, spriteMaps),
            downRight = ReplaceSprites(source.downRight, spriteMaps),
            down = ReplaceSprites(source.down, spriteMaps),
            downLeft = ReplaceSprites(source.downLeft, spriteMaps),
            left = ReplaceSprites(source.left, spriteMaps),
            upLeft = ReplaceSprites(source.upLeft, spriteMaps)
        };
    }

    private static Sprite[] ReplaceSprites(
        Sprite[] sourceSprites,
        Dictionary<string, Dictionary<Rect, Sprite>> spriteMaps)
    {
        if (sourceSprites == null)
            return null;

        Sprite[] result = new Sprite[sourceSprites.Length];

        for (int i = 0; i < sourceSprites.Length; i++)
        {
            Sprite sourceSprite = sourceSprites[i];

            if (sourceSprite == null)
                continue;

            string sourcePath =
                AssetDatabase.GetAssetPath(sourceSprite);

            if (!spriteMaps.TryGetValue(
                    sourcePath,
                    out Dictionary<Rect, Sprite> targetMap))
            {
                throw new InvalidOperationException(
                    $"Sprite '{sourceSprite.name}' gehört zu keiner " +
                    "der fünf ausgewählten Original-PNGs.");
            }

            if (!targetMap.TryGetValue(
                    sourceSprite.rect,
                    out Sprite targetSprite))
            {
                throw new InvalidOperationException(
                    $"Kein passendes Ziel-Sprite für " +
                    $"'{sourceSprite.name}' gefunden.");
            }

            result[i] = targetSprite;
        }

        return result;
    }
}

}