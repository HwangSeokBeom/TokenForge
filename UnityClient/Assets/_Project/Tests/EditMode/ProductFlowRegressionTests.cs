using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using UnityEngine;

namespace TokenForge.Client.Tests
{
    public sealed class ProductFlowRegressionTests
    {
        [Test]
        public void RunRepositoryAnalysisDoesNotBlockMainThread()
        {
            var fixture = CreateGitFixture(new DelayedGitRunner(TimeSpan.FromMilliseconds(250)));
            RunAsync(() => fixture.Controller.SelectLocalOnlyApprovedRepositoryPathAsyncForTest(fixture.RepositoryPath));

            var stopwatch = Stopwatch.StartNew();
            var task = fixture.Controller.AnalyzeAsync(CancellationToken.None);
            var elapsed = stopwatch.ElapsedMilliseconds;

            Assert.Less(elapsed, 100);
            Assert.AreEqual(GitAnalysisFlowState.Analyzing, fixture.Controller.State);
            Assert.IsFalse(task.IsCompleted);
            Assert.IsTrue(RunAsync(() => task).IsSuccess);
        }

        [Test]
        public void RunRepositoryAnalysisCreatesPendingReview()
        {
            var fixture = CreateViewModelFixture(new SafeGitRunner());
            RunAsync(() => fixture.ViewModel.GitFlow.SelectLocalOnlyApprovedRepositoryPathAsyncForTest(fixture.RepositoryPath));

            var result = RunAsync(() => fixture.ViewModel.AnalyzeGitActivityAsync());
            var saved = RunAsync(() => fixture.SaveRepository.LoadAsync());

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            Assert.IsNotNull(saved.PendingNativeActivityReview);
            Assert.AreEqual("repository", saved.PendingNativeActivityReview.SourceKind);
            Assert.AreEqual(0, saved.CharacterProfile.TotalExp);
        }

        [Test]
        public void RecentFailedRunStoresOnlySafeSummary()
        {
            var fixture = CreateViewModelFixture(new SafeGitRunner());

            var record = RunAsync(() => fixture.ViewModel.RecordNativeAnalysisRunAsync(
                "repository",
                "failed",
                "RepositoryFolderNotFound",
                "Repository folder not found. Reconnect required."));
            var saved = RunAsync(() => fixture.SaveRepository.LoadAsync());

            Assert.IsTrue(record.IsSuccess, record.ErrorMessage);
            Assert.AreEqual(1, saved.RecentNativeAnalysisRuns.Count);
            Assert.AreEqual("RepositoryFolderNotFound", saved.RecentNativeAnalysisRuns[0].ErrorCode);
            Assert.IsTrue(new PrivacySanitizer().ValidateSafeSaveData(saved).IsSuccess);
            Assert.AreEqual(0, saved.CharacterProfile.TotalExp);
        }

        [Test]
        public void GitAnalysisTimeoutProducesFailureState()
        {
            var fixture = CreateGitFixture(new TimeoutGitRunner());
            RunAsync(() => fixture.Controller.SelectLocalOnlyApprovedRepositoryPathAsyncForTest(fixture.RepositoryPath));

            var result = RunAsync(() => fixture.Controller.AnalyzeAsync(CancellationToken.None));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("ProcessTimeout", result.ErrorCode);
            Assert.AreEqual(GitAnalysisFlowState.Failed, fixture.Controller.State);
            Assert.AreEqual("Git command timed out.", fixture.Controller.UserMessage);
        }

        [Test]
        public void GitAggregateAnalyzerUsesValidLogFormatArgument()
        {
            var runner = new RecordingGitRunner();
            var analyzer = new GitAggregateAnalyzer(runner, new PrivacySanitizer());
            var repositoryPath = CreateGitLikeDirectory();

            var result = RunAsync(() => analyzer.AnalyzeAsync(new GitRepositoryAnalysisInput { RepositoryRootPath = repositoryPath }));

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            Assert.That(runner.Arguments, Has.Some.Contains("log --all --numstat"));
            var numstatLog = runner.Arguments.First(item => item.Contains("--numstat"));
            Assert.That(numstatLog, Does.Contain("--format=--TOKENFORGE-COMMIT--"));
            Assert.That(numstatLog, Does.Not.Contain("--format=format:"));
        }

        [Test]
        public void GitCommandRunnerReturnsPathNotFoundBeforeLaunchingGit()
        {
            var missing = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Guid.NewGuid().ToString("N"), "missing");

            var result = RunAsync(() => new SystemGitCommandRunner(TimeSpan.FromMilliseconds(100))
                .RunAsync(missing, "rev-parse --show-toplevel", CancellationToken.None));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("RepositoryFolderNotFound", result.ErrorCode);
        }

        [Test]
        public void NativeActionRunRepositoryAnalysisRoutesToFlow()
        {
            Assert.IsTrue(MacNativeDashboardService.TryParseAction("runRepositoryAnalysis", out var request));
            Assert.AreEqual(NativeDashboardAction.RunRepositoryAnalysis, request.Action);
        }

        [Test]
        public void CodexProviderConnectDetectAnalyzeDisconnectFlow()
        {
            var fixture = CreateViewModelFixture(new SafeGitRunner(), CreateCodexLogDirectory());

            var detect = RunAsync(() => fixture.ViewModel.DetectAgentSourceForOnboardingAsync(ConnectedAgentSourceType.Codex));
            Assert.IsFalse(detect.IsSuccess);
            var source = fixture.ViewModel.Onboarding.AgentSources.First(item => item.SourceType == ConnectedAgentSourceType.Codex);
            Assert.AreEqual(AgentSourceSetupState.ManualImportRequired, source.State);

            var choose = RunAsync(() => fixture.ViewModel.SelectManualAgentLogForOnboardingAsync(ConnectedAgentSourceType.Codex));
            Assert.IsTrue(choose.IsSuccess, choose.ErrorMessage);
            Assert.AreEqual(AgentSourceSetupState.ReadyToAnalyze, source.State);

            var analyze = RunAsync(() => fixture.ViewModel.AnalyzeAgentSourceForOnboardingAsync(ConnectedAgentSourceType.Codex));
            var saved = RunAsync(() => fixture.SaveRepository.LoadAsync());
            Assert.IsTrue(analyze.IsSuccess, analyze.ErrorMessage);
            Assert.IsNotNull(saved.PendingNativeActivityReview);
            Assert.AreEqual("Codex", saved.PendingNativeActivityReview.SourceKind);
            Assert.AreEqual("CODEX", fixture.ViewModel.AgentFlow.PendingSessionForLocalOnlyApproval.SourceProvider);

            var disconnect = RunAsync(() => fixture.ViewModel.DisconnectAgentSourceForOnboardingAsync(ConnectedAgentSourceType.Codex));
            var afterDisconnect = RunAsync(() => fixture.SaveRepository.LoadAsync());
            Assert.IsTrue(disconnect.IsSuccess, disconnect.ErrorMessage);
            Assert.IsFalse(source.Selected);
            Assert.IsFalse(afterDisconnect.ProviderSettings.Any(item => item.ProviderId == AgentProviderType.Codex.ToString()));
        }

        [Test]
        public void AgentAnalyzeWithoutVerifiedSourceDoesNotCreatePendingReview()
        {
            var fixture = CreateViewModelFixture(new SafeGitRunner());

            var analyze = RunAsync(() => fixture.ViewModel.AnalyzeAgentSourceForOnboardingAsync(ConnectedAgentSourceType.ClaudeCode));
            var saved = RunAsync(() => fixture.SaveRepository.LoadAsync());
            var source = fixture.ViewModel.Onboarding.AgentSources.First(item => item.SourceType == ConnectedAgentSourceType.ClaudeCode);

            Assert.IsFalse(analyze.IsSuccess);
            Assert.AreEqual("agent_source_not_ready", analyze.ErrorCode);
            Assert.IsNull(saved.PendingNativeActivityReview);
            Assert.AreEqual(0, saved.CharacterProfile.TotalExp);
            Assert.IsFalse(source.Selected && source.State == AgentSourceSetupState.ReadyToAnalyze);
        }

        [Test]
        public void AutoDetectFindsCandidateButDoesNotConnectUntilApproved()
        {
            var saveRepository = new SaveDataRepository(Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName()));
            var approvedRepository = new ApprovedLocationSettingsRepository(Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName()));
            var candidatePath = CreateCodexLogDirectory();
            var fixture = CreateViewModelFixture(new SafeGitRunner(), string.Empty, saveRepository, approvedRepository, providerType => new CandidateAgentSourceDetector(providerType, candidatePath));

            var detect = RunAsync(() => fixture.ViewModel.DetectAgentSourceForOnboardingAsync(ConnectedAgentSourceType.Codex));
            var source = fixture.ViewModel.Onboarding.AgentSources.First(item => item.SourceType == ConnectedAgentSourceType.Codex);
            var afterDetect = RunAsync(() => fixture.SaveRepository.LoadAsync());

            Assert.IsTrue(detect.IsSuccess, detect.ErrorMessage);
            Assert.AreEqual(AgentSourceSetupState.LocalSourceDetected, source.State);
            Assert.IsFalse(source.Selected);
            Assert.IsFalse(afterDetect.ProviderSettings.First(item => item.ProviderId == "Codex").Selected);

            var approve = RunAsync(() => fixture.ViewModel.ApproveDetectedAgentSourceForOnboardingAsync(ConnectedAgentSourceType.Codex));
            Assert.IsTrue(approve.IsSuccess, approve.ErrorMessage);
            Assert.IsTrue(source.Selected);
            Assert.AreEqual(AgentSourceSetupState.ReadyToAnalyze, source.State);
        }

        [Test]
        public void RestoredProviderWithoutSourceHashIsNotConnectedOrReady()
        {
            var saveRepository = new SaveDataRepository(Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName()));
            var approvedRepository = new ApprovedLocationSettingsRepository(Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName()));
            var saveData = SaveData.CreateDefault();
            saveData.ProviderSettings.Add(new ProviderSettings
            {
                ProviderId = AgentProviderType.ClaudeCode.ToString(),
                Enabled = true,
                Selected = true,
                ConnectionState = AgentSourceSetupState.ReadyToAnalyze.ToString(),
                SafeLocationHash = string.Empty
            });
            RunAsync(() => saveRepository.SaveAsync(saveData));
            var fixture = CreateViewModelFixture(new SafeGitRunner(), string.Empty, saveRepository, approvedRepository);

            RunAsync(() => fixture.ViewModel.RestoreLocalSelectionsFromApprovedLocationsAsync());
            var source = fixture.ViewModel.Onboarding.AgentSources.First(item => item.SourceType == ConnectedAgentSourceType.ClaudeCode);

            Assert.IsFalse(source.Selected);
            Assert.AreEqual(AgentSourceSetupState.NotSelected, source.State);
            Assert.IsTrue(string.IsNullOrWhiteSpace(source.SafeLocationHash));
        }

        [Test]
        public void UnknownProviderActionShowsUnsupportedReason()
        {
            Assert.IsTrue(MacNativeDashboardService.TryParseAction("analyzeAgent:not-a-provider", out var request));
            Assert.AreEqual(NativeDashboardAction.AnalyzeAgent, request.Action);
            Assert.AreEqual(AgentProviderType.Unknown, MacAgentSourceDetector.NormalizeProviderValue(request.Value));

            var source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/AppBootstrapper.cs"));
            StringAssert.Contains("unsupported_provider_action", source);
            StringAssert.Contains("Unsupported AI provider", source);
        }

        [Test]
        public void AgentActionMappingCoversAllVisibleButtons()
        {
            var nativeSource = File.ReadAllText(Path.Combine(Application.dataPath, "Plugins/macOS/DesktopCompanionOverlay.mm"));
            var actions = new[]
            {
                "connectRepository",
                "chooseRepositoryFolder",
                "runRepositoryAnalysis",
                "connectAiAgent",
                "connectAiAgent:codex",
                "autoDetectAgent:codex",
                "analyzeAgent:codex",
                "disconnectAgent:codex",
                "chooseAgentFolder:codex",
                "runAgentAnalysis",
                "saveGrowth",
                "levelUp",
                "discardReview",
                "safeSync"
            };

            StringAssert.Contains("connectAgentAction", nativeSource);
            StringAssert.Contains("runAgentAnalysis", nativeSource);
            StringAssert.Contains("connectRepository", nativeSource);
            StringAssert.Contains("runRepositoryAnalysis", nativeSource);
            StringAssert.Contains("autoDetectAgent", nativeSource);
            StringAssert.Contains("chooseAgentFolder", nativeSource);
            StringAssert.Contains("saveGrowth", nativeSource);
            foreach (var action in actions)
            {
                Assert.IsTrue(MacNativeDashboardService.TryParseAction(action, out _), action);
            }
        }

        [Test]
        public void SafeSyncPayloadDoesNotContainRawFields()
        {
            var saveData = SaveData.CreateDefault();
            saveData.WorkSessionSummaries.Add(AgentLogActivityProvider.CreateSession(new AgentActivitySummary
            {
                ProviderType = AgentProviderType.Codex,
                SourceIdentifierHash = "safe-source-hash",
                DayBucket = "2026-05-26",
                SessionCountBucket = CountBucket.One,
                InteractionCountBucket = CountBucket.Small,
                ConfidenceLevel = ConfidenceLevel.High,
                AnalyzerVersion = CodexAgentLogParser.Version
            }));

            var payload = new SafeSyncMapper().ToPayload(saveData);

            Assert.AreEqual(1, payload.SessionSummary.Sessions.Count);
            Assert.AreEqual("CODEX", payload.SessionSummary.Sessions[0].SourceProvider);
            SerializerFreePrivacyDtoAssert.DoesNotContainForbiddenMembersOrValues(payload);
        }

        [Test]
        public void RepositoryAndAgentCombinedReviewDoesNotOverwrite()
        {
            var fixture = CreateViewModelFixture(new SafeGitRunner(), CreateCodexLogDirectory());
            RunAsync(() => fixture.ViewModel.GitFlow.SelectLocalOnlyApprovedRepositoryPathAsyncForTest(fixture.RepositoryPath));
            Assert.IsTrue(RunAsync(() => fixture.ViewModel.AnalyzeGitActivityAsync()).IsSuccess);
            fixture.ViewModel.SelectedAgentProviderType = AgentProviderType.Codex;
            Assert.IsTrue(fixture.ViewModel.AgentFlow.SelectApprovedLogLocation(new AgentAnalysisInput
            {
                SelectedLocationPath = fixture.AgentLogPath,
                ProviderHint = AgentProviderType.Codex,
                SourceKind = AgentSourceKind.ManualFolder,
                SafeSourceAlias = "Codex local activity"
            }).IsSuccess);
            Assert.IsTrue(RunAsync(() => fixture.ViewModel.AnalyzeAgentActivityAsync()).IsSuccess);

            var combined = RunAsync(() => fixture.ViewModel.CombinePendingNativeReviewsFromFlowsAsync());

            Assert.IsTrue(combined.IsSuccess, combined.ErrorMessage);
            Assert.AreEqual(2, combined.Value.SafeSessions.Count);
            Assert.That(combined.Value.SafeSessions.Select(session => session.SourceProvider), Does.Contain("GIT"));
            Assert.That(combined.Value.SafeSessions.Select(session => session.SourceProvider), Does.Contain("CODEX"));
        }

        [Test]
        public void DisconnectAgentClearsStateAndPersistence()
        {
            var fixture = CreateViewModelFixture(new SafeGitRunner(), CreateCodexLogDirectory());
            Assert.IsTrue(RunAsync(() => fixture.ViewModel.SelectManualAgentLogForOnboardingAsync(ConnectedAgentSourceType.Codex)).IsSuccess);
            var before = RunAsync(() => fixture.SaveRepository.LoadAsync());
            Assert.That(before.ProviderSettings, Has.Some.Matches<ProviderSettings>(item => item.ProviderId == "Codex" && item.Selected));

            var disconnect = RunAsync(() => fixture.ViewModel.DisconnectAgentSourceForOnboardingAsync(ConnectedAgentSourceType.Codex));
            var after = RunAsync(() => fixture.SaveRepository.LoadAsync());
            var reloaded = CreateViewModelFixture(new SafeGitRunner(), fixture.AgentLogPath, fixture.SaveRepository, fixture.ApprovedLocationRepository);
            RunAsync(() => reloaded.ViewModel.RestoreLocalSelectionsFromApprovedLocationsAsync());
            var source = reloaded.ViewModel.Onboarding.AgentSources.First(item => item.SourceType == ConnectedAgentSourceType.Codex);

            Assert.IsTrue(disconnect.IsSuccess, disconnect.ErrorMessage);
            Assert.IsFalse(after.ProviderSettings.Any(item => item.ProviderId == "Codex"));
            Assert.IsFalse(source.Selected);
        }

        [Test]
        public void AddRepositoryPersistsAcrossDashboardNavigationAndRelaunchRestore()
        {
            var saveRepository = new SaveDataRepository(Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName()));
            var approvedRepository = new ApprovedLocationSettingsRepository(Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName()));
            var fixture = CreateViewModelFixture(new SafeGitRunner(), string.Empty, saveRepository, approvedRepository);

            var add = RunAsync(() => fixture.ViewModel.SelectLocalGitRepositoryForOnboardingAsync());
            RunAsync(() => fixture.ViewModel.RefreshDashboardAsync());
            var afterHomeNavigation = RunAsync(() => saveRepository.LoadAsync());
            var activeRepository = afterHomeNavigation.SelectedRepositoryHash;
            var reloaded = CreateViewModelFixture(new SafeGitRunner(), string.Empty, saveRepository, approvedRepository);
            RunAsync(() => reloaded.ViewModel.RefreshDashboardAsync());
            var afterRelaunch = RunAsync(() => saveRepository.LoadAsync());

            Assert.IsTrue(add.IsSuccess, add.ErrorMessage);
            Assert.IsNotEmpty(activeRepository);
            Assert.AreEqual(activeRepository, afterRelaunch.SelectedRepositoryHash);
            Assert.AreEqual(1, afterRelaunch.ConnectedProjects.Count(project => !project.IsArchived));
            Assert.IsTrue(afterRelaunch.ConnectedProjects.Single(project => !project.IsArchived).IsActive);
            Assert.AreEqual(1, reloaded.ViewModel.RepositoryCompanions.Count);
            Assert.AreEqual(activeRepository, reloaded.ViewModel.CharacterDashboard.CurrentRepositoryHash);
        }

        [Test]
        public void NativeDashboardSourceRepairsCollapsedFramesAndSuppressesNoRepositoryCompanion()
        {
            var nativeSource = File.ReadAllText(Path.Combine(Application.dataPath, "Plugins/macOS/DesktopCompanionOverlay.mm"));

            StringAssert.Contains("TokenForgeNormalizeDashboardFrame", nativeSource);
            StringAssert.Contains("[DashboardLifecycle][FRAME_RESTORE]", nativeSource);
            StringAssert.Contains("splitAlignment=height", nativeSource);
            StringAssert.Contains("placeholderCompanion=false", nativeSource);
            StringAssert.Contains("No repository connected", nativeSource);
        }

        private static ViewModelFixture CreateViewModelFixture(IGitCommandRunner gitRunner, string agentLogPath = "")
        {
            return CreateViewModelFixture(
                gitRunner,
                agentLogPath,
                new SaveDataRepository(Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName())),
                new ApprovedLocationSettingsRepository(Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName())));
        }

        private static ViewModelFixture CreateViewModelFixture(
            IGitCommandRunner gitRunner,
            string agentLogPath,
            SaveDataRepository saveRepository,
            ApprovedLocationSettingsRepository approvedLocationRepository)
        {
            return CreateViewModelFixture(gitRunner, agentLogPath, saveRepository, approvedLocationRepository, providerType => new EmptyAgentSourceDetector(providerType));
        }

        private static ViewModelFixture CreateViewModelFixture(
            IGitCommandRunner gitRunner,
            string agentLogPath,
            SaveDataRepository saveRepository,
            ApprovedLocationSettingsRepository approvedLocationRepository,
            Func<AgentProviderType, IAgentSourceDetector> detectorFactory)
        {
            var sanitizer = new PrivacySanitizer();
            var git = CreateGitFixture(gitRunner, saveRepository);
            var agentFlow = new AgentAnalysisFlowController(
                new AgentLogActivityProvider(new AgentActivityAnalyzer(new FileAgentLogSourceReader(), null, sanitizer)),
                saveRepository,
                null,
                sanitizer);
            var viewModel = new ApprovedActivityAnalysisViewModel(
                git.Controller,
                agentFlow,
                new TestAgentLogLocationPicker(agentLogPath),
                saveRepository,
                sanitizer,
                approvedLocationRepository,
                null,
                null,
                detectorFactory);
            return new ViewModelFixture(viewModel, saveRepository, approvedLocationRepository, git.RepositoryPath, agentLogPath);
        }

        private static GitFixture CreateGitFixture(IGitCommandRunner runner, SaveDataRepository saveRepository = null)
        {
            var repositoryPath = CreateGitLikeDirectory();
            var sanitizer = new PrivacySanitizer();
            var controller = new GitAnalysisFlowController(
                new TestRepositoryPicker(repositoryPath),
                new GitAggregateAnalyzer(runner, sanitizer),
                saveRepository ?? new SaveDataRepository(Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName())),
                null,
                sanitizer);
            return new GitFixture(repositoryPath, controller);
        }

        private static string CreateGitLikeDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName());
            Directory.CreateDirectory(Path.Combine(directory, ".git"));
            return directory;
        }

        private static string CreateCodexLogDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName());
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory, "codex.jsonl"),
                "{\"provider\":\"codex\",\"timestamp\":\"2026-05-26T01:00:00Z\",\"session_id\":\"codex-safe\",\"type\":\"message\"}\n" +
                "{\"provider\":\"codex\",\"timestamp\":\"2026-05-26T01:01:00Z\",\"tool\":\"apply_patch\",\"language\":\"cs\"}\n");
            return directory;
        }

        private static void RunAsync(Func<Task> operation)
        {
            operation().GetAwaiter().GetResult();
        }

        private static T RunAsync<T>(Func<Task<T>> operation)
        {
            return operation().GetAwaiter().GetResult();
        }

        private sealed class GitFixture
        {
            public GitFixture(string repositoryPath, GitAnalysisFlowController controller)
            {
                RepositoryPath = repositoryPath;
                Controller = controller;
            }

            public string RepositoryPath { get; }
            public GitAnalysisFlowController Controller { get; }
        }

        private sealed class ViewModelFixture
        {
            public ViewModelFixture(
                ApprovedActivityAnalysisViewModel viewModel,
                SaveDataRepository saveRepository,
                ApprovedLocationSettingsRepository approvedLocationRepository,
                string repositoryPath,
                string agentLogPath)
            {
                ViewModel = viewModel;
                SaveRepository = saveRepository;
                ApprovedLocationRepository = approvedLocationRepository;
                RepositoryPath = repositoryPath;
                AgentLogPath = agentLogPath;
            }

            public ApprovedActivityAnalysisViewModel ViewModel { get; }
            public SaveDataRepository SaveRepository { get; }
            public ApprovedLocationSettingsRepository ApprovedLocationRepository { get; }
            public string RepositoryPath { get; }
            public string AgentLogPath { get; }
        }

        private sealed class TestRepositoryPicker : IRepositoryPicker
        {
            private readonly string path;

            public TestRepositoryPicker(string path)
            {
                this.path = path;
            }

            public Task<RepositoryPickerResult> PickRepositoryAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(RepositoryPickerResult.Selected(path));
            }
        }

        private sealed class TestAgentLogLocationPicker : IAgentLogLocationPicker
        {
            private readonly string path;

            public TestAgentLogLocationPicker(string path)
            {
                this.path = path;
            }

            public Task<AgentLogLocationPickerResult> PickAgentLogLocationAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(string.IsNullOrWhiteSpace(path)
                    ? AgentLogLocationPickerResult.Cancelled()
                    : AgentLogLocationPickerResult.Selected(path));
            }
        }

        private sealed class SafeGitRunner : IGitCommandRunner
        {
            public Task<GitCommandResult> RunAsync(string workingDirectory, string arguments, CancellationToken cancellationToken)
            {
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

                if (arguments.Contains("rev-parse"))
                {
                    return Task.FromResult(GitCommandResult.Success("true\n"));
                }

                if (arguments.Contains("status --porcelain"))
                {
                    return Task.FromResult(GitCommandResult.Success(" M Assets/_Project/Scripts/UI/View.cs\n"));
                }

                if (arguments.Contains("diff --numstat"))
                {
                    return Task.FromResult(GitCommandResult.Success("12\t3\tAssets/_Project/Scripts/UI/View.cs\n"));
                }

                if (arguments.Contains("log "))
                {
                    return Task.FromResult(GitCommandResult.Success("--TOKENFORGE-COMMIT--\n4\t1\tAssets/_Project/Tests/EditMode/ViewTests.cs\n"));
                }

                return Task.FromResult(GitCommandResult.Success(string.Empty));
            }
        }

        private sealed class DelayedGitRunner : IGitCommandRunner
        {
            private readonly TimeSpan delay;
            private readonly SafeGitRunner inner = new SafeGitRunner();

            public DelayedGitRunner(TimeSpan delay)
            {
                this.delay = delay;
            }

            public async Task<GitCommandResult> RunAsync(string workingDirectory, string arguments, CancellationToken cancellationToken)
            {
                await Task.Delay(delay, cancellationToken);
                return await inner.RunAsync(workingDirectory, arguments, cancellationToken);
            }
        }

        private sealed class TimeoutGitRunner : IGitCommandRunner
        {
            public Task<GitCommandResult> RunAsync(string workingDirectory, string arguments, CancellationToken cancellationToken)
            {
                return Task.FromResult(arguments.Contains("rev-parse")
                    ? GitCommandResult.Success("true\n")
                    : GitCommandResult.Failure("ProcessTimeout", "Git command timed out."));
            }
        }

        private sealed class RecordingGitRunner : IGitCommandRunner
        {
            public List<string> Arguments { get; } = new List<string>();

            public Task<GitCommandResult> RunAsync(string workingDirectory, string arguments, CancellationToken cancellationToken)
            {
                Arguments.Add(arguments);
                if (arguments.Contains("rev-parse"))
                {
                    return Task.FromResult(GitCommandResult.Success("true\n"));
                }

                if (arguments.StartsWith("log ", StringComparison.Ordinal))
                {
                    return Task.FromResult(GitCommandResult.Success("--TOKENFORGE-COMMIT--\n1\t0\tAssets/_Project/Scripts/AppBootstrapper.cs\n"));
                }

                return Task.FromResult(GitCommandResult.Success(string.Empty));
            }
        }

        private sealed class EmptyAgentSourceDetector : IAgentSourceDetector
        {
            public EmptyAgentSourceDetector(AgentProviderType providerType)
            {
                ProviderType = providerType;
            }

            public AgentProviderType ProviderType { get; }

            public Task<AgentSourceDetectionResult> DetectAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(new AgentSourceDetectionResult
                {
                    ProviderType = ProviderType,
                    AccessState = AgentSourceAccessState.NotDetected,
                    WarningIds = new List<string> { "agent_source_not_detected" }
                });
            }
        }

        private sealed class CandidateAgentSourceDetector : IAgentSourceDetector
        {
            private readonly string path;

            public CandidateAgentSourceDetector(AgentProviderType providerType, string path)
            {
                ProviderType = providerType;
                this.path = path;
            }

            public AgentProviderType ProviderType { get; }

            public Task<AgentSourceDetectionResult> DetectAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(new AgentSourceDetectionResult
                {
                    ProviderType = ProviderType,
                    AccessState = AgentSourceAccessState.Detected,
                    Candidates = new List<AgentSourceCandidate>
                    {
                        new AgentSourceCandidate
                        {
                            LocalPath = path,
                            SafeAlias = "Codex local activity",
                            SourceKind = AgentSourceKind.DetectedLocal,
                            AccessState = AgentSourceAccessState.Detected,
                            Confidence = ConfidenceLevel.High
                        }
                    }
                });
            }
        }
    }

    internal static class GitAnalysisFlowControllerTestExtensions
    {
        public static Task<Result> SelectLocalOnlyApprovedRepositoryPathAsyncForTest(this GitAnalysisFlowController controller, string path)
        {
            return controller.SelectLocalOnlyApprovedRepositoryPathAsync(path);
        }
    }
}
