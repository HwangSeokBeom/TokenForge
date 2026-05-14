using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TokenForge.Client.Common;
using TokenForge.Client.Domain;
using TokenForge.Client.Persistence;
using TokenForge.Client.Platform;
using TokenForge.Client.Privacy;
using TokenForge.Client.Sync;

namespace TokenForge.Client.Tests
{
    public sealed class SyncServicePhase2Tests
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
                    : new SafeHttpResponse { StatusCode = 200, BodyJson = "{}" });
            }
        }

        private sealed class FailingSyncService : ISyncService
        {
            public Task<Result<SaveData>> PushAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(Result<SaveData>.Failure("sync_no_token", "No development token configured."));
            }

            public Task<Result<SaveData>> PullAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(Result<SaveData>.Failure("sync_no_token", "No development token configured."));
            }

            public Task<Result<SaveData>> PushThenPullAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(Result<SaveData>.Failure("sync_no_token", "No development token configured."));
            }
        }

        [Test]
        public void SafeOutboundDtoMapping_DoesNotIncludeForbiddenFields()
        {
            var saveData = CreateSafeSaveData();
            var payload = new SafeSyncMapper().ToPayload(saveData);
            var detector = new ForbiddenFieldDetector();

            Assert.IsTrue(new PrivacySanitizer(detector).ValidateObject(payload).IsSafe);
            AssertNoForbiddenProperties(payload, detector);
        }

        [Test]
        public void SyncService_RejectsOutboundPayload_WhenPrivacySanitizerDetectsForbiddenValue()
        {
            var directory = TempDirectory();
            var repository = new SaveDataRepository(directory);
            Assert.IsTrue(RunAsync(() => repository.SaveAsync(CreateSafeSaveData())).IsSuccess);

            var saveJson = File.ReadAllText(repository.SaveFilePath);
            File.WriteAllText(repository.SaveFilePath, saveJson.Replace("\"manual\"", "\"prompt: do not send\""));

            var transport = new FakeTransport();
            var syncService = CreateSyncService(repository, transport);

            var result = RunAsync(() => syncService.PushAsync(CancellationToken.None));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("privacy_forbidden_data", result.ErrorCode);
            Assert.AreEqual(0, transport.Requests.Count);
        }

        [Test]
        public void HttpClient_DoesNotLogRawBodiesOrSensitiveValues()
        {
            var transport = new FakeTransport();
            transport.Enqueue(new SafeHttpResponse
            {
                StatusCode = 400,
                BodyJson = "{\"code\":\"PRIVACY_GUARD_REJECTED\",\"message\":\"prompt: /Users/example/secret\"}"
            });
            var logger = new InMemorySafeSyncLogger();
            var client = new TokenForgeHttpClient(
                new ApiConfiguration("http://localhost:3000"),
                new DevelopmentAuthTokenProvider("dev-secret-token"),
                transport,
                logger);

            var result = RunAsync(() => client.PostJsonAsync<object, SafeSyncPushResponse>(
                "/sync/push",
                "sync_push",
                new { value = "prompt: /Users/example/secret" },
                CancellationToken.None));

            var logText = string.Join("\n", logger.Entries);
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("PRIVACY_GUARD_REJECTED", result.ErrorCode);
            Assert.AreEqual("dev-secret-token", transport.Requests[0].BearerToken);
            Assert.IsFalse(logText.Contains("prompt"));
            Assert.IsFalse(logText.Contains("/Users"));
            Assert.IsFalse(logText.Contains("secret"));
            Assert.IsFalse(logText.Contains(transport.Requests[0].BodyJson));
        }

        [Test]
        public void FailedPush_KeepsLocalSaveUnchanged()
        {
            var directory = TempDirectory();
            var repository = new SaveDataRepository(directory);
            var saveData = CreateSafeSaveData();
            saveData.CharacterProfile.TotalExp = 77;
            Assert.IsTrue(RunAsync(() => repository.SaveAsync(saveData)).IsSuccess);

            var transport = new FakeTransport();
            transport.Enqueue(new SafeHttpResponse
            {
                StatusCode = 503,
                BodyJson = "{\"code\":\"sync_unavailable\"}"
            });

            var result = RunAsync(() => CreateSyncService(repository, transport).PushAsync(CancellationToken.None));
            var loaded = RunAsync(() => repository.LoadAsync());

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(77, loaded.CharacterProfile.TotalExp);
            Assert.IsNull(loaded.SyncState.LastSyncAt);
            Assert.AreEqual(string.Empty, loaded.SyncState.LastErrorCode);
        }

        [Test]
        public void PullMerge_WritesOnlySafeBoundedFields()
        {
            var directory = TempDirectory();
            var repository = new SaveDataRepository(directory);
            Assert.IsTrue(RunAsync(() => repository.SaveAsync(CreateSafeSaveData())).IsSuccess);

            var transport = new FakeTransport();
            transport.Enqueue(new SafeHttpResponse
            {
                StatusCode = 200,
                BodyJson = BuildPullResponseJson(DateTimeOffset.UtcNow.AddMinutes(-10), DateTimeOffset.UtcNow)
            });

            var result = RunAsync(() => CreateSyncService(repository, transport).PullAsync(CancellationToken.None));
            var loaded = RunAsync(() => repository.LoadAsync());

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            Assert.AreEqual(100, loaded.CharacterProfile.Level);
            Assert.AreEqual(100000000, loaded.CharacterProfile.TotalExp);
            Assert.AreEqual(999, loaded.CharacterProfile.Stats.Logic);
            Assert.AreEqual(0, loaded.CharacterProfile.Stats.Debug);
            Assert.AreEqual(200, loaded.CharacterProfile.UnlockedItems.Count);
            Assert.AreEqual(9, loaded.SyncState.SyncVersion);
            Assert.AreEqual(1, loaded.Achievements.Count);
            Assert.AreEqual("achievement-1", loaded.Achievements[0].AchievementId);
            Assert.AreEqual(10000, loaded.Achievements[0].Progress);
            Assert.AreEqual(1, loaded.WorkSessionSummaries.Count(session => session.SessionId == "server-session"));
            var session = loaded.WorkSessionSummaries.First(session => session.SessionId == "server-session");
            Assert.AreEqual(500, session.GitChangeSummary.ChangedFileCount);
            Assert.AreEqual(10000, session.ActionSummary.PromptCount);
            Assert.AreEqual(0, session.ActionSummary.ToolCallCount);
            Assert.AreEqual("safehash", session.GitChangeSummary.ProjectPathHash);
        }

        [Test]
        public void Bootstrap_OfflineNoTokenMode_DoesNotCrash()
        {
            var directory = TempDirectory();
            var repository = new SaveDataRepository(directory);
            var flow = new BootstrapSmokeFlow(repository, null, null, new FailingSyncService());

            var result = RunAsync(() => flow.RunIfEmptyThenOptionalSyncAsync(true, BootstrapSyncMode.PushThenPull, CancellationToken.None));
            var loaded = RunAsync(() => repository.LoadAsync());

            Assert.IsTrue(result.IsSuccess);
            Assert.Contains("sync_no_token", result.Warnings);
            Assert.AreEqual(1, loaded.WorkSessionSummaries.Count);
        }

        private static SyncService CreateSyncService(SaveDataRepository repository, FakeTransport transport)
        {
            var client = new TokenForgeHttpClient(
                new ApiConfiguration("http://localhost:3000"),
                new EmptyAuthTokenProvider(),
                transport,
                new InMemorySafeSyncLogger());
            return new SyncService(repository, client);
        }

        private static SaveData CreateSafeSaveData()
        {
            var saveData = SaveData.CreateDefault();
            saveData.PrivacyPreferences.CloudSyncEnabled = true;
            saveData.SyncState.SyncEnabled = true;
            saveData.CharacterProfile.CharacterId = "character-safe";
            saveData.CharacterProfile.DisplayName = "Token";
            saveData.CharacterProfile.Level = 3;
            saveData.CharacterProfile.TotalExp = 1200;
            saveData.CharacterProfile.Stats.Logic = 5;
            saveData.WorkSessionSummaries.Add(new AgentWorkSession
            {
                SessionId = "session-safe",
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
                SourceProviders = new List<string> { "manual" },
                UserReviewed = true
            });

            return saveData;
        }

        private static string TempDirectory()
        {
            return Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName());
        }

        private static T RunAsync<T>(Func<Task<T>> taskFactory)
        {
            return Task.Run(taskFactory).GetAwaiter().GetResult();
        }

        private static void AssertNoForbiddenProperties(object value, ForbiddenFieldDetector detector)
        {
            if (value == null)
            {
                return;
            }

            var type = value.GetType();
            if (type == typeof(string) || type.IsPrimitive || type.IsEnum || type == typeof(DateTimeOffset))
            {
                return;
            }

            if (value is System.Collections.IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    AssertNoForbiddenProperties(item, detector);
                }

                return;
            }

            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                Assert.IsFalse(detector.IsForbiddenFieldName(property.Name), $"{type.Name}.{property.Name}");
                AssertNoForbiddenProperties(property.GetValue(value), detector);
            }
        }

        private static string BuildPullResponseJson(DateTimeOffset startedAt, DateTimeOffset endedAt)
        {
            var unlockedItems = string.Join(",", Enumerable.Range(0, 250).Select(index => "\"item-" + index + "\""));
            return "{"
                + "\"SyncVersion\":9,"
                + "\"UpdatedAt\":\"" + endedAt.ToString("o") + "\","
                + "\"CharacterSnapshot\":{"
                + "\"SyncVersion\":9,"
                + "\"UpdatedAt\":\"" + endedAt.ToString("o") + "\","
                + "\"CharacterId\":\"server-character\","
                + "\"DisplayName\":\"Server Token\","
                + "\"Level\":999,"
                + "\"TotalExp\":2147483647,"
                + "\"EvolutionType\":1,"
                + "\"CharacterStats\":{\"Logic\":2000,\"Debug\":-5,\"Architecture\":30,\"Design\":0,\"Stability\":0,\"Velocity\":0,\"Creativity\":0,\"Efficiency\":0,\"Stress\":0},"
                + "\"UnlockedItems\":[" + unlockedItems + "]"
                + "},"
                + "\"Sessions\":[{"
                + "\"SessionId\":\"server-session\","
                + "\"AgentType\":2,"
                + "\"WorkType\":1,"
                + "\"StartedAt\":\"" + startedAt.ToString("o") + "\","
                + "\"EndedAt\":\"" + endedAt.ToString("o") + "\","
                + "\"TokenUsageBucket\":4,"
                + "\"ChangedFileCount\":10000,"
                + "\"AddedLineBucket\":5,"
                + "\"DeletedLineBucket\":2,"
                + "\"PromptCount\":50000,"
                + "\"ToolCallCount\":-5,"
                + "\"FileEditCount\":0,"
                + "\"CommandRunCount\":0,"
                + "\"TestRunCount\":0,"
                + "\"BuildRunCount\":0,"
                + "\"ResultStatus\":1,"
                + "\"Confidence\":3,"
                + "\"ProjectPathHash\":\"safehash\","
                + "\"SourceProviders\":[\"sync\"]"
                + "}],"
                + "\"Achievements\":[{\"AchievementId\":\"achievement-1\",\"UnlockedAt\":\"" + endedAt.ToString("o") + "\",\"Progress\":50000}]"
                + "}";
        }
    }
}
