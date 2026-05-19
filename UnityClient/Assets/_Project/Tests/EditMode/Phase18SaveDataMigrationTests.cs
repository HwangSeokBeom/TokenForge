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
    public sealed class Phase18SaveDataMigrationTests
    {
        [Test]
        public void CurrentSaveData_WritesExplicitSchemaVersion()
        {
            var repository = new SaveDataRepository(CreateTempDirectory());
            var saveData = SaveData.CreateDefault();
            saveData.CharacterProfile.TotalExp = 120;

            var saveResult = RunAsync(() => repository.SaveAsync(saveData, CancellationToken.None));
            var loaded = RunAsync(() => repository.LoadAsync(CancellationToken.None));
            var json = File.ReadAllText(repository.SaveFilePath);

            Assert.IsTrue(saveResult.IsSuccess, saveResult.ErrorMessage);
            Assert.That(json, Does.Contain("\"schemaVersion\": 1"));
            Assert.That(json, Does.Contain("\"SaveVersion\": 1"));
            Assert.AreEqual(SaveData.CurrentSchemaVersion, loaded.SchemaVersion);
            Assert.AreEqual(120, loaded.CharacterProfile.TotalExp);
        }

        [Test]
        public void MissingSchemaVersion_MigratesFromLegacySaveVersion()
        {
            var repository = new SaveDataRepository(CreateTempDirectory());
            File.WriteAllText(repository.SaveFilePath, "{\"SaveVersion\":1,\"CharacterProfile\":{\"TotalExp\":42},\"WorkSessionSummaries\":[]}");

            var loaded = RunAsync(() => repository.LoadAsync(CancellationToken.None));

            Assert.AreEqual(SaveData.CurrentSchemaVersion, loaded.SchemaVersion);
            Assert.AreEqual(SaveData.CurrentSaveVersion, loaded.SaveVersion);
            Assert.AreEqual(42, loaded.CharacterProfile.TotalExp);
            Assert.AreEqual(CompanionStage.Egg, loaded.CompanionState.Stage);
            Assert.IsNotNull(loaded.DesktopCompanionSettings);
            Assert.IsFalse(loaded.DesktopCompanionSettings.IsDesktopCompanionEnabled);
            Assert.IsTrue(loaded.DesktopCompanionSettings.IsClickThroughEnabled);
            Assert.IsFalse(File.Exists(repository.CorruptFilePath));
        }

        [Test]
        public void OlderKnownSchemaVersion_MigratesToCurrentShape()
        {
            var repository = new SaveDataRepository(CreateTempDirectory());
            File.WriteAllText(repository.SaveFilePath, "{\"schemaVersion\":0,\"SaveVersion\":0,\"CharacterProfile\":{\"Level\":2},\"WorkSessionSummaries\":null}");

            var loaded = RunAsync(() => repository.LoadAsync(CancellationToken.None));

            Assert.AreEqual(SaveData.CurrentSchemaVersion, loaded.SchemaVersion);
            Assert.AreEqual(SaveData.CurrentSaveVersion, loaded.SaveVersion);
            Assert.AreEqual(2, loaded.CharacterProfile.Level);
            Assert.IsNotNull(loaded.WorkSessionSummaries);
            Assert.AreEqual(CompanionStage.Egg, loaded.CompanionState.Stage);
            Assert.IsFalse(loaded.DesktopCompanionSettings.IsDesktopCompanionEnabled);
        }

        [Test]
        public void UnknownFutureSchemaVersion_FailsSafelyAndPreservesOriginalFile()
        {
            var repository = new SaveDataRepository(CreateTempDirectory());
            File.WriteAllText(repository.SaveFilePath, "{\"schemaVersion\":99,\"SaveVersion\":99,\"CharacterProfile\":{\"TotalExp\":999}}");

            var loaded = RunAsync(() => repository.LoadAsync(CancellationToken.None));
            var original = File.ReadAllText(repository.SaveFilePath);

            Assert.AreEqual(SaveData.CurrentSchemaVersion, loaded.SchemaVersion);
            Assert.AreNotEqual(999, loaded.CharacterProfile.TotalExp);
            Assert.That(original, Does.Contain("\"schemaVersion\":99"));
            Assert.IsTrue(File.Exists(repository.UnsupportedSchemaFilePath));
        }

        [Test]
        public void MigratedSafeSaveData_ContainsNoForbiddenFieldsOrApprovedLocations()
        {
            var repository = new SaveDataRepository(CreateTempDirectory());
            var saveData = SaveData.CreateDefault();
            saveData.WorkSessionSummaries.Add(new AgentWorkSession
            {
                SessionId = "session-safe",
                SourceProvider = "CODEX",
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                EndedAt = DateTimeOffset.UtcNow,
                TokenUsageBucket = TokenUsageBucket.Small,
                GitChangeSummary = new GitChangeSummary
                {
                    ChangedFileCount = 3,
                    ProjectPathHash = "projecthash"
                }
            });

            var saveResult = RunAsync(() => repository.SaveAsync(saveData, CancellationToken.None));
            var json = File.ReadAllText(repository.SaveFilePath);
            var payload = new SafeSyncMapper().ToPayload(RunAsync(() => repository.LoadAsync(CancellationToken.None)));

            Assert.IsTrue(saveResult.IsSuccess, saveResult.ErrorMessage);
            Assert.IsTrue(new PrivacySanitizer().ValidateNoForbiddenFields(json).IsSuccess);
            Assert.IsTrue(new SyncPayloadSanitizer().ValidatePayload(payload).IsSafe);
            AssertNoForbiddenKeys(json);
        }

        private static void AssertNoForbiddenKeys(string json)
        {
            var forbiddenKeys = new[]
            {
                "rawPath",
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

            foreach (var key in forbiddenKeys)
            {
                Assert.That(json, Does.Not.Contain("\"" + key + "\""), key);
            }
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
