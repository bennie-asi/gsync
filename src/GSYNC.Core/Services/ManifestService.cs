using GSYNC.Core.Abstractions.Manifest;
using GSYNC.Core.Abstractions.Data;
using GSYNC.Core.Models.Manifest;
using GSYNC.Core.Utilities;
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
    private readonly IGameInstanceRepository? _gameInstances;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    public ManifestService(
        IUserDefinitionStore userDefinitionStore,
        IDefinitionOverrideStore overrideStore,
        ICommunityDefinitionStore communityDefinitionStore,
        ILudusaviManifestParser manifestParser,
        IManifestUpdateSource manifestUpdateSource,
        ILogger<ManifestService> logger,
        IGameInstanceRepository? gameInstances = null)
    {
        _userDefinitionStore = userDefinitionStore;
        _overrideStore = overrideStore;
        _communityDefinitionStore = communityDefinitionStore;
        _manifestParser = manifestParser;
        _manifestUpdateSource = manifestUpdateSource;
        _logger = logger;
        _gameInstances = gameInstances;
    }

    public async Task<IReadOnlyList<GameContentDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken)
    {
        var userDefinitions = await _userDefinitionStore.LoadDefinitionsAsync(cancellationToken);
        var overrideDefinitions = await _overrideStore.GetDefinitionsAsync(cancellationToken);
        var communityDefinitions = await _communityDefinitionStore.GetDefinitionsAsync(cancellationToken);
        var collidingLegacyIds = communityDefinitions.GroupBy(definition => definition.GameId, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var merged = new Dictionary<string, GameContentDefinition>(StringComparer.OrdinalIgnoreCase);
        var legacyTitles = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var stored in communityDefinitions)
        {
            // Old caches used raw titles as IDs. Keep them usable offline until the next refresh.
            var definition = collidingLegacyIds.Contains(stored.GameId)
                ? WithIdentity(stored, GameIdentity.ForLudusaviTitle(stored.GameId), null, stored.GameId) : stored;
            merged.Add(definition.GameId, definition);
            if (definition.SourceHints.TryGetValue("ludusavi.title", out var title)) legacyTitles.Add(title, definition.GameId);
        }
        ApplyDefinitions(merged, overrideDefinitions, legacyTitles);
        ApplyDefinitions(merged, userDefinitions, legacyTitles);

        return merged.Values
            .OrderBy(definition => definition.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<GameContentDefinition?> GetDefinitionAsync(string gameId, CancellationToken cancellationToken)
    {
        var userDefinitions = await _userDefinitionStore.LoadDefinitionsAsync(cancellationToken);
        var community = await _communityDefinitionStore.GetDefinitionAsync(gameId, cancellationToken)
            ?? await _communityDefinitionStore.GetDefinitionAsync(GameIdentity.ForLudusaviTitle(gameId), cancellationToken);
        if (community is null)
        {
            var pinned = userDefinitions.FirstOrDefault(definition => string.Equals(definition.GameId, gameId, StringComparison.OrdinalIgnoreCase));
            if (pinned is not null) return WithIdentity(pinned, gameId, null);
        }
        if (community is null && gameId.StartsWith("ludusavi-", StringComparison.OrdinalIgnoreCase))
        {
            var legacy = (await _communityDefinitionStore.GetDefinitionsAsync(cancellationToken))
                .SingleOrDefault(definition => string.Equals(GameIdentity.ForLudusaviTitle(definition.GameId), gameId, StringComparison.OrdinalIgnoreCase));
            if (legacy is not null) community = WithIdentity(legacy, gameId, null, legacy.GameId);
        }
        if (community is not null && !community.SourceHints.ContainsKey("ludusavi.title") && community.GameId == community.DisplayName)
            community = WithIdentity(community, community.GameId, null, community.GameId);
        string? title = null;
        community?.SourceHints.TryGetValue("ludusavi.title", out title);

        var user = userDefinitions.FirstOrDefault(definition => string.Equals(definition.GameId, gameId, StringComparison.Ordinal));
        // Legacy title aliases are case-sensitive: AFTERLIFE and Afterlife are different games.
        if (user is null && !string.Equals(gameId, title, StringComparison.Ordinal))
            user = userDefinitions.FirstOrDefault(definition => string.Equals(definition.GameId, gameId, StringComparison.OrdinalIgnoreCase));
        if (community is not null)
            user ??= userDefinitions.FirstOrDefault(definition => string.Equals(definition.GameId, community.GameId,
                community.GameId == title ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase));
        if (title is not null)
            user ??= userDefinitions.FirstOrDefault(definition => string.Equals(definition.GameId, title, StringComparison.Ordinal));
        if (user is not null) return WithIdentity(user, gameId, community);

        var overridden = await _overrideStore.GetDefinitionAsync(gameId, cancellationToken);
        if (title is not null && string.Equals(gameId, title, StringComparison.Ordinal) &&
            !string.Equals(overridden?.GameId, title, StringComparison.Ordinal)) overridden = null;
        if (overridden is null && community is not null && !string.Equals(gameId, community.GameId, StringComparison.OrdinalIgnoreCase))
            overridden = await _overrideStore.GetDefinitionAsync(community.GameId, cancellationToken);
        if (overridden is null && title is not null && !string.Equals(gameId, title, StringComparison.Ordinal))
        {
            var legacy = await _overrideStore.GetDefinitionAsync(title, cancellationToken);
            if (string.Equals(legacy?.GameId, title, StringComparison.Ordinal)) overridden = legacy;
        }
        return overridden is not null ? WithIdentity(overridden, gameId, community)
            : community is not null ? WithIdentity(community, gameId, null) : null;
    }

    public async Task RefreshCommunityDefinitionsAsync(CancellationToken cancellationToken)
    {
        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
        var latestManifest = await _manifestUpdateSource.TryFetchLatestManifestAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(latestManifest))
        {
            _logger.LogInformation("No newer community manifest was available.");
            return;
        }

        var definitions = await Task.Run(() => _manifestParser.Parse(latestManifest), cancellationToken);
        if (definitions.Count == 0) throw new InvalidDataException("The downloaded manifest contains no definitions; the existing cache was preserved.");
        if (_gameInstances is not null)
        {
            var used = (await _gameInstances.ListAsync(cancellationToken)).Select(game => game.GameId).Distinct(StringComparer.Ordinal);
            var userIds = (await _userDefinitionStore.LoadDefinitionsAsync(cancellationToken)).Select(game => game.GameId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var gameId in used.Where(id => !userIds.Contains(id)))
            {
                var definition = await GetDefinitionAsync(gameId, cancellationToken);
                if (definition is null) continue;
                try { await _userDefinitionStore.SaveDefinitionAsync(definition, cancellationToken, overwrite: false); }
                catch (InvalidOperationException)
                {
                    // A user edit may have created the higher-priority definition in the meantime.
                    if (!(await _userDefinitionStore.LoadDefinitionsAsync(cancellationToken)).Any(current => string.Equals(current.GameId, definition.GameId, StringComparison.OrdinalIgnoreCase))) throw;
                }
            }
        }
        await _communityDefinitionStore.SaveDefinitionsAsync(definitions, cancellationToken);
        _logger.LogInformation("Community manifest cache refreshed with {DefinitionCount} definitions.", definitions.Count);
        }
        finally { _refreshGate.Release(); }
    }

    private static void ApplyDefinitions(
        IDictionary<string, GameContentDefinition> target,
        IEnumerable<GameContentDefinition> definitions,
        IReadOnlyDictionary<string, string> legacyTitles)
    {
        // A definition already using the canonical ID takes precedence over its old title alias.
        foreach (var definition in definitions.OrderBy(definition => legacyTitles.ContainsKey(definition.GameId) ? 0 : 1))
        {
            var id = legacyTitles.TryGetValue(definition.GameId, out var canonical) ? canonical : definition.GameId;
            target.TryGetValue(id, out var existing);
            target[id] = WithIdentity(definition, id, existing);
        }
    }

    private static GameContentDefinition WithIdentity(GameContentDefinition definition, string id, GameContentDefinition? inherited, string? legacyTitle = null)
    {
        var hints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (inherited is not null) foreach (var hint in inherited.SourceHints) hints[hint.Key] = hint.Value;
        foreach (var hint in definition.SourceHints) hints[hint.Key] = hint.Value;
        if (inherited?.SourceHints.TryGetValue("ludusavi.title", out var title) == true) hints["ludusavi.title"] = title;
        if (legacyTitle is not null) hints["ludusavi.title"] = legacyTitle;
        return new GameContentDefinition
        {
            GameId = id,
            DisplayName = definition.DisplayName,
            Description = definition.Description,
            SourceHints = hints,
            ContentItems = definition.ContentItems,
        };
    }
}
