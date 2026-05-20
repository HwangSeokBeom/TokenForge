using System;
using System.IO;
using System.Linq;
using TokenForge.Client.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TokenForge.Client.Editor
{
    [InitializeOnLoad]
    public static class ProductUiPlayModeSmokeCommand
    {
        private const int TimeoutFrames = 600;
        private const string ActiveKey = "TokenForge.ProductUiPlayModeSmoke.Active";
        private const string ResultPathKey = "TokenForge.ProductUiPlayModeSmoke.ResultPath";
        private static int frame;
        private static string resultPath;

        static ProductUiPlayModeSmokeCommand()
        {
            if (SessionState.GetBool(ActiveKey, false))
            {
                resultPath = SessionState.GetString(ResultPathKey, "/tmp/tokenforge-playmode-smoke.log");
                EditorApplication.update -= Poll;
                EditorApplication.update += Poll;
            }
        }

        public static void Run()
        {
            resultPath = GetArgument("-smokeResult", "/tmp/tokenforge-playmode-smoke.log");
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetString(ResultPathKey, resultPath);
            frame = 0;
            EditorSceneManager.OpenScene(TokenForgeStartupSceneSettings.StartupScenePath, OpenSceneMode.Single);
            EditorApplication.update += Poll;
            EditorApplication.isPlaying = true;
        }

        private static void Poll()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            frame++;
            try
            {
                var bootstrapper = UnityEngine.Object.FindObjectOfType<AppBootstrapper>();
                if (bootstrapper == null || !bootstrapper.IsBootstrapComplete)
                {
                    if (frame < TimeoutFrames)
                    {
                        return;
                    }

                    Fail("AppBootstrapper did not complete.");
                    return;
                }

                var root = UnityEngine.Object.FindObjectOfType<BootstrapRootView>();
                var canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
                var eventSystem = UnityEngine.Object.FindObjectOfType<EventSystem>();
                if (root == null || canvas == null || eventSystem == null)
                {
                    Fail("Canvas, EventSystem, or BootstrapRoot is missing.");
                    return;
                }

                if (!root.ValidateReferences(out var referenceError))
                {
                    Fail("BootstrapRoot references invalid: " + referenceError);
                    return;
                }

                if (!root.startScreenRoot.activeInHierarchy ||
                    !root.startGameButton.interactable ||
                    !root.startAnalyzeRepositoryButton.interactable ||
                    !root.startAnalyzeAgentLogsButton.interactable)
                {
                    Fail("Start screen or CTA buttons are not ready.");
                    return;
                }

                var visibleText = string.Join("\n", UiVisibleTextScanner.Collect(root.gameObject));
                var required = new[] { "TokenForge", "Start Game", "Add Repository", "Connect Codex Agent" };
                var missing = required.Where(text => !visibleText.Contains(text)).ToArray();
                if (missing.Length > 0)
                {
                    Fail("Missing visible text: " + string.Join(", ", missing));
                    return;
                }

                var forbidden = UiVisibleTextScanner.FindForbiddenRuntimeText(root.gameObject, UiVisibleTextScanner.ProductUiForbiddenTextFragments);
                if (forbidden.Count > 0)
                {
                    Fail("Forbidden visible text: " + string.Join(", ", forbidden));
                    return;
                }

                Pass(visibleText);
            }
            catch (Exception exception)
            {
                Fail(exception.GetType().Name + ": " + exception.Message);
            }
        }

        private static void Pass(string visibleText)
        {
            Finish(0, "PASS Product UI PlayMode smoke\nVisibleText:\n" + visibleText);
        }

        private static void Fail(string message)
        {
            Finish(1, "FAIL Product UI PlayMode smoke\n" + message);
        }

        private static void Finish(int exitCode, string message)
        {
            EditorApplication.update -= Poll;
            SessionState.EraseBool(ActiveKey);
            SessionState.EraseString(ResultPathKey);
            Directory.CreateDirectory(Path.GetDirectoryName(resultPath));
            File.WriteAllText(resultPath, message);
            Debug.Log(message);
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(exitCode);
        }

        private static string GetArgument(string name, string fallback)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.Ordinal))
                {
                    return args[i + 1];
                }
            }

            return fallback;
        }
    }
}
