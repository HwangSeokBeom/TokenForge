using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TokenForge.Client.Auth;
using TokenForge.Client.Common;
using TokenForge.Client.Domain;
using TokenForge.Client.Persistence;

namespace TokenForge.Client.Tests
{
    public sealed class Phase12AuthHardeningTests
    {
        private sealed class FakeAuthApiClient : IAuthApiClient
        {
            private readonly Queue<AuthApiResult<AuthUserDto>> currentUserResults = new Queue<AuthApiResult<AuthUserDto>>();

            public AuthApiConfig Config { get; } = new AuthApiConfig("http://localhost:3000/api/v1");
            public int LoginCount { get; private set; }
            public int SignupCount { get; private set; }
            public int RefreshCount { get; private set; }
            public int LogoutCount { get; private set; }
            public int CurrentUserCount { get; private set; }
            public AuthApiResult<AuthTokenResponse> LoginResult { get; set; }
            public AuthApiResult<AuthTokenResponse> SignupResult { get; set; }
            public AuthApiResult<AuthRefreshResponse> RefreshResult { get; set; }

            public void EnqueueCurrentUser(AuthApiResult<AuthUserDto> result)
            {
                currentUserResults.Enqueue(result);
            }

            public Task<AuthApiResult<AuthTokenResponse>> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
            {
                LoginCount += 1;
                return Task.FromResult(LoginResult);
            }

            public Task<AuthApiResult<AuthTokenResponse>> SignupAsync(string email, string password, string displayName = "", CancellationToken cancellationToken = default)
            {
                SignupCount += 1;
                return Task.FromResult(SignupResult);
            }

            public Task<AuthApiResult<AuthTokenResponse>> RegisterAsync(string email, string password, string nickname = "", CancellationToken cancellationToken = default)
            {
                return SignupAsync(email, password, nickname, cancellationToken);
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
                return Task.FromResult(currentUserResults.Count == 0
                    ? AuthApiResult<AuthUserDto>.Failure(AuthApiError.AuthRequired, "auth required", 401)
                    : currentUserResults.Dequeue());
            }
        }

        private sealed class FakeRepository : ILocalSaveDataRepository
        {
            public SaveData Current { get; set; } = SaveData.CreateDefault();
            public Task<SaveData> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Current);
            public Task<Result> SaveAsync(SaveData saveData, CancellationToken cancellationToken = default)
            {
                Current = saveData;
                return Task.FromResult(Result.Success());
            }
        }

        [Test]
        public void SignupSuccessStoresSession_AndDoesNotTriggerLoginOrSync()
        {
            var api = new FakeAuthApiClient
            {
                SignupResult = AuthApiResult<AuthTokenResponse>.Success(new AuthTokenResponse
                {
                    AccessToken = Jwt(DateTimeOffset.UtcNow.AddMinutes(15)),
                    RefreshToken = "refresh-secret",
                    User = new AuthUserDto { Id = "user-1", Email = "new@example.com", Nickname = "Tester" }
                }, 201)
            };
            var store = new InMemoryTokenStore();
            var service = new AuthSessionService(api, store);

            var result = RunAsync(() => service.SignupAsync("new@example.com", "password123", "Tester", CancellationToken.None));
            var stored = RunAsync(() => store.LoadAsync(CancellationToken.None));

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(AuthState.LoggedIn, service.State);
            Assert.AreEqual(1, api.SignupCount);
            Assert.AreEqual(0, api.LoginCount);
            Assert.AreEqual("user-1", stored.UserId);
            Assert.AreEqual("Tester", stored.DisplayName);
        }

        [Test]
        public void SignupFailureReturnsSafeErrorAndDoesNotStoreSession()
        {
            var api = new FakeAuthApiClient
            {
                SignupResult = AuthApiResult<AuthTokenResponse>.Failure(AuthApiError.EmailAlreadyExists, AuthApiError.ToSafeMessage(AuthApiError.EmailAlreadyExists), 409)
            };
            var store = new InMemoryTokenStore();
            var service = new AuthSessionService(api, store);

            var result = RunAsync(() => service.SignupAsync("new@example.com", "password123", "Tester", CancellationToken.None));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(AuthApiError.EmailAlreadyExists, result.ErrorCode);
            Assert.IsNull(RunAsync(() => store.LoadAsync(CancellationToken.None)));
            Assert.IsFalse(result.ErrorMessage.Contains("password123"));
            Assert.IsFalse(result.ErrorMessage.Contains("refresh"));
        }

        [Test]
        public void LoadCurrentUserSuccessUpdatesUserSummary()
        {
            var api = new FakeAuthApiClient();
            api.EnqueueCurrentUser(AuthApiResult<AuthUserDto>.Success(new AuthUserDto { Id = "user-2", Email = "me@example.com", Nickname = "Me" }, 200));
            var store = new InMemoryTokenStore();
            RunAsync(() => store.SaveAsync(new AuthSession { AccessToken = Jwt(DateTimeOffset.UtcNow.AddMinutes(15)), RefreshToken = "refresh" }, CancellationToken.None));
            var service = new AuthSessionService(api, store);

            var result = RunAsync(() => service.LoadCurrentUserAsync(CancellationToken.None));

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("user-2", service.CurrentSession.UserId);
            Assert.AreEqual("Me", service.CurrentSession.DisplayName);
        }

        [Test]
        public void LoadCurrentUser401RefreshesOnceAndRetries()
        {
            var api = new FakeAuthApiClient
            {
                RefreshResult = AuthApiResult<AuthRefreshResponse>.Success(new AuthRefreshResponse
                {
                    AccessToken = Jwt(DateTimeOffset.UtcNow.AddMinutes(20)),
                    RefreshToken = "refresh-2"
                }, 200)
            };
            api.EnqueueCurrentUser(AuthApiResult<AuthUserDto>.Failure(AuthApiError.AuthRequired, "auth required", 401));
            api.EnqueueCurrentUser(AuthApiResult<AuthUserDto>.Success(new AuthUserDto { Id = "user-2", Email = "me@example.com" }, 200));
            var store = new InMemoryTokenStore();
            RunAsync(() => store.SaveAsync(new AuthSession { AccessToken = Jwt(DateTimeOffset.UtcNow.AddMinutes(15)), RefreshToken = "refresh" }, CancellationToken.None));
            var service = new AuthSessionService(api, store);

            var result = RunAsync(() => service.LoadCurrentUserAsync(CancellationToken.None));

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, api.RefreshCount);
            Assert.AreEqual(2, api.CurrentUserCount);
            Assert.AreEqual("user-2", service.CurrentSession.UserId);
        }

        [Test]
        public void LoadCurrentUserRefreshFailureMarksExpiredAndClearsTokensOnly()
        {
            var api = new FakeAuthApiClient
            {
                RefreshResult = AuthApiResult<AuthRefreshResponse>.Failure(AuthApiError.RefreshFailed, AuthApiError.ToSafeMessage(AuthApiError.RefreshFailed), 401)
            };
            api.EnqueueCurrentUser(AuthApiResult<AuthUserDto>.Failure(AuthApiError.AuthRequired, "auth required", 401));
            var store = new InMemoryTokenStore();
            RunAsync(() => store.SaveAsync(new AuthSession { AccessToken = Jwt(DateTimeOffset.UtcNow.AddMinutes(15)), RefreshToken = "refresh" }, CancellationToken.None));
            var service = new AuthSessionService(api, store);

            var result = RunAsync(() => service.LoadCurrentUserAsync(CancellationToken.None));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(AuthState.Expired, service.State);
            Assert.AreEqual(AuthApiError.SessionExpired, result.ErrorCode);
            Assert.IsNull(RunAsync(() => store.LoadAsync(CancellationToken.None)));
        }

        [Test]
        public void NoTokenCurrentUserReturnsAuthRequiredWithoutServerCall()
        {
            var api = new FakeAuthApiClient();
            var service = new AuthSessionService(api, new InMemoryTokenStore());

            var result = RunAsync(() => service.LoadCurrentUserAsync(CancellationToken.None));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(AuthApiError.AuthRequired, result.ErrorCode);
            Assert.AreEqual(0, api.CurrentUserCount);
        }

        [Test]
        public void LogoutAndAuthFailureDoNotDeleteLocalSafeSessions()
        {
            var repository = new FakeRepository { Current = CreateSaveData() };
            var store = new InMemoryTokenStore();
            RunAsync(() => store.SaveAsync(new AuthSession { AccessToken = Jwt(DateTimeOffset.UtcNow.AddMinutes(15)), RefreshToken = "refresh" }, CancellationToken.None));
            var api = new FakeAuthApiClient
            {
                LoginResult = AuthApiResult<AuthTokenResponse>.Failure(AuthApiError.InvalidCredentials, AuthApiError.ToSafeMessage(AuthApiError.InvalidCredentials), 401)
            };
            var service = new AuthSessionService(api, store);

            RunAsync(() => service.LoginAsync("test@example.com", "wrong-password", CancellationToken.None));
            Assert.AreEqual(1, RunAsync(() => repository.LoadAsync(CancellationToken.None)).WorkSessionSummaries.Count);

            RunAsync(() => service.LoadSessionAsync(CancellationToken.None));
            RunAsync(() => service.LogoutAsync(CancellationToken.None));

            Assert.AreEqual(1, RunAsync(() => repository.LoadAsync(CancellationToken.None)).WorkSessionSummaries.Count);
        }

        [Test]
        public void InvalidBaseUrlFailsBeforeSendingCredentials()
        {
            var api = new FakeAuthApiClient
            {
                LoginResult = AuthApiResult<AuthTokenResponse>.Success(new AuthTokenResponse
                {
                    AccessToken = Jwt(DateTimeOffset.UtcNow.AddMinutes(15)),
                    RefreshToken = "refresh",
                    User = new AuthUserDto { Id = "user-1", Email = "test@example.com" }
                }, 200)
            };
            var service = new AuthSessionService(api, new InMemoryTokenStore());

            service.SetBaseUrl("not a url");
            var result = RunAsync(() => service.LoginAsync("test@example.com", "password123", CancellationToken.None));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(AuthApiError.InvalidBaseUrl, result.ErrorCode);
            Assert.AreEqual(0, api.LoginCount);

            service.SetBaseUrl("http://localhost:3000/api/v1");
            var recovered = RunAsync(() => service.LoginAsync("test@example.com", "password123", CancellationToken.None));
            Assert.IsTrue(recovered.IsSuccess);
            Assert.AreEqual(1, api.LoginCount);
        }

        private static SaveData CreateSaveData()
        {
            var saveData = SaveData.CreateDefault();
            saveData.WorkSessionSummaries.Add(new AgentWorkSession
            {
                SessionId = "local-safe-session",
                WorkType = WorkType.Feature,
                StartedAt = DateTimeOffset.UtcNow.AddHours(-1),
                EndedAt = DateTimeOffset.UtcNow,
                Confidence = ProviderConfidence.High
            });
            return saveData;
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
