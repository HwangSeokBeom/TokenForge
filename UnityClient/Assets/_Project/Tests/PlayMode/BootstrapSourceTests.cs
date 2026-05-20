#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using TokenForge.Client.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TokenForge.Client.Tests
{
    public sealed class BootstrapSourceTests
    {
        [UnityTest]
        public IEnumerator BootstrapRootIsCreatedFromPrefabPathOnly()
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
            var marker = root.GetComponent<BootstrapRootSourceMarker>();
            Assert.IsNotNull(marker, "BootstrapRootSourceMarker is missing.");
            Assert.AreEqual("prefab", marker.Source);
            Assert.AreEqual(BootstrapRootSourceMarker.PrefabPath, marker.PrefabPathValue);
            Assert.AreEqual(BootstrapRootSourceMarker.CurrentUiVersion, marker.UiVersion);
            Assert.AreEqual("prefab", bootstrapper.LastBootstrapRootSource);
            Assert.AreEqual(BootstrapRootSourceMarker.PrefabPath, bootstrapper.LastBootstrapRootPrefabPath);
            Assert.AreEqual(BootstrapRootSourceMarker.CurrentUiVersion, bootstrapper.LastBootstrapRootUiVersion);
        }
    }
}
#endif
