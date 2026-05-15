using System.Threading;
using System.Threading.Tasks;

namespace TokenForge.Client.Auth
{
    public sealed class InMemoryTokenStore : ISecureTokenStore
    {
        private AuthSession session;

        public string SaveFailureCode { get; set; } = string.Empty;
        public string LoadFailureCode { get; set; } = string.Empty;
        public string ClearFailureCode { get; set; } = string.Empty;

        public Task SaveAsync(AuthSession session, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfConfigured(SaveFailureCode);
            this.session = Clone(session);
            return Task.CompletedTask;
        }

        public Task<AuthSession> LoadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfConfigured(LoadFailureCode);
            return Task.FromResult(Clone(session));
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfConfigured(ClearFailureCode);
            session = null;
            return Task.CompletedTask;
        }

        private static void ThrowIfConfigured(string errorCode)
        {
            if (!string.IsNullOrWhiteSpace(errorCode))
            {
                throw new SecureTokenStoreException(errorCode);
            }
        }

        private static AuthSession Clone(AuthSession source)
        {
            if (source == null)
            {
                return null;
            }

            return new AuthSession
            {
                AccessToken = source.AccessToken,
                RefreshToken = source.RefreshToken,
                AccessTokenExpiresAt = source.AccessTokenExpiresAt,
                UserId = source.UserId,
                Email = source.Email,
                DisplayName = source.DisplayName,
                ServerBaseUrl = source.ServerBaseUrl,
                LastLoginAt = source.LastLoginAt
            };
        }
    }
}
