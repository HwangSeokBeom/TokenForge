#if UNITY_EDITOR
using System.IO;
using NUnit.Framework;

namespace TokenForge.Client.Tests
{
    public sealed class Phase20ReleaseArtifactPrivacyTests
    {
        [Test]
        public void ReleaseCandidatePackageScript_RejectsPlaceholderGeneratedAndSensitiveArtifacts()
        {
            var repoRoot = FindRepoRoot();
            var script = File.ReadAllText(Path.Combine(repoRoot, "scripts/package-macos-release-candidate.sh"));

            foreach (var marker in new[]
            {
                "TokenForgePlaceholderIcon\\.png",
                "tokenforge-approved-locations\\.local\\.json",
                "artifacts/client-contract/",
                "tokenforge-session",
                "tokenforge-token",
                "tokenforge-sync-retry-queue\\.local\\.json",
                "tokenforge-sync-tombstones\\.local\\.json",
                "tokenforge-sync-conflicts\\.local\\.json",
                "tokenforge-sync-conflict-audit\\.local\\.json",
                "tokenforge-sync-local-state\\.local\\.json",
                "retry[ _-]?backup",
                "recovery[ _-]?sync[ _-]?state",
                "\\.corrupt$",
                "\\.unsupported-schema$",
                "\\.unsafe$",
                "\\.log$",
                "\\.env$",
                "APPLE_ID",
                "APP_SPECIFIC_PASSWORD",
                "NOTARYTOOL_KEYCHAIN_PROFILE",
                "credentials",
                "raw[ _-]?sync[ _-]?payload",
                "Tests/Fixtures/Persistence",
                "TokenForge\\.Client\\.Tests",
                "\\.sh$",
                "localhost:3000"
            })
            {
                Assert.That(script, Does.Contain(marker), marker);
            }

            Assert.That(script, Does.Contain("CFBundleIdentifier"));
            Assert.That(script, Does.Contain("CFBundleShortVersionString"));
            Assert.That(script, Does.Contain("CFBundleVersion"));
            Assert.That(script, Does.Contain("placeholder bundle metadata"));
        }

        [Test]
        public void SmokeScripts_DocumentManualReleaseQaWithoutCredentials()
        {
            var repoRoot = FindRepoRoot();
            var cleanInstall = File.ReadAllText(Path.Combine(repoRoot, "scripts/smoke-clean-install-macos.sh"));
            var migration = File.ReadAllText(Path.Combine(repoRoot, "scripts/smoke-update-migration-macos.sh"));

            Assert.That(cleanInstall, Does.Contain("verify Gatekeeper behavior"));
            Assert.That(cleanInstall, Does.Contain("no login starts automatically"));
            Assert.That(cleanInstall, Does.Contain("no sync starts automatically"));
            Assert.That(cleanInstall, Does.Contain("approved locations are empty"));
            Assert.That(cleanInstall, Does.Contain("no sensitive logs"));
            Assert.That(migration, Does.Contain("legacy v0/missing-schema"));
            Assert.That(migration, Does.Contain("approved-location paths remain local-only"));
            Assert.That(migration, Does.Contain("no sync starts automatically"));
            Assert.That(migration, Does.Not.Contain("APPLE_ID"));
            Assert.That(migration, Does.Not.Contain("APP_SPECIFIC_PASSWORD"));
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
