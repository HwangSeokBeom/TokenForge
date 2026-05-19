#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using TokenForge.Client.Auth;
using TokenForge.Client.Sync;
using TokenForge.Client.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TokenForge.Client.Tests
{
    public sealed class Phase15BootstrapSceneSmokePlayModeTests
    {
        [UnityTest]
        public IEnumerator BootstrapSceneLoadsPrefabUiWithoutStartingRemoteOrAnalysisWork()
        {
            var load = SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            while (!load.isDone)
            {
                yield return null;
            }

            for (var i = 0; i < 8; i++)
            {
                yield return null;
            }

            var bootstrapper = Object.FindObjectOfType<AppBootstrapper>();
            var root = Object.FindObjectOfType<BootstrapRootView>();
            Assert.IsNotNull(bootstrapper);
            Assert.IsNotNull(root);
            Assert.IsTrue(bootstrapper.IsPrefabUiActive);

            Assert.IsTrue(root.ValidateReferences(out var error), error);
            Assert.IsNotNull(root.accountPanel);
            Assert.IsNotNull(root.activityAnalysisPanel);
            Assert.IsNotNull(root.approvedLocationsPanel);
            Assert.IsNotNull(root.reviewPanel);
            Assert.IsNotNull(root.safeSyncPanel);
            Assert.IsNotNull(root.recentSessionsPanel);
            Assert.IsNotNull(root.privacyNoticePanel);
            Assert.IsNotNull(bootstrapper.LocalStatus);
            Assert.IsNotNull(bootstrapper.ApprovedActivityAnalysis);
            Assert.IsNotNull(bootstrapper.GitAnalysisFlow);
            Assert.IsNotNull(bootstrapper.AgentAnalysisFlow);
            Assert.IsTrue(root.startScreenRoot.activeInHierarchy);
            Assert.IsFalse(root.settingsAdvancedRoot.activeInHierarchy);

            Assert.IsTrue(root.activityAnalysisPanel.selectGitButton.interactable);
            Assert.IsTrue(root.activityAnalysisPanel.selectAgentButton.interactable);
            Assert.IsTrue(root.activityAnalysisPanel.analyzeGitButton.interactable);
            Assert.IsTrue(root.activityAnalysisPanel.analyzeAgentButton.interactable);
            Assert.IsTrue(root.safeSyncPanel.healthButton.interactable);
            Assert.IsFalse(root.safeSyncPanel.syncButton.interactable);
            Assert.IsFalse(root.safeSyncPanel.fetchButton.interactable);
            Assert.AreEqual(AuthState.LoggedOut, bootstrapper.ApprovedActivityAnalysis.AuthState);
            Assert.AreEqual(SafeSyncStatus.Idle, bootstrapper.ApprovedActivityAnalysis.SafeSyncStatus);

            var visibleText = string.Join("\n", UiVisibleTextScanner.Collect(root.gameObject));
            Assert.That(visibleText, Does.Contain("Turn local development activity into RPG growth."));
            Assert.That(visibleText, Does.Contain("Start Game"));
            Assert.That(visibleText, Does.Contain("Run Analysis"));
            Assert.That(visibleText, Does.Contain("Safe Sync"));
            Assert.That(visibleText, Does.Contain("No saved run yet."));
            Assert.IsEmpty(UiVisibleTextScanner.FindForbiddenRuntimeText(root.gameObject));
            Assert.IsNotNull(root.GetComponentInChildren<ScrollRect>(true));
        }
    }
}
#endif
