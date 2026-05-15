using System;

namespace TokenForge.Client.Auth
{
    public sealed class AuthApiConfig
    {
        public const string DefaultBaseUrl = "http://localhost:3000/api/v1";
        public const int DefaultTimeoutSeconds = 15;

        public AuthApiConfig()
            : this(DefaultBaseUrl, DefaultTimeoutSeconds)
        {
        }

        public AuthApiConfig(string baseUrl, int timeoutSeconds = DefaultTimeoutSeconds)
        {
            BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl.TrimEnd('/');
            TimeoutSeconds = timeoutSeconds <= 0 ? DefaultTimeoutSeconds : timeoutSeconds;
        }

        public string BaseUrl { get; set; }
        public int TimeoutSeconds { get; set; }

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
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return BaseUrl;
            }

            return BaseUrl.TrimEnd('/') + "/" + endpoint.TrimStart('/');
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
