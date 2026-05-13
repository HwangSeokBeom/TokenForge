using System;

namespace TokenForge.Client.Domain
{
    [Serializable]
    public sealed class AgentActionSummary
    {
        public int PromptCount { get; set; }
        public int ToolCallCount { get; set; }
        public int FileEditCount { get; set; }
        public int CommandRunCount { get; set; }
        public int TestRunCount { get; set; }
        public int BuildRunCount { get; set; }
        public int FailedCommandCount { get; set; }
        public DurationBucket DurationBucket { get; set; } = DurationBucket.Unknown;

        public static AgentActionSummary Empty()
        {
            return new AgentActionSummary();
        }
    }
}
