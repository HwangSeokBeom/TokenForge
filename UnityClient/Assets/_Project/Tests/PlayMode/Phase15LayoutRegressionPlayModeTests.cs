#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TokenForge.Client.Tests
{
    public sealed class Phase15LayoutRegressionPlayModeTests
    {
        [UnityTest]
        public IEnumerator RootLayoutAndPanelWiringRemainStable()
        {
            var fixture = Phase14UiFixture.Create(loggedIn: false);
            yield return null;

            var root = fixture.Root;
            Assert.IsNotNull(root.rootScrollRect);
            Assert.IsTrue(root.rootScrollRect.vertical);
            Assert.IsFalse(root.rootScrollRect.horizontal);
            Assert.IsNotNull(root.rootScrollRect.content);

            var content = root.rootScrollRect.content.transform;
            Assert.AreSame(content, root.accountPanel.transform.parent);
            Assert.AreSame(content, root.activityAnalysisPanel.transform.parent);
            Assert.AreSame(content, root.approvedLocationsPanel.transform.parent);
            Assert.AreSame(content, root.reviewPanel.transform.parent);
            Assert.AreSame(content, root.safeSyncPanel.transform.parent);
            Assert.AreSame(content, root.recentSessionsPanel.transform.parent);
            Assert.AreSame(content, root.privacyNoticePanel.transform.parent);

            Assert.IsTrue(root.accountPanel.gameObject.activeInHierarchy);
            Assert.IsTrue(root.activityAnalysisPanel.gameObject.activeInHierarchy);
            Assert.IsTrue(root.approvedLocationsPanel.gameObject.activeInHierarchy);
            Assert.IsTrue(root.reviewPanel.gameObject.activeInHierarchy);
            Assert.IsTrue(root.safeSyncPanel.gameObject.activeInHierarchy);
            Assert.IsTrue(root.recentSessionsPanel.gameObject.activeInHierarchy);
            Assert.IsTrue(root.privacyNoticePanel.gameObject.activeInHierarchy);

            fixture.Destroy();
        }

        [UnityTest]
        public IEnumerator DefaultButtonStatesAndRequiredContainersAreStable()
        {
            var fixture = Phase14UiFixture.Create(loggedIn: false);
            yield return null;

            Assert.IsTrue(fixture.Root.accountPanel.loginButton.interactable);
            Assert.IsTrue(fixture.Root.accountPanel.signupButton.interactable);
            Assert.IsFalse(fixture.Root.accountPanel.logoutButton.interactable);
            Assert.IsTrue(fixture.Root.activityAnalysisPanel.selectGitButton.interactable);
            Assert.IsFalse(fixture.Root.activityAnalysisPanel.analyzeGitButton.interactable);
            Assert.IsTrue(fixture.Root.activityAnalysisPanel.selectAgentButton.interactable);
            Assert.IsFalse(fixture.Root.activityAnalysisPanel.analyzeAgentButton.interactable);
            Assert.IsTrue(fixture.Root.safeSyncPanel.healthButton.interactable);
            Assert.IsFalse(fixture.Root.safeSyncPanel.syncButton.interactable);
            Assert.IsFalse(fixture.Root.safeSyncPanel.fetchButton.interactable);
            Assert.IsFalse(fixture.Root.safeSyncPanel.deleteRemoteButton.interactable);

            Assert.IsNotNull(fixture.Root.accountPanel.statusLabel);
            Assert.IsNotNull(fixture.Root.activityAnalysisPanel.gitStatusLabel);
            Assert.IsNotNull(fixture.Root.activityAnalysisPanel.agentStatusLabel);
            Assert.IsNotNull(fixture.Root.approvedLocationsPanel.statusLabel);
            Assert.IsNotNull(fixture.Root.reviewPanel.reviewLabel);
            Assert.IsNotNull(fixture.Root.safeSyncPanel.statusLabel);
            Assert.IsNotNull(fixture.Root.recentSessionsPanel.localRecentSessionsLabel);
            Assert.IsNotNull(fixture.Root.recentSessionsPanel.remoteSessionsLabel);
            Assert.IsNotNull(fixture.Root.privacyNoticePanel.bodyLabel);
            Assert.IsNotNull(fixture.Root.safeSyncPanel.remoteSessionsDropdown);
            Assert.IsNotNull(fixture.Root.approvedLocationsPanel.gitLocationsDropdown);
            Assert.IsNotNull(fixture.Root.approvedLocationsPanel.agentLocationsDropdown);
            Assert.AreEqual(InputField.ContentType.Password, fixture.Root.accountPanel.passwordInput.contentType);

            fixture.Destroy();
        }
    }
}
#endif
