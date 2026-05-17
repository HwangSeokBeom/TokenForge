#if UNITY_EDITOR
using System.Collections;
using System.Text.RegularExpressions;
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

        [UnityTest]
        public IEnumerator TokenForgeMainLoadsBootstrapUiInsteadOfEmptySkybox()
        {
            LogAssert.Expect(LogType.Log, "INFO [TokenForgeBootstrap] TokenForge bootstrap starting.");
            LogAssert.Expect(LogType.Log, new Regex(@"^INFO \[TokenForgeBootstrap\] visible UI validated canvas=Canvas renderMode=ScreenSpaceOverlay targetDisplay=0 candidates=\d+ graphics=\d+ texts=\d+ panels=\d+$"));
            LogAssert.Expect(LogType.Log, "INFO [TokenForgeBootstrap] TokenForge bootstrap UI ready.");
            LogAssert.Expect(LogType.Log, new Regex(@"^INFO \[TokenForgeBootstrap\] visible UI validated canvas=Canvas renderMode=ScreenSpaceOverlay targetDisplay=0 candidates=\d+ graphics=\d+ texts=\d+ panels=\d+$"));
            LogAssert.Expect(LogType.Log, new Regex(@"^INFO \[TokenForgeBootstrap\] rendered frame smoke passed nonBlackPixelRatio=\d+\.\d+ averageLuminance=\d+\.\d+$"));
            LogAssert.Expect(LogType.Log, "INFO [TokenForgeBootstrap] TokenForge bootstrap completed.");

            var load = SceneManager.LoadSceneAsync(StartupSceneName, LoadSceneMode.Single);
            while (!load.isDone)
            {
                yield return null;
            }

            for (var i = 0; i < 20; i++)
            {
                yield return null;
            }

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

            var bootstrapper = Object.FindObjectOfType<AppBootstrapper>();
            Assert.IsTrue(bootstrapper.IsPrefabUiActive);
            Assert.IsTrue(bootstrapper.IsVisibleUiValidated, "Bootstrapper completed without visible UI validation.");
            Assert.GreaterOrEqual(bootstrapper.LastViewportVisibleCandidateCount, 1, "Bootstrapper found no viewport-visible UI candidates.");
            Assert.GreaterOrEqual(
                bootstrapper.LastRenderedFrameNonBlackPixelRatio,
                0.003f,
                "Bootstrapper completed even though the rendered frame was effectively black.");
            Assert.AreEqual(AuthState.LoggedOut, bootstrapper.ApprovedActivityAnalysis.AuthState);
            Assert.AreEqual(SafeSyncStatus.Idle, bootstrapper.ApprovedActivityAnalysis.SafeSyncStatus);

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(root.GetComponent<RectTransform>());
            Assert.Greater(CountViewportVisibleGraphics(root.gameObject, canvas), 0, "Startup Scene rendered no viewport-visible UI graphics.");
            Assert.Greater(CountViewportVisibleTexts(root.gameObject, canvas), 0, "Startup Scene rendered no viewport-visible UI text.");

            var visibleText = string.Join("\n", UiVisibleTextScanner.Collect(root.gameObject));
            Assert.That(visibleText, Does.Contain("Privacy-safe developer activity companion"));
            Assert.That(visibleText, Does.Contain("Approved locations"));
            Assert.That(visibleText, Does.Contain("Safe Sync"));
            Assert.IsNotEmpty(visibleText, "Startup Scene rendered no visible TokenForge UI text.");

            Assert.GreaterOrEqual(
                bootstrapper.LastRenderedFrameAverageLuminance,
                0.01f,
                "Game View capture average luminance was not recorded.");
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
                && graphic.color.a > 0.001f
                && HasRenderableRect(graphic.rectTransform)
                && GetScreenRect(graphic.rectTransform, canvas).Overlaps(new Rect(0, 0, Screen.width, Screen.height), true);
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

    }
}
#endif
