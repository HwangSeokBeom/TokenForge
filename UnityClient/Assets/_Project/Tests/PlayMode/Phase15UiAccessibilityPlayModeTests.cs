#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TokenForge.Client.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TokenForge.Client.Tests
{
    public sealed class Phase15UiAccessibilityPlayModeTests
    {
        [UnityTest]
        public IEnumerator PrefabUiHasVisibleLabelsAndMeaningfulActions()
        {
            var fixture = Phase14UiFixture.Create();
            yield return null;

            var visible = UiVisibleTextScanner.Collect(fixture.Root.gameObject);
            Assert.That(visible, Does.Contain("TokenForge"));
            Assert.That(visible, Does.Contain("Turn local development activity into RPG growth."));
            Assert.That(visible, Does.Contain("Start Game"));
            Assert.That(visible, Does.Contain("Run Analysis"));
            Assert.That(visible, Does.Contain("Safe Sync"));
            Assert.That(string.Join("\n", visible), Does.Contain("No saved run yet."));

            Assert.AreEqual(InputField.ContentType.Password, fixture.Root.accountPanel.passwordInput.contentType);
            foreach (var button in fixture.Root.GetComponentsInChildren<Button>(true))
            {
                var label = button.GetComponentsInChildren<Text>(true).FirstOrDefault();
                Assert.IsNotNull(label, button.name + " is missing visible button text.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(label.text), button.name + " has empty button text.");
                Assert.GreaterOrEqual(label.text.Trim().Length, 4, "Button label should be descriptive: " + label.text);
            }

            fixture.Destroy();
        }

        [UnityTest]
        public IEnumerator EmptyStatesAndPrivacyNoticeAreVisibleAndSafe()
        {
            var fixture = Phase14UiFixture.Create(loggedIn: false);
            yield return null;

            var visibleText = string.Join("\n", UiVisibleTextScanner.Collect(fixture.Root.gameObject));
            Assert.That(visibleText, Does.Contain("Start Game opens the dashboard without login or sync."));
            Assert.That(visibleText, Does.Contain("Saving the review is the only step that grants XP."));
            Assert.That(visibleText, Does.Contain("Local mode · Sync optional"));
            Assert.That(visibleText, Does.Contain("Raw paths, prompts, logs, source code, diffs, and file names stay local."));

            var forbidden = UiVisibleTextScanner.FindForbiddenRuntimeText(fixture.Root.gameObject, new[]
            {
                "password123",
                "access-token-secret",
                "refresh-token-secret",
                fixture.RawRepositoryPath,
                fixture.RawAgentLogPath,
                "SecretRepo",
                "feature/customer-token",
                "git status --short",
                "please inspect the production auth token flow",
                "the response includes customer secret handling",
                "public class Secret"
            });
            Assert.IsEmpty(forbidden, "Forbidden visible runtime text: " + string.Join(", ", forbidden));

            fixture.Destroy();
        }
    }
}
#endif
