using System;
using TokenForge.Client.Domain;

namespace TokenForge.Client.Git
{
    public static class GitPathClassifier
    {
        public static FileCategory Classify(string repositoryRelativePath)
        {
            if (string.IsNullOrWhiteSpace(repositoryRelativePath))
            {
                return FileCategory.Unknown;
            }

            var path = repositoryRelativePath.Replace('\\', '/').ToLowerInvariant();
            var fileName = LastSegment(path);

            if (path.Contains("/test/") || path.Contains("/tests/") || path.Contains("/spec/") ||
                fileName.Contains(".test.") || fileName.Contains(".tests.") || fileName.Contains(".spec.") ||
                fileName.EndsWith("tests.cs", StringComparison.Ordinal))
            {
                return FileCategory.Test;
            }

            if (path.StartsWith("docs/", StringComparison.Ordinal) || path.Contains("/docs/") ||
                fileName == "readme.md" || fileName.EndsWith(".md", StringComparison.Ordinal))
            {
                return FileCategory.Docs;
            }

            if (path.Contains("/ui/") || path.Contains("/view/") || path.Contains("/views/") ||
                path.Contains("/screen/") || path.Contains("/screens/") || path.Contains("/component/") ||
                path.Contains("/components/") || path.Contains("/prefab/") || fileName.EndsWith(".prefab", StringComparison.Ordinal))
            {
                return FileCategory.UI;
            }

            if (path.Contains("/service/") || path.Contains("/services/") || path.Contains("/repository/") ||
                path.Contains("/repositories/") || path.Contains("/usecase/") || path.Contains("/usecases/") ||
                path.Contains("/di/") || path.Contains("/container/") || path.Contains("/architecture/"))
            {
                return FileCategory.Architecture;
            }

            if (path.StartsWith(".github/", StringComparison.Ordinal) || path.Contains("/config/") ||
                path.Contains("/settings/") || path.Contains("/ci/") || fileName.Contains("package") ||
                fileName.EndsWith(".asmdef", StringComparison.Ordinal) || fileName.EndsWith(".json", StringComparison.Ordinal))
            {
                return FileCategory.Config;
            }

            if (path.Contains("/domain/") || path.Contains("/model/") || path.Contains("/models/") ||
                path.Contains("/entity/") || path.Contains("/entities/"))
            {
                return FileCategory.Domain;
            }

            return FileCategory.Unknown;
        }

        private static string LastSegment(string path)
        {
            var index = path.LastIndexOf('/');
            return index >= 0 ? path.Substring(index + 1) : path;
        }
    }
}
