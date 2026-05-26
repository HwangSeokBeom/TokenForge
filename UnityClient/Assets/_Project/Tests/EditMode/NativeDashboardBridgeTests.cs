using NUnit.Framework;
using System.IO;
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

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("connectCodexAgent", out var codex));
            Assert.AreEqual(NativeDashboardAction.ConnectCodexAgent, codex.Action);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("runAnalysis", out var analysis));
            Assert.AreEqual(NativeDashboardAction.RunAnalysis, analysis.Action);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("approveReview", out var approve));
            Assert.AreEqual(NativeDashboardAction.ApproveReview, approve.Action);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("discardReview", out var discard));
            Assert.AreEqual(NativeDashboardAction.DiscardReview, discard.Action);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("selectCodexLogFolder", out var codexFolder));
            Assert.AreEqual(NativeDashboardAction.SelectCodexLogFolder, codexFolder.Action);

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
            Assert.IsFalse(MacNativeDashboardService.TryParseAction("unknown_action", out var request));
            Assert.IsNull(request);
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
            StringAssert.Contains("review", json);
        }

        [Test]
        public void GitRepositoryValidatorRejectsPlainFolder()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName());
            Directory.CreateDirectory(directory);

            var result = GitRepositoryPathValidator.ToPickerResult(directory);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("not_git_repository", result.ErrorCode);
            Assert.AreEqual("This folder is not a Git repository.", result.ErrorMessage);
        }

        [Test]
        public async Task ApprovePendingNativeReviewAppliesXpAndStats()
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
            await saveRepository.SaveAsync(saveData);
            var viewModel = CreateNativeReviewViewModel(saveRepository);

            var result = await viewModel.ApprovePendingNativeReviewAsync();
            var loaded = await saveRepository.LoadAsync();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(120, loaded.CharacterProfile.TotalExp);
            Assert.AreEqual(3, loaded.CharacterProfile.Stats.Logic);
            Assert.AreEqual(2, loaded.CharacterProfile.Stats.Debug);
            Assert.IsNull(loaded.PendingNativeActivityReview);
            Assert.AreEqual(1, loaded.WorkSessionSummaries.Count);
        }

        [Test]
        public async Task DiscardPendingNativeReviewDoesNotApplyXpOrStats()
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
            await saveRepository.SaveAsync(saveData);
            var viewModel = CreateNativeReviewViewModel(saveRepository);

            var result = await viewModel.DiscardPendingNativeReviewAsync();
            var loaded = await saveRepository.LoadAsync();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0, loaded.CharacterProfile.TotalExp);
            Assert.AreEqual(0, loaded.CharacterProfile.Stats.Logic);
            Assert.IsNull(loaded.PendingNativeActivityReview);
            Assert.AreEqual(0, loaded.WorkSessionSummaries.Count);
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
