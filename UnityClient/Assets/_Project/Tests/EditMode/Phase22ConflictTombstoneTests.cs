using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TokenForge.Client.Sync;

namespace TokenForge.Client.Tests
{
    public sealed class Phase22ConflictTombstoneTests
    {
        [Test]
        public void ConflictAndTombstoneRepositoriesStoreSafeSummariesOnly()
        {
            var directory = TempDirectory();
            var conflictRepository = new SafeSyncConflictRepository(directory);
            var tombstoneRepository = new SafeSyncTombstoneRepository(directory);

            RunAsync(() => conflictRepository.SaveAsync(new SafeSyncConflictState
            {
                Conflicts = new List<SafeSyncConflict>
                {
                    new SafeSyncConflict
                    {
                        ClientSessionId = "client-session-1",
                        ServerSessionId = "server-session-1",
                        ConflictType = SafeSyncConflictType.RemoteDifferent,
                        SafeErrorCode = SafeSyncApiError.ConflictDetected,
                        SafeLocalSummary = new SafeSyncSessionSafeSummary { ClientSessionId = "client-session-1", DayBucket = "2026-05-15", SourceProvider = "CODEX" },
                        SafeRemoteSummary = new SafeSyncSessionSafeSummary { ClientSessionId = "client-session-1", DayBucket = "2026-05-15", SourceProvider = "CODEX" }
                    }
                }
            }, CancellationToken.None));
            RunAsync(() => tombstoneRepository.SaveAsync(new SafeSyncTombstoneState
            {
                Tombstones = new List<SafeSyncTombstone>
                {
                    new SafeSyncTombstone
                    {
                        ClientSessionId = "client-session-1",
                        ServerSessionId = "server-session-1",
                        DeleteSource = SafeSyncTombstoneDeleteSource.ServerNotFound,
                        SyncStatus = SafeSyncTombstoneStatus.DeleteSynced,
                        LastSafeErrorCode = SafeSyncApiError.DeleteAlreadyApplied
                    }
                }
            }, CancellationToken.None));

            var combined = File.ReadAllText(conflictRepository.FilePath) + "\n" + File.ReadAllText(tombstoneRepository.FilePath);
            foreach (var forbidden in new[] { "rawPath", "approvedLocation", "prompt", "response", "command", "fileName", "repoName", "branchName", "token", "rawLog", "/Users/" })
            {
                Assert.That(combined, Does.Not.Contain(forbidden), forbidden);
            }

            Assert.AreEqual(1, RunAsync(() => conflictRepository.LoadAsync(CancellationToken.None)).Conflicts.Count);
            Assert.AreEqual(1, RunAsync(() => tombstoneRepository.LoadAsync(CancellationToken.None)).Tombstones.Count);
        }

        [Test]
        public void TombstoneCorruptionDoesNotCrash()
        {
            var directory = TempDirectory();
            var repository = new SafeSyncTombstoneRepository(directory);
            File.WriteAllText(repository.FilePath, "{broken");

            var state = RunAsync(() => repository.LoadAsync(CancellationToken.None));

            Assert.AreEqual(0, state.Tombstones.Count);
            Assert.IsTrue(File.Exists(repository.FilePath + ".corrupt"));
        }

        private static string TempDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static T RunAsync<T>(Func<Task<T>> taskFactory)
        {
            return Task.Run(taskFactory).GetAwaiter().GetResult();
        }
    }
}
