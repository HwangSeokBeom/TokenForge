using System;
using TokenForge.Client.Domain;

namespace TokenForge.Client.Agents
{
    public sealed class AgentProviderContext
    {
        public string ProjectRootPath { get; set; } = string.Empty;
        public string ProjectPathHash { get; set; } = string.Empty;
        public string LocalOnlyProjectId { get; set; } = string.Empty;
        public string ProjectAlias { get; set; } = string.Empty;
        public DateTimeOffset ScanStartedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset ScanEndedAt { get; set; } = DateTimeOffset.UtcNow;
        public ProviderConfidence MinimumAutoMergeConfidence { get; set; } = ProviderConfidence.High;
    }
}
