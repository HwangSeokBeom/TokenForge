using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TokenForge.Client.Sync;

namespace TokenForge.Client.Tests
{
    public sealed class Phase22RetryQueueRepositoryTests
    {
        [Test]
        public void SavesAndLoadsSafeQueueEntries()
        {
            var directory = TempDirectory();
            var repository = new SafeSyncRetryQueueRepository(directory);
            var state = new SafeSyncRetryQueueState
            {
                Entries = new List<SafeSyncRetryQueueEntry>
                {
                    new SafeSyncRetryQueueEntry
                    {
                        OperationType = SafeSyncRetryOperationType.UPSERT_ACTIVITY_SESSIONS,
                        ClientSessionIds = new List<string> { "client-session-1" },
                        LastSafeErrorCode = SafeSyncApiError.ServerUnavailable,
                        Status = SafeSyncRetryQueueEntryStatus.Pending
                    }
                }
            };

            var save = RunAsync(() => repository.SaveAsync(state, CancellationToken.None));
            var loaded = RunAsync(() => repository.LoadAsync(CancellationToken.None));

            Assert.IsTrue(save.IsSuccess, save.ErrorCode);
            Assert.AreEqual(1, loaded.SchemaVersion);
            Assert.AreEqual(1, loaded.Entries.Count);
            Assert.AreEqual(SafeSyncRetryOperationType.UPSERT_ACTIVITY_SESSIONS, loaded.Entries[0].OperationType);
            Assert.AreEqual("client-session-1", loaded.Entries[0].ClientSessionIds[0]);
        }

        [Test]
        public void MissingSchemaVersionMigratesToCurrentVersion()
        {
            var directory = TempDirectory();
            var path = Path.Combine(directory, SafeSyncRetryQueueRepository.FileName);
            File.WriteAllText(path, "{\"entries\":[]}");

            var loaded = RunAsync(() => new SafeSyncRetryQueueRepository(directory).LoadAsync(CancellationToken.None));

            Assert.AreEqual(1, loaded.SchemaVersion);
            Assert.AreEqual(0, loaded.Entries.Count);
        }

        [Test]
        public void UnknownFutureSchemaAndCorruptionReturnDefaults()
        {
            var directory = TempDirectory();
            var repository = new SafeSyncRetryQueueRepository(directory);
            File.WriteAllText(repository.FilePath, "{\"schemaVersion\":99,\"entries\":[{\"queueEntryId\":\"future\"}]}");

            var future = RunAsync(() => repository.LoadAsync(CancellationToken.None));
            Assert.AreEqual(0, future.Entries.Count);
            Assert.IsTrue(File.Exists(repository.FilePath + ".unsupported-schema"));

            File.WriteAllText(repository.FilePath, "{broken");
            var corrupt = RunAsync(() => repository.LoadAsync(CancellationToken.None));

            Assert.AreEqual(0, corrupt.Entries.Count);
            Assert.IsTrue(File.Exists(repository.FilePath + ".corrupt"));
        }

        [Test]
        public void QueueFileContainsNoForbiddenPrivateFieldsAndIsGitignored()
        {
            var directory = TempDirectory();
            var repository = new SafeSyncRetryQueueRepository(directory);
            RunAsync(() => repository.SaveAsync(new SafeSyncRetryQueueState
            {
                Entries = new List<SafeSyncRetryQueueEntry>
                {
                    new SafeSyncRetryQueueEntry
                    {
                        OperationType = SafeSyncRetryOperationType.DELETE_REMOTE_SESSION,
                        ServerSessionId = "server-session-1",
                        LastSafeErrorCode = SafeSyncApiError.NetworkTimeout
                    }
                }
            }, CancellationToken.None));

            var json = File.ReadAllText(repository.FilePath);
            foreach (var forbidden in new[] { "rawPath", "approvedLocation", "prompt", "response", "command", "fileName", "repoName", "branchName", "token", "rawLog", "raw request", "raw response" })
            {
                Assert.That(json, Does.Not.Contain(forbidden), forbidden);
            }

            var gitignore = File.ReadAllText(Path.Combine(FindRepoRoot(), ".gitignore"));
            Assert.That(gitignore, Does.Contain("tokenforge-sync-retry-queue.local.json"));
        }

        private static string TempDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
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

        private static T RunAsync<T>(Func<Task<T>> taskFactory)
        {
            return Task.Run(taskFactory).GetAwaiter().GetResult();
        }
    }
}
