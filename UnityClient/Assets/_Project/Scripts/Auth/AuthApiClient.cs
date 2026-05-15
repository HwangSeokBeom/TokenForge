using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using TokenForge.Client.Sync;

namespace TokenForge.Client.Auth
{
    public sealed class AuthApiClient : IAuthApiClient
    {
        private readonly ITokenForgeHttpTransport transport;
        private readonly ISafeSyncLogger logger;
        private readonly JsonSerializerSettings serializerSettings;

        public AuthApiClient(
            AuthApiConfig config = null,
            ITokenForgeHttpTransport transport = null,
            ISafeSyncLogger logger = null)
        {
            Config = config ?? new AuthApiConfig();
            this.transport = transport ?? new SystemNetHttpTransport();
            this.logger = logger ?? new UnitySafeSyncLogger();
            serializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
        }

        public AuthApiConfig Config { get; }

        public Task<AuthApiResult<AuthTokenResponse>> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
        {
            return SendAsync<AuthTokenResponse>(
                "POST",
                "/auth/login",
                "auth_login",
                new AuthLoginRequest { Email = (email ?? string.Empty).Trim(), Password = password ?? string.Empty },
                string.Empty,
                cancellationToken);
        }

        public Task<AuthApiResult<AuthTokenResponse>> RegisterAsync(string email, string password, string nickname = "", CancellationToken cancellationToken = default)
        {
            return SignupAsync(email, password, nickname, cancellationToken);
        }

        public Task<AuthApiResult<AuthTokenResponse>> SignupAsync(string email, string password, string displayName = "", CancellationToken cancellationToken = default)
        {
            return SendAsync<AuthTokenResponse>(
                "POST",
                "/auth/signup",
                "auth_signup",
                new AuthSignupRequest
                {
                    Email = (email ?? string.Empty).Trim(),
                    Password = password ?? string.Empty,
                    Nickname = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),
                    DisplayName = null
                },
                string.Empty,
                cancellationToken);
        }

        public Task<AuthApiResult<AuthRefreshResponse>> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            return SendAsync<AuthRefreshResponse>(
                "POST",
                "/auth/refresh",
                "auth_refresh",
                new AuthRefreshRequest { RefreshToken = refreshToken ?? string.Empty },
                string.Empty,
                cancellationToken);
        }

        public Task<AuthApiResult<AuthLogoutResponse>> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            return SendAsync<AuthLogoutResponse>(
                "POST",
                "/auth/logout",
                "auth_logout",
                new AuthLogoutRequest { RefreshToken = refreshToken ?? string.Empty },
                string.Empty,
                cancellationToken);
        }

        public Task<AuthApiResult<AuthUserDto>> GetCurrentUserAsync(string accessToken, CancellationToken cancellationToken = default)
        {
            return SendAsync<AuthUserDto>(
                "GET",
                "/users/me",
                "auth_current_user",
                null,
                accessToken ?? string.Empty,
                cancellationToken);
        }

        private async Task<AuthApiResult<TResponse>> SendAsync<TResponse>(
            string method,
            string endpoint,
            string endpointName,
            object requestBody,
            string bearerToken,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            if (!Config.HasValidBaseUrl())
            {
                Log(method, endpointName, 0, AuthApiError.InvalidBaseUrl, stopwatch.ElapsedMilliseconds);
                return AuthApiResult<TResponse>.Failure(AuthApiError.InvalidBaseUrl, AuthApiError.ToSafeMessage(AuthApiError.InvalidBaseUrl));
            }

            var bodyJson = requestBody == null
                ? string.Empty
                : JsonConvert.SerializeObject(requestBody, serializerSettings);

            SafeHttpResponse response;
            try
            {
                response = await transport.SendAsync(new SafeHttpRequest
                {
                    Method = method,
                    Url = Config.BuildUrl(endpoint),
                    EndpointName = endpointName,
                    BodyJson = bodyJson,
                    TimeoutSeconds = Config.TimeoutSeconds,
                    BearerToken = bearerToken ?? string.Empty
                }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Log(method, endpointName, 0, AuthApiError.Timeout, stopwatch.ElapsedMilliseconds);
                return AuthApiResult<TResponse>.Failure(AuthApiError.Timeout, AuthApiError.ToSafeMessage(AuthApiError.Timeout));
            }
            catch (Exception)
            {
                Log(method, endpointName, 0, AuthApiError.NetworkError, stopwatch.ElapsedMilliseconds);
                return AuthApiResult<TResponse>.Failure(AuthApiError.NetworkError, AuthApiError.ToSafeMessage(AuthApiError.ServerUnavailable));
            }

            if (response.IsNetworkError)
            {
                var errorCode = AuthApiError.Normalize(SafeErrorCode(response.ErrorCode, AuthApiError.NetworkError), response.StatusCode, endpointName);
                Log(method, endpointName, response.StatusCode, errorCode, stopwatch.ElapsedMilliseconds);
                return AuthApiResult<TResponse>.Failure(errorCode, AuthApiError.ToSafeMessage(errorCode), response.StatusCode);
            }

            if (response.StatusCode < 200 || response.StatusCode >= 300)
            {
                var errorCode = AuthApiError.Normalize(ExtractSafeErrorCode(response.BodyJson, HttpErrorCode(response.StatusCode, endpointName)), response.StatusCode, endpointName);
                Log(method, endpointName, response.StatusCode, errorCode, stopwatch.ElapsedMilliseconds);
                return AuthApiResult<TResponse>.Failure(errorCode, AuthApiError.ToSafeMessage(errorCode), response.StatusCode);
            }

            try
            {
                var value = string.IsNullOrWhiteSpace(response.BodyJson)
                    ? default(TResponse)
                    : JsonConvert.DeserializeObject<TResponse>(response.BodyJson, serializerSettings);
                Log(method, endpointName, response.StatusCode, string.Empty, stopwatch.ElapsedMilliseconds);
                return AuthApiResult<TResponse>.Success(value, response.StatusCode);
            }
            catch (JsonException)
            {
                Log(method, endpointName, response.StatusCode, AuthApiError.InvalidJson, stopwatch.ElapsedMilliseconds);
                return AuthApiResult<TResponse>.Failure(AuthApiError.InvalidJson, "Authentication response could not be read.", response.StatusCode);
            }
        }

        private void Log(string method, string endpointName, long statusCode, string errorCode, long durationMs)
        {
            logger.Metadata(method, endpointName, statusCode, errorCode, 0, 0, 0, durationMs);
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

        private static string HttpErrorCode(long statusCode, string endpointName)
        {
            if (statusCode == 401 || statusCode == 403)
            {
                if (string.Equals(endpointName, "auth_login", StringComparison.OrdinalIgnoreCase))
                {
                    return AuthApiError.InvalidCredentials;
                }

                if (string.Equals(endpointName, "auth_refresh", StringComparison.OrdinalIgnoreCase))
                {
                    return AuthApiError.RefreshFailed;
                }

                return AuthApiError.AuthRequired;
            }

            if (statusCode == 409)
            {
                return AuthApiError.EmailAlreadyExists;
            }

            if (statusCode == 400 || statusCode == 422)
            {
                return AuthApiError.ValidationFailed;
            }

            return statusCode >= 500 ? AuthApiError.ServerUnavailable : "HTTP_" + statusCode;
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
            if (string.Equals(value, AuthApiError.ValidationFailed, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, AuthApiError.AuthRequired, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, AuthApiError.InvalidCredentials, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, AuthApiError.EmailAlreadyExists, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, AuthApiError.TokenExpired, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, AuthApiError.RefreshFailed, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, AuthApiError.SessionExpired, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, AuthApiError.ServerUnavailable, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var lowered = value.ToLowerInvariant();
            return lowered.Contains("authorization") ||
                   lowered.Contains("bearer") ||
                   lowered.Contains("token") ||
                   lowered.Contains("password") ||
                   lowered.Contains("prompt") ||
                   lowered.Contains("response") ||
                   lowered.Contains("command") ||
                   lowered.Contains("diff") ||
                   lowered.Contains("path") ||
                   lowered.Contains("body");
        }
    }
}
