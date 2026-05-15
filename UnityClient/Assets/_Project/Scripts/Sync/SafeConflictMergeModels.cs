using System;
using System.Collections.Generic;

namespace TokenForge.Client.Sync
{
    public enum SafeConflictMergePolicy
    {
        KeepLocal,
        KeepRemote,
        PreferHigherConfidence,
        PreferNewerSafeTimestamp,
        MergeNonConflictingAggregates,
        MarkResolvedOnly
    }

    [Serializable]
    public sealed class SafeConflictMergePreview
    {
        public string ConflictId { get; set; } = string.Empty;
        public SafeSyncSessionSafeSummary LocalSummary { get; set; } = new SafeSyncSessionSafeSummary();
        public SafeSyncSessionSafeSummary RemoteSummary { get; set; } = new SafeSyncSessionSafeSummary();
        public SafeSessionDiffSummary DiffSummary { get; set; } = new SafeSessionDiffSummary();
        public SafeConflictMergePolicy SelectedPolicy { get; set; } = SafeConflictMergePolicy.KeepLocal;
        public SafeConflictMergePolicy EffectivePolicy { get; set; } = SafeConflictMergePolicy.KeepLocal;
        public SafeSyncSessionSafeSummary ResultingSummary { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
        public bool CanApply { get; set; }
        public string BlockedReason { get; set; } = string.Empty;
    }

    [Serializable]
    public sealed class SafeConflictMergeResult
    {
        public string ConflictId { get; set; } = string.Empty;
        public SafeConflictMergePolicy Policy { get; set; } = SafeConflictMergePolicy.KeepLocal;
        public SafeConflictMergePolicy EffectivePolicy { get; set; } = SafeConflictMergePolicy.KeepLocal;
        public bool Applied { get; set; }
        public bool LocalChanged { get; set; }
        public bool QueuedRetry { get; set; }
        public string QueuedRetryEntryId { get; set; } = string.Empty;
        public string AuditEntryId { get; set; } = string.Empty;
        public string UserMessage { get; set; } = string.Empty;
        public string UserMessageCode { get; set; } = string.Empty;
        public SafeSyncResult SyncResult { get; set; } = SafeSyncResult.Failure(SafeSyncStatus.Failed, SafeSyncApiError.ConflictResolutionFailed, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.ConflictResolutionFailed));
    }

    [Serializable]
    public sealed class SafeConflictAuditEntry
    {
        public string AuditEntryId { get; set; } = Guid.NewGuid().ToString("N");
        public string ConflictId { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public string Action { get; set; } = string.Empty;
        public SafeConflictMergePolicy Policy { get; set; } = SafeConflictMergePolicy.KeepLocal;
        public string ResultStatus { get; set; } = string.Empty;
        public List<string> SafeDiffFieldNames { get; set; } = new List<string>();
        public SafeSyncSessionSafeSummary SafeLocalSummaryBefore { get; set; } = new SafeSyncSessionSafeSummary();
        public SafeSyncSessionSafeSummary SafeRemoteSummaryBefore { get; set; } = new SafeSyncSessionSafeSummary();
        public SafeSyncSessionSafeSummary SafeResultSummaryAfter { get; set; }
        public string QueuedRetryEntryId { get; set; } = string.Empty;
        public List<string> WarningIds { get; set; } = new List<string>();
        public string UserMessageCode { get; set; } = string.Empty;
        public int SchemaVersion { get; set; } = 1;
    }

    [Serializable]
    public sealed class SafeConflictAuditState
    {
        public int SchemaVersion { get; set; } = 1;
        public List<SafeConflictAuditEntry> Entries { get; set; } = new List<SafeConflictAuditEntry>();
    }

    [Serializable]
    public sealed class SafeConflictAuditSummary
    {
        public int TotalCount { get; set; }
        public int ResolvedCount { get; set; }
        public int FailedCount { get; set; }
        public List<SafeConflictAuditEntry> SafeEntries { get; set; } = new List<SafeConflictAuditEntry>();
    }
}
