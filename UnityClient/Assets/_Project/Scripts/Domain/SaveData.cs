using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace TokenForge.Client.Domain
{
    [Serializable]
    public sealed class MiniGameSession
    {
        public string MiniGameSessionId { get; set; } = Guid.NewGuid().ToString("N");
        public int DurationSeconds { get; set; }
        public int Score { get; set; }
        public int TokensCollected { get; set; }
        public int BugsAvoided { get; set; }
        public int TestShieldsCollected { get; set; }
        public int BuildGaugeFilled { get; set; }
        public MiniGameResultGrade ResultGrade { get; set; } = MiniGameResultGrade.Unknown;
        public float GrowthBonus { get; set; }
    }

    [Serializable]
    public sealed class ConnectedProject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string DisplayName { get; set; } = string.Empty;
        public DateTimeOffset? ApprovedAt { get; set; }
        public string ConnectionSource { get; set; } = "userSelected";
        public string PathHash { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsArchived { get; set; }
        public string CompanionId { get; set; } = string.Empty;
        public string LocalOnlyProjectId { get; set; } = Guid.NewGuid().ToString("N");

        // Local-only by default. Sync only when the user explicitly opts in because aliases may reveal project identity.
        public string ProjectAlias { get; set; } = string.Empty;

        public string ProjectPathHash { get; set; } = string.Empty;
        public bool IsGitRepository { get; set; }
        public DateTimeOffset? LastAnalyzedAt { get; set; }
        public string LastAnalyzedCommit { get; set; } = string.Empty;
        public string FirstCommitAt { get; set; } = string.Empty;
        public int TotalCommitCount { get; set; }
        public string AnalyzedCommitRange { get; set; } = string.Empty;
        public string LastAnalysisMode { get; set; } = string.Empty;
        public string LastAnalysisScope { get; set; } = string.Empty;
        public bool AnalysisEnabled { get; set; } = true;
    }

    [Serializable]
    public sealed class ProviderSettings
    {
        public string ProviderId { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public bool Selected { get; set; }
        public bool Detected { get; set; }
        public bool ManualFolderApproved { get; set; }
        public string ConnectionState { get; set; } = string.Empty;
        public string Status { get; set; } = "notConfigured";
        public List<string> DetectedSources { get; set; } = new List<string>();
        public string ApprovedSource { get; set; } = string.Empty;
        public DateTimeOffset? LastAnalyzedAt { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
        public string Confidence { get; set; } = string.Empty;
        public string SafeLocationHash { get; set; } = string.Empty;
        public DateTimeOffset? LastScanAt { get; set; }
        public string ParserVersion { get; set; } = string.Empty;
        public int PollingIntervalSeconds { get; set; } = 300;
    }

    [Serializable]
    public sealed class ActivityReview
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string RepositoryId { get; set; } = string.Empty;
        public string SourceType { get; set; } = "repository";
        public string ProviderId { get; set; } = string.Empty;
        public string Status { get; set; } = "pending";
        public int XpDelta { get; set; }
        public CharacterStats CategoryBreakdown { get; set; } = CharacterStats.Zero();
        public string EvidenceSummary { get; set; } = string.Empty;
        public List<string> Warnings { get; set; } = new List<string>();
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? SavedAt { get; set; }
    }

    [Serializable]
    public sealed class CompanionProgress
    {
        public int Level { get; set; } = 1;
        public int CurrentXP { get; set; }
        public int XPRequiredForNextLevel { get; set; } = 250;
        public int TotalLifetimeXP { get; set; }
        public bool CanLevelUp { get; set; }
    }

    [Serializable]
    public sealed class SyncState
    {
        public bool IsLoggedIn { get; set; }
        public bool SyncEnabled { get; set; }
        public DateTimeOffset? LastSyncAt { get; set; }
        public int SyncVersion { get; set; } = 1;
        public int PendingQueueCount { get; set; }
        public string LastErrorCode { get; set; } = string.Empty;
    }

    [Serializable]
    public sealed class PrivacyPreferences
    {
        public bool AgentLogAnalysisEnabled { get; set; } = true;
        public bool GitAnalysisEnabled { get; set; } = true;
        public bool CloudSyncEnabled { get; set; }
        public bool TelemetryOptIn { get; set; }
        public bool MaskProjectAlias { get; set; } = true;
        public DateTimeOffset? DeleteDataRequestedAt { get; set; }
    }

    [Serializable]
    public sealed class UserSettings
    {
        public PrivacyPreferences PrivacyPreferences { get; set; } = new PrivacyPreferences();
        public bool AutoAnalyzeEnabled { get; set; } = true;
        public bool MiniGameBonusEnabled { get; set; } = true;
        public string PreferredLocale { get; set; } = string.Empty;
    }

    [Serializable]
    public sealed class DailyProgress
    {
        public string DayKey { get; set; } = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");
        public int ExpGainedToday { get; set; }
        public int SessionsConfirmedToday { get; set; }
    }

    [Serializable]
    public sealed class AchievementProgress
    {
        public string AchievementId { get; set; } = string.Empty;
        public DateTimeOffset UnlockedAt { get; set; } = DateTimeOffset.UtcNow;
        public int Progress { get; set; }
    }

    [Serializable]
    public sealed class PendingNativeActivityReview
    {
        public string ReviewId { get; set; } = string.Empty;
        public string SourceKind { get; set; } = string.Empty;
        public string RepositoryHash { get; set; } = string.Empty;
        public string SafeSummary { get; set; } = string.Empty;
        public string ActivityCategory { get; set; } = string.Empty;
        public string Confidence { get; set; } = string.Empty;
        public CountBucket CommitCountBucket { get; set; } = CountBucket.Unknown;
        public CountBucket ChangedFileCountBucket { get; set; } = CountBucket.Unknown;
        public int EstimatedXpDelta { get; set; }
        public CharacterStats StatDeltas { get; set; } = CharacterStats.Zero();
        public List<string> WarningIds { get; set; } = new List<string>();
        public AgentWorkSession SafeSession { get; set; }
        public CharacterGrowthResult GrowthResult { get; set; }
        public List<AgentWorkSession> SafeSessions { get; set; } = new List<AgentWorkSession>();
        public List<CharacterGrowthResult> GrowthResults { get; set; } = new List<CharacterGrowthResult>();
        public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    }

    [Serializable]
    public sealed class NativeAnalysisRunRecord
    {
        public string RunId { get; set; } = Guid.NewGuid().ToString("N");
        public string SourceKind { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
        public string SafeSummary { get; set; } = string.Empty;
        public string RepositoryId { get; set; } = string.Empty;
        public string RepositoryAlias { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public string CommitRange { get; set; } = string.Empty;
        public string AnalysisScope { get; set; } = string.Empty;
        public int XpDelta { get; set; }
        public CharacterStats StatDeltas { get; set; } = CharacterStats.Zero();
        public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    }

    [Serializable]
    public sealed class RepositoryTimelineEvent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
        public string RepositoryId { get; set; } = string.Empty;
        public string RepositoryAlias { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string TimelineSource { get; set; } = "local";
        public int DeltaXp { get; set; }
        public int DeltaCoins { get; set; }
        public int CodeDelta { get; set; }
        public int FocusDelta { get; set; }
        public int DebugDelta { get; set; }
        public int DesignDelta { get; set; }
        public int SyncDelta { get; set; }
        public string AiAgentId { get; set; } = string.Empty;
        public string ItemId { get; set; } = string.Empty;
        public string ZodiacId { get; set; } = string.Empty;
        public string Severity { get; set; } = "info";
        public string MetadataJson { get; set; } = string.Empty;
    }

    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentSchemaVersion = 1;
        public const int CurrentSaveVersion = CurrentSchemaVersion;

        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public int SaveVersion { get; set; } = CurrentSaveVersion;
        public CharacterProfile CharacterProfile { get; set; } = new CharacterProfile();
        public CompanionState CompanionState { get; set; } = CompanionState.CreateDefault();
        public List<RepositoryCompanionProfile> RepositoryCompanionProfiles { get; set; } = new List<RepositoryCompanionProfile>();
        public List<AiAgentShopState> AiAgentShopStates { get; set; } = new List<AiAgentShopState>();
        public string SelectedRepositoryHash { get; set; } = string.Empty;
        public DesktopCompanionSettings DesktopCompanionSettings { get; set; } = DesktopCompanionSettings.CreateDefault();
        public List<AgentWorkSession> WorkSessionSummaries { get; set; } = new List<AgentWorkSession>();
        public List<CharacterGrowthResult> GrowthHistory { get; set; } = new List<CharacterGrowthResult>();
        public List<string> AppliedNativeReviewIds { get; set; } = new List<string>();
        public PendingNativeActivityReview PendingNativeActivityReview { get; set; }
        public List<ActivityReview> ActivityReviews { get; set; } = new List<ActivityReview>();
        public List<NativeAnalysisRunRecord> RecentNativeAnalysisRuns { get; set; } = new List<NativeAnalysisRunRecord>();
        public List<RepositoryTimelineEvent> RepositoryTimelineEvents { get; set; } = new List<RepositoryTimelineEvent>();
        public List<ConnectedProject> ConnectedProjects { get; set; } = new List<ConnectedProject>();
        public List<ProviderSettings> ProviderSettings { get; set; } = new List<ProviderSettings>();
        public SyncState SyncState { get; set; } = new SyncState();
        public PrivacyPreferences PrivacyPreferences { get; set; } = new PrivacyPreferences();
        public UserSettings UserSettings { get; set; } = new UserSettings();
        public OnboardingPreferences OnboardingPreferences { get; set; } = new OnboardingPreferences();
        public DailyProgress DailyProgress { get; set; } = new DailyProgress();
        public List<MiniGameSession> MiniGameHistory { get; set; } = new List<MiniGameSession>();
        public List<AchievementProgress> Achievements { get; set; } = new List<AchievementProgress>();

        public static SaveData CreateDefault()
        {
            return new SaveData();
        }
    }
}
