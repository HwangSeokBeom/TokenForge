using System.Threading;
using System.Threading.Tasks;
using TokenForge.Client.Sync;

namespace TokenForge.Client.Auth
{
    public interface IAuthTokenRefreshProvider : IAuthTokenProvider
    {
        Task<string> RefreshBearerTokenAsync(CancellationToken cancellationToken);
    }

    public sealed class AuthSessionTokenProvider : IAuthTokenRefreshProvider
    {
        private readonly IAuthSessionService sessionService;

        public AuthSessionTokenProvider(IAuthSessionService sessionService)
        {
            this.sessionService = sessionService;
        }

        public Task<string> GetBearerTokenAsync(CancellationToken cancellationToken)
        {
            return sessionService == null
                ? Task.FromResult(string.Empty)
                : sessionService.GetAccessTokenForSyncAsync(cancellationToken);
        }

        public Task<string> RefreshBearerTokenAsync(CancellationToken cancellationToken)
        {
            return sessionService == null
                ? Task.FromResult(string.Empty)
                : sessionService.ForceRefreshForRetryAsync(cancellationToken);
        }
    }
}
