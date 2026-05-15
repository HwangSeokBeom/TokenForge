using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TokenForge.Client.Sync;

namespace TokenForge.Client.Tests
{
    public sealed class SafeSyncContractBundlePhase9Tests
    {
        private static readonly string[] RequiredForbiddenFields =
        {
            "path",
            "filePath",
            "filename",
            "fileName",
            "repoName",
            "repositoryName",
            "branchName",
            "command",
            "prompt",
            "response",
            "rawLog",
            "source",
            "sourceText",
            "code",
            "snippet",
            "username",
            "token",
            "secret",
            "apiKey",
            "password",
            "approvedLocation",
            "approvedLocations",
            "localPath",
            "localOnlyPath"
        };

        private static readonly HashSet<string> ForbiddenKeys = new HashSet<string>(
            SafeSyncContractBundleFixtures.CreateRawFieldsNeverSynced(),
            StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> AllowedValidFixtureKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "schemaVersion",
            "clientSyncId",
            "requestId",
            "sessions",
            "clientSessionId",
            "sourceProvider",
            "dayBucket",
            "timeBucket",
            "confidence",
            "warningIds",
            "analyzerVersion",
            "parserVersion",
            "hashedRepositoryId",
            "changeCountBucket",
            "lineCountBucket",
            "commitCountBucket",
            "sessionCountBucket",
            "interactionCountBucket",
            "activityCategory",
            "durationBucket",
            "categoryBuckets",
            "languageBuckets",
            "toolBuckets",
            "key",
            "countBucket"
        };

        [Test]
        public void BundleStructure_MatchesServerPhase4Contract()
        {
            var bundle = SafeSyncContractBundleFixtures.CreateBundle();

            Assert.AreEqual(1, bundle.BundleVersion);
            Assert.AreEqual("TokenForgeUnityClient", bundle.ClientName);
            Assert.AreEqual(1, bundle.ClientSchemaVersion);
            Assert.Contains(1, bundle.SupportedSchemaVersions);
            CollectionAssert.AreEquivalent(
                new[] { "MANUAL", "GIT", "CLAUDE", "CODEX", "UNKNOWN_AGENT" },
                bundle.SupportedSourceProviders);
            Assert.AreEqual("WHOLE_REQUEST_REJECTED_BY_DTO_VALIDATION", bundle.MixedBehavior);
        }

        [Test]
        public void ValidFixture_IsGeneratedThroughSafeSyncMapperAndCoversExpectedProviders()
        {
            var saveData = SafeSyncContractBundleFixtures.CreateDeterministicSafeSaveData();
            var mapperOutput = new SafeSyncMapper().ToActivitySessionsContractRequest(saveData, "unity-sync-v1-001");
            var bundle = SafeSyncContractBundleFixtures.CreateBundle();

            Assert.AreEqual(
                SafeSyncContractBundleExporter.ToJson(mapperOutput),
                SafeSyncContractBundleExporter.ToJson(bundle.Fixtures.Valid));
            CollectionAssert.AreEquivalent(
                new[] { "MANUAL", "GIT", "CLAUDE", "CODEX", "UNKNOWN_AGENT" },
                bundle.Fixtures.Valid.Sessions.Select(session => session.SourceProvider).ToArray());
        }

        [Test]
        public void ValidFixture_DoesNotContainForbiddenKeysOrRawLookingValues()
        {
            var valid = SafeSyncContractBundleExporter.ToJson(SafeSyncContractBundleFixtures.CreateBundle().Fixtures.Valid);

            CollectionAssert.IsEmpty(CollectForbiddenKeys(valid));
            CollectionAssert.IsEmpty(CollectUnexpectedValidFixtureKeys(valid));
            AssertNoRawLookingValues(valid);
        }

        [Test]
        public void UnsafeFixture_IsMarkedAndSeparatedFromProductionValidFixture()
        {
            var bundle = SafeSyncContractBundleFixtures.CreateBundle();
            var unsafeFixture = SafeSyncContractBundleExporter.ToJson(bundle.Fixtures.Unsafe);
            var validFixture = SafeSyncContractBundleExporter.ToJson(bundle.Fixtures.Valid);

            Assert.AreEqual("UNSAFE_FOR_SERVER_REJECTION_ONLY", bundle.Fixtures.Unsafe.FixtureSafety);
            CollectionAssert.IsNotEmpty(CollectForbiddenKeys(unsafeFixture));
            CollectionAssert.IsEmpty(CollectForbiddenKeys(validFixture));
        }

        [Test]
        public void PrivacyExpectations_ContainServerForbiddenFieldSet()
        {
            var expected = new HashSet<string>(
                SafeSyncContractBundleFixtures.CreateBundle().PrivacyExpectations.RawFieldsNeverSynced,
                StringComparer.OrdinalIgnoreCase);

            Assert.IsTrue(SafeSyncContractBundleFixtures.CreateBundle().PrivacyExpectations.ApprovedLocationsAreLocalOnly);
            foreach (var field in RequiredForbiddenFields)
            {
                Assert.IsTrue(expected.Contains(field), field);
            }
        }

        [Test]
        public void Exporter_WritesParsableBundleThatMatchesInMemoryStructure()
        {
            var outputPath = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName(), "unity-safe-sync-contract-v1.bundle.json");
            var result = SafeSyncContractBundleExporter.Export(outputPath, SafeSyncContractBundleFixtures.StableGeneratedAt);
            var json = File.ReadAllText(outputPath);
            var parsed = SafeSyncContractBundleExporter.FromJson(json);
            var expected = SafeSyncContractBundleFixtures.CreateBundle(SafeSyncContractBundleFixtures.StableGeneratedAt);

            Assert.AreEqual(outputPath, result.OutputPath);
            Assert.AreEqual(1, result.BundleVersion);
            Assert.AreEqual(1, result.SchemaVersion);
            Assert.AreEqual(5, result.ValidSessionCount);
            Assert.AreEqual(1, parsed.BundleVersion);
            Assert.AreEqual(SafeSyncContractBundleExporter.ToJson(expected), json);
            var parsedValidJson = SafeSyncContractBundleExporter.ToJson(parsed.Fixtures.Valid);
            CollectionAssert.IsEmpty(CollectForbiddenKeys(parsedValidJson));
            AssertNoRawLookingValues(parsedValidJson);
        }

        private static List<string> CollectForbiddenKeys(string json)
        {
            var found = new List<string>();
            foreach (Match match in Regex.Matches(json, "\"(?<key>[A-Za-z0-9_]+)\"\\s*:"))
            {
                var key = match.Groups["key"].Value;
                if (ForbiddenKeys.Contains(key))
                {
                    found.Add(key);
                }
            }

            return found;
        }

        private static List<string> CollectUnexpectedValidFixtureKeys(string json)
        {
            var found = new List<string>();
            foreach (Match match in Regex.Matches(json, "\"(?<key>[A-Za-z0-9_]+)\"\\s*:"))
            {
                var key = match.Groups["key"].Value;
                if (!AllowedValidFixtureKeys.Contains(key))
                {
                    found.Add(key);
                }
            }

            return found;
        }

        private static void AssertNoRawLookingValues(string json)
        {
            Assert.IsFalse(Regex.IsMatch(json, @"/Users/|/home/|[A-Za-z]:\\|sk-[A-Za-z0-9_-]{20,}|-----BEGIN [A-Z ]*PRIVATE KEY-----"));
            Assert.IsFalse(json.Contains("git status"));
            Assert.IsFalse(json.Contains("private raw log"));
            Assert.IsFalse(json.Contains("CustomerSecret"));
            Assert.IsFalse(json.Contains("approvedLocations"));
        }
    }
}
