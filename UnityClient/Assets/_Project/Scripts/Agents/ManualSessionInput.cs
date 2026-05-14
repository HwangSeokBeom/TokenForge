using System;
using System.Collections.Generic;
using TokenForge.Client.Domain;

namespace TokenForge.Client.Agents
{
    [Serializable]
    public sealed class ManualSessionInput
    {
        public AgentType AgentType { get; set; } = AgentType.ManualFallback;
        public WorkType WorkType { get; set; } = WorkType.Unknown;
        public TokenUsageBucket TokenUsageBucket { get; set; } = TokenUsageBucket.Unknown;
        public int ApproximateTokenCount { get; set; }
        public int ChangedFileCount { get; set; }
        public LineChangeBucket AddedLineBucket { get; set; } = LineChangeBucket.Unknown;
        public LineChangeBucket DeletedLineBucket { get; set; } = LineChangeBucket.Unknown;
        public int ApproximateAddedLineCount { get; set; }
        public int ApproximateDeletedLineCount { get; set; }
        public bool TestFileChanged { get; set; }
        public bool DocsFileChanged { get; set; }
        public bool UiFileChanged { get; set; }
        public bool ArchitectureFileChanged { get; set; }
        public bool TestRunDetected { get; set; }
        public bool BuildRunDetected { get; set; }
        public ResultStatus ResultStatus { get; set; } = ResultStatus.Unknown;
        public ProviderConfidence Confidence { get; set; } = ProviderConfidence.Low;
        public List<string> Warnings { get; set; } = new List<string>();
    }
}
