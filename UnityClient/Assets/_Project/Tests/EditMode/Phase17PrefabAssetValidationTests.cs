#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TokenForge.Client;
using TokenForge.Client.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TokenForge.Client.Tests
{
    public sealed class Phase17PrefabAssetValidationTests
    {
        private const string BootstrapScenePath = "Assets/_Project/Scenes/Bootstrap.unity";
        private const string BootstrapRootPrefabPath = "Assets/_Project/Prefabs/UI/BootstrapRoot.prefab";

        [Test]
        public void BootstrapScenePrefabAndPanelBindersAreValid()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapScenePath));
            Assert.IsTrue(EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path == BootstrapScenePath));

            var rootPrefab = AssertPrefabHas<BootstrapRootView>(BootstrapRootPrefabPath);
            var root = rootPrefab.GetComponent<BootstrapRootView>();
            Assert.IsTrue(root.ValidateReferences(out var error), error);
            Assert.IsNotNull(root.rootScrollRect);
            AssertPanel(root.accountPanel, "Assets/_Project/Prefabs/UI/AccountPanel.prefab");
            AssertPanel(root.activityAnalysisPanel, "Assets/_Project/Prefabs/UI/ActivityAnalysisPanel.prefab");
            AssertPanel(root.approvedLocationsPanel, "Assets/_Project/Prefabs/UI/ApprovedLocationsPanel.prefab");
            AssertPanel(root.reviewPanel, "Assets/_Project/Prefabs/UI/ReviewPanel.prefab");
            AssertPanel(root.safeSyncPanel, "Assets/_Project/Prefabs/UI/SafeSyncPanel.prefab");
            AssertPanel(root.recentSessionsPanel, "Assets/_Project/Prefabs/UI/RecentSessionsPanel.prefab");
            AssertPanel(root.privacyNoticePanel, "Assets/_Project/Prefabs/UI/PrivacyNoticePanel.prefab");
        }

        [Test]
        public void PrefabVisibleTextContainsPrivacyNoticeAndNoForbiddenRuntimeText()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BootstrapRootPrefabPath);
            Assert.IsNotNull(prefab);
            var text = string.Join("\n", prefab.GetComponentsInChildren<Text>(true).Select(label => label.text));

            Assert.That(text, Does.Contain("aggregate-only"));
            Assert.That(text, Does.Contain("Approved locations"));
            Assert.That(text, Does.Contain("never synced"));
            Assert.That(text, Does.Not.Contain("/Users/"));
            Assert.That(text, Does.Not.Contain("access-token"));
            Assert.That(text, Does.Not.Contain("refresh-token"));
            Assert.That(text, Does.Not.Contain("password"));
            Assert.IsEmpty(UiVisibleTextScanner.FindForbiddenRuntimeText(prefab));
        }

        [Test]
        public void ScriptUiFallbackIsRemovedFromRuntimeSurface()
        {
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/_Project/Scripts/UI/BootstrapScreenView.cs"));
            Assert.IsNull(typeof(AppBootstrapper).GetField("allowFallbackUi", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNull(typeof(AppBootstrapper).GetField("bootstrapScreen", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNull(typeof(AppBootstrapper).GetProperty("AllowFallbackUi", BindingFlags.Instance | BindingFlags.Public));
            Assert.IsNull(typeof(AppBootstrapper).GetProperty("WasFallbackUiUsed", BindingFlags.Instance | BindingFlags.Public));
        }

        [Test]
        public void MacOSEntitlementsFileExistsAndIsMinimal()
        {
            var path = Path.Combine(FindRepoRoot(), "BuildSupport/macOS/TokenForge.entitlements");
            Assert.IsTrue(File.Exists(path), path);
            var text = File.ReadAllText(path);
            Assert.That(text, Does.Contain("<plist"));
            Assert.That(text, Does.Contain("<dict>"));
            Assert.That(text, Does.Not.Contain("com.apple.security.cs.allow-jit"));
            Assert.That(text, Does.Not.Contain("com.apple.security.files.user-selected.read-write"));
            Assert.That(text, Does.Not.Contain("com.apple.security.application-groups"));
        }

        private static GameObject AssertPrefabHas<T>(string path) where T : Component
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, path);
            Assert.IsTrue(File.Exists(path + ".meta"), path + ".meta");
            Assert.IsNotNull(prefab.GetComponent<T>(), typeof(T).Name + " missing on " + path);
            return prefab;
        }

        private static void AssertPanel(UiBinderBase panel, string prefabPath)
        {
            Assert.IsNotNull(panel, prefabPath);
            Assert.IsTrue(panel.gameObject.activeSelf, panel.name + " must be active or reachable in the root prefab.");
            Assert.IsTrue(panel.ValidateRequiredReferences(out var error), panel.GetType().Name + ": " + error);
            AssertPrefabHas<UiBinderBase>(prefabPath);
        }

        private static string FindRepoRoot()
        {
            var current = Directory.GetCurrentDirectory();
            while (!string.IsNullOrWhiteSpace(current))
            {
                if (File.Exists(Path.Combine(current, ".gitignore")))
                {
                    return current;
                }

                current = Directory.GetParent(current)?.FullName;
            }

            Assert.Fail("Could not locate repository root.");
            return Directory.GetCurrentDirectory();
        }
    }
}
#endif
