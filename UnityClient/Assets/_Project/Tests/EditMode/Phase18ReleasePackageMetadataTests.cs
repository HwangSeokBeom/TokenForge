#if UNITY_EDITOR
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace TokenForge.Client.Tests
{
    public sealed class Phase18ReleasePackageMetadataTests
    {
        [Test]
        public void PackageSmokeScript_ValidatesInfoPlistMetadataAndForbiddenArtifacts()
        {
            var repoRoot = FindRepoRoot();
            var script = File.ReadAllText(Path.Combine(repoRoot, "scripts/package-macos-smoke.sh"));

            Assert.That(script, Does.Contain("EXPECTED_BUNDLE_ID"));
            Assert.That(script, Does.Contain("com.tokenforge.client"));
            Assert.That(script, Does.Contain("EXPECTED_PRODUCT_NAME"));
            Assert.That(script, Does.Contain("TokenForge"));
            Assert.That(script, Does.Contain("EXPECTED_VERSION"));
            Assert.That(script, Does.Contain("0.18.0"));
            Assert.That(script, Does.Contain("EXPECTED_BUILD_NUMBER"));
            Assert.That(script, Does.Contain("CFBundleIdentifier"));
            Assert.That(script, Does.Contain("CFBundleExecutable"));
            Assert.That(script, Does.Contain("tokenforge-approved-locations.local.json"));
            Assert.That(script, Does.Contain("artifacts/client-contract/"));
            Assert.That(script, Does.Contain("tokenforge-session"));
            Assert.That(script, Does.Contain("tokenforge-token"));
        }

        [Test]
        public void ReleasePrepScript_RunsCredentialFreeReleaseChecks()
        {
            var repoRoot = FindRepoRoot();
            var scriptPath = Path.Combine(repoRoot, "scripts/release-prep-check.sh");
            var script = File.ReadAllText(scriptPath);

            Assert.That(File.ReadLines(scriptPath).FirstOrDefault(), Is.EqualTo("#!/usr/bin/env bash"));
            Assert.That(script, Does.Contain("validate-release-metadata.sh"));
            Assert.That(script, Does.Contain("-testPlatform editmode"));
            Assert.That(script, Does.Contain("-testPlatform playmode"));
            Assert.That(script, Does.Contain("SafeSyncContractBundleExportCommand.Export"));
            Assert.That(script, Does.Contain("build-macos-smoke.sh"));
            Assert.That(script, Does.Contain("package-macos-smoke.sh"));
            Assert.That(script, Does.Contain("Developer ID/notary credentials: not required"));
        }

        [Test]
        public void ReleaseScriptsAndGitIgnore_CoverRecoveryArtifactsWithoutIgnoringSourceAssets()
        {
            var repoRoot = FindRepoRoot();
            var ignore = File.ReadAllText(Path.Combine(repoRoot, ".gitignore"));

            Assert.That(ignore, Does.Contain("*.corrupt"));
            Assert.That(ignore, Does.Contain("*.unsupported-schema"));
            Assert.That(ignore, Does.Contain("*.unsafe"));
            Assert.That(ignore, Does.Contain("**/tokenforge-save.json"));
            Assert.That(ignore, Does.Not.Contain("Assets/_Project/Prefabs/UI/*.prefab"));
            Assert.That(ignore, Does.Not.Contain("Assets/_Project/Scenes/Bootstrap.unity"));
            Assert.That(ignore, Does.Not.Contain("*.meta"));
        }

        [Test]
        public void ReleaseScripts_DoNotEchoAppleCredentials()
        {
            var repoRoot = FindRepoRoot();
            foreach (var scriptPath in Directory.GetFiles(Path.Combine(repoRoot, "scripts"), "*.sh"))
            {
                foreach (var line in File.ReadAllLines(scriptPath).Select(item => item.TrimStart()))
                {
                    if (!line.StartsWith("echo ", System.StringComparison.Ordinal))
                    {
                        continue;
                    }

                    Assert.That(line, Does.Not.Contain("${APPLE_ID"), scriptPath);
                    Assert.That(line, Does.Not.Contain("$APPLE_ID"), scriptPath);
                    Assert.That(line, Does.Not.Contain("${APP_SPECIFIC_PASSWORD"), scriptPath);
                    Assert.That(line, Does.Not.Contain("$APP_SPECIFIC_PASSWORD"), scriptPath);
                    Assert.That(line, Does.Not.Contain("${TEAM_ID"), scriptPath);
                    Assert.That(line, Does.Not.Contain("$TEAM_ID"), scriptPath);
                    Assert.That(line, Does.Not.Contain("${TOKENFORGE_MACOS_SIGN_IDENTITY"), scriptPath);
                    Assert.That(line, Does.Not.Contain("$TOKENFORGE_MACOS_SIGN_IDENTITY"), scriptPath);
                    Assert.That(line, Does.Not.Contain("${NOTARYTOOL_KEYCHAIN_PROFILE"), scriptPath);
                    Assert.That(line, Does.Not.Contain("$NOTARYTOOL_KEYCHAIN_PROFILE"), scriptPath);
                }
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
