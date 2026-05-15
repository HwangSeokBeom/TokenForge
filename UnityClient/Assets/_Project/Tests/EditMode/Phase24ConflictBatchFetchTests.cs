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
    public sealed class Phase24ConflictBatchFetchTests
    {
        private sealed class FakeRepository : ILocalSaveDataRepository
        {
            public SaveData Current { get; set; } = SaveDataWithSession("client-session-1");
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
            private readonly Queue<SafeSyncApiResult<SafeActivitySessionDeleteResponse>> deleteResults = new Queue<SafeSyncApiResult<SafeActivitySessionDeleteResponse>>();
            public SafeSyncApiConfig Config { get; } = new SafeSyncApiConfig("http://localhost:3000/api/v1");
            public int DeleteCount { get; private set; }
            public SafeSyncApiResult<SafeActivitySessionsListResponse> FetchResult { get; set; } =
                SafeSyncApiResult<SafeActivitySessionsListResponse>.Success(new SafeActivitySessionsListResponse(), 200);
            public SafeSyncApiResult<SafeActivitySessionsUpsertResponse> PostResult { get; set; } =
                SafeSyncApiResult<SafeActivitySessionsUpsertResponse>.Success(new SafeActivitySessionsUpsertResponse { Success = true, AcceptedCount = 1, SchemaVersion = 1 }, 201);
            public void EnqueueDelete(SafeSyncApiResult<SafeActivitySessionDeleteResponse> result) => deleteResults.Enqueue(result);
            public Task<SafeSyncApiResult<SafeSyncHealthResponse>> GetHealthAsync(CancellationToken cancellationToken = default) => Task.FromResult(SafeSyncApiResult<SafeSyncHealthResponse>.Success(new SafeSyncHealthResponse { SchemaVersion = 1 }, 200));
            public Task<SafeSyncApiResult<SafeActivitySessionsUpsertResponse>> PostActivitySessionsAsync(SafeActivitySessionsContractRequest request, CancellationToken cancellationToken = default) => Task.FromResult(PostResult);
            public Task<SafeSyncApiResult<SafeActivitySessionsListResponse>> GetActivitySessionsAsync(CancellationToken cancellationToken = default) => Task.FromResult(FetchResult);
            public Task<SafeSyncApiResult<SafeActivitySessionDeleteResponse>> DeleteActivitySessionAsync(string serverSessionId, CancellationToken cancellationToken = default)
            {
                DeleteCount += 1;
                return Task.FromResult(deleteResults.Count > 0
                    ? deleteResults.Dequeue()
                    : SafeSyncApiResult<SafeActivitySessionDeleteResponse>.Success(new SafeActivitySessionDeleteResponse { Success = true }, 200));
            }
        }

        [Test]
        public void KeepRemoteAppliesSafeRemoteSessionAndResolvesConflict()
        {
            var directory = TempDirectory();
            var repository = new FakeRepository();
            var conflictRepository = new SafeSyncConflictRepository(directory);
            RunAsync(() => conflictRepository.SaveAsync(new SafeSyncConflictState
            {
                Conflicts = new List<SafeSyncConflict>
                {
                    Conflict("conflict-1", "client-session-1", SafeSyncConflictType.RemoteDifferent, Phase24RemoteToLocalApplyTests.Remote("client-session-1"))
                }
            }, CancellationToken.None));
            var service = CreateService(repository, new FakeApiClient(), directory);

            var result = RunAsync(() => service.KeepRemoteConflictAsync("conflict-1", CancellationToken.None));
            var conflict = RunAsync(() => conflictRepository.LoadAsync(CancellationToken.None)).Conflicts.Single();

            Assert.IsTrue(result.IsSuccess, result.ErrorCode);
            Assert.AreEqual(SafeSyncApiError.KeepRemoteApplied, result.ErrorCode);
            Assert.AreEqual(SafeSyncConflictResolutionStatus.KeepRemote, conflict.ResolutionStatus);
            Assert.AreEqual("2026-05-15", repository.Current.WorkSessionSummaries.Single().AgentActivitySummary.DayBucket);
        }

        [Test]
        public void KeepRemoteFallsBackToMarkerOnlyWhenConflictHasOnlySummary()
        {
            var directory = TempDirectory();
            var repository = new FakeRepository();
            var conflictRepository = new SafeSyncConflictRepository(directory);
            RunAsync(() => conflictRepository.SaveAsync(new SafeSyncConflictState
            {
                Conflicts = new List<SafeSyncConflict> { Conflict("conflict-1", "client-session-1", SafeSyncConflictType.RemoteDifferent, null) }
            }, CancellationToken.None));
            var service = CreateService(repository, new FakeApiClient(), directory);

            var result = RunAsync(() => service.KeepRemoteConflictAsync("conflict-1", CancellationToken.None));

            Assert.IsTrue(result.IsSuccess, result.ErrorCode);
            Assert.AreEqual(SafeSyncApiError.KeepRemoteMarkerOnly, result.ErrorCode);
            Assert.AreEqual(0, repository.SaveCount);
        }

        [Test]
        public void KeepRemoteRejectsUnsafeRemoteSessionAndLeavesConflictUnresolved()
        {
            var directory = TempDirectory();
            var repository = new FakeRepository();
            var unsafeRemote = Phase24RemoteToLocalApplyTests.Remote("client-session-1", category: "WORK_BAD");
            var conflictRepository = new SafeSyncConflictRepository(directory);
            RunAsync(() => conflictRepository.SaveAsync(new SafeSyncConflictState
            {
                Conflicts = new List<SafeSyncConflict> { Conflict("conflict-1", "client-session-1", SafeSyncConflictType.ValidationConflict, unsafeRemote) }
            }, CancellationToken.None));
            var service = CreateService(repository, new FakeApiClient(), directory);

            var result = RunAsync(() => service.KeepRemoteConflictAsync("conflict-1", CancellationToken.None));
            var conflict = RunAsync(() => conflictRepository.LoadAsync(CancellationToken.None)).Conflicts.Single();

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(SafeSyncConflictResolutionStatus.Unresolved, conflict.ResolutionStatus);
            Assert.AreEqual(0, repository.SaveCount);
        }

        [Test]
        public void FetchDetectsRemoteDifferentLocalMissingRemoteMissingAndRejectsUnsafe()
        {
            var directory = TempDirectory();
            var repository = new FakeRepository();
            RunAsync(() => new SafeSyncLocalStateRepository(directory).SaveAsync(new SafeSyncLocalState
            {
                SessionMappings = new List<SafeSyncLocalSessionMapping>
                {
                    new SafeSyncLocalSessionMapping { ClientSessionId = "remote-missing-local", ServerSessionId = "server-missing" }
                }
            }, CancellationToken.None));
            repository.Current.WorkSessionSummaries.Add(SafeLocalSession("remote-missing-local"));
            var api = new FakeApiClient
            {
                FetchResult = SafeSyncApiResult<SafeActivitySessionsListResponse>.Success(new SafeActivitySessionsListResponse
                {
                    Sessions = new List<RemoteSafeActivitySessionDto>
                    {
                        Phase24RemoteToLocalApplyTests.Remote("client-session-1", category: "WORK_BUGFIX"),
                        Phase24RemoteToLocalApplyTests.Remote("remote-only-session"),
                        Phase24RemoteToLocalApplyTests.Remote("unsafe-session", schema: 99)
                    }
                }, 200)
            };
            var service = CreateService(repository, api, directory);

            var result = RunAsync(() => service.FetchRemoteSessionsAsync(CancellationToken.None));
            var conflicts = RunAsync(() => new SafeSyncConflictRepository(directory).LoadAsync(CancellationToken.None)).Conflicts;

            Assert.IsTrue(result.IsSuccess, result.ErrorCode);
            Assert.AreEqual(1, result.UnsafeRejectedCount);
            Assert.IsTrue(conflicts.Any(conflict => conflict.ClientSessionId == "client-session-1" && conflict.ConflictType == SafeSyncConflictType.RemoteDifferent));
            Assert.IsTrue(conflicts.Any(conflict => conflict.ClientSessionId == "remote-only-session" && conflict.ConflictType == SafeSyncConflictType.LocalMissing));
            Assert.IsTrue(conflicts.Any(conflict => conflict.ClientSessionId == "remote-missing-local" && conflict.ConflictType == SafeSyncConflictType.RemoteMissing));
            Assert.AreEqual(0, repository.SaveCount);
        }

        [Test]
        public void BatchTombstoneProcessingHandlesMixedResultsAndCancelFailedOnly()
        {
            var directory = TempDirectory();
            var tombstones = new SafeSyncTombstoneRepository(directory);
            RunAsync(() => tombstones.SaveAsync(new SafeSyncTombstoneState
            {
                Tombstones = new List<SafeSyncTombstone>
                {
                    new SafeSyncTombstone { TombstoneId = "t1", ClientSessionId = "c1", ServerSessionId = "s1", SyncStatus = SafeSyncTombstoneStatus.PendingDelete },
                    new SafeSyncTombstone { TombstoneId = "t2", ClientSessionId = "c2", ServerSessionId = "s2", SyncStatus = SafeSyncTombstoneStatus.PendingDelete },
                    new SafeSyncTombstone { TombstoneId = "t3", ClientSessionId = "c3", ServerSessionId = "s3", SyncStatus = SafeSyncTombstoneStatus.PendingDelete }
                }
            }, CancellationToken.None));
            var api = new FakeApiClient();
            api.EnqueueDelete(SafeSyncApiResult<SafeActivitySessionDeleteResponse>.Success(new SafeActivitySessionDeleteResponse { Success = true }, 200));
            api.EnqueueDelete(SafeSyncApiResult<SafeActivitySessionDeleteResponse>.Failure("HTTP_404", "missing", 404));
            api.EnqueueDelete(SafeSyncApiResult<SafeActivitySessionDeleteResponse>.Failure(SafeSyncApiError.ValidationFailed, "invalid", 400));
            var service = CreateService(new FakeRepository(), api, directory);

            var result = RunAsync(() => service.ProcessPendingTombstoneDeletesOnceAsync(CancellationToken.None));
            RunAsync(() => service.CancelAllFailedTombstonesAsync(CancellationToken.None));
            var state = RunAsync(() => tombstones.LoadAsync(CancellationToken.None));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(3, result.AttemptedCount);
            Assert.AreEqual(2, result.SucceededCount);
            Assert.AreEqual(SafeSyncTombstoneStatus.DeleteSynced, state.Tombstones.Single(item => item.TombstoneId == "t1").SyncStatus);
            Assert.AreEqual(SafeSyncTombstoneStatus.DeleteAlreadyApplied, state.Tombstones.Single(item => item.TombstoneId == "t2").SyncStatus);
            Assert.AreEqual(SafeSyncTombstoneStatus.DeleteCancelled, state.Tombstones.Single(item => item.TombstoneId == "t3").SyncStatus);
        }

        [Test]
        public void BatchRetrySkipsNotReadyAndCancelsFailedOnly()
        {
            var directory = TempDirectory();
            var queueRepository = new SafeSyncRetryQueueRepository(directory);
            RunAsync(() => queueRepository.SaveAsync(new SafeSyncRetryQueueState
            {
                Entries = new List<SafeSyncRetryQueueEntry>
                {
                    new SafeSyncRetryQueueEntry { QueueEntryId = "ready", OperationType = SafeSyncRetryOperationType.UPSERT_ACTIVITY_SESSIONS, ClientSessionIds = new List<string> { "client-session-1" }, Status = SafeSyncRetryQueueEntryStatus.Pending, NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(-1) },
                    new SafeSyncRetryQueueEntry { QueueEntryId = "later", OperationType = SafeSyncRetryOperationType.UPSERT_ACTIVITY_SESSIONS, ClientSessionIds = new List<string> { "client-session-1" }, Status = SafeSyncRetryQueueEntryStatus.Pending, NextAttemptAt = DateTimeOffset.UtcNow.AddHours(1) },
                    new SafeSyncRetryQueueEntry { QueueEntryId = "failed", OperationType = SafeSyncRetryOperationType.DELETE_REMOTE_SESSION, ServerSessionId = "server-failed", Status = SafeSyncRetryQueueEntryStatus.Failed }
                }
            }, CancellationToken.None));
            var service = CreateService(new FakeRepository(), new FakeApiClient(), directory);

            var result = RunAsync(() => service.ProcessAllEligibleRetryEntriesOnceAsync(CancellationToken.None));
            RunAsync(() => service.CancelAllFailedRetryEntriesAsync(CancellationToken.None));
            var state = RunAsync(() => queueRepository.LoadAsync(CancellationToken.None));

            Assert.IsTrue(result.IsSuccess, result.ErrorCode);
            Assert.AreEqual(1, result.AttemptedCount);
            Assert.AreEqual(1, result.SkippedNotReadyCount);
            Assert.AreEqual(SafeSyncRetryQueueEntryStatus.Succeeded, state.Entries.Single(entry => entry.QueueEntryId == "ready").Status);
            Assert.AreEqual(SafeSyncRetryQueueEntryStatus.Pending, state.Entries.Single(entry => entry.QueueEntryId == "later").Status);
            Assert.AreEqual(SafeSyncRetryQueueEntryStatus.Cancelled, state.Entries.Single(entry => entry.QueueEntryId == "failed").Status);
        }

        private static SafeSyncConflict Conflict(string conflictId, string clientSessionId, SafeSyncConflictType type, RemoteSafeActivitySessionDto remote)
        {
            return new SafeSyncConflict
            {
                ConflictId = conflictId,
                ClientSessionId = clientSessionId,
                ServerSessionId = "server-" + clientSessionId,
                ConflictType = type,
                SafeLocalSummary = new SafeSyncSessionSafeSummary { ClientSessionId = clientSessionId, SourceProvider = "CODEX", DayBucket = "2026-05-14", ActivityCategory = "WORK_FEATURE", Confidence = "HIGH" },
                SafeRemoteSummary = new SafeSyncSessionSafeSummary { ClientSessionId = clientSessionId, ServerSessionId = "server-" + clientSessionId, SourceProvider = "CODEX", DayBucket = "2026-05-15", ActivityCategory = "WORK_FEATURE", Confidence = "HIGH" },
                SafeRemoteSession = remote,
                SafeErrorCode = SafeSyncApiError.ConflictDetected
            };
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

        private static SaveData SaveDataWithSession(string id)
        {
            var saveData = SaveData.CreateDefault();
            saveData.WorkSessionSummaries.Add(SafeLocalSession(id));
            return saveData;
        }

        private static AgentWorkSession SafeLocalSession(string id)
        {
            return new AgentWorkSession
            {
                SessionId = id,
                SourceProvider = "CODEX",
                AgentType = AgentType.Codex,
                WorkType = WorkType.Feature,
                StartedAt = new DateTimeOffset(2026, 5, 15, 2, 0, 0, TimeSpan.Zero),
                EndedAt = new DateTimeOffset(2026, 5, 15, 2, 30, 0, TimeSpan.Zero),
                Confidence = ProviderConfidence.High,
                GitChangeSummary = new GitChangeSummary { ChangedFileCountBucket = CountBucket.Small, AddedLineBucket = LineChangeBucket.Small, CommitCountBucket = CountBucket.One },
                AgentActivitySummary = new AgentActivitySummary
                {
                    DayBucket = "2026-05-15",
                    SessionCountBucket = CountBucket.One,
                    InteractionCountBucket = CountBucket.Small,
                    ToolUsageCategoryBuckets = new List<AgentToolUsageCategoryBucket> { new AgentToolUsageCategoryBucket { Category = AgentToolUsageCategory.CodeEditing, CountBucket = CountBucket.Small } },
                    LanguageCategoryBuckets = new List<AgentLanguageCategoryBucket> { new AgentLanguageCategoryBucket { Category = AgentLanguageCategory.CSharp, CountBucket = CountBucket.Small } }
                },
                Warnings = new List<string> { "SAFE_WARNING" }
            };
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
