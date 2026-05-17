#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using NUnit.Framework;

namespace TokenForge.Client.Tests
{
    public sealed class Phase30ReleaseFreezeHandoffTests
    {
        [Test]
        public void ReleaseFreezeManifest_FreezesDryRunMvpMetadataAndPrivacyAssertions()
        {
            var repoRoot = FindRepoRoot();
            var manifestPath = Path.Combine(repoRoot, "Docs/release-freeze-manifest-v0.18.0-build18.md");
            Assert.IsTrue(File.Exists(manifestPath));

            var manifest = File.ReadAllText(manifestPath);
            Assert.That(manifest, Does.Contain("App name: TokenForge"));
            Assert.That(manifest, Does.Contain("Target platform: macOS"));
            Assert.That(manifest, Does.Contain("Release type: MVP release candidate"));
            Assert.That(manifest, Does.Contain("Version: 0.18.0"));
            Assert.That(manifest, Does.Contain("Build: 18"));
            Assert.That(manifest, Does.Contain("TokenForge-macOS-0.18.0-build18.zip"));
            Assert.That(manifest, Does.Contain("24d6c3d30c3a967a350fec56baf010c39b80f8b9ef2be5bb21543dac4b385d46"));
            Assert.That(manifest, Does.Contain("dry-run only, not ready for public distribution"));
            Assert.That(manifest, Does.Contain("SIGNING_READY_BUT_IDENTITY_MISSING"));
            Assert.That(manifest, Does.Contain("NOTARIZATION_READY_BUT_CREDENTIALS_MISSING"));
            Assert.That(manifest, Does.Contain("DEVELOPER_ID_APPLICATION"));
            Assert.That(manifest, Does.Contain("APPLE_ID"));
            Assert.That(manifest, Does.Contain("APPLE_TEAM_ID"));
            Assert.That(manifest, Does.Contain("APPLE_APP_SPECIFIC_PASSWORD"));
            Assert.That(manifest, Does.Contain("No local-only sync"));
            Assert.That(manifest, Does.Contain("No approved-location paths in evidence"));
            Assert.That(manifest, Does.Contain("No raw sync state in evidence"));
            Assert.That(manifest, Does.Contain("No secrets in evidence"));
            Assert.That(manifest, Does.Not.Contain("Bearer "));
            Assert.That(manifest, Does.Not.Contain("BEGIN PRIVATE KEY"));
        }

        [Test]
        public void ReleaseOperatorHandoff_GeneratesSafeDryRunHandoffWithExactCommands()
        {
            var repoRoot = FindRepoRoot();
            var script = Path.Combine(repoRoot, "scripts/generate-release-operator-handoff.sh");

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("safe package");
                fixture.WriteSummaries(developerId: false, notarized: false, stapled: false, spctl: false, shaOverride: fixture.Sha256);
                fixture.WriteFinalRecord("dry-run only");
                fixture.WriteQaInProgress();

                var result = RunScript(script, fixture);
                Assert.AreEqual(0, result.ExitCode, result.Output);
                Assert.IsTrue(File.Exists(fixture.ReleaseOperatorHandoffMarkdown));
                Assert.IsTrue(File.Exists(fixture.ReleaseOperatorHandoffJson));

                var handoff = File.ReadAllText(fixture.ReleaseOperatorHandoffMarkdown);
                Assert.That(handoff, Does.Contain("scripts/run-credentialed-release.sh --credentialed"));
                Assert.That(handoff, Does.Contain("scripts/verify-credentialed-release.sh"));
                Assert.That(handoff, Does.Contain("scripts/update-gatekeeper-qa-status.sh init"));
                Assert.That(handoff, Does.Contain("scripts/generate-final-release-record.sh"));
                Assert.That(handoff, Does.Contain("scripts/final-privacy-regression-audit.sh"));
                Assert.That(handoff, Does.Contain("DEVELOPER_ID_APPLICATION"));
                Assert.That(handoff, Does.Contain("APPLE_APP_SPECIFIC_PASSWORD"));
                Assert.That(handoff, Does.Contain("Distribution decision: dry-run only"));
                Assert.That(handoff, Does.Not.Contain("Bearer "));
                Assert.That(handoff, Does.Not.Contain("tokenforge-sync-state.local.json"));
                Assert.That(handoff, Does.Not.Contain("tokenforge-approved-locations.local.json"));
                Assert.That(handoff, Does.Not.Contain("/Users/"));
            }
        }

        [Test]
        public void PrepareReleaseTag_DryRunWarnsDirtyAndDoesNotCreateOrPush()
        {
            var repoRoot = FindRepoRoot();
            var script = Path.Combine(repoRoot, "scripts/prepare-release-tag.sh");
            var tagName = "v0.18.0-build18";
            var tagExistedBefore = RunGit(repoRoot, "rev-parse -q --verify refs/tags/" + tagName).ExitCode == 0;

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("tag package");
                fixture.WriteSummaries(developerId: false, notarized: false, stapled: false, spctl: false, shaOverride: fixture.Sha256);
                var result = RunScript(script, fixture, "", skipTagPrepCheck: true);
                Assert.AreEqual(0, result.ExitCode, result.Output);
                Assert.That(result.Output, Does.Contain("Suggested tag: v0.18.0-build18"));
                Assert.That(result.Output, Does.Contain("TokenForge macOS 0.18.0 build 18"));
                Assert.That(result.Output, Does.Contain("MVP release"));
                Assert.That(result.Output, Does.Contain("SHA-256: " + fixture.Sha256));
                Assert.That(result.Output, Does.Contain("Evidence lock status"));
                Assert.That(result.Output, Does.Contain("git push origin v0.18.0-build18"));
                Assert.That(result.Output, Does.Contain("Result: dry-run only; no tag created"));

                var tagExistsAfter = RunGit(repoRoot, "rev-parse -q --verify refs/tags/" + tagName).ExitCode == 0;
                Assert.AreEqual(tagExistedBefore, tagExistsAfter);

                var createWithoutYes = RunScript(script, fixture, "--create", skipTagPrepCheck: true);
                Assert.AreNotEqual(0, createWithoutYes.ExitCode);
                Assert.That(createWithoutYes.Output, Does.Contain("--create requires --yes"));

                var pushWithoutYes = RunScript(script, fixture, "--push", skipTagPrepCheck: true);
                Assert.AreNotEqual(0, pushWithoutYes.ExitCode);
                Assert.That(pushWithoutYes.Output, Does.Contain("--push requires --yes"));

                var pushWithoutReady = RunScript(script, fixture, "--push --yes", skipTagPrepCheck: true);
                Assert.AreNotEqual(0, pushWithoutReady.ExitCode);
                Assert.That(pushWithoutReady.Output, Does.Contain("TAG_BLOCKED_RELEASE_NOT_READY"));
            }
        }

        [Test]
        public void FinalPrivacyRegressionAudit_PassesCurrentDryRunShapeAndProducesJsonSummary()
        {
            var repoRoot = FindRepoRoot();
            var script = Path.Combine(repoRoot, "scripts/final-privacy-regression-audit.sh");

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("safe package");
                fixture.WriteSummaries(developerId: false, notarized: false, stapled: false, spctl: false, shaOverride: fixture.Sha256);
                fixture.WriteFinalRecord("dry-run only");
                fixture.WriteQaInProgress();

                var result = RunScript(script, fixture);
                Assert.AreEqual(0, result.ExitCode, result.Output);
                Assert.That(File.ReadAllText(fixture.FinalPrivacyAuditJson), Does.Contain("FINAL_PRIVACY_AUDIT_PASSED"));
            }
        }

        [Test]
        public void FinalPrivacyRegressionAudit_FailsUnsafePackageEvidenceAndOverclaims()
        {
            var repoRoot = FindRepoRoot();
            var script = Path.Combine(repoRoot, "scripts/final-privacy-regression-audit.sh");

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("safe", "tokenforge-sync-state.local.json");
                fixture.WriteSummaries(developerId: false, notarized: false, stapled: false, spctl: false, shaOverride: fixture.Sha256);
                fixture.WriteFinalRecord("dry-run only");
                var result = RunScript(script, fixture);
                Assert.AreNotEqual(0, result.ExitCode, result.Output);
                Assert.That(File.ReadAllText(fixture.FinalPrivacyAuditJson), Does.Contain("FINAL_PRIVACY_AUDIT_FAILED"));
                Assert.That(File.ReadAllText(fixture.FinalPrivacyAuditJson), Does.Contain("tokenforge-sync-state.local.json"));
            }

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("safe");
                fixture.WriteSummaries(developerId: false, notarized: false, stapled: false, spctl: false, shaOverride: fixture.Sha256);
                File.WriteAllText(Path.Combine(fixture.EvidenceDir, "unsafe.md"), "Bearer abcdefghijklmnopqrstuvwxyz1234567890");
                var result = RunScript(script, fixture);
                Assert.AreNotEqual(0, result.ExitCode, result.Output);
                Assert.That(File.ReadAllText(fixture.FinalPrivacyAuditJson), Does.Contain("evidence"));
            }

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("safe");
                fixture.WriteSummaries(developerId: false, notarized: false, stapled: false, spctl: false, shaOverride: fixture.Sha256, releaseStatus: "readyForDistribution");
                fixture.WriteFinalRecord("dry-run only");
                var result = RunScript(script, fixture);
                Assert.AreNotEqual(0, result.ExitCode, result.Output);
                Assert.That(File.ReadAllText(fixture.FinalPrivacyAuditJson), Does.Contain("readiness report overclaims"));
            }

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("safe");
                fixture.WriteSummaries(developerId: false, notarized: false, stapled: false, spctl: false, shaOverride: fixture.Sha256);
                fixture.WriteFinalRecord("ready for distribution");
                var result = RunScript(script, fixture);
                Assert.AreNotEqual(0, result.ExitCode, result.Output);
                Assert.That(File.ReadAllText(fixture.FinalPrivacyAuditJson), Does.Contain("final release record overclaims"));
            }
        }

        [Test]
        public void ReleasePrepCheck_ReferencesPhase30ArtifactsAndFailClosedRules()
        {
            var repoRoot = FindRepoRoot();
            var prep = File.ReadAllText(Path.Combine(repoRoot, "scripts/release-prep-check.sh"));
            Assert.That(prep, Does.Contain("validate_phase30_summaries"));
            Assert.That(prep, Does.Contain("release-freeze-manifest-v0.18.0-build18.md"));
            Assert.That(prep, Does.Contain("generate-release-operator-handoff.sh"));
            Assert.That(prep, Does.Contain("prepare-release-tag.sh"));
            Assert.That(prep, Does.Contain("final-privacy-regression-audit.sh"));
            Assert.That(prep, Does.Contain("FINAL_PRIVACY_AUDIT_PASSED"));
            Assert.That(prep, Does.Contain("Readiness report overclaims readyForDistribution"));
            Assert.That(prep, Does.Contain("Final release record overclaims ready for distribution"));
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
                ReleaseDir = Path.Combine(Path.GetTempPath(), "tokenforge-phase30-" + Guid.NewGuid().ToString("N"));
                EvidenceDir = Path.Combine(ReleaseDir, "evidence");
                PackagePath = Path.Combine(ReleaseDir, "TokenForge-macOS-0.18.0-build18.zip");
                Directory.CreateDirectory(EvidenceDir);
            }

            public string ReleaseDir { get; }
            public string EvidenceDir { get; }
            public string PackagePath { get; }
            public string Sha256 { get; private set; }
            public string QaStatus => Path.Combine(EvidenceDir, "gatekeeper-qa-status.json");
            public string ReleaseOperatorHandoffMarkdown => Path.Combine(EvidenceDir, "release-operator-handoff.md");
            public string ReleaseOperatorHandoffJson => Path.Combine(EvidenceDir, "release-operator-handoff.json");
            public string FinalPrivacyAuditJson => Path.Combine(EvidenceDir, "final-privacy-regression-audit.json");

            public void WritePackage(string content, string entryName = "TokenForge.app/Contents/Resources/safe.txt")
            {
                Directory.CreateDirectory(ReleaseDir);
                using (var archive = ZipFile.Open(PackagePath, ZipArchiveMode.Create))
                {
                    var entry = archive.CreateEntry(entryName);
                    using (var writer = new StreamWriter(entry.Open()))
                    {
                        writer.Write(content);
                    }
                }

                Sha256 = Sha256File(PackagePath);
            }

            public void WriteSummaries(bool developerId, bool notarized, bool stapled, bool spctl, string shaOverride, string releaseStatus = null)
            {
                var signingStatus = developerId ? "SIGNING_SUCCEEDED" : "SIGNING_READY_BUT_IDENTITY_MISSING";
                var identity = developerId ? "developerId" : "adHoc";
                var notarizationStatus = notarized ? "NOTARIZATION_SUCCEEDED" : "NOTARIZATION_READY_BUT_CREDENTIALS_MISSING";
                var staplingStatus = stapled ? "STAPLE_SUCCEEDED" : "notAttempted";
                var spctlStatus = spctl ? "SPCTL_SUCCEEDED" : "notAttempted";
                var computedReleaseStatus = releaseStatus ?? (developerId && notarized && stapled && spctl ? "pendingQa" : "dryRunReady");

                File.WriteAllText(Path.Combine(ReleaseDir, "signing-status-summary.json"),
                    "{\"status\":\"" + signingStatus + "\",\"signingIdentityType\":\"" + identity + "\",\"signedWithDeveloperId\":" + developerId.ToString().ToLowerInvariant() + ",\"hardenedRuntime\":\"enabled\",\"releaseCandidateSha256\":\"" + shaOverride + "\"}");
                File.WriteAllText(Path.Combine(ReleaseDir, "notarization-status-summary.json"),
                    "{\"notarizationStatus\":\"" + notarizationStatus + "\",\"staplingStatus\":\"" + staplingStatus + "\",\"spctlStatus\":\"" + spctlStatus + "\",\"releaseCandidateSha256\":\"" + shaOverride + "\"}");
                File.WriteAllText(Path.Combine(ReleaseDir, "credentialed-release-summary.json"),
                    "{\"status\":\"" + (developerId ? "CRED_RELEASE_READY_FOR_QA" : "CRED_RELEASE_DRY_RUN_READY") + "\",\"releaseCandidateSha256\":\"" + shaOverride + "\"}");
                File.WriteAllText(Path.Combine(ReleaseDir, "credentialed-release-verification.json"),
                    "{\"status\":\"" + (developerId ? "CRED_RELEASE_VERIFIED_PENDING_QA" : "CRED_RELEASE_VERIFY_DRY_RUN_ONLY") + "\",\"qaStatus\":\"inProgress\",\"releaseCandidateSha256\":\"" + shaOverride + "\"}");
                File.WriteAllText(Path.Combine(ReleaseDir, "release-readiness-report.json"),
                    "{\"schemaVersion\":4,\"version\":\"0.18.0\",\"build\":\"18\",\"releaseStatus\":\"" + computedReleaseStatus + "\",\"releaseCandidateSha256\":\"" + shaOverride + "\",\"packageScanResult\":\"passed\",\"privacyScanResult\":\"passed\",\"signingSummary\":{\"signingIdentityType\":\"" + identity + "\",\"signedWithDeveloperId\":" + developerId.ToString().ToLowerInvariant() + "},\"notarizationSummary\":{\"notarizationStatus\":\"" + notarizationStatus + "\",\"staplingStatus\":\"" + staplingStatus + "\",\"spctlStatus\":\"" + spctlStatus + "\"},\"privacyAssertions\":{\"noLocalOnlyFilesInPackage\":true,\"noApprovedLocationPathsInEvidence\":true,\"noSecretsInEvidence\":true,\"noRawSyncStateInEvidence\":true,\"noBackgroundSyncAdded\":true},\"knownLimitations\":[],\"nextAction\":\"test\"}");
            }

            public void WriteFinalRecord(string distributionReadiness)
            {
                File.WriteAllText(Path.Combine(EvidenceDir, "final-release-record.json"),
                    "{\"schemaVersion\":1,\"distributionReadiness\":\"" + distributionReadiness + "\",\"signingIdentityType\":\"adHoc\",\"notarizationStatus\":\"NOTARIZATION_READY_BUT_CREDENTIALS_MISSING\",\"releaseCandidateSha256\":\"" + Sha256 + "\"}");
                File.WriteAllText(Path.Combine(EvidenceDir, "final-release-record.md"), "# Final release record\n\nDistribution readiness: " + distributionReadiness + "\n");
            }

            public void WriteQaInProgress()
            {
                var items = new List<string>();
                foreach (var item in new[] { "cleanInstall", "firstLaunch", "moveToApplications", "quarantineAssessment", "localStateLocation", "safeSyncPanelLaunch", "confirmationModalCancel", "confirmationModalTypedPhrase", "conflictAuditLocalOnly", "retryTombstoneLocalOnly", "approvedLocationsLocalOnly", "privacyScan", "packageScan" })
                {
                    items.Add("\"" + item + "\":{\"status\":\"notStarted\",\"note\":\"\"}");
                }

                File.WriteAllText(QaStatus, "{\"schemaVersion\":2,\"qaOverallStatus\":\"inProgress\",\"overallStatus\":\"inProgress\",\"items\":{" + string.Join(",", items) + "}}");
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
