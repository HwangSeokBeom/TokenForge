using System.Threading;
using System.Threading.Tasks;
using TokenForge.Client.Common;

namespace TokenForge.Client.Sync
{
    public interface ISafeSyncRetryQueueRepository
    {
        string FilePath { get; }
        string BackupFilePath { get; }
        Task<SafeSyncRetryQueueState> LoadAsync(CancellationToken cancellationToken = default);
        Task<Result> SaveAsync(SafeSyncRetryQueueState state, CancellationToken cancellationToken = default);
    }
}
