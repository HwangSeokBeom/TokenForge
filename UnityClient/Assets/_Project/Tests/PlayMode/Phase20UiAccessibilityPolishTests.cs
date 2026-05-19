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

            Assert.That(visibleText, Does.Contain("Start Game"));
            Assert.That(visibleText, Does.Contain("Run Analysis"));
            Assert.That(visibleText, Does.Contain("Safe Sync"));
            Assert.That(visibleText, Does.Contain("No saved run yet."));
            Assert.That(visibleText, Does.Contain("Raw paths, prompts, logs, source code, diffs, and file names stay local."));

            Assert.IsTrue(fixture.Root.activityAnalysisPanel.analyzeGitButton.interactable);
            Assert.IsTrue(fixture.Root.activityAnalysisPanel.analyzeAgentButton.interactable);
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
                "TokenForge",
                "Start Game",
                "Run Analysis",
                "Safe Sync"
            })
            {
                Assert.That(visible, Does.Contain(label), label);
            }
            Assert.That(string.Join("\n", visible), Does.Contain("No saved run yet."));

            foreach (var button in fixture.Root.GetComponentsInChildren<Button>(true))
            {
                var label = button.GetComponentsInChildren<Text>(true).FirstOrDefault();
                Assert.IsNotNull(label, button.name + " is missing visible text.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(label.text), button.name + " has empty visible text.");
                Assert.GreaterOrEqual(label.text.Trim().Length, 4, label.text);
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
