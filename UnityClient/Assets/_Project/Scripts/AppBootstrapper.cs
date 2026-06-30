using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TokenForge.Client.Agents;
using TokenForge.Client.Auth;
using TokenForge.Client.Common;
using TokenForge.Client.Domain;
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
        private const string LogPrefix = "[TokenForgeBootstrap]";
        private const string HierarchyLogPrefix = "[TokenForgeHierarchy]";
        private const string AppBootstrapperVersionMarker = "app-bootstrapper-overlay-projection-v9";
        private const string RuntimeBuildIdentityMarker = "tokenforge_runtime_fix_20260613_mono_crash";
        private const string RuntimeBuildIdentityGitMarker = "git=ef8500e workingTreeHash=167daf548046abe649ba56bd1c67ee1a22fba25ce963be0abfdf8063d8ccf0af";
        private const string BootstrapRootPrefabPath = "Assets/_Project/Prefabs/UI/BootstrapRoot.prefab";
        private const string StartupScenePath = "Assets/_Project/Scenes/TokenForgeMain.unity";
        private const string LegacyBootstrapScenePath = "Assets/_Project/Scenes/Bootstrap.unity";
        private const float MinimumCanvasGroupAlpha = 0.01f;
        private const float MinimumVisibleLuminance = 0.1f;
        private const float MinimumRenderedNonBlackPixelRatio = 0.003f;
        private const int MinimumVisibleRepresentativeStartupTextCount = 3;
        private const float ContentHorizontalInset = 72f;
        private const int ContentTopPadding = 30;
        private const int ContentBottomPadding = 40;
        private const float ContentSectionSpacing = 22f;
        private const int MaximumVisibilityDiagnosticsPerPass = 20;
        private static readonly string[] RepresentativeStartupTexts =
        {
            "TokenForge",
            "Turn your coding activity into a growing desktop companion",
            "Local-first",
            "Optional sync",
            "Run Analysis",
            "Add Repository",
            "Connect AI Agent",
            "My Companion",
            "Today’s Growth",
            "Review Activity"
        };

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
        private IApplicationLifecycleService lifecycleService;
        private INativeDashboardService nativeDashboardService;
        private DesktopCompanionOverlayController nativeDesktopCompanionController;
        private bool nativeDashboardShown;
        private bool prefabUiUnavailableLogged;
        private readonly Queue<NativeDashboardActionRequest> pendingNativeActions = new Queue<NativeDashboardActionRequest>();
        private readonly object pendingNativeActionsLock = new object();
        private string nativeSelectedNavItem = "dashboard";
        private bool nativeAnalysisInProgress;
        private string nativeCurrentAnalysisJobId = string.Empty;
        private string nativeCurrentAnalysisType = string.Empty;
        private string nativeCurrentAnalysisSourceName = string.Empty;
        private string nativeCurrentAnalysisStartedAt = string.Empty;
        private string nativeCurrentAnalysisStep = string.Empty;
        private string nativeSelectedReviewId = string.Empty;
        private bool nativeReviewDetailVisible;
        private string nativeShopTargetType = "aiAgent";
        private string nativeShopSelectedAgentId = "codex";
        private string nativeShopSelectedCategory = "featured";
        private string nativeActionStatusKind = "idle";
        private string nativeActionStatusText = "Ready";
        private string lastNativeDashboardStateJson = string.Empty;
        private float lastUnchangedProjectionLogTime;
        private int nativeProjectionRevision;
        private int unityMainThreadId;
        private bool nativeCompanionDesiredVisible;
        private bool nativeCompanionDesiredVisibleInitialized;
        private string nativeCompanionLastProjectionSource = "startup";
        private bool nativeExplicitQuitRequested;
        private bool runtimeVerificationMode;
        private bool startupIdleLogged;
        private bool nativeStartupStateHydrated;

#if UNITY_EDITOR
        public static bool DisableEditorAssetPrefabLookupForTests { get; set; }
#endif

        public LocalClientStatus LocalStatus => localStatus;
        public GitAnalysisFlowController GitAnalysisFlow => gitAnalysisFlow;
        public AgentAnalysisFlowController AgentAnalysisFlow => agentAnalysisFlow;
        public ApprovedActivityAnalysisViewModel ApprovedActivityAnalysis => approvedActivityAnalysis;
        public bool IsPrefabUiActive => bootstrapRoot != null;
        public bool IsVisibleUiValidated { get; private set; }
        public bool IsBootstrapComplete { get; private set; }
        public bool IsRenderedFrameSmokeSkippedForBatchMode { get; private set; }
        public bool RequirePrefabUi => !UseNativeMacDashboardShell;
        public bool LoadAuthSessionOnStart => loadAuthSessionOnStart;
        public bool RunSafeSmokeFlowWhenEmpty => runSafeSmokeFlowWhenEmpty;
        public int LastViewportVisibleCandidateCount { get; private set; }
        public float LastRenderedFrameAverageLuminance { get; private set; }
        public float LastRenderedFrameNonBlackPixelRatio { get; private set; }
        public float LastRenderedFrameBrightestPixelLuminance { get; private set; }
        public string LastBootstrapRootSource { get; private set; } = string.Empty;
        public string LastBootstrapRootPrefabPath { get; private set; } = string.Empty;
        public string LastBootstrapRootUiVersion { get; private set; } = string.Empty;
        public int LastRemovedStaleUnityDashboardRootCount { get; private set; }

        private static bool UseNativeMacDashboardShell
        {
            get
            {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LogManagedStartupEnter()
        {
            Debug.Log("INFO [ManagedStartup][ENTER] phase=BeforeSceneLoad runtimeMarker=" + RuntimeBuildIdentityMarker +
                      " csharpMarker=" + AppBootstrapperVersionMarker +
                      " " + RuntimeBuildIdentityGitMarker);
            Debug.Log("INFO [BuildIdentity][RUNTIME_CODE_VERSION] " + RuntimeBuildIdentityMarker +
                      " csharpMarker=" + AppBootstrapperVersionMarker +
                      " " + RuntimeBuildIdentityGitMarker +
                      " phase=BeforeSceneLoad");
        }

        private void Awake()
        {
            Debug.Log("INFO [ManagedStartup][ENTER] phase=AppBootstrapper.Awake runtimeMarker=" + RuntimeBuildIdentityMarker);
            Debug.Log("INFO [StartupDiagnostic][BEGIN]");
            Debug.Log("INFO [StartupDiagnostic][CSharpBootstrap_BEGIN]");
            unityMainThreadId = Thread.CurrentThread.ManagedThreadId;
            runtimeVerificationMode = IsRuntimeVerificationMode();
            LogNativeSafeModeState();
            if (runtimeVerificationMode)
            {
                Debug.Log("INFO [RuntimeVerify][ENABLED] source=AppBootstrapper dashboardAutoOpen=false overlayAutoShow=false");
                Debug.Log("INFO [CrashRecovery][SUPPRESSED_REPORT_UI] reason=verificationMode source=AppBootstrapper");
            }

            Debug.Log("INFO [Startup] AppBootstrapper begin");
            Debug.Log("INFO " + LogPrefix + " TokenForge bootstrap starting.");
            Debug.Log("INFO [StartupDiagnostic][CSharpBootstrap_OK]");
            Debug.Log("INFO [StartupDiagnostic][NATIVE_BRIDGE_LOAD_BEGIN]");
            LogRuntimeBuildIdentity();
            Debug.Log("INFO [StartupDiagnostic][NATIVE_BRIDGE_LOAD_OK]");
            LogStartupScene();
            LogHierarchyDump("AwakeStart");
            lifecycleService = new MacApplicationLifecycleService();
            Debug.Log("INFO [StartupDiagnostic][APPKIT_INIT_BEGIN]");
            Debug.Log("INFO [StartupDiagnostic][STATUS_ITEM_INIT_BEGIN]");
            if (NativeSafeModeEnabled || DisableStatusItemEnabled)
            {
                Debug.Log("INFO [StartupDiagnostic][STATUS_ITEM_INIT_OK] skipped=true reason=" + NativeSkipReason("statusItem"));
            }
            else
            {
                lifecycleService.Install();
                Debug.Log("INFO [StartupDiagnostic][STATUS_ITEM_INIT_OK]");
            }
            Debug.Log("INFO [StartupDiagnostic][APPKIT_INIT_OK]");
            EnsureSceneInfrastructure();
            LogHierarchyDump("AfterEnsureSceneInfrastructure");
            ConfigureServices();

            if (bootstrapRoot == null)
            {
                bootstrapRoot = FindSceneObject<BootstrapRootView>();
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

            if (UseNativeMacDashboardShell)
            {
                Debug.Log("INFO [Bootstrap] uiMode=nativeAppKit");
                Debug.Log("INFO [NativeDashboard] mode=macOSPlayer source=AppKit");
                Debug.Log("INFO [BootstrapRoot] productUI=disabled reason=nativeShell");
                EnsureNativeDashboardShell();
                // The persisted repository projection is loaded asynchronously in Start(). Opening
                // the AppKit window here exposes the default, unhydrated onboarding state for a
                // frame (or longer on a cold disk) even when an approved repository already exists.
                // Keep the native shell installed but invisible until RefreshDashboardAsync has
                // loaded and normalized persisted state and selected the first real route.
                Debug.Log("INFO [LaunchRouteDiagnostic] bootState=waitingForPersistedRepository firstVisibleRoute=deferred");
            }
            else if (bootstrapRoot != null)
            {
                bootstrapRoot.Bind(localStatus, approvedActivityAnalysis);
                LogHierarchyDump("AfterBootstrapRootBind");
                if (bootstrapRoot.ValidateReferences(out var error))
                {
                    Debug.Log("INFO " + LogPrefix + " TokenForge bootstrap UI references ready.");
                }
                else
                {
                    Debug.LogError("ERROR " + LogPrefix + " BootstrapRootView serialized reference validation failed: " + error);
                }
            }
            else
            {
                Debug.Log("INFO [Bootstrap] uiMode=unityFallback");
                if (!prefabUiUnavailableLogged)
                {
                    Debug.LogError("ERROR " + LogPrefix + " BootstrapRootView missing");
                }
            }

            Debug.Log("INFO [ManagedStartup][AFTER_BOOTSTRAP] phase=AppBootstrapper.Awake bootstrapComplete=" + IsBootstrapComplete +
                      " nativeShell=" + UseNativeMacDashboardShell +
                      " runtimeVerificationMode=" + runtimeVerificationMode);
        }

        private void Update()
        {
            if (!UseNativeMacDashboardShell)
            {
                return;
            }

            while (TryDequeueNativeAction(out var request))
            {
                RouteNativeDashboardAction(request);
            }
        }

        private async void Start()
        {
            StartupResult result;
            try
            {
                result = await RunStartupAsync();
            }
            catch (Exception exception)
            {
                Debug.LogError("ERROR " + LogPrefix + " startup task failed: " + exception.Message);
                return;
            }

            if (!result.ShouldComplete)
            {
                Debug.LogWarning("WARN " + LogPrefix + " TokenForge safe bootstrap could not complete.");
                return;
            }

            for (var frame = 0; frame < 10; frame++)
            {
                await Task.Yield();
            }

            if (Application.isBatchMode)
            {
                await Task.Yield();
            }

            CompleteBootstrap(result.CompletionMessage);
        }

        private async Task<StartupResult> RunStartupAsync()
        {
            if (!runSafeSmokeFlowWhenEmpty && bootstrapSyncMode == BootstrapSyncMode.None)
            {
                await RefreshDashboardAsync(UseNativeMacDashboardShell ? false : loadAuthSessionOnStart, UseNativeMacDashboardShell);
                return new StartupResult(true, "TokenForge bootstrap completed.");
            }

            var result = await new BootstrapSmokeFlow(repository, privacySanitizer, null, syncService, backendSyncSmokeFlow)
                .RunIfEmptyThenOptionalSyncAsync(runSafeSmokeFlowWhenEmpty, bootstrapSyncMode);
            if (result.IsSuccess)
            {
                if (approvedActivityAnalysis != null)
                {
                    await RefreshDashboardAsync(loadAuthSessionOnStart, UseNativeMacDashboardShell);
                }

                return new StartupResult(true, "TokenForge safe bootstrap completed.");
            }

            return new StartupResult(false, string.Empty);
        }

        private async System.Threading.Tasks.Task RefreshDashboardAsync(bool loadAuthSession, bool startupScope = false)
        {
            if (approvedActivityAnalysis == null)
            {
                return;
            }

            if (startupScope)
            {
                Debug.Log("INFO [Startup] Save load begin");
            }

            if (loadAuthSession)
            {
                await approvedActivityAnalysis.RefreshDashboardAsync();
            }
            else
            {
                await approvedActivityAnalysis.RefreshApprovedLocationsAsync();
                await approvedActivityAnalysis.RestoreLocalSelectionsFromApprovedLocationsAsync();
                await approvedActivityAnalysis.RefreshRecentSessionsAsync();
                if (!startupScope)
                {
                    await approvedActivityAnalysis.RefreshSafeSyncLocalStateAsync();
                }
            }

            if (startupScope)
            {
                Debug.Log("INFO [Startup] Save load end");
            }

            if (startupScope && UseNativeMacDashboardShell)
            {
                nativeStartupStateHydrated = true;
                var connectedRepositories = approvedActivityAnalysis?.RepositoryCompanions ?? new List<RepositoryCompanionDisplayItem>();
                var activeRepository = approvedActivityAnalysis?.CharacterDashboard;
                var hasActiveApprovedRepository = ActiveRepositoryReadyForNative();
                nativeSelectedNavItem = "dashboard";
                var onboardingPreferences = approvedActivityAnalysis?.CurrentSaveData?.OnboardingPreferences ?? new OnboardingPreferences();
                var firstVisibleRoute = !hasActiveApprovedRepository &&
                                        !onboardingPreferences.FirstRunOnboardingCompleted &&
                                        !onboardingPreferences.FirstRunOnboardingDismissedForNow
                    ? "onboarding"
                    : "dashboard";
                Debug.Log("INFO [LaunchRouteDiagnostic] loadedRepositoriesCount=" + connectedRepositories.Count);
                Debug.Log("INFO [LaunchRouteDiagnostic] activeRepositoryId=" + SafeNativeText(activeRepository?.CurrentRepositoryHash, "none") +
                          " activeRepositoryName=" + SafeNativeText(activeRepository?.CurrentRepositoryAlias, "none") +
                          " approved=" + hasActiveApprovedRepository);
                Debug.Log("INFO [LaunchRouteDiagnostic] firstVisibleRoute=" + firstVisibleRoute + " hydrationComplete=true");
            }

            if (bootstrapRoot != null)
            {
                bootstrapRoot.Bind(localStatus, approvedActivityAnalysis);
            }

            if (UseNativeMacDashboardShell)
            {
                if (startupScope)
                {
                    Debug.Log("INFO [Startup] State projection begin");
                }

                ApplyNativeShellState(showDashboardIfNeeded: startupScope);
                if (startupScope)
                {
                    var finalPreferences = approvedActivityAnalysis?.CurrentSaveData?.OnboardingPreferences ?? new OnboardingPreferences();
                    var finalRoute = !ActiveRepositoryReadyForNative() &&
                                     !finalPreferences.FirstRunOnboardingCompleted &&
                                     !finalPreferences.FirstRunOnboardingDismissedForNow
                        ? "onboarding"
                        : "dashboard";
                    Debug.Log("INFO [LaunchRouteDiagnostic] finalRouteAfterHydration=" + finalRoute +
                              " windowRequested=" + (!runtimeVerificationMode));
                    Debug.Log("INFO [Startup] State projection end");
                }
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

        private void EnsureNativeDashboardShell()
        {
            if (!UseNativeMacDashboardShell)
            {
                return;
            }

            Debug.Log("INFO [StartupDiagnostic][DASHBOARD_INIT_BEGIN]");
            if (NativeSafeModeEnabled || DisableNativeDashboardEnabled)
            {
                Debug.Log("INFO [StartupDiagnostic][DASHBOARD_INIT_OK] skipped=true reason=" + NativeSkipReason("dashboard"));
                Debug.Log("INFO [StartupDiagnostic][RIGHT_CLICK_MENU_INIT_BEGIN]");
                Debug.Log("INFO [StartupDiagnostic][RIGHT_CLICK_MENU_INIT_OK] skipped=true reason=" + NativeSkipReason("contextMenu"));
                Debug.Log("INFO [StartupDiagnostic][OVERLAY_INIT_BEGIN]");
                Debug.Log("INFO [StartupDiagnostic][OVERLAY_INIT_OK] skipped=true reason=" + NativeSkipReason("overlay"));
                Debug.Log("INFO [StartupDiagnostic][PIXEL_RENDERER_INIT_BEGIN]");
                Debug.Log("INFO [StartupDiagnostic][PIXEL_RENDERER_INIT_OK] skipped=true reason=" + NativeSkipReason("pixelRenderer"));
                Debug.Log("INFO [StartupDiagnostic][MOVEMENT_TIMER_INIT_BEGIN]");
                Debug.Log("INFO [StartupDiagnostic][MOVEMENT_TIMER_INIT_OK] skipped=true reason=" + NativeSkipReason("movementTimer"));
                return;
            }

            if (nativeDashboardService == null)
            {
                Debug.Log("INFO [Startup] Native dashboard init begin");
                nativeDashboardService = new MacNativeDashboardService();
                nativeDashboardService.ActionRequested -= HandleNativeDashboardAction;
                nativeDashboardService.ActionRequested += HandleNativeDashboardAction;
                nativeDashboardService.Install();
                Debug.Log("INFO [Startup] Native dashboard init end");
            }
            Debug.Log("INFO [StartupDiagnostic][DASHBOARD_INIT_OK]");
            Debug.Log("INFO [StartupDiagnostic][RIGHT_CLICK_MENU_INIT_BEGIN]");
            Debug.Log("INFO [StartupDiagnostic][RIGHT_CLICK_MENU_INIT_OK] deferred=true owner=nativeOverlayContextMenu");

            Debug.Log("INFO [StartupDiagnostic][OVERLAY_INIT_BEGIN]");
            if (DisableNativeOverlayEnabled)
            {
                Debug.Log("INFO [StartupDiagnostic][OVERLAY_INIT_OK] skipped=true reason=" + NativeSkipReason("overlay"));
                Debug.Log("INFO [StartupDiagnostic][PIXEL_RENDERER_INIT_BEGIN]");
                Debug.Log("INFO [StartupDiagnostic][PIXEL_RENDERER_INIT_OK] skipped=true reason=" + NativeSkipReason("pixelRenderer"));
                Debug.Log("INFO [StartupDiagnostic][MOVEMENT_TIMER_INIT_BEGIN]");
                Debug.Log("INFO [StartupDiagnostic][MOVEMENT_TIMER_INIT_OK] skipped=true reason=" + NativeSkipReason("movementTimer"));
                return;
            }

            if (nativeDesktopCompanionController == null)
            {
                var controllerObject = new GameObject("NativeDesktopCompanionController");
                DontDestroyOnLoad(controllerObject);
                nativeDesktopCompanionController = controllerObject.AddComponent<DesktopCompanionOverlayController>();
                nativeDesktopCompanionController.Initialize(null, lifecycleService);
                nativeDesktopCompanionController.PositionChanged -= HandleNativeCompanionPositionChanged;
                nativeDesktopCompanionController.PositionChanged += HandleNativeCompanionPositionChanged;
                nativeDesktopCompanionController.RepositoryPositionChanged -= HandleNativeRepositoryCompanionPositionChanged;
                nativeDesktopCompanionController.RepositoryPositionChanged += HandleNativeRepositoryCompanionPositionChanged;
                nativeDesktopCompanionController.DashboardRestoreRequested -= HandleNativeCompanionDashboardRestoreRequested;
                nativeDesktopCompanionController.DashboardRestoreRequested += HandleNativeCompanionDashboardRestoreRequested;
            }
            Debug.Log("INFO [StartupDiagnostic][OVERLAY_INIT_OK]");
            Debug.Log("INFO [StartupDiagnostic][PIXEL_RENDERER_INIT_BEGIN]");
            Debug.Log("INFO [StartupDiagnostic][PIXEL_RENDERER_INIT_OK] disabled=" + DisablePixelNativeRendererEnabled);
            Debug.Log("INFO [StartupDiagnostic][MOVEMENT_TIMER_INIT_BEGIN]");
            Debug.Log("INFO [StartupDiagnostic][MOVEMENT_TIMER_INIT_OK] disabled=" + DisableNativeOverlayEnabled);
        }

        private void HandleNativeCompanionDashboardRestoreRequested()
        {
            OpenNativeDashboardCanonical("csharp.dashboard");
        }

        private void OpenNativeDashboardCanonical(string reason)
        {
            if (nativeExplicitQuitRequested)
            {
                Debug.Log("INFO [DashboardLifecycle][SUPPRESS_REOPEN] reason=explicitQuit source=" + SafeNativeText(reason, "unknown"));
                return;
            }

            nativeDashboardShown = true;
            var source = NormalizeDashboardOpenSource(reason);
            Debug.Log("INFO [WindowLifecycle] openDashboard route reason=" + SafeNativeText(reason, "unknown") + " source=" + source);
            nativeDashboardService?.ShowDashboardWindow(source);
        }

        private void RequestInitialNativeDashboardOpen(string source)
        {
            if (!UseNativeMacDashboardShell || runtimeVerificationMode || nativeDashboardService == null || nativeDashboardShown)
            {
                return;
            }

            Debug.Log("INFO [LaunchDashboard][REQUEST] source=launch.initial appReady=false pending=true csharpSource=" + SafeNativeText(source, "startup"));
            OpenNativeDashboardCanonical("launch.initial");
        }

        private static string NormalizeDashboardOpenSource(string reason)
        {
            var safeReason = SafeNativeText(reason, "csharp.dashboard");
            if (string.Equals(safeReason, "menubar.dashboard", StringComparison.Ordinal) ||
                string.Equals(safeReason, "dock.reopen", StringComparison.Ordinal) ||
                string.Equals(safeReason, "launch.initial", StringComparison.Ordinal) ||
                string.Equals(safeReason, "csharp.dashboard", StringComparison.Ordinal))
            {
                return safeReason;
            }

            if (string.Equals(safeReason, "startup", StringComparison.Ordinal))
            {
                return "launch.initial";
            }

            return "csharp.dashboard";
        }

        private void HandleNativeCompanionPositionChanged(Vector2 position)
        {
            if (approvedActivityAnalysis == null)
            {
                return;
            }

            _ = SaveNativeCompanionPositionAsync(position);
        }

        private void HandleNativeRepositoryCompanionPositionChanged(string repositoryId, Vector2 position)
        {
            if (approvedActivityAnalysis == null || string.IsNullOrWhiteSpace(repositoryId))
            {
                return;
            }

            _ = SaveNativeRepositoryCompanionPositionAsync(repositoryId, position);
        }

        private async Task SaveNativeCompanionPositionAsync(Vector2 position)
        {
            await approvedActivityAnalysis.SaveDesktopCompanionPositionAsync(position.x, position.y);
            Debug.Log("INFO [OverlayPositionSync][COMMIT] source=dragEnd position=(" + position.x.ToString("0.##") + "," + position.y.ToString("0.##") + ")");
        }

        private async Task SaveNativeRepositoryCompanionPositionAsync(string repositoryId, Vector2 position)
        {
            await approvedActivityAnalysis.SaveDesktopCompanionPositionForRepositoryAsync(repositoryId, position.x, position.y);
            Debug.Log("INFO [OverlayPositionSync][COMMIT] repo=" + repositoryId + " source=dragEnd position=(" + position.x.ToString("0.##") + "," + position.y.ToString("0.##") + ")");
        }

        private void ApplyNativeShellState(bool showDashboardIfNeeded)
        {
            if (!UseNativeMacDashboardShell)
            {
                return;
            }

            if (NativeSafeModeEnabled)
            {
                Debug.Log("INFO [NativeSafeMode][SKIP] function=ApplyNativeShellState reason=TOKENFORGE_NATIVE_SAFE_MODE");
                return;
            }

            if (nativeExplicitQuitRequested)
            {
                Debug.Log("INFO [DashboardLifecycle][SUPPRESS_REOPEN] reason=explicitQuit source=applyNativeShellState");
                return;
            }

            if (showDashboardIfNeeded && !nativeStartupStateHydrated)
            {
                Debug.Log("INFO [LaunchRouteDiagnostic] firstVisibleRoute=deferred reason=persistedStateNotHydrated");
                return;
            }

            EnsureNativeDashboardShell();
            var companionSettings = RepositoryCompanionProfileService.CloneDesktopCompanionSettings(
                approvedActivityAnalysis?.CharacterDashboard?.DesktopCompanionSettings ?? DesktopCompanionSettings.CreateDefault());
            var hasRepositoryOverlayFarm = (approvedActivityAnalysis?.RepositoryCompanions ?? new List<RepositoryCompanionDisplayItem>())
                .Any(item => item != null &&
                             !item.Archived &&
                             item.ApprovedByUser &&
                             !string.IsNullOrWhiteSpace(item.RepositoryHash) &&
                             !string.Equals(item.RepositoryHash, RepositoryCompanionProfileService.DefaultLocalRepositoryHash, StringComparison.Ordinal));
            if (!nativeCompanionDesiredVisibleInitialized)
            {
                nativeCompanionDesiredVisible = runtimeVerificationMode || !hasRepositoryOverlayFarm ? false : companionSettings.IsDesktopCompanionEnabled;
                nativeCompanionDesiredVisibleInitialized = true;
                nativeCompanionLastProjectionSource = runtimeVerificationMode ? "verificationMode" : (hasRepositoryOverlayFarm ? "initial_profile" : "noRepository");
                if (runtimeVerificationMode && companionSettings.IsDesktopCompanionEnabled)
                {
                    Debug.Log("INFO [DashboardLifecycle][SUPPRESS_REOPEN] reason=verificationMode source=initialOverlayProjection");
                }
            }

            if (!hasRepositoryOverlayFarm)
            {
                nativeCompanionDesiredVisible = false;
                nativeCompanionLastProjectionSource = "noRepository";
                Debug.Log("INFO [Overlay][Guard] ignored stale update reason=noApprovedRepository desiredForced=false");
            }

            if (nativeCompanionDesiredVisible && !companionSettings.IsDesktopCompanionEnabled)
            {
                Debug.Log("INFO [OverlayTrace] projection_hide_blocked reason=desired_visible_true");
            }

            companionSettings.IsDesktopCompanionEnabled = nativeCompanionDesiredVisible;
            Debug.Log("INFO [Overlay][Desired] visible=" + nativeCompanionDesiredVisible +
                      " source=" + SafeNativeText(nativeCompanionLastProjectionSource, "projection") +
                      " repositoryFarm=" + hasRepositoryOverlayFarm);
            var dashboardSnapshot = approvedActivityAnalysis?.CharacterDashboard;
            if (nativeDesktopCompanionController != null && nativeDesktopCompanionController.IsAnyOverlayDragging())
            {
                Debug.Log("INFO [CSharpProjection][SKIP_TO_NATIVE] repo=unknown reason=overlayDragInProgress");
                Debug.Log("INFO [DashboardLifecycle][REDRAW_SUPPRESSED] reason=overlayDrag repo=unknown");
                Debug.Log("INFO [DashboardProjection][SKIP] reason=overlayDrag repo=unknown");
                Debug.Log("INFO [DashboardState][UNCHANGED_DURING_DRAG] repo=unknown");
            }
            else
            {
                Debug.Log(hasRepositoryOverlayFarm
                    ? "INFO [FarmProjection][LEGACY_ACTIVE_OVERLAY_SKIPPED] reason=repositoryKeyedFarmActive"
                    : "INFO [DashboardPlaceholder][RENDER] reason=noRepository");
                if (!hasRepositoryOverlayFarm)
                {
                    Debug.Log("INFO [DashboardPlaceholder][NOT_PERSISTED]");
                    Debug.Log("INFO [FarmProjection][SKIP_PLACEHOLDER] reason=noRepository");
                    nativeDesktopCompanionController?.HideLegacyOverlay("csharp.dashboardPlaceholder");
                }
            }

            nativeDesktopCompanionController?.ApplyFarmSettings(
                approvedActivityAnalysis?.RepositoryCompanions,
                companionSettings,
                nativeCompanionDesiredVisible,
                nativeProjectionRevision + 1);

            var state = BuildNativeDashboardState();
            state.companionVisible = nativeCompanionDesiredVisible;
            if (nativeCompanionDesiredVisible && string.Equals(state.companion.mood, "hidden", StringComparison.Ordinal))
            {
                state.companion.mood = string.IsNullOrWhiteSpace(state.companion.motion?.mood) ? "active" : state.companion.motion.mood;
            }

            Debug.Log("INFO [OverlayTrace] projection_applied traceId=unity desiredVisible=" + nativeCompanionDesiredVisible +
                      " nativeVisible=" + state.companionVisible +
                      " motion=" + state.wanderEnabled +
                      " clickThrough=" + state.clickThroughEnabled +
                      " source=" + nativeCompanionLastProjectionSource);
            Debug.Log("INFO [OverlayState][PROJECTED_TO_NATIVE] desiredVisible=" + state.desiredVisible +
                      " actualVisible=" + state.actualVisible +
                      " dragEnabled=" + state.dragEnabled +
                      " source=" + nativeCompanionLastProjectionSource);
            Debug.Log("INFO [OverlayState][DASHBOARD_RENDER] desiredVisible=" + state.desiredVisible +
                      " actualVisible=" + state.actualVisible +
                      " dragEnabled=" + state.dragEnabled +
                      " clickThrough=" + state.clickThroughEnabled +
                      " revision=" + state.stateRevision);
            var stateJson = state.ToJson();
            var changed = !string.Equals(lastNativeDashboardStateJson, stateJson, StringComparison.Ordinal);
            if (changed)
            {
                Debug.Log("INFO [DashboardState] projected changed=true");
                lastNativeDashboardStateJson = stateJson;
                if (nativeDesktopCompanionController != null && nativeDesktopCompanionController.IsAnyOverlayDragging())
                {
                    Debug.Log("INFO [DashboardLifecycle][REDRAW_SUPPRESSED] reason=overlayDrag repo=unknown");
                    Debug.Log("INFO [DashboardProjection][SKIP] reason=overlayDrag repo=unknown");
                    Debug.Log("INFO [DashboardState][UNCHANGED_DURING_DRAG] repo=unknown");
                }
                else
                {
                    nativeDashboardService.UpdateDashboardState(state);
                    nativeDashboardService.SetMenuBarStatus(state);
                    Debug.Log("INFO [DashboardProjection] selectedTab=" + state.selectedNavItem +
                              " activeRepositoryId=" + SafeNativeText(state.repository.id, "none") +
                              " activeCompanionId=" + SafeNativeText(state.companion.motion.repositoryId, "none") +
                              " overlayVisible=" + state.companionVisible +
                              " motionEnabled=" + state.wanderEnabled);
                }
                Debug.Log("INFO [MenuBarProjection] providersShown=" + state.statusText + " hiddenReason=" + (state.providerUsagePercentages.Any(item => item.hasSavedApprovedActivity) ? "none" : "noSavedAgentAnalysis"));
            }
            else if (Time.realtimeSinceStartup - lastUnchangedProjectionLogTime > 2f)
            {
                lastUnchangedProjectionLogTime = Time.realtimeSinceStartup;
                Debug.Log("INFO [DashboardState] projected changed=false");
            }

            if (showDashboardIfNeeded && !runtimeVerificationMode && !nativeDashboardShown)
            {
                nativeDashboardShown = true;
                OpenNativeDashboardCanonical("startup");
            }
        }

        private NativeDashboardState BuildNativeDashboardState()
        {
            var dashboard = approvedActivityAnalysis?.CharacterDashboard ?? new CharacterDashboardSummary();
            var companion = dashboard.CompanionState ?? CompanionState.CreateDefault();
            var settings = dashboard.DesktopCompanionSettings ?? DesktopCompanionSettings.CreateDefault();
            var projectionSaveData = approvedActivityAnalysis?.CurrentSaveData;
            var codexConnected = approvedActivityAnalysis != null
                && approvedActivityAnalysis.Onboarding.AgentSources.Any(source => source.SourceType == ConnectedAgentSourceType.Codex && IsAgentReadyForNative(source) && NativeAgentFlowMatchesSource(source));
            var connectedRepositories = approvedActivityAnalysis == null
                ? new List<RepositoryCompanionDisplayItem>()
                : (approvedActivityAnalysis.RepositoryCompanions ?? new List<RepositoryCompanionDisplayItem>())
                    .Where(item => !item.Archived &&
                                   item.ApprovedByUser &&
                                   !string.IsNullOrWhiteSpace(item.RepositoryHash) &&
                                   !string.Equals(item.RepositoryHash, RepositoryCompanionProfileService.DefaultLocalRepositoryHash, StringComparison.Ordinal))
                    .ToList();
            var repositoryConnected = ActiveRepositoryReadyForNative();

            var state = NativeDashboardState.CreateDefault();
            state.connection = "local";
            state.stateRevision = ++nativeProjectionRevision;
            state.sync = approvedActivityAnalysis != null && approvedActivityAnalysis.AuthState == AuthState.LoggedIn ? "connected" : "optional";
            state.appTitle = "TokenForge";
            state.subtitle = "Track Git and AI-assisted work as companion growth.";
            state.isLocalMode = true;
            state.syncStatusText = state.sync == "connected" ? "Safe sync connected" : "Optional sync";
            var onboardingPreferences = projectionSaveData?.OnboardingPreferences ?? new OnboardingPreferences();
            state.selectedNavItem = string.IsNullOrWhiteSpace(nativeSelectedNavItem) ? "dashboard" : nativeSelectedNavItem;
            state.primaryActionEnabled = true;
            state.isAnalysisRunning = nativeAnalysisInProgress;
            state.actionStatusKind = string.IsNullOrWhiteSpace(nativeActionStatusKind) ? "idle" : nativeActionStatusKind;
            state.actionStatusText = SafeNativeText(nativeActionStatusText, "Ready");
            var pendingNativeReview = approvedActivityAnalysis?.PendingNativeActivityReview;
            state.pendingReviewCount = pendingNativeReview != null ? 1 : 0;
            state.hasPendingReview = pendingNativeReview != null;
            state.pendingEstimatedXP = Math.Max(0, pendingNativeReview?.EstimatedXpDelta ?? 0);
            state.warningCount = Math.Max(0, (approvedActivityAnalysis?.RetryQueueSummary?.PendingCount ?? 0) + (approvedActivityAnalysis?.TombstoneSummary?.PendingDeleteCount ?? 0));
            state.lastRunSummary = FriendlyNativeSummary(dashboard.LatestSafeSessionSummary, "No saved growth yet. Run Analysis on a repository or AI agent log to generate your first XP.");
            state.growthBasis = SafeNativeText(dashboard.GrowthBasis, "Full local Git history");
            state.growthFirstCommit = SafeNativeText(dashboard.GrowthFirstCommit, string.Empty);
            state.growthFirstCommitDate = SafeNativeText(dashboard.GrowthFirstCommitDate, string.Empty);
            state.growthCurrentHead = SafeNativeText(dashboard.GrowthCurrentHead, string.Empty);
            state.growthLastAnalyzedCommit = SafeNativeText(dashboard.GrowthLastAnalyzedCommit, string.Empty);
            state.growthCommitsAnalyzed = Math.Max(0, dashboard.GrowthCommitsAnalyzed);
            state.growthFilesChanged = Math.Max(0, dashboard.GrowthFilesChanged);
            state.growthProjectionSource = SafeNativeText(dashboard.GrowthProjectionSource, "none");
            state.growthFallbackUsed = dashboard.GrowthFallbackUsed;
            state.growthCacheHit = dashboard.GrowthCacheHit;
            state.growthReasonIfUnchanged = SafeNativeText(dashboard.GrowthReasonIfUnchanged, string.Empty);
            state.codeStat = Math.Max(0, dashboard.Code);
            state.focusStat = Math.Max(0, dashboard.Focus);
            state.debugStat = Math.Max(0, dashboard.Debug);
            state.designStat = Math.Max(0, dashboard.Design);
            state.syncStat = Math.Max(0, dashboard.Sync);
            state.weeklyCodeStat = Math.Max(0, dashboard.WeeklyCode);
            state.weeklyFocusStat = Math.Max(0, dashboard.WeeklyFocus);
            state.weeklyDebugStat = Math.Max(0, dashboard.WeeklyDebug);
            state.weeklyDesignStat = Math.Max(0, dashboard.WeeklyDesign);
            state.weeklySyncStat = Math.Max(0, dashboard.WeeklySync);
            state.hasGrowthAxisData = dashboard.HasGrowthAxisData;
            state.hasLegacyGrowthAxisGap = dashboard.HasLegacyGrowthAxisGap;
            state.growthAxisDataStatusText = SafeNativeText(dashboard.GrowthAxisDataStatusText, "No axis data recorded yet.");
            state.dominantGrowthPath = SafeNativeText(dashboard.DominantGrowthPath, "Unknown");
            state.secondaryGrowthTrait = SafeNativeText(dashboard.SecondaryGrowthTrait, "Unknown");
            state.currentEvolutionBias = SafeNativeText(dashboard.CurrentEvolutionBias, "Unknown");
            state.nextEvolutionPreview = SafeNativeText(dashboard.NextEvolutionPreview, "Repository Hatchling");
            state.eggInfluenceText = SafeNativeText(dashboard.EggInfluenceText, "Connect a repository to start shaping a companion.");
            state.tokenCurrencyName = SafeNativeText(dashboard.TokenCurrencyName, "Forge Coins");
            state.tokenCurrencyBalance = Math.Max(0, dashboard.TokenCurrencyBalance);
            state.tokenUsageTrackingEnabled = dashboard.TokenUsageTrackingEnabled;
            state.companionVisible = repositoryConnected && settings.IsDesktopCompanionEnabled;
            state.wanderEnabled = settings.MotionMode != CompanionDesktopMotionMode.Calm;
            state.clickThroughEnabled = settings.IsClickThroughEnabled;
            state.clickReactionEnabled = !settings.IsClickThroughEnabled;
            state.desiredVisible = repositoryConnected && (nativeCompanionDesiredVisibleInitialized ? nativeCompanionDesiredVisible : settings.IsDesktopCompanionEnabled);
            state.actualVisible = NativeOverlayActuallyVisible();
            state.movementEnabled = state.wanderEnabled;
            state.overlayMode = connectedRepositories.Count > 1 ? "allConnectedRepos" : "selectedRepoCompanion";
            state.movementMode = state.overlayMode;
            state.dragEnabled = !state.clickThroughEnabled;
            state.panelExists = nativeDesktopCompanionController != null &&
                                nativeDesktopCompanionController.OverlayState != CompanionDesktopOverlayState.Unavailable;
            state.panelFrame = settings.HasSavedOverlayPosition
                ? settings.LastOverlayPositionX.ToString("0.#") + "," + settings.LastOverlayPositionY.ToString("0.#")
                : string.Empty;
            state.selectedRepoHash = repositoryConnected ? dashboard.CurrentRepositoryHash ?? string.Empty : string.Empty;
            state.repoApproved = repositoryConnected;
            state.movementPaused = !state.movementEnabled || state.clickThroughEnabled;
            state.overlayLastAction = SafeNativeText(nativeCompanionLastProjectionSource, "startup");
            state.overlayLastError = string.Equals(nativeActionStatusKind, "error", StringComparison.Ordinal)
                ? SafeNativeText(nativeActionStatusText, string.Empty)
                : string.Empty;
            state.explicitQuitRequested = nativeExplicitQuitRequested;
            state.dashboardVisible = nativeDashboardShown;
            state.lastKnownFrame = settings.HasSavedOverlayPosition
                ? settings.LastOverlayPositionX.ToString("0.#") + "," + settings.LastOverlayPositionY.ToString("0.#")
                : string.Empty;
            state.companion.name = string.IsNullOrWhiteSpace(dashboard.CharacterName) ? "Token" : dashboard.CharacterName;
            state.companion.stage = companion.Stage.ToString();
            state.companion.stageIndex = (int)companion.Stage;
            state.companion.level = Math.Max(1, companion.Level);
            state.companion.xp = Math.Max(0, dashboard.CurrentLevelExp);
            state.persistedCompanionXP = Math.Max(0, dashboard.TotalExp);
            state.companion.xpToNextLevel = Math.Max(1, dashboard.ExpForNextLevel);
            state.companion.totalLifetimeXP = Math.Max(0, dashboard.TotalExp);
            state.companion.canLevelUp = companion.CanLevelUp;
            state.companion.evolveActionVisible = companion.CanLevelUp;
            state.companion.evolveActionHiddenReason = companion.CanLevelUp
                ? string.Empty
                : LevelUpHiddenReason(repositoryConnected, dashboard.CurrentRepositoryHash, dashboard.CurrentLevelExp, dashboard.ExpForNextLevel);
            state.companion.xpProgressRatio = dashboard.ExpForNextLevel <= 0
                ? 0f
                : Mathf.Clamp01((float)Math.Max(0, dashboard.CurrentLevelExp) / Math.Max(1, dashboard.ExpForNextLevel));
            state.companion.xpStatusText = companion.CanLevelUp
                ? Math.Max(0, dashboard.CurrentLevelExp) + " XP · " + Math.Max(1, dashboard.ExpForNextLevel) + " XP required · Ready to evolve"
                : Math.Max(0, dashboard.CurrentLevelExp) + "/" + Math.Max(1, dashboard.ExpForNextLevel) + " XP · " + Math.Max(0, dashboard.ExpForNextLevel - dashboard.CurrentLevelExp) + " XP to next growth";
            state.companion.carryForwardText = companion.CanLevelUp
                ? Math.Max(0, dashboard.CurrentLevelExp - dashboard.ExpForNextLevel) + " XP will carry over after evolution"
                : string.Empty;
            state.companion.levelUpStatusText = companion.CanLevelUp
                ? state.companion.xpStatusText + ". " + state.companion.carryForwardText + "."
                : "Earn " + Math.Max(0, dashboard.ExpForNextLevel - dashboard.CurrentLevelExp) + " more XP to level up.";
            state.companion.levelUpDisabledReason = companion.CanLevelUp ? string.Empty : state.companion.levelUpStatusText;
            state.companion.motion = ToNativeMotionState(dashboard.MotionState);
            state.companion.mood = repositoryConnected && settings.IsDesktopCompanionEnabled ? state.companion.motion.mood : "hidden";
            state.companion.dashboardAnimationState = DashboardAnimationStateFor(dashboard.MotionState, companion.CanLevelUp);
            state.companion.skin = CompanionSkinCatalog.Normalize(settings.VisualThemeId);
            state.companion.zodiacType = RepositoryCompanionProfileService.NormalizeZodiacTypeId(settings.ZodiacTypeId, "repository");
            state.companion.zodiacLabel = RepositoryCompanionProfileService.ZodiacDisplayName(state.companion.zodiacType);
            state.repository.connected = repositoryConnected;
            state.hasActiveRepository = repositoryConnected;
            state.repository.id = repositoryConnected ? dashboard.CurrentRepositoryHash ?? string.Empty : string.Empty;
            state.activeRepositoryId = state.repository.id;
            var activeRepositoryDisplay = connectedRepositories.FirstOrDefault(item => string.Equals(item.RepositoryHash, state.repository.id, StringComparison.Ordinal));
            state.repository.name = repositoryConnected ? SafeNativeText(NativeRepositoryDisplayName(activeRepositoryDisplay), "Repository") : string.Empty;
            state.repository.folderName = repositoryConnected ? SafeNativeText(activeRepositoryDisplay?.LocalFolderName, string.Empty) : string.Empty;
            state.repository.status = repositoryConnected ? "active" : "not_selected";
            state.repositoryStatus = state.repository.status;
            state.repository.connectedCount = connectedRepositories.Count;
            state.repository.statusText = repositoryConnected ? "Active repository" : "No repository connected";
            state.repository.growthBasis = state.growthBasis;
            state.repository.firstCommit = state.growthFirstCommit;
            state.repository.firstCommitDate = state.growthFirstCommitDate;
            state.repository.currentHead = state.growthCurrentHead;
            state.repository.lastAnalyzedCommit = state.growthLastAnalyzedCommit;
            state.repository.commitsAnalyzed = state.growthCommitsAnalyzed;
            state.repository.filesChanged = state.growthFilesChanged;
            state.repository.projectionSource = state.growthProjectionSource;
            state.repository.disabledReason = repositoryConnected ? string.Empty : "Connect a repository first";
            state.repository.hasValidSource = repositoryConnected;
            state.repository.canAnalyze = repositoryConnected && !nativeAnalysisInProgress;
            state.repository.analyzeDisabledReason = RepositoryAnalyzeDisabledReason(repositoryConnected, nativeAnalysisInProgress);
            if (repositoryConnected)
            {
                Debug.Log("INFO [RepositoryIdentity][APPROVED_FOLDER] repositoryId=" + SafeNativeText(state.repository.id, "none") +
                          " folderName=" + SafeNativeText(state.repository.folderName, "unknown") +
                          " displayName=" + SafeNativeText(state.repository.name, "unknown"));
                Debug.Log("INFO [RepositoryIdentity][DISPLAY_NAME] repositoryId=" + SafeNativeText(state.repository.id, "none") +
                          " displayName=" + SafeNativeText(state.repository.name, "unknown"));
            }
            var activeRepositoryRuns = NativeRunsForRepository(state.repository.id).ToList();
            var activeRepositoryTimelineEvents = TimelineEventsForRepository(state.repository.id).ToList();
            var activeRepositoryHasSavedRun = activeRepositoryRuns.Any(IsSavedNativeRun) || activeRepositoryTimelineEvents.Any(IsSavedTimelineEvent);
            var crossRepositoryRunCount = (approvedActivityAnalysis?.RecentNativeAnalysisRuns ?? new List<NativeAnalysisRunRecord>())
                .Count(run => run != null &&
                              !string.IsNullOrWhiteSpace(run.RepositoryId) &&
                              !string.Equals(run.RepositoryId, state.repository.id, StringComparison.Ordinal));
            Debug.Log("INFO [GrowthSummary][SELECTED_REPOSITORY] repositoryId=" + SafeNativeText(state.repository.id, "none") +
                      " repositoryName=" + SafeNativeText(state.repository.name, "none") +
                      " approved=" + repositoryConnected +
                      " scopedRunCount=" + activeRepositoryRuns.Count +
                      " savedRunCount=" + activeRepositoryRuns.Count(IsSavedNativeRun));
            if (repositoryConnected && activeRepositoryRuns.Count == 0)
            {
                Debug.Log("INFO [GrowthSummary][NO_RUN_FOR_REPOSITORY] repositoryId=" + SafeNativeText(state.repository.id, "none"));
            }
            if (crossRepositoryRunCount > 0)
            {
                Debug.Log("INFO [GrowthSummary][CROSS_REPO_SUPPRESSED] selectedRepositoryId=" + SafeNativeText(state.repository.id, "none") +
                          " suppressedRunCount=" + crossRepositoryRunCount);
            }
            state.lastRunSummary = repositoryConnected
                ? SafeNativeText(activeRepositoryTimelineEvents.FirstOrDefault(IsSavedTimelineEvent)?.Summary, SafeNativeText(activeRepositoryRuns.FirstOrDefault(IsSavedNativeRun)?.SafeSummary, "No analysis yet"))
                : "Connect a repository to start tracking Git growth.";
            if (repositoryConnected && activeRepositoryHasSavedRun)
            {
                var displayRun = activeRepositoryRuns.FirstOrDefault(IsSavedNativeRun);
                Debug.Log("INFO [GrowthSummary][DISPLAY_RUN] repositoryId=" + SafeNativeText(displayRun?.RepositoryId, "none") +
                          " repositoryAlias=" + SafeNativeText(displayRun?.RepositoryAlias, state.repository.name) +
                          " scope=" + SafeNativeText(displayRun?.AnalysisScope, "unknown") +
                          " commitRange=" + SafeNativeText(displayRun?.CommitRange, "unknown"));
                Debug.Log("INFO [GrowthSummary][RUN_SCOPE] repositoryId=" + SafeNativeText(displayRun?.RepositoryId, "none") +
                          " scope=" + SafeNativeText(displayRun?.AnalysisScope, "unknown"));
            }
            state.agentProviders = BuildNativeAgentProviderItems();
            state.codexAgent.connected = codexConnected;
            state.codexAgent.status = AgentCodexStatus();
            state.codexAgent.statusText = AgentCodexStatusText();
            state.repositories = BuildNativeRepositoryItems(dashboard);
            state.companionFarm = BuildNativeCompanionFarmState(state.repositories, settings, state.desiredVisible);
            state.tokenShop = BuildNativeTokenShopState(state.repositories.FirstOrDefault(item => item != null && item.selected), repositoryConnected, projectionSaveData, state.agentProviders);
            state.onboarding = BuildNativeOnboardingState(projectionSaveData, repositoryConnected);
            state.agents.connectedCount = state.agentProviders.Count(provider => provider.connected && provider.hasValidSource);
            state.agents.warningCount = state.agentProviders.Sum(provider => Math.Max(0, provider.warningCount));
            state.agents.lastProvider = state.agentProviders.FirstOrDefault(provider => provider.connected && provider.hasValidSource)?.displayName ?? "None";
            state.agents.statusText = state.agents.connectedCount > 0
                ? state.agents.connectedCount + " provider" + (state.agents.connectedCount == 1 ? "" : "s") + " ready"
                : "No agents connected";
            state.agents.privacyText = "Local aggregate only";
            state.primaryActionEnabled = !nativeAnalysisInProgress && (state.repository.canAnalyze || state.agentProviders.Any(provider => provider.canAnalyze));
            state.providerUsagePercentages = BuildNativeProviderUsagePercentages();
            state.activity.todaySummary = FriendlyNativeSummary(dashboard.LatestSafeSessionSummary, "No activity yet");
            state.activity.state = ActivityStateText(repositoryConnected, state.agents.connectedCount > 0, dashboard.HasSavedRun, state.pendingReviewCount > 0);
            state.activity.code = Math.Max(0, dashboard.Code);
            state.activity.focus = Math.Max(0, dashboard.Focus);
            state.activity.debug = Math.Max(0, dashboard.Debug);
            state.activity.design = Math.Max(0, dashboard.Design);
            state.activity.sync = Math.Max(0, dashboard.Sync);
            state.activity.hasAxisData = dashboard.HasGrowthAxisData;
            state.activity.hasLegacyAxisGap = dashboard.HasLegacyGrowthAxisGap;
            state.activity.axisDataStatusText = SafeNativeText(dashboard.GrowthAxisDataStatusText, "No axis data recorded yet.");
            state.activity.recentRunsSummary = activeRepositoryRuns.Count > 0
                ? SafeNativeText(activeRepositoryRuns[0].SafeSummary, activeRepositoryRuns[0].Status)
                : (repositoryConnected ? "No analysis yet" : "No repository activity yet");
            state.activity.savedReviewsSummary = SavedGrowthHistorySummary(state.repository.id);
            state.activity.repositoryActivitySummary = repositoryConnected
                ? RepositoryActivitySummary(state.repository.id)
                : "Connect a repository to start tracking local development growth.";
            state.activity.agentActivitySummary = NativeAgentActivitySummary();
            state.activity.hasRecentRuns = activeRepositoryRuns.Count > 0;
            state.activity.hasSavedReviews = activeRepositoryHasSavedRun;
            state.activity.hasRepositoryActivity = HasSavedRepositoryActivity(state.repository.id) || activeRepositoryHasSavedRun;
            state.activity.hasAiAgentActivity = HasSavedAgentActivity();
            state.activity.hidesZeroDeltaSystemNoise = true;
            state.activity.runningJobs = BuildNativeRunningJobs();
            state.activity.pendingReviews = BuildNativePendingReviews(pendingNativeReview);
            state.activity.recentRuns = BuildNativeRecentRuns(state.repository.id);
            state.hasSavedReviews = state.activity.hasSavedReviews;
            state.hasRepositoryActivity = state.activity.hasRepositoryActivity;
            state.hasAiAgentActivity = state.activity.hasAiAgentActivity;
            ApplyPendingReviewState(state, pendingNativeReview);
            if (pendingNativeReview != null && nativeReviewDetailVisible &&
                string.Equals(nativeSelectedReviewId, state.review.reviewId, StringComparison.Ordinal))
            {
                state.review.detailVisible = true;
                state.review.selectedReviewId = nativeSelectedReviewId;
            }

            state.canSaveGrowth = state.review.pending && state.review.canSaveGrowth;
            state.canDiscardPendingReview = state.review.pending && state.review.canDiscard;
            if (nativeAnalysisInProgress)
            {
                state.activity.state = "Analyzing";
                state.activity.todaySummary = SafeNativeText(nativeActionStatusText, "Analyzing safe aggregate activity.");
            }
            else if (string.Equals(nativeActionStatusKind, "error", StringComparison.Ordinal))
            {
                state.activity.state = "Needs attention";
                state.activity.todaySummary = SafeNativeText(nativeActionStatusText, "Analysis failed safely.");
                state.repositorySafeError = NativeSafeErrorCategoryFromStatusText(nativeActionStatusText);
            }
            state.statusText = NativeStatusText(state);
            if (!repositoryConnected)
            {
                ApplyNoRepositoryNativeState(state, projectionSaveData, settings);
                Debug.Log("INFO [DashboardEmptyState] reason=no_connected_repository");
                Debug.Log("INFO [RepositoryProjection][NO_APPROVED_REPOSITORY_CLEAR_ACTIVE]");
                Debug.Log("INFO [RepositoryIdentity][NO_APPROVED_REPOSITORY]");
                Debug.Log("INFO [OverlayLifecycle][NO_REPOSITORY_HIDE_OVERLAY]");
                Debug.Log("INFO [OverlaySuppressed] reason=noApprovedRepository");
            }
            else
            {
                Debug.Log("INFO [RepositoryProjection][ACTIVE_REPOSITORY_FROM_CONNECTED_PROJECT] repositoryId=" + SafeNativeText(state.repository.id, "none"));
            }
            EnforceNativeDashboardInvariants(state);
            state.statusText = NativeStatusText(state);
            Debug.Log("INFO [Projection] revision=" + state.stateRevision + " activeRepo=" + SafeNativeText(state.repository.id, "none") + " repoCount=" + (state.repositories?.Length ?? 0));
            return state;
        }

        private NativeRepositoryListItem[] BuildNativeRepositoryItems(CharacterDashboardSummary dashboard)
        {
            var items = approvedActivityAnalysis?.RepositoryCompanions ?? new List<RepositoryCompanionDisplayItem>();
            var eligibleItems = items
                .Where(item => !string.IsNullOrWhiteSpace(item.RepositoryHash))
                .Where(item => !string.Equals(item.RepositoryHash, RepositoryCompanionProfileService.DefaultLocalRepositoryHash, StringComparison.Ordinal))
                .Where(item => item.ApprovedByUser && !item.Archived)
                .ToList();
            var displayNames = eligibleItems
                .Select(NativeRepositoryDisplayName)
                .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

            return eligibleItems
                .Select(item => new NativeRepositoryListItem
                {
                    id = item.RepositoryHash,
                    name = NativeRepositoryProjectedName(item, displayNames),
                    folderName = SafeNativeText(item.LocalFolderName, string.Empty),
                    safePath = SafeNativeText(item.ShortLocalPath, item.ApprovedByUser ? "Approved local folder" : "Local approval missing"),
                    remoteUrl = SafeNativeText(item.RemoteUrl, "No remote"),
                    branch = SafeNativeText(item.Branch, "unknown"),
                    repositoryId = item.RepositoryHash,
                    lastAnalysisScope = SafeNativeText(item.LastAnalysisScope, "Not analyzed"),
                    firstCommit = SafeNativeText(item.FirstCommit, string.Empty),
                    firstCommitDate = SafeNativeText(item.FirstCommitDate, string.Empty),
                    currentHead = SafeNativeText(item.CurrentHead, string.Empty),
                    lastAnalyzedCommit = SafeNativeText(item.LastAnalyzedCommit, string.Empty),
                    commitsAnalyzed = Math.Max(0, item.CommitsAnalyzed),
                    filesChanged = Math.Max(0, item.FilesChanged),
                    companion = item.CanLevelUp
                        ? item.Stage + " · Lv " + Math.Max(1, item.Level) + " · Level Up Ready"
                        : item.Stage + " · Lv " + Math.Max(1, item.Level) + " · " + Math.Max(0, item.CurrentXp) + "/" + Math.Max(1, item.XpRequiredForNextLevel) + " XP",
                    lastAnalyzed = item.LastAnalyzedAt == null
                        ? (string.IsNullOrWhiteSpace(item.LastApprovedActivityBucket) ? "Not analyzed" : item.LastApprovedActivityBucket)
                        : item.LastAnalyzedAt.Value.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss") + " UTC",
                    status = item.Archived ? "archived" : item.Selected ? "active" : "connected",
                    statusText = item.Archived ? "Archived" : item.Selected ? "Active context" : "Connected",
                    selected = item.Selected,
                    canAnalyze = !item.Archived && item.Selected && gitAnalysisFlow != null && gitAnalysisFlow.HasSelectedRepositoryForLocalOnlyApproval,
                    analyzeDisabledReason = item.Archived
                        ? "Archived repositories cannot be analyzed."
                        : item.Selected
                            ? RepositoryAnalyzeDisabledReason(gitAnalysisFlow != null && gitAnalysisFlow.HasSelectedRepositoryForLocalOnlyApproval, nativeAnalysisInProgress)
                            : "Set this repository active before analysis.",
                    canDisconnect = !item.Archived && !nativeAnalysisInProgress,
                    canRestore = item.Archived,
                    canDelete = item.Archived,
                    archived = item.Archived,
                    avatarSkin = CompanionSkinCatalog.Normalize(item.Skin),
                    stage = item.Stage.ToString(),
                    stageIndex = (int)item.Stage,
                    level = Math.Max(1, item.Level),
                    currentXP = Math.Max(0, item.CurrentXp),
                    lifetimeGrowthXP = Math.Max(0, item.LifetimeGrowthXp),
                    weeklyGrowthXP = Math.Max(0, item.WeeklyGrowthXp),
                    requiredXP = Math.Max(1, item.XpRequiredForNextLevel),
                    canLevelUp = item.CanLevelUp,
                    canEvolve = item.CanLevelUp && !item.Archived,
                    xpStatusText = item.CanLevelUp
                        ? Math.Max(0, item.CurrentXp) + " XP · " + Math.Max(1, item.XpRequiredForNextLevel) + " XP required · Ready to evolve"
                        : Math.Max(0, item.CurrentXp) + "/" + Math.Max(1, item.XpRequiredForNextLevel) + " XP",
                    recentGrowthSource = SafeNativeText(item.RecentGrowthSource, "None"),
                    recentGitXP = Math.Max(0, item.RecentGitXp),
                    recentAiXP = Math.Max(0, item.RecentAiXp),
                    estimatedTokenActivity = TokenActivityLabel(item.EstimatedTokenActivity),
                    dominantStat = SafeNativeText(item.DominantStat, "Unknown"),
                    secondaryStat = SafeNativeText(item.SecondaryStat, "Unknown"),
                    evolutionPath = SafeNativeText(item.EvolutionPath, "Unknown"),
                    nextEvolutionPreview = SafeNativeText(item.NextEvolutionPreview, "Repository Hatchling"),
                    tokenCurrencyName = SafeNativeText(item.TokenCurrencyName, "Forge Coins"),
                    tokenCurrencyBalance = Math.Max(0, item.TokenCurrencyBalance),
                    purchasedTokenShopItemIds = (item.PurchasedTokenShopItemIds ?? new List<string>())
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .ToArray(),
                    equippedTokenShopItemIds = (item.EquippedTokenShopItemIds ?? new List<string>())
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .ToArray(),
                    motionMood = SafeNativeText(item.MotionState?.Mood, "idle"),
                    motionReason = SafeNativeText(item.MotionState?.ReasonSummary, "No recent aggregate activity."),
                    canViewGrowth = true,
                    sourceBadge = item.ApprovedByUser ? "Approved by you" : "Approval missing"
                })
                .ToArray();
        }

        private static string NativeRepositoryProjectedName(RepositoryCompanionDisplayItem item, IReadOnlyDictionary<string, int> displayNames)
        {
            var baseName = NativeRepositoryDisplayName(item);
            if (displayNames != null &&
                displayNames.TryGetValue(baseName, out var duplicateCount) &&
                duplicateCount > 1)
            {
                var shortId = ShortRepositoryId(item?.RepositoryHash);
                return string.IsNullOrWhiteSpace(shortId) ? baseName : baseName + " · " + shortId;
            }

            return baseName;
        }

        private static string NativeRepositoryDisplayName(RepositoryCompanionDisplayItem item)
        {
            if (item == null)
            {
                return "Repository";
            }

            var folderName = SafeNativeText(item.LocalFolderName, string.Empty);
            if (!IsGenericRepositoryLabel(folderName))
            {
                return folderName;
            }

            var alias = SafeNativeText(item.SafeRepositoryAlias, string.Empty);
            if (!IsGenericRepositoryLabel(alias))
            {
                return alias;
            }

            var pathAlias = LastPathToken(SafeNativeText(item.ShortLocalPath, string.Empty));
            if (!IsGenericRepositoryLabel(pathAlias) &&
                !string.Equals(pathAlias, "folder", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(pathAlias, "local", StringComparison.OrdinalIgnoreCase))
            {
                return pathAlias;
            }

            Debug.LogWarning("WARN [RepositoryIdentity] missing approved folder basename; visible repository hash fallback suppressed.");
            return "Unresolved approved folder";
        }

        private static bool IsGenericRepositoryLabel(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            var normalized = value.Trim();
            return string.Equals(normalized, "Repository", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "Local Repository", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "Approved local folder", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "Local approval missing", StringComparison.OrdinalIgnoreCase);
        }

        private static string LastPathToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var trimmed = value.Trim().TrimEnd('/', '\\');
            var lastSlash = Math.Max(trimmed.LastIndexOf('/'), trimmed.LastIndexOf('\\'));
            return lastSlash >= 0 && lastSlash + 1 < trimmed.Length ? trimmed.Substring(lastSlash + 1) : trimmed;
        }

        private static string ShortRepositoryId(string repositoryHash)
        {
            if (string.IsNullOrWhiteSpace(repositoryHash))
            {
                return string.Empty;
            }

            var trimmed = repositoryHash.Trim();
            return trimmed.Length <= 8 ? trimmed : trimmed.Substring(0, 8);
        }

        private void ApplyNoRepositoryNativeState(NativeDashboardState state, SaveData saveData, DesktopCompanionSettings settings)
        {
            if (state == null)
            {
                return;
            }

            settings = settings ?? DesktopCompanionSettings.CreateDefault();
            state.hasActiveRepository = false;
            state.activeRepositoryId = string.Empty;
            state.repository.connected = false;
            state.repository.id = string.Empty;
            state.repository.name = string.Empty;
            state.repository.status = "not_selected";
            state.repository.statusText = "No repository connected";
            state.repository.connectedCount = 0;
            state.repository.disabledReason = "Connect a repository first";
            state.repository.hasValidSource = false;
            state.repository.canAnalyze = false;
            state.repository.analyzeDisabledReason = "Connect a repository first";
            state.primaryActionEnabled = false;
            state.repositoryStatus = "not_selected";
            state.repositories = new NativeRepositoryListItem[0];
            state.companionVisible = false;
            state.desiredVisible = false;
            state.actualVisible = false;
            state.panelExists = false;
            state.panelFrame = string.Empty;
            state.selectedRepoHash = string.Empty;
            state.repoApproved = false;
            state.movementPaused = true;
            state.overlayLastAction = "noRepository";
            state.overlayLastError = string.Empty;
            state.companion = NativeCompanionState.CreateDefault();
            state.companion.name = "No companion";
            state.companion.stage = "None";
            state.companion.stageIndex = 0;
            state.companion.level = 0;
            state.companion.xp = 0;
            state.companion.totalLifetimeXP = 0;
            state.companion.canLevelUp = false;
            state.companion.mood = "hidden";
            state.companion.evolveActionVisible = false;
            state.companion.evolveActionHiddenReason = "no companion selected";
            state.companion.levelUpStatusText = "Connect a repository to enable companion growth.";
            state.companion.levelUpDisabledReason = "Connect a repository to enable companion growth.";
            state.companion.xpStatusText = "No repository connected";
            state.companion.motion = NativeCompanionMotionState.CreateDefault();
            state.companion.motion.mood = "hidden";
            state.persistedCompanionXP = 0;
            state.pendingEstimatedXP = 0;
            state.codeStat = 0;
            state.focusStat = 0;
            state.debugStat = 0;
            state.designStat = 0;
            state.syncStat = 0;
            state.weeklyCodeStat = 0;
            state.weeklyFocusStat = 0;
            state.weeklyDebugStat = 0;
            state.weeklyDesignStat = 0;
            state.weeklySyncStat = 0;
            state.hasGrowthAxisData = false;
            state.hasLegacyGrowthAxisGap = false;
            state.growthAxisDataStatusText = "No axis data recorded yet.";
            state.dominantGrowthPath = "Unknown";
            state.secondaryGrowthTrait = "Unknown";
            state.currentEvolutionBias = "Unknown";
            state.nextEvolutionPreview = "Repository Hatchling";
            state.eggInfluenceText = "Connect a repository to start shaping a companion.";
            state.tokenCurrencyBalance = 0;
            state.lastRunSummary = "Connect a repository to start tracking Git growth.";
            state.actionStatusText = string.Equals(state.actionStatusKind, "idle", StringComparison.Ordinal)
                ? "Connect a repository to create your first companion."
                : state.actionStatusText;
            state.hasSavedReviews = false;
            state.hasRepositoryActivity = false;
            state.companionFarm = BuildNativeCompanionFarmState(new NativeRepositoryListItem[0], settings, false);
            state.activity.todaySummary = "No repository activity yet";
            state.activity.state = "No repository connected";
            state.activity.code = 0;
            state.activity.focus = 0;
            state.activity.debug = 0;
            state.activity.design = 0;
            state.activity.sync = 0;
            state.activity.hasAxisData = false;
            state.activity.hasLegacyAxisGap = false;
            state.activity.axisDataStatusText = "No axis data recorded yet.";
            state.activity.recentRunsSummary = "No repository activity yet";
            state.activity.savedReviewsSummary = "Connect a repository to start tracking Git growth.";
            state.activity.repositoryActivitySummary = "No repository activity yet";
            state.activity.hasRecentRuns = false;
            state.activity.hasSavedReviews = false;
            state.activity.hasRepositoryActivity = false;
            state.activity.recentRuns = new NativeActivityItem[0];
            state.activity.pendingReviews = new NativeActivityItem[0];
            state.review = NativeReviewState.CreateDefault();
            state.review.pending = false;
            state.review.canSaveGrowth = false;
            state.review.canDiscard = false;
            state.review.canViewDetails = false;
            state.hasPendingReview = false;
            state.pendingReviewCount = 0;
            state.canSaveGrowth = false;
            state.canDiscardPendingReview = false;
            state.tokenShop = BuildNativeTokenShopState(null, false, saveData, state.agentProviders);
        }

        private NativeTokenShopState BuildNativeTokenShopState(NativeRepositoryListItem selectedRepository, bool hasActiveRepository, SaveData saveData, NativeAgentProviderState[] agentProviders)
        {
            saveData = RepositoryCompanionProfileService.Normalize(saveData);
            agentProviders = agentProviders ?? new NativeAgentProviderState[0];
            var targetType = string.Equals(nativeShopTargetType, "aiAgent", StringComparison.Ordinal) ? ShopTargetType.AiAgent : ShopTargetType.RepositoryCompanion;
            var selectedAgentId = RepositoryCompanionProfileService.NormalizeAgentShopId(nativeShopSelectedAgentId);
            if (targetType == ShopTargetType.AiAgent && string.IsNullOrWhiteSpace(selectedAgentId))
            {
                selectedAgentId = agentProviders.FirstOrDefault(provider => provider != null && !string.IsNullOrWhiteSpace(provider.id))?.id ?? "codex";
                nativeShopSelectedAgentId = selectedAgentId;
            }

            var selectedAgent = agentProviders.FirstOrDefault(provider => provider != null && string.Equals(provider.id, selectedAgentId, StringComparison.Ordinal));
            var agentConnected = selectedAgent != null && selectedAgent.connected && selectedAgent.hasValidSource;
            var agentShop = RepositoryCompanionProfileService.GetAgentTokenShopState(saveData, selectedAgentId);
            var selectedAgentState = (saveData.AiAgentShopStates ?? new List<AiAgentShopState>())
                .FirstOrDefault(state => state != null && string.Equals(state.AgentId, selectedAgentId, StringComparison.Ordinal));
            var selectedZodiac = RepositoryCompanionProfileService.NormalizeZodiacTypeId(selectedAgentState?.ZodiacTypeId, selectedAgentId);
            var balance = targetType == ShopTargetType.AiAgent
                ? Math.Max(0, agentShop.CurrencyBalance)
                : hasActiveRepository ? Math.Max(0, selectedRepository?.tokenCurrencyBalance ?? 0) : 0;
            var currencyName = targetType == ShopTargetType.AiAgent
                ? RepositoryCompanionProfileService.AgentCurrencyName(selectedAgentId)
                : SafeNativeText(selectedRepository?.tokenCurrencyName, "Repository Coins");
            var purchased = targetType == ShopTargetType.AiAgent
                ? (agentShop.PurchasedItemIds ?? new List<string>()).ToArray()
                : selectedRepository?.purchasedTokenShopItemIds ?? new string[0];
            var equipped = targetType == ShopTargetType.AiAgent
                ? (agentShop.EquippedItemIds ?? new List<string>()).ToArray()
                : selectedRepository?.equippedTokenShopItemIds ?? new string[0];
            var category = NormalizeShopCategory(nativeShopSelectedCategory);
            nativeShopSelectedCategory = category;
            var targetReady = targetType == ShopTargetType.RepositoryCompanion ? hasActiveRepository : agentConnected;
            var targetLockedReason = targetType == ShopTargetType.RepositoryCompanion
                ? "Connect repository first."
                : agentConnected
                    ? string.Empty
                    : "Connect to unlock agent cosmetics.";
            var items = RepositoryCompanionProfileService.GetTokenShopCatalog()
                .Where(item => item.TargetType == targetType)
                .Where(item => IsNativeShopCategoryVisible(item, category, purchased))
                .Select(item =>
                {
                    var owned = purchased.Any(id => string.Equals(id, item.ItemId, StringComparison.Ordinal));
                    var isEquipped = equipped.Any(id => string.Equals(id, item.ItemId, StringComparison.Ordinal));
                    var affordable = balance >= Math.Max(0, item.Price);
                    var compatible = targetType == ShopTargetType.RepositoryCompanion ||
                                     (item.CompatibleAgentIds ?? new List<string>()).Count == 0 ||
                                     item.CompatibleAgentIds.Any(id => string.Equals(RepositoryCompanionProfileService.NormalizeAgentShopId(id), selectedAgentId, StringComparison.Ordinal));
                    if (compatible && targetType == ShopTargetType.AiAgent && !string.IsNullOrWhiteSpace(item.ZodiacTypeId))
                    {
                        compatible = string.Equals(item.ZodiacTypeId, selectedZodiac, StringComparison.Ordinal);
                    }
                    var insufficientReason = affordable ? string.Empty : "Need " + Math.Max(0, Math.Max(0, item.Price) - balance) + " more " + currencyName + ".";
                    var stateLabel = !compatible
                        ? "Not compatible"
                        : targetReady
                            ? isEquipped
                                ? "Equipped"
                                : owned
                                    ? "Owned"
                                    : affordable
                                        ? "Buy"
                                        : "Need " + Math.Max(0, Math.Max(0, item.Price) - balance) + " more coins"
                            : targetType == ShopTargetType.AiAgent ? "Connect agent" : "Connect repository";
                    return new NativeTokenShopItemState
                    {
                        itemId = item.ItemId,
                        name = SafeNativeText(item.Name, "Shop Item"),
                        description = SafeNativeText(item.Description, "Cosmetic companion item."),
                        itemType = SafeNativeText(item.ItemType, item.Category.ToString()),
                        category = RepositoryCompanionProfileService.CategoryId(item.Category),
                        targetCompatibility = SafeNativeText(item.Compatibility, targetType == ShopTargetType.AiAgent ? "AI Agents" : "Repository Companion"),
                        rarity = item.Rarity.ToString(),
                        previewIcon = SafeNativeText(item.PreviewIcon, "TF"),
                        previewType = SafeNativeText(item.PreviewType, item.PreviewIcon),
                        zodiacType = SafeNativeText(item.ZodiacTypeId, string.Empty),
                        price = Math.Max(0, item.Price),
                        owned = owned,
                        equipped = isEquipped,
                        locked = !targetReady,
                        available = targetReady && compatible && !owned && affordable,
                        canEquip = targetReady && compatible && owned && !isEquipped,
                        stateLabel = stateLabel,
                        buttonTitle = !compatible
                            ? "Not compatible"
                            : !targetReady
                                ? (targetType == ShopTargetType.AiAgent ? "Connect agent" : "Unavailable")
                                : isEquipped
                                    ? "Equipped"
                                    : owned
                                        ? "Equip"
                                        : affordable
                                            ? "Buy"
                                            : "Need Coins",
                        disabledReason = !compatible
                            ? string.IsNullOrWhiteSpace(item.ZodiacTypeId) ? "Not compatible with this target." : "Only for " + RepositoryCompanionProfileService.ZodiacDisplayName(item.ZodiacTypeId) + " companions."
                            : !targetReady
                                ? targetLockedReason
                                : isEquipped
                                    ? "Equipped."
                                    : owned
                                        ? string.Empty
                                        : affordable
                                            ? string.Empty
                                            : insufficientReason,
                        insufficientCoinReason = insufficientReason,
                        lockedAgentReason = targetType == ShopTargetType.AiAgent && !agentConnected ? targetLockedReason : string.Empty,
                        previewEffect = SafeNativeText(item.PreviewEffect, "Preview cosmetic")
                    };
                })
                .ToArray();
            var agents = agentProviders
                .Where(provider => provider != null && !string.IsNullOrWhiteSpace(provider.id))
                .Select(provider =>
                {
                    var providerShop = RepositoryCompanionProfileService.GetAgentTokenShopState(saveData, provider.id);
                    var providerState = (saveData.AiAgentShopStates ?? new List<AiAgentShopState>())
                        .FirstOrDefault(state => state != null && string.Equals(state.AgentId, provider.id, StringComparison.Ordinal));
                    var providerZodiac = RepositoryCompanionProfileService.NormalizeZodiacTypeId(providerState?.ZodiacTypeId, provider.id);
                    return new NativeTokenShopAgentState
                    {
                        id = provider.id,
                        displayName = SafeNativeText(provider.displayName, "AI Agent"),
                        connected = provider.connected && provider.hasValidSource,
                        selected = string.Equals(provider.id, selectedAgentId, StringComparison.Ordinal),
                        statusText = provider.connected && provider.hasValidSource
                            ? "Connected · " + Math.Max(0, providerShop.LifetimeTokenUsageScore) + " usage score · " + Math.Max(0, providerShop.CurrencyBalance) + " " + RepositoryCompanionProfileService.AgentCurrencyName(provider.id)
                            : "Locked · No token usage yet",
                        lockedReason = provider.connected && provider.hasValidSource ? string.Empty : "Connect to unlock agent cosmetics.",
                        actionTitle = provider.connected && provider.hasValidSource ? "Selected" : "Connect agent",
                        tokenUsageTotal = Math.Max(0, providerShop.LifetimeTokenUsageScore),
                        tokenUsageRecent = 0,
                        spendableCoins = Math.Max(0, providerShop.CurrencyBalance),
                        currencyName = RepositoryCompanionProfileService.AgentCurrencyName(provider.id),
                        zodiacType = providerZodiac,
                        zodiacLabel = RepositoryCompanionProfileService.ZodiacDisplayName(providerZodiac)
                    };
                })
                .ToArray();
            return new NativeTokenShopState
            {
                currencyName = currencyName,
                balance = balance,
                hasActiveRepository = hasActiveRepository,
                statusText = hasActiveRepository
                    ? targetType == ShopTargetType.AiAgent
                        ? agentConnected
                            ? "Spend coins earned by each AI agent's token usage. " + RepositoryCompanionProfileService.AgentDisplayName(selectedAgentId) + " shop uses " + currencyName + " only."
                            : "Connect this AI agent to earn and spend its own token usage coins. No token usage yet."
                        : "Shopping for repository mascot. Repository mascot cosmetics use repository-earned coins."
                    : targetType == ShopTargetType.AiAgent
                        ? agentConnected
                            ? "Spend coins earned by this AI agent's token usage. Repository mascot cosmetics are locked until a repository is connected."
                            : "Connect this AI agent to earn and spend its own token usage coins. Repository mascot cosmetics are locked until a repository is connected."
                        : "Connect repository first to use repository mascot cosmetics.",
                lastTransactionStatus = string.Equals(nativeActionStatusKind, "shop", StringComparison.Ordinal)
                    ? SafeNativeText(nativeActionStatusText, string.Empty)
                    : string.Empty,
                targetType = RepositoryCompanionProfileService.TargetTypeId(targetType),
                selectedAgentId = targetType == ShopTargetType.AiAgent ? selectedAgentId : string.Empty,
                selectedCategory = category,
                categoryIds = new[] { "featured", "zodiac", "skins", "outfits", "accessories", "effects", "motions", "themes", "exclusive", "owned" },
                agents = agents,
                ownedItemIds = string.Join(",", purchased),
                equippedItemIds = string.Join(",", equipped),
                items = items
            };
        }

        private static string NormalizeShopCategory(string category)
        {
            category = string.IsNullOrWhiteSpace(category) ? "featured" : category.Trim();
            switch (category)
            {
                case "skins":
                case "outfits":
                case "accessories":
                case "effects":
                case "motions":
                case "themes":
                case "zodiac":
                case "exclusive":
                case "badges":
                case "owned":
                    return category;
                default:
                    return "featured";
            }
        }

        private static bool IsNativeShopCategoryVisible(TokenShopItemDefinition item, string category, string[] purchased)
        {
            if (item == null)
            {
                return false;
            }

            if (string.Equals(category, "owned", StringComparison.Ordinal))
            {
                return (purchased ?? new string[0]).Any(id => string.Equals(id, item.ItemId, StringComparison.Ordinal));
            }

            if (string.Equals(category, "zodiac", StringComparison.Ordinal))
            {
                return !string.IsNullOrWhiteSpace(item.ZodiacTypeId);
            }

            if (string.Equals(category, "exclusive", StringComparison.Ordinal))
            {
                return (item.CompatibleAgentIds ?? new List<string>()).Count == 1 || !string.IsNullOrWhiteSpace(item.ZodiacTypeId);
            }

            if (string.Equals(category, "featured", StringComparison.Ordinal))
            {
                return item.Featured;
            }

            var itemCategory = RepositoryCompanionProfileService.CategoryId(item.Category);
            if (string.Equals(category, "effects", StringComparison.Ordinal) &&
                string.Equals(itemCategory, "tokenEffects", StringComparison.Ordinal))
            {
                return true;
            }

            return string.Equals(itemCategory, category, StringComparison.Ordinal);
        }

        private static NativeOnboardingState BuildNativeOnboardingState(SaveData saveData, bool hasActiveApprovedRepository)
        {
            var prefs = saveData?.OnboardingPreferences ?? new OnboardingPreferences();
            var steps = NativeOnboardingStepTitles();
            var stepIndex = Math.Max(0, Math.Min(steps.Length - 1, prefs.CurrentStepIndex));
            return new NativeOnboardingState
            {
                firstRunCompleted = prefs.FirstRunOnboardingCompleted,
                dismissedForNow = prefs.FirstRunOnboardingDismissedForNow,
                shouldPresentFirstRunGuide = !hasActiveApprovedRepository &&
                                             !prefs.FirstRunOnboardingCompleted &&
                                             !prefs.FirstRunOnboardingDismissedForNow,
                presentationMode = "guidedTutorial",
                currentStep = "step_" + (stepIndex + 1),
                currentStepIndex = stepIndex,
                stepCount = steps.Length,
                canGoBack = stepIndex > 0,
                canGoNext = stepIndex + 1 < steps.Length,
                statusText = prefs.FirstRunOnboardingCompleted
                    ? "Replay the tutorial anytime from the sidebar."
                    : prefs.FirstRunOnboardingDismissedForNow
                        ? "Tutorial paused. Resume it from the sidebar when you are ready."
                    : "Connect a repository, analyze your work, grow a mascot, and keep it on your Mac desktop.",
                steps = steps,
                zodiacIds = RepositoryCompanionProfileService.GetZodiacCompanionTypes().Select(item => item.Id).ToArray()
            };
        }

        private static string[] NativeOnboardingStepTitles()
        {
            return new[]
            {
                "Pick a repository",
                "Analyze Git history",
                "Grow your companion",
                "Unlock cosmetics",
                "Bring it to the desktop"
            };
        }

        private static DesktopCompanionFarmState BuildNativeCompanionFarmState(NativeRepositoryListItem[] repositories, DesktopCompanionSettings settings, bool desiredVisible)
        {
            settings = settings ?? DesktopCompanionSettings.CreateDefault();
            repositories = repositories ?? new NativeRepositoryListItem[0];
            var overlays = repositories
                .Where(item => item != null && !item.archived && !string.IsNullOrWhiteSpace(item.id))
                .Select(item => new RepositoryCompanionOverlayState
                {
                    repositoryId = item.id,
                    repositoryName = SafeNativeText(item.name, "Repository"),
                    companionId = item.id,
                    desiredVisible = desiredVisible && settings.IsDesktopCompanionEnabled,
                    actualVisible = false,
                    desiredPositionX = -1f,
                    desiredPositionY = -1f,
                    actualPositionX = -1f,
                    actualPositionY = -1f,
                    hasSavedPosition = false,
                    dragEnabled = !settings.IsClickThroughEnabled,
                    isDragging = false,
                    hydratedSnapshot = new NativeCompanionFarmSnapshot
                    {
                        repositoryId = item.id,
                        repositoryName = SafeNativeText(item.name, "Repository"),
                        companionId = item.id,
                        stage = item.stageIndex,
                        level = Math.Max(1, item.level),
                        xp = Math.Max(0, item.currentXP),
                        archetype = 0,
                        visualThemeId = CompanionSkinCatalog.Normalize(item.avatarSkin),
                        hydrated = true,
                        desiredVisible = desiredVisible && settings.IsDesktopCompanionEnabled
                    }
                })
                .ToArray();
            if (overlays.Length == 0)
            {
                Debug.Log("INFO [FarmProjection][SKIP_PLACEHOLDER] reason=noRepository");
                Debug.Log("INFO [FarmProjection][BUILD] connectedRepositories=0 snapshots=0");
            }

            return new DesktopCompanionFarmState
            {
                enabled = desiredVisible && settings.IsDesktopCompanionEnabled && overlays.Length > 0,
                overlays = overlays,
                visibleCount = desiredVisible && settings.IsDesktopCompanionEnabled ? overlays.Count(item => item.desiredVisible) : 0,
                globalMotionEnabled = settings.MotionMode != CompanionDesktopMotionMode.Calm,
                globalClickThroughEnabled = settings.IsClickThroughEnabled
            };
        }

        private static string RepositoryAnalyzeDisabledReason(bool hasActiveRepository, bool analysisRunning)
        {
            if (analysisRunning)
            {
                return "Analysis is already running.";
            }

            return hasActiveRepository ? string.Empty : "Connect a repository first";
        }

        private static string LevelUpHiddenReason(bool hasActiveRepository, string repositoryHash, int currentXp, int requiredXp)
        {
            if (string.IsNullOrWhiteSpace(repositoryHash))
            {
                return "no companion selected";
            }

            if (!hasActiveRepository)
            {
                return "active repository missing";
            }

            return currentXp < requiredXp ? "currentXP below requirement" : string.Empty;
        }

        private static NativeCompanionMotionState ToNativeMotionState(CompanionMotionState motion)
        {
            motion = motion ?? CompanionMotionState.Idle(string.Empty);
            return new NativeCompanionMotionState
            {
                repositoryId = motion.RepositoryId ?? string.Empty,
                activityLevel = motion.ActivityLevel.ToString(),
                movementSpeed = motion.MovementSpeed,
                bounceAmplitude = motion.BounceAmplitude,
                idleFrequency = motion.IdleFrequency,
                pulseFrequency = motion.PulseFrequency,
                reaction = motion.Reaction.ToString(),
                mood = SafeNativeText(motion.Mood, "idle"),
                reasonSummary = SafeNativeText(motion.ReasonSummary, "No recent aggregate activity."),
                updatedAt = motion.UpdatedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss")
            };
        }

        private static string DashboardAnimationStateFor(CompanionMotionState motion, bool canLevelUp)
        {
            if (canLevelUp)
            {
                return "evolvePulse";
            }

            motion = motion ?? CompanionMotionState.Idle(string.Empty);
            switch (motion.Reaction)
            {
                case CompanionMotionReaction.GrowthSaved: return "growthSparkle";
                case CompanionMotionReaction.ReadyToReview: return "attentionBounce";
                case CompanionMotionReaction.AiPulse:
                case CompanionMotionReaction.TokenPulse: return "activityPulse";
                case CompanionMotionReaction.WarningShake: return "warningShake";
                default: return "subtleIdle";
            }
        }

        private NativeAgentProviderState[] BuildNativeAgentProviderItems()
        {
            if (approvedActivityAnalysis == null)
            {
                return new NativeAgentProviderState[0];
            }

            return approvedActivityAnalysis.Onboarding.AgentSources
                .Select(source =>
                {
                    var providerType = ProviderTypeForNative(source.SourceType);
                    var manual = source.SourceType == ConnectedAgentSourceType.OtherManualLogFolder;
                    var hasValidSource = AgentHasValidSourceForNative(source);
                    var ready = IsAgentReadyForNative(source) && NativeAgentFlowMatchesSource(source);
                    var status = ready
                        ? "connected"
                        : IsAgentReadyForNative(source)
                            ? "warning"
                            : AgentProviderStatusForNative(source);
                    var disabledReason = AgentDisabledReasonForNative(source);
                    var sourceLabel = string.IsNullOrWhiteSpace(source.SafeLabel) ? "No local source selected" : source.SafeLabel;
                    var pending = PendingProviderXp(providerType);
                    var saved = SavedProviderXp(providerType);
                    var tokenActivity = EstimatedTokenActivityForProvider(providerType);
                    var repositoryAttribution = RepositoryAttributionForProvider(providerType);
                    return new NativeAgentProviderState
                    {
                        id = NativeProviderId(providerType),
                        displayName = source.DisplayName,
                        type = NativeProviderId(providerType),
                        detectionStrategy = manual ? "manual_folder" : "auto_detect_or_manual",
                        supportedStatus = AgentSupportedStatusForNative(source),
                        status = status,
                        statusText = AgentProviderStatusText(status),
                        connected = ready,
                        hasValidSource = hasValidSource,
                        canAutoDetect = !manual && !nativeAnalysisInProgress && source.State != AgentSourceSetupState.DetectingLocalSource,
                        canConnect = !nativeAnalysisInProgress && !ready && (source.State == AgentSourceSetupState.NotSelected ||
                                                                            source.State == AgentSourceSetupState.LocalSourceDetected ||
                                                                            source.State == AgentSourceSetupState.AnalysisFailedSafely ||
                                                                            source.State == AgentSourceSetupState.PermissionRequired ||
                                                                            source.State == AgentSourceSetupState.ManualImportRequired),
                        canChooseFolder = !nativeAnalysisInProgress,
                        canAnalyze = ready && !nativeAnalysisInProgress,
                        canDisconnect = ready,
                        warningCount = Math.Max(0, source.WarningCount),
                        safeCandidateSummary = sourceLabel,
                        selectedSourceLabel = sourceLabel,
                        lastAnalyzedAt = source.LastScanTimeUtc == null ? "Not analyzed" : source.LastScanTimeUtc.Value.UtcDateTime.ToString("yyyy-MM-dd"),
                        approvedSource = hasValidSource,
                        estimatedTokenActivity = TokenActivityLabel(tokenActivity),
                        estimatedTokensText = "unavailable",
                        sessionCountText = CountBucketLabel(ProviderSessionBucket(providerType)),
                        interactionCountText = CountBucketLabel(ProviderInteractionBucket(providerType)),
                        recentAnalyzedRepository = RecentRepositoryForProvider(providerType),
                        repositoryAttributionSummary = repositoryAttribution,
                        pendingXP = pending,
                        savedXP = saved,
                        confidence = ProviderConfidenceLabel(providerType),
                        warningsText = source.WarningCount > 0 ? source.WarningCount + " warning" + (source.WarningCount == 1 ? string.Empty : "s") : "None",
                        canApprove = !ready && hasValidSource,
                        canViewUsage = true,
                        canSaveGrowth = pending > 0 && stateHasPendingProvider(providerType),
                        lastErrorSafeMessage = status == "failed" ? SafeNativeText(source.StatusLabel, "Analysis failed safely.") : string.Empty,
                        disabledReason = ready ? string.Empty : disabledReason,
                        unsupportedReason = ready ? string.Empty : AgentUnsupportedReasonForNative(source)
                    };
                })
                .ToArray();
        }

        private bool stateHasPendingProvider(AgentProviderType providerType)
        {
            var pending = approvedActivityAnalysis?.PendingNativeActivityReview;
            if (pending == null)
            {
                return false;
            }

            return PendingReviewSessionsForNative(pending)
                .Any(session => MacAgentSourceDetector.NormalizeProvider(session.AgentActivitySummary?.ProviderType ?? AgentProviderType.Unknown) == MacAgentSourceDetector.NormalizeProvider(providerType));
        }

        private int PendingProviderXp(AgentProviderType providerType)
        {
            var pending = approvedActivityAnalysis?.PendingNativeActivityReview;
            if (pending == null)
            {
                return 0;
            }

            var normalized = MacAgentSourceDetector.NormalizeProvider(providerType);
            var sessions = PendingReviewSessionsForNative(pending)
                .Where(session => MacAgentSourceDetector.NormalizeProvider(session.AgentActivitySummary?.ProviderType ?? AgentProviderType.Unknown) == normalized)
                .Select(session => session.SessionId)
                .ToHashSet(StringComparer.Ordinal);
            if (sessions.Count == 0)
            {
                return 0;
            }

            var growthResults = pending.GrowthResults ?? new List<CharacterGrowthResult>();
            if (growthResults.Count == 0 && pending.GrowthResult != null)
            {
                growthResults = new List<CharacterGrowthResult> { pending.GrowthResult };
            }

            return growthResults
                .Where(growth => growth != null && sessions.Contains(growth.SessionId))
                .Sum(growth => Math.Max(0, growth.ExpGained));
        }

        private int SavedProviderXp(AgentProviderType providerType)
        {
            var normalized = MacAgentSourceDetector.NormalizeProvider(providerType);
            return (approvedActivityAnalysis?.RecentSessions ?? new List<RecentSafeSessionSummary>())
                .Where(summary => summary != null && MacAgentSourceDetector.NormalizeProvider(summary.AgentProviderType) == normalized)
                .Take(12)
                .Sum(summary => Math.Max(0, summary.ExpGained));
        }

        private TokenUsageBucket EstimatedTokenActivityForProvider(AgentProviderType providerType)
        {
            var normalized = MacAgentSourceDetector.NormalizeProvider(providerType);
            var pending = approvedActivityAnalysis?.PendingNativeActivityReview;
            var pendingBucket = PendingReviewSessionsForNative(pending)
                .Where(session => MacAgentSourceDetector.NormalizeProvider(session.AgentActivitySummary?.ProviderType ?? AgentProviderType.Unknown) == normalized)
                .Select(session => session.TokenUsageBucket)
                .OrderByDescending(bucket => (int)bucket)
                .FirstOrDefault();
            return pendingBucket;
        }

        private CountBucket ProviderSessionBucket(AgentProviderType providerType)
        {
            var normalized = MacAgentSourceDetector.NormalizeProvider(providerType);
            return PendingReviewSessionsForNative(approvedActivityAnalysis?.PendingNativeActivityReview)
                .Where(session => MacAgentSourceDetector.NormalizeProvider(session.AgentActivitySummary?.ProviderType ?? AgentProviderType.Unknown) == normalized)
                .Select(session => session.AgentActivitySummary?.SessionCountBucket ?? CountBucket.Unknown)
                .OrderByDescending(bucket => (int)bucket)
                .FirstOrDefault();
        }

        private CountBucket ProviderInteractionBucket(AgentProviderType providerType)
        {
            var normalized = MacAgentSourceDetector.NormalizeProvider(providerType);
            return PendingReviewSessionsForNative(approvedActivityAnalysis?.PendingNativeActivityReview)
                .Where(session => MacAgentSourceDetector.NormalizeProvider(session.AgentActivitySummary?.ProviderType ?? AgentProviderType.Unknown) == normalized)
                .Select(session => session.AgentActivitySummary?.InteractionCountBucket ?? CountBucket.Unknown)
                .OrderByDescending(bucket => (int)bucket)
                .FirstOrDefault();
        }

        private string RecentRepositoryForProvider(AgentProviderType providerType)
        {
            var normalized = MacAgentSourceDetector.NormalizeProvider(providerType);
            var repositoryHash = PendingReviewSessionsForNative(approvedActivityAnalysis?.PendingNativeActivityReview)
                .Where(session => MacAgentSourceDetector.NormalizeProvider(session.AgentActivitySummary?.ProviderType ?? AgentProviderType.Unknown) == normalized)
                .Select(RepositoryCompanionProfileService.SafeRepositoryHashForSession)
                .FirstOrDefault(hash => !string.IsNullOrWhiteSpace(hash));

            return RepositoryAliasForHash(repositoryHash);
        }

        private string RepositoryAttributionForProvider(AgentProviderType providerType)
        {
            var normalized = MacAgentSourceDetector.NormalizeProvider(providerType);
            var pending = approvedActivityAnalysis?.PendingNativeActivityReview;
            var sessions = PendingReviewSessionsForNative(pending)
                .Where(session => MacAgentSourceDetector.NormalizeProvider(session.AgentActivitySummary?.ProviderType ?? AgentProviderType.Unknown) == normalized)
                .ToList();
            if (sessions.Count > 0)
            {
                var sessionIds = sessions.Select(session => session.SessionId).ToHashSet(StringComparer.Ordinal);
                var growthResults = pending?.GrowthResults ?? new List<CharacterGrowthResult>();
                if (growthResults.Count == 0 && pending?.GrowthResult != null)
                {
                    growthResults = new List<CharacterGrowthResult> { pending.GrowthResult };
                }

                var xp = growthResults
                    .Where(growth => growth != null && sessionIds.Contains(growth.SessionId))
                    .Sum(growth => Math.Max(0, growth.ExpGained));
                var repository = RepositoryAliasForHash(sessions.Select(RepositoryCompanionProfileService.SafeRepositoryHashForSession).FirstOrDefault(hash => !string.IsNullOrWhiteSpace(hash)));
                var bucket = sessions.Select(session => session.TokenUsageBucket).OrderByDescending(bucketValue => (int)bucketValue).FirstOrDefault();
                return repository + ": +" + xp + " XP · token activity " + TokenActivityLabel(bucket);
            }

            var saved = SavedProviderXp(providerType);
            return saved > 0
                ? "Unassigned: +" + saved + " XP saved · repository attribution unavailable"
                : "No recent repository attribution";
        }

        private string ProviderConfidenceLabel(AgentProviderType providerType)
        {
            var normalized = MacAgentSourceDetector.NormalizeProvider(providerType);
            var confidence = PendingReviewSessionsForNative(approvedActivityAnalysis?.PendingNativeActivityReview)
                .Where(session => MacAgentSourceDetector.NormalizeProvider(session.AgentActivitySummary?.ProviderType ?? AgentProviderType.Unknown) == normalized)
                .Select(session => session.Confidence)
                .OrderByDescending(value => (int)value)
                .FirstOrDefault();
            return confidence == ProviderConfidence.Unknown ? "Unknown" : confidence.ToString();
        }

        private static List<AgentWorkSession> PendingReviewSessionsForNative(PendingNativeActivityReview pending)
        {
            if (pending == null)
            {
                return new List<AgentWorkSession>();
            }

            var sessions = pending.SafeSessions ?? new List<AgentWorkSession>();
            if (sessions.Count == 0 && pending.SafeSession != null)
            {
                sessions = new List<AgentWorkSession> { pending.SafeSession };
            }

            return sessions.Where(session => session != null).ToList();
        }

        private string RepositoryAliasForHash(string repositoryHash)
        {
            if (string.IsNullOrWhiteSpace(repositoryHash))
            {
                return "Unassigned";
            }

            var item = (approvedActivityAnalysis?.RepositoryCompanions ?? new List<RepositoryCompanionDisplayItem>())
                .FirstOrDefault(repository => string.Equals(repository.RepositoryHash, repositoryHash, StringComparison.Ordinal));
            return SafeNativeText(item?.SafeRepositoryAlias, "Active repository");
        }

        private static string TokenActivityLabel(TokenUsageBucket bucket)
        {
            switch (bucket)
            {
                case TokenUsageBucket.Small: return "Low";
                case TokenUsageBucket.Medium: return "Medium";
                case TokenUsageBucket.Large:
                case TokenUsageBucket.Huge: return "High";
                case TokenUsageBucket.None: return "None";
                default: return "Unknown";
            }
        }

        private static string CountBucketLabel(CountBucket bucket)
        {
            switch (bucket)
            {
                case CountBucket.One: return "1";
                case CountBucket.Small: return "Small";
                case CountBucket.Medium: return "Medium";
                case CountBucket.Large:
                case CountBucket.Huge: return "Large";
                case CountBucket.None: return "0";
                default: return "unavailable";
            }
        }

        private static string ActivityStateText(bool repositoryConnected, bool agentConnected, bool hasSavedRun, bool hasPendingReview)
        {
            if (hasPendingReview)
            {
                return "Pending review ready";
            }

            if (hasSavedRun)
            {
                return "Saved growth for selected repository";
            }

            if (!repositoryConnected && !agentConnected)
            {
                return "Connect a repository or AI agent source";
            }

            return "No analysis runs yet";
        }

        private string NativeRecentRunsSummary(bool hasSavedRun, string savedRunSummary)
        {
            var latest = approvedActivityAnalysis?.RecentNativeAnalysisRuns?.FirstOrDefault();
            if (latest != null)
            {
                return SafeNativeText(latest.SafeSummary, latest.Status + " · " + latest.ErrorCode);
            }

            return hasSavedRun ? SafeNativeText(savedRunSummary, "Recent aggregate activity saved.") : "No analysis runs yet.";
        }

        private NativeActivityItem[] BuildNativeRunningJobs()
        {
            if (!nativeAnalysisInProgress)
            {
                return new NativeActivityItem[0];
            }

            return new[]
            {
                new NativeActivityItem
                {
                    id = SafeNativeText(nativeCurrentAnalysisJobId, "analysis-running"),
                    type = string.Equals(nativeCurrentAnalysisType, "agent", StringComparison.OrdinalIgnoreCase) ? "agentAnalysis" : "repositoryAnalysis",
                    sourceName = SafeNativeText(nativeCurrentAnalysisSourceName, "Activity source"),
                    status = "running",
                    createdAt = SafeNativeText(nativeCurrentAnalysisStartedAt, DateTimeOffset.UtcNow.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss")),
                    summary = SafeNativeText(nativeActionStatusText, "Analyzing safe aggregate activity."),
                    currentStep = SafeNativeText(nativeCurrentAnalysisStep, "validating repository"),
                    disabledReason = "Analysis is already running."
                }
            };
        }

        private NativeActivityItem[] BuildNativePendingReviews(PendingNativeActivityReview pending)
        {
            if (pending == null)
            {
                return new NativeActivityItem[0];
            }

            return new[]
            {
                new NativeActivityItem
                {
                    id = SafeNativeText(pending.ReviewId, "pending-review"),
                    type = "pendingReview",
                    sourceName = SafeNativeText(pending.SourceKind, "activity"),
                    status = "pendingReview",
                    createdAt = pending.CreatedAtUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    summary = SafeNativeText(pending.SafeSummary, "Growth review is ready."),
                    currentStep = "review ready",
                    actionKey = "review.saveGrowth",
                    xpDelta = Math.Max(0, pending.EstimatedXpDelta),
                    categoryBreakdown = CategoryBreakdownText(pending.StatDeltas ?? CharacterStats.Zero()),
                    confidence = SafeNativeText(pending.Confidence, "Unknown"),
                    warnings = string.Join(", ", (pending.WarningIds ?? new List<string>()).Take(3)),
                    target = string.Equals(NormalizePendingSourceKind(pending.SourceKind), "aiAgent", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(pending.RepositoryHash)
                        ? "Agent-only"
                        : SafeNativeText(pending.RepositoryHash, "Active repository"),
                    period = pending.CreatedAtUtc.UtcDateTime.ToString("yyyy-MM-dd")
                }
            };
        }

        private IEnumerable<NativeAnalysisRunRecord> NativeRunsForRepository(string repositoryId)
        {
            if (string.IsNullOrWhiteSpace(repositoryId))
            {
                return Enumerable.Empty<NativeAnalysisRunRecord>();
            }

            var aliases = RepositoryAliasesForNative(repositoryId);
            return (approvedActivityAnalysis?.RecentNativeAnalysisRuns ?? new List<NativeAnalysisRunRecord>())
                .Where(run => run != null && aliases.Contains(run.RepositoryId ?? string.Empty))
                .OrderByDescending(run => run.CreatedAtUtc);
        }

        private static bool IsSavedNativeRun(NativeAnalysisRunRecord run)
        {
            return run != null &&
                   (string.Equals(run.Status, "saved", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(run.SourceKind, "reviewSaved", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(run.SourceKind, "levelUp", StringComparison.OrdinalIgnoreCase));
        }

        private NativeActivityItem[] BuildNativeRecentRuns(string repositoryId)
        {
            var items = new List<NativeActivityProjectionItem>();
            items.AddRange(NativeRunsForRepository(repositoryId)
                .Select(run => new NativeActivityProjectionItem
                {
                    TimestampUtc = run.CreatedAtUtc,
                    Item = new NativeActivityItem
                    {
                        id = SafeNativeText(run.RunId, "activity-run"),
                        type = string.Equals(run.SourceKind, "agent", StringComparison.OrdinalIgnoreCase) ? "agentAnalysis" :
                            string.Equals(run.SourceKind, "repository", StringComparison.OrdinalIgnoreCase) ? "repositoryAnalysis" :
                            string.Equals(run.SourceKind, "reviewSaved", StringComparison.OrdinalIgnoreCase) ? "reviewSaved" : SafeNativeText(run.SourceKind, "activity"),
                        sourceName = SafeNativeText(run.SourceKind, "activity"),
                        status = SafeNativeText(run.Status, "completed"),
                        createdAt = run.CreatedAtUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                        completedAt = run.CreatedAtUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                        summary = SafeNativeText(run.SafeSummary, "Activity recorded."),
                        currentStep = string.Equals(run.Status, "failed", StringComparison.OrdinalIgnoreCase) ? SafeNativeText(run.ErrorCode, "Unknown") : "completed",
                        xpDelta = Math.Max(0, run.XpDelta),
                        categoryBreakdown = CategoryBreakdownText(run.StatDeltas ?? CharacterStats.Zero()),
                        target = SafeNativeText(run.RepositoryAlias, repositoryId),
                        period = run.CreatedAtUtc.UtcDateTime.ToString("yyyy-MM-dd"),
                        commitHash = SafeNativeText(run.CommitRange, string.Empty),
                        fileCategory = CategoryBreakdownText(run.StatDeltas ?? CharacterStats.Zero()),
                        deltaReason = Math.Max(0, run.XpDelta) > 0 ? "growth-producing saved analysis" : "diagnostic-only zero delta"
                    }
                }));
            items.AddRange(TimelineEventsForRepository(repositoryId)
                .Select(timelineEvent => new NativeActivityProjectionItem
                {
                    TimestampUtc = timelineEvent.TimestampUtc,
                    Item = new NativeActivityItem
                    {
                        id = SafeNativeText(timelineEvent.Id, "timeline-event"),
                        type = TimelineNativeType(timelineEvent.EventType),
                        sourceName = SafeNativeText(timelineEvent.TimelineSource, "local"),
                        status = SafeNativeText(timelineEvent.Severity, "info"),
                        createdAt = timelineEvent.TimestampUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                        completedAt = timelineEvent.TimestampUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                        summary = SafeNativeText(timelineEvent.Summary, SafeNativeText(timelineEvent.Title, "Repository activity recorded.")),
                        currentStep = SafeNativeText(timelineEvent.EventType, "timeline"),
                        xpDelta = Math.Max(0, timelineEvent.DeltaXp),
                        categoryBreakdown = TimelineCategoryBreakdownText(timelineEvent),
                        target = SafeNativeText(timelineEvent.RepositoryAlias, repositoryId),
                        period = timelineEvent.TimestampUtc.UtcDateTime.ToString("yyyy-MM-dd"),
                        commitHash = SafeNativeText(timelineEvent.MetadataJson, string.Empty),
                        fileCategory = TimelineCategoryBreakdownText(timelineEvent),
                        deltaReason = HasTimelineAxisDelta(timelineEvent) || Math.Max(0, timelineEvent.DeltaXp) > 0
                            ? "growth-producing repository event"
                            : "diagnostic-only zero delta"
                    }
                }));

            return items
                .OrderByDescending(item => item.TimestampUtc)
                .Where(item => IsGrowthProducingRecentActivity(item.Item))
                .Take(8)
                .Select(item => item.Item)
                .ToArray();
        }

        private static bool IsGrowthProducingRecentActivity(NativeActivityItem item)
        {
            if (item == null)
            {
                return false;
            }

            return Math.Max(0, item.xpDelta) > 0 ||
                   HasPositiveCategoryBreakdown(item.categoryBreakdown) ||
                   string.Equals(item.type, "levelUp", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasPositiveCategoryBreakdown(string categoryBreakdown)
        {
            if (string.IsNullOrWhiteSpace(categoryBreakdown))
            {
                return false;
            }

            return categoryBreakdown.IndexOf("+1", StringComparison.Ordinal) >= 0 ||
                   categoryBreakdown.IndexOf("+2", StringComparison.Ordinal) >= 0 ||
                   categoryBreakdown.IndexOf("+3", StringComparison.Ordinal) >= 0 ||
                   categoryBreakdown.IndexOf("+4", StringComparison.Ordinal) >= 0 ||
                   categoryBreakdown.IndexOf("+5", StringComparison.Ordinal) >= 0 ||
                   categoryBreakdown.IndexOf("+6", StringComparison.Ordinal) >= 0 ||
                   categoryBreakdown.IndexOf("+7", StringComparison.Ordinal) >= 0 ||
                   categoryBreakdown.IndexOf("+8", StringComparison.Ordinal) >= 0 ||
                   categoryBreakdown.IndexOf("+9", StringComparison.Ordinal) >= 0;
        }

        private string SavedGrowthHistorySummary(string repositoryId)
        {
            var saved = NativeRunsForRepository(repositoryId)
                .Where(IsSavedNativeRun)
                .Select(run => new NativeActivityProjectionItem
                {
                    TimestampUtc = run.CreatedAtUtc,
                    Item = new NativeActivityItem { summary = SafeNativeText(run.SafeSummary, "Growth saved.") }
                })
                .Concat(TimelineEventsForRepository(repositoryId)
                    .Where(IsSavedTimelineEvent)
                    .Select(timelineEvent => new NativeActivityProjectionItem
                    {
                        TimestampUtc = timelineEvent.TimestampUtc,
                        Item = new NativeActivityItem { summary = SafeNativeText(timelineEvent.Summary, SafeNativeText(timelineEvent.Title, "Growth saved.")) }
                    }))
                .OrderByDescending(item => item.TimestampUtc)
                .Take(4)
                .Select(item => item.TimestampUtc.UtcDateTime.ToString("yyyy-MM-dd") + " · " + item.Item.summary)
                .ToList();
            return saved.Count == 0 ? "No saved growth history yet." : string.Join("\n", saved);
        }

        private string RepositoryActivitySummary(string repositoryId)
        {
            var repository = approvedActivityAnalysis?.CharacterDashboard?.CurrentRepositoryAlias;
            var latestTimeline = TimelineEventsForRepository(repositoryId).FirstOrDefault(IsRepositoryActivityTimelineEvent);
            if (latestTimeline != null)
            {
                return "Repository · " + SafeNativeText(repository, "Active repository") + " · " + SafeNativeText(latestTimeline.Title, latestTimeline.EventType) + "\n" +
                       SafeNativeText(latestTimeline.Summary, "Repository timeline activity was saved.") + "\n" +
                       "Recorded for this repository only.";
            }

            var latest = NativeRunsForRepository(repositoryId)
                .FirstOrDefault(run => string.Equals(run.SourceKind, "repository", StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(run.SourceKind, "reviewSaved", StringComparison.OrdinalIgnoreCase));
            if (latest == null)
            {
                return "No analysis yet for the selected repository.";
            }

            return "Repository · " + SafeNativeText(repository, "Active repository") + " · " + SafeNativeText(latest.AnalysisScope, "Saved growth") + "\n" +
                   SafeNativeText(latest.SafeSummary, "Git changes were analyzed and safe aggregate growth was saved.") + "\n" +
                   "Recorded for this repository only.";
        }

        private bool HasSavedRepositoryActivity(string repositoryId)
        {
            if (string.IsNullOrWhiteSpace(repositoryId))
            {
                return false;
            }

            return TimelineEventsForRepository(repositoryId).Any(IsRepositoryActivityTimelineEvent) ||
                   NativeRunsForRepository(repositoryId)
                .Any(run => string.Equals(run.SourceKind, "repository", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(run.SourceKind, "reviewSaved", StringComparison.OrdinalIgnoreCase));
        }

        private IEnumerable<RepositoryTimelineEvent> TimelineEventsForRepository(string repositoryId)
        {
            if (string.IsNullOrWhiteSpace(repositoryId))
            {
                return Enumerable.Empty<RepositoryTimelineEvent>();
            }

            var aliases = RepositoryAliasesForNative(repositoryId);
            return (approvedActivityAnalysis?.CurrentSaveData?.RepositoryTimelineEvents ?? new List<RepositoryTimelineEvent>())
                .Where(item => item != null && aliases.Contains(item.RepositoryId ?? string.Empty))
                .OrderByDescending(item => item.TimestampUtc);
        }

        private HashSet<string> RepositoryAliasesForNative(string repositoryId)
        {
            var aliases = new HashSet<string>(StringComparer.Ordinal);
            AddRepositoryAlias(aliases, repositoryId);
            var saveData = approvedActivityAnalysis?.CurrentSaveData;
            foreach (var project in saveData?.ConnectedProjects ?? new List<ConnectedProject>())
            {
                if (project == null || project.IsArchived || project.ApprovedAt == null)
                {
                    continue;
                }

                var projectIds = new[] { project.Id, project.PathHash, project.ProjectPathHash, project.LocalOnlyProjectId };
                var matchesRepository = projectIds.Any(id => !string.IsNullOrWhiteSpace(id) && aliases.Contains(id.Trim())) ||
                                        (project.IsActive &&
                                         !string.IsNullOrWhiteSpace(saveData?.SelectedRepositoryHash) &&
                                         string.Equals(saveData.SelectedRepositoryHash.Trim(), repositoryId.Trim(), StringComparison.Ordinal));
                if (!matchesRepository)
                {
                    continue;
                }

                foreach (var id in projectIds)
                {
                    AddRepositoryAlias(aliases, id);
                }
            }

            return aliases;
        }

        private static void AddRepositoryAlias(HashSet<string> aliases, string value)
        {
            if (aliases == null || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            aliases.Add(value.Trim());
        }

        private static bool IsSavedTimelineEvent(RepositoryTimelineEvent item)
        {
            return item != null &&
                   (Math.Max(0, item.DeltaXp) > 0 ||
                    string.Equals(item.EventType, "growth_saved", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(item.EventType, "xp_applied", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(item.EventType, "level_up", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsRepositoryActivityTimelineEvent(RepositoryTimelineEvent item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.EventType))
            {
                return false;
            }

            return IsSavedTimelineEvent(item) ||
                   item.EventType.IndexOf("analysis", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   item.EventType.IndexOf("sync", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   item.EventType.IndexOf("conflict", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   item.EventType.IndexOf("merge", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string TimelineNativeType(string eventType)
        {
            if (string.Equals(eventType, "level_up", StringComparison.OrdinalIgnoreCase))
            {
                return "levelUp";
            }

            if (string.Equals(eventType, "growth_saved", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(eventType, "xp_applied", StringComparison.OrdinalIgnoreCase))
            {
                return "reviewSaved";
            }

            if (!string.IsNullOrWhiteSpace(eventType) &&
                eventType.IndexOf("sync", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "sync";
            }

            return "repositoryTimeline";
        }

        private static string TimelineCategoryBreakdownText(RepositoryTimelineEvent item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            return "Code +" + Math.Max(0, item.CodeDelta) +
                   " · Focus +" + Math.Max(0, item.FocusDelta) +
                   " · Debug +" + Math.Max(0, item.DebugDelta) +
                   " · Design +" + Math.Max(0, item.DesignDelta) +
                   " · Sync +" + Math.Max(0, item.SyncDelta);
        }

        private static bool HasTimelineAxisDelta(RepositoryTimelineEvent item)
        {
            return item != null &&
                   (item.CodeDelta != 0 ||
                    item.FocusDelta != 0 ||
                    item.DebugDelta != 0 ||
                    item.DesignDelta != 0 ||
                    item.SyncDelta != 0);
        }

        private sealed class NativeActivityProjectionItem
        {
            public DateTimeOffset TimestampUtc { get; set; }
            public NativeActivityItem Item { get; set; }
        }

        private bool HasSavedAgentActivity()
        {
            return (approvedActivityAnalysis?.RecentSessions ?? new List<RecentSafeSessionSummary>())
                .Any(summary => IsTrackedAgentProvider(summary.AgentProviderType));
        }

        private string NativeAgentActivitySummary()
        {
            var agentSummaries = (approvedActivityAnalysis?.RecentSessions ?? new List<RecentSafeSessionSummary>())
                .Where(summary => IsTrackedAgentProvider(summary.AgentProviderType))
                .Take(3)
                .Select(summary => MacAgentSourceDetector.SafeProviderLabel(summary.AgentProviderType) + " session activity was saved · +" + Math.Max(0, summary.ExpGained) + " XP")
                .Where(summary => !string.IsNullOrWhiteSpace(summary))
                .ToList();

            return agentSummaries.Count == 0
                ? "Analyze connected AI agents to review AI-assisted growth."
                : string.Join("\n", agentSummaries);
        }

        private NativeProviderUsagePercentage[] BuildNativeProviderUsagePercentages()
        {
            var summaries = approvedActivityAnalysis?.RecentSessions ?? new List<RecentSafeSessionSummary>();
            var counts = new Dictionary<AgentProviderType, int>
            {
                { AgentProviderType.Codex, 0 },
                { AgentProviderType.ClaudeCode, 0 },
                { AgentProviderType.GeminiCli, 0 }
            };

            foreach (var summary in summaries)
            {
                var provider = MacAgentSourceDetector.NormalizeProvider(summary.AgentProviderType);
                if (counts.ContainsKey(provider))
                {
                    counts[provider]++;
                }
            }

            var total = counts.Values.Sum();
            return new[]
            {
                ProviderUsage("codex", "Cdx", counts[AgentProviderType.Codex], total),
                ProviderUsage("claudeCode", "Cl", counts[AgentProviderType.ClaudeCode], total),
                ProviderUsage("geminiCli", "Gem", counts[AgentProviderType.GeminiCli], total)
            };
        }

        private static NativeProviderUsagePercentage ProviderUsage(string id, string label, int count, int total)
        {
            return new NativeProviderUsagePercentage
            {
                providerId = id,
                label = label,
                percentage = total <= 0 || count <= 0 ? 0 : (int)Math.Round(100.0 * count / total),
                hasSavedApprovedActivity = count > 0
            };
        }

        private static string NativeProviderUsageStatusText(NativeProviderUsagePercentage[] usage, int readyProviderCount)
        {
            if (readyProviderCount <= 0)
            {
                return "AI Agents: 0 connected";
            }

            return readyProviderCount == 1 ? "AI Agents: 1 connected" : "AI Agents: " + readyProviderCount + " connected";
        }

        private static string NativeStatusText(NativeDashboardState state)
        {
            var repo = state?.repository != null && state.repository.connected
                ? "Repo: " + SafeNativeText(state.repository.name, "Connected")
                : "Repo: None";
            var agents = NativeProviderUsageStatusText(state?.providerUsagePercentages, Math.Max(0, state?.agents?.connectedCount ?? 0));
            var level = state?.companion != null && state.companion.canLevelUp ? "Level Up Ready" : "Lv " + Math.Max(1, state?.companion?.level ?? 1);
            var farm = state?.companionFarm;
            var farmText = "Desktop: " + ((farm?.enabled ?? false) ? "On" : "Off") +
                           " · Connected companions: " + Math.Max(0, state?.repository?.connectedCount ?? 0) +
                           " · Visible overlays: " + Math.Max(0, farm?.visibleCount ?? 0) +
                           " · Drag: " + ((state?.dragEnabled ?? false) ? "Enabled" : "Disabled") +
                           " · Motion: " + ((farm?.globalMotionEnabled ?? false) ? "On" : "Off") +
                           " · Click-through: " + ((farm?.globalClickThroughEnabled ?? false) ? "On" : "Off");
            return repo + " · " + agents + " · " + level + " · " + farmText;
        }

        private static bool IsTrackedAgentProvider(AgentProviderType providerType)
        {
            providerType = MacAgentSourceDetector.NormalizeProvider(providerType);
            return providerType == AgentProviderType.Codex ||
                   providerType == AgentProviderType.ClaudeCode ||
                   providerType == AgentProviderType.GeminiCli ||
                   providerType == AgentProviderType.Cursor ||
                   providerType == AgentProviderType.GitHubCopilot ||
                   providerType == AgentProviderType.Manual;
        }

        private static string NativeSafeErrorCategoryFromStatusText(string statusText)
        {
            statusText = statusText ?? string.Empty;
            foreach (var category in new[]
                     {
                         "NoActiveRepository",
                         "RepositoryPathMissing",
                         "RepositoryFolderNotFound",
                         "NotAGitRepository",
                         "GitExecutableNotFound",
                         "PermissionDenied",
                         "ProcessTimeout",
                         "GitCommandFailed",
                         "Unknown"
                     })
            {
                if (statusText.IndexOf(category, StringComparison.Ordinal) >= 0)
                {
                    return category;
                }
            }

            return string.Empty;
        }

        private static bool IsAgentReadyForNative(ConnectedAgentSource source)
        {
            return source != null &&
                   source.Selected &&
                   AgentHasValidSourceForNative(source) &&
                   (source.State == AgentSourceSetupState.ReadyToAnalyze ||
                    source.State == AgentSourceSetupState.AnalysisComplete);
        }

        private static bool AgentHasValidSourceForNative(ConnectedAgentSource source)
        {
            return source != null &&
                   source.Selected &&
                   !string.IsNullOrWhiteSpace(source.SafeLabel) &&
                   !string.IsNullOrWhiteSpace(source.SafeLocationHash);
        }

        private static AgentProviderType ProviderTypeForNative(ConnectedAgentSourceType sourceType)
        {
            switch (sourceType)
            {
                case ConnectedAgentSourceType.Cursor: return AgentProviderType.Cursor;
                case ConnectedAgentSourceType.ClaudeCode: return AgentProviderType.ClaudeCode;
                case ConnectedAgentSourceType.Codex: return AgentProviderType.Codex;
                case ConnectedAgentSourceType.GitHubCopilot: return AgentProviderType.GitHubCopilot;
                case ConnectedAgentSourceType.GeminiCli: return AgentProviderType.GeminiCli;
                case ConnectedAgentSourceType.OtherManualLogFolder: return AgentProviderType.Manual;
                default: return AgentProviderType.Unknown;
            }
        }

        private static string NativeProviderId(AgentProviderType providerType)
        {
            switch (MacAgentSourceDetector.NormalizeProvider(providerType))
            {
                case AgentProviderType.Codex: return "codex";
                case AgentProviderType.ClaudeCode: return "claudeCode";
                case AgentProviderType.Cursor: return "cursor";
                case AgentProviderType.GitHubCopilot: return "githubCopilot";
                case AgentProviderType.GeminiCli: return "geminiCli";
                case AgentProviderType.Manual: return "manual";
                default: return "unknown";
            }
        }

        private static string AgentProviderStatusForNative(ConnectedAgentSource source)
        {
            if (source == null)
            {
                return "notConfigured";
            }

            switch (source.State)
            {
                case AgentSourceSetupState.DetectingLocalSource:
                    return "validating";
                case AgentSourceSetupState.LocalSourceDetected:
                    return "detected";
                case AgentSourceSetupState.ReadyToAnalyze:
                    return AgentHasValidSourceForNative(source) ? "connected" : "detected";
                case AgentSourceSetupState.AnalysisComplete:
                    return AgentHasValidSourceForNative(source) ? "connected" : "detected";
                case AgentSourceSetupState.AnalysisFailedSafely:
                    return "error";
                case AgentSourceSetupState.PermissionRequired:
                    return "warning";
                case AgentSourceSetupState.ManualImportRequired:
                    return "needsFolder";
                case AgentSourceSetupState.Selected:
                    return "detected";
                default:
                    return "notConfigured";
            }
        }

        private static string AgentProviderStatusText(string status)
        {
            switch (status)
            {
                case "detected": return "Detected. Connect to approve.";
                case "connected": return "Connected";
                case "needsFolder": return "Needs folder";
                case "validating": return "Validating source";
                case "analyzing": return "Analysis running";
                case "warning": return "Needs attention";
                case "error": return "Analysis failed";
                case "disconnected": return "Disconnected";
                default: return "Not configured";
            }
        }

        private static string AgentDisabledReasonForNative(ConnectedAgentSource source)
        {
            if (source == null || !source.Selected)
            {
                return "Connect this provider first.";
            }

            if (source.State == AgentSourceSetupState.PermissionRequired)
            {
                return "Folder permission is required before analysis.";
            }

            if (source.State == AgentSourceSetupState.AnalysisFailedSafely)
            {
                return string.IsNullOrWhiteSpace(source.StatusLabel) ? "Last analysis failed safely." : source.StatusLabel;
            }

            return "Detect or choose a folder before analyzing.";
        }

        private static string AgentSupportedStatusForNative(ConnectedAgentSource source)
        {
            if (source == null)
            {
                return "unsupported_on_this_machine";
            }

            switch (source.State)
            {
                case AgentSourceSetupState.ReadyToAnalyze:
                case AgentSourceSetupState.AnalysisComplete:
                    return AgentHasValidSourceForNative(source) ? "connected" : "notConfigured";
                case AgentSourceSetupState.LocalSourceDetected:
                    return source.Selected ? "available" : "available";
                case AgentSourceSetupState.PermissionRequired:
                    return "error";
                case AgentSourceSetupState.AnalysisFailedSafely:
                    return "error";
                case AgentSourceSetupState.ManualImportRequired:
                    return source.Selected ? "selected" : "notConfigured";
                case AgentSourceSetupState.DetectingLocalSource:
                    return "selected";
                default:
                    if (source.SourceType == ConnectedAgentSourceType.GitHubCopilot)
                    {
                        return source.Selected ? "selected" : "notConfigured";
                    }

                    return source.SourceType == ConnectedAgentSourceType.OtherManualLogFolder
                        ? source.Selected ? "selected" : "notConfigured"
                        : source.Selected ? "selected" : "notConfigured";
            }
        }

        private static string AgentUnsupportedReasonForNative(ConnectedAgentSource source)
        {
            if (source == null)
            {
                return "Provider is unavailable.";
            }

            switch (source.State)
            {
                case AgentSourceSetupState.PermissionRequired:
                case AgentSourceSetupState.ManualImportRequired:
                    return "Choose a local folder before analyzing.";
                case AgentSourceSetupState.AnalysisFailedSafely:
                    return string.IsNullOrWhiteSpace(source.StatusLabel) ? "Last analysis failed safely." : source.StatusLabel;
                case AgentSourceSetupState.DetectingLocalSource:
                    return "Detection is running.";
                default:
                    return source.Selected ? "Detect or choose a folder before analyzing." : "Select or connect this provider first.";
            }
        }

        private string AgentCodexStatus()
        {
            var source = approvedActivityAnalysis?.Onboarding.AgentSources.FirstOrDefault(item => item.SourceType == ConnectedAgentSourceType.Codex);
            if (source == null)
            {
                return "not_connected";
            }

            switch (source.State)
            {
                case AgentSourceSetupState.DetectingLocalSource:
                    return "detecting";
                case AgentSourceSetupState.LocalSourceDetected:
                    return "detected";
                case AgentSourceSetupState.PermissionRequired:
                    return "needs_folder_access";
                case AgentSourceSetupState.ManualImportRequired:
                    return "manual_folder_required";
                case AgentSourceSetupState.AnalysisFailedSafely:
                    return "analysis_failed";
                case AgentSourceSetupState.ReadyToAnalyze:
                case AgentSourceSetupState.AnalysisComplete:
                    return source.Selected ? "ready" : "not_connected";
                default:
                    return source.Selected ? "selected" : "not_connected";
            }
        }

        private string AgentCodexStatusText()
        {
            switch (AgentCodexStatus())
            {
                case "needs_folder_access":
                    return "Needs folder access";
                case "manual_folder_required":
                    return "Manual folder required";
                case "analysis_failed":
                    return "Analysis failed safely";
                case "detecting":
                    return "Detecting";
                case "ready":
                    return "Connected";
                case "detected":
                    return "Detected. Connect to approve.";
                case "selected":
                    return "Selected. Detect or choose folder.";
                default:
                    return "Not connected";
            }
        }

        private static void ApplyPendingReviewState(NativeDashboardState state, PendingNativeActivityReview pending)
        {
            if (state == null || pending == null)
            {
                return;
            }

            var deltas = pending.StatDeltas ?? CharacterStats.Zero();
            state.review.reviewId = SafeNativeText(pending.ReviewId, pending.SafeSession?.SessionId ?? "pending-review");
            state.review.pending = true;
            state.review.summary = SafeNativeText(pending.SafeSummary, "Aggregate activity ready for review.");
            state.review.source = pending.SourceKind;
            state.review.repositoryName = SafeNativeText(state.repository?.name, "No active repository");
            if (string.Equals(NormalizePendingSourceKind(pending.SourceKind), "aiAgent", StringComparison.Ordinal) &&
                string.IsNullOrWhiteSpace(pending.RepositoryHash))
            {
                state.review.repositoryName = "Agent-only";
            }
            state.review.providerName = PendingReviewProviderName(pending);
            state.review.confidence = pending.Confidence;
            state.review.estimatedXpDelta = Math.Max(0, pending.EstimatedXpDelta);
            state.review.codeDelta = Math.Max(0, deltas.Logic + deltas.Architecture + deltas.Velocity);
            state.review.focusDelta = Math.Max(0, deltas.Efficiency + deltas.Stability);
            state.review.debugDelta = Math.Max(0, deltas.Debug);
            state.review.designDelta = Math.Max(0, deltas.Design + deltas.Creativity);
            state.review.syncDelta = 0;
            state.review.warnings = string.Join(", ", (pending.WarningIds ?? new List<string>()).Take(3));
            state.review.canSaveGrowth = true;
            state.review.canDiscard = true;
            state.review.canViewDetails = true;
            state.review.status = "pending";
            state.review.generatedAt = pending.CreatedAtUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss");
            state.review.selectedReviewId = state.review.reviewId;
            state.review.evidenceSummary = ReviewEvidenceText(pending);
            state.review.categoryBreakdown = CategoryBreakdownText(deltas);
            state.activity.state = "Pending review";
            state.activity.todaySummary = state.review.summary;
            state.lastRunSummary = state.review.summary + " Approve to apply +" + state.review.estimatedXpDelta + " XP.";
        }

        private static string ReviewEvidenceText(PendingNativeActivityReview pending)
        {
            if (pending == null)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            var git = pending.SafeSessions?.FirstOrDefault(session => session?.GitChangeSummary != null)?.GitChangeSummary ??
                      pending.SafeSession?.GitChangeSummary;
            var agent = pending.SafeSessions?.FirstOrDefault(session => session?.AgentActivitySummary != null)?.AgentActivitySummary ??
                        pending.SafeSession?.AgentActivitySummary;
            if (git != null)
            {
                parts.Add("Git changes were analyzed and +" + Math.Max(0, pending.EstimatedXpDelta) + " XP was calculated.");
                parts.Add(git.TestFileChanged
                    ? "Implementation changes and test or fix loops were detected."
                    : "Implementation changes were detected from safe Git aggregates.");
            }

            if (agent != null && agent.ProviderType != AgentProviderType.Unknown)
            {
                parts.Add(MacAgentSourceDetector.SafeProviderLabel(agent.ProviderType) + " session activity was analyzed and +" + Math.Max(0, pending.EstimatedXpDelta) + " XP was calculated.");
                parts.Add("Raw prompts, code content, file content, and command text were not stored; only aggregate activity was used.");
            }

            return parts.Count == 0 ? SafeNativeText(pending.SafeSummary, "Safe aggregate evidence is available in this review.") : string.Join("\n", parts);
        }

        private static string CategoryBreakdownText(CharacterStats deltas)
        {
            deltas = deltas ?? CharacterStats.Zero();
            return "Code +" + Math.Max(0, deltas.Logic + deltas.Architecture + deltas.Velocity) +
                   " · Focus +" + Math.Max(0, deltas.Efficiency + deltas.Stability) +
                   " · Debug +" + Math.Max(0, deltas.Debug) +
                   " · Design +" + Math.Max(0, deltas.Design + deltas.Creativity) +
                   " · Sync +0";
        }

        private static string PendingReviewProviderName(PendingNativeActivityReview pending)
        {
            var session = pending?.SafeSessions?.FirstOrDefault(item => item?.AgentActivitySummary != null) ??
                          (pending?.SafeSession?.AgentActivitySummary != null ? pending.SafeSession : null);
            if (session?.AgentActivitySummary == null)
            {
                return string.Empty;
            }

            return MacAgentSourceDetector.SafeProviderLabel(session.AgentActivitySummary.ProviderType);
        }

        private static string NormalizePendingSourceKind(string sourceKind)
        {
            sourceKind = sourceKind ?? string.Empty;
            if (sourceKind.IndexOf("repository", StringComparison.OrdinalIgnoreCase) >= 0) return "repository";
            if (sourceKind.IndexOf("combined", StringComparison.OrdinalIgnoreCase) >= 0) return "combined";
            if (sourceKind.IndexOf("agent", StringComparison.OrdinalIgnoreCase) >= 0) return "aiAgent";
            return string.IsNullOrWhiteSpace(sourceKind) ? "repository" : "aiAgent";
        }

        private static void EnforceNativeDashboardInvariants(NativeDashboardState state)
        {
            if (state == null)
            {
                return;
            }

            if (!string.Equals(state.persistentStatusBarIdentifier, "TokenForge.PersistentStatusBar", StringComparison.Ordinal))
            {
                Debug.Log("INFO [NativeDashboard][NORMALIZE_LEGACY_ALIAS] field=persistentStatusBarIdentifier value=" + SafeNativeText(state.persistentStatusBarIdentifier, "empty") + " canonical=TokenForge.PersistentStatusBar");
                state.persistentStatusBarIdentifier = "TokenForge.PersistentStatusBar";
            }

            if (!string.Equals(state.persistentStatusBarAccessibilityLabel, "TokenForge persistent app status bar", StringComparison.Ordinal))
            {
                Debug.Log("INFO [NativeDashboard][NORMALIZE_LEGACY_ALIAS] field=persistentStatusBarAccessibilityLabel value=" + SafeNativeText(state.persistentStatusBarAccessibilityLabel, "empty") + " canonical=TokenForge persistent app status bar");
                state.persistentStatusBarAccessibilityLabel = "TokenForge persistent app status bar";
            }

            if (string.IsNullOrWhiteSpace(state.selectedNavItem))
            {
                Debug.Log("INFO [NativeDashboard][NORMALIZE_LEGACY_ALIAS] field=selectedNavItem value=empty canonical=dashboard");
                state.selectedNavItem = "dashboard";
            }

            var hasPendingReview = state.review != null && state.review.pending;
            state.pendingReviewCount = hasPendingReview ? 1 : 0;
            state.hasPendingReview = hasPendingReview;
            if (!hasPendingReview)
            {
                state.review = state.review ?? NativeReviewState.CreateDefault();
                state.review.pending = false;
                state.review.summary = "No pending review";
                state.review.reviewId = string.Empty;
                state.review.source = string.Empty;
                state.review.repositoryName = string.Empty;
                state.review.providerName = string.Empty;
                state.review.confidence = string.Empty;
                state.review.estimatedXpDelta = 0;
                state.review.codeDelta = 0;
                state.review.focusDelta = 0;
                state.review.debugDelta = 0;
                state.review.designDelta = 0;
                state.review.syncDelta = 0;
                state.review.warnings = string.Empty;
                state.review.canSaveGrowth = false;
                state.review.canDiscard = false;
                state.review.canViewDetails = false;
                state.review.detailVisible = false;
                state.review.selectedReviewId = string.Empty;
                state.review.generatedAt = string.Empty;
                state.review.status = "none";
                state.review.evidenceSummary = string.Empty;
                state.review.categoryBreakdown = string.Empty;
                state.canSaveGrowth = false;
                state.canDiscardPendingReview = false;
                state.pendingEstimatedXP = 0;
                if (string.Equals(state.activity?.state, "Pending review ready", StringComparison.Ordinal) ||
                    string.Equals(state.activity?.state, "Pending review", StringComparison.Ordinal))
                {
                    state.activity.state = "No pending review";
                }

                if (string.Equals(state.actionStatusKind, "success", StringComparison.Ordinal) &&
                    (state.actionStatusText ?? string.Empty).IndexOf("Pending review is ready", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    state.actionStatusKind = "idle";
                    state.actionStatusText = "Ready";
                }
            }
            else
            {
                state.review.canSaveGrowth = true;
                state.review.canDiscard = true;
                state.review.canViewDetails = true;
                state.canSaveGrowth = true;
                state.canDiscardPendingReview = true;
                state.pendingEstimatedXP = Math.Max(0, state.review.estimatedXpDelta);
                if (string.Equals(state.review.summary, "No pending review", StringComparison.OrdinalIgnoreCase))
                {
                    state.review.summary = "Aggregate activity ready for review.";
                }
            }
            if (!state.hasActiveRepository)
            {
                state.repositories = new NativeRepositoryListItem[0];
                if (state.repository != null)
                {
                    state.repository.connected = false;
                    state.repository.id = string.Empty;
                    state.repository.name = string.Empty;
                    state.repository.status = "not_selected";
                    state.repository.statusText = "No repository connected";
                    state.repository.connectedCount = 0;
                }

                state.companionFarm = state.companionFarm ?? DesktopCompanionFarmState.CreateDefault();
                state.companionFarm.enabled = false;
                state.companionFarm.overlays = new RepositoryCompanionOverlayState[0];
                state.companionFarm.visibleCount = 0;
                state.companionVisible = false;
                state.desiredVisible = false;
                state.actualVisible = false;
                state.activeRepositoryId = string.Empty;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!hasPendingReview && (state.review.canSaveGrowth || state.review.canDiscard || state.review.canViewDetails))
            {
                Debug.LogError("ERROR [NativeDashboard] invariant violated: pendingReview=false but review actions are enabled");
            }

            if (!hasPendingReview && (state.actionStatusText ?? string.Empty).IndexOf("Pending review is ready", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Debug.LogError("ERROR [NativeDashboard] invariant violated: pendingReview=false but header says pending review is ready");
            }

            if (hasPendingReview && string.Equals(state.review.summary, "No pending review", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError("ERROR [NativeDashboard] invariant violated: pendingReview=true but pending card says no pending review");
            }

            if (!state.hasSavedReviews && LooksLikeSampleSavedReview(state.activity?.savedReviewsSummary))
            {
                Debug.LogError("ERROR [NativeDashboard] invariant violated: sample saved review leaked into production projection");
            }

            if (!state.hasRepositoryActivity && LooksLikeSampleRepositoryActivity(state.activity?.repositoryActivitySummary))
            {
                Debug.LogError("ERROR [NativeDashboard] invariant violated: sample repository activity leaked into production projection");
            }

            if (!state.hasAiAgentActivity && LooksLikeSampleAgentActivity(state.activity?.agentActivitySummary))
            {
                Debug.LogError("ERROR [NativeDashboard] invariant violated: sample AI agent activity leaked into production projection");
            }

            foreach (var provider in state.agentProviders ?? new NativeAgentProviderState[0])
            {
                if (!provider.hasValidSource && provider.connected)
                {
                    Debug.LogError("ERROR [NativeDashboard] invariant violated: provider connected without valid source provider=" + provider.id);
                }

                if (!provider.hasValidSource && provider.canAnalyze)
                {
                    Debug.LogError("ERROR [NativeDashboard] invariant violated: provider canAnalyze without valid source provider=" + provider.id);
                }
            }

            var claudeUsage = (state.providerUsagePercentages ?? new NativeProviderUsagePercentage[0])
                .FirstOrDefault(item => string.Equals(item.providerId, "claudeCode", StringComparison.Ordinal));
            if ((claudeUsage == null || claudeUsage.percentage == 0) &&
                (state.statusText ?? string.Empty).IndexOf("Cl " + "10%", StringComparison.Ordinal) >= 0)
            {
                Debug.LogError("ERROR [NativeDashboard] invariant violated: status bar shows Claude usage without approved Claude activity");
            }

            if (!state.hasActiveRepository && string.Equals(state.repository?.statusText, "Active repository", StringComparison.Ordinal))
            {
                Debug.LogError("ERROR [NativeDashboard] invariant violated: active repository shown without active repository state");
            }

            if (!state.hasActiveRepository && (state.repositories?.Length ?? 0) > 0)
            {
                Debug.LogError("ERROR [NativeDashboard] invariant violated: repository companions shown without active connected repository");
            }
#endif
        }

        private static bool LooksLikeSampleSavedReview(string value)
        {
            value = value ?? string.Empty;
            return value.IndexOf("sample saved review", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("+101" + " XP", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("Level 1" + " -> 1", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool LooksLikeSampleRepositoryActivity(string value)
        {
            value = value ?? string.Empty;
            return value.IndexOf("sample repository activity", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("category " + "Test", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("confidence " + "Medium", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("warnings " + "2", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("sessions " + "One", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("interactions " + "Large", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool LooksLikeSampleAgentActivity(string value)
        {
            value = value ?? string.Empty;
            return value.IndexOf("2 providers " + "ready", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("sample AI agent activity", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void HandleNativeDashboardAction(NativeDashboardActionRequest request)
        {
            if (request == null)
            {
                return;
            }

            Debug.Log("INFO [NativeAction] received action=" + request.RawAction);
            lock (pendingNativeActionsLock)
            {
                pendingNativeActions.Enqueue(request);
            }
        }

        private bool TryDequeueNativeAction(out NativeDashboardActionRequest request)
        {
            lock (pendingNativeActionsLock)
            {
                if (pendingNativeActions.Count > 0)
                {
                    request = pendingNativeActions.Dequeue();
                    return true;
                }
            }

            request = null;
            return false;
        }

        private void RouteNativeDashboardAction(NativeDashboardActionRequest request)
        {
            if (nativeExplicitQuitRequested && request.Action != NativeDashboardAction.Quit)
            {
                Debug.Log("INFO [NativeAction][IGNORED_DURING_QUIT] action=" + request.RawAction);
                return;
            }

            if (Thread.CurrentThread.ManagedThreadId != unityMainThreadId)
            {
                Debug.LogWarning("WARN [Threading] mainThread violation action=" + request.RawAction);
            }

            Debug.Log("INFO [NativeAction] routed action=" + request.RawAction + " handler=" + request.Action);
            Debug.Log("INFO [DashboardAction] action=" + request.RawAction + " target=" + request.Value + " enabled=true result=received");
            Debug.Log("INFO [OverlayTrace:" + request.TraceId + "] C# route resolved target=" + request.Action);
            if (request.Action == NativeDashboardAction.ShowCompanion || request.Action == NativeDashboardAction.HideCompanion || request.Action == NativeDashboardAction.ToggleCompanionVisible)
            {
                var normalizedOverlayAction = request.Action == NativeDashboardAction.HideCompanion ? "hideFromDesktop" : "showOnDesktop";
                Debug.Log("INFO [OverlayAction][RECEIVED] action=" + normalizedOverlayAction + " source=dashboard explicit=true rawAction=" + request.RawAction);
            }
            switch (request.Action)
            {
                case NativeDashboardAction.Dashboard:
                    nativeSelectedNavItem = "dashboard";
                    RunNativeDashboardTask(RefreshAndPublishNativeDashboardAsync, request.RawAction);
                    OpenNativeDashboardCanonical(request.RawAction);
                    break;
                case NativeDashboardAction.ShowDashboard:
                    OpenNativeDashboardCanonical(request.RawAction);
                    break;
                case NativeDashboardAction.ToggleDashboard:
                    nativeDashboardService?.ToggleDashboardWindow();
                    break;
                case NativeDashboardAction.HideDashboard:
                    nativeDashboardShown = false;
                    nativeDashboardService?.HideDashboardWindow();
                    break;
                case NativeDashboardAction.Settings:
                    nativeSelectedNavItem = "settings";
                    nativeDashboardService?.ShowSettingsWindow();
                    RunNativeDashboardTask(RefreshAndPublishNativeDashboardAsync, request.RawAction);
                    break;
                case NativeDashboardAction.Activity:
                    nativeSelectedNavItem = "activity";
                    RunNativeDashboardTask(RefreshAndPublishNativeDashboardAsync, request.RawAction);
                    break;
                case NativeDashboardAction.RunAnalysis:
                    RunNativeDashboardTask(() => RunNativeAnalysisAsync(requestedGitAnalysisMode: NativeGitAnalysisModeFromScope(request.Value)), request.RawAction);
                    break;
                case NativeDashboardAction.RunRepositoryAnalysis:
                    RunNativeDashboardTask(() => RunNativeAnalysisAsync(repositoryOnly: true, requestedGitAnalysisMode: NativeGitAnalysisModeFromScope(request.Value)), request.RawAction);
                    break;
                case NativeDashboardAction.RunAgentAnalysis:
                    RunNativeDashboardTask(RunNativeAgentAnalysisAsync, request.RawAction);
                    break;
                case NativeDashboardAction.ConnectRepository:
                case NativeDashboardAction.ChangeRepository:
                case NativeDashboardAction.ChooseRepositoryFolder:
                    nativeSelectedNavItem = "repository";
                    RunNativeDashboardTask(ConnectRepositoryFromNativeAsync, request.RawAction);
                    break;
                case NativeDashboardAction.Repository:
                    nativeSelectedNavItem = "repository";
                    RunNativeDashboardTask(RefreshAndPublishNativeDashboardAsync, request.RawAction);
                    break;
                case NativeDashboardAction.SelectRepository:
                    nativeSelectedNavItem = "repository";
                    RunNativeDashboardTask(() => SelectRepositoryFromNativeAsync(request.Value), request.RawAction);
                    break;
                case NativeDashboardAction.OpenActiveCompanionDashboard:
                    nativeSelectedNavItem = "dashboard";
                    RunNativeDashboardTask(RefreshAndPublishNativeDashboardAsync, request.RawAction);
                    OpenNativeDashboardCanonical(request.RawAction);
                    break;
                case NativeDashboardAction.OpenRepositoryCompanionDashboard:
                    nativeSelectedNavItem = "dashboard";
                    RunNativeDashboardTask(() => SelectRepositoryCompanionDashboardFromNativeAsync(request.Value), request.RawAction);
                    OpenNativeDashboardCanonical(request.RawAction);
                    break;
                case NativeDashboardAction.AnalyzeRepository:
                    RunNativeDashboardTask(() => RunNativeAnalysisAsync(request.Value, repositoryOnly: true, requestedGitAnalysisMode: NativeGitAnalysisModeFromScope(request.Value)), request.RawAction);
                    break;
                case NativeDashboardAction.DisconnectRepository:
                    nativeSelectedNavItem = "repository";
                    RunNativeDashboardTask(() => DisconnectRepositoryFromNativeAsync(request.Value), request.RawAction);
                    break;
                case NativeDashboardAction.ConnectCodexAgent:
                    nativeSelectedNavItem = "aiAgents";
                    RunNativeDashboardTask(ConnectCodexFromNativeAsync, request.RawAction);
                    break;
                case NativeDashboardAction.ConnectAiAgent:
                    nativeSelectedNavItem = "aiAgents";
                    RunNativeDashboardTask(() => ConnectAgentFromNativeAsync(string.IsNullOrWhiteSpace(request.Value) ? "codex" : request.Value), request.RawAction);
                    break;
                case NativeDashboardAction.CodexAgent:
                case NativeDashboardAction.ManageAgents:
                    nativeSelectedNavItem = "aiAgents";
                    RunNativeDashboardTask(RefreshAndPublishNativeDashboardAsync, request.RawAction);
                    break;
                case NativeDashboardAction.AutoDetectAgent:
                case NativeDashboardAction.DetectAgent:
                    nativeSelectedNavItem = "aiAgents";
                    RunNativeDashboardTask(() => AutoDetectAgentFromNativeAsync(request.Value), request.RawAction);
                    break;
                case NativeDashboardAction.ConnectAgent:
                    nativeSelectedNavItem = "aiAgents";
                    RunNativeDashboardTask(() => ConnectAgentFromNativeAsync(request.Value), request.RawAction);
                    break;
                case NativeDashboardAction.ChooseAgentFolder:
                case NativeDashboardAction.SelectCodexLogFolder:
                    nativeSelectedNavItem = "aiAgents";
                    RunNativeDashboardTask(() => SelectAgentFolderFromNativeAsync(request.Value), request.RawAction);
                    break;
                case NativeDashboardAction.AnalyzeAgent:
                    RunNativeDashboardTask(() => RunNativeAnalysisAsync(string.Empty, request.Value), request.RawAction);
                    break;
                case NativeDashboardAction.DisconnectAgent:
                    nativeSelectedNavItem = "aiAgents";
                    RunNativeDashboardTask(() => DisconnectAgentFromNativeAsync(request.Value), request.RawAction);
                    break;
                case NativeDashboardAction.ReviewActivity:
                    nativeSelectedNavItem = "activity";
                    RunNativeDashboardTask(ReviewNativeActivityAsync, request.RawAction);
                    break;
                case NativeDashboardAction.TokenShop:
                    nativeSelectedNavItem = "tokenShop";
                    nativeActionStatusKind = "idle";
                    nativeActionStatusText = "Token Shop opened. Spend coins earned by each AI agent's token usage.";
                    RunNativeDashboardTask(RefreshAndPublishNativeDashboardAsync, request.RawAction);
                    OpenNativeDashboardCanonical(request.RawAction);
                    break;
                case NativeDashboardAction.Wardrobe:
                    nativeSelectedNavItem = "wardrobe";
                    nativeActionStatusKind = "idle";
                    nativeActionStatusText = "Wardrobe opened. Equip owned cosmetics without spending coins.";
                    Debug.Log("INFO [Wardrobe][TARGET_SELECTED] target=" + SafeNativeText(nativeShopTargetType, "repositoryCompanion"));
                    RunNativeDashboardTask(RefreshAndPublishNativeDashboardAsync, request.RawAction);
                    OpenNativeDashboardCanonical(request.RawAction);
                    break;
                case NativeDashboardAction.Onboarding:
                    nativeSelectedNavItem = "onboarding";
                    nativeActionStatusKind = "idle";
                    nativeActionStatusText = "Onboarding opened.";
                    RunNativeDashboardTask(async () =>
                    {
                        if (approvedActivityAnalysis != null)
                        {
                            await approvedActivityAnalysis.MarkOnboardingOpenedAsync();
                        }

                        await RefreshAndPublishNativeDashboardAsync();
                    }, request.RawAction);
                    OpenNativeDashboardCanonical(request.RawAction);
                    break;
                case NativeDashboardAction.SetOnboardingStep:
                    nativeSelectedNavItem = "onboarding";
                    nativeActionStatusKind = "idle";
                    nativeActionStatusText = "Onboarding step changed.";
                    RunNativeDashboardTask(async () =>
                    {
                        if (approvedActivityAnalysis != null)
                        {
                            var stepIndex = 0;
                            int.TryParse(request.Value, out stepIndex);
                            await approvedActivityAnalysis.SetFirstRunOnboardingStepAsync(stepIndex);
                        }

                        await RefreshAndPublishNativeDashboardAsync();
                    }, request.RawAction);
                    break;
                case NativeDashboardAction.SkipOnboarding:
                    nativeSelectedNavItem = "dashboard";
                    nativeActionStatusKind = "idle";
                    nativeActionStatusText = "Onboarding skipped for now. You can reopen it from the sidebar.";
                    RunNativeDashboardTask(async () =>
                    {
                        if (approvedActivityAnalysis != null)
                        {
                            await approvedActivityAnalysis.DismissFirstRunOnboardingForNowAsync();
                        }

                        await RefreshAndPublishNativeDashboardAsync();
                    }, request.RawAction);
                    break;
                case NativeDashboardAction.CompleteOnboarding:
                    nativeSelectedNavItem = "dashboard";
                    nativeActionStatusKind = "success";
                    nativeActionStatusText = "Onboarding completed. You can reopen it from the sidebar.";
                    RunNativeDashboardTask(async () =>
                    {
                        if (approvedActivityAnalysis != null)
                        {
                            await approvedActivityAnalysis.CompleteFirstRunOnboardingAsync();
                        }

                        await RefreshAndPublishNativeDashboardAsync();
                    }, request.RawAction);
                    break;
                case NativeDashboardAction.ResetOnboarding:
                    nativeSelectedNavItem = "onboarding";
                    nativeActionStatusKind = "success";
                    nativeActionStatusText = "Onboarding reset. The guide will open on first launch until Done is clicked.";
                    RunNativeDashboardTask(async () =>
                    {
                        if (approvedActivityAnalysis != null)
                        {
                            await approvedActivityAnalysis.ResetFirstRunOnboardingAsync();
                        }

                        await RefreshAndPublishNativeDashboardAsync();
                    }, request.RawAction);
                    OpenNativeDashboardCanonical(request.RawAction);
                    break;
                case NativeDashboardAction.PurchaseTokenShopItem:
                    nativeSelectedNavItem = "tokenShop";
                    RunNativeDashboardTask(() => PurchaseTokenShopItemFromNativeAsync(request.Value), request.RawAction);
                    break;
                case NativeDashboardAction.SelectShopRepositoryTarget:
                    nativeSelectedNavItem = "tokenShop";
                    nativeShopTargetType = "repositoryCompanion";
                    nativeActionStatusKind = "shop";
                    nativeActionStatusText = "Shopping for repository mascot.";
                    RunNativeDashboardTask(RefreshAndPublishNativeDashboardAsync, request.RawAction);
                    break;
                case NativeDashboardAction.SelectShopAgentTarget:
                    nativeSelectedNavItem = "tokenShop";
                    nativeShopTargetType = "aiAgent";
                    nativeShopSelectedAgentId = RepositoryCompanionProfileService.NormalizeAgentShopId(request.Value);
                    if (string.IsNullOrWhiteSpace(nativeShopSelectedAgentId) || string.Equals(nativeShopSelectedAgentId, "agents", StringComparison.OrdinalIgnoreCase))
                    {
                        nativeShopSelectedAgentId = "codex";
                    }
                    nativeActionStatusKind = "shop";
                    nativeActionStatusText = RepositoryCompanionProfileService.AgentDisplayName(nativeShopSelectedAgentId) + " shop uses " + RepositoryCompanionProfileService.AgentCurrencyName(nativeShopSelectedAgentId) + " only.";
                    RunNativeDashboardTask(RefreshAndPublishNativeDashboardAsync, request.RawAction);
                    break;
                case NativeDashboardAction.SelectShopCategory:
                    nativeSelectedNavItem = "tokenShop";
                    nativeShopSelectedCategory = NormalizeShopCategory(request.Value);
                    RunNativeDashboardTask(RefreshAndPublishNativeDashboardAsync, request.RawAction);
                    break;
                case NativeDashboardAction.EquipTokenShopItem:
                    nativeSelectedNavItem = "tokenShop";
                    RunNativeDashboardTask(() => EquipTokenShopItemFromNativeAsync(request.Value), request.RawAction);
                    break;
                case NativeDashboardAction.PreviewTokenShopItem:
                    nativeSelectedNavItem = "tokenShop";
                    nativeActionStatusKind = "shop";
                    nativeActionStatusText = string.IsNullOrWhiteSpace(request.Value) ? "Previewing zodiac cosmetics." : "Previewing " + SafeNativeText(request.Value, "shop item") + ".";
                    RunNativeDashboardTask(RefreshAndPublishNativeDashboardAsync, request.RawAction);
                    break;
                case NativeDashboardAction.SelectRepositoryZodiacMascot:
                    nativeSelectedNavItem = "settings";
                    RunNativeDashboardTask(() => SetRepositoryZodiacMascotFromNativeAsync(request.Value), request.RawAction);
                    break;
                case NativeDashboardAction.OpenAgentConnect:
                    nativeSelectedNavItem = "aiAgents";
                    RunNativeDashboardTask(() => ConnectAgentFromNativeAsync(request.Value), request.RawAction);
                    break;
                case NativeDashboardAction.ViewReviewDetails:
                    nativeSelectedNavItem = "activity";
                    RunNativeDashboardTask(() => ViewNativeReviewDetailsAsync(request.Value), request.RawAction);
                    break;
                case NativeDashboardAction.ApproveReview:
                case NativeDashboardAction.SaveReview:
                case NativeDashboardAction.SaveGrowth:
                    RunNativeDashboardTask(ApproveNativeReviewAsync, request.RawAction);
                    break;
                case NativeDashboardAction.LevelUpCompanion:
                    RunNativeDashboardTask(() => LevelUpNativeCompanionAsync(request.Value), request.RawAction);
                    break;
                case NativeDashboardAction.SafeSync:
                    nativeActionStatusKind = "error";
                    nativeActionStatusText = "Safe Sync needs a server session. Local analysis and Save Growth still work offline.";
                    RunNativeDashboardTask(RefreshAndPublishNativeDashboardAsync, request.RawAction);
                    break;
                case NativeDashboardAction.DiscardReview:
                    RunNativeDashboardTask(DiscardNativeReviewAsync, request.RawAction);
                    break;
                case NativeDashboardAction.ToggleCompanionVisible:
                    RunNativeDashboardTask(() => SetCompanionVisibleFromNativeAsync(request.BoolValue(!(approvedActivityAnalysis?.CharacterDashboard?.DesktopCompanionSettings?.IsDesktopCompanionEnabled ?? true)), request.TraceId, "explicitDashboardAction"), request.RawAction);
                    break;
                case NativeDashboardAction.ShowCompanion:
                    RunNativeDashboardTask(() => SetCompanionVisibleFromNativeAsync(true, request.TraceId, "explicitDashboardAction"), request.RawAction);
                    break;
                case NativeDashboardAction.HideCompanion:
                    RunNativeDashboardTask(() => SetCompanionVisibleFromNativeAsync(false, request.TraceId, "explicitDashboardAction"), request.RawAction);
                    break;
                case NativeDashboardAction.SetWanderEnabled:
                    RunNativeDashboardTask(() => SetWanderEnabledFromNativeAsync(request.BoolValue(true), request.TraceId), request.RawAction);
                    break;
                case NativeDashboardAction.EnableWander:
                    RunNativeDashboardTask(() => SetWanderEnabledFromNativeAsync(true, request.TraceId), request.RawAction);
                    break;
                case NativeDashboardAction.DisableWander:
                    RunNativeDashboardTask(() => SetWanderEnabledFromNativeAsync(false, request.TraceId), request.RawAction);
                    break;
                case NativeDashboardAction.SetClickReactionEnabled:
                    RunNativeDashboardTask(() => SetClickReactionEnabledFromNativeAsync(request.BoolValue(true)), request.RawAction);
                    break;
                case NativeDashboardAction.SetClickThroughEnabled:
                    RunNativeDashboardTask(() => SetClickThroughEnabledFromNativeAsync(request.BoolValue(false)), request.RawAction);
                    break;
                case NativeDashboardAction.EnableDrag:
                    RunNativeDashboardTask(() => SetDragEnabledFromNativeAsync(true), request.RawAction);
                    break;
                case NativeDashboardAction.EnableClickThrough:
                    RunNativeDashboardTask(() => SetClickThroughEnabledFromNativeAsync(true), request.RawAction);
                    break;
                case NativeDashboardAction.DisableClickThrough:
                    RunNativeDashboardTask(() => SetClickThroughEnabledFromNativeAsync(false), request.RawAction);
                    break;
                case NativeDashboardAction.EnableClick:
                    RunNativeDashboardTask(() => SetClickReactionEnabledFromNativeAsync(true), request.RawAction);
                    break;
                case NativeDashboardAction.DisableClick:
                    RunNativeDashboardTask(() => SetClickReactionEnabledFromNativeAsync(false), request.RawAction);
                    break;
                case NativeDashboardAction.ResetCompanionPosition:
                    RunNativeDashboardTask(ResetCompanionPositionFromNativeAsync, request.RawAction);
                    break;
                case NativeDashboardAction.ChangeCompanionSkin:
                    RunNativeDashboardTask(() => SetCompanionSkinFromNativeAsync(request.Value), request.RawAction);
                    break;
                case NativeDashboardAction.SetLaunchAtLogin:
                    Debug.Log("INFO [NativeDashboard] launch at login requested value=" + request.Value + " status=comingSoon");
                    RunNativeDashboardTask(RefreshAndPublishNativeDashboardAsync, request.RawAction);
                    break;
                case NativeDashboardAction.Homepage:
                    Application.OpenURL("https://github.com/HwangSeokBeom/TokenForge");
                    break;
                case NativeDashboardAction.ReportIssue:
                    Debug.Log("INFO [ReportIssue] manualOpen requested=true autoPresent=false nonBlocking=true source=nativeDashboard");
                    Application.OpenURL("https://github.com/HwangSeokBeom/TokenForge/issues");
                    break;
                case NativeDashboardAction.Quit:
                    nativeExplicitQuitRequested = true;
                    Debug.Log("INFO [AppLifecycle][QUIT_REQUESTED] source=nativeBridge traceId=" + request.TraceId);
                    Debug.Log("INFO [AppLifecycle] explicitQuitRequested=true source=nativeDashboard action=" + request.RawAction);
                    lifecycleService?.Quit();
                    break;
                case NativeDashboardAction.ResetLocalState:
                    Debug.Log("INFO [NativeDashboard] reset local state requested status=manualRequired");
                    OpenNativeDashboardCanonical(request.RawAction);
                    break;
                case NativeDashboardAction.Unsupported:
                    Debug.LogWarning("WARN [NativeAction] unknown action=" + request.RawAction);
                    SetUnsupportedNativeAction("Unsupported dashboard action.");
                    RunNativeDashboardTask(RefreshAndPublishNativeDashboardAsync, request.RawAction);
                    break;
            }
        }

        private async void RunNativeDashboardTask(Func<Task> taskFactory, string action)
        {
            if (nativeExplicitQuitRequested)
            {
                Debug.Log("INFO [NativeAction][TASK_SUPPRESSED_DURING_QUIT] action=" + action);
                return;
            }

            Debug.Log("INFO [Threading] dispatch background action=" + action);
            try
            {
                await taskFactory();
                if (nativeExplicitQuitRequested)
                {
                    Debug.Log("INFO [NativeAction][TASK_COMPLETED_SUPPRESSED_DURING_QUIT] action=" + action);
                    return;
                }

                Debug.Log("INFO [Threading] dispatch main action=" + action);
                Debug.Log("INFO [DashboardAction] action=" + action + " target= result=completed");
            }
            catch (Exception exception)
            {
                nativeAnalysisInProgress = false;
                nativeActionStatusKind = "error";
                nativeActionStatusText = "Action failed safely: " + exception.GetType().Name;
                Debug.LogError("ERROR [NativeAction] action failed action=" + action + " reason=" + exception.Message);
                Debug.Log("INFO [DashboardAction] action=" + action + " target= result=failed");
                ApplyNativeShellState(showDashboardIfNeeded: false);
            }
        }

        private async Task RefreshAndPublishNativeDashboardAsync()
        {
            await RefreshDashboardAsync(loadAuthSessionOnStart);
            ApplyNativeShellState(showDashboardIfNeeded: false);
        }

        private async Task ConnectRepositoryFromNativeAsync()
        {
            if (approvedActivityAnalysis == null)
            {
                return;
            }

            Debug.Log("INFO [Repository] connect begin");
            var result = await approvedActivityAnalysis.SelectLocalGitRepositoryForOnboardingAsync();
            var connected = result.IsSuccess && ActiveRepositoryReadyForNative();
            nativeActionStatusKind = connected ? "success" : result.IsSuccess ? "warning" : "error";
            nativeActionStatusText = connected
                ? "Repository connected. Run Analysis to create a pending review."
                : result.IsSuccess
                    ? "Repository selection cancelled. No repository connected."
                    : "Repository connection failed: " + SafeNativeText(result.ErrorMessage, result.ErrorCode);
            if (!result.IsSuccess)
            {
                Debug.LogWarning("WARN [Repository] validate failed reason=" + result.ErrorCode);
            }
            Debug.Log("INFO [RepositoryState] active=" + SafeNativeText(approvedActivityAnalysis.CharacterDashboard?.CurrentRepositoryHash, "none") + " count=" + (approvedActivityAnalysis.RepositoryCompanions?.Count ?? 0) + " archived=0");

            await RefreshAndPublishNativeDashboardAsync();
        }

        private async Task ConnectCodexFromNativeAsync()
        {
            await ConnectAgentFromNativeAsync("Codex");
        }

        private async Task SelectRepositoryFromNativeAsync(string repositoryHash)
        {
            if (approvedActivityAnalysis == null || string.IsNullOrWhiteSpace(repositoryHash))
            {
                return;
            }

            Debug.Log("INFO [NativeBridge] callback action=setActiveRepository repo=" + SafeNativeText(repositoryHash, "unknown"));
            var result = await approvedActivityAnalysis.SelectRepositoryCompanionProfileAsync(repositoryHash);
            nativeActionStatusKind = result.IsSuccess ? "success" : "error";
            nativeActionStatusText = result.IsSuccess ? "Active repository changed." : "Repository switch failed: " + SafeNativeText(result.ErrorMessage, result.ErrorCode);
            await RefreshAndPublishNativeDashboardAsync();
        }

        private async Task SelectRepositoryCompanionDashboardFromNativeAsync(string repositoryHash)
        {
            if (approvedActivityAnalysis == null || string.IsNullOrWhiteSpace(repositoryHash))
            {
                nativeActionStatusKind = "error";
                nativeActionStatusText = "Repository companion selection failed: missing repository id.";
                await RefreshAndPublishNativeDashboardAsync();
                return;
            }

            Debug.Log("INFO [DashboardActionRouter] action=open_repository_companion_dashboard resolvedRepository=" + SafeNativeText(repositoryHash, "unknown"));
            var result = await approvedActivityAnalysis.SelectRepositoryCompanionProfileAsync(repositoryHash);
            nativeActionStatusKind = result.IsSuccess ? "success" : "error";
            nativeActionStatusText = result.IsSuccess
                ? "Repository companion dashboard opened."
                : "Repository companion switch failed: " + SafeNativeText(result.ErrorMessage, result.ErrorCode);
            Debug.Log("INFO [RepositoryState] active=" + SafeNativeText(approvedActivityAnalysis.CharacterDashboard?.CurrentRepositoryHash, "none") +
                      " requested=" + SafeNativeText(repositoryHash, "unknown") +
                      " dashboardTab=dashboard result=" + (result.IsSuccess ? "selected" : "failed"));
            await RefreshAndPublishNativeDashboardAsync();
        }

        private async Task DisconnectRepositoryFromNativeAsync(string repositoryHash)
        {
            if (approvedActivityAnalysis == null || string.IsNullOrWhiteSpace(repositoryHash))
            {
                return;
            }

            var result = await approvedActivityAnalysis.RemoveRepositoryCompanionProfileAsync(repositoryHash);
            if (result.IsSuccess)
            {
                await approvedActivityAnalysis.RefreshApprovedLocationsAsync();
                await approvedActivityAnalysis.RestoreLocalSelectionsFromApprovedLocationsAsync();
                if (!ActiveRepositoryReadyForNative())
                {
                    gitAnalysisFlow?.ClearSelection();
                }
            }

            nativeActionStatusKind = result.IsSuccess ? "success" : "error";
            nativeActionStatusText = result.IsSuccess ? "Repository archived. Active context moved to the next available repository." : "Repository archive failed: " + SafeNativeText(result.ErrorMessage, result.ErrorCode);
            Debug.Log("INFO [RepositoryState] active=" + SafeNativeText(approvedActivityAnalysis.CharacterDashboard?.CurrentRepositoryHash, "none") + " count=" + (approvedActivityAnalysis.RepositoryCompanions?.Count ?? 0) + " archived=true");
            await RefreshAndPublishNativeDashboardAsync();
        }

        private async Task AutoDetectAgentFromNativeAsync(string providerValue)
        {
            if (approvedActivityAnalysis == null)
            {
                return;
            }

            if (!TrySourceTypeForNative(providerValue, out var sourceType))
            {
                SetUnsupportedNativeAction("Unsupported AI provider action.");
                await RefreshAndPublishNativeDashboardAsync();
                return;
            }

            var result = await approvedActivityAnalysis.DetectAgentSourceForOnboardingAsync(sourceType);
            var detected = approvedActivityAnalysis.Onboarding.AgentSources.FirstOrDefault(item => item.SourceType == sourceType)?.State == AgentSourceSetupState.LocalSourceDetected;
            nativeActionStatusKind = result.IsSuccess ? "success" : "error";
            nativeActionStatusText = result.IsSuccess
                ? detected ? "Detected source found. Connect to approve before analysis." : "AI agent provider checked."
                : "AI agent detection needs attention: " + SafeNativeText(result.ErrorMessage, result.ErrorCode);
            var source = approvedActivityAnalysis.Onboarding.AgentSources.FirstOrDefault(item => item.SourceType == sourceType);
            Debug.Log("INFO [AgentState] provider=" + sourceType + " status=" + AgentProviderStatusForNative(source) + " source=" + SafeNativeText(source?.SafeLabel, "none") + " warnings=" + Math.Max(0, source?.WarningCount ?? 0));
            await RefreshAndPublishNativeDashboardAsync();
        }

        private async Task ConnectAgentFromNativeAsync(string providerValue)
        {
            if (approvedActivityAnalysis == null)
            {
                return;
            }

            if (!TrySourceTypeForNative(providerValue, out var sourceType))
            {
                SetUnsupportedNativeAction("Unsupported AI provider action.");
                await RefreshAndPublishNativeDashboardAsync();
                return;
            }

            var sourceBefore = approvedActivityAnalysis.Onboarding.AgentSources.FirstOrDefault(item => item.SourceType == sourceType);
            Result result;
            if (sourceBefore != null && sourceBefore.State == AgentSourceSetupState.LocalSourceDetected)
            {
                result = await approvedActivityAnalysis.ApproveDetectedAgentSourceForOnboardingAsync(sourceType);
            }
            else
            {
                result = await approvedActivityAnalysis.DetectAgentSourceForOnboardingAsync(sourceType);
            }

            var sourceAfterDetect = approvedActivityAnalysis.Onboarding.AgentSources.FirstOrDefault(item => item.SourceType == sourceType);
            if (result.IsSuccess && sourceAfterDetect != null && sourceAfterDetect.State == AgentSourceSetupState.LocalSourceDetected)
            {
                nativeActionStatusKind = "success";
                nativeActionStatusText = "Detected source found. Click Connect again to approve it, or choose a folder manually.";
                Debug.Log("INFO [AgentState] provider=" + sourceType + " status=" + AgentProviderStatusForNative(sourceAfterDetect) + " source=" + SafeNativeText(sourceAfterDetect.SafeLabel, "none") + " warnings=" + Math.Max(0, sourceAfterDetect.WarningCount));
                await RefreshAndPublishNativeDashboardAsync();
                return;
            }

            if (!result.IsSuccess)
            {
                result = await approvedActivityAnalysis.SelectManualAgentLogForOnboardingAsync(sourceType);
            }

            var ready = IsAgentReadyForNative(approvedActivityAnalysis.Onboarding.AgentSources.FirstOrDefault(item => item.SourceType == sourceType));
            nativeActionStatusKind = result.IsSuccess && ready ? "success" : "error";
            nativeActionStatusText = result.IsSuccess && ready
                ? "AI agent provider ready. Analyze it from the AI Agents screen."
                : "AI agent connection needs a manual folder: " + SafeNativeText(result.ErrorMessage, result.ErrorCode);
            var source = approvedActivityAnalysis.Onboarding.AgentSources.FirstOrDefault(item => item.SourceType == sourceType);
            Debug.Log("INFO [AgentState] provider=" + sourceType + " status=" + AgentProviderStatusForNative(source) + " source=" + SafeNativeText(source?.SafeLabel, "none") + " warnings=" + Math.Max(0, source?.WarningCount ?? 0));
            await RefreshAndPublishNativeDashboardAsync();
        }

        private async Task SelectAgentFolderFromNativeAsync(string providerValue)
        {
            if (approvedActivityAnalysis == null)
            {
                return;
            }

            if (!TrySourceTypeForNative(providerValue, out var sourceType))
            {
                SetUnsupportedNativeAction("Unsupported AI provider action.");
                await RefreshAndPublishNativeDashboardAsync();
                return;
            }

            var result = await approvedActivityAnalysis.SelectManualAgentLogForOnboardingAsync(sourceType);
            var ready = IsAgentReadyForNative(approvedActivityAnalysis.Onboarding.AgentSources.FirstOrDefault(item => item.SourceType == sourceType));
            nativeActionStatusKind = result.IsSuccess && ready ? "success" : result.IsSuccess ? "warning" : "error";
            nativeActionStatusText = result.IsSuccess && ready
                ? "AI agent folder selected."
                : result.IsSuccess
                    ? "AI agent folder selection cancelled. Existing state is unchanged."
                    : "AI agent folder selection failed: " + SafeNativeText(result.ErrorMessage, result.ErrorCode);
            await RefreshAndPublishNativeDashboardAsync();
        }

        private async Task DisconnectAgentFromNativeAsync(string providerValue)
        {
            if (approvedActivityAnalysis == null)
            {
                return;
            }

            if (!TrySourceTypeForNative(providerValue, out var sourceType))
            {
                SetUnsupportedNativeAction("Unsupported AI provider action.");
                await RefreshAndPublishNativeDashboardAsync();
                return;
            }

            var result = await approvedActivityAnalysis.DisconnectAgentSourceForOnboardingAsync(sourceType);
            nativeActionStatusKind = result.IsSuccess ? "success" : "error";
            nativeActionStatusText = result.IsSuccess ? "AI agent provider disconnected." : "AI agent disconnect failed: " + SafeNativeText(result.ErrorMessage, result.ErrorCode);
            await RefreshAndPublishNativeDashboardAsync();
        }

        private static ConnectedAgentSourceType SourceTypeForNative(string providerValue)
        {
            return TrySourceTypeForNative(providerValue, out var sourceType) ? sourceType : ConnectedAgentSourceType.Codex;
        }

        private static bool TrySourceTypeForNative(string providerValue, out ConnectedAgentSourceType sourceType)
        {
            var normalizedValue = (providerValue ?? string.Empty).Trim();
            switch (normalizedValue)
            {
                case "cursor":
                    sourceType = ConnectedAgentSourceType.Cursor;
                    return true;
                case "claudeCode":
                case "claude-code":
                case "claude_code":
                    sourceType = ConnectedAgentSourceType.ClaudeCode;
                    return true;
                case "codex":
                    sourceType = ConnectedAgentSourceType.Codex;
                    return true;
                case "githubCopilot":
                case "github-copilot":
                case "github_copilot":
                    sourceType = ConnectedAgentSourceType.GitHubCopilot;
                    return true;
                case "geminiCli":
                case "gemini-cli":
                case "gemini_cli":
                    sourceType = ConnectedAgentSourceType.GeminiCli;
                    return true;
                case "manual":
                    sourceType = ConnectedAgentSourceType.OtherManualLogFolder;
                    return true;
            }

            switch (MacAgentSourceDetector.NormalizeProviderValue(providerValue).ToString())
            {
                case "Cursor":
                    sourceType = ConnectedAgentSourceType.Cursor;
                    return true;
                case "ClaudeCode":
                    sourceType = ConnectedAgentSourceType.ClaudeCode;
                    return true;
                case "Codex":
                    sourceType = ConnectedAgentSourceType.Codex;
                    return true;
                case "GitHubCopilot":
                    sourceType = ConnectedAgentSourceType.GitHubCopilot;
                    return true;
                case "GeminiCli":
                    sourceType = ConnectedAgentSourceType.GeminiCli;
                    return true;
                case "Manual":
                    sourceType = ConnectedAgentSourceType.OtherManualLogFolder;
                    return true;
                default:
                    sourceType = ConnectedAgentSourceType.Codex;
                    return false;
            }
        }

        private bool ProviderReadyForNative(string providerValue)
        {
            if (!TrySourceTypeForNative(providerValue, out var sourceType) || approvedActivityAnalysis == null)
            {
                return false;
            }

            var source = approvedActivityAnalysis.Onboarding.AgentSources.FirstOrDefault(item => item.SourceType == sourceType);
            return IsAgentReadyForNative(source) && NativeAgentFlowMatchesSource(source);
        }

        private bool NativeAgentFlowMatchesSource(ConnectedAgentSource source)
        {
            if (source == null || agentAnalysisFlow == null || approvedActivityAnalysis == null)
            {
                return false;
            }

            if (!agentAnalysisFlow.HasSelectedAgentLogLocationForLocalOnlyApproval)
            {
                return false;
            }

            var selectedProvider = MacAgentSourceDetector.NormalizeProvider(approvedActivityAnalysis.SelectedAgentProviderType);
            var sourceProvider = MacAgentSourceDetector.NormalizeProvider(ProviderTypeForNative(source.SourceType));
            return selectedProvider == sourceProvider;
        }

        private bool ActiveRepositoryReadyForNative()
        {
            var dashboard = approvedActivityAnalysis?.CharacterDashboard;
            var activeHash = dashboard?.CurrentRepositoryHash ?? string.Empty;
            if (string.IsNullOrWhiteSpace(activeHash) ||
                string.Equals(activeHash, RepositoryCompanionProfileService.DefaultLocalRepositoryHash, StringComparison.Ordinal))
            {
                return false;
            }

            return (approvedActivityAnalysis.RepositoryCompanions ?? new List<RepositoryCompanionDisplayItem>())
                .Any(item => item.Selected &&
                             !item.Archived &&
                             item.ApprovedByUser &&
                             string.Equals(item.RepositoryHash, activeHash, StringComparison.Ordinal));
        }

        private static GitAnalysisMode? NativeGitAnalysisModeFromScope(string scope)
        {
            var normalized = (scope ?? string.Empty).Trim().Replace("-", string.Empty).Replace("_", string.Empty);
            if (string.Equals(normalized, "full", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "fullhistory", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "fullbaseline", StringComparison.OrdinalIgnoreCase))
            {
                return GitAnalysisMode.FullBaseline;
            }

            if (string.Equals(normalized, "sincelast", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "incremental", StringComparison.OrdinalIgnoreCase))
            {
                return GitAnalysisMode.Incremental;
            }

            if (string.Equals(normalized, "recent", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "recentrange", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "recenttrend", StringComparison.OrdinalIgnoreCase))
            {
                return GitAnalysisMode.RecentTrend;
            }

            return null;
        }

        private void SetUnsupportedNativeAction(string message)
        {
            nativeActionStatusKind = "error";
            nativeActionStatusText = string.IsNullOrWhiteSpace(message) ? "Unsupported action." : message;
            Debug.LogWarning("WARN [NativeDashboard] unsupported action reason=" + nativeActionStatusText);
        }

        private async Task RunNativeAgentAnalysisAsync()
        {
            if (approvedActivityAnalysis == null)
            {
                return;
            }

            var ready = approvedActivityAnalysis.Onboarding.AgentSources
                .FirstOrDefault(source => IsAgentReadyForNative(source) && NativeAgentFlowMatchesSource(source));
            if (ready == null)
            {
                nativeActionStatusKind = "error";
                nativeActionStatusText = "Analysis failed safely: No approved source - Choose a readable AI agent folder before analysis.";
                await approvedActivityAnalysis.RecordNativeAnalysisRunAsync("agent", "failed", "No approved source", nativeActionStatusText);
                await RefreshAndPublishNativeDashboardAsync();
                return;
            }

            await RunNativeAnalysisAsync(string.Empty, ProviderTypeForNative(ready.SourceType).ToString());
        }

        private string ResolveActiveRepositoryHashForAnalysis(string explicitRepositoryHash)
        {
            if (!string.IsNullOrWhiteSpace(explicitRepositoryHash))
            {
                return explicitRepositoryHash;
            }

            var activeRepositoryHash = approvedActivityAnalysis?.CharacterDashboard?.CurrentRepositoryHash ?? string.Empty;
            if (string.IsNullOrWhiteSpace(activeRepositoryHash))
            {
                return explicitRepositoryHash;
            }

            var currentAnalysisPath = gitAnalysisFlow?.GetSelectedRepositoryPathForLocalOnlyApproval() ?? string.Empty;
            var currentAnalysisHash = string.IsNullOrWhiteSpace(currentAnalysisPath)
                ? string.Empty
                : RepositoryCompanionProfileService.HashRepositoryPath(currentAnalysisPath);
            var stale = !string.Equals(activeRepositoryHash, currentAnalysisHash, StringComparison.Ordinal);
            Debug.Log("INFO [RepositoryStateDiagnostic] action=resolveAnalysisRepository" +
                      " activeRepositoryStableId=" + SafeNativeText(activeRepositoryHash, "none") +
                      " analysisPathStableId=" + SafeNativeText(currentAnalysisHash, "none") +
                      " stale=" + stale);
            return stale ? activeRepositoryHash : explicitRepositoryHash;
        }

        private async Task RunNativeAnalysisAsync(string repositoryHash = "", string providerValue = "", bool repositoryOnly = false, GitAnalysisMode? requestedGitAnalysisMode = null)
        {
            if (approvedActivityAnalysis == null)
            {
                return;
            }

            Debug.Log("INFO [Analysis] run requested");
            if (nativeAnalysisInProgress)
            {
                nativeActionStatusKind = "warning";
                nativeActionStatusText = "Analysis is already running.";
                Debug.LogWarning("WARN [Analysis] blocked reason=alreadyRunning");
                Debug.Log("INFO [AnalysisJob] type=repository/provider status=failed id=already-running");
                ApplyNativeShellState(showDashboardIfNeeded: false);
                return;
            }

            await approvedActivityAnalysis.RefreshApprovedLocationsAsync();
            await approvedActivityAnalysis.RestoreLocalSelectionsFromApprovedLocationsAsync();

            // When no explicit repository was passed (Dashboard/Activity "Analyze"), make sure the
            // git analysis path follows the currently active repository. RestoreLocalSelections only
            // populates the path when none is selected, so a previously loaded repository would keep
            // being analyzed after switching the active repository — leaving each repository's growth
            // summary frozen at its last-saved values. Re-point to the active repository when stale.
            repositoryHash = ResolveActiveRepositoryHashForAnalysis(repositoryHash);

            if (!string.IsNullOrWhiteSpace(repositoryHash))
            {
                var select = await approvedActivityAnalysis.SelectRepositoryCompanionProfileAsync(repositoryHash);
                if (!select.IsSuccess)
                {
                    nativeActionStatusKind = "error";
                    nativeActionStatusText = "Repository analysis failed: " + SafeNativeText(select.ErrorMessage, select.ErrorCode);
                    await approvedActivityAnalysis.RecordNativeAnalysisRunAsync("repository", "failed", select.ErrorCode, nativeActionStatusText);
                    Debug.Log("INFO [AnalysisJob] type=repository status=failed id=not-started");
                    await RefreshAndPublishNativeDashboardAsync();
                    return;
                }
            }

            var preliminaryHasRepository = ActiveRepositoryReadyForNative();
            var preliminaryHasAgent = agentAnalysisFlow != null && agentAnalysisFlow.HasSelectedAgentLogLocationForLocalOnlyApproval;
            if (repositoryOnly && !preliminaryHasRepository)
            {
                nativeActionStatusKind = "error";
                nativeActionStatusText = "Analysis failed safely: NoActiveRepository - Connect a repository first.";
                await approvedActivityAnalysis.RecordNativeAnalysisRunAsync("repository", "failed", "NoActiveRepository", nativeActionStatusText);
                Debug.Log("INFO [AnalysisJob] type=repository status=failed id=not-started");
                await RefreshAndPublishNativeDashboardAsync();
                return;
            }

            if (!repositoryOnly && !string.IsNullOrWhiteSpace(providerValue) && !ProviderReadyForNative(providerValue))
            {
                nativeActionStatusKind = "error";
                nativeActionStatusText = "Analysis failed safely: agent_source_not_ready - Detect or choose a folder before analyzing.";
                await approvedActivityAnalysis.RecordNativeAnalysisRunAsync("agent", "failed", "agent_source_not_ready", nativeActionStatusText);
                Debug.Log("INFO [AnalysisJob] type=provider status=failed id=not-started");
                await RefreshAndPublishNativeDashboardAsync();
                return;
            }

            if (!repositoryOnly && string.IsNullOrWhiteSpace(providerValue) && !preliminaryHasRepository && !preliminaryHasAgent)
            {
                nativeActionStatusKind = "error";
                nativeActionStatusText = "Analysis failed safely: NoActiveRepository - Connect a repository first.";
                await approvedActivityAnalysis.RecordNativeAnalysisRunAsync("repository", "failed", "NoActiveRepository", nativeActionStatusText);
                Debug.Log("INFO [AnalysisJob] type=repository status=failed id=not-started");
                await RefreshAndPublishNativeDashboardAsync();
                return;
            }

            nativeSelectedNavItem = "activity";
            nativeAnalysisInProgress = true;
            nativeActionStatusKind = "running";
            nativeCurrentAnalysisJobId = Guid.NewGuid().ToString("N");
            nativeCurrentAnalysisType = string.IsNullOrWhiteSpace(providerValue) ? "repository" : "agent";
            nativeCurrentAnalysisSourceName = string.IsNullOrWhiteSpace(providerValue)
                ? SafeNativeText(approvedActivityAnalysis.CharacterDashboard?.CurrentRepositoryAlias, "Repository")
                : SafeNativeText(providerValue, "AI agent");
            nativeCurrentAnalysisStartedAt = DateTimeOffset.UtcNow.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss");
            nativeCurrentAnalysisStep = "validating repository";
            nativeActionStatusText = "Analyzing...";
            Debug.Log("INFO [AnalysisJob] type=" + nativeCurrentAnalysisType + " status=started id=" + nativeCurrentAnalysisJobId);
            ApplyNativeShellState(showDashboardIfNeeded: false);

            var anySuccess = false;
            var lastError = string.Empty;
            var lastErrorMessage = string.Empty;
            try
            {
                await approvedActivityAnalysis.RefreshApprovedLocationsAsync();
                await approvedActivityAnalysis.RestoreLocalSelectionsFromApprovedLocationsAsync();

                if (!string.IsNullOrWhiteSpace(repositoryHash))
                {
                    var select = await approvedActivityAnalysis.SelectRepositoryCompanionProfileAsync(repositoryHash);
                    if (!select.IsSuccess)
                    {
                        lastError = select.ErrorCode;
                        lastErrorMessage = select.ErrorMessage;
                        if (repositoryOnly)
                        {
                            nativeActionStatusKind = "error";
                            nativeActionStatusText = "Repository analysis failed: " + SafeNativeText(lastErrorMessage, lastError);
                            await approvedActivityAnalysis.RecordNativeAnalysisRunAsync("repository", "failed", lastError, nativeActionStatusText);
                            return;
                        }
                    }
                }

                var gitSucceeded = false;
                var agentSucceeded = false;
                var hasRepository = ActiveRepositoryReadyForNative();
                var hasAgent = agentAnalysisFlow != null && agentAnalysisFlow.HasSelectedAgentLogLocationForLocalOnlyApproval;
                if (repositoryOnly && !hasRepository)
                {
                    nativeActionStatusKind = "error";
                    nativeActionStatusText = "Analysis failed safely: NoActiveRepository - Connect a repository first.";
                    Debug.Log("INFO [Analysis] blocked reason=noRepository");
                    await approvedActivityAnalysis.RecordNativeAnalysisRunAsync("repository", "failed", "NoActiveRepository", nativeActionStatusText);
                    return;
                }

                if (!hasRepository && string.IsNullOrWhiteSpace(providerValue) && !hasAgent)
                {
                    nativeActionStatusKind = "error";
                    nativeActionStatusText = "Analysis failed safely: NoActiveRepository - Connect a repository first.";
                    Debug.Log("INFO [Analysis] blocked reason=noRepository");
                    await approvedActivityAnalysis.RecordNativeAnalysisRunAsync("repository", "failed", "NoActiveRepository", nativeActionStatusText);
                    return;
                }

                if (ActiveRepositoryReadyForNative() && string.IsNullOrWhiteSpace(providerValue))
                {
                    nativeActionStatusText = "Analyzing repository.";
                    nativeCurrentAnalysisStep = "reading git stats";
                    ApplyNativeShellState(showDashboardIfNeeded: false);
                    Debug.Log("INFO [Analysis] begin repositoryId=" + SafeNativeText(approvedActivityAnalysis.CharacterDashboard?.CurrentRepositoryHash, "unknown"));
                    Debug.Log("INFO [Analysis] git begin");
                    var gitResult = await approvedActivityAnalysis.AnalyzeGitActivityAsync(requestedGitAnalysisMode);
                    if (gitResult.IsSuccess)
                    {
                        anySuccess = true;
                        gitSucceeded = true;
                        nativeCurrentAnalysisStep = "generating review";
                        Debug.Log("INFO [Analysis] git end");
                    }
                    else
                    {
                        lastError = gitResult.ErrorCode;
                        lastErrorMessage = gitResult.ErrorMessage;
                        Debug.LogWarning("WARN [Analysis] failed reason=" + lastError);
                    }
                }

                if (repositoryOnly)
                {
                    // Repository-only runs intentionally skip agent analysis, even if an agent source is ready.
                }
                else if (!string.IsNullOrWhiteSpace(providerValue))
                {
                    if (!TrySourceTypeForNative(providerValue, out var sourceType))
                    {
                        lastError = "unsupported_provider_action";
                        lastErrorMessage = "Unsupported AI provider action.";
                        SetUnsupportedNativeAction("Unsupported AI provider action.");
                    }
                    else
                    {
                        nativeActionStatusText = "Analyzing agent logs.";
                        nativeCurrentAnalysisStep = "aggregating activity";
                        ApplyNativeShellState(showDashboardIfNeeded: false);
                        Debug.Log("INFO [Analysis] agent begin provider=" + sourceType);
                        var agentResult = await approvedActivityAnalysis.AnalyzeAgentSourceForOnboardingAsync(sourceType);
                        if (agentResult.IsSuccess)
                        {
                            anySuccess = true;
                            agentSucceeded = true;
                            nativeCurrentAnalysisStep = "generating review";
                            Debug.Log("INFO [Analysis] agent end provider=" + sourceType);
                        }
                        else
                        {
                            lastError = agentResult.ErrorCode;
                            lastErrorMessage = agentResult.ErrorMessage;
                            Debug.LogWarning("WARN [Analysis] failed reason=" + lastError);
                        }
                    }
                }
                else if (agentAnalysisFlow != null && agentAnalysisFlow.HasSelectedAgentLogLocationForLocalOnlyApproval)
                {
                    nativeActionStatusText = "Analyzing agent logs.";
                    nativeCurrentAnalysisStep = "aggregating activity";
                    ApplyNativeShellState(showDashboardIfNeeded: false);
                    Debug.Log("INFO [Analysis] agent begin provider=" + approvedActivityAnalysis.SelectedAgentProviderType);
                    var agentResult = await approvedActivityAnalysis.AnalyzeAgentActivityAsync();
                    if (agentResult.IsSuccess)
                    {
                        anySuccess = true;
                        agentSucceeded = true;
                        nativeCurrentAnalysisStep = "generating review";
                        Debug.Log("INFO [Analysis] agent end provider=" + approvedActivityAnalysis.SelectedAgentProviderType);
                    }
                    else
                    {
                        lastError = agentResult.ErrorCode;
                        lastErrorMessage = agentResult.ErrorMessage;
                        Debug.LogWarning("WARN [Analysis] failed reason=" + lastError);
                    }
                }

                if (gitSucceeded && agentSucceeded)
                {
                    var combined = await approvedActivityAnalysis.CombinePendingNativeReviewsFromFlowsAsync();
                    if (!combined.IsSuccess)
                    {
                        lastError = combined.ErrorCode;
                        lastErrorMessage = combined.ErrorMessage;
                        Debug.LogWarning("WARN [NativeDashboard] combined review failed category=" + combined.ErrorCode);
                    }
                }

                if (!anySuccess)
                {
                    var safeCategory = NativeSafeErrorCategory(lastError, string.IsNullOrWhiteSpace(providerValue) ? "repository" : "agent");
                    var safeMessage = NativeSafeRecoveryMessage(safeCategory, lastErrorMessage);
                    nativeActionStatusKind = "error";
                    nativeActionStatusText = string.Equals(lastError, "unsupported_provider_action", StringComparison.Ordinal)
                        ? "Unsupported AI provider action."
                        : string.IsNullOrWhiteSpace(lastError)
                        ? "Connect a repository or AI agent provider before running analysis."
                        : "Analysis failed safely: " + safeCategory + " - " + safeMessage;
                    await approvedActivityAnalysis.RecordNativeAnalysisRunAsync(
                        string.IsNullOrWhiteSpace(providerValue) ? "repository" : "agent",
                        "failed",
                        string.IsNullOrWhiteSpace(lastError) ? "NoActiveRepository" : safeCategory,
                        nativeActionStatusText);
                    Debug.Log("INFO [NativeDashboard] runAnalysis blocked category=" + (string.IsNullOrWhiteSpace(lastError) ? "missing_activity_source" : lastError));
                    Debug.Log("INFO [AnalysisJob] type=" + nativeCurrentAnalysisType + " status=failed id=" + nativeCurrentAnalysisJobId);
                }
                else
                {
                    await RefreshDashboardAsync(loadAuthSessionOnStart);
                    if (approvedActivityAnalysis.PendingNativeActivityReview == null)
                    {
                        nativeActionStatusKind = "error";
                        var missingReviewReason = agentSucceeded
                            ? "Provider detector returned no evidence - No pending AI agent review was created."
                            : "Analysis service exception - No pending review was created.";
                        nativeActionStatusText = "Analysis failed safely: " + missingReviewReason;
                        await approvedActivityAnalysis.RecordNativeAnalysisRunAsync(
                            gitSucceeded && agentSucceeded ? "combined" : gitSucceeded ? "repository" : "agent",
                            "failed",
                            agentSucceeded ? "Provider detector returned no evidence" : "Analysis service exception",
                            nativeActionStatusText);
                        Debug.LogWarning("WARN [Analysis] pendingReview missing after successful flow");
                        Debug.Log("INFO [AnalysisJob] type=" + nativeCurrentAnalysisType + " status=failed id=" + nativeCurrentAnalysisJobId);
                    }
                    else
                    {
                        nativeActionStatusKind = "success";
                        nativeActionStatusText = "Analysis complete. Pending review is ready in Activity. XP is unchanged until Save Growth.";
                        nativeCurrentAnalysisStep = "completed";
                        await approvedActivityAnalysis.RecordNativeAnalysisRunAsync(
                            gitSucceeded && agentSucceeded ? "combined" : gitSucceeded ? "repository" : "agent",
                            "pending_review",
                            string.Empty,
                            nativeActionStatusText);
                        var pendingId = approvedActivityAnalysis.PendingNativeActivityReview.ReviewId ?? "pending";
                        Debug.Log("INFO [Analysis] pendingReview created id=" + pendingId);
                        Debug.Log("INFO [AnalysisJob] type=" + nativeCurrentAnalysisType + " status=completed id=" + nativeCurrentAnalysisJobId);
                    }
                }
            }
            catch (Exception exception)
            {
                nativeActionStatusKind = "error";
                nativeActionStatusText = "Analysis failed safely: " + exception.GetType().Name;
                await approvedActivityAnalysis.RecordNativeAnalysisRunAsync("activity", "failed", exception.GetType().Name, nativeActionStatusText);
                Debug.LogError("ERROR [Analysis] failed reason=" + exception.Message);
                Debug.Log("INFO [AnalysisJob] type=" + nativeCurrentAnalysisType + " status=failed id=" + nativeCurrentAnalysisJobId);
            }
            finally
            {
                nativeAnalysisInProgress = false;
                nativeCurrentAnalysisJobId = string.Empty;
                nativeCurrentAnalysisType = string.Empty;
                nativeCurrentAnalysisSourceName = string.Empty;
                nativeCurrentAnalysisStartedAt = string.Empty;
                nativeCurrentAnalysisStep = string.Empty;
                await RefreshAndPublishNativeDashboardAsync();
            }
        }

        private async Task ApproveNativeReviewAsync()
        {
            if (approvedActivityAnalysis == null)
            {
                return;
            }

            var pendingXp = Math.Max(0, approvedActivityAnalysis.PendingNativeActivityReview?.EstimatedXpDelta ?? 0);
            var result = await approvedActivityAnalysis.ApprovePendingNativeReviewAsync();
            if (!result.IsSuccess)
            {
                Debug.LogWarning("WARN [NativeDashboard] approveReview failed category=" + result.ErrorCode);
                nativeActionStatusKind = "error";
                nativeActionStatusText = "Save Growth failed: " + result.ErrorCode;
                Debug.Log("INFO [ReviewState] pending=1 saved=0 appliedReviewId= result=failed");
            }
            else
            {
                nativeReviewDetailVisible = false;
                nativeSelectedReviewId = string.Empty;
                nativeActionStatusKind = "success";
                nativeActionStatusText = pendingXp > 0
                    ? "Growth saved. +" + pendingXp + " XP applied."
                    : "Growth saved. No pending review remains.";
                await approvedActivityAnalysis.RecordNativeAnalysisRunAsync("reviewSaved", "saved", string.Empty, nativeActionStatusText);
                nativeDesktopCompanionController?.TriggerReaction(CompanionReaction.GrowthSaved, "Growth saved.");
                Debug.Log("INFO [ReviewState] pending=0 saved=1 appliedReviewId=saved result=success");
            }

            await RefreshAndPublishNativeDashboardAsync();
        }

        private async Task DiscardNativeReviewAsync()
        {
            if (approvedActivityAnalysis == null)
            {
                return;
            }

            var result = await approvedActivityAnalysis.DiscardPendingNativeReviewAsync();
            if (!result.IsSuccess)
            {
                Debug.LogWarning("WARN [NativeDashboard] discardReview failed category=" + result.ErrorCode);
                nativeActionStatusKind = "error";
                nativeActionStatusText = "Discard failed: " + result.ErrorCode;
            }
            else
            {
                nativeReviewDetailVisible = false;
                nativeSelectedReviewId = string.Empty;
                nativeActionStatusKind = "success";
                nativeActionStatusText = "Pending review discarded. Existing XP and stats are unchanged.";
            }

            await RefreshAndPublishNativeDashboardAsync();
        }

        private async Task LevelUpNativeCompanionAsync(string repositoryHash = "")
        {
            if (approvedActivityAnalysis == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(repositoryHash))
            {
                var select = await approvedActivityAnalysis.SelectRepositoryCompanionProfileAsync(repositoryHash);
                if (!select.IsSuccess)
                {
                    nativeActionStatusKind = "warning";
                    nativeActionStatusText = "Level Up unavailable: " + SafeNativeText(select.ErrorMessage, select.ErrorCode);
                    Debug.Log("INFO [Action] evolveToken clicked repo=" + SafeNativeText(repositoryHash, "active") + " result=selectFailed");
                    await RefreshAndPublishNativeDashboardAsync();
                    return;
                }
            }

            Debug.Log("INFO [Action] evolveToken clicked repo=" + SafeNativeText(repositoryHash, approvedActivityAnalysis.CharacterDashboard?.CurrentRepositoryHash ?? "active"));
            var result = await approvedActivityAnalysis.LevelUpSelectedCompanionAsync();
            if (result.IsSuccess)
            {
                nativeActionStatusKind = "success";
                nativeActionStatusText = "Level Up saved. Extra XP carried into the next level.";
                nativeDesktopCompanionController?.TriggerReaction(CompanionReaction.LevelUp, "Evolution saved.");
                Debug.Log("INFO [CompanionLevel] levelUp result=success");
            }
            else
            {
                nativeActionStatusKind = "warning";
                nativeActionStatusText = "Level Up unavailable: " + SafeNativeText(result.ErrorMessage, result.ErrorCode);
                Debug.Log("INFO [CompanionLevel] levelUp result=blocked reason=" + result.ErrorCode);
            }

            await RefreshAndPublishNativeDashboardAsync();
        }

        private async Task PurchaseTokenShopItemFromNativeAsync(string itemId)
        {
            if (approvedActivityAnalysis == null)
            {
                return;
            }

            Debug.Log("INFO [TokenShop][ACTION] purchase itemId=" + SafeNativeText(itemId, "none"));
            var targetType = string.Equals(nativeShopTargetType, "aiAgent", StringComparison.Ordinal) ? ShopTargetType.AiAgent : ShopTargetType.RepositoryCompanion;
            var targetId = targetType == ShopTargetType.AiAgent ? RepositoryCompanionProfileService.NormalizeAgentShopId(nativeShopSelectedAgentId) : string.Empty;
            var result = await approvedActivityAnalysis.PurchaseTokenShopItemAsync(itemId, targetType, targetId, IsNativeShopAgentConnected(targetId));
            if (result.IsSuccess)
            {
                nativeActionStatusKind = "shop";
                nativeActionStatusText = SafeNativeText(result.Value?.StatusText, "Purchase saved.") +
                                         " Balance: " + Math.Max(0, result.Value?.BalanceAfter ?? 0) + " " +
                                         SafeNativeText(result.Value?.CurrencyName, "Forge Coins") + ".";
                Debug.Log("INFO [TokenShop][PURCHASE] itemId=" + SafeNativeText(itemId, "none") + " result=success balance=" + Math.Max(0, result.Value?.BalanceAfter ?? 0));
            }
            else
            {
                nativeActionStatusKind = "shop";
                nativeActionStatusText = "Purchase unavailable: " + SafeNativeText(result.ErrorMessage, result.ErrorCode);
                Debug.LogWarning("WARN [TokenShop][PURCHASE] itemId=" + SafeNativeText(itemId, "none") + " result=blocked reason=" + result.ErrorCode);
            }

            await RefreshAndPublishNativeDashboardAsync();
        }

        private async Task EquipTokenShopItemFromNativeAsync(string itemId)
        {
            if (approvedActivityAnalysis == null)
            {
                return;
            }

            var targetType = string.Equals(nativeShopTargetType, "aiAgent", StringComparison.Ordinal) ? ShopTargetType.AiAgent : ShopTargetType.RepositoryCompanion;
            var targetId = targetType == ShopTargetType.AiAgent ? RepositoryCompanionProfileService.NormalizeAgentShopId(nativeShopSelectedAgentId) : string.Empty;
            var result = await approvedActivityAnalysis.EquipTokenShopItemAsync(itemId, targetType, targetId, IsNativeShopAgentConnected(targetId));
            nativeActionStatusKind = "shop";
            if (result.IsSuccess)
            {
                nativeActionStatusText = SafeNativeText(result.Value?.StatusText, "Item equipped.");
                Debug.Log("INFO [TokenShop][EQUIP] itemId=" + SafeNativeText(itemId, "none") + " result=success");
            }
            else
            {
                nativeActionStatusText = "Equip unavailable: " + SafeNativeText(result.ErrorMessage, result.ErrorCode);
                Debug.LogWarning("WARN [TokenShop][EQUIP] itemId=" + SafeNativeText(itemId, "none") + " result=blocked reason=" + result.ErrorCode);
            }

            await RefreshAndPublishNativeDashboardAsync();
        }

        private bool IsNativeShopAgentConnected(string agentId)
        {
            agentId = RepositoryCompanionProfileService.NormalizeAgentShopId(agentId);
            if (approvedActivityAnalysis == null || string.IsNullOrWhiteSpace(agentId))
            {
                return false;
            }

            return approvedActivityAnalysis.Onboarding.AgentSources.Any(source =>
            {
                var providerId = NativeProviderId(ProviderTypeForNative(source.SourceType));
                return string.Equals(providerId, agentId, StringComparison.Ordinal) &&
                       IsAgentReadyForNative(source) &&
                       NativeAgentFlowMatchesSource(source);
            });
        }

        private async Task ReviewNativeActivityAsync()
        {
            if (approvedActivityAnalysis == null)
            {
                return;
            }

            if (gitAnalysisFlow != null && gitAnalysisFlow.HasPendingReview)
            {
                Debug.Log("INFO [NativeDashboard] reviewActivity pending=git safeAggregateReady=true");
            }
            else if (agentAnalysisFlow != null && agentAnalysisFlow.HasPendingReview)
            {
                Debug.Log("INFO [NativeDashboard] reviewActivity pending=agent safeAggregateReady=true");
            }
            else
            {
                Debug.Log("INFO [NativeDashboard] reviewActivity pending=false");
            }

            await RefreshAndPublishNativeDashboardAsync();
        }

        private async Task ViewNativeReviewDetailsAsync(string reviewId)
        {
            if (approvedActivityAnalysis == null)
            {
                return;
            }

            await approvedActivityAnalysis.RefreshRecentSessionsAsync();
            var pending = approvedActivityAnalysis.PendingNativeActivityReview;
            if (pending == null)
            {
                nativeReviewDetailVisible = false;
                nativeSelectedReviewId = string.Empty;
                nativeActionStatusKind = "warning";
                nativeActionStatusText = "No pending review is available.";
                Debug.Log("INFO [DashboardAction] action=review.viewDetails target=" + reviewId + " enabled=false result=disabled reason=noPendingReview");
                await RefreshAndPublishNativeDashboardAsync();
                return;
            }

            var selectedId = string.IsNullOrWhiteSpace(reviewId) ? pending.ReviewId : reviewId;
            if (!string.IsNullOrWhiteSpace(selectedId) &&
                !string.Equals(selectedId, pending.ReviewId, StringComparison.Ordinal))
            {
                nativeActionStatusKind = "error";
                nativeActionStatusText = "Review details are unavailable for that review.";
                Debug.LogWarning("WARN [DashboardAction] action=review.viewDetails target=" + selectedId + " result=failed reason=reviewNotFound");
                await RefreshAndPublishNativeDashboardAsync();
                return;
            }

            nativeSelectedReviewId = string.IsNullOrWhiteSpace(pending.ReviewId) ? pending.SafeSession?.SessionId ?? "pending-review" : pending.ReviewId;
            nativeReviewDetailVisible = true;
            nativeActionStatusKind = "success";
            nativeActionStatusText = "Review details opened.";
            Debug.Log("INFO [DashboardAction] action=review.viewDetails target=" + nativeSelectedReviewId + " enabled=true result=detailOpened");
            await RefreshAndPublishNativeDashboardAsync();
        }

        private async Task SetCompanionVisibleFromNativeAsync(bool visible, string traceId = "none", string source = "explicitDashboardAction")
        {
            Debug.Log("INFO [OverlayTrace:" + SafeNativeText(traceId, "none") + "] AppBootstrapper route invoked target=DesktopCompanionOverlayController.SetVisible visible=" + visible);
            Debug.Log("INFO [Overlay][Action] " + (visible ? "show" : "hide") + " source=" + SafeNativeText(source, "explicitDashboardAction") + " traceId=" + SafeNativeText(traceId, "none"));
            var oldDesired = nativeCompanionDesiredVisibleInitialized && nativeCompanionDesiredVisible;
            var oldActual = NativeOverlayActuallyVisible();
            Debug.Log("INFO [OverlayState][BEFORE_ACTION] desiredVisible=" + oldDesired + " actualVisible=" + oldActual + " source=" + SafeNativeText(source, "explicitDashboardAction"));
            nativeCompanionDesiredVisible = visible;
            nativeCompanionDesiredVisibleInitialized = true;
            nativeCompanionLastProjectionSource = SafeNativeText(source, visible ? "button_show" : "button_hide");
            Debug.Log("INFO [OverlayTrace:" + SafeNativeText(traceId, "none") + "] desired_visible_changed old=" + oldDesired + " new=" + visible + " source=button");
            Debug.Log("INFO [OverlayProjection][REQUEST] desiredVisible=" + visible + " source=" + nativeCompanionLastProjectionSource);
            var showWithoutRepoWarning = visible && !ActiveRepositoryReadyForNative();
            if (showWithoutRepoWarning)
            {
                nativeCompanionDesiredVisible = false;
                nativeCompanionDesiredVisibleInitialized = true;
                nativeActionStatusKind = "warning";
                nativeActionStatusText = "Connect a repository to enable desktop companion.";
                var approvedRepoCount = (approvedActivityAnalysis?.RepositoryCompanions ?? new List<RepositoryCompanionDisplayItem>())
                    .Count(item => item != null &&
                                   !item.Archived &&
                                   item.ApprovedByUser &&
                                   !string.IsNullOrWhiteSpace(item.RepositoryHash) &&
                                   !string.Equals(item.RepositoryHash, RepositoryCompanionProfileService.DefaultLocalRepositoryHash, StringComparison.Ordinal));
                var selectedRepoHash = approvedActivityAnalysis?.CharacterDashboard?.CurrentRepositoryHash ?? string.Empty;
                var actualVisible = NativeOverlayActuallyVisible();
                var panelFrame = approvedActivityAnalysis?.CharacterDashboard?.DesktopCompanionSettings?.HasSavedOverlayPosition == true
                    ? approvedActivityAnalysis.CharacterDashboard.DesktopCompanionSettings.LastOverlayPositionX.ToString("0.#") + "," + approvedActivityAnalysis.CharacterDashboard.DesktopCompanionSettings.LastOverlayPositionY.ToString("0.#")
                    : "none";
                Debug.LogWarning("WARN [OverlayTrace:" + SafeNativeText(traceId, "none") + "] show_requested repoId=none source=button result=blocked_no_repository");
                Debug.Log("INFO [Overlay][Guard] repoHash=none desiredVisible=" + visible +
                          " actualVisible=" + actualVisible +
                          " panelExists=" + (nativeDesktopCompanionController != null) +
                          " panelFrame=" + SafeNativeText(panelFrame, "none") +
                          " reason=noApprovedRepository sourceAction=" + nativeCompanionLastProjectionSource +
                          " selectedRepoId=" + SafeNativeText(selectedRepoHash, "none") +
                          " selectedRepoHash=" + SafeNativeText(selectedRepoHash, "none") +
                          " approvedRepoCount=" + approvedRepoCount);
                Debug.Log("INFO [OverlayLifecycle][NO_REPOSITORY_HIDE_OVERLAY] repoHash=none desiredVisible=false actualVisible=false panelExists=" + (nativeDesktopCompanionController != null) +
                          " panelFrame=" + SafeNativeText(panelFrame, "none") +
                          " reason=noApprovedRepository sourceAction=" + nativeCompanionLastProjectionSource +
                          " selectedRepoId=" + SafeNativeText(selectedRepoHash, "none") +
                          " selectedRepoHash=" + SafeNativeText(selectedRepoHash, "none") +
                          " approvedRepoCount=" + approvedRepoCount);
                Debug.Log("INFO [OverlayVisibilityDiagnostic] reason=noApprovedRepository" +
                          " approvedRepoCount=" + approvedRepoCount +
                          " selectedRepoHash=" + SafeNativeText(selectedRepoHash, "none") +
                          " desiredVisible=" + visible +
                          " actualVisible=" + actualVisible +
                          " panelExists=" + (nativeDesktopCompanionController != null) +
                          " farmPanelCount=0" +
                          " forcedHiddenByNoRepo=true" +
                          " canonicalRepoHash=none panelFrame=" + SafeNativeText(panelFrame, "none"));
                nativeDesktopCompanionController?.HideLegacyOverlay(nativeCompanionLastProjectionSource);
                nativeDashboardService?.SetCompanionVisible(false, nativeCompanionLastProjectionSource);
                await RefreshAndPublishNativeDashboardAsync();
                return;
            }

            if (approvedActivityAnalysis != null)
            {
                var result = await approvedActivityAnalysis.SetDesktopCompanionEnabledAsync(visible);
                if (!result.IsSuccess)
                {
                    nativeActionStatusKind = "error";
                    nativeActionStatusText = "Companion visible save failed: " + SafeNativeText(result.ErrorMessage, result.ErrorCode);
                    await RefreshAndPublishNativeDashboardAsync();
                    return;
                }
            }

            if (!showWithoutRepoWarning)
            {
                nativeActionStatusKind = "success";
                nativeActionStatusText = visible ? "Desktop companion shown." : "Desktop companion hidden.";
            }

            Debug.Log("INFO [OverlayTrace:" + SafeNativeText(traceId, "none") + "] MacNativeDashboardService SetCompanionVisible visible=" + visible + " source=" + nativeCompanionLastProjectionSource);
            nativeDashboardService?.SetCompanionVisible(visible, nativeCompanionLastProjectionSource);
            ApplyNativeShellState(showDashboardIfNeeded: false);
            var actualAfterNativeCall = NativeOverlayActuallyVisible();
            Debug.Log("INFO [OverlayState][AFTER_ACTION] desiredVisible=" + nativeCompanionDesiredVisible + " actualVisible=" + actualAfterNativeCall + " source=" + nativeCompanionLastProjectionSource);
            Debug.Log("INFO [Overlay][Actual] panelExists=" + (nativeDesktopCompanionController != null) +
                      " visible=" + actualAfterNativeCall +
                      " frame=" + SafeNativeText(approvedActivityAnalysis?.CharacterDashboard?.DesktopCompanionSettings?.HasSavedOverlayPosition == true
                          ? approvedActivityAnalysis.CharacterDashboard.DesktopCompanionSettings.LastOverlayPositionX.ToString("0.#") + "," + approvedActivityAnalysis.CharacterDashboard.DesktopCompanionSettings.LastOverlayPositionY.ToString("0.#")
                          : "unknown", "unknown"));
            await RefreshAndPublishNativeDashboardAsync();
        }

        private bool NativeOverlayActuallyVisible()
        {
            return nativeDesktopCompanionController != null && nativeDesktopCompanionController.IsAnyOverlayActuallyVisible();
        }

        private async Task SetWanderEnabledFromNativeAsync(bool enabled, string traceId = "none")
        {
            Debug.Log("INFO [OverlayTrace:" + SafeNativeText(traceId, "none") + "] AppBootstrapper route invoked target=DesktopCompanionOverlayController.SetWander enabled=" + enabled);
            var oldVisible = nativeCompanionDesiredVisibleInitialized && nativeCompanionDesiredVisible;
            if (approvedActivityAnalysis != null)
            {
                var result = await approvedActivityAnalysis.SetDesktopCompanionMotionModeAsync(enabled ? CompanionDesktopMotionMode.Normal : CompanionDesktopMotionMode.Calm);
                if (!result.IsSuccess)
                {
                    nativeActionStatusKind = "error";
                    nativeActionStatusText = "Wander movement save failed: " + SafeNativeText(result.ErrorMessage, result.ErrorCode);
                    await RefreshAndPublishNativeDashboardAsync();
                    return;
                }
            }

            nativeActionStatusKind = "success";
            nativeActionStatusText = enabled
                ? (oldVisible ? "Movement enabled." : "Movement enabled. Use Show on Desktop when you want Token visible.")
                : "Movement paused.";
            Debug.Log("INFO [Overlay][Action] movement enabled=" + enabled);
            Debug.Log("INFO [DashboardAction] action=desktop.movement." + (enabled ? "enable" : "pause") +
                      " repositoryId=" + SafeNativeText(approvedActivityAnalysis?.CharacterDashboard?.CurrentRepositoryHash, "none") +
                      " previousVisible=" + oldVisible +
                      " nextVisible=" + (nativeCompanionDesiredVisibleInitialized && nativeCompanionDesiredVisible) +
                      " movementEnabled=" + enabled +
                      " nativeResult=settingsSaved");
            await RefreshAndPublishNativeDashboardAsync();
        }

        private Task SetDragEnabledFromNativeAsync(bool enabled)
        {
            return SetClickThroughEnabledFromNativeAsync(!enabled);
        }

        private async Task SetClickReactionEnabledFromNativeAsync(bool enabled)
        {
            var previous = approvedActivityAnalysis?.CharacterDashboard?.DesktopCompanionSettings?.IsClickThroughEnabled ?? false;
            if (approvedActivityAnalysis != null)
            {
                var result = await approvedActivityAnalysis.SetDesktopCompanionClickThroughAsync(!enabled);
                if (!result.IsSuccess)
                {
                    nativeActionStatusKind = "error";
                    nativeActionStatusText = "Click reaction save failed: " + SafeNativeText(result.ErrorMessage, result.ErrorCode);
                    await RefreshAndPublishNativeDashboardAsync();
                    return;
                }
            }

            nativeActionStatusKind = "success";
            nativeActionStatusText = enabled
                ? "Drag enabled. Click-through disabled while repositioning."
                : "Click-through enabled. Drag is disabled.";
            Debug.Log("INFO [DashboardAction] action=" + (enabled ? "desktop.drag.enable" : "desktop.clickThrough.enable") +
                      " previousClickThrough=" + previous +
                      " nextClickThrough=" + (!enabled) +
                      " dragEnabled=" + enabled +
                      " nativeResult=settingsSaved");
            await RefreshAndPublishNativeDashboardAsync();
        }

        private async Task SetClickThroughEnabledFromNativeAsync(bool enabled)
        {
            var previous = approvedActivityAnalysis?.CharacterDashboard?.DesktopCompanionSettings?.IsClickThroughEnabled ?? false;
            if (approvedActivityAnalysis != null)
            {
                var result = await approvedActivityAnalysis.SetDesktopCompanionClickThroughAsync(enabled);
                if (!result.IsSuccess)
                {
                    nativeActionStatusKind = "error";
                    nativeActionStatusText = "Click-through save failed: " + SafeNativeText(result.ErrorMessage, result.ErrorCode);
                    await RefreshAndPublishNativeDashboardAsync();
                    return;
                }
            }

            nativeActionStatusKind = "success";
            nativeActionStatusText = enabled
                ? "Click-through enabled. Drag is disabled."
                : "Drag enabled. Click-through disabled while repositioning.";
            Debug.Log("INFO [Overlay][Action] clickThrough enabled=" + enabled);
            Debug.Log("INFO [DashboardAction] action=" + (enabled ? "desktop.clickThrough.enable" : "desktop.drag.enable") +
                      " repositoryId=" + SafeNativeText(approvedActivityAnalysis?.CharacterDashboard?.CurrentRepositoryHash, "none") +
                      " previousClickThrough=" + previous +
                      " nextClickThrough=" + enabled +
                      " dragEnabled=" + (!enabled) +
                      " nativeResult=settingsSaved");
            await RefreshAndPublishNativeDashboardAsync();
        }

        private async Task SetCompanionSkinFromNativeAsync(string skin)
        {
            if (approvedActivityAnalysis != null)
            {
                await approvedActivityAnalysis.SetDesktopCompanionVisualThemeAsync(skin);
            }

            await RefreshAndPublishNativeDashboardAsync();
        }

        private async Task SetRepositoryZodiacMascotFromNativeAsync(string zodiacTypeId)
        {
            zodiacTypeId = RepositoryCompanionProfileService.NormalizeZodiacTypeId(zodiacTypeId, "repository");
            if (approvedActivityAnalysis != null)
            {
                var result = await approvedActivityAnalysis.SetRepositoryZodiacMascotAsync(zodiacTypeId);
                nativeActionStatusKind = result.IsSuccess ? "success" : "warning";
                nativeActionStatusText = result.IsSuccess
                    ? RepositoryCompanionProfileService.ZodiacDisplayName(zodiacTypeId) + " equipped for the repository mascot."
                    : "Zodiac mascot unavailable: " + SafeNativeText(result.ErrorMessage, result.ErrorCode);
            }

            await RefreshAndPublishNativeDashboardAsync();
        }

        private async Task ResetCompanionPositionFromNativeAsync()
        {
            if (approvedActivityAnalysis != null)
            {
                await approvedActivityAnalysis.ResetDesktopCompanionPositionAsync();
            }

            nativeDesktopCompanionController?.ResetPosition();
            await RefreshAndPublishNativeDashboardAsync();
        }

        private static string SafeNativeText(string value, string fallback)
        {
            value = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            value = value.Replace('\n', ' ').Replace('\r', ' ');
            return value.Length <= 180 ? value : value.Substring(0, 180);
        }

        private static string FriendlyNativeSummary(string value, string fallback)
        {
            value = SafeNativeText(value, fallback);
            if (!LooksLikeSampleRepositoryActivity(value) && value.IndexOf(" | ", StringComparison.Ordinal) < 0)
            {
                return value;
            }

            var agent = value.IndexOf("Codex", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        value.IndexOf("Claude", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        value.IndexOf("Cursor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        value.IndexOf("Copilot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        value.IndexOf("AI", StringComparison.OrdinalIgnoreCase) >= 0;
            var hasXp = value.IndexOf("+0 XP", StringComparison.OrdinalIgnoreCase) < 0 &&
                        value.IndexOf("XP", StringComparison.OrdinalIgnoreCase) >= 0;
            var hasWarnings = value.IndexOf("warnings 0", StringComparison.OrdinalIgnoreCase) < 0 &&
                              value.IndexOf("warning", StringComparison.OrdinalIgnoreCase) >= 0;

            return (agent
                    ? "AI-assisted activity was detected and summarized safely. "
                    : "Recent Git changes were analyzed and summarized safely. ") +
                   (hasXp
                       ? "Growth XP was saved for this repository companion. "
                       : "No additional XP was applied in this summary. ") +
                   (hasWarnings
                       ? "Some items need attention in Activity details."
                       : "No sync issues detected.");
        }

        private static string NativeSafeErrorCategory(string errorCode, string sourceKind)
        {
            switch ((errorCode ?? string.Empty).Trim())
            {
                case "NoActiveRepository":
                case "RepositoryPathMissing":
                case "RepositoryFolderNotFound":
                case "NotAGitRepository":
                case "GitExecutableNotFound":
                case "PermissionDenied":
                case "ProcessTimeout":
                case "GitCommandFailed":
                case "Unknown":
                    return string.Equals(sourceKind, "agent", StringComparison.OrdinalIgnoreCase) ? "Provider detector returned no evidence" : "NoActiveRepository";
                case "missing_repository_selection":
                case "missing_activity_source":
                    return string.Equals(sourceKind, "agent", StringComparison.OrdinalIgnoreCase) ? "No approved source" : "NoActiveRepository";
                case "missing_agent_log_location":
                    return "No readable aggregate file";
                case "agent_source_not_ready":
                    return "No approved source";
                case "agent_log_no_entries":
                case "agent_log_no_supported_entries":
                    return "Provider detector returned no evidence";
                case "missing_repository_path":
                    return "RepositoryPathMissing";
                case "path_not_found":
                case "invalid_repository_path":
                    return "RepositoryFolderNotFound";
                case "not_git_repository":
                    return "NotAGitRepository";
                case "git_unavailable":
                case "git_executable_not_found":
                    return "GitExecutableNotFound";
                case "permission_denied":
                    return "PermissionDenied";
                case "git_timeout":
                case "git_cancelled":
                    return "ProcessTimeout";
                case "git_command_failed":
                    return "GitCommandFailed";
                default:
                    return string.IsNullOrWhiteSpace(errorCode)
                        ? (string.Equals(sourceKind, "agent", StringComparison.OrdinalIgnoreCase) ? "No connected AI agent" : "NoActiveRepository")
                        : SafeNativeText(errorCode, string.Equals(sourceKind, "agent", StringComparison.OrdinalIgnoreCase) ? "Analysis service exception" : "NoActiveRepository");
            }
        }

        private static string NativeSafeRecoveryMessage(string category, string fallback)
        {
            switch (category)
            {
                case "NoActiveRepository": return "Connect a repository first.";
                case "RepositoryPathMissing": return "Repository path is missing. Reconnect required.";
                case "RepositoryFolderNotFound": return "Repository folder was not found. Reconnect required.";
                case "NotAGitRepository": return "This folder is not a Git repository.";
                case "GitExecutableNotFound": return "Git executable was not found. Install Xcode Command Line Tools or Git.";
                case "PermissionDenied": return "Permission denied while reading repository folder.";
                case "ProcessTimeout": return "Git command timed out.";
                case "GitCommandFailed": return "Git command failed. Check repository state and try again.";
                case "No connected AI agent": return "Connect an AI agent provider before analysis.";
                case "No approved source": return "Choose or approve a readable AI agent source before analysis.";
                case "No readable aggregate file": return "The selected AI agent source has no readable aggregate file.";
                case "Provider detector returned no evidence": return "The provider detector did not find safe aggregate activity.";
                case "Analysis service exception": return "The analysis service raised an exception.";
                default: return SafeNativeText(fallback, "Analysis failed safely.");
            }
        }

        private void EnsureSceneInfrastructure()
        {
            if (UseNativeMacDashboardShell)
            {
                LastRemovedStaleUnityDashboardRootCount = RemoveStaleUnityDashboardRoots();
                Debug.Log("INFO [Bootstrap] removedStaleUnityDashboardRoot count=" + LastRemovedStaleUnityDashboardRootCount);
                Debug.Log("INFO [Bootstrap] uiMode=nativeAppKit");
                Debug.Log("INFO [NativeDashboard] mode=macOSPlayer source=AppKit");
                Debug.Log("INFO [BootstrapRoot] productUI=disabled reason=nativeShell");
                Debug.Log("INFO " + LogPrefix + " native macOS dashboard shell active; Unity BootstrapRoot UI is disabled for product runtime.");
                return;
            }

#if UNITY_EDITOR
            Debug.Log("INFO [Bootstrap] uiMode=unityFallback");
            Debug.Log("INFO [NativeDashboard] mode=UnityEditor source=BootstrapRootFallback productUI=debugFallback");
#else
            Debug.Log("INFO [Bootstrap] uiMode=unityFallback");
            Debug.Log("INFO [NativeDashboard] mode=NonMacPlayer source=BootstrapRootFallback productUI=fallback");
#endif
            EnsureEventSystem();
            var canvasObject = EnsureCanvas();
            bootstrapRoot = EnsureBootstrapRoot(canvasObject);
            if (bootstrapRoot == null)
            {
                if (prefabUiUnavailableLogged)
                {
                    Debug.LogError("TokenForge prefab UI is required but BootstrapRoot.prefab is unavailable. Startup UI was not created.");
                }
                else
                {
                    Debug.LogError("ERROR " + LogPrefix + " BootstrapRootView missing");
                }
            }
        }

        private int RemoveStaleUnityDashboardRoots()
        {
            var removed = 0;
            foreach (var root in FindSceneObjects<BootstrapRootView>())
            {
                if (root == null)
                {
                    continue;
                }

                DestroySceneObjectImmediate(root.gameObject);
                removed++;
            }

            foreach (var canvas in FindSceneObjects<Canvas>())
            {
                if (canvas == null)
                {
                    continue;
                }

                DestroySceneObjectImmediate(canvas.gameObject);
                removed++;
            }

            bootstrapRoot = null;
            return removed;
        }

        private void EnsureEventSystem()
        {
            var eventSystem = EventSystem.current;
            var eventSystemObject = eventSystem != null && eventSystem.gameObject.scene == SceneManager.GetActiveScene()
                ? eventSystem.gameObject
                : FindSceneGameObject("EventSystem");
            if (eventSystemObject == null)
            {
                eventSystemObject = new GameObject("EventSystem");
            }

            eventSystemObject.SetActive(true);
            GetOrAddComponent<EventSystem>(eventSystemObject);
            GetOrAddComponent<StandaloneInputModule>(eventSystemObject);
        }

        private GameObject EnsureCanvas()
        {
            var canvas = FindSceneObject<Canvas>();
            var canvasObject = canvas != null ? canvas.gameObject : FindSceneGameObject("Canvas");
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

            canvas = GetOrAddComponent<Canvas>(canvasObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            canvas.pixelPerfect = false;
            canvas.enabled = true;
            canvas.targetDisplay = 0;
            canvas.overrideSorting = false;
            canvas.sortingOrder = 0;

            var scaler = GetOrAddComponent<CanvasScaler>(canvasObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GetOrAddComponent<GraphicRaycaster>(canvasObject);
            return canvasObject;
        }

        private BootstrapRootView EnsureBootstrapRoot(GameObject canvasObject)
        {
            var existingRoots = FindSceneObjects<BootstrapRootView>();
            var prefab = bootstrapRootPrefab;
#if UNITY_EDITOR
            if (prefab == null && loadPrefabFromAssetPathInEditor && !DisableEditorAssetPrefabLookupForTests)
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BootstrapRootPrefabPath);
            }
#endif
            if (prefab != null)
            {
                foreach (var sceneRoot in existingRoots)
                {
                    if (sceneRoot == null)
                    {
                        continue;
                    }

                    Debug.Log("INFO " + HierarchyLogPrefix + " removing scene BootstrapRoot before prefab instantiation path="
                        + GetHierarchyPath(sceneRoot.transform)
                        + " scene=" + sceneRoot.gameObject.scene.path);
                    DestroySceneObjectImmediate(sceneRoot.gameObject);
                }

                var instance = Instantiate(prefab, canvasObject.transform, false);
                instance.name = "BootstrapRoot";
                NormalizeBootstrapRoot(instance, canvasObject);
                MarkBootstrapRoot(instance, "prefab", BootstrapRootPrefabPath);
                instance.SetActive(true);
                Debug.Log("INFO " + HierarchyLogPrefix + " instantiated BootstrapRoot prefab path="
                    + GetHierarchyPath(instance.transform)
                    + " parent=" + (instance.transform.parent != null ? instance.transform.parent.name : "<none>")
                    + " scene=" + instance.scene.path);
                var root = instance.GetComponent<BootstrapRootView>();
                LogBootstrapRootSource(root);
                return root;
            }

            if (prefab == null)
            {
                prefabUiUnavailableLogged = true;
                Debug.LogWarning("TokenForge BootstrapRoot.prefab is unavailable. Prefab UI is the only supported startup path.");
                Debug.LogError("TokenForge prefab UI is required but BootstrapRoot.prefab is unavailable. No script UI fallback will be created.");
                return null;
            }

            return null;
        }

        private void LogStartupScene()
        {
            var activeScene = SceneManager.GetActiveScene();
            Debug.Log("[StartupScene] activeScene=" + activeScene.name);
            Debug.Log("[StartupScene] buildIndex=" + activeScene.buildIndex);
            Debug.Log("[StartupScene] path=" + activeScene.path);
#if UNITY_EDITOR
            var buildScenePath = UnityEditor.EditorBuildSettings.scenes != null && UnityEditor.EditorBuildSettings.scenes.Length > 0
                ? UnityEditor.EditorBuildSettings.scenes[0].path
                : "<none>";
            Debug.Log("[StartupScene] buildSettingsFirst=" + buildScenePath);
#endif
        }

        private void LogRuntimeBuildIdentity()
        {
            Debug.Log("INFO [BuildIdentity][RUNTIME_CODE_VERSION] " + RuntimeBuildIdentityMarker +
                      " csharpMarker=" + AppBootstrapperVersionMarker +
                      " " + RuntimeBuildIdentityGitMarker +
                      " buildTimestamp=" + DateTimeOffset.UtcNow.ToString("O") +
                      " unityProductVersion=" + Application.version +
                      " unityVersion=" + Application.unityVersion +
                      " appBundlePath=" + RuntimeAppBundlePath() +
                      " playerLogPath=" + RuntimePlayerLogPath());
            Debug.Log("INFO [RuntimeIdentity] AppBootstrapperVersionMarker=" + AppBootstrapperVersionMarker);
            Debug.Log("INFO [RuntimeIdentity] RuntimeBuild=" + RuntimeBuildIdentityMarker + " " + RuntimeBuildIdentityGitMarker);
            LogNativeRuntimeMarkerIfAvailable();
            Debug.Log("INFO [RuntimeIdentity] applicationVersion=" + Application.version + " unityVersion=" + Application.unityVersion + " buildGUID=" + Application.buildGUID);
            Debug.Log("INFO [RuntimeIdentity] dataPath=" + Application.dataPath + " persistentDataPath=" + Application.persistentDataPath);
            Debug.Log("INFO [RuntimeIdentity] runtimeMode=" + (Application.isEditor ? "Editor" : "Player") + " appBundlePath=" + RuntimeAppBundlePath() + " unityProjectPath=" + RuntimeUnityProjectPath());
            try
            {
                var assembly = typeof(AppBootstrapper).GetTypeInfo().Assembly;
                var location = assembly.Location;
                Debug.Log("INFO [RuntimeIdentity] csharpAssembly=" + (string.IsNullOrWhiteSpace(location) ? "unavailable" : location));
                if (!string.IsNullOrWhiteSpace(location) && File.Exists(location))
                {
                    Debug.Log("INFO [RuntimeIdentity] csharpAssemblyModified=" + File.GetLastWriteTimeUtc(location).ToString("O") + " hash=" + Sha256ForFile(location));
                }
                else
                {
                    Debug.Log("WARN [RuntimeIdentity] csharpAssemblyModified=unavailable reason=assemblyLocationMissing");
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("WARN [RuntimeIdentity] csharpAssembly identity failed: " + exception.GetType().Name);
            }
        }

        private static void LogNativeRuntimeMarkerIfAvailable()
        {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
            try
            {
                NativeLogAppBootstrapperRuntimeMarker();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("WARN [RuntimeIdentity] native AppBootstrapper marker failed: " + exception.GetType().Name);
            }
#endif
        }

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        [DllImport("DesktopCompanionOverlay", EntryPoint = "TokenForge_LogAppBootstrapperRuntimeMarker")]
        private static extern void NativeLogAppBootstrapperRuntimeMarker();
#endif

        private static string RuntimeAppBundlePath()
        {
            if (Application.isEditor)
            {
                return "editor";
            }

            try
            {
                var dataPath = Application.dataPath;
                var contentsDirectory = Directory.GetParent(dataPath);
                return contentsDirectory != null ? contentsDirectory.FullName : "unavailable";
            }
            catch (Exception exception)
            {
                return "unavailable:" + exception.GetType().Name;
            }
        }

        private static string RuntimePlayerLogPath()
        {
            try
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                return string.IsNullOrWhiteSpace(home)
                    ? "unavailable"
                    : Path.Combine(home, "Library/Logs/TokenForge/TokenForge/Player.log");
            }
            catch (Exception exception)
            {
                return "unavailable:" + exception.GetType().Name;
            }
        }

        private static string RuntimeUnityProjectPath()
        {
            try
            {
                if (Application.isEditor)
                {
                    var assetsDirectory = Directory.GetParent(Application.dataPath);
                    return assetsDirectory != null ? assetsDirectory.FullName : "unavailable";
                }

                return "player-build";
            }
            catch (Exception exception)
            {
                return "unavailable:" + exception.GetType().Name;
            }
        }

        private static bool IsRuntimeVerificationMode()
        {
            var env = Environment.GetEnvironmentVariable("TOKENFORGE_VERIFY_RUNTIME");
            if (IsTruthy(env))
            {
                return true;
            }

            var args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length; index++)
            {
                var arg = args[index] ?? string.Empty;
                if (string.Equals(arg, "-TokenForgeVerifyRuntime", StringComparison.OrdinalIgnoreCase))
                {
                    return index + 1 >= args.Length || IsTruthy(args[index + 1]);
                }

                const string prefix = "-TokenForgeVerifyRuntime=";
                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return IsTruthy(arg.Substring(prefix.Length));
                }
            }

            return false;
        }

        private static bool IsTruthy(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            value = value.Trim();
            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
        }

        private static string Sha256ForFile(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private void MarkBootstrapRoot(GameObject rootObject, string source, string prefabPath)
        {
            if (rootObject == null)
            {
                return;
            }

            var marker = GetOrAddComponent<BootstrapRootSourceMarker>(rootObject);
            marker.SetSource(source, prefabPath, BootstrapRootSourceMarker.CurrentUiVersion);
        }

        private void LogBootstrapRootSource(BootstrapRootView root)
        {
            if (root == null)
            {
                LastBootstrapRootSource = "missing";
                LastBootstrapRootPrefabPath = BootstrapRootPrefabPath;
                LastBootstrapRootUiVersion = BootstrapRootSourceMarker.CurrentUiVersion;
                Debug.Log("[BootstrapRoot] source=missing");
                Debug.Log("[BootstrapRoot] prefabPath=" + BootstrapRootPrefabPath);
                Debug.Log("[BootstrapRoot] rootInstanceId=<none>");
                Debug.Log("[BootstrapRoot] uiVersion=" + BootstrapRootSourceMarker.CurrentUiVersion);
                return;
            }

            var marker = root.GetComponent<BootstrapRootSourceMarker>();
            LastBootstrapRootSource = marker != null ? marker.Source : "unknown";
            LastBootstrapRootPrefabPath = marker != null ? marker.PrefabPathValue : BootstrapRootPrefabPath;
            LastBootstrapRootUiVersion = marker != null ? marker.UiVersion : BootstrapRootSourceMarker.CurrentUiVersion;
            Debug.Log("[BootstrapRoot] source=" + LastBootstrapRootSource);
            Debug.Log("[BootstrapRoot] prefabPath=" + LastBootstrapRootPrefabPath);
            Debug.Log("[BootstrapRoot] rootInstanceId=" + root.gameObject.GetInstanceID());
            Debug.Log("[BootstrapRoot] uiVersion=" + LastBootstrapRootUiVersion);
        }

        private void LogHierarchyDump(string stage)
        {
            var activeScene = SceneManager.GetActiveScene();
            var roots = activeScene.IsValid() ? activeScene.GetRootGameObjects() : new GameObject[0];
            Debug.Log("INFO " + HierarchyLogPrefix + " stage=" + stage
                + " Scene=" + activeScene.name
                + " path=" + activeScene.path
                + " isLoaded=" + activeScene.isLoaded
                + " rootCount=" + roots.Length
                + " isPlaying=" + Application.isPlaying);

            if (UseNativeMacDashboardShell)
            {
                Debug.Log("INFO " + HierarchyLogPrefix + " native dashboard shell owns product UI; Unity hierarchy validation is limited to runtime companion objects.");
                return;
            }

            if (Application.isPlaying
                && activeScene.path != StartupScenePath
                && activeScene.path != LegacyBootstrapScenePath)
            {
                Debug.LogError("ERROR " + HierarchyLogPrefix + " active scene path mismatch expected="
                    + StartupScenePath
                    + " actual=" + activeScene.path);
            }

#if UNITY_EDITOR
            var buildScenePath = UnityEditor.EditorBuildSettings.scenes != null && UnityEditor.EditorBuildSettings.scenes.Length > 0
                ? UnityEditor.EditorBuildSettings.scenes[0].path
                : "<none>";
            Debug.Log("INFO " + HierarchyLogPrefix + " BuildSettings[0]=" + buildScenePath
                + " expected=" + StartupScenePath);
            if (buildScenePath != StartupScenePath)
            {
                Debug.LogError("ERROR " + HierarchyLogPrefix + " BuildSettings[0] path mismatch expected="
                    + StartupScenePath
                    + " actual=" + buildScenePath);
            }
#endif

            foreach (var rootObject in roots)
            {
                Debug.Log("INFO " + HierarchyLogPrefix + " root=" + rootObject.name
                    + " active=" + rootObject.activeInHierarchy
                    + " childCount=" + rootObject.transform.childCount
                    + " scale=" + FormatVector(rootObject.transform.lossyScale)
                    + " scene=" + rootObject.scene.path);
            }

            var canvas = FindSceneObject<Canvas>();
            if (canvas == null)
            {
                if (stage == "AwakeStart")
                {
                    Debug.Log("INFO " + HierarchyLogPrefix + " Canvas missing in active scene before bootstrap infrastructure normalization");
                }
                else
                {
                    Debug.LogError("ERROR " + HierarchyLogPrefix + " Canvas missing in active scene");
                }

                return;
            }

            var canvasRect = canvas.GetComponent<RectTransform>();
            Debug.Log("INFO " + HierarchyLogPrefix + " Canvas renderMode=" + canvas.renderMode
                + " enabled=" + canvas.enabled
                + " activeInHierarchy=" + canvas.gameObject.activeInHierarchy
                + " sortingOrder=" + canvas.sortingOrder
                + " targetDisplay=" + canvas.targetDisplay
                + " scale=" + FormatVector(canvas.transform.lossyScale)
                + " worldCorners=" + (canvasRect != null ? FormatWorldCorners(canvasRect) : "<none>"));

            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                Debug.Log("INFO " + HierarchyLogPrefix + " CanvasScaler uiScaleMode=" + scaler.uiScaleMode
                    + " referenceResolution=" + FormatVector2(scaler.referenceResolution)
                    + " screenMatchMode=" + scaler.screenMatchMode
                    + " matchWidthOrHeight=" + scaler.matchWidthOrHeight.ToString("0.###"));
            }

            var activeRootsInScene = CountActiveSceneObjects(FindSceneObjects<BootstrapRootView>());
            if (activeRootsInScene > 1)
            {
                Debug.LogError("ERROR " + HierarchyLogPrefix + " duplicate active BootstrapRoot count=" + activeRootsInScene);
            }

            var root = bootstrapRoot != null ? bootstrapRoot : FindSceneObject<BootstrapRootView>();
            if (root == null || root.transform.parent != canvas.transform)
            {
                if (prefabUiUnavailableLogged)
                {
                    Debug.Log("INFO " + HierarchyLogPrefix + " BootstrapRoot missing because prefab UI is unavailable.");
                    return;
                }

                Debug.LogError("ERROR " + HierarchyLogPrefix + " BootstrapRoot missing under Canvas");
                return;
            }

            LogPathState(canvas.transform, "Canvas/BootstrapRoot");
            LogPathState(canvas.transform, "Canvas/BootstrapRoot/Background");
            LogPathState(canvas.transform, "Canvas/BootstrapRoot/WindowShell");
            LogPathState(canvas.transform, "Canvas/BootstrapRoot/WindowShell/TopBar");
            LogPathState(canvas.transform, "Canvas/BootstrapRoot/WindowShell/AppBody/Sidebar");
            LogPathState(canvas.transform, "Canvas/BootstrapRoot/WindowShell/AppBody/MainContent");
            LogPathState(canvas.transform, "Canvas/BootstrapRoot/WindowShell/AppBody/MainContent/Root Scroll");
            LogPathState(canvas.transform, "Canvas/BootstrapRoot/WindowShell/AppBody/MainContent/Root Scroll/Viewport");
            LogPathState(canvas.transform, "Canvas/BootstrapRoot/WindowShell/AppBody/MainContent/Root Scroll/Viewport/Content");
            LogPathState(canvas.transform, "Canvas/BootstrapRoot/WindowShell/AppBody/MainContent/Root Scroll/Viewport/Content/Start Screen Root");
            LogPathState(canvas.transform, "Canvas/BootstrapRoot/WindowShell/AppBody/MainContent/Root Scroll/Viewport/Content/Game Dashboard Root");
            LogPathState(canvas.transform, "Canvas/BootstrapRoot/WindowShell/AppBody/MainContent/Root Scroll/Viewport/Content/Add Repository Flow Root");
            LogPathState(canvas.transform, "Canvas/BootstrapRoot/WindowShell/AppBody/MainContent/Root Scroll/Viewport/Content/Settings Root");

            var scrollRect = root.rootScrollRect;
            var background = root.transform.Find("Background");
            var viewport = scrollRect != null ? scrollRect.viewport : null;
            var content = scrollRect != null ? scrollRect.content : null;
            Debug.Log("INFO " + HierarchyLogPrefix + " Background siblingIndex="
                + (background != null ? background.GetSiblingIndex() : -1)
                + " RootScroll siblingIndex=" + (scrollRect != null ? scrollRect.transform.GetSiblingIndex() : -1));

            if (viewport != null)
            {
                var viewportImage = viewport.GetComponent<Image>();
                var mask = viewport.GetComponent<Mask>();
                var rectMask = viewport.GetComponent<RectMask2D>();
                Debug.Log("INFO " + HierarchyLogPrefix + " Viewport imageAlpha="
                    + (viewportImage != null ? viewportImage.color.a.ToString("0.###") : "<missing>")
                    + " Mask.showMaskGraphic=" + (mask != null ? mask.showMaskGraphic.ToString() : "<missing>")
                    + " RectMask2D=" + (rectMask != null)
                    + " canvasRendererCull=" + (viewportImage != null && viewportImage.canvasRenderer != null && viewportImage.canvasRenderer.cull));
            }

            if (content == null)
            {
                Debug.LogError("ERROR " + HierarchyLogPrefix + " Content missing under Viewport");
            }
            else
            {
                Debug.Log("INFO " + HierarchyLogPrefix + " Content childCount=" + content.childCount
                    + " worldCorners=" + FormatWorldCorners(content)
                    + " scale=" + FormatVector(content.lossyScale));
            }

            if (!RequiredPanelExists(content, "Start Screen Root")
                || !RequiredPanelExists(content, "Game Dashboard Root"))
            {
                Debug.LogError("ERROR " + HierarchyLogPrefix + " representative root screens missing");
            }

            LogRepresentativeTextState(root.transform, "Hero Title");
            LogRepresentativeTextState(root.transform, "Run Analysis/Label");
            LogRepresentativeTextState(root.transform, "Connect AI Agent/Label");
        }

        private static bool RequiredPanelExists(RectTransform content, string childName)
        {
            return content != null && content.Find(childName) != null;
        }

        private static void LogPathState(Transform canvasTransform, string expectedPath)
        {
            var relativePath = expectedPath.StartsWith("Canvas/", System.StringComparison.Ordinal)
                ? expectedPath.Substring("Canvas/".Length)
                : expectedPath;
            var target = canvasTransform != null ? canvasTransform.Find(relativePath) : null;
            if (target == null)
            {
                Debug.LogError("ERROR " + HierarchyLogPrefix + " path=" + expectedPath + " missing");
                return;
            }

            var rect = target.GetComponent<RectTransform>();
            Debug.Log("INFO " + HierarchyLogPrefix + " path=" + expectedPath
                + " active=" + target.gameObject.activeInHierarchy
                + " childCount=" + target.childCount
                + " scale=" + FormatVector(target.lossyScale)
                + " worldCorners=" + (rect != null ? FormatWorldCorners(rect) : "<none>"));
        }

        private static void LogRepresentativeTextState(Transform root, string textObjectName)
        {
            if (root == null)
            {
                return;
            }

            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                if (text == null)
                {
                    continue;
                }

                var textPath = GetHierarchyPath(text.transform);
                if (text.name != textObjectName
                    && !textPath.EndsWith("/" + textObjectName, System.StringComparison.Ordinal))
                {
                    continue;
                }

                Debug.Log("INFO " + HierarchyLogPrefix + " text path=" + textPath
                    + " active=" + text.gameObject.activeInHierarchy
                    + " enabled=" + text.enabled
                    + " alpha=" + (text.color.a * GetCanvasGroupAlpha(text.transform)).ToString("0.###")
                    + " text=" + FormatTextSample(text.text));
                return;
            }

            Debug.LogError("ERROR " + HierarchyLogPrefix + " text " + textObjectName + " missing");
        }

        private static void NormalizeBootstrapRoot(GameObject rootObject, GameObject canvasObject)
        {
            if (rootObject == null || canvasObject == null)
            {
                return;
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
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            var rootImage = rootObject.GetComponent<Image>();
            if (rootImage != null)
            {
                rootImage.color = new Color(rootImage.color.r, rootImage.color.g, rootImage.color.b, 0f);
                rootImage.raycastTarget = false;
            }

            var rootView = rootObject.GetComponent<BootstrapRootView>();
            if (rootView != null)
            {
                EnsureBootstrapBackground(rootView);
                NormalizeRootScrollLayout(rootView);
            }
        }

        private static void EnsureBootstrapBackground(BootstrapRootView root)
        {
            if (root == null)
            {
                return;
            }

            var background = root.transform.Find("Background");
            if (background == null)
            {
                var backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
                backgroundObject.transform.SetParent(root.transform, false);
                background = backgroundObject.transform;
            }

            background.gameObject.SetActive(true);
            NormalizeTransform(background);
            StretchRect(background.GetComponent<RectTransform>());
            var image = GetOrAddComponent<Image>(background.gameObject);
            image.color = new Color(0.965f, 0.945f, 0.905f, 1f);
            image.raycastTarget = false;
            background.SetSiblingIndex(0);
        }

        private static void NormalizeRootScrollLayout(BootstrapRootView root)
        {
            var scrollRect = root.rootScrollRect;
            if (scrollRect == null)
            {
                return;
            }

            scrollRect.gameObject.SetActive(true);
            NormalizeTransform(scrollRect.transform);
            StretchRect(scrollRect.GetComponent<RectTransform>());
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.inertia = true;
            scrollRect.scrollSensitivity = 80f;
            scrollRect.normalizedPosition = new Vector2(0f, 1f);
            GetOrAddComponent<BootstrapScrollDiagnostics>(scrollRect.gameObject);

            var viewport = scrollRect.viewport;
            if (viewport == null)
            {
                var viewportTransform = scrollRect.transform.Find("Viewport");
                viewport = viewportTransform != null ? viewportTransform.GetComponent<RectTransform>() : null;
                scrollRect.viewport = viewport;
            }

            if (viewport != null)
            {
                if (viewport.parent != scrollRect.transform)
                {
                    viewport.SetParent(scrollRect.transform, false);
                }

                viewport.gameObject.SetActive(true);
                NormalizeTransform(viewport.transform);
                StretchRect(viewport);
                var viewportImage = viewport.GetComponent<Image>();
                if (viewportImage != null)
                {
                    viewportImage.color = new Color(0f, 0f, 0f, 0f);
                    viewportImage.raycastTarget = true;
                }

                var mask = viewport.GetComponent<Mask>();
                if (mask != null)
                {
                    DestroySceneComponent(mask);
                }

                GetOrAddComponent<RectMask2D>(viewport.gameObject);
            }

            var content = scrollRect.content;
            if (content == null && viewport != null)
            {
                var contentTransform = viewport.Find("Content");
                content = contentTransform != null ? contentTransform.GetComponent<RectTransform>() : null;
                scrollRect.content = content;
            }

            if (content != null)
            {
                if (viewport != null && content.parent != viewport)
                {
                    content.SetParent(viewport, false);
                }

                content.gameObject.SetActive(true);
                NormalizeTransform(content.transform);
                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = new Vector2(1f, 1f);
                content.pivot = new Vector2(0.5f, 1f);
                content.offsetMin = new Vector2(ContentHorizontalInset, 0f);
                content.offsetMax = new Vector2(-ContentHorizontalInset, 0f);
                content.anchoredPosition = Vector2.zero;

                var layout = content.GetComponent<VerticalLayoutGroup>();
                if (layout != null)
                {
                    layout.padding = new RectOffset(0, 0, ContentTopPadding, ContentBottomPadding);
                    layout.spacing = ContentSectionSpacing;
                    layout.childControlWidth = true;
                    layout.childControlHeight = true;
                    layout.childForceExpandWidth = true;
                    layout.childForceExpandHeight = false;
                }
            }

            if (root.transform.childCount > 0)
            {
                scrollRect.transform.SetAsLastSibling();
            }

            Canvas.ForceUpdateCanvases();
            if (content != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            }
        }

        private static void StretchRect(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private static void NormalizeTransform(Transform transform)
        {
            if (transform == null)
            {
                return;
            }

            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        public bool ValidateVisibleUiTree()
        {
            if (UseNativeMacDashboardShell)
            {
                IsVisibleUiValidated = nativeDashboardService != null && nativeDashboardService.IsAvailable;
                if (!IsVisibleUiValidated)
                {
                    Debug.LogError("ERROR " + LogPrefix + " native macOS dashboard shell unavailable.");
                }

                return IsVisibleUiValidated;
            }

            Canvas.ForceUpdateCanvases();
            if (bootstrapRoot != null)
            {
                if (bootstrapRoot.rootScrollRect != null && bootstrapRoot.rootScrollRect.content != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(bootstrapRoot.rootScrollRect.content);
                }

                var rootRect = bootstrapRoot.GetComponent<RectTransform>();
                if (rootRect != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
                }

                if (bootstrapRoot.rootScrollRect != null && bootstrapRoot.rootScrollRect.content != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(bootstrapRoot.rootScrollRect.content);
                }
            }
            Canvas.ForceUpdateCanvases();

            var canvas = FindSceneObject<Canvas>();
            var eventSystem = FindSceneObject<EventSystem>();
            var root = bootstrapRoot != null ? bootstrapRoot : FindSceneObject<BootstrapRootView>();
            var valid = true;

            if (canvas == null)
            {
                Debug.LogError("ERROR " + LogPrefix + " Canvas missing");
                valid = false;
            }
            else
            {
                if (!canvas.gameObject.activeInHierarchy || !canvas.enabled)
                {
                    Debug.LogError("ERROR " + LogPrefix + " Canvas inactive or disabled");
                    valid = false;
                }

                Debug.Log("INFO " + LogPrefix + " canvas targetDisplay=" + canvas.targetDisplay
                    + " renderMode=" + canvas.renderMode
                    + " sortingOrder=" + canvas.sortingOrder
                    + " overrideSorting=" + canvas.overrideSorting
                    + " sortingLayer=" + canvas.sortingLayerName);

                if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null)
                {
                    Debug.LogError("ERROR " + LogPrefix + " Canvas renderMode is ScreenSpaceCamera but worldCamera is null");
                    valid = false;
                }

                var canvasRect = canvas.GetComponent<RectTransform>();
                if (!HasVisibleRect(canvasRect))
                {
                    Debug.LogError("ERROR " + LogPrefix + " Canvas RectTransform has zero size");
                    valid = false;
                }

                if (canvas.GetComponent<GraphicRaycaster>() == null)
                {
                    Debug.LogError("ERROR " + LogPrefix + " GraphicRaycaster missing");
                    valid = false;
                }
            }

            if (eventSystem == null || !eventSystem.gameObject.activeInHierarchy || !eventSystem.enabled)
            {
                Debug.LogError("ERROR " + LogPrefix + " EventSystem missing");
                valid = false;
            }

            if (root == null)
            {
                Debug.LogError("ERROR " + LogPrefix + " BootstrapRootView missing");
                IsVisibleUiValidated = false;
                return false;
            }

            if (!root.gameObject.activeInHierarchy)
            {
                Debug.LogError("ERROR " + LogPrefix + " BootstrapRootView inactive");
                valid = false;
            }

            if (!root.ValidateReferences(out var referenceError))
            {
                Debug.LogError("ERROR " + LogPrefix + " BootstrapRootView serialized reference validation failed: " + referenceError);
                valid = false;
            }

            var rootRectTransform = root.GetComponent<RectTransform>();
            if (!HasVisibleRect(rootRectTransform))
            {
                Debug.LogError("ERROR " + LogPrefix + " BootstrapRoot RectTransform has zero size");
                valid = false;
            }
            else
            {
                Debug.Log("INFO " + LogPrefix + " root rect worldCorners=" + FormatWorldCorners(rootRectTransform));
            }

            if (!ValidateMvpScreenLayout(root, canvas))
            {
                valid = false;
            }

            var launchPanelActive = root.startScreenRoot != null && root.startScreenRoot.activeInHierarchy;
            if (!launchPanelActive)
            {
                Debug.LogError("ERROR " + LogPrefix + " Start Screen inactive");
                valid = false;
            }

            var panelCount = CountActivePanels(root);
            var visibility = CountViewportVisibleCandidates(root.gameObject, canvas);
            var visibleGraphicCount = visibility.VisibleGraphicCount;
            var visibleTextCount = visibility.VisibleTextCount;
            LastViewportVisibleCandidateCount = visibility.VisibleCandidateCount;
            Debug.Log("INFO " + LogPrefix + " visible candidate mix images=" + visibility.VisibleImageCount
                + " buttons=" + visibility.VisibleButtonCount
                + " panels=" + visibility.VisiblePanelImageCount
                + " text=" + visibility.VisibleTextCount
                + " tmpText=" + visibility.VisibleTmpTextCount
                + " representativeTextVisible=" + visibility.HasRepresentativeStartupText
                + " representativeTextVisibleCount=" + visibility.RepresentativeTextVisibleCount);

            if (visibleGraphicCount == 0)
            {
                Debug.LogError("ERROR " + LogPrefix + " No visible UI graphics found");
                valid = false;
            }

            if (visibleTextCount == 0)
            {
                Debug.LogError("ERROR " + LogPrefix + " No visible UI text found");
                valid = false;
            }

            if (visibility.VisibleCandidateCount == 0)
            {
                Debug.LogError("ERROR " + LogPrefix + " no viewport-visible UI candidates");
                valid = false;
            }

            if (visibility.RepresentativeTextVisibleCount < MinimumVisibleRepresentativeStartupTextCount)
            {
                Debug.LogError("ERROR " + LogPrefix + " fewer than "
                    + MinimumVisibleRepresentativeStartupTextCount
                    + " representative product dashboard texts are viewport-visible count="
                    + visibility.RepresentativeTextVisibleCount);
                LogRepresentativeTextDiagnostics(root.gameObject, canvas);
                valid = false;
            }

            if (!ValidateTopmostFullScreenGraphics())
            {
                valid = false;
            }

            if (valid)
            {
                IsVisibleUiValidated = true;
                var canvasName = canvas != null ? canvas.name : "missing";
                var renderMode = canvas != null ? canvas.renderMode.ToString() : "missing";
                Debug.Log("INFO " + LogPrefix + " visible UI validated canvas=" + canvasName
                    + " renderMode=" + renderMode
                    + " targetDisplay=" + (canvas != null ? canvas.targetDisplay : -1)
                    + " candidates=" + visibility.VisibleCandidateCount
                    + " graphics=" + visibleGraphicCount
                    + " texts=" + visibleTextCount
                    + " panels=" + panelCount);
                return true;
            }

            IsVisibleUiValidated = false;
            return false;
        }

        private void CompleteBootstrap(string message)
        {
            if (UseNativeMacDashboardShell)
            {
                if (!NativeSafeModeEnabled)
                {
                    ApplyNativeShellState(showDashboardIfNeeded: true);
                }
                IsVisibleUiValidated = NativeSafeModeEnabled || (nativeDashboardService != null && nativeDashboardService.IsAvailable);
                IsRenderedFrameSmokeSkippedForBatchMode = true;
                IsBootstrapComplete = IsVisibleUiValidated;
                Debug.Log("INFO " + LogPrefix + " native macOS dashboard shell ready; Unity uGUI dashboard validation skipped.");
                Debug.Log("INFO " + LogPrefix + " " + message);
                Debug.Log("INFO [Startup] AppBootstrapper end");
                LogStartupIdleReached();
                return;
            }

            if (!ValidateVisibleUiTree())
            {
                Debug.LogError("ERROR " + LogPrefix + " visible UI validation failed; bootstrap completion suppressed.");
                return;
            }

            Debug.Log("INFO " + LogPrefix + " TokenForge bootstrap UI ready.");

            if (Application.isBatchMode)
            {
                IsRenderedFrameSmokeSkippedForBatchMode = true;
                Debug.Log("INFO " + LogPrefix + " rendered frame smoke skipped in batchmode; structure validation only.");
                Debug.Log("INFO " + LogPrefix + " " + message);
                IsBootstrapComplete = true;
                Debug.Log("INFO [Startup] AppBootstrapper end");
                LogStartupIdleReached();
                return;
            }

            if (!ValidateRenderedFrameSmoke())
            {
                Debug.LogError("ERROR " + LogPrefix + " rendered frame smoke failed; bootstrap completion suppressed.");
                return;
            }

            Debug.Log("INFO " + LogPrefix + " " + message);
            IsBootstrapComplete = true;
            Debug.Log("INFO [Startup] AppBootstrapper end");
            LogStartupIdleReached();
        }

        private void LogStartupIdleReached()
        {
            if (startupIdleLogged)
            {
                return;
            }

            startupIdleLogged = true;
            Debug.Log("INFO [StartupDiagnostic][IDLE_REACHED]");
        }

        private static bool NativeSafeModeEnabled => IsEnvironmentFlagEnabled("TOKENFORGE_NATIVE_SAFE_MODE");
        private static bool DisableNativeOverlayEnabled => NativeSafeModeEnabled || IsEnvironmentFlagEnabled("TOKENFORGE_DISABLE_NATIVE_OVERLAY");
        private static bool DisableStatusItemEnabled => NativeSafeModeEnabled || IsEnvironmentFlagEnabled("TOKENFORGE_DISABLE_STATUS_ITEM");
        private static bool DisableNativeDashboardEnabled => NativeSafeModeEnabled || IsEnvironmentFlagEnabled("TOKENFORGE_DISABLE_NATIVE_DASHBOARD");
        private static bool DisableContextMenuEnabled => NativeSafeModeEnabled || IsEnvironmentFlagEnabled("TOKENFORGE_DISABLE_CONTEXT_MENU");
        private static bool DisablePixelNativeRendererEnabled => NativeSafeModeEnabled || IsEnvironmentFlagEnabled("TOKENFORGE_DISABLE_PIXEL_NATIVE_RENDERER");

        private static bool IsEnvironmentFlagEnabled(string name)
        {
            return IsTruthy(Environment.GetEnvironmentVariable(name));
        }

        private static void LogNativeSafeModeState()
        {
            Debug.Log("INFO [NativeSafeMode][FLAGS] safeMode=" + NativeSafeModeEnabled +
                      " disableNativeOverlay=" + DisableNativeOverlayEnabled +
                      " disableStatusItem=" + DisableStatusItemEnabled +
                      " disableNativeDashboard=" + DisableNativeDashboardEnabled +
                      " disableContextMenu=" + DisableContextMenuEnabled +
                      " disablePixelNativeRenderer=" + DisablePixelNativeRendererEnabled);
        }

        private static string NativeSkipReason(string subsystem)
        {
            if (NativeSafeModeEnabled)
            {
                return "TOKENFORGE_NATIVE_SAFE_MODE subsystem=" + subsystem;
            }

            if (string.Equals(subsystem, "dashboard", StringComparison.Ordinal) && DisableNativeDashboardEnabled)
            {
                return "TOKENFORGE_DISABLE_NATIVE_DASHBOARD";
            }

            if (string.Equals(subsystem, "statusItem", StringComparison.Ordinal) && DisableStatusItemEnabled)
            {
                return "TOKENFORGE_DISABLE_STATUS_ITEM";
            }

            if (string.Equals(subsystem, "contextMenu", StringComparison.Ordinal) && DisableContextMenuEnabled)
            {
                return "TOKENFORGE_DISABLE_CONTEXT_MENU";
            }

            if (string.Equals(subsystem, "pixelRenderer", StringComparison.Ordinal) && DisablePixelNativeRendererEnabled)
            {
                return "TOKENFORGE_DISABLE_PIXEL_NATIVE_RENDERER";
            }

            return "TOKENFORGE_DISABLE_NATIVE_OVERLAY";
        }

        private static bool HasVisibleRect(RectTransform rect)
        {
            if (rect == null)
            {
                return false;
            }

            var rectValue = rect.rect;
            var scale = rect.lossyScale;
            return Mathf.Abs(rectValue.width) > 0.5f
                && Mathf.Abs(rectValue.height) > 0.5f
                && Mathf.Abs(scale.x) > 0.001f
                && Mathf.Abs(scale.y) > 0.001f;
        }

        private static int CountActivePanels(BootstrapRootView root)
        {
            var count = 0;
            count += IsPanelActive(root.accountPanel) ? 1 : 0;
            count += IsPanelActive(root.activityAnalysisPanel) ? 1 : 0;
            count += IsPanelActive(root.approvedLocationsPanel) ? 1 : 0;
            count += IsPanelActive(root.reviewPanel) ? 1 : 0;
            count += IsPanelActive(root.safeSyncPanel) ? 1 : 0;
            count += IsPanelActive(root.recentSessionsPanel) ? 1 : 0;
            count += IsPanelActive(root.privacyNoticePanel) ? 1 : 0;
            return count;
        }

        private static bool IsPanelActive(UiBinderBase panel)
        {
            return panel != null && panel.gameObject.activeInHierarchy;
        }

        private static bool ValidateRootScrollLayout(BootstrapRootView root, Canvas canvas)
        {
            var valid = true;
            var rootScroll = root.rootScrollRect;
            var scrollRect = rootScroll != null ? rootScroll.GetComponent<RectTransform>() : null;
            var viewport = rootScroll != null ? rootScroll.viewport : null;
            var content = rootScroll != null ? rootScroll.content : null;

            valid &= LogLayoutRect("BootstrapRoot", root.GetComponent<RectTransform>(), canvas, null);
            valid &= LogLayoutRect("Root Scroll", scrollRect, canvas, root.GetComponent<RectTransform>());
            valid &= LogLayoutRect("Viewport", viewport, canvas, scrollRect);
            valid &= LogLayoutRect("Content", content, canvas, viewport);

            var background = root.transform.Find("Background");
            if (background == null)
            {
                Debug.LogError("ERROR " + LogPrefix + " BootstrapRoot/Background missing.");
                valid = false;
            }
            else if (rootScroll != null
                     && background.parent == rootScroll.transform.parent
                     && background.GetSiblingIndex() >= rootScroll.transform.GetSiblingIndex())
            {
                Debug.LogError("ERROR " + LogPrefix + " BootstrapRoot/Background is not behind Root Scroll siblingIndex="
                    + background.GetSiblingIndex()
                    + " rootScrollSiblingIndex=" + rootScroll.transform.GetSiblingIndex());
                valid = false;
            }

            if (viewport != null && rootScroll != null && viewport.parent != rootScroll.transform)
            {
                Debug.LogError("ERROR " + LogPrefix + " Root Scroll viewport is not a direct child of Root Scroll.");
                valid = false;
            }

            if (content != null && viewport != null && content.parent != viewport)
            {
                Debug.LogError("ERROR " + LogPrefix + " Root Scroll content is not a child of Viewport.");
                valid = false;
            }

            if (viewport != null)
            {
                var viewportImage = viewport.GetComponent<Image>();
                if (viewportImage != null && viewportImage.color.a > 0.01f)
                {
                    Debug.LogError("ERROR " + LogPrefix + " Viewport Image must be transparent alpha="
                        + viewportImage.color.a.ToString("0.###"));
                    valid = false;
                }

                var mask = viewport.GetComponent<Mask>();
                if (mask != null)
                {
                    Debug.LogError("ERROR " + LogPrefix + " Root Scroll Viewport must use RectMask2D instead of Mask.");
                    valid = false;
                }

                if (viewport.GetComponent<RectMask2D>() == null)
                {
                    Debug.LogError("ERROR " + LogPrefix + " Root Scroll Viewport RectMask2D is missing.");
                    valid = false;
                }
            }

            if (content != null)
            {
                valid &= ValidateObjectParent(root.startScreenRoot, content, "Start Screen Root");
                valid &= ValidateObjectParent(root.gameDashboardRoot, content, "Game Dashboard Root");
                valid &= ValidateObjectParent(root.runAnalysisRoot, content, "Run Analysis Root");
                valid &= ValidateObjectParent(root.settingsAdvancedRoot, content, "Settings Advanced Root");
                valid &= ValidateObjectParent(root.developerDiagnosticsRoot, content, "Developer Diagnostics Root");
            }

            if (viewport != null && content != null)
            {
                var viewportRect = GetScreenRect(viewport, canvas);
                var contentRect = GetScreenRect(content, canvas);
                var intersects = viewportRect.Overlaps(contentRect, true);
                Debug.Log("INFO " + LogPrefix + " layout intersection viewportContent=" + intersects
                    + " contentAnchoredPosition=" + FormatVector2(content.anchoredPosition)
                    + " contentSize=" + FormatVector2(content.rect.size)
                    + " contentAnchorMin=" + FormatVector2(content.anchorMin)
                    + " contentAnchorMax=" + FormatVector2(content.anchorMax)
                    + " contentPivot=" + FormatVector2(content.pivot)
                    + " contentOffsetMin=" + FormatVector2(content.offsetMin)
                    + " contentOffsetMax=" + FormatVector2(content.offsetMax)
                    + " viewportSize=" + FormatVector2(viewport.rect.size)
                    + " viewportMask=" + (viewport.GetComponent<Mask>() != null)
                    + " viewportRectMask2D=" + (viewport.GetComponent<RectMask2D>() != null));
                if (!intersects)
                {
                    Debug.LogError("ERROR " + LogPrefix + " Root Scroll content does not intersect viewport.");
                    valid = false;
                }

                var childIntersections = 0;
                for (var i = 0; i < content.childCount; i++)
                {
                    var childRect = content.GetChild(i).GetComponent<RectTransform>();
                    if (childRect != null && GetScreenRect(childRect, canvas).Overlaps(viewportRect, true))
                    {
                        childIntersections++;
                    }
                }

                Debug.Log("INFO " + LogPrefix + " layout content children intersecting viewport=" + childIntersections
                    + "/" + content.childCount);
                if (content.childCount > 0 && childIntersections == 0)
                {
                    Debug.LogError("ERROR " + LogPrefix + " Root Scroll viewport clips every content child.");
                    valid = false;
                }
            }

            return valid;
        }

        private static bool ValidateMvpScreenLayout(BootstrapRootView root, Canvas canvas)
        {
            var valid = true;
            valid &= LogLayoutRect("BootstrapRoot", root.GetComponent<RectTransform>(), canvas, null);
            valid &= LogLayoutRect("Game Dashboard Root", root.gameDashboardRoot != null ? root.gameDashboardRoot.GetComponent<RectTransform>() : null, canvas, root.GetComponent<RectTransform>());

            var dashboardRect = root.gameDashboardRoot != null ? root.gameDashboardRoot.GetComponent<RectTransform>() : null;
            var settingsRect = root.settingsAdvancedRoot != null ? root.settingsAdvancedRoot.GetComponent<RectTransform>() : null;
            if (dashboardRect == null || settingsRect == null)
            {
                Debug.LogError("ERROR " + LogPrefix + " Dashboard or Settings root missing.");
                valid = false;
            }

            var background = root.transform.Find("Background");
            if (background == null)
            {
                Debug.LogError("ERROR " + LogPrefix + " BootstrapRoot/Background missing.");
                valid = false;
            }

            var scroll = root.rootScrollRect;
            var viewport = scroll != null ? scroll.viewport : null;
            var content = scroll != null ? scroll.content : null;
            if (scroll == null || viewport == null || content == null)
            {
                Debug.LogError("ERROR " + LogPrefix + " Root Scroll structure missing.");
                valid = false;
            }
            else
            {
                if (scroll.transform.parent != root.transform && scroll.transform.parent == null)
                {
                    Debug.LogError("ERROR " + LogPrefix + " Root Scroll parent is missing.");
                    valid = false;
                }

                if (viewport.parent != scroll.transform || content.parent != viewport)
                {
                    Debug.LogError("ERROR " + LogPrefix + " Root Scroll Viewport/Content hierarchy is invalid.");
                    valid = false;
                }

                var viewportImage = viewport.GetComponent<Image>();
                if (viewportImage == null || viewportImage.color.a > 0.01f)
                {
                    Debug.LogError("ERROR " + LogPrefix + " Root Scroll viewport Image must stay transparent.");
                    valid = false;
                }

                if (viewport.GetComponent<Mask>() != null || viewport.GetComponent<RectMask2D>() == null)
                {
                    Debug.LogError("ERROR " + LogPrefix + " Root Scroll viewport must use RectMask2D without Mask.");
                    valid = false;
                }

                valid &= ValidateObjectParent(root.startScreenRoot, content, "Start Screen Root");
                valid &= ValidateObjectParent(root.gameDashboardRoot, content, "Game Dashboard Root");
                valid &= ValidateObjectParent(root.runAnalysisRoot, content, "Run Analysis Root");
                valid &= ValidateObjectParent(root.settingsAdvancedRoot, content, "Settings Advanced Root");
                valid &= ValidateObjectParent(root.developerDiagnosticsRoot, content, "Developer Diagnostics Root");
            }

            return valid;
        }

        private static bool ValidatePanelParent(UiBinderBase panel, RectTransform content, string label)
        {
            if (panel == null || content == null)
            {
                return false;
            }

            if (panel.transform.parent == content)
            {
                return true;
            }

            Debug.LogError("ERROR " + LogPrefix + " " + label + " is not a direct child of Root Scroll/Viewport/Content.");
            return false;
        }

        private static bool ValidateObjectParent(GameObject child, RectTransform expectedParent, string label)
        {
            if (child == null || expectedParent == null)
            {
                return false;
            }

            if (child.transform.parent == expectedParent)
            {
                return true;
            }

            Debug.LogError("ERROR " + LogPrefix + " " + label + " is under "
                + (child.transform.parent != null ? child.transform.parent.name : "<none>")
                + " instead of " + expectedParent.name + ".");
            return false;
        }

        private static bool LogLayoutRect(string label, RectTransform rect, Canvas canvas, RectTransform expectedParentRect)
        {
            if (rect == null)
            {
                Debug.LogError("ERROR " + LogPrefix + " layout " + label + " missing");
                return false;
            }

            var screenRect = GetScreenRect(rect, canvas);
            var viewportRect = GetViewportRect();
            var hasRect = HasVisibleRect(rect);
            var intersectsScreen = screenRect.Overlaps(viewportRect, true);
            var intersectsParent = expectedParentRect == null || screenRect.Overlaps(GetScreenRect(expectedParentRect, canvas), true);
            Debug.Log("INFO " + LogPrefix + " layout " + label
                + " active=" + rect.gameObject.activeInHierarchy
                + " scale=" + FormatVector(rect.lossyScale)
                + " rectSize=" + FormatVector2(rect.rect.size)
                + " screenRect=" + FormatRect(screenRect)
                + " worldCorners=" + FormatWorldCorners(rect)
                + " intersectsScreen=" + intersectsScreen
                + " intersectsParent=" + intersectsParent);

            if (!hasRect || !intersectsScreen || !intersectsParent)
            {
                Debug.LogError("ERROR " + LogPrefix + " layout " + label + " is not renderable.");
                return false;
            }

            return true;
        }

        public bool ValidateRenderedFrameSmoke()
        {
            try
            {
                var analysis = RenderedFrameAnalysis.Empty;
                var captured = false;
                for (var attempt = 0; attempt < 5; attempt++)
                {
                    var texture = CaptureScreenshotAsTexture();
                    if (texture == null)
                    {
                        continue;
                    }

                    try
                    {
                        analysis = AnalyzeRenderedFrame(texture, "ScreenCapture.CaptureScreenshotAsTexture", attempt + 1);
                        Debug.Log("INFO " + LogPrefix + " rendered frame capture method=" + analysis.CaptureMethod
                            + " attempt=" + analysis.Attempt
                            + " size=" + analysis.Width + "x" + analysis.Height
                            + " sampledPixels=" + analysis.SampledPixelCount
                            + " nonBlackThreshold=" + MinimumVisibleLuminance.ToString("0.000000")
                            + " nonBlackPixelRatio=" + analysis.NonBlackPixelRatio.ToString("0.000000")
                            + " averageLuminance=" + analysis.AverageLuminance.ToString("0.000000")
                            + " brightestPixelLuminance=" + analysis.BrightestPixelLuminance.ToString("0.000000"));
                        captured = true;
                        if (analysis.NonBlackPixelRatio >= MinimumRenderedNonBlackPixelRatio)
                        {
                            break;
                        }
                    }
                    finally
                    {
                        Destroy(texture);
                    }
                }

                if (!captured)
                {
                    Debug.LogError("ERROR " + LogPrefix + " rendered frame capture unavailable");
                    return false;
                }

                LastRenderedFrameAverageLuminance = analysis.AverageLuminance;
                LastRenderedFrameNonBlackPixelRatio = analysis.NonBlackPixelRatio;
                LastRenderedFrameBrightestPixelLuminance = analysis.BrightestPixelLuminance;
                var fullScreenGraphicsValid = ValidateTopmostFullScreenGraphics();

                if (analysis.NonBlackPixelRatio < MinimumRenderedNonBlackPixelRatio)
                {
                    Debug.LogError("ERROR " + LogPrefix + " rendered frame appears black nonBlackPixelRatio="
                        + analysis.NonBlackPixelRatio.ToString("0.000000")
                        + " averageLuminance=" + analysis.AverageLuminance.ToString("0.000000")
                        + " brightestPixelLuminance=" + analysis.BrightestPixelLuminance.ToString("0.000000")
                        + " captureMethod=" + analysis.CaptureMethod
                        + " capturedSize=" + analysis.Width + "x" + analysis.Height
                        + " sampledPixels=" + analysis.SampledPixelCount
                        + " nonBlackThreshold=" + MinimumVisibleLuminance.ToString("0.000000"));
                    return false;
                }

                if (!fullScreenGraphicsValid)
                {
                    return false;
                }

                Debug.Log("INFO " + LogPrefix + " rendered frame smoke passed nonBlackPixelRatio="
                    + analysis.NonBlackPixelRatio.ToString("0.000000")
                    + " averageLuminance=" + analysis.AverageLuminance.ToString("0.000000")
                    + " brightestPixelLuminance=" + analysis.BrightestPixelLuminance.ToString("0.000000")
                    + " captureMethod=" + analysis.CaptureMethod
                    + " capturedSize=" + analysis.Width + "x" + analysis.Height
                    + " sampledPixels=" + analysis.SampledPixelCount
                    + " nonBlackThreshold=" + MinimumVisibleLuminance.ToString("0.000000"));
                return true;
            }
            catch (System.Exception exception)
            {
                Debug.LogError("ERROR " + LogPrefix + " rendered frame capture failed: " + exception.Message);
                return false;
            }
        }

        private static VisibleUiValidationResult CountViewportVisibleCandidates(GameObject rootObject, Canvas canvas)
        {
            var result = new VisibleUiValidationResult();
            if (rootObject == null || canvas == null)
            {
                return result;
            }

            var graphics = new List<Graphic>(rootObject.GetComponentsInChildren<Graphic>(false));
            var warningCount = 0;
            var candidateLogCount = 0;
            for (var index = 0; index < graphics.Count; index++)
            {
                var graphic = graphics[index];
                if (!TryCreateVisibleCandidate(graphic, canvas, graphics, index, out var candidate, out var ignoredReason))
                {
                    if (warningCount < MaximumVisibilityDiagnosticsPerPass)
                    {
                        Debug.Log("INFO " + LogPrefix + " ignored graphic reason=" + ignoredReason
                            + " name=" + (graphic != null ? graphic.name : "null"));
                        warningCount++;
                    }

                    continue;
                }

                result.VisibleCandidateCount++;
                result.VisibleGraphicCount++;
                if (graphic is Image)
                {
                    result.VisibleImageCount++;
                    if (graphic.name.IndexOf("Panel", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || graphic.name.IndexOf("Root", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || graphic.name.IndexOf("Background", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        result.VisiblePanelImageCount++;
                    }
                }

                if (graphic.GetComponentInParent<Button>() != null)
                {
                    result.VisibleButtonCount++;
                }

                if (candidate.TextLength > 0)
                {
                    result.VisibleTextCount++;
                    if (ContainsRepresentativeStartupText(candidate.TextContent))
                    {
                        result.HasRepresentativeStartupText = true;
                        result.RepresentativeTextVisibleCount++;
                    }
                }

                if (IsTmpTextGraphic(graphic))
                {
                    result.VisibleTmpTextCount++;
                }

                if (candidateLogCount < MaximumVisibilityDiagnosticsPerPass)
                {
                    Debug.Log("INFO " + LogPrefix + " visible candidate rank=" + (candidateLogCount + 1)
                        + " path=" + candidate.Path
                        + " name=" + candidate.Name
                        + " type=" + candidate.TypeName
                        + " color=" + FormatColor(candidate.Color)
                        + " renderAlpha=" + candidate.Alpha.ToString("0.###")
                        + " expectedLuminance=" + candidate.ExpectedLuminance.ToString("0.###")
                        + " rect=" + FormatRect(candidate.LocalRect)
                        + " screenRect=" + FormatRect(candidate.ScreenRect)
                        + " canvasRendererCull=" + candidate.CanvasRendererCull
                        + " alpha=" + candidate.Alpha.ToString("0.###")
                        + " textLength=" + candidate.TextLength
                        + " textSample=" + FormatTextSample(candidate.TextContent));
                    candidateLogCount++;
                }
            }

            return result;
        }

        private static bool TryCreateVisibleCandidate(
            Graphic graphic,
            Canvas canvas,
            List<Graphic> renderOrderedGraphics,
            int graphicIndex,
            out VisibleUiCandidate candidate,
            out string ignoredReason)
        {
            candidate = default;
            ignoredReason = "unknown";
            if (graphic == null)
            {
                ignoredReason = "null";
                return false;
            }

            if (!graphic.gameObject.activeInHierarchy || !graphic.enabled)
            {
                ignoredReason = "inactive_or_disabled";
                return false;
            }

            if (graphic.canvas == null)
            {
                ignoredReason = "missing_canvas";
                return false;
            }

            if (graphic.canvasRenderer != null && graphic.canvasRenderer.cull)
            {
                ignoredReason = "canvas_renderer_culled";
                return false;
            }

            if (graphic.color.a <= 0.001f)
            {
                ignoredReason = "alpha_zero";
                return false;
            }

            var canvasGroupAlpha = GetCanvasGroupAlpha(graphic.transform);
            if (canvasGroupAlpha <= MinimumCanvasGroupAlpha)
            {
                ignoredReason = "alpha_zero";
                return false;
            }

            if (!HasVisibleRect(graphic.rectTransform))
            {
                ignoredReason = "zero_rect";
                return false;
            }

            if (!HasNonZeroParentScale(graphic.transform))
            {
                ignoredReason = "zero_parent_scale";
                return false;
            }

            var textContent = GetVisibleTextContent(graphic);
            var textLength = string.IsNullOrWhiteSpace(textContent) ? 0 : textContent.Trim().Length;
            if (IsTextGraphic(graphic) && textLength == 0)
            {
                ignoredReason = "empty_text";
                return false;
            }

            var screenRect = GetScreenRect(graphic.rectTransform, canvas);
            var viewportRect = GetViewportRect();
            if (!screenRect.Overlaps(viewportRect, true))
            {
                ignoredReason = "offscreen";
                return false;
            }

            if (!HasMeaningfulContrast(graphic, textLength, screenRect, viewportRect))
            {
                ignoredReason = "low_contrast_background";
                return false;
            }

            if (IsCoveredByBlackOverlay(graphic, screenRect, renderOrderedGraphics, graphicIndex, canvas))
            {
                ignoredReason = "covered_by_black_overlay";
                return false;
            }

            candidate = new VisibleUiCandidate(
                GetHierarchyPath(graphic.transform),
                graphic.name,
                graphic.GetType().Name,
                graphic.rectTransform.rect,
                screenRect,
                graphic.color.a * canvasGroupAlpha,
                textLength,
                textContent,
                graphic.color,
                CalculateLuminance(graphic.color),
                graphic.canvasRenderer != null && graphic.canvasRenderer.cull);
            return true;
        }

        private static bool HasMeaningfulContrast(Graphic graphic, int textLength, Rect screenRect, Rect viewportRect)
        {
            if (textLength > 0)
            {
                return true;
            }

            var luminance = CalculateLuminance(graphic.color);
            if (luminance >= MinimumVisibleLuminance)
            {
                return true;
            }

            var intersection = Intersect(screenRect, viewportRect);
            return intersection.width * intersection.height < viewportRect.width * viewportRect.height * 0.95f;
        }

        private static bool IsCoveredByBlackOverlay(
            Graphic candidate,
            Rect candidateRect,
            List<Graphic> renderOrderedGraphics,
            int candidateIndex,
            Canvas canvas)
        {
            for (var index = candidateIndex + 1; index < renderOrderedGraphics.Count; index++)
            {
                var overlay = renderOrderedGraphics[index];
                if (overlay == null
                    || overlay == candidate
                    || !overlay.gameObject.activeInHierarchy
                    || !overlay.enabled
                    || overlay.canvas == null
                    || overlay.color.a < 0.95f
                    || CalculateLuminance(overlay.color) > 0.02f
                    || !HasVisibleRect(overlay.rectTransform)
                    || GetCanvasGroupAlpha(overlay.transform) < 0.95f)
                {
                    continue;
                }

                var overlayRect = GetScreenRect(overlay.rectTransform, canvas);
                if (ContainsRect(overlayRect, candidateRect))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetVisibleTextContent(Graphic graphic)
        {
            if (graphic is Text text)
            {
                return text.text;
            }

            var type = graphic.GetType();
            if (type.FullName == null || !type.FullName.StartsWith("TMPro.", System.StringComparison.Ordinal))
            {
                return string.Empty;
            }

            var textProperty = type.GetProperty("text");
            return textProperty != null ? textProperty.GetValue(graphic, null) as string : string.Empty;
        }

        private static bool ContainsRepresentativeStartupText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            for (var i = 0; i < RepresentativeStartupTexts.Length; i++)
            {
                if (text.IndexOf(RepresentativeStartupTexts[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void LogRepresentativeTextDiagnostics(GameObject rootObject, Canvas canvas)
        {
            if (rootObject == null || canvas == null)
            {
                return;
            }

            foreach (var text in rootObject.GetComponentsInChildren<Text>(true))
            {
                if (!ContainsRepresentativeStartupText(text.text))
                {
                    continue;
                }

                var screenRect = GetScreenRect(text.rectTransform, canvas);
                Debug.Log("INFO " + LogPrefix + " representative text diagnostic path=" + GetHierarchyPath(text.transform)
                    + " activeInHierarchy=" + text.gameObject.activeInHierarchy
                    + " enabled=" + text.enabled
                    + " textLength=" + (text.text != null ? text.text.Length : 0)
                    + " alpha=" + (text.color.a * GetCanvasGroupAlpha(text.transform)).ToString("0.###")
                    + " screenRect=" + FormatRect(screenRect)
                    + " intersectsViewport=" + screenRect.Overlaps(GetViewportRect(), true)
                    + " canvasRendererCull=" + (text.canvasRenderer != null && text.canvasRenderer.cull)
                    + " parent=" + (text.transform.parent != null ? text.transform.parent.name : "<none>"));
            }
        }

        private static bool IsTextGraphic(Graphic graphic)
        {
            if (graphic is Text)
            {
                return true;
            }

            var fullName = graphic.GetType().FullName;
            return fullName != null && fullName.StartsWith("TMPro.", System.StringComparison.Ordinal);
        }

        private static bool IsTmpTextGraphic(Graphic graphic)
        {
            var fullName = graphic != null ? graphic.GetType().FullName : null;
            return fullName != null && fullName.StartsWith("TMPro.", System.StringComparison.Ordinal);
        }

        private static float GetCanvasGroupAlpha(Transform transform)
        {
            var alpha = 1f;
            foreach (var group in transform.GetComponentsInParent<CanvasGroup>(false))
            {
                if (!group.enabled)
                {
                    continue;
                }

                alpha *= group.alpha;
                if (group.ignoreParentGroups)
                {
                    break;
                }
            }

            return alpha;
        }

        private static bool HasNonZeroParentScale(Transform transform)
        {
            var current = transform;
            while (current != null)
            {
                var scale = current.localScale;
                if (Mathf.Abs(scale.x) <= 0.001f || Mathf.Abs(scale.y) <= 0.001f)
                {
                    return false;
                }

                current = current.parent;
            }

            return true;
        }

        private static Rect GetScreenRect(RectTransform rectTransform, Canvas canvas)
        {
            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            var max = min;
            for (var i = 1; i < corners.Length; i++)
            {
                var point = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static Rect GetViewportRect()
        {
            return new Rect(0f, 0f, Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
        }

        private static bool ContainsRect(Rect outer, Rect inner)
        {
            return outer.xMin <= inner.xMin + 0.5f
                && outer.yMin <= inner.yMin + 0.5f
                && outer.xMax >= inner.xMax - 0.5f
                && outer.yMax >= inner.yMax - 0.5f;
        }

        private static Rect Intersect(Rect a, Rect b)
        {
            var xMin = Mathf.Max(a.xMin, b.xMin);
            var yMin = Mathf.Max(a.yMin, b.yMin);
            var xMax = Mathf.Min(a.xMax, b.xMax);
            var yMax = Mathf.Min(a.yMax, b.yMax);
            if (xMax <= xMin || yMax <= yMin)
            {
                return Rect.zero;
            }

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
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

        private static string FormatVector2(Vector2 value)
        {
            return "(" + value.x.ToString("0.##") + "," + value.y.ToString("0.##") + ")";
        }

        private static string FormatRect(Rect rect)
        {
            return "(" + rect.xMin.ToString("0.#") + "," + rect.yMin.ToString("0.#")
                + ")-(" + rect.xMax.ToString("0.#") + "," + rect.yMax.ToString("0.#") + ")";
        }

        private static string FormatColor(Color color)
        {
            return "(" + color.r.ToString("0.###") + "," + color.g.ToString("0.###") + ","
                + color.b.ToString("0.###") + "," + color.a.ToString("0.###") + ")";
        }

        private static string FormatTextSample(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "<none>";
            }

            var normalized = text.Replace('\n', ' ').Replace('\r', ' ').Trim();
            return normalized.Length <= 64 ? normalized : normalized.Substring(0, 64);
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return "<null>";
            }

            var path = transform.name;
            var current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        private static bool ValidateTopmostFullScreenGraphics()
        {
            var canvas = FindSceneObject<Canvas>();
            if (canvas == null)
            {
                return false;
            }

            var root = FindSceneObject<BootstrapRootView>();
            var content = root != null && root.rootScrollRect != null ? root.rootScrollRect.content : null;
            var graphics = new List<Graphic>(canvas.GetComponentsInChildren<Graphic>(false));
            var viewportRect = GetViewportRect();
            var logged = 0;
            var valid = true;
            for (var index = graphics.Count - 1; index >= 0 && logged < 8; index--)
            {
                var graphic = graphics[index];
                if (graphic == null || !graphic.enabled || !graphic.gameObject.activeInHierarchy || graphic.canvas == null)
                {
                    continue;
                }

                var effectiveAlpha = graphic.color.a * GetCanvasGroupAlpha(graphic.transform);
                if (effectiveAlpha <= 0.001f)
                {
                    continue;
                }

                var rect = GetScreenRect(graphic.rectTransform, canvas);
                var intersection = Intersect(rect, viewportRect);
                if (intersection.width * intersection.height < viewportRect.width * viewportRect.height * 0.8f)
                {
                    continue;
                }

                var materialName = graphic.material != null ? graphic.material.name : "<none>";
                var path = GetHierarchyPath(graphic.transform);
                Debug.Log("INFO " + LogPrefix + " topmost fullscreen graphic rank=" + (logged + 1)
                    + " path=" + path
                    + " type=" + graphic.GetType().Name
                    + " color=" + FormatColor(graphic.color)
                    + " luminance=" + CalculateLuminance(graphic.color).ToString("0.###")
                    + " alpha=" + effectiveAlpha.ToString("0.###")
                    + " siblingIndex=" + graphic.transform.GetSiblingIndex()
                    + " depth=" + GetTransformDepth(graphic.transform)
                    + " parent=" + (graphic.transform.parent != null ? graphic.transform.parent.name : "<none>")
                    + " raycastTarget=" + graphic.raycastTarget
                    + " material=" + materialName
                    + " screenRect=" + FormatRect(rect)
                    + " canvasRendererCull=" + (graphic.canvasRenderer != null && graphic.canvasRenderer.cull));

                if (IsRootViewportGraphic(graphic, root) && effectiveAlpha > 0.01f)
                {
                    Debug.LogError("ERROR " + LogPrefix + " Root Scroll Viewport Image is a visible fullscreen graphic path=" + path
                        + " alpha=" + effectiveAlpha.ToString("0.###"));
                    valid = false;
                }

                if (IsBlockingFullScreenGraphic(graphic, effectiveAlpha, root, content, graphics, index))
                {
                    Debug.LogError("ERROR " + LogPrefix + " blocking fullscreen graphic may cover startup content path=" + path
                        + " color=" + FormatColor(graphic.color)
                        + " alpha=" + effectiveAlpha.ToString("0.###")
                        + " luminance=" + CalculateLuminance(graphic.color).ToString("0.###"));
                    valid = false;
                }

                logged++;
            }

            return valid;
        }

        private static bool IsRootViewportGraphic(Graphic graphic, BootstrapRootView root)
        {
            return graphic != null
                && root != null
                && root.rootScrollRect != null
                && root.rootScrollRect.viewport != null
                && graphic.transform == root.rootScrollRect.viewport;
        }

        private static bool IsBlockingFullScreenGraphic(
            Graphic graphic,
            float effectiveAlpha,
            BootstrapRootView root,
            RectTransform content,
            List<Graphic> renderOrderedGraphics,
            int graphicIndex)
        {
            if (!(graphic is Image)
                || effectiveAlpha < 0.95f
                || CalculateLuminance(graphic.color) > 0.1f)
            {
                return false;
            }

            if (IsExpectedBackgroundGraphic(graphic, root))
            {
                return false;
            }

            return content == null || IsRenderedAfterContent(graphic, content, renderOrderedGraphics, graphicIndex);
        }

        private static bool IsExpectedBackgroundGraphic(Graphic graphic, BootstrapRootView root)
        {
            if (graphic == null || root == null || root.rootScrollRect == null)
            {
                return false;
            }

            if (graphic.transform.name != "Background" || graphic.transform.parent != root.transform)
            {
                return false;
            }

            return root.rootScrollRect.transform.parent != root.transform
                || graphic.transform.GetSiblingIndex() < root.rootScrollRect.transform.GetSiblingIndex();
        }

        private static bool IsRenderedAfterContent(Graphic graphic, RectTransform content, List<Graphic> renderOrderedGraphics, int graphicIndex)
        {
            if (graphic == null || content == null)
            {
                return true;
            }

            if (graphic.transform == content || graphic.transform.IsChildOf(content))
            {
                return false;
            }

            var firstContentGraphic = int.MaxValue;
            for (var i = 0; i < renderOrderedGraphics.Count; i++)
            {
                var candidate = renderOrderedGraphics[i];
                if (candidate != null && candidate.transform.IsChildOf(content))
                {
                    firstContentGraphic = i;
                    break;
                }
            }

            return firstContentGraphic == int.MaxValue || graphicIndex > firstContentGraphic;
        }

        private static int GetTransformDepth(Transform transform)
        {
            var depth = 0;
            var current = transform;
            while (current != null)
            {
                depth++;
                current = current.parent;
            }

            return depth;
        }

        private static RenderedFrameAnalysis AnalyzeRenderedFrame(Texture2D texture, string captureMethod, int attempt)
        {
            var pixels = texture.GetPixels32();
            if (pixels == null || pixels.Length == 0)
            {
                return new RenderedFrameAnalysis(captureMethod, attempt, texture.width, texture.height, 0, 0f, 0f, 0f);
            }

            var stride = Mathf.Max(1, pixels.Length / 20000);
            var sampled = 0;
            var nonBlack = 0;
            double luminanceSum = 0d;
            var brightest = 0f;
            for (var i = 0; i < pixels.Length; i += stride)
            {
                var color = pixels[i];
                var luminance = (0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b) / 255f;
                luminanceSum += luminance;
                brightest = Mathf.Max(brightest, luminance);
                if (luminance >= MinimumVisibleLuminance)
                {
                    nonBlack++;
                }

                sampled++;
            }

            var averageLuminance = sampled > 0 ? (float)(luminanceSum / sampled) : 0f;
            var nonBlackPixelRatio = sampled > 0 ? (float)nonBlack / sampled : 0f;
            return new RenderedFrameAnalysis(
                captureMethod,
                attempt,
                texture.width,
                texture.height,
                sampled,
                averageLuminance,
                nonBlackPixelRatio,
                brightest);
        }

        private static float CalculateLuminance(Color color)
        {
            return 0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b;
        }

        private static Texture2D CaptureScreenshotAsTexture()
        {
            var screenCaptureType = System.Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule");
            var captureMethod = screenCaptureType != null
                ? screenCaptureType.GetMethod(
                    "CaptureScreenshotAsTexture",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                    null,
                    System.Type.EmptyTypes,
                    null)
                : null;
            return captureMethod != null ? captureMethod.Invoke(null, null) as Texture2D : null;
        }

        private static T FindSceneObject<T>() where T : Component
        {
            var activeScene = SceneManager.GetActiveScene();
            foreach (var component in Resources.FindObjectsOfTypeAll<T>())
            {
                if (component != null && component.gameObject.scene == activeScene)
                {
                    return component;
                }
            }

            return null;
        }

        private static List<T> FindSceneObjects<T>() where T : Component
        {
            var results = new List<T>();
            var activeScene = SceneManager.GetActiveScene();
            foreach (var component in Resources.FindObjectsOfTypeAll<T>())
            {
                if (component != null && component.gameObject.scene == activeScene)
                {
                    results.Add(component);
                }
            }

            return results;
        }

        private static int CountActiveSceneObjects<T>(List<T> components) where T : Component
        {
            if (components == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < components.Count; i++)
            {
                var component = components[i];
                if (component != null && component.gameObject.activeInHierarchy)
                {
                    count++;
                }
            }

            return count;
        }

        private static void DestroySceneObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            target.SetActive(false);
#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
#else
            Destroy(target);
#endif
        }

        private static void DestroySceneObjectImmediate(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            target.SetActive(false);
            DestroyImmediate(target);
        }

        private static void DestroySceneComponent(Component target)
        {
            if (target == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
#else
            Destroy(target);
#endif
        }

        private static GameObject FindSceneGameObject(string objectName)
        {
            var activeScene = SceneManager.GetActiveScene();
            foreach (var transform in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (transform != null && transform.gameObject.scene == activeScene && transform.name == objectName)
                {
                    return transform.gameObject;
                }
            }

            return null;
        }

        private static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private readonly struct StartupResult
        {
            public StartupResult(bool shouldComplete, string completionMessage)
            {
                ShouldComplete = shouldComplete;
                CompletionMessage = completionMessage;
            }

            public bool ShouldComplete { get; }
            public string CompletionMessage { get; }
        }

        private struct VisibleUiValidationResult
        {
            public int VisibleCandidateCount { get; set; }
            public int VisibleGraphicCount { get; set; }
            public int VisibleTextCount { get; set; }
            public int VisibleTmpTextCount { get; set; }
            public int VisibleImageCount { get; set; }
            public int VisibleButtonCount { get; set; }
            public int VisiblePanelImageCount { get; set; }
            public bool HasRepresentativeStartupText { get; set; }
            public int RepresentativeTextVisibleCount { get; set; }
        }

        private readonly struct VisibleUiCandidate
        {
            public VisibleUiCandidate(
                string path,
                string name,
                string typeName,
                Rect localRect,
                Rect screenRect,
                float alpha,
                int textLength,
                string textContent,
                Color color,
                float expectedLuminance,
                bool canvasRendererCull)
            {
                Path = path;
                Name = name;
                TypeName = typeName;
                LocalRect = localRect;
                ScreenRect = screenRect;
                Alpha = alpha;
                TextLength = textLength;
                TextContent = textContent ?? string.Empty;
                Color = color;
                ExpectedLuminance = expectedLuminance;
                CanvasRendererCull = canvasRendererCull;
            }

            public string Path { get; }
            public string Name { get; }
            public string TypeName { get; }
            public Rect LocalRect { get; }
            public Rect ScreenRect { get; }
            public float Alpha { get; }
            public int TextLength { get; }
            public string TextContent { get; }
            public Color Color { get; }
            public float ExpectedLuminance { get; }
            public bool CanvasRendererCull { get; }
        }

        private readonly struct RenderedFrameAnalysis
        {
            public static RenderedFrameAnalysis Empty => new RenderedFrameAnalysis("none", 0, 0, 0, 0, 0f, 0f, 0f);

            public RenderedFrameAnalysis(
                string captureMethod,
                int attempt,
                int width,
                int height,
                int sampledPixelCount,
                float averageLuminance,
                float nonBlackPixelRatio,
                float brightestPixelLuminance)
            {
                CaptureMethod = captureMethod;
                Attempt = attempt;
                Width = width;
                Height = height;
                SampledPixelCount = sampledPixelCount;
                AverageLuminance = averageLuminance;
                NonBlackPixelRatio = nonBlackPixelRatio;
                BrightestPixelLuminance = brightestPixelLuminance;
            }

            public string CaptureMethod { get; }
            public int Attempt { get; }
            public int Width { get; }
            public int Height { get; }
            public int SampledPixelCount { get; }
            public float AverageLuminance { get; }
            public float NonBlackPixelRatio { get; }
            public float BrightestPixelLuminance { get; }
        }
    }
}
