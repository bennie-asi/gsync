using GSYNC.Core.Models;

namespace GSYNC.Core.Abstractions.Data;

public interface IGameInstanceRepository
{
    Task<IReadOnlyList<GameInstance>> ListAsync(CancellationToken cancellationToken);

    Task<GameInstance?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task UpsertAsync(GameInstance instance, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
