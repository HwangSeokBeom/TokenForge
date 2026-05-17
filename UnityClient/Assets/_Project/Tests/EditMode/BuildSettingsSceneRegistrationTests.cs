#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using TokenForge.Client.Editor;
using TokenForge.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace TokenForge.Client.Tests
{
    public sealed class BuildSettingsSceneRegistrationTests
    {
        [Test]
        public void TokenForgeMainIsFirstEnabledBuildScene()
        {
            var scenes = EditorBuildSettings.scenes;
            Assert.That(scenes, Is.Not.Empty);
            Assert.AreEqual(TokenForgeStartupSceneSettings.StartupScenePath, scenes[0].path);
            Assert.IsTrue(scenes[0].enabled);

            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(TokenForgeStartupSceneSettings.StartupScenePath);
            Assert.IsNotNull(sceneAsset);
            Assert.AreEqual(AssetDatabase.AssetPathToGUID(TokenForgeStartupSceneSettings.StartupScenePath), scenes[0].guid.ToString());
            Assert.That(scenes.Count(scene => scene.path == TokenForgeStartupSceneSettings.StartupScenePath), Is.EqualTo(1));
        }

        [Test]
        public void EditorPlayModeAndMacOSSmokeBuildUseTokenForgeMain()
        {
            TokenForgeStartupSceneSettings.ConfigureEditorPlayModeStartScene();

            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(TokenForgeStartupSceneSettings.StartupScenePath);
            Assert.AreEqual(sceneAsset, EditorSceneManager.playModeStartScene);
            Assert.AreEqual(TokenForgeStartupSceneSettings.StartupScenePath, MacOSBuildSmokeCommand.BootstrapScenePath);
            Assert.DoesNotThrow(MacOSBuildSmokeCommand.ValidateProjectState);
        }
    }
}
#endif
