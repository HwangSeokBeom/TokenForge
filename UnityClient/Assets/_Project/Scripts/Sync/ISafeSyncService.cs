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
        Task<SafeSyncResult> FetchRemoteSessionsAsync(CancellationToken cancellationToken = default);
        Task<SafeSyncResult> DeleteRemoteSessionAsync(string serverSessionId, CancellationToken cancellationToken = default);
    }
}
