using System;

namespace TokenForge.Client.Sync
{
    [Serializable]
    public sealed class ApiConfiguration
    {
        public const string DefaultBaseUrl = "http://localhost:3000/api/v1";
        public const string LocalDevelopmentBaseUrl = DefaultBaseUrl;
        public const int DefaultTimeoutSeconds = 10;

        public string BaseUrl { get; set; } = DefaultBaseUrl;
        public int TimeoutSeconds { get; set; } = DefaultTimeoutSeconds;

        public ApiConfiguration()
        {
        }

        public ApiConfiguration(string baseUrl, int timeoutSeconds = DefaultTimeoutSeconds)
        {
            BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl.TrimEnd('/');
            TimeoutSeconds = timeoutSeconds <= 0 ? DefaultTimeoutSeconds : timeoutSeconds;
        }

        public static ApiConfiguration CreateLocalDevelopment(int timeoutSeconds = DefaultTimeoutSeconds)
        {
            return new ApiConfiguration(LocalDevelopmentBaseUrl, timeoutSeconds);
        }

        public string BuildUrl(string endpoint)
        {
            var normalizedBaseUrl = string.IsNullOrWhiteSpace(BaseUrl)
                ? DefaultBaseUrl
                : BaseUrl.TrimEnd('/');
            var normalizedEndpoint = string.IsNullOrWhiteSpace(endpoint)
                ? string.Empty
                : endpoint.TrimStart('/');

            return string.IsNullOrEmpty(normalizedEndpoint)
                ? normalizedBaseUrl
                : normalizedBaseUrl + "/" + normalizedEndpoint;
        }
    }
}
