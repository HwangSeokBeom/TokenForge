using NUnit.Framework;
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TokenForge.Client.Agents;
using TokenForge.Client.Common;
using TokenForge.Client.Domain;
using TokenForge.Client.Git;
using TokenForge.Client.Persistence;
using TokenForge.Client.Platform;
using TokenForge.Client.Privacy;
using TokenForge.Client.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace TokenForge.Client.Tests
{
    public sealed class NativeDashboardBridgeTests
    {
        [SetUp]
        public void SetUp()
        {
            ResetNativeDashboardBridgeStaticState();
        }

        [TearDown]
        public void TearDown()
        {
            ResetNativeDashboardBridgeStaticState();
        }

        [Test]
        public void NativeDashboardBridgeNUnitDiscoverySmoke()
        {
            Debug.Log("PHASE NUnit discovery smoke");
            Assert.Pass();
        }

        [UnityTest]
        public IEnumerator NativeDashboardBridgeUnityTestDiscoverySmoke()
        {
            Debug.Log("PHASE UnityTest discovery smoke");
            yield return null;
            Assert.Pass();
        }

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

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("open_active_companion_dashboard", out var openActiveCompanion));
            Assert.AreEqual(NativeDashboardAction.OpenActiveCompanionDashboard, openActiveCompanion.Action);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("open_repository_companion_dashboard:repo-1", out var openRepositoryCompanion));
            Assert.AreEqual(NativeDashboardAction.OpenRepositoryCompanionDashboard, openRepositoryCompanion.Action);
            Assert.AreEqual("repo-1", openRepositoryCompanion.Value);

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

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("tokenShop", out var tokenShop));
            Assert.AreEqual(NativeDashboardAction.TokenShop, tokenShop.Action);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("shop.purchase:skin_white_cat", out var shopPurchase));
            Assert.AreEqual(NativeDashboardAction.PurchaseTokenShopItem, shopPurchase.Action);
            Assert.AreEqual("skin_white_cat", shopPurchase.Value);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("shop.target:repository", out var shopRepositoryTarget));
            Assert.AreEqual(NativeDashboardAction.SelectShopRepositoryTarget, shopRepositoryTarget.Action);
            Assert.AreEqual("repository", shopRepositoryTarget.Value);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("shop.target.agent:codex", out var shopAgentTarget));
            Assert.AreEqual(NativeDashboardAction.SelectShopAgentTarget, shopAgentTarget.Action);
            Assert.AreEqual("codex", shopAgentTarget.Value);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("shop.mode:agents", out var shopAgentMode));
            Assert.AreEqual(NativeDashboardAction.SelectShopAgentTarget, shopAgentMode.Action);
            Assert.AreEqual("agents", shopAgentMode.Value);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("shop.category:effects", out var shopCategory));
            Assert.AreEqual(NativeDashboardAction.SelectShopCategory, shopCategory.Action);
            Assert.AreEqual("effects", shopCategory.Value);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("shop.equip:skin_white_cat", out var shopEquip));
            Assert.AreEqual(NativeDashboardAction.EquipTokenShopItem, shopEquip.Action);
            Assert.AreEqual("skin_white_cat", shopEquip.Value);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("shop.preview:zodiac_dragon", out var shopPreview));
            Assert.AreEqual(NativeDashboardAction.PreviewTokenShopItem, shopPreview.Action);
            Assert.AreEqual("zodiac_dragon", shopPreview.Value);

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
        public void TryParseAction_MapsCanonicalCompanionRuntimeActions()
        {
            Assert.IsTrue(MacNativeDashboardService.TryParseAction("show_companion", out var show));
            Assert.AreEqual(NativeDashboardAction.ShowCompanion, show.Action);
            Assert.IsTrue(show.BoolValue(false));

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("showOnDesktop:trace-1", out var explicitShow));
            Assert.AreEqual(NativeDashboardAction.ShowCompanion, explicitShow.Action);
            Assert.AreEqual("trace-1", explicitShow.TraceId);
            Assert.IsTrue(explicitShow.BoolValue(false));

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("hide_companion", out var hide));
            Assert.AreEqual(NativeDashboardAction.HideCompanion, hide.Action);
            Assert.IsFalse(hide.BoolValue(true));

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("hideFromDesktop:trace-2", out var explicitHide));
            Assert.AreEqual(NativeDashboardAction.HideCompanion, explicitHide.Action);
            Assert.AreEqual("trace-2", explicitHide.TraceId);
            Assert.IsFalse(explicitHide.BoolValue(true));

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("enable_wander", out var enableWander));
            Assert.AreEqual(NativeDashboardAction.EnableWander, enableWander.Action);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("disable_wander", out var disableWander));
            Assert.AreEqual(NativeDashboardAction.DisableWander, disableWander.Action);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("enable_click", out var enableClick));
            Assert.AreEqual(NativeDashboardAction.EnableClick, enableClick.Action);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("disable_click", out var disableClick));
            Assert.AreEqual(NativeDashboardAction.DisableClick, disableClick.Action);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("set_companion_click_through_enabled:true", out var clickThrough));
            Assert.AreEqual(NativeDashboardAction.SetClickThroughEnabled, clickThrough.Action);
            Assert.IsTrue(clickThrough.BoolValue(false));

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("reset_companion_position", out var reset));
            Assert.AreEqual(NativeDashboardAction.ResetCompanionPosition, reset.Action);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("desktop.show", out var desktopShow));
            Assert.AreEqual(NativeDashboardAction.ShowCompanion, desktopShow.Action);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("desktop.movement.enable", out var desktopMovement));
            Assert.AreEqual(NativeDashboardAction.EnableWander, desktopMovement.Action);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("desktop.drag.enable", out var drag));
            Assert.AreEqual(NativeDashboardAction.EnableDrag, drag.Action);

            Assert.IsTrue(MacNativeDashboardService.TryParseAction("desktop.clickThrough.enable", out var desktopClickThrough));
            Assert.AreEqual(NativeDashboardAction.EnableClickThrough, desktopClickThrough.Action);
        }

        [UnityTest]
        public IEnumerator NativeDesktopCompanionSettingsPersistAndRestore()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName());
            var saveRepository = new SaveDataRepository(directory);
            var viewModel = CreateNativeReviewViewModel(saveRepository);

            Result<DesktopCompanionSettings> enableResult = null;
            yield return RunTaskWithTimeout(
                cancellationToken => viewModel.SetDesktopCompanionEnabledAsync(false, cancellationToken),
                "desktop companion settings enable",
                10f,
                result => enableResult = result);
            Assert.IsTrue(enableResult.IsSuccess);

            Result<DesktopCompanionSettings> updateResult = null;
            yield return RunTaskWithTimeout(
                async cancellationToken =>
                {
                    var motionResult = await viewModel.SetDesktopCompanionMotionModeAsync(CompanionDesktopMotionMode.Calm, cancellationToken);
                    if (!motionResult.IsSuccess)
                    {
                        return motionResult;
                    }

                    var clickThroughResult = await viewModel.SetDesktopCompanionClickThroughAsync(true, cancellationToken);
                    if (!clickThroughResult.IsSuccess)
                    {
                        return clickThroughResult;
                    }

                    return await viewModel.SetDesktopCompanionVisualThemeAsync("black_cat", cancellationToken);
                },
                "desktop companion settings update",
                10f,
                result => updateResult = result);
            Assert.IsTrue(updateResult.IsSuccess);

            SaveData restored = null;
            yield return RunTaskWithTimeout(
                cancellationToken => new SaveDataRepository(directory).LoadAsync(cancellationToken),
                "desktop companion settings reload",
                10f,
                result => restored = result);
            var settings = RepositoryCompanionProfileService.GetSelectedDesktopCompanionSettings(restored);

            Debug.Log("PHASE desktop companion settings assertions start");
            Assert.IsFalse(settings.IsDesktopCompanionEnabled);
            Assert.AreEqual(CompanionDesktopMotionMode.Calm, settings.MotionMode);
            Assert.IsTrue(settings.IsClickThroughEnabled);
            Assert.AreEqual("black_cat", settings.VisualThemeId);
            Debug.Log("PHASE desktop companion settings assertions complete");
        }

        [Test]
        public void NativeDesktopCompanionSettingsNormalizeLegacySkinIds()
        {
            var settings = DesktopCompanionSettings.CreateDefault();
            settings.VisualThemeId = "pixel-default";

            var clone = RepositoryCompanionProfileService.CloneDesktopCompanionSettings(settings);

            Assert.AreEqual("orange_cat", DesktopCompanionSettings.CreateDefault().VisualThemeId);
            Assert.AreEqual("orange_cat", clone.VisualThemeId);
            Assert.AreEqual("orange_cat", CompanionSkinCatalog.Normalize("unknown_skin"));
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
            // JsonUtility only emits collection item fields when an item exists; keep runtime defaults empty.
            state.repositories = new[] { new NativeRepositoryListItem() };
            state.agentProviders = new[] { new NativeAgentProviderState() };
            state.tokenShop.targetType = "aiAgent";
            state.tokenShop.selectedAgentId = "codex";
            state.tokenShop.selectedCategory = "skins";
            state.tokenShop.agents = new[] { new NativeTokenShopAgentState { id = "codex", lockedReason = "Connect to unlock agent cosmetics." } };
            state.tokenShop.items = new[] { new NativeTokenShopItemState { itemId = "agent_skin_codex_terminal", itemType = "Skin", category = "skins", rarity = "Common", targetCompatibility = "Codex", stateLabel = "Connect agent", insufficientCoinReason = "Need 1 more Forge Coins.", lockedAgentReason = "Connect to unlock agent cosmetics." } };
            var json = state.ToJson();

            StringAssert.Contains("appTitle", json);
            StringAssert.Contains("syncStatusText", json);
            StringAssert.Contains("companionVisible", json);
            StringAssert.Contains("repository", json);
            StringAssert.Contains("agentProviders", json);
            StringAssert.Contains("repositories", json);
            StringAssert.Contains("review", json);
            StringAssert.Contains("evolveActionVisible", json);
            StringAssert.Contains("xpProgressRatio", json);
            StringAssert.Contains("motion", json);
            StringAssert.Contains("recentGitXP", json);
            StringAssert.Contains("recentAiXP", json);
            StringAssert.Contains("estimatedTokenActivity", json);
            StringAssert.Contains("tokenCurrencyBalance", json);
            StringAssert.Contains("dominantGrowthPath", json);
            StringAssert.Contains("nextEvolutionPreview", json);
            StringAssert.Contains("repositoryAttributionSummary", json);
            StringAssert.Contains("selectedAgentId", json);
            StringAssert.Contains("selectedCategory", json);
            StringAssert.Contains("ownedItemIds", json);
            StringAssert.Contains("equippedItemIds", json);
            StringAssert.Contains("insufficientCoinReason", json);
            StringAssert.Contains("lockedAgentReason", json);
            StringAssert.Contains("AI Agents: 0 connected", json);
            Assert.IsFalse(json.Contains("Cdx 0%"));
        }

        [Test]
        public void NativeDashboardState_ProjectsRuntimeVisibleEvolveAndAgentUsageFields()
        {
            var state = NativeDashboardState.CreateDefault();
            state.companion.xp = 1200;
            state.companion.xpToNextLevel = 419;
            state.companion.canLevelUp = true;
            state.companion.evolveActionVisible = true;
            state.companion.xpStatusText = "1200 XP · 419 XP required · Ready to evolve";
            state.companion.carryForwardText = "781 XP will carry over after evolution";
            state.repositories = new[]
            {
                new NativeRepositoryListItem
                {
                    id = "repo-a",
                    name = "TokenForge",
                    stage = "Junior",
                    level = 2,
                    currentXP = 1200,
                    requiredXP = 419,
                    canLevelUp = true,
                    canEvolve = true,
                    recentGitXP = 33,
                    recentAiXP = 101,
                    estimatedTokenActivity = "Medium"
                }
            };
            state.agentProviders = new[]
            {
                new NativeAgentProviderState
                {
                    id = "codex",
                    displayName = "Codex",
                    connected = true,
                    approvedSource = true,
                    estimatedTokenActivity = "Medium",
                    estimatedTokensText = "unavailable",
                    sessionCountText = "1",
                    interactionCountText = "Large",
                    recentAnalyzedRepository = "TokenForge",
                    repositoryAttributionSummary = "TokenForge: +101 XP · token activity Medium",
                    pendingXP = 101
                }
            };

            var json = state.ToJson();

            StringAssert.Contains("evolveActionVisible", json);
            StringAssert.Contains("Ready to evolve", json);
            StringAssert.Contains("781 XP will carry over", json);
            StringAssert.Contains("recentGitXP", json);
            StringAssert.Contains("recentAiXP", json);
            StringAssert.Contains("repositoryAttributionSummary", json);
            StringAssert.Contains("estimatedTokensText", json);
            Assert.IsFalse(json.Contains("1200/419 XP"));
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

        [UnityTest]
        public IEnumerator ApprovePendingNativeReviewAppliesXpAndStats()
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

            Result initialSave = null;
            yield return RunTaskWithTimeout(
                cancellationToken => saveRepository.SaveAsync(saveData, cancellationToken),
                "approve pending review save",
                10f,
                result => initialSave = result);
            Assert.IsTrue(initialSave.IsSuccess);
            var viewModel = CreateNativeReviewViewModel(saveRepository);

            Result result = null;
            yield return RunTaskWithTimeout(
                cancellationToken => viewModel.ApprovePendingNativeReviewAsync(cancellationToken),
                "approve pending review action",
                10f,
                approveResult => result = approveResult);

            SaveData loaded = null;
            yield return RunTaskWithTimeout(
                cancellationToken => saveRepository.LoadAsync(cancellationToken),
                "approve pending review reload",
                10f,
                loadResult => loaded = loadResult);

            Debug.Log("PHASE approve pending review assertions start");
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(120, loaded.CharacterProfile.TotalExp);
            Assert.AreEqual(3, loaded.CharacterProfile.Stats.Logic);
            Assert.AreEqual(2, loaded.CharacterProfile.Stats.Debug);
            Assert.IsNull(loaded.PendingNativeActivityReview);
            Assert.AreEqual(1, loaded.WorkSessionSummaries.Count);
            Debug.Log("PHASE approve pending review assertions complete");
        }

        [UnityTest]
        public IEnumerator DiscardPendingNativeReviewDoesNotApplyXpOrStats()
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

            Result initialSave = null;
            yield return RunTaskWithTimeout(
                cancellationToken => saveRepository.SaveAsync(saveData, cancellationToken),
                "discard pending review save",
                10f,
                saveResult => initialSave = saveResult);
            Assert.IsTrue(initialSave.IsSuccess);
            var viewModel = CreateNativeReviewViewModel(saveRepository);

            Result result = null;
            yield return RunTaskWithTimeout(
                cancellationToken => viewModel.DiscardPendingNativeReviewAsync(cancellationToken),
                "discard pending review action",
                10f,
                discardResult => result = discardResult);

            SaveData loaded = null;
            yield return RunTaskWithTimeout(
                cancellationToken => saveRepository.LoadAsync(cancellationToken),
                "discard pending review reload",
                10f,
                loadResult => loaded = loadResult);

            Debug.Log("PHASE discard pending review assertions start");
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0, loaded.CharacterProfile.TotalExp);
            Assert.AreEqual(0, loaded.CharacterProfile.Stats.Logic);
            Assert.IsNull(loaded.PendingNativeActivityReview);
            Assert.AreEqual(0, loaded.WorkSessionSummaries.Count);
            Debug.Log("PHASE discard pending review assertions complete");
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

        [UnityTest]
        public IEnumerator ApprovePendingNativeReviewIsIdempotentByReviewId()
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

            Result initialSave = null;
            yield return RunTaskWithTimeout(
                cancellationToken => saveRepository.SaveAsync(saveData, cancellationToken),
                "idempotent pending review save",
                10f,
                result => initialSave = result);
            Assert.IsTrue(initialSave.IsSuccess);
            var viewModel = CreateNativeReviewViewModel(saveRepository);

            Result first = null;
            yield return RunTaskWithTimeout(
                cancellationToken => viewModel.ApprovePendingNativeReviewAsync(cancellationToken),
                "idempotent pending review first approve",
                10f,
                result => first = result);

            SaveData afterFirst = null;
            yield return RunTaskWithTimeout(
                cancellationToken => saveRepository.LoadAsync(cancellationToken),
                "idempotent pending review restore load",
                10f,
                result => afterFirst = result);
            afterFirst.PendingNativeActivityReview = pending;

            Result restorePendingReview = null;
            yield return RunTaskWithTimeout(
                cancellationToken => saveRepository.SaveAsync(afterFirst, cancellationToken),
                "idempotent pending review restore save",
                10f,
                result => restorePendingReview = result);
            Assert.IsTrue(restorePendingReview.IsSuccess);

            Result second = null;
            yield return RunTaskWithTimeout(
                cancellationToken => viewModel.ApprovePendingNativeReviewAsync(cancellationToken),
                "idempotent pending review second approve",
                10f,
                result => second = result);

            SaveData loaded = null;
            yield return RunTaskWithTimeout(
                cancellationToken => saveRepository.LoadAsync(cancellationToken),
                "idempotent pending review reload",
                10f,
                result => loaded = result);

            Debug.Log("PHASE idempotent pending review assertions start");
            Assert.IsTrue(first.IsSuccess);
            Assert.IsTrue(second.IsSuccess);
            Assert.AreEqual(90, loaded.CharacterProfile.TotalExp);
            Assert.AreEqual(1, loaded.WorkSessionSummaries.Count);
            Assert.That(loaded.AppliedNativeReviewIds, Does.Contain("review-idempotent"));
            Debug.Log("PHASE idempotent pending review assertions complete");
        }

        [UnityTest]
        public IEnumerator ApproveCombinedNativeReviewAppliesGitAndAgentSessionsOnce()
        {
            ResetNativeDashboardBridgeStaticState();
            Debug.Log("PHASE combined native review start");
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

            Result initialSave = null;
            yield return RunTaskWithTimeout(
                cancellationToken => saveRepository.SaveAsync(saveData, cancellationToken),
                "seed save",
                10f,
                result => initialSave = result);
            Assert.IsTrue(initialSave.IsSuccess);
            var viewModel = CreateNativeReviewViewModel(saveRepository);

            Result first = null;
            yield return RunTaskWithTimeout(
                cancellationToken => viewModel.ApprovePendingNativeReviewAsync(cancellationToken),
                "approve combined review first",
                10f,
                result => first = result);

            SaveData afterFirst = null;
            yield return RunTaskWithTimeout(
                cancellationToken => saveRepository.LoadAsync(cancellationToken),
                "load",
                10f,
                result => afterFirst = result);
            afterFirst.PendingNativeActivityReview = saveData.PendingNativeActivityReview;

            Result restorePendingReview = null;
            yield return RunTaskWithTimeout(
                cancellationToken => saveRepository.SaveAsync(afterFirst, cancellationToken),
                "restore pending review save",
                10f,
                result => restorePendingReview = result);
            Assert.IsTrue(restorePendingReview.IsSuccess);

            Result second = null;
            yield return RunTaskWithTimeout(
                cancellationToken => viewModel.ApprovePendingNativeReviewAsync(cancellationToken),
                "approve combined review second",
                10f,
                result => second = result);

            SaveData loaded = null;
            yield return RunTaskWithTimeout(
                cancellationToken => saveRepository.LoadAsync(cancellationToken),
                "reload",
                10f,
                result => loaded = result);

            Debug.Log("PHASE assertions start");
            Assert.IsTrue(first.IsSuccess);
            Assert.IsTrue(second.IsSuccess);
            Assert.AreEqual(100, loaded.CharacterProfile.TotalExp);
            Assert.AreEqual(2, loaded.WorkSessionSummaries.Count);
            Assert.That(loaded.AppliedNativeReviewIds, Does.Contain("combined-review"));
            Debug.Log("PHASE assertions complete");
            ResetNativeDashboardBridgeStaticState();
        }

        private static IEnumerator RunTaskWithTimeout<T>(
            Func<CancellationToken, Task<T>> operation,
            string phase,
            float timeoutSeconds,
            Action<T> onCompleted)
        {
            using (var cancellation = new CancellationTokenSource())
            {
                Debug.Log("PHASE " + phase + " start");
                Task<T> operationTask = null;
                try
                {
                    operationTask = operation(cancellation.Token);
                }
                catch (Exception exception)
                {
                    Assert.Fail("PHASE " + phase + " failed to start: " + exception);
                }

                if (operationTask == null)
                {
                    Assert.Fail("PHASE " + phase + " did not return a task.");
                }

                var startedAt = Time.realtimeSinceStartup;
                while (!operationTask.IsCompleted)
                {
                    if (Time.realtimeSinceStartup - startedAt > timeoutSeconds)
                    {
                        cancellation.Cancel();
                        Assert.Fail("PHASE " + phase + " timed out after " + timeoutSeconds + "s.");
                    }

                    yield return null;
                }

                if (operationTask.IsCanceled)
                {
                    Assert.Fail("PHASE " + phase + " was canceled.");
                }

                if (operationTask.IsFaulted)
                {
                    Assert.Fail("PHASE " + phase + " failed: " + operationTask.Exception.Flatten());
                }

                onCompleted(operationTask.Result);
                Debug.Log("PHASE " + phase + " complete");
            }
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
            StringAssert.Contains("TokenForgeSettingsSwitchRow", source);
            StringAssert.Contains("TokenForgeDashboardCompanionPreviewView", source);
            StringAssert.Contains("startDashboardPreviewAnimation", source);
            StringAssert.Contains("xpProgressRatio", source);
            StringAssert.Contains("Evolve Token", source);
            StringAssert.Contains("[DashboardUI] render hero evolveVisible", source);
            StringAssert.Contains("[DashboardUI] evolve button added", source);
            StringAssert.Contains("[RepositoriesUI] render repoCards count", source);
            StringAssert.Contains("[AIUsageUI] render provider", source);
            StringAssert.Contains("[DesktopCompanion] orderFront completed", source);
            StringAssert.Contains("[DesktopCompanion] animation start old=", source);
            StringAssert.Contains("[MenuBarCompanion] animation started", source);
            StringAssert.Contains("View Growth", source);
            StringAssert.Contains("TokenForgeMenuCanLevelUp", source);
            StringAssert.Contains("NSSwitch", source);
            StringAssert.Contains("Click anywhere in this row", source);
            StringAssert.Contains("Available in a signed release build.", source);
            StringAssert.Contains("NSPointInRect(togglePoint, self.toggleControl.bounds)", source);
            StringAssert.Contains("Connect a repository to customize its companion.", source);
            Assert.IsFalse(source.Contains("%ld/%ld XP, overflow carries forward"));
            StringAssert.Contains("skinTileWithId", source);
            StringAssert.Contains("SetCompanionOverlayVisualTheme", source);
            StringAssert.Contains("repository.add", source);
            StringAssert.Contains("repository.analyze", source);
            StringAssert.Contains("review.saveGrowth", source);
            StringAssert.Contains("review.viewDetails", source);
            StringAssert.Contains("agent.autoDetect", source);
            StringAssert.Contains("AI Agents: 0 connected", source);
            Assert.IsFalse(source.Contains("Native shell preferences"));
        }

        [Test]
        public void NativeDashboardRendererUsesCleanGridRuntimePath()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "Plugins/macOS/DesktopCompanionOverlay.mm"));

            StringAssert.Contains("dashboardRenderer=clean-grid-v4", source);
            StringAssert.Contains("gridVersion=clean-v3", source);
            StringAssert.Contains("cardFrames aligned=true gap=22", source);
            StringAssert.Contains("rowHeights consistent=true", source);
            StringAssert.Contains("heroCardWithCompanion", source);
            StringAssert.Contains("repositoryStatusCardWithRepository", source);
            StringAssert.Contains("aiAgentsStatusCardWithAgents", source);
            StringAssert.Contains("companionMotionCardWithCompanion", source);
            StringAssert.Contains("recentActivityTimelineCardWithActivity", source);
            StringAssert.Contains("TokenForgeFriendlyDashboardSummary", source);
            Assert.IsFalse(source.Contains("Evolve hidden:"));
        }

        [Test]
        public void NativeDashboardHeroDefinesAvatarBoundsAndAnimationLogs()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "Plugins/macOS/DesktopCompanionOverlay.mm"));

            StringAssert.Contains("TokenForgeAvatarPreviewView", source);
            StringAssert.Contains("TokenForgeDashboardHeroAvatarContainerView", source);
            StringAssert.Contains("TokenForgeAvatarCanonicalBounds", source);
            StringAssert.Contains("TokenForgeAvatarFinalDrawingRect", source);
            StringAssert.Contains("TokenForgeDrawAvatarInRect", source);
            StringAssert.Contains("safePadding = 32.0", source);
            StringAssert.Contains("constraintGreaterThanOrEqualToConstant:250.0", source);
            StringAssert.Contains("constraintLessThanOrEqualToConstant:430.0", source);
            StringAssert.Contains("[AvatarPreview] sourceSize=", source);
            StringAssert.Contains("[AvatarRenderer] preset=", source);
            StringAssert.Contains("parts=head/body/headset/legs/shadow allInsideFinalRect=", source);
            StringAssert.Contains("assetType=hero notMenuBar", source);
            StringAssert.Contains("[DashboardHero] relayout width=", source);
            StringAssert.Contains("withinSafeBounds=", source);
            StringAssert.Contains("warningShake", source);
            StringAssert.Contains("attentionBounce", source);
            StringAssert.Contains("activityPulse", source);
        }

        [Test]
        public void NativeDesktopOverlayIsIndependentAndKeepsRunningAfterWindowClose()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "Plugins/macOS/DesktopCompanionOverlay.mm"));

            StringAssert.Contains("NSWindowStyleMaskNonactivatingPanel", source);
            StringAssert.Contains("NSFloatingWindowLevel", source);
            StringAssert.Contains("releasedWhenClosed = NO", source);
            StringAssert.Contains("strongReference panel=", source);
            StringAssert.Contains("orderFront called visibleBefore=", source);
            StringAssert.Contains("[DesktopOverlay] create window independent=true", source);
            StringAssert.Contains("[DesktopOverlay] orderFrontRegardless completed", source);
            StringAssert.Contains("[DesktopOverlay] show requested visibleSetting=true", source);
            StringAssert.Contains("[AppLifecycle] shouldTerminateAfterLastWindowClosed=false", source);
            StringAssert.Contains("[AppLifecycle] lastWindowClosed keepRunning=true", source);
            StringAssert.Contains("[DashboardLifecycle][CLOSE_REQUEST] shouldTerminate=false", source);
            StringAssert.Contains("[OverlayLifecycle][KEEP_ALIVE_AFTER_DASHBOARD_CLOSE]", source);
            StringAssert.Contains("[AppLifecycle][QUIT_REQUESTED] source=menu", source);
            StringAssert.Contains("[AppLifecycle][UNEXPECTED_TERMINATE_ATTEMPT]", source);
            StringAssert.Contains("[DesktopOverlay] movementTimer started interval=", source);
            StringAssert.Contains("[DesktopOverlay] tick oldOrigin=", source);
            StringAssert.Contains("[DesktopOverlay] tick oldFrame=", source);
            StringAssert.Contains("[DesktopOverlay] boundsClamped screen=", source);
            StringAssert.Contains("[DesktopOverlay] quit cleanup completed", source);
        }

        [Test]
        public void NativeDesktopOverlayDragIsGatedToPanelContentOnly()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "Plugins/macOS/DesktopCompanionOverlay.mm"));

            StringAssert.Contains("TokenForgeCompanionRenderRoleDesktopOverlay", source);
            StringAssert.Contains("TokenForgeCompanionRenderRoleDashboardPreview", source);
            StringAssert.Contains("TokenForgeDesktopOverlayCompanionView", source);
            StringAssert.Contains("TokenForgeIsDesktopOverlayPanelContentView", source);
            StringAssert.Contains("TokenForge.DesktopCompanion", source);
            StringAssert.Contains("[OverlayDrag][BEGIN] repo=%@ generation=", source);
            StringAssert.Contains("[DashboardAvatarView][NO_DRAG] reason=previewRole", source);
            StringAssert.Contains("[OverlayDrag][MOVE] repo=%@ generation=", source);
            StringAssert.Contains("[OverlayDrag][END] repo=%@ generation=", source);
            StringAssert.Contains("[OverlayDrag][COORDS]", source);
            StringAssert.Contains("[OverlayDrag][POSITION_ERROR]", source);
            StringAssert.Contains("[OverlayDrag][SUPPRESS_PROJECTION] reason=dragInProgress", source);
            StringAssert.Contains("[OverlayMovement][PAUSE] reason=drag", source);
            StringAssert.Contains("[OverlayMovement][RESUME] reason=dragEnded", source);
            StringAssert.Contains("[OverlayProjection][SUPPRESSED_POSITION_APPLY] reason=dragging", source);
            StringAssert.Contains("[OverlayProjection][SUPPRESSED_SIZE_APPLY] reason=dragging", source);
            StringAssert.Contains("[OverlayLifecycle][DEFER_HIDE] reason=dragging", source);
            StringAssert.Contains("[OverlayWatchdog][SUPPRESSED] reason=dragging", source);
            StringAssert.Contains("[OverlayLifecycle][SUPPRESSED_RECREATE] reason=dragging", source);
            StringAssert.Contains("TokenForgePendingOverlayActionHide", source);
            StringAssert.Contains("TokenForgePendingOverlayActionDestroy", source);
            StringAssert.Contains("TokenForgePendingOverlayActionResetPosition", source);
            StringAssert.Contains("TokenForgeOverlayDragFinalizing", source);
            StringAssert.Contains("TokenForgeOverlayDragPersistedThisGesture", source);
            StringAssert.Contains("[OverlayLifecycle][APPLY_DEFERRED_DESTROY]", source);
            StringAssert.Contains("[OverlayLifecycle][APPLY_DEFERRED_RESET_POSITION]", source);
            StringAssert.Contains("[OverlayState][DEFER_CLICK_THROUGH] reason=dragging", source);
            StringAssert.Contains("[OverlayState][APPLY] desiredDrag=", source);
            StringAssert.Contains("[OverlayState][PROJECT] visible=", source);
            StringAssert.Contains("TokenForgeDashboardLayoutHashForState", source);
            StringAssert.Contains("[DashboardLayout][STABLE_DURING_DRAG]", source);
            StringAssert.Contains("[DashboardLayout][WARN] changedDuringOverlayDrag", source);
        }

        [Test]
        public void NativeDashboardLifecycleUsesSingleCanonicalWindow()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "Plugins/macOS/DesktopCompanionOverlay.mm"));

            StringAssert.Contains("TokenForgeNativeDashboardWindow", source);
            StringAssert.Contains("TokenForgeDashboardWindowIdentifier", source);
            StringAssert.Contains("openOrFocusDashboardFromSource", source);
            StringAssert.Contains("hideDashboardFromSource", source);
            StringAssert.Contains("[DashboardLifecycle] open.request source=", source);
            StringAssert.Contains("[DashboardLifecycle] open.focusExisting windowNumber=", source);
            StringAssert.Contains("[DashboardLifecycle] open.createNew reason=", source);
            StringAssert.Contains("[DashboardLifecycle] close.request source=", source);
            StringAssert.Contains("[DashboardLifecycle] windowWillClose windowNumber=", source);
            StringAssert.Contains("[DashboardLifecycle] canonical.nil reason=windowWillClose", source);
            StringAssert.Contains("[DashboardLifecycle][WARN] duplicateDashboardWindows count=", source);
            StringAssert.Contains("[DashboardLifecycle] windowsDump phase=", source);
            StringAssert.Contains("[DashboardLifecycle][OPEN] source=", source);
            StringAssert.Contains("[DashboardLifecycle][FOCUS_EXISTING] source=", source);
            StringAssert.Contains("[DashboardLifecycle][CLOSE] source=", source);
            StringAssert.Contains("[DashboardLifecycle][OPEN_REQUEST] source=", source);
            StringAssert.Contains("[DashboardLifecycle][REUSE_EXISTING] window=", source);
            StringAssert.Contains("[DashboardLifecycle][FOCUS] source=", source);
            StringAssert.Contains("[DashboardLifecycle][SUPPRESS_REOPEN] reason=recentExplicitClose", source);
            StringAssert.Contains("[DashboardLifecycle][SUPPRESS_DUPLICATE] reason=", source);
            StringAssert.Contains("[DashboardLifecycle][WARN_DUPLICATE] count=", source);
            StringAssert.Contains("TokenForgeCleanupDuplicateDashboardWindows", source);
            StringAssert.Contains("TokenForgeCleanupStaleUnityDashboardWindows", source);
            StringAssert.Contains("TokenForgeOpenOrFocusDashboard(@\"dock.reopen\")", source);
            StringAssert.Contains("TokenForgeOpenOrFocusDashboard(@\"menubar.dashboard\")", source);
            StringAssert.Contains("TokenForgeOpenOrFocusDashboard(@\"csharp.dashboard\")", source);
            StringAssert.Contains("[DockReopen][ENTER] hasVisibleWindows=", source);
            StringAssert.Contains("[DockReopen][CLASSIFY] dashboardVisible=", source);
            StringAssert.Contains("[DockReopen][ACTION] openOrFocusDashboard source=dock.reopen", source);
            StringAssert.Contains("TokenForgeDumpWindowClassifications(@\"AFTER_DOCK_REOPEN\")", source);
            StringAssert.Contains("TokenForgeDumpWindowClassifications(@\"AFTER_MENUBAR_OPEN\")", source);
            StringAssert.Contains("TokenForgeDumpWindowClassifications(@\"AFTER_DASHBOARD_CLOSE\")", source);
            StringAssert.Contains("hideDashboardFromSource:@\"dashboardX\"", source);
            StringAssert.Contains("blank TokenForge window detected; orderOut without dashboard reopen", source);
        }

        [Test]
        public void NativeRuntimeVerificationSuppressesCrashReopenAndReportLoops()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "Plugins/macOS/DesktopCompanionOverlay.mm"));
            var bootstrapper = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/AppBootstrapper.cs"));
            var dashboardService = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/Platform/MacNativeDashboardService.cs"));

            StringAssert.Contains("TOKENFORGE_VERIFY_RUNTIME", source);
            StringAssert.Contains("-TokenForgeVerifyRuntime", source);
            StringAssert.Contains("[RuntimeVerify][ENABLED]", source);
            StringAssert.Contains("[CrashRecovery][SUPPRESSED_REPORT_UI] reason=verificationMode", source);
            StringAssert.Contains("[DashboardLifecycle][SUPPRESS_REOPEN] reason=verificationMode", source);
            StringAssert.Contains("[OverlayWatchdog][SUPPRESSED] reason=verificationModeWarmup", source);
            StringAssert.Contains("[WindowsDump][LAUNCH_STABLE]", source);
            StringAssert.Contains("[AppLifecycle][SUPPRESS_QUIT] reason=verificationMode", source);
            StringAssert.Contains("LaunchInProgress", source);
            StringAssert.Contains("NormalTerminationAt", source);
            StringAssert.Contains("LastAbnormalTerminationAt", source);
            StringAssert.Contains("ReportIssueAutoPresented", source);
            StringAssert.Contains("applicationWillTerminate", source);
            StringAssert.Contains("shouldRestoreApplicationState=false route=suppressed", source);
            StringAssert.Contains("reportIssueAutoPresent=false route=manualOnly", source);
            StringAssert.Contains("manualOpen requested=true autoPresent=false nonBlocking=true", source);
            StringAssert.Contains("TokenForge_ShowDashboardWindowWithSource", source);

            StringAssert.Contains("IsRuntimeVerificationMode", bootstrapper);
            StringAssert.Contains("ApplyNativeShellState(showDashboardIfNeeded: false)", bootstrapper);
            StringAssert.Contains("source=initialOverlayProjection", bootstrapper);
            StringAssert.Contains("manualOpen requested=true autoPresent=false nonBlocking=true source=nativeDashboard", bootstrapper);
            StringAssert.Contains("NativeShowDashboardWindowWithSource", dashboardService);
        }

        [Test]
        public void NativeMotionCardProvidesImmediateRuntimeActions()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "Plugins/macOS/DesktopCompanionOverlay.mm"));

            StringAssert.Contains("Show on Desktop", source);
            StringAssert.Contains("Hide Overlay", source);
            StringAssert.Contains("Enable Movement", source);
            StringAssert.Contains("Pause Movement", source);
            StringAssert.Contains("Enable Drag", source);
            StringAssert.Contains("Click-through", source);
            StringAssert.Contains("Reset Position", source);
            StringAssert.Contains("showCompanionFromDashboard", source);
            StringAssert.Contains("enableWanderFromDashboard", source);
            StringAssert.Contains("disableWanderFromDashboard", source);
            StringAssert.Contains("SetCompanionOverlayMotionProfile", source);
            StringAssert.Contains("ShowDesktopCompanionOverlay", source);
            StringAssert.Contains("showOnDesktop", source);
            StringAssert.Contains("hideFromDesktop", source);
            StringAssert.Contains("desktop.show", source);
            StringAssert.Contains("desktop.hide", source);
            StringAssert.Contains("desktop.movement.enable", source);
            StringAssert.Contains("desktop.movement.pause", source);
            StringAssert.Contains("desktop.drag.enable", source);
            StringAssert.Contains("desktop.clickThrough.enable", source);
            StringAssert.Contains("desktop.position.reset", source);
            StringAssert.Contains("showButtonRequired", source);
        }

        [Test]
        public void NativeMenuBarCompanionAnimatesImageFrames()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "Plugins/macOS/DesktopCompanionOverlay.mm"));

            StringAssert.Contains("TokenForgeCreateStatusCompanionImage(NSInteger stage, NSInteger archetype, NSInteger frameIndex, NSString *mode)", source);
            StringAssert.Contains("TokenForgeAvatarImageForPreset(@\"menuBar\"", source);
            StringAssert.Contains("TokenForgeAvatarCacheKey(@\"menuBar\"", source);
            StringAssert.Contains("NSImage *newImage = TokenForgeCreateStatusCompanionImage(animatedStage, animatedArchetype, self.statusAnimationFrame, mode)", source);
            StringAssert.Contains("[MenuBarCompanion] timerStarted interval=", source);
            StringAssert.Contains("[MenuBarCompanion] tick frameIndex=", source);
            StringAssert.Contains("[MenuBarCompanion] buttonImageUpdated changed=", source);
            StringAssert.Contains("[MenuBarCompanion] cacheKey=", source);
            StringAssert.Contains("[MenuBarCompanion] animation started mode=", source);
            StringAssert.Contains("[MenuBarCompanion] frame index=", source);
            StringAssert.Contains("[MenuBarCompanion] reaction started type=", source);
            StringAssert.Contains("[MenuBarCompanion] animation stopped reason=", source);
        }

        [Test]
        public void NativeCompanionBridgeExportsRuntimeEntryPoints()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "Plugins/macOS/DesktopCompanionOverlay.mm"));
            var service = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/Platform/MacDesktopCompanionOverlayService.cs"));

            StringAssert.Contains("extern \"C\" bool CreateDesktopCompanionOverlay", source);
            StringAssert.Contains("extern \"C\" void ShowDesktopCompanionOverlay", source);
            StringAssert.Contains("extern \"C\" void HideDesktopCompanionOverlay", source);
            StringAssert.Contains("extern \"C\" bool TokenForge_IsOverlayDragging", source);
            StringAssert.Contains("extern \"C\" bool TokenForge_IsOverlayDraggingForRepository", source);
            StringAssert.Contains("TokenForgeOverlayPanelsByRepositoryId", source);
            StringAssert.Contains("TokenForgeOverlayViewsByRepositoryId", source);
            StringAssert.Contains("TokenForgeOverlaySnapshotsByRepositoryId", source);
            StringAssert.Contains("TokenForgeOverlayFramesByRepositoryId", source);
            StringAssert.Contains("TokenForgeOverlayDragStatesByRepositoryId", source);
            StringAssert.Contains("TokenForgeOverlayGenerationsByRepositoryId", source);
            StringAssert.Contains("TokenForgeResolveFarmFrameForRepository", source);
            StringAssert.Contains("[OverlayFarmLayout][COLLISION]", source);
            StringAssert.Contains("[OverlayFarmLayout][NUDGE]", source);
            StringAssert.Contains("TokenForgeActiveDragRepositoryId", source);
            StringAssert.Contains("[OverlayDrag][CAPTURE] repo=", source);
            StringAssert.Contains("[OverlayDrag][IGNORE] repo=%@ reason=notActiveDrag", source);
            StringAssert.Contains("[StatusItem][ENSURE] source=%@", source);
            StringAssert.Contains("TokenForgeEnsureStatusItem(@\"launch.statusItem\")", source);
            StringAssert.Contains("[DashboardLifecycle][FOCUS] source=%@ visible=%@ key=%@ main=%@ miniaturized=%@", source);
            StringAssert.Contains("extern \"C\" void SetCompanionOverlayMotionProfile", source);
            StringAssert.Contains("extern \"C\" void SetCompanionOverlayClickThrough", source);
            StringAssert.Contains("extern \"C\" void TokenForge_SetOverlayClickEnabled", source);
            StringAssert.Contains("extern \"C\" void ResetCompanionOverlayPosition", source);
            StringAssert.Contains("extern \"C\" void TokenForge_RegisterOverlayClickedCallback", source);
            StringAssert.Contains("extern \"C\" void TokenForge_RegisterOverlayDoubleClickedCallback", source);
            StringAssert.Contains("extern \"C\" void TokenForge_RegisterOverlayDragEndedCallback", source);
            StringAssert.Contains("public bool IsAnyOverlayDragging()", service);
            StringAssert.Contains("public bool IsOverlayDragging(string repositoryId)", service);
            StringAssert.Contains("public void SetCompanionFarmSnapshots(DesktopCompanionFarmState farmState)", service);
            StringAssert.Contains("EntryPoint = \"TokenForge_IsOverlayDragging\"", service);
            StringAssert.Contains("EntryPoint = \"TokenForge_IsOverlayDraggingForRepository\"", service);
            Assert.IsFalse(service.Contains("public bool IsDragging"));
        }

        [Test]
        public void NativeCompanionLifecycleUsesMainThreadStrongPanelAndCommonTimers()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "Plugins/macOS/DesktopCompanionOverlay.mm"));

            StringAssert.Contains("dispatch_get_main_queue()", source);
            StringAssert.Contains("panel.releasedWhenClosed = NO", source);
            StringAssert.Contains("TokenForgeCompanionWindow = panel", source);
            StringAssert.Contains("orderFrontRegardless", source);
            StringAssert.Contains("orderFront:nil", source);
            StringAssert.Contains("NSRunLoopCommonModes", source);
            StringAssert.Contains("TokenForgeCompanionMotionTimer", source);
            StringAssert.Contains("TokenForgeAvatarCacheKey", source);
            StringAssert.Contains("preset", source);
            StringAssert.Contains("frameIndex", source);
            StringAssert.Contains("mode", source);
        }

        [Test]
        public void NativeDashboardAvoidsRawEnumTextOnMainCards()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "Plugins/macOS/DesktopCompanionOverlay.mm"));

            Assert.IsFalse(source.Contains("category Test"));
            Assert.IsFalse(source.Contains("stat Focus"));
            Assert.IsFalse(source.Contains("sessions One"));
            Assert.IsFalse(source.Contains("interactions Large"));
            Assert.IsFalse(source.Contains("Codex | token activity"));
            StringAssert.Contains("No repository-attributed AI growth yet.", source);
            StringAssert.Contains("Analyze AI activity to review estimated token-based growth.", source);
        }

        [Test]
        public void NativeSidebarCompanionSwitcherIsInRuntimeRenderer()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "Plugins/macOS/DesktopCompanionOverlay.mm"));

            StringAssert.Contains("Repository Companions", source);
            StringAssert.Contains("sidebarCompanionMiniItem", source);
            StringAssert.Contains("[SidebarCompanions] render count=", source);
            StringAssert.Contains("[SidebarCompanions] item repo=", source);
            StringAssert.Contains("[SidebarCompanions] select repo=", source);
            StringAssert.Contains("repository.setActive:", source);
            StringAssert.Contains("open_active_companion_dashboard", source);
            StringAssert.Contains("open_repository_companion_dashboard:", source);
            StringAssert.Contains("Active repository", source);
            StringAssert.Contains("row.toolTip = [NSString stringWithFormat:@\"%@ · %@ · Lv %ld%@\"", source);
            StringAssert.Contains("Evolve", source);
        }

        [Test]
        public void NativeTokenShopRendererContainsTargetCategoriesAndStateLabels()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "Plugins/macOS/DesktopCompanionOverlay.mm"));

            StringAssert.Contains("shop-target-repository", source);
            StringAssert.Contains("shop-target-ai-agents", source);
            StringAssert.Contains("shop.category:", source);
            StringAssert.Contains("shop.equip:", source);
            StringAssert.Contains("TokenForgeShopPreviewView", source);
            StringAssert.Contains("drawZodiacMascot", source);
            StringAssert.Contains("drawCatInRect", source);
            StringAssert.Contains("zodiac", source);
            StringAssert.Contains("exclusive", source);
            StringAssert.Contains("owned/equipped labels=true", source);
            StringAssert.Contains("locked/insufficient states=true", source);
            StringAssert.Contains("Connect to unlock agent cosmetics", source);
            StringAssert.Contains("Need Coins", source);
            StringAssert.Contains("Equipped", source);
        }

        [Test]
        public void NativeProjectionDoesNotInventAiRepositoryAttribution()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/AppBootstrapper.cs"));
            var viewModelSource = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/UI/ApprovedActivityAnalysisViewModel.cs"));

            StringAssert.Contains("return RepositoryAliasForHash(repositoryHash);", source);
            StringAssert.Contains("Unassigned: +", source);
            StringAssert.Contains("repository attribution unavailable", source);
            StringAssert.Contains("FriendlyNativeSummary", source);
            StringAssert.Contains("state.subtitle = \"Track Git and AI-assisted work as companion growth.\"", source);
            StringAssert.Contains("? string.Empty", viewModelSource);
            Assert.IsFalse(source.Contains("repositoryHash = approvedActivityAnalysis?.CharacterDashboard?.CurrentRepositoryHash"));
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

        private static void ResetNativeDashboardBridgeStaticState()
        {
            typeof(MacNativeDashboardService)
                .GetField("GlobalActionRequested", BindingFlags.NonPublic | BindingFlags.Static)
                ?.SetValue(null, null);
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
