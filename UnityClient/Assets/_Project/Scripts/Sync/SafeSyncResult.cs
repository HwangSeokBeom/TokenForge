using System;
using System.Collections.Generic;

namespace TokenForge.Client.Sync
{
    [Serializable]
    public sealed class RemoteSafeSessionSummary
    {
        public string ServerSessionId { get; set; } = string.Empty;
        public string ClientSessionId { get; set; } = string.Empty;
        public string SourceProvider { get; set; } = "UNKNOWN_AGENT";
        public string DayBucket { get; set; } = string.Empty;
        public string TimeBucket { get; set; } = string.Empty;
        public string Confidence { get; set; } = "LOW";
        public string ActivityCategory { get; set; } = string.Empty;
        public string ChangeCountBucket { get; set; } = string.Empty;
        public string LineCountBucket { get; set; } = string.Empty;
        public string SessionCountBucket { get; set; } = string.Empty;
        public string InteractionCountBucket { get; set; } = string.Empty;
        public int WarningCount { get; set; }
        public string CategoryBucketSummary { get; set; } = string.Empty;
        public string ToolBucketSummary { get; set; } = string.Empty;
        public string LanguageBucketSummary { get; set; } = string.Empty;
        public string AnalyzerVersion { get; set; } = string.Empty;
        public string ParserVersion { get; set; } = string.Empty;
        public int SchemaVersion { get; set; } = 1;
    }

    [Serializable]
    public sealed class SafeSyncResult
    {
        public bool IsSuccess { get; set; }
        public SafeSyncStatus Status { get; set; } = SafeSyncStatus.Idle;
        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public int AcceptedCount { get; set; }
        public int RejectedCount { get; set; }
        public int AttemptedCount { get; set; }
        public int SucceededCount { get; set; }
        public int FailedCount { get; set; }
        public int PausedCount { get; set; }
        public int SkippedNotReadyCount { get; set; }
        public int ConflictDetectedCount { get; set; }
        public int UnsafeRejectedCount { get; set; }
        public int UnchangedCount { get; set; }
        public int SchemaVersion { get; set; } = 1;
        public List<RemoteSafeSessionSummary> RemoteSessions { get; set; } = new List<RemoteSafeSessionSummary>();
        public SafeSyncRetryQueueSummary RetryQueueSummary { get; set; } = new SafeSyncRetryQueueSummary();
        public SafeSyncConflictSummary ConflictSummary { get; set; } = new SafeSyncConflictSummary();
        public SafeSyncTombstoneSummary TombstoneSummary { get; set; } = new SafeSyncTombstoneSummary();

        public static SafeSyncResult Success(SafeSyncStatus status)
        {
            return new SafeSyncResult
            {
                IsSuccess = true,
                Status = status
            };
        }

        public static SafeSyncResult Failure(SafeSyncStatus status, string errorCode, string errorMessage)
        {
            return new SafeSyncResult
            {
                IsSuccess = false,
                Status = status,
                ErrorCode = errorCode ?? string.Empty,
                ErrorMessage = errorMessage ?? string.Empty
            };
        }
    }
}
