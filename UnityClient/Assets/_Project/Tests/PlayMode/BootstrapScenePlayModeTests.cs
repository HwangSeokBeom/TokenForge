#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using TokenForge.Client.Auth;
using TokenForge.Client.Sync;
using TokenForge.Client.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TokenForge.Client.Tests
{
    public sealed class BootstrapScenePlayModeTests
    {
        private const string StartupSceneName = "TokenForgeMain";
        private const int SceneLoadTimeoutFrames = 240;
        private const int BootstrapTimeoutFrames = 360;

        [UnityTest]
        public IEnumerator TokenForgeMainLoadsBootstrapUiStructureInsteadOfEmptySkybox()
        {
            yield return LoadStartupSceneAndWaitForBootstrap(requireRenderedSmoke: false);

            Assert.AreEqual(StartupSceneName, SceneManager.GetActiveScene().name);
            Assert.IsNotNull(Object.FindObjectOfType<AppBootstrapper>(), "AppBootstrapper is missing.");
            var canvas = Object.FindObjectOfType<Canvas>();
            Assert.IsNotNull(canvas, "Canvas is missing.");
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
            Assert.AreEqual(0, canvas.sortingOrder);
            Assert.AreEqual(0, canvas.targetDisplay);
            Assert.IsNotNull(canvas.GetComponent<GraphicRaycaster>(), "GraphicRaycaster is missing.");
            AssertRenderableRect(canvas.GetComponent<RectTransform>(), "Canvas");
            Assert.IsNotNull(Object.FindObjectOfType<EventSystem>(), "EventSystem is missing.");

            var root = Object.FindObjectOfType<BootstrapRootView>();
            Assert.IsNotNull(root, "TokenForge UI root is missing.");
            Assert.IsTrue(root.ValidateReferences(out var error), error);
            Assert.IsTrue(root.gameObject.activeInHierarchy, "TokenForge UI root is inactive.");
            AssertRenderableRect(root.GetComponent<RectTransform>(), "BootstrapRoot");
            Assert.IsNotNull(root.accountPanel);
            Assert.IsNotNull(root.activityAnalysisPanel);
            Assert.IsNotNull(root.approvedLocationsPanel);
            Assert.IsNotNull(root.safeSyncPanel);
            Assert.IsNotNull(root.recentSessionsPanel);
            AssertRootScrollLayout(root, canvas);
            Assert.IsTrue(root.startScreenRoot.activeInHierarchy, "Start Screen must be visible at launch.");
            Assert.IsFalse(root.settingsAdvancedRoot.activeInHierarchy, "Settings/Advanced must not be visible at launch.");

            var bootstrapper = Object.FindObjectOfType<AppBootstrapper>();
            Assert.IsTrue(bootstrapper.IsPrefabUiActive);
            Assert.IsTrue(bootstrapper.IsVisibleUiValidated, "Bootstrapper completed without visible UI validation.");
            Assert.IsTrue(bootstrapper.IsBootstrapComplete, "Bootstrapper did not reach deterministic completion before the test timeout.");
            Assert.GreaterOrEqual(bootstrapper.LastViewportVisibleCandidateCount, 1, "Bootstrapper found no viewport-visible UI candidates.");
            Assert.AreEqual(AuthState.LoggedOut, bootstrapper.ApprovedActivityAnalysis.AuthState);
            Assert.AreEqual(SafeSyncStatus.Idle, bootstrapper.ApprovedActivityAnalysis.SafeSyncStatus);

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(root.GetComponent<RectTransform>());
            AssertNoTopmostOpaqueViewportOrBlockingFullscreenGraphic(root, canvas);
            Assert.Greater(CountViewportVisibleGraphics(root.gameObject, canvas), 0, "Startup Scene rendered no viewport-visible UI graphics.");
            Assert.Greater(CountViewportVisibleTexts(root.gameObject, canvas), 0, "Startup Scene rendered no viewport-visible UI text.");
            Assert.GreaterOrEqual(CountVisibleRepresentativeTexts(root.gameObject, canvas), 3, "Fewer than three representative startup texts are visible in the viewport.");

            var visibleText = string.Join("\n", UiVisibleTextScanner.Collect(root.gameObject));
            Assert.That(visibleText, Does.Contain("Turn local development activity into RPG growth."));
            Assert.That(visibleText, Does.Contain("Start Game"));
            Assert.That(visibleText, Does.Contain("Run Analysis"));
            Assert.That(visibleText, Does.Contain("Safe Sync"));
            Assert.That(visibleText, Does.Contain("No saved run yet."));
            Assert.IsNotEmpty(visibleText, "Startup Scene rendered no visible TokenForge UI text.");

            if (Application.isBatchMode)
            {
                Assert.IsTrue(bootstrapper.IsRenderedFrameSmokeSkippedForBatchMode, "Batchmode should not depend on Game View screenshot capture.");
                yield break;
            }

            Assert.GreaterOrEqual(
                bootstrapper.LastRenderedFrameAverageLuminance,
                0.01f,
                "Game View capture average luminance was not recorded.");
        }

        [UnityTest]
        [Category("GuiRenderSmoke")]
        public IEnumerator TokenForgeMainPassesGuiGameViewRenderedFrameSmoke()
        {
            if (Application.isBatchMode)
            {
                Assert.Ignore("GUI Game View render smoke is skipped in batchmode; run this category in a GUI Editor session.");
            }

            yield return LoadStartupSceneAndWaitForBootstrap(requireRenderedSmoke: true);

            var bootstrapper = Object.FindObjectOfType<AppBootstrapper>();
            Assert.IsNotNull(bootstrapper, "AppBootstrapper is missing.");
            Assert.GreaterOrEqual(
                bootstrapper.LastRenderedFrameNonBlackPixelRatio,
                0.003f,
                "Bootstrapper completed even though the rendered frame was effectively black.");
            Assert.Greater(
                bootstrapper.LastRenderedFrameBrightestPixelLuminance,
                0.2f,
                "Bootstrapper completed without any bright rendered pixels.");
            Assert.GreaterOrEqual(
                bootstrapper.LastRenderedFrameAverageLuminance,
                0.01f,
                "Game View capture average luminance was not recorded.");

            yield return CaptureAndAssertGuiFrame("Free Aspect", Screen.width, Screen.height);
            yield return CaptureRequestedResolution("1280x720", 1280, 720);
            yield return CaptureRequestedResolution("1440x900", 1440, 900);
        }

        [UnityTearDown]
        public IEnumerator CleanupLoadedStartupScene()
        {
            if (Application.isBatchMode)
            {
                yield break;
            }

            var cleanupScene = SceneManager.CreateScene("BootstrapScenePlayModeCleanup");
            SceneManager.SetActiveScene(cleanupScene);
            for (var i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || scene == cleanupScene || !scene.isLoaded)
                {
                    continue;
                }

                var unload = SceneManager.UnloadSceneAsync(scene);
                if (unload == null)
                {
                    continue;
                }

                var unloadFrames = 0;
                while (!unload.isDone)
                {
                    unloadFrames++;
                    if (unloadFrames > 120)
                    {
                        Debug.LogWarning("WARN [TokenForgeBootstrapTests] cleanup scene unload timed out for " + scene.name);
                        break;
                    }

                    yield return null;
                }
            }

            yield return null;
        }

        private static IEnumerator LoadStartupSceneAndWaitForBootstrap(bool requireRenderedSmoke)
        {
            if (Application.isBatchMode)
            {
                SceneManager.LoadScene(StartupSceneName, LoadSceneMode.Single);
                yield return null;
                yield return WaitForBootstrap(requireRenderedSmoke);
                yield break;
            }

            if (SceneManager.GetActiveScene().name == StartupSceneName)
            {
                yield return WaitForBootstrap(requireRenderedSmoke);
                yield break;
            }

            var load = SceneManager.LoadSceneAsync(StartupSceneName, LoadSceneMode.Single);
            Assert.IsNotNull(load, StartupSceneName + " could not be loaded. Check Build Settings scene 0.");
            var loadFrames = 0;
            while (!load.isDone && loadFrames < SceneLoadTimeoutFrames)
            {
                if (SceneManager.GetActiveScene().name == StartupSceneName && Object.FindObjectOfType<AppBootstrapper>() != null)
                {
                    break;
                }

                loadFrames++;
                yield return null;
            }

            Assert.IsTrue(
                load.isDone || SceneManager.GetActiveScene().name == StartupSceneName,
                StartupSceneName + " did not finish loading within " + SceneLoadTimeoutFrames + " frames.");

            yield return WaitForBootstrap(requireRenderedSmoke);
        }

        private static IEnumerator WaitForBootstrap(bool requireRenderedSmoke)
        {
            AppBootstrapper bootstrapper = null;
            var bootstrapFrames = 0;
            while (bootstrapFrames < BootstrapTimeoutFrames)
            {
                bootstrapper = Object.FindObjectOfType<AppBootstrapper>();
                if (bootstrapper != null
                    && bootstrapper.IsBootstrapComplete
                    && (!requireRenderedSmoke || bootstrapper.LastRenderedFrameNonBlackPixelRatio > 0f))
                {
                    break;
                }

                bootstrapFrames++;
                yield return null;
            }

            Assert.IsNotNull(bootstrapper, "AppBootstrapper was not created after scene load.");
            Assert.IsTrue(
                bootstrapper.IsBootstrapComplete,
                "AppBootstrapper did not complete within " + BootstrapTimeoutFrames + " frames.");
            if (requireRenderedSmoke)
            {
                Assert.Greater(
                    bootstrapper.LastRenderedFrameNonBlackPixelRatio,
                    0f,
                    "GUI rendered frame smoke did not record a non-black ratio before timeout.");
            }
        }

        private static IEnumerator CaptureRequestedResolution(string label, int width, int height)
        {
            Screen.SetResolution(width, height, false);
            yield return null;
            yield return CaptureAndAssertGuiFrame(label, width, height);
        }

        private static IEnumerator CaptureAndAssertGuiFrame(string label, int requestedWidth, int requestedHeight)
        {
            yield return new WaitForEndOfFrame();
            var texture = CaptureScreenshotAsTexture();
            Assert.IsNotNull(texture, "ScreenCapture did not return a " + label + " Game View texture.");
            try
            {
                AnalyzeRenderedFrame(texture, out var averageLuminance, out var nonBlackRatio, out var brightest, out var sampled);
                Debug.Log("INFO [TokenForgeBootstrapTests] gui screen capture label=" + label
                    + " requestedSize=" + requestedWidth + "x" + requestedHeight
                    + " capturedSize=" + texture.width + "x" + texture.height
                    + " sampledPixels=" + sampled
                    + " nonBlackThreshold=0.100000"
                    + " nonBlackPixelRatio=" + nonBlackRatio.ToString("0.000000")
                    + " averageLuminance=" + averageLuminance.ToString("0.000000")
                    + " brightestPixelLuminance=" + brightest.ToString("0.000000"));
                Assert.GreaterOrEqual(nonBlackRatio, 0.003f, label + " Game View ScreenCapture is effectively black.");
                Assert.Greater(brightest, 0.2f, label + " Game View ScreenCapture contains no bright UI pixels.");
            }
            finally
            {
                Object.Destroy(texture);
            }
        }

        private static int CountViewportVisibleGraphics(GameObject root, Canvas canvas)
        {
            var count = 0;
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(false))
            {
                if (IsViewportVisibleGraphic(graphic, canvas))
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountViewportVisibleTexts(GameObject root, Canvas canvas)
        {
            var count = 0;
            foreach (var text in root.GetComponentsInChildren<Text>(false))
            {
                if (IsViewportVisibleGraphic(text, canvas) && !string.IsNullOrWhiteSpace(text.text))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsViewportVisibleGraphic(Graphic graphic, Canvas canvas)
        {
            return graphic != null
                && graphic.enabled
                && graphic.gameObject.activeInHierarchy
                && graphic.canvas != null
                && (graphic.canvasRenderer == null || !graphic.canvasRenderer.cull)
                && graphic.color.a > 0.001f
                && HasRenderableRect(graphic.rectTransform)
                && GetScreenRect(graphic.rectTransform, canvas).Overlaps(new Rect(0, 0, Screen.width, Screen.height), true);
        }

        private static int CountVisibleRepresentativeTexts(GameObject root, Canvas canvas)
        {
            var count = 0;
            foreach (var text in root.GetComponentsInChildren<Text>(false))
            {
                if (IsRepresentativeText(text.text)
                    && text.color.a > 0.001f
                    && IsViewportVisibleGraphic(text, canvas))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsRepresentativeText(string text)
        {
            return !string.IsNullOrWhiteSpace(text)
                && (text.Contains("Turn local development activity into RPG growth")
                    || text.Contains("TokenForge")
                    || text.Contains("Start Game")
                    || text.Contains("Run Analysis")
                    || text.Contains("Safe Sync"));
        }

        private static void AssertRootScrollLayout(BootstrapRootView root, Canvas canvas)
        {
            Assert.IsNotNull(root.rootScrollRect, "Root Scroll is missing.");
            var scrollRect = root.rootScrollRect.GetComponent<RectTransform>();
            var viewport = root.rootScrollRect.viewport;
            var content = root.rootScrollRect.content;
            Assert.IsNotNull(viewport, "Root Scroll viewport reference is missing.");
            Assert.IsNotNull(content, "Root Scroll content reference is missing.");
            var background = root.transform.Find("Background");
            Assert.IsNotNull(background, "BootstrapRoot/Background is missing.");
            Assert.Less(background.GetSiblingIndex(), root.rootScrollRect.transform.GetSiblingIndex(), "Background must be behind Root Scroll.");
            Assert.AreEqual(root.transform, root.rootScrollRect.transform.parent, "Root Scroll must be under BootstrapRoot.");
            Assert.AreEqual(root.rootScrollRect.transform, viewport.parent, "Viewport must be a direct Root Scroll child.");
            Assert.AreEqual(viewport, content.parent, "Content must be a direct Viewport child.");
            Assert.AreEqual(new Vector2(0f, 1f), content.anchorMin, "Content anchorMin must stay top-stretched.");
            Assert.AreEqual(new Vector2(1f, 1f), content.anchorMax, "Content anchorMax must stay top-stretched.");
            Assert.AreEqual(new Vector2(0.5f, 1f), content.pivot, "Content pivot must stay top-centered.");
            Assert.AreEqual(5, content.childCount, "Root Scroll content must keep the app screens.");
            Assert.AreEqual(content, root.startScreenRoot.transform.parent, "Start Screen must be under Content.");
            Assert.AreEqual(content, root.gameDashboardRoot.transform.parent, "Dashboard must be under Content.");
            Assert.AreEqual(content, root.settingsAdvancedRoot.transform.parent, "Settings must be under Content.");
            Assert.AreEqual(root.settingsAdvancedRoot.transform, root.accountPanel.transform.parent, "AccountPanel must be under Settings.");
            Assert.AreEqual(root.settingsAdvancedRoot.transform, root.privacyNoticePanel.transform.parent, "PrivacyNoticePanel must be under Settings.");
            Assert.AreEqual(root.runAnalysisRoot.transform.Find("Run Analysis Grid"), root.activityAnalysisPanel.transform.parent, "ActivityAnalysisPanel must be under Run Analysis.");
            Assert.AreEqual(root.runAnalysisRoot.transform.Find("Run Analysis Grid"), root.reviewPanel.transform.parent, "ReviewPanel must be under Run Analysis.");
            Assert.AreEqual(root.developerDiagnosticsRoot.transform, root.approvedLocationsPanel.transform.parent, "ApprovedLocationsPanel must be under Developer Diagnostics.");
            Assert.AreEqual(root.developerDiagnosticsRoot.transform, root.recentSessionsPanel.transform.parent, "RecentSessionsPanel must be under Developer Diagnostics.");
            Assert.AreEqual(root.developerDiagnosticsRoot.transform, root.safeSyncPanel.transform.parent, "SafeSyncPanel must be under Developer Diagnostics.");
            if (root.startScreenRoot.activeInHierarchy || root.settingsAdvancedRoot.activeInHierarchy)
            {
                AssertRenderableRect(scrollRect, "Root Scroll");
                AssertRenderableRect(viewport, "Viewport");
                AssertRenderableRect(content, "Content");
            }

            var rootImage = root.GetComponent<Image>();
            Assert.IsTrue(rootImage == null || rootImage.color.a <= 0.01f, "BootstrapRoot Image must not render above Content.");
            var viewportImage = viewport.GetComponent<Image>();
            Assert.IsNotNull(viewportImage, "Viewport Image is missing.");
            Assert.LessOrEqual(viewportImage.color.a, 0.01f, "Viewport Image must be transparent.");
            Assert.IsNull(viewport.GetComponent<Mask>(), "Viewport must not use a transparent Image Mask because it can suppress children in the GUI Game View.");
            Assert.IsNotNull(viewport.GetComponent<RectMask2D>(), "Viewport RectMask2D is missing.");

            if (root.startScreenRoot.activeInHierarchy || root.settingsAdvancedRoot.activeInHierarchy)
            {
                var viewportScreenRect = GetScreenRect(viewport, canvas);
                var contentScreenRect = GetScreenRect(content, canvas);
                Assert.IsTrue(viewportScreenRect.Overlaps(contentScreenRect, true), "Content does not intersect the Root Scroll viewport.");

                var childIntersections = 0;
                for (var i = 0; i < content.childCount; i++)
                {
                    var childRect = content.GetChild(i).GetComponent<RectTransform>();
                    if (childRect != null && GetScreenRect(childRect, canvas).Overlaps(viewportScreenRect, true))
                    {
                        childIntersections++;
                    }
                }

                Assert.Greater(childIntersections, 0, "Root Scroll viewport clips every content child.");
            }
            var advanced = root.safeSyncPanel.transform.Find("Advanced Safe Sync Diagnostics") as RectTransform;
            Assert.IsNotNull(advanced, "Advanced Safe Sync Diagnostics is missing.");
            Assert.IsTrue(advanced.IsChildOf(root.safeSyncPanel.transform), "Advanced diagnostics must remain inside Safe Sync.");
        }

        private static void AssertNoSectionOverlap(RectTransform first, RectTransform second, Canvas canvas, string firstLabel, string secondLabel)
        {
            Assert.IsNotNull(first, firstLabel + " RectTransform is missing.");
            Assert.IsNotNull(second, secondLabel + " RectTransform is missing.");
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(first);
            LayoutRebuilder.ForceRebuildLayoutImmediate(second);
            var firstRect = GetScreenRect(first, canvas);
            var secondRect = GetScreenRect(second, canvas);
            Assert.IsFalse(
                firstRect.Overlaps(secondRect, true),
                firstLabel + " overlaps " + secondLabel + " in screen coordinates.");
        }

        private static void AssertNoTopmostOpaqueViewportOrBlockingFullscreenGraphic(BootstrapRootView root, Canvas canvas)
        {
            var viewportRect = new Rect(0, 0, Screen.width, Screen.height);
            var content = root.rootScrollRect.content;
            var graphics = canvas.GetComponentsInChildren<Graphic>(false);
            for (var i = graphics.Length - 1; i >= 0; i--)
            {
                var graphic = graphics[i];
                if (graphic == null || !graphic.enabled || !graphic.gameObject.activeInHierarchy || graphic.color.a <= 0.01f)
                {
                    continue;
                }

                var screenRect = GetScreenRect(graphic.rectTransform, canvas);
                var intersection = Intersect(screenRect, viewportRect);
                if (intersection.width * intersection.height < viewportRect.width * viewportRect.height * 0.8f)
                {
                    continue;
                }

                Assert.IsFalse(
                    graphic.transform == root.rootScrollRect.viewport && graphic.color.a > 0.01f,
                    "Topmost fullscreen graphic must not be the visible Root Scroll Viewport Image.");

                if (graphic is Image && graphic.color.a > 0.95f && CalculateLuminance(graphic.color) < 0.1f)
                {
                    var isBackground = graphic.transform.name == "Background"
                        && graphic.transform.parent == root.transform
                        && graphic.transform.GetSiblingIndex() < root.rootScrollRect.transform.GetSiblingIndex();
                    var isContentChild = graphic.transform == content || graphic.transform.IsChildOf(content);
                    Assert.IsTrue(isBackground || isContentChild, "Opaque dark fullscreen Image can cover Content: " + graphic.name);
                }
            }
        }

        private static void AssertRenderableRect(RectTransform rect, string label)
        {
            Assert.IsTrue(HasRenderableRect(rect), label + " RectTransform has zero renderable size.");
        }

        private static bool HasRenderableRect(RectTransform rect)
        {
            if (rect == null)
            {
                return false;
            }

            var rectValue = rect.rect;
            var scale = rect.lossyScale;
            return rectValue.width > 0.5f
                && rectValue.height > 0.5f
                && Mathf.Abs(scale.x) > 0.001f
                && Mathf.Abs(scale.y) > 0.001f;
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

        private static Rect Intersect(Rect a, Rect b)
        {
            var xMin = Mathf.Max(a.xMin, b.xMin);
            var yMin = Mathf.Max(a.yMin, b.yMin);
            var xMax = Mathf.Min(a.xMax, b.xMax);
            var yMax = Mathf.Min(a.yMax, b.yMax);
            return xMax <= xMin || yMax <= yMin ? Rect.zero : Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static float CalculateLuminance(Color color)
        {
            return 0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b;
        }

        private static void AnalyzeRenderedFrame(Texture2D texture, out float averageLuminance, out float nonBlackPixelRatio, out float brightestPixelLuminance, out int sampled)
        {
            var pixels = texture.GetPixels32();
            var stride = Mathf.Max(1, pixels.Length / 20000);
            sampled = 0;
            var nonBlack = 0;
            double luminanceSum = 0d;
            brightestPixelLuminance = 0f;
            for (var i = 0; i < pixels.Length; i += stride)
            {
                var color = pixels[i];
                var luminance = (0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b) / 255f;
                luminanceSum += luminance;
                brightestPixelLuminance = Mathf.Max(brightestPixelLuminance, luminance);
                if (luminance >= 0.1f)
                {
                    nonBlack++;
                }

                sampled++;
            }

            averageLuminance = sampled > 0 ? (float)(luminanceSum / sampled) : 0f;
            nonBlackPixelRatio = sampled > 0 ? (float)nonBlack / sampled : 0f;
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
    }
}
#endif
