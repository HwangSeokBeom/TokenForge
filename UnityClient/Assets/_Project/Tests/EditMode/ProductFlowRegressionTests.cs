using System;
using System.Collections;
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
using UnityEngine.TestTools;

namespace TokenForge.Client.Tests
{
    public sealed class ProductFlowRegressionTests
    {
        [UnityTest]
        public IEnumerator RunRepositoryAnalysisDoesNotBlockMainThread()
        {
            var fixture = CreateGitFixture(new DelayedGitRunner(TimeSpan.FromMilliseconds(250)));
            var selectTask = fixture.Controller.SelectLocalOnlyApprovedRepositoryPathAsyncForTest(fixture.RepositoryPath);
            yield return WaitForTask(selectTask, "SelectLocalOnlyApprovedRepositoryPathAsyncForTest");

            var stopwatch = Stopwatch.StartNew();
            var task = fixture.Controller.AnalyzeAsync(CancellationToken.None);
            var elapsed = stopwatch.ElapsedMilliseconds;

            Assert.Less(elapsed, 100);
            Assert.AreEqual(GitAnalysisFlowState.Analyzing, fixture.Controller.State);
            Assert.IsFalse(task.IsCompleted);
            yield return WaitForTask(task, nameof(fixture.Controller.AnalyzeAsync));
            Assert.IsTrue(task.Result.IsSuccess);
        }

        [UnityTest]
        public IEnumerator RunRepositoryAnalysisCreatesPendingReview()
        {
            var fixture = CreateViewModelFixture(new SafeGitRunner());
            var selectTask = fixture.ViewModel.GitFlow.SelectLocalOnlyApprovedRepositoryPathAsyncForTest(fixture.RepositoryPath);
            yield return WaitForTask(selectTask, "SelectLocalOnlyApprovedRepositoryPathAsyncForTest");

            var resultTask = fixture.ViewModel.AnalyzeGitActivityAsync();
            yield return WaitForTask(resultTask, nameof(fixture.ViewModel.AnalyzeGitActivityAsync));
            var savedTask = fixture.SaveRepository.LoadAsync();
            yield return WaitForTask(savedTask, nameof(fixture.SaveRepository.LoadAsync));
            var result = resultTask.Result;
            var saved = savedTask.Result;

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            Assert.IsNotNull(saved.PendingNativeActivityReview);
            Assert.AreEqual("repository", saved.PendingNativeActivityReview.SourceKind);
            Assert.AreEqual(0, saved.CharacterProfile.TotalExp);
        }

        [UnityTest]
        public IEnumerator RecentFailedRunStoresOnlySafeSummary()
        {
            var fixture = CreateViewModelFixture(new SafeGitRunner());

            var recordTask = fixture.ViewModel.RecordNativeAnalysisRunAsync(
                "repository",
                "failed",
                "RepositoryFolderNotFound",
                "Repository folder not found. Reconnect required.");
            yield return WaitForTask(recordTask, nameof(fixture.ViewModel.RecordNativeAnalysisRunAsync));
            var savedTask = fixture.SaveRepository.LoadAsync();
            yield return WaitForTask(savedTask, nameof(fixture.SaveRepository.LoadAsync));
            var record = recordTask.Result;
            var saved = savedTask.Result;

            Assert.IsTrue(record.IsSuccess, record.ErrorMessage);
            Assert.AreEqual(1, saved.RecentNativeAnalysisRuns.Count);
            Assert.AreEqual("RepositoryFolderNotFound", saved.RecentNativeAnalysisRuns[0].ErrorCode);
            Assert.IsTrue(new PrivacySanitizer().ValidateSafeSaveData(saved).IsSuccess);
            Assert.AreEqual(0, saved.CharacterProfile.TotalExp);
        }

        [UnityTest]
        public IEnumerator GitAnalysisTimeoutProducesFailureState()
        {
            var fixture = CreateGitFixture(new TimeoutGitRunner());
            var selectTask = fixture.Controller.SelectLocalOnlyApprovedRepositoryPathAsyncForTest(fixture.RepositoryPath);
            yield return WaitForTask(selectTask, "SelectLocalOnlyApprovedRepositoryPathAsyncForTest");

            var resultTask = fixture.Controller.AnalyzeAsync(CancellationToken.None);
            yield return WaitForTask(resultTask, nameof(fixture.Controller.AnalyzeAsync));
            var result = resultTask.Result;

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("ProcessTimeout", result.ErrorCode);
            Assert.AreEqual(GitAnalysisFlowState.Failed, fixture.Controller.State);
            Assert.AreEqual("Git command timed out.", fixture.Controller.UserMessage);
        }

        [UnityTest]
        public IEnumerator GitAggregateAnalyzerUsesValidLogFormatArgument()
        {
            var runner = new RecordingGitRunner();
            var analyzer = new GitAggregateAnalyzer(runner, new PrivacySanitizer());
            var repositoryPath = CreateGitLikeDirectory();

            var resultTask = analyzer.AnalyzeAsync(new GitRepositoryAnalysisInput { RepositoryRootPath = repositoryPath });
            yield return WaitForTask(resultTask, nameof(analyzer.AnalyzeAsync));
            var result = resultTask.Result;

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            Assert.That(runner.Arguments, Has.Some.Contains("log --all --numstat"));
            var numstatLog = runner.Arguments.First(item => item.StartsWith("log --all --numstat", StringComparison.Ordinal));
            Assert.That(numstatLog, Does.Contain("--format=format:--TOKENFORGE-COMMIT--"));
            Assert.That(numstatLog, Does.Not.Contain("--format=--TOKENFORGE-COMMIT--"));
        }

        [UnityTest]
        public IEnumerator GitCommandRunnerReturnsPathNotFoundBeforeLaunchingGit()
        {
            var missing = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Guid.NewGuid().ToString("N"), "missing");

            var resultTask = new SystemGitCommandRunner(TimeSpan.FromMilliseconds(100))
                .RunAsync(missing, "rev-parse --show-toplevel", CancellationToken.None);
            yield return WaitForTask(resultTask, nameof(SystemGitCommandRunner.RunAsync));
            var result = resultTask.Result;

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("RepositoryFolderNotFound", result.ErrorCode);
        }

        [Test]
        public void NativeActionRunRepositoryAnalysisRoutesToFlow()
        {
            Assert.IsTrue(MacNativeDashboardService.TryParseAction("runRepositoryAnalysis", out var request));
            Assert.AreEqual(NativeDashboardAction.RunRepositoryAnalysis, request.Action);
        }

        [UnityTest]
        public IEnumerator CodexProviderConnectDetectAnalyzeDisconnectFlow()
        {
            var fixture = CreateViewModelFixture(new SafeGitRunner(), CreateCodexLogDirectory());

            var detectTask = fixture.ViewModel.DetectAgentSourceForOnboardingAsync(ConnectedAgentSourceType.Codex);
            yield return WaitForTask(detectTask, nameof(fixture.ViewModel.DetectAgentSourceForOnboardingAsync));
            var detect = detectTask.Result;
            Assert.IsFalse(detect.IsSuccess);
            var source = fixture.ViewModel.Onboarding.AgentSources.First(item => item.SourceType == ConnectedAgentSourceType.Codex);
            Assert.AreEqual(AgentSourceSetupState.ManualImportRequired, source.State);

            var chooseTask = fixture.ViewModel.SelectManualAgentLogForOnboardingAsync(ConnectedAgentSourceType.Codex);
            yield return WaitForTask(chooseTask, nameof(fixture.ViewModel.SelectManualAgentLogForOnboardingAsync));
            var choose = chooseTask.Result;
            Assert.IsTrue(choose.IsSuccess, choose.ErrorMessage);
            Assert.AreEqual(AgentSourceSetupState.ReadyToAnalyze, source.State);

            var analyzeTask = fixture.ViewModel.AnalyzeAgentSourceForOnboardingAsync(ConnectedAgentSourceType.Codex);
            yield return WaitForTask(analyzeTask, nameof(fixture.ViewModel.AnalyzeAgentSourceForOnboardingAsync));
            var savedTask = fixture.SaveRepository.LoadAsync();
            yield return WaitForTask(savedTask, nameof(fixture.SaveRepository.LoadAsync));
            var analyze = analyzeTask.Result;
            var saved = savedTask.Result;
            Assert.IsTrue(analyze.IsSuccess, analyze.ErrorMessage);
            Assert.IsNotNull(saved.PendingNativeActivityReview);
            Assert.AreEqual("aiAgent", saved.PendingNativeActivityReview.SourceKind);
            Assert.AreEqual("CODEX", fixture.ViewModel.AgentFlow.PendingSessionForLocalOnlyApproval.SourceProvider);

            var disconnectTask = fixture.ViewModel.DisconnectAgentSourceForOnboardingAsync(ConnectedAgentSourceType.Codex);
            yield return WaitForTask(disconnectTask, nameof(fixture.ViewModel.DisconnectAgentSourceForOnboardingAsync));
            var afterDisconnectTask = fixture.SaveRepository.LoadAsync();
            yield return WaitForTask(afterDisconnectTask, nameof(fixture.SaveRepository.LoadAsync));
            var disconnect = disconnectTask.Result;
            var afterDisconnect = afterDisconnectTask.Result;
            Assert.IsTrue(disconnect.IsSuccess, disconnect.ErrorMessage);
            Assert.IsFalse(source.Selected);
            Assert.IsFalse(afterDisconnect.ProviderSettings.Any(item => item.ProviderId == AgentProviderType.Codex.ToString()));
        }

        [UnityTest]
        public IEnumerator AgentAnalyzeWithoutVerifiedSourceDoesNotCreatePendingReview()
        {
            var fixture = CreateViewModelFixture(new SafeGitRunner());

            var analyzeTask = fixture.ViewModel.AnalyzeAgentSourceForOnboardingAsync(ConnectedAgentSourceType.ClaudeCode);
            yield return WaitForTask(analyzeTask, nameof(fixture.ViewModel.AnalyzeAgentSourceForOnboardingAsync));
            var savedTask = fixture.SaveRepository.LoadAsync();
            yield return WaitForTask(savedTask, nameof(fixture.SaveRepository.LoadAsync));
            var analyze = analyzeTask.Result;
            var saved = savedTask.Result;
            var source = fixture.ViewModel.Onboarding.AgentSources.First(item => item.SourceType == ConnectedAgentSourceType.ClaudeCode);

            Assert.IsFalse(analyze.IsSuccess);
            Assert.AreEqual("agent_source_not_ready", analyze.ErrorCode);
            Assert.IsNull(saved.PendingNativeActivityReview);
            Assert.AreEqual(0, saved.CharacterProfile.TotalExp);
            Assert.IsFalse(source.Selected && source.State == AgentSourceSetupState.ReadyToAnalyze);
        }

        [UnityTest]
        public IEnumerator AutoDetectFindsCandidateButDoesNotConnectUntilApproved()
        {
            var saveRepository = new SaveDataRepository(Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName()));
            var approvedRepository = new ApprovedLocationSettingsRepository(Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName()));
            var candidatePath = CreateCodexLogDirectory();
            var fixture = CreateViewModelFixture(new SafeGitRunner(), string.Empty, saveRepository, approvedRepository, providerType => new CandidateAgentSourceDetector(providerType, candidatePath));

            var detectTask = fixture.ViewModel.DetectAgentSourceForOnboardingAsync(ConnectedAgentSourceType.Codex);
            yield return WaitForTask(detectTask, nameof(fixture.ViewModel.DetectAgentSourceForOnboardingAsync));
            var source = fixture.ViewModel.Onboarding.AgentSources.First(item => item.SourceType == ConnectedAgentSourceType.Codex);
            var afterDetectTask = fixture.SaveRepository.LoadAsync();
            yield return WaitForTask(afterDetectTask, nameof(fixture.SaveRepository.LoadAsync));
            var detect = detectTask.Result;
            var afterDetect = afterDetectTask.Result;

            Assert.IsTrue(detect.IsSuccess, detect.ErrorMessage);
            Assert.AreEqual(AgentSourceSetupState.LocalSourceDetected, source.State);
            Assert.IsFalse(source.Selected);
            Assert.IsFalse(afterDetect.ProviderSettings.First(item => item.ProviderId == "Codex").Selected);

            var approveTask = fixture.ViewModel.ApproveDetectedAgentSourceForOnboardingAsync(ConnectedAgentSourceType.Codex);
            yield return WaitForTask(approveTask, nameof(fixture.ViewModel.ApproveDetectedAgentSourceForOnboardingAsync));
            var approve = approveTask.Result;
            Assert.IsTrue(approve.IsSuccess, approve.ErrorMessage);
            Assert.IsTrue(source.Selected);
            Assert.AreEqual(AgentSourceSetupState.ReadyToAnalyze, source.State);
        }

        [UnityTest]
        public IEnumerator RestoredProviderWithoutSourceHashIsNotConnectedOrReady()
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
            var saveTask = saveRepository.SaveAsync(saveData);
            yield return WaitForTask(saveTask, nameof(saveRepository.SaveAsync));
            var fixture = CreateViewModelFixture(new SafeGitRunner(), string.Empty, saveRepository, approvedRepository);

            var restoreTask = fixture.ViewModel.RestoreLocalSelectionsFromApprovedLocationsAsync();
            yield return WaitForTask(restoreTask, nameof(fixture.ViewModel.RestoreLocalSelectionsFromApprovedLocationsAsync));
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
            StringAssert.Contains("Run Repository Analysis", nativeSource);
            StringAssert.Contains("runAnalysis:", nativeSource);
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

        [UnityTest]
        public IEnumerator RepositoryAndAgentCombinedReviewDoesNotOverwrite()
        {
            var fixture = CreateViewModelFixture(new SafeGitRunner(), CreateCodexLogDirectory());
            var selectTask = fixture.ViewModel.GitFlow.SelectLocalOnlyApprovedRepositoryPathAsyncForTest(fixture.RepositoryPath);
            yield return WaitForTask(selectTask, "SelectLocalOnlyApprovedRepositoryPathAsyncForTest");
            var analyzeGitTask = fixture.ViewModel.AnalyzeGitActivityAsync();
            yield return WaitForTask(analyzeGitTask, nameof(fixture.ViewModel.AnalyzeGitActivityAsync));
            Assert.IsTrue(analyzeGitTask.Result.IsSuccess);
            fixture.ViewModel.SelectedAgentProviderType = AgentProviderType.Codex;
            Assert.IsTrue(fixture.ViewModel.AgentFlow.SelectApprovedLogLocation(new AgentAnalysisInput
            {
                SelectedLocationPath = fixture.AgentLogPath,
                ProviderHint = AgentProviderType.Codex,
                SourceKind = AgentSourceKind.ManualFolder,
                SafeSourceAlias = "Codex local activity"
            }).IsSuccess);
            var analyzeAgentTask = fixture.ViewModel.AnalyzeAgentActivityAsync();
            yield return WaitForTask(analyzeAgentTask, nameof(fixture.ViewModel.AnalyzeAgentActivityAsync));
            Assert.IsTrue(analyzeAgentTask.Result.IsSuccess);

            var combinedTask = fixture.ViewModel.CombinePendingNativeReviewsFromFlowsAsync();
            yield return WaitForTask(combinedTask, nameof(fixture.ViewModel.CombinePendingNativeReviewsFromFlowsAsync));
            var combined = combinedTask.Result;

            Assert.IsTrue(combined.IsSuccess, combined.ErrorMessage);
            Assert.AreEqual(2, combined.Value.SafeSessions.Count);
            Assert.That(combined.Value.SafeSessions.Select(session => session.SourceProvider), Does.Contain("GIT"));
            Assert.That(combined.Value.SafeSessions.Select(session => session.SourceProvider), Does.Contain("CODEX"));
        }

        [UnityTest]
        public IEnumerator DisconnectAgentClearsStateAndPersistence()
        {
            var fixture = CreateViewModelFixture(new SafeGitRunner(), CreateCodexLogDirectory());
            var selectTask = fixture.ViewModel.SelectManualAgentLogForOnboardingAsync(ConnectedAgentSourceType.Codex);
            yield return WaitForTask(selectTask, nameof(fixture.ViewModel.SelectManualAgentLogForOnboardingAsync));
            Assert.IsTrue(selectTask.Result.IsSuccess);
            var beforeTask = fixture.SaveRepository.LoadAsync();
            yield return WaitForTask(beforeTask, nameof(fixture.SaveRepository.LoadAsync));
            var before = beforeTask.Result;
            Assert.That(before.ProviderSettings, Has.Some.Matches<ProviderSettings>(item => item.ProviderId == "Codex" && item.Selected));

            var disconnectTask = fixture.ViewModel.DisconnectAgentSourceForOnboardingAsync(ConnectedAgentSourceType.Codex);
            yield return WaitForTask(disconnectTask, nameof(fixture.ViewModel.DisconnectAgentSourceForOnboardingAsync));
            var afterTask = fixture.SaveRepository.LoadAsync();
            yield return WaitForTask(afterTask, nameof(fixture.SaveRepository.LoadAsync));
            var disconnect = disconnectTask.Result;
            var after = afterTask.Result;
            var reloaded = CreateViewModelFixture(new SafeGitRunner(), fixture.AgentLogPath, fixture.SaveRepository, fixture.ApprovedLocationRepository);
            var restoreTask = reloaded.ViewModel.RestoreLocalSelectionsFromApprovedLocationsAsync();
            yield return WaitForTask(restoreTask, nameof(reloaded.ViewModel.RestoreLocalSelectionsFromApprovedLocationsAsync));
            var source = reloaded.ViewModel.Onboarding.AgentSources.First(item => item.SourceType == ConnectedAgentSourceType.Codex);

            Assert.IsTrue(disconnect.IsSuccess, disconnect.ErrorMessage);
            Assert.IsFalse(after.ProviderSettings.Any(item => item.ProviderId == "Codex"));
            Assert.IsFalse(source.Selected);
        }

        [UnityTest]
        public IEnumerator AddRepositoryPersistsAcrossDashboardNavigationAndRelaunchRestore()
        {
            var saveRepository = new SaveDataRepository(Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName()));
            var approvedRepository = new ApprovedLocationSettingsRepository(Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName()));
            var fixture = CreateViewModelFixture(new SafeGitRunner(), string.Empty, saveRepository, approvedRepository);

            var addTask = fixture.ViewModel.SelectLocalGitRepositoryForOnboardingAsync();
            yield return WaitForTask(addTask, nameof(fixture.ViewModel.SelectLocalGitRepositoryForOnboardingAsync));
            var refreshTask = fixture.ViewModel.RefreshDashboardAsync();
            yield return WaitForTask(refreshTask, nameof(fixture.ViewModel.RefreshDashboardAsync));
            var afterHomeNavigationTask = saveRepository.LoadAsync();
            yield return WaitForTask(afterHomeNavigationTask, nameof(saveRepository.LoadAsync));
            var add = addTask.Result;
            var afterHomeNavigation = afterHomeNavigationTask.Result;
            var activeRepository = afterHomeNavigation.SelectedRepositoryHash;
            var reloaded = CreateViewModelFixture(new SafeGitRunner(), string.Empty, saveRepository, approvedRepository);
            var relaunchRefreshTask = reloaded.ViewModel.RefreshDashboardAsync();
            yield return WaitForTask(relaunchRefreshTask, nameof(reloaded.ViewModel.RefreshDashboardAsync));
            var afterRelaunchTask = saveRepository.LoadAsync();
            yield return WaitForTask(afterRelaunchTask, nameof(saveRepository.LoadAsync));
            var afterRelaunch = afterRelaunchTask.Result;

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

                if (arguments == "rev-list --max-parents=0 --all --reverse")
                {
                    return Task.FromResult(GitCommandResult.Success("FIRSTSHA\n"));
                }

                if (arguments == "show -s --format=%cI FIRSTSHA")
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
