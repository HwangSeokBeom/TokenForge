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

        public SafeActivitySessionsContractRequest ToActivitySessionsContractRequest(
            SaveData saveData,
            string clientSyncId = "unity-sync-v1-001")
        {
            if (saveData == null) throw new ArgumentNullException(nameof(saveData));

            var payload = ToPayload(saveData);
            var sourceBySessionId = (saveData.WorkSessionSummaries ?? new List<AgentWorkSession>())
                .Where(session => !string.IsNullOrWhiteSpace(session.SessionId))
                .GroupBy(session => session.SessionId)
                .ToDictionary(group => group.Key, group => group.First());

            return new SafeActivitySessionsContractRequest
            {
                SchemaVersion = 1,
                ClientSyncId = clientSyncId,
                Sessions = (payload.SessionSummary?.Sessions ?? new List<SessionSummaryDto>())
                    .Select(session =>
                    {
                        sourceBySessionId.TryGetValue(session.SessionId, out var sourceSession);
                        return ToContractActivitySession(session, sourceSession);
                    })
                    .ToList()
            };
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

        private static SafeActivitySessionContractDto ToContractActivitySession(
            SessionSummaryDto session,
            AgentWorkSession sourceSession)
        {
            return new SafeActivitySessionContractDto
            {
                ClientSessionId = SafeContractId(session.SessionId),
                SourceProvider = ToContractSourceProvider(session.SourceProvider, sourceSession),
                DayBucket = ToContractDayBucket(session, sourceSession),
                TimeBucket = ToContractTimeBucket(session.StartedAt),
                Confidence = ToContractConfidence(session.Confidence),
                WarningIds = BuildContractWarningIds(sourceSession),
                AnalyzerVersion = ToContractAnalyzerVersion(sourceSession),
                ParserVersion = ToContractParserVersion(sourceSession),
                HashedRepositoryId = ToContractHash(session.ProjectPathHash),
                ChangeCountBucket = ToContractChangedFileBucket(session.ChangedFileCount),
                LineCountBucket = ToContractLineBucket(session.AddedLineBucket, session.DeletedLineBucket),
                CommitCountBucket = ToContractCountBucket(sourceSession?.GitChangeSummary?.CommitCountBucket),
                SessionCountBucket = ToContractCountBucket(session.AgentSessionCountBucket),
                InteractionCountBucket = ToContractCountBucket(session.AgentInteractionCountBucket),
                ActivityCategory = ToContractWorkType(session.WorkType),
                DurationBucket = ToContractDurationBucket(sourceSession?.ActionSummary?.DurationBucket),
                CategoryBuckets = BuildCategoryBuckets(session),
                LanguageBuckets = BuildLanguageBuckets(session),
                ToolBuckets = BuildToolBuckets(session)
            };
        }

        private static List<string> BuildContractWarningIds(AgentWorkSession sourceSession)
        {
            var warnings = new List<string>();
            if (sourceSession?.Warnings != null)
            {
                warnings.AddRange(sourceSession.Warnings);
            }

            if (sourceSession?.GitChangeSummary?.PrivacyWarnings != null)
            {
                warnings.AddRange(sourceSession.GitChangeSummary.PrivacyWarnings);
            }

            if (sourceSession?.AgentActivitySummary?.WarningIds != null)
            {
                warnings.AddRange(sourceSession.AgentActivitySummary.WarningIds);
            }

            return warnings
                .Select(ToContractEnumKey)
                .Where(value => !string.IsNullOrEmpty(value))
                .Distinct()
                .Take(25)
                .ToList();
        }

        private static string ToContractAnalyzerVersion(AgentWorkSession sourceSession)
        {
            var analyzerVersion = sourceSession?.GitChangeSummary?.AnalyzerVersion;
            if (string.IsNullOrWhiteSpace(analyzerVersion))
            {
                analyzerVersion = sourceSession?.AgentActivitySummary?.AnalyzerVersion;
            }

            return SafeContractVersion(analyzerVersion);
        }

        private static string ToContractParserVersion(AgentWorkSession sourceSession)
        {
            return SafeContractVersion(sourceSession?.ParserVersion);
        }

        private static string ToContractSourceProvider(string mappedProvider, AgentWorkSession sourceSession)
        {
            var provider = mappedProvider;
            if (string.IsNullOrWhiteSpace(provider) || string.Equals(provider, "UNKNOWN", StringComparison.OrdinalIgnoreCase))
            {
                provider = ToBackendSourceProvider(sourceSession);
            }

            if (string.Equals(provider, "UNKNOWN", StringComparison.OrdinalIgnoreCase))
            {
                return "UNKNOWN_AGENT";
            }

            if (string.Equals(provider, "MANUAL", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(provider, "GIT", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(provider, "CLAUDE", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(provider, "CODEX", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(provider, "UNKNOWN_AGENT", StringComparison.OrdinalIgnoreCase))
            {
                return provider.ToUpperInvariant();
            }

            return "UNKNOWN_AGENT";
        }

        private static string ToContractDayBucket(SessionSummaryDto session, AgentWorkSession sourceSession)
        {
            if (!string.IsNullOrWhiteSpace(session.AgentActivityDayBucket))
            {
                var safeDay = SafeDayBucket(session.AgentActivityDayBucket);
                if (!string.IsNullOrEmpty(safeDay))
                {
                    return safeDay;
                }
            }

            if (!string.IsNullOrWhiteSpace(sourceSession?.GitChangeSummary?.AnalysisTimeBucket))
            {
                var safeDay = SafeDayBucket(sourceSession.GitChangeSummary.AnalysisTimeBucket);
                if (!string.IsNullOrEmpty(safeDay))
                {
                    return safeDay;
                }
            }

            return session.StartedAt.UtcDateTime.ToString("yyyy-MM-dd");
        }

        private static string ToContractTimeBucket(DateTimeOffset startedAt)
        {
            return "HOUR_" + startedAt.UtcDateTime.Hour.ToString("00");
        }

        private static string ToContractConfidence(ProviderConfidence confidence)
        {
            switch (confidence)
            {
                case ProviderConfidence.High: return "HIGH";
                case ProviderConfidence.Medium: return "MEDIUM";
                case ProviderConfidence.Low: return "LOW";
                default: return "LOW";
            }
        }

        private static string ToContractHash(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var filtered = new string(value.Trim().Where(Uri.IsHexDigit).ToArray());
            if (filtered.Length < 16)
            {
                return null;
            }

            return filtered.Length > 128 ? filtered.Substring(0, 128) : filtered;
        }

        private static string ToContractChangedFileBucket(int changedFileCount)
        {
            if (changedFileCount <= 0) return "NONE";
            if (changedFileCount == 1) return "ONE";
            if (changedFileCount <= 5) return "FEW";
            if (changedFileCount <= 30) return "MANY";
            return "MASSIVE";
        }

        private static string ToContractLineBucket(LineChangeBucket added, LineChangeBucket deleted)
        {
            var rank = Math.Max(LineBucketRank(added), LineBucketRank(deleted));
            switch (rank)
            {
                case 0: return "NONE";
                case 1: return "FEW";
                case 2: return "MANY";
                default: return "MASSIVE";
            }
        }

        private static int LineBucketRank(LineChangeBucket bucket)
        {
            switch (bucket)
            {
                case LineChangeBucket.None: return 0;
                case LineChangeBucket.Small: return 1;
                case LineChangeBucket.Medium:
                case LineChangeBucket.Large: return 2;
                case LineChangeBucket.Huge: return 3;
                default: return 0;
            }
        }

        private static string ToContractCountBucket(CountBucket? bucket)
        {
            if (!bucket.HasValue)
            {
                return null;
            }

            switch (bucket.Value)
            {
                case CountBucket.None: return "NONE";
                case CountBucket.One: return "ONE";
                case CountBucket.Small: return "FEW";
                case CountBucket.Medium:
                case CountBucket.Large: return "MANY";
                case CountBucket.Huge: return "MASSIVE";
                default: return null;
            }
        }

        private static string ToContractDurationBucket(DurationBucket? bucket)
        {
            if (!bucket.HasValue)
            {
                return null;
            }

            switch (bucket.Value)
            {
                case DurationBucket.Under5Minutes: return "UNDER_5M";
                case DurationBucket.FiveTo15Minutes: return "M_5_15";
                case DurationBucket.FifteenTo60Minutes: return "M_30_60";
                case DurationBucket.OneTo3Hours: return "H_1_2";
                case DurationBucket.Over3Hours: return "H_2_PLUS";
                default: return null;
            }
        }

        private static List<SafeBucketContractDto> BuildCategoryBuckets(SessionSummaryDto session)
        {
            return new List<SafeBucketContractDto>
            {
                new SafeBucketContractDto
                {
                    Key = ToContractWorkType(session.WorkType),
                    CountBucket = "ONE"
                }
            };
        }

        private static List<SafeBucketContractDto> BuildLanguageBuckets(SessionSummaryDto session)
        {
            var buckets = (session.AgentLanguageCategoryBuckets ?? new List<AgentLanguageCategoryBucket>())
                .Select(item => new SafeBucketContractDto
                {
                    Key = ToContractLanguage(item.Category),
                    CountBucket = ToContractCountBucket(item.CountBucket) ?? "FEW"
                })
                .ToList();

            if (buckets.Count == 0)
            {
                buckets.Add(new SafeBucketContractDto
                {
                    Key = "LANG_UNKNOWN",
                    CountBucket = "FEW"
                });
            }

            return buckets;
        }

        private static List<SafeBucketContractDto> BuildToolBuckets(SessionSummaryDto session)
        {
            var buckets = (session.AgentToolUsageCategoryBuckets ?? new List<AgentToolUsageCategoryBucket>())
                .Select(item => new SafeBucketContractDto
                {
                    Key = ToContractTool(item.Category),
                    CountBucket = ToContractCountBucket(item.CountBucket) ?? "FEW"
                })
                .ToList();

            if (buckets.Count == 0)
            {
                buckets.Add(new SafeBucketContractDto
                {
                    Key = session.SourceProvider == "GIT" ? "TOOL_GIT_COMMIT" : "TOOL_UNKNOWN",
                    CountBucket = "ONE"
                });
            }

            return buckets;
        }

        private static string ToContractWorkType(WorkType workType)
        {
            switch (workType)
            {
                case WorkType.Feature: return "WORK_FEATURE";
                case WorkType.Bugfix: return "WORK_BUGFIX";
                case WorkType.Refactor: return "WORK_REFACTOR";
                case WorkType.Test: return "WORK_TEST";
                case WorkType.UIUX: return "WORK_UIUX";
                case WorkType.Docs: return "WORK_DOCS";
                case WorkType.Build: return "WORK_BUILD";
                case WorkType.Chore: return "WORK_CHORE";
                case WorkType.Research: return "WORK_RESEARCH";
                case WorkType.Mixed: return "WORK_MIXED";
                default: return "WORK_UNKNOWN";
            }
        }

        private static string ToContractLanguage(AgentLanguageCategory category)
        {
            switch (category)
            {
                case AgentLanguageCategory.CSharp: return "LANG_CSHARP";
                case AgentLanguageCategory.JavaScript: return "LANG_JAVASCRIPT";
                case AgentLanguageCategory.TypeScript: return "LANG_TYPESCRIPT";
                case AgentLanguageCategory.Python: return "LANG_PYTHON";
                case AgentLanguageCategory.Web: return "LANG_WEB";
                case AgentLanguageCategory.Config: return "LANG_CONFIG";
                case AgentLanguageCategory.Docs: return "LANG_DOCS";
                case AgentLanguageCategory.Test: return "LANG_TEST";
                case AgentLanguageCategory.Shell: return "LANG_SHELL";
                default: return "LANG_UNKNOWN";
            }
        }

        private static string ToContractTool(AgentToolUsageCategory category)
        {
            switch (category)
            {
                case AgentToolUsageCategory.CodeEditing: return "TOOL_EDIT";
                case AgentToolUsageCategory.ShellCommand: return "TOOL_SHELL";
                case AgentToolUsageCategory.TestRun: return "TOOL_TEST";
                case AgentToolUsageCategory.BuildRun: return "TOOL_BUILD";
                case AgentToolUsageCategory.FileNavigation: return "TOOL_NAVIGATION";
                case AgentToolUsageCategory.Search: return "TOOL_SEARCH";
                default: return "TOOL_UNKNOWN";
            }
        }

        private static string SafeContractId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unity-session-unknown";
            }

            var filtered = new string(value.Trim().Where(character =>
                char.IsLetterOrDigit(character) ||
                character == '_' ||
                character == '-' ||
                character == ':').ToArray());

            if (string.IsNullOrWhiteSpace(filtered))
            {
                return "unity-session-unknown";
            }

            return filtered.Length > 128 ? filtered.Substring(0, 128) : filtered;
        }

        private static string SafeContractVersion(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var filtered = new string(value.Trim().Where(character =>
                char.IsLetterOrDigit(character) ||
                character == ':' ||
                character == '.' ||
                character == '_' ||
                character == '-').ToArray());

            if (string.IsNullOrWhiteSpace(filtered))
            {
                return null;
            }

            return filtered.Length > 40 ? filtered.Substring(0, 40) : filtered;
        }

        private static string ToContractEnumKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Trim().Replace("-", "_").Replace(" ", "_").ToUpperInvariant();
            var filtered = new string(normalized.Where(character =>
                char.IsLetterOrDigit(character) ||
                character == '_' ||
                character == ':' ||
                character == '-').ToArray());

            if (string.IsNullOrWhiteSpace(filtered) || !char.IsLetter(filtered[0]))
            {
                return string.Empty;
            }

            return filtered.Length > 64 ? filtered.Substring(0, 64) : filtered;
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
