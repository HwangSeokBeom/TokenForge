#if UNITY_EDITOR
using System.IO;
using NUnit.Framework;

namespace TokenForge.Client.Tests
{
    public sealed class Phase19ReleaseArtifactPrivacyTests
    {
        [Test]
        public void ReleaseCandidatePackageScript_RejectsForbiddenLocalAndGeneratedArtifacts()
        {
            var repoRoot = FindRepoRoot();
            var script = File.ReadAllText(Path.Combine(repoRoot, "scripts/package-macos-release-candidate.sh"));

            foreach (var marker in new[]
            {
                "tokenforge-approved-locations",
                "artifacts/client-contract",
                "tokenforge-session",
                "tokenforge-token",
                ".corrupt",
                ".unsupported-schema",
                ".unsafe",
                ".env",
                "raw[ _-]?sync[ _-]?payload",
                "Tests/Fixtures/Persistence"
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
        public void CleanInstallSmoke_PerformsStructuralChecksWithoutCredentials()
        {
            var repoRoot = FindRepoRoot();
            var script = File.ReadAllText(Path.Combine(repoRoot, "scripts/smoke-clean-install-macos.sh"));

            Assert.That(script, Does.Contain("TokenForge.app"));
            Assert.That(script, Does.Contain("tokenforge-approved-locations.local.json"));
            Assert.That(script, Does.Contain("tokenforge-save.json"));
            Assert.That(script, Does.Contain("TOKENFORGE_CLEAN_INSTALL_LAUNCH"));
            Assert.That(script, Does.Contain("Manual checklist"));
            Assert.That(script, Does.Not.Contain("APPLE_ID"));
            Assert.That(script, Does.Not.Contain("APP_SPECIFIC_PASSWORD"));
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
