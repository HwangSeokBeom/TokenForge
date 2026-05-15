#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TokenForge.Client.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TokenForge.Client.Tests
{
    public sealed class Phase17FallbackRemovalPlayModeTests
    {
        [TearDown]
        public void TearDown()
        {
            AppBootstrapper.DisableEditorAssetPrefabLookupForTests = false;
        }

        [UnityTest]
        public IEnumerator PrefabFixtureDoesNotContainLegacyScriptUi()
        {
            var fixture = Phase14UiFixture.Create();
            yield return null;

            Assert.IsNotNull(fixture.Root);
            Assert.IsNull(System.Type.GetType("TokenForge.Client.UI.BootstrapScreenView, TokenForge.Client"));
            Assert.IsFalse(fixture.Root.GetComponents<MonoBehaviour>().Any(component => component.GetType().Name == "BootstrapScreenView"));

            fixture.Destroy();
        }

        [UnityTest]
        public IEnumerator BootstrapSceneDoesNotContainLegacyRootUiPanel()
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

            Assert.IsNotNull(Object.FindObjectOfType<BootstrapRootView>());
            Assert.IsNull(GameObject.Find("Root UI Panel"));
        }

        [UnityTest]
        public IEnumerator MissingPrefabDoesNotCreateLegacyRootUiPanel()
        {
            LogAssert.Expect(LogType.Warning, "TokenForge BootstrapRoot.prefab is unavailable. Prefab UI is the only supported startup path.");
            LogAssert.Expect(LogType.Error, "TokenForge prefab UI is required but BootstrapRoot.prefab is unavailable. No script UI fallback will be created.");
            LogAssert.Expect(LogType.Error, "TokenForge prefab UI is required but BootstrapRoot.prefab is unavailable. Startup UI was not created.");

            CleanupGeneratedObjects();
            AppBootstrapper.DisableEditorAssetPrefabLookupForTests = true;
            var bootstrapperObject = new GameObject("Phase17 Missing Prefab Bootstrapper");
            bootstrapperObject.SetActive(false);
            var bootstrapper = bootstrapperObject.AddComponent<AppBootstrapper>();
            SetPrivateField(bootstrapper, "bootstrapRoot", null);
            SetPrivateField(bootstrapper, "bootstrapRootPrefab", null);
            SetPrivateField(bootstrapper, "loadAuthSessionOnStart", false);
            SetPrivateField(bootstrapper, "runSafeSmokeFlowWhenEmpty", false);
            SetPrivateField(bootstrapper, "loadPrefabFromAssetPathInEditor", false);
            bootstrapperObject.SetActive(true);
            yield return null;

            Assert.IsFalse(bootstrapper.IsPrefabUiActive);
            Assert.IsNull(GameObject.Find("Root UI Panel"));

            CleanupGeneratedObjects();
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
