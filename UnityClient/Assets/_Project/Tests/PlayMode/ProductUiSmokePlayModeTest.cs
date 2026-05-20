#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TokenForge.Client.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TokenForge.Client.Tests
{
    public sealed class ProductUiSmokePlayModeTest
    {
        [UnityTest]
        public IEnumerator TokenForgeMainLoadsProductStartScreen()
        {
            var load = SceneManager.LoadSceneAsync("TokenForgeMain", LoadSceneMode.Single);
            while (!load.isDone)
            {
                yield return null;
            }

            AppBootstrapper bootstrapper = null;
            for (var i = 0; i < 360; i++)
            {
                bootstrapper = Object.FindObjectOfType<AppBootstrapper>();
                if (bootstrapper != null && bootstrapper.IsBootstrapComplete)
                {
                    break;
                }

                yield return null;
            }

            Assert.IsNotNull(bootstrapper, "AppBootstrapper is missing.");
            Assert.IsTrue(bootstrapper.IsBootstrapComplete, "AppBootstrapper did not complete.");
            Assert.IsNotNull(Object.FindObjectOfType<Canvas>(), "Canvas is missing.");
            Assert.IsNotNull(Object.FindObjectOfType<EventSystem>(), "EventSystem is missing.");

            var root = Object.FindObjectOfType<BootstrapRootView>();
            Assert.IsNotNull(root, "BootstrapRoot is missing.");
            Assert.IsTrue(root.ValidateReferences(out var error), error);
            Assert.IsTrue(root.startScreenRoot.activeInHierarchy, "Start Screen is not the first screen.");
            Assert.IsFalse(root.gameDashboardRoot.activeInHierarchy, "Game Dashboard should not replace the first screen.");
            Assert.IsTrue(root.startGameButton.interactable, "Start Game is not clickable.");
            Assert.IsTrue(root.startAnalyzeRepositoryButton.interactable, "Add Repository is not clickable.");
            Assert.IsTrue(root.startAnalyzeAgentLogsButton.interactable, "Connect Codex Agent is not clickable.");

            var visibleText = string.Join("\n", UiVisibleTextScanner.Collect(root.gameObject));
            foreach (var required in new[] { "TokenForge", "Start Game", "Add Repository", "Connect Codex Agent" })
            {
                Assert.That(visibleText, Does.Contain(required));
            }

            CollectionAssert.IsEmpty(
                UiVisibleTextScanner.FindForbiddenRuntimeText(root.gameObject, UiVisibleTextScanner.ProductUiForbiddenTextFragments));
        }

        [UnityTest]
        public IEnumerator RuntimeBindingKeepsRequiredProductText()
        {
            var load = SceneManager.LoadSceneAsync("TokenForgeMain", LoadSceneMode.Single);
            while (!load.isDone)
            {
                yield return null;
            }

            AppBootstrapper bootstrapper = null;
            for (var i = 0; i < 360; i++)
            {
                bootstrapper = Object.FindObjectOfType<AppBootstrapper>();
                if (bootstrapper != null && bootstrapper.IsBootstrapComplete)
                {
                    break;
                }

                yield return null;
            }

            var root = Object.FindObjectOfType<BootstrapRootView>();
            Assert.IsNotNull(root);
            root.Bind(bootstrapper.LocalStatus, bootstrapper.ApprovedActivityAnalysis);
            root.Render();
            yield return null;

            var allText = string.Join("\n", UiVisibleTextScanner.Collect(root.gameObject, includeInactive: true));
            var missing = new[] { "Your companion is ready to grow", "Review Activity", "Repository", "Codex Agent", "Level 1", "XP 0 / 1000" }
                .Where(required => !allText.Contains(required))
                .ToArray();

            CollectionAssert.IsEmpty(missing);
            CollectionAssert.IsEmpty(
                UiVisibleTextScanner.FindForbiddenRuntimeText(root.gameObject, UiVisibleTextScanner.ProductUiForbiddenTextFragments));
        }
    }
}
#endif
