using System;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace TokenForge.Client.Sync
{
    public sealed class SafeHttpRequest
    {
        public string Method { get; set; } = "GET";
        public string Url { get; set; } = string.Empty;
        public string EndpointName { get; set; } = string.Empty;
        public string BodyJson { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = ApiConfiguration.DefaultTimeoutSeconds;
        public string BearerToken { get; set; } = string.Empty;
    }

    public sealed class SafeHttpResponse
    {
        public long StatusCode { get; set; }
        public string BodyJson { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
        public bool IsNetworkError { get; set; }
    }

    public sealed class TokenForgeHttpResult<T>
    {
        public bool IsSuccess { get; set; }
        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public T Value { get; set; }
        public long StatusCode { get; set; }
    }

    public interface ITokenForgeHttpTransport
    {
        Task<SafeHttpResponse> SendAsync(SafeHttpRequest request, CancellationToken cancellationToken);
    }

    public sealed class SystemNetHttpTransport : ITokenForgeHttpTransport
    {
        public async Task<SafeHttpResponse> SendAsync(SafeHttpRequest request, CancellationToken cancellationToken)
        {
            using (var client = new HttpClient())
            using (var message = new HttpRequestMessage(new HttpMethod(request.Method), request.Url))
            {
                client.Timeout = TimeSpan.FromSeconds(request.TimeoutSeconds <= 0
                    ? ApiConfiguration.DefaultTimeoutSeconds
                    : request.TimeoutSeconds);

                if (!string.IsNullOrWhiteSpace(request.BearerToken))
                {
                    message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", request.BearerToken);
                }

                if (!string.IsNullOrEmpty(request.BodyJson) &&
                    (string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(request.Method, "PUT", StringComparison.OrdinalIgnoreCase)))
                {
                    message.Content = new StringContent(request.BodyJson, Encoding.UTF8, "application/json");
                }

                var response = await client.SendAsync(message, cancellationToken);
                var body = response.Content == null
                    ? string.Empty
                    : await response.Content.ReadAsStringAsync();

                return new SafeHttpResponse
                {
                    StatusCode = (long)response.StatusCode,
                    BodyJson = body
                };
            }
        }
    }

    public sealed class TokenForgeHttpClient
    {
        private readonly ApiConfiguration configuration;
        private readonly IAuthTokenProvider authTokenProvider;
        private readonly ITokenForgeHttpTransport transport;
        private readonly ISafeSyncLogger logger;
        private readonly JsonSerializerSettings serializerSettings;

        public TokenForgeHttpClient(
            ApiConfiguration configuration = null,
            IAuthTokenProvider authTokenProvider = null,
            ITokenForgeHttpTransport transport = null,
            ISafeSyncLogger logger = null)
        {
            this.configuration = configuration ?? new ApiConfiguration();
            this.authTokenProvider = authTokenProvider ?? new EmptyAuthTokenProvider();
            this.transport = transport ?? new SystemNetHttpTransport();
            this.logger = logger ?? new UnitySafeSyncLogger();
            serializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Include,
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
        }

        public Task<TokenForgeHttpResult<TResponse>> GetAsync<TResponse>(string endpoint, string endpointName, CancellationToken cancellationToken)
        {
            return SendAsync<TResponse>("GET", endpoint, endpointName, null, cancellationToken);
        }

        public Task<TokenForgeHttpResult<TResponse>> DeleteAsync<TResponse>(string endpoint, string endpointName, CancellationToken cancellationToken)
        {
            return SendAsync<TResponse>("DELETE", endpoint, endpointName, null, cancellationToken);
        }

        public Task<TokenForgeHttpResult<TResponse>> PostJsonAsync<TRequest, TResponse>(
            string endpoint,
            string endpointName,
            TRequest request,
            CancellationToken cancellationToken)
        {
            return SendAsync<TResponse>("POST", endpoint, endpointName, request, cancellationToken);
        }

        public Task<TokenForgeHttpResult<TResponse>> PutJsonAsync<TRequest, TResponse>(
            string endpoint,
            string endpointName,
            TRequest request,
            CancellationToken cancellationToken)
        {
            return SendAsync<TResponse>("PUT", endpoint, endpointName, request, cancellationToken);
        }

        private async Task<TokenForgeHttpResult<TResponse>> SendAsync<TResponse>(
            string method,
            string endpoint,
            string endpointName,
            object requestBody,
            CancellationToken cancellationToken)
        {
            string bodyJson = string.Empty;
            if (requestBody != null)
            {
                bodyJson = JsonConvert.SerializeObject(requestBody, serializerSettings);
            }

            var bearerToken = await authTokenProvider.GetBearerTokenAsync(cancellationToken);
            var request = new SafeHttpRequest
            {
                Method = method,
                Url = configuration.BuildUrl(endpoint),
                EndpointName = endpointName,
                BodyJson = bodyJson,
                TimeoutSeconds = configuration.TimeoutSeconds,
                BearerToken = bearerToken ?? string.Empty
            };

            SafeHttpResponse response;
            try
            {
                response = await transport.SendAsync(request, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                logger.Warning(method, endpointName, 0, "sync_timeout");
                return Failure<TResponse>(0, "sync_timeout", "Sync request timed out or was cancelled.");
            }
            catch (Exception)
            {
                logger.Warning(method, endpointName, 0, "sync_network_error");
                return Failure<TResponse>(0, "sync_network_error", "Sync request failed.");
            }

            if (response.IsNetworkError)
            {
                var errorCode = SafeErrorCode(response.ErrorCode, "sync_network_error");
                logger.Warning(method, endpointName, response.StatusCode, errorCode);
                return Failure<TResponse>(response.StatusCode, errorCode, "Sync request failed.");
            }

            if (response.StatusCode < 200 || response.StatusCode >= 300)
            {
                var errorCode = ExtractSafeErrorCode(response.BodyJson, "http_" + response.StatusCode);
                logger.Warning(method, endpointName, response.StatusCode, errorCode);
                return Failure<TResponse>(response.StatusCode, errorCode, "Sync request was rejected.");
            }

            try
            {
                var value = string.IsNullOrWhiteSpace(response.BodyJson)
                    ? default(TResponse)
                    : JsonConvert.DeserializeObject<TResponse>(response.BodyJson, serializerSettings);
                logger.Info(method, endpointName, response.StatusCode);
                return new TokenForgeHttpResult<TResponse>
                {
                    IsSuccess = true,
                    StatusCode = response.StatusCode,
                    Value = value
                };
            }
            catch (JsonException)
            {
                logger.Warning(method, endpointName, response.StatusCode, "sync_invalid_json");
                return Failure<TResponse>(response.StatusCode, "sync_invalid_json", "Sync response could not be read.");
            }
        }

        private static TokenForgeHttpResult<T> Failure<T>(long statusCode, string errorCode, string message)
        {
            return new TokenForgeHttpResult<T>
            {
                IsSuccess = false,
                StatusCode = statusCode,
                ErrorCode = errorCode,
                ErrorMessage = message
            };
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
                var code = token["code"]?.Value<string>() ?? token["errorCode"]?.Value<string>() ?? string.Empty;
                return SafeErrorCode(code, fallback);
            }
            catch (JsonException)
            {
                return SafeErrorCode(string.Empty, fallback);
            }
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
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var lowered = value.ToLowerInvariant();
            return lowered.Contains("authorization") ||
                   lowered.Contains("bearer") ||
                   lowered.Contains("token") ||
                   lowered.Contains("prompt") ||
                   lowered.Contains("diff") ||
                   lowered.Contains("path") ||
                   lowered.Contains("claudelog") ||
                   lowered.Contains("codexlog") ||
                   lowered.Contains("body");
        }
    }
}
