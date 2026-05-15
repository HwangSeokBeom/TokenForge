using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TokenForge.Client.Common;
using TokenForge.Client.Domain;
using TokenForge.Client.Persistence;
using TokenForge.Client.Sync;

namespace TokenForge.Client.Tests
{
    public sealed class Phase13LocalDataPreservationTests
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

        private sealed class FakeSafeSyncApiClient : ISafeSyncApiClient
        {
            public SafeSyncApiConfig Config { get; } = new SafeSyncApiConfig("http://localhost:3000/api/v1");
            public int PostCount { get; private set; }
            public SafeSyncApiResult<SafeActivitySessionsUpsertResponse> PostResult { get; set; } =
                SafeSyncApiResult<SafeActivitySessionsUpsertResponse>.Failure(SafeSyncApiError.ServerUnavailable, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.ServerUnavailable), 503);
            public Task<SafeSyncApiResult<SafeSyncHealthResponse>> GetHealthAsync(CancellationToken cancellationToken = default) => Task.FromResult(SafeSyncApiResult<SafeSyncHealthResponse>.Success(new SafeSyncHealthResponse(), 200));
            public Task<SafeSyncApiResult<SafeActivitySessionsUpsertResponse>> PostActivitySessionsAsync(SafeActivitySessionsContractRequest request, CancellationToken cancellationToken = default)
            {
                PostCount += 1;
                return Task.FromResult(PostResult);
            }
            public Task<SafeSyncApiResult<SafeActivitySessionsListResponse>> GetActivitySessionsAsync(CancellationToken cancellationToken = default) => Task.FromResult(SafeSyncApiResult<SafeActivitySessionsListResponse>.Success(new SafeActivitySessionsListResponse(), 200));
            public Task<SafeSyncApiResult<SafeActivitySessionDeleteResponse>> DeleteActivitySessionAsync(string serverSessionId, CancellationToken cancellationToken = default) => Task.FromResult(SafeSyncApiResult<SafeActivitySessionDeleteResponse>.Success(new SafeActivitySessionDeleteResponse(), 200));
        }

        [Test]
        public void SyncFailureDoesNotDeleteOrSaveLocalSessions()
        {
            var repository = new FakeRepository();
            var service = new SafeSyncService(repository, new FakeSafeSyncApiClient());

            var result = RunAsync(() => service.SyncNowAsync(CancellationToken.None));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(0, repository.SaveCount);
            Assert.AreEqual(1, repository.Current.WorkSessionSummaries.Count);
        }

        [Test]
        public void InvalidBaseUrlFailsBeforeNetworkAndPreservesLocalSessions()
        {
            var repository = new FakeRepository();
            var api = new FakeSafeSyncApiClient();
            var service = new SafeSyncService(repository, api);

            service.SetBaseUrl("not a url");
            var result = RunAsync(() => service.SyncNowAsync(CancellationToken.None));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(SafeSyncApiError.InvalidBaseUrl, result.ErrorCode);
            Assert.AreEqual(0, api.PostCount);
            Assert.AreEqual(0, repository.SaveCount);
            Assert.AreEqual(1, repository.Current.WorkSessionSummaries.Count);
        }

        [Test]
        public void PrivacyGuardBlockDoesNotDeleteLocalSessions()
        {
            var repository = new FakeRepository();
            var api = new FakeSafeSyncApiClient();
            var service = new SafeSyncService(
                repository,
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
                            ClientSessionId = "session-safe",
                            SourceProvider = "GIT",
                            DayBucket = "2026-05-15",
                            Confidence = "HIGH",
                            AnalyzerVersion = "raw path /Users/private/project"
                        }
                    }
                });

            var result = RunAsync(() => service.SyncNowAsync(CancellationToken.None));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(SafeSyncApiError.PrivacyGuardBlockedPayload, result.ErrorCode);
            Assert.AreEqual(0, api.PostCount);
            Assert.AreEqual(1, repository.Current.WorkSessionSummaries.Count);
        }

        private static SaveData CreateSaveData()
        {
            var saveData = SaveData.CreateDefault();
            saveData.WorkSessionSummaries.Add(new AgentWorkSession
            {
                SessionId = "session-safe",
                SourceProvider = "CODEX",
                StartedAt = DateTimeOffset.UtcNow.AddHours(-1),
                EndedAt = DateTimeOffset.UtcNow,
                Confidence = ProviderConfidence.High,
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
