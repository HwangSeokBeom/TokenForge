using System;

namespace TokenForge.Client.Git
{
    public static class GitExtensionCategoryClassifier
    {
        public static string Classify(string repositoryRelativePath)
        {
            if (string.IsNullOrWhiteSpace(repositoryRelativePath))
            {
                return "other";
            }

            var path = repositoryRelativePath.Replace('\\', '/').ToLowerInvariant();
            var fileName = LastSegment(path);

            if (IsTestPath(path, fileName)) return "test";
            if (fileName.EndsWith(".swift", StringComparison.Ordinal)) return "swift";
            if (fileName.EndsWith(".cs", StringComparison.Ordinal)) return "csharp";
            if (fileName.EndsWith(".ts", StringComparison.Ordinal) || fileName.EndsWith(".tsx", StringComparison.Ordinal)) return "typescript";
            if (fileName.EndsWith(".js", StringComparison.Ordinal) || fileName.EndsWith(".jsx", StringComparison.Ordinal)) return "javascript";
            if (fileName.EndsWith(".json", StringComparison.Ordinal)) return "json";
            if (fileName.EndsWith(".md", StringComparison.Ordinal) || fileName.EndsWith(".markdown", StringComparison.Ordinal)) return "markdown";
            if (IsConfig(path, fileName)) return "config";
            if (IsAsset(fileName)) return "asset";
            return "other";
        }

        private static bool IsTestPath(string path, string fileName)
        {
            return path.Contains("/test/") ||
                   path.Contains("/tests/") ||
                   path.Contains("/spec/") ||
                   fileName.Contains(".test.") ||
                   fileName.Contains(".tests.") ||
                   fileName.Contains(".spec.") ||
                   fileName.EndsWith("tests.cs", StringComparison.Ordinal);
        }

        private static bool IsConfig(string path, string fileName)
        {
            return path.StartsWith(".github/", StringComparison.Ordinal) ||
                   path.Contains("/config/") ||
                   path.Contains("/settings/") ||
                   path.Contains("/ci/") ||
                   fileName == ".gitignore" ||
                   fileName == ".editorconfig" ||
                   fileName == "dockerfile" ||
                   fileName.Contains("package") ||
                   fileName.EndsWith(".yml", StringComparison.Ordinal) ||
                   fileName.EndsWith(".yaml", StringComparison.Ordinal) ||
                   fileName.EndsWith(".toml", StringComparison.Ordinal) ||
                   fileName.EndsWith(".xml", StringComparison.Ordinal) ||
                   fileName.EndsWith(".asmdef", StringComparison.Ordinal) ||
                   fileName.EndsWith(".csproj", StringComparison.Ordinal) ||
                   fileName.EndsWith(".sln", StringComparison.Ordinal);
        }

        private static bool IsAsset(string fileName)
        {
            return fileName.EndsWith(".png", StringComparison.Ordinal) ||
                   fileName.EndsWith(".jpg", StringComparison.Ordinal) ||
                   fileName.EndsWith(".jpeg", StringComparison.Ordinal) ||
                   fileName.EndsWith(".gif", StringComparison.Ordinal) ||
                   fileName.EndsWith(".webp", StringComparison.Ordinal) ||
                   fileName.EndsWith(".svg", StringComparison.Ordinal) ||
                   fileName.EndsWith(".prefab", StringComparison.Ordinal) ||
                   fileName.EndsWith(".unity", StringComparison.Ordinal) ||
                   fileName.EndsWith(".asset", StringComparison.Ordinal) ||
                   fileName.EndsWith(".mp3", StringComparison.Ordinal) ||
                   fileName.EndsWith(".wav", StringComparison.Ordinal);
        }

        private static string LastSegment(string path)
        {
            var index = path.LastIndexOf('/');
            return index >= 0 ? path.Substring(index + 1) : path;
        }
    }
}
