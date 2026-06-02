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
using TokenForge.Client.Platform;
using TokenForge.Client.Privacy;
using TokenForge.Client.UI;
using UnityEngine.TestTools;

namespace TokenForge.Client.Tests
{
    public sealed class MvpFunctionalFlowTests
    {
        private const string RawRepoName = "SecretRepo";
        private const string RawFilePath = "/Users/alice/SecretRepo/src/private/UserSecret.cs";
        private const string RawFileName = "UserSecret.cs";
        private const string RawCommitMessage = "Fix production auth token";
        private const string RawDiff = "diff --git a/src/private/UserSecret.cs b/src/private/UserSecret.cs";
        private const string RawPrompt = "please inspect the production auth token flow";
        private const string RawLogText = "response includes customer secret handling";
        private const string RawSourceText = "public class Secret { string password = \"hunter2\"; }";
        private const string RawToken = "Bearer abc.def.secret";

        [Test]
        public void AiAgentSelectRemove_UpdatesStateWithoutRawData()
        {
            var fixture = CreateFixture();

            fixture.ViewModel.SetAgentSourceSelected(ConnectedAgentSourceType.Codex, true);
            var codex = fixture.ViewModel.Onboarding.AgentSources.First(source => source.SourceType == ConnectedAgentSourceType.Codex);

            Assert.IsTrue(codex.Selected);
            Assert.AreEqual(AgentSourceSetupState.Selected, codex.State);
            Assert.That(codex.StatusLabel, Does.Contain("Codex selected"));

            fixture.ViewModel.SetAgentSourceSelected(ConnectedAgentSourceType.Codex, false);
            Assert.IsFalse(codex.Selected);
            Assert.AreEqual(AgentSourceSetupState.NotSelected, codex.State);
            AssertNoForbiddenText(codex);
        }

        [UnityTest]
        public IEnumerator GitRepositorySelectClearSkip_UsesSafeAliasAndState()
        {
            var fixture = CreateFixture();

            var selectedTask = fixture.ViewModel.SelectLocalGitRepositoryForOnboardingAsync();
            yield return WaitForTask(selectedTask, nameof(fixture.ViewModel.SelectLocalGitRepositoryForOnboardingAsync));
            var selected = selectedTask.Result;
            Assert.IsTrue(selected.IsSuccess, selected.ErrorMessage);
            Assert.IsTrue(fixture.ViewModel.Onboarding.GitConnected);
            Assert.IsFalse(string.IsNullOrWhiteSpace(fixture.ViewModel.Onboarding.GitSafeAlias));
            Assert.IsFalse(ObjectContainsString(fixture.ViewModel.Onboarding, fixture.RawRepositoryPath));

            fixture.ViewModel.ClearGitForOnboarding();
            Assert.IsFalse(fixture.ViewModel.Onboarding.GitConnected);
            Assert.IsFalse(fixture.ViewModel.GitFlow.HasSelectedRepositoryForLocalOnlyApproval);

            fixture.ViewModel.SkipGitForOnboarding();
            Assert.IsTrue(fixture.ViewModel.Onboarding.GitSkipped);
            Assert.AreEqual("Skipped", fixture.ViewModel.Onboarding.GitSafeAlias);
        }

        [UnityTest]
        public IEnumerator AnalyzeGitWithoutRepository_FailsSafely()
        {
            var fixture = CreateFixture();

            var resultTask = fixture.ViewModel.AnalyzeGitActivityAsync();
            yield return WaitForTask(resultTask, nameof(fixture.ViewModel.AnalyzeGitActivityAsync));
            var result = resultTask.Result;

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("NoActiveRepository", result.ErrorCode);
            Assert.That(BootstrapUiTextFormatter.FriendlyGitStatus(fixture.ViewModel), Does.Contain("Connect a repository"));
            AssertNoForbiddenText(fixture.ViewModel.GitFlow);
        }

        [UnityTest]
        public IEnumerator AnalyzeGitWithFakeRepository_CreatesSafeReview()
        {
            var fixture = CreateFixture();

            var selectTask = fixture.ViewModel.SelectLocalGitRepositoryForOnboardingAsync();
            yield return WaitForTask(selectTask, nameof(fixture.ViewModel.SelectLocalGitRepositoryForOnboardingAsync));
            var resultTask = fixture.ViewModel.AnalyzeGitActivityAsync();
            yield return WaitForTask(resultTask, nameof(fixture.ViewModel.AnalyzeGitActivityAsync));
            var result = resultTask.Result;

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            Assert.AreEqual(GitAnalysisFlowState.ReviewReady, fixture.ViewModel.GitFlow.State);
            Assert.IsTrue(fixture.ViewModel.GitFlow.HasPendingReview);
            AssertNoForbiddenText(result.Value);
        }

        [UnityTest]
        public IEnumerator SaveGitReview_PersistsOnlySafeAggregate()
        {
            var fixture = CreateFixture();

            var selectTask = fixture.ViewModel.SelectLocalGitRepositoryForOnboardingAsync();
            yield return WaitForTask(selectTask, nameof(fixture.ViewModel.SelectLocalGitRepositoryForOnboardingAsync));
            var analyzeTask = fixture.ViewModel.AnalyzeGitActivityAsync();
            yield return WaitForTask(analyzeTask, nameof(fixture.ViewModel.AnalyzeGitActivityAsync));
            var saveTask = fixture.ViewModel.SaveGitSessionAsync();
            yield return WaitForTask(saveTask, nameof(fixture.ViewModel.SaveGitSessionAsync));
            var save = saveTask.Result;

            Assert.IsTrue(save.IsSuccess, save.ErrorMessage);
            Assert.AreEqual(1, fixture.Repository.Current.WorkSessionSummaries.Count);
            Assert.GreaterOrEqual(fixture.Repository.SaveCount, 1);
            Assert.IsTrue(new PrivacySanitizer().ValidateSafeSaveData(fixture.Repository.Current).IsSuccess);
            AssertNoForbiddenText(fixture.Repository.Current);
        }

        [UnityTest]
        public IEnumerator DiscardGitReview_DoesNotMutateSavedSessions()
        {
            var fixture = CreateFixture();
            fixture.Repository.Current.WorkSessionSummaries.Add(new AgentWorkSession { SessionId = "existing-safe-session", SourceProvider = "GIT" });

            var selectTask = fixture.ViewModel.SelectLocalGitRepositoryForOnboardingAsync();
            yield return WaitForTask(selectTask, nameof(fixture.ViewModel.SelectLocalGitRepositoryForOnboardingAsync));
            var analyzeTask = fixture.ViewModel.AnalyzeGitActivityAsync();
            yield return WaitForTask(analyzeTask, nameof(fixture.ViewModel.AnalyzeGitActivityAsync));
            fixture.ViewModel.DiscardGitReview();

            Assert.AreEqual(1, fixture.Repository.Current.WorkSessionSummaries.Count);
            Assert.IsFalse(fixture.ViewModel.GitFlow.HasPendingReview);
            AssertNoForbiddenText(fixture.Repository.Current);
        }

        [UnityTest]
        public IEnumerator AgentManualLogSelectAnalyzeSaveDiscard_UsesSafeAggregateOnly()
        {
            var fixture = CreateFixture();

            var selectedTask = fixture.ViewModel.SelectManualAgentLogForOnboardingAsync();
            yield return WaitForTask(selectedTask, nameof(fixture.ViewModel.SelectManualAgentLogForOnboardingAsync));
            var selected = selectedTask.Result;
            Assert.IsTrue(selected.IsSuccess, selected.ErrorMessage);
            Assert.IsTrue(fixture.ViewModel.AgentFlow.HasSelectedAgentLogLocationForLocalOnlyApproval);
            Assert.IsFalse(ObjectContainsString(fixture.ViewModel.Onboarding, fixture.RawAgentLogPath));

            var reviewTask = fixture.ViewModel.AnalyzeSelectedAgentActivityAsync();
            yield return WaitForTask(reviewTask, nameof(fixture.ViewModel.AnalyzeSelectedAgentActivityAsync));
            var review = reviewTask.Result;
            Assert.IsTrue(review.IsSuccess, review.ErrorMessage);
            Assert.AreEqual(AgentAnalysisFlowState.ReviewReady, fixture.ViewModel.AgentFlow.State);
            AssertNoForbiddenText(review.Value);

            fixture.ViewModel.DiscardAgentReview();
            Assert.IsFalse(fixture.ViewModel.AgentFlow.HasPendingReview);
            Assert.AreEqual(0, fixture.Repository.Current.WorkSessionSummaries.Count);

            var secondSelectedTask = fixture.ViewModel.SelectManualAgentLogForOnboardingAsync();
            yield return WaitForTask(secondSelectedTask, "second " + nameof(fixture.ViewModel.SelectManualAgentLogForOnboardingAsync));
            var secondReviewTask = fixture.ViewModel.AnalyzeSelectedAgentActivityAsync();
            yield return WaitForTask(secondReviewTask, "second " + nameof(fixture.ViewModel.AnalyzeSelectedAgentActivityAsync));
            var saveTask = fixture.ViewModel.SaveAgentSessionAsync();
            yield return WaitForTask(saveTask, nameof(fixture.ViewModel.SaveAgentSessionAsync));
            var save = saveTask.Result;
            Assert.IsTrue(save.IsSuccess, save.ErrorMessage);
            Assert.AreEqual(1, fixture.Repository.Current.WorkSessionSummaries.Count);
            AssertNoForbiddenText(fixture.Repository.Current);
        }

        [UnityTest]
        public IEnumerator UnsupportedSelectedAgent_FailsWithoutManualLogFolder()
        {
            var fixture = CreateFixture();
            fixture.ViewModel.SetAgentSourceSelected(ConnectedAgentSourceType.Codex, true);

            var resultTask = fixture.ViewModel.AnalyzeSelectedAgentActivityAsync();
            yield return WaitForTask(resultTask, nameof(fixture.ViewModel.AnalyzeSelectedAgentActivityAsync));
            var result = resultTask.Result;

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("agent_source_manual_import_required", result.ErrorCode);
            Assert.That(BootstrapUiTextFormatter.FriendlyAgentStatus(fixture.ViewModel), Does.Contain("Detect a local source"));
            Assert.AreEqual(0, fixture.Repository.SaveCount);
        }

        [UnityTest]
        public IEnumerator DashboardReflectsSavedSession()
        {
            var fixture = CreateFixture();

            var selectTask = fixture.ViewModel.SelectLocalGitRepositoryForOnboardingAsync();
            yield return WaitForTask(selectTask, nameof(fixture.ViewModel.SelectLocalGitRepositoryForOnboardingAsync));
            var analyzeTask = fixture.ViewModel.AnalyzeGitActivityAsync();
            yield return WaitForTask(analyzeTask, nameof(fixture.ViewModel.AnalyzeGitActivityAsync));
            var saveTask = fixture.ViewModel.SaveGitSessionAsync();
            yield return WaitForTask(saveTask, nameof(fixture.ViewModel.SaveGitSessionAsync));

            Assert.IsTrue(fixture.ViewModel.CharacterDashboard.HasSavedRun);
            Assert.That(fixture.ViewModel.CharacterDashboard.LatestSafeSessionSummary, Does.Contain("Git"));
            Assert.That(fixture.ViewModel.CharacterDashboard.RecentGrowthSummary, Does.Contain("XP"));
            AssertNoForbiddenText(fixture.ViewModel.CharacterDashboard);
        }

        [UnityTest]
        public IEnumerator PrivacyForbiddenFields_NeverPersist()
        {
            var fixture = CreateFixture();

            var selectGitTask = fixture.ViewModel.SelectLocalGitRepositoryForOnboardingAsync();
            yield return WaitForTask(selectGitTask, nameof(fixture.ViewModel.SelectLocalGitRepositoryForOnboardingAsync));
            var analyzeGitTask = fixture.ViewModel.AnalyzeGitActivityAsync();
            yield return WaitForTask(analyzeGitTask, nameof(fixture.ViewModel.AnalyzeGitActivityAsync));
            var saveGitTask = fixture.ViewModel.SaveGitSessionAsync();
            yield return WaitForTask(saveGitTask, nameof(fixture.ViewModel.SaveGitSessionAsync));
            var selectAgentTask = fixture.ViewModel.SelectManualAgentLogForOnboardingAsync();
            yield return WaitForTask(selectAgentTask, nameof(fixture.ViewModel.SelectManualAgentLogForOnboardingAsync));
            var analyzeAgentTask = fixture.ViewModel.AnalyzeSelectedAgentActivityAsync();
            yield return WaitForTask(analyzeAgentTask, nameof(fixture.ViewModel.AnalyzeSelectedAgentActivityAsync));
            var saveAgentTask = fixture.ViewModel.SaveAgentSessionAsync();
            yield return WaitForTask(saveAgentTask, nameof(fixture.ViewModel.SaveAgentSessionAsync));

            AssertNoForbiddenText(fixture.Repository.Current);
        }

        private static MvpFixture CreateFixture()
        {
            var privacy = new PrivacySanitizer();
            var repository = new FakeRepository();
            var rawRepositoryPath = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName(), RawRepoName);
            var rawAgentLogPath = Path.Combine(rawRepositoryPath, "logs", "claude-private.jsonl");
            Directory.CreateDirectory(rawRepositoryPath);
            var viewModel = new ApprovedActivityAnalysisViewModel(
                new GitAnalysisFlowController(
                    new FakeRepositoryPicker(rawRepositoryPath),
                    new GitAggregateAnalyzer(new FakeGitRunner(), privacy),
                    repository,
                    null,
                    privacy),
                new AgentAnalysisFlowController(
                    new AgentLogActivityProvider(new AgentActivityAnalyzer(new FakeAgentLogReader(), null, privacy)),
                    repository,
                    null,
                    privacy),
                new FakeAgentLogLocationPicker(rawAgentLogPath),
                repository,
                privacy,
                new ApprovedLocationSettingsRepository(Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName())));
            return new MvpFixture(viewModel, repository, rawRepositoryPath, rawAgentLogPath);
        }

        private static void AssertNoForbiddenText(object value)
        {
            foreach (var forbidden in new[]
            {
                RawRepoName,
                RawFilePath,
                RawFileName,
                RawCommitMessage,
                RawDiff,
                RawPrompt,
                RawLogText,
                RawSourceText,
                RawToken,
                "password",
                "hunter2",
                "production auth token"
            })
            {
                Assert.IsFalse(ObjectContainsString(value, forbidden), "Forbidden value persisted or rendered: " + forbidden);
            }
        }

        private static bool ObjectContainsString(object value, string expected)
        {
            return ObjectContainsString(value, expected, new HashSet<object>());
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

        private static bool ObjectContainsString(object value, string expected, HashSet<object> visited)
        {
            if (value == null || string.IsNullOrWhiteSpace(expected))
            {
                return false;
            }

            if (value is string text)
            {
                return text.Contains(expected);
            }

            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || type == typeof(DateTime) || type == typeof(DateTimeOffset))
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
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                if (ObjectContainsString(property.GetValue(value, null), expected, visited))
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class MvpFixture
        {
            public MvpFixture(ApprovedActivityAnalysisViewModel viewModel, FakeRepository repository, string rawRepositoryPath, string rawAgentLogPath)
            {
                ViewModel = viewModel;
                Repository = repository;
                RawRepositoryPath = rawRepositoryPath;
                RawAgentLogPath = rawAgentLogPath;
            }

            public ApprovedActivityAnalysisViewModel ViewModel { get; }
            public FakeRepository Repository { get; }
            public string RawRepositoryPath { get; }
            public string RawAgentLogPath { get; }
        }

        private sealed class FakeRepository : ILocalSaveDataRepository
        {
            public SaveData Current { get; set; } = SaveData.CreateDefault();
            public int SaveCount { get; private set; }

            public Task<SaveData> LoadAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Current);
            }

            public Task<Result> SaveAsync(SaveData saveData, CancellationToken cancellationToken = default)
            {
                SaveCount++;
                Current = saveData;
                return Task.FromResult(Result.Success());
            }
        }

        private sealed class FakeRepositoryPicker : IRepositoryPicker
        {
            private readonly string path;

            public FakeRepositoryPicker(string path)
            {
                this.path = path;
            }

            public Task<RepositoryPickerResult> PickRepositoryAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(RepositoryPickerResult.Selected(path));
            }
        }

        private sealed class FakeAgentLogLocationPicker : IAgentLogLocationPicker
        {
            private readonly string path;

            public FakeAgentLogLocationPicker(string path)
            {
                this.path = path;
            }

            public Task<AgentLogLocationPickerResult> PickAgentLogLocationAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(AgentLogLocationPickerResult.Selected(path));
            }
        }

        private sealed class FakeGitRunner : IGitCommandRunner
        {
            public Task<GitCommandResult> RunAsync(string workingDirectory, string arguments, CancellationToken cancellationToken)
            {
                if (arguments == "rev-parse --is-inside-work-tree")
                {
                    return Task.FromResult(GitCommandResult.Success("true\n"));
                }

                if (arguments == "rev-parse HEAD")
                {
                    return Task.FromResult(GitCommandResult.Success("HEADSHA\n"));
                }

                if (arguments == "log --all --reverse --format=%cI -n 1")
                {
                    return Task.FromResult(GitCommandResult.Success("2026-01-02T03:04:05Z\n"));
                }

                if (arguments == "rev-list --all --count")
                {
                    return Task.FromResult(GitCommandResult.Success("42\n"));
                }

                if (arguments == "status --porcelain")
                {
                    return Task.FromResult(GitCommandResult.Success(" M " + RawFilePath + "\n"));
                }

                if (arguments == "diff --numstat" || arguments == "diff --cached --numstat")
                {
                    return Task.FromResult(GitCommandResult.Success("12\t3\t" + RawFilePath + "\n"));
                }

                if (arguments.StartsWith("log --since=", StringComparison.Ordinal))
                {
                    return Task.FromResult(GitCommandResult.Success("--TOKENFORGE-COMMIT--\n8\t2\t" + RawFilePath + "\n"));
                }

                if (arguments == "log --all --numstat --format=--TOKENFORGE-COMMIT--")
                {
                    return Task.FromResult(GitCommandResult.Success("--TOKENFORGE-COMMIT--\n8\t2\t" + RawFilePath + "\n"));
                }

                return Task.FromResult(GitCommandResult.Failure("unsupported_git_command", "Unsupported test command."));
            }
        }

        private sealed class FakeAgentLogReader : IAgentLogSourceReader
        {
            public Task<AgentLogReadResult> ReadAsync(AgentAnalysisInput input, CancellationToken cancellationToken)
            {
                return Task.FromResult(AgentLogReadResult.Success(new List<AgentLogEntry>
                {
                    new AgentLogEntry
                    {
                        Text = "{\"provider\":\"claude\",\"timestamp\":\"2026-05-15T01:00:00Z\",\"session_id\":\"session-1\",\"type\":\"message\",\"role\":\"user\",\"content\":\"safe aggregate fixture\",\"tool_name\":\"Edit\",\"language\":\"CSharp\"}",
                        LastWriteTimeUtc = new DateTimeOffset(2026, 5, 15, 1, 0, 0, TimeSpan.Zero)
                    }
                }, new List<string>()));
            }
        }
    }
}
