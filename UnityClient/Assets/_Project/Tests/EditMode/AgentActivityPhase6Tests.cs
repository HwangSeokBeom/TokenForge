using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TokenForge.Client.Agents;
using TokenForge.Client.Common;
using TokenForge.Client.Domain;
using TokenForge.Client.Git;
using TokenForge.Client.Persistence;
using TokenForge.Client.Privacy;
using TokenForge.Client.Sync;
using TokenForge.Client.UI;
using UnityEngine.TestTools;

namespace TokenForge.Client.Tests
{
    public sealed class AgentActivityPhase6Tests
    {
        private const string RawPath = "/Users/alice/SecretRepo/src/private/UserSecret.cs";
        private const string RawFileName = "UserSecret.cs";
        private const string RawRepoName = "SecretRepo";
        private const string RawBranchName = "feature/customer-token";
        private const string RawPromptText = "please inspect the production auth token flow";
        private const string RawResponseText = "the bug is in customer secret handling";
        private const string RawSourceSnippet = "public class Secret { string password = \"hunter2\"; }";
        private const string RawCommand = "git status --short && npm test";
        private const string RawToken = "Bearer abc.def.secret";
        private const string RawSecret = "secret: customer-production-value";
        private const string RawUsername = "username: alice";

        [Test]
        public void ClaudeParser_ProducesSanitizedAggregateSummary()
        {
            var summary = ParseClaudeSample();

            Assert.AreEqual(AgentProviderType.ClaudeCode, summary.ProviderType);
            Assert.AreEqual("2026-05-14", summary.DayBucket);
            Assert.AreEqual(CountBucket.One, summary.SessionCountBucket);
            Assert.AreEqual(CountBucket.Medium, summary.InteractionCountBucket);
            AssertHasTool(summary, AgentToolUsageCategory.CodeEditing);
            AssertHasTool(summary, AgentToolUsageCategory.TestRun);
            AssertHasLanguage(summary, AgentLanguageCategory.CSharp);
            Assert.AreEqual(ClaudeAgentLogParser.Version, summary.AnalyzerVersion);
            AssertNoRawAgentData(summary);
            Assert.IsTrue(new PrivacySanitizer().ValidateNoForbiddenFields(summary).IsSuccess);
        }

        [Test]
        public void CodexParser_ProducesSanitizedAggregateSummary()
        {
            var entries = new List<AgentLogEntry>
            {
                Entry("{\"provider\":\"codex\",\"timestamp\":\"2026-05-14T03:00:00Z\",\"session_id\":\"codex-session-1\",\"type\":\"message\",\"role\":\"user\",\"prompt\":\"" + RawPromptText + "\"}"),
                Entry("{\"provider\":\"codex\",\"timestamp\":\"2026-05-14T03:01:00Z\",\"tool\":\"exec_command\",\"command\":\"" + RawCommand + "\"}"),
                Entry("{\"provider\":\"codex\",\"timestamp\":\"2026-05-14T03:02:00Z\",\"tool\":\"apply_patch\",\"file_path\":\"" + RawPath + "\",\"output\":\"" + RawResponseText + "\"}")
            };

            var result = new CodexAgentLogParser().Parse(Input(AgentProviderType.Codex), entries);

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            Assert.AreEqual(AgentProviderType.Codex, result.Value.ProviderType);
            AssertHasTool(result.Value, AgentToolUsageCategory.CodeEditing);
            AssertHasTool(result.Value, AgentToolUsageCategory.TestRun);
            AssertHasLanguage(result.Value, AgentLanguageCategory.CSharp);
            AssertNoRawAgentData(result.Value);
            Assert.Contains("agent_log_risky_content_discarded", result.Value.WarningIds);
        }

        [Test]
        public void UnknownParser_UnsupportedFormatReturnsLowConfidenceWarning()
        {
            var result = new UnknownAgentLogParser().Parse(Input(AgentProviderType.Unknown), new List<AgentLogEntry>
            {
                Entry("unstructured text without safe metadata")
            });

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            Assert.AreEqual(AgentProviderType.Unknown, result.Value.ProviderType);
            Assert.AreEqual(ConfidenceLevel.Low, result.Value.ConfidenceLevel);
            Assert.Contains("agent_log_unknown_provider", result.Value.WarningIds);
            Assert.Contains("agent_log_unsupported_format", result.Value.WarningIds);
        }

        [Test]
        public void RiskyContent_IsDiscardedFromSessionSaveAndSyncPayload()
        {
            var summary = ParseClaudeSample();
            var session = AgentLogActivityProvider.CreateSession(summary);
            var saveData = SaveData.CreateDefault();
            saveData.WorkSessionSummaries.Add(session);

            var payload = new SafeSyncMapper().ToPayload(saveData);

            AssertNoRawAgentData(saveData);
            AssertNoRawAgentData(payload);
            Assert.AreEqual("CLAUDE", session.SourceProvider);
            Assert.AreEqual("CLAUDE", payload.SessionSummary.Sessions[0].SourceProvider);
            Assert.AreEqual(AgentProviderType.ClaudeCode, payload.SessionSummary.Sessions[0].AgentProviderType);
            Assert.IsTrue(new PrivacySanitizer().ValidateSafeSaveData(saveData).IsSuccess);
            Assert.IsTrue(new SyncPayloadSanitizer().ValidatePayload(payload).IsSafe);
        }

        [Test]
        public void ForbiddenFields_AreRejectedForAgentPersistedModels()
        {
            var sanitizer = new PrivacySanitizer();
            var unsafeValues = new[]
            {
                RawPath,
                "prompt: " + RawPromptText,
                "response: " + RawResponseText,
                RawSourceSnippet,
                RawCommand,
                RawToken,
                RawSecret,
                "repo: " + RawRepoName,
                "branch: " + RawBranchName,
                RawUsername
            };

            foreach (var value in unsafeValues)
            {
                var saveData = SaveData.CreateDefault();
                saveData.WorkSessionSummaries.Add(new AgentWorkSession
                {
                    SourceProvider = "AI_AGENT",
                    AgentActivitySummary = new AgentActivitySummary
                    {
                        ProviderType = AgentProviderType.ClaudeCode,
                        SourceIdentifierHash = "safehash",
                        DayBucket = "2026-05-14",
                        WarningIds = { value }
                    }
                });

                Assert.IsFalse(sanitizer.ValidateSafeSaveData(saveData).IsSuccess, value);
            }
        }

        [Test]
        public void SafeAggregateAgentSummary_IsAccepted()
        {
            var saveData = SaveData.CreateDefault();
            saveData.WorkSessionSummaries.Add(AgentLogActivityProvider.CreateSession(new AgentActivitySummary
            {
                ProviderType = AgentProviderType.Codex,
                SourceIdentifierHash = "abcdef123456",
                DayBucket = "2026-05-14",
                SessionCountBucket = CountBucket.One,
                InteractionCountBucket = CountBucket.Small,
                EstimatedCodingActivityBucket = CountBucket.Small,
                ToolUsageCategoryBuckets =
                {
                    new AgentToolUsageCategoryBucket { Category = AgentToolUsageCategory.CodeEditing, CountBucket = CountBucket.Small }
                },
                ConfidenceLevel = ConfidenceLevel.High,
                AnalyzerVersion = CodexAgentLogParser.Version
            }));

            Assert.IsTrue(new PrivacySanitizer().ValidateSafeSaveData(saveData).IsSuccess);
            Assert.IsTrue(new SyncPayloadSanitizer().ValidatePayload(new SafeSyncMapper().ToPayload(saveData)).IsSafe);
        }

        [Test]
        public void ActivityProviderPoller_RequiresExplicitApprovedSelection()
        {
            var provider = new FakeActivityProvider("AI_AGENT");
            var poller = new ActivityProviderPoller(new[] { provider });

            var denied = RunAsync(() => poller.PollAsync(new ActivityProviderPollRequest
            {
                UserApproved = false,
                ApprovedSelections = { new ActivityProviderSelection { ProviderId = "AI_AGENT", UserApproved = true } }
            }, CancellationToken.None));
            var notSelected = RunAsync(() => poller.PollAsync(new ActivityProviderPollRequest
            {
                UserApproved = true,
                ApprovedSelections = { new ActivityProviderSelection { ProviderId = "AI_AGENT", UserApproved = false } }
            }, CancellationToken.None));
            var allowed = RunAsync(() => poller.PollAsync(new ActivityProviderPollRequest
            {
                UserApproved = true,
                ApprovedSelections = { new ActivityProviderSelection { ProviderId = "AI_AGENT", UserApproved = true } }
            }, CancellationToken.None));

            Assert.AreEqual("user_approval_required", denied[0].ErrorCategory);
            Assert.AreEqual("provider_location_not_approved", notSelected[0].ErrorCategory);
            Assert.IsTrue(allowed[0].IsSuccess);
            Assert.AreEqual(1, provider.PollCount);
        }

        [Test]
        public void GitActivityProvider_StillProducesSafeGitSessionThroughOrchestration()
        {
            var repositoryPath = CreateTempDirectory();
            var gitProvider = new GitActivityProviderAdapter(new GitAggregateAnalyzer(FakeGitRunner.WithSafeOutput()));
            var poller = new ActivityProviderPoller(new IActivityProvider[] { gitProvider });

            var results = RunAsync(() => poller.PollAsync(new ActivityProviderPollRequest
            {
                UserApproved = true,
                ApprovedSelections =
                {
                    new ActivityProviderSelection
                    {
                        ProviderId = "GIT",
                        SelectedLocationPath = repositoryPath,
                        UserApproved = true,
                        AnalysisWindowDays = 7
                    }
                }
            }, CancellationToken.None));

            Assert.AreEqual(1, results.Count);
            Assert.IsTrue(results[0].IsSuccess, results[0].ErrorCategory);
            Assert.AreEqual("GIT", results[0].Session.SourceProvider);
            Assert.IsTrue(new PrivacySanitizer().ValidateSafeSession(results[0].Session).IsSuccess);
        }

        [Test]
        public void AgentFlow_SaveAndSyncRequireReviewAndSaveConfirmation()
        {
            var fixture = CreateAgentFlowFixture();

            var saveBeforeReview = RunAsync(() => fixture.Controller.SaveSessionAsync(CancellationToken.None));
            fixture.Controller.SelectApprovedLogLocation(Input(AgentProviderType.ClaudeCode));
            var review = RunAsync(() => fixture.Controller.AnalyzeAsync(CancellationToken.None));
            var syncBeforeSave = RunAsync(() => fixture.Controller.SyncNowAsync(CancellationToken.None));
            var save = RunAsync(() => fixture.Controller.SaveSessionAsync(CancellationToken.None));
            var sync = RunAsync(() => fixture.Controller.SyncNowAsync(CancellationToken.None));

            Assert.IsFalse(saveBeforeReview.IsSuccess);
            Assert.IsTrue(review.IsSuccess, review.ErrorMessage + " " + review.ErrorCode + " " + fixture.Controller.ErrorCategory);
            Assert.IsFalse(syncBeforeSave.IsSuccess);
            Assert.AreEqual("sync_requires_saved_session", syncBeforeSave.ErrorCode);
            Assert.IsTrue(save.IsSuccess, save.ErrorMessage);
            Assert.AreEqual(1, fixture.Repository.Current.WorkSessionSummaries.Count);
            Assert.IsTrue(sync.IsSuccess, sync.ErrorMessage);
            Assert.AreEqual(1, fixture.Sync.PushThenPullCount);
        }

        [UnityTest]
        public IEnumerator AnalyzeAgentSourceForOnboardingCreatesPendingNativeReview()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName());
            var repository = new SaveDataRepository(directory);
            var sanitizer = new PrivacySanitizer();
            var agentFlow = new AgentAnalysisFlowController(
                new AgentLogActivityProvider(new AgentActivityAnalyzer(new FakeReader(CodexSampleEntries()))),
                repository,
                null,
                sanitizer);
            var viewModel = new ApprovedActivityAnalysisViewModel(
                new GitAnalysisFlowController(new CancelledRepositoryPicker(), new GitAggregateAnalyzer(null, sanitizer), repository, null, sanitizer),
                agentFlow,
                new CancelledAgentLogLocationPicker(),
                repository,
                sanitizer,
                new ApprovedLocationSettingsRepository(Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName())));
            viewModel.SelectedAgentProviderType = AgentProviderType.Codex;
            var source = viewModel.Onboarding.AgentSources.First(item => item.SourceType == ConnectedAgentSourceType.Codex);
            source.Selected = true;
            source.State = AgentSourceSetupState.ReadyToAnalyze;
            source.SafeLabel = "Codex local activity";
            source.SafeLocationHash = "codex-safe-location";
            agentFlow.SelectApprovedLogLocation(Input(AgentProviderType.Codex));

            var resultTask = viewModel.AnalyzeAgentSourceForOnboardingAsync(ConnectedAgentSourceType.Codex);
            yield return WaitForTask(resultTask, nameof(AnalyzeAgentSourceForOnboardingCreatesPendingNativeReview));
            var result = resultTask.Result;

            var loadTask = repository.LoadAsync();
            yield return WaitForTask(loadTask, nameof(AnalyzeAgentSourceForOnboardingCreatesPendingNativeReview) + ".load");
            var loaded = loadTask.Result;

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            Assert.IsNotNull(loaded.PendingNativeActivityReview);
            Assert.AreEqual("aiAgent", loaded.PendingNativeActivityReview.SourceKind);
            Assert.Greater(loaded.PendingNativeActivityReview.EstimatedXpDelta, 0);
            Assert.AreEqual("aiAgent", loaded.ActivityReviews[0].SourceType);
            Assert.AreEqual("codex", loaded.ActivityReviews[0].ProviderId);
            Assert.AreEqual("pending", loaded.ActivityReviews[0].Status);
        }

        [Test]
        public void AgentFlow_SavedJsonContainsNoRawPathPromptResponseCodeCommandOrSecrets()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName());
            var repository = new SaveDataRepository(directory);
            var controller = new AgentAnalysisFlowController(
                new AgentLogActivityProvider(new AgentActivityAnalyzer(new FakeReader(ClaudeSampleEntries()))),
                repository,
                null,
                new PrivacySanitizer());

            controller.SelectApprovedLogLocation(Input(AgentProviderType.ClaudeCode));
            var review = RunAsync(() => controller.AnalyzeAsync(CancellationToken.None));
            var save = RunAsync(() => controller.SaveSessionAsync(CancellationToken.None));
            var json = File.ReadAllText(repository.SaveFilePath);

            Assert.IsTrue(review.IsSuccess, review.ErrorMessage);
            Assert.IsTrue(save.IsSuccess, save.ErrorMessage);
            AssertNoRawAgentData(json);
            Assert.IsTrue(new PrivacySanitizer().ValidateNoForbiddenFields(json).IsSuccess);
        }

        [Test]
        public void DomainSaveModels_DoNotExposeAgentRawFields()
        {
            var detector = new ForbiddenFieldDetector();
            var modelTypes = new[]
            {
                typeof(AgentActivitySummary),
                typeof(AgentToolUsageCategoryBucket),
                typeof(AgentLanguageCategoryBucket),
                typeof(AgentAnalysisReviewModel)
            };

            foreach (var type in modelTypes)
            {
                foreach (var property in type.GetProperties())
                {
                    Assert.IsFalse(detector.IsForbiddenFieldName(property.Name), $"{type.Name}.{property.Name}");
                }
            }
        }

        private static AgentActivitySummary ParseClaudeSample()
        {
            var result = new ClaudeAgentLogParser().Parse(Input(AgentProviderType.ClaudeCode), ClaudeSampleEntries());
            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            return result.Value;
        }

        private static List<AgentLogEntry> ClaudeSampleEntries()
        {
            return new List<AgentLogEntry>
            {
                Entry("{\"provider\":\"claude\",\"timestamp\":\"2026-05-14T01:00:00Z\",\"session_id\":\"claude-session-1\",\"type\":\"message\",\"role\":\"user\",\"prompt\":\"" + RawPromptText + "\",\"branchName\":\"" + RawBranchName + "\"}"),
                Entry("{\"provider\":\"claude\",\"timestamp\":\"2026-05-14T01:02:00Z\",\"tool_name\":\"Read\",\"file_path\":\"" + RawPath + "\",\"repoName\":\"" + RawRepoName + "\"}"),
                Entry("{\"provider\":\"claude\",\"timestamp\":\"2026-05-14T01:03:00Z\",\"tool_name\":\"Edit\",\"file_path\":\"" + RawPath + "\",\"response\":\"" + RawResponseText + "\",\"sourceText\":\"" + Escape(RawSourceSnippet) + "\"}"),
                Entry("{\"provider\":\"claude\",\"timestamp\":\"2026-05-14T01:04:00Z\",\"tool_name\":\"Bash\",\"command\":\"" + RawCommand + "\",\"authorization\":\"" + RawToken + "\",\"username\":\"alice\"}"),
                Entry("{\"provider\":\"claude\",\"timestamp\":\"2026-05-14T01:05:00Z\",\"tool_name\":\"Grep\",\"pattern\":\"" + RawSecret + "\"}")
            };
        }

        private static List<AgentLogEntry> CodexSampleEntries()
        {
            return new List<AgentLogEntry>
            {
                Entry("{\"provider\":\"codex\",\"timestamp\":\"2026-05-14T03:00:00Z\",\"session_id\":\"codex-session-1\",\"type\":\"message\",\"role\":\"user\",\"prompt\":\"" + RawPromptText + "\"}"),
                Entry("{\"provider\":\"codex\",\"timestamp\":\"2026-05-14T03:01:00Z\",\"tool\":\"exec_command\",\"command\":\"" + RawCommand + "\"}"),
                Entry("{\"provider\":\"codex\",\"timestamp\":\"2026-05-14T03:02:00Z\",\"tool\":\"apply_patch\",\"file_path\":\"" + RawPath + "\",\"output\":\"" + RawResponseText + "\"}")
            };
        }

        private static AgentLogEntry Entry(string text)
        {
            return new AgentLogEntry
            {
                Text = text,
                LastWriteTimeUtc = new DateTimeOffset(2026, 5, 14, 0, 0, 0, TimeSpan.Zero)
            };
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static AgentAnalysisInput Input(AgentProviderType providerType)
        {
            return new AgentAnalysisInput
            {
                SelectedLocationPath = "/Users/alice/SecretRepo/.agent/logs",
                ProviderHint = providerType,
                AnalysisWindowDays = 7,
                MaxFilesToScan = 5,
                MaxLogEntriesToScan = 100
            };
        }

        private static AgentFlowFixture CreateAgentFlowFixture()
        {
            var repository = new FakeLocalRepository();
            var sync = new FakeSyncService(repository);
            var controller = new AgentAnalysisFlowController(
                new AgentLogActivityProvider(new AgentActivityAnalyzer(new FakeReader(ClaudeSampleEntries()))),
                repository,
                null,
                new PrivacySanitizer(),
                sync);

            return new AgentFlowFixture(controller, repository, sync);
        }

        private static void AssertHasTool(AgentActivitySummary summary, AgentToolUsageCategory category)
        {
            Assert.IsTrue((summary.ToolUsageCategoryBuckets ?? new List<AgentToolUsageCategoryBucket>())
                .Any(item => item.Category == category && item.CountBucket != CountBucket.None), category.ToString());
        }

        private static void AssertHasLanguage(AgentActivitySummary summary, AgentLanguageCategory category)
        {
            Assert.IsTrue((summary.LanguageCategoryBuckets ?? new List<AgentLanguageCategoryBucket>())
                .Any(item => item.Category == category && item.CountBucket != CountBucket.None), category.ToString());
        }

        private static void AssertNoRawAgentData(object value)
        {
            Assert.IsFalse(ObjectContainsString(value, RawPath), RawPath);
            Assert.IsFalse(ObjectContainsString(value, RawFileName), RawFileName);
            Assert.IsFalse(ObjectContainsString(value, RawRepoName), RawRepoName);
            Assert.IsFalse(ObjectContainsString(value, RawBranchName), RawBranchName);
            Assert.IsFalse(ObjectContainsString(value, RawPromptText), RawPromptText);
            Assert.IsFalse(ObjectContainsString(value, RawResponseText), RawResponseText);
            Assert.IsFalse(ObjectContainsString(value, RawSourceSnippet), RawSourceSnippet);
            Assert.IsFalse(ObjectContainsString(value, RawCommand), RawCommand);
            Assert.IsFalse(ObjectContainsString(value, RawToken), RawToken);
            Assert.IsFalse(ObjectContainsString(value, RawSecret), RawSecret);
            Assert.IsFalse(ObjectContainsString(value, "username: alice"), "username");
            Assert.IsFalse(ObjectContainsString(value, "alice"), "alice");
        }

        private static bool ObjectContainsString(object value, string expected)
        {
            return ObjectContainsString(value, expected, new HashSet<object>());
        }

        private static bool ObjectContainsString(object value, string expected, HashSet<object> visited)
        {
            if (value == null)
            {
                return false;
            }

            if (value is string text)
            {
                return text.Contains(expected);
            }

            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || type == typeof(DateTimeOffset) || type == typeof(DateTime))
            {
                return false;
            }

            if (!visited.Add(value))
            {
                return false;
            }

            if (value is System.Collections.IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (ObjectContainsString(item, expected, visited))
                    {
                        return true;
                    }
                }

                return false;
            }

            foreach (var property in type.GetProperties())
            {
                if (property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                if (ObjectContainsString(property.GetValue(value), expected, visited))
                {
                    return true;
                }
            }

            return false;
        }

        private static string CreateTempDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName());
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static T RunAsync<T>(Func<Task<T>> operation)
        {
            return Task.Run(operation).GetAwaiter().GetResult();
        }

        private static IEnumerator WaitForTask(Task task, string operationName)
        {
            var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
            while (!task.IsCompleted && DateTimeOffset.UtcNow < deadline)
            {
                yield return null;
            }

            Assert.IsTrue(task.IsCompleted, operationName + " timed out.");
            if (task.IsFaulted)
            {
                throw task.Exception?.GetBaseException() ?? task.Exception;
            }

            if (task.IsCanceled)
            {
                Assert.Fail(operationName + " was canceled.");
            }
        }

        private sealed class FakeReader : IAgentLogSourceReader
        {
            private readonly List<AgentLogEntry> entries;

            public FakeReader(List<AgentLogEntry> entries)
            {
                this.entries = entries;
            }

            public Task<AgentLogReadResult> ReadAsync(AgentAnalysisInput input, CancellationToken cancellationToken)
            {
                return Task.FromResult(AgentLogReadResult.Success(entries, new List<string>()));
            }
        }

        private sealed class CancelledRepositoryPicker : TokenForge.Client.Platform.IRepositoryPicker
        {
            public Task<TokenForge.Client.Platform.RepositoryPickerResult> PickRepositoryAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(TokenForge.Client.Platform.RepositoryPickerResult.Cancelled());
            }
        }

        private sealed class CancelledAgentLogLocationPicker : TokenForge.Client.Platform.IAgentLogLocationPicker
        {
            public Task<TokenForge.Client.Platform.AgentLogLocationPickerResult> PickAgentLogLocationAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(TokenForge.Client.Platform.AgentLogLocationPickerResult.Cancelled());
            }
        }

        private sealed class FakeActivityProvider : IActivityProvider
        {
            public FakeActivityProvider(string providerId)
            {
                ProviderId = providerId;
            }

            public string ProviderId { get; }
            public int PollCount { get; private set; }

            public Task<ActivityProviderResult> PollAsync(ActivityProviderSelection selection, CancellationToken cancellationToken)
            {
                PollCount++;
                return Task.FromResult(ActivityProviderResult.Success(ProviderId, AgentLogActivityProvider.CreateSession(new AgentActivitySummary
                {
                    ProviderType = AgentProviderType.Codex,
                    SourceIdentifierHash = "safehash",
                    DayBucket = "2026-05-14",
                    SessionCountBucket = CountBucket.One,
                    InteractionCountBucket = CountBucket.One,
                    EstimatedCodingActivityBucket = CountBucket.One,
                    ConfidenceLevel = ConfidenceLevel.High,
                    AnalyzerVersion = CodexAgentLogParser.Version
                })));
            }
        }

        private sealed class AgentFlowFixture
        {
            public AgentFlowFixture(AgentAnalysisFlowController controller, FakeLocalRepository repository, FakeSyncService sync)
            {
                Controller = controller;
                Repository = repository;
                Sync = sync;
            }

            public AgentAnalysisFlowController Controller { get; }
            public FakeLocalRepository Repository { get; }
            public FakeSyncService Sync { get; }
        }

        private sealed class FakeLocalRepository : ILocalSaveDataRepository
        {
            private readonly PrivacySanitizer privacySanitizer = new PrivacySanitizer();

            public SaveData Current { get; private set; } = SaveData.CreateDefault();

            public Task<SaveData> LoadAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Current);
            }

            public Task<Result> SaveAsync(SaveData saveData, CancellationToken cancellationToken = default)
            {
                var validation = privacySanitizer.ValidateSafeSaveData(saveData);
                if (!validation.IsSuccess)
                {
                    return Task.FromResult(validation);
                }

                Current = saveData;
                return Task.FromResult(Result.Success());
            }
        }

        private sealed class FakeSyncService : ISyncService
        {
            private readonly FakeLocalRepository repository;

            public FakeSyncService(FakeLocalRepository repository)
            {
                this.repository = repository;
            }

            public int PushThenPullCount { get; private set; }

            public Task<Result<SaveData>> PushAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(Result<SaveData>.Success(repository.Current));
            }

            public Task<Result<SaveData>> PullAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(Result<SaveData>.Success(repository.Current));
            }

            public Task<Result<SaveData>> PushThenPullAsync(CancellationToken cancellationToken)
            {
                PushThenPullCount++;
                return Task.FromResult(Result<SaveData>.Success(repository.Current));
            }
        }

        private sealed class FakeGitRunner : IGitCommandRunner
        {
            public Task<GitCommandResult> RunAsync(string workingDirectory, string arguments, CancellationToken cancellationToken)
            {
                switch (arguments)
                {
                    case "rev-parse --is-inside-work-tree":
                        return Task.FromResult(GitCommandResult.Success("true\n"));
                    case "rev-parse HEAD":
                        return Task.FromResult(GitCommandResult.Success("HEADSHA\n"));
                    case "log --all --reverse --format=%cI -n 1":
                        return Task.FromResult(GitCommandResult.Success("2026-01-02T03:04:05Z\n"));
                    case "rev-list --all --count":
                        return Task.FromResult(GitCommandResult.Success("42\n"));
                    case "status --porcelain":
                        return Task.FromResult(GitCommandResult.Success(" M Assets/_Project/Scripts/Domain/CharacterModels.cs\n"));
                    case "diff --numstat":
                        return Task.FromResult(GitCommandResult.Success("10\t2\tAssets/_Project/Scripts/Domain/CharacterModels.cs\n"));
                    case "diff --cached --numstat":
                        return Task.FromResult(GitCommandResult.Success(string.Empty));
                    case "log --since=7.days.ago --numstat --format=--TOKENFORGE-COMMIT-- -n 200":
                    case "log --since=7.days.ago --numstat --format=--TOKENFORGE-COMMIT-- -n 50":
                    case "log --all --numstat --format=--TOKENFORGE-COMMIT--":
                        return Task.FromResult(GitCommandResult.Success("--TOKENFORGE-COMMIT--\n1\t0\tREADME.md\n"));
                    default:
                        return Task.FromResult(GitCommandResult.Success(string.Empty));
                }
            }

            public static FakeGitRunner WithSafeOutput()
            {
                return new FakeGitRunner();
            }
        }
    }
}
