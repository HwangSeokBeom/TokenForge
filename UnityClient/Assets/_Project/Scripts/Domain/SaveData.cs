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
        public string LocalOnlyProjectId { get; set; } = Guid.NewGuid().ToString("N");

        // Local-only by default. Sync only when the user explicitly opts in because aliases may reveal project identity.
        public string ProjectAlias { get; set; } = string.Empty;

        public string ProjectPathHash { get; set; } = string.Empty;
        public bool IsGitRepository { get; set; }
        public DateTimeOffset? LastAnalyzedAt { get; set; }
        public bool AnalysisEnabled { get; set; } = true;
    }

    [Serializable]
    public sealed class ProviderSettings
    {
        public string ProviderId { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public DateTimeOffset? LastScanAt { get; set; }
        public string ParserVersion { get; set; } = string.Empty;
        public int PollingIntervalSeconds { get; set; } = 300;
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
    public sealed class SaveData
    {
        public const int CurrentSchemaVersion = 1;
        public const int CurrentSaveVersion = CurrentSchemaVersion;

        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public int SaveVersion { get; set; } = CurrentSaveVersion;
        public CharacterProfile CharacterProfile { get; set; } = new CharacterProfile();
        public List<AgentWorkSession> WorkSessionSummaries { get; set; } = new List<AgentWorkSession>();
        public List<CharacterGrowthResult> GrowthHistory { get; set; } = new List<CharacterGrowthResult>();
        public List<ConnectedProject> ConnectedProjects { get; set; } = new List<ConnectedProject>();
        public List<ProviderSettings> ProviderSettings { get; set; } = new List<ProviderSettings>();
        public SyncState SyncState { get; set; } = new SyncState();
        public PrivacyPreferences PrivacyPreferences { get; set; } = new PrivacyPreferences();
        public UserSettings UserSettings { get; set; } = new UserSettings();
        public DailyProgress DailyProgress { get; set; } = new DailyProgress();
        public List<MiniGameSession> MiniGameHistory { get; set; } = new List<MiniGameSession>();
        public List<AchievementProgress> Achievements { get; set; } = new List<AchievementProgress>();

        public static SaveData CreateDefault()
        {
            return new SaveData();
        }
    }
}
