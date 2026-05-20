#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using TokenForge.Client.Editor;
using UnityEditor;

namespace TokenForge.Client.Tests
{
    public sealed class BuildSettingsValidationTest
    {
        [Test]
        public void EnabledBuildSceneIsOnlyTokenForgeMain()
        {
            var enabledScenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).ToArray();

            Assert.That(enabledScenes, Has.Length.EqualTo(1));
            Assert.AreEqual(TokenForgeStartupSceneSettings.StartupScenePath, enabledScenes[0].path);
        }

        [Test]
        public void BuildIndexZeroIsTokenForgeMain()
        {
            var scenes = EditorBuildSettings.scenes;

            Assert.That(scenes, Is.Not.Empty);
            Assert.IsTrue(scenes[0].enabled);
            Assert.AreEqual(TokenForgeStartupSceneSettings.StartupScenePath, scenes[0].path);
        }
    }
}
#endif
