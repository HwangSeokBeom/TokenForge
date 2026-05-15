using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TokenForge.Client.Domain;
using TokenForge.Client.Persistence;
using TokenForge.Client.Sync;

namespace TokenForge.Client.Tests
{
    public sealed class Phase18ApprovedLocationMigrationTests
    {
        private const string RawApprovedPath = "/Users/alice/PrivateTokenForgeRepo";

        [Test]
        public void ApprovedLocationSettings_WritesExplicitSchemaVersionAndKeepsRawPathLocalOnly()
        {
            var repository = new ApprovedLocationSettingsRepository(CreateTempDirectory());

            var result = RunAsync(() => repository.AddOrUpdateAsync(new ApprovedLocationEntry
            {
                DisplayAlias = "Local repo",
                SourceType = ApprovedLocationSourceType.Git,
                LocalPath = RawApprovedPath,
                Enabled = true
            }, CancellationToken.None));
            var json = File.ReadAllText(repository.SettingsFilePath);
            var loaded = RunAsync(() => repository.LoadAsync(CancellationToken.None));

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            Assert.That(json, Does.Contain("\"schemaVersion\": 1"));
            Assert.That(json, Does.Contain("\"Version\": 1"));
            Assert.That(json, Does.Contain(RawApprovedPath));
            Assert.AreEqual(RawApprovedPath, loaded.Locations[0].LocalPath);
        }

        [Test]
        public void MissingSchemaVersion_MigratesFromLegacyVersionAndPreservesLocalOnlyPath()
        {
            var repository = new ApprovedLocationSettingsRepository(CreateTempDirectory());
            File.WriteAllText(repository.SettingsFilePath, "{\"Version\":1,\"Locations\":[{\"LocalId\":\"abc\",\"DisplayAlias\":\"Local repo\",\"SourceType\":0,\"LocalPath\":\"" + RawApprovedPath + "\",\"Enabled\":true}]}");

            var loaded = RunAsync(() => repository.LoadAsync(CancellationToken.None));

            Assert.AreEqual(ApprovedLocationSettings.CurrentSchemaVersion, loaded.SchemaVersion);
            Assert.AreEqual(ApprovedLocationSettings.CurrentVersion, loaded.Version);
            Assert.AreEqual(1, loaded.Locations.Count);
            Assert.AreEqual(RawApprovedPath, loaded.Locations[0].LocalPath);
        }

        [Test]
        public void UnknownFutureSchemaVersion_FailsSafelyAndPreservesLocalOnlyFile()
        {
            var repository = new ApprovedLocationSettingsRepository(CreateTempDirectory());
            File.WriteAllText(repository.SettingsFilePath, "{\"schemaVersion\":99,\"Version\":99,\"Locations\":[{\"LocalId\":\"abc\",\"DisplayAlias\":\"Local repo\",\"SourceType\":0,\"LocalPath\":\"" + RawApprovedPath + "\",\"Enabled\":true}]}");

            var loaded = RunAsync(() => repository.LoadAsync(CancellationToken.None));

            Assert.AreEqual(0, loaded.Locations.Count);
            Assert.IsTrue(File.Exists(repository.UnsupportedSchemaFilePath));
            Assert.That(File.ReadAllText(repository.SettingsFilePath), Does.Contain(RawApprovedPath));
        }

        [Test]
        public void ApprovedLocationMigration_DoesNotAffectSafeSessionsOrSafeSyncMapping()
        {
            var approvedRepository = new ApprovedLocationSettingsRepository(CreateTempDirectory());
            RunAsync(() => approvedRepository.AddOrUpdateAsync(new ApprovedLocationEntry
            {
                DisplayAlias = "Local repo",
                SourceType = ApprovedLocationSourceType.Git,
                LocalPath = RawApprovedPath,
                Enabled = true
            }, CancellationToken.None));

            var saveData = SaveData.CreateDefault();
            saveData.WorkSessionSummaries.Add(new AgentWorkSession
            {
                SessionId = "session-safe",
                SourceProvider = "GIT",
                GitChangeSummary = new GitChangeSummary
                {
                    ChangedFileCount = 2,
                    ProjectPathHash = "safehash"
                }
            });

            var payload = new SafeSyncMapper().ToPayload(saveData);

            Assert.That(File.ReadAllText(approvedRepository.SettingsFilePath), Does.Contain(RawApprovedPath));
            Assert.IsFalse(ObjectContainsString(saveData, RawApprovedPath));
            Assert.IsFalse(ObjectContainsString(payload, RawApprovedPath));
            Assert.IsFalse(ObjectContainsString(payload, "approvedLocation"));
        }

        private static bool ObjectContainsString(object value, string expected)
        {
            return ObjectContainsString(value, expected, new System.Collections.Generic.HashSet<object>());
        }

        private static bool ObjectContainsString(object value, string expected, System.Collections.Generic.HashSet<object> visited)
        {
            if (value == null || string.IsNullOrEmpty(expected))
            {
                return false;
            }

            if (value is string text)
            {
                return text.Contains(expected);
            }

            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || type == typeof(DateTimeOffset) || type == typeof(DateTime))
            {
                return false;
            }

            if (!visited.Add(value))
            {
                return false;
            }

            if (value is System.Collections.IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (ObjectContainsString(item, expected, visited))
                    {
                        return true;
                    }
                }

                return false;
            }

            foreach (var property in type.GetProperties())
            {
                if (property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                if (ObjectContainsString(property.GetValue(value), expected, visited))
                {
                    return true;
                }
            }

            return false;
        }

        private static string CreateTempDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName());
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static T RunAsync<T>(Func<Task<T>> operation)
        {
            return Task.Run(operation).GetAwaiter().GetResult();
        }
    }
}
