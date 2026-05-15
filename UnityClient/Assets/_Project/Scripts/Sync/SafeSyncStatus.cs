namespace TokenForge.Client.Sync
{
    public enum SafeSyncStatus
    {
        Idle,
        CheckingHealth,
        CheckingServer,
        Ready,
        ServerReady,
        Syncing,
        Synced,
        Fetching,
        FetchingRemoteSessions,
        DeleteInProgress,
        RetryPending,
        RetryInProgress,
        RetrySucceeded,
        RetryFailed,
        RetryWaiting,
        PrivacyBlocked,
        ConflictDetected,
        Failed,
        AuthRequired,
        ServerUnavailable
    }
}
