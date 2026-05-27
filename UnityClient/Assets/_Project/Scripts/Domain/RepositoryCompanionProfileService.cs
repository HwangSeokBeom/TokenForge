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

            foreach (var profile in saveData.RepositoryCompanionProfiles)
            {
                if (IsStaleFallbackProfile(profile) && profile.ArchivedAtUtc == null)
                {
                    profile.ConnectionSource = string.IsNullOrWhiteSpace(profile.ConnectionSource) ? "debugFallback" : profile.ConnectionSource;
                    profile.ArchivedAtUtc = DateTimeOffset.UtcNow;
                    profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
                }
            }

            saveData.ConnectedProjects = saveData.ConnectedProjects ?? new List<ConnectedProject>();
            foreach (var project in saveData.ConnectedProjects)
            {
                if (project == null)
                {
                    continue;
                }

                if (IsStaleFallbackProject(project))
                {
                    project.IsActive = false;
                    project.IsArchived = true;
                    project.ConnectionSource = string.IsNullOrWhiteSpace(project.ConnectionSource) ? "debugFallback" : project.ConnectionSource;
                }
            }

            if (string.IsNullOrWhiteSpace(saveData.SelectedRepositoryHash) ||
                saveData.RepositoryCompanionProfiles.All(profile => profile.ArchivedAtUtc != null || !string.Equals(profile.RepositoryHash, saveData.SelectedRepositoryHash, StringComparison.Ordinal)))
            {
                var nextActive = saveData.RepositoryCompanionProfiles.FirstOrDefault(profile => profile.ArchivedAtUtc == null);
                saveData.SelectedRepositoryHash = nextActive?.RepositoryHash ?? string.Empty;
            }

            var selected = saveData.RepositoryCompanionProfiles.FirstOrDefault(profile => profile.ArchivedAtUtc == null && string.Equals(profile.RepositoryHash, saveData.SelectedRepositoryHash, StringComparison.Ordinal)) ??
                           saveData.RepositoryCompanionProfiles.FirstOrDefault(profile => profile.ArchivedAtUtc == null);
            if (selected != null)
            {
                if (ShouldMigrateLegacyDesktopSettings(selected.DesktopCompanionSettings, saveData.DesktopCompanionSettings))
                {
                    selected.DesktopCompanionSettings = CloneDesktopCompanionSettings(saveData.DesktopCompanionSettings);
                }

                saveData.CompanionState = CompanionProgressionRules.Normalize(selected.CompanionState);
                saveData.DesktopCompanionSettings = CloneDesktopCompanionSettings(selected.DesktopCompanionSettings);
            }
            else
            {
                saveData.CompanionState = CompanionState.CreateDefault();
                saveData.DesktopCompanionSettings = CloneDesktopCompanionSettings(saveData.DesktopCompanionSettings);
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
                profile = CreateProfile(hash, SafeRepositoryAlias(repositoryRootPath));
                saveData.RepositoryCompanionProfiles.Add(profile);
            }

            profile.ArchivedAtUtc = null;
            profile.ApprovedAtUtc = profile.ApprovedAtUtc ?? DateTimeOffset.UtcNow;
            profile.ConnectionSource = "userSelected";
            profile.SafeRepositoryAlias = SafeRepositoryAlias(repositoryRootPath);
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
                   saveData.RepositoryCompanionProfiles.FirstOrDefault(profile => profile.ArchivedAtUtc == null);
        }

        public static RepositoryCompanionProfile ApplyApprovedGrowth(
            SaveData saveData,
            AgentWorkSession session,
            IEnumerable<AgentWorkSession> repositorySessions,
            IEnumerable<CharacterGrowthResult> repositoryGrowthHistory)
        {
            saveData = Normalize(saveData);
            var profile = GetSelectedProfile(saveData);
            if (profile == null && !string.IsNullOrWhiteSpace(SafeRepositoryHashForSession(session)))
            {
                profile = CreateProfile(SafeRepositoryHashForSession(session), NextRepositoryAlias(saveData), saveData.CompanionState);
                saveData.RepositoryCompanionProfiles.Add(profile);
                saveData.SelectedRepositoryHash = profile.RepositoryHash;
            }

            if (profile == null)
            {
                return null;
            }

            var sessionHash = SafeRepositoryHashForSession(session);
            if (!string.IsNullOrWhiteSpace(sessionHash))
            {
                profile.RepositoryHash = sessionHash;
                saveData.SelectedRepositoryHash = sessionHash;
            }

            var previous = CompanionProgressionRules.Normalize(profile.CompanionState);
            var calculated = CompanionProgressionRules.CalculateState(repositorySessions, repositoryGrowthHistory);
            var repositoryLifetimeXp = Math.Max(0, calculated.TotalLifetimeXp);
            var xpDelta = Math.Max(0, repositoryLifetimeXp - Math.Max(0, previous.TotalLifetimeXp));
            calculated.Level = previous.Level;
            calculated.CurrentXp = Math.Max(0, previous.CurrentXp) + xpDelta;
            calculated.TotalLifetimeXp = repositoryLifetimeXp;
            calculated.TotalXp = repositoryLifetimeXp;
            calculated.XpRequiredForNextLevel = CompanionProgressionRules.XpRequiredForLevel(calculated.Level);
            calculated.CanLevelUp = calculated.CurrentXp >= calculated.XpRequiredForNextLevel;
            profile.CompanionState = CompanionProgressionRules.Normalize(calculated);
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
                saveData.SelectedRepositoryHash = string.Empty;
                saveData.CompanionState = CompanionState.CreateDefault();
                saveData.DesktopCompanionSettings = CloneDesktopCompanionSettings(saveData.DesktopCompanionSettings);
                return Result.Success();
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
            return CreateProfile(DefaultLocalRepositoryHash, "Repository", legacyCompanion);
        }

        private static RepositoryCompanionProfile CreateProfile(string repositoryHash, string alias, CompanionState companionState = null)
        {
            var now = DateTimeOffset.UtcNow;
            return new RepositoryCompanionProfile
            {
                SchemaVersion = 1,
                RepositoryHash = repositoryHash ?? string.Empty,
                SafeRepositoryAlias = string.IsNullOrWhiteSpace(alias) ? "Repository" : alias.Trim(),
                ApprovedAtUtc = DateTimeOffset.UtcNow,
                ConnectionSource = string.Equals(repositoryHash, DefaultLocalRepositoryHash, StringComparison.Ordinal) ? "debugFallback" : "userSelected",
                CompanionId = Guid.NewGuid().ToString("N"),
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
                ? "Repository"
                : profile.SafeRepositoryAlias.Trim();
            profile.ConnectionSource = string.IsNullOrWhiteSpace(profile.ConnectionSource) ? "userSelected" : profile.ConnectionSource.Trim();
            profile.CompanionId = string.IsNullOrWhiteSpace(profile.CompanionId) ? Guid.NewGuid().ToString("N") : profile.CompanionId;
            profile.CompanionState = CompanionProgressionRules.Normalize(profile.CompanionState);
            profile.DesktopCompanionSettings = CloneDesktopCompanionSettings(profile.DesktopCompanionSettings);
            profile.SourceProviderMix = profile.SourceProviderMix ?? new List<SourceProviderMixEntry>();
            profile.CreatedAtUtc = profile.CreatedAtUtc == default(DateTimeOffset) ? DateTimeOffset.UtcNow : profile.CreatedAtUtc;
            profile.UpdatedAtUtc = profile.UpdatedAtUtc == default(DateTimeOffset) ? profile.CreatedAtUtc : profile.UpdatedAtUtc;
        }

        private static bool IsStaleFallbackProfile(RepositoryCompanionProfile profile)
        {
            if (profile == null)
            {
                return false;
            }

            return string.Equals(profile.RepositoryHash, DefaultLocalRepositoryHash, StringComparison.Ordinal) ||
                   string.Equals(profile.SafeRepositoryAlias, "Local Repository", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(profile.ConnectionSource, "auto", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(profile.ConnectionSource, "default", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(profile.ConnectionSource, "unknown", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(profile.ConnectionSource, "dev", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(profile.ConnectionSource, "debugFallback", StringComparison.OrdinalIgnoreCase) ||
                   (string.Equals(profile.ConnectionSource, "userSelected", StringComparison.OrdinalIgnoreCase) &&
                    profile.ApprovedAtUtc == null);
        }

        public static bool IsStaleFallbackProject(ConnectedProject project)
        {
            if (project == null)
            {
                return false;
            }

            var displayName = project.DisplayName ?? string.Empty;
            var alias = project.ProjectAlias ?? string.Empty;
            var source = project.ConnectionSource ?? string.Empty;
            var hasApprovalEvidence = project.ApprovedAt != null &&
                                      (!string.IsNullOrWhiteSpace(project.PathHash) ||
                                       !string.IsNullOrWhiteSpace(project.ProjectPathHash));
            return string.Equals(displayName, "Local Repository", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(alias, "Local Repository", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(source, "auto", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(source, "default", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(source, "unknown", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(source, "dev", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(source, "debugFallback", StringComparison.OrdinalIgnoreCase) ||
                   (string.Equals(source, "userSelected", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(displayName, "Local Repository", StringComparison.OrdinalIgnoreCase) &&
                    !hasApprovalEvidence);
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

        public static string SafeRepositoryAlias(string repositoryRootPath)
        {
            var alias = string.Empty;
            try
            {
                var trimmed = (repositoryRootPath ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                alias = Path.GetFileName(trimmed);
            }
            catch (ArgumentException)
            {
                alias = string.Empty;
            }

            alias = new string(alias.Where(character => char.IsLetterOrDigit(character) || character == '-' || character == '_' || character == ' ').Take(48).ToArray()).Trim();
            if (string.IsNullOrWhiteSpace(alias) || string.Equals(alias, "Local Repository", StringComparison.OrdinalIgnoreCase) || new ForbiddenFieldDetector().ContainsSensitiveString(alias))
            {
                alias = "Repository";
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
