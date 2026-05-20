using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TokenForge.Client.Editor
{
    [InitializeOnLoad]
    public static class TokenForgeStartupSceneSettings
    {
        public const string StartupScenePath = "Assets/_Project/Scenes/TokenForgeMain.unity";
        public const string LegacyBootstrapScenePath = "Assets/_Project/Scenes/Bootstrap.unity";
        public const string StartupSceneName = "TokenForgeMain";

        static TokenForgeStartupSceneSettings()
        {
            if (IsCommandLineTestRun())
            {
                EditorApplication.delayCall += ClearEditorPlayModeStartSceneForCommandLineTests;
            }
            else
            {
                EditorApplication.delayCall += ConfigureEditorPlayModeStartScene;
            }
        }

        public static void ConfigureEditorPlayModeStartScene()
        {
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(StartupScenePath);
            if (sceneAsset == null)
            {
                return;
            }

            if (EditorSceneManager.playModeStartScene != sceneAsset)
            {
                EditorSceneManager.playModeStartScene = sceneAsset;
            }
        }

        private static void ClearEditorPlayModeStartSceneForCommandLineTests()
        {
            if (EditorSceneManager.playModeStartScene != null)
            {
                EditorSceneManager.playModeStartScene = null;
            }
        }

        private static bool IsCommandLineTestRun()
        {
            foreach (var argument in System.Environment.GetCommandLineArgs())
            {
                if (string.Equals(argument, "-runTests", System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public static void EnsureStartupSceneIsFirstInBuildSettings()
        {
            var startupScene = new EditorBuildSettingsScene(StartupScenePath, true)
            {
                guid = new GUID(AssetDatabase.AssetPathToGUID(StartupScenePath))
            };

            EditorBuildSettings.scenes = new[] { startupScene };
        }
    }
}
