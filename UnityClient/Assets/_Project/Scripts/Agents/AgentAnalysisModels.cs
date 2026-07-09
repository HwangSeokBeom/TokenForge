using System;
using System.Collections.Generic;
using TokenForge.Client.Domain;

namespace TokenForge.Client.Agents
{
    public sealed class AgentAnalysisInput
    {
        public const int DefaultAnalysisWindowDays = 7;
        public const int DefaultMaxFilesToScan = 20;
        public const int DefaultMaxLogEntriesToScan = 2000;
        public const int MaxAnalysisWindowDays = 90;
        public const int MaxFilesToScanLimit = 100;
        public const int MaxLogEntriesToScanLimit = 10000;

        // Memory-only. Never copy this value into summaries, save data, logs, or sync DTOs.
        public string SelectedLocationPath { get; set; } = string.Empty;
        public AgentProviderType ProviderHint { get; set; } = AgentProviderType.Unknown;
        public int AnalysisWindowDays { get; set; } = DefaultAnalysisWindowDays;
        public int MaxFilesToScan { get; set; } = DefaultMaxFilesToScan;
        public int MaxLogEntriesToScan { get; set; } = DefaultMaxLogEntriesToScan;
        public AgentSourceKind SourceKind { get; set; } = AgentSourceKind.Unknown;
        public string SafeSourceAlias { get; set; } = string.Empty;
        public bool LocalOnly { get; set; } = true;
        public List<string> IncludeRules { get; set; } = new List<string>();
        public List<string> ExcludeRules { get; set; } = new List<string>();
    }

    public sealed class AgentAnalysisReviewModel
    {
        public string SourceProvider { get; set; } = AgentLogActivityProvider.SourceProviderId;
        public AgentProviderType ProviderType { get; set; } = AgentProviderType.Unknown;
        public AgentSourceKind SourceKind { get; set; } = AgentSourceKind.Unknown;
        public string SafeSourceAlias { get; set; } = string.Empty;
        public string DayBucket { get; set; } = string.Empty;
        public CountBucket FileCountBucket { get; set; } = CountBucket.Unknown;
        public CountBucket SessionCountBucket { get; set; } = CountBucket.Unknown;
        public CountBucket InteractionCountBucket { get; set; } = CountBucket.Unknown;
        public CountBucket EstimatedCodingActivityBucket { get; set; } = CountBucket.Unknown;
        public List<AgentToolUsageCategoryBucket> ToolUsageCategoryBuckets { get; set; } = new List<AgentToolUsageCategoryBucket>();
        public List<AgentLanguageCategoryBucket> LanguageCategoryBuckets { get; set; } = new List<AgentLanguageCategoryBucket>();
        public ConfidenceLevel ConfidenceLevel { get; set; } = ConfidenceLevel.Unknown;
        public int WarningCount { get; set; }
        public List<string> WarningIds { get; set; } = new List<string>();
        public bool SaveEligible { get; set; }
        public CharacterStats DerivedStatDeltas { get; set; } = new CharacterStats();
        public int DerivedExpGained { get; set; }
        public string AnalyzerVersion { get; set; } = string.Empty;
        public int EstimatedSessionCount { get; set; }
        public int EstimatedInteractionCount { get; set; }
        public long EstimatedInputTokenCount { get; set; }
        public long EstimatedOutputTokenCount { get; set; }
        public long EstimatedTotalTokenCount { get; set; }
        public DateTimeOffset? LastActivityAtUtc { get; set; }

        public static AgentAnalysisReviewModel From(AgentWorkSession session, CharacterGrowthResult growthResult, bool saveEligible)
        {
            var summary = session?.AgentActivitySummary ?? AgentActivitySummary.Empty();
            return new AgentAnalysisReviewModel
            {
                SourceProvider = AgentLogActivityProvider.SourceProviderId,
                ProviderType = summary.ProviderType,
                SourceKind = summary.SourceKind,
                SafeSourceAlias = summary.SafeSourceAlias,
                DayBucket = summary.DayBucket,
                FileCountBucket = summary.FileCountBucket,
                SessionCountBucket = summary.SessionCountBucket,
                InteractionCountBucket = summary.InteractionCountBucket,
                EstimatedCodingActivityBucket = summary.EstimatedCodingActivityBucket,
                ToolUsageCategoryBuckets = summary.ToolUsageCategoryBuckets ?? new List<AgentToolUsageCategoryBucket>(),
                LanguageCategoryBuckets = summary.LanguageCategoryBuckets ?? new List<AgentLanguageCategoryBucket>(),
                ConfidenceLevel = summary.ConfidenceLevel,
                WarningCount = summary.WarningIds?.Count ?? 0,
                WarningIds = summary.WarningIds ?? new List<string>(),
                SaveEligible = saveEligible,
                DerivedStatDeltas = growthResult?.StatDeltas ?? new CharacterStats(),
                DerivedExpGained = growthResult?.ExpGained ?? 0,
                AnalyzerVersion = summary.AnalyzerVersion,
                EstimatedSessionCount = Math.Max(0, summary.EstimatedSessionCount),
                EstimatedInteractionCount = Math.Max(0, summary.EstimatedInteractionCount),
                EstimatedInputTokenCount = Math.Max(0, summary.EstimatedInputTokenCount),
                EstimatedOutputTokenCount = Math.Max(0, summary.EstimatedOutputTokenCount),
                EstimatedTotalTokenCount = Math.Max(0, summary.EstimatedTotalTokenCount),
                LastActivityAtUtc = summary.LastActivityAtUtc
            };
        }
    }

    public sealed class AgentLogEntry
    {
        public string Text { get; set; } = string.Empty;
        public DateTimeOffset? LastWriteTimeUtc { get; set; }
        // Memory-only opaque key used to keep cumulative token counters from
        // different log files separate. It never contains or persists a path.
        public string SourceKey { get; set; } = string.Empty;
    }

    public sealed class AgentLogReadResult
    {
        public bool IsSuccess { get; set; }
        public List<AgentLogEntry> Entries { get; set; } = new List<AgentLogEntry>();
        public List<string> WarningIds { get; set; } = new List<string>();
        public string ErrorCode { get; set; } = string.Empty;

        public static AgentLogReadResult Success(List<AgentLogEntry> entries, List<string> warningIds)
        {
            return new AgentLogReadResult
            {
                IsSuccess = true,
                Entries = entries ?? new List<AgentLogEntry>(),
                WarningIds = warningIds ?? new List<string>()
            };
        }

        public static AgentLogReadResult Failure(string errorCode)
        {
            return new AgentLogReadResult
            {
                IsSuccess = false,
                ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? "agent_read_failed" : errorCode
            };
        }
    }
}
