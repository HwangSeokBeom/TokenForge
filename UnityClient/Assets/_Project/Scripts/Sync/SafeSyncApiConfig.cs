using System;

namespace TokenForge.Client.Sync
{
    [Serializable]
    public sealed class SafeSyncApiConfig
    {
        public const string DefaultBaseUrl = ApiConfiguration.DefaultBaseUrl;
        public const int DefaultTimeoutSeconds = ApiConfiguration.DefaultTimeoutSeconds;

        public string BaseUrl { get; set; } = DefaultBaseUrl;
        public int TimeoutSeconds { get; set; } = DefaultTimeoutSeconds;

        public SafeSyncApiConfig()
        {
        }

        public SafeSyncApiConfig(string baseUrl, int timeoutSeconds = DefaultTimeoutSeconds)
        {
            BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl.TrimEnd('/');
            TimeoutSeconds = timeoutSeconds <= 0 ? DefaultTimeoutSeconds : timeoutSeconds;
        }

        public bool TrySetBaseUrl(string baseUrl)
        {
            if (!IsValidBaseUrl(baseUrl))
            {
                return false;
            }

            BaseUrl = baseUrl.TrimEnd('/');
            return true;
        }

        public bool HasValidBaseUrl()
        {
            return IsValidBaseUrl(BaseUrl);
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

        public static bool IsValidBaseUrl(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return false;
            }

            Uri uri;
            if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out uri))
            {
                return false;
            }

            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
        }
    }
}
