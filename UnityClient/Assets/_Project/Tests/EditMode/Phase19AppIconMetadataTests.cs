#if UNITY_EDITOR
using System.IO;
using System.Linq;
using NUnit.Framework;
using TokenForge.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace TokenForge.Client.Tests
{
    public sealed class Phase19AppIconMetadataTests
    {
        [Test]
        public void StandaloneAppIcon_IsConfiguredAndSourceControlled()
        {
            var report = ReleaseMetadataValidator.Validate();
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(ReleaseMetadataValidator.AppIconAssetPath);
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;

            Assert.IsTrue(report.AppIconConfigured, string.Join("; ", report.Errors));
            Assert.IsTrue(report.IsValid, string.Join("; ", report.Errors));
            Assert.IsNotNull(icon);
            Assert.IsTrue(File.Exists(Path.Combine(projectRoot, ReleaseMetadataValidator.AppIconAssetPath)));
            Assert.IsTrue(File.Exists(Path.Combine(projectRoot, ReleaseMetadataValidator.AppIconMetaPath)));
        }

        [Test]
        public void StandalonePlayerSettings_ReferencesExpectedIconAsset()
        {
            var groupIcons = PlayerSettings.GetIconsForTargetGroup(BuildTargetGroup.Standalone) ?? new Texture2D[0];
            var platformIconPaths = PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Standalone)
                .SelectMany(kind => PlayerSettings.GetPlatformIcons(NamedBuildTarget.Standalone, kind) ?? new PlatformIcon[0])
                .SelectMany(icon => icon.GetTextures() ?? new Texture2D[0])
                .Where(icon => icon != null)
                .Select(AssetDatabase.GetAssetPath);
            var paths = groupIcons.Where(icon => icon != null)
                .Select(AssetDatabase.GetAssetPath)
                .Concat(platformIconPaths)
                .ToArray();

            Assert.That(paths, Does.Contain(ReleaseMetadataValidator.AppIconAssetPath));
        }
    }
}
#endif
