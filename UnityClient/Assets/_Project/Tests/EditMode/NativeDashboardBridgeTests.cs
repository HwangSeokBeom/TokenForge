using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using TokenForge.Client.Agents;
using TokenForge.Client.Domain;
using TokenForge.Client.Git;
using TokenForge.Client.Persistence;
using TokenForge.Client.Platform;
using TokenForge.Client.Privacy;
using TokenForge.Client.UI;
using UnityEngine;

namespace TokenForge.Client.Tests
{
    public sealed class NativeDashboardBridgeTests
    {
        [Test]
        public void TryParseAction_MapsPrimaryNativeActions()
        {
            Assert.IsTrue(MacNativeDashboardService.TryParseAction("dashboard", out var dashboard));
            Assert.AreEqual(NativeDashboardAction.Dashboard, dashboard.Action);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("connectRepository", out var repository));
            Assert.AreEqual(NativeDashboardAction.ConnectRepository, repository.Action);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("repository.add", out var canonicalRepository));
            Assert.AreEqual(NativeDashboardAction.ConnectRepository, canonicalRepository.Action);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("chooseRepositoryFolder", out var chooseRepository));
            Assert.AreEqual(NativeDashboardAction.ChooseRepositoryFolder, chooseRepository.Action);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("connectCodexAgent", out var codex));
            Assert.AreEqual(NativeDashboardAction.ConnectCodexAgent, codex.Action);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("ai_agents", out var aiAgents));
            Assert.AreEqual(NativeDashboardAction.CodexAgent, aiAgents.Action);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("activity", out var activity));
            Assert.AreEqual(NativeDashboardAction.Activity, activity.Action);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("runAnalysis", out var analysis));
            Assert.AreEqual(NativeDashboardAction.RunAnalysis, analysis.Action);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("analyzeRepository:repo-1", out var analyzeRepository));
            Assert.AreEqual(NativeDashboardAction.AnalyzeRepository, analyzeRepository.Action);
            Assert.AreEqual("repo-1", analyzeRepository.Value);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("repository.analyze:repo-1", out var canonicalAnalyzeRepository));
            Assert.AreEqual(NativeDashboardAction.AnalyzeRepository, canonicalAnalyzeRepository.Action);
            Assert.AreEqual("repo-1", canonicalAnalyzeRepository.Value);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("analyzeAgent:ClaudeCode", out var analyzeAgent));
            Assert.AreEqual(NativeDashboardAction.AnalyzeAgent, analyzeAgent.Action);
            Assert.AreEqual("ClaudeCode", analyzeAgent.Value);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("agent.analyze:claudeCode", out var canonicalAnalyzeAgent));
            Assert.AreEqual(NativeDashboardAction.AnalyzeAgent, canonicalAnalyzeAgent.Action);
            Assert.AreEqual("claudeCode", canonicalAnalyzeAgent.Value);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("approveReview", out var approve));
            Assert.AreEqual(NativeDashboardAction.ApproveReview, approve.Action);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("discardReview", out var discard));
            Assert.AreEqual(NativeDashboardAction.DiscardReview, discard.Action);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("review.viewDetails:review-1", out var detail));
            Assert.AreEqual(NativeDashboardAction.ViewReviewDetails, detail.Action);
            Assert.AreEqual("review-1", detail.Value);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("selectCodexLogFolder", out var codexFolder));
            Assert.AreEqual(NativeDashboardAction.SelectCodexLogFolder, codexFolder.Action);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("selectRepository:repo-1", out var selectRepository));
            Assert.AreEqual(NativeDashboardAction.SelectRepository, selectRepository.Action);
            Assert.AreEqual("repo-1", selectRepository.Value);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("autoDetectAgent:ClaudeCode", out var detectAgent));
            Assert.AreEqual(NativeDashboardAction.AutoDetectAgent, detectAgent.Action);
            Assert.AreEqual("ClaudeCode", detectAgent.Value);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("autoDetectAgent:claudeCode", out var stableDetectAgent));
            Assert.AreEqual(NativeDashboardAction.AutoDetectAgent, stableDetectAgent.Action);
            Assert.AreEqual("claudeCode", stableDetectAgent.Value);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("saveGrowth", out var saveGrowth));
            Assert.AreEqual(NativeDashboardAction.SaveGrowth, saveGrowth.Action);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("review.saveGrowth", out var canonicalSaveGrowth));
            Assert.AreEqual(NativeDashboardAction.SaveGrowth, canonicalSaveGrowth.Action);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("changeCompanionSkin:orange_cat", out var skin));
            Assert.AreEqual(NativeDashboardAction.ChangeCompanionSkin, skin.Action);
            Assert.AreEqual("orange_cat", skin.Value);
        }

        [Test]
        public void TryParseAction_PreservesTogglePayload()
        {
            Assert.IsTrue(MacNativeDashboardService.TryParseAction("toggleCompanionVisible:false", out var request));
            Assert.AreEqual(NativeDashboardAction.ToggleCompanionVisible, request.Action);
            Assert.AreEqual("false", request.Value);
            Assert.IsFalse(request.BoolValue(true));
        }

        [Test]
        public void TryParseAction_RejectsUnknownActionWithoutThrowing()
        {
            Assert.IsTrue(MacNativeDashboardService.TryParseAction("unknown_action", out var request));
            Assert.AreEqual(NativeDashboardAction.Unsupported, request.Action);
        }

        [Test]
        public void NativeDashboardState_DefaultSerializesMvpFields()
        {
            var state = NativeDashboardState.CreateDefault();
            var json = state.ToJson();

            StringAssert.Contains("appTitle", json);
            StringAssert.Contains("syncStatusText", json);
            StringAssert.Contains("companionVisible", json);
            StringAssert.Contains("repository", json);
            StringAssert.Contains("agentProviders", json);
            StringAssert.Contains("repositories", json);
            StringAssert.Contains("review", json);
            StringAssert.Contains("No Agents", json);
            Assert.IsFalse(json.Contains("Cdx 0%"));
        }

        [Test]
        public void NativeDashboardState_ReviewDetailsProjectionIsExplicit()
        {
            var state = NativeDashboardState.CreateDefault();
            state.review.pending = true;
            state.review.reviewId = "review-detail";
            state.review.selectedReviewId = "review-detail";
            state.review.detailVisible = true;
            state.review.source = "repository";
            state.review.repositoryName = "Repository 1";
            state.review.providerName = "Codex";
            state.review.confidence = "High";
            state.review.estimatedXpDelta = 193;
            state.review.status = "pending";
            state.review.generatedAt = "2026-05-26 10:00:00";

            var json = state.ToJson();

            StringAssert.Contains("review-detail", json);
            StringAssert.Contains("detailVisible", json);
            StringAssert.Contains("Raw prompt, code, file content, and command logs are not stored.", json);
        }

        [Test]
        public void GitRepositoryValidatorRejectsPlainFolder()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName());
            Directory.CreateDirectory(directory);

            var result = RunAsync(() => GitRepositoryPathValidator.ToPickerResultAsync(directory));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("NotAGitRepository", result.ErrorCode);
            Assert.AreEqual("This folder is not a Git repository.", result.ErrorMessage);
        }

        private static T RunAsync<T>(Func<Task<T>> operation)
        {
            return Task.Run(operation).GetAwaiter().GetResult();
        }

        [Test]
        public void ApprovePendingNativeReviewAppliesXpAndStats()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName());
            var saveRepository = new SaveDataRepository(directory);
            var saveData = SaveData.CreateDefault();
            var session = new AgentWorkSession
            {
                SessionId = "pending-session",
                WorkType = WorkType.Feature,
                GitChangeSummary = new GitChangeSummary { ProjectPathHash = "repo-hash", ChangedFileCountBucket = CountBucket.Small }
            };
            saveData.SelectedRepositoryHash = "repo-hash";
            saveData.PendingNativeActivityReview = new PendingNativeActivityReview
            {
                SourceKind = "repository",
                RepositoryHash = "repo-hash",
                SafeSummary = "Repository aggregate activity ready for review.",
                EstimatedXpDelta = 120,
                StatDeltas = new CharacterStats { Logic = 3, Debug = 2 },
                SafeSession = session,
                GrowthResult = new CharacterGrowthResult
                {
                    SessionId = session.SessionId,
                    ExpGained = 120,
                    LevelBefore = 1,
                    LevelAfter = 1,
                    StatDeltas = new CharacterStats { Logic = 3, Debug = 2 }
                }
            };
            saveRepository.SaveAsync(saveData).GetAwaiter().GetResult();
            var viewModel = CreateNativeReviewViewModel(saveRepository);

            var result = viewModel.ApprovePendingNativeReviewAsync().GetAwaiter().GetResult();
            var loaded = saveRepository.LoadAsync().GetAwaiter().GetResult();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(120, loaded.CharacterProfile.TotalExp);
            Assert.AreEqual(3, loaded.CharacterProfile.Stats.Logic);
            Assert.AreEqual(2, loaded.CharacterProfile.Stats.Debug);
            Assert.IsNull(loaded.PendingNativeActivityReview);
            Assert.AreEqual(1, loaded.WorkSessionSummaries.Count);
        }

        [Test]
        public void DiscardPendingNativeReviewDoesNotApplyXpOrStats()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName());
            var saveRepository = new SaveDataRepository(directory);
            var saveData = SaveData.CreateDefault();
            saveData.PendingNativeActivityReview = new PendingNativeActivityReview
            {
                SafeSummary = "Aggregate activity ready for review.",
                EstimatedXpDelta = 200,
                StatDeltas = new CharacterStats { Logic = 5 },
                SafeSession = new AgentWorkSession { SessionId = "discard-session" },
                GrowthResult = new CharacterGrowthResult { SessionId = "discard-session", ExpGained = 200, StatDeltas = new CharacterStats { Logic = 5 } }
            };
            saveRepository.SaveAsync(saveData).GetAwaiter().GetResult();
            var viewModel = CreateNativeReviewViewModel(saveRepository);

            var result = viewModel.DiscardPendingNativeReviewAsync().GetAwaiter().GetResult();
            var loaded = saveRepository.LoadAsync().GetAwaiter().GetResult();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0, loaded.CharacterProfile.TotalExp);
            Assert.AreEqual(0, loaded.CharacterProfile.Stats.Logic);
            Assert.IsNull(loaded.PendingNativeActivityReview);
            Assert.AreEqual(0, loaded.WorkSessionSummaries.Count);
        }

        [Test]
        public void RunRepositoryAnalysisWithoutSelectionReturnsVisibleSafeFailure()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName());
            var saveRepository = new SaveDataRepository(directory);
            var viewModel = CreateNativeReviewViewModel(saveRepository);

            var result = viewModel.AnalyzeGitActivityAsync().GetAwaiter().GetResult();
            var loaded = saveRepository.LoadAsync().GetAwaiter().GetResult();

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("NoActiveRepository", result.ErrorCode);
            Assert.IsNull(loaded.PendingNativeActivityReview);
            Assert.AreEqual(0, loaded.CharacterProfile.TotalExp);
        }

        [Test]
        public void NativeDashboardInvariantClearsActionsWhenNoPendingReview()
        {
            var state = NativeDashboardState.CreateDefault();
            state.pendingReviewCount = 1;
            state.actionStatusKind = "success";
            state.actionStatusText = "Analysis complete. Pending review is ready in Activity. XP is unchanged until Save Growth.";
            state.review.pending = false;
            state.review.summary = "No pending review";
            state.review.canSaveGrowth = true;
            state.review.canDiscard = true;
            state.review.canViewDetails = true;
            state.activity.state = "Pending review ready";

            InvokeNativeInvariant(state);

            Assert.AreEqual(0, state.pendingReviewCount);
            Assert.IsFalse(state.review.canSaveGrowth);
            Assert.IsFalse(state.review.canDiscard);
            Assert.IsFalse(state.review.canViewDetails);
            Assert.AreEqual("No pending review", state.review.summary);
            Assert.AreNotEqual("Pending review ready", state.activity.state);
            Assert.AreNotEqual("Analysis complete. Pending review is ready in Activity. XP is unchanged until Save Growth.", state.actionStatusText);
        }

        [Test]
        public void NativeDashboardState_DefaultDoesNotContainSampleProductionActivity()
        {
            var json = NativeDashboardState.CreateDefault().ToJson();

            Assert.IsFalse(json.Contains("+101 XP"));
            Assert.IsFalse(json.Contains("category Test"));
            Assert.IsFalse(json.Contains("sessions One"));
            Assert.IsFalse(json.Contains("interactions Large"));
            Assert.IsFalse(json.Contains("confidence Medium"));
            Assert.IsFalse(json.Contains("warnings 2"));
        }

        [Test]
        public void ApprovePendingNativeReviewIsIdempotentByReviewId()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName());
            var saveRepository = new SaveDataRepository(directory);
            var session = new AgentWorkSession
            {
                SessionId = "idempotent-session",
                WorkType = WorkType.Feature,
                GitChangeSummary = new GitChangeSummary { ProjectPathHash = "repo-hash", ChangedFileCountBucket = CountBucket.Small }
            };
            var pending = new PendingNativeActivityReview
            {
                ReviewId = "review-idempotent",
                SourceKind = "repository",
                RepositoryHash = "repo-hash",
                SafeSummary = "Repository aggregate activity ready for review.",
                EstimatedXpDelta = 90,
                StatDeltas = new CharacterStats { Logic = 2 },
                SafeSession = session,
                GrowthResult = new CharacterGrowthResult
                {
                    SessionId = session.SessionId,
                    ExpGained = 90,
                    LevelBefore = 1,
                    LevelAfter = 1,
                    StatDeltas = new CharacterStats { Logic = 2 }
                }
            };
            var saveData = SaveData.CreateDefault();
            saveData.SelectedRepositoryHash = "repo-hash";
            saveData.PendingNativeActivityReview = pending;
            saveRepository.SaveAsync(saveData).GetAwaiter().GetResult();
            var viewModel = CreateNativeReviewViewModel(saveRepository);

            var first = viewModel.ApprovePendingNativeReviewAsync().GetAwaiter().GetResult();
            var afterFirst = saveRepository.LoadAsync().GetAwaiter().GetResult();
            afterFirst.PendingNativeActivityReview = pending;
            saveRepository.SaveAsync(afterFirst).GetAwaiter().GetResult();
            var second = viewModel.ApprovePendingNativeReviewAsync().GetAwaiter().GetResult();
            var loaded = saveRepository.LoadAsync().GetAwaiter().GetResult();

            Assert.IsTrue(first.IsSuccess);
            Assert.IsTrue(second.IsSuccess);
            Assert.AreEqual(90, loaded.CharacterProfile.TotalExp);
            Assert.AreEqual(1, loaded.WorkSessionSummaries.Count);
            Assert.That(loaded.AppliedNativeReviewIds, Does.Contain("review-idempotent"));
        }

        [Test]
        public void ApproveCombinedNativeReviewAppliesGitAndAgentSessionsOnce()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName());
            var saveRepository = new SaveDataRepository(directory);
            var gitSession = new AgentWorkSession
            {
                SessionId = "combined-git",
                SourceProvider = "GIT",
                WorkType = WorkType.Feature,
                GitChangeSummary = new GitChangeSummary { ProjectPathHash = "repo-hash", ChangedFileCountBucket = CountBucket.Small }
            };
            var agentSession = new AgentWorkSession
            {
                SessionId = "combined-agent",
                SourceProvider = "CODEX",
                WorkType = WorkType.Mixed,
                GitChangeSummary = new GitChangeSummary { ProjectPathHash = "repo-hash" },
                AgentActivitySummary = new AgentActivitySummary { ProviderType = AgentProviderType.Codex, SessionCountBucket = CountBucket.One }
            };
            var saveData = SaveData.CreateDefault();
            saveData.SelectedRepositoryHash = "repo-hash";
            saveData.PendingNativeActivityReview = new PendingNativeActivityReview
            {
                ReviewId = "combined-review",
                SourceKind = "combined",
                RepositoryHash = "repo-hash",
                SafeSummary = "Repository and Codex aggregate activity ready for review.",
                EstimatedXpDelta = 100,
                StatDeltas = new CharacterStats { Logic = 2, Debug = 1 },
                SafeSession = gitSession,
                SafeSessions = new System.Collections.Generic.List<AgentWorkSession> { gitSession, agentSession },
                GrowthResult = new CharacterGrowthResult { SessionId = "combined-git", ExpGained = 40, StatDeltas = new CharacterStats { Logic = 2 } },
                GrowthResults = new System.Collections.Generic.List<CharacterGrowthResult>
                {
                    new CharacterGrowthResult { SessionId = "combined-git", ExpGained = 40, StatDeltas = new CharacterStats { Logic = 2 } },
                    new CharacterGrowthResult { SessionId = "combined-agent", ExpGained = 60, StatDeltas = new CharacterStats { Debug = 1 } }
                }
            };
            saveRepository.SaveAsync(saveData).GetAwaiter().GetResult();
            var viewModel = CreateNativeReviewViewModel(saveRepository);

            var first = viewModel.ApprovePendingNativeReviewAsync().GetAwaiter().GetResult();
            var afterFirst = saveRepository.LoadAsync().GetAwaiter().GetResult();
            afterFirst.PendingNativeActivityReview = saveData.PendingNativeActivityReview;
            saveRepository.SaveAsync(afterFirst).GetAwaiter().GetResult();
            var second = viewModel.ApprovePendingNativeReviewAsync().GetAwaiter().GetResult();
            var loaded = saveRepository.LoadAsync().GetAwaiter().GetResult();

            Assert.IsTrue(first.IsSuccess);
            Assert.IsTrue(second.IsSuccess);
            Assert.AreEqual(100, loaded.CharacterProfile.TotalExp);
            Assert.AreEqual(2, loaded.WorkSessionSummaries.Count);
            Assert.That(loaded.AppliedNativeReviewIds, Does.Contain("combined-review"));
        }

        [Test]
        public void NativeDashboardState_ModelsMultiRepositoryAndMultiAgentState()
        {
            var state = NativeDashboardState.CreateDefault();
            state.repositories = new[]
            {
                new NativeRepositoryListItem { id = "repo-a", name = "Repository 1", companion = "Egg · Lv 1", selected = true },
                new NativeRepositoryListItem { id = "repo-b", name = "Repository 2", companion = "Baby · Lv 2" }
            };
            state.agentProviders = new[]
            {
                new NativeAgentProviderState { id = "Codex", displayName = "Codex", supportedStatus = "auto_detect_supported" },
                new NativeAgentProviderState { id = "ClaudeCode", displayName = "Claude Code", supportedStatus = "auto_detect_supported" },
                new NativeAgentProviderState { id = "Cursor", displayName = "Cursor", supportedStatus = "auto_detect_supported" },
                new NativeAgentProviderState { id = "GitHubCopilot", displayName = "GitHub Copilot", supportedStatus = "partially_supported" },
                new NativeAgentProviderState { id = "GeminiCli", displayName = "Gemini CLI", supportedStatus = "auto_detect_supported" },
                new NativeAgentProviderState { id = "Manual", displayName = "Other / Manual Log", supportedStatus = "manual_folder_required" }
            };

            var json = state.ToJson();

            StringAssert.Contains("Repository 1", json);
            StringAssert.Contains("Claude Code", json);
            StringAssert.Contains("Gemini CLI", json);
            StringAssert.Contains("manual_folder_required", json);
        }

        [Test]
        public void DesktopCompanionOverlayDefinesLightCardTextHelpers()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "Plugins/macOS/DesktopCompanionOverlay.mm"));

            StringAssert.Contains("TokenForgeLightCardPrimaryTextColor", source);
            StringAssert.Contains("TokenForgeLightCardSecondaryTextColor", source);
            StringAssert.Contains("TokenForgeDisabledTextColor", source);
            StringAssert.Contains("TokenForgeLightCardTitleLabel", source);
            StringAssert.Contains("TokenForgeSecondaryButton", source);
            StringAssert.Contains("TokenForgeShellHeaderLabel", source);
            StringAssert.Contains("repositoryScreen", source);
            StringAssert.Contains("agentProviderRow", source);
            StringAssert.Contains("activityScreenWithActivity", source);
            StringAssert.Contains("TokenForge Settings", source);
            StringAssert.Contains("repository.add", source);
            StringAssert.Contains("repository.analyze", source);
            StringAssert.Contains("review.saveGrowth", source);
            StringAssert.Contains("review.viewDetails", source);
            StringAssert.Contains("agent.autoDetect", source);
            StringAssert.Contains("No Agents", source);
        }

        [Test]
        public void AppBootstrapperDefinesNativeOnlyUiModeAndStaleUnityDashboardRemoval()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/AppBootstrapper.cs"));

            StringAssert.Contains("[Bootstrap] uiMode=nativeAppKit", source);
            StringAssert.Contains("[Bootstrap] uiMode=unityFallback", source);
            StringAssert.Contains("[Bootstrap] removedStaleUnityDashboardRoot count=", source);
            StringAssert.Contains("RemoveStaleUnityDashboardRoots", source);
        }

        private static ApprovedActivityAnalysisViewModel CreateNativeReviewViewModel(SaveDataRepository saveRepository)
        {
            var sanitizer = new PrivacySanitizer();
            var gitFlow = new GitAnalysisFlowController(
                new TestRepositoryPicker(),
                new GitAggregateAnalyzer(null, sanitizer),
                saveRepository,
                null,
                sanitizer);
            var agentFlow = new AgentAnalysisFlowController(
                new AgentLogActivityProvider(new AgentActivityAnalyzer(null, null, sanitizer)),
                saveRepository,
                null,
                sanitizer);
            return new ApprovedActivityAnalysisViewModel(
                gitFlow,
                agentFlow,
                new TestAgentLogLocationPicker(),
                saveRepository,
                sanitizer,
                new ApprovedLocationSettingsRepository(Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName())));
        }

        private static void InvokeNativeInvariant(NativeDashboardState state)
        {
            typeof(TokenForge.Client.AppBootstrapper)
                .GetMethod("EnforceNativeDashboardInvariants", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { state });
        }

        private sealed class TestRepositoryPicker : IRepositoryPicker
        {
            public Task<RepositoryPickerResult> PickRepositoryAsync(System.Threading.CancellationToken cancellationToken = default)
            {
                return Task.FromResult(RepositoryPickerResult.Cancelled());
            }
        }

        private sealed class TestAgentLogLocationPicker : IAgentLogLocationPicker
        {
            public Task<AgentLogLocationPickerResult> PickAgentLogLocationAsync(System.Threading.CancellationToken cancellationToken = default)
            {
                return Task.FromResult(AgentLogLocationPickerResult.Cancelled());
            }
        }
    }
}
