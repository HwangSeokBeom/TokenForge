using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TokenForge.Client.Agents;
using TokenForge.Client.Common;
using TokenForge.Client.Domain;
using TokenForge.Client.Git;
using TokenForge.Client.Persistence;
using TokenForge.Client.Platform;
using TokenForge.Client.Sync;
using TokenForge.Client.UI;

namespace TokenForge.Client.Tests
{
    public sealed class Phase10SafeSyncUiTests
    {
        private sealed class FakeSafeSyncService : ISafeSyncService
        {
            public int SyncCount { get; private set; }
            public int HealthCount { get; private set; }
            public SafeSyncStatus Status { get; private set; } = SafeSyncStatus.Idle;
            public string BaseUrl { get; private set; } = "http://localhost:3000/api/v1";
            public SafeSyncResult NextSyncResult { get; set; } = SafeSyncResult.Success(SafeSyncStatus.Synced);

            public void SetBaseUrl(string baseUrl)
            {
                BaseUrl = baseUrl;
            }

            public Task<SafeSyncResult> CheckHealthAsync(CancellationToken cancellationToken = default)
            {
                HealthCount += 1;
                Status = SafeSyncStatus.Ready;
                return Task.FromResult(SafeSyncResult.Success(Status));
            }

            public Task<SafeSyncResult> SyncNowAsync(CancellationToken cancellationToken = default)
            {
                SyncCount += 1;
                Status = NextSyncResult.Status;
                return Task.FromResult(NextSyncResult);
            }

            public Task<SafeSyncResult> FetchRemoteSessionsAsync(CancellationToken cancellationToken = default)
            {
                Status = SafeSyncStatus.Ready;
                return Task.FromResult(new SafeSyncResult
                {
                    IsSuccess = true,
                    Status = Status,
                    RemoteSessions = new List<RemoteSafeSessionSummary>
                    {
                        new RemoteSafeSessionSummary
                        {
                            ServerSessionId = "11111111-1111-4111-8111-111111111111",
                            SourceProvider = "CODEX",
                            DayBucket = "2026-05-14",
                            ActivityCategory = "WORK_FEATURE",
                            ChangeCountBucket = "FEW",
                            Confidence = "HIGH"
                        }
                    }
                });
            }

            public Task<SafeSyncResult> DeleteRemoteSessionAsync(string serverSessionId, CancellationToken cancellationToken = default)
            {
                Status = SafeSyncStatus.Ready;
                return Task.FromResult(SafeSyncResult.Success(Status));
            }
        }

        private sealed class FakeRepository : ILocalSaveDataRepository
        {
            public SaveData Current { get; set; } = SaveData.CreateDefault();
            public Task<SaveData> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Current);
            public Task<Result> SaveAsync(SaveData saveData, CancellationToken cancellationToken = default) => Task.FromResult(Result.Success());
        }

        private sealed class NullRepositoryPicker : IRepositoryPicker
        {
            public Task<RepositoryPickerResult> PickRepositoryAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(RepositoryPickerResult.Unavailable());
            }
        }

        private sealed class NullAgentLogLocationPicker : IAgentLogLocationPicker
        {
            public Task<AgentLogLocationPickerResult> PickAgentLogLocationAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(AgentLogLocationPickerResult.Unavailable());
            }
        }

        [Test]
        public void SafeSyncButtonTriggersExplicitSyncOnly()
        {
            var sync = new FakeSafeSyncService();
            var viewModel = CreateViewModel(sync);

            Assert.AreEqual(0, sync.SyncCount);
            RunAsync(() => viewModel.SyncSafeSessionsAsync(CancellationToken.None));

            Assert.AreEqual(1, sync.SyncCount);
            Assert.AreEqual(SafeSyncStatus.Synced, viewModel.SafeSyncStatus);
        }

        [Test]
        public void HealthCheckUpdatesStatus()
        {
            var sync = new FakeSafeSyncService();
            var viewModel = CreateViewModel(sync);

            RunAsync(() => viewModel.CheckSyncHealthAsync(CancellationToken.None));

            Assert.AreEqual(1, sync.HealthCount);
            Assert.AreEqual(SafeSyncStatus.Ready, viewModel.SafeSyncStatus);
        }

        [Test]
        public void AuthRequiredDisplaysSafely()
        {
            var sync = new FakeSafeSyncService
            {
                NextSyncResult = SafeSyncResult.Failure(SafeSyncStatus.AuthRequired, SafeSyncApiError.AuthRequired, "auth required")
            };
            var viewModel = CreateViewModel(sync);

            RunAsync(() => viewModel.SyncSafeSessionsAsync(CancellationToken.None));

            Assert.AreEqual(SafeSyncStatus.AuthRequired, viewModel.SafeSyncStatus);
            Assert.AreEqual(SafeSyncApiError.AuthRequired, viewModel.SafeSyncErrorCode);
        }

        [Test]
        public void RemoteSessionListContainsNoRawData()
        {
            var viewModel = CreateViewModel(new FakeSafeSyncService());

            RunAsync(() => viewModel.FetchSyncedSessionsAsync(CancellationToken.None));
            var serialized = string.Join("\n", viewModel.RemoteSafeSessions.Select(session =>
                session.ServerSessionId + session.SourceProvider + session.DayBucket + session.ActivityCategory + session.ChangeCountBucket + session.Confidence));

            Assert.AreEqual(1, viewModel.RemoteSafeSessions.Count);
            Assert.IsFalse(serialized.Contains("path"));
            Assert.IsFalse(serialized.Contains("prompt"));
            Assert.IsFalse(serialized.Contains("command"));
            Assert.IsFalse(serialized.Contains("token"));
        }

        private static ApprovedActivityAnalysisViewModel CreateViewModel(ISafeSyncService syncService)
        {
            var repository = new FakeRepository();
            return new ApprovedActivityAnalysisViewModel(
                new GitAnalysisFlowController(
                    new NullRepositoryPicker(),
                    new GitAggregateAnalyzer(),
                    repository),
                new AgentAnalysisFlowController(
                    new AgentLogActivityProvider(new AgentActivityAnalyzer()),
                    repository),
                new NullAgentLogLocationPicker(),
                repository,
                null,
                new ApprovedLocationSettingsRepository(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TokenForgeTests", System.IO.Path.GetRandomFileName())),
                syncService);
        }

        private static T RunAsync<T>(Func<Task<T>> taskFactory)
        {
            return Task.Run(taskFactory).GetAwaiter().GetResult();
        }
    }
}
