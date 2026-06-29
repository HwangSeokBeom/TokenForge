using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TokenForge.Editor
{
    public static class MacOSBuildSmokeCommand
    {
        public const string BootstrapScenePath = TokenForge.Client.Editor.TokenForgeStartupSceneSettings.StartupScenePath;
        public const string DefaultBuildOutput = "/private/tmp/tokenforge-macos-build/TokenForge.app";

        public static void Build()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                var outputPath = GetArgument(args, "-buildOutput", DefaultBuildOutput);
                var developmentBuild = GetBoolArgument(args, "-developmentBuild", false);
                var cleanBuild = GetBoolArgument(args, "-cleanBuild", true);

                Build(outputPath, developmentBuild, cleanBuild);
            }
            catch (Exception exception)
            {
                Debug.LogError("TokenForge macOS build smoke failed: " + SafeError(exception.Message));
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                    return;
                }

                throw;
            }
        }

        public static BuildReport Build(string outputPath, bool developmentBuild = false, bool cleanBuild = true)
        {
            TokenForge.Client.Editor.TokenForgeBootstrapSceneBuilder.BuildMainScene();
            ValidateProjectState();
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("Build output path is required.", nameof(outputPath));
            }

            CleanMacOSBuildOutput(outputPath);

            var options = BuildOptions.None;
            if (developmentBuild)
            {
                options |= BuildOptions.Development;
            }

            if (cleanBuild)
            {
                options |= BuildOptions.CleanBuildCache;
            }

            Debug.Log("TokenForge macOS build smoke starting.");
            Debug.Log("Build target: StandaloneOSX");
            Debug.Log("Build scene: " + TokenForge.Client.Editor.TokenForgeStartupSceneSettings.StartupSceneName + ".unity");
            Debug.Log("Build output: " + outputPath);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { BootstrapScenePath },
                locationPathName = outputPath,
                target = BuildTarget.StandaloneOSX,
                options = options
            });

            var summary = report.summary;
            Debug.Log("TokenForge macOS build smoke result: " + summary.result);
            Debug.Log("Build target: " + summary.platform);
            Debug.Log("Build output: " + outputPath);

            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException("BuildPipeline returned " + summary.result + ".");
            }

            return report;
        }

        public static void CleanMacOSBuildOutput(string outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("Build output path is required.", nameof(outputPath));
            }

            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new ArgumentException("Build output directory is required.", nameof(outputPath));
            }

            Directory.CreateDirectory(outputDirectory);
            Debug.Log("INFO [BuildPipeline][CLEAN_OUTPUT] directory=" + outputDirectory + " canonicalOutput=" + outputPath);

            foreach (var appBundle in Directory.GetDirectories(outputDirectory, "TokenForge*.app", SearchOption.TopDirectoryOnly))
            {
                Debug.Log("INFO [BuildPipeline][CLEAN_OUTPUT] removeApp=" + appBundle);
                Directory.Delete(appBundle, true);
            }

            foreach (var monoCrash in Directory.GetFiles(outputDirectory, "mono_crash*.json", SearchOption.TopDirectoryOnly))
            {
                Debug.Log("INFO [BuildPipeline][CLEAN_OUTPUT] removeMonoCrash=" + monoCrash);
                File.Delete(monoCrash);
            }
        }

        public static void ValidateProjectState()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapScenePath) == null)
            {
                throw new FileNotFoundException(TokenForge.Client.Editor.TokenForgeStartupSceneSettings.StartupSceneName + ".unity is missing.");
            }

            var buildScene = EditorBuildSettings.scenes.FirstOrDefault(scene => scene.path == BootstrapScenePath);
            if (buildScene == null || !buildScene.enabled)
            {
                throw new InvalidOperationException(TokenForge.Client.Editor.TokenForgeStartupSceneSettings.StartupSceneName + ".unity is not enabled in EditorBuildSettings.");
            }
        }

        public static string GetArgument(string[] args, string name, string fallback = "")
        {
            if (args == null)
            {
                return fallback;
            }

            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.Ordinal))
                {
                    return args[i + 1];
                }
            }

            return fallback;
        }

        public static bool GetBoolArgument(string[] args, string name, bool fallback)
        {
            var value = GetArgument(args, name, string.Empty);
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            return bool.TryParse(value, out var parsed) ? parsed : fallback;
        }

        private static string SafeError(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return "unknown_error";
            }

            return message
                .Replace(Environment.UserName, "<user>")
                .Replace(Directory.GetCurrentDirectory(), "<project>");
        }
    }

    public sealed class MacOSBuildOutputCleaner : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report == null || report.summary.platform != BuildTarget.StandaloneOSX)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(report.summary.outputPath))
            {
                return;
            }

            MacOSBuildSmokeCommand.CleanMacOSBuildOutput(report.summary.outputPath);
        }
    }
}
