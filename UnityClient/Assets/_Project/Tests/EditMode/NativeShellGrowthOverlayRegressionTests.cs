using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TokenForge.Client.Common;
using TokenForge.Client.Domain;
using TokenForge.Client.Persistence;
using TokenForge.Client.UI;
using UnityEngine;

namespace TokenForge.Client.Tests
{
    public sealed class NativeShellGrowthOverlayRegressionTests
    {
        [Test]
        public void NativeShellPersistsAcrossTabs()
        {
            var source = NativeSource();

            Assert.That(source, Does.Contain("TokenForge.RootWindowContent"));
            Assert.That(source, Does.Contain("TokenForge.FixedTopShellHeader"));
            Assert.That(source, Does.Contain("TokenForge.BodyContainer"));
            Assert.That(source, Does.Contain("TokenForge.FixedLeftSidebar"));
            Assert.That(source, Does.Contain("TokenForge.Sidebar"));
            Assert.That(source, Does.Contain("TokenForge.BodyScroll"));
            Assert.That(source, Does.Contain("TokenForge.BottomTabBar"));
            Assert.That(source, Does.Contain("TokenForge.OverlayPanel"));
            Assert.That(source, Does.Contain("buildFixedTopShellHeader"));
            Assert.That(source, Does.Contain("[PersistentStatusBar][BODY_ONLY_REBUILD]"));
        }

        [Test]
        public void HeaderIsOutsideScrollableContent()
        {
            var source = NativeSource();
            var rootBuilderIndex = source.IndexOf("- (NSView *)buildDashboardRootView", StringComparison.Ordinal);
            Assert.GreaterOrEqual(rootBuilderIndex, 0);
            var headerIndex = source.IndexOf("identifier=TokenForge.FixedTopShellHeader", rootBuilderIndex, StringComparison.Ordinal);
            var scrollIndex = source.IndexOf("scrollView.identifier = @\"TokenForge.DashboardTabScrollView\"", rootBuilderIndex, StringComparison.Ordinal);

            Assert.GreaterOrEqual(headerIndex, 0);
            Assert.Greater(scrollIndex, headerIndex);
            Assert.That(source, Does.Contain("[PersistentStatusBar][OWNERSHIP]"));
            Assert.That(source, Does.Contain("insideScrollView=false"));
        }

        [Test]
        public void RepositoryGrowthSummaryIsNotSharedPlaceholder()
        {
            var saveData = SaveData.CreateDefault();
            saveData.CharacterProfile.Stats = new CharacterStats { Logic = 99, Architecture = 99, Velocity = 99, Debug = 99, Design = 99 };
            saveData.WorkSessionSummaries.Add(Session("repo-a", "a1", WorkType.Feature, CountBucket.Small, true, false));
            saveData.WorkSessionSummaries.Add(Session("repo-b", "b1", WorkType.UIUX, CountBucket.Huge, false, true));
            saveData.GrowthHistory.Add(new CharacterGrowthResult { SessionId = "a1", ExpGained = 80, StatDeltas = new CharacterStats { Logic = 2, Architecture = 1, Velocity = 1, Debug = 1 } });
            saveData.GrowthHistory.Add(new CharacterGrowthResult { SessionId = "b1", ExpGained = 120, StatDeltas = new CharacterStats { Design = 5, Creativity = 2 } });

            var repoA = RepositoryGrowthSummaryProjection.Build(saveData, "repo-a");
            var repoB = RepositoryGrowthSummaryProjection.Build(saveData, "repo-b");

            Assert.AreNotEqual(repoA.Code, repoB.Code);
            Assert.AreNotEqual(297, repoA.Code);
            Assert.Greater(repoA.Debug, 0);
            Assert.Greater(repoB.Design, repoA.Design);
        }

        [Test]
        public void SelectedRepositoriesABCProjectDistinctGrowthVectors()
        {
            var saveData = SaveData.CreateDefault();
            saveData.WorkSessionSummaries.Add(Session("repo-a", "a1", WorkType.Feature, CountBucket.Small, false, false));
            saveData.WorkSessionSummaries.Add(Session("repo-b", "b1", WorkType.Bugfix, CountBucket.Medium, true, false));
            saveData.WorkSessionSummaries.Add(Session("repo-c", "c1", WorkType.UIUX, CountBucket.Large, false, true));
            saveData.GrowthHistory.Add(new CharacterGrowthResult { SessionId = "a1", ExpGained = 40, StatDeltas = new CharacterStats { Logic = 4, Architecture = 2, Velocity = 1 } });
            saveData.GrowthHistory.Add(new CharacterGrowthResult { SessionId = "b1", ExpGained = 55, StatDeltas = new CharacterStats { Debug = 5, Stability = 1 } });
            saveData.GrowthHistory.Add(new CharacterGrowthResult { SessionId = "c1", ExpGained = 65, StatDeltas = new CharacterStats { Design = 4, Creativity = 3 } });

            var repoA = VectorKey(RepositoryGrowthSummaryProjection.Build(saveData, "repo-a"));
            var repoB = VectorKey(RepositoryGrowthSummaryProjection.Build(saveData, "repo-b"));
            var repoC = VectorKey(RepositoryGrowthSummaryProjection.Build(saveData, "repo-c"));

            Assert.AreNotEqual(repoA, repoB);
            Assert.AreNotEqual(repoB, repoC);
            Assert.AreNotEqual(repoA, repoC);
        }

        [Test]
        public void LegacyTimelineWithoutAxisDeltaDoesNotBecomePlaceholderGrowth()
        {
            var saveData = SaveData.CreateDefault();
            RepositoryCompanionProfileService.RecordTimelineEvent(saveData, "growth_saved", "Growth saved", "Legacy growth before axis deltas.", "repo-legacy", "Legacy Repo", "legacy_import", 40);

            var summary = RepositoryGrowthSummaryProjection.Build(saveData, "repo-legacy");

            Assert.IsTrue(summary.HasSavedGrowth);
            Assert.IsTrue(summary.HasLegacyAxisGap);
            Assert.AreEqual(40, summary.TotalXp);
            Assert.AreEqual("0:0:0:0:0", VectorKey(summary));
            Assert.That(summary.LatestSummary, Does.Contain("Legacy event lacks axis delta"));
        }

        [Test]
        public void LegacyTimelineWithoutAxisDeltaProjectsExplicitMissingAxisState()
        {
            var saveData = SaveData.CreateDefault();
            RepositoryCompanionProfileService.RecordTimelineEvent(saveData, "growth_saved", "Growth saved", "Legacy growth before axis deltas.", "repo-legacy", "Legacy Repo", "legacy_import", 40);

            var summary = RepositoryGrowthSummaryProjection.Build(saveData, "repo-legacy");

            Assert.IsTrue(summary.HasSavedGrowth);
            Assert.IsFalse(summary.HasStoredAxisDeltas);
            Assert.IsTrue(summary.HasLegacyAxisGap);
            Assert.AreEqual("legacyAxisMissing", summary.ProjectionSource);
        }

        [Test]
        public void ConnectedProjectStoresBaselineAndHeadCheckpoints()
        {
            var project = new ConnectedProject
            {
                Id = "repo-a",
                PathHash = "repo-a",
                FirstConnectedAt = DateTimeOffset.UtcNow.AddDays(-3),
                FirstAnalyzedCommit = "FIRST_COMMIT",
                LastAnalyzedCommit = "BASESHA",
                CurrentHeadCommit = "HEADSHA",
                FirstCommitAt = DateTimeOffset.UtcNow.AddDays(-10).ToString("O"),
                TotalCommitCount = 42,
                AnalyzedCommitRange = "FIRST_COMMIT..HEADSHA",
                LastAnalysisMode = "full-baseline"
            };

            Assert.IsNotNull(project.FirstConnectedAt);
            Assert.AreEqual("FIRST_COMMIT", project.FirstAnalyzedCommit);
            Assert.AreEqual("BASESHA", project.LastAnalyzedCommit);
            Assert.AreEqual("HEADSHA", project.CurrentHeadCommit);
            Assert.AreEqual("FIRST_COMMIT..HEADSHA", project.AnalyzedCommitRange);
        }

        [Test]
        public void GitBaselineDiagnosticsAndRecomputeMarkersExist()
        {
            var flowSource = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "UI", "GitAnalysisFlowController.cs"));
            var viewModelSource = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "UI", "ApprovedActivityAnalysisViewModel.cs"));

            Assert.That(flowSource, Does.Contain("[GrowthSummary][RECOMPUTE]"));
            Assert.That(flowSource, Does.Contain("[GrowthSummary][GIT_BASELINE]"));
            Assert.That(flowSource, Does.Contain("FirstAnalyzedCommit"));
            Assert.That(flowSource, Does.Contain("CurrentHeadCommit"));
            Assert.That(viewModelSource, Does.Contain("[GrowthSummary][GIT_BASELINE]"));
        }

        [Test]
        public void RepositoryGrowthUsesHistoricalEvents()
        {
            var saveData = SaveData.CreateDefault();
            saveData.WorkSessionSummaries.Add(Session("repo-a", "a1", WorkType.Bugfix, CountBucket.Medium, true, false));
            saveData.GrowthHistory.Add(new CharacterGrowthResult { SessionId = "a1", ExpGained = 70, StatDeltas = new CharacterStats { Logic = 1, Debug = 3, Efficiency = 2 } });
            RepositoryCompanionProfileService.RecordTimelineEvent(
                saveData,
                "safe_sync_completed",
                "Safe sync completed",
                "Remote sync completed without conflict.",
                "repo-a",
                "Repo A",
                "safe_sync",
                0,
                0,
                string.Empty,
                string.Empty,
                string.Empty,
                "info",
                string.Empty,
                new CharacterStats { Stability = 1 });

            var summary = RepositoryGrowthSummaryProjection.Build(saveData, "repo-a");

            Assert.IsTrue(summary.HasHistoricalEvents);
            Assert.IsTrue(summary.HasSavedGrowth);
            Assert.Greater(summary.Code, 0);
            Assert.Greater(summary.Debug, 0);
            Assert.Greater(summary.Sync, 0);
        }

        [Test]
        public void RepositoryGrowthProjectionMatchesConnectedProjectAliasesWithoutCrossRepoBleed()
        {
            var saveData = SaveData.CreateDefault();
            saveData.SelectedRepositoryHash = "repo-a-canonical";
            saveData.ConnectedProjects.Add(new ConnectedProject
            {
                Id = "repo-a-connected-project",
                PathHash = "repo-a-canonical",
                ProjectPathHash = "repo-a-normalized-path",
                DisplayName = "TokenForge",
                ApprovedAt = DateTimeOffset.UtcNow,
                IsActive = true
            });
            saveData.ConnectedProjects.Add(new ConnectedProject
            {
                Id = "repo-b-connected-project",
                PathHash = "repo-b-canonical",
                ProjectPathHash = "repo-b-normalized-path",
                DisplayName = "Other",
                ApprovedAt = DateTimeOffset.UtcNow,
                IsActive = false
            });
            saveData.WorkSessionSummaries.Add(Session("repo-a-connected-project", "a1", WorkType.Feature, CountBucket.Small, false, false));
            saveData.WorkSessionSummaries.Add(Session("repo-b-canonical", "b1", WorkType.UIUX, CountBucket.Huge, false, true));
            saveData.GrowthHistory.Add(new CharacterGrowthResult { SessionId = "a1", ExpGained = 42, StatDeltas = new CharacterStats { Logic = 3 } });
            saveData.GrowthHistory.Add(new CharacterGrowthResult { SessionId = "b1", ExpGained = 99, StatDeltas = new CharacterStats { Design = 9 } });

            var summary = RepositoryGrowthSummaryProjection.Build(saveData, "repo-a-canonical");

            Assert.AreEqual(42, summary.TotalXp);
            Assert.AreEqual(3, summary.Code);
            Assert.AreEqual(0, summary.Design);
        }

        [Test]
        public void GrowthProjectionKeepsRepoABSourcesSeparateAndRejectsGlobalPlaceholders()
        {
            var saveData = SaveData.CreateDefault();
            saveData.SelectedRepositoryHash = "repo-a-canonical";
            saveData.CharacterProfile.Stats = new CharacterStats { Logic = 99, Architecture = 99, Velocity = 99, Debug = 99, Design = 99, Stability = 99 };
            saveData.SyncState.PendingQueueCount = 99;
            saveData.ConnectedProjects.Add(new ConnectedProject
            {
                Id = "repo-a-connected",
                PathHash = "repo-a-canonical",
                ProjectPathHash = "repo-a-normalized",
                LocalOnlyProjectId = "repo-a-local",
                DisplayName = "Repo A",
                ApprovedAt = DateTimeOffset.UtcNow,
                IsActive = true
            });
            saveData.ConnectedProjects.Add(new ConnectedProject
            {
                Id = "repo-b-connected",
                PathHash = "repo-b-canonical",
                ProjectPathHash = "repo-b-normalized",
                LocalOnlyProjectId = "repo-b-local",
                DisplayName = "Repo B",
                ApprovedAt = DateTimeOffset.UtcNow,
                IsActive = false
            });
            saveData.WorkSessionSummaries.Add(Session("repo-a-connected", "a-session", WorkType.Feature, CountBucket.Small, false, false));
            saveData.WorkSessionSummaries.Add(Session("repo-b-normalized", "b-session", WorkType.UIUX, CountBucket.Huge, true, true));
            saveData.WorkSessionSummaries.Add(Session("remote-placeholder", "remote-session", WorkType.Refactor, CountBucket.Huge, true, true));
            saveData.GrowthHistory.Add(new CharacterGrowthResult { SessionId = "a-session", ExpGained = 40, StatDeltas = new CharacterStats { Logic = 2 } });
            saveData.GrowthHistory.Add(new CharacterGrowthResult { SessionId = "b-session", ExpGained = 90, StatDeltas = new CharacterStats { Design = 7, Creativity = 3 } });
            saveData.GrowthHistory.Add(new CharacterGrowthResult { SessionId = "remote-session", ExpGained = 999, StatDeltas = new CharacterStats { Logic = 50, Design = 50, Stability = 50 } });
            saveData.RepositoryTimelineEvents.Add(new RepositoryTimelineEvent { RepositoryId = "repo-a-canonical", RepositoryAlias = "Repo A", EventType = "growth_saved", Title = "Repo A growth", Summary = "Repo A code/focus.", DeltaXp = 40, CodeDelta = 3, FocusDelta = 2 });
            saveData.RepositoryTimelineEvents.Add(new RepositoryTimelineEvent { RepositoryId = "repo-b-canonical", RepositoryAlias = "Repo B", EventType = "growth_saved", Title = "Repo B growth", Summary = "Repo B design/sync.", DeltaXp = 90, DesignDelta = 10, SyncDelta = 4 });
            saveData.RecentNativeAnalysisRuns.Add(new NativeAnalysisRunRecord { RepositoryId = "repo-b-canonical", Status = "saved", XpDelta = 90, StatDeltas = new CharacterStats { Debug = 6 } });
            saveData.RecentNativeAnalysisRuns.Add(new NativeAnalysisRunRecord { RepositoryId = "remote-placeholder", Status = "saved", XpDelta = 999, StatDeltas = new CharacterStats { Logic = 50, Debug = 50 } });
            saveData.ActivityReviews.Add(new ActivityReview { RepositoryId = "repo-b-canonical", Status = "saved", XpDelta = 90, CategoryBreakdown = new CharacterStats { Design = 8 } });
            saveData.ActivityReviews.Add(new ActivityReview { RepositoryId = "remote-placeholder", Status = "saved", XpDelta = 999, CategoryBreakdown = new CharacterStats { Stability = 50 } });

            var repoA = RepositoryGrowthSummaryProjection.Build(saveData, "repo-a-canonical");
            var repoB = RepositoryGrowthSummaryProjection.Build(saveData, "repo-b-canonical");

            Assert.AreNotEqual(VectorKey(repoA), VectorKey(repoB));
            Assert.AreEqual("3:2:0:0:0", VectorKey(repoA));
            Assert.AreEqual("0:0:0:10:4", VectorKey(repoB));
            Assert.AreEqual(40, repoA.TotalXp);
            Assert.AreEqual(90, repoB.TotalXp);
            Assert.AreEqual(1, repoA.MatchedSessionCount);
            Assert.AreEqual(1, repoA.MatchedApprovedGrowthCount);
            Assert.AreEqual(1, repoA.MatchedTimelineEventCount);
            Assert.AreEqual(0, repoA.MatchedNativeRunCount);
            Assert.AreEqual(0, repoA.MatchedActivityReviewCount);
            Assert.AreNotEqual(297, repoA.Code);
            Assert.Less(repoA.TotalXp, 999);
        }

        [Test]
        public void TimelineAxisDeltaPreferredOverLegacySessionMetadata()
        {
            var saveData = SaveData.CreateDefault();
            saveData.WorkSessionSummaries.Add(Session("repo-a", "legacy-a", WorkType.UIUX, CountBucket.Huge, true, true));
            saveData.GrowthHistory.Add(new CharacterGrowthResult { SessionId = "legacy-a", ExpGained = 90, StatDeltas = CharacterStats.Zero() });
            RepositoryCompanionProfileService.RecordTimelineEvent(
                saveData,
                "growth_saved",
                "Growth saved",
                "Stored timeline deltas are canonical.",
                "repo-a",
                "Repo A",
                "growth_review",
                90,
                0,
                string.Empty,
                string.Empty,
                string.Empty,
                "success",
                string.Empty,
                new CharacterStats { Logic = 1 });

            var summary = RepositoryGrowthSummaryProjection.Build(saveData, "repo-a");

            Assert.AreEqual("1:0:0:0:0", VectorKey(summary));
            Assert.AreEqual("timelineAxisDelta", summary.ProjectionSource);
            Assert.IsFalse(summary.HasLegacyAxisGap);
        }

        [Test]
        public void SyncOnlyTimelineAxisDoesNotDropSavedGrowthXp()
        {
            var saveData = SaveData.CreateDefault();
            saveData.WorkSessionSummaries.Add(Session("repo-a", "growth-a", WorkType.Feature, CountBucket.Small, false, false));
            saveData.GrowthHistory.Add(new CharacterGrowthResult { SessionId = "growth-a", ExpGained = 77, StatDeltas = new CharacterStats { Logic = 2 } });
            RepositoryCompanionProfileService.RecordTimelineEvent(
                saveData,
                "safe_sync_completed",
                "Safe sync completed",
                "Sync event with explicit sync axis only.",
                "repo-a",
                "Repo A",
                "safe_sync",
                0,
                0,
                string.Empty,
                string.Empty,
                string.Empty,
                "info",
                string.Empty,
                new CharacterStats { Stability = 1 });

            var summary = RepositoryGrowthSummaryProjection.Build(saveData, "repo-a");

            Assert.AreEqual(77, summary.TotalXp);
            Assert.Greater(summary.Code, 0);
            Assert.Greater(summary.Sync, 0);
        }

        [Test]
        public void XpAppliedLifecycleEventDoesNotDoubleCountGrowthSavedAxisDelta()
        {
            var saveData = SaveData.CreateDefault();
            saveData.WorkSessionSummaries.Add(Session("repo-a", "growth-a", WorkType.Feature, CountBucket.Small, false, false));
            saveData.GrowthHistory.Add(new CharacterGrowthResult { SessionId = "growth-a", ExpGained = 90, StatDeltas = new CharacterStats { Logic = 3 } });
            RepositoryCompanionProfileService.RecordTimelineEvent(
                saveData,
                "growth_saved",
                "Growth saved",
                "Canonical saved review axis delta.",
                "repo-a",
                "Repo A",
                "growth_review",
                90,
                0,
                string.Empty,
                string.Empty,
                string.Empty,
                "success",
                string.Empty,
                new CharacterStats { Logic = 3 });
            RepositoryCompanionProfileService.RecordTimelineEvent(
                saveData,
                "xp_applied",
                "XP applied",
                "Lifecycle event without axis delta.",
                "repo-a",
                "Repo A",
                "growth_review",
                90);

            var summary = RepositoryGrowthSummaryProjection.Build(saveData, "repo-a");

            Assert.AreEqual(90, summary.TotalXp);
            Assert.AreEqual("3:0:0:0:0", VectorKey(summary));
            Assert.IsFalse(summary.HasLegacyAxisGap);
        }

        [Test]
        public void GrowthRadarUsesFiveCategoryValues()
        {
            var source = NativeSource();

            Assert.That(source, Does.Contain("@interface TokenForgeGrowthRadarView"));
            Assert.That(source, Does.Contain("configureWithCode"));
            Assert.That(source, Does.Contain("@\"Code\", @\"Focus\", @\"Debug\", @\"Design\", @\"Sync\""));
            Assert.That(source, Does.Contain("hasAxisData"));
            Assert.That(source, Does.Contain("No axis data recorded yet."));
            Assert.That(source, Does.Contain("[GrowthRadar][AXIS_MISSING]"));
            Assert.That(source, Does.Contain("INFO [GrowthRadar] values="));
        }

        [Test]
        public void ActivityTimelinePersistsPerRepository()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName());
            var repository = new SaveDataRepository(directory);
            var saveData = SaveData.CreateDefault();
            RepositoryCompanionProfileService.RecordTimelineEvent(saveData, "growth_saved", "Growth saved", "Repo A growth.", "repo-a", "Repo A", "growth_review", 40);
            RepositoryCompanionProfileService.RecordTimelineEvent(saveData, "growth_saved", "Growth saved", "Repo B growth.", "repo-b", "Repo B", "growth_review", 60);
            RunResultAsync(token => repository.SaveAsync(saveData, token));

            var loaded = RunTaskAsync(token => repository.LoadAsync(token));
            var repoA = RepositoryGrowthSummaryProjection.Build(loaded, "repo-a");

            Assert.AreEqual(1, repoA.TimelineEvents.Count);
            Assert.AreEqual("repo-a", repoA.TimelineEvents.Single().RepositoryId);
            Assert.That(repoA.LatestSummary, Does.Contain("Repo A growth"));
        }

        [Test]
        public void WardrobeLayoutDoesNotPushHeaderIntoContent()
        {
            var source = NativeSource();

            Assert.That(source, Does.Contain("TokenForge.FixedTopShellHeader"));
            Assert.That(source, Does.Contain("if ([self.selectedNavItem isEqualToString:@\"wardrobe\"])"));
            Assert.That(source, Does.Contain("[grid addArrangedSubview:[self wardrobeScreen]]"));
            Assert.That(source, Does.Contain("TokenForge.DashboardTabContent"));
        }

        [Test]
        public void TokenShopAndWardrobeHaveBottomSafeInset()
        {
            var source = NativeSource();

            Assert.That(source, Does.Contain("TokenForgeTabSafeBottomInset"));
            Assert.That(source, Does.Contain("TokenForgeTabContentTopInset"));
            Assert.That(source, Does.Contain("TokenForgeTabContentSideInset"));
            Assert.That(source, Does.Contain("scrollView.contentInsets"));
            Assert.That(source, Does.Contain("TokenForgePinSubview(content, document, TokenForgeTabContentTopInset, TokenForgeTabContentSideInset, TokenForgeTabSafeBottomInset, TokenForgeTabContentSideInset)"));
            Assert.That(source, Does.Contain("[LayoutBounds][BOTTOM_INSET]"));
            Assert.That(source, Does.Contain("[LayoutDiagnostic] selectedTab=%@"));
            Assert.That(source, Does.Contain("dashboardFrame=%@ shellHeaderFrame=fixed92 scrollContentFrame=bodyFill bottomTabFrame=TokenForge.BottomTabBar safeBottomInset=%.0f"));
            Assert.That(source, Does.Contain("visibleContentHeight=%.0f contentBottomY=%.0f bottomTabTopY=%.0f isBottomClipped=%@"));
            Assert.That(source, Does.Contain("isBottomClipped ? @\"true\" : @\"false\""));
            Assert.That(source, Does.Contain("scrollView.contentInsets = NSEdgeInsetsMake(0, 0, TokenForgeTabSafeBottomInset, 0);"));
            Assert.That(source, Does.Contain("tokenShopScreen"));
            Assert.That(source, Does.Contain("wardrobeScreen"));
        }

        [Test]
        public void WardrobePreviewUsesEquippedItemSourceOfTruth()
        {
            var source = NativeSource();

            Assert.That(source, Does.Contain("@property(nonatomic, strong) NSString *equippedItemIds"));
            Assert.That(source, Does.Contain("preview.equippedItemIds = TokenForgeDashboardString(shop, @\"equippedItemIds\", @\"\")"));
            Assert.That(source, Does.Contain("[WardrobePreview][EQUIPPED_LAYER]"));
        }

        [Test]
        public void TokenShopEquipDoesNotCallLegacyOverlayApplySettings()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "AppBootstrapper.cs"));
            var purchaseIndex = source.IndexOf("private async Task PurchaseTokenShopItemFromNativeAsync", StringComparison.Ordinal);
            var equipIndex = source.IndexOf("private async Task EquipTokenShopItemFromNativeAsync", StringComparison.Ordinal);
            Assert.GreaterOrEqual(purchaseIndex, 0);
            Assert.GreaterOrEqual(equipIndex, 0);
            var tokenShopSlice = source.Substring(purchaseIndex, source.IndexOf("private bool IsNativeShopAgentConnected", StringComparison.Ordinal) - purchaseIndex);

            Assert.That(tokenShopSlice, Does.Not.Contain("ApplySettings("));
            Assert.That(tokenShopSlice, Does.Contain("await RefreshAndPublishNativeDashboardAsync();"));
        }

        [Test]
        public void UnityPreviewSpriteKeyUsesSelectedRepositoryCosmetics()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "UI", "CompanionView.cs"));

            Assert.That(source, Does.Contain("DesktopCompanionSettings"));
            Assert.That(source, Does.Contain("EquippedTokenShopItemIds"));
            Assert.That(source, Does.Contain("CurrentRepositoryHash"));
            Assert.That(source, Does.Contain("dashboardPreview"));
            Assert.That(source, Does.Contain("CompanionPixelArtFactory.GetSprite(state, walkFrame, zodiacTypeId, equippedItemIds, repositoryIdentity, previewRole, cosmeticVariant)"));
        }

        [Test]
        public void ZodiacRendererProducesDistinctStageSprites()
        {
            var child = new CompanionState { Stage = CompanionStage.Child, Archetype = CompanionArchetype.Builder, Level = 4 };
            var adult = new CompanionState { Stage = CompanionStage.Adult, Archetype = CompanionArchetype.Builder, Level = 12 };

            var ratChild = CompanionPixelArtFactory.SpriteSignature(child, false, "rat");
            var tigerChild = CompanionPixelArtFactory.SpriteSignature(child, false, "tiger");
            var ratAdult = CompanionPixelArtFactory.SpriteSignature(adult, false, "rat");
            var ratCosmetic = CompanionPixelArtFactory.SpriteSignature(child, false, "rat", new[] { "skin_white_cat" });

            Assert.AreNotEqual(ratChild, tigerChild);
            Assert.AreNotEqual(ratChild, ratAdult);
            Assert.AreNotEqual(ratChild, ratCosmetic);
            Assert.That(ratCosmetic, Does.Contain("sprite:v4:grid24"));
            Assert.That(CompanionPixelArtFactory.SpriteSignature(child, false, "rat", new[] { "Skin White Cat!?", "skin_white_cat" }), Does.Contain("skinwhitecat"));
            Assert.That(File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "UI", "CompanionPixelArtFactory.cs")), Does.Contain("[PixelDiagnostic] signature=sprite:v4:grid24"));
        }

        [Test]
        public void SpriteCacheKeySeparatesRepositoryPreviewRoleAndCosmetics()
        {
            var state = new CompanionState { Stage = CompanionStage.Child, Archetype = CompanionArchetype.Builder, Level = 4 };

            var dashboardRepoA = CompanionPixelArtFactory.SpriteSignature(state, false, "rat", new[] { "skin_white_cat" }, "repo-a", "dashboardPreview", "white_cat");
            var dashboardRepoB = CompanionPixelArtFactory.SpriteSignature(state, false, "rat", new[] { "skin_white_cat" }, "repo-b", "dashboardPreview", "white_cat");
            var overlayRepoA = CompanionPixelArtFactory.SpriteSignature(state, false, "rat", new[] { "skin_white_cat" }, "repo-a", "desktopOverlay", "white_cat");
            var unequippedRepoA = CompanionPixelArtFactory.SpriteSignature(state, false, "rat", Array.Empty<string>(), "repo-a", "dashboardPreview", "default");

            Assert.AreNotEqual(dashboardRepoA, dashboardRepoB);
            Assert.AreNotEqual(dashboardRepoA, overlayRepoA);
            Assert.AreNotEqual(dashboardRepoA, unequippedRepoA);
            Assert.That(dashboardRepoA, Does.Contain("repo=repo-a"));
            Assert.That(dashboardRepoA, Does.Contain("role=dashboardpreview"));
            Assert.That(dashboardRepoA, Does.Contain("variant=white_cat"));
        }

        [Test]
        public void SpriteCacheKeyIncludesSignatureZodiacStageAndEquippedItemsHash()
        {
            var child = new CompanionState { Stage = CompanionStage.Child, Archetype = CompanionArchetype.Builder, Level = 4 };
            var adult = new CompanionState { Stage = CompanionStage.Adult, Archetype = CompanionArchetype.Builder, Level = 12 };
            var first = CompanionPixelArtFactory.SpriteSignature(child, false, "rat", new[] { "skin_white_cat" }, "repo-a", "dashboardPreview", "white_cat");
            var same = CompanionPixelArtFactory.SpriteSignature(child, false, "rat", new[] { "skin_white_cat" }, "repo-a", "dashboardPreview", "white_cat");
            var differentStage = CompanionPixelArtFactory.SpriteSignature(adult, false, "rat", new[] { "skin_white_cat" }, "repo-a", "dashboardPreview", "white_cat");
            var differentItem = CompanionPixelArtFactory.SpriteSignature(child, false, "rat", new[] { "skin_calico" }, "repo-a", "dashboardPreview", "calico");
            var differentZodiac = CompanionPixelArtFactory.SpriteSignature(child, false, "tiger", new[] { "skin_white_cat" }, "repo-a", "dashboardPreview", "white_cat");

            Assert.AreEqual(first, same);
            Assert.AreNotEqual(first, differentStage);
            Assert.AreNotEqual(first, differentItem);
            Assert.AreNotEqual(first, differentZodiac);
            Assert.That(first, Does.Contain("signature=sprite:v4:grid24"));
            Assert.That(first, Does.Contain("zodiac=rat"));
            Assert.That(first, Does.Contain("stage=Child"));
            Assert.That(first, Does.Contain("equippedItemsHash=skinwhitecat"));
            Assert.That(first, Does.Not.Contain("zodiac=default"));
        }

        [Test]
        public void ZodiacCatalogContainsAllTwelveKeysAndExpandableStageMetadata()
        {
            var expected = new[] { "rat", "ox", "tiger", "rabbit", "dragon", "snake", "horse", "goat", "monkey", "rooster", "dog", "pig" };
            var zodiacs = RepositoryCompanionProfileService.GetZodiacCompanionTypes();

            CollectionAssert.AreEquivalent(expected, zodiacs.Select(item => item.Id).ToArray());
            foreach (var zodiac in zodiacs)
            {
                Assert.GreaterOrEqual(zodiac.Stages.Count, 4, zodiac.Id);
                Assert.LessOrEqual(zodiac.Stages.Count, 10, zodiac.Id);
                CollectionAssert.IsSubsetOf(new[] { "egg", "baby", "junior", "adult" }, zodiac.Stages.Select(stage => stage.StageId).ToArray(), zodiac.Id);
                Assert.That(zodiac.SilhouetteHint, Is.Not.Empty, zodiac.Id);
            }
        }

        [Test]
        public void CosmeticEquipStatePersistsAndProjects()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName());
            var repository = new SaveDataRepository(directory);
            var saveData = SaveData.CreateDefault();
            var profile = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, CreateGitRepository("CosmeticsRepo")).Value;
            profile.TokenShop.CurrencyBalance = 1000;
            var purchase = RepositoryCompanionProfileService.PurchaseTokenShopItem(saveData, "skin_white_cat");
            var equip = RepositoryCompanionProfileService.EquipTokenShopItem(saveData, ShopTargetType.RepositoryCompanion, string.Empty, "skin_white_cat", true);
            RunResultAsync(token => repository.SaveAsync(saveData, token));

            var loaded = RunTaskAsync(token => repository.LoadAsync(token));
            var loadedProfile = RepositoryCompanionProfileService.GetSelectedProfile(loaded);

            Assert.IsTrue(purchase.IsSuccess, purchase.ErrorMessage);
            Assert.IsTrue(equip.IsSuccess, equip.ErrorMessage);
            CollectionAssert.Contains(loadedProfile.TokenShop.PurchasedItemIds, "skin_white_cat");
            CollectionAssert.Contains(loadedProfile.TokenShop.EquippedItemIds, "skin_white_cat");
        }

        [Test]
        public void MissingRepoEmptySaveAndMalformedItemsFallbackWithoutCrash()
        {
            Assert.DoesNotThrow(() => RepositoryGrowthSummaryProjection.Build(null, "missing-repo"));
            var saveData = SaveData.CreateDefault();
            var profile = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, CreateGitRepository("MalformedItems")).Value;
            profile.TokenShop.PurchasedItemIds.AddRange(new[] { "skin_white_cat", "skin_white_cat", "", "missing_legacy_item" });
            profile.TokenShop.EquippedItemIds.AddRange(new[] { "missing_legacy_item", "skin_calico", "skin_white_cat", "skin_white_cat" });

            RepositoryCompanionProfileService.Normalize(saveData);
            var normalized = RepositoryCompanionProfileService.GetSelectedProfile(saveData);

            Assert.AreEqual(1, normalized.TokenShop.PurchasedItemIds.Count(id => id == "skin_white_cat"));
            CollectionAssert.DoesNotContain(normalized.TokenShop.PurchasedItemIds, "missing_legacy_item");
            CollectionAssert.Contains(normalized.TokenShop.EquippedItemIds, "skin_white_cat");
            CollectionAssert.DoesNotContain(normalized.TokenShop.EquippedItemIds, "skin_calico");
            CollectionAssert.DoesNotContain(normalized.TokenShop.EquippedItemIds, "missing_legacy_item");
        }

        [Test]
        public void NoRepositoryDashboardShowPathForcesOverlayHidden()
        {
            var source = NativeSource();
            var controllerSource = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "UI", "DesktopCompanionOverlayController.cs"));

            Assert.That(source, Does.Contain("settings.noApprovedRepository"));
            Assert.That(source, Does.Contain("TokenForge_HideAllRepositoryCompanions(explicitTrace.UTF8String)"));
            Assert.That(source, Does.Contain("sourceAction=dashboard.showCompanion"));
            Assert.That(source, Does.Contain("sourceAction=menubar.showCompanion"));
            Assert.That(source, Does.Contain("selectedRepoId=none selectedRepoHash=none approvedRepoCount=0"));
            Assert.That(source, Does.Contain("[OverlayLifecycle][NO_REPOSITORY_HIDE_OVERLAY]"));
            Assert.That(controllerSource, Does.Contain("desiredVisible=false actualVisible=false"));
            Assert.That(controllerSource, Does.Contain("legacyPanelVisible=false farmPanelCount=0 visibleFarmPanelCount=0 persistedFarmSnapshotCount=0"));
            Assert.That(controllerSource, Does.Contain("actual_visibility_mismatch"));
        }

        [Test]
        public void NativeDashboardModelCarriesExplicitAxisAvailability()
        {
            var model = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "Platform", "NativeDashboardModels.cs"));
            var bootstrapper = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "AppBootstrapper.cs"));

            Assert.That(model, Does.Contain("public bool hasGrowthAxisData"));
            Assert.That(model, Does.Contain("public bool hasLegacyGrowthAxisGap"));
            Assert.That(model, Does.Contain("public bool hasAxisData"));
            Assert.That(bootstrapper, Does.Contain("state.hasGrowthAxisData = dashboard.HasGrowthAxisData"));
            Assert.That(bootstrapper, Does.Contain("state.activity.hasAxisData = dashboard.HasGrowthAxisData"));
        }

        [Test]
        public void PixelArtRenderHarness_ZodiacStagesRolesAndPlaceholderSideBlocks()
        {
            var zodiacs = new[] { "rat", "ox", "tiger", "rabbit", "dragon", "snake", "horse", "goat", "monkey", "rooster", "dog", "pig" };
            var roles = new[] { "dashboardMascot", "desktopOverlay", "wardrobePreview", "tokenShopPreview" };
            var states = new[]
            {
                new CompanionState { Stage = CompanionStage.Child, Archetype = CompanionArchetype.Builder, Level = 4 },
                new CompanionState { Stage = CompanionStage.Adult, Archetype = CompanionArchetype.Debugger, Level = 16 }
            };
            var keys = new HashSet<string>(StringComparer.Ordinal);

            foreach (var zodiac in zodiacs)
            {
                foreach (var state in states)
                {
                    foreach (var role in roles)
                    {
                        var key = CompanionPixelArtFactory.SpriteSignature(
                            state,
                            false,
                            zodiac,
                            new[] { "agent_outfit_work_jacket", "effect_soft_glow" },
                            "repo-" + zodiac,
                            role,
                            "equipped");
                        Assert.That(key, Does.Contain("repo=repo-" + zodiac), key);
                        Assert.That(key, Does.Contain("role=" + role.ToLowerInvariant()), key);
                        Assert.That(key, Does.Contain("zodiac=" + zodiac), key);
                        Assert.That(key, Does.Contain("equippedItemsHash=agent_outfit_work_jacket,effect_soft_glow"), key);
                        keys.Add(key);
                    }
                }
            }

            Assert.AreEqual(zodiacs.Length * states.Length * roles.Length, keys.Count);
            var pixelSource = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "UI", "CompanionPixelArtFactory.cs"));
            var nativeSource = NativeSource();
            Assert.That(pixelSource, Does.Contain("sourceOfTruth=CompanionPixelArtFactory.CanonicalAssetKey"));
            Assert.That(pixelSource, Does.Not.Contain("Rect(texture, 5, 12, 7, 15"));
            Assert.That(pixelSource, Does.Not.Contain("Rect(texture, 17, 12, 19, 15"));
            Assert.That(nativeSource, Does.Contain("TokenForgeDrawSharedZodiacSprite"));
            Assert.That(nativeSource, Does.Contain("sourceOfTruth=TokenForgeShopPreviewView.drawZodiacMascot"));
            Assert.That(nativeSource, Does.Not.Contain("fill(bodyX + bodyW - 4, bodyY + 2, 3, bodyH - 3, 'D')"));
        }

        [Test]
        public void PixelArtRenderHarness_NoPlaceholderSideBlocksAcrossTargets()
        {
            var pixelSource = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "UI", "CompanionPixelArtFactory.cs"));
            var nativeSource = NativeSource();

            Assert.That(pixelSource, Does.Contain("sideBlockDetected=false"));
            Assert.That(pixelSource, Does.Contain("bodyShadeMode=contourPattern"));
            Assert.That(nativeSource, Does.Contain("sideBlockDetected=false"));
            Assert.That(nativeSource, Does.Contain("bodyShadeMode=contourPattern"));
            Assert.That(pixelSource, Does.Not.Contain("Pixel(texture, 5, 14, palette.Outline)"));
            Assert.That(pixelSource, Does.Not.Contain("Pixel(texture, 18, 14, palette.Outline)"));
        }

        [Test]
        public void PixelArtStageHarness_SixStagesHaveDistinctVisualSignatures()
        {
            var pixelSource = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "UI", "CompanionPixelArtFactory.cs"));
            var nativeSource = NativeSource();

            Assert.That(pixelSource, Does.Contain("egg-shell-zodiac-mark"));
            Assert.That(pixelSource, Does.Contain("tiny-face-partial-traits"));
            Assert.That(pixelSource, Does.Contain("junior-body-traits"));
            Assert.That(pixelSource, Does.Contain("expanded-silhouette-expression"));
            Assert.That(pixelSource, Does.Contain("adult-crown-complete-traits"));
            Assert.That(pixelSource, Does.Contain("legend-aura-rare-outline"));
            Assert.That(nativeSource, Does.Contain("TokenForgeCompanionStageVisualSignature"));
        }

        [Test]
        public void GrowthSummaryProjectionHarness_RepoScopedInputsRejectLegacyGlobalFallback()
        {
            var saveData = SaveData.CreateDefault();
            saveData.CharacterProfile.Stats = new CharacterStats { Logic = 4, Efficiency = 5, Debug = 2, Design = 2, Stability = 0 };
            saveData.SelectedRepositoryHash = "repo-a";
            saveData.ConnectedProjects.Add(new ConnectedProject
            {
                Id = "repo-a",
                PathHash = "repo-a",
                DisplayName = "TokenForge",
                IsActive = true,
                FirstConnectedAt = DateTimeOffset.UtcNow.AddDays(-6),
                FirstAnalyzedCommit = "A_FIRST",
                CurrentHeadCommit = "A_HEAD"
            });
            saveData.ConnectedProjects.Add(new ConnectedProject
            {
                Id = "repo-b",
                PathHash = "repo-b",
                DisplayName = "OtherRepo",
                FirstConnectedAt = DateTimeOffset.UtcNow.AddDays(-2),
                FirstAnalyzedCommit = "B_FIRST",
                CurrentHeadCommit = "B_HEAD"
            });
            saveData.WorkSessionSummaries.Add(Session("repo-a", "a-session", WorkType.Feature, CountBucket.Small, false, false));
            saveData.WorkSessionSummaries.Last().TokenUsageBucket = TokenUsageBucket.Medium;
            saveData.WorkSessionSummaries.Add(Session("repo-b", "b-session", WorkType.UIUX, CountBucket.Large, false, true));
            saveData.WorkSessionSummaries.Last().TokenUsageBucket = TokenUsageBucket.Huge;
            saveData.GrowthHistory.Add(new CharacterGrowthResult { SessionId = "a-session", ExpGained = 30, StatDeltas = new CharacterStats { Logic = 2, Architecture = 1 } });
            saveData.GrowthHistory.Add(new CharacterGrowthResult { SessionId = "b-session", ExpGained = 45, StatDeltas = new CharacterStats { Design = 5, Creativity = 1 } });
            saveData.RepositoryTimelineEvents.Add(new RepositoryTimelineEvent { RepositoryId = "repo-a", EventType = "growth_saved", Title = "Repo A growth", Summary = "Code axis.", DeltaXp = 30, CodeDelta = 3 });
            saveData.RepositoryTimelineEvents.Add(new RepositoryTimelineEvent { RepositoryId = "repo-b", EventType = "growth_saved", Title = "Repo B growth", Summary = "Design axis.", DeltaXp = 45, DesignDelta = 6 });
            RepositoryCompanionProfileService.RecordTimelineEvent(saveData, "growth_saved", "Legacy growth", "No axis delta.", "repo-legacy", "Legacy", "legacy", 99);

            var repoA = RepositoryGrowthSummaryProjection.Build(saveData, "repo-a");
            var repoB = RepositoryGrowthSummaryProjection.Build(saveData, "repo-b");
            var legacy = RepositoryGrowthSummaryProjection.Build(saveData, "repo-legacy");

            Assert.AreNotEqual(VectorKey(repoA), VectorKey(repoB));
            Assert.AreEqual("3:0:0:0:0", VectorKey(repoA));
            Assert.AreEqual("0:0:0:6:0", VectorKey(repoB));
            Assert.AreEqual("0:0:0:0:0", VectorKey(legacy));
            Assert.IsTrue(legacy.HasLegacyAxisGap);
            Assert.AreEqual("legacyAxisMissing", legacy.ProjectionSource);
            Assert.AreNotEqual("4:5:2:2:0", VectorKey(repoA));
            Assert.AreNotEqual("4:5:2:2:0", VectorKey(repoB));

            var projectionSource = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "Domain", "RepositoryGrowthSummaryProjection.cs"));
            Assert.That(projectionSource, Does.Contain("firstConnectedAt="));
            Assert.That(projectionSource, Does.Contain("firstAnalyzedCommit="));
            Assert.That(projectionSource, Does.Contain("currentHead="));
            Assert.That(projectionSource, Does.Contain("aiTokenUsageCount="));
            Assert.That(projectionSource, Does.Contain("projectionSource="));
        }

        [Test]
        public void GrowthSummaryHarness_FullGitHistoryFromFirstCommit()
        {
            var gitSource = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "Git", "GitAggregateAnalyzer.cs"));
            var projectionSource = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "Domain", "RepositoryGrowthSummaryProjection.cs"));

            Assert.That(gitSource, Does.Contain("rev-list --max-parents=0 HEAD"));
            Assert.That(gitSource, Does.Contain("log --all --numstat"));
            Assert.That(projectionSource, Does.Contain("firstCommit="));
            Assert.That(projectionSource, Does.Contain("commitsAnalyzed="));
            Assert.That(projectionSource, Does.Contain("filesChanged="));
        }

        [Test]
        public void GrowthSummaryHarness_CommitsChangeProjection()
        {
            var saveData = SaveData.CreateDefault();
            saveData.ConnectedProjects.Add(new ConnectedProject { Id = "repo-a", PathHash = "repo-a", ApprovedAt = DateTimeOffset.UtcNow, IsActive = true, FirstCommitHash = "A_FIRST", LastAnalyzedCommit = "A_HEAD_1", CurrentHeadCommit = "A_HEAD_2", TotalCommitCount = 2, FilesChangedAnalyzed = 4 });
            saveData.WorkSessionSummaries.Add(Session("repo-a", "a1", WorkType.Feature, CountBucket.Small, false, false));
            saveData.GrowthHistory.Add(new CharacterGrowthResult { SessionId = "a1", ExpGained = 20, StatDeltas = new CharacterStats { Logic = 1 } });

            var summary = RepositoryGrowthSummaryProjection.Build(saveData, "repo-a");

            Assert.AreEqual("A_FIRST", summary.FirstCommit);
            Assert.AreEqual("A_HEAD_2", summary.CurrentHead);
            Assert.AreEqual(2, summary.CommitsAnalyzed);
            Assert.AreEqual(4, summary.FilesChanged);
            Assert.AreEqual("head_or_analysis_checkpoint_changed", summary.ReasonIfUnchanged);
        }

        [Test]
        public void GrowthSummaryHarness_RejectsFixedFallback_4_5_2_2_0()
        {
            var saveData = SaveData.CreateDefault();
            saveData.CharacterProfile.Stats = new CharacterStats { Logic = 4, Efficiency = 5, Debug = 2, Design = 2, Stability = 0 };
            saveData.WorkSessionSummaries.Add(Session("repo-a", "a1", WorkType.Feature, CountBucket.Small, false, false));
            saveData.GrowthHistory.Add(new CharacterGrowthResult { SessionId = "a1", ExpGained = 20, StatDeltas = new CharacterStats { Logic = 1 } });

            var summary = RepositoryGrowthSummaryProjection.Build(saveData, "repo-a");

            Assert.AreNotEqual("4:5:2:2:0", VectorKey(summary));
            Assert.IsFalse(summary.FallbackUsed);
            Assert.IsFalse(summary.CacheHit);
        }

        [Test]
        public void NativeLayoutHarness_SafeInsetsClippingAndModalBoundsAreDiagnosed()
        {
            var source = NativeSource();

            Assert.That(source, Does.Contain("screen=%@ windowFrame=%@ contentFrame=%@ sidebarFrame=TokenForge.FixedLeftSidebar headerFrame=%@ scrollFrame=%@ contentSize=%@ bottomInset=%.0f dockSafeAreaGuess=%.0f clippedViewCount=%d clippedViewNames=%@"));
            Assert.That(source, Does.Contain("scrollView.contentInsets = NSEdgeInsetsMake(0, 0, TokenForgeTabSafeBottomInset, 0);"));
            Assert.That(source, Does.Contain("TokenForgePinSubview(content, document, TokenForgeTabContentTopInset, TokenForgeTabContentSideInset, TokenForgeTabSafeBottomInset, TokenForgeTabContentSideInset)"));
            Assert.That(source, Does.Contain("tokenShopScreen"));
            Assert.That(source, Does.Contain("wardrobeScreen"));
            Assert.That(source, Does.Contain("TokenForge.Wardrobe.ContentRoot"));
            Assert.That(source, Does.Contain("clippedViewNames"));
        }

        [Test]
        public void LayoutHarness_NoBottomDockClippingAcrossScreens()
        {
            var source = NativeSource();

            Assert.That(source, Does.Contain("[LayoutDiagnostic]"));
            Assert.That(source, Does.Contain("dockSafeAreaGuess"));
            Assert.That(source, Does.Contain("clippedViewCount"));
            Assert.That(source, Does.Contain("isBottomClipped"));
            Assert.That(source, Does.Contain("TokenForgeTabSafeBottomInset"));
        }

        [Test]
        public void OnboardingLayoutHarness_AllStepsFitAndDoNotClip()
        {
            var source = NativeSource();

            Assert.That(source, Does.Contain("[OnboardingLayoutDiagnostic]"));
            Assert.That(source, Does.Contain("stepIndex=%ld"));
            Assert.That(source, Does.Contain("clippedTextCount=0"));
            Assert.That(source, Does.Contain("clippedImageCount=0"));
        }

        [Test]
        public void WardrobeLayoutHarness_NoTopGapNoBottomClip()
        {
            var source = NativeSource();

            Assert.That(source, Does.Contain("[WardrobeLayoutDiagnostic]"));
            Assert.That(source, Does.Contain("topGap=%.0f"));
            Assert.That(source, Does.Contain("bottomClipped=false"));
            Assert.That(source, Does.Contain("TokenForge.Wardrobe.ContentRoot"));
        }

        [Test]
        public void RecentActivityHarness_HidesZeroDeltaSystemNoise()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "AppBootstrapper.cs"));
            var model = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "Platform", "NativeDashboardModels.cs"));

            Assert.That(source, Does.Contain("IsGrowthProducingRecentActivity"));
            Assert.That(source, Does.Contain("diagnostic-only zero delta"));
            Assert.That(source, Does.Contain("HasPositiveCategoryBreakdown"));
            Assert.That(model, Does.Contain("hidesZeroDeltaSystemNoise"));
            Assert.That(model, Does.Contain("deltaReason"));
        }

        [Test]
        public void OverlayStateHarness_CompanionFarmAndControlsStayExplicit()
        {
            var source = NativeSource();
            var controller = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "UI", "DesktopCompanionOverlayController.cs"));
            var service = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "Platform", "MacDesktopCompanionOverlayService.cs"));

            Assert.That(source, Does.Contain("Overlay: Active"));
            Assert.That(source, Does.Contain("Overlay: Disabled"));
            Assert.That(source, Does.Contain("Visible: %@"));
            Assert.That(source, Does.Contain("Movement: %@"));
            Assert.That(source, Does.Contain("Show Overlay"));
            Assert.That(source, Does.Contain("Hide Overlay"));
            Assert.That(source, Does.Contain("Pause Movement"));
            Assert.That(source, Does.Contain("Resume Movement"));
            Assert.That(source, Does.Contain("Disable Drag"));
            Assert.That(source, Does.Contain("Disable Click-through"));
            Assert.That(source, Does.Contain("targetCompanionCount=%ld movingCompanionCount=%ld"));
            Assert.That(controller, Does.Contain("SetCompanionFarmSnapshots"));
            Assert.That(controller, Does.Contain("ShowAllRepositoryCompanions"));
            Assert.That(service, Does.Contain("globalMotionEnabled"));
            Assert.That(service, Does.Contain("movementEnabled"));
        }

        [Test]
        public void OverlayMovementHarness_AllConnectedCompanionsMoveWhenTargetAll()
        {
            var source = NativeSource();
            var model = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "Platform", "NativeDashboardModels.cs"));
            var bootstrapper = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "AppBootstrapper.cs"));

            Assert.That(model, Does.Contain("overlayMode"));
            Assert.That(model, Does.Contain("movementMode"));
            Assert.That(bootstrapper, Does.Contain("\"allConnectedRepos\""));
            Assert.That(source, Does.Contain("connectedRepoCount=%ld targetCompanionCount=%ld movingCompanionCount=%ld overlayMode=%@ movementMode=%@ tickTargetHashes=%@ panelFrames=%@"));
            Assert.That(source, Does.Contain("Target Mode: %@"));
        }

        [Test]
        public void OverlayMovementHarness_SelectedOnlyModeIsExplicit()
        {
            var source = NativeSource();
            var bootstrapper = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "AppBootstrapper.cs"));

            Assert.That(bootstrapper, Does.Contain("\"selectedRepoCompanion\""));
            Assert.That(source, Does.Contain("selectedRepoCompanion"));
            Assert.That(source, Does.Contain("selected repo"));
        }

        [Test]
        public void StatusBarWindowHarness_HeaderMenuAndStateDiagnosticsPersist()
        {
            var source = NativeSource();

            Assert.That(source, Does.Contain("TokenForge.FixedTopShellHeader"));
            Assert.That(source, Does.Contain("TokenForge.PersistentStatusBar"));
            Assert.That(source, Does.Contain("[StatusBarDiagnostic] activeScreen=%@ selectedRepoHash=%@ aiAgentCount=%ld overlayState=%@ syncMode=%@ activeMascot=%@ headerFrame=fixed92 statusItemExists=%@ windowMenuAction=Dashboard/ShowOverlay/HideOverlay/Settings/Quit nativeStateSynced=%@"));
            Assert.That(source, Does.Contain("persistent-status-dashboard-action"));
            Assert.That(source, Does.Contain("persistent-status-overlay-action"));
            Assert.That(source, Does.Contain("persistent-status-settings-action"));
            Assert.That(source, Does.Contain("verifyPersistentStatusBarForContext"));
            Assert.That(source, Does.Contain("[[self.statusItem.menu itemWithTag:1017] setTitle:TokenForgeMenuMovementEnabled ? @\"Pause Movement\" : @\"Resume Movement\""));
            Assert.That(source, Does.Contain("showTokenForgeFromStatusItem"));
            Assert.That(source, Does.Contain("hideTokenForgeFromStatusItem"));
            Assert.That(source, Does.Contain("enableDesktopCompanionFromStatusItem"));
            Assert.That(source, Does.Contain("disableDesktopCompanionFromStatusItem"));
        }

        [Test]
        public void StatusBarHarness_BadgesAndWindowMenuActionsSyncState()
        {
            var source = NativeSource();

            Assert.That(source, Does.Contain("[StatusBarDiagnostic]"));
            Assert.That(source, Does.Contain("activeScreen=%@"));
            Assert.That(source, Does.Contain("selectedRepoHash=%@"));
            Assert.That(source, Does.Contain("aiAgentCount=%ld"));
            Assert.That(source, Does.Contain("overlayState=%@"));
            Assert.That(source, Does.Contain("windowMenuAction=Dashboard/ShowOverlay/HideOverlay/Settings/Quit"));
            Assert.That(source, Does.Contain("nativeStateSynced=%@"));
        }

        private static string NativeSource()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var nativeSourcePath = Path.Combine(projectRoot, "UnityClient", "Assets", "Plugins", "macOS", "DesktopCompanionOverlay.mm");
            return File.ReadAllText(nativeSourcePath);
        }

        private static AgentWorkSession Session(string repositoryId, string sessionId, WorkType workType, CountBucket changedFiles, bool testFileChanged, bool uiFileChanged)
        {
            return new AgentWorkSession
            {
                SessionId = sessionId,
                WorkType = workType,
                StartedAt = DateTimeOffset.UtcNow.AddHours(-2),
                EndedAt = DateTimeOffset.UtcNow.AddHours(-1),
                GitChangeSummary = new GitChangeSummary
                {
                    ProjectPathHash = repositoryId,
                    ChangedFileCountBucket = changedFiles,
                    CommitCountBucket = CountBucket.One,
                    AddedLineBucket = LineChangeBucket.Medium,
                    TestFileChanged = testFileChanged,
                    UiFileChanged = uiFileChanged,
                    FileCategoryCounts = new List<FileCategoryCount>
                    {
                        new FileCategoryCount { Category = testFileChanged ? FileCategory.Test : FileCategory.Domain, Count = 1 },
                        new FileCategoryCount { Category = uiFileChanged ? FileCategory.UI : FileCategory.Config, Count = 1 }
                    }
                },
                ActionSummary = new AgentActionSummary
                {
                    TestRunCount = testFileChanged ? 1 : 0,
                    FailedCommandCount = testFileChanged ? 1 : 0,
                    BuildRunCount = 1
                }
            };
        }

        private static string VectorKey(RepositoryGrowthSummary summary)
        {
            return summary.Code + ":" + summary.Focus + ":" + summary.Debug + ":" + summary.Design + ":" + summary.Sync;
        }

        private static string CreateGitRepository(string name)
        {
            var path = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName(), name);
            Directory.CreateDirectory(Path.Combine(path, ".git"));
            return path;
        }

        private static T RunResultAsync<T>(Func<CancellationToken, Task<Result<T>>> action)
        {
            var result = Task.Run(() => action(CancellationToken.None)).GetAwaiter().GetResult();
            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            return result.Value;
        }

        private static void RunResultAsync(Func<CancellationToken, Task<Result>> action)
        {
            var result = Task.Run(() => action(CancellationToken.None)).GetAwaiter().GetResult();
            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        }

        private static T RunTaskAsync<T>(Func<CancellationToken, Task<T>> action)
        {
            return Task.Run(() => action(CancellationToken.None)).GetAwaiter().GetResult();
        }
    }
}
