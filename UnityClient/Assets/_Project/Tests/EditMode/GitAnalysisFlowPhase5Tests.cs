using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
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
    public sealed class GitAnalysisFlowPhase5Tests
    {
        private const string RawRepoName = "SecretRepo";
        private const string RawFilePath = "/Users/alice/SecretRepo/Assets/private/UserSecret.cs";
        private const string RawFileName = "UserSecret.cs";
        private const string RawBranchName = "feature/customer-token";
        private const string RawCommitMessage = "Fix production auth using customer token";
        private const string RawDiff = "diff --git a/private/UserSecret.cs b/private/UserSecret.cs";
        private const string RawSourceText = "public class Secret { string password = \"hunter2\"; }";
        private const string RawToken = "Bearer abc.def.secret";
        private const string RawAuthorization = "authorization: Bearer abc.def.secret";

        [Test]
        public void RepositoryPickerResult_RawPathIsNotExposedInReviewModel()
        {
            var fixture = CreateSuccessfulFixture();

            RunAsync(() => fixture.Controller.SelectRepositoryAsync(CancellationToken.None));
            var result = RunAsync(() => fixture.Controller.AnalyzeAsync(CancellationToken.None));

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            Assert.IsFalse(ObjectContainsString(result.Value, fixture.RepositoryPath));
            Assert.IsFalse(ObjectContainsString(result.Value, RawRepoName));
            Assert.AreEqual("Repository analyzed", fixture.Controller.SelectionStatus);
        }

        [Test]
        public void SelectedPath_IsKeptOnlyInMemoryBeforeAnalysis()
        {
            var fixture = CreateSuccessfulFixture();

            RunAsync(() => fixture.Controller.SelectRepositoryAsync(CancellationToken.None));

            Assert.AreEqual(GitAnalysisFlowState.Selected, fixture.Controller.State);
            Assert.IsNull(fixture.Controller.Review);
            Assert.AreEqual(1, fixture.Repository.LoadCount);
            Assert.AreEqual(1, fixture.Repository.SaveCount);
            AssertLogsAreSafe(fixture.Logger.Messages, fixture.RepositoryPath);
        }

        [Test]
        public void ReviewModel_ContainsOnlySafeAggregateFields()
        {
            var fixture = CreateSuccessfulFixture();

            RunAsync(() => fixture.Controller.SelectRepositoryAsync(CancellationToken.None));
            var result = RunAsync(() => fixture.Controller.AnalyzeAsync(CancellationToken.None));

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            Assert.AreEqual("GIT", result.Value.SourceProvider);
            Assert.AreEqual("Git aggregate session", result.Value.SessionAlias);
            Assert.AreEqual(LineChangeBucket.Huge, result.Value.AddedLinesBucket);
            Assert.AreEqual(CountBucket.Small, result.Value.CommitCountBucket);
            Assert.Greater(result.Value.ExtensionCategoryBuckets.Count, 0);
            Assert.IsTrue(new PrivacySanitizer().ValidateNoForbiddenFields(result.Value).IsSuccess);
        }

        [Test]
        public void ReviewModel_DoesNotContainRawGitData()
        {
            var fixture = CreateSuccessfulFixture();

            RunAsync(() => fixture.Controller.SelectRepositoryAsync(CancellationToken.None));
            RunAsync(() => fixture.Controller.AnalyzeAsync(CancellationToken.None));

            AssertNoRawGitData(fixture.Controller.Review, fixture.RepositoryPath);
        }

        [Test]
        public void SaveSession_PersistsOnlySafeSessionData()
        {
            var fixture = CreateSuccessfulFixture();

            RunAsync(() => fixture.Controller.SelectRepositoryAsync(CancellationToken.None));
            RunAsync(() => fixture.Controller.AnalyzeAsync(CancellationToken.None));
            var saveResult = RunAsync(() => fixture.Controller.SaveSessionAsync(CancellationToken.None));

            Assert.IsTrue(saveResult.IsSuccess, saveResult.ErrorMessage);
            Assert.AreEqual(1, fixture.Repository.Current.WorkSessionSummaries.Count);
            Assert.AreEqual("GIT", fixture.Repository.Current.WorkSessionSummaries[0].SourceProvider);
            AssertNoRawGitData(fixture.Repository.Current, fixture.RepositoryPath);
            Assert.IsTrue(new PrivacySanitizer().ValidateSafeSaveData(fixture.Repository.Current).IsSuccess);
        }

        [Test]
        public void SaveSession_ValidatesPrivacyBeforeWriting()
        {
            var fixture = CreateSuccessfulFixture();

            RunAsync(() => fixture.Controller.SelectRepositoryAsync(CancellationToken.None));
            RunAsync(() => fixture.Controller.AnalyzeAsync(CancellationToken.None));
            var saveResult = RunAsync(() => fixture.Controller.SaveSessionAsync(CancellationToken.None));

            Assert.IsTrue(saveResult.IsSuccess, saveResult.ErrorMessage);
            Assert.AreEqual(2, fixture.Repository.SaveCount);
            Assert.IsTrue(fixture.Repository.LastSaveWasPrivacySafe);
        }

        [Test]
        public void FailedAnalysis_DoesNotCorruptExistingSaveData()
        {
            var fixture = CreateFixture(FakeRunner.FailOn("status --porcelain"));
            fixture.Repository.Current.CharacterProfile.TotalExp = 123;
            fixture.Repository.Current.WorkSessionSummaries.Add(new AgentWorkSession { SessionId = "existing-safe-session", SourceProvider = "MANUAL" });

            RunAsync(() => fixture.Controller.SelectRepositoryAsync(CancellationToken.None));
            var result = RunAsync(() => fixture.Controller.AnalyzeAsync(CancellationToken.None));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(123, fixture.Repository.Current.CharacterProfile.TotalExp);
            Assert.AreEqual(1, fixture.Repository.Current.WorkSessionSummaries.Count);
            Assert.AreEqual(1, fixture.Repository.SaveCount);
        }

        [Test]
        public void FailedSave_DoesNotCorruptExistingSaveData()
        {
            var fixture = CreateSuccessfulFixture();
            fixture.Repository.Current.CharacterProfile.TotalExp = 77;
            fixture.Repository.FailSaves = true;

            RunAsync(() => fixture.Controller.SelectRepositoryAsync(CancellationToken.None));
            RunAsync(() => fixture.Controller.AnalyzeAsync(CancellationToken.None));
            var result = RunAsync(() => fixture.Controller.SaveSessionAsync(CancellationToken.None));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(77, fixture.Repository.Current.CharacterProfile.TotalExp);
            Assert.AreEqual(0, fixture.Repository.Current.WorkSessionSummaries.Count);
        }

        [Test]
        public void SyncNow_IsNotCalledBeforeSaveConfirmation()
        {
            var fixture = CreateSuccessfulFixture();

            RunAsync(() => fixture.Controller.SelectRepositoryAsync(CancellationToken.None));
            RunAsync(() => fixture.Controller.AnalyzeAsync(CancellationToken.None));
            var result = RunAsync(() => fixture.Controller.SyncNowAsync(CancellationToken.None));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("sync_requires_saved_session", result.ErrorCode);
            Assert.AreEqual(0, fixture.Sync.PushThenPullCount);
        }

        [Test]
        public void SyncNow_CallsExistingSyncServiceOnlyAfterSave()
        {
            var fixture = CreateSuccessfulFixture();

            RunAsync(() => fixture.Controller.SelectRepositoryAsync(CancellationToken.None));
            RunAsync(() => fixture.Controller.AnalyzeAsync(CancellationToken.None));
            RunAsync(() => fixture.Controller.SaveSessionAsync(CancellationToken.None));
            var syncResult = RunAsync(() => fixture.Controller.SyncNowAsync(CancellationToken.None));

            Assert.IsTrue(syncResult.IsSuccess, syncResult.ErrorMessage);
            Assert.AreEqual(1, fixture.Sync.PushThenPullCount);
            Assert.AreEqual(GitAnalysisFlowState.Synced, fixture.Controller.State);
        }

        [Test]
        public void Logs_DoNotContainRawPathTokenAuthorizationOutputCommitDiffOrSource()
        {
            var fixture = CreateSuccessfulFixture();

            RunAsync(() => fixture.Controller.SelectRepositoryAsync(CancellationToken.None));
            RunAsync(() => fixture.Controller.AnalyzeAsync(CancellationToken.None));
            RunAsync(() => fixture.Controller.SaveSessionAsync(CancellationToken.None));
            RunAsync(() => fixture.Controller.SyncNowAsync(CancellationToken.None));

            AssertLogsAreSafe(fixture.Logger.Messages, fixture.RepositoryPath);
        }

        [Test]
        public void StateTransitions_CoverSelectAnalyzeReviewSaveSync()
        {
            var fixture = CreateSuccessfulFixture();
            Assert.AreEqual(GitAnalysisFlowState.Idle, fixture.Controller.State);

            RunAsync(() => fixture.Controller.SelectRepositoryAsync(CancellationToken.None));
            Assert.AreEqual(GitAnalysisFlowState.Selected, fixture.Controller.State);

            RunAsync(() => fixture.Controller.AnalyzeAsync(CancellationToken.None));
            Assert.AreEqual(GitAnalysisFlowState.ReviewReady, fixture.Controller.State);

            RunAsync(() => fixture.Controller.SaveSessionAsync(CancellationToken.None));
            Assert.AreEqual(GitAnalysisFlowState.Saved, fixture.Controller.State);

            RunAsync(() => fixture.Controller.SyncNowAsync(CancellationToken.None));
            Assert.AreEqual(GitAnalysisFlowState.Synced, fixture.Controller.State);
        }

        private static FlowFixture CreateSuccessfulFixture()
        {
            return CreateFixture(FakeRunner.WithMaliciousAggregateOutput());
        }

        private static FlowFixture CreateFixture(FakeRunner runner)
        {
            var repositoryPath = CreateTempDirectory(RawRepoName);
            var picker = new FakeRepositoryPicker(repositoryPath);
            var repository = new FakeLocalRepository();
            var sync = new FakeSyncService(repository);
            var logger = new CapturingGitAnalysisLogger();
            var sanitizer = new PrivacySanitizer();
            var controller = new GitAnalysisFlowController(
                picker,
                new GitAggregateAnalyzer(runner, sanitizer, logger),
                repository,
                null,
                sanitizer,
                sync,
                logger);

            return new FlowFixture(repositoryPath, controller, repository, sync, logger);
        }

        private static void AssertNoRawGitData(object value, string selectedRepositoryPath)
        {
            Assert.IsFalse(ObjectContainsString(value, selectedRepositoryPath), selectedRepositoryPath);
            Assert.IsFalse(ObjectContainsString(value, RawRepoName), RawRepoName);
            Assert.IsFalse(ObjectContainsString(value, RawFilePath), RawFilePath);
            Assert.IsFalse(ObjectContainsString(value, RawFileName), RawFileName);
            Assert.IsFalse(ObjectContainsString(value, RawBranchName), RawBranchName);
            Assert.IsFalse(ObjectContainsString(value, RawCommitMessage), RawCommitMessage);
            Assert.IsFalse(ObjectContainsString(value, RawDiff), RawDiff);
            Assert.IsFalse(ObjectContainsString(value, RawSourceText), RawSourceText);
            Assert.IsFalse(ObjectContainsString(value, RawToken), RawToken);
            Assert.IsFalse(ObjectContainsString(value, RawAuthorization), RawAuthorization);
            Assert.IsFalse(ObjectContainsString(value, "password"), "password");
        }

        private static void AssertLogsAreSafe(IEnumerable<string> messages, string selectedRepositoryPath)
        {
            var logs = string.Join("\n", messages ?? Enumerable.Empty<string>());
            AssertNoRawGitData(logs, selectedRepositoryPath);
            Assert.IsFalse(logs.Contains("git output"));
            Assert.IsFalse(logs.Contains("diff --numstat"));
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

        private static string CreateTempDirectory(string leafName)
        {
            var parent = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName());
            var directory = Path.Combine(parent, leafName);
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static T RunAsync<T>(System.Func<Task<T>> operation)
        {
            return Task.Run(operation).GetAwaiter().GetResult();
        }

        private sealed class FlowFixture
        {
            public FlowFixture(
                string repositoryPath,
                GitAnalysisFlowController controller,
                FakeLocalRepository repository,
                FakeSyncService sync,
                CapturingGitAnalysisLogger logger)
            {
                RepositoryPath = repositoryPath;
                Controller = controller;
                Repository = repository;
                Sync = sync;
                Logger = logger;
            }

            public string RepositoryPath { get; }
            public GitAnalysisFlowController Controller { get; }
            public FakeLocalRepository Repository { get; }
            public FakeSyncService Sync { get; }
            public CapturingGitAnalysisLogger Logger { get; }
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

        private sealed class FakeLocalRepository : ILocalSaveDataRepository
        {
            private readonly PrivacySanitizer privacySanitizer = new PrivacySanitizer();

            public SaveData Current { get; set; } = SaveData.CreateDefault();
            public int LoadCount { get; private set; }
            public int SaveCount { get; private set; }
            public bool FailSaves { get; set; }
            public bool LastSaveWasPrivacySafe { get; private set; }

            public Task<SaveData> LoadAsync(CancellationToken cancellationToken = default)
            {
                LoadCount++;
                return Task.FromResult(Clone(Current));
            }

            public Task<Result> SaveAsync(SaveData saveData, CancellationToken cancellationToken = default)
            {
                SaveCount++;
                LastSaveWasPrivacySafe = privacySanitizer.ValidateSafeSaveData(saveData).IsSuccess;
                if (FailSaves)
                {
                    return Task.FromResult(Result.Failure("save_failed", "Save failed."));
                }

                Current = Clone(saveData);
                return Task.FromResult(Result.Success());
            }

            private static SaveData Clone(SaveData saveData)
            {
                if (saveData == null)
                {
                    return SaveData.CreateDefault();
                }

                return new SaveData
                {
                    SaveVersion = saveData.SaveVersion,
                    CharacterProfile = Clone(saveData.CharacterProfile),
                    WorkSessionSummaries = new List<AgentWorkSession>(saveData.WorkSessionSummaries),
                    GrowthHistory = new List<CharacterGrowthResult>(saveData.GrowthHistory),
                    ConnectedProjects = new List<ConnectedProject>(saveData.ConnectedProjects),
                    ProviderSettings = new List<ProviderSettings>(saveData.ProviderSettings),
                    SyncState = saveData.SyncState,
                    PrivacyPreferences = saveData.PrivacyPreferences,
                    UserSettings = saveData.UserSettings,
                    DailyProgress = Clone(saveData.DailyProgress),
                    MiniGameHistory = new List<MiniGameSession>(saveData.MiniGameHistory),
                    Achievements = new List<AchievementProgress>(saveData.Achievements)
                };
            }

            private static CharacterProfile Clone(CharacterProfile profile)
            {
                if (profile == null)
                {
                    return new CharacterProfile();
                }

                return new CharacterProfile
                {
                    CharacterId = profile.CharacterId,
                    DisplayName = profile.DisplayName,
                    Level = profile.Level,
                    TotalExp = profile.TotalExp,
                    CurrentEvolutionType = profile.CurrentEvolutionType,
                    Stats = new CharacterStats
                    {
                        Logic = profile.Stats?.Logic ?? 0,
                        Debug = profile.Stats?.Debug ?? 0,
                        Architecture = profile.Stats?.Architecture ?? 0,
                        Design = profile.Stats?.Design ?? 0,
                        Stability = profile.Stats?.Stability ?? 0,
                        Velocity = profile.Stats?.Velocity ?? 0,
                        Creativity = profile.Stats?.Creativity ?? 0,
                        Efficiency = profile.Stats?.Efficiency ?? 0,
                        Stress = profile.Stats?.Stress ?? 0
                    },
                    AppearanceVariant = profile.AppearanceVariant,
                    MoodState = profile.MoodState,
                    UnlockedItems = new List<string>(profile.UnlockedItems)
                };
            }

            private static DailyProgress Clone(DailyProgress progress)
            {
                if (progress == null)
                {
                    return new DailyProgress();
                }

                return new DailyProgress
                {
                    DayKey = progress.DayKey,
                    ExpGainedToday = progress.ExpGainedToday,
                    SessionsConfirmedToday = progress.SessionsConfirmedToday
                };
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

        private sealed class CapturingGitAnalysisLogger : IGitAnalysisLogger
        {
            public List<string> Messages { get; } = new List<string>();

            public void Info(string message)
            {
                Messages.Add(message);
            }

            public void Warning(string message)
            {
                Messages.Add(message);
            }
        }

        private sealed class FakeRunner : IGitCommandRunner
        {
            private readonly Dictionary<string, GitCommandResult> responses;

            private FakeRunner(Dictionary<string, GitCommandResult> responses)
            {
                this.responses = responses;
            }

            public Task<GitCommandResult> RunAsync(string workingDirectory, string arguments, CancellationToken cancellationToken)
            {
                if (responses.TryGetValue(arguments, out var result))
                {
                    return Task.FromResult(result);
                }

                return Task.FromResult(GitCommandResult.Success(string.Empty));
            }

            public static FakeRunner WithMaliciousAggregateOutput()
            {
                return new FakeRunner(BaseResponses(
                    $" M {RawFilePath}\n?? docs/README.md\n?? {RawBranchName}\n",
                    $"9999\t5000\t{RawFilePath}\n{RawAuthorization}\n{RawToken}\n{RawDiff}\n{RawSourceText}\n",
                    "2\t1\tpackage.json\n",
                    $"--TOKENFORGE-COMMIT--\n{RawCommitMessage}\n3\t0\tsrc/foo.test.ts\n--TOKENFORGE-COMMIT--\n7\t2\tREADME.md\n"));
            }

            public static FakeRunner FailOn(string command)
            {
                var responses = BaseResponses(string.Empty, string.Empty, string.Empty, string.Empty);
                responses[command] = GitCommandResult.Failure("git_command_failed", "Git command failed with stderr.");
                return new FakeRunner(responses);
            }

            private static Dictionary<string, GitCommandResult> BaseResponses(string status, string diff, string cachedDiff, string log)
            {
                return new Dictionary<string, GitCommandResult>
                {
                    ["rev-parse --is-inside-work-tree"] = GitCommandResult.Success("true\n"),
                    ["status --porcelain"] = GitCommandResult.Success(status),
                    ["diff --numstat"] = GitCommandResult.Success(diff),
                    ["diff --cached --numstat"] = GitCommandResult.Success(cachedDiff),
                    ["log --since=7.days.ago --numstat --format=--TOKENFORGE-COMMIT-- -n 50"] = GitCommandResult.Success(log)
                };
            }
        }
    }
}
