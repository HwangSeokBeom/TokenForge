#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using NUnit.Framework;

namespace TokenForge.Client.Tests
{
    public sealed class Phase31ReleaseMachineFinalizationTests
    {
        [Test]
        public void ReleaseMachineFinalization_DryRunAndMissingCredentialsAreSafe()
        {
            var repoRoot = FindRepoRoot();
            var script = Path.Combine(repoRoot, "scripts/run-release-machine-finalization.sh");

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("safe package");
                fixture.WriteDryRunSummaries();

                var dryRun = RunScript(script, fixture);
                Assert.AreEqual(0, dryRun.ExitCode, dryRun.Output);
                var summary = File.ReadAllText(fixture.ReleaseMachineFinalizationSummary);
                Assert.That(summary, Does.Contain("RELEASE_MACHINE_BLOCKED"));
                Assert.That(summary, Does.Not.Contain("Bearer "));
                Assert.That(summary, Does.Not.Contain("tokenforge-approved-locations.local.json"));

                var credentialed = RunScript(script, fixture, "--credentialed");
                Assert.AreNotEqual(0, credentialed.ExitCode, credentialed.Output);
                summary = File.ReadAllText(fixture.ReleaseMachineFinalizationSummary);
                Assert.That(summary, Does.Contain("Missing required credential environment variables"));
                Assert.That(summary, Does.Contain("DEVELOPER_ID_APPLICATION"));
                Assert.That(summary, Does.Contain("APPLE_APP_SPECIFIC_PASSWORD"));
            }
        }

        [Test]
        public void ReleaseMachineFinalization_MapsPendingQaReadyAndBlockedStates()
        {
            var repoRoot = FindRepoRoot();
            var helper = Path.Combine(repoRoot, "scripts/phase31_release_machine_summary.py");

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("safe package");
                fixture.WriteCredentialedSummaries(qaPassed: false, blocked: false, includeEvidenceLock: false);
                var result = RunPython(helper, fixture);
                Assert.AreEqual(0, result.ExitCode, result.Output);
                Assert.That(File.ReadAllText(fixture.ReleaseMachineFinalizationSummary), Does.Contain("RELEASE_MACHINE_PENDING_QA"));
            }

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("safe package");
                fixture.WriteCredentialedSummaries(qaPassed: true, blocked: false, includeEvidenceLock: false);
                var result = RunPython(helper, fixture);
                Assert.AreEqual(0, result.ExitCode, result.Output);
                Assert.That(File.ReadAllText(fixture.ReleaseMachineFinalizationSummary), Does.Contain("RELEASE_MACHINE_READY_FOR_TAG"));
            }

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("safe package");
                fixture.WriteCredentialedSummaries(qaPassed: true, blocked: true, includeEvidenceLock: false);
                var result = RunPython(helper, fixture);
                Assert.AreNotEqual(0, result.ExitCode);
                Assert.That(File.ReadAllText(fixture.ReleaseMachineFinalizationSummary), Does.Contain("RELEASE_MACHINE_BLOCKED"));
            }
        }

        [Test]
        public void FreezeManifestValidation_HandlesDryRunDriftBlocksAndFinalMatch()
        {
            var repoRoot = FindRepoRoot();
            var script = Path.Combine(repoRoot, "scripts/validate-release-freeze-manifest.sh");

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("new dry run package");
                fixture.WriteManifest(documentDryRunDrift: true, sha: new string('a', 64));
                fixture.WriteDryRunSummaries();
                var result = RunScript(script, fixture);
                Assert.AreEqual(0, result.ExitCode, result.Output);
                Assert.That(File.ReadAllText(fixture.FreezeValidation), Does.Contain("FREEZE_MANIFEST_VALID_DRY_RUN_DRIFT_DOCUMENTED"));
            }

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("new dry run package");
                fixture.WriteManifest(documentDryRunDrift: false, sha: new string('a', 64));
                fixture.WriteDryRunSummaries();
                var result = RunScript(script, fixture);
                Assert.AreNotEqual(0, result.ExitCode);
                Assert.That(File.ReadAllText(fixture.FreezeValidation), Does.Contain("FREEZE_MANIFEST_BLOCKED_UNDOCUMENTED_SHA_DRIFT"));
            }

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("final package");
                fixture.WriteCredentialedSummaries(qaPassed: true, blocked: false, includeEvidenceLock: true, releaseStatus: "readyForDistribution");
                fixture.WriteManifest(documentDryRunDrift: true, sha: fixture.Sha256, finalSha: fixture.Sha256);
                var result = RunScript(script, fixture);
                Assert.AreEqual(0, result.ExitCode, result.Output);
                Assert.That(File.ReadAllText(fixture.FreezeValidation), Does.Contain("FREEZE_MANIFEST_VALID_FINAL_MATCH"));
            }
        }

        [Test]
        public void EvidenceLock_DryRunPendingBlockedReadyAndUnsafeEvidence()
        {
            var repoRoot = FindRepoRoot();
            var script = Path.Combine(repoRoot, "scripts/lock-release-evidence.sh");

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("safe package");
                fixture.WriteDryRunSummaries();
                var result = RunScript(script, fixture);
                Assert.AreEqual(0, result.ExitCode, result.Output);
                Assert.That(File.ReadAllText(fixture.EvidenceLockSummary), Does.Contain("EVIDENCE_LOCK_DRY_RUN_ONLY"));
            }

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("safe package");
                fixture.WriteCredentialedSummaries(qaPassed: false, blocked: false, includeEvidenceLock: false);
                var result = RunScript(script, fixture);
                Assert.AreEqual(0, result.ExitCode, result.Output);
                Assert.That(File.ReadAllText(fixture.EvidenceLockSummary), Does.Contain("EVIDENCE_LOCK_PENDING_QA"));
            }

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("safe package");
                fixture.WriteCredentialedSummaries(qaPassed: true, blocked: true, includeEvidenceLock: false);
                var result = RunScript(script, fixture);
                Assert.AreNotEqual(0, result.ExitCode);
                Assert.That(File.ReadAllText(fixture.EvidenceLockSummary), Does.Contain("EVIDENCE_LOCK_BLOCKED"));
            }

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("safe package");
                fixture.WriteCredentialedSummaries(qaPassed: true, blocked: false, includeEvidenceLock: false);
                var result = RunScript(script, fixture);
                Assert.AreEqual(0, result.ExitCode, result.Output);
                Assert.That(File.ReadAllText(fixture.EvidenceLockSummary), Does.Contain("EVIDENCE_LOCK_READY"));
                Assert.IsTrue(File.Exists(fixture.EvidenceArchive));
                using (var archive = ZipFile.OpenRead(fixture.EvidenceArchive))
                {
                    foreach (var entry in archive.Entries)
                    {
                        Assert.That(entry.FullName, Does.Not.Contain("tokenforge-sync-state.local.json"));
                        Assert.That(entry.FullName, Does.Not.Contain("tokenforge-approved-locations.local.json"));
                    }
                }
            }

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("safe package");
                fixture.WriteCredentialedSummaries(qaPassed: true, blocked: false, includeEvidenceLock: false);
                File.WriteAllText(Path.Combine(fixture.EvidenceDir, "release-evidence.md"), "Bearer abcdefghijklmnopqrstuvwxyz1234567890");
                var result = RunScript(script, fixture);
                Assert.AreNotEqual(0, result.ExitCode);
                Assert.That(File.ReadAllText(fixture.EvidenceLockSummary), Does.Contain("EVIDENCE_LOCK_BLOCKED"));
            }
        }

        [Test]
        public void TagGuardrails_BlockDryRunAndRequireEvidenceLockReady()
        {
            var repoRoot = FindRepoRoot();
            var script = Path.Combine(repoRoot, "scripts/prepare-release-tag.sh");
            var tagName = "v0.18.0-build18";
            var tagExistedBefore = RunGit(repoRoot, "rev-parse -q --verify refs/tags/" + tagName).ExitCode == 0;

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("safe package");
                fixture.WriteDryRunSummaries();
                var dryRun = RunScript(script, fixture, "", skipTagPrepCheck: true);
                Assert.AreEqual(0, dryRun.ExitCode, dryRun.Output);
                Assert.That(dryRun.Output, Does.Contain("Suggested tag: v0.18.0-build18"));
                Assert.That(dryRun.Output, Does.Contain("Tag currently allowed: false"));

                var createWithoutYes = RunScript(script, fixture, "--create", skipTagPrepCheck: true);
                Assert.AreNotEqual(0, createWithoutYes.ExitCode);
                Assert.That(createWithoutYes.Output, Does.Contain("--create requires --yes"));

                var createDryRun = RunScript(script, fixture, "--create --yes", skipTagPrepCheck: true);
                Assert.AreNotEqual(0, createDryRun.ExitCode);
                Assert.That(createDryRun.Output, Does.Contain("TAG_BLOCKED_RELEASE_NOT_READY"));
            }

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("safe package");
                fixture.WriteCredentialedSummaries(qaPassed: true, blocked: false, includeEvidenceLock: false, releaseStatus: "readyForDistribution");
                var result = RunScript(script, fixture, "--create --yes", skipTagPrepCheck: true);
                Assert.AreNotEqual(0, result.ExitCode);
                Assert.That(result.Output, Does.Contain("evidenceLockStatus is missing"));
            }

            var tagExistsAfter = RunGit(repoRoot, "rev-parse -q --verify refs/tags/" + tagName).ExitCode == 0;
            Assert.AreEqual(tagExistedBefore, tagExistsAfter);
        }

        [Test]
        public void ReadinessReportAndReleasePrepCheck_ReferencePhase31SchemaAndGuards()
        {
            var repoRoot = FindRepoRoot();
            var readinessScript = File.ReadAllText(Path.Combine(repoRoot, "scripts/generate-release-readiness-report.sh"));
            Assert.That(readinessScript, Does.Contain("\"schemaVersion\": 5"));
            Assert.That(readinessScript, Does.Contain("evidencePending"));
            Assert.That(readinessScript, Does.Contain("EVIDENCE_LOCK_READY"));
            Assert.That(readinessScript, Does.Contain("releaseMachineFinalizationSummary"));
            Assert.That(readinessScript, Does.Contain("freezeManifestValidationSummary"));

            var prep = File.ReadAllText(Path.Combine(repoRoot, "scripts/release-prep-check.sh"));
            Assert.That(prep, Does.Contain("validate_phase31_summaries"));
            Assert.That(prep, Does.Contain("validate-release-freeze-manifest.sh"));
            Assert.That(prep, Does.Contain("run-release-machine-finalization.sh"));
            Assert.That(prep, Does.Contain("lock-release-evidence.sh"));
            Assert.That(prep, Does.Contain("readyForDistribution requires evidence lock ready"));
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

        private static ScriptResult RunPython(string helper, TempReleaseFixture fixture)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "/usr/bin/python3",
                Arguments = Quote(helper) + " " + Quote(fixture.ReleaseMachineFinalizationSummary) + " " + Quote(fixture.ReleaseDir) + " " + Quote(fixture.EvidenceDir) + " " + Quote(fixture.PackagePath),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            using (var process = Process.Start(startInfo))
            {
                Assert.IsNotNull(process);
                var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                process.WaitForExit();
                return new ScriptResult(process.ExitCode, output);
            }
        }

        private static ScriptResult RunGit(string repoRoot, string args)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = "-lc " + Quote("git " + args),
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
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
                ReleaseDir = Path.Combine(Path.GetTempPath(), "tokenforge-phase31-" + Guid.NewGuid().ToString("N"));
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
            public string ReleaseMachineFinalizationSummary => Path.Combine(ReleaseDir, "release-machine-finalization-summary.json");
            public string FreezeValidation => Path.Combine(EvidenceDir, "release-freeze-validation.json");
            public string EvidenceLockSummary => Path.Combine(EvidenceDir, "evidence-lock-summary.json");
            public string EvidenceArchive => Path.Combine(ReleaseDir, "TokenForge-0.18.0-build18-release-evidence.zip");

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

            public void WriteManifest(bool documentDryRunDrift, string sha, string finalSha = "")
            {
                var drift = documentDryRunDrift ? "\n## Phase 31 dry-run SHA drift policy\n\nDry-run rebuilds may legitimately regenerate zip SHA values before credentialed finalization.\n- Current regenerated dry-run SHA-256: " + Sha256 + "\n" : "";
                var final = string.IsNullOrEmpty(finalSha) ? "" : "\n- Credentialed final SHA-256: " + finalSha + "\n";
                File.WriteAllText(ManifestPath,
                    "# TokenForge MVP Release Freeze Manifest\n\n" +
                    "- Version: 0.18.0\n" +
                    "- Build: 18\n" +
                    "- Release candidate filename: TokenForge-macOS-0.18.0-build18.zip\n" +
                    "- Current dry-run SHA-256: " + sha + "\n" +
                    "- Distribution readiness: dry-run only, not ready for public distribution\n" +
                    drift + final);
            }

            public void WriteDryRunSummaries()
            {
                if (!File.Exists(ManifestPath))
                {
                    WriteManifest(true, new string('a', 64));
                }
                WriteQa(false);
                File.WriteAllText(Path.Combine(ReleaseDir, "signing-status-summary.json"), "{\"status\":\"SIGNING_READY_BUT_IDENTITY_MISSING\",\"signingIdentityType\":\"adHoc\",\"signedWithDeveloperId\":false,\"hardenedRuntime\":\"enabled\",\"releaseCandidateSha256\":\"" + Sha256 + "\"}");
                File.WriteAllText(Path.Combine(ReleaseDir, "notarization-status-summary.json"), "{\"notarizationStatus\":\"NOTARIZATION_READY_BUT_CREDENTIALS_MISSING\",\"staplingStatus\":\"notAttempted\",\"spctlStatus\":\"notAttempted\",\"releaseCandidateSha256\":\"" + Sha256 + "\"}");
                File.WriteAllText(Path.Combine(ReleaseDir, "credentialed-release-verification.json"), "{\"status\":\"CRED_RELEASE_VERIFY_DRY_RUN_ONLY\",\"qaStatus\":\"inProgress\",\"releaseCandidateSha256\":\"" + Sha256 + "\"}");
                File.WriteAllText(Path.Combine(EvidenceDir, "final-privacy-regression-audit.json"), "{\"status\":\"FINAL_PRIVACY_AUDIT_PASSED\"}");
                WriteReadiness("dryRunReady", false, false);
                WriteSafeEvidence();
            }

            public void WriteCredentialedSummaries(bool qaPassed, bool blocked, bool includeEvidenceLock, string releaseStatus = null)
            {
                WriteQa(qaPassed);
                var status = blocked ? "blocked" : (releaseStatus ?? (qaPassed ? "evidencePending" : "pendingQa"));
                File.WriteAllText(Path.Combine(ReleaseDir, "signing-status-summary.json"), "{\"status\":\"" + (blocked ? "SIGNING_BLOCKED_PRIVACY_SCAN_FAILED" : "SIGNING_SUCCEEDED") + "\",\"signingIdentityType\":\"developerId\",\"signedWithDeveloperId\":true,\"hardenedRuntime\":\"enabled\",\"releaseCandidateSha256\":\"" + Sha256 + "\"}");
                File.WriteAllText(Path.Combine(ReleaseDir, "notarization-status-summary.json"), "{\"notarizationStatus\":\"NOTARIZATION_SUCCEEDED\",\"staplingStatus\":\"STAPLE_SUCCEEDED\",\"spctlStatus\":\"SPCTL_SUCCEEDED\",\"releaseCandidateSha256\":\"" + Sha256 + "\"}");
                File.WriteAllText(Path.Combine(ReleaseDir, "credentialed-release-verification.json"), "{\"status\":\"" + (blocked ? "CRED_RELEASE_VERIFY_BLOCKED" : qaPassed ? "CRED_RELEASE_VERIFIED_READY_FOR_DISTRIBUTION" : "CRED_RELEASE_VERIFIED_PENDING_QA") + "\",\"qaStatus\":\"" + (qaPassed ? "passed" : "inProgress") + "\",\"releaseCandidateSha256\":\"" + Sha256 + "\"}");
                File.WriteAllText(Path.Combine(EvidenceDir, "final-privacy-regression-audit.json"), "{\"status\":\"" + (blocked ? "FINAL_PRIVACY_AUDIT_FAILED" : "FINAL_PRIVACY_AUDIT_PASSED") + "\"}");
                if (includeEvidenceLock)
                {
                    File.WriteAllText(EvidenceLockSummary, "{\"status\":\"EVIDENCE_LOCK_READY\"}");
                }
                WriteReadiness(status, true, qaPassed);
                WriteSafeEvidence();
            }

            private void WriteReadiness(string releaseStatus, bool developerId, bool qaPassed)
            {
                var identity = developerId ? "developerId" : "adHoc";
                var verification = developerId && qaPassed ? "CRED_RELEASE_VERIFIED_READY_FOR_DISTRIBUTION" : developerId ? "CRED_RELEASE_VERIFIED_PENDING_QA" : "CRED_RELEASE_VERIFY_DRY_RUN_ONLY";
                File.WriteAllText(Path.Combine(ReleaseDir, "release-readiness-report.json"),
                    "{\"schemaVersion\":5,\"version\":\"0.18.0\",\"build\":\"18\",\"releaseStatus\":\"" + releaseStatus + "\",\"releaseCandidateSha256\":\"" + Sha256 + "\",\"packageScanResult\":\"passed\",\"privacyScanResult\":\"passed\",\"signingSummary\":{\"signingIdentityType\":\"" + identity + "\",\"signedWithDeveloperId\":" + developerId.ToString().ToLowerInvariant() + ",\"hardenedRuntime\":\"enabled\"},\"notarizationSummary\":{\"notarizationStatus\":\"" + (developerId ? "NOTARIZATION_SUCCEEDED" : "NOTARIZATION_READY_BUT_CREDENTIALS_MISSING") + "\",\"staplingStatus\":\"" + (developerId ? "STAPLE_SUCCEEDED" : "notAttempted") + "\",\"spctlStatus\":\"" + (developerId ? "SPCTL_SUCCEEDED" : "notAttempted") + "\"},\"credentialedVerificationSummary\":{\"status\":\"" + verification + "\"},\"evidenceLockSummary\":{},\"qaStatusSummary\":{\"overallStatus\":\"" + (qaPassed ? "passed" : "inProgress") + "\"},\"privacyAssertions\":{\"noLocalOnlyFilesInPackage\":true,\"noApprovedLocationPathsInEvidence\":true,\"noSecretsInEvidence\":true,\"noRawSyncStateInEvidence\":true,\"noBackgroundSyncAdded\":true},\"knownLimitations\":[],\"nextAction\":\"test\"}");
            }

            private void WriteQa(bool passed)
            {
                var status = passed ? "passed" : "notStarted";
                var items = "\"cleanInstall\":{\"status\":\"" + status + "\",\"note\":\"\"},\"firstLaunch\":{\"status\":\"" + status + "\",\"note\":\"\"},\"moveToApplications\":{\"status\":\"" + status + "\",\"note\":\"\"},\"quarantineAssessment\":{\"status\":\"" + status + "\",\"note\":\"\"},\"localStateLocation\":{\"status\":\"" + status + "\",\"note\":\"\"},\"safeSyncPanelLaunch\":{\"status\":\"" + status + "\",\"note\":\"\"},\"confirmationModalCancel\":{\"status\":\"" + status + "\",\"note\":\"\"},\"confirmationModalTypedPhrase\":{\"status\":\"" + status + "\",\"note\":\"\"},\"conflictAuditLocalOnly\":{\"status\":\"" + status + "\",\"note\":\"\"},\"retryTombstoneLocalOnly\":{\"status\":\"" + status + "\",\"note\":\"\"},\"approvedLocationsLocalOnly\":{\"status\":\"" + status + "\",\"note\":\"\"},\"privacyScan\":{\"status\":\"" + status + "\",\"note\":\"\"},\"packageScan\":{\"status\":\"" + status + "\",\"note\":\"\"}";
                File.WriteAllText(QaStatus, "{\"schemaVersion\":2,\"qaOverallStatus\":\"" + (passed ? "passed" : "inProgress") + "\",\"overallStatus\":\"" + (passed ? "passed" : "inProgress") + "\",\"items\":{" + items + "}}");
            }

            private void WriteSafeEvidence()
            {
                File.WriteAllText(Path.Combine(EvidenceDir, "final-release-record.md"), "# Final release record\n\nSafe summary.\n");
                File.WriteAllText(Path.Combine(EvidenceDir, "final-release-record.json"), "{\"releaseCandidateSha256\":\"" + Sha256 + "\",\"distributionReadiness\":\"dry-run only\"}");
                File.WriteAllText(Path.Combine(EvidenceDir, "release-operator-handoff.md"), "# Release operator handoff\n\nSafe summary.\n");
                File.WriteAllText(Path.Combine(EvidenceDir, "release-operator-handoff.json"), "{\"safe\":true}");
                File.WriteAllText(Path.Combine(EvidenceDir, "final-release-handoff.md"), "# Final handoff\n\nSafe summary.\n");
                File.WriteAllText(Path.Combine(EvidenceDir, "release-evidence.json"), "{\"safe\":true}");
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
