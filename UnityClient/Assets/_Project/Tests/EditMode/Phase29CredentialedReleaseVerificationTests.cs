#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;

namespace TokenForge.Client.Tests
{
    public sealed class Phase29CredentialedReleaseVerificationTests
    {
        private static readonly string[] QaItems =
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
        };

        [Test]
        public void Phase29ScriptsExposeSafeVerificationQaRecordAndReadinessStates()
        {
            var repoRoot = FindRepoRoot();
            var verifier = File.ReadAllText(Path.Combine(repoRoot, "scripts/verify-credentialed-release.sh"));
            var qaHelper = File.ReadAllText(Path.Combine(repoRoot, "scripts/update-gatekeeper-qa-status.sh"));
            var record = File.ReadAllText(Path.Combine(repoRoot, "scripts/generate-final-release-record.sh"));
            var report = File.ReadAllText(Path.Combine(repoRoot, "scripts/generate-release-readiness-report.sh"));
            var prep = File.ReadAllText(Path.Combine(repoRoot, "scripts/release-prep-check.sh"));

            foreach (var status in new[]
            {
                "CRED_RELEASE_VERIFY_DRY_RUN_ONLY",
                "CRED_RELEASE_VERIFY_BLOCKED",
                "CRED_RELEASE_VERIFIED_PENDING_QA",
                "CRED_RELEASE_VERIFIED_READY_FOR_DISTRIBUTION"
            })
            {
                Assert.That(verifier, Does.Contain(status), status);
            }

            foreach (var command in new[] { "init", "set", "note", "summary", "validate" })
            {
                Assert.That(qaHelper, Does.Contain(command), command);
            }

            Assert.That(record, Does.Contain("final-release-record.md"));
            Assert.That(record, Does.Contain("final-release-record.json"));
            Assert.That(record, Does.Contain("dry-run only"));
            Assert.That(record, Does.Contain("pending QA"));
            Assert.That(record, Does.Contain("ready for distribution"));
            Assert.That(report, Does.Contain("\"schemaVersion\": 5"));
            Assert.That(report, Does.Contain("pendingQa"));
            Assert.That(report, Does.Contain("evidencePending"));
            Assert.That(report, Does.Contain("readyForDistribution"));
            Assert.That(report, Does.Contain("finalReleaseRecordPath"));
            Assert.That(prep, Does.Contain("validate_phase29_summaries"));
            Assert.That(prep, Does.Contain("readyForDistribution was reported without all required release gates"));

            foreach (var unsafeEcho in new[]
            {
                "echo \"${APPLE_ID",
                "echo \"${APPLE_TEAM_ID",
                "echo \"${APPLE_APP_SPECIFIC_PASSWORD",
                "echo \"${DEVELOPER_ID_APPLICATION"
            })
            {
                Assert.That(verifier, Does.Not.Contain(unsafeEcho), unsafeEcho);
                Assert.That(qaHelper, Does.Not.Contain(unsafeEcho), unsafeEcho);
                Assert.That(record, Does.Not.Contain(unsafeEcho), unsafeEcho);
            }
        }

        [Test]
        public void VerifyCredentialedRelease_ReportsDryRunPendingQaReadyAndChecksumMismatch()
        {
            var repoRoot = FindRepoRoot();
            var script = Path.Combine(repoRoot, "scripts/verify-credentialed-release.sh");

            using (var dryRun = new TempReleaseFixture())
            {
                dryRun.WritePackage("dry run");
                dryRun.WriteSummaries(developerId: false, notarized: false, stapled: false, spctl: false, shaOverride: dryRun.Sha256);
                var result = RunScript(script, dryRun);
                Assert.AreEqual(0, result.ExitCode, result.Output);
                Assert.That(File.ReadAllText(dryRun.VerificationSummary), Does.Contain("CRED_RELEASE_VERIFY_DRY_RUN_ONLY"));
            }

            using (var missing = new TempReleaseFixture())
            {
                missing.WritePackage("missing summaries");
                var result = RunScript(script, missing);
                Assert.AreNotEqual(0, result.ExitCode, result.Output);
                Assert.That(File.ReadAllText(missing.VerificationSummary), Does.Contain("CRED_RELEASE_VERIFY_BLOCKED"));
            }

            using (var pendingQa = new TempReleaseFixture())
            {
                pendingQa.WritePackage("pending qa");
                pendingQa.WriteSummaries(developerId: true, notarized: true, stapled: true, spctl: true, shaOverride: pendingQa.Sha256);
                var result = RunScript(script, pendingQa);
                Assert.AreEqual(0, result.ExitCode, result.Output);
                Assert.That(File.ReadAllText(pendingQa.VerificationSummary), Does.Contain("CRED_RELEASE_VERIFIED_PENDING_QA"));
            }

            using (var ready = new TempReleaseFixture())
            {
                ready.WritePackage("ready");
                ready.WriteSummaries(developerId: true, notarized: true, stapled: true, spctl: true, shaOverride: ready.Sha256);
                ready.WriteQaPassed();
                var result = RunScript(script, ready);
                Assert.AreEqual(0, result.ExitCode, result.Output);
                Assert.That(File.ReadAllText(ready.VerificationSummary), Does.Contain("CRED_RELEASE_VERIFIED_READY_FOR_DISTRIBUTION"));
            }

            using (var mismatch = new TempReleaseFixture())
            {
                mismatch.WritePackage("mismatch");
                mismatch.WriteSummaries(developerId: true, notarized: true, stapled: true, spctl: true, shaOverride: new string('0', 64));
                var result = RunScript(script, mismatch);
                Assert.AreNotEqual(0, result.ExitCode, result.Output);
                Assert.That(File.ReadAllText(mismatch.VerificationSummary), Does.Contain("SHA-256 does not match"));
            }
        }

        [Test]
        public void UpdateGatekeeperQaStatus_InitSetNoteSummaryAndValidationAreSafe()
        {
            var repoRoot = FindRepoRoot();
            var script = Path.Combine(repoRoot, "scripts/update-gatekeeper-qa-status.sh");

            using (var fixture = new TempReleaseFixture())
            {
                var init = RunScript(script, fixture, "init");
                Assert.AreEqual(0, init.ExitCode, init.Output);
                var statusText = File.ReadAllText(fixture.QaStatus);
                foreach (var item in QaItems)
                {
                    Assert.That(statusText, Does.Contain(item), item);
                }

                Assert.AreEqual(0, RunScript(script, fixture, "set cleanInstall passed").ExitCode);
                Assert.AreNotEqual(0, RunScript(script, fixture, "set unknownItem passed").ExitCode);
                Assert.AreNotEqual(0, RunScript(script, fixture, "set cleanInstall unknownStatus").ExitCode);
                Assert.AreEqual(0, RunScript(script, fixture, "note cleanInstall \"Installed cleanly from release candidate.\"").ExitCode);
                Assert.AreNotEqual(0, RunScript(script, fixture, "note cleanInstall \"/Users/example/private path\"").ExitCode);

                var summary = RunScript(script, fixture, "summary");
                Assert.AreEqual(0, summary.ExitCode, summary.Output);
                Assert.That(summary.Output, Does.Contain("Status: inProgress"));
                Assert.AreEqual(0, RunScript(script, fixture, "validate").ExitCode);
            }
        }

        [Test]
        public void FinalReleaseRecord_DoesNotClaimReadyForDryRunAndRequiresVerifiedGates()
        {
            var repoRoot = FindRepoRoot();
            var verify = Path.Combine(repoRoot, "scripts/verify-credentialed-release.sh");
            var record = Path.Combine(repoRoot, "scripts/generate-final-release-record.sh");

            using (var dryRun = new TempReleaseFixture())
            {
                dryRun.WritePackage("dry run record");
                dryRun.WriteSummaries(developerId: false, notarized: false, stapled: false, spctl: false, shaOverride: dryRun.Sha256);
                Assert.AreEqual(0, RunScript(verify, dryRun).ExitCode);
                Assert.AreEqual(0, RunScript(record, dryRun).ExitCode);
                Assert.That(File.ReadAllText(dryRun.FinalRecordJson), Does.Contain("\"distributionReadiness\": \"dry-run only\""));
                Assert.That(File.ReadAllText(dryRun.FinalRecordMarkdown), Does.Not.Contain("Distribution readiness: ready for distribution"));
            }

            using (var ready = new TempReleaseFixture())
            {
                ready.WritePackage("ready record");
                ready.WriteSummaries(developerId: true, notarized: true, stapled: true, spctl: true, shaOverride: ready.Sha256);
                ready.WriteQaPassed();
                Assert.AreEqual(0, RunScript(verify, ready).ExitCode);
                Assert.AreEqual(0, RunScript(record, ready).ExitCode);
                Assert.That(File.ReadAllText(ready.FinalRecordJson), Does.Contain("\"distributionReadiness\": \"ready for distribution\""));
                Assert.That(File.ReadAllText(ready.FinalRecordMarkdown), Does.Not.Contain("APPLE_APP_SPECIFIC_PASSWORD"));
            }
        }

        private static ScriptResult RunScript(string script, TempReleaseFixture fixture, string args = "")
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = string.IsNullOrWhiteSpace(args) ? Quote(script) : Quote(script) + " " + args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            startInfo.EnvironmentVariables["TOKENFORGE_RELEASE_DIR"] = fixture.ReleaseDir;
            startInfo.EnvironmentVariables["TOKENFORGE_RELEASE_EVIDENCE_DIR"] = fixture.EvidenceDir;
            startInfo.EnvironmentVariables["TOKENFORGE_GATEKEEPER_QA_STATUS"] = fixture.QaStatus;
            startInfo.EnvironmentVariables["PACKAGE_PATH"] = fixture.PackagePath;

            using (var process = Process.Start(startInfo))
            {
                Assert.IsNotNull(process);
                var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                process.WaitForExit();
                return new ScriptResult(process.ExitCode, output);
            }
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static string Sha256File(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
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

        private readonly struct ScriptResult
        {
            public ScriptResult(int exitCode, string output)
            {
                ExitCode = exitCode;
                Output = output;
            }

            public int ExitCode { get; }
            public string Output { get; }
        }

        private sealed class TempReleaseFixture : IDisposable
        {
            public TempReleaseFixture()
            {
                ReleaseDir = Path.Combine(Path.GetTempPath(), "tokenforge-phase29-" + Guid.NewGuid().ToString("N"));
                EvidenceDir = Path.Combine(ReleaseDir, "evidence");
                PackagePath = Path.Combine(ReleaseDir, "TokenForge-macOS-0.18.0-build18.zip");
                Directory.CreateDirectory(EvidenceDir);
            }

            public string ReleaseDir { get; }
            public string EvidenceDir { get; }
            public string PackagePath { get; }
            public string Sha256 { get; private set; }
            public string QaStatus => Path.Combine(EvidenceDir, "gatekeeper-qa-status.json");
            public string VerificationSummary => Path.Combine(ReleaseDir, "credentialed-release-verification.json");
            public string FinalRecordMarkdown => Path.Combine(EvidenceDir, "final-release-record.md");
            public string FinalRecordJson => Path.Combine(EvidenceDir, "final-release-record.json");

            public void WritePackage(string content)
            {
                Directory.CreateDirectory(ReleaseDir);
                File.WriteAllText(PackagePath, content);
                Sha256 = Sha256File(PackagePath);
                File.WriteAllText(Path.Combine(EvidenceDir, "final-release-handoff.md"), "# Safe handoff\n");
            }

            public void WriteSummaries(bool developerId, bool notarized, bool stapled, bool spctl, string shaOverride)
            {
                var signingStatus = developerId ? "SIGNING_SUCCEEDED" : "SIGNING_READY_BUT_IDENTITY_MISSING";
                var identity = developerId ? "developerId" : "adHoc";
                var notarizationStatus = notarized ? "NOTARIZATION_SUCCEEDED" : "NOTARIZATION_READY_BUT_CREDENTIALS_MISSING";
                var staplingStatus = stapled ? "STAPLE_SUCCEEDED" : "notAttempted";
                var spctlStatus = spctl ? "SPCTL_SUCCEEDED" : "notAttempted";
                var releaseStatus = developerId && notarized && stapled && spctl ? "pendingQa" : "dryRunReady";

                File.WriteAllText(Path.Combine(ReleaseDir, "signing-status-summary.json"),
                    "{\"status\":\"" + signingStatus + "\",\"signingIdentityType\":\"" + identity + "\",\"signedWithDeveloperId\":" + developerId.ToString().ToLowerInvariant() + ",\"hardenedRuntime\":\"enabled\",\"releaseCandidateSha256\":\"" + shaOverride + "\"}");
                File.WriteAllText(Path.Combine(ReleaseDir, "notarization-status-summary.json"),
                    "{\"notarizationStatus\":\"" + notarizationStatus + "\",\"staplingStatus\":\"" + staplingStatus + "\",\"spctlStatus\":\"" + spctlStatus + "\",\"releaseCandidateSha256\":\"" + shaOverride + "\"}");
                File.WriteAllText(Path.Combine(ReleaseDir, "credentialed-release-summary.json"),
                    "{\"status\":\"" + (developerId ? "CRED_RELEASE_READY_FOR_QA" : "CRED_RELEASE_DRY_RUN_READY") + "\",\"releaseCandidateSha256\":\"" + shaOverride + "\"}");
                File.WriteAllText(Path.Combine(ReleaseDir, "release-readiness-report.json"),
                    "{\"schemaVersion\":4,\"releaseStatus\":\"" + releaseStatus + "\",\"releaseCandidateSha256\":\"" + shaOverride + "\",\"packageScanResult\":\"passed\",\"privacyScanResult\":\"passed\",\"privacyAssertions\":{\"noLocalOnlyFilesInPackage\":true,\"noApprovedLocationPathsInEvidence\":true,\"noSecretsInEvidence\":true,\"noRawSyncStateInEvidence\":true,\"noBackgroundSyncAdded\":true},\"knownLimitations\":[],\"nextAction\":\"test\"}");
            }

            public void WriteQaPassed()
            {
                var entries = new List<string>();
                foreach (var item in QaItems)
                {
                    entries.Add("\"" + item + "\":{\"status\":\"passed\",\"note\":\"\"}");
                }

                File.WriteAllText(QaStatus, "{\"schemaVersion\":2,\"qaOverallStatus\":\"passed\",\"overallStatus\":\"passed\",\"items\":{" + string.Join(",", entries) + "}}");
            }

            public void Dispose()
            {
                if (Directory.Exists(ReleaseDir))
                {
                    Directory.Delete(ReleaseDir, true);
                }
            }
        }
    }
}
#endif
