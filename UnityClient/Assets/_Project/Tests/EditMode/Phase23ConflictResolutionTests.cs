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
    public sealed class Phase23ConflictResolutionTests
    {
        private sealed class FakeRepository : ILocalSaveDataRepository
        {
            public SaveData Current { get; set; } = CreateSaveData("session-a");
            public Task<SaveData> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Current);
            public Task<Result> SaveAsync(SaveData saveData, CancellationToken cancellationToken = default)
            {
                Current = saveData;
                return Task.FromResult(Result.Success());
            }
        }

        private sealed class FakeApiClient : ISafeSyncApiClient
        {
            public SafeSyncApiConfig Config { get; } = new SafeSyncApiConfig("http://localhost:3000/api/v1");
            public SafeSyncApiResult<SafeActivitySessionsListResponse> FetchResult { get; set; } =
                SafeSyncApiResult<SafeActivitySessionsListResponse>.Success(new SafeActivitySessionsListResponse(), 200);
            public Task<SafeSyncApiResult<SafeSyncHealthResponse>> GetHealthAsync(CancellationToken cancellationToken = default) => Task.FromResult(SafeSyncApiResult<SafeSyncHealthResponse>.Success(new SafeSyncHealthResponse { SchemaVersion = 1 }, 200));
            public Task<SafeSyncApiResult<SafeActivitySessionsUpsertResponse>> PostActivitySessionsAsync(SafeActivitySessionsContractRequest request, CancellationToken cancellationToken = default) => Task.FromResult(SafeSyncApiResult<SafeActivitySessionsUpsertResponse>.Success(new SafeActivitySessionsUpsertResponse { Success = true, AcceptedCount = request.Sessions.Count, SchemaVersion = 1 }, 201));
            public Task<SafeSyncApiResult<SafeActivitySessionsListResponse>> GetActivitySessionsAsync(CancellationToken cancellationToken = default) => Task.FromResult(FetchResult);
            public Task<SafeSyncApiResult<SafeActivitySessionDeleteResponse>> DeleteActivitySessionAsync(string serverSessionId, CancellationToken cancellationToken = default) => Task.FromResult(SafeSyncApiResult<SafeActivitySessionDeleteResponse>.Success(new SafeActivitySessionDeleteResponse { Success = true }, 200));
        }

        [Test]
        public void KeepLocalQueuesSafeReuploadWithoutSendingImmediately()
        {
            var directory = TempDirectory();
            var conflictRepository = new SafeSyncConflictRepository(directory);
            RunAsync(() => conflictRepository.SaveAsync(new SafeSyncConflictState { Conflicts = new List<SafeSyncConflict> { Conflict("conflict-a", "session-a", SafeSyncConflictType.RemoteDifferent) } }, CancellationToken.None));
            var service = CreateService(new FakeRepository(), new FakeApiClient(), directory);

            var result = RunAsync(() => service.KeepLocalConflictAsync("conflict-a", CancellationToken.None));
            var queue = RunAsync(() => new SafeSyncRetryQueueRepository(directory).LoadAsync(CancellationToken.None));
            var conflicts = RunAsync(() => conflictRepository.LoadAsync(CancellationToken.None));

            Assert.IsTrue(result.IsSuccess, result.ErrorCode);
            Assert.AreEqual(SafeSyncApiError.ConflictKeepLocalQueued, result.ErrorCode);
            Assert.AreEqual(SafeSyncRetryOperationType.UPSERT_ACTIVITY_SESSIONS, queue.Entries.Single().OperationType);
            Assert.AreEqual("session-a", queue.Entries.Single().ClientSessionIds.Single());
            Assert.AreEqual(SafeSyncConflictResolutionStatus.KeepLocal, conflicts.Conflicts.Single().ResolutionStatus);
        }

        [Test]
        public void KeepRemoteAndMarkResolvedAreSafeMarkersOnly()
        {
            var directory = TempDirectory();
            var repository = new FakeRepository();
            var conflictRepository = new SafeSyncConflictRepository(directory);
            RunAsync(() => conflictRepository.SaveAsync(new SafeSyncConflictState
            {
                Conflicts = new List<SafeSyncConflict>
                {
                    Conflict("conflict-remote", "session-a", SafeSyncConflictType.RemoteDifferent),
                    Conflict("conflict-mark", "session-a", SafeSyncConflictType.ValidationConflict)
                }
            }, CancellationToken.None));
            var service = CreateService(repository, new FakeApiClient(), directory);

            var keepRemote = RunAsync(() => service.KeepRemoteConflictAsync("conflict-remote", CancellationToken.None));
            var markResolved = RunAsync(() => service.MarkConflictResolvedAsync("conflict-mark", CancellationToken.None));
            var conflicts = RunAsync(() => conflictRepository.LoadAsync(CancellationToken.None));

            Assert.IsTrue(keepRemote.IsSuccess, keepRemote.ErrorCode);
            Assert.IsTrue(markResolved.IsSuccess, markResolved.ErrorCode);
            Assert.AreEqual(1, repository.Current.WorkSessionSummaries.Count);
            Assert.AreEqual(SafeSyncConflictResolutionStatus.KeepRemote, conflicts.Conflicts.Single(item => item.ConflictId == "conflict-remote").ResolutionStatus);
            Assert.AreEqual(SafeSyncConflictResolutionStatus.MarkResolved, conflicts.Conflicts.Single(item => item.ConflictId == "conflict-mark").ResolutionStatus);
        }

        [Test]
        public void CancelResolutionLeavesConflictUnresolved()
        {
            var directory = TempDirectory();
            var conflictRepository = new SafeSyncConflictRepository(directory);
            RunAsync(() => conflictRepository.SaveAsync(new SafeSyncConflictState { Conflicts = new List<SafeSyncConflict> { Conflict("conflict-a", "session-a", SafeSyncConflictType.Unknown) } }, CancellationToken.None));
            var service = CreateService(new FakeRepository(), new FakeApiClient(), directory);

            var result = RunAsync(() => service.CancelConflictResolutionAsync("conflict-a", CancellationToken.None));
            var conflict = RunAsync(() => conflictRepository.LoadAsync(CancellationToken.None)).Conflicts.Single();

            Assert.IsTrue(result.IsSuccess, result.ErrorCode);
            Assert.AreEqual(SafeSyncConflictResolutionStatus.Unresolved, conflict.ResolutionStatus);
        }

        [Test]
        public void KeepLocalReturnsSafeErrorWhenLocalSessionMissing()
        {
            var directory = TempDirectory();
            var conflictRepository = new SafeSyncConflictRepository(directory);
            RunAsync(() => conflictRepository.SaveAsync(new SafeSyncConflictState { Conflicts = new List<SafeSyncConflict> { Conflict("conflict-a", "missing-session", SafeSyncConflictType.RemoteDifferent) } }, CancellationToken.None));
            var service = CreateService(new FakeRepository(), new FakeApiClient(), directory);

            var result = RunAsync(() => service.KeepLocalConflictAsync("conflict-a", CancellationToken.None));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(SafeSyncApiError.LocalSessionMissing, result.ErrorCode);
        }

        [Test]
        public void FetchRemoteSessionsDetectsSafeMetadataConflicts()
        {
            var directory = TempDirectory();
            var api = new FakeApiClient
            {
                FetchResult = SafeSyncApiResult<SafeActivitySessionsListResponse>.Success(new SafeActivitySessionsListResponse
                {
                    Sessions = new List<RemoteSafeActivitySessionDto>
                    {
                        new RemoteSafeActivitySessionDto
                        {
                            Id = "server-a",
                            ClientSessionId = "session-a",
                            SourceProvider = "CODEX",
                            DayBucket = "2026-05-16",
                            ActivityCategory = "Feature",
                            Confidence = "HIGH",
                            AggregateSchemaVersion = 1
                        },
                        new RemoteSafeActivitySessionDto
                        {
                            Id = "server-missing-local",
                            ClientSessionId = "session-missing-local",
                            SourceProvider = "CODEX",
                            DayBucket = "2026-05-15",
                            Confidence = "HIGH",
                            AggregateSchemaVersion = 1
                        }
                    }
                }, 200)
            };
            var service = CreateService(new FakeRepository(), api, directory);

            var result = RunAsync(() => service.FetchRemoteSessionsAsync(CancellationToken.None));
            var conflicts = RunAsync(() => new SafeSyncConflictRepository(directory).LoadAsync(CancellationToken.None)).Conflicts;

            Assert.IsTrue(result.IsSuccess, result.ErrorCode);
            Assert.IsTrue(conflicts.Any(conflict => conflict.ClientSessionId == "session-a" && conflict.ConflictType == SafeSyncConflictType.RemoteDifferent));
            Assert.IsTrue(conflicts.Any(conflict => conflict.ClientSessionId == "session-missing-local" && conflict.ConflictType == SafeSyncConflictType.LocalMissing));
        }

        [Test]
        public void ConflictFileContainsNoForbiddenPrivateFields()
        {
            var directory = TempDirectory();
            var conflictRepository = new SafeSyncConflictRepository(directory);
            RunAsync(() => conflictRepository.SaveAsync(new SafeSyncConflictState { Conflicts = new List<SafeSyncConflict> { Conflict("conflict-a", "session-a", SafeSyncConflictType.ValidationConflict) } }, CancellationToken.None));

            var json = File.ReadAllText(conflictRepository.FilePath);
            foreach (var forbidden in new[] { "rawPath", "approvedLocation", "prompt", "response", "command", "fileName", "repoName", "branchName", "token", "rawLog", "/Users/" })
            {
                Assert.That(json, Does.Not.Contain(forbidden), forbidden);
            }
        }

        private static SafeSyncConflict Conflict(string conflictId, string clientSessionId, SafeSyncConflictType type)
        {
            return new SafeSyncConflict
            {
                ConflictId = conflictId,
                ClientSessionId = clientSessionId,
                ServerSessionId = "server-" + clientSessionId,
                ConflictType = type,
                SafeErrorCode = SafeSyncApiError.ConflictDetected,
                SafeLocalSummary = new SafeSyncSessionSafeSummary { ClientSessionId = clientSessionId, DayBucket = "2026-05-15", SourceProvider = "CODEX", ActivityCategory = "Feature", Confidence = "HIGH" },
                SafeRemoteSummary = new SafeSyncSessionSafeSummary { ClientSessionId = clientSessionId, DayBucket = "2026-05-16", SourceProvider = "CODEX", ActivityCategory = "Feature", Confidence = "HIGH" }
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

        private static SaveData CreateSaveData(string id)
        {
            var saveData = SaveData.CreateDefault();
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
