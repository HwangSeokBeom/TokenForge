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
        private const string MainScenePath = "Assets/_Project/Scenes/Main.unity";

        [MenuItem("Tools/TokenForge/Rebuild Bootstrap Scene")]
        public static void BuildMainScene()
        {
            var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

            EnsureCamera();
            EnsureLight();
            EnsureEventSystem();
            var screenView = EnsureCanvasAndRootPanel();
            EnsureBootstrapper(screenView);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainScenePath, true)
            };

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureCamera()
        {
            var cameraObject = GameObject.Find("Main Camera");
            if (cameraObject == null)
            {
                cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            }

            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.045f, 0.06f, 1f);
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

        private static BootstrapScreenView EnsureCanvasAndRootPanel()
        {
            var canvasObject = GameObject.Find("Canvas");
            if (canvasObject == null)
            {
                canvasObject = new GameObject("Canvas", typeof(RectTransform));
            }

            var canvas = canvasObject.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = canvasObject.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;

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

            var root = canvasObject.transform.Find("Root UI Panel");
            GameObject rootObject;
            if (root == null)
            {
                rootObject = new GameObject("Root UI Panel", typeof(RectTransform), typeof(Image));
                rootObject.transform.SetParent(canvasObject.transform, false);
            }
            else
            {
                rootObject = root.gameObject;
            }

            var rect = rootObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = rootObject.GetComponent<Image>();
            if (image == null)
            {
                image = rootObject.AddComponent<Image>();
            }

            image.color = new Color(0.035f, 0.045f, 0.06f, 1f);

            var screenView = rootObject.GetComponent<BootstrapScreenView>();
            if (screenView == null)
            {
                screenView = rootObject.AddComponent<BootstrapScreenView>();
            }

            return screenView;
        }

        private static void EnsureBootstrapper(BootstrapScreenView screenView)
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
            serialized.FindProperty("bootstrapScreen").objectReferenceValue = screenView;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
