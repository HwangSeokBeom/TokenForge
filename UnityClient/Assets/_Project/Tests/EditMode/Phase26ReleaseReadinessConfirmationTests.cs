#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TokenForge.Client.Sync;
using TokenForge.Client.UI;

namespace TokenForge.Client.Tests
{
    public sealed class Phase26ReleaseReadinessConfirmationTests
    {
        [Test]
        public void NotarizationReadinessScriptsExposeDryRunSubmitStatusesAndDoNotEchoSecrets()
        {
            var repoRoot = FindRepoRoot();
            var notarize = File.ReadAllText(Path.Combine(repoRoot, "scripts/notarize-macos-release-candidate.sh"));
            var report = File.ReadAllText(Path.Combine(repoRoot, "scripts/generate-release-readiness-report.sh"));

            foreach (var status in new[]
            {
                "READY_FOR_CREDENTIALLED_NOTARIZATION",
                "READY_BUT_CREDENTIALS_MISSING",
                "BLOCKED_MISSING_RELEASE_CANDIDATE",
                "BLOCKED_UNSIGNED_APP",
                "BLOCKED_PRIVACY_SCAN_FAILED",
                "BLOCKED_METADATA_INVALID",
                "NOTARIZATION_SUCCEEDED",
                "NOTARIZATION_FAILED",
                "STAPLE_FAILED",
                "SPCTL_FAILED"
            })
            {
                Assert.That(notarize, Does.Contain(status), status);
            }

            Assert.That(notarize, Does.Contain("--submit"));
            Assert.That(notarize, Does.Contain("xcrun notarytool submit"));
            Assert.That(notarize, Does.Contain("xcrun stapler staple"));
            Assert.That(notarize, Does.Contain("spctl --assess"));
            Assert.That(notarize, Does.Contain("APPLE_TEAM_ID"));
            Assert.That(notarize, Does.Contain("APPLE_APP_SPECIFIC_PASSWORD"));
            Assert.That(notarize, Does.Contain("DEVELOPER_ID_APPLICATION"));
            Assert.That(report, Does.Contain("release-readiness-report.json"));
            Assert.That(report, Does.Contain("\"schemaVersion\": 1"));
            Assert.That(report, Does.Contain("manualQaChecklist"));

            foreach (var secretEcho in new[]
            {
                "echo \"${APPLE_ID",
                "echo \"${APPLE_TEAM_ID",
                "echo \"${APPLE_APP_SPECIFIC_PASSWORD",
                "echo \"${DEVELOPER_ID_APPLICATION"
            })
            {
                Assert.That(notarize, Does.Not.Contain(secretEcho), secretEcho);
                Assert.That(report, Does.Not.Contain(secretEcho), secretEcho);
            }
        }

        [Test]
        public void ConfirmationRequestsContainOnlySafeTextAndHighRiskRequiresTypedConfirmation()
        {
            var merge = SafeSyncConfirmationRequestFactory.ForMergePreview(new SafeConflictMergePreview
            {
                ConflictId = "conflict-1",
                SelectedPolicy = SafeConflictMergePolicy.MergeNonConflictingAggregates,
                EffectivePolicy = SafeConflictMergePolicy.MergeNonConflictingAggregates,
                CanApply = true,
                DiffSummary = new SafeSessionDiffSummary { ChangedSafeFieldNames = new List<string> { "dayBucket", "/Users/private" } },
                Warnings = new List<string> { "SAFE_WARNING", "{\"raw\":\"json\"}" }
            });
            var force = SafeSyncConfirmationRequestFactory.ForRetryAction(
                SafeSyncConfirmationActionType.ForceRetry,
                new SafeSyncRetryQueueSummary { PendingCount = 1 },
                new SafeSyncRetryQueueEntry { QueueEntryId = "retry-1", Status = SafeSyncRetryQueueEntryStatus.Pending, LastSafeErrorCode = SafeSyncApiError.ServerUnavailable });
            var clear = SafeSyncConfirmationRequestFactory.ForClearResolvedAuditHistory(new SafeConflictAuditSummary { ResolvedCount = 2 });

            Assert.IsNotNull(merge);
            Assert.IsTrue(merge.RequiresTypedConfirmation);
            Assert.AreEqual("MERGE", merge.TypedConfirmationPhrase);
            Assert.IsTrue(force.RequiresTypedConfirmation);
            Assert.AreEqual("FORCE RETRY", force.TypedConfirmationPhrase);
            Assert.IsTrue(clear.RequiresTypedConfirmation);
            Assert.AreEqual("CLEAR HISTORY", clear.TypedConfirmationPhrase);

            foreach (var request in new[] { merge, force, clear })
            {
                Assert.IsTrue(SafeSyncConfirmationRequestFactory.ContainsOnlySafeText(request));
                Assert.IsEmpty(UiVisibleTextScanner.FindForbiddenRuntimeText(request));
            }
        }

        [Test]
        public void ConfirmationResultsBlockCancelAndWrongTypedPhrase()
        {
            var request = SafeSyncConfirmationRequestFactory.ForClearResolvedAuditHistory(new SafeConflictAuditSummary { ResolvedCount = 1 });

            var cancelled = SafeSyncConfirmationRequestFactory.Cancel(request);
            var wrong = SafeSyncConfirmationRequestFactory.Confirm(request, "CLEAR");
            var correct = SafeSyncConfirmationRequestFactory.Confirm(request, "CLEAR HISTORY");

            Assert.IsFalse(cancelled.Confirmed);
            Assert.IsFalse(wrong.Confirmed);
            Assert.IsFalse(wrong.TypedPhraseMatched);
            Assert.IsTrue(correct.Confirmed);
            Assert.IsTrue(correct.TypedPhraseMatched);
        }

        [Test]
        public void SupportedActionTypesGenerateConfirmableSafeRequests()
        {
            var conflict = new SafeSyncConflict
            {
                ConflictId = "conflict-1",
                ConflictType = SafeSyncConflictType.RemoteDifferent,
                SafeErrorCode = SafeSyncApiError.ConflictDetected,
                SafeDiffSummary = new SafeSessionDiffSummary { ChangedSafeFieldNames = new List<string> { "confidence" } }
            };
            var retrySummary = new SafeSyncRetryQueueSummary { PendingCount = 1, FailedCount = 1 };
            var tombstoneSummary = new SafeSyncTombstoneSummary { PendingDeleteCount = 1, DeleteFailedCount = 1 };

            var requests = new[]
            {
                SafeSyncConfirmationRequestFactory.ForConflictAction(SafeSyncConfirmationActionType.KeepLocal, conflict),
                SafeSyncConfirmationRequestFactory.ForConflictAction(SafeSyncConfirmationActionType.KeepRemote, conflict),
                SafeSyncConfirmationRequestFactory.ForConflictAction(SafeSyncConfirmationActionType.MarkConflictResolved, conflict),
                SafeSyncConfirmationRequestFactory.ForRetryAction(SafeSyncConfirmationActionType.ProcessRetryBatch, retrySummary),
                SafeSyncConfirmationRequestFactory.ForRetryAction(SafeSyncConfirmationActionType.CancelRetryBatch, retrySummary),
                SafeSyncConfirmationRequestFactory.ForTombstoneAction(SafeSyncConfirmationActionType.ProcessTombstoneBatch, tombstoneSummary),
                SafeSyncConfirmationRequestFactory.ForTombstoneAction(SafeSyncConfirmationActionType.CancelTombstoneBatch, tombstoneSummary),
                SafeSyncConfirmationRequestFactory.ForClearResolvedAuditHistory(new SafeConflictAuditSummary { ResolvedCount = 1 })
            };

            Assert.IsTrue(requests.All(request => request != null && SafeSyncConfirmationRequestFactory.ContainsOnlySafeText(request)));
        }

        [Test]
        public void GatekeeperQaDocumentationCoversLocalOnlyAndNoBypassBoundaries()
        {
            var repoRoot = FindRepoRoot();
            var docs = File.ReadAllText(Path.Combine(repoRoot, "Docs/macos-gatekeeper-qa.md"));

            Assert.That(docs, Does.Contain("clean install"));
            Assert.That(docs, Does.Contain("Gatekeeper"));
            Assert.That(docs, Does.Contain("local-only"));
            Assert.That(docs, Does.Contain("tokenforge-sync-conflict-audit.local.json"));
            Assert.That(docs, Does.Contain("typed confirmation"));
            Assert.That(docs, Does.Not.Contain("xattr -d com.apple.quarantine"));
            Assert.That(docs, Does.Not.Contain("spctl --master-disable"));
        }

        private static string FindRepoRoot()
        {
            var current = Directory.GetCurrentDirectory();
            while (!string.IsNullOrWhiteSpace(current))
            {
                if (File.Exists(Path.Combine(current, ".gitignore")))
                {
                    return current;
                }

                current = Directory.GetParent(current)?.FullName;
            }

            Assert.Fail("Could not locate repository root.");
            return Directory.GetCurrentDirectory();
        }
    }
}
#endif
