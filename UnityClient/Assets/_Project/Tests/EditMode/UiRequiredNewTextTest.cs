#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using TokenForge.Client.UI;
using UnityEditor;
using UnityEngine;

namespace TokenForge.Client.Tests
{
    public sealed class UiRequiredNewTextTest
    {
        private static readonly string[] RequiredText =
        {
            "TokenForge",
            "Dashboard",
            "Repository",
            "Codex Agent",
            "Activity",
            "Settings",
            "Your companion is ready to grow",
            "Start Game",
            "Add Repository",
            "Connect Codex Agent",
            "Review Activity",
            "Level 1",
            "XP 0 / 1000"
        };

        private static readonly string[] RequiredObjects =
        {
            "WindowShell",
            "Sidebar",
            "TopBar",
            "HeroCompanionCard",
            "StatusCardGrid",
            "RepositoryStatusCard",
            "CodexAgentStatusCard",
            "ReviewActivityCard",
            "ProgressCard"
        };

        [Test]
        public void BootstrapRootPrefabContainsRequiredProductText()
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/UI/BootstrapRoot.prefab");
            try
            {
                var allText = string.Join("\n", UiVisibleTextScanner.Collect(prefabRoot, includeInactive: true));
                var missing = RequiredText
                    .Where(required => allText.IndexOf(required, StringComparison.Ordinal) < 0)
                    .ToArray();

                CollectionAssert.IsEmpty(missing);

                var missingObjects = RequiredObjects
                    .Where(required => prefabRoot.transform.FindDeep(required) == null)
                    .ToArray();

                CollectionAssert.IsEmpty(missingObjects);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
    }

    internal static class TransformTestExtensions
    {
        public static Transform FindDeep(this Transform parent, string name)
        {
            if (parent == null)
            {
                return null;
            }

            if (parent.name == name)
            {
                return parent;
            }

            for (var i = 0; i < parent.childCount; i++)
            {
                var match = parent.GetChild(i).FindDeep(name);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }
    }
}
#endif
