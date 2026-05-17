#if UNITY_EDITOR
using System.IO;
using NUnit.Framework;

namespace TokenForge.Client.Tests
{
    public sealed class Phase28CredentialedReleaseExecutionTests
    {
        [Test]
        public void CredentialedReleaseScript_ExposesDryRunCredentialedAndSafeFailureStatuses()
        {
            var repoRoot = FindRepoRoot();
            var script = File.ReadAllText(Path.Combine(repoRoot, "scripts/run-credentialed-release.sh"));

            foreach (var status in new[]
            {
                "CRED_RELEASE_DRY_RUN_READY",
                "CRED_RELEASE_BLOCKED_MISSING_CREDENTIALS",
                "CRED_RELEASE_BLOCKED_SIGNING_FAILED",
                "CRED_RELEASE_BLOCKED_NOTARIZATION_FAILED",
                "CRED_RELEASE_BLOCKED_STAPLING_FAILED",
                "CRED_RELEASE_BLOCKED_SPCTL_FAILED",
                "CRED_RELEASE_READY_FOR_QA",
                "CRED_RELEASE_READY_FOR_DISTRIBUTION"
            })
            {
                Assert.That(script, Does.Contain(status), status);
            }

            Assert.That(script, Does.Contain("--credentialed"));
            Assert.That(script, Does.Contain("credentialed-release-summary.json"));
            Assert.That(script, Does.Contain("nextRequiredCredentialedCommand"));
            Assert.That(script, Does.Contain("scripts/run-credentialed-release.sh --credentialed"));
            Assert.That(script, Does.Contain("signedWithDeveloperId"));
            Assert.That(script, Does.Contain("NOTARIZATION_SUCCEEDED"));
            Assert.That(script, Does.Contain("STAPLE_SUCCEEDED"));
            Assert.That(script, Does.Contain("SPCTL_SUCCEEDED"));
            Assert.That(script, Does.Not.Contain("echo \"${APPLE_ID"));
            Assert.That(script, Does.Not.Contain("echo \"${APPLE_APP_SPECIFIC_PASSWORD"));
            Assert.That(script, Does.Not.Contain("echo \"${DEVELOPER_ID_APPLICATION"));
        }

        [Test]
        public void SigningAndNotarizationSummaries_AreNormalizedAndPrivacySafe()
        {
            var repoRoot = FindRepoRoot();
            var sign = File.ReadAllText(Path.Combine(repoRoot, "scripts/sign-macos-release-candidate.sh"));
            var notarize = File.ReadAllText(Path.Combine(repoRoot, "scripts/notarize-macos-release-candidate.sh"));

            foreach (var field in new[]
            {
                "\"status\"",
                "\"signingIdentityType\"",
                "\"developerIdIdentityAvailable\"",
                "\"signedWithDeveloperId\"",
                "\"hardenedRuntime\"",
                "\"timestamp\"",
                "\"entitlementsStatus\"",
                "\"appBundleId\"",
                "\"appVersion\"",
                "\"appBuild\"",
                "\"releaseCandidatePath\"",
                "\"releaseCandidateSha256\"",
                "\"errorCategory\"",
                "\"safeFailureReason\""
            })
            {
                Assert.That(sign, Does.Contain(field), field);
            }

            foreach (var category in new[]
            {
                "missingDeveloperIdIdentity",
                "codesignFailed",
                "verificationFailed",
                "packageMissing",
                "metadataInvalid",
                "entitlementsInvalid",
                "privacyScanFailed"
            })
            {
                Assert.That(sign, Does.Contain(category), category);
            }

            foreach (var field in new[]
            {
                "\"submitted\"",
                "\"submissionId\"",
                "\"notarizationStartedAt\"",
                "\"notarizationFinishedAt\"",
                "\"staplingStatus\"",
                "\"spctlStatus\"",
                "\"signedIdentityType\"",
                "\"releaseCandidateSha256\"",
                "\"errorCategory\"",
                "\"safeFailureReason\""
            })
            {
                Assert.That(notarize, Does.Contain(field), field);
            }

            foreach (var category in new[]
            {
                "missingCredentials",
                "adhocSigned",
                "notarytoolUnavailable",
                "submitFailed",
                "notarizationRejected",
                "stapleFailed",
                "spctlFailed",
                "packageMissing",
                "metadataInvalid"
            })
            {
                Assert.That(notarize, Does.Contain(category), category);
            }

            foreach (var unsafeText in new[]
            {
                "notarytool logs",
                "echo \"${APPLE_ID",
                "echo \"${APPLE_APP_SPECIFIC_PASSWORD",
                "echo \"${DEVELOPER_ID_APPLICATION"
            })
            {
                Assert.That(sign, Does.Not.Contain(unsafeText), unsafeText);
                Assert.That(notarize, Does.Not.Contain(unsafeText), unsafeText);
            }
        }

        [Test]
        public void ReadinessReportV5AndEvidenceHandoff_RequireVerifiedDistributionSignals()
        {
            var repoRoot = FindRepoRoot();
            var report = File.ReadAllText(Path.Combine(repoRoot, "scripts/generate-release-readiness-report.sh"));
            var evidence = File.ReadAllText(Path.Combine(repoRoot, "scripts/collect-release-evidence.sh"));

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
            Assert.That(report, Does.Contain("\"privacyAssertions\""));
            Assert.That(report, Does.Contain("readyForDistribution"));
            Assert.That(report, Does.Contain("signed_developer_id"));
            Assert.That(report, Does.Contain("notary_status == \"NOTARIZATION_SUCCEEDED\""));
            Assert.That(report, Does.Contain("stapling_status == \"STAPLE_SUCCEEDED\""));
            Assert.That(report, Does.Contain("spctl_status == \"SPCTL_SUCCEEDED\""));
            Assert.That(report, Does.Contain("privacy_scan == \"passed\""));
            Assert.That(report, Does.Contain("evidence_lock_status == \"EVIDENCE_LOCK_READY\""));

            Assert.That(evidence, Does.Contain("final-release-handoff.md"));
            Assert.That(evidence, Does.Contain("credentialed-release-summary.json"));
            Assert.That(evidence, Does.Contain("credentialed-release-verification.json"));
            Assert.That(evidence, Does.Contain("final-release-record.md"));
            Assert.That(evidence, Does.Contain("notarization-status-summary.json"));
            Assert.That(evidence, Does.Contain("signing-status-summary.json"));
            Assert.That(evidence, Does.Contain("Exact next action"));
            Assert.That(evidence, Does.Contain("run credentialed release"));
            Assert.That(evidence, Does.Contain("run manual QA"));
            Assert.That(evidence, Does.Contain("ready for distribution"));
            Assert.That(evidence, Does.Contain("blocked with reason"));
            Assert.That(evidence, Does.Not.Contain("APPLE_APP_SPECIFIC_PASSWORD}\""));
        }

        [Test]
        public void GatekeeperQaValidator_ConstrainsStatusesAndRejectsUnsafeNotes()
        {
            var repoRoot = FindRepoRoot();
            var validator = File.ReadAllText(Path.Combine(repoRoot, "scripts/validate-gatekeeper-qa-status.sh"));

            foreach (var status in new[]
            {
                "QA_STATUS_VALID",
                "QA_STATUS_MISSING",
                "QA_STATUS_INVALID",
                "QA_STATUS_UNSAFE_NOTES"
            })
            {
                Assert.That(validator, Does.Contain(status), status);
            }

            foreach (var item in new[]
            {
                "cleanInstall",
                "firstLaunch",
                "moveToApplications",
                "quarantineAssessment",
                "localStateLocation",
                "safeSyncPanelLaunch",
                "confirmationModalCancel",
                "confirmationModalTypedPhrase",
                "conflictAuditLocalOnly",
                "retryTombstoneLocalOnly",
                "approvedLocationsLocalOnly",
                "privacyScan",
                "packageScan"
            })
            {
                Assert.That(validator, Does.Contain(item), item);
            }

            foreach (var unsafeMarker in new[]
            {
                "/Users/",
                "Bearer",
                "stack trace",
                "prompt:",
                "response:",
                "tokenforge-approved-locations\\.local\\.json",
                "raw[ _-]?sync[ _-]?payload"
            })
            {
                Assert.That(validator, Does.Contain(unsafeMarker), unsafeMarker);
            }
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
