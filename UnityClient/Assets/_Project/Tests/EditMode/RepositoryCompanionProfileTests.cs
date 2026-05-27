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
