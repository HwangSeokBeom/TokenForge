#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace TokenForge.Editor
{
    public static class Phase19AppIconConfigurator
    {
        public static void Configure()
        {
            try
            {
                ValidateFinalIconAsset();
                var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(ReleaseMetadataValidator.AppIconAssetPath);
                if (icon == null)
                {
                    throw new InvalidOperationException("App icon asset could not be loaded.");
                }

                ConfigureStandaloneIcon(icon);
                AssetDatabase.SaveAssets();
                Debug.Log("TokenForge release app icon configured.");

                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(0);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError("TokenForge release app icon configuration failed: " + exception.GetType().Name);
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                    return;
                }

                throw;
            }
        }

        private static void ConfigureStandaloneIcon(Texture2D icon)
        {
            var iconSizes = PlayerSettings.GetIconSizesForTargetGroup(BuildTargetGroup.Standalone);
            var legacyIcons = new Texture2D[Math.Max(1, iconSizes?.Length ?? 0)];
            for (var i = 0; i < legacyIcons.Length; i++)
            {
                legacyIcons[i] = icon;
            }

            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Standalone, legacyIcons);

            var kinds = PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Standalone);
            foreach (var kind in kinds)
            {
                var platformIcons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Standalone, kind);
                if (platformIcons == null || platformIcons.Length == 0)
                {
                    continue;
                }

                foreach (var platformIcon in platformIcons)
                {
                    platformIcon.SetTexture(icon);
                }

                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Standalone, kind, platformIcons);
            }
        }

        private static void ValidateFinalIconAsset()
        {
            var assetPath = ReleaseMetadataValidator.AppIconAssetPath;
            var absolutePath = Path.Combine(Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty, assetPath);
            if (!File.Exists(absolutePath))
            {
                throw new FileNotFoundException("Final release app icon is missing.", absolutePath);
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException("Final release app icon importer could not be loaded.");
            }

            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = false;
            importer.sRGBTexture = true;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }
    }
}
#endif
