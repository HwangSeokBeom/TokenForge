using System.Threading;
using System.Threading.Tasks;
using TokenForge.Client.Domain;

namespace TokenForge.Client.Persistence
{
    public interface ISaveDataStore
    {
        Task<SaveData> LoadAsync(CancellationToken cancellationToken);
        Task SaveAsync(SaveData saveData, CancellationToken cancellationToken);
        Task DeleteAsync(CancellationToken cancellationToken);
    }
}
