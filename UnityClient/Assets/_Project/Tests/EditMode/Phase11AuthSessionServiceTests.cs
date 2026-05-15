using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TokenForge.Client.Auth;

namespace TokenForge.Client.Tests
{
    public sealed class Phase11AuthSessionServiceTests
    {
        private sealed class FakeAuthApiClient : IAuthApiClient
        {
            public AuthApiConfig Config { get; } = new AuthApiConfig("http://localhost:3000/api/v1");
            public int LoginCount { get; private set; }
            public int SignupCount { get; private set; }
            public int RefreshCount { get; private set; }
            public int LogoutCount { get; private set; }
            public int CurrentUserCount { get; private set; }
            public AuthApiResult<AuthTokenResponse> LoginResult { get; set; }
            public AuthApiResult<AuthTokenResponse> SignupResult { get; set; }
            public AuthApiResult<AuthRefreshResponse> RefreshResult { get; set; }
            public AuthApiResult<AuthUserDto> CurrentUserResult { get; set; }

            public Task<AuthApiResult<AuthTokenResponse>> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
            {
                LoginCount += 1;
                return Task.FromResult(LoginResult);
            }

            public Task<AuthApiResult<AuthTokenResponse>> RegisterAsync(string email, string password, string nickname = "", CancellationToken cancellationToken = default)
            {
                return SignupAsync(email, password, nickname, cancellationToken);
            }

            public Task<AuthApiResult<AuthTokenResponse>> SignupAsync(string email, string password, string displayName = "", CancellationToken cancellationToken = default)
            {
                SignupCount += 1;
                return Task.FromResult(SignupResult ?? LoginResult);
            }

            public Task<AuthApiResult<AuthRefreshResponse>> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
            {
                RefreshCount += 1;
                return Task.FromResult(RefreshResult);
            }

            public Task<AuthApiResult<AuthLogoutResponse>> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
            {
                LogoutCount += 1;
                return Task.FromResult(AuthApiResult<AuthLogoutResponse>.Success(new AuthLogoutResponse { Status = "ok" }, 200));
            }

            public Task<AuthApiResult<AuthUserDto>> GetCurrentUserAsync(string accessToken, CancellationToken cancellationToken = default)
            {
                CurrentUserCount += 1;
                return Task.FromResult(CurrentUserResult ?? AuthApiResult<AuthUserDto>.Success(new AuthUserDto { Id = "user-1" }, 200));
            }
        }

        [Test]
        public void StartsLoggedOut_WhenNoTokenExists()
        {
            var service = new AuthSessionService(new FakeAuthApiClient(), new InMemoryTokenStore());

            var result = RunAsync(() => service.LoadSessionAsync(CancellationToken.None));

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(AuthState.LoggedOut, service.State);
        }

        [Test]
        public void LoadsExistingValidTokenSession()
        {
            var store = new InMemoryTokenStore();
            RunAsync(() => store.SaveAsync(new AuthSession
            {
                AccessToken = Jwt(DateTimeOffset.UtcNow.AddMinutes(15)),
                RefreshToken = "refresh",
                UserId = "user-1",
                AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15)
            }, CancellationToken.None));
            var service = new AuthSessionService(new FakeAuthApiClient(), store);

            RunAsync(() => service.LoadSessionAsync(CancellationToken.None));

            Assert.AreEqual(AuthState.LoggedIn, service.State);
            Assert.IsTrue(service.HasUsableAccessToken);
        }

        [Test]
        public void LoginStoresTokenSecurely_AndDoesNotAutoSync()
        {
            var api = new FakeAuthApiClient
            {
                LoginResult = AuthApiResult<AuthTokenResponse>.Success(new AuthTokenResponse
                {
                    AccessToken = Jwt(DateTimeOffset.UtcNow.AddMinutes(15)),
                    RefreshToken = "refresh-secret",
                    User = new AuthUserDto { Id = "user-1", Email = "test@example.com" }
                }, 201)
            };
            var store = new InMemoryTokenStore();
            var service = new AuthSessionService(api, store);

            var result = RunAsync(() => service.LoginAsync("test@example.com", "password123", CancellationToken.None));
            var loaded = RunAsync(() => store.LoadAsync(CancellationToken.None));

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, api.LoginCount);
            Assert.AreEqual("refresh-secret", loaded.RefreshToken);
            Assert.AreEqual("user-1", loaded.UserId);
        }

        [Test]
        public void LogoutClearsToken()
        {
            var store = new InMemoryTokenStore();
            RunAsync(() => store.SaveAsync(new AuthSession { AccessToken = Jwt(DateTimeOffset.UtcNow.AddMinutes(15)), RefreshToken = "refresh" }, CancellationToken.None));
            var api = new FakeAuthApiClient();
            var service = new AuthSessionService(api, store);
            RunAsync(() => service.LoadSessionAsync(CancellationToken.None));

            RunAsync(() => service.LogoutAsync(CancellationToken.None));
            var loaded = RunAsync(() => store.LoadAsync(CancellationToken.None));

            Assert.AreEqual(AuthState.LoggedOut, service.State);
            Assert.AreEqual(1, api.LogoutCount);
            Assert.IsNull(loaded);
        }

        [Test]
        public void ExpiredTokenRefreshesOnce_WhenSupported()
        {
            var store = new InMemoryTokenStore();
            RunAsync(() => store.SaveAsync(new AuthSession { AccessToken = Jwt(DateTimeOffset.UtcNow.AddMinutes(-1)), RefreshToken = "refresh" }, CancellationToken.None));
            var api = new FakeAuthApiClient
            {
                RefreshResult = AuthApiResult<AuthRefreshResponse>.Success(new AuthRefreshResponse
                {
                    AccessToken = Jwt(DateTimeOffset.UtcNow.AddMinutes(20)),
                    RefreshToken = "refresh-2"
                }, 200)
            };
            var service = new AuthSessionService(api, store);

            var token = RunAsync(() => service.GetAccessTokenForSyncAsync(CancellationToken.None));

            Assert.IsNotEmpty(token);
            Assert.AreEqual(1, api.RefreshCount);
            Assert.AreEqual(AuthState.LoggedIn, service.State);
        }

        [Test]
        public void RefreshFailureClearsSessionAndMarksExpired()
        {
            var store = new InMemoryTokenStore();
            RunAsync(() => store.SaveAsync(new AuthSession { AccessToken = Jwt(DateTimeOffset.UtcNow.AddMinutes(-1)), RefreshToken = "refresh" }, CancellationToken.None));
            var api = new FakeAuthApiClient
            {
                RefreshResult = AuthApiResult<AuthRefreshResponse>.Failure(AuthApiError.AuthRequired, "bad refresh", 401)
            };
            var service = new AuthSessionService(api, store);

            var token = RunAsync(() => service.GetAccessTokenForSyncAsync(CancellationToken.None));

            Assert.IsEmpty(token);
            Assert.AreEqual(AuthState.Expired, service.State);
            Assert.IsNull(RunAsync(() => store.LoadAsync(CancellationToken.None)));
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
