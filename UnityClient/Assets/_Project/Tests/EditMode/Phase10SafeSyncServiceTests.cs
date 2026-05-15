using System;
using System.Collections.Generic;
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
    public sealed class Phase10SafeSyncServiceTests
    {
        private sealed class FakeRepository : ILocalSaveDataRepository
        {
            public SaveData Current { get; set; } = SaveData.CreateDefault();
            public int SaveCount { get; private set; }

            public Task<SaveData> LoadAsync(CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(Current);
            }

            public Task<Result> SaveAsync(SaveData saveData, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SaveCount += 1;
                Current = saveData;
                return Task.FromResult(Result.Success());
            }
        }

        private sealed class FakeApiClient : ISafeSyncApiClient
        {
            public SafeSyncApiConfig Config { get; } = new SafeSyncApiConfig("http://localhost:3000/api/v1");
            public SafeActivitySessionsContractRequest LastRequest { get; private set; }
            public int PostCount { get; private set; }
            public int FetchCount { get; private set; }
            public int DeleteCount { get; private set; }
            public SafeSyncApiResult<SafeActivitySessionsUpsertResponse> PostResult { get; set; } =
                SafeSyncApiResult<SafeActivitySessionsUpsertResponse>.Success(new SafeActivitySessionsUpsertResponse { Success = true, AcceptedCount = 1, RejectedCount = 0, SchemaVersion = 1 }, 201);
            public SafeSyncApiResult<SafeActivitySessionsListResponse> FetchResult { get; set; }
            public SafeSyncApiResult<SafeActivitySessionDeleteResponse> DeleteResult { get; set; } =
                SafeSyncApiResult<SafeActivitySessionDeleteResponse>.Success(new SafeActivitySessionDeleteResponse { Success = true }, 200);

            public Task<SafeSyncApiResult<SafeSyncHealthResponse>> GetHealthAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(SafeSyncApiResult<SafeSyncHealthResponse>.Success(new SafeSyncHealthResponse { Status = "ok", SchemaVersion = 1 }, 200));
            }

            public Task<SafeSyncApiResult<SafeActivitySessionsUpsertResponse>> PostActivitySessionsAsync(SafeActivitySessionsContractRequest request, CancellationToken cancellationToken = default)
            {
                PostCount += 1;
                LastRequest = request;
                return Task.FromResult(PostResult);
            }

            public Task<SafeSyncApiResult<SafeActivitySessionsListResponse>> GetActivitySessionsAsync(CancellationToken cancellationToken = default)
            {
                FetchCount += 1;
                return Task.FromResult(FetchResult ?? SafeSyncApiResult<SafeActivitySessionsListResponse>.Success(new SafeActivitySessionsListResponse(), 200));
            }

            public Task<SafeSyncApiResult<SafeActivitySessionDeleteResponse>> DeleteActivitySessionAsync(string serverSessionId, CancellationToken cancellationToken = default)
            {
                DeleteCount += 1;
                return Task.FromResult(DeleteResult);
            }
        }

        [Test]
        public void Constructor_DoesNotAutoSync()
        {
            var api = new FakeApiClient();
            _ = new SafeSyncService(new FakeRepository { Current = CreateSafeSaveData() }, api);

            Assert.AreEqual(0, api.PostCount);
            Assert.AreEqual(0, api.FetchCount);
        }

        [Test]
        public void SyncNow_MapsUsingSafeSyncMapper_AndSendsOnlyAggregatePayload()
        {
            var repository = new FakeRepository { Current = CreateSafeSaveData() };
            var api = new FakeApiClient();
            var service = new SafeSyncService(repository, api);

            var result = RunAsync(() => service.SyncNowAsync(CancellationToken.None));

            Assert.IsTrue(result.IsSuccess, result.ErrorCode);
            Assert.AreEqual(1, api.PostCount);
            Assert.AreEqual(1, api.LastRequest.SchemaVersion);
            Assert.AreEqual("CODEX", api.LastRequest.Sessions[0].SourceProvider);
            var serialized = SafeSyncContractBundleExporter.ToJson(api.LastRequest);
            Assert.IsFalse(serialized.Contains("approved"));
            Assert.IsFalse(serialized.Contains("rawPath"));
            Assert.IsFalse(serialized.Contains("prompt"));
            Assert.IsFalse(serialized.Contains("response"));
            Assert.IsFalse(serialized.Contains("command"));
            Assert.IsFalse(serialized.Contains("rawLog"));
            Assert.IsFalse(serialized.Contains("token"));
        }

        [Test]
        public void BlocksUnsafeOutgoingPayloadBeforeApiCall()
        {
            var api = new FakeApiClient();
            var service = new SafeSyncService(
                new FakeRepository { Current = CreateSafeSaveData() },
                api,
                null,
                null,
                _ => new SafeActivitySessionsContractRequest
                {
                    SchemaVersion = 1,
                    Sessions = new List<SafeActivitySessionContractDto>
                    {
                        new SafeActivitySessionContractDto
                        {
                            ClientSessionId = "session-1",
                            SourceProvider = "GIT",
                            DayBucket = "2026-05-14",
                            Confidence = "HIGH",
                            AnalyzerVersion = "prompt: do not send"
                        }
                    }
                });

            var result = RunAsync(() => service.SyncNowAsync(CancellationToken.None));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(SafeSyncApiError.PrivacyGuardBlockedPayload, result.ErrorCode);
            Assert.AreEqual(0, api.PostCount);
        }

        [Test]
        public void ServerUnavailable_DoesNotDeleteOrSaveLocalData()
        {
            var repository = new FakeRepository { Current = CreateSafeSaveData() };
            var api = new FakeApiClient
            {
                PostResult = SafeSyncApiResult<SafeActivitySessionsUpsertResponse>.Failure(SafeSyncApiError.ServerUnavailable, "down", 503)
            };
            var service = new SafeSyncService(repository, api);

            var result = RunAsync(() => service.SyncNowAsync(CancellationToken.None));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(SafeSyncStatus.ServerUnavailable, result.Status);
            Assert.AreEqual(0, repository.SaveCount);
            Assert.AreEqual(1, repository.Current.WorkSessionSummaries.Count);
        }

        [Test]
        public void FetchRemoteSessions_ConvertsToSafeSummaries_AndDeleteUsesServerId()
        {
            var api = new FakeApiClient
            {
                FetchResult = SafeSyncApiResult<SafeActivitySessionsListResponse>.Success(new SafeActivitySessionsListResponse
                {
                    SchemaVersion = 1,
                    Sessions = new List<RemoteSafeActivitySessionDto>
                    {
                        new RemoteSafeActivitySessionDto
                        {
                            Id = "11111111-1111-4111-8111-111111111111",
                            ClientSessionId = "client-1",
                            SourceProvider = "GIT",
                            DayBucket = "2026-05-14",
                            Confidence = "HIGH",
                            ActivityCategory = "WORK_FEATURE",
                            ChangeCountBucket = "FEW",
                            AggregateSchemaVersion = 1
                        }
                    }
                }, 200)
            };
            var service = new SafeSyncService(new FakeRepository { Current = CreateSafeSaveData() }, api);

            var fetch = RunAsync(() => service.FetchRemoteSessionsAsync(CancellationToken.None));
            var delete = RunAsync(() => service.DeleteRemoteSessionAsync(fetch.RemoteSessions.Single().ServerSessionId, CancellationToken.None));

            Assert.IsTrue(fetch.IsSuccess);
            Assert.AreEqual("GIT", fetch.RemoteSessions.Single().SourceProvider);
            Assert.IsTrue(delete.IsSuccess);
            Assert.AreEqual(1, api.DeleteCount);
        }

        private static SaveData CreateSafeSaveData()
        {
            var saveData = SaveData.CreateDefault();
            saveData.WorkSessionSummaries.Add(new AgentWorkSession
            {
                SessionId = "session-safe",
                AgentType = AgentType.Codex,
                SourceProvider = "CODEX",
                WorkType = WorkType.Feature,
                StartedAt = new DateTimeOffset(2026, 5, 14, 10, 0, 0, TimeSpan.Zero),
                EndedAt = new DateTimeOffset(2026, 5, 14, 11, 0, 0, TimeSpan.Zero),
                Confidence = ProviderConfidence.High,
                GitChangeSummary = new GitChangeSummary
                {
                    ProjectPathHash = "1234567890abcdef",
                    ChangedFileCount = 3,
                    AddedLineBucket = LineChangeBucket.Medium,
                    DeletedLineBucket = LineChangeBucket.Small
                },
                AgentActivitySummary = new AgentActivitySummary
                {
                    DayBucket = "2026-05-14",
                    SessionCountBucket = CountBucket.One,
                    InteractionCountBucket = CountBucket.Small
                },
                UserReviewed = true
            });
            return saveData;
        }

        private static T RunAsync<T>(Func<Task<T>> taskFactory)
        {
            return Task.Run(taskFactory).GetAwaiter().GetResult();
        }
    }
}
