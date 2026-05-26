using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TokenForge.Client.Agents;
using TokenForge.Client.Auth;
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
            "Sync optional",
            "Start Game",
            "Add Repository",
            "Connect Codex Agent",
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

        private void Awake()
        {
            Debug.Log("INFO " + LogPrefix + " TokenForge bootstrap starting.");
            LogStartupScene();
            LogHierarchyDump("AwakeStart");
            lifecycleService = new MacApplicationLifecycleService();
            lifecycleService.Install();
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
                ApplyNativeShellState(showDashboardIfNeeded: true);
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
        }

        private IEnumerator Start()
        {
            var startupTask = RunStartupAsync();
            while (!startupTask.IsCompleted)
            {
                yield return null;
            }

            if (startupTask.IsFaulted)
            {
                var exception = startupTask.Exception != null ? startupTask.Exception.GetBaseException() : null;
                Debug.LogError("ERROR " + LogPrefix + " startup task failed: " + (exception != null ? exception.Message : "unknown error"));
                yield break;
            }

            var result = startupTask.Result;
            if (!result.ShouldComplete)
            {
                Debug.LogWarning("WARN " + LogPrefix + " TokenForge safe bootstrap could not complete.");
                yield break;
            }

            for (var frame = 0; frame < 10; frame++)
            {
                yield return null;
            }

            if (Application.isBatchMode)
            {
                yield return null;
            }
            else
            {
                yield return new WaitForEndOfFrame();
            }

            CompleteBootstrap(result.CompletionMessage);
        }

        private async Task<StartupResult> RunStartupAsync()
        {
            if (!runSafeSmokeFlowWhenEmpty && bootstrapSyncMode == BootstrapSyncMode.None)
            {
                await RefreshDashboardAsync(loadAuthSessionOnStart);
                return new StartupResult(true, "TokenForge bootstrap completed.");
            }

            var result = await new BootstrapSmokeFlow(repository, privacySanitizer, null, syncService, backendSyncSmokeFlow)
                .RunIfEmptyThenOptionalSyncAsync(runSafeSmokeFlowWhenEmpty, bootstrapSyncMode);
            if (result.IsSuccess)
            {
                if (approvedActivityAnalysis != null)
                {
                    await RefreshDashboardAsync(loadAuthSessionOnStart);
                }

                return new StartupResult(true, "TokenForge safe bootstrap completed.");
            }

            return new StartupResult(false, string.Empty);
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

            if (UseNativeMacDashboardShell)
            {
                ApplyNativeShellState(showDashboardIfNeeded: false);
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

            if (nativeDashboardService == null)
            {
                nativeDashboardService = new MacNativeDashboardService();
                nativeDashboardService.ActionRequested -= HandleNativeDashboardAction;
                nativeDashboardService.ActionRequested += HandleNativeDashboardAction;
                nativeDashboardService.Install();
            }

            if (nativeDesktopCompanionController == null)
            {
                var controllerObject = new GameObject("NativeDesktopCompanionController");
                DontDestroyOnLoad(controllerObject);
                nativeDesktopCompanionController = controllerObject.AddComponent<DesktopCompanionOverlayController>();
                nativeDesktopCompanionController.Initialize(null, lifecycleService);
                nativeDesktopCompanionController.PositionChanged -= HandleNativeCompanionPositionChanged;
                nativeDesktopCompanionController.PositionChanged += HandleNativeCompanionPositionChanged;
                nativeDesktopCompanionController.DashboardRestoreRequested -= HandleNativeCompanionDashboardRestoreRequested;
                nativeDesktopCompanionController.DashboardRestoreRequested += HandleNativeCompanionDashboardRestoreRequested;
            }
        }

        private void HandleNativeCompanionDashboardRestoreRequested()
        {
            nativeDashboardService?.ToggleDashboardWindow();
        }

        private void HandleNativeCompanionPositionChanged(Vector2 position)
        {
            if (approvedActivityAnalysis == null)
            {
                return;
            }

            _ = SaveNativeCompanionPositionAsync(position);
        }

        private async Task SaveNativeCompanionPositionAsync(Vector2 position)
        {
            await approvedActivityAnalysis.SaveDesktopCompanionPositionAsync(position.x, position.y);
            ApplyNativeShellState(showDashboardIfNeeded: false);
        }

        private void ApplyNativeShellState(bool showDashboardIfNeeded)
        {
            if (!UseNativeMacDashboardShell)
            {
                return;
            }

            EnsureNativeDashboardShell();
            var state = BuildNativeDashboardState();
            nativeDashboardService.UpdateDashboardState(state);
            nativeDashboardService.SetMenuBarStatus(state);
            nativeDesktopCompanionController?.ApplySettings(
                approvedActivityAnalysis?.CharacterDashboard?.DesktopCompanionSettings ?? DesktopCompanionSettings.CreateDefault(),
                approvedActivityAnalysis?.CharacterDashboard?.CompanionState ?? CompanionState.CreateDefault());

            if (showDashboardIfNeeded && !nativeDashboardShown)
            {
                nativeDashboardShown = true;
                nativeDashboardService.ShowDashboardWindow();
            }
        }

        private NativeDashboardState BuildNativeDashboardState()
        {
            var dashboard = approvedActivityAnalysis?.CharacterDashboard ?? new CharacterDashboardSummary();
            var companion = dashboard.CompanionState ?? CompanionState.CreateDefault();
            var settings = dashboard.DesktopCompanionSettings ?? DesktopCompanionSettings.CreateDefault();
            var codexConnected = approvedActivityAnalysis != null
                && approvedActivityAnalysis.Onboarding.AgentSources.Any(source => source.SourceType == ConnectedAgentSourceType.Codex && source.Selected);
            var repositoryConnected = approvedActivityAnalysis != null
                && (approvedActivityAnalysis.Onboarding.GitConnected || !string.IsNullOrWhiteSpace(dashboard.CurrentRepositoryHash));

            var state = NativeDashboardState.CreateDefault();
            state.connection = "local";
            state.sync = approvedActivityAnalysis != null && approvedActivityAnalysis.AuthState == AuthState.LoggedIn ? "connected" : "optional";
            state.appTitle = "TokenForge";
            state.subtitle = "Turn your development activity into companion growth.";
            state.isLocalMode = true;
            state.syncStatusText = state.sync == "connected" ? "Safe sync connected" : "Sync optional";
            state.selectedNavItem = "dashboard";
            state.primaryActionEnabled = true;
            state.pendingReviewCount = (gitAnalysisFlow != null && gitAnalysisFlow.HasPendingReview ? 1 : 0)
                + (agentAnalysisFlow != null && agentAnalysisFlow.HasPendingReview ? 1 : 0)
                + (approvedActivityAnalysis?.PendingNativeActivityReview != null && gitAnalysisFlow?.HasPendingReview != true && agentAnalysisFlow?.HasPendingReview != true ? 1 : 0);
            state.warningCount = Math.Max(0, (approvedActivityAnalysis?.RetryQueueSummary?.PendingCount ?? 0) + (approvedActivityAnalysis?.TombstoneSummary?.PendingDeleteCount ?? 0));
            state.lastRunSummary = SafeNativeText(dashboard.LatestSafeSessionSummary, "No saved growth yet. Run Analysis on a repository or AI agent log to generate your first XP.");
            state.codeStat = Math.Max(0, dashboard.Code);
            state.focusStat = Math.Max(0, dashboard.Focus);
            state.debugStat = Math.Max(0, dashboard.Debug);
            state.designStat = Math.Max(0, dashboard.Design);
            state.syncStat = Math.Max(0, dashboard.Sync);
            state.companionVisible = settings.IsDesktopCompanionEnabled;
            state.wanderEnabled = settings.MotionMode != CompanionDesktopMotionMode.Calm;
            state.clickReactionEnabled = !settings.IsClickThroughEnabled;
            state.companion.name = string.IsNullOrWhiteSpace(dashboard.CharacterName) ? "Token" : dashboard.CharacterName;
            state.companion.stage = companion.Stage.ToString();
            state.companion.stageIndex = (int)companion.Stage;
            state.companion.level = Math.Max(1, companion.Level);
            state.companion.xp = Math.Max(0, dashboard.CurrentLevelExp);
            state.companion.xpToNextLevel = Math.Max(1, companion.XpToNextStage);
            state.companion.mood = settings.IsDesktopCompanionEnabled ? "active" : "hidden";
            state.companion.skin = string.IsNullOrWhiteSpace(settings.VisualThemeId) ? "orange_cat" : settings.VisualThemeId;
            state.repository.connected = repositoryConnected;
            state.repository.name = repositoryConnected ? SafeNativeText(dashboard.CurrentRepositoryAlias, "Local Repository") : string.Empty;
            state.repository.status = repositoryConnected ? "local_connected" : "not_selected";
            state.repository.statusText = repositoryConnected ? "Connected locally" : "Not selected";
            state.codexAgent.connected = codexConnected;
            state.codexAgent.status = codexConnected ? "connected_locally" : AgentCodexStatus();
            state.codexAgent.statusText = codexConnected ? "Connected locally" : AgentCodexStatusText();
            state.activity.todaySummary = SafeNativeText(dashboard.LatestSafeSessionSummary, "No activity yet");
            state.activity.state = state.pendingReviewCount > 0 ? "Pending review" : dashboard.HasSavedRun ? "Saved" : "No pending review";
            state.activity.code = Math.Max(0, dashboard.Code);
            state.activity.focus = Math.Max(0, dashboard.Focus);
            state.activity.debug = Math.Max(0, dashboard.Debug);
            state.activity.design = Math.Max(0, dashboard.Design);
            state.activity.sync = Math.Max(0, dashboard.Sync);
            ApplyPendingReviewState(state, approvedActivityAnalysis?.PendingNativeActivityReview);
            state.statusText = "Cdx " + state.activity.code + "% · CI " + state.activity.focus + "% · Gem " + state.activity.design + "%";
            return state;
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
                case AgentSourceSetupState.PermissionRequired:
                    return "needs_folder_access";
                case AgentSourceSetupState.ManualImportRequired:
                    return "no_activity_found";
                case AgentSourceSetupState.AnalysisFailedSafely:
                    return "unsupported";
                case AgentSourceSetupState.ReadyToAnalyze:
                case AgentSourceSetupState.AnalysisComplete:
                    return "connected_locally";
                default:
                    return "not_connected";
            }
        }

        private string AgentCodexStatusText()
        {
            switch (AgentCodexStatus())
            {
                case "needs_folder_access":
                    return "Needs folder access";
                case "no_activity_found":
                    return "Unsupported / No activity found";
                case "unsupported":
                    return "Unsupported / No activity found";
                case "connected_locally":
                    return "Connected locally";
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
            state.review.pending = true;
            state.review.summary = SafeNativeText(pending.SafeSummary, "Aggregate activity ready for review.");
            state.review.source = pending.SourceKind;
            state.review.confidence = pending.Confidence;
            state.review.estimatedXpDelta = Math.Max(0, pending.EstimatedXpDelta);
            state.review.codeDelta = Math.Max(0, deltas.Logic + deltas.Architecture + deltas.Velocity);
            state.review.focusDelta = Math.Max(0, deltas.Efficiency + deltas.Stability);
            state.review.debugDelta = Math.Max(0, deltas.Debug);
            state.review.designDelta = Math.Max(0, deltas.Design + deltas.Creativity);
            state.review.syncDelta = 0;
            state.review.warnings = string.Join(", ", (pending.WarningIds ?? new List<string>()).Take(3));
            state.activity.state = "Pending review";
            state.activity.todaySummary = state.review.summary;
            state.lastRunSummary = state.review.summary + " Approve to apply +" + state.review.estimatedXpDelta + " XP.";
        }

        private void HandleNativeDashboardAction(NativeDashboardActionRequest request)
        {
            if (request == null)
            {
                return;
            }

            Debug.Log("INFO [NativeDashboard] action received action=" + request.Action + " raw=" + request.RawAction);
            switch (request.Action)
            {
                case NativeDashboardAction.Dashboard:
                case NativeDashboardAction.ShowDashboard:
                    nativeDashboardService?.ShowDashboardWindow();
                    break;
                case NativeDashboardAction.ToggleDashboard:
                    nativeDashboardService?.ToggleDashboardWindow();
                    break;
                case NativeDashboardAction.HideDashboard:
                    nativeDashboardService?.HideDashboardWindow();
                    break;
                case NativeDashboardAction.Settings:
                    nativeDashboardService?.ShowSettingsWindow();
                    break;
                case NativeDashboardAction.Activity:
                    _ = RefreshAndPublishNativeDashboardAsync();
                    break;
                case NativeDashboardAction.RunAnalysis:
                    _ = RunNativeAnalysisAsync();
                    break;
                case NativeDashboardAction.ConnectRepository:
                case NativeDashboardAction.ChangeRepository:
                case NativeDashboardAction.Repository:
                    _ = ConnectRepositoryFromNativeAsync();
                    break;
                case NativeDashboardAction.ConnectCodexAgent:
                case NativeDashboardAction.CodexAgent:
                    _ = ConnectCodexFromNativeAsync();
                    break;
                case NativeDashboardAction.SelectCodexLogFolder:
                    _ = SelectCodexLogFolderFromNativeAsync();
                    break;
                case NativeDashboardAction.ReviewActivity:
                    _ = ReviewNativeActivityAsync();
                    break;
                case NativeDashboardAction.ApproveReview:
                    _ = ApproveNativeReviewAsync();
                    break;
                case NativeDashboardAction.DiscardReview:
                    _ = DiscardNativeReviewAsync();
                    break;
                case NativeDashboardAction.ToggleCompanionVisible:
                    _ = SetCompanionVisibleFromNativeAsync(request.BoolValue(!(approvedActivityAnalysis?.CharacterDashboard?.DesktopCompanionSettings?.IsDesktopCompanionEnabled ?? true)));
                    break;
                case NativeDashboardAction.SetWanderEnabled:
                    _ = SetWanderEnabledFromNativeAsync(request.BoolValue(true));
                    break;
                case NativeDashboardAction.SetClickReactionEnabled:
                    _ = SetClickReactionEnabledFromNativeAsync(request.BoolValue(true));
                    break;
                case NativeDashboardAction.ResetCompanionPosition:
                    _ = ResetCompanionPositionFromNativeAsync();
                    break;
                case NativeDashboardAction.ChangeCompanionSkin:
                    _ = SetCompanionSkinFromNativeAsync(request.Value);
                    break;
                case NativeDashboardAction.SetLaunchAtLogin:
                    Debug.Log("INFO [NativeDashboard] launch at login requested value=" + request.Value + " status=comingSoon");
                    _ = RefreshAndPublishNativeDashboardAsync();
                    break;
                case NativeDashboardAction.Homepage:
                    Application.OpenURL("https://github.com/HwangSeokBeom/TokenForge");
                    break;
                case NativeDashboardAction.ReportIssue:
                    Application.OpenURL("https://github.com/HwangSeokBeom/TokenForge/issues");
                    break;
                case NativeDashboardAction.Quit:
                    lifecycleService?.Quit();
                    break;
                case NativeDashboardAction.ResetLocalState:
                    Debug.Log("INFO [NativeDashboard] reset local state requested status=manualRequired");
                    nativeDashboardService?.ShowDashboardWindow();
                    break;
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

            await approvedActivityAnalysis.SelectLocalGitRepositoryForOnboardingAsync();
            await RefreshAndPublishNativeDashboardAsync();
        }

        private async Task ConnectCodexFromNativeAsync()
        {
            if (approvedActivityAnalysis == null)
            {
                return;
            }

            var result = await approvedActivityAnalysis.DetectAgentSourceForOnboardingAsync(ConnectedAgentSourceType.Codex);
            if (!result.IsSuccess)
            {
                Debug.Log("WARN [NativeDashboard] Codex local auto-detect unavailable; requesting local folder selection only category=" + result.ErrorCode);
                await approvedActivityAnalysis.SelectManualAgentLogForOnboardingAsync(ConnectedAgentSourceType.Codex);
            }

            await RefreshAndPublishNativeDashboardAsync();
        }

        private async Task SelectCodexLogFolderFromNativeAsync()
        {
            if (approvedActivityAnalysis == null)
            {
                return;
            }

            await approvedActivityAnalysis.SelectManualAgentLogForOnboardingAsync(ConnectedAgentSourceType.Codex);
            await RefreshAndPublishNativeDashboardAsync();
        }

        private async Task RunNativeAnalysisAsync()
        {
            if (approvedActivityAnalysis == null)
            {
                return;
            }

            if (gitAnalysisFlow != null && gitAnalysisFlow.HasSelectedRepositoryForLocalOnlyApproval)
            {
                await approvedActivityAnalysis.AnalyzeGitActivityAsync();
            }
            else if (agentAnalysisFlow != null && agentAnalysisFlow.HasSelectedAgentLogLocationForLocalOnlyApproval)
            {
                await approvedActivityAnalysis.AnalyzeAgentActivityAsync();
            }
            else
            {
                Debug.Log("INFO [NativeDashboard] runAnalysis requires repository or Codex connection");
            }

            await RefreshAndPublishNativeDashboardAsync();
        }

        private async Task ApproveNativeReviewAsync()
        {
            if (approvedActivityAnalysis == null)
            {
                return;
            }

            var result = await approvedActivityAnalysis.ApprovePendingNativeReviewAsync();
            if (!result.IsSuccess)
            {
                Debug.LogWarning("WARN [NativeDashboard] approveReview failed category=" + result.ErrorCode);
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
            }

            await RefreshAndPublishNativeDashboardAsync();
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

        private async Task SetCompanionVisibleFromNativeAsync(bool visible)
        {
            if (approvedActivityAnalysis != null)
            {
                await approvedActivityAnalysis.SetDesktopCompanionEnabledAsync(visible);
            }

            nativeDashboardService?.SetCompanionVisible(visible);
            await RefreshAndPublishNativeDashboardAsync();
        }

        private async Task SetWanderEnabledFromNativeAsync(bool enabled)
        {
            if (approvedActivityAnalysis != null)
            {
                await approvedActivityAnalysis.SetDesktopCompanionMotionModeAsync(enabled ? CompanionDesktopMotionMode.Normal : CompanionDesktopMotionMode.Calm);
            }

            await RefreshAndPublishNativeDashboardAsync();
        }

        private async Task SetClickReactionEnabledFromNativeAsync(bool enabled)
        {
            if (approvedActivityAnalysis != null)
            {
                await approvedActivityAnalysis.SetDesktopCompanionClickThroughAsync(!enabled);
            }

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
            LogRepresentativeTextState(root.transform, "Start Game/Label");
            LogRepresentativeTextState(root.transform, "Connect Codex Agent/Label");
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
                ApplyNativeShellState(showDashboardIfNeeded: true);
                IsVisibleUiValidated = nativeDashboardService != null && nativeDashboardService.IsAvailable;
                IsRenderedFrameSmokeSkippedForBatchMode = true;
                IsBootstrapComplete = IsVisibleUiValidated;
                Debug.Log("INFO " + LogPrefix + " native macOS dashboard shell ready; Unity uGUI dashboard validation skipped.");
                Debug.Log("INFO " + LogPrefix + " " + message);
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
                return;
            }

            if (!ValidateRenderedFrameSmoke())
            {
                Debug.LogError("ERROR " + LogPrefix + " rendered frame smoke failed; bootstrap completion suppressed.");
                return;
            }

            Debug.Log("INFO " + LogPrefix + " " + message);
            IsBootstrapComplete = true;
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
