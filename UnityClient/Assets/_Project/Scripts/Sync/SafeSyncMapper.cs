using System;
using System.Collections.Generic;
using System.Linq;
using TokenForge.Client.Domain;
using TokenForge.Client.Privacy;

namespace TokenForge.Client.Sync
{
    public sealed class SafeSyncMapper
    {
        private const int MaxLevel = 100;
        private const int MaxTotalExp = 100000000;
        private const int MaxStatValue = 999;
        private const int MaxUnlockedItems = 200;
        private const int MaxSessionsPerPull = 500;
        private const int MaxAchievementsPerPull = 200;
        private const int MaxChangedFileCount = 500;
        private const int MaxActionCount = 10000;
        private const int MaxAchievementProgress = 10000;
        private const int MaxProviderCount = 8;

        private readonly SyncPayloadSanitizer sanitizer;

        public SafeSyncMapper(SyncPayloadSanitizer sanitizer = null)
        {
            this.sanitizer = sanitizer ?? new SyncPayloadSanitizer();
        }

        public SafeSyncPayload ToPayload(SaveData saveData)
        {
            if (saveData == null) throw new ArgumentNullException(nameof(saveData));

            var payload = new SafeSyncPayload
            {
                SyncVersion = saveData.SyncState?.SyncVersion ?? 1,
                UpdatedAt = DateTimeOffset.UtcNow,
                CharacterSnapshot = ToCharacterSnapshot(saveData.CharacterProfile ?? new CharacterProfile(), saveData.SyncState?.SyncVersion ?? 1),
                SessionSummary = ToSessionSummary(saveData, saveData.SyncState?.SyncVersion ?? 1),
                Settings = ToSettings(saveData.PrivacyPreferences ?? new PrivacyPreferences(), saveData.SyncState?.SyncVersion ?? 1)
            };

            sanitizer.ThrowIfUnsafe(payload);
            return payload;
        }

        public SafeSyncPullRequest ToPullRequest(SaveData saveData)
        {
            if (saveData == null) throw new ArgumentNullException(nameof(saveData));

            var request = new SafeSyncPullRequest
            {
                SyncVersion = saveData.SyncState?.SyncVersion ?? 1,
                LastSyncAt = saveData.SyncState?.LastSyncAt
            };

            sanitizer.ThrowIfUnsafe(request);
            return request;
        }

        public void MergePullResponse(SaveData saveData, SafeSyncPullResponse response)
        {
            if (saveData == null) throw new ArgumentNullException(nameof(saveData));
            if (response == null) return;

            sanitizer.ThrowIfUnsafe(response);

            if (response.CharacterSnapshot != null)
            {
                if (saveData.CharacterProfile == null)
                {
                    saveData.CharacterProfile = new CharacterProfile();
                }

                MergeCharacter(saveData.CharacterProfile, response.CharacterSnapshot);
            }

            MergeSessions(saveData, response.Sessions);
            MergeAchievements(saveData, response.Achievements);

            if (saveData.SyncState == null)
            {
                saveData.SyncState = new SyncState();
            }

            saveData.SyncState.SyncVersion = Math.Max(saveData.SyncState.SyncVersion, Math.Max(1, response.SyncVersion));
            saveData.SyncState.LastSyncAt = DateTimeOffset.UtcNow;
            saveData.SyncState.PendingQueueCount = 0;
            saveData.SyncState.LastErrorCode = string.Empty;
        }

        private static CharacterSnapshotSyncRequest ToCharacterSnapshot(CharacterProfile profile, int syncVersion)
        {
            return new CharacterSnapshotSyncRequest
            {
                SyncVersion = syncVersion,
                UpdatedAt = DateTimeOffset.UtcNow,
                CharacterId = profile.CharacterId,
                DisplayName = profile.DisplayName,
                Level = Clamp(profile.Level, 1, MaxLevel),
                TotalExp = Clamp(profile.TotalExp, 0, MaxTotalExp),
                EvolutionType = profile.CurrentEvolutionType,
                CharacterStats = ToStatsDto(profile.Stats),
                UnlockedItems = (profile.UnlockedItems ?? new List<string>())
                    .Take(MaxUnlockedItems)
                    .ToList()
            };
        }

        private static SessionSummaryUploadRequest ToSessionSummary(SaveData saveData, int syncVersion)
        {
            var sessions = (saveData.WorkSessionSummaries ?? new List<AgentWorkSession>()).Select(session => new SessionSummaryDto
            {
                SessionId = session.SessionId,
                AgentType = session.AgentType,
                WorkType = session.WorkType,
                StartedAt = session.StartedAt,
                EndedAt = session.EndedAt,
                TokenUsageBucket = session.TokenUsageBucket,
                ChangedFileCount = Clamp(session.GitChangeSummary?.ChangedFileCount ?? 0, 0, MaxChangedFileCount),
                AddedLineBucket = session.GitChangeSummary?.AddedLineBucket ?? LineChangeBucket.Unknown,
                DeletedLineBucket = session.GitChangeSummary?.DeletedLineBucket ?? LineChangeBucket.Unknown,
                PromptCount = Clamp(session.ActionSummary?.PromptCount ?? 0, 0, MaxActionCount),
                ToolCallCount = Clamp(session.ActionSummary?.ToolCallCount ?? 0, 0, MaxActionCount),
                FileEditCount = Clamp(session.ActionSummary?.FileEditCount ?? 0, 0, MaxActionCount),
                CommandRunCount = Clamp(session.ActionSummary?.CommandRunCount ?? 0, 0, MaxActionCount),
                TestRunCount = Clamp(session.ActionSummary?.TestRunCount ?? 0, 0, MaxActionCount),
                BuildRunCount = Clamp(session.ActionSummary?.BuildRunCount ?? 0, 0, MaxActionCount),
                ResultStatus = session.ResultStatus,
                Confidence = session.Confidence,
                ProjectPathHash = session.GitChangeSummary?.ProjectPathHash ?? string.Empty,
                AgentProviderType = session.AgentActivitySummary?.ProviderType ?? AgentProviderType.Unknown,
                AgentSourceIdentifierHash = session.AgentActivitySummary?.SourceIdentifierHash ?? string.Empty,
                AgentActivityDayBucket = session.AgentActivitySummary?.DayBucket ?? string.Empty,
                AgentSessionCountBucket = session.AgentActivitySummary?.SessionCountBucket ?? CountBucket.Unknown,
                AgentInteractionCountBucket = session.AgentActivitySummary?.InteractionCountBucket ?? CountBucket.Unknown,
                AgentCodingActivityBucket = session.AgentActivitySummary?.EstimatedCodingActivityBucket ?? CountBucket.Unknown,
                AgentToolUsageCategoryBuckets = CloneToolBuckets(session.AgentActivitySummary?.ToolUsageCategoryBuckets),
                AgentLanguageCategoryBuckets = CloneLanguageBuckets(session.AgentActivitySummary?.LanguageCategoryBuckets),
                SourceProvider = ToBackendSourceProvider(session),
                SourceProviders = (session.SourceProviders ?? new List<string>())
                    .Select(ToBackendSourceProvider)
                    .Where(provider => !string.IsNullOrEmpty(provider))
                    .Distinct()
                    .Take(MaxProviderCount)
                    .ToList()
            }).ToList();

            return new SessionSummaryUploadRequest
            {
                SyncVersion = syncVersion,
                UpdatedAt = DateTimeOffset.UtcNow,
                Sessions = sessions,
                WorkTypeDistribution = sessions.GroupBy(session => session.WorkType)
                    .Select(group => new WorkTypeDistributionDto { WorkType = group.Key, Count = group.Count() })
                    .ToList()
            };
        }

        private static SettingsSyncRequest ToSettings(PrivacyPreferences preferences, int syncVersion)
        {
            return new SettingsSyncRequest
            {
                SyncVersion = syncVersion,
                UpdatedAt = DateTimeOffset.UtcNow,
                CloudSyncEnabled = preferences.CloudSyncEnabled,
                TelemetryOptIn = preferences.TelemetryOptIn,
                MaskProjectAlias = preferences.MaskProjectAlias
            };
        }

        private static CharacterStatsDto ToStatsDto(CharacterStats stats)
        {
            stats = stats ?? CharacterStats.Zero();
            return new CharacterStatsDto
            {
                Logic = Clamp(stats.Logic, 0, MaxStatValue),
                Debug = Clamp(stats.Debug, 0, MaxStatValue),
                Architecture = Clamp(stats.Architecture, 0, MaxStatValue),
                Design = Clamp(stats.Design, 0, MaxStatValue),
                Stability = Clamp(stats.Stability, 0, MaxStatValue),
                Velocity = Clamp(stats.Velocity, 0, MaxStatValue),
                Creativity = Clamp(stats.Creativity, 0, MaxStatValue),
                Efficiency = Clamp(stats.Efficiency, 0, MaxStatValue),
                Stress = Clamp(stats.Stress, 0, MaxStatValue)
            };
        }

        private static void MergeCharacter(CharacterProfile profile, CharacterSnapshotSyncRequest snapshot)
        {
            profile.CharacterId = SafeIdentifier(snapshot.CharacterId);
            profile.DisplayName = SafeLabel(snapshot.DisplayName, profile.DisplayName, 32);
            profile.Level = Clamp(snapshot.Level, 1, MaxLevel);
            profile.TotalExp = Clamp(snapshot.TotalExp, 0, MaxTotalExp);
            profile.CurrentEvolutionType = snapshot.EvolutionType;
            profile.Stats = FromStatsDto(snapshot.CharacterStats);
            profile.UnlockedItems = (snapshot.UnlockedItems ?? new List<string>())
                .Select(item => SafeIdentifier(item))
                .Where(item => !string.IsNullOrEmpty(item))
                .Distinct()
                .Take(MaxUnlockedItems)
                .ToList();
        }

        private static void MergeSessions(SaveData saveData, List<SessionSummaryDto> inboundSessions)
        {
            if (inboundSessions == null || inboundSessions.Count == 0)
            {
                return;
            }

            if (saveData.WorkSessionSummaries == null)
            {
                saveData.WorkSessionSummaries = new List<AgentWorkSession>();
            }

            var existingById = saveData.WorkSessionSummaries
                .Where(session => !string.IsNullOrWhiteSpace(session.SessionId))
                .GroupBy(session => session.SessionId)
                .ToDictionary(group => group.Key, group => group.First());

            foreach (var sessionDto in inboundSessions.Take(MaxSessionsPerPull))
            {
                var sessionId = SafeIdentifier(sessionDto.SessionId);
                if (string.IsNullOrEmpty(sessionId))
                {
                    continue;
                }

                var mapped = ToSession(sessionDto, sessionId);
                if (existingById.ContainsKey(sessionId))
                {
                    var index = saveData.WorkSessionSummaries.IndexOf(existingById[sessionId]);
                    saveData.WorkSessionSummaries[index] = mapped;
                    existingById[sessionId] = mapped;
                }
                else
                {
                    saveData.WorkSessionSummaries.Add(mapped);
                    existingById.Add(sessionId, mapped);
                }
            }
        }

        private static void MergeAchievements(SaveData saveData, List<SafeAchievementDto> inboundAchievements)
        {
            if (inboundAchievements == null || inboundAchievements.Count == 0)
            {
                return;
            }

            if (saveData.Achievements == null)
            {
                saveData.Achievements = new List<AchievementProgress>();
            }

            var existingById = saveData.Achievements
                .Where(achievement => !string.IsNullOrWhiteSpace(achievement.AchievementId))
                .GroupBy(achievement => achievement.AchievementId)
                .ToDictionary(group => group.Key, group => group.First());

            foreach (var achievementDto in inboundAchievements.Take(MaxAchievementsPerPull))
            {
                var achievementId = SafeIdentifier(achievementDto.AchievementId);
                if (string.IsNullOrEmpty(achievementId))
                {
                    continue;
                }

                var mapped = new AchievementProgress
                {
                    AchievementId = achievementId,
                    UnlockedAt = achievementDto.UnlockedAt,
                    Progress = Clamp(achievementDto.Progress, 0, MaxAchievementProgress)
                };

                if (existingById.ContainsKey(achievementId))
                {
                    var index = saveData.Achievements.IndexOf(existingById[achievementId]);
                    saveData.Achievements[index] = mapped;
                    existingById[achievementId] = mapped;
                }
                else
                {
                    saveData.Achievements.Add(mapped);
                    existingById.Add(achievementId, mapped);
                }
            }
        }

        private static AgentWorkSession ToSession(SessionSummaryDto sessionDto, string sessionId)
        {
            return new AgentWorkSession
            {
                SessionId = sessionId,
                AgentType = sessionDto.AgentType,
                WorkType = sessionDto.WorkType,
                StartedAt = sessionDto.StartedAt,
                EndedAt = sessionDto.EndedAt >= sessionDto.StartedAt ? sessionDto.EndedAt : sessionDto.StartedAt,
                TokenUsageBucket = sessionDto.TokenUsageBucket,
                ActionSummary = new AgentActionSummary
                {
                    PromptCount = Clamp(sessionDto.PromptCount, 0, MaxActionCount),
                    ToolCallCount = Clamp(sessionDto.ToolCallCount, 0, MaxActionCount),
                    FileEditCount = Clamp(sessionDto.FileEditCount, 0, MaxActionCount),
                    CommandRunCount = Clamp(sessionDto.CommandRunCount, 0, MaxActionCount),
                    TestRunCount = Clamp(sessionDto.TestRunCount, 0, MaxActionCount),
                    BuildRunCount = Clamp(sessionDto.BuildRunCount, 0, MaxActionCount)
                },
                GitChangeSummary = new GitChangeSummary
                {
                    ChangedFileCount = Clamp(sessionDto.ChangedFileCount, 0, MaxChangedFileCount),
                    AddedLineBucket = sessionDto.AddedLineBucket,
                    DeletedLineBucket = sessionDto.DeletedLineBucket,
                    ProjectPathHash = SafeIdentifier(sessionDto.ProjectPathHash)
                },
                AgentActivitySummary = new AgentActivitySummary
                {
                    ProviderType = sessionDto.AgentProviderType,
                    SourceIdentifierHash = SafeIdentifier(sessionDto.AgentSourceIdentifierHash),
                    DayBucket = SafeDayBucket(sessionDto.AgentActivityDayBucket),
                    SessionCountBucket = sessionDto.AgentSessionCountBucket,
                    InteractionCountBucket = sessionDto.AgentInteractionCountBucket,
                    EstimatedCodingActivityBucket = sessionDto.AgentCodingActivityBucket,
                    ToolUsageCategoryBuckets = CloneToolBuckets(sessionDto.AgentToolUsageCategoryBuckets),
                    LanguageCategoryBuckets = CloneLanguageBuckets(sessionDto.AgentLanguageCategoryBuckets),
                    ConfidenceLevel = ToConfidenceLevel(sessionDto.Confidence)
                },
                ResultStatus = sessionDto.ResultStatus,
                Confidence = sessionDto.Confidence,
                SourceProvider = ToBackendSourceProvider(sessionDto.SourceProvider),
                SourceProviders = BuildInboundSourceProviders(sessionDto)
                    .Select(provider => SafeIdentifier(provider))
                    .Where(provider => !string.IsNullOrEmpty(provider))
                    .Distinct()
                    .Take(MaxProviderCount)
                    .ToList(),
                UserReviewed = true
            };
        }

        private static List<string> BuildInboundSourceProviders(SessionSummaryDto sessionDto)
        {
            var providers = new List<string>();
            if (!string.IsNullOrWhiteSpace(sessionDto.SourceProvider))
            {
                providers.Add(ToBackendSourceProvider(sessionDto.SourceProvider));
            }

            if (sessionDto.SourceProviders != null)
            {
                providers.AddRange(sessionDto.SourceProviders.Select(ToBackendSourceProvider));
            }

            return providers;
        }

        private static List<AgentToolUsageCategoryBucket> CloneToolBuckets(List<AgentToolUsageCategoryBucket> buckets)
        {
            return (buckets ?? new List<AgentToolUsageCategoryBucket>())
                .Take(12)
                .Select(item => new AgentToolUsageCategoryBucket
                {
                    Category = item.Category,
                    CountBucket = item.CountBucket
                })
                .ToList();
        }

        private static List<AgentLanguageCategoryBucket> CloneLanguageBuckets(List<AgentLanguageCategoryBucket> buckets)
        {
            return (buckets ?? new List<AgentLanguageCategoryBucket>())
                .Take(12)
                .Select(item => new AgentLanguageCategoryBucket
                {
                    Category = item.Category,
                    CountBucket = item.CountBucket
                })
                .ToList();
        }

        private static ConfidenceLevel ToConfidenceLevel(ProviderConfidence confidence)
        {
            switch (confidence)
            {
                case ProviderConfidence.High: return ConfidenceLevel.High;
                case ProviderConfidence.Medium: return ConfidenceLevel.Medium;
                case ProviderConfidence.Low: return ConfidenceLevel.Low;
                default: return ConfidenceLevel.Unknown;
            }
        }

        private static string ToBackendSourceProvider(AgentWorkSession session)
        {
            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(session?.SourceProvider))
            {
                candidates.Add(session.SourceProvider);
            }

            if (session?.SourceProviders != null)
            {
                candidates.AddRange(session.SourceProviders);
            }

            if (session != null)
            {
                candidates.Add(session.AgentType.ToString());
            }

            foreach (var candidate in candidates)
            {
                var normalized = ToBackendSourceProvider(candidate);
                if (!string.Equals(normalized, "UNKNOWN", StringComparison.Ordinal))
                {
                    return normalized;
                }
            }

            return "UNKNOWN";
        }

        private static string ToBackendSourceProvider(string provider)
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                return "UNKNOWN";
            }

            var normalized = provider.Trim().Replace("-", string.Empty).Replace("_", string.Empty);
            if (string.Equals(normalized, "UNITYCLIENT", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "UNITY", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "SYNC", StringComparison.OrdinalIgnoreCase))
            {
                return "UNITY_CLIENT";
            }

            if (string.Equals(normalized, "AIAGENT", StringComparison.OrdinalIgnoreCase))
            {
                return "AI_AGENT";
            }

            if (normalized.IndexOf("CODEX", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "CODEX";
            }

            if (normalized.IndexOf("CLAUDE", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "CLAUDE";
            }

            if (normalized.IndexOf("GIT", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "GIT";
            }

            if (normalized.IndexOf("MANUAL", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "MANUAL";
            }

            if (string.Equals(normalized, "UNKNOWN", StringComparison.OrdinalIgnoreCase))
            {
                return "UNKNOWN";
            }

            return "UNKNOWN";
        }

        private static CharacterStats FromStatsDto(CharacterStatsDto stats)
        {
            stats = stats ?? new CharacterStatsDto();
            return new CharacterStats
            {
                Logic = Clamp(stats.Logic, 0, MaxStatValue),
                Debug = Clamp(stats.Debug, 0, MaxStatValue),
                Architecture = Clamp(stats.Architecture, 0, MaxStatValue),
                Design = Clamp(stats.Design, 0, MaxStatValue),
                Stability = Clamp(stats.Stability, 0, MaxStatValue),
                Velocity = Clamp(stats.Velocity, 0, MaxStatValue),
                Creativity = Clamp(stats.Creativity, 0, MaxStatValue),
                Efficiency = Clamp(stats.Efficiency, 0, MaxStatValue),
                Stress = Clamp(stats.Stress, 0, MaxStatValue)
            };
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static string SafeIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var filtered = new string(value.Trim().Where(character =>
                char.IsLetterOrDigit(character) ||
                character == '_' ||
                character == '-' ||
                character == '.').ToArray());

            return filtered.Length > 64 ? filtered.Substring(0, 64) : filtered;
        }

        private static string SafeDayBucket(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var trimmed = value.Trim();
            if (trimmed.Length == 10 &&
                trimmed[4] == '-' &&
                trimmed[7] == '-' &&
                trimmed.Where((character, index) => index != 4 && index != 7).All(char.IsDigit))
            {
                return trimmed;
            }

            return string.Empty;
        }

        private static string SafeLabel(string value, string fallback, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                value = fallback ?? string.Empty;
            }

            var filtered = new string(value.Trim().Where(character =>
                char.IsLetterOrDigit(character) ||
                character == ' ' ||
                character == '_' ||
                character == '-').ToArray());

            if (string.IsNullOrWhiteSpace(filtered))
            {
                filtered = fallback ?? string.Empty;
            }

            return filtered.Length > maxLength ? filtered.Substring(0, maxLength) : filtered;
        }
    }
}
