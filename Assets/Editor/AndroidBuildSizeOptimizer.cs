using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class AndroidBuildSizeOptimizer
{
    private const string CardsFolder = "Assets/Resources/卡牌";
    private const string CharacterFolder = "Assets/Resources/Character";
    private const string UiFolder = "Assets/Resources/UI";
    private const string MusicFolder = "Assets/Resources/Music";

    [MenuItem("Tools/Android/Optimize Build Size")]
    public static void OptimizeAndroidBuildSize()
    {
        ConfigureAndroidPlayerSettings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("安卓包体优化完成。已同步安卓构建配置，未再改动图片和 BGM 质量。");
    }

    [MenuItem("Tools/Android/Restore Original Media Quality")]
    public static void RestoreOriginalMediaQuality()
    {
        int changedAssetCount = 0;

        changedAssetCount += RestoreSpriteFolder(CardsFolder);
        changedAssetCount += RestoreSpriteFolder(CharacterFolder);
        changedAssetCount += RestoreSpriteFolder(UiFolder);
        changedAssetCount += RestoreMusicFolder();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"已恢复图片和 BGM 的原有质量设置，共更新 {changedAssetCount} 个资源。");
    }

    private static void ConfigureAndroidPlayerSettings()
    {
        PlayerSettings.Android.minifyRelease = true;
        PlayerSettings.Android.minifyDebug = false;

        // Medium stripping usually offers a good size reduction without being as risky as High.
        SetManagedStrippingLevelToMedium();
    }

    private static void SetManagedStrippingLevelToMedium()
    {
#if UNITY_2021_2_OR_NEWER
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Medium);
#else
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Medium);
#endif
    }

    private static int RestoreMusicFolder()
    {
        int changedCount = 0;
        foreach (string assetPath in EnumerateAssets(MusicFolder, "t:AudioClip"))
        {
            AudioImporter importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
            if (importer == null)
            {
                continue;
            }

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            bool changed = false;

            if (settings.loadType != AudioClipLoadType.DecompressOnLoad)
            {
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                changed = true;
            }

            if (settings.compressionFormat != AudioCompressionFormat.MP3)
            {
                settings.compressionFormat = AudioCompressionFormat.MP3;
                changed = true;
            }

            if (Mathf.Abs(settings.quality - 1f) > 0.001f)
            {
                settings.quality = 1f;
                changed = true;
            }

            if (settings.preloadAudioData)
            {
                settings.preloadAudioData = false;
                changed = true;
            }

            if (importer.loadInBackground)
            {
                importer.loadInBackground = false;
                changed = true;
            }

            if (changed == false)
            {
                continue;
            }

            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
            changedCount++;
        }

        return changedCount;
    }

    private static int RestoreSpriteFolder(string folderPath)
    {
        int changedCount = 0;
        foreach (string assetPath in EnumerateAssets(folderPath, "t:Texture2D"))
        {
            if (TryGetTextureImporter(assetPath, out TextureImporter importer) == false)
            {
                continue;
            }

            if (RestoreAndroidTextureSettings(importer))
            {
                changedCount++;
            }
        }

        return changedCount;
    }

    private static bool RestoreAndroidTextureSettings(TextureImporter importer)
    {
        TextureImporterPlatformSettings androidSettings = importer.GetPlatformTextureSettings("Android");
        bool changed = false;

        if (androidSettings.overridden)
        {
            androidSettings.overridden = false;
            changed = true;
        }

        if (androidSettings.maxTextureSize != 2048)
        {
            androidSettings.maxTextureSize = 2048;
            changed = true;
        }

        if (androidSettings.textureCompression != TextureImporterCompression.Compressed)
        {
            androidSettings.textureCompression = TextureImporterCompression.Compressed;
            changed = true;
        }

        if (androidSettings.compressionQuality != 50)
        {
            androidSettings.compressionQuality = 50;
            changed = true;
        }

        if (androidSettings.crunchedCompression)
        {
            androidSettings.crunchedCompression = false;
            changed = true;
        }

        if (changed == false)
        {
            return false;
        }

        importer.SetPlatformTextureSettings(androidSettings);
        importer.SaveAndReimport();
        return true;
    }

    private static bool TryGetTextureImporter(string assetPath, out TextureImporter importer)
    {
        importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        return importer != null;
    }

    private static IEnumerable<string> EnumerateAssets(string folderPath, string filter)
    {
        string[] guids = AssetDatabase.FindAssets(filter, new[] { folderPath });
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath))
            {
                continue;
            }

            yield return assetPath;
        }
    }
}
