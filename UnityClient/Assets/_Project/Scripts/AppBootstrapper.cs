using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
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
        private const string LogPrefix = "[TokenForgeBootstrap]";
        private const string BootstrapRootPrefabPath = "Assets/_Project/Prefabs/UI/BootstrapRoot.prefab";
        private const float MinimumCanvasGroupAlpha = 0.01f;
        private const float MinimumVisibleLuminance = 0.1f;
        private const float MinimumRenderedNonBlackPixelRatio = 0.003f;
        private const int MaximumVisibilityDiagnosticsPerPass = 12;

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
        public bool IsVisibleUiValidated { get; private set; }
        public bool RequirePrefabUi => true;
        public bool LoadAuthSessionOnStart => loadAuthSessionOnStart;
        public bool RunSafeSmokeFlowWhenEmpty => runSafeSmokeFlowWhenEmpty;
        public int LastViewportVisibleCandidateCount { get; private set; }
        public float LastRenderedFrameAverageLuminance { get; private set; }
        public float LastRenderedFrameNonBlackPixelRatio { get; private set; }

        private void Awake()
        {
            Debug.Log("INFO " + LogPrefix + " TokenForge bootstrap starting.");
            EnsureSceneInfrastructure();
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

            if (bootstrapRoot != null)
            {
                bootstrapRoot.Bind(localStatus, approvedActivityAnalysis);
                if (bootstrapRoot.ValidateReferences(out var error))
                {
                    if (ValidateVisibleUiTree())
                    {
                        Debug.Log("INFO " + LogPrefix + " TokenForge bootstrap UI ready.");
                    }
                }
                else
                {
                    Debug.LogError("ERROR " + LogPrefix + " BootstrapRootView serialized reference validation failed: " + error);
                }
            }
            else
            {
                Debug.LogError("ERROR " + LogPrefix + " BootstrapRootView missing");
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
                Debug.LogError("ERROR " + LogPrefix + " BootstrapRootView missing");
            }
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
            if (canvasRect != null && (canvasRect.rect.width <= 0.5f || canvasRect.rect.height <= 0.5f))
            {
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
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GetOrAddComponent<GraphicRaycaster>(canvasObject);
            return canvasObject;
        }

        private BootstrapRootView EnsureBootstrapRoot(GameObject canvasObject)
        {
            var root = FindSceneObject<BootstrapRootView>();
            if (root != null)
            {
                NormalizeBootstrapRoot(root.gameObject, canvasObject);
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
                Debug.LogWarning("WARN " + LogPrefix + " TokenForge BootstrapRoot.prefab is unavailable. Prefab UI is the only supported startup path.");
                return null;
            }

            var instance = Instantiate(prefab, canvasObject.transform, false);
            instance.name = "BootstrapRoot";
            NormalizeBootstrapRoot(instance, canvasObject);
            instance.SetActive(true);

            return instance.GetComponent<BootstrapRootView>();
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
        }

        public bool ValidateVisibleUiTree()
        {
            Canvas.ForceUpdateCanvases();
            if (bootstrapRoot != null)
            {
                var rootRect = bootstrapRoot.GetComponent<RectTransform>();
                if (rootRect != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
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

            var mainPanelActive = root.rootScrollRect != null && root.rootScrollRect.gameObject.activeInHierarchy;
            if (!mainPanelActive)
            {
                Debug.LogError("ERROR " + LogPrefix + " Main panel inactive");
                valid = false;
            }

            var panelCount = CountActivePanels(root);
            var visibility = CountViewportVisibleCandidates(root.gameObject, canvas);
            var visibleGraphicCount = visibility.VisibleGraphicCount;
            var visibleTextCount = visibility.VisibleTextCount;
            LastViewportVisibleCandidateCount = visibility.VisibleCandidateCount;

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
            if (!ValidateVisibleUiTree())
            {
                Debug.LogError("ERROR " + LogPrefix + " visible UI validation failed; bootstrap completion suppressed.");
                return;
            }

            if (!ValidateRenderedFrameSmoke())
            {
                Debug.LogError("ERROR " + LogPrefix + " rendered frame smoke failed; bootstrap completion suppressed.");
                return;
            }

            Debug.Log("INFO " + LogPrefix + " " + message);
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

        public bool ValidateRenderedFrameSmoke()
        {
            try
            {
                var averageLuminance = 0f;
                var nonBlackPixelRatio = 0f;
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
                        AnalyzeRenderedFrame(texture, out averageLuminance, out nonBlackPixelRatio);
                        captured = true;
                        if (nonBlackPixelRatio >= MinimumRenderedNonBlackPixelRatio)
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

                if (nonBlackPixelRatio < MinimumRenderedNonBlackPixelRatio && Application.isBatchMode)
                {
                    Debug.LogWarning("WARN " + LogPrefix + " ScreenCapture returned a black frame in batchmode; trying canvas render fallback.");
                    if (TryCaptureCanvasRenderFallback(out var fallbackAverageLuminance, out var fallbackNonBlackPixelRatio))
                    {
                        averageLuminance = fallbackAverageLuminance;
                        nonBlackPixelRatio = fallbackNonBlackPixelRatio;
                    }
                }

                LastRenderedFrameAverageLuminance = averageLuminance;
                LastRenderedFrameNonBlackPixelRatio = nonBlackPixelRatio;

                if (nonBlackPixelRatio < MinimumRenderedNonBlackPixelRatio)
                {
                    Debug.LogError("ERROR " + LogPrefix + " rendered frame appears black nonBlackPixelRatio="
                        + nonBlackPixelRatio.ToString("0.000000")
                        + " averageLuminance=" + averageLuminance.ToString("0.000000"));
                    return false;
                }

                Debug.Log("INFO " + LogPrefix + " rendered frame smoke passed nonBlackPixelRatio="
                    + nonBlackPixelRatio.ToString("0.000000")
                    + " averageLuminance=" + averageLuminance.ToString("0.000000"));
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
                        Debug.LogWarning("WARN " + LogPrefix + " ignored graphic reason=" + ignoredReason
                            + " name=" + (graphic != null ? graphic.name : "null"));
                        warningCount++;
                    }

                    continue;
                }

                result.VisibleCandidateCount++;
                result.VisibleGraphicCount++;
                if (candidate.TextLength > 0)
                {
                    result.VisibleTextCount++;
                }

                if (candidateLogCount < MaximumVisibilityDiagnosticsPerPass)
                {
                    Debug.Log("INFO " + LogPrefix + " visible candidate name=" + candidate.Name
                        + " type=" + candidate.TypeName
                        + " rect=" + FormatRect(candidate.ScreenRect)
                        + " alpha=" + candidate.Alpha.ToString("0.###")
                        + " textLength=" + candidate.TextLength);
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

            var textLength = GetVisibleTextLength(graphic);
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
                graphic.name,
                graphic.GetType().Name,
                screenRect,
                graphic.color.a * canvasGroupAlpha,
                textLength);
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

        private static int GetVisibleTextLength(Graphic graphic)
        {
            if (graphic is Text text)
            {
                return string.IsNullOrWhiteSpace(text.text) ? 0 : text.text.Trim().Length;
            }

            var type = graphic.GetType();
            if (type.FullName == null || !type.FullName.StartsWith("TMPro.", System.StringComparison.Ordinal))
            {
                return 0;
            }

            var textProperty = type.GetProperty("text");
            var value = textProperty != null ? textProperty.GetValue(graphic, null) as string : null;
            return string.IsNullOrWhiteSpace(value) ? 0 : value.Trim().Length;
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

        private static string FormatRect(Rect rect)
        {
            return "(" + rect.xMin.ToString("0.#") + "," + rect.yMin.ToString("0.#")
                + ")-(" + rect.xMax.ToString("0.#") + "," + rect.yMax.ToString("0.#") + ")";
        }

        private static void AnalyzeRenderedFrame(Texture2D texture, out float averageLuminance, out float nonBlackPixelRatio)
        {
            var pixels = texture.GetPixels32();
            if (pixels == null || pixels.Length == 0)
            {
                averageLuminance = 0f;
                nonBlackPixelRatio = 0f;
                return;
            }

            var stride = Mathf.Max(1, pixels.Length / 20000);
            var sampled = 0;
            var nonBlack = 0;
            double luminanceSum = 0d;
            for (var i = 0; i < pixels.Length; i += stride)
            {
                var color = pixels[i];
                var luminance = (0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b) / 255f;
                luminanceSum += luminance;
                if (luminance >= MinimumVisibleLuminance)
                {
                    nonBlack++;
                }

                sampled++;
            }

            averageLuminance = sampled > 0 ? (float)(luminanceSum / sampled) : 0f;
            nonBlackPixelRatio = sampled > 0 ? (float)nonBlack / sampled : 0f;
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

        private static bool TryCaptureCanvasRenderFallback(out float averageLuminance, out float nonBlackPixelRatio)
        {
            averageLuminance = 0f;
            nonBlackPixelRatio = 0f;
            var canvas = FindSceneObject<Canvas>();
            if (canvas == null)
            {
                return false;
            }

            var originalRenderMode = canvas.renderMode;
            var originalCamera = canvas.worldCamera;
            var originalPlaneDistance = canvas.planeDistance;
            var renderTexture = new RenderTexture(640, 360, 24, RenderTextureFormat.ARGB32);
            var previousActive = RenderTexture.active;
            Texture2D texture = null;
            GameObject cameraObject = null;
            try
            {
                cameraObject = new GameObject("TokenForge Render Smoke Camera", typeof(Camera));
                cameraObject.hideFlags = HideFlags.HideAndDontSave;
                var camera = cameraObject.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.cullingMask = -1;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 100f;
                camera.targetTexture = renderTexture;
                cameraObject.transform.position = new Vector3(0f, 0f, -10f);
                cameraObject.transform.rotation = Quaternion.identity;

                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10f;
                Canvas.ForceUpdateCanvases();

                camera.Render();
                RenderTexture.active = renderTexture;
                texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                texture.Apply(false);
                AnalyzeRenderedFrame(texture, out averageLuminance, out nonBlackPixelRatio);
                return nonBlackPixelRatio >= MinimumRenderedNonBlackPixelRatio;
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("WARN " + LogPrefix + " canvas render fallback failed: " + exception.Message);
                return false;
            }
            finally
            {
                canvas.renderMode = originalRenderMode;
                canvas.worldCamera = originalCamera;
                canvas.planeDistance = originalPlaneDistance;
                RenderTexture.active = previousActive;
                if (texture != null)
                {
                    Destroy(texture);
                }

                renderTexture.Release();
                Destroy(renderTexture);
                if (cameraObject != null)
                {
                    Destroy(cameraObject);
                }

                Canvas.ForceUpdateCanvases();
            }
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
        }

        private readonly struct VisibleUiCandidate
        {
            public VisibleUiCandidate(string name, string typeName, Rect screenRect, float alpha, int textLength)
            {
                Name = name;
                TypeName = typeName;
                ScreenRect = screenRect;
                Alpha = alpha;
                TextLength = textLength;
            }

            public string Name { get; }
            public string TypeName { get; }
            public Rect ScreenRect { get; }
            public float Alpha { get; }
            public int TextLength { get; }
        }
    }
}
