using GSYNC.Core.Models;

namespace GSYNC.Core.Abstractions.Data;

public interface IStorageBindingRepository
{
    Task<IReadOnlyList<StorageBinding>> ListByGameInstanceAsync(Guid gameInstanceId, CancellationToken cancellationToken);

    Task<StorageBinding?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task UpsertAsync(StorageBinding binding, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
