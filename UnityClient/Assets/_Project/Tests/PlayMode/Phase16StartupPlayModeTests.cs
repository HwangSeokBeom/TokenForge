#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TokenForge.Client.Auth;
using TokenForge.Client.Sync;
using TokenForge.Client.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TokenForge.Client.Tests
{
    public sealed class Phase16StartupPlayModeTests
    {
        [UnityTest]
        public IEnumerator BootstrapSceneDefaultsRequirePrefabUiAndDoNotAutoStartWork()
        {
            var load = SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            while (!load.isDone)
            {
                yield return null;
            }

            for (var i = 0; i < 8; i++)
            {
                yield return null;
            }

            var bootstrapper = Object.FindObjectOfType<AppBootstrapper>();
            var root = Object.FindObjectOfType<BootstrapRootView>();
            Assert.IsNotNull(bootstrapper);
            Assert.IsNotNull(root);
            Assert.IsTrue(bootstrapper.RequirePrefabUi);
            Assert.IsFalse(bootstrapper.LoadAuthSessionOnStart);
            Assert.IsFalse(bootstrapper.RunSafeSmokeFlowWhenEmpty);
            Assert.IsTrue(bootstrapper.IsPrefabUiActive);

            Assert.AreEqual(AuthState.LoggedOut, bootstrapper.ApprovedActivityAnalysis.AuthState);
            Assert.AreEqual(SafeSyncStatus.Idle, bootstrapper.ApprovedActivityAnalysis.SafeSyncStatus);
            Assert.IsFalse(bootstrapper.ApprovedActivityAnalysis.IsAuthRequestInProgress);
            Assert.IsFalse(bootstrapper.ApprovedActivityAnalysis.IsSafeSyncRequestInProgress);
            Assert.IsFalse(root.activityAnalysisPanel.analyzeGitButton.interactable);
            Assert.IsFalse(root.activityAnalysisPanel.analyzeAgentButton.interactable);
            Assert.IsTrue(root.safeSyncPanel.healthButton.interactable);
            Assert.IsFalse(root.safeSyncPanel.syncButton.interactable);
            Assert.IsFalse(root.safeSyncPanel.fetchButton.interactable);

            var visibleText = string.Join("\n", UiVisibleTextScanner.Collect(root.gameObject));
            Assert.That(visibleText, Does.Contain("Safe Sync sends aggregate-only"));
            Assert.That(visibleText, Does.Contain("Approved locations are stored only on this device"));
            Assert.That(visibleText, Does.Not.Contain("access-token"));
            Assert.That(visibleText, Does.Not.Contain("refresh-token"));
            Assert.IsEmpty(UiVisibleTextScanner.FindForbiddenRuntimeText(root.gameObject));
        }
    }

    public sealed class Phase16FallbackRemovalPlayModeTests
    {
        [TearDown]
        public void TearDown()
        {
            AppBootstrapper.DisableEditorAssetPrefabLookupForTests = false;
        }

        [UnityTest]
        public IEnumerator RequirePrefabUiFailsSafelyWhenPrefabIsMissing()
        {
            LogAssert.Expect(LogType.Warning, "TokenForge BootstrapRoot.prefab is unavailable. Prefab UI is the only supported startup path.");
            LogAssert.Expect(LogType.Error, "TokenForge prefab UI is required but BootstrapRoot.prefab is unavailable. No script UI fallback will be created.");
            LogAssert.Expect(LogType.Error, "TokenForge prefab UI is required but BootstrapRoot.prefab is unavailable. Startup UI was not created.");

            var bootstrapper = CreateBootstrapperWithMissingPrefab();
            yield return null;

            Assert.IsFalse(bootstrapper.IsPrefabUiActive);
            Assert.IsTrue(bootstrapper.RequirePrefabUi);
            Assert.IsNull(Object.FindObjectOfType<BootstrapRootView>());

            CleanupGeneratedObjects();
        }

        [UnityTest]
        public IEnumerator FallbackTypeIsUnavailableInRuntimeAssembly()
        {
            Assert.IsNull(System.Type.GetType("TokenForge.Client.UI.BootstrapScreenView, TokenForge.Client"));
            yield return null;
        }

        private static AppBootstrapper CreateBootstrapperWithMissingPrefab()
        {
            CleanupGeneratedObjects();
            AppBootstrapper.DisableEditorAssetPrefabLookupForTests = true;
            var bootstrapperObject = new GameObject("Phase16 Missing Prefab Bootstrapper");
            bootstrapperObject.SetActive(false);
            var bootstrapper = bootstrapperObject.AddComponent<AppBootstrapper>();
            SetPrivateField(bootstrapper, "bootstrapRoot", null);
            SetPrivateField(bootstrapper, "bootstrapRootPrefab", null);
            SetPrivateField(bootstrapper, "loadAuthSessionOnStart", false);
            SetPrivateField(bootstrapper, "runSafeSmokeFlowWhenEmpty", false);
            SetPrivateField(bootstrapper, "loadPrefabFromAssetPathInEditor", false);
            bootstrapperObject.SetActive(true);
            return bootstrapper;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(target, value);
        }

        private static void CleanupGeneratedObjects()
        {
            AppBootstrapper.DisableEditorAssetPrefabLookupForTests = false;
            foreach (var canvas in Object.FindObjectsOfType<Canvas>())
            {
                Object.DestroyImmediate(canvas.gameObject);
            }

            var eventSystem = GameObject.Find("EventSystem");
            if (eventSystem != null)
            {
                Object.DestroyImmediate(eventSystem);
            }

            foreach (var bootstrapper in Object.FindObjectsOfType<AppBootstrapper>())
            {
                Object.DestroyImmediate(bootstrapper.gameObject);
            }
        }
    }
}
#endif
