using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TokenForge.Client.Domain;
using TokenForge.Client.Persistence;
using TokenForge.Client.Privacy;
using TokenForge.Client.Sync;

namespace TokenForge.Client.Tests
{
    public sealed class Phase19MigrationSmokeTests
    {
        private const string FixtureRoot = "Assets/_Project/Tests/Fixtures/Persistence";

        [Test]
        public void PackagedUpdateSmoke_LegacySafeSaveMigratesToSchemaV1AndRemainsAggregateOnly()
        {
            var directory = CreateTempDirectory();
            var repository = new SaveDataRepository(directory);
            File.Copy(FixturePath("legacy-save-missing-schema.json"), repository.SaveFilePath);

            var loaded = RunAsync(() => repository.LoadAsync(CancellationToken.None));
            var saveResult = RunAsync(() => repository.SaveAsync(loaded, CancellationToken.None));
            var json = File.ReadAllText(repository.SaveFilePath);
            var payload = new SafeSyncMapper().ToPayload(loaded);

            Assert.IsTrue(saveResult.IsSuccess, saveResult.ErrorMessage);
            Assert.AreEqual(SaveData.CurrentSchemaVersion, loaded.SchemaVersion);
            Assert.AreEqual(SaveData.CurrentSaveVersion, loaded.SaveVersion);
            Assert.AreEqual(75, loaded.CharacterProfile.TotalExp);
            Assert.That(json, Does.Contain("\"schemaVersion\": 1"));
            Assert.That(json, Does.Contain("\"SaveVersion\": 1"));
            Assert.IsTrue(new PrivacySanitizer().ValidateNoForbiddenFields(json).IsSuccess);
            Assert.IsTrue(new SyncPayloadSanitizer().ValidatePayload(payload).IsSafe);
        }

        [Test]
        public void PackagedUpdateSmoke_ApprovedLocationsMigrateLocallyAndAreNotSynced()
        {
            var directory = CreateTempDirectory();
            var repository = new ApprovedLocationSettingsRepository(directory);
            File.Copy(FixturePath("legacy-approved-locations-missing-schema.json"), repository.SettingsFilePath);

            var loaded = RunAsync(() => repository.LoadAsync(CancellationToken.None));
            var saveData = SaveData.CreateDefault();
            saveData.WorkSessionSummaries.Add(new AgentWorkSession
            {
                SessionId = "safe-session",
                SourceProvider = "GIT",
                GitChangeSummary = new GitChangeSummary
                {
                    ChangedFileCount = 1,
                    ProjectPathHash = "safehash"
                }
            });
            var payload = new SafeSyncMapper().ToPayload(saveData);

            Assert.AreEqual(ApprovedLocationSettings.CurrentSchemaVersion, loaded.SchemaVersion);
            Assert.AreEqual(ApprovedLocationSettings.CurrentVersion, loaded.Version);
            Assert.AreEqual(1, loaded.Locations.Count);
            Assert.That(loaded.Locations[0].LocalPath, Does.Contain("/tmp/tokenforge-fixture"));
            Assert.IsFalse(ObjectContainsString(saveData, loaded.Locations[0].LocalPath));
            Assert.IsFalse(ObjectContainsString(payload, loaded.Locations[0].LocalPath));
            Assert.IsFalse(ObjectContainsString(payload, "approvedLocation"));
        }

        [Test]
        public void PackagedUpdateSmoke_FutureAndCorruptFilesFailSafelyWithRecoveryCopies()
        {
            var futureSaveDirectory = CreateTempDirectory();
            var futureSaveRepository = new SaveDataRepository(futureSaveDirectory);
            File.Copy(FixturePath("future-save-schema.json"), futureSaveRepository.SaveFilePath);

            var futureSave = RunAsync(() => futureSaveRepository.LoadAsync(CancellationToken.None));
            Assert.AreEqual(SaveData.CurrentSchemaVersion, futureSave.SchemaVersion);
            Assert.AreNotEqual(9999, futureSave.CharacterProfile.TotalExp);
            Assert.IsTrue(File.Exists(futureSaveRepository.UnsupportedSchemaFilePath));

            var corruptSaveDirectory = CreateTempDirectory();
            var corruptSaveRepository = new SaveDataRepository(corruptSaveDirectory);
            File.Copy(FixturePath("corrupt-save.json"), corruptSaveRepository.SaveFilePath);

            var corruptSave = RunAsync(() => corruptSaveRepository.LoadAsync(CancellationToken.None));
            Assert.AreEqual(SaveData.CurrentSchemaVersion, corruptSave.SchemaVersion);
            Assert.IsTrue(File.Exists(corruptSaveRepository.CorruptFilePath));

            var futureApprovedDirectory = CreateTempDirectory();
            var approvedRepository = new ApprovedLocationSettingsRepository(futureApprovedDirectory);
            File.Copy(FixturePath("future-approved-locations-schema.json"), approvedRepository.SettingsFilePath);

            var approved = RunAsync(() => approvedRepository.LoadAsync(CancellationToken.None));
            Assert.AreEqual(0, approved.Locations.Count);
            Assert.IsTrue(File.Exists(approvedRepository.UnsupportedSchemaFilePath));
        }

        private static string FixturePath(string fileName)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), FixtureRoot, fileName);
        }

        private static string CreateTempDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TokenForgePhase19MigrationSmoke", Path.GetRandomFileName());
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static T RunAsync<T>(Func<Task<T>> operation)
        {
            return Task.Run(operation).GetAwaiter().GetResult();
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
    }
}
