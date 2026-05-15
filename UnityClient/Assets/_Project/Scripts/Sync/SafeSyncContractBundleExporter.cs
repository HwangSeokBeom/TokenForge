using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace TokenForge.Client.Sync
{
    public sealed class SafeSyncContractBundleExportResult
    {
        public string OutputPath { get; set; } = string.Empty;
        public int BundleVersion { get; set; }
        public int SchemaVersion { get; set; }
        public int ValidSessionCount { get; set; }
        public string[] SupportedSourceProviders { get; set; } = new string[0];
    }

    public static class SafeSyncContractBundleExporter
    {
        public const string DefaultRelativeOutputPath = "artifacts/client-contract/unity-safe-sync-contract-v1.bundle.json";

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };

        public static SafeSyncContractBundleExportResult Export(string outputPath = null, DateTimeOffset? generatedAt = null)
        {
            var resolvedOutputPath = ResolveOutputPath(outputPath);
            var bundle = SafeSyncContractBundleFixtures.CreateBundle(generatedAt ?? DateTimeOffset.UtcNow);
            var json = ToJson(bundle);

            var directory = Path.GetDirectoryName(resolvedOutputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(resolvedOutputPath, json, new UTF8Encoding(false));

            return new SafeSyncContractBundleExportResult
            {
                OutputPath = resolvedOutputPath,
                BundleVersion = bundle.BundleVersion,
                SchemaVersion = bundle.ClientSchemaVersion,
                ValidSessionCount = bundle.Fixtures?.Valid?.Sessions?.Count ?? 0,
                SupportedSourceProviders = bundle.SupportedSourceProviders.ToArray()
            };
        }

        public static string ToJson(object bundle)
        {
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));
            return JsonConvert.SerializeObject(bundle, Settings);
        }

        public static SafeSyncContractBundle FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("JSON is required.", nameof(json));
            return JsonConvert.DeserializeObject<SafeSyncContractBundle>(json, Settings);
        }

        public static string ResolveOutputPath(string outputPath = null)
        {
            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                return Path.GetFullPath(outputPath);
            }

#if UNITY_5_3_OR_NEWER
            var projectPath = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
#else
            var projectPath = Directory.GetCurrentDirectory();
#endif
            return Path.GetFullPath(Path.Combine(projectPath, DefaultRelativeOutputPath));
        }
    }
}
