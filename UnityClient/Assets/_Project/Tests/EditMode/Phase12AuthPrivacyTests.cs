using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TokenForge.Client.Agents;
using TokenForge.Client.Auth;
using TokenForge.Client.Common;
using TokenForge.Client.Domain;
using TokenForge.Client.Git;
using TokenForge.Client.Persistence;
using TokenForge.Client.Platform;
using TokenForge.Client.Sync;
using TokenForge.Client.UI;

namespace TokenForge.Client.Tests
{
    public sealed class Phase12AuthPrivacyTests
    {
        private sealed class FakeTransport : ITokenForgeHttpTransport
        {
            public List<SafeHttpRequest> Requests { get; } = new List<SafeHttpRequest>();
            public SafeHttpResponse Response { get; set; } = new SafeHttpResponse
            {
                StatusCode = 401,
                BodyJson = "{\"errorCode\":\"INVALID_CREDENTIALS\",\"message\":\"password123 access-token refresh-token\"}"
            };

            public Task<SafeHttpResponse> SendAsync(SafeHttpRequest request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return Task.FromResult(Response);
            }
        }

        private sealed class FakeAuthSessionService : IAuthSessionService
        {
            public AuthState State { get; private set; } = AuthState.LoggedIn;
            public AuthSession CurrentSession { get; private set; } = new AuthSession
            {
                UserId = "user-1",
                Email = "test@example.com",
                DisplayName = "Tester",
                AccessToken = "access-token-secret",
                RefreshToken = "refresh-token-secret",
                AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15)
            };
            public string ErrorCode { get; private set; } = string.Empty;
            public string BaseUrl { get; private set; } = "http://localhost:3000/api/v1";
            public bool HasUsableAccessToken => true;
            public void SetBaseUrl(string baseUrl) => BaseUrl = baseUrl;
            public Task<AuthResult> LoadSessionAsync(CancellationToken cancellationToken = default) => Task.FromResult(AuthResult.Success(State));
            public Task<AuthResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default) => Task.FromResult(AuthResult.Success(State));
            public Task<AuthResult> SignupAsync(string email, string password, string displayName = "", CancellationToken cancellationToken = default) => Task.FromResult(AuthResult.Success(State));
            public Task<AuthResult> LoadCurrentUserAsync(CancellationToken cancellationToken = default) => Task.FromResult(AuthResult.Success(State));
            public Task<AuthResult> LogoutAsync(CancellationToken cancellationToken = default)
            {
                CurrentSession = null;
                State = AuthState.LoggedOut;
                return Task.FromResult(AuthResult.Success(State));
            }
            public Task<string> GetAccessTokenForSyncAsync(CancellationToken cancellationToken = default) => Task.FromResult("access-token-secret");
            public Task<string> ForceRefreshForRetryAsync(CancellationToken cancellationToken = default) => Task.FromResult("fresh-access-token-secret");
        }

        private sealed class FakeSafeSyncService : ISafeSyncService
        {
            public SafeSyncStatus Status { get; private set; } = SafeSyncStatus.Idle;
            public string BaseUrl { get; private set; } = "http://localhost:3000/api/v1";
            public void SetBaseUrl(string baseUrl) => BaseUrl = baseUrl;
            public Task<SafeSyncResult> CheckHealthAsync(CancellationToken cancellationToken = default) => Task.FromResult(SafeSyncResult.Success(SafeSyncStatus.Ready));
            public Task<SafeSyncResult> SyncNowAsync(CancellationToken cancellationToken = default) => Task.FromResult(SafeSyncResult.Success(SafeSyncStatus.Synced));
            public Task<SafeSyncResult> FetchRemoteSessionsAsync(CancellationToken cancellationToken = default) => Task.FromResult(SafeSyncResult.Success(SafeSyncStatus.Ready));
            public Task<SafeSyncResult> DeleteRemoteSessionAsync(string serverSessionId, CancellationToken cancellationToken = default) => Task.FromResult(SafeSyncResult.Success(SafeSyncStatus.Ready));
        }

        private sealed class FakeRepository : ILocalSaveDataRepository
        {
            public SaveData Current { get; set; } = CreateSafeSaveData("phase12-live-test-local");
            public Task<SaveData> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Current);
            public Task<Result> SaveAsync(SaveData saveData, CancellationToken cancellationToken = default)
            {
                Current = saveData;
                return Task.FromResult(Result.Success());
            }
        }

        private sealed class NullRepositoryPicker : IRepositoryPicker
        {
            public Task<RepositoryPickerResult> PickRepositoryAsync(CancellationToken cancellationToken = default) => Task.FromResult(RepositoryPickerResult.Unavailable());
        }

        private sealed class NullAgentLogLocationPicker : IAgentLogLocationPicker
        {
            public Task<AgentLogLocationPickerResult> PickAgentLogLocationAsync(CancellationToken cancellationToken = default) => Task.FromResult(AgentLogLocationPickerResult.Unavailable());
        }

        private sealed class CapturingSafeSyncApiClient : ISafeSyncApiClient
        {
            public SafeSyncApiConfig Config { get; } = new SafeSyncApiConfig("http://localhost:3000/api/v1");
            public SafeActivitySessionsContractRequest LastRequest { get; private set; }
            public int PostCount { get; private set; }
            public Task<SafeSyncApiResult<SafeSyncHealthResponse>> GetHealthAsync(CancellationToken cancellationToken = default) => Task.FromResult(SafeSyncApiResult<SafeSyncHealthResponse>.Success(new SafeSyncHealthResponse(), 200));
            public Task<SafeSyncApiResult<SafeActivitySessionsUpsertResponse>> PostActivitySessionsAsync(SafeActivitySessionsContractRequest request, CancellationToken cancellationToken = default)
            {
                PostCount += 1;
                LastRequest = request;
                return Task.FromResult(SafeSyncApiResult<SafeActivitySessionsUpsertResponse>.Success(new SafeActivitySessionsUpsertResponse { Success = true, AcceptedCount = request.Sessions.Count, SchemaVersion = 1 }, 201));
            }
            public Task<SafeSyncApiResult<SafeActivitySessionsListResponse>> GetActivitySessionsAsync(CancellationToken cancellationToken = default) => Task.FromResult(SafeSyncApiResult<SafeActivitySessionsListResponse>.Success(new SafeActivitySessionsListResponse(), 200));
            public Task<SafeSyncApiResult<SafeActivitySessionDeleteResponse>> DeleteActivitySessionAsync(string serverSessionId, CancellationToken cancellationToken = default) => Task.FromResult(SafeSyncApiResult<SafeActivitySessionDeleteResponse>.Success(new SafeActivitySessionDeleteResponse(), 200));
        }

        [Test]
        public void AuthUiStateNeverExposesPasswordOrToken()
        {
            var viewModel = CreateViewModel(new FakeRepository(), new FakeSafeSyncService(), new FakeAuthSessionService());

            var status = viewModel.AuthStatusMessage + " " + viewModel.AuthUserSummary;

            Assert.IsFalse(status.Contains("password123"));
            Assert.IsFalse(status.Contains("access-token-secret"));
            Assert.IsFalse(status.Contains("refresh-token-secret"));
        }

        [Test]
        public void AuthLogsNeverIncludePasswordTokenOrResponseBody()
        {
            var transport = new FakeTransport();
            var logger = new InMemorySafeSyncLogger();
            var client = new AuthApiClient(new AuthApiConfig("http://localhost:3000/api/v1"), transport, logger);

            var result = RunAsync(() => client.LoginAsync("test@example.com", "password123", CancellationToken.None));

            Assert.IsFalse(result.IsSuccess);
            var logs = string.Join("\n", logger.Entries);
            Assert.IsFalse(logs.Contains("password123"));
            Assert.IsFalse(logs.Contains("access-token"));
            Assert.IsFalse(logs.Contains("refresh-token"));
            Assert.IsFalse(logs.Contains(transport.Response.BodyJson));
        }

        [Test]
        public void ManualLiveTestEnvironmentSummaryRedactsSensitiveValues()
        {
            var summary = Phase12LiveIntegrationSettings.RedactedEnvironmentSummary();

            var password = Environment.GetEnvironmentVariable(Phase12LiveIntegrationSettings.Password);
            var signupPassword = Environment.GetEnvironmentVariable(Phase12LiveIntegrationSettings.SignupPassword);
            if (!string.IsNullOrWhiteSpace(password))
            {
                Assert.IsFalse(summary.Contains(password));
            }

            if (!string.IsNullOrWhiteSpace(signupPassword))
            {
                Assert.IsFalse(summary.Contains(signupPassword));
            }

            Assert.IsTrue(summary.Contains("password=<redacted>"));
            Assert.IsTrue(summary.Contains("signupPassword=<redacted>"));
        }

        [Test]
        public void SafeSyncPayloadUsesMapperAndDoesNotReadApprovedLocationSettings()
        {
            var repository = new FakeRepository { Current = CreateSafeSaveData("phase12-live-test-mapped") };
            var api = new CapturingSafeSyncApiClient();
            var service = new SafeSyncService(repository, api);

            var result = RunAsync(() => service.SyncNowAsync(CancellationToken.None));

            Assert.IsTrue(result.IsSuccess, result.ErrorCode);
            Assert.AreEqual(1, api.PostCount);
            Assert.AreEqual("phase12-live-test-mapped", api.LastRequest.Sessions.Single().ClientSessionId);
            var json = SafeSyncContractBundleExporter.ToJson(api.LastRequest);
            Assert.IsFalse(json.IndexOf("approved", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.IsFalse(json.Contains("Users/"));
            Assert.IsFalse(json.Contains("TokenForgeTests"));
        }

        [Test]
        public void OutgoingPayloadPrivacyGuardBlocksUnsafeLivePayloadBeforeSend()
        {
            var api = new CapturingSafeSyncApiClient();
            var service = new SafeSyncService(
                new FakeRepository(),
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
                            ClientSessionId = "phase12-live-test-unsafe",
                            SourceProvider = "CODEX",
                            DayBucket = "2026-05-15",
                            Confidence = "HIGH",
                            ParserVersion = "raw path /Users/private/project"
                        }
                    }
                });

            var result = RunAsync(() => service.SyncNowAsync(CancellationToken.None));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(SafeSyncApiError.PrivacyGuardBlockedPayload, result.ErrorCode);
            Assert.AreEqual(0, api.PostCount);
        }

        private static ApprovedActivityAnalysisViewModel CreateViewModel(FakeRepository repository, ISafeSyncService syncService, IAuthSessionService authService)
        {
            return new ApprovedActivityAnalysisViewModel(
                new GitAnalysisFlowController(new NullRepositoryPicker(), new GitAggregateAnalyzer(), repository),
                new AgentAnalysisFlowController(new AgentLogActivityProvider(new AgentActivityAnalyzer()), repository),
                new NullAgentLogLocationPicker(),
                repository,
                null,
                new ApprovedLocationSettingsRepository(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TokenForgeTests", System.IO.Path.GetRandomFileName())),
                syncService,
                authService);
        }

        private static SaveData CreateSafeSaveData(string sessionId)
        {
            var saveData = SaveData.CreateDefault();
            saveData.WorkSessionSummaries.Add(new AgentWorkSession
            {
                SessionId = sessionId,
                AgentType = AgentType.Codex,
                SourceProvider = "CODEX",
                WorkType = WorkType.Test,
                StartedAt = new DateTimeOffset(2026, 5, 15, 1, 0, 0, TimeSpan.Zero),
                EndedAt = new DateTimeOffset(2026, 5, 15, 2, 0, 0, TimeSpan.Zero),
                Confidence = ProviderConfidence.High,
                GitChangeSummary = new GitChangeSummary
                {
                    ProjectPathHash = "1234567890abcdef",
                    ChangedFileCount = 1,
                    AddedLineBucket = LineChangeBucket.Small,
                    DeletedLineBucket = LineChangeBucket.None,
                    AnalysisTimeBucket = "2026-05-15"
                },
                AgentActivitySummary = new AgentActivitySummary
                {
                    ProviderType = AgentProviderType.Codex,
                    DayBucket = "2026-05-15",
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
