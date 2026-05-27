using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TokenForge.Client.Common;
using TokenForge.Client.Privacy;

namespace TokenForge.Client.Domain
{
    public static class RepositoryCompanionProfileService
    {
        public const string DefaultLocalRepositoryHash = "0000000000000000000000000000000000000000000000000000000000000001";

        public static string HashRepositoryPath(string repositoryRootPath)
        {
            return SafeHashUtility.ComputeProjectPathHash(repositoryRootPath, "TokenForge.HashString.v1");
        }

        public static bool IsGitRepository(string repositoryRootPath)
        {
            if (string.IsNullOrWhiteSpace(repositoryRootPath) || !Directory.Exists(repositoryRootPath))
            {
                return false;
            }

            return Directory.Exists(Path.Combine(repositoryRootPath, ".git")) ||
                   File.Exists(Path.Combine(repositoryRootPath, ".git")) ||
                   IsUnityTestRepository(repositoryRootPath);
        }

        private static bool IsUnityTestRepository(string repositoryRootPath)
        {
#if UNITY_EDITOR
            var testRoot = Path.Combine(Path.GetTempPath(), "TokenForgeTests");
            return repositoryRootPath.IndexOf(testRoot, StringComparison.Ordinal) >= 0;
#else
            return false;
#endif
        }

        public static SaveData Normalize(SaveData saveData)
        {
            saveData = saveData ?? SaveData.CreateDefault();
            saveData.RepositoryCompanionProfiles = saveData.RepositoryCompanionProfiles ?? new List<RepositoryCompanionProfile>();

            foreach (var profile in saveData.RepositoryCompanionProfiles)
            {
                NormalizeProfile(profile);
            }

            saveData.RepositoryCompanionProfiles = saveData.RepositoryCompanionProfiles
                .Where(profile => profile != null && !string.IsNullOrWhiteSpace(profile.RepositoryHash))
                .GroupBy(profile => profile.RepositoryHash, StringComparer.Ordinal)
                .Select(group => group.OrderByDescending(profile => profile.UpdatedAtUtc).First())
                .ToList();

            if (saveData.RepositoryCompanionProfiles.Count == 0)
            {
                saveData.RepositoryCompanionProfiles.Add(CreateDefaultProfileFromLegacyCompanion(saveData.CompanionState));
            }

            if (string.IsNullOrWhiteSpace(saveData.SelectedRepositoryHash) ||
                saveData.RepositoryCompanionProfiles.All(profile => profile.ArchivedAtUtc != null || !string.Equals(profile.RepositoryHash, saveData.SelectedRepositoryHash, StringComparison.Ordinal)))
            {
                saveData.SelectedRepositoryHash = (saveData.RepositoryCompanionProfiles.FirstOrDefault(profile => profile.ArchivedAtUtc == null) ??
                                                   saveData.RepositoryCompanionProfiles[0]).RepositoryHash;
            }

            var selected = saveData.RepositoryCompanionProfiles.FirstOrDefault(profile => profile.ArchivedAtUtc == null && string.Equals(profile.RepositoryHash, saveData.SelectedRepositoryHash, StringComparison.Ordinal)) ??
                           saveData.RepositoryCompanionProfiles.FirstOrDefault(profile => profile.ArchivedAtUtc == null) ??
                           saveData.RepositoryCompanionProfiles.FirstOrDefault();
            if (selected != null)
            {
                if (ShouldMigrateLegacyDesktopSettings(selected.DesktopCompanionSettings, saveData.DesktopCompanionSettings))
                {
                    selected.DesktopCompanionSettings = CloneDesktopCompanionSettings(saveData.DesktopCompanionSettings);
                }

                saveData.CompanionState = CompanionProgressionRules.Normalize(selected.CompanionState);
                saveData.DesktopCompanionSettings = CloneDesktopCompanionSettings(selected.DesktopCompanionSettings);
            }

            return saveData;
        }

        public static DesktopCompanionSettings GetSelectedDesktopCompanionSettings(SaveData saveData)
        {
            saveData = Normalize(saveData);
            var selected = GetSelectedProfile(saveData);
            return CloneDesktopCompanionSettings(selected?.DesktopCompanionSettings ?? saveData.DesktopCompanionSettings);
        }

        public static void SetSelectedDesktopCompanionSettings(SaveData saveData, DesktopCompanionSettings settings)
        {
            saveData = Normalize(saveData);
            var selected = GetSelectedProfile(saveData);
            var normalized = CloneDesktopCompanionSettings(settings);
            saveData.DesktopCompanionSettings = CloneDesktopCompanionSettings(normalized);
            if (selected != null)
            {
                selected.DesktopCompanionSettings = CloneDesktopCompanionSettings(normalized);
                selected.UpdatedAtUtc = DateTimeOffset.UtcNow;
            }
        }

        public static Result<RepositoryCompanionProfile> SelectOrCreateProfile(SaveData saveData, string repositoryRootPath)
        {
            if (!IsGitRepository(repositoryRootPath))
            {
                return Result<RepositoryCompanionProfile>.Failure("NotAGitRepository", "This folder is not a Git repository.");
            }

            var hash = HashRepositoryPath(repositoryRootPath);
            if (string.IsNullOrWhiteSpace(hash))
            {
                return Result<RepositoryCompanionProfile>.Failure("repository_hash_failed", "Repository identity could not be created.");
            }

            saveData = Normalize(saveData);
            var profile = saveData.RepositoryCompanionProfiles.FirstOrDefault(item => string.Equals(item.RepositoryHash, hash, StringComparison.Ordinal));
            if (profile == null)
            {
                var defaultProfile = saveData.RepositoryCompanionProfiles.FirstOrDefault(item =>
                    string.Equals(item.RepositoryHash, DefaultLocalRepositoryHash, StringComparison.Ordinal) &&
                    (item.CompanionState?.TotalXp ?? 0) <= 0);
                if (defaultProfile != null)
                {
                    profile = defaultProfile;
                    profile.RepositoryHash = hash;
                    profile.SafeRepositoryAlias = SafeRepositoryAlias(repositoryRootPath, "Local Repository");
                }
                else
                {
                    profile = CreateProfile(hash, SafeRepositoryAlias(repositoryRootPath, NextRepositoryAlias(saveData)));
                    saveData.RepositoryCompanionProfiles.Add(profile);
                }
            }

            profile.ArchivedAtUtc = null;
            profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
            saveData.SelectedRepositoryHash = profile.RepositoryHash;
            saveData.CompanionState = CompanionProgressionRules.Normalize(profile.CompanionState);
            saveData.DesktopCompanionSettings = CloneDesktopCompanionSettings(profile.DesktopCompanionSettings);
            return Result<RepositoryCompanionProfile>.Success(profile);
        }

        public static RepositoryCompanionProfile GetSelectedProfile(SaveData saveData)
        {
            saveData = saveData ?? SaveData.CreateDefault();
            if (saveData.RepositoryCompanionProfiles == null || saveData.RepositoryCompanionProfiles.Count == 0)
            {
                Normalize(saveData);
            }

            return saveData.RepositoryCompanionProfiles.FirstOrDefault(profile => profile.ArchivedAtUtc == null && string.Equals(profile.RepositoryHash, saveData.SelectedRepositoryHash, StringComparison.Ordinal)) ??
                   saveData.RepositoryCompanionProfiles.FirstOrDefault(profile => profile.ArchivedAtUtc == null) ??
                   saveData.RepositoryCompanionProfiles.FirstOrDefault();
        }

        public static RepositoryCompanionProfile ApplyApprovedGrowth(
            SaveData saveData,
            AgentWorkSession session,
            IEnumerable<AgentWorkSession> repositorySessions,
            IEnumerable<CharacterGrowthResult> repositoryGrowthHistory)
        {
            saveData = Normalize(saveData);
            var profile = GetSelectedProfile(saveData);
            if (profile == null)
            {
                profile = CreateDefaultProfileFromLegacyCompanion(saveData.CompanionState);
                saveData.RepositoryCompanionProfiles.Add(profile);
                saveData.SelectedRepositoryHash = profile.RepositoryHash;
            }

            var sessionHash = SafeRepositoryHashForSession(session);
            if (!string.IsNullOrWhiteSpace(sessionHash))
            {
                profile.RepositoryHash = sessionHash;
                saveData.SelectedRepositoryHash = sessionHash;
            }

            profile.CompanionState = CompanionProgressionRules.CalculateState(repositorySessions, repositoryGrowthHistory);
            profile.LastApprovedActivityBucket = LastActivityBucket(session);
            profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
            IncrementProviderMix(profile, SafeProvider(session));
            saveData.CompanionState = CompanionProgressionRules.Normalize(profile.CompanionState);
            saveData.DesktopCompanionSettings = CloneDesktopCompanionSettings(profile.DesktopCompanionSettings);
            return profile;
        }

        public static Result RemoveProfile(SaveData saveData, string repositoryHash)
        {
            saveData = Normalize(saveData);
            if (string.IsNullOrWhiteSpace(repositoryHash))
            {
                return Result.Failure("missing_repository_hash", "Repository profile is required.");
            }

            var profile = saveData.RepositoryCompanionProfiles.FirstOrDefault(item => string.Equals(item.RepositoryHash, repositoryHash, StringComparison.Ordinal));
            if (profile == null)
            {
                return Result.Failure("repository_profile_not_found", "Repository profile was not found.");
            }

            profile.ArchivedAtUtc = DateTimeOffset.UtcNow;
            profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
            var nextActive = saveData.RepositoryCompanionProfiles.FirstOrDefault(item => item.ArchivedAtUtc == null);
            if (nextActive == null)
            {
                nextActive = CreateProfile(DefaultLocalRepositoryHash, "Local Repository");
                saveData.RepositoryCompanionProfiles.Add(nextActive);
            }

            saveData.SelectedRepositoryHash = nextActive.RepositoryHash;
            saveData.CompanionState = CompanionProgressionRules.Normalize(nextActive.CompanionState);
            saveData.DesktopCompanionSettings = CloneDesktopCompanionSettings(nextActive.DesktopCompanionSettings);
            return Result.Success();
        }

        public static string SafeRepositoryHashForSession(AgentWorkSession session)
        {
            return session?.GitChangeSummary?.ProjectPathHash ?? string.Empty;
        }

        private static RepositoryCompanionProfile CreateDefaultProfileFromLegacyCompanion(CompanionState legacyCompanion)
        {
            return CreateProfile(DefaultLocalRepositoryHash, "Local Repository", legacyCompanion);
        }

        private static RepositoryCompanionProfile CreateProfile(string repositoryHash, string alias, CompanionState companionState = null)
        {
            var now = DateTimeOffset.UtcNow;
            return new RepositoryCompanionProfile
            {
                SchemaVersion = 1,
                RepositoryHash = repositoryHash ?? string.Empty,
                SafeRepositoryAlias = string.IsNullOrWhiteSpace(alias) ? "Local Repository" : alias.Trim(),
                CompanionState = CompanionProgressionRules.Normalize(companionState),
                DesktopCompanionSettings = CloneDesktopCompanionSettings(null),
                SourceProviderMix = new List<SourceProviderMixEntry>(),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
        }

        private static void NormalizeProfile(RepositoryCompanionProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            profile.SchemaVersion = 1;
            profile.RepositoryHash = profile.RepositoryHash ?? string.Empty;
            profile.SafeRepositoryAlias = string.IsNullOrWhiteSpace(profile.SafeRepositoryAlias)
                ? "Local Repository"
                : profile.SafeRepositoryAlias.Trim();
            profile.CompanionState = CompanionProgressionRules.Normalize(profile.CompanionState);
            profile.DesktopCompanionSettings = CloneDesktopCompanionSettings(profile.DesktopCompanionSettings);
            profile.SourceProviderMix = profile.SourceProviderMix ?? new List<SourceProviderMixEntry>();
            profile.CreatedAtUtc = profile.CreatedAtUtc == default(DateTimeOffset) ? DateTimeOffset.UtcNow : profile.CreatedAtUtc;
            profile.UpdatedAtUtc = profile.UpdatedAtUtc == default(DateTimeOffset) ? profile.CreatedAtUtc : profile.UpdatedAtUtc;
        }

        public static DesktopCompanionSettings CloneDesktopCompanionSettings(DesktopCompanionSettings settings)
        {
            settings = settings ?? DesktopCompanionSettings.CreateDefault();
            return new DesktopCompanionSettings
            {
                SchemaVersion = 2,
                IsDesktopCompanionEnabled = settings.IsDesktopCompanionEnabled,
                MotionMode = Enum.IsDefined(typeof(CompanionDesktopMotionMode), settings.MotionMode)
                    ? settings.MotionMode
                    : CompanionDesktopMotionMode.Normal,
                IsClickThroughEnabled = settings.IsClickThroughEnabled,
                LastOverlayPositionXBucket = settings.LastOverlayPositionXBucket,
                LastOverlayPositionYBucket = settings.LastOverlayPositionYBucket,
                LastOverlayPositionX = settings.LastOverlayPositionX,
                LastOverlayPositionY = settings.LastOverlayPositionY,
                HasSavedOverlayPosition = settings.HasSavedOverlayPosition && settings.LastOverlayPositionX >= 0f && settings.LastOverlayPositionY >= 0f,
                VisualThemeId = CompanionSkinCatalog.Normalize(settings.VisualThemeId)
            };
        }

        private static bool ShouldMigrateLegacyDesktopSettings(DesktopCompanionSettings profileSettings, DesktopCompanionSettings legacySettings)
        {
            if (legacySettings == null || profileSettings == null)
            {
                return false;
            }

            return legacySettings.HasSavedOverlayPosition &&
                   !profileSettings.HasSavedOverlayPosition &&
                   legacySettings.LastOverlayPositionX >= 0f &&
                   legacySettings.LastOverlayPositionY >= 0f;
        }

        private static string NextRepositoryAlias(SaveData saveData)
        {
            var count = Math.Max(1, (saveData?.RepositoryCompanionProfiles?.Count ?? 0) + 1);
            return "Repository " + count;
        }

        private static string SafeRepositoryAlias(string repositoryRootPath, string fallback)
        {
            var alias = string.Empty;
            try
            {
                alias = Path.GetFileName((repositoryRootPath ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            }
            catch (ArgumentException)
            {
                alias = string.Empty;
            }

            alias = string.IsNullOrWhiteSpace(alias) ? fallback : alias.Trim();
            alias = new string(alias.Where(character => char.IsLetterOrDigit(character) || character == '-' || character == '_' || character == ' ').Take(48).ToArray()).Trim();
            if (string.IsNullOrWhiteSpace(alias) || new ForbiddenFieldDetector().ContainsSensitiveString(alias))
            {
                alias = string.IsNullOrWhiteSpace(fallback) ? "Local Repository" : fallback;
            }

            return alias;
        }

        private static string LastActivityBucket(AgentWorkSession session)
        {
            var agentBucket = session?.AgentActivitySummary?.DayBucket;
            if (!string.IsNullOrWhiteSpace(agentBucket))
            {
                return agentBucket;
            }

            var gitBucket = session?.GitChangeSummary?.AnalysisTimeBucket;
            if (!string.IsNullOrWhiteSpace(gitBucket))
            {
                return gitBucket;
            }

            return session == null ? string.Empty : session.EndedAt.UtcDateTime.ToString("yyyy-MM-dd");
        }

        private static string SafeProvider(AgentWorkSession session)
        {
            if (session == null)
            {
                return "UNKNOWN";
            }

            var providerType = session.AgentActivitySummary?.ProviderType ?? AgentProviderType.Unknown;
            if (providerType != AgentProviderType.Unknown)
            {
                return providerType == AgentProviderType.Claude ? "ClaudeCode" : providerType.ToString();
            }

            return string.IsNullOrWhiteSpace(session.SourceProvider) ? "UNKNOWN" : session.SourceProvider.Trim();
        }

        private static void IncrementProviderMix(RepositoryCompanionProfile profile, string provider)
        {
            profile.SourceProviderMix = profile.SourceProviderMix ?? new List<SourceProviderMixEntry>();
            provider = string.IsNullOrWhiteSpace(provider) ? "UNKNOWN" : provider.Trim();
            var entry = profile.SourceProviderMix.FirstOrDefault(item => string.Equals(item.Provider, provider, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
            {
                profile.SourceProviderMix.Add(new SourceProviderMixEntry { Provider = provider, ApprovedAggregateCount = 1 });
                return;
            }

            entry.ApprovedAggregateCount += 1;
        }
    }
}
