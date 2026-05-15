using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TokenForge.Client.Domain;
using TokenForge.Client.Persistence;

namespace TokenForge.Client.Tests
{
    public sealed class Phase18PersistenceCorruptionRecoveryTests
    {
        [Test]
        public void CorruptedSaveDataJson_ReturnsDefaultAndPreservesRecoveryCopy()
        {
            var repository = new SaveDataRepository(CreateTempDirectory());
            File.WriteAllText(repository.SaveFilePath, "{\"schemaVersion\":1,\"CharacterProfile\":");

            var loaded = RunAsync(() => repository.LoadAsync(CancellationToken.None));

            Assert.AreEqual(SaveData.CurrentSchemaVersion, loaded.SchemaVersion);
            Assert.IsTrue(File.Exists(repository.CorruptFilePath));
            Assert.That(File.ReadAllText(repository.SaveFilePath), Does.Contain("CharacterProfile"));
        }

        [Test]
        public void CorruptedApprovedLocationJson_ReturnsDefaultAndPreservesRecoveryCopy()
        {
            var repository = new ApprovedLocationSettingsRepository(CreateTempDirectory());
            File.WriteAllText(repository.SettingsFilePath, "{\"schemaVersion\":1,\"Locations\":");

            var loaded = RunAsync(() => repository.LoadAsync(CancellationToken.None));

            Assert.AreEqual(ApprovedLocationSettings.CurrentSchemaVersion, loaded.SchemaVersion);
            Assert.AreEqual(0, loaded.Locations.Count);
            Assert.IsTrue(File.Exists(repository.CorruptFilePath));
        }

        [Test]
        public void AtomicSave_WritesValidFinalFileAndCreatesBackupOnSecondWrite()
        {
            var repository = new SaveDataRepository(CreateTempDirectory());
            var first = SaveData.CreateDefault();
            first.CharacterProfile.TotalExp = 10;
            var second = SaveData.CreateDefault();
            second.CharacterProfile.TotalExp = 20;

            var firstResult = RunAsync(() => repository.SaveAsync(first, CancellationToken.None));
            var secondResult = RunAsync(() => repository.SaveAsync(second, CancellationToken.None));
            var finalJson = File.ReadAllText(repository.SaveFilePath);
            var backupJson = File.ReadAllText(repository.BackupFilePath);

            Assert.IsTrue(firstResult.IsSuccess, firstResult.ErrorMessage);
            Assert.IsTrue(secondResult.IsSuccess, secondResult.ErrorMessage);
            Assert.That(finalJson, Does.Contain("\"TotalExp\": 20"));
            Assert.That(backupJson, Does.Contain("\"TotalExp\": 10"));
            Assert.IsFalse(File.Exists(repository.SaveFilePath + ".tmp"));
        }

        [Test]
        public void FailedPrivacyGuardWrite_DoesNotDestroyPreviousValidSaveData()
        {
            var repository = new SaveDataRepository(CreateTempDirectory());
            var valid = SaveData.CreateDefault();
            valid.CharacterProfile.TotalExp = 77;
            var unsafeData = SaveData.CreateDefault();
            unsafeData.CharacterProfile.TotalExp = 99;
            unsafeData.WorkSessionSummaries.Add(new AgentWorkSession
            {
                SessionId = "unsafe-session",
                Warnings = { "prompt: do not write this payload" }
            });

            var firstResult = RunAsync(() => repository.SaveAsync(valid, CancellationToken.None));
            var secondResult = RunAsync(() => repository.SaveAsync(unsafeData, CancellationToken.None));
            var loaded = RunAsync(() => repository.LoadAsync(CancellationToken.None));

            Assert.IsTrue(firstResult.IsSuccess, firstResult.ErrorMessage);
            Assert.IsFalse(secondResult.IsSuccess);
            Assert.AreEqual(77, loaded.CharacterProfile.TotalExp);
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
