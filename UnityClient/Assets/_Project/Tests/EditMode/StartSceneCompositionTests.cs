#if UNITY_EDITOR
using System.IO;
using System.Linq;
using NUnit.Framework;
using TokenForge.Client.Editor;
using TokenForge.Client.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TokenForge.Client.Tests
{
    public sealed class StartSceneCompositionTests
    {
        private const string BootstrapRootPrefabPath = "Assets/_Project/Prefabs/UI/BootstrapRoot.prefab";

        [Test]
        public void TokenForgeMainSceneAssetExistsInProjectScenes()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(TokenForgeStartupSceneSettings.StartupScenePath));
            Assert.IsTrue(File.Exists(TokenForgeStartupSceneSettings.StartupScenePath + ".meta"));

            var projectScenes = Directory
                .GetFiles(Application.dataPath, "*.unity", SearchOption.AllDirectories)
                .Select(path => "Assets" + path.Substring(Application.dataPath.Length).Replace('\\', '/'))
                .ToArray();

            Assert.That(projectScenes, Does.Contain(TokenForgeStartupSceneSettings.StartupScenePath));
        }

        [Test]
        public void TokenForgeMainSceneContainsBootstrapInfrastructure()
        {
            EditorSceneManager.OpenScene(TokenForgeStartupSceneSettings.StartupScenePath, OpenSceneMode.Single);

            var bootstrapper = Object.FindObjectOfType<AppBootstrapper>();
            var canvas = Object.FindObjectOfType<Canvas>();
            var root = Object.FindObjectOfType<BootstrapRootView>();
            var eventSystem = Object.FindObjectOfType<EventSystem>();
            var mainCamera = Camera.main;

            Assert.IsNotNull(mainCamera, "Main Camera is missing.");
            Assert.IsTrue(mainCamera.enabled, "Main Camera is disabled.");
            Assert.AreEqual(CameraClearFlags.SolidColor, mainCamera.clearFlags);
            Assert.AreEqual(0, mainCamera.depth);
            Assert.AreEqual(0, mainCamera.targetDisplay);
            Assert.AreNotEqual(0, mainCamera.cullingMask, "Main Camera culling mask renders no layers.");
            Assert.IsNotNull(eventSystem, "EventSystem is missing.");
            Assert.IsNotNull(canvas, "Canvas is missing.");
            Assert.IsTrue(canvas.gameObject.activeSelf, "Canvas root is inactive.");
            Assert.IsTrue(canvas.enabled, "Canvas is disabled.");
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
            Assert.AreEqual(0, canvas.sortingOrder);
            Assert.AreEqual(0, canvas.targetDisplay);
            Assert.IsNotNull(canvas.GetComponent<CanvasScaler>(), "CanvasScaler is missing.");
            Assert.IsNotNull(canvas.GetComponent<GraphicRaycaster>(), "GraphicRaycaster is missing.");
            AssertResolvableRect(canvas.GetComponent<RectTransform>(), "Canvas");
            Assert.IsNotNull(bootstrapper, "AppBootstrapper is missing.");
            Assert.IsNotNull(root, "BootstrapRootView is missing.");
            Assert.IsTrue(root.gameObject.activeSelf, "BootstrapRoot is inactive.");
            Assert.IsTrue(root.ValidateReferences(out var error), error);
            AssertResolvableRect(root.GetComponent<RectTransform>(), "BootstrapRoot");
            Assert.IsTrue(root.rootScrollRect != null && root.rootScrollRect.gameObject.activeSelf, "Main panel is inactive.");

            var serialized = new SerializedObject(bootstrapper);
            Assert.IsNotNull(serialized.FindProperty("bootstrapRoot").objectReferenceValue);
            Assert.IsNotNull(serialized.FindProperty("bootstrapRootPrefab").objectReferenceValue);
            Assert.IsFalse(serialized.FindProperty("loadAuthSessionOnStart").boolValue);
            Assert.IsFalse(serialized.FindProperty("runSafeSmokeFlowWhenEmpty").boolValue);
        }

        [Test]
        public void TokenForgeMainSceneHasNoMissingScriptsOrInactiveStartupRoots()
        {
            var scene = EditorSceneManager.OpenScene(TokenForgeStartupSceneSettings.StartupScenePath, OpenSceneMode.Single);
            var allSceneObjects = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(transform => transform.gameObject)
                .ToArray();

            foreach (var sceneObject in allSceneObjects)
            {
                Assert.AreEqual(
                    0,
                    GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(sceneObject),
                    sceneObject.name + " has missing MonoBehaviour scripts.");
            }

            var canvas = Object.FindObjectOfType<Canvas>();
            var rootView = Object.FindObjectOfType<BootstrapRootView>();
            Assert.IsNotNull(canvas, "Canvas is missing.");
            Assert.IsNotNull(rootView, "BootstrapRootView is missing.");
            Assert.IsTrue(canvas.gameObject.activeSelf, "Canvas root is inactive.");
            Assert.IsTrue(rootView.gameObject.activeSelf, "BootstrapRoot is inactive.");
            Assert.IsTrue(rootView.ValidateReferences(out var error), error);
        }

        [Test]
        public void BootstrapRootPrefabHasNoMissingScriptsOrSerializedReferenceGaps()
        {
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BootstrapRootPrefabPath);
            Assert.IsNotNull(prefabAsset, "BootstrapRoot prefab asset is missing.");

            var prefabRoot = PrefabUtility.LoadPrefabContents(BootstrapRootPrefabPath);
            try
            {
                foreach (var prefabObject in prefabRoot.GetComponentsInChildren<Transform>(true).Select(transform => transform.gameObject))
                {
                    Assert.AreEqual(
                        0,
                        GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefabObject),
                        prefabObject.name + " has missing MonoBehaviour scripts.");
                }

                var rootView = prefabRoot.GetComponent<BootstrapRootView>();
                Assert.IsNotNull(rootView, "BootstrapRootView is missing from BootstrapRoot prefab.");
                Assert.IsTrue(prefabRoot.activeSelf, "BootstrapRoot prefab root is inactive.");
                Assert.IsTrue(rootView.ValidateReferences(out var error), error);
                AssertResolvableRect(prefabRoot.GetComponent<RectTransform>(), "BootstrapRoot prefab");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void AssertResolvableRect(RectTransform rect, string label)
        {
            Assert.IsNotNull(rect, label + " RectTransform is missing.");
            Assert.Greater(Mathf.Abs(rect.localScale.x), 0.001f, label + " RectTransform has zero x scale.");
            Assert.Greater(Mathf.Abs(rect.localScale.y), 0.001f, label + " RectTransform has zero y scale.");

            var rectSize = rect.rect.size;
            var stretchedSize = rect.anchorMax - rect.anchorMin;
            Assert.IsTrue(
                rectSize.x > 0.5f || stretchedSize.x > 0.001f,
                label + " RectTransform cannot resolve a visible width.");
            Assert.IsTrue(
                rectSize.y > 0.5f || stretchedSize.y > 0.001f,
                label + " RectTransform cannot resolve a visible height.");
        }
    }
}
#endif
