#if UNITY_EDITOR
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace TokenForge.Client.Tests
{
    public sealed class Phase17ReleaseSmokeScriptTests
    {
        [Test]
        public void ReleaseScriptsExistAndHandleMissingInputsSafely()
        {
            var repoRoot = FindRepoRoot();
            AssertScriptExists(repoRoot, "scripts/sign-macos-app.sh");
            AssertScriptExists(repoRoot, "scripts/verify-macos-signing.sh");
            AssertScriptExists(repoRoot, "scripts/package-macos-smoke.sh");
            AssertScriptExists(repoRoot, "scripts/notarize-macos-app.sh");
            AssertScriptExists(repoRoot, "scripts/staple-macos-app.sh");

            Assert.That(File.ReadAllText(Path.Combine(repoRoot, "scripts/sign-macos-app.sh")), Does.Contain("Set APP_PATH"));
            Assert.That(File.ReadAllText(Path.Combine(repoRoot, "scripts/notarize-macos-app.sh")), Does.Contain("TOKENFORGE_NOTARIZE"));
        }

        [Test]
        public void ReleaseScriptsDoNotEchoCredentialEnvironmentVariables()
        {
            var repoRoot = FindRepoRoot();
            foreach (var scriptPath in Directory.GetFiles(Path.Combine(repoRoot, "scripts"), "*.sh"))
            {
                var lines = File.ReadAllLines(scriptPath);
                foreach (var line in lines.Select(item => item.TrimStart()))
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

        [Test]
        public void PackageSmokeChecksForbiddenLocalArtifactsAndPackageOutputIsIgnored()
        {
            var repoRoot = FindRepoRoot();
            var packageScript = File.ReadAllText(Path.Combine(repoRoot, "scripts/package-macos-smoke.sh"));
            Assert.That(packageScript, Does.Contain("tokenforge-approved-locations.local.json"));
            Assert.That(packageScript, Does.Contain("artifacts/client-contract/"));
            Assert.That(packageScript, Does.Contain("tokenforge-session"));
            Assert.That(packageScript, Does.Contain("tokenforge-token"));
            Assert.That(packageScript, Does.Contain("Notarized: false"));

            var ignore = File.ReadAllText(Path.Combine(repoRoot, ".gitignore"));
            Assert.That(ignore, Does.Contain("*.zip"));
            Assert.That(ignore, Does.Contain("*.dmg"));
            Assert.That(ignore, Does.Contain("*.pkg"));
            Assert.That(ignore, Does.Contain("[Rr]eleases/"));
        }

        private static void AssertScriptExists(string repoRoot, string relativePath)
        {
            var path = Path.Combine(repoRoot, relativePath);
            Assert.IsTrue(File.Exists(path), relativePath);
            Assert.That(File.ReadLines(path).FirstOrDefault(), Is.EqualTo("#!/usr/bin/env bash"), relativePath);
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
