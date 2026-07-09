using System;
using System.Collections.Generic;

namespace TokenForge.Client.Domain
{
    [Serializable]
    public sealed class AgentToolUsageCategoryBucket
    {
        public AgentToolUsageCategory Category { get; set; } = AgentToolUsageCategory.Unknown;
        public CountBucket CountBucket { get; set; } = CountBucket.Unknown;
    }

    [Serializable]
    public sealed class AgentLanguageCategoryBucket
    {
        public AgentLanguageCategory Category { get; set; } = AgentLanguageCategory.Unknown;
        public CountBucket CountBucket { get; set; } = CountBucket.Unknown;
    }

    [Serializable]
    public sealed class AgentActivitySummary
    {
        public AgentProviderType ProviderType { get; set; } = AgentProviderType.Unknown;
        public AgentSourceKind SourceKind { get; set; } = AgentSourceKind.Unknown;
        public string SafeSourceAlias { get; set; } = string.Empty;
        public string SourceIdentifierHash { get; set; } = string.Empty;
        public string DayBucket { get; set; } = string.Empty;
        public CountBucket FileCountBucket { get; set; } = CountBucket.Unknown;
        public CountBucket SessionCountBucket { get; set; } = CountBucket.Unknown;
        public CountBucket InteractionCountBucket { get; set; } = CountBucket.Unknown;
        public CountBucket EstimatedCodingActivityBucket { get; set; } = CountBucket.Unknown;
        // Privacy-safe aggregates. Raw prompts, responses, code, and transcripts are never
        // copied into this model or persisted by the analyzer.
        public int EstimatedSessionCount { get; set; }
        public int EstimatedInteractionCount { get; set; }
        public long EstimatedInputTokenCount { get; set; }
        public long EstimatedOutputTokenCount { get; set; }
        public long EstimatedTotalTokenCount { get; set; }
        public DateTimeOffset? LastActivityAtUtc { get; set; }
        public List<AgentToolUsageCategoryBucket> ToolUsageCategoryBuckets { get; set; } = new List<AgentToolUsageCategoryBucket>();
        public List<AgentLanguageCategoryBucket> LanguageCategoryBuckets { get; set; } = new List<AgentLanguageCategoryBucket>();
        public ConfidenceLevel ConfidenceLevel { get; set; } = ConfidenceLevel.Unknown;
        public List<string> WarningIds { get; set; } = new List<string>();
        public string AnalyzerVersion { get; set; } = string.Empty;

        public static AgentActivitySummary Empty()
        {
            return new AgentActivitySummary();
        }
    }
}
