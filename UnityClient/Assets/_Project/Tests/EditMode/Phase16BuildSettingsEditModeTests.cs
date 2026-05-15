#if UNITY_EDITOR
using System.IO;
using System.Linq;
using NUnit.Framework;
using TokenForge.Editor;
using TokenForge.Client.UI;
using UnityEditor;
using UnityEngine;

namespace TokenForge.Client.Tests
{
    public sealed class Phase16BuildSettingsEditModeTests
    {
        private const string BootstrapScenePath = "Assets/_Project/Scenes/Bootstrap.unity";
        private const string BootstrapRootPrefabPath = "Assets/_Project/Prefabs/UI/BootstrapRoot.prefab";

        [Test]
        public void BootstrapSceneAndPrefabAssetsExistAndAreInBuildSettings()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapScenePath));
            Assert.IsTrue(File.Exists(BootstrapScenePath + ".meta"));

            var buildScene = EditorBuildSettings.scenes.FirstOrDefault(scene => scene.path == BootstrapScenePath);
            Assert.IsNotNull(buildScene);
            Assert.IsTrue(buildScene.enabled);

            AssertRequiredPrefab<BootstrapRootView>(BootstrapRootPrefabPath);
            AssertRequiredPrefab<AccountPanelView>("Assets/_Project/Prefabs/UI/AccountPanel.prefab");
            AssertRequiredPrefab<ActivityAnalysisPanelView>("Assets/_Project/Prefabs/UI/ActivityAnalysisPanel.prefab");
            AssertRequiredPrefab<ApprovedLocationsPanelView>("Assets/_Project/Prefabs/UI/ApprovedLocationsPanel.prefab");
            AssertRequiredPrefab<ReviewPanelView>("Assets/_Project/Prefabs/UI/ReviewPanel.prefab");
            AssertRequiredPrefab<SafeSyncPanelView>("Assets/_Project/Prefabs/UI/SafeSyncPanel.prefab");
            AssertRequiredPrefab<RecentSessionsPanelView>("Assets/_Project/Prefabs/UI/RecentSessionsPanel.prefab");
            AssertRequiredPrefab<PrivacyNoticePanelView>("Assets/_Project/Prefabs/UI/PrivacyNoticePanel.prefab");

            var rootPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BootstrapRootPrefabPath);
            var root = rootPrefab.GetComponent<BootstrapRootView>();
            Assert.IsTrue(root.ValidateReferences(out var error), error);
        }

        [Test]
        public void LocalGeneratedArtifactsAreIgnoredButSourceAssetsAreNot()
        {
            var repoRoot = FindRepoRoot();
            var ignore = File.ReadAllText(Path.Combine(repoRoot, ".gitignore"));
            Assert.That(ignore, Does.Contain("[Bb]uilds/"));
            Assert.That(ignore, Does.Contain("[Bb]uild/"));
            Assert.That(ignore, Does.Contain("[Dd]erived[Dd]ata/"));
            Assert.That(ignore, Does.Contain("artifacts/client-contract/*.json"));
            Assert.That(ignore, Does.Contain("tokenforge-approved-locations.local.json"));
            Assert.That(ignore, Does.Contain("tokenforge-session"));
            Assert.That(ignore, Does.Contain("tokenforge-token"));

            Assert.That(ignore, Does.Not.Contain("Assets/_Project/Prefabs/UI/*.prefab"));
            Assert.That(ignore, Does.Not.Contain("Assets/_Project/Scenes/Bootstrap.unity"));
            Assert.That(ignore, Does.Not.Contain("*.meta"));
        }

        [Test]
        public void MacOSBuildSmokeCommandParsesSafeArgumentsAndValidatesProject()
        {
            MacOSBuildSmokeCommand.ValidateProjectState();

            var args = new[]
            {
                "-batchmode",
                "-buildOutput",
                "/tmp/tokenforge-macos-build/TokenForge.app",
                "-developmentBuild",
                "true",
                "-cleanBuild",
                "false"
            };

            Assert.AreEqual(
                "/tmp/tokenforge-macos-build/TokenForge.app",
                MacOSBuildSmokeCommand.GetArgument(args, "-buildOutput", string.Empty));
            Assert.IsTrue(MacOSBuildSmokeCommand.GetBoolArgument(args, "-developmentBuild", false));
            Assert.IsFalse(MacOSBuildSmokeCommand.GetBoolArgument(args, "-cleanBuild", true));
        }

        [Test]
        public void BuildAndLaunchScriptsDoNotEchoCredentialEnvironmentVariables()
        {
            var repoRoot = FindRepoRoot();
            AssertScriptDoesNotEchoCredentials(Path.Combine(repoRoot, "scripts/build-macos-smoke.sh"));
            AssertScriptDoesNotEchoCredentials(Path.Combine(repoRoot, "scripts/run-macos-smoke.sh"));

            var buildCommand = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Editor/MacOSBuildSmokeCommand.cs"));
            Assert.That(buildCommand, Does.Not.Contain("AccessToken"));
            Assert.That(buildCommand, Does.Not.Contain("RefreshToken"));
            Assert.That(buildCommand, Does.Not.Contain("Password"));
            Assert.That(buildCommand, Does.Not.Contain("request body"));
            Assert.That(buildCommand, Does.Not.Contain("response body"));
        }

        private static void AssertRequiredPrefab<T>(string path) where T : Component
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, path);
            Assert.IsTrue(File.Exists(path + ".meta"), path + ".meta");
            Assert.IsNotNull(prefab.GetComponent<T>(), typeof(T).Name + " missing on " + path);
        }

        private static void AssertScriptDoesNotEchoCredentials(string path)
        {
            var script = File.ReadAllText(path);
            Assert.That(script, Does.Not.Contain("TOKENFORGE_LIVE_PASSWORD"));
            Assert.That(script, Does.Not.Contain("TOKENFORGE_LIVE_EMAIL"));
            Assert.That(script, Does.Not.Contain("TOKENFORGE_LIVE_SIGNUP_PASSWORD"));
            Assert.That(script, Does.Not.Contain("TOKENFORGE_ACCESS_TOKEN"));
            Assert.That(script, Does.Not.Contain("TOKENFORGE_REFRESH_TOKEN"));
        }

        private static string FindRepoRoot()
        {
            var current = Directory.GetCurrentDirectory();
            if (File.Exists(Path.Combine(current, ".gitignore")))
            {
                return current;
            }

            var parent = Directory.GetParent(current)?.FullName;
            if (!string.IsNullOrWhiteSpace(parent) && File.Exists(Path.Combine(parent, ".gitignore")))
            {
                return parent;
            }

            Assert.Fail("Could not locate repository root.");
            return current;
        }
    }
}
#endif
