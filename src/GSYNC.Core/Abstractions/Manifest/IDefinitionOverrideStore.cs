using GSYNC.Core.Models.Manifest;

namespace GSYNC.Core.Abstractions.Manifest;

public interface IDefinitionOverrideStore
{
    Task<IReadOnlyList<GameContentDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken);

    Task SaveDefinitionsAsync(IEnumerable<GameContentDefinition> definitions, CancellationToken cancellationToken);
}
