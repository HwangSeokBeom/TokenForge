using System.Threading;
using System.Threading.Tasks;

namespace TokenForge.Client.Auth
{
    public interface IAuthSessionService
    {
        AuthState State { get; }
        AuthSession CurrentSession { get; }
        string ErrorCode { get; }
        string BaseUrl { get; }
        bool HasUsableAccessToken { get; }

        void SetBaseUrl(string baseUrl);
        Task<AuthResult> LoadSessionAsync(CancellationToken cancellationToken = default);
        Task<AuthResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
        Task<AuthResult> SignupAsync(string email, string password, string displayName = "", CancellationToken cancellationToken = default);
        Task<AuthResult> LoadCurrentUserAsync(CancellationToken cancellationToken = default);
        Task<AuthResult> LogoutAsync(CancellationToken cancellationToken = default);
        Task<string> GetAccessTokenForSyncAsync(CancellationToken cancellationToken = default);
        Task<string> ForceRefreshForRetryAsync(CancellationToken cancellationToken = default);
    }
}
