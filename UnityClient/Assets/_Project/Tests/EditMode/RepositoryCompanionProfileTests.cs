using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TokenForge.Client.Domain;
using TokenForge.Client.Privacy;
using TokenForge.Client.Sync;

namespace TokenForge.Client.Tests
{
    public sealed class RepositoryCompanionProfileTests
    {
        [Test]
        public void FreshSaveDoesNotCreateConnectedLocalRepository()
        {
            var saveData = SaveData.CreateDefault();

            RepositoryCompanionProfileService.Normalize(saveData);

            Assert.AreEqual(0, saveData.RepositoryCompanionProfiles.Count(profile => profile.ArchivedAtUtc == null));
            Assert.IsTrue(string.IsNullOrWhiteSpace(saveData.SelectedRepositoryHash));
        }

        [Test]
        public void ConnectingRepositoryCreatesProfileWithHashAndSafeAlias()
        {
            var saveData = SaveData.CreateDefault();
            var repo = CreateGitRepository("TokenForgeCoreServer");

            var result = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, repo);

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            Assert.IsNotEmpty(result.Value.RepositoryHash);
            Assert.AreEqual("TokenForgeCoreServer", result.Value.SafeRepositoryAlias);
            Assert.AreEqual(result.Value.RepositoryHash, saveData.SelectedRepositoryHash);
            Assert.IsFalse(result.Value.SafeRepositoryAlias.Contains(repo));
            Assert.IsTrue(new PrivacySanitizer().ValidateSafeSaveData(saveData).IsSuccess);
        }

        [Test]
        public void ConnectingRepositoryImmediatelyCommitsConnectionAndActiveSelection()
        {
            var saveData = SaveData.CreateDefault();
            var repo = CreateGitRepository("TokenForge");

            var result = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, repo);

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            Assert.AreEqual(result.Value.RepositoryHash, saveData.SelectedRepositoryHash);
            Assert.AreEqual(1, saveData.ConnectedProjects.Count(project => !project.IsArchived));
            Assert.IsTrue(saveData.ConnectedProjects.Single().IsActive);
            Assert.AreEqual(result.Value.RepositoryHash, saveData.ConnectedProjects.Single().Id);
        }

        [Test]
        public void LegacyLocalRepositoryMigrationArchivesFallbackAndClearsActiveSelection()
        {
            var saveData = SaveData.CreateDefault();
            saveData.SelectedRepositoryHash = RepositoryCompanionProfileService.DefaultLocalRepositoryHash;
            saveData.RepositoryCompanionProfiles.Add(new RepositoryCompanionProfile
            {
                RepositoryHash = RepositoryCompanionProfileService.DefaultLocalRepositoryHash,
                SafeRepositoryAlias = "Local Repository",
                ConnectionSource = "debugFallback",
                ApprovedAtUtc = null
            });
            saveData.ConnectedProjects.Add(new ConnectedProject
            {
                DisplayName = "Local Repository",
                ConnectionSource = "userSelected",
                IsActive = true
            });

            RepositoryCompanionProfileService.Normalize(saveData);

            Assert.AreEqual(0, saveData.RepositoryCompanionProfiles.Count(profile => profile.ArchivedAtUtc == null));
            Assert.IsTrue(string.IsNullOrWhiteSpace(saveData.SelectedRepositoryHash));
            Assert.IsTrue(saveData.ConnectedProjects.All(project => !project.IsActive));
            Assert.IsTrue(saveData.ConnectedProjects.All(project => project.IsArchived));
        }

        [Test]
        public void TokenForgeNamedRepositoryWithoutUserApprovalIsNotActive()
        {
            var saveData = SaveData.CreateDefault();
            saveData.SelectedRepositoryHash = "tokenforge-auto";
            saveData.RepositoryCompanionProfiles.Add(new RepositoryCompanionProfile
            {
                RepositoryHash = "tokenforge-auto",
                SafeRepositoryAlias = "TokenForge",
                ConnectionSource = "userSelected",
                ApprovedAtUtc = null,
                CompanionState = new CompanionState { TotalLifetimeXp = 1200, CurrentXp = 1200 }
            });

            RepositoryCompanionProfileService.Normalize(saveData);

            Assert.AreEqual(0, saveData.RepositoryCompanionProfiles.Count(profile => profile.ArchivedAtUtc == null));
            Assert.IsTrue(string.IsNullOrWhiteSpace(saveData.SelectedRepositoryHash));
        }

        [Test]
        public void SameRepositorySelectsExistingProfileAndDifferentRepositoryCreatesDifferentProfile()
        {
            var saveData = SaveData.CreateDefault();
            var first = CreateGitRepository();
            var second = CreateGitRepository();

            var firstResult = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, first);
            var repeatResult = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, first);
            var secondResult = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, second);

            Assert.AreEqual(firstResult.Value.RepositoryHash, repeatResult.Value.RepositoryHash);
            Assert.AreNotEqual(firstResult.Value.RepositoryHash, secondResult.Value.RepositoryHash);
            Assert.AreEqual(2, saveData.RepositoryCompanionProfiles.Count);
        }

        [Test]
        public void ReconnectingSameCanonicalPathRestoresExistingCompanionState()
        {
            var saveData = SaveData.CreateDefault();
            var repository = CreateGitRepository("TokenForge");
            var first = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, repository).Value;
            first.CompanionState = CompanionProgressionRules.Normalize(new CompanionState
            {
                Stage = CompanionStage.Junior,
                Level = 4,
                CurrentXp = 420,
                TotalXp = 1200,
                TotalLifetimeXp = 1200
            });

            var restored = RepositoryCompanionProfileService.SelectOrCreateProfile(
                saveData,
                repository + Path.DirectorySeparatorChar).Value;

            Assert.AreEqual(first.RepositoryHash, restored.RepositoryHash);
            Assert.AreEqual(1, saveData.RepositoryCompanionProfiles.Count);
            Assert.AreEqual(CompanionStage.Junior, restored.CompanionState.Stage);
            Assert.AreEqual(4, restored.CompanionState.Level);
            Assert.Greater(restored.CompanionState.CurrentXp, 0);
        }

        [Test]
        public void ReconnectingLegacyIdentityMigratesAndPreservesJuniorProfile()
        {
            var saveData = SaveData.CreateDefault();
            var repository = CreateGitRepository("TokenForge");
            var canonical = RepositoryCompanionProfileService.CanonicalRepositoryPathForIdentity(repository);
            var legacyHash = SafeHashUtility.ComputeProjectPathHash(canonical);
            saveData.SelectedRepositoryHash = legacyHash;
            saveData.RepositoryCompanionProfiles.Add(new RepositoryCompanionProfile
            {
                RepositoryHash = legacyHash,
                SafeRepositoryAlias = "TokenForge",
                ApprovedAtUtc = DateTimeOffset.UtcNow,
                ConnectionSource = "userSelected",
                CompanionState = new CompanionState
                {
                    Stage = CompanionStage.Junior,
                    Level = 4,
                    CurrentXp = 420,
                    TotalXp = 1200,
                    TotalLifetimeXp = 1200
                }
            });

            var restored = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, repository).Value;

            Assert.AreEqual(RepositoryCompanionProfileService.HashRepositoryPath(repository), restored.RepositoryHash);
            Assert.AreEqual(1, saveData.RepositoryCompanionProfiles.Count);
            Assert.AreEqual(CompanionStage.Junior, restored.CompanionState.Stage);
            Assert.AreEqual(4, restored.CompanionState.Level);
            Assert.AreEqual(restored.RepositoryHash, saveData.SelectedRepositoryHash);
            Assert.IsTrue(RepositoryCompanionProfileService.IsConnectedRepository(saveData, restored.RepositoryHash));
        }

        [Test]
        public void MultipleRepositoriesEachHaveExactlyOneActiveCompanion()
        {
            var saveData = SaveData.CreateDefault();
            var repositories = new[] { CreateGitRepository(), CreateGitRepository(), CreateGitRepository() };

            foreach (var repository in repositories)
            {
                RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, repository);
            }

            var activeProfiles = saveData.RepositoryCompanionProfiles.Where(profile => profile.ArchivedAtUtc == null).ToList();
            Assert.AreEqual(3, activeProfiles.Count);
            Assert.AreEqual(3, activeProfiles.Select(profile => profile.RepositoryHash).Distinct().Count());
            Assert.IsTrue(activeProfiles.All(profile => profile.CompanionState != null));
        }

        [Test]
        public void DisconnectArchivesCompanionInsteadOfDeletingByDefault()
        {
            var saveData = SaveData.CreateDefault();
            var first = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, CreateGitRepository()).Value;
            var second = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, CreateGitRepository()).Value;

            var result = RepositoryCompanionProfileService.RemoveProfile(saveData, first.RepositoryHash);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(first.ArchivedAtUtc);
            Assert.AreEqual(second.RepositoryHash, saveData.SelectedRepositoryHash);
            Assert.Contains(first, saveData.RepositoryCompanionProfiles);
        }

        [Test]
        public void ApprovedGrowthUpdatesOnlySelectedRepositoryCompanion()
        {
            var saveData = SaveData.CreateDefault();
            var repoA = CreateGitRepository();
            var repoB = CreateGitRepository();
            var profileA = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, repoA).Value;
            var profileB = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, repoB).Value;
            saveData.SelectedRepositoryHash = profileA.RepositoryHash;
            var session = SessionFor(profileA.RepositoryHash, "session-a");
            var growth = GrowthFor("session-a", 320);
            saveData.WorkSessionSummaries.Add(session);
            saveData.GrowthHistory.Add(growth);

            RepositoryCompanionProfileService.ApplyApprovedGrowth(saveData, session, new[] { session }, new[] { growth });

            Assert.Greater(profileA.CompanionState.TotalXp, 0);
            Assert.AreEqual(0, profileB.CompanionState.TotalXp);
        }

        [Test]
        public void CompanionLevelUpConsumesOneLevelRequirementAndCarriesOverflow()
        {
            var state = CompanionProgressionRules.Normalize(new CompanionState
            {
                Level = 2,
                CurrentXp = 1200,
                TotalLifetimeXp = 1200
            });

            Assert.IsTrue(state.CanLevelUp);
            Assert.AreEqual(419, state.XpRequiredForNextLevel);
            Assert.IsTrue(CompanionProgressionRules.TryLevelUpOnce(state));

            Assert.AreEqual(3, state.Level);
            Assert.AreEqual(781, state.CurrentXp);
        }

        [Test]
        public void MotionIntensityIncreasesWithGitActivity()
        {
            var low = CompanionMotionStateResolver.Resolve(new CompanionMotionSignal
            {
                RecentGitChangedFiles = CountBucket.One,
                AddedLines = LineChangeBucket.Small
            });
            var high = CompanionMotionStateResolver.Resolve(new CompanionMotionSignal
            {
                RecentGitChangedFiles = CountBucket.Large,
                CommitCount = CountBucket.Large,
                AddedLines = LineChangeBucket.Huge,
                RecentRepositoryXp = 500
            });

            Assert.Less(low.MovementSpeed, high.MovementSpeed);
            Assert.AreEqual(CompanionActivityLevel.High, high.ActivityLevel);
        }

        [Test]
        public void MotionIntensityRespondsToAiTokenActivityAndPendingReview()
        {
            var ai = CompanionMotionStateResolver.Resolve(new CompanionMotionSignal
            {
                AiAgentSessionCount = CountBucket.Medium,
                AiAgentInteractionCount = CountBucket.Large,
                EstimatedTokenActivity = TokenUsageBucket.Huge,
                AiAgentXp = 400
            });
            var pending = CompanionMotionStateResolver.Resolve(new CompanionMotionSignal { HasPendingReview = true });

            Assert.Greater(ai.PulseFrequency, 0.9f);
            Assert.IsTrue(ai.Reaction == CompanionMotionReaction.TokenPulse || ai.Reaction == CompanionMotionReaction.AiPulse);
            Assert.AreEqual(CompanionMotionReaction.ReadyToReview, pending.Reaction);
        }

        [Test]
        public void MotionIntensityPrioritizesLevelUpReady()
        {
            var ready = CompanionMotionStateResolver.Resolve(new CompanionMotionSignal { CanLevelUp = true });

            Assert.AreEqual(CompanionActivityLevel.ReadyToEvolve, ready.ActivityLevel);
            Assert.AreEqual(CompanionMotionReaction.EvolvePulse, ready.Reaction);
        }

        [Test]
        public void PendingReviewDoesNotMutatePersistedCompanion()
        {
            var saveData = SaveData.CreateDefault();
            var repo = CreateGitRepository();
            var profile = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, repo).Value;
            var before = profile.CompanionState.TotalXp;
            var pending = SessionFor(profile.RepositoryHash, "pending");

            Assert.IsNotNull(pending);
            Assert.AreEqual(before, profile.CompanionState.TotalXp);
            Assert.AreEqual(0, saveData.GrowthHistory.Count);
        }

        [Test]
        public void ApprovedGrowthWithoutConnectedRepositoryDoesNotCreateDefaultCompanion()
        {
            var saveData = SaveData.CreateDefault();
            var session = SessionFor("repo-hash", "session-a");
            var growth = GrowthFor("session-a", 500);

            var profile = RepositoryCompanionProfileService.ApplyApprovedGrowth(saveData, session, new[] { session }, new[] { growth });

            Assert.IsNull(profile);
            Assert.AreEqual(0, saveData.RepositoryCompanionProfiles.Count(item => item.ArchivedAtUtc == null));
            Assert.IsTrue(string.IsNullOrWhiteSpace(saveData.SelectedRepositoryHash));
            Assert.AreEqual(0, saveData.CompanionState.TotalLifetimeXp);
        }

        [Test]
        public void TokenUsageAddsForgeCoinsButDoesNotIncreaseCompanionLevel()
        {
            var saveData = SaveData.CreateDefault();
            var profile = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, CreateGitRepository()).Value;
            var aiSession = SessionFor(profile.RepositoryHash, "ai-session");
            aiSession.SourceProvider = "Codex";
            aiSession.AgentActivitySummary = new AgentActivitySummary { ProviderType = AgentProviderType.Codex };
            aiSession.TokenUsageBucket = TokenUsageBucket.Huge;
            var aiGrowth = GrowthFor("ai-session", 900);

            RepositoryCompanionProfileService.ApplyApprovedGrowth(saveData, aiSession, new[] { aiSession }, new[] { aiGrowth });

            Assert.AreEqual(0, profile.CompanionState.TotalLifetimeXp);
            Assert.AreEqual(1, profile.CompanionState.Level);
            Assert.Greater(profile.TokenShop.CurrencyBalance, 0);
            Assert.AreEqual("Forge Coins", profile.TokenShop.CurrencyName);
        }

        [Test]
        public void TokenShopPurchasePersistsItemAndDebitsBalance()
        {
            var saveData = SaveData.CreateDefault();
            var profile = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, CreateGitRepository()).Value;
            profile.TokenShop.CurrencyBalance = 6;

            var result = RepositoryCompanionProfileService.PurchaseTokenShopItem(saveData, "skin_white_cat");

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            Assert.AreEqual(3, profile.TokenShop.CurrencyBalance);
            CollectionAssert.Contains(profile.TokenShop.PurchasedItemIds, "skin_white_cat");
            CollectionAssert.Contains(profile.TokenShop.EquippedItemIds, "skin_white_cat");
            Assert.AreEqual("white_cat", profile.DesktopCompanionSettings.VisualThemeId);
        }

        [Test]
        public void TokenShopPurchaseWithInsufficientTokensDoesNotChangeBalance()
        {
            var saveData = SaveData.CreateDefault();
            var profile = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, CreateGitRepository()).Value;
            profile.TokenShop.CurrencyBalance = 2;

            var result = RepositoryCompanionProfileService.PurchaseTokenShopItem(saveData, "skin_calico");

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("insufficient_tokens", result.ErrorCode);
            Assert.AreEqual(2, profile.TokenShop.CurrencyBalance);
            CollectionAssert.DoesNotContain(profile.TokenShop.PurchasedItemIds, "skin_calico");
        }

        [Test]
        public void TokenShopOwnedOneTimeItemCannotBePurchasedTwiceOrGoNegative()
        {
            var saveData = SaveData.CreateDefault();
            var profile = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, CreateGitRepository()).Value;
            profile.TokenShop.CurrencyBalance = 3;

            var first = RepositoryCompanionProfileService.PurchaseTokenShopItem(saveData, "skin_white_cat");
            var second = RepositoryCompanionProfileService.PurchaseTokenShopItem(saveData, "skin_white_cat");

            Assert.IsTrue(first.IsSuccess, first.ErrorMessage);
            Assert.IsFalse(second.IsSuccess);
            Assert.AreEqual("shop_item_already_owned", second.ErrorCode);
            Assert.AreEqual(0, profile.TokenShop.CurrencyBalance);
            Assert.AreEqual(1, profile.TokenShop.PurchasedItemIds.Count(id => id == "skin_white_cat"));
        }

        [Test]
        public void TokenShopCatalogContainsExpandedCosmeticCategories()
        {
            var catalog = RepositoryCompanionProfileService.GetTokenShopCatalog();

            CollectionAssert.Contains(catalog.Select(item => item.Category).ToArray(), ShopItemCategory.Skins);
            CollectionAssert.Contains(catalog.Select(item => item.Category).ToArray(), ShopItemCategory.Accessories);
            CollectionAssert.Contains(catalog.Select(item => item.Category).ToArray(), ShopItemCategory.Effects);
            CollectionAssert.Contains(catalog.Select(item => item.Category).ToArray(), ShopItemCategory.Motions);
            CollectionAssert.Contains(catalog.Select(item => item.Category).ToArray(), ShopItemCategory.Themes);
            CollectionAssert.Contains(catalog.Select(item => item.Category).ToArray(), ShopItemCategory.TokenEffects);
            CollectionAssert.Contains(catalog.Select(item => item.Category).ToArray(), ShopItemCategory.Badges);
            Assert.IsTrue(catalog.Any(item => item.TargetType == ShopTargetType.AiAgent));
            Assert.IsTrue(catalog.Any(item => item.CompatibleAgentIds.Count == 1));
            Assert.IsTrue(catalog.Any(item => !string.IsNullOrWhiteSpace(item.ZodiacTypeId)));
            Assert.IsFalse(catalog.Any(item => item.PreviewIcon == "WC" || item.PreviewIcon == "CA" || item.PreviewIcon == "CB"));
        }

        [Test]
        public void TokenShopCatalogContainsTwelveZodiacTypesWithExclusiveItems()
        {
            var zodiacs = RepositoryCompanionProfileService.GetZodiacCompanionTypes();
            var catalog = RepositoryCompanionProfileService.GetTokenShopCatalog();

            Assert.AreEqual(12, zodiacs.Count);
            CollectionAssert.AreEquivalent(new[] { "rat", "ox", "tiger", "rabbit", "dragon", "snake", "horse", "goat", "monkey", "rooster", "dog", "pig" }, zodiacs.Select(item => item.Id).ToArray());
            foreach (var zodiac in zodiacs)
            {
                Assert.IsTrue(catalog.Any(item => string.Equals(item.ZodiacTypeId, zodiac.Id, StringComparison.Ordinal)), zodiac.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(zodiac.KoreanName), zodiac.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(zodiac.SilhouetteHint), zodiac.Id);
            }
        }

        [Test]
        public void TokenShopEquipOwnedItemSucceedsAndUnownedFailsSafely()
        {
            var saveData = SaveData.CreateDefault();
            var profile = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, CreateGitRepository()).Value;
            profile.TokenShop.CurrencyBalance = 6;
            var purchase = RepositoryCompanionProfileService.PurchaseTokenShopItem(saveData, "skin_white_cat");

            var equipOwned = RepositoryCompanionProfileService.EquipTokenShopItem(saveData, ShopTargetType.RepositoryCompanion, string.Empty, "skin_white_cat", true);
            var equipUnowned = RepositoryCompanionProfileService.EquipTokenShopItem(saveData, ShopTargetType.RepositoryCompanion, string.Empty, "skin_calico", true);

            Assert.IsTrue(purchase.IsSuccess, purchase.ErrorMessage);
            Assert.IsTrue(equipOwned.IsSuccess, equipOwned.ErrorMessage);
            Assert.IsFalse(equipUnowned.IsSuccess);
            Assert.AreEqual("shop_item_not_owned", equipUnowned.ErrorCode);
            CollectionAssert.DoesNotContain(profile.TokenShop.EquippedItemIds, "skin_calico");
        }

        [Test]
        public void TokenShopAgentPurchasePersistsUnderStableAgentIdOnly()
        {
            var saveData = SaveData.CreateDefault();
            var profile = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, CreateGitRepository()).Value;
            profile.TokenShop.CurrencyBalance = 12;
            var codexShop = RepositoryCompanionProfileService.GetOrCreateAgentShopState(saveData, "Codex").TokenShop;
            codexShop.CurrencyBalance = 12;

            var result = RepositoryCompanionProfileService.PurchaseTokenShopItem(saveData, ShopTargetType.AiAgent, "Codex", "agent_skin_codex_terminal", true);

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            Assert.AreEqual(12, profile.TokenShop.CurrencyBalance);
            CollectionAssert.DoesNotContain(profile.TokenShop.PurchasedItemIds, "agent_skin_codex_terminal");
            var agentShop = RepositoryCompanionProfileService.GetAgentTokenShopState(saveData, "codex");
            Assert.AreEqual(8, agentShop.CurrencyBalance);
            CollectionAssert.Contains(agentShop.PurchasedItemIds, "agent_skin_codex_terminal");
            CollectionAssert.Contains(agentShop.EquippedItemIds, "agent_skin_codex_terminal");
        }

        [Test]
        public void TokenShopAgentPurchaseDebitsOnlySelectedAgentCurrency()
        {
            var saveData = SaveData.CreateDefault();
            RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, CreateGitRepository()).Value.TokenShop.CurrencyBalance = 50;
            RepositoryCompanionProfileService.GetOrCreateAgentShopState(saveData, "codex").TokenShop.CurrencyBalance = 12;
            RepositoryCompanionProfileService.GetOrCreateAgentShopState(saveData, "claudeCode").TokenShop.CurrencyBalance = 9;

            var result = RepositoryCompanionProfileService.PurchaseTokenShopItem(saveData, ShopTargetType.AiAgent, "codex", "agent_skin_codex_terminal", true);

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            Assert.AreEqual(8, RepositoryCompanionProfileService.GetAgentTokenShopState(saveData, "codex").CurrencyBalance);
            Assert.AreEqual(9, RepositoryCompanionProfileService.GetAgentTokenShopState(saveData, "claudeCode").CurrencyBalance);
            Assert.AreEqual(50, RepositoryCompanionProfileService.GetSelectedProfile(saveData).TokenShop.CurrencyBalance);
            CollectionAssert.DoesNotContain(RepositoryCompanionProfileService.GetAgentTokenShopState(saveData, "claudeCode").PurchasedItemIds, "agent_skin_codex_terminal");
        }

        [Test]
        public void TokenShopAgentCurrencyAccruesFromThatAgentsTokenUsage()
        {
            var saveData = SaveData.CreateDefault();
            saveData.WorkSessionSummaries.Add(new AgentWorkSession
            {
                TokenUsageBucket = TokenUsageBucket.Medium,
                AgentActivitySummary = new AgentActivitySummary { ProviderType = AgentProviderType.Codex }
            });
            saveData.WorkSessionSummaries.Add(new AgentWorkSession
            {
                TokenUsageBucket = TokenUsageBucket.Large,
                AgentActivitySummary = new AgentActivitySummary { ProviderType = AgentProviderType.ClaudeCode }
            });

            RepositoryCompanionProfileService.Normalize(saveData);

            Assert.AreEqual(3, RepositoryCompanionProfileService.GetAgentTokenShopState(saveData, "codex").CurrencyBalance);
            Assert.AreEqual(6, RepositoryCompanionProfileService.GetAgentTokenShopState(saveData, "claudeCode").CurrencyBalance);
            Assert.AreEqual("Codex Coins", RepositoryCompanionProfileService.GetAgentTokenShopState(saveData, "codex").CurrencyName);
        }

        [Test]
        public void TokenShopRepositoryPurchaseDoesNotLeakIntoAiAgentOwnership()
        {
            var saveData = SaveData.CreateDefault();
            var profile = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, CreateGitRepository()).Value;
            profile.TokenShop.CurrencyBalance = 6;

            var result = RepositoryCompanionProfileService.PurchaseTokenShopItem(saveData, "skin_white_cat");

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            var agentShop = RepositoryCompanionProfileService.GetAgentTokenShopState(saveData, "codex");
            CollectionAssert.DoesNotContain(agentShop.PurchasedItemIds, "skin_white_cat");
        }

        [Test]
        public void TokenShopDisconnectedAiAgentCannotPurchaseOrEquip()
        {
            var saveData = SaveData.CreateDefault();
            var profile = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, CreateGitRepository()).Value;
            profile.TokenShop.CurrencyBalance = 20;

            var purchase = RepositoryCompanionProfileService.PurchaseTokenShopItem(saveData, ShopTargetType.AiAgent, "codex", "agent_skin_codex_terminal", false);
            var equip = RepositoryCompanionProfileService.EquipTokenShopItem(saveData, ShopTargetType.AiAgent, "codex", "agent_skin_codex_terminal", false);

            Assert.IsFalse(purchase.IsSuccess);
            Assert.AreEqual("agent_not_connected", purchase.ErrorCode);
            Assert.IsFalse(equip.IsSuccess);
            Assert.AreEqual("agent_not_connected", equip.ErrorCode);
            Assert.AreEqual(20, profile.TokenShop.CurrencyBalance);
            Assert.AreEqual(0, saveData.AiAgentShopStates.Count);
        }

        [Test]
        public void EvolutionBiasUsesDominantAndSecondaryGitStats()
        {
            var bias = CompanionEvolutionPathResolver.Resolve(new CompanionStatProfile
            {
                DebugStat = 8,
                CodeStat = 5,
                FocusStat = 1
            }, CompanionStage.Egg);

            Assert.AreEqual(CompanionGrowthStat.Debug, bias.DominantStat);
            Assert.AreEqual(CompanionGrowthStat.Code, bias.SecondaryStat);
            StringAssert.Contains("Debug", bias.CurrentBias);
            StringAssert.Contains("Scanner", bias.NextEvolutionPreview);
        }

        [Test]
        public void RepositoryCompanionProfilesKeepIndependentDesktopPositions()
        {
            var saveData = SaveData.CreateDefault();
            var repoA = CreateGitRepository();
            var repoB = CreateGitRepository();
            var profileA = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, repoA).Value;
            var settingsA = RepositoryCompanionProfileService.GetSelectedDesktopCompanionSettings(saveData);
            settingsA.HasSavedOverlayPosition = true;
            settingsA.LastOverlayPositionX = 321f;
            settingsA.LastOverlayPositionY = 222f;
            RepositoryCompanionProfileService.SetSelectedDesktopCompanionSettings(saveData, settingsA);

            var profileB = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, repoB).Value;
            var settingsB = RepositoryCompanionProfileService.GetSelectedDesktopCompanionSettings(saveData);
            settingsB.HasSavedOverlayPosition = true;
            settingsB.LastOverlayPositionX = 48f;
            settingsB.LastOverlayPositionY = 96f;
            RepositoryCompanionProfileService.SetSelectedDesktopCompanionSettings(saveData, settingsB);

            RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, repoA);
            var restoredA = RepositoryCompanionProfileService.GetSelectedDesktopCompanionSettings(saveData);
            RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, repoB);
            var restoredB = RepositoryCompanionProfileService.GetSelectedDesktopCompanionSettings(saveData);

            Assert.AreEqual(profileA.RepositoryHash, RepositoryCompanionProfileService.HashRepositoryPath(repoA));
            Assert.AreEqual(profileB.RepositoryHash, RepositoryCompanionProfileService.HashRepositoryPath(repoB));
            Assert.AreEqual(321f, restoredA.LastOverlayPositionX);
            Assert.AreEqual(222f, restoredA.LastOverlayPositionY);
            Assert.AreEqual(48f, restoredB.LastOverlayPositionX);
            Assert.AreEqual(96f, restoredB.LastOverlayPositionY);
        }

        [Test]
        public void RepositoryCompanionProfilesKeepIndependentDesktopSkins()
        {
            var saveData = SaveData.CreateDefault();
            var repoA = CreateGitRepository();
            var repoB = CreateGitRepository();
            var profileA = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, repoA).Value;
            var settingsA = RepositoryCompanionProfileService.GetSelectedDesktopCompanionSettings(saveData);
            settingsA.VisualThemeId = "black_cat";
            RepositoryCompanionProfileService.SetSelectedDesktopCompanionSettings(saveData, settingsA);

            var profileB = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, repoB).Value;
            var settingsB = RepositoryCompanionProfileService.GetSelectedDesktopCompanionSettings(saveData);
            settingsB.VisualThemeId = "runner";
            RepositoryCompanionProfileService.SetSelectedDesktopCompanionSettings(saveData, settingsB);

            RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, repoA);
            var restoredA = RepositoryCompanionProfileService.GetSelectedDesktopCompanionSettings(saveData);
            RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, repoB);
            var restoredB = RepositoryCompanionProfileService.GetSelectedDesktopCompanionSettings(saveData);

            Assert.AreEqual(profileA.RepositoryHash, RepositoryCompanionProfileService.HashRepositoryPath(repoA));
            Assert.AreEqual(profileB.RepositoryHash, RepositoryCompanionProfileService.HashRepositoryPath(repoB));
            Assert.AreEqual("black_cat", restoredA.VisualThemeId);
            Assert.AreEqual("runner", restoredB.VisualThemeId);
        }

        [Test]
        public void LegacySingleCompanionDoesNotAutoConnectDefaultLocalProfile()
        {
            var saveData = SaveData.CreateDefault();
            saveData.CompanionState.TotalXp = 700;

            RepositoryCompanionProfileService.Normalize(saveData);

            Assert.AreEqual(0, saveData.RepositoryCompanionProfiles.Count(profile => profile.ArchivedAtUtc == null));
            Assert.IsTrue(string.IsNullOrWhiteSpace(saveData.SelectedRepositoryHash));
            Assert.AreEqual(0, saveData.CompanionState.TotalLifetimeXp);
        }

        [Test]
        public void RepositoryCompanionSyncDtoContainsHashButNoRawRepositoryData()
        {
            var profile = new RepositoryCompanionProfile
            {
                RepositoryHash = new string('a', 64),
                SafeRepositoryAlias = "Worktree A",
                CompanionState = new CompanionState { TotalXp = 800 }
            };

            var dto = RepositoryCompanionSyncDto.FromProfile(profile);
            Assert.AreEqual(profile.RepositoryHash, dto.RepositoryHash);
            Assert.IsTrue(new SyncPayloadSanitizer().ValidatePayload(dto).IsSafe);
            Assert.IsTrue(new PrivacySanitizer().ValidateNoForbiddenFields(dto).IsSuccess);
        }

        private static string CreateGitRepository(string directoryName = "")
        {
            var parent = Path.Combine(Path.GetTempPath(), "TokenForgeRepoTests", Guid.NewGuid().ToString("N"));
            var path = Path.Combine(parent, string.IsNullOrWhiteSpace(directoryName) ? "Repository" : directoryName);
            Directory.CreateDirectory(Path.Combine(path, ".git"));
            return path;
        }

        private static AgentWorkSession SessionFor(string repositoryHash, string sessionId)
        {
            return new AgentWorkSession
            {
                SessionId = sessionId,
                WorkType = WorkType.Feature,
                SourceProvider = "GIT",
                GitChangeSummary = new GitChangeSummary
                {
                    ProjectPathHash = repositoryHash,
                    ChangedFileCount = 3,
                    ChangedFileCountBucket = CountBucket.Small,
                    AddedLineBucket = LineChangeBucket.Medium,
                    ConfidenceLevel = ConfidenceLevel.High
                },
                ResultStatus = ResultStatus.Succeeded,
                UserReviewed = true
            };
        }

        private static CharacterGrowthResult GrowthFor(string sessionId, int xp)
        {
            return new CharacterGrowthResult
            {
                SessionId = sessionId,
                ExpGained = xp,
                StatDeltas = new CharacterStats { Logic = 2, Velocity = 1 }
            };
        }
    }
}
