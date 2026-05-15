using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TokenForge.Client.Auth;

namespace TokenForge.Client.Tests
{
    public sealed class Phase13TokenStoreRecoveryTests
    {
        private sealed class FakeAuthApiClient : IAuthApiClient
        {
            public AuthApiConfig Config { get; } = new AuthApiConfig("http://localhost:3000/api/v1");
            public int LoginCount { get; private set; }
            public int LogoutCount { get; private set; }
            public AuthApiResult<AuthTokenResponse> LoginResult { get; set; }

            public Task<AuthApiResult<AuthTokenResponse>> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
            {
                LoginCount += 1;
                return Task.FromResult(LoginResult);
            }

            public Task<AuthApiResult<AuthTokenResponse>> SignupAsync(string email, string password, string displayName = "", CancellationToken cancellationToken = default) => Task.FromResult(LoginResult);
            public Task<AuthApiResult<AuthTokenResponse>> RegisterAsync(string email, string password, string nickname = "", CancellationToken cancellationToken = default) => SignupAsync(email, password, nickname, cancellationToken);
            public Task<AuthApiResult<AuthRefreshResponse>> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default) => Task.FromResult(AuthApiResult<AuthRefreshResponse>.Failure(AuthApiError.RefreshFailed, AuthApiError.ToSafeMessage(AuthApiError.RefreshFailed), 401));
            public Task<AuthApiResult<AuthLogoutResponse>> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
            {
                LogoutCount += 1;
                return Task.FromResult(AuthApiResult<AuthLogoutResponse>.Success(new AuthLogoutResponse { Status = "ok" }, 200));
            }
            public Task<AuthApiResult<AuthUserDto>> GetCurrentUserAsync(string accessToken, CancellationToken cancellationToken = default) => Task.FromResult(AuthApiResult<AuthUserDto>.Success(new AuthUserDto { Id = "user-1" }, 200));
        }

        [Test]
        public void LoadTokenStoreFailureDoesNotCrashStartupOrExposeTokens()
        {
            var store = new InMemoryTokenStore { LoadFailureCode = AuthApiError.TokenStoreReadFailed };
            var service = new AuthSessionService(new FakeAuthApiClient(), store);

            var result = RunAsync(() => service.LoadSessionAsync(CancellationToken.None));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(AuthState.Failed, service.State);
            Assert.AreEqual(AuthApiError.TokenStoreReadFailed, result.ErrorCode);
            Assert.IsFalse(result.ErrorMessage.Contains("access-token"));
            Assert.IsFalse(result.ErrorMessage.Contains("refresh-token"));
        }

        [Test]
        public void LoginWriteFailureIsNotReportedAsSuccessful()
        {
            var api = new FakeAuthApiClient
            {
                LoginResult = AuthApiResult<AuthTokenResponse>.Success(new AuthTokenResponse
                {
                    AccessToken = Jwt(DateTimeOffset.UtcNow.AddMinutes(15)),
                    RefreshToken = "refresh-token-secret",
                    User = new AuthUserDto { Id = "user-1", Email = "test@example.com" }
                }, 200)
            };
            var store = new InMemoryTokenStore { SaveFailureCode = AuthApiError.TokenStoreWriteFailed };
            var service = new AuthSessionService(api, store);

            var result = RunAsync(() => service.LoginAsync("test@example.com", "password123", CancellationToken.None));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(AuthApiError.TokenStoreWriteFailed, result.ErrorCode);
            Assert.IsNull(service.CurrentSession);
            Assert.AreEqual(1, api.LoginCount);
        }

        [Test]
        public void LogoutClearFailureClearsInMemoryStateAndReturnsSafeWarning()
        {
            var store = new InMemoryTokenStore { ClearFailureCode = AuthApiError.TokenStoreClearFailed };
            RunAsync(() => store.SaveAsync(new AuthSession
            {
                AccessToken = Jwt(DateTimeOffset.UtcNow.AddMinutes(15)),
                RefreshToken = "refresh-token-secret"
            }, CancellationToken.None));
            var service = new AuthSessionService(new FakeAuthApiClient(), store);
            RunAsync(() => service.LoadSessionAsync(CancellationToken.None));

            var result = RunAsync(() => service.LogoutAsync(CancellationToken.None));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(AuthState.LoggedOut, service.State);
            Assert.IsNull(service.CurrentSession);
            Assert.AreEqual(AuthApiError.TokenStoreClearFailed, result.ErrorCode);
        }

        private static string Jwt(DateTimeOffset expiresAt)
        {
            return Base64Url("{\"alg\":\"none\"}") + "." + Base64Url("{\"exp\":" + expiresAt.ToUnixTimeSeconds() + "}") + ".";
        }

        private static string Base64Url(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static T RunAsync<T>(Func<Task<T>> taskFactory)
        {
            return Task.Run(taskFactory).GetAwaiter().GetResult();
        }

        private static void RunAsync(Func<Task> taskFactory)
        {
            Task.Run(taskFactory).GetAwaiter().GetResult();
        }
    }
}
