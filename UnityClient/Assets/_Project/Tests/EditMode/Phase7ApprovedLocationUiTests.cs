using System;
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
using TokenForge.Client.Platform;
using TokenForge.Client.Privacy;
using TokenForge.Client.Sync;
using TokenForge.Client.UI;

namespace TokenForge.Client.Tests
{
    public sealed class Phase7ApprovedLocationUiTests
    {
        private const string RawRepositoryName = "SecretRepo";
        private const string RawRepositoryPathSuffix = "SecretRepo";
        private const string RawAgentLogPath = "/Users/alice/SecretRepo/.claude/projects/private-log.jsonl";
        private const string RawFileName = "UserSecret.cs";
        private const string RawBranchName = "feature/customer-token";
        private const string RawPrompt = "please inspect the production auth token flow";
        private const string RawResponse = "the response includes customer secret handling";
        private const string RawCommand = "git status --short && npm test";
        private const string RawSourceSnippet = "public class Secret { string password = \"hunter2\"; }";
        private const string RawToken = "Bearer abc.def.secret";

        [Test]
        public void ReviewUiModels_DoNotExposeRawSelectedLocations()
        {
            var fixture = CreateFixture();

            RunAsync(() => fixture.Dashboard.GitFlow.SelectRepositoryAsync(CancellationToken.None));
            var gitReview = RunAsync(() => fixture.Dashboard.GitFlow.AnalyzeAsync(CancellationToken.None));
            RunAsync(() => fixture.Dashboard.SelectAgentLogLocationAsync(CancellationToken.None));
            var agentReview = RunAsync(() => fixture.Dashboard.AnalyzeAgentActivityAsync(CancellationToken.None));

            Assert.IsTrue(gitReview.IsSuccess, gitReview.ErrorMessage);
            Assert.IsTrue(agentReview.IsSuccess, agentReview.ErrorMessage);
            AssertNoRawData(fixture.Dashboard.GitFlow.Review, fixture.RepositoryPath);
            AssertNoRawData(fixture.Dashboard.AgentFlow.Review, fixture.RepositoryPath);
            Assert.IsFalse(ObjectContainsString(fixture.Dashboard, RawAgentLogPath));
        }

        [Test]
        public void SavedSessionsAndSyncDtos_DoNotContainRawPathsOrAgentContent()
        {
            var fixture = CreateFixture();

            RunAsync(() => fixture.Dashboard.SelectAgentLogLocationAsync(CancellationToken.None));
            RunAsync(() => fixture.Dashboard.AnalyzeAgentActivityAsync(CancellationToken.None));
            var save = RunAsync(() => fixture.Dashboard.SaveAgentSessionAsync(CancellationToken.None));
            var payload = new SafeSyncMapper().ToPayload(fixture.Repository.Current);

            Assert.IsTrue(save.IsSuccess, save.ErrorMessage);
            Assert.AreEqual(1, fixture.Repository.Current.WorkSessionSummaries.Count);
            AssertNoRawData(fixture.Repository.Current, fixture.RepositoryPath);
            AssertNoRawData(payload, fixture.RepositoryPath);
            Assert.IsTrue(new PrivacySanitizer().ValidateSafeSaveData(fixture.Repository.Current).IsSuccess);
            Assert.IsTrue(new SyncPayloadSanitizer().ValidatePayload(payload).IsSafe);
        }

        [Test]
        public void ClearSelectionClearsPendingRawInputAndDiscardClearsReviewState()
        {
            var fixture = CreateFixture();

            RunAsync(() => fixture.Dashboard.GitFlow.SelectRepositoryAsync(CancellationToken.None));
            Assert.AreEqual(GitAnalysisFlowState.Selected, fixture.Dashboard.GitFlow.State);
            fixture.Dashboard.ClearGitForOnboarding();
            var gitAnalyze = RunAsync(() => fixture.Dashboard.GitFlow.AnalyzeAsync(CancellationToken.None));

            RunAsync(() => fixture.Dashboard.SelectAgentLogLocationAsync(CancellationToken.None));
            RunAsync(() => fixture.Dashboard.AnalyzeAgentActivityAsync(CancellationToken.None));
            Assert.IsNotNull(fixture.Dashboard.AgentFlow.Review);
            fixture.Dashboard.DiscardAgentReview();
            var agentSave = RunAsync(() => fixture.Dashboard.SaveAgentSessionAsync(CancellationToken.None));

            Assert.IsFalse(gitAnalyze.IsSuccess);
            Assert.AreEqual("missing_repository_selection", gitAnalyze.ErrorCode);
            Assert.IsFalse(agentSave.IsSuccess);
            Assert.AreEqual("missing_agent_review_session", agentSave.ErrorCode);
            Assert.IsNull(fixture.Dashboard.GitFlow.Review);
            Assert.IsNull(fixture.Dashboard.AgentFlow.Review);
            Assert.AreEqual(1, fixture.Repository.SaveCount);
            AssertNoRawData(fixture.Dashboard, fixture.RepositoryPath);
        }

        [Test]
        public void AgentReview_ShowsOnlySafeAggregateFields()
        {
            var fixture = CreateFixture();
            fixture.Dashboard.SelectedAgentProviderType = AgentProviderType.ClaudeCode;

            RunAsync(() => fixture.Dashboard.SelectAgentLogLocationAsync(CancellationToken.None));
            var review = RunAsync(() => fixture.Dashboard.AnalyzeAgentActivityAsync(CancellationToken.None)).Value;

            Assert.AreEqual(AgentProviderType.ClaudeCode, review.ProviderType);
            Assert.AreEqual("2026-05-14", review.DayBucket);
            Assert.AreEqual(CountBucket.One, review.SessionCountBucket);
            Assert.AreEqual(ClaudeAgentLogParser.Version, review.AnalyzerVersion);
            Assert.IsTrue(review.ToolUsageCategoryBuckets.Count > 0);
            Assert.IsTrue(review.LanguageCategoryBuckets.Count > 0);
            Assert.Contains("agent_log_risky_content_discarded", review.WarningIds);
            AssertNoRawData(review, fixture.RepositoryPath);
        }

        [Test]
        public void GitReviewFlow_StillWorksThroughPhase7Dashboard()
        {
            var fixture = CreateFixture();

            RunAsync(() => fixture.Dashboard.GitFlow.SelectRepositoryAsync(CancellationToken.None));
            var review = RunAsync(() => fixture.Dashboard.GitFlow.AnalyzeAsync(CancellationToken.None));
            var save = RunAsync(() => fixture.Dashboard.SaveGitSessionAsync(CancellationToken.None));

            Assert.IsTrue(review.IsSuccess, review.ErrorMessage);
            Assert.IsTrue(save.IsSuccess, save.ErrorMessage);
            Assert.AreEqual(GitAnalysisFlowState.Saved, fixture.Dashboard.GitFlow.State);
            Assert.AreEqual(1, fixture.Dashboard.RecentSessions.Count);
            Assert.AreEqual("GIT", fixture.Repository.Current.WorkSessionSummaries[0].SourceProvider);
            AssertNoRawData(fixture.Dashboard.RecentSessions, fixture.RepositoryPath);
        }

        [Test]
        public void ForbiddenFieldDetector_RejectsRawPhase7SensitiveValues()
        {
            var detector = new ForbiddenFieldDetector();

            Assert.IsTrue(detector.IsForbiddenFieldName("fileName"));
            Assert.IsTrue(detector.IsForbiddenFieldName("repoName"));
            Assert.IsTrue(detector.IsForbiddenFieldName("branchName"));
            Assert.IsTrue(detector.IsForbiddenFieldName("commandString"));
            Assert.IsTrue(detector.IsForbiddenFieldName("rawPrompt"));
            Assert.IsTrue(detector.IsForbiddenFieldName("rawResponse"));
            Assert.IsTrue(detector.ContainsSensitiveString("fileName: " + RawFileName));
            Assert.IsTrue(detector.ContainsSensitiveString("repo: " + RawRepositoryName));
            Assert.IsTrue(detector.ContainsSensitiveString("branch: " + RawBranchName));
            Assert.IsTrue(detector.ContainsSensitiveString("username: alice"));
            Assert.IsTrue(detector.ContainsSensitiveString(RawCommand));
            Assert.IsTrue(detector.ContainsSensitiveString(RawToken));
            Assert.IsTrue(detector.ContainsSensitiveString(RawSourceSnippet));
            Assert.IsTrue(detector.ContainsSensitiveString("prompt: " + RawPrompt));
            Assert.IsTrue(detector.ContainsSensitiveString("response: " + RawResponse));
        }

        private static Phase7Fixture CreateFixture()
        {
            var repositoryPath = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName(), RawRepositoryPathSuffix);
            Directory.CreateDirectory(repositoryPath);

            var privacySanitizer = new PrivacySanitizer();
            var repository = new FakeLocalRepository();
            var gitFlow = new GitAnalysisFlowController(
                new FakeRepositoryPicker(repositoryPath),
                new GitAggregateAnalyzer(new FakeGitRunner(), privacySanitizer),
                repository,
                null,
                privacySanitizer);
            var agentFlow = new AgentAnalysisFlowController(
                new AgentLogActivityProvider(new AgentActivityAnalyzer(new FakeAgentLogReader(), null, privacySanitizer)),
                repository,
                null,
                privacySanitizer);
            var dashboard = new ApprovedActivityAnalysisViewModel(
                gitFlow,
                agentFlow,
                new FakeAgentLogLocationPicker(RawAgentLogPath),
                repository,
                privacySanitizer);

            return new Phase7Fixture(repositoryPath, dashboard, repository);
        }

        private static void AssertNoRawData(object value, string repositoryPath)
        {
            Assert.IsFalse(ObjectContainsString(value, repositoryPath), repositoryPath);
            Assert.IsFalse(ObjectContainsString(value, RawAgentLogPath), RawAgentLogPath);
            Assert.IsFalse(ObjectContainsString(value, RawRepositoryName), RawRepositoryName);
            Assert.IsFalse(ObjectContainsString(value, RawFileName), RawFileName);
            Assert.IsFalse(ObjectContainsString(value, RawBranchName), RawBranchName);
            Assert.IsFalse(ObjectContainsString(value, RawPrompt), RawPrompt);
            Assert.IsFalse(ObjectContainsString(value, RawResponse), RawResponse);
            Assert.IsFalse(ObjectContainsString(value, RawCommand), RawCommand);
            Assert.IsFalse(ObjectContainsString(value, RawSourceSnippet), RawSourceSnippet);
            Assert.IsFalse(ObjectContainsString(value, RawToken), RawToken);
            Assert.IsFalse(ObjectContainsString(value, "alice"), "alice");
        }

        private static bool ObjectContainsString(object value, string expected)
        {
            return ObjectContainsString(value, expected, new HashSet<object>());
        }

        private static bool ObjectContainsString(object value, string expected, HashSet<object> visited)
        {
            if (value == null || string.IsNullOrEmpty(expected))
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

        private static T RunAsync<T>(Func<Task<T>> operation)
        {
            return Task.Run(operation).GetAwaiter().GetResult();
        }

        private sealed class Phase7Fixture
        {
            public Phase7Fixture(string repositoryPath, ApprovedActivityAnalysisViewModel dashboard, FakeLocalRepository repository)
            {
                RepositoryPath = repositoryPath;
                Dashboard = dashboard;
                Repository = repository;
            }

            public string RepositoryPath { get; }
            public ApprovedActivityAnalysisViewModel Dashboard { get; }
            public FakeLocalRepository Repository { get; }
        }

        private sealed class FakeRepositoryPicker : IRepositoryPicker
        {
            private readonly string repositoryRootPath;

            public FakeRepositoryPicker(string repositoryRootPath)
            {
                this.repositoryRootPath = repositoryRootPath;
            }

            public Task<RepositoryPickerResult> PickRepositoryAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(RepositoryPickerResult.Selected(repositoryRootPath));
            }
        }

        private sealed class FakeAgentLogLocationPicker : IAgentLogLocationPicker
        {
            private readonly string agentLogLocationPath;

            public FakeAgentLogLocationPicker(string agentLogLocationPath)
            {
                this.agentLogLocationPath = agentLogLocationPath;
            }

            public Task<AgentLogLocationPickerResult> PickAgentLogLocationAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(AgentLogLocationPickerResult.Selected(agentLogLocationPath));
            }
        }

        private sealed class FakeAgentLogReader : IAgentLogSourceReader
        {
            public Task<AgentLogReadResult> ReadAsync(AgentAnalysisInput input, CancellationToken cancellationToken)
            {
                var entries = new List<AgentLogEntry>
                {
                    Entry("{\"provider\":\"claude\",\"timestamp\":\"2026-05-14T01:00:00Z\",\"session_id\":\"session-1\",\"type\":\"message\",\"role\":\"user\",\"prompt\":\"" + RawPrompt + "\",\"branchName\":\"" + RawBranchName + "\"}"),
                    Entry("{\"provider\":\"claude\",\"timestamp\":\"2026-05-14T01:01:00Z\",\"tool_name\":\"Read\",\"file_path\":\"/Users/alice/SecretRepo/src/private/" + RawFileName + "\"}"),
                    Entry("{\"provider\":\"claude\",\"timestamp\":\"2026-05-14T01:02:00Z\",\"tool_name\":\"Edit\",\"file_path\":\"/Users/alice/SecretRepo/src/private/" + RawFileName + "\",\"response\":\"" + RawResponse + "\",\"sourceText\":\"" + Escape(RawSourceSnippet) + "\"}"),
                    Entry("{\"provider\":\"claude\",\"timestamp\":\"2026-05-14T01:03:00Z\",\"tool_name\":\"Bash\",\"command\":\"" + RawCommand + "\",\"authorization\":\"" + RawToken + "\",\"username\":\"alice\"}")
                };

                return Task.FromResult(AgentLogReadResult.Success(entries, new List<string>()));
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
        }

        private sealed class FakeGitRunner : IGitCommandRunner
        {
            public Task<GitCommandResult> RunAsync(string workingDirectory, string arguments, CancellationToken cancellationToken)
            {
                switch (arguments)
                {
                    case "rev-parse --is-inside-work-tree":
                        return Task.FromResult(GitCommandResult.Success("true\n"));
                    case "status --porcelain":
                        return Task.FromResult(GitCommandResult.Success(" M src/private/" + RawFileName + "\n"));
                    case "diff --numstat":
                        return Task.FromResult(GitCommandResult.Success("120\t8\tsrc/private/" + RawFileName + "\n"));
                    case "diff --cached --numstat":
                        return Task.FromResult(GitCommandResult.Success(string.Empty));
                    case "log --since=7.days.ago --numstat --format=--TOKENFORGE-COMMIT-- -n 200":
                    case "log --since=7.days.ago --numstat --format=--TOKENFORGE-COMMIT-- -n 50":
                        return Task.FromResult(GitCommandResult.Success("--TOKENFORGE-COMMIT--\n1\t0\tREADME.md\n"));
                    default:
                        return Task.FromResult(GitCommandResult.Success(string.Empty));
                }
            }
        }

        private sealed class FakeLocalRepository : ILocalSaveDataRepository
        {
            private readonly PrivacySanitizer privacySanitizer = new PrivacySanitizer();

            public SaveData Current { get; private set; } = SaveData.CreateDefault();
            public int SaveCount { get; private set; }

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

                SaveCount++;
                Current = saveData;
                return Task.FromResult(Result.Success());
            }
        }
    }
}
