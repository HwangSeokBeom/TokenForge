#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine;
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
            Assert.AreSame(root.transform, root.rootScrollRect.transform.parent);
            Assert.AreSame(content, root.startScreenRoot.transform.parent);
            Assert.AreSame(content, root.gameDashboardRoot.transform.parent);
            Assert.AreSame(content, root.settingsAdvancedRoot.transform.parent);
            Assert.AreSame(root.settingsAdvancedRoot.transform, root.accountPanel.transform.parent);
            Assert.AreSame(root.settingsAdvancedRoot.transform, root.privacyNoticePanel.transform.parent);
            Assert.AreSame(root.runAnalysisRoot.transform.Find("Run Analysis Grid"), root.activityAnalysisPanel.transform.parent);
            Assert.AreSame(root.runAnalysisRoot.transform.Find("Run Analysis Grid"), root.reviewPanel.transform.parent);
            Assert.AreSame(root.developerDiagnosticsRoot.transform, root.approvedLocationsPanel.transform.parent);
            Assert.AreSame(root.developerDiagnosticsRoot.transform, root.safeSyncPanel.transform.parent);
            Assert.AreSame(root.developerDiagnosticsRoot.transform, root.recentSessionsPanel.transform.parent);

            root.ShowSettings();
            yield return null;

            Assert.IsTrue(root.accountPanel.gameObject.activeInHierarchy);
            Assert.IsTrue(root.privacyNoticePanel.gameObject.activeInHierarchy);
            Assert.IsFalse(root.activityAnalysisPanel.gameObject.activeInHierarchy);
            Assert.IsFalse(root.safeSyncPanel.gameObject.activeInHierarchy);

            root.ShowRunAnalysis();
            yield return null;
            Assert.IsTrue(root.activityAnalysisPanel.gameObject.activeInHierarchy);
            Assert.IsTrue(root.reviewPanel.gameObject.activeInHierarchy);

            root.ShowDeveloperDiagnostics();
            yield return null;
            Assert.IsTrue(root.approvedLocationsPanel.gameObject.activeInHierarchy);
            Assert.IsTrue(root.safeSyncPanel.gameObject.activeInHierarchy);
            Assert.IsTrue(root.recentSessionsPanel.gameObject.activeInHierarchy);

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
            Assert.IsTrue(fixture.Root.activityAnalysisPanel.analyzeGitButton.interactable);
            Assert.IsTrue(fixture.Root.activityAnalysisPanel.selectAgentButton.interactable);
            Assert.IsTrue(fixture.Root.activityAnalysisPanel.analyzeAgentButton.interactable);
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

        [UnityTest]
        public IEnumerator RootScrollContentCanExceedViewportAndRemainScrollable()
        {
            Screen.SetResolution(1280, 720, false);
            var fixture = Phase14UiFixture.Create(loggedIn: false);
            yield return null;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(fixture.Root.rootScrollRect.content);

            var scroll = fixture.Root.rootScrollRect;
            Assert.IsTrue(scroll.vertical);
            Assert.IsFalse(scroll.horizontal);
            Assert.AreEqual(ScrollRect.MovementType.Clamped, scroll.movementType);
            Assert.IsNotNull(scroll.viewport);
            Assert.IsNotNull(scroll.content);
            Assert.Greater(scroll.content.rect.height, scroll.viewport.rect.height, "Onboarding content should be taller than a 720p viewport and rely on ScrollRect reachability.");

            var before = scroll.verticalNormalizedPosition;
            scroll.verticalNormalizedPosition = 0f;
            yield return null;
            Assert.Less(scroll.verticalNormalizedPosition, before, "Root Scroll did not accept a vertical scroll position change.");

            fixture.Destroy();
        }
    }
}
#endif
