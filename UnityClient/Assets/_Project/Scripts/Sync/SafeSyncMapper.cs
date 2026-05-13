using System;
using System.Linq;
using TokenForge.Client.Domain;
using TokenForge.Client.Privacy;

namespace TokenForge.Client.Sync
{
    public sealed class SafeSyncMapper
    {
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
                SyncVersion = saveData.SyncState.SyncVersion,
                UpdatedAt = DateTimeOffset.UtcNow,
                CharacterSnapshot = ToCharacterSnapshot(saveData.CharacterProfile, saveData.SyncState.SyncVersion),
                SessionSummary = ToSessionSummary(saveData, saveData.SyncState.SyncVersion),
                Settings = ToSettings(saveData.PrivacyPreferences, saveData.SyncState.SyncVersion)
            };

            sanitizer.ThrowIfUnsafe(payload);
            return payload;
        }

        private static CharacterSnapshotSyncRequest ToCharacterSnapshot(CharacterProfile profile, int syncVersion)
        {
            return new CharacterSnapshotSyncRequest
            {
                SyncVersion = syncVersion,
                UpdatedAt = DateTimeOffset.UtcNow,
                CharacterId = profile.CharacterId,
                DisplayName = profile.DisplayName,
                Level = profile.Level,
                TotalExp = profile.TotalExp,
                EvolutionType = profile.CurrentEvolutionType,
                CharacterStats = ToStatsDto(profile.Stats),
                UnlockedItems = profile.UnlockedItems.ToList()
            };
        }

        private static SessionSummaryUploadRequest ToSessionSummary(SaveData saveData, int syncVersion)
        {
            var sessions = saveData.WorkSessionSummaries.Select(session => new SessionSummaryDto
            {
                SessionId = session.SessionId,
                AgentType = session.AgentType,
                WorkType = session.WorkType,
                StartedAt = session.StartedAt,
                EndedAt = session.EndedAt,
                TokenUsageBucket = session.TokenUsageBucket,
                ChangedFileCount = session.GitChangeSummary?.ChangedFileCount ?? 0,
                AddedLineBucket = session.GitChangeSummary?.AddedLineBucket ?? LineChangeBucket.Unknown,
                DeletedLineBucket = session.GitChangeSummary?.DeletedLineBucket ?? LineChangeBucket.Unknown,
                PromptCount = session.ActionSummary?.PromptCount ?? 0,
                ToolCallCount = session.ActionSummary?.ToolCallCount ?? 0,
                FileEditCount = session.ActionSummary?.FileEditCount ?? 0,
                CommandRunCount = session.ActionSummary?.CommandRunCount ?? 0,
                TestRunCount = session.ActionSummary?.TestRunCount ?? 0,
                BuildRunCount = session.ActionSummary?.BuildRunCount ?? 0,
                ResultStatus = session.ResultStatus,
                Confidence = session.Confidence,
                ProjectPathHash = session.GitChangeSummary?.ProjectPathHash ?? string.Empty,
                SourceProviders = session.SourceProviders.ToList()
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
            return new CharacterStatsDto
            {
                Logic = stats.Logic,
                Debug = stats.Debug,
                Architecture = stats.Architecture,
                Design = stats.Design,
                Stability = stats.Stability,
                Velocity = stats.Velocity,
                Creativity = stats.Creativity,
                Efficiency = stats.Efficiency,
                Stress = stats.Stress
            };
        }
    }
}
