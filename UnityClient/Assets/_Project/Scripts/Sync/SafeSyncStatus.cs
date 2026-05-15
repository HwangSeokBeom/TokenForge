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
        Failed,
        AuthRequired,
        ServerUnavailable
    }
}
