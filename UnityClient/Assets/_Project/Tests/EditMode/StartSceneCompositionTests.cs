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
            var scaler = canvas.GetComponent<CanvasScaler>();
            Assert.IsNotNull(scaler, "CanvasScaler is missing.");
            Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);
            Assert.AreEqual(new Vector2(1280f, 720f), scaler.referenceResolution);
            Assert.AreEqual(CanvasScaler.ScreenMatchMode.MatchWidthOrHeight, scaler.screenMatchMode);
            Assert.AreEqual(0.5f, scaler.matchWidthOrHeight);
            Assert.IsNotNull(canvas.GetComponent<GraphicRaycaster>(), "GraphicRaycaster is missing.");
            AssertResolvableRect(canvas.GetComponent<RectTransform>(), "Canvas");
            Assert.IsNotNull(bootstrapper, "AppBootstrapper is missing.");
            Assert.IsNotNull(root, "BootstrapRootView is missing.");
            Assert.IsTrue(root.gameObject.activeSelf, "BootstrapRoot is inactive.");
            Assert.IsTrue(root.ValidateReferences(out var error), error);
            AssertResolvableRect(root.GetComponent<RectTransform>(), "BootstrapRoot");
            Assert.IsTrue(root.startScreenRoot != null && root.startScreenRoot.activeSelf, "Start Screen root is inactive.");
            Assert.IsNotNull(root.startGameButton, "Start Game button is missing.");
            Assert.IsTrue(root.startGameButton.interactable, "Start Game button must be interactable.");
            Assert.IsNotNull(root.gameDashboardRoot, "Game Dashboard root is missing.");
            Assert.IsNotNull(root.runAnalysisRoot, "Run Analysis root is missing.");
            Assert.IsNotNull(root.developerDiagnosticsRoot, "Developer Diagnostics root is missing.");
            Assert.IsFalse(root.settingsAdvancedRoot.activeSelf, "Settings/Advanced must not be the default first screen.");
            Assert.AreEqual(canvas.transform, root.transform.parent, "BootstrapRoot must be saved under Canvas.");
            Assert.AreEqual(root.transform, root.rootScrollRect.transform.parent, "Root Scroll must be saved directly under BootstrapRoot.");
            Assert.AreEqual(root.rootScrollRect.transform, root.rootScrollRect.viewport.parent, "Viewport must be saved under Root Scroll.");
            Assert.AreEqual(root.rootScrollRect.viewport, root.rootScrollRect.content.parent, "Content must be saved under Viewport.");
            Assert.IsTrue(root.rootScrollRect.vertical, "Root Scroll must scroll vertically.");
            Assert.IsFalse(root.rootScrollRect.horizontal, "Root Scroll must not scroll horizontally.");
            Assert.AreEqual(ScrollRect.MovementType.Clamped, root.rootScrollRect.movementType);
            Assert.GreaterOrEqual(root.rootScrollRect.scrollSensitivity, 50f, "Root Scroll sensitivity must be usable with macOS wheel/trackpad input.");
            Assert.IsNotNull(root.rootScrollRect.GetComponent<BootstrapScrollDiagnostics>(), "Root Scroll diagnostics must be present.");
            Assert.IsNotNull(root.rootScrollRect.viewport.GetComponent<RectMask2D>(), "Viewport must have RectMask2D.");
            Assert.AreEqual(new Vector2(0f, 1f), root.rootScrollRect.content.anchorMin);
            Assert.AreEqual(new Vector2(1f, 1f), root.rootScrollRect.content.anchorMax);
            Assert.AreEqual(new Vector2(0.5f, 1f), root.rootScrollRect.content.pivot);
            AssertRootScrollCanOverflowAndMove(root);
            Assert.AreEqual(5, root.rootScrollRect.content.childCount, "Root Scroll content must contain the app screens.");
            AssertSavedStartupPath(canvas.transform, "Canvas/BootstrapRoot/Background");
            AssertSavedStartupPath(canvas.transform, "Canvas/BootstrapRoot/Root Scroll");
            AssertSavedStartupPath(canvas.transform, "Canvas/BootstrapRoot/Root Scroll/Viewport");
            AssertSavedStartupPath(canvas.transform, "Canvas/BootstrapRoot/Root Scroll/Viewport/Content");
            AssertSavedStartupPath(canvas.transform, "Canvas/BootstrapRoot/Root Scroll/Viewport/Content/Start Screen Root");
            AssertSavedStartupPath(canvas.transform, "Canvas/BootstrapRoot/Root Scroll/Viewport/Content/Game Dashboard Root", false);
            AssertSavedStartupPath(canvas.transform, "Canvas/BootstrapRoot/Root Scroll/Viewport/Content/Run Analysis Root", false);
            AssertSavedStartupPath(canvas.transform, "Canvas/BootstrapRoot/Root Scroll/Viewport/Content/Settings Advanced Root", false);
            AssertSavedStartupPath(canvas.transform, "Canvas/BootstrapRoot/Root Scroll/Viewport/Content/Developer Diagnostics Root", false);
            AssertSavedStartupPath(canvas.transform, "Canvas/BootstrapRoot/Root Scroll/Viewport/Content/Settings Advanced Root/AccountPanel", false);
            AssertSavedStartupPath(canvas.transform, "Canvas/BootstrapRoot/Root Scroll/Viewport/Content/Settings Advanced Root/PrivacyNoticePanel", false);
            AssertSavedStartupPath(canvas.transform, "Canvas/BootstrapRoot/Root Scroll/Viewport/Content/Run Analysis Root/Run Analysis Grid/ActivityAnalysisPanel", false);
            AssertSavedStartupPath(canvas.transform, "Canvas/BootstrapRoot/Root Scroll/Viewport/Content/Run Analysis Root/Run Analysis Grid/ReviewPanel", false);
            AssertSavedStartupPath(canvas.transform, "Canvas/BootstrapRoot/Root Scroll/Viewport/Content/Developer Diagnostics Root/ApprovedLocationsPanel", false);
            AssertSavedStartupPath(canvas.transform, "Canvas/BootstrapRoot/Root Scroll/Viewport/Content/Developer Diagnostics Root/RecentSessionsPanel", false);
            AssertSavedStartupPath(canvas.transform, "Canvas/BootstrapRoot/Root Scroll/Viewport/Content/Developer Diagnostics Root/SafeSyncPanel", false);

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
                Assert.IsTrue(rootView.startScreenRoot.activeSelf, "Start Screen must be active by default in the prefab.");
                Assert.IsFalse(rootView.gameDashboardRoot.activeSelf, "Dashboard must not be active by default in the prefab.");
                Assert.IsFalse(rootView.runAnalysisRoot.activeSelf, "Run Analysis must not be active by default in the prefab.");
                Assert.IsFalse(rootView.settingsAdvancedRoot.activeSelf, "Settings Advanced must not be active by default in the prefab.");
                Assert.IsFalse(rootView.developerDiagnosticsRoot.activeSelf, "Developer Diagnostics must not be active by default in the prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        [Test]
        public void BootstrapRootPrefabKeepsMvpInformationArchitectureAndAdvancedSyncSeparation()
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(BootstrapRootPrefabPath);
            try
            {
                var rootView = prefabRoot.GetComponent<BootstrapRootView>();
                Assert.IsNotNull(rootView, "BootstrapRootView is missing from BootstrapRoot prefab.");
                Assert.IsTrue(rootView.ValidateReferences(out var error), error);

                var title = rootView.startScreenRoot.transform.Find("Start Header/Title Stack/TokenForge Title").GetComponent<Text>();
                var subtitle = rootView.startScreenRoot.transform.Find("Start Header/Title Stack/TokenForge Subtitle").GetComponent<Text>();
                Assert.IsNotNull(title, "Start title is missing.");
                Assert.IsNotNull(subtitle, "Start subtitle is missing.");
                Assert.AreEqual("TokenForge", title.text);
                Assert.AreEqual(HorizontalWrapMode.Overflow, title.horizontalOverflow, "TokenForge title must not wrap into chunks.");
                Assert.GreaterOrEqual(title.GetComponent<LayoutElement>().minWidth, 400f, "TokenForge title needs a stable minimum width.");
                Assert.Greater(
                    subtitle.transform.GetSiblingIndex(),
                    title.transform.GetSiblingIndex(),
                    "Subtitle must live below the non-wrapping title in the title stack.");
                Assert.IsTrue(rootView.startGameButton.interactable, "Start Game must be interactable.");
                Assert.IsNotNull(rootView.startAnalyzeAgentLogsButton, "AI Agents button is missing.");
                Assert.IsNotNull(rootView.startCreateSyncAccountButton, "Create Sync Account button is missing.");
                Assert.IsNotNull(rootView.startLoginButton, "Log In button is missing.");
                Assert.IsNotNull(rootView.gameDashboardRoot, "Game Dashboard root is missing.");
                Assert.IsNotNull(rootView.settingsAdvancedRoot, "Settings Advanced root is missing.");

                var content = rootView.rootScrollRect.content;
                Assert.IsNotNull(content, "Root Scroll content is missing.");
                AssertSection(content, "Start Screen Root");
                AssertSection(rootView.settingsAdvancedRoot.transform, "Settings Local Sources");
                AssertSection(rootView.settingsAdvancedRoot.transform, "Settings Sync Conflict");
                AssertSection(rootView.runAnalysisRoot.transform.Find("Run Analysis Grid"), "ActivityAnalysisPanel");
                AssertSection(rootView.runAnalysisRoot.transform.Find("Run Analysis Grid"), "ReviewPanel");
                AssertSection(rootView.developerDiagnosticsRoot.transform, "ApprovedLocationsPanel");
                AssertSection(rootView.developerDiagnosticsRoot.transform, "SafeSyncPanel");
                AssertSection(rootView.developerDiagnosticsRoot.transform, "RecentSessionsPanel");
                AssertSection(rootView.settingsAdvancedRoot.transform, "PrivacyNoticePanel");
                Assert.IsNotNull(rootView.startScreenRoot, "Start Screen root is missing.");
                Assert.IsNotNull(rootView.gameDashboardRoot, "Game Dashboard root is missing.");
                Assert.IsFalse(rootView.accountPanel.gameObject.activeInHierarchy, "Account must be hidden on the default first screen.");
                Assert.IsFalse(rootView.safeSyncPanel.gameObject.activeInHierarchy, "Safe Sync must be hidden on the default first screen.");
                Assert.IsFalse(rootView.privacyNoticePanel.gameObject.activeInHierarchy, "Privacy must be hidden on the default first screen.");

                Assert.Less(
                    rootView.accountPanel.transform.GetSiblingIndex(),
                    rootView.privacyNoticePanel.transform.GetSiblingIndex(),
                    "Privacy should remain visible in basic settings.");
                Assert.Less(
                    rootView.privacyNoticePanel.transform.GetSiblingIndex(),
                    rootView.settingsAdvancedRoot.transform.Find("Settings Local Sources").GetSiblingIndex(),
                    "Local Sources should follow Account and Privacy.");
                Assert.Less(
                    rootView.settingsAdvancedRoot.transform.Find("Settings Local Sources").GetSiblingIndex(),
                    rootView.settingsAdvancedRoot.transform.Find("Settings Sync Conflict").GetSiblingIndex(),
                    "Sync / Conflict should follow Local Sources.");

                var advanced = rootView.safeSyncPanel.transform.Find("Advanced Safe Sync Diagnostics");
                Assert.IsNotNull(advanced, "Advanced Safe Sync Diagnostics container is missing.");
                AssertAdvancedOnly(rootView.safeSyncPanel.transform, advanced, "Process Eligible Retries");
                AssertAdvancedOnly(rootView.safeSyncPanel.transform, advanced, "Force Retry Selected");
                AssertAdvancedOnly(rootView.safeSyncPanel.transform, advanced, "Cancel Selected Retry");
                AssertAdvancedOnly(rootView.safeSyncPanel.transform, advanced, "Cancel Failed Retries");
                AssertAdvancedOnly(rootView.safeSyncPanel.transform, advanced, "Clear Succeeded");
                AssertAdvancedOnly(rootView.safeSyncPanel.transform, advanced, "Pause Pending Retries");
                AssertAdvancedOnly(rootView.safeSyncPanel.transform, advanced, "Resume Paused Retries");
                AssertAdvancedOnly(rootView.safeSyncPanel.transform, advanced, "Delete tombstones Field");
                AssertAdvancedOnly(rootView.safeSyncPanel.transform, advanced, "Enqueue Pending Deletes");
                AssertAdvancedOnly(rootView.safeSyncPanel.transform, advanced, "Process Deletes Once");
                AssertAdvancedOnly(rootView.safeSyncPanel.transform, advanced, "Conflicts Field");
                AssertAdvancedOnly(rootView.safeSyncPanel.transform, advanced, "Keep Local");
                AssertAdvancedOnly(rootView.safeSyncPanel.transform, advanced, "Keep Remote");
                AssertAdvancedOnly(rootView.safeSyncPanel.transform, advanced, "Mark Resolved");
                AssertAdvancedOnly(rootView.safeSyncPanel.transform, advanced, "Clear Resolved History");
                AssertAdvancedOnly(rootView.safeSyncPanel.transform, advanced, "Cancel Resolution");
                AssertAdvancedOnly(rootView.safeSyncPanel.transform, advanced, "Apply Merge");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        [Test]
        public void BootstrapRootPrefabUsesTransparentViewportAndNoBlockingFullscreenOverlay()
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(BootstrapRootPrefabPath);
            try
            {
                var rootView = prefabRoot.GetComponent<BootstrapRootView>();
                var background = prefabRoot.transform.Find("Background").GetComponent<Image>();
                Assert.IsNotNull(background, "Background graphic is missing.");
                Assert.IsFalse(background.raycastTarget, "Background must not intercept clicks.");
                Assert.AreEqual(0, background.transform.GetSiblingIndex(), "Background must stay behind content.");

                var viewport = rootView.rootScrollRect.viewport;
                Assert.IsNotNull(viewport.GetComponent<RectMask2D>(), "Viewport must use RectMask2D.");
                var viewportImage = viewport.GetComponent<Image>();
                Assert.IsNotNull(viewportImage, "Viewport transparent image is missing.");
                Assert.AreEqual(0f, viewportImage.color.a, 0.001f, "Viewport image must be transparent.");
                Assert.IsFalse(viewportImage.raycastTarget, "Viewport image should not block controls; ScrollRect handles scrolling through its own event chain.");

                var rootRect = prefabRoot.GetComponent<RectTransform>();
                foreach (var graphic in prefabRoot.GetComponentsInChildren<Graphic>(true))
                {
                    if (graphic == background || graphic == viewportImage)
                    {
                        continue;
                    }

                    var rect = graphic.GetComponent<RectTransform>();
                    if (rect == null)
                    {
                        continue;
                    }

                    var fullScreenAnchored = rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one
                        && rect.offsetMin == Vector2.zero && rect.offsetMax == Vector2.zero;
                    Assert.IsFalse(
                        fullScreenAnchored && graphic.transform.GetSiblingIndex() > background.transform.GetSiblingIndex() && graphic.raycastTarget,
                        graphic.name + " is a top-level fullscreen blocking graphic.");
                }

                AssertResolvableRect(rootRect, "BootstrapRoot");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        [Test]
        public void DashboardConflictBannerDefaultTextIsCompactAndSafe()
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(BootstrapRootPrefabPath);
            try
            {
                var rootView = prefabRoot.GetComponent<BootstrapRootView>();
                var text = rootView.conflictBannerLabel.text;
                Assert.That(text.Split('\n').Length, Is.EqualTo(1), "Dashboard conflict banner must stay single-line by default.");
                Assert.That(text, Does.Not.Contain("Local:"));
                Assert.That(text, Does.Not.Contain("Remote:"));
                Assert.That(text, Does.Not.Contain("/Users/"));
                Assert.That(text, Does.Not.Contain("commit message"));
                Assert.That(text, Does.Not.Contain("source code"));
                Assert.That(text, Does.Not.Contain("prompt"));
                Assert.That(text, Does.Not.Contain("response"));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        [Test]
        public void BootstrapRootPrefabCardsReserveStableLayoutAndTextHeights()
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(BootstrapRootPrefabPath);
            try
            {
                var rootView = prefabRoot.GetComponent<BootstrapRootView>();
                var content = rootView.rootScrollRect.content;
                AssertCardLayout(content.Find("Start Screen Root"), 900f);
                AssertCardLayout(rootView.activityAnalysisPanel.transform, 420f);
                AssertCardLayout(rootView.approvedLocationsPanel.transform, 460f);
                AssertCardLayout(rootView.reviewPanel.transform, 260f);
                AssertCardLayout(rootView.recentSessionsPanel.transform, 300f);
                AssertCardLayout(rootView.safeSyncPanel.transform, 1000f);
                AssertCardLayout(rootView.privacyNoticePanel.transform, 180f);

                AssertTextBlock(rootView.activityAnalysisPanel.gitStatusLabel, 44f, "Git Status");
                AssertTextBlock(rootView.activityAnalysisPanel.agentStatusLabel, 44f, "Agent Status");
                AssertTextBlock(rootView.reviewPanel.reviewLabel, 52f, "Review Summary");
                AssertTextBlock(rootView.safeSyncPanel.statusLabel, 66f, "Safe Sync Status");
                AssertTextBlock(rootView.safeSyncPanel.retryQueueLabel, 54f, "Retry Queue Status");
                AssertTextBlock(rootView.safeSyncPanel.conflictLabel, 150f, "Conflict Status");
                AssertTextBlock(rootView.safeSyncPanel.tombstoneLabel, 52f, "Tombstone Status");
                AssertTextBlock(rootView.recentSessionsPanel.localRecentSessionsLabel, 52f, "Local Recent Sessions");
                AssertTextBlock(rootView.recentSessionsPanel.remoteSessionsLabel, 52f, "Remote Sessions");
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

        private static void AssertSection(Transform content, string name)
        {
            var section = content.Find(name);
            Assert.IsNotNull(section, name + " section is missing.");
            Assert.IsTrue(section.gameObject.activeSelf, name + " section is inactive.");
        }

        private static void AssertAdvancedOnly(Transform safeSyncPanel, Transform advanced, string childName)
        {
            var matches = safeSyncPanel.GetComponentsInChildren<Transform>(true)
                .Where(child => child.name == childName)
                .ToArray();
            Assert.IsNotEmpty(matches, childName + " was not found in Safe Sync.");
            foreach (var match in matches)
            {
                Assert.IsTrue(
                    match == advanced || match.IsChildOf(advanced),
                    childName + " must live under Advanced Safe Sync Diagnostics.");
            }
        }

        private static void AssertCardLayout(Transform section, float expectedMinimumHeight)
        {
            Assert.IsNotNull(section, "Section is missing.");
            var element = section.GetComponent<LayoutElement>();
            Assert.IsNotNull(element, section.name + " LayoutElement is missing.");
            Assert.GreaterOrEqual(element.minHeight, expectedMinimumHeight, section.name + " minHeight is too small for stable startup layout.");
            Assert.GreaterOrEqual(element.preferredHeight, expectedMinimumHeight, section.name + " preferredHeight is too small for stable startup layout.");
        }

        private static void AssertTextBlock(Text text, float expectedMinimumHeight, string label)
        {
            Assert.IsNotNull(text, label + " text is missing.");
            Assert.AreEqual(HorizontalWrapMode.Wrap, text.horizontalOverflow, label + " must wrap horizontally.");
            var element = text.GetComponent<LayoutElement>() ??
                          (text.transform.parent != null ? text.transform.parent.GetComponent<LayoutElement>() : null);
            Assert.IsNotNull(element, label + " LayoutElement is missing.");
            Assert.GreaterOrEqual(element.minHeight, expectedMinimumHeight, label + " minHeight is too small for wrapped text.");
            Assert.GreaterOrEqual(element.preferredHeight, expectedMinimumHeight, label + " preferredHeight is too small for wrapped text.");
        }

        private static void AssertRootScrollCanOverflowAndMove(BootstrapRootView root)
        {
            var viewport = root.rootScrollRect.viewport;
            var content = root.rootScrollRect.content;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            var preferredHeight = LayoutUtility.GetPreferredHeight(content);
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(preferredHeight, viewport.rect.height + 1f));

            Assert.Greater(content.rect.height, viewport.rect.height, "Root Scroll content height must exceed viewport height when onboarding overflows.");
            root.rootScrollRect.verticalNormalizedPosition = 1f;
            var start = root.rootScrollRect.verticalNormalizedPosition;
            root.rootScrollRect.verticalNormalizedPosition = 0.25f;
            Assert.AreNotEqual(start, root.rootScrollRect.verticalNormalizedPosition, "Root Scroll normalized position must be able to change.");
        }

        private static void AssertSavedStartupPath(Transform canvasTransform, string expectedPath, bool requireActive = true)
        {
            var relativePath = expectedPath.StartsWith("Canvas/", System.StringComparison.Ordinal)
                ? expectedPath.Substring("Canvas/".Length)
                : expectedPath;
            var target = canvasTransform.Find(relativePath);
            Assert.IsNotNull(target, expectedPath + " is missing from the saved startup scene.");
            if (requireActive)
            {
                Assert.IsTrue(target.gameObject.activeSelf, expectedPath + " is inactive in the saved startup scene.");
            }

            Assert.Greater(Mathf.Abs(target.localScale.x), 0.001f, expectedPath + " has zero x scale.");
            Assert.Greater(Mathf.Abs(target.localScale.y), 0.001f, expectedPath + " has zero y scale.");
        }
    }
}
#endif
