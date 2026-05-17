#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using NUnit.Framework;

namespace TokenForge.Client.Tests
{
    public sealed class Phase32FinalDistributionReadinessTests
    {
        [Test]
        public void FinalDistributionVerifier_MapsDryRunPendingQaPendingEvidenceAndReady()
        {
            var repoRoot = FindRepoRoot();
            var script = Path.Combine(repoRoot, "scripts/verify-final-distribution-readiness.sh");

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("dry run");
                fixture.WriteDryRun();
                var result = RunScript(script, fixture);
                Assert.AreEqual(0, result.ExitCode, result.Output);
                Assert.That(File.ReadAllText(fixture.FinalDistribution), Does.Contain("FINAL_DISTRIBUTION_DRY_RUN_ONLY"));
            }

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("pending qa");
                fixture.WriteCredentialed(qaPassed: false, evidenceLocked: false, readinessStatus: "pendingQa");
                var result = RunScript(script, fixture);
                Assert.AreEqual(0, result.ExitCode, result.Output);
                Assert.That(File.ReadAllText(fixture.FinalDistribution), Does.Contain("FINAL_DISTRIBUTION_PENDING_QA"));
            }

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("pending evidence");
                fixture.WriteCredentialed(qaPassed: true, evidenceLocked: false, readinessStatus: "evidencePending");
                var result = RunScript(script, fixture);
                Assert.AreEqual(0, result.ExitCode, result.Output);
                Assert.That(File.ReadAllText(fixture.FinalDistribution), Does.Contain("FINAL_DISTRIBUTION_PENDING_EVIDENCE_LOCK"));
            }

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("ready");
                fixture.WriteCredentialed(qaPassed: true, evidenceLocked: true, readinessStatus: "readyForDistribution");
                var result = RunScript(script, fixture);
                Assert.AreEqual(0, result.ExitCode, result.Output);
                var json = File.ReadAllText(fixture.FinalDistribution);
                Assert.That(json, Does.Contain("FINAL_DISTRIBUTION_READY"));
                Assert.That(json, Does.Contain("\"tagAllowed\": true"));
            }
        }

        [Test]
        public void FinalDistributionVerifier_BlocksShaMismatchAndOverclaimsAndUnsafeOutput()
        {
            var repoRoot = FindRepoRoot();
            var script = Path.Combine(repoRoot, "scripts/verify-final-distribution-readiness.sh");

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("sha mismatch");
                fixture.WriteCredentialed(qaPassed: true, evidenceLocked: true, readinessStatus: "readyForDistribution");
                File.WriteAllText(fixture.ReadinessReport, File.ReadAllText(fixture.ReadinessReport).Replace(fixture.Sha256, new string('a', 64)));
                var result = RunScript(script, fixture);
                Assert.AreNotEqual(0, result.ExitCode);
                Assert.That(File.ReadAllText(fixture.FinalDistribution), Does.Contain("SHA-256 mismatch"));
            }

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("readiness overclaim");
                fixture.WriteDryRun(readinessStatus: "readyForDistribution");
                var result = RunScript(script, fixture);
                Assert.AreNotEqual(0, result.ExitCode);
                Assert.That(File.ReadAllText(fixture.FinalDistribution), Does.Contain("Readiness report overclaims"));
            }

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("record overclaim");
                fixture.WriteDryRun(finalRecordReady: true);
                var result = RunScript(script, fixture);
                Assert.AreNotEqual(0, result.ExitCode);
                var json = File.ReadAllText(fixture.FinalDistribution);
                Assert.That(json, Does.Contain("Final release record overclaims"));
                Assert.That(json, Does.Not.Contain("Bearer "));
                Assert.That(json, Does.Not.Contain("tokenforge-approved-locations.local.json"));
                Assert.That(json, Does.Not.Contain("tokenforge-sync-local-state.local.json"));
                Assert.That(json, Does.Not.Contain("/Users/"));
            }
        }

        [Test]
        public void GithubReleaseDraft_DryRunAndReadyStatesAreHonestAndSafe()
        {
            var repoRoot = FindRepoRoot();
            var verify = Path.Combine(repoRoot, "scripts/verify-final-distribution-readiness.sh");
            var draftScript = Path.Combine(repoRoot, "scripts/generate-github-release-draft.sh");

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("dry draft");
                fixture.WriteDryRun();
                RunScript(verify, fixture);
                var result = RunScript(draftScript, fixture);
                Assert.AreEqual(0, result.ExitCode, result.Output);
                var markdown = File.ReadAllText(fixture.GithubDraftMarkdown);
                Assert.That(markdown, Does.Contain("not public distribution-ready"));
                Assert.That(markdown, Does.Contain("Developer ID notarization is not claimed complete"));
                Assert.That(markdown, Does.Contain("release candidate zip"));
                Assert.That(markdown, Does.Contain(fixture.Sha256));
                AssertSafe(markdown);
            }

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("ready draft");
                fixture.WriteCredentialed(qaPassed: true, evidenceLocked: true, readinessStatus: "readyForDistribution");
                RunScript(verify, fixture);
                var result = RunScript(draftScript, fixture);
                Assert.AreEqual(0, result.ExitCode, result.Output);
                var markdown = File.ReadAllText(fixture.GithubDraftMarkdown);
                var json = File.ReadAllText(fixture.GithubDraftJson);
                Assert.That(markdown, Does.Contain("ready for distribution"));
                Assert.That(markdown, Does.Contain("Download the release zip"));
                Assert.That(json, Does.Contain("\"installationNotesIncludedForPublicDistribution\": true"));
                Assert.That(markdown, Does.Contain(fixture.Sha256));
                AssertSafe(markdown + json);
            }
        }

        [Test]
        public void ReleaseBundleIndexAndDashboard_RecordMissingRequiredArtifactsAndNextActions()
        {
            var repoRoot = FindRepoRoot();
            var verify = Path.Combine(repoRoot, "scripts/verify-final-distribution-readiness.sh");
            var indexScript = Path.Combine(repoRoot, "scripts/generate-release-bundle-index.sh");
            var dashboardScript = Path.Combine(repoRoot, "scripts/print-release-status-dashboard.sh");

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("index");
                fixture.WriteDryRun();
                RunScript(verify, fixture);
                File.Delete(fixture.FinalReleaseRecordMarkdown);

                var index = RunScript(indexScript, fixture);
                Assert.AreEqual(0, index.ExitCode, index.Output);
                var indexJson = File.ReadAllText(fixture.BundleIndexJson);
                Assert.That(indexJson, Does.Contain("missing"));
                Assert.That(indexJson, Does.Contain("requiredForDistribution"));
                Assert.That(indexJson, Does.Contain(fixture.Sha256));
                Assert.That(indexJson, Does.Contain("FINAL_DISTRIBUTION_DRY_RUN_ONLY"));
                AssertSafe(indexJson + File.ReadAllText(fixture.BundleIndexMarkdown));

                var dashboard = RunScript(dashboardScript, fixture);
                Assert.AreEqual(0, dashboard.ExitCode, dashboard.Output);
                Assert.That(File.ReadAllText(fixture.StatusDashboard), Does.Contain("run credentialed finalization"));
                AssertSafe(dashboard.Output + File.ReadAllText(fixture.StatusDashboard));
            }

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("pending qa dashboard");
                fixture.WriteCredentialed(qaPassed: false, evidenceLocked: false, readinessStatus: "pendingQa");
                RunScript(verify, fixture);
                RunScript(dashboardScript, fixture);
                Assert.That(File.ReadAllText(fixture.StatusDashboard), Does.Contain("complete QA"));
            }

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("ready dashboard");
                fixture.WriteCredentialed(qaPassed: true, evidenceLocked: true, readinessStatus: "readyForDistribution");
                RunScript(verify, fixture);
                RunScript(dashboardScript, fixture);
                Assert.That(File.ReadAllText(fixture.StatusDashboard), Does.Contain("create tag"));
            }
        }

        [Test]
        public void TagFinalization_BlocksDryRunAndPushRequiresLocalTagAndFinalGates()
        {
            var repoRoot = FindRepoRoot();
            var script = Path.Combine(repoRoot, "scripts/prepare-release-tag.sh");

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("dry tag");
                fixture.WriteDryRun();
                var result = RunScript(script, fixture, "--create --yes", skipTagPrepCheck: true);
                Assert.AreNotEqual(0, result.ExitCode);
                Assert.That(result.Output, Does.Contain("TAG_BLOCKED_RELEASE_NOT_READY"));
            }

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("ready push");
                fixture.WriteCredentialed(qaPassed: true, evidenceLocked: true, readinessStatus: "readyForDistribution");
                var dryRun = RunScript(script, fixture, "", skipTagPrepCheck: true);
                Assert.AreEqual(0, dryRun.ExitCode, dryRun.Output);
                Assert.That(dryRun.Output, Does.Contain("Tag currently allowed: true"));
                Assert.That(dryRun.Output, Does.Contain(fixture.Sha256));
                AssertSafe(dryRun.Output);

                var push = RunScript(script, fixture, "--push --yes", skipTagPrepCheck: true);
                Assert.AreNotEqual(0, push.ExitCode);
                Assert.That(push.Output, Does.Contain("Local tag does not exist"));
            }
        }

        [Test]
        public void ReleasePrepCheck_ReferencesPhase32ScriptsAndGuards()
        {
            var repoRoot = FindRepoRoot();
            var prep = File.ReadAllText(Path.Combine(repoRoot, "scripts/release-prep-check.sh"));
            Assert.That(prep, Does.Contain("validate_phase32_summaries"));
            Assert.That(prep, Does.Contain("verify-final-distribution-readiness.sh"));
            Assert.That(prep, Does.Contain("generate-github-release-draft.sh"));
            Assert.That(prep, Does.Contain("generate-release-bundle-index.sh"));
            Assert.That(prep, Does.Contain("print-release-status-dashboard.sh"));
            Assert.That(prep, Does.Contain("Dry-run/ad-hoc tag creation unexpectedly succeeded"));

            var tag = File.ReadAllText(Path.Combine(repoRoot, "scripts/prepare-release-tag.sh"));
            Assert.That(tag, Does.Contain("FINAL_DISTRIBUTION_READY"));
            Assert.That(tag, Does.Contain("--create requires --yes"));
            Assert.That(tag, Does.Contain("--push requires --yes"));
            Assert.That(tag, Does.Contain("Evidence lock status"));
        }

        private static ScriptResult RunScript(string script, TempReleaseFixture fixture, string args = "", bool skipTagPrepCheck = false)
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
            startInfo.EnvironmentVariables["TOKENFORGE_RELEASE_FREEZE_MANIFEST"] = fixture.ManifestPath;
            startInfo.EnvironmentVariables["PACKAGE_PATH"] = fixture.PackagePath;
            if (skipTagPrepCheck)
            {
                startInfo.EnvironmentVariables["TOKENFORGE_RELEASE_TAG_SKIP_PREP_CHECK"] = "true";
            }

            using (var process = Process.Start(startInfo))
            {
                Assert.IsNotNull(process);
                var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                process.WaitForExit();
                return new ScriptResult(process.ExitCode, output);
            }
        }

        private static void AssertSafe(string text)
        {
            Assert.That(text, Does.Not.Contain("Bearer "));
            Assert.That(text, Does.Not.Contain("tokenforge-approved-locations.local.json"));
            Assert.That(text, Does.Not.Contain("tokenforge-sync-local-state.local.json"));
            Assert.That(text, Does.Not.Contain("raw sync payload"));
            Assert.That(text, Does.Not.Contain("/Users/"));
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
                ReleaseDir = Path.Combine(Path.GetTempPath(), "tokenforge-phase32-" + Guid.NewGuid().ToString("N"));
                EvidenceDir = Path.Combine(ReleaseDir, "evidence");
                PackagePath = Path.Combine(ReleaseDir, "TokenForge-macOS-0.18.0-build18.zip");
                ManifestPath = Path.Combine(ReleaseDir, "release-freeze-manifest.md");
                Directory.CreateDirectory(EvidenceDir);
            }

            public string ReleaseDir { get; }
            public string EvidenceDir { get; }
            public string PackagePath { get; }
            public string ManifestPath { get; }
            public string Sha256 { get; private set; }
            public string QaStatus => Path.Combine(EvidenceDir, "gatekeeper-qa-status.json");
            public string ReadinessReport => Path.Combine(ReleaseDir, "release-readiness-report.json");
            public string FinalDistribution => Path.Combine(ReleaseDir, "final-distribution-readiness.json");
            public string FinalReleaseRecordMarkdown => Path.Combine(EvidenceDir, "final-release-record.md");
            public string GithubDraftMarkdown => Path.Combine(EvidenceDir, "github-release-draft.md");
            public string GithubDraftJson => Path.Combine(EvidenceDir, "github-release-draft.json");
            public string BundleIndexJson => Path.Combine(EvidenceDir, "release-bundle-index.json");
            public string BundleIndexMarkdown => Path.Combine(EvidenceDir, "release-bundle-index.md");
            public string StatusDashboard => Path.Combine(ReleaseDir, "release-status-dashboard.json");
            public string EvidenceLock => Path.Combine(EvidenceDir, "evidence-lock-summary.json");

            public void WritePackage(string content)
            {
                using (var archive = ZipFile.Open(PackagePath, ZipArchiveMode.Create))
                {
                    var entry = archive.CreateEntry("TokenForge.app/Contents/Resources/safe.txt");
                    using (var writer = new StreamWriter(entry.Open()))
                    {
                        writer.Write(content);
                    }
                }

                Sha256 = Sha256File(PackagePath);
            }

            public void WriteDryRun(string readinessStatus = "dryRunReady", bool finalRecordReady = false)
            {
                WriteQa(false);
                WriteManifest("FREEZE_MANIFEST_VALID_DRY_RUN_DRIFT_DOCUMENTED");
                File.WriteAllText(Path.Combine(ReleaseDir, "signing-status-summary.json"), "{\"status\":\"SIGNING_READY_BUT_IDENTITY_MISSING\",\"signingIdentityType\":\"adHoc\",\"signedWithDeveloperId\":false,\"hardenedRuntime\":\"enabled\",\"releaseCandidateSha256\":\"" + Sha256 + "\"}");
                File.WriteAllText(Path.Combine(ReleaseDir, "notarization-status-summary.json"), "{\"notarizationStatus\":\"NOTARIZATION_READY_BUT_CREDENTIALS_MISSING\",\"staplingStatus\":\"notAttempted\",\"spctlStatus\":\"notAttempted\",\"releaseCandidateSha256\":\"" + Sha256 + "\"}");
                File.WriteAllText(Path.Combine(ReleaseDir, "credentialed-release-summary.json"), "{\"status\":\"CRED_RELEASE_DRY_RUN_READY\",\"releaseCandidateSha256\":\"" + Sha256 + "\"}");
                File.WriteAllText(Path.Combine(ReleaseDir, "credentialed-release-verification.json"), "{\"status\":\"CRED_RELEASE_VERIFY_DRY_RUN_ONLY\",\"qaStatus\":\"inProgress\",\"releaseCandidateSha256\":\"" + Sha256 + "\"}");
                File.WriteAllText(Path.Combine(ReleaseDir, "release-machine-finalization-summary.json"), "{\"status\":\"RELEASE_MACHINE_BLOCKED\",\"releaseCandidateSha256\":\"" + Sha256 + "\"}");
                File.WriteAllText(Path.Combine(EvidenceDir, "final-privacy-regression-audit.json"), "{\"status\":\"FINAL_PRIVACY_AUDIT_PASSED\"}");
                File.WriteAllText(EvidenceLock, "{\"status\":\"EVIDENCE_LOCK_DRY_RUN_ONLY\",\"releaseCandidateSha256\":\"" + Sha256 + "\"}");
                WriteReadiness(readinessStatus, false, false);
                WriteFinalRecord(finalRecordReady ? "ready for distribution" : "dry-run only");
            }

            public void WriteCredentialed(bool qaPassed, bool evidenceLocked, string readinessStatus)
            {
                WriteQa(qaPassed);
                WriteManifest(readinessStatus == "readyForDistribution" ? "FREEZE_MANIFEST_VALID_FINAL_MATCH" : "FREEZE_MANIFEST_VALID_DRY_RUN_DRIFT_DOCUMENTED");
                File.WriteAllText(Path.Combine(ReleaseDir, "signing-status-summary.json"), "{\"status\":\"SIGNING_SUCCEEDED\",\"signingIdentityType\":\"developerId\",\"signedWithDeveloperId\":true,\"hardenedRuntime\":\"enabled\",\"releaseCandidateSha256\":\"" + Sha256 + "\"}");
                File.WriteAllText(Path.Combine(ReleaseDir, "notarization-status-summary.json"), "{\"notarizationStatus\":\"NOTARIZATION_SUCCEEDED\",\"staplingStatus\":\"STAPLE_SUCCEEDED\",\"spctlStatus\":\"SPCTL_SUCCEEDED\",\"releaseCandidateSha256\":\"" + Sha256 + "\"}");
                File.WriteAllText(Path.Combine(ReleaseDir, "credentialed-release-summary.json"), "{\"status\":\"CRED_RELEASE_READY\",\"releaseCandidateSha256\":\"" + Sha256 + "\"}");
                File.WriteAllText(Path.Combine(ReleaseDir, "credentialed-release-verification.json"), "{\"status\":\"" + (qaPassed ? "CRED_RELEASE_VERIFIED_READY_FOR_DISTRIBUTION" : "CRED_RELEASE_VERIFIED_PENDING_QA") + "\",\"qaStatus\":\"" + (qaPassed ? "passed" : "inProgress") + "\",\"releaseCandidateSha256\":\"" + Sha256 + "\"}");
                File.WriteAllText(Path.Combine(ReleaseDir, "release-machine-finalization-summary.json"), "{\"status\":\"" + (qaPassed ? "RELEASE_MACHINE_READY_FOR_TAG" : "RELEASE_MACHINE_PENDING_QA") + "\",\"releaseCandidateSha256\":\"" + Sha256 + "\"}");
                File.WriteAllText(Path.Combine(EvidenceDir, "final-privacy-regression-audit.json"), "{\"status\":\"FINAL_PRIVACY_AUDIT_PASSED\"}");
                File.WriteAllText(EvidenceLock, "{\"status\":\"" + (evidenceLocked ? "EVIDENCE_LOCK_READY" : "EVIDENCE_LOCK_PENDING_QA") + "\",\"releaseCandidateSha256\":\"" + Sha256 + "\",\"evidenceArchivePath\":\"" + Path.Combine(ReleaseDir, "TokenForge-0.18.0-build18-release-evidence.zip") + "\",\"evidenceArchiveSha256\":\"" + new string('b', 64) + "\"}");
                WriteReadiness(readinessStatus, true, qaPassed, evidenceLocked);
                WriteFinalRecord(readinessStatus == "readyForDistribution" ? "ready for distribution" : qaPassed ? "pending evidence lock" : "pending QA");
                File.WriteAllText(Path.Combine(ReleaseDir, "TokenForge-0.18.0-build18-release-evidence.zip"), "evidence");
            }

            private void WriteReadiness(string releaseStatus, bool developerId, bool qaPassed, bool evidenceLocked = false)
            {
                File.WriteAllText(ReadinessReport,
                    "{\"schemaVersion\":5,\"version\":\"0.18.0\",\"build\":\"18\",\"releaseStatus\":\"" + releaseStatus + "\",\"releaseCandidateSha256\":\"" + Sha256 + "\",\"packageScanResult\":\"passed\",\"privacyScanResult\":\"passed\",\"signingSummary\":{\"status\":\"" + (developerId ? "SIGNING_SUCCEEDED" : "SIGNING_READY_BUT_IDENTITY_MISSING") + "\",\"signingIdentityType\":\"" + (developerId ? "developerId" : "adHoc") + "\",\"signedWithDeveloperId\":" + developerId.ToString().ToLowerInvariant() + ",\"hardenedRuntime\":\"enabled\"},\"notarizationSummary\":{\"notarizationStatus\":\"" + (developerId ? "NOTARIZATION_SUCCEEDED" : "NOTARIZATION_READY_BUT_CREDENTIALS_MISSING") + "\",\"staplingStatus\":\"" + (developerId ? "STAPLE_SUCCEEDED" : "notAttempted") + "\",\"spctlStatus\":\"" + (developerId ? "SPCTL_SUCCEEDED" : "notAttempted") + "\"},\"credentialedVerificationSummary\":{\"status\":\"" + (developerId && qaPassed ? "CRED_RELEASE_VERIFIED_READY_FOR_DISTRIBUTION" : developerId ? "CRED_RELEASE_VERIFIED_PENDING_QA" : "CRED_RELEASE_VERIFY_DRY_RUN_ONLY") + "\"},\"evidenceLockSummary\":{\"status\":\"" + (evidenceLocked ? "EVIDENCE_LOCK_READY" : "EVIDENCE_LOCK_DRY_RUN_ONLY") + "\"},\"qaStatusSummary\":{\"overallStatus\":\"" + (qaPassed ? "passed" : "inProgress") + "\"},\"privacyAssertions\":{\"noLocalOnlyFilesInPackage\":true,\"noApprovedLocationPathsInEvidence\":true,\"noSecretsInEvidence\":true,\"noRawSyncStateInEvidence\":true,\"noBackgroundSyncAdded\":true},\"knownLimitations\":[],\"nextAction\":\"test\"}");
            }

            private void WriteFinalRecord(string readiness)
            {
                File.WriteAllText(FinalReleaseRecordMarkdown, "# Final release record\n\nSafe summary.\n");
                File.WriteAllText(Path.Combine(EvidenceDir, "final-release-record.json"), "{\"releaseCandidateSha256\":\"" + Sha256 + "\",\"distributionReadiness\":\"" + readiness + "\",\"notarizationStatus\":\"NOTARIZATION_SUCCEEDED\",\"signingIdentityType\":\"developerId\"}");
            }

            private void WriteManifest(string status)
            {
                File.WriteAllText(Path.Combine(EvidenceDir, "release-freeze-validation.json"), "{\"status\":\"" + status + "\",\"currentRegeneratedDryRunSha256\":\"" + Sha256 + "\",\"readinessSha256\":\"" + Sha256 + "\",\"credentialedFinalSha256\":\"" + Sha256 + "\"}");
                File.WriteAllText(ManifestPath, "# Release freeze manifest\n\n- Version: 0.18.0\n- Build: 18\n- Release candidate filename: TokenForge-macOS-0.18.0-build18.zip\n- Current dry-run SHA-256: " + Sha256 + "\n");
            }

            private void WriteQa(bool passed)
            {
                File.WriteAllText(QaStatus, "{\"schemaVersion\":2,\"qaOverallStatus\":\"" + (passed ? "passed" : "inProgress") + "\",\"overallStatus\":\"" + (passed ? "passed" : "inProgress") + "\",\"items\":{}}");
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
