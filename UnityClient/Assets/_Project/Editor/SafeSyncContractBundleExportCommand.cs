using System;
using System.Linq;
using TokenForge.Client.Sync;
using UnityEditor;
using UnityEngine;

namespace TokenForge.Editor
{
    public static class SafeSyncContractBundleExportCommand
    {
        private const string OutputArgumentName = "-contractBundleOutput";

        public static void Export()
        {
            try
            {
                var outputPath = GetArgumentValue(OutputArgumentName);
                var result = SafeSyncContractBundleExporter.Export(outputPath);

                Debug.Log("[TokenForge] Safe Sync contract bundle export completed.");
                Debug.Log("[TokenForge] Output path: " + result.OutputPath);
                Debug.Log("[TokenForge] Bundle version: " + result.BundleVersion);
                Debug.Log("[TokenForge] Schema version: " + result.SchemaVersion);
                Debug.Log("[TokenForge] Valid session count: " + result.ValidSessionCount);
                Debug.Log("[TokenForge] Provider enums: " + string.Join(",", result.SupportedSourceProviders));
            }
            catch (Exception exception)
            {
                Debug.LogError("[TokenForge] Safe Sync contract bundle export failed: " + exception.GetType().Name);
                EditorApplication.Exit(1);
            }
        }

        private static string GetArgumentValue(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.FindIndex(args, arg => string.Equals(arg, name, StringComparison.Ordinal));
            if (index < 0)
            {
                return null;
            }

            var valueIndex = index + 1;
            if (valueIndex >= args.Length || string.IsNullOrWhiteSpace(args[valueIndex]) || args[valueIndex].StartsWith("-", StringComparison.Ordinal))
            {
                throw new ArgumentException(name + " requires an output path value.");
            }

            return args[valueIndex];
        }
    }
}
