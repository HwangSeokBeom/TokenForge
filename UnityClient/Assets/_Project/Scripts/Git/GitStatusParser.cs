using System;
using System.Collections.Generic;
using TokenForge.Client.Domain;

namespace TokenForge.Client.Git
{
    public static class GitStatusParser
    {
        public static List<FileCategory> ParseCategories(string statusShortOutput)
        {
            var categories = new List<FileCategory>();
            if (string.IsNullOrWhiteSpace(statusShortOutput))
            {
                return categories;
            }

            var lines = statusShortOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.Length < 4)
                {
                    continue;
                }

                var relativePath = line.Substring(3).Trim();
                var renameMarker = relativePath.IndexOf(" -> ", StringComparison.Ordinal);
                if (renameMarker >= 0)
                {
                    relativePath = relativePath.Substring(renameMarker + 4);
                }

                categories.Add(GitPathClassifier.Classify(relativePath));
            }

            return categories;
        }
    }
}
