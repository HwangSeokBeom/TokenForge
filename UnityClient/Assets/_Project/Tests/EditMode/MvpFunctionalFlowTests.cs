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
using TokenForge.Client.UI;

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

        [Test]
        public void GitRepositorySelectClearSkip_UsesSafeAliasAndState()
        {
            var fixture = CreateFixture();

            var selected = RunAsync(() => fixture.ViewModel.SelectLocalGitRepositoryForOnboardingAsync());
            Assert.IsTrue(selected.IsSuccess, selected.ErrorMessage);
            Assert.IsTrue(fixture.ViewModel.Onboarding.GitConnected);
            Assert.AreEqual("Local Repository", fixture.ViewModel.Onboarding.GitSafeAlias);
            Assert.IsFalse(ObjectContainsString(fixture.ViewModel.Onboarding, fixture.RawRepositoryPath));
            Assert.IsFalse(ObjectContainsString(fixture.ViewModel.Onboarding, RawRepoName));

            fixture.ViewModel.ClearGitForOnboarding();
            Assert.IsFalse(fixture.ViewModel.Onboarding.GitConnected);
            Assert.IsFalse(fixture.ViewModel.GitFlow.HasSelectedRepositoryForLocalOnlyApproval);

            fixture.ViewModel.SkipGitForOnboarding();
            Assert.IsTrue(fixture.ViewModel.Onboarding.GitSkipped);
            Assert.AreEqual("Skipped", fixture.ViewModel.Onboarding.GitSafeAlias);
        }

        [Test]
        public void AnalyzeGitWithoutRepository_FailsSafely()
        {
            var fixture = CreateFixture();

            var result = RunAsync(() => fixture.ViewModel.AnalyzeGitActivityAsync());

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("missing_repository_selection", result.ErrorCode);
            Assert.That(BootstrapUiTextFormatter.FriendlyGitStatus(fixture.ViewModel), Does.Contain("Select a repository"));
            AssertNoForbiddenText(fixture.ViewModel.GitFlow);
        }

        [Test]
        public void AnalyzeGitWithFakeRepository_CreatesSafeReview()
        {
            var fixture = CreateFixture();

            RunAsync(() => fixture.ViewModel.SelectLocalGitRepositoryForOnboardingAsync());
            var result = RunAsync(() => fixture.ViewModel.AnalyzeGitActivityAsync());

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            Assert.AreEqual(GitAnalysisFlowState.ReviewReady, fixture.ViewModel.GitFlow.State);
            Assert.IsTrue(fixture.ViewModel.GitFlow.HasPendingReview);
            AssertNoForbiddenText(result.Value);
        }

        [Test]
        public void SaveGitReview_PersistsOnlySafeAggregate()
        {
            var fixture = CreateFixture();

            RunAsync(() => fixture.ViewModel.SelectLocalGitRepositoryForOnboardingAsync());
            RunAsync(() => fixture.ViewModel.AnalyzeGitActivityAsync());
            var save = RunAsync(() => fixture.ViewModel.SaveGitSessionAsync());

            Assert.IsTrue(save.IsSuccess, save.ErrorMessage);
            Assert.AreEqual(1, fixture.Repository.Current.WorkSessionSummaries.Count);
            Assert.AreEqual(2, fixture.Repository.SaveCount);
            Assert.IsTrue(new PrivacySanitizer().ValidateSafeSaveData(fixture.Repository.Current).IsSuccess);
            AssertNoForbiddenText(fixture.Repository.Current);
        }

        [Test]
        public void DiscardGitReview_DoesNotMutateSavedSessions()
        {
            var fixture = CreateFixture();
            fixture.Repository.Current.WorkSessionSummaries.Add(new AgentWorkSession { SessionId = "existing-safe-session", SourceProvider = "GIT" });

            RunAsync(() => fixture.ViewModel.SelectLocalGitRepositoryForOnboardingAsync());
            RunAsync(() => fixture.ViewModel.AnalyzeGitActivityAsync());
            fixture.ViewModel.DiscardGitReview();

            Assert.AreEqual(1, fixture.Repository.Current.WorkSessionSummaries.Count);
            Assert.AreEqual(1, fixture.Repository.SaveCount);
            Assert.IsFalse(fixture.ViewModel.GitFlow.HasPendingReview);
        }

        [Test]
        public void AgentManualLogSelectAnalyzeSaveDiscard_UsesSafeAggregateOnly()
        {
            var fixture = CreateFixture();

            var selected = RunAsync(() => fixture.ViewModel.SelectManualAgentLogForOnboardingAsync());
            Assert.IsTrue(selected.IsSuccess, selected.ErrorMessage);
            Assert.IsTrue(fixture.ViewModel.AgentFlow.HasSelectedAgentLogLocationForLocalOnlyApproval);
            Assert.IsFalse(ObjectContainsString(fixture.ViewModel.Onboarding, fixture.RawAgentLogPath));

            var review = RunAsync(() => fixture.ViewModel.AnalyzeSelectedAgentActivityAsync());
            Assert.IsTrue(review.IsSuccess, review.ErrorMessage);
            Assert.AreEqual(AgentAnalysisFlowState.ReviewReady, fixture.ViewModel.AgentFlow.State);
            AssertNoForbiddenText(review.Value);

            fixture.ViewModel.DiscardAgentReview();
            Assert.IsFalse(fixture.ViewModel.AgentFlow.HasPendingReview);
            Assert.AreEqual(0, fixture.Repository.SaveCount);

            RunAsync(() => fixture.ViewModel.SelectManualAgentLogForOnboardingAsync());
            RunAsync(() => fixture.ViewModel.AnalyzeSelectedAgentActivityAsync());
            var save = RunAsync(() => fixture.ViewModel.SaveAgentSessionAsync());
            Assert.IsTrue(save.IsSuccess, save.ErrorMessage);
            Assert.AreEqual(1, fixture.Repository.Current.WorkSessionSummaries.Count);
            AssertNoForbiddenText(fixture.Repository.Current);
        }

        [Test]
        public void UnsupportedSelectedAgent_FailsWithoutManualLogFolder()
        {
            var fixture = CreateFixture();
            fixture.ViewModel.SetAgentSourceSelected(ConnectedAgentSourceType.Codex, true);

            var result = RunAsync(() => fixture.ViewModel.AnalyzeSelectedAgentActivityAsync());

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("agent_source_manual_import_required", result.ErrorCode);
            Assert.That(BootstrapUiTextFormatter.FriendlyAgentStatus(fixture.ViewModel), Does.Contain("Detect a local source"));
            Assert.AreEqual(0, fixture.Repository.SaveCount);
        }

        [Test]
        public void DashboardReflectsSavedSession()
        {
            var fixture = CreateFixture();

            RunAsync(() => fixture.ViewModel.SelectLocalGitRepositoryForOnboardingAsync());
            RunAsync(() => fixture.ViewModel.AnalyzeGitActivityAsync());
            RunAsync(() => fixture.ViewModel.SaveGitSessionAsync());

            Assert.IsTrue(fixture.ViewModel.CharacterDashboard.HasSavedRun);
            Assert.That(fixture.ViewModel.CharacterDashboard.LatestSafeSessionSummary, Does.Contain("Git"));
            Assert.That(fixture.ViewModel.CharacterDashboard.RecentGrowthSummary, Does.Contain("XP"));
            AssertNoForbiddenText(fixture.ViewModel.CharacterDashboard);
        }

        [Test]
        public void PrivacyForbiddenFields_NeverPersist()
        {
            var fixture = CreateFixture();

            RunAsync(() => fixture.ViewModel.SelectLocalGitRepositoryForOnboardingAsync());
            RunAsync(() => fixture.ViewModel.AnalyzeGitActivityAsync());
            RunAsync(() => fixture.ViewModel.SaveGitSessionAsync());
            RunAsync(() => fixture.ViewModel.SelectManualAgentLogForOnboardingAsync());
            RunAsync(() => fixture.ViewModel.AnalyzeSelectedAgentActivityAsync());
            RunAsync(() => fixture.ViewModel.SaveAgentSessionAsync());

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

        private static T RunAsync<T>(Func<Task<T>> action)
        {
            return action().GetAwaiter().GetResult();
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
