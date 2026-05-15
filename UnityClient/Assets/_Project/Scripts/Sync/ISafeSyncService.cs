using System.Threading;
using System.Threading.Tasks;

namespace TokenForge.Client.Sync
{
    public interface ISafeSyncService
    {
        SafeSyncStatus Status { get; }
        string BaseUrl { get; }
        void SetBaseUrl(string baseUrl);
        Task<SafeSyncResult> CheckHealthAsync(CancellationToken cancellationToken = default);
        Task<SafeSyncResult> SyncNowAsync(CancellationToken cancellationToken = default);
        Task<SafeSyncResult> EnqueueSyncSafeSessionsAsync(CancellationToken cancellationToken = default);
        Task<SafeSyncResult> ProcessRetryQueueOnceAsync(CancellationToken cancellationToken = default);
        Task<SafeSyncResult> ProcessAllEligibleRetryEntriesOnceAsync(CancellationToken cancellationToken = default);
        Task<SafeSyncResult> ForceRetryEntryAsync(string queueEntryId, bool explicitConfirmation, CancellationToken cancellationToken = default);
        Task<SafeSyncResult> CancelRetryEntryAsync(string queueEntryId, CancellationToken cancellationToken = default);
        Task<SafeSyncResult> CancelAllFailedRetryEntriesAsync(CancellationToken cancellationToken = default);
        Task<SafeSyncResult> ClearSucceededRetryEntriesAsync(CancellationToken cancellationToken = default);
        Task<SafeSyncResult> PauseAllPendingRetryEntriesAsync(CancellationToken cancellationToken = default);
        Task<SafeSyncResult> ResumeAllPausedRetryEntriesAsync(CancellationToken cancellationToken = default);
        Task<SafeSyncRetryQueueSummary> GetRetryQueueSummaryAsync(CancellationToken cancellationToken = default);
        Task<SafeSyncConflictSummary> GetConflictSummaryAsync(CancellationToken cancellationToken = default);
        Task<SafeSyncTombstoneSummary> GetTombstoneSummaryAsync(CancellationToken cancellationToken = default);
        Task<SafeSyncResult> DeleteLocalSavedSessionAsync(string clientSessionId, CancellationToken cancellationToken = default);
        Task<SafeSyncResult> EnqueuePendingTombstoneDeletesAsync(CancellationToken cancellationToken = default);
        Task<SafeSyncResult> ProcessPendingTombstoneDeletesOnceAsync(CancellationToken cancellationToken = default);
        Task<SafeSyncResult> CancelTombstoneAsync(string tombstoneId, CancellationToken cancellationToken = default);
        Task<SafeSyncResult> CancelAllFailedTombstonesAsync(CancellationToken cancellationToken = default);
        Task<SafeSyncResult> MarkTombstoneResolvedAsync(string tombstoneId, CancellationToken cancellationToken = default);
        Task<SafeSyncResult> ClearResolvedTombstonesAsync(CancellationToken cancellationToken = default);
        Task<SafeSyncResult> KeepLocalConflictAsync(string conflictId, CancellationToken cancellationToken = default);
        Task<SafeSyncResult> KeepRemoteConflictAsync(string conflictId, CancellationToken cancellationToken = default);
        Task<SafeSyncResult> MarkConflictResolvedAsync(string conflictId, CancellationToken cancellationToken = default);
        Task<SafeSyncResult> CancelConflictResolutionAsync(string conflictId, CancellationToken cancellationToken = default);
        Task<SafeConflictMergePreview> GetConflictMergePreviewAsync(string conflictId, SafeConflictMergePolicy policy, CancellationToken cancellationToken = default);
        Task<SafeConflictMergeResult> ApplyConflictMergePolicyAsync(string conflictId, SafeConflictMergePolicy policy, bool explicitConfirmation, CancellationToken cancellationToken = default);
        Task<SafeConflictAuditSummary> GetConflictAuditHistoryAsync(CancellationToken cancellationToken = default);
        Task<SafeSyncResult> ClearResolvedConflictAuditHistoryAsync(bool explicitConfirmation, CancellationToken cancellationToken = default);
        Task<SafeSyncResult> FetchRemoteSessionsAsync(CancellationToken cancellationToken = default);
        Task<SafeSyncResult> DeleteRemoteSessionAsync(string serverSessionId, CancellationToken cancellationToken = default);
    }
}
