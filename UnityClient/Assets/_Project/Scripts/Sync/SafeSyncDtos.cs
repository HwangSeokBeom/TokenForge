using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using TokenForge.Client.Domain;

namespace TokenForge.Client.Sync
{
    [Serializable]
    public sealed class CharacterStatsDto
    {
        public int Logic { get; set; }
        public int Debug { get; set; }
        public int Architecture { get; set; }
        public int Design { get; set; }
        public int Stability { get; set; }
        public int Velocity { get; set; }
        public int Creativity { get; set; }
        public int Efficiency { get; set; }
        public int Stress { get; set; }
    }

    [Serializable]
    public sealed class SessionSummaryDto
    {
        public string SessionId { get; set; } = string.Empty;
        public AgentType AgentType { get; set; } = AgentType.Unknown;
        public WorkType WorkType { get; set; } = WorkType.Unknown;
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset EndedAt { get; set; }
        public TokenUsageBucket TokenUsageBucket { get; set; } = TokenUsageBucket.Unknown;
        public int ChangedFileCount { get; set; }
        public LineChangeBucket AddedLineBucket { get; set; } = LineChangeBucket.Unknown;
        public LineChangeBucket DeletedLineBucket { get; set; } = LineChangeBucket.Unknown;
        public int PromptCount { get; set; }
        public int ToolCallCount { get; set; }
        public int FileEditCount { get; set; }
        public int CommandRunCount { get; set; }
        public int TestRunCount { get; set; }
        public int BuildRunCount { get; set; }
        public ResultStatus ResultStatus { get; set; } = ResultStatus.Unknown;
        public ProviderConfidence Confidence { get; set; } = ProviderConfidence.Unknown;
        public string ProjectPathHash { get; set; } = string.Empty;
        public AgentProviderType AgentProviderType { get; set; } = AgentProviderType.Unknown;
        public string AgentSourceIdentifierHash { get; set; } = string.Empty;
        public string AgentActivityDayBucket { get; set; } = string.Empty;
        public CountBucket AgentSessionCountBucket { get; set; } = CountBucket.Unknown;
        public CountBucket AgentInteractionCountBucket { get; set; } = CountBucket.Unknown;
        public CountBucket AgentCodingActivityBucket { get; set; } = CountBucket.Unknown;
        public List<AgentToolUsageCategoryBucket> AgentToolUsageCategoryBuckets { get; set; } = new List<AgentToolUsageCategoryBucket>();
        public List<AgentLanguageCategoryBucket> AgentLanguageCategoryBuckets { get; set; } = new List<AgentLanguageCategoryBucket>();
        public string SourceProvider { get; set; } = "UNKNOWN";
        [JsonIgnore]
        public List<string> SourceProviders { get; set; } = new List<string>();
    }

    [Serializable]
    public sealed class WorkTypeDistributionDto
    {
        public WorkType WorkType { get; set; } = WorkType.Unknown;
        public int Count { get; set; }
    }

    [Serializable]
    public sealed class SessionSummaryUploadRequest
    {
        public int SyncVersion { get; set; } = 1;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        public List<SessionSummaryDto> Sessions { get; set; } = new List<SessionSummaryDto>();
        public List<WorkTypeDistributionDto> WorkTypeDistribution { get; set; } = new List<WorkTypeDistributionDto>();
    }

    [Serializable]
    public sealed class CharacterSnapshotSyncRequest
    {
        public int SyncVersion { get; set; } = 1;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        public string CharacterId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int Level { get; set; }
        public int TotalExp { get; set; }
        public EvolutionType EvolutionType { get; set; } = EvolutionType.Unknown;
        public CharacterStatsDto CharacterStats { get; set; } = new CharacterStatsDto();
        public List<string> UnlockedItems { get; set; } = new List<string>();
    }

    [Serializable]
    public sealed class SettingsSyncRequest
    {
        public int SyncVersion { get; set; } = 1;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        public bool CloudSyncEnabled { get; set; }
        public bool TelemetryOptIn { get; set; }
        public bool MaskProjectAlias { get; set; } = true;
    }

    [Serializable]
    public sealed class SafeSyncPayload
    {
        public int SyncVersion { get; set; } = 1;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        public CharacterSnapshotSyncRequest CharacterSnapshot { get; set; } = new CharacterSnapshotSyncRequest();
        public SessionSummaryUploadRequest SessionSummary { get; set; } = new SessionSummaryUploadRequest();
        public SettingsSyncRequest Settings { get; set; } = new SettingsSyncRequest();
    }

    [Serializable]
    public sealed class SafeSyncPullRequest
    {
        public int SyncVersion { get; set; } = 1;
        public DateTimeOffset? LastSyncAt { get; set; }
    }

    [Serializable]
    public sealed class SafeAchievementDto
    {
        public string AchievementId { get; set; } = string.Empty;
        public DateTimeOffset UnlockedAt { get; set; } = DateTimeOffset.UtcNow;
        public int Progress { get; set; }
    }

    [Serializable]
    public sealed class SafeSyncPullResponse
    {
        public int SyncVersion { get; set; } = 1;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        public CharacterSnapshotSyncRequest CharacterSnapshot { get; set; }
        public List<SessionSummaryDto> Sessions { get; set; } = new List<SessionSummaryDto>();
        public List<SafeAchievementDto> Achievements { get; set; } = new List<SafeAchievementDto>();
    }

    [Serializable]
    public sealed class SafeSyncPushResponse
    {
        public int SyncVersion { get; set; } = 1;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        public int AcceptedSessionCount { get; set; }
        public int AcceptedAchievementCount { get; set; }
    }
}
