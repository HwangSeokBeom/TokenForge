using System;
using System.Collections.Generic;

namespace TokenForge.Client.Sync
{
    public enum SafeSyncRetryOperationType
    {
        Unknown,
        UPSERT_ACTIVITY_SESSIONS,
        DELETE_REMOTE_SESSION,
        FETCH_REMOTE_SESSIONS,
        TOMBSTONE_SYNC
    }

    public enum SafeSyncRetryQueueEntryStatus
    {
        Pending,
        InProgress,
        Succeeded,
        Failed,
        Paused,
        Cancelled,
        Conflict
    }

    [Serializable]
    public sealed class SafeSyncRetryQueueEntry
    {
        public string QueueEntryId { get; set; } = Guid.NewGuid().ToString("N");
        public SafeSyncRetryOperationType OperationType { get; set; } = SafeSyncRetryOperationType.Unknown;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;
        public int AttemptCount { get; set; }
        public int MaxAttempts { get; set; } = SafeSyncRetryPolicy.DefaultMaxAttempts;
        public string LastSafeErrorCode { get; set; } = string.Empty;
        public SafeSyncRetryQueueEntryStatus Status { get; set; } = SafeSyncRetryQueueEntryStatus.Pending;
        public List<string> ClientSessionIds { get; set; } = new List<string>();
        public string ServerSessionId { get; set; } = string.Empty;
        public int SchemaVersion { get; set; } = 1;

        public bool IsEligible(DateTimeOffset now)
        {
            return Status == SafeSyncRetryQueueEntryStatus.Pending &&
                   AttemptCount < MaxAttempts &&
                   NextAttemptAt <= now;
        }
    }

    [Serializable]
    public sealed class SafeSyncRetryQueueState
    {
        public int SchemaVersion { get; set; } = 1;
        public List<SafeSyncRetryQueueEntry> Entries { get; set; } = new List<SafeSyncRetryQueueEntry>();
    }

    [Serializable]
    public sealed class SafeSyncRetryQueueSummary
    {
        public int PendingCount { get; set; }
        public int InProgressCount { get; set; }
        public int SucceededCount { get; set; }
        public int FailedCount { get; set; }
        public int PausedCount { get; set; }
        public int CancelledCount { get; set; }
        public int ConflictCount { get; set; }
        public string NextQueueEntryId { get; set; } = string.Empty;
        public DateTimeOffset? NextAttemptAt { get; set; }
        public List<SafeSyncRetryQueueEntry> SafeEntries { get; set; } = new List<SafeSyncRetryQueueEntry>();
    }
}
