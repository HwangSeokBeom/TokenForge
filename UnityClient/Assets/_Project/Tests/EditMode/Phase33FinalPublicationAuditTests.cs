#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using NUnit.Framework;

namespace TokenForge.Client.Tests
{
    public sealed class Phase33FinalPublicationAuditTests
    {
        [Test]
        public void GithubAssetValidation_MapsDryRunAndReadyAndRejectsUnsafeInputs()
        {
            var repoRoot = FindRepoRoot();
            var verify = Path.Combine(repoRoot, "scripts/verify-final-distribution-readiness.sh");
            var draft = Path.Combine(repoRoot, "scripts/generate-github-release-draft.sh");
            var index = Path.Combine(repoRoot, "scripts/generate-release-bundle-index.sh");
            var assets = Path.Combine(repoRoot, "scripts/validate-github-release-assets.sh");

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("dry assets");
                fixture.WriteDryRun();
                RunScript(verify, fixture);
                RunScript(draft, fixture);
                RunScript(index, fixture);
                var result = RunScript(assets, fixture);
                Assert.AreEqual(0, result.ExitCode, result.Output);
                Assert.That(File.ReadAllText(fixture.AssetValidation), Does.Contain("GITHUB_RELEASE_ASSETS_DRY_RUN_ONLY"));
                AssertSafe(File.ReadAllText(fixture.AssetValidation));
            }

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("ready assets");
                fixture.WriteReady();
                RunScript(verify, fixture);
                RunScript(draft, fixture);
                RunScript(index, fixture);
                var result = RunScript(assets, fixture);
                Assert.AreEqual(0, result.ExitCode, result.Output);
                var json = File.ReadAllText(fixture.AssetValidation);
                Assert.That(json, Does.Contain("GITHUB_RELEASE_ASSETS_READY"));
                Assert.That(json, Does.Contain(fixture.Sha256));
                Assert.That(json, Does.Contain(fixture.EvidenceSha256));
                AssertSafe(json);
            }

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("sha mismatch");
                fixture.WriteReady();
                RunScript(verify, fixture);
                RunScript(draft, fixture);
                RunScript(index, fixture);
                File.WriteAllText(fixture.GithubDraftJson, File.ReadAllText(fixture.GithubDraftJson).Replace(fixture.Sha256, new string('a', 64)));
                var result = RunScript(assets, fixture);
                Assert.AreNotEqual(0, result.ExitCode);
                Assert.That(File.ReadAllText(fixture.AssetValidation), Does.Contain("SHA-256 mismatch"));
            }

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("missing zip");
                fixture.WriteReady();
                RunScript(verify, fixture);
                RunScript(draft, fixture);
                RunScript(index, fixture);
                File.Delete(fixture.PackagePath);
                var result = RunScript(assets, fixture);
                Assert.AreNotEqual(0, result.ExitCode);
                Assert.That(File.ReadAllText(fixture.AssetValidation), Does.Contain("Release candidate zip is missing"));
            }

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("unsafe evidence");
                fixture.WriteReady(includeUnsafeEvidence: true);
                RunScript(verify, fixture);
                RunScript(draft, fixture);
                RunScript(index, fixture);
                var result = RunScript(assets, fixture);
                Assert.AreNotEqual(0, result.ExitCode);
                Assert.That(File.ReadAllText(fixture.AssetValidation), Does.Contain("Evidence archive contains unsafe"));
            }

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("draft overclaim");
                fixture.WriteDryRun();
                RunScript(verify, fixture);
                RunScript(draft, fixture);
                RunScript(index, fixture);
                File.WriteAllText(fixture.GithubDraftJson, File.ReadAllText(fixture.GithubDraftJson).Replace("\"installationNotesIncludedForPublicDistribution\": false", "\"installationNotesIncludedForPublicDistribution\": true"));
                var result = RunScript(assets, fixture);
                Assert.AreNotEqual(0, result.ExitCode);
                Assert.That(File.ReadAllText(fixture.AssetValidation), Does.Contain("overclaims"));
            }
        }

        [Test]
        public void PublicationPlan_IsBlockedInDryRunAndReadyPlanIncludesSuggestedCommands()
        {
            var repoRoot = FindRepoRoot();
            var plan = Path.Combine(repoRoot, "scripts/generate-github-release-publication-plan.sh");

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("dry plan");
                fixture.WriteDryRun();
                GeneratePhase33Inputs(repoRoot, fixture);
                var result = RunScript(plan, fixture);
                Assert.AreEqual(0, result.ExitCode, result.Output);
                var markdown = File.ReadAllText(fixture.PublicationPlanMarkdown);
                Assert.That(markdown, Does.Contain("GITHUB_RELEASE_PUBLICATION_PLAN_BLOCKED"));
                Assert.That(markdown, Does.Contain("Final distribution readiness is not ready"));
                AssertSafe(markdown + File.ReadAllText(fixture.PublicationPlanJson));
            }

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("ready plan");
                fixture.WriteReady();
                GeneratePhase33Inputs(repoRoot, fixture);
                var result = RunScript(plan, fixture);
                Assert.AreEqual(0, result.ExitCode, result.Output);
                var markdown = File.ReadAllText(fixture.PublicationPlanMarkdown);
                Assert.That(markdown, Does.Contain("GITHUB_RELEASE_PUBLICATION_PLAN_READY"));
                Assert.That(markdown, Does.Contain("gh release create v0.18.0-build18"));
                Assert.That(markdown, Does.Contain("gh release upload v0.18.0-build18"));
                Assert.That(markdown, Does.Contain(fixture.Sha256));
                Assert.That(markdown, Does.Contain(fixture.EvidenceSha256));
                AssertSafe(markdown + File.ReadAllText(fixture.PublicationPlanJson));
            }
        }

        [Test]
        public void PublishScript_DryRunDoesNotPublishAndGuardedPublishBlocksWithoutFinalGates()
        {
            var repoRoot = FindRepoRoot();
            var publish = Path.Combine(repoRoot, "scripts/publish-github-release.sh");

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("dry publish");
                fixture.WriteDryRun();
                GeneratePhase33Inputs(repoRoot, fixture);
                var dryRun = RunScript(publish, fixture);
                Assert.AreEqual(0, dryRun.ExitCode, dryRun.Output);
                Assert.That(File.ReadAllText(fixture.PublicationSummary), Does.Contain("GITHUB_RELEASE_DRY_RUN_BLOCKED"));
                Assert.That(dryRun.Output, Does.Not.Contain("GITHUB_RELEASE_PUBLISHED"));

                var publishResult = RunScript(publish, fixture, "--publish --yes", skipPublishPrepCheck: true);
                Assert.AreNotEqual(0, publishResult.ExitCode);
                var summary = File.ReadAllText(fixture.PublicationSummary);
                Assert.That(summary, Does.Contain("GITHUB_RELEASE_BLOCKED"));
                Assert.That(summary, Does.Contain("Local release tag"));
                AssertSafe(summary);
            }

            var scriptText = File.ReadAllText(publish);
            Assert.That(scriptText, Does.Contain("command -v gh"));
            Assert.That(scriptText, Does.Contain("gh auth status"));
            Assert.That(scriptText, Does.Contain("GITHUB_RELEASE_ALREADY_EXISTS"));
            Assert.That(scriptText, Does.Contain("--update-existing"));
            Assert.That(scriptText, Does.Not.Contain("GITHUB_TOKEN"));
        }

        [Test]
        public void PostReleaseAudit_RecordsNotPublishedAndPublishedSummarySafely()
        {
            var repoRoot = FindRepoRoot();
            var auditScript = Path.Combine(repoRoot, "scripts/generate-post-release-audit.sh");

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("not published");
                fixture.WriteDryRun();
                GeneratePhase33Inputs(repoRoot, fixture);
                RunScript(Path.Combine(repoRoot, "scripts/publish-github-release.sh"), fixture);
                var result = RunScript(auditScript, fixture);
                Assert.AreEqual(0, result.ExitCode, result.Output);
                var json = File.ReadAllText(fixture.PostReleaseAuditJson);
                Assert.That(json, Does.Contain("\"published\": false"));
                Assert.That(json, Does.Contain("POST_RELEASE_AUDIT_NOT_PUBLISHED"));
                Assert.That(json, Does.Contain("run credentialed release"));
                AssertSafe(json + File.ReadAllText(fixture.PostReleaseAuditMarkdown));
            }

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("published summary");
                fixture.WriteReady();
                GeneratePhase33Inputs(repoRoot, fixture);
                File.WriteAllText(fixture.PublicationSummary, "{\"status\":\"GITHUB_RELEASE_PUBLISHED\",\"publishedAt\":\"2026-05-16T00:00:00Z\",\"releaseUrl\":\"https://github.com/example/releases/tag/v0.18.0-build18\"}");
                var result = RunScript(auditScript, fixture);
                Assert.AreEqual(0, result.ExitCode, result.Output);
                var json = File.ReadAllText(fixture.PostReleaseAuditJson);
                Assert.That(json, Does.Contain("\"published\": true"));
                Assert.That(json, Does.Contain("POST_RELEASE_AUDIT_VERIFY_RELEASE"));
                Assert.That(json, Does.Contain("verify published release"));
                AssertSafe(json);
            }
        }

        [Test]
        public void StatusDashboardV2_ShowsPhase33StatusAndNextActions()
        {
            var repoRoot = FindRepoRoot();
            var dashboardScript = Path.Combine(repoRoot, "scripts/print-release-status-dashboard.sh");

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("dashboard dry");
                fixture.WriteDryRun();
                GeneratePhase33Inputs(repoRoot, fixture);
                RunScript(dashboardScript, fixture);
                var json = File.ReadAllText(fixture.StatusDashboard);
                Assert.That(json, Does.Contain("tokenforge.releaseStatusDashboard.v2"));
                Assert.That(json, Does.Contain("githubAssetValidationStatus"));
                Assert.That(json, Does.Contain("githubPublicationStatus"));
                Assert.That(json, Does.Contain("localTagExists"));
                Assert.That(json, Does.Contain("run credentialed finalization"));
                AssertSafe(json);
            }

            using (var fixture = new TempReleaseFixture())
            {
                fixture.WritePackage("dashboard published");
                fixture.WriteReady();
                GeneratePhase33Inputs(repoRoot, fixture);
                File.WriteAllText(fixture.PublicationSummary, "{\"status\":\"GITHUB_RELEASE_PUBLISHED\",\"publishedAt\":\"2026-05-16T00:00:00Z\",\"releaseUrl\":\"https://github.com/example/releases/tag/v0.18.0-build18\"}");
                RunScript(Path.Combine(repoRoot, "scripts/generate-post-release-audit.sh"), fixture);
                RunScript(dashboardScript, fixture);
                Assert.That(File.ReadAllText(fixture.StatusDashboard), Does.Contain("verify release"));
            }
        }

        [Test]
        public void ReleasePrepCheck_ReferencesPhase33ScriptsAndSafetyGuards()
        {
            var repoRoot = FindRepoRoot();
            var prep = File.ReadAllText(Path.Combine(repoRoot, "scripts/release-prep-check.sh"));
            Assert.That(prep, Does.Contain("validate_phase33_summaries"));
            Assert.That(prep, Does.Contain("validate-github-release-assets.sh"));
            Assert.That(prep, Does.Contain("generate-github-release-publication-plan.sh"));
            Assert.That(prep, Does.Contain("publish-github-release.sh"));
            Assert.That(prep, Does.Contain("generate-post-release-audit.sh"));
            Assert.That(prep, Does.Contain("GitHub assets became ready without final distribution readiness"));
            Assert.That(prep, Does.Contain("Post-release audit claims publication without final distribution readiness"));
        }

        private static void GeneratePhase33Inputs(string repoRoot, TempReleaseFixture fixture)
        {
            RunScript(Path.Combine(repoRoot, "scripts/verify-final-distribution-readiness.sh"), fixture);
            RunScript(Path.Combine(repoRoot, "scripts/generate-github-release-draft.sh"), fixture);
            RunScript(Path.Combine(repoRoot, "scripts/generate-release-bundle-index.sh"), fixture);
            RunScript(Path.Combine(repoRoot, "scripts/validate-github-release-assets.sh"), fixture);
            RunScript(Path.Combine(repoRoot, "scripts/generate-github-release-publication-plan.sh"), fixture);
        }

        private static ScriptResult RunScript(string script, TempReleaseFixture fixture, string args = "", bool skipPublishPrepCheck = false)
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
            if (skipPublishPrepCheck)
            {
                startInfo.EnvironmentVariables["TOKENFORGE_RELEASE_PUBLISH_SKIP_PREP_CHECK"] = "true";
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
            Assert.That(text, Does.Not.Contain("GITHUB_TOKEN"));
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
                ReleaseDir = Path.Combine(Path.GetTempPath(), "tokenforge-phase33-" + Guid.NewGuid().ToString("N"));
                EvidenceDir = Path.Combine(ReleaseDir, "evidence");
                PackagePath = Path.Combine(ReleaseDir, "TokenForge-macOS-0.18.0-build18.zip");
                EvidenceArchivePath = Path.Combine(ReleaseDir, "TokenForge-0.18.0-build18-release-evidence.zip");
                ManifestPath = Path.Combine(ReleaseDir, "release-freeze-manifest.md");
                Directory.CreateDirectory(EvidenceDir);
            }

            public string ReleaseDir { get; }
            public string EvidenceDir { get; }
            public string PackagePath { get; }
            public string EvidenceArchivePath { get; }
            public string ManifestPath { get; }
            public string Sha256 { get; private set; }
            public string EvidenceSha256 { get; private set; }
            public string QaStatus => Path.Combine(EvidenceDir, "gatekeeper-qa-status.json");
            public string ReadinessReport => Path.Combine(ReleaseDir, "release-readiness-report.json");
            public string FinalDistribution => Path.Combine(ReleaseDir, "final-distribution-readiness.json");
            public string GithubDraftJson => Path.Combine(EvidenceDir, "github-release-draft.json");
            public string AssetValidation => Path.Combine(ReleaseDir, "github-release-assets-validation.json");
            public string PublicationPlanMarkdown => Path.Combine(EvidenceDir, "github-release-publication-plan.md");
            public string PublicationPlanJson => Path.Combine(EvidenceDir, "github-release-publication-plan.json");
            public string PublicationSummary => Path.Combine(ReleaseDir, "github-release-publication-summary.json");
            public string PostReleaseAuditMarkdown => Path.Combine(EvidenceDir, "post-release-audit.md");
            public string PostReleaseAuditJson => Path.Combine(EvidenceDir, "post-release-audit.json");
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

            public void WriteDryRun()
            {
                WriteQa(false);
                WriteManifest("FREEZE_MANIFEST_VALID_DRY_RUN_DRIFT_DOCUMENTED");
                WriteCommonSummaries(developerId: false, qaPassed: false, readinessStatus: "dryRunReady", finalizationStatus: "RELEASE_MACHINE_BLOCKED", verificationStatus: "CRED_RELEASE_VERIFY_DRY_RUN_ONLY", evidenceLocked: false);
                WriteFinalRecord("dry-run only");
            }

            public void WriteReady(bool includeUnsafeEvidence = false)
            {
                WriteQa(true);
                WriteManifest("FREEZE_MANIFEST_VALID_FINAL_MATCH");
                WriteEvidenceArchive(includeUnsafeEvidence);
                WriteCommonSummaries(developerId: true, qaPassed: true, readinessStatus: "readyForDistribution", finalizationStatus: "RELEASE_MACHINE_READY_FOR_TAG", verificationStatus: "CRED_RELEASE_VERIFIED_READY_FOR_DISTRIBUTION", evidenceLocked: true);
                WriteFinalRecord("ready for distribution");
            }

            private void WriteCommonSummaries(bool developerId, bool qaPassed, string readinessStatus, string finalizationStatus, string verificationStatus, bool evidenceLocked)
            {
                File.WriteAllText(Path.Combine(ReleaseDir, "signing-status-summary.json"), "{\"status\":\"" + (developerId ? "SIGNING_SUCCEEDED" : "SIGNING_READY_BUT_IDENTITY_MISSING") + "\",\"signingIdentityType\":\"" + (developerId ? "developerId" : "adHoc") + "\",\"signedWithDeveloperId\":" + developerId.ToString().ToLowerInvariant() + ",\"hardenedRuntime\":\"enabled\",\"releaseCandidateSha256\":\"" + Sha256 + "\"}");
                File.WriteAllText(Path.Combine(ReleaseDir, "notarization-status-summary.json"), "{\"notarizationStatus\":\"" + (developerId ? "NOTARIZATION_SUCCEEDED" : "NOTARIZATION_READY_BUT_CREDENTIALS_MISSING") + "\",\"staplingStatus\":\"" + (developerId ? "STAPLE_SUCCEEDED" : "notAttempted") + "\",\"spctlStatus\":\"" + (developerId ? "SPCTL_SUCCEEDED" : "notAttempted") + "\",\"releaseCandidateSha256\":\"" + Sha256 + "\"}");
                File.WriteAllText(Path.Combine(ReleaseDir, "credentialed-release-summary.json"), "{\"status\":\"" + (developerId ? "CRED_RELEASE_READY" : "CRED_RELEASE_DRY_RUN_READY") + "\",\"releaseCandidateSha256\":\"" + Sha256 + "\"}");
                File.WriteAllText(Path.Combine(ReleaseDir, "credentialed-release-verification.json"), "{\"status\":\"" + verificationStatus + "\",\"qaStatus\":\"" + (qaPassed ? "passed" : "inProgress") + "\",\"releaseCandidateSha256\":\"" + Sha256 + "\"}");
                File.WriteAllText(Path.Combine(ReleaseDir, "release-machine-finalization-summary.json"), "{\"status\":\"" + finalizationStatus + "\",\"releaseCandidateSha256\":\"" + Sha256 + "\"}");
                File.WriteAllText(Path.Combine(EvidenceDir, "final-privacy-regression-audit.json"), "{\"status\":\"FINAL_PRIVACY_AUDIT_PASSED\"}");
                File.WriteAllText(EvidenceLock, "{\"status\":\"" + (evidenceLocked ? "EVIDENCE_LOCK_READY" : "EVIDENCE_LOCK_DRY_RUN_ONLY") + "\",\"releaseCandidateSha256\":\"" + Sha256 + "\",\"evidenceArchivePath\":\"" + (evidenceLocked ? EvidenceArchivePath : "") + "\",\"evidenceArchiveSha256\":\"" + (evidenceLocked ? EvidenceSha256 : "") + "\"}");
                File.WriteAllText(ReadinessReport,
                    "{\"schemaVersion\":5,\"version\":\"0.18.0\",\"build\":\"18\",\"releaseStatus\":\"" + readinessStatus + "\",\"releaseCandidateSha256\":\"" + Sha256 + "\",\"packageScanResult\":\"passed\",\"privacyScanResult\":\"passed\",\"signingSummary\":{\"status\":\"" + (developerId ? "SIGNING_SUCCEEDED" : "SIGNING_READY_BUT_IDENTITY_MISSING") + "\",\"signingIdentityType\":\"" + (developerId ? "developerId" : "adHoc") + "\",\"signedWithDeveloperId\":" + developerId.ToString().ToLowerInvariant() + ",\"hardenedRuntime\":\"enabled\"},\"notarizationSummary\":{\"notarizationStatus\":\"" + (developerId ? "NOTARIZATION_SUCCEEDED" : "NOTARIZATION_READY_BUT_CREDENTIALS_MISSING") + "\",\"staplingStatus\":\"" + (developerId ? "STAPLE_SUCCEEDED" : "notAttempted") + "\",\"spctlStatus\":\"" + (developerId ? "SPCTL_SUCCEEDED" : "notAttempted") + "\"},\"credentialedVerificationSummary\":{\"status\":\"" + verificationStatus + "\"},\"evidenceLockSummary\":{\"status\":\"" + (evidenceLocked ? "EVIDENCE_LOCK_READY" : "EVIDENCE_LOCK_DRY_RUN_ONLY") + "\"},\"qaStatusSummary\":{\"overallStatus\":\"" + (qaPassed ? "passed" : "inProgress") + "\"},\"privacyAssertions\":{\"noLocalOnlyFilesInPackage\":true,\"noApprovedLocationPathsInEvidence\":true,\"noSecretsInEvidence\":true,\"noRawSyncStateInEvidence\":true,\"noBackgroundSyncAdded\":true},\"knownLimitations\":[],\"nextAction\":\"test\"}");
            }

            private void WriteFinalRecord(string readiness)
            {
                File.WriteAllText(Path.Combine(EvidenceDir, "final-release-record.md"), "# Final release record\n\nSHA-256: " + Sha256 + "\n");
                File.WriteAllText(Path.Combine(EvidenceDir, "final-release-record.json"), "{\"releaseCandidateSha256\":\"" + Sha256 + "\",\"distributionReadiness\":\"" + readiness + "\",\"notarizationStatus\":\"NOTARIZATION_SUCCEEDED\",\"signingIdentityType\":\"developerId\"}");
            }

            private void WriteManifest(string status)
            {
                File.WriteAllText(Path.Combine(EvidenceDir, "release-freeze-validation.json"), "{\"status\":\"" + status + "\",\"currentRegeneratedDryRunSha256\":\"" + Sha256 + "\",\"readinessSha256\":\"" + Sha256 + "\",\"credentialedFinalSha256\":\"" + Sha256 + "\"}");
                File.WriteAllText(ManifestPath, "# Release freeze manifest\n\n- Version: 0.18.0\n- Build: 18\n");
            }

            private void WriteQa(bool passed)
            {
                File.WriteAllText(QaStatus, "{\"schemaVersion\":2,\"qaOverallStatus\":\"" + (passed ? "passed" : "inProgress") + "\",\"overallStatus\":\"" + (passed ? "passed" : "inProgress") + "\",\"items\":{}}");
            }

            private void WriteEvidenceArchive(bool includeUnsafeEvidence)
            {
                using (var archive = ZipFile.Open(EvidenceArchivePath, ZipArchiveMode.Create))
                {
                    var entry = archive.CreateEntry(includeUnsafeEvidence ? "tokenforge-sync-local-state.local.json" : "release-evidence.md");
                    using (var writer = new StreamWriter(entry.Open()))
                    {
                        writer.Write(includeUnsafeEvidence ? "unsafe local-only state" : "safe release evidence");
                    }
                }

                EvidenceSha256 = Sha256File(EvidenceArchivePath);
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
