using GSYNC.Core.Models.Manifest;

namespace GSYNC.Core.Abstractions.Manifest;

public interface IUserDefinitionStore
{
    Task<IReadOnlyList<GameContentDefinition>> LoadDefinitionsAsync(CancellationToken cancellationToken);

    Task SaveDefinitionAsync(GameContentDefinition definition, CancellationToken cancellationToken, bool overwrite = true);
}
