using System;
using System.Collections.Generic;

namespace TokenForge.Client.Domain
{
    [Serializable]
    public sealed class AgentWorkSession
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString("N");
        public AgentType AgentType { get; set; } = AgentType.Unknown;
        public WorkType WorkType { get; set; } = WorkType.Unknown;
        public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset EndedAt { get; set; } = DateTimeOffset.UtcNow;
        public TokenUsageBucket TokenUsageBucket { get; set; } = TokenUsageBucket.Unknown;
        public AgentActionSummary ActionSummary { get; set; } = AgentActionSummary.Empty();
        public GitChangeSummary GitChangeSummary { get; set; } = GitChangeSummary.Empty();
        public AgentActivitySummary AgentActivitySummary { get; set; } = AgentActivitySummary.Empty();
        public ResultStatus ResultStatus { get; set; } = ResultStatus.Unknown;
        public string SourceProvider { get; set; } = string.Empty;
        public List<string> SourceProviders { get; set; } = new List<string>();
        public string ParserVersion { get; set; } = string.Empty;
        public ProviderConfidence Confidence { get; set; } = ProviderConfidence.Unknown;
        public List<string> Warnings { get; set; } = new List<string>();
        public string DeduplicationKey { get; set; } = string.Empty;
        public List<string> SourceSessionIds { get; set; } = new List<string>();
        public bool UserReviewed { get; set; }

        public TimeSpan Duration => EndedAt >= StartedAt ? EndedAt - StartedAt : TimeSpan.Zero;
    }
}
