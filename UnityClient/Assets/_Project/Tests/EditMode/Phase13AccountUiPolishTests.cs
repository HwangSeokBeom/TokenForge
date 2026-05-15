using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TokenForge.Client.Auth;
using TokenForge.Client.UI;

namespace TokenForge.Client.Tests
{
    public sealed class Phase13AccountUiPolishTests
    {
        private sealed class SlowAuthApiClient : IAuthApiClient
        {
            private readonly TaskCompletionSource<AuthApiResult<AuthTokenResponse>> loginCompletion = new TaskCompletionSource<AuthApiResult<AuthTokenResponse>>();

            public AuthApiConfig Config { get; } = new AuthApiConfig("http://localhost:3000/api/v1");
            public int LoginCount { get; private set; }

            public Task<AuthApiResult<AuthTokenResponse>> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
            {
                LoginCount += 1;
                return loginCompletion.Task;
            }

            public void CompleteLogin()
            {
                loginCompletion.TrySetResult(AuthApiResult<AuthTokenResponse>.Failure(AuthApiError.InvalidCredentials, AuthApiError.ToSafeMessage(AuthApiError.InvalidCredentials), 401));
            }

            public Task<AuthApiResult<AuthTokenResponse>> SignupAsync(string email, string password, string displayName = "", CancellationToken cancellationToken = default) => LoginAsync(email, password, cancellationToken);
            public Task<AuthApiResult<AuthTokenResponse>> RegisterAsync(string email, string password, string nickname = "", CancellationToken cancellationToken = default) => SignupAsync(email, password, nickname, cancellationToken);
            public Task<AuthApiResult<AuthRefreshResponse>> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default) => Task.FromResult(AuthApiResult<AuthRefreshResponse>.Failure(AuthApiError.RefreshFailed, AuthApiError.ToSafeMessage(AuthApiError.RefreshFailed), 401));
            public Task<AuthApiResult<AuthLogoutResponse>> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default) => Task.FromResult(AuthApiResult<AuthLogoutResponse>.Success(new AuthLogoutResponse(), 200));
            public Task<AuthApiResult<AuthUserDto>> GetCurrentUserAsync(string accessToken, CancellationToken cancellationToken = default) => Task.FromResult(AuthApiResult<AuthUserDto>.Success(new AuthUserDto { Id = "user-1" }, 200));
        }

        [Test]
        public void AccountStateLabelsAndExpiryCopyAreUserFacing()
        {
            Assert.AreEqual("Logged Out", SafeUserMessageMapper.AuthStateLabel(AuthState.LoggedOut));
            Assert.AreEqual("Refreshing Session", SafeUserMessageMapper.AuthStateLabel(AuthState.Refreshing));
            Assert.AreEqual("Session Expired", SafeUserMessageMapper.AuthStateLabel(AuthState.Expired));
            Assert.AreEqual("Session active", SafeUserMessageMapper.SessionExpirySummary(AuthState.LoggedIn, DateTimeOffset.UtcNow.AddMinutes(10)));
            Assert.AreEqual("Refresh required", SafeUserMessageMapper.SessionExpirySummary(AuthState.LoggedOut, null));
        }

        [Test]
        public void DuplicateLoginDoesNotStartMultipleApiCalls()
        {
            var api = new SlowAuthApiClient();
            var service = new AuthSessionService(api, new InMemoryTokenStore());

            var first = service.LoginAsync("test@example.com", "password123", CancellationToken.None);
            var second = RunAsync(() => service.LoginAsync("test@example.com", "password123", CancellationToken.None));
            api.CompleteLogin();
            RunAsync(() => first);

            Assert.AreEqual(1, api.LoginCount);
            Assert.IsFalse(second.IsSuccess);
            Assert.AreEqual(AuthApiError.AlreadyInProgress, second.ErrorCode);
        }

        [Test]
        public void SafeMessagesDoNotEchoRawServerBodiesOrSensitiveValues()
        {
            var message = SafeUserMessageMapper.FromAuthError("{\"message\":\"password123 access-token refresh-token /Users/private\"}");

            Assert.IsFalse(message.Contains("password123"));
            Assert.IsFalse(message.Contains("access-token"));
            Assert.IsFalse(message.Contains("refresh-token"));
            Assert.IsFalse(message.Contains("/Users/private"));
        }

        private static T RunAsync<T>(Func<Task<T>> taskFactory)
        {
            return Task.Run(taskFactory).GetAwaiter().GetResult();
        }
    }
}
