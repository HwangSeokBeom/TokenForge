using System;

namespace TokenForge.Client.Auth
{
    public sealed class AuthSession
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTimeOffset? AccessTokenExpiresAt { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ServerBaseUrl { get; set; } = string.Empty;
        public DateTimeOffset LastLoginAt { get; set; } = DateTimeOffset.UtcNow;

        public bool HasAccessToken => !string.IsNullOrWhiteSpace(AccessToken);
        public bool HasRefreshToken => !string.IsNullOrWhiteSpace(RefreshToken);

        public bool IsAccessTokenExpired(DateTimeOffset now, TimeSpan refreshSkew)
        {
            return !AccessTokenExpiresAt.HasValue || AccessTokenExpiresAt.Value <= now.Add(refreshSkew);
        }

        public AuthSession WithoutTokenValues()
        {
            return new AuthSession
            {
                AccessTokenExpiresAt = AccessTokenExpiresAt,
                UserId = UserId,
                Email = Email,
                DisplayName = DisplayName,
                ServerBaseUrl = ServerBaseUrl,
                LastLoginAt = LastLoginAt
            };
        }
    }
}
