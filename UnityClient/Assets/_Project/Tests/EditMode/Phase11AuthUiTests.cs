using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TokenForge.Client.Agents;
using TokenForge.Client.Auth;
using TokenForge.Client.Common;
using TokenForge.Client.Domain;
using TokenForge.Client.Git;
using TokenForge.Client.Persistence;
using TokenForge.Client.Platform;
using TokenForge.Client.Sync;
using TokenForge.Client.UI;

namespace TokenForge.Client.Tests
{
    public sealed class Phase11AuthUiTests
    {
        private sealed class FakeAuthSessionService : IAuthSessionService
        {
            public int LoginCount { get; private set; }
            public int SignupCount { get; private set; }
            public int CurrentUserCount { get; private set; }
            public int LogoutCount { get; private set; }
            public AuthState State { get; private set; } = AuthState.LoggedOut;
            public AuthSession CurrentSession { get; private set; }
            public string ErrorCode { get; private set; } = string.Empty;
            public string BaseUrl { get; private set; } = "http://localhost:3000/api/v1";
            public bool HasUsableAccessToken => State == AuthState.LoggedIn;
            public void SetBaseUrl(string baseUrl) => BaseUrl = baseUrl;
            public Task<AuthResult> LoadSessionAsync(CancellationToken cancellationToken = default) => Task.FromResult(AuthResult.Success(State));
            public Task<AuthResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
            {
                LoginCount += 1;
                State = AuthState.LoggedIn;
                CurrentSession = new AuthSession { UserId = "user-1", Email = email, DisplayName = "Tester", AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15) };
                return Task.FromResult(AuthResult.Success(State));
            }
            public Task<AuthResult> SignupAsync(string email, string password, string displayName = "", CancellationToken cancellationToken = default)
            {
                SignupCount += 1;
                State = AuthState.LoggedIn;
                CurrentSession = new AuthSession { UserId = "user-1", Email = email, DisplayName = displayName, AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15) };
                return Task.FromResult(AuthResult.Success(State));
            }
            public Task<AuthResult> LoadCurrentUserAsync(CancellationToken cancellationToken = default)
            {
                CurrentUserCount += 1;
                return Task.FromResult(State == AuthState.LoggedIn
                    ? AuthResult.Success(State)
                    : AuthResult.Failure(AuthState.AuthRequired, AuthApiError.AuthRequired, "auth required"));
            }
            public Task<AuthResult> LogoutAsync(CancellationToken cancellationToken = default)
            {
                LogoutCount += 1;
                State = AuthState.LoggedOut;
                CurrentSession = null;
                return Task.FromResult(AuthResult.Success(State));
            }
            public Task<string> GetAccessTokenForSyncAsync(CancellationToken cancellationToken = default) => Task.FromResult(State == AuthState.LoggedIn ? "access" : string.Empty);
            public Task<string> ForceRefreshForRetryAsync(CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
        }

        private sealed class FakeSafeSyncService : ISafeSyncService
        {
            public int SyncCount { get; private set; }
            public SafeSyncStatus Status { get; private set; } = SafeSyncStatus.Idle;
            public string BaseUrl { get; private set; } = "http://localhost:3000/api/v1";
            public void SetBaseUrl(string baseUrl) => BaseUrl = baseUrl;
            public Task<SafeSyncResult> CheckHealthAsync(CancellationToken cancellationToken = default) => Task.FromResult(SafeSyncResult.Success(SafeSyncStatus.Ready));
            public Task<SafeSyncResult> SyncNowAsync(CancellationToken cancellationToken = default)
            {
                SyncCount += 1;
                return Task.FromResult(SafeSyncResult.Failure(SafeSyncStatus.AuthRequired, SafeSyncApiError.AuthRequired, "auth required"));
            }
            public Task<SafeSyncResult> FetchRemoteSessionsAsync(CancellationToken cancellationToken = default) => Task.FromResult(SafeSyncResult.Success(SafeSyncStatus.Ready));
            public Task<SafeSyncResult> DeleteRemoteSessionAsync(string serverSessionId, CancellationToken cancellationToken = default) => Task.FromResult(SafeSyncResult.Success(SafeSyncStatus.Ready));
        }

        private sealed class FakeRepository : ILocalSaveDataRepository
        {
            public SaveData Current { get; set; } = SaveData.CreateDefault();
            public Task<SaveData> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Current);
            public Task<Result> SaveAsync(SaveData saveData, CancellationToken cancellationToken = default) => Task.FromResult(Result.Success());
        }

        private sealed class NullRepositoryPicker : IRepositoryPicker
        {
            public Task<RepositoryPickerResult> PickRepositoryAsync(CancellationToken cancellationToken = default) => Task.FromResult(RepositoryPickerResult.Unavailable());
        }

        private sealed class NullAgentLogLocationPicker : IAgentLogLocationPicker
        {
            public Task<AgentLogLocationPickerResult> PickAgentLogLocationAsync(CancellationToken cancellationToken = default) => Task.FromResult(AgentLogLocationPickerResult.Unavailable());
        }

        [Test]
        public void LoginButtonActionUpdatesAuthStateWithoutStartingSync()
        {
            var auth = new FakeAuthSessionService();
            var sync = new FakeSafeSyncService();
            var viewModel = CreateViewModel(sync, auth);

            RunAsync(() => viewModel.LoginAsync("test@example.com", "password123", CancellationToken.None));

            Assert.AreEqual(AuthState.LoggedIn, viewModel.AuthState);
            Assert.AreEqual(1, auth.LoginCount);
            Assert.AreEqual(0, sync.SyncCount);
            Assert.IsFalse(viewModel.AuthStatusMessage.Contains("password123"));
            Assert.IsFalse(viewModel.AuthStatusMessage.Contains("access"));
        }

        [Test]
        public void LogoutClearsAuthState()
        {
            var auth = new FakeAuthSessionService();
            var viewModel = CreateViewModel(new FakeSafeSyncService(), auth);
            RunAsync(() => viewModel.LoginAsync("test@example.com", "password123", CancellationToken.None));

            RunAsync(() => viewModel.LogoutAsync(CancellationToken.None));

            Assert.AreEqual(AuthState.LoggedOut, viewModel.AuthState);
            Assert.AreEqual(1, auth.LogoutCount);
        }

        [Test]
        public void SyncWhileLoggedOutDisplaysAuthRequired()
        {
            var sync = new FakeSafeSyncService();
            var viewModel = CreateViewModel(sync, new FakeAuthSessionService());

            RunAsync(() => viewModel.SyncSafeSessionsAsync(CancellationToken.None));

            Assert.AreEqual(SafeSyncStatus.AuthRequired, viewModel.SafeSyncStatus);
            Assert.AreEqual(SafeSyncApiError.AuthRequired, viewModel.SafeSyncErrorCode);
        }

        [Test]
        public void LocalOnlyFeaturesRemainAvailableWhenLoggedOut()
        {
            var viewModel = CreateViewModel(new FakeSafeSyncService(), new FakeAuthSessionService());

            var refresh = RunAsync(() => viewModel.RefreshRecentSessionsAsync(CancellationToken.None));

            Assert.IsTrue(refresh.IsSuccess);
            Assert.AreEqual(AuthState.LoggedOut, viewModel.AuthState);
        }

        private static ApprovedActivityAnalysisViewModel CreateViewModel(ISafeSyncService syncService, IAuthSessionService authService)
        {
            var repository = new FakeRepository();
            return new ApprovedActivityAnalysisViewModel(
                new GitAnalysisFlowController(new NullRepositoryPicker(), new GitAggregateAnalyzer(), repository),
                new AgentAnalysisFlowController(new AgentLogActivityProvider(new AgentActivityAnalyzer()), repository),
                new NullAgentLogLocationPicker(),
                repository,
                null,
                new ApprovedLocationSettingsRepository(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TokenForgeTests", System.IO.Path.GetRandomFileName())),
                syncService,
                authService);
        }

        private static T RunAsync<T>(Func<Task<T>> taskFactory)
        {
            return Task.Run(taskFactory).GetAwaiter().GetResult();
        }
    }
}
