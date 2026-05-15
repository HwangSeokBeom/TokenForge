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
    public sealed class Phase13SafeSyncUiPolishTests
    {
        private sealed class SlowSafeSyncService : ISafeSyncService
        {
            private readonly TaskCompletionSource<SafeSyncResult> syncCompletion = new TaskCompletionSource<SafeSyncResult>();
            public int SyncCount { get; private set; }
            public SafeSyncStatus Status { get; private set; } = SafeSyncStatus.Idle;
            public string BaseUrl { get; private set; } = "http://localhost:3000/api/v1";
            public void SetBaseUrl(string baseUrl) => BaseUrl = baseUrl;
            public Task<SafeSyncResult> CheckHealthAsync(CancellationToken cancellationToken = default) => Task.FromResult(SafeSyncResult.Success(SafeSyncStatus.Ready));
            public Task<SafeSyncResult> SyncNowAsync(CancellationToken cancellationToken = default)
            {
                SyncCount += 1;
                Status = SafeSyncStatus.Syncing;
                return syncCompletion.Task;
            }
            public void CompleteSync() => syncCompletion.TrySetResult(new SafeSyncResult { IsSuccess = true, Status = SafeSyncStatus.Synced, AcceptedCount = 2, RejectedCount = 0 });
            public Task<SafeSyncResult> FetchRemoteSessionsAsync(CancellationToken cancellationToken = default) => Task.FromResult(SafeSyncResult.Success(SafeSyncStatus.Ready));
            public Task<SafeSyncResult> DeleteRemoteSessionAsync(string serverSessionId, CancellationToken cancellationToken = default) => Task.FromResult(SafeSyncResult.Success(SafeSyncStatus.Ready));
        }

        private sealed class FakeAuthSessionService : IAuthSessionService
        {
            public AuthState State { get; private set; } = AuthState.LoggedIn;
            public AuthSession CurrentSession { get; private set; } = new AuthSession { AccessToken = "access-token-secret", AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15), UserId = "user-1" };
            public string ErrorCode { get; private set; } = string.Empty;
            public string BaseUrl { get; private set; } = "http://localhost:3000/api/v1";
            public bool HasUsableAccessToken { get; set; } = true;
            public void SetBaseUrl(string baseUrl) => BaseUrl = baseUrl;
            public Task<AuthResult> LoadSessionAsync(CancellationToken cancellationToken = default) => Task.FromResult(AuthResult.Success(State));
            public Task<AuthResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default) => Task.FromResult(AuthResult.Success(State));
            public Task<AuthResult> SignupAsync(string email, string password, string displayName = "", CancellationToken cancellationToken = default) => Task.FromResult(AuthResult.Success(State));
            public Task<AuthResult> LoadCurrentUserAsync(CancellationToken cancellationToken = default) => Task.FromResult(AuthResult.Success(State));
            public Task<AuthResult> LogoutAsync(CancellationToken cancellationToken = default) => Task.FromResult(AuthResult.Success(AuthState.LoggedOut));
            public Task<string> GetAccessTokenForSyncAsync(CancellationToken cancellationToken = default) => Task.FromResult(HasUsableAccessToken ? "access-token-secret" : string.Empty);
            public Task<string> ForceRefreshForRetryAsync(CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
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
        public void SafeSyncStatusLabelsAndMessagesAreUserFacing()
        {
            Assert.AreEqual("Checking Server", SafeUserMessageMapper.SafeSyncStatusLabel(SafeSyncStatus.CheckingHealth));
            Assert.AreEqual("Server Ready", SafeUserMessageMapper.SafeSyncStatusLabel(SafeSyncStatus.Ready));
            Assert.AreEqual("Fetching Remote Sessions", SafeUserMessageMapper.SafeSyncStatusLabel(SafeSyncStatus.Fetching));
            Assert.AreEqual("Log in to use server sync.", SafeUserMessageMapper.FromSyncError(SafeSyncApiError.AuthRequired));
            Assert.AreEqual("Sync was blocked because the outgoing payload failed the privacy check.", SafeUserMessageMapper.FromSyncError(SafeSyncApiError.PrivacyGuardBlockedPayload));
        }

        [Test]
        public void DuplicateSyncActionDoesNotStartMultipleServiceCalls()
        {
            var sync = new SlowSafeSyncService();
            var viewModel = CreateViewModel(sync, new FakeAuthSessionService());

            var first = viewModel.SyncSafeSessionsAsync(CancellationToken.None);
            var second = RunAsync(() => viewModel.SyncSafeSessionsAsync(CancellationToken.None));
            sync.CompleteSync();
            RunAsync(() => first);

            Assert.AreEqual(1, sync.SyncCount);
            Assert.IsFalse(second.IsSuccess);
            Assert.AreEqual(SafeSyncApiError.AlreadyInProgress, second.ErrorCode);
        }

        [Test]
        public void SyncFetchAndDeleteRequireAuthWhenAuthServiceIsPresent()
        {
            var sync = new SlowSafeSyncService();
            var auth = new FakeAuthSessionService { HasUsableAccessToken = false };
            var viewModel = CreateViewModel(sync, auth);

            var result = RunAsync(() => viewModel.SyncSafeSessionsAsync(CancellationToken.None));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(SafeSyncStatus.AuthRequired, viewModel.SafeSyncStatus);
            Assert.AreEqual(0, sync.SyncCount);
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
