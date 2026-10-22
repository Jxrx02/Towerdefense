namespace TowerDefense.EditorTools
{

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public class DominantColorPivotTool : EditorWindow
{
    private const string TargetColorOne = "#D9A066";
    private const string TargetColorTwo = "#EEC39A";

    private Color32 colorOne;
    private Color32 colorTwo;

    [SerializeField] private DefaultAsset targetFolder;
    [SerializeField] private bool includeSubfolders = true;

    [MenuItem("Tools/Sprites/Dominant Color Pivot")]
    private static void OpenWindow()
    {
        GetWindow<DominantColorPivotTool>(
            "Dominant Color Pivot"
        );
    }

    private void OnEnable()
    {
        ColorUtility.TryParseHtmlString(
            TargetColorOne,
            out Color parsedColorOne
        );

        ColorUtility.TryParseHtmlString(
            TargetColorTwo,
            out Color parsedColorTwo
        );

        colorOne = parsedColorOne;
        colorTwo = parsedColorTwo;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "Dominant Color Pivot",
            EditorStyles.boldLabel
        );

        EditorGUILayout.HelpBox(
            "Setzt den Pivot jedes Sprites auf den Schwerpunkt " +
            "der häufigsten der beiden Zielfarben.",
            MessageType.Info
        );

        EditorGUILayout.LabelField(
            "Zielfarbe 1",
            $"{TargetColorOne}  {colorOne}"
        );

        EditorGUILayout.LabelField(
            "Zielfarbe 2",
            $"{TargetColorTwo}  {colorTwo}"
        );

        targetFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            "PNG-Ordner",
            targetFolder,
            typeof(DefaultAsset),
            false
        );

        includeSubfolders = EditorGUILayout.Toggle(
            "Unterordner einbeziehen",
            includeSubfolders
        );

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(targetFolder == null))
        {
            if (GUILayout.Button("Pivots berechnen"))
                ProcessFolder();
        }
    }

    private void ProcessFolder()
    {
        string folderPath = AssetDatabase.GetAssetPath(targetFolder);

        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            Debug.LogError("Bitte einen gültigen Projektordner auswählen.");
            return;
        }

        string absoluteFolderPath = Path.GetFullPath(folderPath);

        string[] pngFiles = Directory.GetFiles(
            absoluteFolderPath,
            "*.png",
            includeSubfolders
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly
        );

        int processedSprites = 0;
        int skippedSprites = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            foreach (string absolutePath in pngFiles)
            {
                string assetPath = ToAssetPath(absolutePath);

     
                TextureImporter importer =
                    AssetImporter.GetAtPath(assetPath)
                    as TextureImporter;
                

                if (!(importer is { isReadable: true }))
                {
                    Debug.LogWarning(
                        $"Sprite benötigt Read/Write: {assetPath}"
                    );

                    skippedSprites++;
                    continue;
                }

                if (importer.textureType != TextureImporterType.Sprite)
                    continue;

                processedSprites += ProcessTexture(
                    assetPath,
                    importer,
                    ref skippedSprites
                );
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        Debug.Log(
            $"Pivot-Berechnung abgeschlossen. " +
            $"Verarbeitet: {processedSprites}, " +
            $"übersprungen: {skippedSprites}."
        );
    }

    private int ProcessTexture(
        string assetPath,
        TextureImporter importer,
        ref int skippedSprites)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
            assetPath
        );

        if (texture == null)
            return 0;

        SpriteMetaData[] spriteMetadata = importer.spritesheet;

        if (importer.spriteImportMode == SpriteImportMode.Single)
        {
            Vector2? pivot = CalculatePivot(
                texture,
                new RectInt(0, 0, texture.width, texture.height)
            );

            if (!pivot.HasValue)
            {
                skippedSprites++;
                return 0;
            }

            importer.spritePivot = pivot.Value;
           // importer. = (int)SpriteAlignment.Custom;

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();

            return 1;
        }

        if (spriteMetadata == null || spriteMetadata.Length == 0)
            return 0;

        int updatedSprites = 0;

        for (int i = 0; i < spriteMetadata.Length; i++)
        {
            Rect rect = spriteMetadata[i].rect;

            RectInt spriteRect = new RectInt(
                Mathf.RoundToInt(rect.x),
                Mathf.RoundToInt(rect.y),
                Mathf.RoundToInt(rect.width),
                Mathf.RoundToInt(rect.height)
            );

            Vector2? pivot = CalculatePivot(texture, spriteRect);

            if (!pivot.HasValue)
            {
                skippedSprites++;
                continue;
            }

            spriteMetadata[i].alignment = (int)SpriteAlignment.Custom;
            spriteMetadata[i].pivot = pivot.Value;

            updatedSprites++;
        }

        importer.spritesheet = spriteMetadata;
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();

        return updatedSprites;
    }

    private Vector2? CalculatePivot(
        Texture2D texture,
        RectInt spriteRect)
    {
        Color32[] pixels;

        try
        {
            pixels = texture.GetPixels32();
        }
        catch (UnityException)
        {
            return null;
        }

        int countOne = 0;
        int countTwo = 0;

        long sumXOne = 0;
        long sumYOne = 0;
        long sumXTwo = 0;
        long sumYTwo = 0;

        int minX = Mathf.Clamp(spriteRect.xMin, 0, texture.width);
        int maxX = Mathf.Clamp(spriteRect.xMax, 0, texture.width);
        int minY = Mathf.Clamp(spriteRect.yMin, 0, texture.height);
        int maxY = Mathf.Clamp(spriteRect.yMax, 0, texture.height);

        for (int y = minY; y < maxY; y++)
        {
            for (int x = minX; x < maxX; x++)
            {
                Color32 pixel = pixels[y * texture.width + x];

                if (pixel.a == 0)
                    continue;

                if (Matches(pixel, colorOne))
                {
                    countOne++;
                    sumXOne += x - spriteRect.x;
                    sumYOne += y - spriteRect.y;
                    continue;
                }

                if (!Matches(pixel, colorTwo))
                    continue;

                countTwo++;
                sumXTwo += x - spriteRect.x;
                sumYTwo += y - spriteRect.y;
            }
        }

        if (countOne == 0 && countTwo == 0)
            return null;

        bool useColorOne = countOne >= countTwo;

        long sumX = useColorOne ? sumXOne : sumXTwo;
        long sumY = useColorOne ? sumYOne : sumYTwo;
        int count = useColorOne ? countOne : countTwo;

        float pivotX = (float)sumX / count / spriteRect.width;
        float pivotY = (float)sumY / count / spriteRect.height;

        return new Vector2(pivotX, pivotY);
    }

    private static bool Matches(Color32 pixel, Color32 target)
    {
        return pixel.r == target.r
            && pixel.g == target.g
            && pixel.b == target.b;
    }

    private static string ToAssetPath(string absolutePath)
    {
        string normalizedPath = absolutePath.Replace('\\', '/');
        string normalizedAssetsPath = Path.GetFullPath("Assets")
            .Replace('\\', '/');

        if (!normalizedPath.StartsWith(
                normalizedAssetsPath + "/",
                StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        return "Assets" + normalizedPath.Substring(
            normalizedAssetsPath.Length
        );
    }
}

}