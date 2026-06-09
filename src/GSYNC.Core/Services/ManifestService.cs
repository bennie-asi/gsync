using GSYNC.Core.Abstractions.Manifest;
using GSYNC.Core.Models.Manifest;
using Microsoft.Extensions.Logging;

namespace GSYNC.Core.Services;

public sealed class ManifestService : IManifestService
{
    private readonly IUserDefinitionStore _userDefinitionStore;
    private readonly IDefinitionOverrideStore _overrideStore;
    private readonly ICommunityDefinitionStore _communityDefinitionStore;
    private readonly ILudusaviManifestParser _manifestParser;
    private readonly IManifestUpdateSource _manifestUpdateSource;
    private readonly ILogger<ManifestService> _logger;

    public ManifestService(
        IUserDefinitionStore userDefinitionStore,
        IDefinitionOverrideStore overrideStore,
        ICommunityDefinitionStore communityDefinitionStore,
        ILudusaviManifestParser manifestParser,
        IManifestUpdateSource manifestUpdateSource,
        ILogger<ManifestService> logger)
    {
        _userDefinitionStore = userDefinitionStore;
        _overrideStore = overrideStore;
        _communityDefinitionStore = communityDefinitionStore;
        _manifestParser = manifestParser;
        _manifestUpdateSource = manifestUpdateSource;
        _logger = logger;
    }

    public async Task<IReadOnlyList<GameContentDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken)
    {
        var userDefinitions = await _userDefinitionStore.LoadDefinitionsAsync(cancellationToken);
        var overrideDefinitions = await _overrideStore.GetDefinitionsAsync(cancellationToken);
        var communityDefinitions = await _communityDefinitionStore.GetDefinitionsAsync(cancellationToken);

        var merged = new Dictionary<string, GameContentDefinition>(StringComparer.OrdinalIgnoreCase);
        ApplyDefinitions(merged, communityDefinitions);
        ApplyDefinitions(merged, overrideDefinitions);
        ApplyDefinitions(merged, userDefinitions);

        return merged.Values
            .OrderBy(definition => definition.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<GameContentDefinition?> GetDefinitionAsync(string gameId, CancellationToken cancellationToken)
    {
        var definitions = await GetDefinitionsAsync(cancellationToken);
        return definitions.FirstOrDefault(definition => string.Equals(definition.GameId, gameId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task RefreshCommunityDefinitionsAsync(CancellationToken cancellationToken)
    {
        var latestManifest = await _manifestUpdateSource.TryFetchLatestManifestAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(latestManifest))
        {
            _logger.LogInformation("No newer community manifest was available.");
            return;
        }

        var definitions = _manifestParser.Parse(latestManifest);
        await _communityDefinitionStore.SaveDefinitionsAsync(definitions, cancellationToken);
        _logger.LogInformation("Community manifest cache refreshed with {DefinitionCount} definitions.", definitions.Count);
    }

    private static void ApplyDefinitions(
        IDictionary<string, GameContentDefinition> target,
        IEnumerable<GameContentDefinition> definitions)
    {
        foreach (var definition in definitions)
        {
            target[definition.GameId] = definition;
        }
    }
}
