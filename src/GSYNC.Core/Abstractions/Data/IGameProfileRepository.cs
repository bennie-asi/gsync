using GSYNC.Core.Models;

namespace GSYNC.Core.Abstractions.Data;

public interface IGameProfileRepository
{
    Task CreateAsync(GameInstance instance, StorageBinding binding, CancellationToken cancellationToken);
}
