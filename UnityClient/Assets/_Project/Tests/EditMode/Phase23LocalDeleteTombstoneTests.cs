using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TokenForge.Client.Common;
using TokenForge.Client.Domain;
using TokenForge.Client.Persistence;
using TokenForge.Client.Sync;

namespace TokenForge.Client.Tests
{
    public sealed class Phase23LocalDeleteTombstoneTests
    {
        private sealed class FakeRepository : ILocalSaveDataRepository
        {
            public SaveData Current { get; set; } = CreateSaveData("session-a", "session-b");
            public int SaveCount { get; private set; }
            public Task<SaveData> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Current);
            public Task<Result> SaveAsync(SaveData saveData, CancellationToken cancellationToken = default)
            {
                SaveCount += 1;
                Current = saveData;
                return Task.FromResult(Result.Success());
            }
        }

        private sealed class FakeApiClient : ISafeSyncApiClient
        {
            public SafeSyncApiConfig Config { get; } = new SafeSyncApiConfig("http://localhost:3000/api/v1");
            public int DeleteCount { get; private set; }
            public SafeSyncApiResult<SafeActivitySessionDeleteResponse> DeleteResult { get; set; } =
                SafeSyncApiResult<SafeActivitySessionDeleteResponse>.Success(new SafeActivitySessionDeleteResponse { Success = true }, 200);
            public Task<SafeSyncApiResult<SafeSyncHealthResponse>> GetHealthAsync(CancellationToken cancellationToken = default) => Task.FromResult(SafeSyncApiResult<SafeSyncHealthResponse>.Success(new SafeSyncHealthResponse { SchemaVersion = 1 }, 200));
            public Task<SafeSyncApiResult<SafeActivitySessionsUpsertResponse>> PostActivitySessionsAsync(SafeActivitySessionsContractRequest request, CancellationToken cancellationToken = default) => Task.FromResult(SafeSyncApiResult<SafeActivitySessionsUpsertResponse>.Success(new SafeActivitySessionsUpsertResponse { Success = true, AcceptedCount = request.Sessions.Count, SchemaVersion = 1 }, 201));
            public Task<SafeSyncApiResult<SafeActivitySessionsListResponse>> GetActivitySessionsAsync(CancellationToken cancellationToken = default) => Task.FromResult(SafeSyncApiResult<SafeActivitySessionsListResponse>.Success(new SafeActivitySessionsListResponse(), 200));
            public Task<SafeSyncApiResult<SafeActivitySessionDeleteResponse>> DeleteActivitySessionAsync(string serverSessionId, CancellationToken cancellationToken = default)
            {
                DeleteCount += 1;
                return Task.FromResult(DeleteResult);
            }
        }

        [Test]
        public void DeletingUnsyncedLocalSessionRemovesOnlyLocalSessionAndCreatesNoTombstone()
        {
            var directory = TempDirectory();
            var repository = new FakeRepository();
            var api = new FakeApiClient();
            var service = CreateService(repository, api, directory);

            var result = RunAsync(() => service.DeleteLocalSavedSessionAsync("session-a", CancellationToken.None));
            var tombstones = RunAsync(() => new SafeSyncTombstoneRepository(directory).LoadAsync(CancellationToken.None));

            Assert.IsTrue(result.IsSuccess, result.ErrorCode);
            Assert.AreEqual(1, repository.SaveCount);
            Assert.IsFalse(repository.Current.WorkSessionSummaries.Any(session => session.SessionId == "session-a"));
            Assert.IsTrue(repository.Current.WorkSessionSummaries.Any(session => session.SessionId == "session-b"));
            Assert.AreEqual(0, tombstones.Tombstones.Count);
            Assert.AreEqual(0, api.DeleteCount);
        }

        [Test]
        public void DeletingSyncedLocalSessionCreatesPendingTombstoneWithoutRemoteDelete()
        {
            var directory = TempDirectory();
            var repository = new FakeRepository();
            var api = new FakeApiClient();
            RunAsync(() => new SafeSyncLocalStateRepository(directory).SaveAsync(new SafeSyncLocalState
            {
                SessionMappings = new List<SafeSyncLocalSessionMapping>
                {
                    new SafeSyncLocalSessionMapping { ClientSessionId = "session-a", ServerSessionId = "server-a" }
                }
            }, CancellationToken.None));
            var service = CreateService(repository, api, directory);

            var result = RunAsync(() => service.DeleteLocalSavedSessionAsync("session-a", CancellationToken.None));
            var tombstones = RunAsync(() => new SafeSyncTombstoneRepository(directory).LoadAsync(CancellationToken.None));

            Assert.IsTrue(result.IsSuccess, result.ErrorCode);
            Assert.AreEqual(SafeSyncApiError.TombstoneCreated, result.ErrorCode);
            Assert.AreEqual(1, tombstones.Tombstones.Count);
            Assert.AreEqual("server-a", tombstones.Tombstones.Single().ServerSessionId);
            Assert.AreEqual(SafeSyncTombstoneStatus.PendingDelete, tombstones.Tombstones.Single().SyncStatus);
            Assert.AreEqual(0, api.DeleteCount);
        }

        [Test]
        public void DeletingLocalSessionUpdatesPendingUpsertRetryEntries()
        {
            var directory = TempDirectory();
            var repository = new FakeRepository();
            var queueRepository = new SafeSyncRetryQueueRepository(directory);
            RunAsync(() => queueRepository.SaveAsync(new SafeSyncRetryQueueState
            {
                Entries = new List<SafeSyncRetryQueueEntry>
                {
                    new SafeSyncRetryQueueEntry
                    {
                        OperationType = SafeSyncRetryOperationType.UPSERT_ACTIVITY_SESSIONS,
                        Status = SafeSyncRetryQueueEntryStatus.Pending,
                        ClientSessionIds = new List<string> { "session-a", "session-b" }
                    },
                    new SafeSyncRetryQueueEntry
                    {
                        OperationType = SafeSyncRetryOperationType.UPSERT_ACTIVITY_SESSIONS,
                        Status = SafeSyncRetryQueueEntryStatus.Pending,
                        ClientSessionIds = new List<string> { "session-a" }
                    },
                    new SafeSyncRetryQueueEntry
                    {
                        OperationType = SafeSyncRetryOperationType.DELETE_REMOTE_SESSION,
                        Status = SafeSyncRetryQueueEntryStatus.Pending,
                        ServerSessionId = "server-other"
                    }
                }
            }, CancellationToken.None));
            var service = CreateService(repository, new FakeApiClient(), directory);

            RunAsync(() => service.DeleteLocalSavedSessionAsync("session-a", CancellationToken.None));
            var queue = RunAsync(() => queueRepository.LoadAsync(CancellationToken.None));

            Assert.AreEqual(new[] { "session-b" }, queue.Entries[0].ClientSessionIds.ToArray());
            Assert.AreEqual(SafeSyncRetryQueueEntryStatus.Cancelled, queue.Entries[1].Status);
            Assert.AreEqual(SafeSyncRetryOperationType.DELETE_REMOTE_SESSION, queue.Entries[2].OperationType);
            Assert.AreEqual("server-other", queue.Entries[2].ServerSessionId);
        }

        [Test]
        public void PendingTombstoneDeleteHandlesSuccessNotFoundAuthAndRetryableFailureSafely()
        {
            var directory = TempDirectory();
            var repository = new FakeRepository();
            var api = new FakeApiClient();
            var tombstoneRepository = new SafeSyncTombstoneRepository(directory);
            RunAsync(() => tombstoneRepository.SaveAsync(new SafeSyncTombstoneState
            {
                Tombstones = new List<SafeSyncTombstone>
                {
                    new SafeSyncTombstone { ClientSessionId = "session-a", ServerSessionId = "server-a", SyncStatus = SafeSyncTombstoneStatus.PendingDelete }
                }
            }, CancellationToken.None));
            var service = CreateService(repository, api, directory);

            var success = RunAsync(() => service.ProcessPendingTombstoneDeletesOnceAsync(CancellationToken.None));
            Assert.IsTrue(success.IsSuccess, success.ErrorCode);
            Assert.AreEqual(SafeSyncTombstoneStatus.DeleteSynced, RunAsync(() => tombstoneRepository.LoadAsync(CancellationToken.None)).Tombstones.Single().SyncStatus);

            RunAsync(() => tombstoneRepository.SaveAsync(new SafeSyncTombstoneState { Tombstones = new List<SafeSyncTombstone> { new SafeSyncTombstone { ClientSessionId = "session-a", ServerSessionId = "server-a", SyncStatus = SafeSyncTombstoneStatus.PendingDelete } } }, CancellationToken.None));
            api.DeleteResult = SafeSyncApiResult<SafeActivitySessionDeleteResponse>.Failure("HTTP_404", "missing", 404);
            var notFound = RunAsync(() => service.ProcessPendingTombstoneDeletesOnceAsync(CancellationToken.None));
            Assert.IsTrue(notFound.IsSuccess, notFound.ErrorCode);
            Assert.AreEqual(SafeSyncTombstoneStatus.DeleteAlreadyApplied, RunAsync(() => tombstoneRepository.LoadAsync(CancellationToken.None)).Tombstones.Single().SyncStatus);

            RunAsync(() => tombstoneRepository.SaveAsync(new SafeSyncTombstoneState { Tombstones = new List<SafeSyncTombstone> { new SafeSyncTombstone { ClientSessionId = "session-a", ServerSessionId = "server-a", SyncStatus = SafeSyncTombstoneStatus.PendingDelete } } }, CancellationToken.None));
            api.DeleteResult = SafeSyncApiResult<SafeActivitySessionDeleteResponse>.Failure(SafeSyncApiError.AuthRequired, "auth", 401);
            var auth = RunAsync(() => service.ProcessPendingTombstoneDeletesOnceAsync(CancellationToken.None));
            Assert.IsFalse(auth.IsSuccess);
            Assert.AreEqual(SafeSyncStatus.AuthRequired, auth.Status);
            Assert.AreEqual(SafeSyncTombstoneStatus.PendingDelete, RunAsync(() => tombstoneRepository.LoadAsync(CancellationToken.None)).Tombstones.Single().SyncStatus);

            api.DeleteResult = SafeSyncApiResult<SafeActivitySessionDeleteResponse>.Failure(SafeSyncApiError.ServerUnavailable, "down", 503);
            var retryable = RunAsync(() => service.ProcessPendingTombstoneDeletesOnceAsync(CancellationToken.None));
            var queue = RunAsync(() => new SafeSyncRetryQueueRepository(directory).LoadAsync(CancellationToken.None));
            Assert.IsFalse(retryable.IsSuccess);
            Assert.AreEqual(SafeSyncRetryOperationType.DELETE_REMOTE_SESSION, queue.Entries.Single().OperationType);
            Assert.AreEqual("server-a", queue.Entries.Single().ServerSessionId);
        }

        [Test]
        public void LocalStateFilesRemainPrivacySafeAndGitignored()
        {
            var directory = TempDirectory();
            var stateRepository = new SafeSyncLocalStateRepository(directory);
            RunAsync(() => stateRepository.SaveAsync(new SafeSyncLocalState
            {
                SessionMappings = new List<SafeSyncLocalSessionMapping>
                {
                    new SafeSyncLocalSessionMapping { ClientSessionId = "client-safe", ServerSessionId = "server-safe" }
                }
            }, CancellationToken.None));

            var json = File.ReadAllText(stateRepository.FilePath);
            foreach (var forbidden in new[] { "rawPath", "approvedLocation", "prompt", "response", "command", "fileName", "repoName", "branchName", "token", "rawLog", "/Users/" })
            {
                Assert.That(json, Does.Not.Contain(forbidden), forbidden);
            }

            Assert.That(File.ReadAllText(Path.Combine(FindRepoRoot(), ".gitignore")), Does.Contain("tokenforge-sync-local-state.local.json"));
        }

        private static SafeSyncService CreateService(FakeRepository repository, FakeApiClient api, string directory)
        {
            return new SafeSyncService(
                repository,
                api,
                null,
                null,
                null,
                new SafeSyncRetryQueueRepository(directory),
                new SafeSyncRetryPolicy(),
                new SafeSyncConflictRepository(directory),
                new SafeSyncTombstoneRepository(directory),
                new SafeSyncLocalStateRepository(directory));
        }

        private static SaveData CreateSaveData(params string[] ids)
        {
            var saveData = SaveData.CreateDefault();
            foreach (var id in ids)
            {
                saveData.WorkSessionSummaries.Add(new AgentWorkSession
                {
                    SessionId = id,
                    SourceProvider = "CODEX",
                    AgentType = AgentType.Codex,
                    WorkType = WorkType.Feature,
                    EndedAt = new DateTimeOffset(2026, 5, 15, 2, 0, 0, TimeSpan.Zero),
                    Confidence = ProviderConfidence.High,
                    GitChangeSummary = new GitChangeSummary { ProjectPathHash = "abcdef1234567890", ChangedFileCountBucket = CountBucket.Small },
                    AgentActivitySummary = new AgentActivitySummary { DayBucket = "2026-05-15", SessionCountBucket = CountBucket.One, InteractionCountBucket = CountBucket.Small }
                });
            }

            return saveData;
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
