using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TokenForge.Client.Sync;

namespace TokenForge.Client.UI
{
    public static class UiVisibleTextScanner
    {
        public static readonly string[] ForbiddenRuntimeTextFragments =
        {
            "access-token",
            "refresh-token",
            "rawPath",
            "filePath",
            "localPath",
            "repoName",
            "branchName",
            "rawLog",
            "sourceText",
            "apiKey",
            "request JSON",
            "response JSON",
            "/Users/",
            "\\Users\\",
            "{\""
        };

        public static IReadOnlyList<string> Collect(GameObject root, bool includeInactive = false)
        {
            if (root == null)
            {
                return Array.Empty<string>();
            }

            return root.GetComponentsInChildren<Text>(includeInactive)
                .Select(text => text.text ?? string.Empty)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList();
        }

        public static bool Contains(GameObject root, string fragment, bool includeInactive = false)
        {
            if (string.IsNullOrWhiteSpace(fragment))
            {
                return false;
            }

            return Collect(root, includeInactive).Any(text => text.IndexOf(fragment, StringComparison.Ordinal) >= 0);
        }

        public static IReadOnlyList<string> FindForbiddenRuntimeText(GameObject root, IEnumerable<string> extraForbiddenFragments = null)
        {
            var allText = string.Join("\n", Collect(root));
            return FindForbiddenRuntimeText(allText, extraForbiddenFragments);
        }

        public static IReadOnlyList<string> FindForbiddenRuntimeText(SafeSyncConfirmationRequest request, IEnumerable<string> extraForbiddenFragments = null)
        {
            var allText = request == null
                ? string.Empty
                : string.Join("\n", SafeSyncConfirmationRequestFactory.AllText(request));
            return FindForbiddenRuntimeText(allText, extraForbiddenFragments);
        }

        public static IReadOnlyList<string> FindForbiddenRuntimeText(string allText, IEnumerable<string> extraForbiddenFragments = null)
        {
            allText = allText ?? string.Empty;
            var fragments = ForbiddenRuntimeTextFragments
                .Concat(extraForbiddenFragments ?? Array.Empty<string>())
                .Where(fragment => !string.IsNullOrWhiteSpace(fragment))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            return fragments
                .Where(fragment => allText.IndexOf(fragment, StringComparison.Ordinal) >= 0)
                .ToList();
        }
    }
}
