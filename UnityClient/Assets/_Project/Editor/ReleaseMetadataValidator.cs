#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
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
            ValidateReleaseDocs(report.Errors, report.Warnings, report.AppIconConfigured);
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

        private static void ValidateReleaseDocs(List<string> errors, List<string> warnings, bool appIconConfigured)
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

            if (!appIconConfigured)
            {
                RequireContains(errors, docs, "App icon", "release docs must document the app icon limitation when no icon is configured.");
                warnings.Add("No Standalone app icon is configured; documented as a known limitation.");
            }
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
                "*.unsupported-schema"
            };

            foreach (var entry in requiredEntries)
            {
                RequireContains(errors, ignore, entry, ".gitignore must ignore generated artifact pattern " + entry + ".");
            }
        }

        private static bool HasConfiguredStandaloneIcon()
        {
            var icons = PlayerSettings.GetIconsForTargetGroup(BuildTargetGroup.Standalone);
            if (icons == null || icons.Length == 0 || icons.All(icon => icon == null))
            {
                return false;
            }

            foreach (var icon in icons.Where(icon => icon != null))
            {
                var path = AssetDatabase.GetAssetPath(icon);
                if (string.IsNullOrWhiteSpace(path) || AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null)
                {
                    return false;
                }
            }

            return true;
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
