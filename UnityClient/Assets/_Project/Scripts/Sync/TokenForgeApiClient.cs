using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TokenForge.Client.Auth;
using TokenForge.Client.Domain;
using TokenForge.Client.Privacy;

namespace TokenForge.Client.Sync
{
    [Serializable]
    public sealed class TokenForgeServerConfig
    {
        public const string EnvironmentBaseUrlVariable = "TOKENFORGE_SERVER_BASE_URL";
        public const string EnvironmentTimeoutVariable = "TOKENFORGE_SERVER_TIMEOUT_SECONDS";

        public string BaseUrl { get; set; } = ApiConfiguration.DefaultBaseUrl;
        public int TimeoutSeconds { get; set; } = ApiConfiguration.DefaultTimeoutSeconds;
        public string ClientVersion { get; set; } = "unity-client";

        public TokenForgeServerConfig()
        {
        }

        public TokenForgeServerConfig(string baseUrl, int timeoutSeconds = ApiConfiguration.DefaultTimeoutSeconds)
        {
            BaseUrl = NormalizeBaseUrl(baseUrl);
            TimeoutSeconds = timeoutSeconds <= 0 ? ApiConfiguration.DefaultTimeoutSeconds : timeoutSeconds;
        }

        public static TokenForgeServerConfig FromEnvironment()
        {
            var baseUrl = Environment.GetEnvironmentVariable(EnvironmentBaseUrlVariable);
            var timeoutRaw = Environment.GetEnvironmentVariable(EnvironmentTimeoutVariable);
            int timeoutSeconds;
            if (!int.TryParse(timeoutRaw, out timeoutSeconds))
            {
                timeoutSeconds = ApiConfiguration.DefaultTimeoutSeconds;
            }

            return new TokenForgeServerConfig(baseUrl, timeoutSeconds);
        }

        public ApiConfiguration ToApiConfiguration()
        {
            return new ApiConfiguration(BaseUrl, TimeoutSeconds);
        }

        public static string NormalizeBaseUrl(string baseUrl)
        {
            return string.IsNullOrWhiteSpace(baseUrl)
                ? ApiConfiguration.DefaultBaseUrl
                : baseUrl.Trim().TrimEnd('/');
        }
    }

    public enum SafeSyncConnectionState
    {
        LocalOnly,
        ServerUnavailable,
        Connected,
        SyncPending
    }

    [Serializable]
    public sealed class ServerHealthResponse
    {
        [JsonProperty("status")]
        public string Status { get; set; } = string.Empty;

        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonProperty("serverTime")]
        public string ServerTime { get; set; } = string.Empty;
    }

    [Serializable]
    public sealed class SafeSyncUploadRequest
    {
        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; } = 1;

        [JsonProperty("clientSyncId", NullValueHandling = NullValueHandling.Ignore)]
        public string ClientSyncId { get; set; } = string.Empty;

        [JsonProperty("requestId", NullValueHandling = NullValueHandling.Ignore)]
        public string RequestId { get; set; }

        [JsonProperty("sessions")]
        public List<SafeActivitySessionContractDto> Sessions { get; set; } = new List<SafeActivitySessionContractDto>();

        public static SafeSyncUploadRequest FromApprovedAggregateRequest(SafeActivitySessionsContractRequest request)
        {
            request = request ?? new SafeActivitySessionsContractRequest();
            return new SafeSyncUploadRequest
            {
                SchemaVersion = request.SchemaVersion,
                ClientSyncId = request.ClientSyncId ?? string.Empty,
                RequestId = request.RequestId,
                Sessions = request.Sessions == null
                    ? new List<SafeActivitySessionContractDto>()
                    : new List<SafeActivitySessionContractDto>(request.Sessions)
            };
        }
    }

    [Serializable]
    public sealed class SafeSyncUploadResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("acceptedCount")]
        public int AcceptedCount { get; set; }

        [JsonProperty("rejectedCount")]
        public int RejectedCount { get; set; }

        [JsonProperty("serverTime")]
        public string ServerTime { get; set; } = string.Empty;

        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; }
    }

    public sealed class SafeSyncConnectionViewModel
    {
        public SafeSyncConnectionState State { get; set; } = SafeSyncConnectionState.LocalOnly;
        public string StatusLabel { get; set; } = "Local only";
        public string LastSyncResult { get; set; } = "No sync attempted.";
        public bool LocalGameplayAvailable { get; set; } = true;
    }

    [Serializable]
    public sealed class RepositoryCompanionSyncDto
    {
        [JsonProperty("repositoryHash")]
        public string RepositoryHash { get; set; } = string.Empty;

        [JsonProperty("stage")]
        public CompanionStage Stage { get; set; } = CompanionStage.Egg;

        [JsonProperty("archetype")]
        public CompanionArchetype Archetype { get; set; } = CompanionArchetype.Unknown;

        [JsonProperty("level")]
        public int Level { get; set; } = 1;

        [JsonProperty("xp")]
        public int Xp { get; set; }

        [JsonProperty("codeStat")]
        public int CodeStat { get; set; }

        [JsonProperty("focusStat")]
        public int FocusStat { get; set; }

        [JsonProperty("debugStat")]
        public int DebugStat { get; set; }

        [JsonProperty("designStat")]
        public int DesignStat { get; set; }

        [JsonProperty("syncStat")]
        public int SyncStat { get; set; }

        [JsonProperty("lastApprovedActivityBucket")]
        public string LastApprovedActivityBucket { get; set; } = string.Empty;

        public static RepositoryCompanionSyncDto FromProfile(RepositoryCompanionProfile profile)
        {
            profile = profile ?? new RepositoryCompanionProfile();
            var companion = CompanionProgressionRules.Normalize(profile.CompanionState);
            var stats = companion.Stats ?? CompanionStatProfile.Empty();
            return new RepositoryCompanionSyncDto
            {
                RepositoryHash = profile.RepositoryHash,
                Stage = companion.Stage,
                Archetype = companion.Archetype,
                Level = companion.Level,
                Xp = companion.TotalXp,
                CodeStat = stats.CodeStat,
                FocusStat = stats.FocusStat,
                DebugStat = stats.DebugStat,
                DesignStat = stats.DesignStat,
                SyncStat = stats.SyncStat,
                LastApprovedActivityBucket = profile.LastApprovedActivityBucket
            };
        }
    }

    [Serializable]
    public sealed class RepositoryCompanionListResponse
    {
        [JsonProperty("repositoryCompanions")]
        public List<RepositoryCompanionSyncDto> RepositoryCompanions { get; set; } = new List<RepositoryCompanionSyncDto>();
    }

    public interface ITokenForgeApiClient
    {
        TokenForgeServerConfig Config { get; }
        Task<TokenForgeHttpResult<ServerHealthResponse>> HealthCheckAsync(CancellationToken cancellationToken = default);
        Task<TokenForgeHttpResult<ServerHealthResponse>> CheckHealthAsync(CancellationToken cancellationToken = default);
        Task<AuthApiResult<AuthTokenResponse>> SignUpAsync(string email, string password, CancellationToken cancellationToken = default);
        Task<AuthApiResult<AuthTokenResponse>> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
        Task<AuthApiResult<AuthLogoutResponse>> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
        Task<AuthApiResult<AuthRefreshResponse>> RefreshSessionAsync(string refreshToken, CancellationToken cancellationToken = default);
        Task<TokenForgeHttpResult<SafeSyncUploadResponse>> UploadApprovedAggregateSessionsAsync(SafeSyncUploadRequest request, CancellationToken cancellationToken = default);
        Task<TokenForgeHttpResult<SafeSyncUploadResponse>> UploadApprovedAggregateAsync(SafeActivitySessionsContractRequest request, CancellationToken cancellationToken = default);
        Task<TokenForgeHttpResult<RepositoryCompanionListResponse>> FetchRepositoryCompanionsAsync(CancellationToken cancellationToken = default);
        Task<TokenForgeHttpResult<RepositoryCompanionSyncDto>> UpsertRepositoryCompanionAsync(RepositoryCompanionSyncDto companion, CancellationToken cancellationToken = default);
    }

    public sealed class TokenForgeApiClient : ITokenForgeApiClient
    {
        private const string HealthEndpoint = "/sync/health";
        private const string RootHealthEndpoint = "/health";
        private const string ActivitySessionsEndpoint = "/sync/activity-sessions";
        private const string ApprovedAggregateEndpoint = "/safe-sync/approved-aggregate";
        private const string RepositoryCompanionsEndpoint = "/repository-companions";

        private readonly TokenForgeHttpClient httpClient;
        private readonly AuthApiClient authApiClient;
        private readonly SyncPayloadSanitizer payloadSanitizer;

        public TokenForgeApiClient(
            TokenForgeServerConfig config = null,
            IAuthTokenProvider authTokenProvider = null,
            ITokenForgeHttpTransport transport = null,
            ISafeSyncLogger logger = null,
            SyncPayloadSanitizer payloadSanitizer = null)
        {
            Config = config ?? TokenForgeServerConfig.FromEnvironment();
            this.payloadSanitizer = payloadSanitizer ?? new SyncPayloadSanitizer();
            httpClient = new TokenForgeHttpClient(
                Config.ToApiConfiguration(),
                authTokenProvider ?? new EmptyAuthTokenProvider(),
                transport,
                logger);
            authApiClient = new AuthApiClient(new AuthApiConfig(Config.BaseUrl, Config.TimeoutSeconds), transport, logger);
        }

        public TokenForgeServerConfig Config { get; }

        public Task<TokenForgeHttpResult<ServerHealthResponse>> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            return httpClient.GetAsync<ServerHealthResponse>(RootHealthEndpoint, "tokenforge_server_health", cancellationToken);
        }

        public Task<TokenForgeHttpResult<ServerHealthResponse>> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            return httpClient.GetAsync<ServerHealthResponse>(HealthEndpoint, "tokenforge_server_health", cancellationToken);
        }

        public Task<AuthApiResult<AuthTokenResponse>> SignUpAsync(string email, string password, CancellationToken cancellationToken = default)
        {
            return authApiClient.SignupAsync(email, password, string.Empty, cancellationToken);
        }

        public Task<AuthApiResult<AuthTokenResponse>> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
        {
            return authApiClient.LoginAsync(email, password, cancellationToken);
        }

        public Task<AuthApiResult<AuthLogoutResponse>> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            return authApiClient.LogoutAsync(refreshToken, cancellationToken);
        }

        public Task<AuthApiResult<AuthRefreshResponse>> RefreshSessionAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            return authApiClient.RefreshAsync(refreshToken, cancellationToken);
        }

        public Task<TokenForgeHttpResult<SafeSyncUploadResponse>> UploadApprovedAggregateSessionsAsync(
            SafeSyncUploadRequest request,
            CancellationToken cancellationToken = default)
        {
            payloadSanitizer.ThrowIfUnsafe(request);
            return httpClient.PostJsonAsync<SafeSyncUploadRequest, SafeSyncUploadResponse>(
                ActivitySessionsEndpoint,
                "tokenforge_safe_sync_upload",
                request,
                cancellationToken);
        }

        public Task<TokenForgeHttpResult<SafeSyncUploadResponse>> UploadApprovedAggregateAsync(
            SafeActivitySessionsContractRequest request,
            CancellationToken cancellationToken = default)
        {
            var upload = SafeSyncUploadRequest.FromApprovedAggregateRequest(request);
            payloadSanitizer.ThrowIfUnsafe(upload);
            return httpClient.PostJsonAsync<SafeSyncUploadRequest, SafeSyncUploadResponse>(
                ApprovedAggregateEndpoint,
                "tokenforge_approved_aggregate_upload",
                upload,
                cancellationToken);
        }

        public Task<TokenForgeHttpResult<RepositoryCompanionListResponse>> FetchRepositoryCompanionsAsync(CancellationToken cancellationToken = default)
        {
            return httpClient.GetAsync<RepositoryCompanionListResponse>(
                RepositoryCompanionsEndpoint,
                "tokenforge_repository_companions_fetch",
                cancellationToken);
        }

        public Task<TokenForgeHttpResult<RepositoryCompanionSyncDto>> UpsertRepositoryCompanionAsync(
            RepositoryCompanionSyncDto companion,
            CancellationToken cancellationToken = default)
        {
            payloadSanitizer.ThrowIfUnsafe(companion);
            var hash = companion?.RepositoryHash ?? string.Empty;
            return httpClient.PutJsonAsync<RepositoryCompanionSyncDto, RepositoryCompanionSyncDto>(
                RepositoryCompanionsEndpoint + "/" + Uri.EscapeDataString(hash),
                "tokenforge_repository_companion_upsert",
                companion,
                cancellationToken);
        }
    }
}
