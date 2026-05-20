#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using TokenForge.Client.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TokenForge.Client.Tests
{
    public sealed class RuntimeBindingRegressionTests
    {
        [UnityTest]
        public IEnumerator BootstrapRootRefreshDoesNotReintroduceLegacyDashboardText()
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

            Assert.IsNotNull(bootstrapper);
            var root = Object.FindObjectOfType<BootstrapRootView>();
            Assert.IsNotNull(root);

            root.Bind(bootstrapper.LocalStatus, bootstrapper.ApprovedActivityAnalysis);
            root.Render();
            yield return null;

            CollectionAssert.IsEmpty(
                UiVisibleTextScanner.FindForbiddenRuntimeText(root.gameObject, UiVisibleTextScanner.ProductUiForbiddenTextFragments),
                "BootstrapRootView.Bind/Render reintroduced legacy dashboard text.");
        }
    }
}
#endif
