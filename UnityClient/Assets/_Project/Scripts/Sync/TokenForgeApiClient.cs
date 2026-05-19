using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
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

    public interface ITokenForgeApiClient
    {
        TokenForgeServerConfig Config { get; }
        Task<TokenForgeHttpResult<ServerHealthResponse>> CheckHealthAsync(CancellationToken cancellationToken = default);
        Task<TokenForgeHttpResult<SafeSyncUploadResponse>> UploadApprovedAggregateSessionsAsync(SafeSyncUploadRequest request, CancellationToken cancellationToken = default);
    }

    public sealed class TokenForgeApiClient : ITokenForgeApiClient
    {
        private const string HealthEndpoint = "/sync/health";
        private const string ActivitySessionsEndpoint = "/sync/activity-sessions";

        private readonly TokenForgeHttpClient httpClient;
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
        }

        public TokenForgeServerConfig Config { get; }

        public Task<TokenForgeHttpResult<ServerHealthResponse>> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            return httpClient.GetAsync<ServerHealthResponse>(HealthEndpoint, "tokenforge_server_health", cancellationToken);
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
    }
}
