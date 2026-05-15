#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace TokenForge.Client.Tests
{
    public sealed class Phase19ReleaseCandidateScriptTests
    {
        [Test]
        public void ReleaseCandidatePackageScript_SupportsExpectedCredentialFreeAndDeveloperIdPaths()
        {
            var repoRoot = FindRepoRoot();
            var scriptPath = Path.Combine(repoRoot, "scripts/package-macos-release-candidate.sh");
            var script = File.ReadAllText(scriptPath);

            Assert.That(File.ReadLines(scriptPath).FirstOrDefault(), Is.EqualTo("#!/usr/bin/env bash"));
            Assert.That(script, Does.Contain("TOKENFORGE_RELEASE_SIGN_MODE"));
            Assert.That(script, Does.Contain("adhoc"));
            Assert.That(script, Does.Contain("developer-id"));
            Assert.That(script, Does.Contain("TOKENFORGE_RELEASE_NOTARIZE"));
            Assert.That(script, Does.Contain("TokenForge-macOS-${EXPECTED_VERSION}-build${EXPECTED_BUILD_NUMBER}.zip"));
            Assert.That(script, Does.Contain("TokenForge-macOS-${EXPECTED_VERSION}-build${EXPECTED_BUILD_NUMBER}-notarized.zip"));
            Assert.That(script, Does.Contain("validate-release-metadata.sh"));
            Assert.That(script, Does.Contain("sign-macos-app.sh"));
            Assert.That(script, Does.Contain("verify-macos-signing.sh"));
            Assert.That(script, Does.Contain("staple-macos-app.sh"));
            Assert.That(script, Does.Contain("Notarized release candidates require Developer ID signing."));
            Assert.That(script, Does.Contain("Privacy scan: passed"));
        }

        [Test]
        public void SigningAndNotarizationScripts_AreOptInAndDoNotEchoSecretEnvironmentValues()
        {
            var repoRoot = FindRepoRoot();
            var scripts = new[]
            {
                "scripts/sign-macos-app.sh",
                "scripts/notarize-macos-app.sh",
                "scripts/staple-macos-app.sh",
                "scripts/verify-macos-signing.sh",
                "scripts/package-macos-release-candidate.sh"
            };

            foreach (var relativePath in scripts)
            {
                var scriptPath = Path.Combine(repoRoot, relativePath);
                var script = File.ReadAllText(scriptPath);
                Assert.That(script, Does.Contain("set -euo pipefail"), relativePath);
                Assert.That(script, Does.Not.Contain("echo \"${APPLE_ID"), relativePath);
                Assert.That(script, Does.Not.Contain("echo \"${APP_SPECIFIC_PASSWORD"), relativePath);
                Assert.That(script, Does.Not.Contain("echo \"${TEAM_ID"), relativePath);
                Assert.That(script, Does.Not.Contain("echo \"${TOKENFORGE_MACOS_SIGN_IDENTITY"), relativePath);
                Assert.That(script, Does.Not.Contain("echo \"${NOTARYTOOL_KEYCHAIN_PROFILE"), relativePath);
            }

            var notarize = File.ReadAllText(Path.Combine(repoRoot, "scripts/notarize-macos-app.sh"));
            Assert.That(notarize, Does.Contain("TOKENFORGE_RELEASE_NOTARIZE"));
            Assert.That(notarize, Does.Contain("notarization is opt-in"));
            Assert.That(notarize, Does.Contain("notarytool submit"));
            Assert.That(notarize, Does.Contain("--output-format json"));
            Assert.That(notarize, Does.Contain("Status: ${notary_status}"));
            Assert.That(notarize, Does.Contain("Timed out"));
        }

        [Test]
        public void ReleasePrepScript_IncludesReleaseCandidateSmokeCommands()
        {
            var repoRoot = FindRepoRoot();
            var script = File.ReadAllText(Path.Combine(repoRoot, "scripts/release-prep-check.sh"));

            Assert.That(script, Does.Contain("package-macos-release-candidate.sh"));
            Assert.That(script, Does.Contain("smoke-clean-install-macos.sh"));
            Assert.That(script, Does.Contain("smoke-update-migration-macos.sh"));
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
