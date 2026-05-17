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
            var sign = File.ReadAllText(Path.Combine(repoRoot, "scripts/sign-macos-release-candidate.sh"));
            var notarize = File.ReadAllText(Path.Combine(repoRoot, "scripts/notarize-macos-release-candidate.sh"));
            var report = File.ReadAllText(Path.Combine(repoRoot, "scripts/generate-release-readiness-report.sh"));

            foreach (var status in new[]
            {
                "SIGNING_DRY_RUN_READY",
                "SIGNING_READY_BUT_IDENTITY_MISSING",
                "SIGNING_BLOCKED_MISSING_APP",
                "SIGNING_BLOCKED_METADATA_INVALID",
                "SIGNING_BLOCKED_ENTITLEMENTS_INVALID",
                "SIGNING_BLOCKED_PRIVACY_SCAN_FAILED"
            })
            {
                Assert.That(sign, Does.Contain(status), status);
            }

            foreach (var status in new[]
            {
                "NOTARIZATION_NOT_ATTEMPTED",
                "NOTARIZATION_READY_BUT_CREDENTIALS_MISSING",
                "NOTARIZATION_BLOCKED_ADHOC_SIGNED",
                "NOTARIZATION_SUBMITTED",
                "NOTARIZATION_SUCCEEDED",
                "NOTARIZATION_FAILED",
                "STAPLE_SUCCEEDED",
                "STAPLE_FAILED",
                "SPCTL_SUCCEEDED",
                "SPCTL_FAILED"
            })
            {
                Assert.That(notarize, Does.Contain(status), status);
            }

            Assert.That(sign, Does.Contain("--sign"));
            Assert.That(sign, Does.Contain("DEVELOPER_ID_APPLICATION"));
            Assert.That(sign, Does.Contain("codesign_args"));
            Assert.That(sign, Does.Contain("--options runtime"));
            Assert.That(sign, Does.Contain("--timestamp"));
            Assert.That(sign, Does.Contain("ditto -c -k"));
            Assert.That(notarize, Does.Contain("--submit"));
            Assert.That(notarize, Does.Contain("xcrun notarytool submit"));
            Assert.That(notarize, Does.Contain("xcrun stapler staple"));
            Assert.That(notarize, Does.Contain("xcrun stapler validate"));
            Assert.That(notarize, Does.Contain("spctl --assess"));
            Assert.That(notarize, Does.Contain("APPLE_TEAM_ID"));
            Assert.That(notarize, Does.Contain("APPLE_APP_SPECIFIC_PASSWORD"));
            Assert.That(report, Does.Contain("release-readiness-report.json"));
            Assert.That(report, Does.Contain("\"schemaVersion\": 5"));
            Assert.That(report, Does.Contain("\"releaseStatus\""));
            Assert.That(report, Does.Contain("\"signingSummary\""));
            Assert.That(report, Does.Contain("\"notarizationSummary\""));
            Assert.That(report, Does.Contain("\"credentialedReleaseSummary\""));
            Assert.That(report, Does.Contain("\"credentialedVerificationSummary\""));
            Assert.That(report, Does.Contain("\"releaseMachineFinalizationSummary\""));
            Assert.That(report, Does.Contain("\"freezeManifestValidationSummary\""));
            Assert.That(report, Does.Contain("\"evidenceLockSummary\""));
            Assert.That(report, Does.Contain("\"finalReleaseRecordPath\""));
            Assert.That(report, Does.Contain("\"releaseCandidateSha256\""));
            Assert.That(report, Does.Contain("\"evidenceSummary\""));
            Assert.That(report, Does.Contain("\"qaStatusSummary\""));
            Assert.That(report, Does.Contain("\"privacyAssertions\""));

            foreach (var secretEcho in new[]
            {
                "echo \"${APPLE_ID",
                "echo \"${APPLE_TEAM_ID",
                "echo \"${APPLE_APP_SPECIFIC_PASSWORD",
                "echo \"${DEVELOPER_ID_APPLICATION"
            })
            {
                Assert.That(sign, Does.Not.Contain(secretEcho), secretEcho);
                Assert.That(notarize, Does.Not.Contain(secretEcho), secretEcho);
                Assert.That(report, Does.Not.Contain(secretEcho), secretEcho);
            }
        }

        [Test]
        public void Phase27EvidenceAndGatekeeperScriptsUseSafeMachineReadableOutputs()
        {
            var repoRoot = FindRepoRoot();
            var evidence = File.ReadAllText(Path.Combine(repoRoot, "scripts/collect-release-evidence.sh"));
            var helper = File.ReadAllText(Path.Combine(repoRoot, "scripts/gatekeeper-qa-helper.sh"));
            var prep = File.ReadAllText(Path.Combine(repoRoot, "scripts/release-prep-check.sh"));

            Assert.That(evidence, Does.Contain("/tmp/tokenforge-release/evidence"));
            Assert.That(evidence, Does.Contain("release-evidence.json"));
            Assert.That(evidence, Does.Contain("release-evidence.md"));
            Assert.That(evidence, Does.Contain("final-release-handoff.md"));
            Assert.That(evidence, Does.Contain("credentialed-release-verification.json"));
            Assert.That(evidence, Does.Contain("final-release-record.md"));
            Assert.That(evidence, Does.Contain("checksums.txt"));
            Assert.That(evidence, Does.Contain("shasum -a 256"));
            Assert.That(evidence, Does.Contain("localOnlyEvidenceBoundary"));
            Assert.That(helper, Does.Contain("gatekeeper-qa-status.json"));
            Assert.That(helper, Does.Contain("notStarted|passed|failed|blocked|skippedWithReason"));
            Assert.That(helper, Does.Contain("--init-status"));
            Assert.That(helper, Does.Contain("--validate-status"));
            Assert.That(helper, Does.Contain("packageLocalOnlyFileAbsence"));
            Assert.That(helper, Does.Contain("codesign --verify"));
            Assert.That(helper, Does.Contain("spctl --assess"));
            Assert.That(helper, Does.Contain("xcrun stapler validate"));
            Assert.That(prep, Does.Contain("sign-macos-release-candidate.sh"));
            Assert.That(prep, Does.Contain("collect-release-evidence.sh"));
            Assert.That(prep, Does.Contain("gatekeeper-qa-helper.sh"));

            foreach (var unsafeText in new[]
            {
                "spctl --master-disable",
                "xattr -d com.apple.quarantine",
                "APPLE_APP_SPECIFIC_PASSWORD}\"",
                "DEVELOPER_ID_APPLICATION}\""
            })
            {
                Assert.That(evidence, Does.Not.Contain(unsafeText), unsafeText);
                Assert.That(helper, Does.Not.Contain(unsafeText), unsafeText);
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
                Assert.IsFalse(string.IsNullOrWhiteSpace(request.SafeActionLabel));
                Assert.IsFalse(string.IsNullOrWhiteSpace(request.SafeResultExpectation));
                Assert.That(request.ValidationErrorMessage, Does.Contain("No Safe Sync action was applied"));
                Assert.That(request.StaleStateMessage, Does.Contain("Review the latest safe summary"));
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
