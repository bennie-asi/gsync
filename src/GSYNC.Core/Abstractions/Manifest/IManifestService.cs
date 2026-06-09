using GSYNC.Core.Models;
using GSYNC.Core.Models.Manifest;

namespace GSYNC.Core.Abstractions.Manifest;

public interface IManifestService
{
    Task<IReadOnlyList<GameContentDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken);

    Task<GameContentDefinition?> GetDefinitionAsync(string gameId, CancellationToken cancellationToken);

    Task RefreshCommunityDefinitionsAsync(CancellationToken cancellationToken);
}
