using System.IO;
using TokenForge.Client;
using TokenForge.Client.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TokenForge.Client.Editor
{
    public static class TokenForgeBootstrapSceneBuilder
    {
        private const string BootstrapScenePath = TokenForgeStartupSceneSettings.StartupScenePath;

        [MenuItem("Tools/TokenForge/Rebuild Startup Scene")]
        public static void BuildMainScene()
        {
            Phase14UiPrefabBuilder.BuildPrefabs();
            var scene = File.Exists(BootstrapScenePath)
                ? EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            EnsureCamera();
            EnsureLight();
            EnsureEventSystem();
            var rootView = EnsureCanvasAndRootPanel();
            EnsureBootstrapper(rootView);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, BootstrapScenePath);
            AssetDatabase.ImportAsset(BootstrapScenePath);
            TokenForgeStartupSceneSettings.EnsureStartupSceneIsFirstInBuildSettings();
            TokenForgeStartupSceneSettings.ConfigureEditorPlayModeStartScene();

            AssetDatabase.SaveAssets();
            Debug.Log("TokenForge startup scene rebuilt: " + BootstrapScenePath);
        }

        private static void EnsureCamera()
        {
            var cameraObject = GameObject.Find("Main Camera");
            if (cameraObject == null)
            {
                cameraObject = new GameObject("Main Camera", typeof(Camera));
            }

            cameraObject.tag = "MainCamera";
            RemoveAudioListener(cameraObject);
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.045f, 0.06f, 1f);
        }

        private static void RemoveAudioListener(GameObject cameraObject)
        {
            foreach (var component in cameraObject.GetComponents<Component>())
            {
                if (component != null && component.GetType().Name == "AudioListener")
                {
                    Object.DestroyImmediate(component);
                }
            }
        }

        private static void EnsureLight()
        {
            if (GameObject.Find("Directional Light") != null)
            {
                return;
            }

            var lightObject = new GameObject("Directional Light", typeof(Light));
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void EnsureEventSystem()
        {
            var eventSystemObject = GameObject.Find("EventSystem");
            if (eventSystemObject == null)
            {
                eventSystemObject = new GameObject("EventSystem");
            }

            if (eventSystemObject.GetComponent<EventSystem>() == null)
            {
                eventSystemObject.AddComponent<EventSystem>();
            }

            if (eventSystemObject.GetComponent<StandaloneInputModule>() == null)
            {
                eventSystemObject.AddComponent<StandaloneInputModule>();
            }
        }

        private static BootstrapRootView EnsureCanvasAndRootPanel()
        {
            var canvasObject = GameObject.Find("Canvas");
            if (canvasObject == null)
            {
                canvasObject = new GameObject("Canvas", typeof(RectTransform));
            }

            canvasObject.SetActive(true);
            canvasObject.transform.localPosition = Vector3.zero;
            canvasObject.transform.localRotation = Quaternion.identity;
            canvasObject.transform.localScale = Vector3.one;
            var canvasRect = canvasObject.GetComponent<RectTransform>();
            if (canvasRect != null)
            {
                canvasRect.sizeDelta = new Vector2(1920f, 1080f);
                canvasRect.pivot = new Vector2(0.5f, 0.5f);
            }

            var canvas = canvasObject.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = canvasObject.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            canvas.pixelPerfect = false;
            canvas.enabled = true;
            canvas.targetDisplay = 0;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvasObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (canvasObject.GetComponent<GraphicRaycaster>() == null)
            {
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            var legacyScriptUi = canvasObject.transform.Find("Root UI Panel");
            if (legacyScriptUi != null)
            {
                Object.DestroyImmediate(legacyScriptUi.gameObject);
            }

            var root = canvasObject.transform.Find("BootstrapRoot");
            if (root != null)
            {
                Object.DestroyImmediate(root.gameObject);
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/BootstrapRoot.prefab");
            var rootObject = prefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvasObject.transform)
                : new GameObject("BootstrapRoot", typeof(RectTransform), typeof(BootstrapRootView));
            rootObject.name = "BootstrapRoot";
            if (rootObject.transform.parent == null)
            {
                rootObject.transform.SetParent(canvasObject.transform, false);
            }

            rootObject.SetActive(true);
            if (rootObject.transform.parent != canvasObject.transform)
            {
                rootObject.transform.SetParent(canvasObject.transform, false);
            }

            rootObject.transform.localPosition = Vector3.zero;
            rootObject.transform.localRotation = Quaternion.identity;
            rootObject.transform.localScale = Vector3.one;

            var rect = rootObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            return rootObject.GetComponent<BootstrapRootView>();
        }

        private static void EnsureBootstrapper(BootstrapRootView rootView)
        {
            var bootstrapperObject = GameObject.Find("AppBootstrapper");
            if (bootstrapperObject == null)
            {
                bootstrapperObject = new GameObject("AppBootstrapper");
            }

            var bootstrapper = bootstrapperObject.GetComponent<AppBootstrapper>();
            if (bootstrapper == null)
            {
                bootstrapper = bootstrapperObject.AddComponent<AppBootstrapper>();
            }

            var serialized = new SerializedObject(bootstrapper);
            serialized.FindProperty("bootstrapRoot").objectReferenceValue = rootView;
            serialized.FindProperty("bootstrapRootPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/BootstrapRoot.prefab");
            serialized.FindProperty("loadAuthSessionOnStart").boolValue = false;
            serialized.FindProperty("runSafeSmokeFlowWhenEmpty").boolValue = false;
            serialized.FindProperty("loadPrefabFromAssetPathInEditor").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
