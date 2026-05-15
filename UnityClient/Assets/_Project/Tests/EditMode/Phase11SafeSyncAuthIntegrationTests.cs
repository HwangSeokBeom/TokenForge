using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TokenForge.Client.Auth;
using TokenForge.Client.Common;
using TokenForge.Client.Domain;
using TokenForge.Client.Persistence;
using TokenForge.Client.Sync;

namespace TokenForge.Client.Tests
{
    public sealed class Phase11SafeSyncAuthIntegrationTests
    {
        private sealed class FakeTransport : ITokenForgeHttpTransport
        {
            private readonly Queue<SafeHttpResponse> responses = new Queue<SafeHttpResponse>();
            public List<SafeHttpRequest> Requests { get; } = new List<SafeHttpRequest>();

            public void Enqueue(SafeHttpResponse response)
            {
                responses.Enqueue(response);
            }

            public Task<SafeHttpResponse> SendAsync(SafeHttpRequest request, CancellationToken cancellationToken)
            {
                Requests.Add(new SafeHttpRequest
                {
                    Method = request.Method,
                    Url = request.Url,
                    EndpointName = request.EndpointName,
                    BodyJson = request.BodyJson,
                    TimeoutSeconds = request.TimeoutSeconds,
                    BearerToken = request.BearerToken
                });
                return Task.FromResult(responses.Count > 0 ? responses.Dequeue() : new SafeHttpResponse { StatusCode = 200, BodyJson = "{}" });
            }
        }

        private sealed class RefreshingProvider : IAuthTokenRefreshProvider
        {
            private readonly string initialToken;
            private readonly string refreshedToken;
            public int RefreshCount { get; private set; }

            public RefreshingProvider(string initialToken, string refreshedToken)
            {
                this.initialToken = initialToken;
                this.refreshedToken = refreshedToken;
            }

            public Task<string> GetBearerTokenAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(initialToken);
            }

            public Task<string> RefreshBearerTokenAsync(CancellationToken cancellationToken)
            {
                RefreshCount += 1;
                return Task.FromResult(refreshedToken);
            }
        }

        private sealed class FakeRepository : ILocalSaveDataRepository
        {
            public int SaveCount { get; private set; }
            public SaveData Current { get; set; } = SaveData.CreateDefault();
            public Task<SaveData> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Current);
            public Task<Result> SaveAsync(SaveData saveData, CancellationToken cancellationToken = default)
            {
                SaveCount += 1;
                Current = saveData;
                return Task.FromResult(Result.Success());
            }
        }

        [Test]
        public void SafeSyncAttachesBearerTokenFromAuthSessionProvider()
        {
            var transport = new FakeTransport();
            transport.Enqueue(new SafeHttpResponse { StatusCode = 201, BodyJson = "{\"success\":true,\"acceptedCount\":1,\"rejectedCount\":0,\"results\":[],\"schemaVersion\":1}" });
            var client = new SafeSyncApiClient(new SafeSyncApiConfig("http://localhost:3000/api/v1"), new StaticAuthTokenProvider("access-secret"), transport, new InMemorySafeSyncLogger());

            var result = RunAsync(() => client.PostActivitySessionsAsync(CreateRequest(), CancellationToken.None));

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("access-secret", transport.Requests.Single().BearerToken);
        }

        [Test]
        public void NoTokenReturnsAuthRequiredWithoutSending()
        {
            var transport = new FakeTransport();
            var client = new SafeSyncApiClient(new SafeSyncApiConfig("http://localhost:3000/api/v1"), new NullAuthTokenProvider(), transport, new InMemorySafeSyncLogger());

            var result = RunAsync(() => client.GetActivitySessionsAsync(CancellationToken.None));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(SafeSyncApiError.AuthRequired, result.ErrorCode);
            Assert.AreEqual(0, transport.Requests.Count);
        }

        [Test]
        public void AuthRejectedRefreshesAndRetriesOriginalRequestOnce()
        {
            var transport = new FakeTransport();
            transport.Enqueue(new SafeHttpResponse { StatusCode = 401, BodyJson = "{\"errorCode\":\"AUTH_REQUIRED\"}" });
            transport.Enqueue(new SafeHttpResponse { StatusCode = 201, BodyJson = "{\"success\":true,\"acceptedCount\":1,\"rejectedCount\":0,\"results\":[],\"schemaVersion\":1}" });
            var provider = new RefreshingProvider("expired-token", "fresh-token");
            var client = new SafeSyncApiClient(new SafeSyncApiConfig("http://localhost:3000/api/v1"), provider, transport, new InMemorySafeSyncLogger());

            var result = RunAsync(() => client.PostActivitySessionsAsync(CreateRequest(), CancellationToken.None));

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, provider.RefreshCount);
            Assert.AreEqual(2, transport.Requests.Count);
            Assert.AreEqual("expired-token", transport.Requests[0].BearerToken);
            Assert.AreEqual("fresh-token", transport.Requests[1].BearerToken);
        }

        [Test]
        public void AuthFailureDoesNotDeleteOrSaveLocalData()
        {
            var repository = new FakeRepository { Current = CreateSaveData() };
            var api = new FakeApiClient();
            var service = new SafeSyncService(repository, api);

            var result = RunAsync(() => service.SyncNowAsync(CancellationToken.None));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(SafeSyncStatus.AuthRequired, result.Status);
            Assert.AreEqual(0, repository.SaveCount);
            Assert.AreEqual(1, repository.Current.WorkSessionSummaries.Count);
        }

        [Test]
        public void OutgoingSyncPayloadStillRunsThroughPrivacyGuard()
        {
            var api = new FakeApiClient { Result = SafeSyncApiResult<SafeActivitySessionsUpsertResponse>.Success(new SafeActivitySessionsUpsertResponse(), 200) };
            var service = new SafeSyncService(new FakeRepository { Current = CreateSaveData() }, api, null, null, _ => new SafeActivitySessionsContractRequest
            {
                SchemaVersion = 1,
                Sessions = new List<SafeActivitySessionContractDto>
                {
                    new SafeActivitySessionContractDto { ClientSessionId = "s1", SourceProvider = "GIT", DayBucket = "2026-05-14", Confidence = "HIGH", AnalyzerVersion = "/Users/private/path" }
                }
            });

            var result = RunAsync(() => service.SyncNowAsync(CancellationToken.None));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(SafeSyncApiError.PrivacyGuardBlockedPayload, result.ErrorCode);
            Assert.AreEqual(0, api.PostCount);
        }

        private sealed class FakeApiClient : ISafeSyncApiClient
        {
            public SafeSyncApiConfig Config { get; } = new SafeSyncApiConfig("http://localhost:3000/api/v1");
            public int PostCount { get; private set; }
            public SafeSyncApiResult<SafeActivitySessionsUpsertResponse> Result { get; set; } =
                SafeSyncApiResult<SafeActivitySessionsUpsertResponse>.Failure(SafeSyncApiError.AuthRequired, "auth required", 401);
            public Task<SafeSyncApiResult<SafeSyncHealthResponse>> GetHealthAsync(CancellationToken cancellationToken = default) => Task.FromResult(SafeSyncApiResult<SafeSyncHealthResponse>.Success(new SafeSyncHealthResponse(), 200));
            public Task<SafeSyncApiResult<SafeActivitySessionsUpsertResponse>> PostActivitySessionsAsync(SafeActivitySessionsContractRequest request, CancellationToken cancellationToken = default)
            {
                PostCount += 1;
                return Task.FromResult(Result);
            }
            public Task<SafeSyncApiResult<SafeActivitySessionsListResponse>> GetActivitySessionsAsync(CancellationToken cancellationToken = default) => Task.FromResult(SafeSyncApiResult<SafeActivitySessionsListResponse>.Failure(SafeSyncApiError.AuthRequired, "auth required", 401));
            public Task<SafeSyncApiResult<SafeActivitySessionDeleteResponse>> DeleteActivitySessionAsync(string serverSessionId, CancellationToken cancellationToken = default) => Task.FromResult(SafeSyncApiResult<SafeActivitySessionDeleteResponse>.Failure(SafeSyncApiError.AuthRequired, "auth required", 401));
        }

        private static SafeActivitySessionsContractRequest CreateRequest()
        {
            return new SafeActivitySessionsContractRequest
            {
                SchemaVersion = 1,
                ClientSyncId = "sync-1",
                Sessions = new List<SafeActivitySessionContractDto>
                {
                    new SafeActivitySessionContractDto { ClientSessionId = "session-1", SourceProvider = "GIT", DayBucket = "2026-05-14", Confidence = "HIGH", ChangeCountBucket = "FEW" }
                }
            };
        }

        private static SaveData CreateSaveData()
        {
            var saveData = SaveData.CreateDefault();
            saveData.WorkSessionSummaries.Add(new AgentWorkSession
            {
                SessionId = "safe-session",
                SourceProvider = "GIT",
                StartedAt = new DateTimeOffset(2026, 5, 14, 10, 0, 0, TimeSpan.Zero),
                EndedAt = new DateTimeOffset(2026, 5, 14, 11, 0, 0, TimeSpan.Zero),
                Confidence = ProviderConfidence.High,
                GitChangeSummary = new GitChangeSummary { ChangedFileCount = 1, AddedLineBucket = LineChangeBucket.Small }
            });
            return saveData;
        }

        private static T RunAsync<T>(Func<Task<T>> taskFactory)
        {
            return Task.Run(taskFactory).GetAwaiter().GetResult();
        }
    }
}
