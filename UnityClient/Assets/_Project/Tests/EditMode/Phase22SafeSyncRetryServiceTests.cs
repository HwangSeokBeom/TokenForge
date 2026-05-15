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
    public sealed class Phase22SafeSyncRetryServiceTests
    {
        private sealed class FakeRepository : ILocalSaveDataRepository
        {
            public SaveData Current { get; set; } = CreateSaveData();
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
            public int PostCount { get; private set; }
            public int DeleteCount { get; private set; }
            public SafeActivitySessionsContractRequest LastRequest { get; private set; }
            public SafeSyncApiResult<SafeActivitySessionsUpsertResponse> PostResult { get; set; } =
                SafeSyncApiResult<SafeActivitySessionsUpsertResponse>.Failure(SafeSyncApiError.ServerUnavailable, "down", 503);
            public SafeSyncApiResult<SafeActivitySessionDeleteResponse> DeleteResult { get; set; } =
                SafeSyncApiResult<SafeActivitySessionDeleteResponse>.Success(new SafeActivitySessionDeleteResponse { Success = true }, 200);

            public Task<SafeSyncApiResult<SafeSyncHealthResponse>> GetHealthAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(SafeSyncApiResult<SafeSyncHealthResponse>.Success(new SafeSyncHealthResponse { SchemaVersion = 1 }, 200));

            public Task<SafeSyncApiResult<SafeActivitySessionsUpsertResponse>> PostActivitySessionsAsync(SafeActivitySessionsContractRequest request, CancellationToken cancellationToken = default)
            {
                PostCount += 1;
                LastRequest = request;
                return Task.FromResult(PostResult);
            }

            public Task<SafeSyncApiResult<SafeActivitySessionsListResponse>> GetActivitySessionsAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(SafeSyncApiResult<SafeActivitySessionsListResponse>.Success(new SafeActivitySessionsListResponse(), 200));

            public Task<SafeSyncApiResult<SafeActivitySessionDeleteResponse>> DeleteActivitySessionAsync(string serverSessionId, CancellationToken cancellationToken = default)
            {
                DeleteCount += 1;
                return Task.FromResult(DeleteResult);
            }
        }

        [Test]
        public void FailedRetryableSyncEnqueuesSafeEntryAndPreservesLocalSessions()
        {
            var directory = TempDirectory();
            var repository = new FakeRepository();
            var api = new FakeApiClient();
            var service = CreateService(repository, api, directory);

            var result = RunAsync(() => service.EnqueueSyncSafeSessionsAsync(CancellationToken.None));
            var queue = RunAsync(() => new SafeSyncRetryQueueRepository(directory).LoadAsync(CancellationToken.None));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(SafeSyncStatus.RetryPending, result.Status);
            Assert.AreEqual(1, queue.Entries.Count);
            Assert.AreEqual(SafeSyncRetryOperationType.UPSERT_ACTIVITY_SESSIONS, queue.Entries[0].OperationType);
            Assert.AreEqual("session-safe-1", queue.Entries[0].ClientSessionIds.Single());
            Assert.AreEqual(0, repository.SaveCount);
            Assert.AreEqual(1, repository.Current.WorkSessionSummaries.Count);
            Assert.That(File.ReadAllText(Path.Combine(directory, SafeSyncRetryQueueRepository.FileName)), Does.Not.Contain("sessions"));
        }

        [Test]
        public void ProcessingQueueRebuildsPayloadViaMapperAndRunsPrivacyGuard()
        {
            var directory = TempDirectory();
            var repository = new FakeRepository();
            var api = new FakeApiClient();
            var queueRepository = new SafeSyncRetryQueueRepository(directory);
            RunAsync(() => queueRepository.SaveAsync(new SafeSyncRetryQueueState
            {
                Entries = new List<SafeSyncRetryQueueEntry>
                {
                    new SafeSyncRetryQueueEntry
                    {
                        OperationType = SafeSyncRetryOperationType.UPSERT_ACTIVITY_SESSIONS,
                        ClientSessionIds = new List<string> { "session-safe-1" },
                        NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(-1),
                        Status = SafeSyncRetryQueueEntryStatus.Pending
                    }
                }
            }, CancellationToken.None));
            api.PostResult = SafeSyncApiResult<SafeActivitySessionsUpsertResponse>.Success(new SafeActivitySessionsUpsertResponse { Success = true, AcceptedCount = 1, RejectedCount = 0, SchemaVersion = 1 }, 201);
            var service = CreateService(repository, api, directory);

            var result = RunAsync(() => service.ProcessRetryQueueOnceAsync(CancellationToken.None));
            var queue = RunAsync(() => queueRepository.LoadAsync(CancellationToken.None));

            Assert.IsTrue(result.IsSuccess, result.ErrorCode);
            Assert.AreEqual(1, api.PostCount);
            Assert.AreEqual("session-safe-1", api.LastRequest.Sessions.Single().ClientSessionId);
            Assert.AreEqual(SafeSyncRetryQueueEntryStatus.Succeeded, queue.Entries.Single().Status);
        }

        [Test]
        public void AuthAndPrivacyErrorsDoNotRetryBlindly()
        {
            var directory = TempDirectory();
            var repository = new FakeRepository();
            var api = new FakeApiClient { PostResult = SafeSyncApiResult<SafeActivitySessionsUpsertResponse>.Failure(SafeSyncApiError.AuthRequired, "auth", 401) };
            var queueRepository = new SafeSyncRetryQueueRepository(directory);
            RunAsync(() => queueRepository.SaveAsync(new SafeSyncRetryQueueState
            {
                Entries = new List<SafeSyncRetryQueueEntry>
                {
                    new SafeSyncRetryQueueEntry
                    {
                        OperationType = SafeSyncRetryOperationType.UPSERT_ACTIVITY_SESSIONS,
                        ClientSessionIds = new List<string> { "session-safe-1" },
                        NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(-1),
                        Status = SafeSyncRetryQueueEntryStatus.Pending
                    }
                }
            }, CancellationToken.None));

            var service = CreateService(repository, api, directory);
            RunAsync(() => service.ProcessRetryQueueOnceAsync(CancellationToken.None));
            var queue = RunAsync(() => queueRepository.LoadAsync(CancellationToken.None));

            Assert.AreEqual(SafeSyncRetryQueueEntryStatus.Paused, queue.Entries.Single().Status);
            Assert.AreEqual(SafeSyncApiError.AuthRequired, queue.Entries.Single().LastSafeErrorCode);
        }

        [Test]
        public void DeleteNotFoundBecomesResolvedTombstone()
        {
            var directory = TempDirectory();
            var repository = new FakeRepository();
            var api = new FakeApiClient { DeleteResult = SafeSyncApiResult<SafeActivitySessionDeleteResponse>.Failure("HTTP_404", "missing", 404) };
            var queueRepository = new SafeSyncRetryQueueRepository(directory);
            RunAsync(() => queueRepository.SaveAsync(new SafeSyncRetryQueueState
            {
                Entries = new List<SafeSyncRetryQueueEntry>
                {
                    new SafeSyncRetryQueueEntry
                    {
                        OperationType = SafeSyncRetryOperationType.DELETE_REMOTE_SESSION,
                        ServerSessionId = "server-session-1",
                        NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(-1),
                        Status = SafeSyncRetryQueueEntryStatus.Pending
                    }
                }
            }, CancellationToken.None));
            var service = CreateService(repository, api, directory);

            var result = RunAsync(() => service.ProcessRetryQueueOnceAsync(CancellationToken.None));
            var queue = RunAsync(() => queueRepository.LoadAsync(CancellationToken.None));
            var tombstones = RunAsync(() => new SafeSyncTombstoneRepository(directory).LoadAsync(CancellationToken.None));

            Assert.IsTrue(result.IsSuccess, result.ErrorCode);
            Assert.AreEqual(SafeSyncRetryQueueEntryStatus.Succeeded, queue.Entries.Single().Status);
            Assert.AreEqual(SafeSyncTombstoneStatus.DeleteSynced, tombstones.Tombstones.Single().SyncStatus);
            Assert.AreEqual(SafeSyncTombstoneDeleteSource.ServerNotFound, tombstones.Tombstones.Single().DeleteSource);
        }

        [Test]
        public void RetryableDeleteFailureEnqueuesDeleteEntry()
        {
            var directory = TempDirectory();
            var repository = new FakeRepository();
            var api = new FakeApiClient { DeleteResult = SafeSyncApiResult<SafeActivitySessionDeleteResponse>.Failure(SafeSyncApiError.ServerUnavailable, "down", 503) };
            var service = CreateService(repository, api, directory);

            var result = RunAsync(() => service.DeleteRemoteSessionAsync("server-session-1", CancellationToken.None));
            var queue = RunAsync(() => new SafeSyncRetryQueueRepository(directory).LoadAsync(CancellationToken.None));
            var tombstones = RunAsync(() => new SafeSyncTombstoneRepository(directory).LoadAsync(CancellationToken.None));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(SafeSyncStatus.RetryPending, result.Status);
            Assert.AreEqual(SafeSyncRetryOperationType.DELETE_REMOTE_SESSION, queue.Entries.Single().OperationType);
            Assert.AreEqual("server-session-1", queue.Entries.Single().ServerSessionId);
            Assert.AreEqual(SafeSyncTombstoneStatus.PendingDelete, tombstones.Tombstones.Single().SyncStatus);
            Assert.AreEqual(0, repository.SaveCount);
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
                new SafeSyncTombstoneRepository(directory));
        }

        private static SaveData CreateSaveData()
        {
            var saveData = SaveData.CreateDefault();
            saveData.WorkSessionSummaries.Add(new AgentWorkSession
            {
                SessionId = "session-safe-1",
                SourceProvider = "CODEX",
                AgentType = AgentType.Codex,
                WorkType = WorkType.Feature,
                StartedAt = new DateTimeOffset(2026, 5, 15, 1, 0, 0, TimeSpan.Zero),
                EndedAt = new DateTimeOffset(2026, 5, 15, 2, 0, 0, TimeSpan.Zero),
                Confidence = ProviderConfidence.High,
                GitChangeSummary = new GitChangeSummary
                {
                    ChangedFileCount = 2,
                    ChangedFileCountBucket = CountBucket.Small,
                    AddedLineBucket = LineChangeBucket.Small,
                    DeletedLineBucket = LineChangeBucket.Small,
                    ProjectPathHash = "abcdef1234567890"
                },
                AgentActivitySummary = new AgentActivitySummary
                {
                    DayBucket = "2026-05-15",
                    SessionCountBucket = CountBucket.One,
                    InteractionCountBucket = CountBucket.Small
                },
                UserReviewed = true
            });
            return saveData;
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
