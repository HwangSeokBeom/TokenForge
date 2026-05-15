using System.Threading;
using System.Threading.Tasks;

namespace TokenForge.Client.Auth
{
    public interface IAuthApiClient
    {
        AuthApiConfig Config { get; }
        Task<AuthApiResult<AuthTokenResponse>> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
        Task<AuthApiResult<AuthTokenResponse>> SignupAsync(string email, string password, string displayName = "", CancellationToken cancellationToken = default);
        Task<AuthApiResult<AuthTokenResponse>> RegisterAsync(string email, string password, string nickname = "", CancellationToken cancellationToken = default);
        Task<AuthApiResult<AuthRefreshResponse>> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
        Task<AuthApiResult<AuthLogoutResponse>> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
        Task<AuthApiResult<AuthUserDto>> GetCurrentUserAsync(string accessToken, CancellationToken cancellationToken = default);
    }
}
