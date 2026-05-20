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
        private const string HierarchyLogPrefix = "[TokenForgeHierarchy]";
        private static readonly Vector2 ReferenceResolution = new Vector2(1280f, 720f);
        private static bool rebuildPendingAfterPlayMode;

        [MenuItem("Tools/TokenForge/Rebuild Startup Scene")]
        public static void BuildMainScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.Log("INFO " + HierarchyLogPrefix + " stopping Play Mode before rebuilding startup scene.");
                if (!rebuildPendingAfterPlayMode)
                {
                    rebuildPendingAfterPlayMode = true;
                    EditorApplication.update += BuildMainSceneAfterPlayModeStops;
                }

                EditorApplication.isPlaying = false;
                return;
            }

            Phase14UiPrefabBuilder.BuildPrefabs();
            EnsureSceneFolder();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (File.Exists(BootstrapScenePath))
            {
                AssetDatabase.DeleteAsset(BootstrapScenePath);
                Debug.Log("INFO " + HierarchyLogPrefix + " deleted stale startup scene asset path=" + BootstrapScenePath);
            }

            EnsureCamera();
            EnsureEventSystem();
            var rootView = EnsureCanvasAndRootPanel();
            EnsureBootstrapper(rootView);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, BootstrapScenePath))
            {
                Debug.LogError("ERROR " + HierarchyLogPrefix + " failed to save startup scene path=" + BootstrapScenePath);
                return;
            }

            AssetDatabase.ImportAsset(BootstrapScenePath);
            TokenForgeStartupSceneSettings.EnsureStartupSceneIsFirstInBuildSettings();
            TokenForgeStartupSceneSettings.ConfigureEditorPlayModeStartScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            var reopenedScene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            VerifySavedSceneHierarchy(reopenedScene);
            Debug.Log("INFO " + HierarchyLogPrefix + " startup scene rebuilt and saved path=" + BootstrapScenePath);
        }

        private static void BuildMainSceneAfterPlayModeStops()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            EditorApplication.update -= BuildMainSceneAfterPlayModeStops;
            rebuildPendingAfterPlayMode = false;
            BuildMainScene();
        }

        private static void EnsureSceneFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project"))
            {
                AssetDatabase.CreateFolder("Assets", "_Project");
            }

            if (!AssetDatabase.IsValidFolder("Assets/_Project/Scenes"))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Scenes");
            }
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
            camera.backgroundColor = new Color(0.965f, 0.945f, 0.905f, 1f);
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
                canvasRect.anchorMin = Vector2.zero;
                canvasRect.anchorMax = Vector2.zero;
                canvasRect.anchoredPosition = Vector2.zero;
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
            canvas.overrideSorting = false;
            canvas.sortingOrder = 0;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvasObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (canvasObject.GetComponent<GraphicRaycaster>() == null)
            {
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            canvasObject.transform.localPosition = Vector3.zero;
            canvasObject.transform.localRotation = Quaternion.identity;
            canvasObject.transform.localScale = Vector3.one;

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
            if (prefab != null)
            {
                PrefabUtility.UnpackPrefabInstance(rootObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }

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

            var marker = rootObject.GetComponent<BootstrapRootSourceMarker>();
            if (marker == null)
            {
                marker = rootObject.AddComponent<BootstrapRootSourceMarker>();
            }

            marker.SetSource(
                "scene-prefab-copy",
                BootstrapRootSourceMarker.PrefabPath,
                BootstrapRootSourceMarker.CurrentUiVersion);

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

        private static void VerifySavedSceneHierarchy(Scene scene)
        {
            Debug.Log("INFO " + HierarchyLogPrefix + " saved scene verification Scene=" + scene.name
                + " path=" + scene.path
                + " rootCount=" + scene.rootCount);

            foreach (var rootObject in scene.GetRootGameObjects())
            {
                Debug.Log("INFO " + HierarchyLogPrefix + " saved root=" + rootObject.name
                    + " active=" + rootObject.activeInHierarchy
                    + " childCount=" + rootObject.transform.childCount
                    + " scale=" + FormatVector(rootObject.transform.lossyScale));
            }

            var canvasObject = GameObject.Find("Canvas");
            var bootstrapperObject = GameObject.Find("AppBootstrapper");
            var eventSystemObject = GameObject.Find("EventSystem");
            var cameraObject = GameObject.Find("Main Camera");
            if (canvasObject == null)
            {
                Debug.LogError("ERROR " + HierarchyLogPrefix + " Canvas missing in saved startup scene");
                return;
            }

            if (bootstrapperObject == null)
            {
                Debug.LogError("ERROR " + HierarchyLogPrefix + " AppBootstrapper missing in saved startup scene");
            }

            if (eventSystemObject == null)
            {
                Debug.LogError("ERROR " + HierarchyLogPrefix + " EventSystem missing in saved startup scene");
            }

            if (cameraObject == null)
            {
                Debug.LogError("ERROR " + HierarchyLogPrefix + " Main Camera missing in saved startup scene");
            }

            LogSavedPath(canvasObject.transform, "Canvas/BootstrapRoot");
            LogSavedPath(canvasObject.transform, "Canvas/BootstrapRoot/Background");
            LogSavedPath(canvasObject.transform, "Canvas/BootstrapRoot/WindowShell");
            LogSavedPath(canvasObject.transform, "Canvas/BootstrapRoot/WindowShell/TopBar");
            LogSavedPath(canvasObject.transform, "Canvas/BootstrapRoot/WindowShell/AppBody/Sidebar");
            LogSavedPath(canvasObject.transform, "Canvas/BootstrapRoot/WindowShell/AppBody/MainContent");
            LogSavedPath(canvasObject.transform, "Canvas/BootstrapRoot/WindowShell/AppBody/MainContent/Root Scroll");
            LogSavedPath(canvasObject.transform, "Canvas/BootstrapRoot/WindowShell/AppBody/MainContent/Root Scroll/Viewport");
            LogSavedPath(canvasObject.transform, "Canvas/BootstrapRoot/WindowShell/AppBody/MainContent/Root Scroll/Viewport/Content");
            LogSavedPath(canvasObject.transform, "Canvas/BootstrapRoot/WindowShell/AppBody/MainContent/Root Scroll/Viewport/Content/Start Screen Root");
            LogSavedPath(canvasObject.transform, "Canvas/BootstrapRoot/WindowShell/AppBody/MainContent/Root Scroll/Viewport/Content/Start Screen Root/HeroCompanionCard");
            LogSavedPath(canvasObject.transform, "Canvas/BootstrapRoot/WindowShell/AppBody/MainContent/Root Scroll/Viewport/Content/Start Screen Root/StatusCardGrid");
        }

        private static void LogSavedPath(Transform canvasTransform, string expectedPath)
        {
            var relativePath = expectedPath.StartsWith("Canvas/", System.StringComparison.Ordinal)
                ? expectedPath.Substring("Canvas/".Length)
                : expectedPath;
            var target = canvasTransform != null ? canvasTransform.Find(relativePath) : null;
            if (target == null)
            {
                Debug.LogError("ERROR " + HierarchyLogPrefix + " saved path=" + expectedPath + " missing");
                return;
            }

            var rect = target.GetComponent<RectTransform>();
            Debug.Log("INFO " + HierarchyLogPrefix + " saved path=" + expectedPath
                + " active=" + target.gameObject.activeInHierarchy
                + " childCount=" + target.childCount
                + " scale=" + FormatVector(target.lossyScale)
                + " worldCorners=" + (rect != null ? FormatWorldCorners(rect) : "<none>"));
        }

        private static string FormatWorldCorners(RectTransform rectTransform)
        {
            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            return FormatVector(corners[0]) + " " + FormatVector(corners[1]) + " "
                + FormatVector(corners[2]) + " " + FormatVector(corners[3]);
        }

        private static string FormatVector(Vector3 value)
        {
            return "(" + value.x.ToString("0.##") + "," + value.y.ToString("0.##") + "," + value.z.ToString("0.##") + ")";
        }
    }
}
