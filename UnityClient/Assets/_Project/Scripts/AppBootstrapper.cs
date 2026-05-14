using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TokenForge.Client.Agents;
using TokenForge.Client.Git;
using TokenForge.Client.Platform;
using TokenForge.Client.Privacy;
using TokenForge.Client.Persistence;
using TokenForge.Client.Sync;
using TokenForge.Client.UI;

namespace TokenForge.Client
{
    public sealed class AppBootstrapper : MonoBehaviour
    {
        [SerializeField] private BootstrapScreenView bootstrapScreen;
        [SerializeField] private bool runSafeSmokeFlowWhenEmpty = true;
        [SerializeField] private BootstrapSyncMode bootstrapSyncMode = BootstrapSyncMode.None;
        [SerializeField] private string apiBaseUrl = ApiConfiguration.DefaultBaseUrl;

        private LocalClientStatus localStatus;
        private PrivacySanitizer privacySanitizer;
        private SaveDataRepository repository;
        private ApprovedLocationSettingsRepository approvedLocationRepository;
        private ISyncService syncService;
        private BackendSyncSmokeFlow backendSyncSmokeFlow;
        private GitAnalysisFlowController gitAnalysisFlow;
        private AgentAnalysisFlowController agentAnalysisFlow;
        private ApprovedActivityAnalysisViewModel approvedActivityAnalysis;

        public LocalClientStatus LocalStatus => localStatus;
        public GitAnalysisFlowController GitAnalysisFlow => gitAnalysisFlow;
        public AgentAnalysisFlowController AgentAnalysisFlow => agentAnalysisFlow;
        public ApprovedActivityAnalysisViewModel ApprovedActivityAnalysis => approvedActivityAnalysis;

        private void Awake()
        {
            EnsureSceneInfrastructure();
            ConfigureServices();

            if (bootstrapScreen == null)
            {
                bootstrapScreen = FindObjectOfType<BootstrapScreenView>();
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
                approvedLocationRepository);

            if (bootstrapScreen != null)
            {
                bootstrapScreen.Bind(localStatus, approvedActivityAnalysis);
            }
            else
            {
                Debug.LogWarning("TokenForge bootstrap screen is not assigned.");
            }
        }

        private async void Start()
        {
            if (!runSafeSmokeFlowWhenEmpty && bootstrapSyncMode == BootstrapSyncMode.None)
            {
                await RefreshDashboardAsync();
                return;
            }

            var result = await new BootstrapSmokeFlow(repository, privacySanitizer, null, syncService, backendSyncSmokeFlow)
                .RunIfEmptyThenOptionalSyncAsync(runSafeSmokeFlowWhenEmpty, bootstrapSyncMode);
            if (result.IsSuccess)
            {
                Debug.Log("TokenForge safe bootstrap completed.");
                if (approvedActivityAnalysis != null)
                {
                    await RefreshDashboardAsync();
                }
            }
            else
            {
                Debug.LogWarning("TokenForge safe bootstrap could not complete.");
            }
        }

        private async System.Threading.Tasks.Task RefreshDashboardAsync()
        {
            if (approvedActivityAnalysis == null)
            {
                return;
            }

            await approvedActivityAnalysis.RefreshDashboardAsync();
            if (bootstrapScreen != null)
            {
                bootstrapScreen.Bind(localStatus, approvedActivityAnalysis);
            }
        }

        private void ConfigureServices()
        {
            privacySanitizer = new PrivacySanitizer();
            repository = new SaveDataRepository(null, privacySanitizer);
            approvedLocationRepository = new ApprovedLocationSettingsRepository();
            syncService = null;
            backendSyncSmokeFlow = null;

            if (bootstrapSyncMode == BootstrapSyncMode.RealBackendSmoke)
            {
                var configuration = new ApiConfiguration(apiBaseUrl);
                var logger = new UnitySafeSyncLogger();
                var transport = new SystemNetHttpTransport();
                var authProvider = new DevGuestAuthTokenProvider(configuration, transport, logger);
                var httpClient = new TokenForgeHttpClient(configuration, authProvider, transport, logger);
                syncService = new SyncService(repository, httpClient, null, privacySanitizer);
                backendSyncSmokeFlow = new BackendSyncSmokeFlow(repository, authProvider, httpClient, null, privacySanitizer, logger);
            }
            else if (bootstrapSyncMode != BootstrapSyncMode.None)
            {
                var httpClient = new TokenForgeHttpClient(
                    new ApiConfiguration(apiBaseUrl),
                    new EmptyAuthTokenProvider());
                syncService = new SyncService(repository, httpClient, null, privacySanitizer);
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
