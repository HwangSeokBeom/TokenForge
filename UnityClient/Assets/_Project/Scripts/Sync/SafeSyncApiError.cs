namespace TokenForge.Client.Sync
{
    public static class SafeSyncApiError
    {
        public const string None = "";
        public const string AuthRequired = "AUTH_REQUIRED";
        public const string ServerUnavailable = "SERVER_UNAVAILABLE";
        public const string NetworkError = "NETWORK_ERROR";
        public const string Timeout = "REQUEST_TIMEOUT";
        public const string NetworkTimeout = "NETWORK_TIMEOUT";
        public const string InvalidJson = "INVALID_JSON";
        public const string InvalidBaseUrl = "INVALID_BASE_URL";
        public const string PrivacyGuardBlockedPayload = "CLIENT_PRIVACY_GUARD_BLOCKED_PAYLOAD";
        public const string EmptyResponse = "EMPTY_RESPONSE";
        public const string AlreadyInProgress = "ALREADY_IN_PROGRESS";
        public const string TemporaryServerError = "TEMPORARY_SERVER_ERROR";
        public const string RateLimited = "RATE_LIMITED";
        public const string ValidationFailed = "VALIDATION_FAILED";
        public const string UnsafePayload = "UNSAFE_PAYLOAD";
        public const string UnknownSchemaVersion = "UNKNOWN_SCHEMA_VERSION";
        public const string Forbidden = "FORBIDDEN";
        public const string NotFound = "NOT_FOUND";
        public const string RetryQueued = "RETRY_QUEUED";
        public const string RetryInProgress = "RETRY_IN_PROGRESS";
        public const string RetryWaiting = "RETRY_WAITING";
        public const string RetryExhausted = "RETRY_EXHAUSTED";
        public const string ConflictDetected = "CONFLICT_DETECTED";
        public const string ValidationConflict = "VALIDATION_CONFLICT";
        public const string DeleteAlreadyApplied = "DELETE_ALREADY_APPLIED";
        public const string TombstonePending = "TOMBSTONE_PENDING";
        public const string LocalSessionDeleted = "LOCAL_SESSION_DELETED";
        public const string LocalSessionDeleteFailed = "LOCAL_SESSION_DELETE_FAILED";
        public const string TombstoneCreated = "TOMBSTONE_CREATED";
        public const string TombstoneDeleteSynced = "TOMBSTONE_DELETE_SYNCED";
        public const string TombstoneDeletePending = "TOMBSTONE_DELETE_PENDING";
        public const string TombstoneDeleteFailed = "TOMBSTONE_DELETE_FAILED";
        public const string ConflictKeepLocalQueued = "CONFLICT_KEEP_LOCAL_QUEUED";
        public const string ConflictKeepRemoteMarked = "CONFLICT_KEEP_REMOTE_MARKED";
        public const string ConflictMarkedResolved = "CONFLICT_MARKED_RESOLVED";
        public const string LocalSessionMissing = "LOCAL_SESSION_MISSING";
        public const string RemoteSessionMissing = "REMOTE_SESSION_MISSING";
        public const string ConflictResolutionFailed = "CONFLICT_RESOLUTION_FAILED";
        public const string UpsertRetryUpdatedAfterLocalDelete = "UPSERT_RETRY_UPDATED_AFTER_LOCAL_DELETE";
        public const string KeepRemoteApplied = "KEEP_REMOTE_APPLIED";
        public const string KeepRemoteMarkerOnly = "KEEP_REMOTE_MARKER_ONLY";
        public const string KeepRemoteUnsafeRejected = "KEEP_REMOTE_UNSAFE_REJECTED";
        public const string RemoteSessionValidationFailed = "REMOTE_SESSION_VALIDATION_FAILED";
        public const string RemoteSessionAppliedLocally = "REMOTE_SESSION_APPLIED_LOCALLY";
        public const string RemoteSessionApplyFailed = "REMOTE_SESSION_APPLY_FAILED";
        public const string ConflictDiffDetected = "CONFLICT_DIFF_DETECTED";
        public const string ConflictBatchDetected = "CONFLICT_BATCH_DETECTED";
        public const string BatchTombstoneProcessComplete = "BATCH_TOMBSTONE_PROCESS_COMPLETE";
        public const string BatchRetryProcessComplete = "BATCH_RETRY_PROCESS_COMPLETE";
        public const string RetrySkippedNotReady = "RETRY_SKIPPED_NOT_READY";
        public const string RetryForceNotSupported = "RETRY_FORCE_NOT_SUPPORTED";
        public const string ForcedRetryStarted = "FORCED_RETRY_STARTED";
        public const string ForcedRetrySkippedAuthPaused = "FORCED_RETRY_SKIPPED_AUTH_PAUSED";
        public const string ForcedRetryBlockedUnsafe = "FORCED_RETRY_BLOCKED_UNSAFE";
        public const string ForcedRetryBlockedPermanentFailure = "FORCED_RETRY_BLOCKED_PERMANENT_FAILURE";
        public const string ForcedRetrySucceeded = "FORCED_RETRY_SUCCEEDED";
        public const string ForcedRetryFailedRetryable = "FORCED_RETRY_FAILED_RETRYABLE";
        public const string ForcedRetryPartiallySucceeded = "FORCED_RETRY_PARTIALLY_SUCCEEDED";
        public const string TombstoneBatchPartialFailure = "TOMBSTONE_BATCH_PARTIAL_FAILURE";
        public const string ConflictDuplicateIgnored = "CONFLICT_DUPLICATE_IGNORED";
        public const string InvalidBucketValue = "INVALID_BUCKET_VALUE";
        public const string MergePolicyManualChoiceRequired = "MERGE_POLICY_MANUAL_CHOICE_REQUIRED";
        public const string MergePolicyAmbiguousAggregate = "MERGE_POLICY_AMBIGUOUS_AGGREGATE";
        public const string MergePolicyConfirmationRequired = "MERGE_POLICY_CONFIRMATION_REQUIRED";
        public const string MergePolicyPreviewBlocked = "MERGE_POLICY_PREVIEW_BLOCKED";
        public const string MergePolicyApplied = "MERGE_POLICY_APPLIED";
        public const string ConflictAuditCleared = "CONFLICT_AUDIT_CLEARED";

        public static string ToSafeMessage(string code)
        {
            switch (code)
            {
                case AuthRequired:
                    return "Log in to use server sync.";
                case ServerUnavailable:
                case NetworkError:
                    return "The server is unavailable. Your local data is safe.";
                case Timeout:
                case NetworkTimeout:
                    return "The request timed out. Try again.";
                case InvalidBaseUrl:
                    return "Enter a valid HTTP or HTTPS server URL.";
                case PrivacyGuardBlockedPayload:
                    return "Sync was blocked because the outgoing payload failed the privacy check.";
                case AlreadyInProgress:
                    return "That Safe Sync request is already in progress.";
                case InvalidJson:
                    return "The server response could not be read safely.";
                case RetryQueued:
                    return "Sync failed temporarily. It was added to the retry queue.";
                case RetryInProgress:
                    return "Retrying pending sync.";
                case RetryWaiting:
                    return "Retry is waiting for the next allowed attempt.";
                case RetryExhausted:
                    return "Retry limit reached. Your local data is still safe.";
                case ConflictDetected:
                case ValidationConflict:
                    return "A safe sync conflict was detected.";
                case DeleteAlreadyApplied:
                case NotFound:
                    return "The remote session was already deleted.";
                case TombstonePending:
                case TombstoneCreated:
                case TombstoneDeletePending:
                    return "Delete will be synced when you retry.";
                case LocalSessionDeleted:
                    return "Local saved session deleted.";
                case LocalSessionDeleteFailed:
                    return "Local saved session could not be deleted.";
                case TombstoneDeleteSynced:
                    return "Remote delete is marked synced.";
                case TombstoneDeleteFailed:
                    return "Remote delete could not be synced. Your local data is safe.";
                case ConflictKeepLocalQueued:
                    return "Keep Local was queued for explicit Safe Sync retry.";
                case ConflictKeepRemoteMarked:
                    return "Keep Remote was marked as resolved without changing local data.";
                case ConflictMarkedResolved:
                    return "Conflict was marked resolved.";
                case KeepRemoteApplied:
                case RemoteSessionAppliedLocally:
                    return "Keep Remote applied the safe remote aggregate locally.";
                case KeepRemoteMarkerOnly:
                    return "Keep Remote was marked resolved; the remote aggregate did not include enough safe data to update local saved sessions.";
                case KeepRemoteUnsafeRejected:
                    return "Keep Remote was blocked because the remote aggregate failed the privacy check.";
                case RemoteSessionValidationFailed:
                case RemoteSessionApplyFailed:
                    return "The remote aggregate could not be applied safely. Your local data is safe.";
                case ConflictDiffDetected:
                    return "A safe difference was detected between local and remote summaries.";
                case ConflictBatchDetected:
                    return "Safe conflicts were detected during explicit fetch.";
                case BatchTombstoneProcessComplete:
                    return "Pending remote deletes were processed once.";
                case BatchRetryProcessComplete:
                    return "Eligible retry entries were processed once.";
                case RetrySkippedNotReady:
                    return "Some retry entries are waiting for their next allowed attempt.";
                case RetryForceNotSupported:
                    return "Force retry is not supported for this action.";
                case ForcedRetryStarted:
                    return "Force retry started for the selected safe retry entry.";
                case ForcedRetrySkippedAuthPaused:
                    return "Force retry was skipped because authentication is paused. Resume the session first.";
                case ForcedRetryBlockedUnsafe:
                    return "Force retry was blocked because the safe payload failed validation.";
                case ForcedRetryBlockedPermanentFailure:
                    return "Force retry was blocked because the entry has a permanent failure.";
                case ForcedRetrySucceeded:
                    return "Force retry completed for the selected entry.";
                case ForcedRetryFailedRetryable:
                    return "Force retry failed temporarily. The entry remains safe for explicit retry.";
                case ForcedRetryPartiallySucceeded:
                    return "Force retry partially completed. Pending items remain safe for explicit retry.";
                case TombstoneBatchPartialFailure:
                    return "Some remote deletes could not be completed. Pending items remain safe for explicit retry.";
                case ConflictDuplicateIgnored:
                    return "An unresolved duplicate conflict was ignored.";
                case MergePolicyManualChoiceRequired:
                    return "This merge policy needs an explicit Keep Local or Keep Remote choice.";
                case MergePolicyAmbiguousAggregate:
                    return "Merge was blocked because the aggregate fields are ambiguous.";
                case MergePolicyConfirmationRequired:
                    return "Confirm the conflict merge policy before applying it.";
                case MergePolicyPreviewBlocked:
                    return "The merge preview cannot be applied safely.";
                case MergePolicyApplied:
                    return "Conflict merge policy was applied.";
                case ConflictAuditCleared:
                    return "Resolved conflict history was cleared.";
                case LocalSessionMissing:
                    return "The local saved session is no longer available.";
                case RemoteSessionMissing:
                    return "The remote session is no longer available.";
                case ConflictResolutionFailed:
                    return "Conflict resolution could not be completed safely.";
                case UpsertRetryUpdatedAfterLocalDelete:
                    return "Pending upload retry was updated after local delete.";
                case ValidationFailed:
                case UnsafePayload:
                case UnknownSchemaVersion:
                case InvalidBucketValue:
                    return "Safe Sync validation failed. Your local data is safe.";
                case Forbidden:
                    return "Log in to retry server sync.";
                default:
                    return "Safe Sync failed. Your local data is safe.";
            }
        }
    }
}
