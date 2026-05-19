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
        public void ConnectingRepositoryCreatesProfileWithHashAndSafeAlias()
        {
            var saveData = SaveData.CreateDefault();
            var repo = CreateGitRepository();

            var result = RepositoryCompanionProfileService.SelectOrCreateProfile(saveData, repo);

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            Assert.IsNotEmpty(result.Value.RepositoryHash);
            Assert.AreEqual("Local Repository", result.Value.SafeRepositoryAlias);
            Assert.AreEqual(result.Value.RepositoryHash, saveData.SelectedRepositoryHash);
            Assert.IsFalse(result.Value.SafeRepositoryAlias.Contains(repo));
            Assert.IsTrue(new PrivacySanitizer().ValidateSafeSaveData(saveData).IsSuccess);
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
        public void LegacySingleCompanionMigratesIntoDefaultLocalProfile()
        {
            var saveData = SaveData.CreateDefault();
            saveData.CompanionState.TotalXp = 700;

            RepositoryCompanionProfileService.Normalize(saveData);

            Assert.AreEqual(1, saveData.RepositoryCompanionProfiles.Count);
            Assert.AreEqual(700, saveData.RepositoryCompanionProfiles[0].CompanionState.TotalXp);
            Assert.AreEqual(saveData.RepositoryCompanionProfiles[0].RepositoryHash, saveData.SelectedRepositoryHash);
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

        private static string CreateGitRepository()
        {
            var path = Path.Combine(Path.GetTempPath(), "TokenForgeRepoTests", Guid.NewGuid().ToString("N"));
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
