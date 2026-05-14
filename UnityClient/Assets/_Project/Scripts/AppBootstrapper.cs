using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TokenForge.Client.Platform;
using TokenForge.Client.UI;

namespace TokenForge.Client
{
    public sealed class AppBootstrapper : MonoBehaviour
    {
        [SerializeField] private BootstrapScreenView bootstrapScreen;
        [SerializeField] private bool runSafeSmokeFlowWhenEmpty = true;

        private LocalClientStatus localStatus;

        public LocalClientStatus LocalStatus => localStatus;

        private void Awake()
        {
            EnsureSceneInfrastructure();

            if (bootstrapScreen == null)
            {
                bootstrapScreen = FindObjectOfType<BootstrapScreenView>();
            }

            localStatus = LocalClientStatus.CreateInitialized();

            if (bootstrapScreen != null)
            {
                bootstrapScreen.Bind(localStatus);
            }
            else
            {
                Debug.LogWarning("TokenForge bootstrap screen is not assigned.");
            }
        }

        private async void Start()
        {
            if (!runSafeSmokeFlowWhenEmpty)
            {
                return;
            }

            var result = await new BootstrapSmokeFlow().RunIfEmptyAsync();
            if (result.IsSuccess)
            {
                Debug.Log("TokenForge safe bootstrap completed.");
            }
            else
            {
                Debug.LogWarning("TokenForge safe bootstrap could not complete.");
            }
        }

        private void EnsureSceneInfrastructure()
        {
            EnsureEventSystem();
            bootstrapScreen = EnsureBootstrapScreen();
        }

        private void EnsureEventSystem()
        {
            var eventSystem = EventSystem.current;
            var eventSystemObject = eventSystem != null ? eventSystem.gameObject : GameObject.Find("EventSystem");
            if (eventSystemObject == null)
            {
                eventSystemObject = new GameObject("EventSystem");
            }

            GetOrAddComponent<EventSystem>(eventSystemObject);
            GetOrAddComponent<StandaloneInputModule>(eventSystemObject);
        }

        private BootstrapScreenView EnsureBootstrapScreen()
        {
            var canvasObject = GameObject.Find("Canvas");
            if (canvasObject == null)
            {
                canvasObject = new GameObject("Canvas", typeof(RectTransform));
            }

            var canvas = GetOrAddComponent<Canvas>(canvasObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;

            var scaler = GetOrAddComponent<CanvasScaler>(canvasObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GetOrAddComponent<GraphicRaycaster>(canvasObject);

            var rootTransform = canvasObject.transform.Find("Root UI Panel") as RectTransform;
            if (rootTransform == null)
            {
                var rootObject = new GameObject("Root UI Panel", typeof(RectTransform));
                rootObject.transform.SetParent(canvasObject.transform, false);
                rootTransform = rootObject.GetComponent<RectTransform>();
            }

            rootTransform.anchorMin = Vector2.zero;
            rootTransform.anchorMax = Vector2.one;
            rootTransform.offsetMin = Vector2.zero;
            rootTransform.offsetMax = Vector2.zero;

            return GetOrAddComponent<BootstrapScreenView>(rootTransform.gameObject);
        }

        private static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }
    }
}
