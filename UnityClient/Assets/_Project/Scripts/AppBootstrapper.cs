using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TokenForge.Client.Agents;
using TokenForge.Client.Auth;
using TokenForge.Client.Git;
using TokenForge.Client.Platform;
using TokenForge.Client.Privacy;
using TokenForge.Client.Persistence;
using TokenForge.Client.Sync;
using TokenForge.Client.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TokenForge.Client
{
    public sealed class AppBootstrapper : MonoBehaviour
    {
        private const string BootstrapRootPrefabPath = "Assets/_Project/Prefabs/UI/BootstrapRoot.prefab";

        [SerializeField] private BootstrapRootView bootstrapRoot;
        [SerializeField] private GameObject bootstrapRootPrefab;
        [SerializeField] private bool loadAuthSessionOnStart;
        [SerializeField] private bool runSafeSmokeFlowWhenEmpty;
        [SerializeField] private bool loadPrefabFromAssetPathInEditor = true;
        [SerializeField] private BootstrapSyncMode bootstrapSyncMode = BootstrapSyncMode.None;
        [SerializeField] private string apiBaseUrl = ApiConfiguration.DefaultBaseUrl;

        private LocalClientStatus localStatus;
        private PrivacySanitizer privacySanitizer;
        private SaveDataRepository repository;
        private ApprovedLocationSettingsRepository approvedLocationRepository;
        private ISyncService syncService;
        private ISafeSyncService safeSyncService;
        private IAuthSessionService authSessionService;
        private BackendSyncSmokeFlow backendSyncSmokeFlow;
        private GitAnalysisFlowController gitAnalysisFlow;
        private AgentAnalysisFlowController agentAnalysisFlow;
        private ApprovedActivityAnalysisViewModel approvedActivityAnalysis;

#if UNITY_EDITOR
        public static bool DisableEditorAssetPrefabLookupForTests { get; set; }
#endif

        public LocalClientStatus LocalStatus => localStatus;
        public GitAnalysisFlowController GitAnalysisFlow => gitAnalysisFlow;
        public AgentAnalysisFlowController AgentAnalysisFlow => agentAnalysisFlow;
        public ApprovedActivityAnalysisViewModel ApprovedActivityAnalysis => approvedActivityAnalysis;
        public bool IsPrefabUiActive => bootstrapRoot != null;
        public bool RequirePrefabUi => true;
        public bool LoadAuthSessionOnStart => loadAuthSessionOnStart;
        public bool RunSafeSmokeFlowWhenEmpty => runSafeSmokeFlowWhenEmpty;

        private void Awake()
        {
            EnsureSceneInfrastructure();
            ConfigureServices();

            if (bootstrapRoot == null)
            {
                bootstrapRoot = FindObjectOfType<BootstrapRootView>();
            }

            localStatus = LocalClientStatus.CreateInitialized();
            gitAnalysisFlow = CreateGitAnalysisFlow();
            agentAnalysisFlow = CreateAgentAnalysisFlow();
            approvedActivityAnalysis = new ApprovedActivityAnalysisViewModel(
                gitAnalysisFlow,
                agentAnalysisFlow,
                new MacOSAgentLogLocationPicker(),
                repository,
                privacySanitizer,
                approvedLocationRepository,
                safeSyncService,
                authSessionService);

            if (bootstrapRoot != null)
            {
                bootstrapRoot.Bind(localStatus, approvedActivityAnalysis);
            }
            else
            {
                Debug.LogError("TokenForge prefab UI is required but BootstrapRoot.prefab is unavailable. Startup UI was not created.");
            }
        }

        private async void Start()
        {
            if (!runSafeSmokeFlowWhenEmpty && bootstrapSyncMode == BootstrapSyncMode.None)
            {
                await RefreshDashboardAsync(loadAuthSessionOnStart);
                return;
            }

            var result = await new BootstrapSmokeFlow(repository, privacySanitizer, null, syncService, backendSyncSmokeFlow)
                .RunIfEmptyThenOptionalSyncAsync(runSafeSmokeFlowWhenEmpty, bootstrapSyncMode);
            if (result.IsSuccess)
            {
                Debug.Log("TokenForge safe bootstrap completed.");
                if (approvedActivityAnalysis != null)
                {
                    await RefreshDashboardAsync(loadAuthSessionOnStart);
                }
            }
            else
            {
                Debug.LogWarning("TokenForge safe bootstrap could not complete.");
            }
        }

        private async System.Threading.Tasks.Task RefreshDashboardAsync(bool loadAuthSession)
        {
            if (approvedActivityAnalysis == null)
            {
                return;
            }

            if (loadAuthSession)
            {
                await approvedActivityAnalysis.RefreshDashboardAsync();
            }
            else
            {
                await approvedActivityAnalysis.RefreshApprovedLocationsAsync();
                await approvedActivityAnalysis.RefreshRecentSessionsAsync();
                await approvedActivityAnalysis.RefreshSafeSyncLocalStateAsync();
            }

            if (bootstrapRoot != null)
            {
                bootstrapRoot.Bind(localStatus, approvedActivityAnalysis);
            }
        }

        private void ConfigureServices()
        {
            privacySanitizer = new PrivacySanitizer();
            repository = new SaveDataRepository(null, privacySanitizer);
            approvedLocationRepository = new ApprovedLocationSettingsRepository();
            syncService = null;
            safeSyncService = null;
            authSessionService = null;
            backendSyncSmokeFlow = null;

            if (bootstrapSyncMode == BootstrapSyncMode.RealBackendSmoke)
            {
                var configuration = new ApiConfiguration(apiBaseUrl);
                var safeSyncConfiguration = new SafeSyncApiConfig(apiBaseUrl);
                var logger = new UnitySafeSyncLogger();
                var transport = new SystemNetHttpTransport();
                authSessionService = new AuthSessionService(
                    new AuthApiClient(new AuthApiConfig(apiBaseUrl), transport, logger),
                    new MacOSKeychainTokenStore());
                var authProvider = new AuthSessionTokenProvider(authSessionService);
                var httpClient = new TokenForgeHttpClient(configuration, authProvider, transport, logger);
                syncService = new SyncService(repository, httpClient, null, privacySanitizer);
                safeSyncService = new SafeSyncService(
                    repository,
                    new SafeSyncApiClient(safeSyncConfiguration, authProvider, transport, logger),
                    null,
                    privacySanitizer);
                backendSyncSmokeFlow = new BackendSyncSmokeFlow(repository, authProvider, httpClient, null, privacySanitizer, logger);
            }
            else if (bootstrapSyncMode != BootstrapSyncMode.None)
            {
                authSessionService = new AuthSessionService(
                    new AuthApiClient(new AuthApiConfig(apiBaseUrl)),
                    new MacOSKeychainTokenStore());
                var authProvider = new AuthSessionTokenProvider(authSessionService);
                var httpClient = new TokenForgeHttpClient(
                    new ApiConfiguration(apiBaseUrl),
                    authProvider);
                syncService = new SyncService(repository, httpClient, null, privacySanitizer);
                safeSyncService = new SafeSyncService(
                    repository,
                    new SafeSyncApiClient(new SafeSyncApiConfig(apiBaseUrl), authProvider),
                    null,
                    privacySanitizer);
            }
            else
            {
                authSessionService = new AuthSessionService(
                    new AuthApiClient(new AuthApiConfig(apiBaseUrl)),
                    new MacOSKeychainTokenStore());
                var authProvider = new AuthSessionTokenProvider(authSessionService);
                safeSyncService = new SafeSyncService(
                    repository,
                    new SafeSyncApiClient(new SafeSyncApiConfig(apiBaseUrl), authProvider),
                    null,
                    privacySanitizer);
            }
        }

        private GitAnalysisFlowController CreateGitAnalysisFlow()
        {
            var logger = new UnityGitAnalysisLogger();
            return new GitAnalysisFlowController(
                new MacOSRepositoryPicker(),
                new GitAggregateAnalyzer(null, privacySanitizer, logger),
                repository,
                null,
                privacySanitizer,
                syncService,
                logger);
        }

        private AgentAnalysisFlowController CreateAgentAnalysisFlow()
        {
            return new AgentAnalysisFlowController(
                new AgentLogActivityProvider(new AgentActivityAnalyzer(null, null, privacySanitizer)),
                repository,
                null,
                privacySanitizer,
                syncService);
        }

        private void EnsureSceneInfrastructure()
        {
            EnsureEventSystem();
            var canvasObject = EnsureCanvas();
            bootstrapRoot = EnsureBootstrapRoot(canvasObject);
            if (bootstrapRoot == null)
            {
                Debug.LogError("TokenForge prefab UI is required but BootstrapRoot.prefab is unavailable. No script UI fallback will be created.");
            }
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

        private GameObject EnsureCanvas()
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
            return canvasObject;
        }

        private BootstrapRootView EnsureBootstrapRoot(GameObject canvasObject)
        {
            var root = FindObjectOfType<BootstrapRootView>();
            if (root != null)
            {
                return root;
            }

            var prefab = bootstrapRootPrefab;
#if UNITY_EDITOR
            if (prefab == null && loadPrefabFromAssetPathInEditor && !DisableEditorAssetPrefabLookupForTests)
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BootstrapRootPrefabPath);
            }
#endif
            if (prefab == null)
            {
                Debug.LogWarning("TokenForge BootstrapRoot.prefab is unavailable. Prefab UI is the only supported startup path.");
                return null;
            }

            var instance = Instantiate(prefab, canvasObject.transform, false);
            instance.name = "BootstrapRoot";
            var rect = instance.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            return instance.GetComponent<BootstrapRootView>();
        }
        private static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }
    }
}
