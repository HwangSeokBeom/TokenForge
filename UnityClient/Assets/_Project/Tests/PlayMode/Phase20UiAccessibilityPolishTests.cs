#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TokenForge.Client.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TokenForge.Client.Tests
{
    public sealed class Phase20UiAccessibilityPolishTests
    {
        [UnityTest]
        public IEnumerator ReleaseCandidateUi_ExplainsDisabledStatesAndLocalRemoteBoundaries()
        {
            var fixture = Phase14UiFixture.Create(loggedIn: false);
            yield return null;

            var visibleText = string.Join("\n", UiVisibleTextScanner.Collect(fixture.Root.gameObject));

            Assert.That(visibleText, Does.Contain("Local analysis is available while logged out"));
            Assert.That(visibleText, Does.Contain("No login starts automatically"));
            Assert.That(visibleText, Does.Contain("Analyze Git stays disabled until a local repository is selected"));
            Assert.That(visibleText, Does.Contain("Analyze Agent stays disabled until a local provider location is selected"));
            Assert.That(visibleText, Does.Contain("No approved locations are present on clean install"));
            Assert.That(visibleText, Does.Contain("Remote sync actions are disabled until login"));
            Assert.That(visibleText, Does.Contain("No sync starts automatically"));
            Assert.That(visibleText, Does.Contain("Log in, then explicitly fetch remote summaries"));
            Assert.That(visibleText, Does.Contain("Raw paths, prompts, responses, commands"));
            Assert.That(visibleText, Does.Contain("Raw local data is never synced"));

            Assert.IsFalse(fixture.Root.activityAnalysisPanel.analyzeGitButton.interactable);
            Assert.IsFalse(fixture.Root.activityAnalysisPanel.analyzeAgentButton.interactable);
            Assert.IsFalse(fixture.Root.safeSyncPanel.syncButton.interactable);
            Assert.IsFalse(fixture.Root.safeSyncPanel.fetchButton.interactable);
            Assert.IsFalse(fixture.Root.safeSyncPanel.deleteRemoteButton.interactable);

            fixture.Destroy();
        }

        [UnityTest]
        public IEnumerator ReleaseCandidateUi_RequiredLabelsButtonsAndPasswordMaskRemainAccessibleAndSafe()
        {
            var fixture = Phase14UiFixture.Create(loggedIn: false);
            yield return null;

            var visible = UiVisibleTextScanner.Collect(fixture.Root.gameObject);
            foreach (var label in new[]
            {
                "Server base URL",
                "Email",
                "Display name",
                "Password",
                "Git window days",
                "Max Git commits",
                "Agent provider",
                "Agent window days",
                "Approved Git locations",
                "Approved agent locations",
                "Remote sessions",
                "Privacy"
            })
            {
                Assert.That(visible, Does.Contain(label), label);
            }

            foreach (var button in fixture.Root.GetComponentsInChildren<Button>(true))
            {
                var label = button.GetComponentsInChildren<Text>(true).FirstOrDefault();
                Assert.IsNotNull(label, button.name + " is missing visible text.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(label.text), button.name + " has empty visible text.");
                Assert.GreaterOrEqual(label.text.Trim().Length, 6, label.text);
            }

            Assert.AreEqual(InputField.ContentType.Password, fixture.Root.accountPanel.passwordInput.contentType);
            Assert.IsFalse(UiTextScanner.VisibleTextContains(fixture.Root.gameObject, "password123"));
            Assert.IsFalse(UiTextScanner.VisibleTextContains(fixture.Root.gameObject, "access-token-secret"));
            Assert.IsFalse(UiTextScanner.VisibleTextContains(fixture.Root.gameObject, fixture.RawRepositoryPath));
            Assert.IsFalse(UiTextScanner.VisibleTextContains(fixture.Root.gameObject, fixture.RawAgentLogPath));

            fixture.Destroy();
        }
    }
}
#endif
