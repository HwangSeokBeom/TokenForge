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
            Assert.That(ratCosmetic, Does.Contain("sprite:v3:grid24"));
            Assert.That(CompanionPixelArtFactory.SpriteSignature(child, false, "rat", new[] { "Skin White Cat!?", "skin_white_cat" }), Does.Contain("skinwhitecat"));
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

            Assert.That(source, Does.Contain("settings.noApprovedRepository"));
            Assert.That(source, Does.Contain("TokenForge_HideAllRepositoryCompanions(explicitTrace.UTF8String)"));
            Assert.That(source, Does.Contain("sourceAction=dashboard.showCompanion"));
            Assert.That(source, Does.Contain("sourceAction=menubar.showCompanion"));
            Assert.That(source, Does.Contain("selectedRepoId=none selectedRepoHash=none approvedRepoCount=0"));
            Assert.That(source, Does.Contain("[OverlayLifecycle][NO_REPOSITORY_HIDE_OVERLAY]"));
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
