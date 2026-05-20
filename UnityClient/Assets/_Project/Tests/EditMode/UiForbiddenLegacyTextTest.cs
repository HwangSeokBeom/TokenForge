#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using TokenForge.Client.Editor;
using TokenForge.Client.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TokenForge.Client.Tests
{
    public sealed class UiForbiddenLegacyTextTest
    {
        private const string BootstrapRootPrefabPath = "Assets/_Project/Prefabs/UI/BootstrapRoot.prefab";

        [Test]
        public void BootstrapRootPrefabHasNoForbiddenLegacyVisibleText()
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(BootstrapRootPrefabPath);
            try
            {
                CollectionAssert.IsEmpty(ForbiddenMatches(prefabRoot, includeInactive: false));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        [Test]
        public void TokenForgeMainSceneInstanceHasNoForbiddenLegacyVisibleText()
        {
            EditorSceneManager.OpenScene(TokenForgeStartupSceneSettings.StartupScenePath, OpenSceneMode.Single);
            var root = UnityEngine.Object.FindObjectOfType<BootstrapRootView>();

            Assert.IsNotNull(root);
            CollectionAssert.IsEmpty(ForbiddenMatches(root.gameObject, includeInactive: false));
        }

        private static string[] ForbiddenMatches(GameObject root, bool includeInactive)
        {
            var allText = string.Join("\n", UiVisibleTextScanner.Collect(root, includeInactive));
            return UiVisibleTextScanner.ProductUiForbiddenTextFragments
                .Where(fragment => allText.IndexOf(fragment, StringComparison.Ordinal) >= 0)
                .ToArray();
        }
    }
}
#endif
