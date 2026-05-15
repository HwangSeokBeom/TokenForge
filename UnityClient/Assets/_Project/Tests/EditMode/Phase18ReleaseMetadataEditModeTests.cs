#if UNITY_EDITOR
using System.IO;
using System.Linq;
using NUnit.Framework;
using TokenForge.Client.Privacy;
using TokenForge.Editor;

namespace TokenForge.Client.Tests
{
    public sealed class Phase18ReleaseMetadataEditModeTests
    {
        [Test]
        public void ReleaseMetadataValidator_PassesWithSafeMetadata()
        {
            var report = ReleaseMetadataValidator.Validate();

            Assert.IsTrue(report.IsValid, string.Join("; ", report.Errors));
            Assert.AreEqual(ReleaseMetadataValidator.ExpectedProductName, report.ProductName);
            Assert.AreEqual(ReleaseMetadataValidator.ExpectedCompanyName, report.CompanyName);
            Assert.AreEqual(ReleaseMetadataValidator.ExpectedBundleIdentifier, report.BundleIdentifier);
            Assert.AreEqual(ReleaseMetadataValidator.ExpectedAppVersion, report.AppVersion);
            Assert.AreEqual(ReleaseMetadataValidator.ExpectedBuildNumber, report.BuildNumber);
            Assert.AreEqual(ReleaseMetadataValidator.BootstrapScenePath, report.BootstrapScenePath);
            Assert.AreEqual(ReleaseMetadataValidator.EntitlementsRelativePath, report.EntitlementsPath);
        }

        [Test]
        public void ReleaseMetadataSummary_DoesNotContainSecretMarkersOrLocalPaths()
        {
            var report = ReleaseMetadataValidator.Validate();
            var safeSummary = string.Join("\n", new[]
            {
                report.ProductName,
                report.CompanyName,
                report.BundleIdentifier,
                report.AppVersion,
                report.BuildNumber,
                report.BootstrapScenePath,
                report.EntitlementsPath
            });

            Assert.IsFalse(new ForbiddenFieldDetector().ContainsSensitiveString(safeSummary), safeSummary);
            Assert.That(safeSummary.ToLowerInvariant(), Does.Not.Contain("/users/"));
            Assert.That(safeSummary, Does.Not.Contain("APPLE_ID"));
            Assert.That(safeSummary, Does.Not.Contain("APP_SPECIFIC_PASSWORD"));
            Assert.That(safeSummary, Does.Not.Contain("TOKENFORGE_MACOS_SIGN_IDENTITY"));
        }

        [Test]
        public void ValidateReleaseMetadataScript_ExistsAndUsesValidator()
        {
            var repoRoot = FindRepoRoot();
            var scriptPath = Path.Combine(repoRoot, "scripts/validate-release-metadata.sh");
            var script = File.ReadAllText(scriptPath);

            Assert.That(File.ReadLines(scriptPath).FirstOrDefault(), Is.EqualTo("#!/usr/bin/env bash"));
            Assert.That(script, Does.Contain("TokenForge.Editor.ReleaseMetadataValidator.ValidateForRelease"));
            Assert.That(script, Does.Contain("com.tokenforge.client"));
            Assert.That(script, Does.Not.Contain("APPLE_ID"));
            Assert.That(script, Does.Not.Contain("APP_SPECIFIC_PASSWORD"));
            Assert.That(script, Does.Not.Contain("TOKENFORGE_MACOS_SIGN_IDENTITY"));
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
