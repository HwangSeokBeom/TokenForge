using System;
using TokenForge.Client.Domain;

namespace TokenForge.Client.Agents
{
    [Serializable]
    public sealed class ManualFallbackInput
    {
        public WorkType WorkType { get; set; } = WorkType.Unknown;
        public TokenUsageBucket TokenUsageBucket { get; set; } = TokenUsageBucket.Unknown;
        public int ChangedFileCount { get; set; }
        public ResultStatus ResultStatus { get; set; } = ResultStatus.Unknown;
        public bool TestRunDetected { get; set; }
        public bool BuildRunDetected { get; set; }
    }
}
