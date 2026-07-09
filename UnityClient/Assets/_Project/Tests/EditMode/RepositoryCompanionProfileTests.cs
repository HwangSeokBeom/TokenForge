using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TokenForge.Client.Domain;
using TokenForge.Client.Persistence;
using TokenForge.Client.Privacy;
using TokenForge.Client.Sync;
using TokenForge.Client.UI;

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
            var repo = CreateGitRepository("CoreServer");

            var result = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, repo);

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            Assert.IsNotEmpty(result.Value.RepositoryHash);
            Assert.AreEqual("CoreServer", result.Value.SafeRepositoryAlias);
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
        public void ConnectingRepositoryCreatesTimelineEvent()
        {
            var saveData = SaveData.CreateDefault();
            var result = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, CreateGitRepository("TimelineRepo"));

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            Assert.IsTrue(saveData.RepositoryTimelineEvents.Any(item =>
                item.EventType == "repository_connected" &&
                item.RepositoryId == result.Value.RepositoryHash &&
                item.RepositoryAlias == "TimelineRepo"));
        }

        [Test]
        public void ReSelectingSameRepositoryDedupesActivationTimelineNoise()
        {
            var saveData = SaveData.CreateDefault();
            var repository = CreateGitRepository("TimelineDedupeRepo");

            var first = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, repository);
            var second = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, repository);

            Assert.IsTrue(first.IsSuccess, first.ErrorMessage);
            Assert.IsTrue(second.IsSuccess, second.ErrorMessage);
            Assert.AreEqual(first.Value.RepositoryHash, second.Value.RepositoryHash);
            Assert.AreEqual(1, saveData.RepositoryTimelineEvents.Count(item =>
                item.RepositoryId == first.Value.RepositoryHash &&
                (item.EventType == "repository_connected" || item.EventType == "active_repository_changed")));
        }

        [Test]
        public void SelectedConnectedProjectAliasRestoresExistingProfileInsteadOfDefaultEgg()
        {
            var saveData = SaveData.CreateDefault();
            var canonicalHash = "canonical-tokenforge-profile";
            saveData.SelectedRepositoryHash = "connected-project-tokenforge";
            saveData.RepositoryCompanionProfiles.Add(new RepositoryCompanionProfile
            {
                RepositoryHash = canonicalHash,
                SafeRepositoryAlias = "TokenForge",
                ApprovedAtUtc = DateTimeOffset.UtcNow.AddDays(-10),
                ConnectionSource = "userSelected",
                CompanionState = new CompanionState
                {
                    Stage = CompanionStage.Adult,
                    Level = 11,
                    CurrentXp = 2400,
                    TotalLifetimeXp = 7200,
                    TotalXp = 7200,
                    Archetype = CompanionArchetype.Builder
                }
            });
            saveData.ConnectedProjects.Add(new ConnectedProject
            {
                Id = "connected-project-tokenforge",
                PathHash = canonicalHash,
                ProjectPathHash = "normalized-tokenforge-path",
                DisplayName = "TokenForge",
                ApprovedAt = DateTimeOffset.UtcNow.AddDays(-10),
                IsActive = true,
                IsGitRepository = true,
                ConnectionSource = "userSelected"
            });

            RepositoryCompanionProfileService.Normalize(saveData);
            var selected = RepositoryCompanionProfileService.GetSelectedProfile(saveData);

            Assert.IsNotNull(selected);
            Assert.AreEqual(canonicalHash, selected.RepositoryHash);
            Assert.AreEqual(canonicalHash, saveData.SelectedRepositoryHash);
            Assert.AreEqual(CompanionStage.Adult, saveData.CompanionState.Stage);
            Assert.AreEqual(11, saveData.CompanionState.Level);
            Assert.AreEqual(2400, saveData.CompanionState.CurrentXp);
            Assert.Greater(saveData.CompanionState.TotalLifetimeXp, 0);
        }

        [Test]
        public void SelectedNormalizedPathAliasRestoresExistingProfileInsteadOfDefaultEgg()
        {
            var saveData = SaveData.CreateDefault();
            var canonicalHash = "canonical-tokenforge-profile";
            saveData.SelectedRepositoryHash = "normalized-tokenforge-path";
            saveData.RepositoryCompanionProfiles.Add(new RepositoryCompanionProfile
            {
                RepositoryHash = canonicalHash,
                SafeRepositoryAlias = "TokenForge",
                ApprovedAtUtc = DateTimeOffset.UtcNow.AddDays(-7),
                ConnectionSource = "userSelected",
                CompanionState = new CompanionState
                {
                    Stage = CompanionStage.Child,
                    Level = 4,
                    CurrentXp = 320,
                    TotalLifetimeXp = 1600,
                    TotalXp = 1600,
                    Archetype = CompanionArchetype.Builder
                }
            });
            saveData.ConnectedProjects.Add(new ConnectedProject
            {
                Id = "connected-project-tokenforge",
                PathHash = canonicalHash,
                ProjectPathHash = "normalized-tokenforge-path",
                DisplayName = "TokenForge",
                ApprovedAt = DateTimeOffset.UtcNow.AddDays(-7),
                IsActive = true,
                IsGitRepository = true,
                ConnectionSource = "userSelected"
            });

            RepositoryCompanionProfileService.Normalize(saveData);
            var selected = RepositoryCompanionProfileService.GetSelectedProfile(saveData);

            Assert.IsNotNull(selected);
            Assert.AreEqual(canonicalHash, selected.RepositoryHash);
            Assert.AreEqual(canonicalHash, saveData.SelectedRepositoryHash);
            Assert.AreEqual(CompanionStage.Child, saveData.CompanionState.Stage);
            Assert.AreEqual(4, saveData.CompanionState.Level);
            Assert.AreEqual(320, saveData.CompanionState.CurrentXp);
        }

        [Test]
        public void SelectedRepositoryPathAliasRestoresExistingProfile()
        {
            var saveData = SaveData.CreateDefault();
            var repositoryPath = CreateGitRepository("TokenForgePathAlias");
            var profile = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, repositoryPath).Value;
            profile.CompanionState = new CompanionState
            {
                Stage = CompanionStage.Child,
                Level = 4,
                CurrentXp = 410,
                TotalLifetimeXp = 1400,
                TotalXp = 1400
            };
            saveData.SelectedRepositoryHash = repositoryPath;

            RepositoryCompanionProfileService.Normalize(saveData);
            var selected = RepositoryCompanionProfileService.GetSelectedProfile(saveData);

            Assert.IsNotNull(selected);
            Assert.AreEqual(profile.RepositoryHash, selected.RepositoryHash);
            Assert.AreEqual(profile.RepositoryHash, saveData.SelectedRepositoryHash);
            Assert.AreEqual(CompanionStage.Child, saveData.CompanionState.Stage);
            Assert.AreEqual(4, saveData.CompanionState.Level);
        }

        [Test]
        public void SelectedRepositoryAliasVariantsRestoreSameExistingLv4Profile()
        {
            var repositoryPath = CreateGitRepository("TokenForgeLv4");
            var canonicalPath = RepositoryCompanionProfileService.CanonicalRepositoryPathForIdentity(repositoryPath);
            var canonicalHash = RepositoryCompanionProfileService.HashRepositoryPath(repositoryPath);
            var legacyPathHash = SafeHashUtility.ComputeProjectPathHash(canonicalPath);
            var cases = new[]
            {
                new { Name = "canonical hash", Selected = canonicalHash },
                new { Name = "connected project id", Selected = "connected-project-tokenforge" },
                new { Name = "absolute path", Selected = repositoryPath },
                new { Name = "normalized path", Selected = canonicalPath },
                new { Name = "display name", Selected = "TokenForge" },
                new { Name = "local-only alias", Selected = "local-only-tokenforge" },
                new { Name = "legacy path hash", Selected = legacyPathHash }
            };

            foreach (var testCase in cases)
            {
                var saveData = BuildLv4TokenForgeAliasSaveData(canonicalHash, legacyPathHash);
                saveData.SelectedRepositoryHash = testCase.Selected;

                RepositoryCompanionProfileService.Normalize(saveData);
                var selected = RepositoryCompanionProfileService.GetSelectedProfile(saveData);

                Assert.IsNotNull(selected, testCase.Name);
                Assert.AreEqual(canonicalHash, selected.RepositoryHash, testCase.Name);
                Assert.AreEqual(canonicalHash, saveData.SelectedRepositoryHash, testCase.Name);
                Assert.AreEqual(CompanionStage.Child, saveData.CompanionState.Stage, testCase.Name);
                Assert.AreEqual(4, saveData.CompanionState.Level, testCase.Name);
                Assert.AreEqual(420, saveData.CompanionState.CurrentXp, testCase.Name);
                Assert.AreEqual(1200, saveData.CompanionState.TotalLifetimeXp, testCase.Name);
                Assert.AreEqual(1, saveData.RepositoryCompanionProfiles.Count(profile => profile.ArchivedAtUtc == null), testCase.Name);
            }
        }

        [Test]
        public void UnknownSelectedRepositoryDoesNotMatchAnotherProfileOrCreateDefaultEgg()
        {
            var saveData = SaveData.CreateDefault();
            saveData.SelectedRepositoryHash = "unrelated-random-hash";
            saveData.RepositoryCompanionProfiles.Add(new RepositoryCompanionProfile
            {
                RepositoryHash = "repo-a-canonical",
                SafeRepositoryAlias = "Repo A",
                ApprovedAtUtc = DateTimeOffset.UtcNow,
                ConnectionSource = "userSelected",
                CompanionState = new CompanionState
                {
                    Stage = CompanionStage.Adult,
                    Level = 9,
                    CurrentXp = 900,
                    TotalLifetimeXp = 5400,
                    TotalXp = 5400
                }
            });
            saveData.ConnectedProjects.Add(new ConnectedProject
            {
                Id = "repo-a-connected-project",
                PathHash = "repo-a-canonical",
                ProjectPathHash = "repo-a-normalized",
                DisplayName = "Repo A",
                ApprovedAt = DateTimeOffset.UtcNow,
                IsActive = true,
                IsGitRepository = true,
                ConnectionSource = "userSelected"
            });

            RepositoryCompanionProfileService.Normalize(saveData);
            var selected = RepositoryCompanionProfileService.GetSelectedProfile(saveData);

            Assert.IsNull(selected);
            Assert.IsTrue(string.IsNullOrWhiteSpace(saveData.SelectedRepositoryHash));
            Assert.AreEqual(1, saveData.RepositoryCompanionProfiles.Count);
            Assert.AreEqual(CompanionStage.Egg, saveData.CompanionState.Stage);
            Assert.AreEqual(1, saveData.CompanionState.Level);
        }

        [Test]
        public void RandomSelectedRepositoryStaysExplicitEmptyDiagnosticState()
        {
            var canonicalHash = "repo-a-canonical";
            var saveData = BuildLv4TokenForgeAliasSaveData(canonicalHash, "repo-a-legacy-path-hash");
            saveData.SelectedRepositoryHash = "random-not-a-repository-alias";

            var diagnostic = RepositoryCompanionProfileService.CanonicalizeSelectedRepositoryHash(saveData, saveData.SelectedRepositoryHash, false);
            RepositoryCompanionProfileService.Normalize(saveData);
            var selected = RepositoryCompanionProfileService.GetSelectedProfile(saveData);

            Assert.IsFalse(diagnostic.Resolved);
            Assert.AreEqual("canonical_profile_not_found", diagnostic.Reason);
            Assert.IsFalse(diagnostic.DidCreateNewProfile);
            Assert.IsNull(selected);
            Assert.IsTrue(string.IsNullOrWhiteSpace(saveData.SelectedRepositoryHash));
            Assert.AreEqual(1, saveData.RepositoryCompanionProfiles.Count(profile => profile.ArchivedAtUtc == null));
            Assert.AreEqual(canonicalHash, saveData.RepositoryCompanionProfiles.Single(profile => profile.ArchivedAtUtc == null).RepositoryHash);
            Assert.AreEqual(CompanionStage.Egg, saveData.CompanionState.Stage);
            Assert.AreEqual(1, saveData.CompanionState.Level);
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
        public void ApprovedLegacyProfileWithoutConnectedProjectIsHistoryOnly()
        {
            var saveData = SaveData.CreateDefault();
            saveData.SelectedRepositoryHash = "legacy-approved-repo";
            saveData.RepositoryCompanionProfiles.Add(new RepositoryCompanionProfile
            {
                RepositoryHash = "legacy-approved-repo",
                SafeRepositoryAlias = "TokenForge",
                ConnectionSource = "userSelected",
                ApprovedAtUtc = DateTimeOffset.UtcNow,
                CompanionState = new CompanionState { TotalLifetimeXp = 1200, CurrentXp = 420, Level = 3 }
            });

            RepositoryCompanionProfileService.Normalize(saveData);

            Assert.IsNull(RepositoryCompanionProfileService.GetSelectedProfile(saveData));
            Assert.IsFalse(RepositoryCompanionProfileService.IsConnectedRepository(saveData, "legacy-approved-repo"));
            Assert.IsTrue(string.IsNullOrWhiteSpace(saveData.SelectedRepositoryHash));
            Assert.AreEqual(0, saveData.CompanionState.TotalLifetimeXp);
        }

        [Test]
        public void NoRepositoryStateSuppressesLegacyTokenForgeCompanion()
        {
            var saveData = SaveData.CreateDefault();
            saveData.SelectedRepositoryHash = "legacy-tokenforge";
            saveData.RepositoryCompanionProfiles.Add(new RepositoryCompanionProfile
            {
                RepositoryHash = "legacy-tokenforge",
                SafeRepositoryAlias = "TokenForge",
                ConnectionSource = "userSelected",
                ApprovedAtUtc = DateTimeOffset.UtcNow,
                CompanionState = new CompanionState { Level = 3, CurrentXp = 193, TotalLifetimeXp = 193 }
            });

            RepositoryCompanionProfileService.Normalize(saveData);
            var projectedItems = InvokeRepositoryCompanionDisplayItems(saveData);

            Assert.IsTrue(string.IsNullOrWhiteSpace(saveData.SelectedRepositoryHash));
            Assert.IsNull(RepositoryCompanionProfileService.GetSelectedProfile(saveData));
            Assert.AreEqual(0, projectedItems.Count);
            Assert.AreEqual(0, saveData.CompanionState.TotalLifetimeXp);
        }

        [Test]
        public void RepositoryTabDoesNotRenderArchivedOrLegacyProfileAsActive()
        {
            var saveData = SaveData.CreateDefault();
            saveData.RepositoryCompanionProfiles.Add(new RepositoryCompanionProfile
            {
                RepositoryHash = "archived-tokenforge",
                SafeRepositoryAlias = "TokenForge",
                ConnectionSource = "userSelected",
                ApprovedAtUtc = DateTimeOffset.UtcNow,
                ArchivedAtUtc = DateTimeOffset.UtcNow
            });
            saveData.ConnectedProjects.Add(new ConnectedProject
            {
                Id = "legacy-local",
                DisplayName = "Local Repository",
                ConnectionSource = "debugFallback",
                ApprovedAt = DateTimeOffset.UtcNow,
                IsActive = true
            });

            RepositoryCompanionProfileService.Normalize(saveData);
            var projectedItems = InvokeRepositoryCompanionDisplayItems(saveData);

            Assert.AreEqual(0, projectedItems.Count);
            Assert.IsTrue(saveData.ConnectedProjects.All(project => project.IsArchived || !project.IsActive));
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
                Stage = CompanionStage.Child,
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
            Assert.AreEqual(CompanionStage.Child, restored.CompanionState.Stage);
            Assert.AreEqual(4, restored.CompanionState.Level);
            Assert.Greater(restored.CompanionState.CurrentXp, 0);
        }

        [Test]
        public void ZodiacMetadataRefreshDoesNotOverwriteExistingRepositoryProgressWithDefaultEgg()
        {
            var saveData = SaveData.CreateDefault();
            var repository = CreateGitRepository("TokenForgeProgress");
            var existing = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, repository).Value;
            existing.CompanionState = CompanionProgressionRules.Normalize(new CompanionState
            {
                Stage = CompanionStage.Teen,
                Level = 8,
                CurrentXp = 900,
                TotalXp = 3600,
                TotalLifetimeXp = 3600
            });

            var zodiacs = RepositoryCompanionProfileService.GetZodiacCompanionTypes();
            var restored = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, repository).Value;

            Assert.AreEqual(12, zodiacs.Count);
            Assert.AreEqual(CompanionStage.Teen, restored.CompanionState.Stage);
            Assert.AreEqual(8, restored.CompanionState.Level);
            Assert.AreEqual(3600, restored.CompanionState.TotalLifetimeXp);
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
                    Stage = CompanionStage.Child,
                    Level = 4,
                    CurrentXp = 420,
                    TotalXp = 1200,
                    TotalLifetimeXp = 1200
                }
            });

            var restored = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, repository).Value;

            Assert.AreEqual(RepositoryCompanionProfileService.HashRepositoryPath(repository), restored.RepositoryHash);
            Assert.AreEqual(1, saveData.RepositoryCompanionProfiles.Count);
            Assert.AreEqual(CompanionStage.Child, restored.CompanionState.Stage);
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
            CollectionAssert.DoesNotContain(profile.TokenShop.EquippedItemIds, "skin_white_cat");
            Assert.IsFalse(result.Value.Equipped);
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
            CollectionAssert.Contains(catalog.Select(item => item.Category).ToArray(), ShopItemCategory.Outfits);
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
        public void NoRepositoryLocksRepositoryShopAndWardrobe()
        {
            var saveData = SaveData.CreateDefault();

            var purchase = RepositoryCompanionProfileService.PurchaseTokenShopItem(saveData, ShopTargetType.RepositoryCompanion, string.Empty, "skin_white_cat", true);
            var equip = RepositoryCompanionProfileService.EquipTokenShopItem(saveData, ShopTargetType.RepositoryCompanion, string.Empty, "skin_white_cat", true);

            Assert.IsFalse(purchase.IsSuccess);
            Assert.AreEqual("no_active_repository", purchase.ErrorCode);
            Assert.IsFalse(equip.IsSuccess);
            Assert.AreEqual("no_active_repository", equip.ErrorCode);
            Assert.IsNull(RepositoryCompanionProfileService.GetSelectedProfile(saveData));
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
                Assert.GreaterOrEqual(catalog.Count(item => string.Equals(item.ZodiacTypeId, zodiac.Id, StringComparison.Ordinal)), 2, zodiac.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(zodiac.KoreanName), zodiac.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(zodiac.DisplayName), zodiac.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(zodiac.ShortDescription), zodiac.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(zodiac.VisualTheme), zodiac.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(zodiac.PlayStyleHint), zodiac.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(zodiac.SilhouetteHint), zodiac.Id);
                Assert.AreEqual(7, zodiac.Stages.Count, zodiac.Id);
                CollectionAssert.AreEqual(new[] { "egg", "baby", "child", "junior", "teen", "young_adult", "adult" }, zodiac.Stages.Select(stage => stage.StageId).ToArray(), zodiac.Id);
                StringAssert.Contains("junior:zodiac_" + zodiac.Id + "_junior", zodiac.EvolutionStageMapping);
                StringAssert.Contains("young_adult:zodiac_" + zodiac.Id + "_young_adult", zodiac.EvolutionStageMapping);
                Assert.IsTrue(zodiac.Stages.All(stage => stage.ArtVariantKey == "zodiac_" + zodiac.Id + "_" + stage.StageId), zodiac.Id);
                Assert.IsTrue(zodiac.Stages.All(stage => !string.IsNullOrWhiteSpace(stage.LevelRange)), zodiac.Id);
                Assert.IsTrue(zodiac.Stages.All(stage => !string.IsNullOrWhiteSpace(stage.SilhouetteTrait)), zodiac.Id);
                Assert.IsTrue(zodiac.Stages.All(stage => !string.IsNullOrWhiteSpace(stage.PersonalityTrait)), zodiac.Id);
                Assert.IsTrue(zodiac.Stages.All(stage => stage.MotionProfileKey == stage.ArtVariantKey + "_motion"), zodiac.Id);
                Assert.IsTrue(zodiac.Stages.All(stage => stage.ShopPreviewKey == stage.ArtVariantKey), zodiac.Id);
                Assert.IsTrue(zodiac.Stages.All(stage => stage.WardrobePreviewKey == stage.ArtVariantKey + "_wardrobe"), zodiac.Id);
                Assert.AreEqual("유년기", zodiac.Stages.Single(stage => stage.StageId == "baby").KoreanStageName, zodiac.Id);
                Assert.AreEqual("주니어", zodiac.Stages.Single(stage => stage.StageId == "junior").KoreanStageName, zodiac.Id);
                Assert.AreEqual("청소년기", zodiac.Stages.Single(stage => stage.StageId == "teen").KoreanStageName, zodiac.Id);
                Assert.AreEqual("성숙기", zodiac.Stages.Single(stage => stage.StageId == "young_adult").KoreanStageName, zodiac.Id);
                Assert.AreEqual("성체", zodiac.Stages.Single(stage => stage.StageId == "adult").KoreanStageName, zodiac.Id);
            }
        }

        [Test]
        public void TokenShopCatalogMeetsMinimumGameShopContentCounts()
        {
            var catalog = RepositoryCompanionProfileService.GetTokenShopCatalog();
            var commonAgentItems = catalog.Where(item => item.TargetType == ShopTargetType.AiAgent && string.IsNullOrWhiteSpace(item.ZodiacTypeId) && (item.CompatibleAgentIds?.Count ?? 0) > 1).ToList();

            Assert.GreaterOrEqual(commonAgentItems.Count(item => item.Category == ShopItemCategory.Skins), 6);
            Assert.GreaterOrEqual(commonAgentItems.Count(item => item.Category == ShopItemCategory.Outfits), 4);
            Assert.GreaterOrEqual(commonAgentItems.Count(item => item.Category == ShopItemCategory.Accessories), 6);
            Assert.GreaterOrEqual(commonAgentItems.Count(item => item.Category == ShopItemCategory.Effects), 6);
            Assert.GreaterOrEqual(commonAgentItems.Count(item => item.Category == ShopItemCategory.Motions), 4);
            Assert.GreaterOrEqual(commonAgentItems.Count(item => item.Category == ShopItemCategory.Themes), 4);
            Assert.GreaterOrEqual(commonAgentItems.Count(item => item.Category == ShopItemCategory.Badges), 4);
            foreach (var agentId in new[] { "codex", "claudeCode", "cursor", "githubCopilot", "geminiCli" })
            {
                Assert.GreaterOrEqual(catalog.Count(item => item.CompatibleAgentIds.Count == 1 && item.CompatibleAgentIds.Contains(agentId)), 2, agentId);
            }
        }

        [Test]
        public void ExtendedCatalogItemsCanBePurchasedAndEquipped()
        {
            var saveData = SaveData.CreateDefault();
            RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, CreateGitRepository());
            var codexShop = RepositoryCompanionProfileService.GetOrCreateAgentShopState(saveData, "codex").TokenShop;
            codexShop.CurrencyBalance = 20;

            var purchase = RepositoryCompanionProfileService.PurchaseTokenShopItem(saveData, ShopTargetType.AiAgent, "codex", "agent_codex_terminal_crown", true);
            var equip = RepositoryCompanionProfileService.EquipTokenShopItem(saveData, ShopTargetType.AiAgent, "codex", "agent_codex_terminal_crown", true);

            Assert.IsTrue(purchase.IsSuccess, purchase.ErrorMessage);
            Assert.IsTrue(equip.IsSuccess, equip.ErrorMessage);
            CollectionAssert.Contains(codexShop.PurchasedItemIds, "agent_codex_terminal_crown");
            CollectionAssert.Contains(codexShop.EquippedItemIds, "agent_codex_terminal_crown");
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
            CollectionAssert.DoesNotContain(agentShop.EquippedItemIds, "agent_skin_codex_terminal");
        }

        [Test]
        public void TokenShopPurchaseAndEquipCreateTimelineEvents()
        {
            var saveData = SaveData.CreateDefault();
            var profile = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, CreateGitRepository()).Value;
            profile.TokenShop.CurrencyBalance = 12;

            var purchase = RepositoryCompanionProfileService.PurchaseTokenShopItem(saveData, "skin_white_cat");
            var equip = RepositoryCompanionProfileService.EquipTokenShopItem(saveData, ShopTargetType.RepositoryCompanion, string.Empty, "skin_white_cat", true);

            Assert.IsTrue(purchase.IsSuccess, purchase.ErrorMessage);
            Assert.IsTrue(equip.IsSuccess, equip.ErrorMessage);
            Assert.IsTrue(saveData.RepositoryTimelineEvents.Any(item => item.EventType == "shop_item_purchased" && item.ItemId == "skin_white_cat"));
            Assert.IsTrue(saveData.RepositoryTimelineEvents.Any(item => item.EventType == "wardrobe_item_equipped" && item.ItemId == "skin_white_cat"));
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
        public void CodexCoinsCannotPurchaseClaudeExclusiveItem()
        {
            var saveData = SaveData.CreateDefault();
            RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, CreateGitRepository());
            RepositoryCompanionProfileService.GetOrCreateAgentShopState(saveData, "codex").TokenShop.CurrencyBalance = 50;

            var result = RepositoryCompanionProfileService.PurchaseTokenShopItem(saveData, ShopTargetType.AiAgent, "codex", "agent_claude_context_scroll", true);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("shop_item_not_compatible", result.ErrorCode);
            Assert.AreEqual(50, RepositoryCompanionProfileService.GetAgentTokenShopState(saveData, "codex").CurrencyBalance);
        }

        [Test]
        public void SettingsZodiacSelectionPersistsThroughSaveLoad()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Guid.NewGuid().ToString("N"));
            var repository = new SaveDataRepository(directory);
            var saveData = SaveData.CreateDefault();
            RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, CreateGitRepository());
            var settings = RepositoryCompanionProfileService.GetSelectedDesktopCompanionSettings(saveData);
            settings.ZodiacTypeId = "dragon";
            RepositoryCompanionProfileService.SetSelectedDesktopCompanionSettings(saveData, settings);
            RunAsync("save zodiac settings", token => repository.SaveAsync(saveData, token));

            var loaded = RunAsync("load zodiac settings", token => repository.LoadAsync(token));
            var restored = RepositoryCompanionProfileService.GetSelectedDesktopCompanionSettings(loaded);

            Assert.AreEqual("dragon", restored.ZodiacTypeId);
        }

        [Test]
        public void SettingsZodiacSelectionCreatesTimelineEvent()
        {
            var saveData = SaveData.CreateDefault();
            var profile = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, CreateGitRepository()).Value;
            var settings = RepositoryCompanionProfileService.GetSelectedDesktopCompanionSettings(saveData);
            settings.ZodiacTypeId = "dragon";

            RepositoryCompanionProfileService.SetSelectedDesktopCompanionSettings(saveData, settings);

            Assert.IsTrue(saveData.RepositoryTimelineEvents.Any(item =>
                item.EventType == "zodiac_changed" &&
                item.RepositoryId == profile.RepositoryHash &&
                item.ZodiacId == "dragon"));
        }

        [Test]
        public void OnboardingCompletedFlagPersistsThroughSaveLoad()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Guid.NewGuid().ToString("N"));
            var repository = new SaveDataRepository(directory);
            var saveData = SaveData.CreateDefault();
            saveData.OnboardingPreferences.FirstRunOnboardingCompleted = true;
            saveData.OnboardingPreferences.CompletedAtUtc = DateTimeOffset.UtcNow;
            RunAsync("save onboarding preferences", token => repository.SaveAsync(saveData, token));

            var loaded = RunAsync("load onboarding preferences", token => repository.LoadAsync(token));

            Assert.IsTrue(loaded.OnboardingPreferences.FirstRunOnboardingCompleted);
            Assert.IsNotNull(loaded.OnboardingPreferences.CompletedAtUtc);
        }

        [Test]
        public void OnboardingFirstRunDoesNotInventRepository()
        {
            var saveData = SaveData.CreateDefault();

            Assert.IsFalse(saveData.OnboardingPreferences.FirstRunOnboardingCompleted);
            saveData.OnboardingPreferences.FirstRunOnboardingCompleted = true;
            RepositoryCompanionProfileService.Normalize(saveData);

            Assert.IsTrue(saveData.OnboardingPreferences.FirstRunOnboardingCompleted);
            Assert.IsTrue(string.IsNullOrWhiteSpace(saveData.SelectedRepositoryHash));
            Assert.AreEqual(0, saveData.RepositoryCompanionProfiles.Count(profile => profile.ArchivedAtUtc == null));
            Assert.IsNull(RepositoryCompanionProfileService.GetSelectedProfile(saveData));
        }

        [Test]
        public void DefaultOnboardingCompletionIsFalse()
        {
            var saveData = SaveData.CreateDefault();

            Assert.IsFalse(saveData.OnboardingPreferences.FirstRunOnboardingCompleted);
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
        public void TokenShopAgentCurrencyAccruesFromEstimatedAggregateWithoutSavedGrowth()
        {
            var saveData = SaveData.CreateDefault();
            saveData.ProviderSettings.Add(new ProviderSettings
            {
                ProviderId = "Codex",
                Selected = true,
                Enabled = true,
                Status = "connected",
                EstimatedTotalTokenCount = 25_001,
                UsageEvidenceState = "aggregateAvailable"
            });

            RepositoryCompanionProfileService.Normalize(saveData);

            var shop = RepositoryCompanionProfileService.GetAgentTokenShopState(saveData, "codex");
            Assert.AreEqual(2, shop.LifetimeTokenUsageScore);
            Assert.AreEqual(2, shop.CurrencyBalance);
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
        public void TokenShopAiAgentPurchaseDoesNotRequireRepositoryConnection()
        {
            var saveData = SaveData.CreateDefault();
            var codexShop = RepositoryCompanionProfileService.GetOrCreateAgentShopState(saveData, "codex").TokenShop;
            codexShop.CurrencyBalance = 12;

            var result = RepositoryCompanionProfileService.PurchaseTokenShopItem(saveData, ShopTargetType.AiAgent, "codex", "agent_skin_codex_terminal", true);

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            Assert.IsTrue(string.IsNullOrWhiteSpace(saveData.SelectedRepositoryHash));
            Assert.AreEqual(8, RepositoryCompanionProfileService.GetAgentTokenShopState(saveData, "codex").CurrencyBalance);
            CollectionAssert.Contains(RepositoryCompanionProfileService.GetAgentTokenShopState(saveData, "codex").PurchasedItemIds, "agent_skin_codex_terminal");
        }

        [Test]
        public void AgentShopRequiresAgentConnectionButNotRepository()
        {
            var saveData = SaveData.CreateDefault();
            RepositoryCompanionProfileService.GetOrCreateAgentShopState(saveData, "codex").TokenShop.CurrencyBalance = 12;

            var disconnected = RepositoryCompanionProfileService.PurchaseTokenShopItem(saveData, ShopTargetType.AiAgent, "codex", "agent_skin_codex_terminal", false);
            var connected = RepositoryCompanionProfileService.PurchaseTokenShopItem(saveData, ShopTargetType.AiAgent, "codex", "agent_skin_codex_terminal", true);

            Assert.IsFalse(disconnected.IsSuccess);
            Assert.AreEqual("agent_not_connected", disconnected.ErrorCode);
            Assert.IsTrue(connected.IsSuccess, connected.ErrorMessage);
            Assert.IsTrue(string.IsNullOrWhiteSpace(saveData.SelectedRepositoryHash));
            CollectionAssert.Contains(RepositoryCompanionProfileService.GetAgentTokenShopState(saveData, "codex").PurchasedItemIds, "agent_skin_codex_terminal");
        }

        [Test]
        public void WardrobeEquipsOwnedItemsOnly()
        {
            var saveData = SaveData.CreateDefault();
            var profile = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, CreateGitRepository("WardrobeRepo")).Value;
            profile.TokenShop.CurrencyBalance = 12;

            var unowned = RepositoryCompanionProfileService.EquipTokenShopItem(saveData, ShopTargetType.RepositoryCompanion, string.Empty, "skin_calico", true);
            var purchase = RepositoryCompanionProfileService.PurchaseTokenShopItem(saveData, "skin_white_cat");
            var owned = RepositoryCompanionProfileService.EquipTokenShopItem(saveData, ShopTargetType.RepositoryCompanion, string.Empty, "skin_white_cat", true);

            Assert.IsFalse(unowned.IsSuccess);
            Assert.AreEqual("shop_item_not_owned", unowned.ErrorCode);
            Assert.IsTrue(purchase.IsSuccess, purchase.ErrorMessage);
            Assert.IsTrue(owned.IsSuccess, owned.ErrorMessage);
            CollectionAssert.Contains(profile.TokenShop.EquippedItemIds, "skin_white_cat");
            CollectionAssert.DoesNotContain(profile.TokenShop.EquippedItemIds, "skin_calico");
        }

        [Test]
        public void WardrobeUnequipPersistsWithoutSpendingCoins()
        {
            var saveData = SaveData.CreateDefault();
            var profile = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, CreateGitRepository("WardrobeUnequipRepo")).Value;
            profile.TokenShop.CurrencyBalance = 12;
            Assert.IsTrue(RepositoryCompanionProfileService.PurchaseTokenShopItem(saveData, "skin_white_cat").IsSuccess);
            Assert.IsTrue(RepositoryCompanionProfileService.EquipTokenShopItem(saveData, ShopTargetType.RepositoryCompanion, string.Empty, "skin_white_cat", true).IsSuccess);
            var before = profile.TokenShop.CurrencyBalance;

            var unequip = RepositoryCompanionProfileService.UnequipTokenShopItem(saveData, ShopTargetType.RepositoryCompanion, string.Empty, "skin_white_cat", true);

            Assert.IsTrue(unequip.IsSuccess, unequip.ErrorMessage);
            Assert.AreEqual(before, profile.TokenShop.CurrencyBalance);
            CollectionAssert.Contains(profile.TokenShop.PurchasedItemIds, "skin_white_cat");
            CollectionAssert.DoesNotContain(profile.TokenShop.EquippedItemIds, "skin_white_cat");
        }

        [Test]
        public void TokenShopPreviewUsesRealPreviewMetadata()
        {
            var catalog = RepositoryCompanionProfileService.GetTokenShopCatalog();

            Assert.IsTrue(catalog.All(item => !string.IsNullOrWhiteSpace(item.PreviewType)));
            Assert.IsTrue(catalog.All(item => !string.IsNullOrWhiteSpace(item.PreviewIcon)));
            Assert.IsFalse(catalog.Any(item => item.PreviewIcon.Length == 2 && item.PreviewType == "generic"));
            Assert.IsTrue(catalog.Any(item => item.PreviewType.StartsWith("zodiac_", StringComparison.Ordinal)));
        }

        [Test]
        public void TokenShopRepositoryMascotPurchaseRequiresConnectedRepository()
        {
            var saveData = SaveData.CreateDefault();

            var result = RepositoryCompanionProfileService.PurchaseTokenShopItem(saveData, ShopTargetType.RepositoryCompanion, string.Empty, "skin_white_cat", true);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("no_active_repository", result.ErrorCode);
            Assert.AreEqual(0, saveData.RepositoryCompanionProfiles.Count(profile => profile.ArchivedAtUtc == null));
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

        [Test]
        public void RepositoryCheckpoint_AccumulatesIncrementalRawSignalsAndIgnoresRecentTrendOverlap()
        {
            var saveData = SaveData.CreateDefault();
            saveData.ConnectedProjects.Add(new ConnectedProject
            {
                Id = "repo-growth",
                PathHash = "repo-growth",
                ProjectPathHash = "repo-growth",
                ApprovedAt = DateTimeOffset.UtcNow,
                ConnectionSource = "userSelected"
            });

            RepositoryCompanionProfileService.ApplyRepositoryAnalysisCheckpoint(saveData, "repo-growth", new GitChangeSummary
            {
                AnalysisMode = "full-baseline",
                AnalyzedStartCommit = "first",
                AnalyzedEndCommit = "head-1",
                LastAnalyzedCommit = "head-1",
                TotalCommitsAnalyzed = 10,
                ChangedFileCount = 12,
                NumstatRowsAnalyzed = 12,
                CodeFileSignalCount = 12,
                GrowthFocusSignalCount = 8,
                DebugCommitSignalCount = 2,
                DesignFileSignalCount = 3,
                SyncSignalCount = 1,
                AnalysisIdempotencyKey = "baseline-1"
            });
            RepositoryCompanionProfileService.ApplyRepositoryAnalysisCheckpoint(saveData, "repo-growth", new GitChangeSummary
            {
                AnalysisMode = "incremental",
                AnalyzedStartCommit = "head-1",
                AnalyzedEndCommit = "head-2",
                LastAnalyzedCommit = "head-2",
                TotalCommitsAnalyzed = 12,
                ChangedFileCount = 4,
                NumstatRowsAnalyzed = 4,
                CodeFileSignalCount = 4,
                GrowthFocusSignalCount = 3,
                DebugCommitSignalCount = 1,
                DesignFileSignalCount = 2,
                SyncSignalCount = 1,
                AnalysisIdempotencyKey = "incremental-2"
            });

            var project = saveData.ConnectedProjects.Single(item => item.Id == "repo-growth");
            Assert.AreEqual(16, project.GrowthCodeSignalCount);
            Assert.AreEqual(11, project.GrowthFocusSignalCount);
            Assert.AreEqual(16, project.FilesChangedAnalyzed);
            Assert.AreEqual("head-2", project.LastAnalyzedCommit);
            Assert.AreEqual(RepositoryCompanionProfileService.NormalizeGrowthSignalScore(16), project.GrowthCodeScore);

            RepositoryCompanionProfileService.ApplyRepositoryAnalysisCheckpoint(saveData, "repo-growth", new GitChangeSummary
            {
                AnalysisMode = "incremental",
                AnalyzedStartCommit = "head-1",
                AnalyzedEndCommit = "head-2",
                LastAnalyzedCommit = "head-2",
                ChangedFileCount = 4,
                CodeFileSignalCount = 4,
                GrowthFocusSignalCount = 3,
                AnalysisIdempotencyKey = "incremental-2"
            });

            Assert.AreEqual(16, project.GrowthCodeSignalCount, "Duplicate incremental checkpoint must not accumulate twice.");
            Assert.AreEqual(16, project.FilesChangedAnalyzed, "Duplicate incremental file count must remain idempotent.");

            var codeBeforeRecent = project.GrowthCodeSignalCount;
            RepositoryCompanionProfileService.ApplyRepositoryAnalysisCheckpoint(saveData, "repo-growth", new GitChangeSummary
            {
                AnalysisMode = "recenttrend",
                AnalyzedEndCommit = "head-3",
                LastAnalyzedCommit = "head-3",
                CodeFileSignalCount = 99,
                GrowthFocusSignalCount = 99,
                AnalysisIdempotencyKey = "recent-overlap"
            });

            Assert.AreEqual(codeBeforeRecent, project.GrowthCodeSignalCount);
            Assert.AreEqual("head-2", project.LastAnalyzedCommit);
            Assert.AreEqual("incremental-2", project.GrowthResultId);
        }

        [Test]
        public void Normalize_MergesDuplicateAgentOwnershipWithoutDoubleCreditingWallet()
        {
            var saveData = SaveData.CreateDefault();
            saveData.AiAgentShopStates = new List<AiAgentShopState>
            {
                new AiAgentShopState
                {
                    AgentId = "claude",
                    TokenShop = new TokenShopState
                    {
                        CurrencyBalance = 7,
                        LifetimeTokenUsageScore = 9,
                        PurchasedItemIds = new List<string> { "agent_skin_mono_matrix" },
                        EquippedItemIds = new List<string> { "agent_skin_mono_matrix" }
                    }
                },
                new AiAgentShopState
                {
                    AgentId = "claudeCode",
                    TokenShop = new TokenShopState
                    {
                        CurrencyBalance = 5,
                        LifetimeTokenUsageScore = 8,
                        PurchasedItemIds = new List<string> { "agent_accessory_focus_halo" },
                        EquippedItemIds = new List<string> { "agent_accessory_focus_halo" }
                    }
                }
            };

            RepositoryCompanionProfileService.Normalize(saveData);

            var state = saveData.AiAgentShopStates.Single(item => item.AgentId == "claudeCode");
            CollectionAssert.AreEquivalent(
                new[] { "agent_skin_mono_matrix", "agent_accessory_focus_halo" },
                state.TokenShop.PurchasedItemIds);
            Assert.AreEqual(7, state.TokenShop.CurrencyBalance);
            Assert.AreEqual(9, state.TokenShop.LifetimeTokenUsageScore);
        }

        private static SaveData BuildLv4TokenForgeAliasSaveData(string canonicalHash, string legacyPathHash)
        {
            var saveData = SaveData.CreateDefault();
            saveData.RepositoryCompanionProfiles.Add(new RepositoryCompanionProfile
            {
                RepositoryHash = canonicalHash,
                SafeRepositoryAlias = "TokenForge",
                ApprovedAtUtc = DateTimeOffset.UtcNow.AddDays(-10),
                ConnectionSource = "userSelected",
                CompanionState = new CompanionState
                {
                    Stage = CompanionStage.Child,
                    Level = 4,
                    CurrentXp = 420,
                    TotalLifetimeXp = 1200,
                    TotalXp = 1200,
                    Archetype = CompanionArchetype.Builder
                }
            });
            saveData.ConnectedProjects.Add(new ConnectedProject
            {
                Id = "connected-project-tokenforge",
                PathHash = canonicalHash,
                ProjectPathHash = legacyPathHash,
                LocalOnlyProjectId = "local-only-tokenforge",
                DisplayName = "TokenForge",
                ProjectAlias = "TokenForge",
                ApprovedAt = DateTimeOffset.UtcNow.AddDays(-10),
                IsActive = true,
                IsGitRepository = true,
                ConnectionSource = "userSelected"
            });

            return saveData;
        }

        private static string CreateGitRepository(string directoryName = "")
        {
            var parent = Path.Combine(Path.GetTempPath(), "TokenForgeRepoTests", Guid.NewGuid().ToString("N"));
            var path = Path.Combine(parent, string.IsNullOrWhiteSpace(directoryName) ? "Repository" : directoryName);
            Directory.CreateDirectory(Path.Combine(path, ".git"));
            return path;
        }

        private static T RunAsync<T>(
            string operationName,
            Func<CancellationToken, Task<T>> action,
            [CallerMemberName] string testName = "")
        {
            const int TimeoutMilliseconds = 10000;
            using (var cancellation = new CancellationTokenSource())
            {
                var originalContext = SynchronizationContext.Current;
                try
                {
                    SynchronizationContext.SetSynchronizationContext(null);
                    var operationTask = action(cancellation.Token);
                    var timeoutTask = Task.Delay(TimeoutMilliseconds);
                    var completed = Task.WhenAny(operationTask, timeoutTask).GetAwaiter().GetResult();
                    if (!ReferenceEquals(completed, operationTask))
                    {
                        cancellation.Cancel();
                        Assert.Fail(
                            testName + " timed out after " + TimeoutMilliseconds + "ms while running " + operationName +
                            ". The async operation did not complete; check for Unity main-thread continuation capture, an infinite wait, or an unobserved cancellation path.");
                    }

                    return operationTask.GetAwaiter().GetResult();
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(originalContext);
                }
            }
        }

        private static IList InvokeRepositoryCompanionDisplayItems(SaveData saveData)
        {
            return (IList)typeof(ApprovedActivityAnalysisViewModel)
                .GetMethod(
                    "ToRepositoryCompanionDisplayItems",
                    BindingFlags.NonPublic | BindingFlags.Static,
                    null,
                    new[] { typeof(SaveData) },
                    null)
                .Invoke(null, new object[] { saveData });
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
