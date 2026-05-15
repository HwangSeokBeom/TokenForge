using System;
using System.Collections.Generic;

namespace TokenForge.Client.Sync
{
    public enum SafeSyncConflictType
    {
        RemoteDifferent,
        RemoteMissing,
        LocalMissing,
        DeleteAlreadyApplied,
        ValidationConflict,
        Unknown
    }

    public enum SafeSyncConflictResolutionStatus
    {
        Unresolved,
        KeepLocal,
        KeepRemote,
        MarkResolved,
        Merged,
        Cancelled
    }

    [Serializable]
    public sealed class SafeSyncSessionSafeSummary
    {
        public string ClientSessionId { get; set; } = string.Empty;
        public string ServerSessionId { get; set; } = string.Empty;
        public string SourceProvider { get; set; } = "UNKNOWN_AGENT";
        public string DayBucket { get; set; } = string.Empty;
        public string TimeBucket { get; set; } = string.Empty;
        public string ActivityCategory { get; set; } = string.Empty;
        public string ChangeCountBucket { get; set; } = string.Empty;
        public string LineCountBucket { get; set; } = string.Empty;
        public string SessionCountBucket { get; set; } = string.Empty;
        public string InteractionCountBucket { get; set; } = string.Empty;
        public string Confidence { get; set; } = "LOW";
        public int WarningCount { get; set; }
        public string CategoryBucketSummary { get; set; } = string.Empty;
        public string ToolBucketSummary { get; set; } = string.Empty;
        public string LanguageBucketSummary { get; set; } = string.Empty;
        public string AnalyzerVersion { get; set; } = string.Empty;
        public string ParserVersion { get; set; } = string.Empty;
        public int SchemaVersion { get; set; } = 1;
    }

    [Serializable]
    public sealed class SafeSyncConflict
    {
        public string ConflictId { get; set; } = Guid.NewGuid().ToString("N");
        public string ClientSessionId { get; set; } = string.Empty;
        public string ServerSessionId { get; set; } = string.Empty;
        public SafeSyncConflictType ConflictType { get; set; } = SafeSyncConflictType.Unknown;
        public DateTimeOffset DetectedAt { get; set; } = DateTimeOffset.UtcNow;
        public SafeSyncSessionSafeSummary SafeLocalSummary { get; set; } = new SafeSyncSessionSafeSummary();
        public SafeSyncSessionSafeSummary SafeRemoteSummary { get; set; } = new SafeSyncSessionSafeSummary();
        public RemoteSafeActivitySessionDto SafeRemoteSession { get; set; }
        public SafeSessionDiffSummary SafeDiffSummary { get; set; } = new SafeSessionDiffSummary();
        public string SafeErrorCode { get; set; } = string.Empty;
        public SafeSyncConflictResolutionStatus ResolutionStatus { get; set; } = SafeSyncConflictResolutionStatus.Unresolved;
        public int SchemaVersion { get; set; } = 1;
    }

    [Serializable]
    public sealed class SafeSyncConflictState
    {
        public int SchemaVersion { get; set; } = 1;
        public List<SafeSyncConflict> Conflicts { get; set; } = new List<SafeSyncConflict>();
    }

    [Serializable]
    public sealed class SafeSyncConflictSummary
    {
        public int UnresolvedCount { get; set; }
        public List<SafeSyncConflict> SafeConflicts { get; set; } = new List<SafeSyncConflict>();
    }
}
