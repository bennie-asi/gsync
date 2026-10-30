using GSYNC.Core.Models.Manifest;

namespace GSYNC.Core.Abstractions.Manifest;

public interface ICommunityDefinitionStore
{
    Task<IReadOnlyList<GameContentDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken);

    async Task<GameContentDefinition?> GetDefinitionAsync(string gameId, CancellationToken cancellationToken)
    {
        var definitions = await GetDefinitionsAsync(cancellationToken);
        return definitions.FirstOrDefault(definition => string.Equals(definition.GameId, gameId, StringComparison.Ordinal))
            ?? definitions.SingleOrDefault(definition => string.Equals(definition.GameId, gameId, StringComparison.OrdinalIgnoreCase));
    }

    Task SaveDefinitionsAsync(IEnumerable<GameContentDefinition> definitions, CancellationToken cancellationToken);
}
