#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace TokenForge.Editor
{
    public sealed class ReleaseMetadataValidationReport
    {
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public string ProductName { get; set; }
        public string CompanyName { get; set; }
        public string BundleIdentifier { get; set; }
        public string AppVersion { get; set; }
        public string BuildNumber { get; set; }
        public string BootstrapScenePath { get; set; }
        public string EntitlementsPath { get; set; }
        public bool AppIconConfigured { get; set; }
        public bool IsValid => Errors.Count == 0;
    }

    public static class ReleaseMetadataValidator
    {
        public const string ExpectedProductName = "TokenForge";
        public const string ExpectedCompanyName = "TokenForge";
        public const string ExpectedBundleIdentifier = "com.tokenforge.client";
        public const string ExpectedAppVersion = "0.18.0";
        public const string ExpectedBuildNumber = "18";
        public const string BootstrapScenePath = "Assets/_Project/Scenes/Bootstrap.unity";
        public const string EntitlementsRelativePath = "BuildSupport/macOS/TokenForge.entitlements";
        public const string ReleaseDocsRelativePath = "Docs/macos-release.md";
        public const string AppIconAssetPath = "Assets/_Project/Art/AppIcon/TokenForgeReleaseIcon.png";
        public const string AppIconMetaPath = AppIconAssetPath + ".meta";
        public const string PlaceholderAppIconAssetPath = "Assets/_Project/Art/AppIcon/TokenForgePlaceholderIcon.png";

        public static void ValidateForRelease()
        {
            var report = Validate();
            LogSafeSummary(report);

            if (!report.IsValid)
            {
                var message = "TokenForge release metadata validation failed: " + string.Join("; ", report.Errors);
                Debug.LogError(message);
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                    return;
                }

                throw new InvalidOperationException(message);
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }

        public static ReleaseMetadataValidationReport Validate()
        {
            var report = new ReleaseMetadataValidationReport
            {
                ProductName = PlayerSettings.productName,
                CompanyName = PlayerSettings.companyName,
                BundleIdentifier = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Standalone),
                AppVersion = PlayerSettings.bundleVersion,
                BuildNumber = ReadStandaloneBuildNumber(),
                BootstrapScenePath = BootstrapScenePath,
                EntitlementsPath = EntitlementsRelativePath,
                AppIconConfigured = HasConfiguredStandaloneIcon()
            };

            RequireEquals(report.Errors, "product name", ExpectedProductName, report.ProductName);
            RequireEquals(report.Errors, "company name", ExpectedCompanyName, report.CompanyName);
            RequireEquals(report.Errors, "bundle identifier", ExpectedBundleIdentifier, report.BundleIdentifier);
            RequireNonEmpty(report.Errors, "app version", report.AppVersion);
            RequireEquals(report.Errors, "app version", ExpectedAppVersion, report.AppVersion);
            RequireNonEmpty(report.Errors, "Standalone build number", report.BuildNumber);
            RequireEquals(report.Errors, "Standalone build number", ExpectedBuildNumber, report.BuildNumber);

            ValidateBootstrapScene(report.Errors);
            ValidateMacOSBuildTarget(report.Errors);
            ValidateEntitlements(report.Errors);
            ValidateAppIcon(report.Errors);
            ValidateReleaseDocs(report.Errors, report.Warnings);
            ValidateGitIgnore(report.Errors);

            return report;
        }

        private static void ValidateBootstrapScene(List<string> errors)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapScenePath) == null)
            {
                errors.Add("Bootstrap scene asset is missing.");
                return;
            }

            var buildScene = EditorBuildSettings.scenes.FirstOrDefault(scene => scene.path == BootstrapScenePath);
            if (buildScene == null || !buildScene.enabled)
            {
                errors.Add("Bootstrap scene is not enabled in build settings.");
            }
        }

        private static void ValidateMacOSBuildTarget(List<string> errors)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX))
            {
                errors.Add("macOS Standalone build target is not supported by this Unity installation.");
            }
        }

        private static void ValidateEntitlements(List<string> errors)
        {
            if (!File.Exists(RepoPath(EntitlementsRelativePath)))
            {
                errors.Add("macOS entitlements file is missing.");
            }
        }

        private static void ValidateAppIcon(List<string> errors)
        {
            if (File.Exists(Path.Combine(ProjectRoot(), PlaceholderAppIconAssetPath)))
            {
                errors.Add("Placeholder app icon asset must not be present in a release candidate.");
            }

            if (AppIconAssetPath.IndexOf("Placeholder", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                errors.Add("Release app icon path must not contain Placeholder.");
            }

            if (!File.Exists(Path.Combine(ProjectRoot(), AppIconAssetPath)))
            {
                errors.Add("macOS Standalone app icon asset is missing.");
                return;
            }

            if (!File.Exists(Path.Combine(ProjectRoot(), AppIconMetaPath)))
            {
                errors.Add("macOS Standalone app icon .meta file is missing.");
            }

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIconAssetPath);
            if (icon == null)
            {
                errors.Add("macOS Standalone app icon asset is not a valid Texture2D.");
            }
            else
            {
                if (icon.name.IndexOf("Placeholder", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    errors.Add("Release app icon asset name must not contain Placeholder.");
                }

                if (icon.width < 1024 || icon.height < 1024)
                {
                    errors.Add("Release app icon must be at least 1024x1024.");
                }
            }

            var importer = AssetImporter.GetAtPath(AppIconAssetPath) as TextureImporter;
            if (importer == null)
            {
                errors.Add("Release app icon importer metadata is missing or invalid.");
            }
            else
            {
                if (importer.maxTextureSize < 1024)
                {
                    errors.Add("Release app icon importer max texture size must be at least 1024.");
                }

                if (importer.alphaIsTransparency)
                {
                    errors.Add("Release app icon must import as an opaque app icon.");
                }
            }

            if (!HasConfiguredStandaloneIcon())
            {
                errors.Add("macOS Standalone app icon is not configured in PlayerSettings.");
                return;
            }

            var referencesExpectedIcon = StandaloneIconPaths()
                .Any(path => string.Equals(path, AppIconAssetPath, StringComparison.Ordinal));
            if (!referencesExpectedIcon)
            {
                errors.Add("macOS Standalone app icon must reference " + AppIconAssetPath + ".");
            }
        }

        private static void ValidateReleaseDocs(List<string> errors, List<string> warnings)
        {
            var docsPath = RepoPath(ReleaseDocsRelativePath);
            if (!File.Exists(docsPath))
            {
                errors.Add("macOS release documentation is missing.");
                return;
            }

            var docs = File.ReadAllText(docsPath);
            RequireContains(errors, docs, ExpectedBundleIdentifier, "release docs must document the bundle identifier.");
            RequireContains(errors, docs, "sign", "release docs must mention signing requirements.");
            RequireContains(errors, docs, "notar", "release docs must mention notarization requirements.");
            RequireContains(errors, docs, "schemaVersion", "release docs must mention persistence schema metadata.");
            RequireContains(errors, docs, AppIconAssetPath, "release docs must document the app icon asset path.");
            RequireContains(errors, docs, "original TokenForge release icon", "release docs must document the app icon license/source status.");
        }

        private static void ValidateGitIgnore(List<string> errors)
        {
            var ignorePath = RepoPath(".gitignore");
            if (!File.Exists(ignorePath))
            {
                errors.Add(".gitignore is missing.");
                return;
            }

            var ignore = File.ReadAllText(ignorePath);
            var requiredEntries = new[]
            {
                "[Bb]uilds/",
                "[Bb]uild/",
                "[Dd]erived[Dd]ata/",
                "artifacts/client-contract/*.json",
                "tokenforge-approved-locations.local.json",
                "tokenforge-session",
                "tokenforge-token",
                "*.zip",
                "*.dmg",
                "*.pkg",
                "*.corrupt",
                "*.unsupported-schema",
                "*.unsafe",
                "*.notarytool*",
                "*.credentials",
                "*.p12",
                "AuthKey_*.p8"
            };

            foreach (var entry in requiredEntries)
            {
                RequireContains(errors, ignore, entry, ".gitignore must ignore generated artifact pattern " + entry + ".");
            }
        }

        private static bool HasConfiguredStandaloneIcon()
        {
            return StandaloneIconPaths().Any();
        }

        private static IEnumerable<string> StandaloneIconPaths()
        {
            var icons = PlayerSettings.GetIconsForTargetGroup(BuildTargetGroup.Standalone);
            foreach (var path in ValidIconPaths(icons))
            {
                yield return path;
            }

            var kinds = PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Standalone);
            foreach (var kind in kinds)
            {
                var platformIcons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Standalone, kind);
                if (platformIcons == null)
                {
                    continue;
                }

                foreach (var path in ValidIconPaths(platformIcons.SelectMany(platformIcon => platformIcon.GetTextures() ?? new Texture2D[0])))
                {
                    yield return path;
                }
            }
        }

        private static IEnumerable<string> ValidIconPaths(IEnumerable<Texture2D> icons)
        {
            if (icons == null)
            {
                yield break;
            }

            foreach (var icon in icons.Where(icon => icon != null))
            {
                var path = AssetDatabase.GetAssetPath(icon);
                if (!string.IsNullOrWhiteSpace(path) && AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null)
                {
                    yield return path;
                }
            }
        }

        private static string ReadStandaloneBuildNumber()
        {
            var settingsPath = Path.Combine(ProjectRoot(), "ProjectSettings/ProjectSettings.asset");
            if (!File.Exists(settingsPath))
            {
                return string.Empty;
            }

            var yaml = File.ReadAllText(settingsPath);
            var match = Regex.Match(yaml, @"buildNumber:\s*\n(?:\s+[A-Za-z0-9_]+:\s*.*\n)*?\s+Standalone:\s*(?<value>[^\r\n]+)");
            return match.Success ? match.Groups["value"].Value.Trim() : string.Empty;
        }

        private static void LogSafeSummary(ReleaseMetadataValidationReport report)
        {
            Debug.Log("TokenForge release metadata validation summary");
            Debug.Log("Product name: " + report.ProductName);
            Debug.Log("Company name: " + report.CompanyName);
            Debug.Log("Bundle identifier: " + report.BundleIdentifier);
            Debug.Log("App version: " + report.AppVersion);
            Debug.Log("Build number: " + report.BuildNumber);
            Debug.Log("Bootstrap scene: " + report.BootstrapScenePath);
            Debug.Log("Entitlements: " + report.EntitlementsPath);
            Debug.Log("App icon configured: " + report.AppIconConfigured);
            Debug.Log("Warnings: " + report.Warnings.Count);
            Debug.Log("Result: " + (report.IsValid ? "success" : "failure"));
        }

        private static void RequireEquals(List<string> errors, string label, string expected, string actual)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                errors.Add(label + " must be " + expected + ".");
            }
        }

        private static void RequireNonEmpty(List<string> errors, string label, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add(label + " is required.");
            }
        }

        private static void RequireContains(List<string> errors, string content, string expected, string error)
        {
            if (content == null || content.IndexOf(expected, StringComparison.OrdinalIgnoreCase) < 0)
            {
                errors.Add(error);
            }
        }

        private static string RepoPath(string relativePath)
        {
            return Path.Combine(RepoRoot(), relativePath);
        }

        private static string ProjectRoot()
        {
            return Directory.GetParent(Application.dataPath)?.FullName;
        }

        private static string RepoRoot()
        {
            return Directory.GetParent(ProjectRoot())?.FullName;
        }
    }
}
#endif
