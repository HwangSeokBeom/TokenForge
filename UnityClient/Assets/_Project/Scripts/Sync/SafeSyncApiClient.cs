using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using TokenForge.Client.Auth;

namespace TokenForge.Client.Sync
{
    public sealed class SafeSyncApiClient : ISafeSyncApiClient
    {
        private const string HealthEndpoint = "/sync/health";
        private const string ActivitySessionsEndpoint = "/sync/activity-sessions";

        private readonly IAuthTokenProvider authTokenProvider;
        private readonly ITokenForgeHttpTransport transport;
        private readonly ISafeSyncLogger logger;
        private readonly JsonSerializerSettings serializerSettings;

        public SafeSyncApiClient(
            SafeSyncApiConfig config = null,
            IAuthTokenProvider authTokenProvider = null,
            ITokenForgeHttpTransport transport = null,
            ISafeSyncLogger logger = null)
        {
            Config = config ?? new SafeSyncApiConfig();
            this.authTokenProvider = authTokenProvider ?? new NullAuthTokenProvider();
            this.transport = transport ?? new SystemNetHttpTransport();
            this.logger = logger ?? new UnitySafeSyncLogger();
            serializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
        }

        public SafeSyncApiConfig Config { get; }

        public Task<SafeSyncApiResult<SafeSyncHealthResponse>> GetHealthAsync(CancellationToken cancellationToken = default)
        {
            return SendAsync<SafeSyncHealthResponse>("GET", HealthEndpoint, "sync_health", null, false, 1, cancellationToken);
        }

        public Task<SafeSyncApiResult<SafeActivitySessionsUpsertResponse>> PostActivitySessionsAsync(
            SafeActivitySessionsContractRequest request,
            CancellationToken cancellationToken = default)
        {
            return SendAsync<SafeActivitySessionsUpsertResponse>(
                "POST",
                ActivitySessionsEndpoint,
                "sync_activity_sessions_post",
                request,
                true,
                request?.SchemaVersion ?? 1,
                cancellationToken);
        }

        public Task<SafeSyncApiResult<SafeActivitySessionsListResponse>> GetActivitySessionsAsync(CancellationToken cancellationToken = default)
        {
            return SendAsync<SafeActivitySessionsListResponse>(
                "GET",
                ActivitySessionsEndpoint,
                "sync_activity_sessions_get",
                null,
                true,
                1,
                cancellationToken);
        }

        public Task<SafeSyncApiResult<SafeActivitySessionDeleteResponse>> DeleteActivitySessionAsync(
            string serverSessionId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serverSessionId))
            {
                return Task.FromResult(SafeSyncApiResult<SafeActivitySessionDeleteResponse>.Failure(
                    "INVALID_SERVER_SESSION_ID",
                    "A server session id is required."));
            }

            return SendAsync<SafeActivitySessionDeleteResponse>(
                "DELETE",
                ActivitySessionsEndpoint + "/" + Uri.EscapeDataString(serverSessionId),
                "sync_activity_sessions_delete",
                null,
                true,
                1,
                cancellationToken);
        }

        private async Task<SafeSyncApiResult<TResponse>> SendAsync<TResponse>(
            string method,
            string endpoint,
            string endpointName,
            object requestBody,
            bool requiresAuth,
            int schemaVersion,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            if (!Config.HasValidBaseUrl())
            {
                Log(method, endpointName, 0, SafeSyncApiError.InvalidBaseUrl, 0, 0, schemaVersion, stopwatch.ElapsedMilliseconds);
                return SafeSyncApiResult<TResponse>.Failure(
                    SafeSyncApiError.InvalidBaseUrl,
                    SafeSyncApiError.ToSafeMessage(SafeSyncApiError.InvalidBaseUrl),
                    0);
            }

            string bearerToken = string.Empty;
            if (requiresAuth)
            {
                bearerToken = await authTokenProvider.GetBearerTokenAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(bearerToken))
                {
                    Log(method, endpointName, 0, SafeSyncApiError.AuthRequired, 0, 0, schemaVersion, stopwatch.ElapsedMilliseconds);
                    return SafeSyncApiResult<TResponse>.Failure(
                        SafeSyncApiError.AuthRequired,
                        SafeSyncApiError.ToSafeMessage(SafeSyncApiError.AuthRequired),
                        0);
                }
            }

            var bodyJson = requestBody == null
                ? string.Empty
                : JsonConvert.SerializeObject(requestBody, serializerSettings);
            var request = new SafeHttpRequest
            {
                Method = method,
                Url = Config.BuildUrl(endpoint),
                EndpointName = endpointName,
                BodyJson = bodyJson,
                TimeoutSeconds = Config.TimeoutSeconds,
                BearerToken = bearerToken ?? string.Empty
            };

            var sendResult = await SendHttpAsync<TResponse>(request, method, endpointName, schemaVersion, stopwatch, cancellationToken);
            if (!sendResult.IsSuccess)
            {
                return sendResult.Failure;
            }

            var response = sendResult.Response;

            if (response.IsNetworkError)
            {
                var errorCode = SafeErrorCode(response.ErrorCode, SafeSyncApiError.NetworkError);
                Log(method, endpointName, response.StatusCode, errorCode, 0, 0, schemaVersion, stopwatch.ElapsedMilliseconds);
                return SafeSyncApiResult<TResponse>.Failure(errorCode, SafeSyncApiError.ToSafeMessage(errorCode), response.StatusCode);
            }

            if (response.StatusCode < 200 || response.StatusCode >= 300)
            {
                var errorCode = ExtractSafeErrorCode(response.BodyJson, HttpErrorCode(response.StatusCode));
                if (requiresAuth && IsAuthRejected(errorCode, response.StatusCode) && authTokenProvider is IAuthTokenRefreshProvider refreshProvider)
                {
                    var refreshedToken = await refreshProvider.RefreshBearerTokenAsync(cancellationToken);
                    if (!string.IsNullOrWhiteSpace(refreshedToken))
                    {
                        request.BearerToken = refreshedToken;
                        sendResult = await SendHttpAsync<TResponse>(request, method, endpointName, schemaVersion, stopwatch, cancellationToken);
                        if (!sendResult.IsSuccess)
                        {
                            return sendResult.Failure;
                        }

                        response = sendResult.Response;
                        if (response.StatusCode >= 200 && response.StatusCode < 300)
                        {
                            return ParseSuccess<TResponse>(method, endpointName, response, schemaVersion, stopwatch.ElapsedMilliseconds);
                        }

                        errorCode = ExtractSafeErrorCode(response.BodyJson, HttpErrorCode(response.StatusCode));
                    }
                }

                Log(method, endpointName, response.StatusCode, errorCode, 0, 0, schemaVersion, stopwatch.ElapsedMilliseconds);
                return SafeSyncApiResult<TResponse>.Failure(errorCode, SafeSyncApiError.ToSafeMessage(errorCode), response.StatusCode);
            }

            return ParseSuccess<TResponse>(method, endpointName, response, schemaVersion, stopwatch.ElapsedMilliseconds);
        }

        private async Task<SendHttpResult<TResponse>> SendHttpAsync<TResponse>(
            SafeHttpRequest request,
            string method,
            string endpointName,
            int schemaVersion,
            Stopwatch stopwatch,
            CancellationToken cancellationToken)
        {
            try
            {
                return SendHttpResult<TResponse>.Success(await transport.SendAsync(request, cancellationToken));
            }
            catch (OperationCanceledException)
            {
                Log(method, endpointName, 0, SafeSyncApiError.Timeout, 0, 0, schemaVersion, stopwatch.ElapsedMilliseconds);
                return SendHttpResult<TResponse>.Failed(SafeSyncApiResult<TResponse>.Failure(SafeSyncApiError.Timeout, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.Timeout)));
            }
            catch (Exception)
            {
                Log(method, endpointName, 0, SafeSyncApiError.NetworkError, 0, 0, schemaVersion, stopwatch.ElapsedMilliseconds);
                return SendHttpResult<TResponse>.Failed(SafeSyncApiResult<TResponse>.Failure(SafeSyncApiError.NetworkError, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.NetworkError)));
            }
        }

        private SafeSyncApiResult<TResponse> ParseSuccess<TResponse>(
            string method,
            string endpointName,
            SafeHttpResponse response,
            int schemaVersion,
            long durationMs)
        {
            try
            {
                var value = string.IsNullOrWhiteSpace(response.BodyJson)
                    ? default(TResponse)
                    : JsonConvert.DeserializeObject<TResponse>(response.BodyJson, serializerSettings);
                LogSuccess(method, endpointName, response.StatusCode, value, schemaVersion, durationMs);
                return SafeSyncApiResult<TResponse>.Success(value, response.StatusCode);
            }
            catch (JsonException)
            {
                Log(method, endpointName, response.StatusCode, SafeSyncApiError.InvalidJson, 0, 0, schemaVersion, durationMs);
                return SafeSyncApiResult<TResponse>.Failure(SafeSyncApiError.InvalidJson, "Safe Sync response could not be read.", response.StatusCode);
            }
        }

        private void LogSuccess<TResponse>(
            string method,
            string endpointName,
            long statusCode,
            TResponse value,
            int fallbackSchemaVersion,
            long durationMs)
        {
            var accepted = 0;
            var rejected = 0;
            var schemaVersion = fallbackSchemaVersion;

            if (value is SafeActivitySessionsUpsertResponse upsert)
            {
                accepted = upsert.AcceptedCount;
                rejected = upsert.RejectedCount;
                schemaVersion = upsert.SchemaVersion;
            }
            else if (value is SafeActivitySessionsListResponse list)
            {
                accepted = list.Sessions?.Count ?? 0;
                schemaVersion = list.SchemaVersion;
            }
            else if (value is SafeSyncHealthResponse health)
            {
                schemaVersion = health.SchemaVersion;
            }

            Log(method, endpointName, statusCode, string.Empty, accepted, rejected, schemaVersion, durationMs);
        }

        private void Log(
            string method,
            string endpointName,
            long statusCode,
            string errorCode,
            int acceptedCount,
            int rejectedCount,
            int schemaVersion,
            long durationMs)
        {
            logger.Metadata(method, endpointName, statusCode, errorCode, acceptedCount, rejectedCount, schemaVersion, durationMs);
        }

        private static string ExtractSafeErrorCode(string bodyJson, string fallback)
        {
            if (string.IsNullOrWhiteSpace(bodyJson))
            {
                return SafeErrorCode(string.Empty, fallback);
            }

            try
            {
                var token = JToken.Parse(bodyJson);
                var code = token["errorCode"]?.Value<string>() ??
                           token["code"]?.Value<string>() ??
                           token["error"]?.Value<string>() ??
                           string.Empty;
                return SafeErrorCode(code, fallback);
            }
            catch (JsonException)
            {
                return SafeErrorCode(string.Empty, fallback);
            }
        }

        private static string HttpErrorCode(long statusCode)
        {
            if (statusCode == 401 || statusCode == 403)
            {
                return SafeSyncApiError.AuthRequired;
            }

            return statusCode >= 500 ? SafeSyncApiError.ServerUnavailable : "HTTP_" + statusCode;
        }

        private static bool IsAuthRejected(string errorCode, long statusCode)
        {
            return statusCode == 401 ||
                   statusCode == 403 ||
                   string.Equals(errorCode, SafeSyncApiError.AuthRequired, StringComparison.OrdinalIgnoreCase);
        }

        private static string SafeErrorCode(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            var trimmed = value.Trim();
            return Regex.IsMatch(trimmed, "^[A-Za-z0-9_.-]{1,64}$") && !ContainsForbiddenLogTerm(trimmed)
                ? trimmed
                : fallback;
        }

        private static bool ContainsForbiddenLogTerm(string value)
        {
            if (IsKnownSafeErrorCode(value))
            {
                return false;
            }

            var lowered = value.ToLowerInvariant();
            return lowered.Contains("authorization") ||
                   lowered.Contains("bearer") ||
                   lowered.Contains("token") ||
                   lowered.Contains("prompt") ||
                   lowered.Contains("response") ||
                   lowered.Contains("command") ||
                   lowered.Contains("diff") ||
                   lowered.Contains("path") ||
                   lowered.Contains("body");
        }

        private static bool IsKnownSafeErrorCode(string value)
        {
            return string.Equals(value, "UNSAFE_FIELD_NAME", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "UNSAFE_PATH_VALUE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "UNSAFE_COMMAND_VALUE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "UNSAFE_TOKEN_VALUE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "UNSAFE_SOURCE_SNIPPET", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "PAYLOAD_TOO_LARGE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "UNKNOWN_SCHEMA_VERSION", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "INVALID_BUCKET_VALUE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "INVALID_ENUM_VALUE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "VALIDATION_FAILED", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "PRIVACY_GUARD_REJECTED", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, SafeSyncApiError.PrivacyGuardBlockedPayload, StringComparison.OrdinalIgnoreCase);
        }

        private sealed class SendHttpResult<TResponse>
        {
            public bool IsSuccess { get; private set; }
            public SafeHttpResponse Response { get; private set; }
            public SafeSyncApiResult<TResponse> Failure { get; private set; }

            public static SendHttpResult<TResponse> Success(SafeHttpResponse response)
            {
                return new SendHttpResult<TResponse>
                {
                    IsSuccess = true,
                    Response = response
                };
            }

            public static SendHttpResult<TResponse> Failed(SafeSyncApiResult<TResponse> failure)
            {
                return new SendHttpResult<TResponse>
                {
                    IsSuccess = false,
                    Failure = failure
                };
            }
        }
    }
}
