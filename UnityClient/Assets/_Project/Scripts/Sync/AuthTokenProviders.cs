using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace TokenForge.Client.Sync
{
    public interface IAuthTokenProvider
    {
        Task<string> GetBearerTokenAsync(CancellationToken cancellationToken);
    }

    public sealed class EmptyAuthTokenProvider : IAuthTokenProvider
    {
        public Task<string> GetBearerTokenAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(string.Empty);
        }
    }

    public sealed class NullAuthTokenProvider : IAuthTokenProvider
    {
        public Task<string> GetBearerTokenAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(string.Empty);
        }
    }

    public sealed class DevelopmentAuthTokenProvider : IAuthTokenProvider
    {
        private readonly string token;

        public DevelopmentAuthTokenProvider(string token)
        {
            this.token = token ?? string.Empty;
        }

        public Task<string> GetBearerTokenAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(token);
        }
    }

    public sealed class StaticAuthTokenProvider : IAuthTokenProvider
    {
        private readonly string token;

        public StaticAuthTokenProvider(string token)
        {
            this.token = token ?? string.Empty;
        }

        public Task<string> GetBearerTokenAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(token);
        }
    }

    public sealed class DevGuestAuthTokenProvider : IAuthTokenProvider
    {
        private const string GuestAuthEndpoint = "/auth/guest";
        private const string GuestAuthEndpointName = "auth_guest";

        private readonly ApiConfiguration configuration;
        private readonly ITokenForgeHttpTransport transport;
        private readonly ISafeSyncLogger logger;

        private string cachedToken = string.Empty;

        public DevGuestAuthTokenProvider(
            ApiConfiguration configuration = null,
            ITokenForgeHttpTransport transport = null,
            ISafeSyncLogger logger = null)
        {
            this.configuration = configuration ?? ApiConfiguration.CreateLocalDevelopment();
            this.transport = transport ?? new SystemNetHttpTransport();
            this.logger = logger ?? new UnitySafeSyncLogger();
        }

        public async Task<string> GetBearerTokenAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.IsNullOrWhiteSpace(cachedToken))
            {
                return cachedToken;
            }

            SafeHttpResponse response;
            try
            {
                response = await transport.SendAsync(new SafeHttpRequest
                {
                    Method = "POST",
                    Url = configuration.BuildUrl(GuestAuthEndpoint),
                    EndpointName = GuestAuthEndpointName,
                    TimeoutSeconds = configuration.TimeoutSeconds
                }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                logger.Warning("POST", GuestAuthEndpointName, 0, "auth_guest_cancelled");
                return string.Empty;
            }
            catch
            {
                logger.Warning("POST", GuestAuthEndpointName, 0, "auth_guest_unavailable");
                return string.Empty;
            }

            if (response.IsNetworkError)
            {
                logger.Warning("POST", GuestAuthEndpointName, response.StatusCode, "auth_guest_unavailable");
                return string.Empty;
            }

            if (response.StatusCode < 200 || response.StatusCode >= 300)
            {
                logger.Warning("POST", GuestAuthEndpointName, response.StatusCode, "auth_guest_rejected");
                return string.Empty;
            }

            var token = ExtractToken(response.BodyJson);
            if (string.IsNullOrWhiteSpace(token))
            {
                logger.Warning("POST", GuestAuthEndpointName, response.StatusCode, "auth_guest_malformed");
                return string.Empty;
            }

            cachedToken = token;
            logger.Info("POST", GuestAuthEndpointName, response.StatusCode, "auth_guest_obtained_true");
            return cachedToken;
        }

        private static string ExtractToken(string bodyJson)
        {
            if (string.IsNullOrWhiteSpace(bodyJson))
            {
                return string.Empty;
            }

            try
            {
                var token = JToken.Parse(bodyJson);
                return FirstTokenValue(token, "accessToken")
                    ?? FirstTokenValue(token, "token")
                    ?? FirstTokenValue(token, "bearerToken")
                    ?? FirstTokenValue(token, "authToken")
                    ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string FirstTokenValue(JToken token, string propertyName)
        {
            if (token == null)
            {
                return null;
            }

            if (token.Type == JTokenType.Object)
            {
                foreach (var property in ((JObject)token).Properties())
                {
                    if (string.Equals(property.Name, propertyName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return property.Value.Type == JTokenType.String
                            ? property.Value.Value<string>()
                            : null;
                    }

                    var nested = FirstTokenValue(property.Value, propertyName);
                    if (!string.IsNullOrWhiteSpace(nested))
                    {
                        return nested;
                    }
                }
            }
            else if (token.Type == JTokenType.Array)
            {
                foreach (var item in token.Children())
                {
                    var nested = FirstTokenValue(item, propertyName);
                    if (!string.IsNullOrWhiteSpace(nested))
                    {
                        return nested;
                    }
                }
            }

            return null;
        }
    }
}
