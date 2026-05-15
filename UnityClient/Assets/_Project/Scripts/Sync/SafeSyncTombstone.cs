using System;
using System.Collections.Generic;

namespace TokenForge.Client.Sync
{
    public enum SafeSyncTombstoneDeleteSource
    {
        LocalUser,
        RemoteUser,
        ServerNotFound
    }

    public enum SafeSyncTombstoneStatus
    {
        PendingDelete,
        DeleteSynced,
        DeleteFailed,
        DeleteCancelled,
        DeleteAlreadyApplied,
        DeleteResolved
    }

    [Serializable]
    public sealed class SafeSyncTombstone
    {
        public string TombstoneId { get; set; } = Guid.NewGuid().ToString("N");
        public string ClientSessionId { get; set; } = string.Empty;
        public string ServerSessionId { get; set; } = string.Empty;
        public DateTimeOffset DeletedAt { get; set; } = DateTimeOffset.UtcNow;
        public SafeSyncTombstoneDeleteSource DeleteSource { get; set; } = SafeSyncTombstoneDeleteSource.LocalUser;
        public SafeSyncTombstoneStatus SyncStatus { get; set; } = SafeSyncTombstoneStatus.PendingDelete;
        public string LastSafeErrorCode { get; set; } = string.Empty;
        public int SchemaVersion { get; set; } = 1;
    }

    [Serializable]
    public sealed class SafeSyncTombstoneState
    {
        public int SchemaVersion { get; set; } = 1;
        public List<SafeSyncTombstone> Tombstones { get; set; } = new List<SafeSyncTombstone>();
    }

    [Serializable]
    public sealed class SafeSyncTombstoneSummary
    {
        public int PendingDeleteCount { get; set; }
        public int DeleteSyncedCount { get; set; }
        public int DeleteFailedCount { get; set; }
        public int DeleteResolvedCount { get; set; }
        public List<SafeSyncTombstone> SafeTombstones { get; set; } = new List<SafeSyncTombstone>();
    }
}
