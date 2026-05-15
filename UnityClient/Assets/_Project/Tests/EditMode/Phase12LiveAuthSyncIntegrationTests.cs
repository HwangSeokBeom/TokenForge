using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TokenForge.Client.Auth;
using TokenForge.Client.Common;
using TokenForge.Client.Domain;
using TokenForge.Client.Persistence;
using TokenForge.Client.Sync;

namespace TokenForge.Client.Tests
{
    internal static class Phase12LiveIntegrationSettings
    {
        public const string Enabled = "TOKENFORGE_LIVE_INTEGRATION_ENABLED";
        public const string BaseUrl = "TOKENFORGE_LIVE_BASE_URL";
        public const string Email = "TOKENFORGE_LIVE_EMAIL";
        public const string Password = "TOKENFORGE_LIVE_PASSWORD";
        public const string SignupEmail = "TOKENFORGE_LIVE_SIGNUP_EMAIL";
        public const string SignupPassword = "TOKENFORGE_LIVE_SIGNUP_PASSWORD";
        public const string AllowSignup = "TOKENFORGE_LIVE_ALLOW_SIGNUP";

        public static bool IsEnabled()
        {
            return string.Equals(Environment.GetEnvironmentVariable(Enabled), "true", StringComparison.OrdinalIgnoreCase);
        }

        public static string GetRequired(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value))
            {
                Assert.Ignore(name + " is required for opt-in live integration tests.");
            }

            return value;
        }

        public static string RedactedEnvironmentSummary()
        {
            return "enabled=" + RedactNonSecret(Environment.GetEnvironmentVariable(Enabled)) +
                   " baseUrl=" + RedactNonSecret(Environment.GetEnvironmentVariable(BaseUrl)) +
                   " email=" + RedactEmail(Environment.GetEnvironmentVariable(Email)) +
                   " password=<redacted>" +
                   " signupEmail=" + RedactEmail(Environment.GetEnvironmentVariable(SignupEmail)) +
                   " signupPassword=<redacted>";
        }

        private static string RedactNonSecret(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "<unset>" : value.Trim();
        }

        private static string RedactEmail(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "<unset>";
            }

            var at = value.IndexOf('@');
            return at <= 1 ? "<redacted-email>" : value.Substring(0, 1) + "***" + value.Substring(at);
        }
    }

    public sealed class Phase12LiveAuthSyncIntegrationTests
    {
        private const string ClientSessionPrefix = "phase12-live-test-";

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
        public void LiveHealthAuthAndSafeSyncRoundTrip_WhenExplicitlyEnabled()
        {
            RequireEnabled();
            RunAsync(async () =>
            {
                var baseUrl = Phase12LiveIntegrationSettings.GetRequired(Phase12LiveIntegrationSettings.BaseUrl);
                var email = Phase12LiveIntegrationSettings.GetRequired(Phase12LiveIntegrationSettings.Email);
                var password = Phase12LiveIntegrationSettings.GetRequired(Phase12LiveIntegrationSettings.Password);
                var logger = new InMemorySafeSyncLogger();
                var transport = new SystemNetHttpTransport();
                var tokenStore = new InMemoryTokenStore();
                var authService = new AuthSessionService(
                    new AuthApiClient(new AuthApiConfig(baseUrl), transport, logger),
                    tokenStore);
                var tokenProvider = new AuthSessionTokenProvider(authService);
                var repository = new FakeRepository { Current = CreateLiveSafeSaveData(BuildClientSessionId()) };
                var syncService = new SafeSyncService(
                    repository,
                    new SafeSyncApiClient(new SafeSyncApiConfig(baseUrl), tokenProvider, transport, logger));

                RemoteSafeSessionSummary created = null;
                try
                {
                    var health = await syncService.CheckHealthAsync(CancellationToken.None);
                    Assert.IsTrue(health.IsSuccess, health.ErrorCode);

                    var login = await authService.LoginAsync(email, password, CancellationToken.None);
                    Assert.IsTrue(login.IsSuccess, login.ErrorCode);

                    var me = await authService.LoadCurrentUserAsync(CancellationToken.None);
                    Assert.IsTrue(me.IsSuccess, me.ErrorCode);

                    var sync = await syncService.SyncNowAsync(CancellationToken.None);
                    Assert.IsTrue(sync.IsSuccess, sync.ErrorCode);

                    var fetch = await syncService.FetchRemoteSessionsAsync(CancellationToken.None);
                    Assert.IsTrue(fetch.IsSuccess, fetch.ErrorCode);
                    created = fetch.RemoteSessions.FirstOrDefault(item => item.ClientSessionId == repository.Current.WorkSessionSummaries[0].SessionId);
                    Assert.IsNotNull(created);

                    var delete = await syncService.DeleteRemoteSessionAsync(created.ServerSessionId, CancellationToken.None);
                    Assert.IsTrue(delete.IsSuccess, delete.ErrorCode);
                    created = null;

                    var afterDelete = await syncService.FetchRemoteSessionsAsync(CancellationToken.None);
                    Assert.IsTrue(afterDelete.IsSuccess, afterDelete.ErrorCode);
                    Assert.IsFalse(afterDelete.RemoteSessions.Any(item => item.ClientSessionId == repository.Current.WorkSessionSummaries[0].SessionId));

                    var logout = await authService.LogoutAsync(CancellationToken.None);
                    Assert.IsTrue(logout.IsSuccess);

                    AssertNoSensitiveLogValues(logger, email, password);
                }
                finally
                {
                    if (created != null)
                    {
                        var cleanup = await syncService.DeleteRemoteSessionAsync(created.ServerSessionId, CancellationToken.None);
                        if (!cleanup.IsSuccess)
                        {
                            TestContext.WriteLine("cleanup warning: " + cleanup.ErrorCode);
                        }
                    }

                    await tokenStore.ClearAsync(CancellationToken.None);
                }
            });
        }

        [Test]
        public void LiveSignup_WhenExplicitlyAllowed()
        {
            RequireEnabled();
            if (!string.Equals(Environment.GetEnvironmentVariable(Phase12LiveIntegrationSettings.AllowSignup), "true", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Ignore("Live signup is opt-in and requires TOKENFORGE_LIVE_ALLOW_SIGNUP=true.");
            }

            RunAsync(async () =>
            {
                var baseUrl = Phase12LiveIntegrationSettings.GetRequired(Phase12LiveIntegrationSettings.BaseUrl);
                var email = Phase12LiveIntegrationSettings.GetRequired(Phase12LiveIntegrationSettings.SignupEmail);
                var password = Phase12LiveIntegrationSettings.GetRequired(Phase12LiveIntegrationSettings.SignupPassword);
                var logger = new InMemorySafeSyncLogger();
                var tokenStore = new InMemoryTokenStore();
                var authService = new AuthSessionService(
                    new AuthApiClient(new AuthApiConfig(baseUrl), new SystemNetHttpTransport(), logger),
                    tokenStore);

                try
                {
                    var signup = await authService.SignupAsync(email, password, "Phase12 Live Test", CancellationToken.None);
                    Assert.IsTrue(signup.IsSuccess, signup.ErrorCode);

                    var me = await authService.LoadCurrentUserAsync(CancellationToken.None);
                    Assert.IsTrue(me.IsSuccess, me.ErrorCode);

                    await authService.LogoutAsync(CancellationToken.None);
                    AssertNoSensitiveLogValues(logger, email, password);
                }
                finally
                {
                    await tokenStore.ClearAsync(CancellationToken.None);
                }
            });
        }

        private static void RequireEnabled()
        {
            if (!Phase12LiveIntegrationSettings.IsEnabled())
            {
                Assert.Ignore("Set TOKENFORGE_LIVE_INTEGRATION_ENABLED=true to run opt-in live auth/sync tests.");
            }
        }

        private static SaveData CreateLiveSafeSaveData(string clientSessionId)
        {
            var saveData = SaveData.CreateDefault();
            saveData.WorkSessionSummaries.Add(new AgentWorkSession
            {
                SessionId = clientSessionId,
                AgentType = AgentType.Codex,
                SourceProvider = "CODEX",
                SourceProviders = new List<string> { "CODEX" },
                WorkType = WorkType.Test,
                StartedAt = new DateTimeOffset(2026, 5, 15, 1, 0, 0, TimeSpan.Zero),
                EndedAt = new DateTimeOffset(2026, 5, 15, 2, 0, 0, TimeSpan.Zero),
                Confidence = ProviderConfidence.High,
                GitChangeSummary = new GitChangeSummary
                {
                    ProjectPathHash = "1234567890abcdef",
                    ChangedFileCount = 2,
                    AddedLineBucket = LineChangeBucket.Small,
                    DeletedLineBucket = LineChangeBucket.None,
                    CommitCountBucket = CountBucket.One,
                    AnalysisTimeBucket = "2026-05-15"
                },
                AgentActivitySummary = new AgentActivitySummary
                {
                    ProviderType = AgentProviderType.Codex,
                    SourceIdentifierHash = "abcdef1234567890",
                    DayBucket = "2026-05-15",
                    SessionCountBucket = CountBucket.One,
                    InteractionCountBucket = CountBucket.Small,
                    EstimatedCodingActivityBucket = CountBucket.Small,
                    ToolUsageCategoryBuckets = new List<AgentToolUsageCategoryBucket>
                    {
                        new AgentToolUsageCategoryBucket { Category = AgentToolUsageCategory.CodeEditing, CountBucket = CountBucket.Small }
                    },
                    LanguageCategoryBuckets = new List<AgentLanguageCategoryBucket>
                    {
                        new AgentLanguageCategoryBucket { Category = AgentLanguageCategory.CSharp, CountBucket = CountBucket.One }
                    }
                },
                ActionSummary = new AgentActionSummary
                {
                    DurationBucket = DurationBucket.FifteenTo60Minutes,
                    TestRunCount = 1
                },
                UserReviewed = true
            });
            return saveData;
        }

        private static string BuildClientSessionId()
        {
            return ClientSessionPrefix + Guid.NewGuid().ToString("N");
        }

        private static void AssertNoSensitiveLogValues(InMemorySafeSyncLogger logger, string email, string password)
        {
            var logs = string.Join("\n", logger.Entries);
            Assert.IsFalse(logs.Contains(password));
            Assert.IsFalse(logs.Contains(email));
            Assert.IsFalse(logs.Contains("accessToken"));
            Assert.IsFalse(logs.Contains("refreshToken"));
            Assert.IsFalse(logs.Contains("Bearer"));
        }

        private static void RunAsync(Func<Task> taskFactory)
        {
            Task.Run(taskFactory).GetAwaiter().GetResult();
        }
    }
}
