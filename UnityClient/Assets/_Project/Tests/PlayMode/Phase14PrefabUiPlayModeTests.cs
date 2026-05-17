#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TokenForge.Client.Agents;
using TokenForge.Client.Auth;
using TokenForge.Client.Common;
using TokenForge.Client.Domain;
using TokenForge.Client.Git;
using TokenForge.Client.Persistence;
using TokenForge.Client.Platform;
using TokenForge.Client.Privacy;
using TokenForge.Client.Sync;
using TokenForge.Client.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TokenForge.Client.Tests
{
    public sealed class Phase14PrefabWiringPlayModeTests
    {
        [UnityTest]
        public IEnumerator BootstrapRootPrefabLoadsAndValidates()
        {
            var fixture = Phase14UiFixture.Create();
            yield return null;

            Assert.IsNotNull(fixture.Root);
            Assert.IsTrue(fixture.Root.ValidateReferences(out var error), error);
            Assert.IsNotNull(fixture.Root.accountPanel.loginButton);
            Assert.IsNotNull(fixture.Root.activityAnalysisPanel.analyzeGitButton);
            Assert.IsNotNull(fixture.Root.safeSyncPanel.syncButton);
            Assert.IsNotNull(fixture.Root.privacyNoticePanel.bodyLabel);

            fixture.Destroy();
        }

        [UnityTest]
        public IEnumerator BrokenPrefabReferenceFailsSafely()
        {
            var fixture = Phase14UiFixture.Create();
            fixture.Root.accountPanel.statusLabel = null;
            fixture.Root.Bind(fixture.Status, fixture.ViewModel);
            yield return null;

            Assert.IsFalse(fixture.Root.ValidateReferences(out var error));
            Assert.IsTrue(error.Contains(nameof(AccountPanelView)));
            Assert.IsTrue(fixture.Root.validationStatusLabel.text.Contains("UI prefab reference error"));

            fixture.Destroy();
        }
    }

    public sealed class Phase14AccountPanelPlayModeTests
    {
        [UnityTest]
        public IEnumerator AccountPanelButtonStatesAndLoginPrivacy()
        {
            var fixture = Phase14UiFixture.Create();
            yield return null;

            Assert.IsTrue(fixture.Root.accountPanel.loginButton.interactable);
            Assert.IsTrue(fixture.Root.accountPanel.signupButton.interactable);
            Assert.IsFalse(fixture.Root.accountPanel.logoutButton.interactable);

            fixture.Root.accountPanel.emailInput.text = "tester@example.com";
            fixture.Root.accountPanel.passwordInput.text = "password123";
            fixture.Root.accountPanel.loginButton.onClick.Invoke();
            yield return null;

            Assert.AreEqual(1, fixture.Auth.LoginCount);
            Assert.AreEqual(0, fixture.Sync.SyncCount);
            Assert.AreEqual(string.Empty, fixture.Root.accountPanel.passwordInput.text);
            Assert.IsFalse(UiTextScanner.VisibleTextContains(fixture.Root.gameObject, "password123"));
            Assert.IsFalse(UiTextScanner.VisibleTextContains(fixture.Root.gameObject, "access-token-secret"));
            Assert.IsFalse(UiTextScanner.VisibleTextContains(fixture.Root.gameObject, "refresh-token-secret"));

            fixture.Root.accountPanel.logoutButton.onClick.Invoke();
            yield return null;
            Assert.AreEqual(1, fixture.Auth.LogoutCount);
            Assert.IsTrue(fixture.Root.activityAnalysisPanel.selectGitButton.interactable);

            fixture.Destroy();
        }

        [UnityTest]
        public IEnumerator AccountPanelDisablesSubmitWhileLoginInProgress()
        {
            var fixture = Phase14UiFixture.Create(slowLogin: true);
            fixture.Root.accountPanel.emailInput.text = "tester@example.com";
            fixture.Root.accountPanel.passwordInput.text = "password123";

            fixture.Root.accountPanel.loginButton.onClick.Invoke();
            yield return null;

            Assert.IsFalse(fixture.Root.accountPanel.loginButton.interactable);
            Assert.IsFalse(fixture.Root.accountPanel.signupButton.interactable);

            fixture.Auth.CompleteLogin();
            yield return null;
            yield return null;

            Assert.AreEqual(string.Empty, fixture.Root.accountPanel.passwordInput.text);
            Assert.AreEqual(0, fixture.Sync.SyncCount);

            fixture.Destroy();
        }
    }

    public sealed class Phase14SafeSyncPanelPlayModeTests
    {
        [UnityTest]
        public IEnumerator SafeSyncStatesAndExplicitSyncOnly()
        {
            var fixture = Phase14UiFixture.Create(loggedIn: false);
            yield return null;

            Assert.IsTrue(fixture.Root.safeSyncPanel.healthButton.interactable);
            Assert.IsFalse(fixture.Root.safeSyncPanel.syncButton.interactable);
            Assert.IsFalse(fixture.Root.safeSyncPanel.fetchButton.interactable);

            fixture.Root.safeSyncPanel.healthButton.onClick.Invoke();
            yield return null;
            Assert.AreEqual(1, fixture.Sync.HealthCount);

            fixture.Auth.SetLoggedIn();
            fixture.Root.Render();
            Assert.IsTrue(fixture.Root.safeSyncPanel.syncButton.interactable);
            Assert.IsTrue(fixture.Root.safeSyncPanel.fetchButton.interactable);

            fixture.Root.safeSyncPanel.syncButton.onClick.Invoke();
            fixture.Root.safeSyncPanel.syncButton.onClick.Invoke();
            yield return null;
            Assert.AreEqual(1, fixture.Sync.SyncCount);
            Assert.IsFalse(fixture.Root.safeSyncPanel.syncButton.interactable);

            fixture.Sync.CompleteSync(accepted: 2, rejected: 1);
            yield return null;
            yield return null;

            Assert.IsTrue(fixture.Root.safeSyncPanel.statusLabel.text.Contains("Accepted 2 / rejected 1"));
            Assert.IsFalse(UiTextScanner.VisibleTextContains(fixture.Root.gameObject, "{\""));
            Assert.IsFalse(UiTextScanner.VisibleTextContains(fixture.Root.gameObject, "access-token-secret"));

            fixture.Destroy();
        }

        [UnityTest]
        public IEnumerator SafeSyncServerUnavailableRendersSafeMessage()
        {
            var fixture = Phase14UiFixture.Create();
            fixture.Sync.NextHealthResult = SafeSyncResult.Failure(SafeSyncStatus.ServerUnavailable, "SERVER_UNAVAILABLE", "{\"token\":\"secret\",\"path\":\"/Users/alice/private\"}");

            fixture.Root.safeSyncPanel.healthButton.onClick.Invoke();
            yield return null;

            Assert.IsTrue(fixture.Root.safeSyncPanel.statusLabel.text.Contains("Server"));
            Assert.IsFalse(UiTextScanner.VisibleTextContains(fixture.Root.gameObject, "{\"token\""));
            Assert.IsFalse(UiTextScanner.VisibleTextContains(fixture.Root.gameObject, "/Users/alice/private"));

            fixture.Destroy();
        }

        [UnityTest]
        public IEnumerator Phase22RetryConflictAndTombstoneSectionsAreExplicitAndSafe()
        {
            var fixture = Phase14UiFixture.Create(loggedIn: true);
            fixture.Sync.RetrySummary = new SafeSyncRetryQueueSummary
            {
                PendingCount = 1,
                FailedCount = 1,
                SafeEntries = new List<SafeSyncRetryQueueEntry>
                {
                    new SafeSyncRetryQueueEntry
                    {
                        QueueEntryId = "retry-entry-1",
                        OperationType = SafeSyncRetryOperationType.UPSERT_ACTIVITY_SESSIONS,
                        Status = SafeSyncRetryQueueEntryStatus.Pending,
                        AttemptCount = 1,
                        MaxAttempts = 4,
                        LastSafeErrorCode = SafeSyncApiError.ServerUnavailable,
                        ClientSessionIds = new List<string> { "client-session-1" }
                    }
                }
            };
            fixture.Sync.ConflictSummary = new SafeSyncConflictSummary
            {
                UnresolvedCount = 1,
                SafeConflicts = new List<SafeSyncConflict>
                {
                    new SafeSyncConflict
                    {
                        ClientSessionId = "client-session-1",
                        ConflictType = SafeSyncConflictType.RemoteDifferent,
                        SafeErrorCode = SafeSyncApiError.ConflictDetected
                    }
                }
            };
            fixture.Sync.ConflictAuditSummary = new SafeConflictAuditSummary
            {
                TotalCount = 1,
                ResolvedCount = 1,
                SafeEntries = new List<SafeConflictAuditEntry>
                {
                    new SafeConflictAuditEntry
                    {
                        Action = "applyMergePolicy",
                        Policy = SafeConflictMergePolicy.KeepRemote,
                        ResultStatus = "resolved",
                        SafeDiffFieldNames = new List<string> { "dayBucket", "confidence" },
                        WarningIds = new List<string> { SafeSyncApiError.KeepRemoteApplied },
                        UserMessageCode = SafeSyncApiError.KeepRemoteApplied
                    }
                }
            };
            fixture.Sync.TombstoneSummary = new SafeSyncTombstoneSummary { PendingDeleteCount = 1 };
            yield return fixture.RefreshDashboard();

            Assert.IsNotNull(fixture.Root.safeSyncPanel.retryPendingButton);
            Assert.IsTrue(fixture.Root.safeSyncPanel.retryQueueLabel.text.Contains("pending 1"));
            Assert.IsTrue(fixture.Root.safeSyncPanel.conflictLabel.text.Contains("unresolved 1"));
            Assert.IsTrue(fixture.Root.safeSyncPanel.tombstoneLabel.text.Contains("pending 1"));
            Assert.AreEqual(0, fixture.Sync.RetryCount);

            fixture.Root.safeSyncPanel.retryPendingButton.onClick.Invoke();
            Assert.IsTrue(fixture.Root.safeSyncPanel.confirmationPanel.activeSelf);
            ConfirmSafeSyncAction(fixture);
            yield return null;

            Assert.AreEqual(1, fixture.Sync.RetryCount);
            Assert.IsFalse(fixture.Root.safeSyncPanel.retryPendingButton.interactable);

            fixture.Sync.CompleteRetry();
            yield return null;
            yield return null;

            Assert.IsTrue(fixture.Root.safeSyncPanel.retryQueueLabel.text.Contains("succeeded 1"));
            UiTextScanner.AssertNoForbiddenVisibleText(fixture.Root.gameObject, new[]
            {
                "access-token-secret",
                "refresh-token-secret",
                fixture.RawRepositoryPath,
                fixture.RawAgentLogPath,
                "{\"",
                "/Users/"
            });

            fixture.Destroy();
        }

        [UnityTest]
        public IEnumerator Phase23DeleteTombstoneAndConflictControlsAreExplicitAndSafe()
        {
            var fixture = Phase14UiFixture.Create(loggedIn: true);
            fixture.Repository.Current.WorkSessionSummaries.Add(new AgentWorkSession
            {
                SessionId = "local-session-1",
                SourceProvider = "CODEX",
                WorkType = WorkType.Feature,
                EndedAt = new DateTimeOffset(2026, 5, 15, 2, 0, 0, TimeSpan.Zero),
                Confidence = ProviderConfidence.High,
                AgentActivitySummary = new AgentActivitySummary { DayBucket = "2026-05-15", SessionCountBucket = CountBucket.One, InteractionCountBucket = CountBucket.Small },
                GitChangeSummary = new GitChangeSummary { ChangedFileCountBucket = CountBucket.Small, AddedLineBucket = LineChangeBucket.Small, DeletedLineBucket = LineChangeBucket.Small }
            });
            fixture.Sync.TombstoneSummary = new SafeSyncTombstoneSummary
            {
                PendingDeleteCount = 1,
                SafeTombstones = new List<SafeSyncTombstone>
                {
                    new SafeSyncTombstone { TombstoneId = "tombstone-1", ClientSessionId = "local-session-1", ServerSessionId = "server-1", SyncStatus = SafeSyncTombstoneStatus.PendingDelete }
                }
            };
            fixture.Sync.ConflictSummary = new SafeSyncConflictSummary
            {
                UnresolvedCount = 1,
                SafeConflicts = new List<SafeSyncConflict>
                {
                    new SafeSyncConflict
                    {
                        ConflictId = "conflict-1",
                        ClientSessionId = "local-session-1",
                        ConflictType = SafeSyncConflictType.RemoteDifferent,
                        SafeErrorCode = SafeSyncApiError.ConflictDetected,
                        SafeLocalSummary = new SafeSyncSessionSafeSummary { DayBucket = "2026-05-15", SourceProvider = "CODEX" },
                        SafeRemoteSummary = new SafeSyncSessionSafeSummary { DayBucket = "2026-05-16", SourceProvider = "CODEX" }
                    }
                }
            };
            yield return fixture.RefreshDashboard();

            Assert.IsNotNull(fixture.Root.recentSessionsPanel.deleteLocalSessionButton);
            Assert.IsTrue(fixture.Root.recentSessionsPanel.deleteLocalSessionButton.interactable);
            Assert.IsTrue(fixture.Root.safeSyncPanel.tombstoneLabel.text.Contains("pending 1"));
            Assert.IsTrue(fixture.Root.safeSyncPanel.conflictLabel.text.Contains("unresolved 1"));
            Assert.AreEqual(0, fixture.Sync.LocalDeleteCount);
            Assert.AreEqual(0, fixture.Sync.TombstoneProcessCount);

            fixture.Root.recentSessionsPanel.deleteLocalSessionButton.onClick.Invoke();
            fixture.Root.recentSessionsPanel.deleteLocalSessionButton.onClick.Invoke();
            yield return null;
            Assert.AreEqual(1, fixture.Sync.LocalDeleteCount);

            fixture.Root.safeSyncPanel.enqueueTombstoneDeletesButton.onClick.Invoke();
            yield return null;
            fixture.Root.safeSyncPanel.processTombstoneDeletesButton.onClick.Invoke();
            ConfirmSafeSyncAction(fixture);
            yield return null;
            Assert.AreEqual(1, fixture.Sync.TombstoneEnqueueCount);
            Assert.AreEqual(1, fixture.Sync.TombstoneProcessCount);

            fixture.Root.safeSyncPanel.keepLocalButton.onClick.Invoke();
            ConfirmSafeSyncAction(fixture);
            yield return null;
            fixture.Root.safeSyncPanel.keepRemoteButton.onClick.Invoke();
            ConfirmSafeSyncAction(fixture);
            yield return null;
            fixture.Root.safeSyncPanel.markConflictResolvedButton.onClick.Invoke();
            ConfirmSafeSyncAction(fixture);
            yield return null;
            Assert.AreEqual(1, fixture.Sync.KeepLocalCount);
            Assert.AreEqual(1, fixture.Sync.KeepRemoteCount);
            Assert.AreEqual(1, fixture.Sync.MarkResolvedCount);

            Assert.IsFalse(UiTextScanner.VisibleTextContains(fixture.Root.gameObject, "{\""));
            Assert.IsFalse(UiTextScanner.VisibleTextContains(fixture.Root.gameObject, fixture.RawRepositoryPath));
            Assert.IsFalse(UiTextScanner.VisibleTextContains(fixture.Root.gameObject, "access-token-secret"));
            Assert.AreEqual(0, fixture.Sync.RetryCount);

            fixture.Destroy();
        }

        [UnityTest]
        public IEnumerator Phase24ConflictReviewAndBatchButtonsAreExplicitAndSafe()
        {
            var fixture = Phase14UiFixture.Create(loggedIn: true);
            fixture.Sync.RetrySummary = new SafeSyncRetryQueueSummary
            {
                PendingCount = 1,
                FailedCount = 1,
                PausedCount = 1,
                SafeEntries = new List<SafeSyncRetryQueueEntry>
                {
                    new SafeSyncRetryQueueEntry { QueueEntryId = "retry-1", OperationType = SafeSyncRetryOperationType.UPSERT_ACTIVITY_SESSIONS, Status = SafeSyncRetryQueueEntryStatus.Pending },
                    new SafeSyncRetryQueueEntry { QueueEntryId = "retry-failed", OperationType = SafeSyncRetryOperationType.DELETE_REMOTE_SESSION, Status = SafeSyncRetryQueueEntryStatus.Failed }
                }
            };
            fixture.Sync.TombstoneSummary = new SafeSyncTombstoneSummary
            {
                PendingDeleteCount = 1,
                DeleteFailedCount = 1,
                SafeTombstones = new List<SafeSyncTombstone>
                {
                    new SafeSyncTombstone { TombstoneId = "tombstone-1", ServerSessionId = "server-1", SyncStatus = SafeSyncTombstoneStatus.DeleteFailed }
                }
            };
            fixture.Sync.ConflictSummary = new SafeSyncConflictSummary
            {
                UnresolvedCount = 1,
                SafeConflicts = new List<SafeSyncConflict>
                {
                    new SafeSyncConflict
                    {
                        ConflictId = "conflict-1",
                        ConflictType = SafeSyncConflictType.RemoteDifferent,
                        SafeErrorCode = SafeSyncApiError.KeepRemoteApplied,
                        SafeLocalSummary = new SafeSyncSessionSafeSummary { ClientSessionId = "client-session-1234", SourceProvider = "CODEX", DayBucket = "2026-05-15", Confidence = "HIGH", CategoryBucketSummary = "WORK_FEATURE:ONE" },
                        SafeRemoteSummary = new SafeSyncSessionSafeSummary { ServerSessionId = "server-session-1234", SourceProvider = "CODEX", DayBucket = "2026-05-16", Confidence = "MEDIUM", CategoryBucketSummary = "WORK_BUGFIX:ONE" },
                        SafeDiffSummary = new SafeSessionDiffSummary { IsDifferent = true, ChangedSafeFieldNames = new List<string> { "dayBucket", "confidence" } }
                    }
                }
            };
            fixture.Sync.NextFetchResult = new SafeSyncResult
            {
                IsSuccess = true,
                Status = SafeSyncStatus.Ready,
                AcceptedCount = 2,
                ConflictDetectedCount = 1,
                ConflictSummary = fixture.Sync.ConflictSummary
            };
            yield return fixture.RefreshDashboard();

            Assert.IsTrue(fixture.Root.safeSyncPanel.conflictLabel.text.Contains("Local:"));
            Assert.IsTrue(fixture.Root.safeSyncPanel.conflictLabel.text.Contains("Remote:"));
            Assert.IsTrue(fixture.Root.safeSyncPanel.conflictLabel.text.Contains("Prefer Higher Confidence"));
            Assert.IsTrue(fixture.Root.safeSyncPanel.conflictLabel.text.Contains("Conflict History"));
            Assert.IsNotNull(fixture.Root.safeSyncPanel.cancelAllFailedTombstonesButton);
            Assert.IsNotNull(fixture.Root.safeSyncPanel.cancelAllFailedRetryButton);
            Assert.IsNotNull(fixture.Root.safeSyncPanel.pauseAllPendingRetryButton);
            Assert.IsNotNull(fixture.Root.safeSyncPanel.resumeAllPausedRetryButton);

            fixture.Root.safeSyncPanel.keepRemoteButton.onClick.Invoke();
            Assert.IsTrue(fixture.Root.safeSyncPanel.confirmationPanel.activeSelf);
            ConfirmSafeSyncAction(fixture);
            yield return null;
            Assert.AreEqual(1, fixture.Sync.KeepRemoteCount);

            fixture.Root.safeSyncPanel.cancelAllFailedTombstonesButton.onClick.Invoke();
            ConfirmSafeSyncAction(fixture, "CANCEL DELETE");
            yield return null;
            fixture.Root.safeSyncPanel.cancelAllFailedRetryButton.onClick.Invoke();
            ConfirmSafeSyncAction(fixture);
            yield return null;
            fixture.Root.safeSyncPanel.pauseAllPendingRetryButton.onClick.Invoke();
            ConfirmSafeSyncAction(fixture);
            yield return null;
            fixture.Root.safeSyncPanel.resumeAllPausedRetryButton.onClick.Invoke();
            yield return null;
            ConfirmSafeSyncAction(fixture);
            yield return null;
            Assert.AreEqual(1, fixture.Sync.CancelAllFailedTombstonesCount);
            Assert.AreEqual(1, fixture.Sync.CancelAllFailedRetryCount);
            Assert.AreEqual(1, fixture.Sync.PauseAllPendingRetryCount);
            Assert.AreEqual(1, fixture.Sync.ResumeAllPausedRetryCount);

            fixture.Root.safeSyncPanel.fetchButton.onClick.Invoke();
            yield return null;
            Assert.AreEqual(1, fixture.Sync.FetchCount);
            Assert.IsTrue(fixture.Root.safeSyncPanel.statusLabel.text.Contains("Accepted 2"));

            UiTextScanner.AssertNoForbiddenVisibleText(fixture.Root.gameObject, new[]
            {
                "{\"",
                fixture.RawRepositoryPath,
                fixture.RawAgentLogPath,
                "access-token-secret",
                "refresh-token-secret"
            });

            fixture.Destroy();
        }

        [UnityTest]
        public IEnumerator Phase27ConfirmationModalQaStatesAreVisibleAndBlocking()
        {
            var fixture = Phase14UiFixture.Create(loggedIn: true);
            fixture.Sync.RetrySummary = new SafeSyncRetryQueueSummary
            {
                PendingCount = 1,
                SafeEntries = new List<SafeSyncRetryQueueEntry>
                {
                    new SafeSyncRetryQueueEntry { QueueEntryId = "retry-1", OperationType = SafeSyncRetryOperationType.UPSERT_ACTIVITY_SESSIONS, Status = SafeSyncRetryQueueEntryStatus.Pending }
                }
            };
            fixture.Sync.ConflictSummary = new SafeSyncConflictSummary
            {
                UnresolvedCount = 1,
                SafeConflicts = new List<SafeSyncConflict>
                {
                    new SafeSyncConflict
                    {
                        ConflictId = "conflict-1",
                        ConflictType = SafeSyncConflictType.RemoteDifferent,
                        SafeErrorCode = SafeSyncApiError.ConflictDetected,
                        SafeDiffSummary = new SafeSessionDiffSummary { ChangedSafeFieldNames = new List<string> { "confidence" } }
                    }
                }
            };
            fixture.Sync.ConflictAuditSummary = new SafeConflictAuditSummary { ResolvedCount = 1 };
            yield return fixture.RefreshDashboard();

            fixture.Root.safeSyncPanel.applyMergePolicyButton.onClick.Invoke();
            yield return null;
            Assert.IsTrue(fixture.Root.safeSyncPanel.confirmationPanel.activeSelf);
            Assert.IsTrue(fixture.Root.safeSyncPanel.confirmationPreviewLabel.text.Contains("Action: applyMergePolicy"));
            Assert.IsTrue(fixture.Root.safeSyncPanel.confirmationPreviewLabel.text.Contains("Expected result:"));
            Assert.IsTrue(fixture.Root.safeSyncPanel.confirmationBodyLabel.text.Contains("Type MERGE to confirm"));

            ConfirmSafeSyncAction(fixture, "WRONG");
            yield return null;
            Assert.AreEqual(0, fixture.Sync.MergeApplyCount);
            Assert.IsTrue(fixture.Root.safeSyncPanel.statusLabel.text.Contains("No Safe Sync action was applied"));

            fixture.Root.safeSyncPanel.confirmationTypedPhraseInput.text = "MERGE";
            fixture.Root.safeSyncPanel.confirmationCancelButton.onClick.Invoke();
            yield return null;
            Assert.AreEqual(0, fixture.Sync.MergeApplyCount);
            Assert.IsTrue(fixture.Root.safeSyncPanel.statusLabel.text.Contains("canceled"));

            fixture.Root.safeSyncPanel.forceRetryButton.onClick.Invoke();
            yield return null;
            Assert.IsTrue(fixture.Root.safeSyncPanel.confirmationBodyLabel.text.Contains("Type FORCE RETRY to confirm"));
            ConfirmSafeSyncAction(fixture, "FORCE RETRY");
            yield return null;
            Assert.AreEqual(1, fixture.Sync.RetryCount);
            Assert.IsTrue(fixture.Root.safeSyncPanel.statusLabel.text.Contains("completed"));

            fixture.Root.safeSyncPanel.clearConflictAuditButton.onClick.Invoke();
            yield return null;
            Assert.IsTrue(fixture.Root.safeSyncPanel.confirmationBodyLabel.text.Contains("Type CLEAR HISTORY to confirm"));
            ConfirmSafeSyncAction(fixture, "CLEAR HISTORY");
            yield return null;
            Assert.AreEqual(1, fixture.Sync.ClearAuditCount);

            fixture.Destroy();
        }

        private static void ConfirmSafeSyncAction(Phase14UiFixture fixture, string typedPhrase = "")
        {
            if (fixture.Root.safeSyncPanel.confirmationTypedPhraseInput != null)
            {
                fixture.Root.safeSyncPanel.confirmationTypedPhraseInput.text = typedPhrase;
            }

            fixture.Root.safeSyncPanel.confirmationConfirmButton.onClick.Invoke();
        }
    }

    public sealed class Phase14ActivityAnalysisPanelPlayModeTests
    {
        [UnityTest]
        public IEnumerator ActivityButtonsApprovedLocationsAndReviewFlow()
        {
            var fixture = Phase14UiFixture.Create();
            yield return null;

            Assert.IsFalse(fixture.Root.activityAnalysisPanel.analyzeGitButton.interactable);
            Assert.IsFalse(fixture.Root.activityAnalysisPanel.analyzeAgentButton.interactable);

            fixture.Root.activityAnalysisPanel.selectGitButton.onClick.Invoke();
            yield return null;
            Assert.IsTrue(fixture.Root.activityAnalysisPanel.analyzeGitButton.interactable);

            fixture.Root.approvedLocationsPanel.gitAliasInput.text = "Work repo";
            fixture.Root.approvedLocationsPanel.approveGitButton.onClick.Invoke();
            yield return fixture.WaitUntil(() => fixture.ViewModel.ApprovedGitLocations.Count > 0);
            fixture.Root.Render();
            Assert.IsTrue(
                fixture.Root.approvedLocationsPanel.gitLocationsDropdown.options[0].text.Contains("Work repo"),
                fixture.Root.approvedLocationsPanel.gitLocationsDropdown.options[0].text);
            Assert.IsFalse(fixture.Root.approvedLocationsPanel.gitLocationsDropdown.options[0].text.Contains(fixture.RawRepositoryPath));

            fixture.Root.activityAnalysisPanel.analyzeGitButton.onClick.Invoke();
            yield return null;
            yield return null;
            Assert.IsTrue(fixture.Root.reviewPanel.saveGitButton.interactable);
            fixture.Root.reviewPanel.discardGitButton.onClick.Invoke();
            yield return null;
            Assert.IsFalse(fixture.Root.reviewPanel.saveGitButton.interactable);

            fixture.Root.activityAnalysisPanel.agentProviderDropdown.value = 1;
            fixture.Root.activityAnalysisPanel.selectAgentButton.onClick.Invoke();
            yield return null;
            Assert.IsTrue(fixture.Root.activityAnalysisPanel.analyzeAgentButton.interactable);
            fixture.Root.approvedLocationsPanel.agentAliasInput.text = "Claude logs";
            fixture.Root.approvedLocationsPanel.approveAgentButton.onClick.Invoke();
            yield return fixture.WaitUntil(() => fixture.ViewModel.ApprovedAgentLocations.Count > 0);
            fixture.Root.Render();
            Assert.IsTrue(
                fixture.Root.approvedLocationsPanel.agentLocationsDropdown.options[0].text.Contains("Claude logs"),
                fixture.Root.approvedLocationsPanel.agentLocationsDropdown.options[0].text);
            Assert.IsFalse(fixture.Root.approvedLocationsPanel.agentLocationsDropdown.options[0].text.Contains(fixture.RawAgentLogPath));

            fixture.Destroy();
        }
    }

    public sealed class Phase14PrivacyUiPlayModeTests
    {
        [UnityTest]
        public IEnumerator PrivacyNoticeAndSessionListsExposeOnlySafeSummaries()
        {
            var fixture = Phase14UiFixture.Create();
            fixture.Repository.Current.WorkSessionSummaries.Add(new AgentWorkSession
            {
                SourceProvider = "AI_AGENT",
                StartedAt = DateTimeOffset.UtcNow.AddHours(-1),
                EndedAt = DateTimeOffset.UtcNow,
                Confidence = ProviderConfidence.High,
                AgentActivitySummary = new AgentActivitySummary
                {
                    ProviderType = AgentProviderType.Codex,
                    DayBucket = "2026-05-15",
                    SessionCountBucket = CountBucket.One,
                    InteractionCountBucket = CountBucket.Small
                }
            });
            yield return fixture.RefreshDashboard();
            fixture.Sync.NextFetchResult = new SafeSyncResult
            {
                IsSuccess = true,
                Status = SafeSyncStatus.Ready,
                RemoteSessions = new List<RemoteSafeSessionSummary>
                {
                    new RemoteSafeSessionSummary
                    {
                        ServerSessionId = "11111111-1111-4111-8111-111111111111",
                        SourceProvider = "CODEX",
                        DayBucket = "2026-05-15",
                        ActivityCategory = "WORK_FEATURE",
                        ChangeCountBucket = "SMALL",
                        LineCountBucket = "SMALL",
                        SessionCountBucket = "ONE",
                        InteractionCountBucket = "SMALL",
                        Confidence = "HIGH",
                        SchemaVersion = 1
                    }
                }
            };
            fixture.Root.safeSyncPanel.fetchButton.onClick.Invoke();
            yield return null;
            yield return null;

            var privacy = fixture.Root.privacyNoticePanel.bodyLabel.text;
            Assert.IsTrue(privacy.Contains("aggregate-only"));
            Assert.IsTrue(privacy.Contains("Approved locations are stored only on this device"));
            Assert.IsTrue(privacy.Contains("Raw local data is never synced"));

            UiTextScanner.AssertNoForbiddenVisibleText(fixture.Root.gameObject, new[]
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
                "public class Secret",
                "apiKey"
            });

            fixture.Destroy();
        }
    }

    internal sealed class Phase14UiFixture
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/UI/BootstrapRoot.prefab";

        public BootstrapRootView Root { get; private set; }
        public LocalClientStatus Status { get; private set; }
        public ApprovedActivityAnalysisViewModel ViewModel { get; private set; }
        public FakeAuthSessionService Auth { get; private set; }
        public FakeSafeSyncService Sync { get; private set; }
        public FakeRepository Repository { get; private set; }
        public string RawRepositoryPath { get; private set; }
        public string RawAgentLogPath { get; private set; }

        private GameObject canvasObject;

        public static Phase14UiFixture Create(bool loggedIn = false, bool slowLogin = false)
        {
            var fixture = new Phase14UiFixture();
            fixture.RawRepositoryPath = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName(), "SecretRepo");
            Directory.CreateDirectory(fixture.RawRepositoryPath);
            fixture.RawAgentLogPath = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName(), "SecretRepo", "claude-private.jsonl");
            fixture.Repository = new FakeRepository();
            fixture.Auth = new FakeAuthSessionService(slowLogin);
            if (loggedIn)
            {
                fixture.Auth.SetLoggedIn();
            }

            fixture.Sync = new FakeSafeSyncService();
            var privacy = new PrivacySanitizer();
            fixture.ViewModel = new ApprovedActivityAnalysisViewModel(
                new GitAnalysisFlowController(
                    new FakeRepositoryPicker(fixture.RawRepositoryPath),
                    new GitAggregateAnalyzer(new FakeGitRunner(), privacy),
                    fixture.Repository,
                    null,
                    privacy),
                new AgentAnalysisFlowController(
                    new AgentLogActivityProvider(new AgentActivityAnalyzer(new FakeAgentLogReader(), null, privacy)),
                    fixture.Repository,
                    null,
                    privacy),
                new FakeAgentLogLocationPicker(fixture.RawAgentLogPath),
                fixture.Repository,
                privacy,
                new ApprovedLocationSettingsRepository(Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName())),
                fixture.Sync,
                fixture.Auth);
            fixture.Status = LocalClientStatus.CreateInitialized();
            fixture.canvasObject = new GameObject("Phase14 Test Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            fixture.canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, "BootstrapRoot prefab missing at " + PrefabPath);
            var instance = UnityEngine.Object.Instantiate(prefab, fixture.canvasObject.transform, false);
            fixture.Root = instance.GetComponent<BootstrapRootView>();
            fixture.Root.Bind(fixture.Status, fixture.ViewModel);
            return fixture;
        }

        public IEnumerator RefreshDashboard()
        {
            var task = ViewModel.RefreshDashboardAsync();
            while (!task.IsCompleted)
            {
                yield return null;
            }

            Root.Bind(Status, ViewModel);
            yield return null;
        }

        public IEnumerator WaitUntil(Func<bool> predicate)
        {
            for (var i = 0; i < 120; i++)
            {
                if (predicate())
                {
                    yield break;
                }

                yield return null;
            }
        }

        public void Destroy()
        {
            if (canvasObject != null)
            {
                UnityEngine.Object.DestroyImmediate(canvasObject);
            }
        }
    }

    internal sealed class FakeAuthSessionService : IAuthSessionService
    {
        private readonly bool slowLogin;
        private TaskCompletionSource<AuthResult> loginCompletion;

        public FakeAuthSessionService(bool slowLogin = false)
        {
            this.slowLogin = slowLogin;
        }

        public int LoginCount { get; private set; }
        public int SignupCount { get; private set; }
        public int LogoutCount { get; private set; }
        public AuthState State { get; private set; } = AuthState.LoggedOut;
        public AuthSession CurrentSession { get; private set; }
        public string ErrorCode { get; private set; } = string.Empty;
        public string BaseUrl { get; private set; } = "http://localhost:3000/api/v1";
        public bool HasUsableAccessToken => State == AuthState.LoggedIn && CurrentSession != null;

        public void SetBaseUrl(string baseUrl)
        {
            BaseUrl = baseUrl;
        }

        public void SetLoggedIn()
        {
            State = AuthState.LoggedIn;
            CurrentSession = new AuthSession
            {
                UserId = "user-1",
                Email = "tester@example.com",
                DisplayName = "Tester",
                AccessToken = "access-token-secret",
                RefreshToken = "refresh-token-secret",
                AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30)
            };
        }

        public void CompleteLogin()
        {
            SetLoggedIn();
            loginCompletion?.TrySetResult(AuthResult.Success(State));
        }

        public Task<AuthResult> LoadSessionAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(AuthResult.Success(State));
        }

        public Task<AuthResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
        {
            LoginCount += 1;
            if (slowLogin)
            {
                State = AuthState.LoggingIn;
                loginCompletion = new TaskCompletionSource<AuthResult>();
                return loginCompletion.Task;
            }

            SetLoggedIn();
            CurrentSession.Email = email;
            return Task.FromResult(AuthResult.Success(State));
        }

        public Task<AuthResult> SignupAsync(string email, string password, string displayName = "", CancellationToken cancellationToken = default)
        {
            SignupCount += 1;
            SetLoggedIn();
            CurrentSession.Email = email;
            CurrentSession.DisplayName = displayName;
            return Task.FromResult(AuthResult.Success(State));
        }

        public Task<AuthResult> LoadCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(State == AuthState.LoggedIn
                ? AuthResult.Success(State)
                : AuthResult.Failure(AuthState.AuthRequired, AuthApiError.AuthRequired, "Log in to use server sync."));
        }

        public Task<AuthResult> LogoutAsync(CancellationToken cancellationToken = default)
        {
            LogoutCount += 1;
            State = AuthState.LoggedOut;
            CurrentSession = null;
            return Task.FromResult(AuthResult.Success(State));
        }

        public Task<string> GetAccessTokenForSyncAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(HasUsableAccessToken ? "access-token-secret" : string.Empty);
        }

        public Task<string> ForceRefreshForRetryAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(string.Empty);
        }
    }

    internal sealed class FakeSafeSyncService : ISafeSyncService
    {
        private TaskCompletionSource<SafeSyncResult> syncCompletion;
        private TaskCompletionSource<SafeSyncResult> retryCompletion;

        public int HealthCount { get; private set; }
        public int SyncCount { get; private set; }
        public int FetchCount { get; private set; }
        public int RetryCount { get; private set; }
        public int LocalDeleteCount { get; private set; }
        public int TombstoneEnqueueCount { get; private set; }
        public int TombstoneProcessCount { get; private set; }
        public int CancelAllFailedTombstonesCount { get; private set; }
        public int CancelAllFailedRetryCount { get; private set; }
        public int PauseAllPendingRetryCount { get; private set; }
        public int ResumeAllPausedRetryCount { get; private set; }
        public int KeepLocalCount { get; private set; }
        public int KeepRemoteCount { get; private set; }
        public int MarkResolvedCount { get; private set; }
        public int MergeApplyCount { get; private set; }
        public int ClearAuditCount { get; private set; }
        public SafeSyncStatus Status { get; private set; } = SafeSyncStatus.Idle;
        public string BaseUrl { get; private set; } = "http://localhost:3000/api/v1";
        public SafeSyncResult NextHealthResult { get; set; } = SafeSyncResult.Success(SafeSyncStatus.Ready);
        public SafeSyncResult NextFetchResult { get; set; } = SafeSyncResult.Success(SafeSyncStatus.Ready);
        public SafeSyncRetryQueueSummary RetrySummary { get; set; } = new SafeSyncRetryQueueSummary();
        public SafeSyncConflictSummary ConflictSummary { get; set; } = new SafeSyncConflictSummary();
        public SafeConflictAuditSummary ConflictAuditSummary { get; set; } = new SafeConflictAuditSummary();
        public SafeSyncTombstoneSummary TombstoneSummary { get; set; } = new SafeSyncTombstoneSummary();

        public void SetBaseUrl(string baseUrl)
        {
            BaseUrl = baseUrl;
        }

        public Task<SafeSyncResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            HealthCount += 1;
            Status = NextHealthResult.Status;
            return Task.FromResult(NextHealthResult);
        }

        public Task<SafeSyncResult> SyncNowAsync(CancellationToken cancellationToken = default)
        {
            SyncCount += 1;
            Status = SafeSyncStatus.Syncing;
            syncCompletion = new TaskCompletionSource<SafeSyncResult>();
            return syncCompletion.Task;
        }

        public Task<SafeSyncResult> EnqueueSyncSafeSessionsAsync(CancellationToken cancellationToken = default)
        {
            return SyncNowAsync(cancellationToken);
        }

        public void CompleteSync(int accepted, int rejected)
        {
            Status = SafeSyncStatus.Synced;
            syncCompletion?.TrySetResult(new SafeSyncResult
            {
                IsSuccess = true,
                Status = SafeSyncStatus.Synced,
                AcceptedCount = accepted,
                RejectedCount = rejected
            });
        }

        public Task<SafeSyncResult> FetchRemoteSessionsAsync(CancellationToken cancellationToken = default)
        {
            FetchCount += 1;
            Status = NextFetchResult.Status;
            return Task.FromResult(NextFetchResult);
        }

        public Task<SafeSyncResult> ProcessRetryQueueOnceAsync(CancellationToken cancellationToken = default)
        {
            RetryCount += 1;
            Status = SafeSyncStatus.RetryInProgress;
            retryCompletion = new TaskCompletionSource<SafeSyncResult>();
            return retryCompletion.Task;
        }

        public Task<SafeSyncResult> ProcessAllEligibleRetryEntriesOnceAsync(CancellationToken cancellationToken = default)
        {
            return ProcessRetryQueueOnceAsync(cancellationToken);
        }

        public Task<SafeSyncResult> ForceRetryEntryAsync(string queueEntryId, bool explicitConfirmation, CancellationToken cancellationToken = default)
        {
            RetryCount += 1;
            return Task.FromResult(new SafeSyncResult { IsSuccess = true, Status = SafeSyncStatus.RetrySucceeded, RetryQueueSummary = RetrySummary, ErrorCode = SafeSyncApiError.ForcedRetrySucceeded });
        }

        public void CompleteRetry()
        {
            Status = SafeSyncStatus.RetrySucceeded;
            RetrySummary.PendingCount = 0;
            RetrySummary.SucceededCount = 1;
            retryCompletion?.TrySetResult(new SafeSyncResult { IsSuccess = true, Status = SafeSyncStatus.RetrySucceeded, RetryQueueSummary = RetrySummary });
        }

        public Task<SafeSyncResult> CancelRetryEntryAsync(string queueEntryId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SafeSyncResult { IsSuccess = true, Status = SafeSyncStatus.Ready, RetryQueueSummary = RetrySummary });
        }

        public Task<SafeSyncResult> CancelAllFailedRetryEntriesAsync(CancellationToken cancellationToken = default)
        {
            CancelAllFailedRetryCount += 1;
            return Task.FromResult(new SafeSyncResult { IsSuccess = true, Status = SafeSyncStatus.Ready, RetryQueueSummary = RetrySummary });
        }

        public Task<SafeSyncResult> ClearSucceededRetryEntriesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SafeSyncResult { IsSuccess = true, Status = SafeSyncStatus.Ready, RetryQueueSummary = RetrySummary });
        }

        public Task<SafeSyncResult> PauseAllPendingRetryEntriesAsync(CancellationToken cancellationToken = default)
        {
            PauseAllPendingRetryCount += 1;
            return Task.FromResult(new SafeSyncResult { IsSuccess = true, Status = SafeSyncStatus.Ready, RetryQueueSummary = RetrySummary });
        }

        public Task<SafeSyncResult> ResumeAllPausedRetryEntriesAsync(CancellationToken cancellationToken = default)
        {
            ResumeAllPausedRetryCount += 1;
            return Task.FromResult(new SafeSyncResult { IsSuccess = true, Status = SafeSyncStatus.RetryPending, RetryQueueSummary = RetrySummary });
        }

        public Task<SafeSyncRetryQueueSummary> GetRetryQueueSummaryAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(RetrySummary);
        }

        public Task<SafeSyncConflictSummary> GetConflictSummaryAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ConflictSummary);
        }

        public Task<SafeSyncTombstoneSummary> GetTombstoneSummaryAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(TombstoneSummary);
        }

        public Task<SafeSyncResult> DeleteLocalSavedSessionAsync(string clientSessionId, CancellationToken cancellationToken = default)
        {
            LocalDeleteCount += 1;
            return Task.FromResult(new SafeSyncResult { IsSuccess = true, Status = SafeSyncStatus.Ready, ErrorCode = SafeSyncApiError.LocalSessionDeleted, RetryQueueSummary = RetrySummary, TombstoneSummary = TombstoneSummary });
        }

        public Task<SafeSyncResult> EnqueuePendingTombstoneDeletesAsync(CancellationToken cancellationToken = default)
        {
            TombstoneEnqueueCount += 1;
            return Task.FromResult(new SafeSyncResult { IsSuccess = true, Status = SafeSyncStatus.RetryPending, TombstoneSummary = TombstoneSummary, RetryQueueSummary = RetrySummary });
        }

        public Task<SafeSyncResult> ProcessPendingTombstoneDeletesOnceAsync(CancellationToken cancellationToken = default)
        {
            TombstoneProcessCount += 1;
            return Task.FromResult(new SafeSyncResult { IsSuccess = true, Status = SafeSyncStatus.RetrySucceeded, TombstoneSummary = TombstoneSummary });
        }

        public Task<SafeSyncResult> CancelTombstoneAsync(string tombstoneId, CancellationToken cancellationToken = default) => Task.FromResult(new SafeSyncResult { IsSuccess = true, Status = SafeSyncStatus.Ready, TombstoneSummary = TombstoneSummary });
        public Task<SafeSyncResult> CancelAllFailedTombstonesAsync(CancellationToken cancellationToken = default)
        {
            CancelAllFailedTombstonesCount += 1;
            return Task.FromResult(new SafeSyncResult { IsSuccess = true, Status = SafeSyncStatus.Ready, TombstoneSummary = TombstoneSummary });
        }
        public Task<SafeSyncResult> MarkTombstoneResolvedAsync(string tombstoneId, CancellationToken cancellationToken = default) => Task.FromResult(new SafeSyncResult { IsSuccess = true, Status = SafeSyncStatus.Ready, TombstoneSummary = TombstoneSummary });
        public Task<SafeSyncResult> ClearResolvedTombstonesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new SafeSyncResult { IsSuccess = true, Status = SafeSyncStatus.Ready, TombstoneSummary = TombstoneSummary });

        public Task<SafeSyncResult> KeepLocalConflictAsync(string conflictId, CancellationToken cancellationToken = default)
        {
            KeepLocalCount += 1;
            return Task.FromResult(new SafeSyncResult { IsSuccess = true, Status = SafeSyncStatus.RetryPending, ConflictSummary = ConflictSummary, RetryQueueSummary = RetrySummary });
        }

        public Task<SafeSyncResult> KeepRemoteConflictAsync(string conflictId, CancellationToken cancellationToken = default)
        {
            KeepRemoteCount += 1;
            return Task.FromResult(new SafeSyncResult { IsSuccess = true, Status = SafeSyncStatus.Ready, ConflictSummary = ConflictSummary });
        }

        public Task<SafeSyncResult> MarkConflictResolvedAsync(string conflictId, CancellationToken cancellationToken = default)
        {
            MarkResolvedCount += 1;
            return Task.FromResult(new SafeSyncResult { IsSuccess = true, Status = SafeSyncStatus.Ready, ConflictSummary = ConflictSummary });
        }

        public Task<SafeSyncResult> CancelConflictResolutionAsync(string conflictId, CancellationToken cancellationToken = default) => Task.FromResult(new SafeSyncResult { IsSuccess = true, Status = SafeSyncStatus.ConflictDetected, ConflictSummary = ConflictSummary });

        public Task<SafeConflictMergePreview> GetConflictMergePreviewAsync(string conflictId, SafeConflictMergePolicy policy, CancellationToken cancellationToken = default) => Task.FromResult(new SafeConflictMergePreview { ConflictId = conflictId, SelectedPolicy = policy, EffectivePolicy = policy, CanApply = true });
        public Task<SafeConflictMergeResult> ApplyConflictMergePolicyAsync(string conflictId, SafeConflictMergePolicy policy, bool explicitConfirmation, CancellationToken cancellationToken = default)
        {
            if (explicitConfirmation)
            {
                MergeApplyCount += 1;
            }

            return Task.FromResult(new SafeConflictMergeResult { ConflictId = conflictId, Policy = policy, Applied = explicitConfirmation, SyncResult = SafeSyncResult.Success(SafeSyncStatus.Ready) });
        }
        public Task<SafeConflictAuditSummary> GetConflictAuditHistoryAsync(CancellationToken cancellationToken = default) => Task.FromResult(ConflictAuditSummary);
        public Task<SafeSyncResult> ClearResolvedConflictAuditHistoryAsync(bool explicitConfirmation, CancellationToken cancellationToken = default)
        {
            if (explicitConfirmation)
            {
                ClearAuditCount += 1;
            }

            return Task.FromResult(SafeSyncResult.Success(SafeSyncStatus.Ready));
        }

        public Task<SafeSyncResult> DeleteRemoteSessionAsync(string serverSessionId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(SafeSyncResult.Success(SafeSyncStatus.Ready));
        }
    }

    internal sealed class FakeRepository : ILocalSaveDataRepository
    {
        public SaveData Current { get; set; } = SaveData.CreateDefault();
        public Task<SaveData> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Current);
        public Task<Result> SaveAsync(SaveData saveData, CancellationToken cancellationToken = default)
        {
            Current = saveData;
            return Task.FromResult(Result.Success());
        }
    }

    internal sealed class FakeRepositoryPicker : IRepositoryPicker
    {
        private readonly string path;
        public FakeRepositoryPicker(string path) => this.path = path;
        public Task<RepositoryPickerResult> PickRepositoryAsync(CancellationToken cancellationToken = default) => Task.FromResult(RepositoryPickerResult.Selected(path));
    }

    internal sealed class FakeAgentLogLocationPicker : IAgentLogLocationPicker
    {
        private readonly string path;
        public FakeAgentLogLocationPicker(string path) => this.path = path;
        public Task<AgentLogLocationPickerResult> PickAgentLogLocationAsync(CancellationToken cancellationToken = default) => Task.FromResult(AgentLogLocationPickerResult.Selected(path));
    }

    internal sealed class FakeGitRunner : IGitCommandRunner
    {
        public Task<GitCommandResult> RunAsync(string workingDirectory, string arguments, CancellationToken cancellationToken)
        {
            if (arguments == "rev-parse --is-inside-work-tree")
            {
                return Task.FromResult(GitCommandResult.Success("true\n"));
            }

            if (arguments == "status --porcelain")
            {
                return Task.FromResult(GitCommandResult.Success(" M src/ui/View.cs\n"));
            }

            if (arguments == "diff --numstat" || arguments == "diff --cached --numstat")
            {
                return Task.FromResult(GitCommandResult.Success("12\t3\tsrc/ui/View.cs\n"));
            }

            if (arguments.StartsWith("log --since=", StringComparison.Ordinal))
            {
                return Task.FromResult(GitCommandResult.Success("--TOKENFORGE-COMMIT--\n8\t2\tsrc/app/App.cs\n"));
            }

            return Task.FromResult(GitCommandResult.Failure("unsupported_git_command", "Unsupported test command."));
        }
    }

    internal sealed class FakeAgentLogReader : IAgentLogSourceReader
    {
        public Task<AgentLogReadResult> ReadAsync(AgentAnalysisInput input, CancellationToken cancellationToken)
        {
            var entries = new List<AgentLogEntry>
            {
                new AgentLogEntry
                {
                    Text = "{\"provider\":\"claude\",\"timestamp\":\"2026-05-15T01:00:00Z\",\"session_id\":\"session-1\",\"type\":\"message\",\"role\":\"user\",\"content\":\"safe aggregate fixture\"}",
                    LastWriteTimeUtc = new DateTimeOffset(2026, 5, 15, 1, 0, 0, TimeSpan.Zero)
                }
            };

            return Task.FromResult(AgentLogReadResult.Success(entries, new List<string>()));
        }
    }

    internal static class UiTextScanner
    {
        public static bool VisibleTextContains(GameObject root, string value)
        {
            return root.GetComponentsInChildren<Text>(true).Any(text => text.text != null && text.text.Contains(value));
        }

        public static void AssertNoForbiddenVisibleText(GameObject root, IEnumerable<string> forbiddenValues)
        {
            var visibleText = string.Join("\n", root.GetComponentsInChildren<Text>(true).Select(text => text.text ?? string.Empty));
            foreach (var forbidden in forbiddenValues.Where(item => !string.IsNullOrWhiteSpace(item)))
            {
                Assert.IsFalse(visibleText.Contains(forbidden), "Visible UI text exposed forbidden value: " + forbidden);
            }
        }
    }
}
#endif
