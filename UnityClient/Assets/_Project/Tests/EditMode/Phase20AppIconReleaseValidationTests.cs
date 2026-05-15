#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TokenForge.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace TokenForge.Client.Tests
{
    public sealed class Phase20AppIconReleaseValidationTests
    {
        [Test]
        public void ReleaseAppIcon_ExistsWithNonPlaceholderNameAndMetadata()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            var iconPath = Path.Combine(projectRoot, ReleaseMetadataValidator.AppIconAssetPath);
            var metaPath = Path.Combine(projectRoot, ReleaseMetadataValidator.AppIconMetaPath);
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(ReleaseMetadataValidator.AppIconAssetPath);
            var importer = AssetImporter.GetAtPath(ReleaseMetadataValidator.AppIconAssetPath) as TextureImporter;

            Assert.IsTrue(File.Exists(iconPath), ReleaseMetadataValidator.AppIconAssetPath);
            Assert.IsTrue(File.Exists(metaPath), ReleaseMetadataValidator.AppIconMetaPath);
            Assert.That(Path.GetFileName(ReleaseMetadataValidator.AppIconAssetPath), Does.Not.Contain("Placeholder").IgnoreCase);
            Assert.That(ReleaseMetadataValidator.AppIconAssetPath, Does.Not.Contain("Placeholder").IgnoreCase);
            Assert.IsFalse(File.Exists(Path.Combine(projectRoot, ReleaseMetadataValidator.PlaceholderAppIconAssetPath)));
            Assert.IsNotNull(icon);
            Assert.GreaterOrEqual(icon.width, 1024);
            Assert.GreaterOrEqual(icon.height, 1024);
            Assert.IsNotNull(importer);
            Assert.AreEqual(TextureImporterType.Default, importer.textureType);
            Assert.IsFalse(importer.mipmapEnabled);
            Assert.IsFalse(importer.alphaIsTransparency);
            Assert.GreaterOrEqual(importer.maxTextureSize, 1024);
        }

        [Test]
        public void StandalonePlayerSettings_ReferencesReleaseIconWhenUnityApiReportsIcons()
        {
            var iconPaths = (PlayerSettings.GetIconsForTargetGroup(BuildTargetGroup.Standalone) ?? Array.Empty<Texture2D>())
                .Where(icon => icon != null)
                .Select(AssetDatabase.GetAssetPath)
                .Concat(PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Standalone)
                    .SelectMany(kind => PlayerSettings.GetPlatformIcons(NamedBuildTarget.Standalone, kind) ?? Array.Empty<PlatformIcon>())
                    .SelectMany(icon => icon.GetTextures() ?? Array.Empty<Texture2D>())
                    .Where(icon => icon != null)
                    .Select(AssetDatabase.GetAssetPath))
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct()
                .ToArray();

            Assert.That(iconPaths, Does.Contain(ReleaseMetadataValidator.AppIconAssetPath));
            Assert.IsFalse(iconPaths.Any(path => path.IndexOf("Placeholder", StringComparison.OrdinalIgnoreCase) >= 0));
        }

        [Test]
        public void ReleaseMetadataValidator_RejectsPlaceholderIconPolicy()
        {
            var report = ReleaseMetadataValidator.Validate();

            Assert.IsTrue(report.IsValid, string.Join("; ", report.Errors));
            Assert.That(string.Join("\n", report.Errors), Does.Not.Contain("Placeholder"));
        }
    }
}
#endif
