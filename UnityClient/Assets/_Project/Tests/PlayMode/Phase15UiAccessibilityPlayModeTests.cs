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
            Assert.That(visible, Does.Contain("Privacy-safe developer activity companion"));
            Assert.That(visible, Does.Contain("Account"));
            Assert.That(visible, Does.Contain("Local Analysis"));
            Assert.That(visible, Does.Contain("Approved Local Locations"));
            Assert.That(visible, Does.Contain("Review"));
            Assert.That(visible, Does.Contain("Safe Sync"));
            Assert.That(visible, Does.Contain("Recent Sessions"));
            Assert.That(visible, Does.Contain("Privacy"));
            Assert.That(visible, Does.Contain("Server base URL"));
            Assert.That(visible, Does.Contain("Email"));
            Assert.That(visible, Does.Contain("Display name"));
            Assert.That(visible, Does.Contain("Password"));

            Assert.AreEqual(InputField.ContentType.Password, fixture.Root.accountPanel.passwordInput.contentType);
            foreach (var button in fixture.Root.GetComponentsInChildren<Button>(true))
            {
                var label = button.GetComponentsInChildren<Text>(true).FirstOrDefault();
                Assert.IsNotNull(label, button.name + " is missing visible button text.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(label.text), button.name + " has empty button text.");
                Assert.GreaterOrEqual(label.text.Trim().Length, 6, "Button label should be descriptive: " + label.text);
            }

            fixture.Destroy();
        }

        [UnityTest]
        public IEnumerator EmptyStatesAndPrivacyNoticeAreVisibleAndSafe()
        {
            var fixture = Phase14UiFixture.Create(loggedIn: false);
            yield return null;

            var visibleText = string.Join("\n", UiVisibleTextScanner.Collect(fixture.Root.gameObject));
            Assert.That(visibleText, Does.Contain("Local: no saved safe sessions yet."));
            Assert.That(visibleText, Does.Contain("Remote: no fetched remote safe sessions."));
            Assert.That(visibleText, Does.Contain("No approved locations"));
            Assert.That(visibleText, Does.Contain("No review ready."));
            Assert.That(visibleText, Does.Contain("Logged Out"));
            Assert.That(visibleText, Does.Contain("Approved locations"));
            Assert.That(visibleText, Does.Contain("never synced"));

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
