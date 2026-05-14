using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TokenForge.Client.Domain;
using TokenForge.Client.Persistence;
using TokenForge.Client.Sync;

namespace TokenForge.Client.Tests
{
    public sealed class BackendSyncSmokeFlowPhase3Tests
    {
        private sealed class FakeTransport : ITokenForgeHttpTransport
        {
            private readonly Queue<SafeHttpResponse> responses = new Queue<SafeHttpResponse>();

            public List<SafeHttpRequest> Requests { get; } = new List<SafeHttpRequest>();

            public void Enqueue(SafeHttpResponse response)
            {
                responses.Enqueue(response);
            }

            public Task<SafeHttpResponse> SendAsync(SafeHttpRequest request, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Requests.Add(request);
                return Task.FromResult(responses.Count > 0
                    ? responses.Dequeue()
                    : new SafeHttpResponse { StatusCode = 500, BodyJson = "{\"code\":\"unexpected_request\"}" });
            }
        }

        [Test]
        public void DevGuestAuthTokenProvider_ParsesTokenFromValidResponse()
        {
            var transport = new FakeTransport();
            transport.Enqueue(new SafeHttpResponse { StatusCode = 200, BodyJson = "{\"accessToken\":\"phase3-secret\"}" });
            var logger = new InMemorySafeSyncLogger();
            var provider = new DevGuestAuthTokenProvider(ApiConfiguration.CreateLocalDevelopment(), transport, logger);

            var token = RunAsync(() => provider.GetBearerTokenAsync(CancellationToken.None));
            var cached = RunAsync(() => provider.GetBearerTokenAsync(CancellationToken.None));

            Assert.AreEqual("phase3-secret", token);
            Assert.AreEqual("phase3-secret", cached);
            Assert.AreEqual(1, transport.Requests.Count);
            Assert.AreEqual("POST", transport.Requests[0].Method);
            Assert.AreEqual("http://localhost:3000/api/v1/auth/guest", transport.Requests[0].Url);
        }

        [Test]
        public void DevGuestAuthTokenProvider_DoesNotLogToken()
        {
            var transport = new FakeTransport();
            transport.Enqueue(new SafeHttpResponse { StatusCode = 200, BodyJson = "{\"accessToken\":\"phase3-secret-token\"}" });
            var logger = new InMemorySafeSyncLogger();
            var provider = new DevGuestAuthTokenProvider(ApiConfiguration.CreateLocalDevelopment(), transport, logger);

            RunAsync(() => provider.GetBearerTokenAsync(CancellationToken.None));

            var logText = string.Join("\n", logger.Entries);
            Assert.IsFalse(logText.Contains("phase3-secret-token"));
            Assert.IsFalse(logText.Contains("accessToken"));
            Assert.IsFalse(logText.Contains("Authorization"));
            Assert.IsFalse(logText.Contains("authorization"));
        }

        [TestCase(401, "{\"code\":\"unauthorized\"}")]
        [TestCase(500, "{\"code\":\"server_error\"}")]
        [TestCase(200, "{}")]
        [TestCase(200, "not-json")]
        public void DevGuestAuthTokenProvider_FailsSafely(int statusCode, string bodyJson)
        {
            var transport = new FakeTransport();
            transport.Enqueue(new SafeHttpResponse { StatusCode = statusCode, BodyJson = bodyJson });
            var logger = new InMemorySafeSyncLogger();
            var provider = new DevGuestAuthTokenProvider(ApiConfiguration.CreateLocalDevelopment(), transport, logger);

            var token = RunAsync(() => provider.GetBearerTokenAsync(CancellationToken.None));

            Assert.AreEqual(string.Empty, token);
            Assert.AreEqual(1, transport.Requests.Count);
            Assert.IsTrue(logger.Entries.Count > 0);
        }

        [Test]
        public void ApiConfiguration_UsesLocalhostApiV1ForDevRealBackendMode()
        {
            var configuration = ApiConfiguration.CreateLocalDevelopment();

            Assert.AreEqual("http://localhost:3000/api/v1", ApiConfiguration.DefaultBaseUrl);
            Assert.AreEqual("http://localhost:3000/api/v1", configuration.BaseUrl);
            Assert.AreEqual("http://localhost:3000/api/v1/sync/push", configuration.BuildUrl("/sync/push"));
        }

        [Test]
        public void SafeSyncMapper_MapsSourceProviderToBackendUppercaseEnum()
        {
            var saveData = CreateSafeSaveData();
            saveData.WorkSessionSummaries[0].SourceProvider = "ManualSessionProvider";
            saveData.WorkSessionSummaries[0].SourceProviders = new List<string> { "ManualSessionProvider", "CodexLogProvider" };

            var payload = new SafeSyncMapper().ToPayload(saveData);

            Assert.AreEqual("MANUAL", payload.SessionSummary.Sessions[0].SourceProvider);
            CollectionAssert.AreEquivalent(new[] { "MANUAL", "CODEX" }, payload.SessionSummary.Sessions[0].SourceProviders);
        }

        [Test]
        public void BackendSmokeFlow_CallsAuthPushPullInOrder()
        {
            var directory = TempDirectory();
            var repository = new SaveDataRepository(directory);
            Assert.IsTrue(RunAsync(() => repository.SaveAsync(CreateSafeSaveData())).IsSuccess);
            var transport = CreateSuccessfulSmokeTransport("phase3-secret");
            var logger = new InMemorySafeSyncLogger();
            var flow = CreateFlow(repository, transport, logger);

            var result = RunAsync(() => flow.RunAsync(CancellationToken.None));

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            Assert.AreEqual(3, transport.Requests.Count);
            Assert.AreEqual("POST", transport.Requests[0].Method);
            Assert.IsTrue(transport.Requests[0].Url.EndsWith("/auth/guest", StringComparison.Ordinal));
            Assert.AreEqual(string.Empty, transport.Requests[0].BearerToken);
            Assert.AreEqual("POST", transport.Requests[1].Method);
            Assert.IsTrue(transport.Requests[1].Url.EndsWith("/sync/push", StringComparison.Ordinal));
            Assert.AreEqual("phase3-secret", transport.Requests[1].BearerToken);
            Assert.AreEqual("GET", transport.Requests[2].Method);
            Assert.IsTrue(transport.Requests[2].Url.EndsWith("/sync/pull", StringComparison.Ordinal));
            Assert.AreEqual("phase3-secret", transport.Requests[2].BearerToken);
        }

        [Test]
        public void BackendSmokeFlow_MergesPullResponseAdditively()
        {
            var directory = TempDirectory();
            var repository = new SaveDataRepository(directory);
            Assert.IsTrue(RunAsync(() => repository.SaveAsync(CreateSafeSaveData())).IsSuccess);
            var flow = CreateFlow(repository, CreateSuccessfulSmokeTransport("phase3-secret"), new InMemorySafeSyncLogger());

            var result = RunAsync(() => flow.RunAsync(CancellationToken.None));
            var loaded = RunAsync(() => repository.LoadAsync());

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            Assert.AreEqual(1, loaded.WorkSessionSummaries.Count(session => session.SessionId == "local-session"));
            Assert.AreEqual(1, loaded.WorkSessionSummaries.Count(session => session.SessionId == "server-session"));
            Assert.AreEqual(1, loaded.Achievements.Count(achievement => achievement.AchievementId == "server-achievement"));
        }

        [Test]
        public void BackendSmokeFlow_DoesNotPersistAuthTokenIntoSaveData()
        {
            var directory = TempDirectory();
            var repository = new SaveDataRepository(directory);
            Assert.IsTrue(RunAsync(() => repository.SaveAsync(CreateSafeSaveData())).IsSuccess);
            var flow = CreateFlow(repository, CreateSuccessfulSmokeTransport("phase3-secret-token"), new InMemorySafeSyncLogger());

            var result = RunAsync(() => flow.RunAsync(CancellationToken.None));
            var saveJson = File.ReadAllText(repository.SaveFilePath);

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            Assert.IsFalse(saveJson.Contains("phase3-secret-token"));
            Assert.IsFalse(saveJson.Contains("accessToken"));
            Assert.IsFalse(saveJson.Contains("bearer"));
        }

        [Test]
        public void BackendSmokeFlow_FailedAuthDoesNotAttemptPushOrPull()
        {
            var directory = TempDirectory();
            var repository = new SaveDataRepository(directory);
            Assert.IsTrue(RunAsync(() => repository.SaveAsync(CreateSafeSaveData())).IsSuccess);
            var transport = new FakeTransport();
            transport.Enqueue(new SafeHttpResponse { StatusCode = 401, BodyJson = "{\"code\":\"unauthorized\"}" });
            var flow = CreateFlow(repository, transport, new InMemorySafeSyncLogger());

            var result = RunAsync(() => flow.RunAsync(CancellationToken.None));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("sync_auth_failed", result.ErrorCode);
            Assert.AreEqual(1, transport.Requests.Count);
        }

        [Test]
        public void BackendSmokeFlow_FailedPushDoesNotCorruptLocalSave()
        {
            var directory = TempDirectory();
            var repository = new SaveDataRepository(directory);
            var saveData = CreateSafeSaveData();
            saveData.CharacterProfile.TotalExp = 77;
            Assert.IsTrue(RunAsync(() => repository.SaveAsync(saveData)).IsSuccess);
            var transport = new FakeTransport();
            transport.Enqueue(new SafeHttpResponse { StatusCode = 200, BodyJson = "{\"accessToken\":\"phase3-secret\"}" });
            transport.Enqueue(new SafeHttpResponse { StatusCode = 400, BodyJson = "{\"code\":\"validation_failed\",\"message\":\"raw prompt /Users/example\"}" });
            var flow = CreateFlow(repository, transport, new InMemorySafeSyncLogger());

            var result = RunAsync(() => flow.RunAsync(CancellationToken.None));
            var loaded = RunAsync(() => repository.LoadAsync());

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(77, loaded.CharacterProfile.TotalExp);
            Assert.AreEqual(1, loaded.WorkSessionSummaries.Count);
            Assert.AreEqual(0, loaded.Achievements.Count);
            Assert.IsNull(loaded.SyncState.LastSyncAt);
        }

        [Test]
        public void BackendSmokeFlow_FailedPullDoesNotCorruptLocalSave()
        {
            var directory = TempDirectory();
            var repository = new SaveDataRepository(directory);
            var saveData = CreateSafeSaveData();
            saveData.CharacterProfile.TotalExp = 88;
            Assert.IsTrue(RunAsync(() => repository.SaveAsync(saveData)).IsSuccess);
            var transport = new FakeTransport();
            transport.Enqueue(new SafeHttpResponse { StatusCode = 200, BodyJson = "{\"accessToken\":\"phase3-secret\"}" });
            transport.Enqueue(new SafeHttpResponse { StatusCode = 200, BodyJson = "{\"syncVersion\":1,\"acceptedSessionCount\":1,\"acceptedAchievementCount\":0}" });
            transport.Enqueue(new SafeHttpResponse { StatusCode = 500, BodyJson = "{\"code\":\"pull_failed\",\"message\":\"raw diff\"}" });
            var flow = CreateFlow(repository, transport, new InMemorySafeSyncLogger());

            var result = RunAsync(() => flow.RunAsync(CancellationToken.None));
            var loaded = RunAsync(() => repository.LoadAsync());

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(88, loaded.CharacterProfile.TotalExp);
            Assert.AreEqual(1, loaded.WorkSessionSummaries.Count);
            Assert.AreEqual(0, loaded.Achievements.Count);
            Assert.IsNull(loaded.SyncState.LastSyncAt);
        }

        [Test]
        public void BackendSmokeFlow_LogsSafeSummariesOnly()
        {
            var directory = TempDirectory();
            var repository = new SaveDataRepository(directory);
            Assert.IsTrue(RunAsync(() => repository.SaveAsync(CreateSafeSaveData())).IsSuccess);
            var logger = new InMemorySafeSyncLogger();
            var flow = CreateFlow(repository, CreateSuccessfulSmokeTransport("phase3-secret-token"), logger);

            var result = RunAsync(() => flow.RunAsync(CancellationToken.None));

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            var logText = string.Join("\n", logger.Entries);
            Assert.IsTrue(logText.Contains("accepted_sessions_1"));
            Assert.IsTrue(logText.Contains("pulled_sessions_1"));
            Assert.IsFalse(logText.Contains("phase3-secret-token"));
            Assert.IsFalse(logText.Contains("raw body"));
            Assert.IsFalse(logText.Contains("/Users/example"));
            Assert.IsFalse(logText.Contains("raw diff"));
            Assert.IsFalse(logText.Contains("raw prompt"));
            Assert.IsFalse(logText.Contains("claudeLog"));
            Assert.IsFalse(logText.Contains("codexLog"));
            Assert.IsFalse(logText.Contains("Authorization"));
            Assert.IsFalse(logText.Contains("authorization"));
        }

        private static BackendSyncSmokeFlow CreateFlow(
            SaveDataRepository repository,
            FakeTransport transport,
            InMemorySafeSyncLogger logger)
        {
            var configuration = ApiConfiguration.CreateLocalDevelopment();
            var authProvider = new DevGuestAuthTokenProvider(configuration, transport, logger);
            var httpClient = new TokenForgeHttpClient(configuration, authProvider, transport, logger);
            return new BackendSyncSmokeFlow(repository, authProvider, httpClient, null, null, logger);
        }

        private static FakeTransport CreateSuccessfulSmokeTransport(string token)
        {
            var transport = new FakeTransport();
            transport.Enqueue(new SafeHttpResponse { StatusCode = 200, BodyJson = "{\"accessToken\":\"" + token + "\"}" });
            transport.Enqueue(new SafeHttpResponse { StatusCode = 200, BodyJson = "{\"syncVersion\":1,\"acceptedSessionCount\":1,\"acceptedAchievementCount\":1}" });
            transport.Enqueue(new SafeHttpResponse { StatusCode = 200, BodyJson = BuildPullResponseJson() });
            return transport;
        }

        private static SaveData CreateSafeSaveData()
        {
            var saveData = SaveData.CreateDefault();
            saveData.PrivacyPreferences.CloudSyncEnabled = true;
            saveData.SyncState.SyncEnabled = true;
            saveData.CharacterProfile.CharacterId = "local-character";
            saveData.CharacterProfile.DisplayName = "Token";
            saveData.CharacterProfile.Level = 3;
            saveData.CharacterProfile.TotalExp = 1200;
            saveData.WorkSessionSummaries.Add(new AgentWorkSession
            {
                SessionId = "local-session",
                AgentType = AgentType.Codex,
                WorkType = WorkType.Feature,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-15),
                EndedAt = DateTimeOffset.UtcNow,
                TokenUsageBucket = TokenUsageBucket.Medium,
                ActionSummary = new AgentActionSummary
                {
                    PromptCount = 2,
                    ToolCallCount = 5,
                    FileEditCount = 3,
                    TestRunCount = 1
                },
                GitChangeSummary = new GitChangeSummary
                {
                    ChangedFileCount = 4,
                    AddedLineBucket = LineChangeBucket.Medium,
                    DeletedLineBucket = LineChangeBucket.Small,
                    ProjectPathHash = "projecthash"
                },
                ResultStatus = ResultStatus.Succeeded,
                Confidence = ProviderConfidence.High,
                SourceProvider = "CodexLogProvider",
                SourceProviders = new List<string> { "CodexLogProvider" },
                UserReviewed = true
            });

            return saveData;
        }

        private static string BuildPullResponseJson()
        {
            var startedAt = DateTimeOffset.UtcNow.AddMinutes(-10).ToString("o");
            var endedAt = DateTimeOffset.UtcNow.ToString("o");
            return "{"
                + "\"syncVersion\":2,"
                + "\"updatedAt\":\"" + endedAt + "\","
                + "\"sessions\":[{"
                + "\"sessionId\":\"server-session\","
                + "\"agentType\":2,"
                + "\"workType\":1,"
                + "\"startedAt\":\"" + startedAt + "\","
                + "\"endedAt\":\"" + endedAt + "\","
                + "\"tokenUsageBucket\":4,"
                + "\"changedFileCount\":2,"
                + "\"addedLineBucket\":2,"
                + "\"deletedLineBucket\":2,"
                + "\"promptCount\":1,"
                + "\"toolCallCount\":2,"
                + "\"fileEditCount\":1,"
                + "\"commandRunCount\":1,"
                + "\"testRunCount\":1,"
                + "\"buildRunCount\":0,"
                + "\"resultStatus\":1,"
                + "\"confidence\":3,"
                + "\"projectPathHash\":\"serverhash\","
                + "\"sourceProvider\":\"CODEX\""
                + "}],"
                + "\"achievements\":[{\"achievementId\":\"server-achievement\",\"unlockedAt\":\"" + endedAt + "\",\"progress\":1}]"
                + "}";
        }

        private static string TempDirectory()
        {
            return Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName());
        }

        private static T RunAsync<T>(Func<Task<T>> taskFactory)
        {
            return Task.Run(taskFactory).GetAwaiter().GetResult();
        }
    }
}
