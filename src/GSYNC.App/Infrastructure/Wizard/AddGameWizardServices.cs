using System.Text.RegularExpressions;
using GSYNC.Core.Models;
using GSYNC.Core.Models.Manifest;
using GSYNC.Core.Utilities;

namespace GSYNC.App.Infrastructure.Wizard;

public sealed class AddGameMatchService
{
    public GameDefinitionMatch Match(
        DiscoveredGame game,
        IReadOnlyList<GameContentDefinition> definitions,
        Func<DiscoveredGame, GameContentDefinition> fallbackFactory)
    {
        foreach (var definition in definitions)
        {
            if (definition.SourceHints.TryGetValue("steam.id", out var steamId) &&
                !string.IsNullOrWhiteSpace(game.PlatformGameId) &&
                string.Equals(steamId, game.PlatformGameId, StringComparison.OrdinalIgnoreCase))
            {
                return new GameDefinitionMatch(
                    game,
                    definition,
                    MatchConfidence.SourceHint,
                    MatchOrigin.ExistingDefinition,
                    $"steam.id = {steamId}",
                    false);
            }
        }

        foreach (var definition in definitions)
        {
            if (!string.IsNullOrWhiteSpace(game.PlatformGameId) &&
                (string.Equals(definition.GameId, game.PlatformGameId, StringComparison.OrdinalIgnoreCase) ||
                 definition.GameId.Contains(game.PlatformGameId, StringComparison.OrdinalIgnoreCase)))
            {
                return new GameDefinitionMatch(
                    game,
                    definition,
                    MatchConfidence.PlatformId,
                    MatchOrigin.ExistingDefinition,
                    game.PlatformGameId,
                    false);
            }
        }

        var normalizedGameName = NormalizeName(game.DisplayName);
        var nameMatch = definitions.FirstOrDefault(definition =>
            definition.ContentItems.Count > 0 &&
            NormalizeName(definition.DisplayName) == normalizedGameName);
        if (nameMatch is not null)
        {
            return new GameDefinitionMatch(
                game,
                nameMatch,
                MatchConfidence.NormalizedName,
                MatchOrigin.ExistingDefinition,
                nameMatch.DisplayName,
                false);
        }

        var fallback = fallbackFactory(game);
        return new GameDefinitionMatch(
            game,
            fallback,
            MatchConfidence.Fallback,
            MatchOrigin.FallbackDefinition,
            $"fallback:{game.DisplayName}",
            true);
    }

    private static string NormalizeName(string name)
    {
        return new string(name.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
    }
}

public sealed class AddGamePathValidationService
{
    private readonly PathResolver _pathResolver;
    private readonly SystemVariableProvider _systemVariableProvider;

    public AddGamePathValidationService(PathResolver pathResolver, SystemVariableProvider systemVariableProvider)
    {
        _pathResolver = pathResolver;
        _systemVariableProvider = systemVariableProvider;
    }

    public IReadOnlyList<PathValidationResult> Validate(
        GameDefinitionMatch match,
        IEnumerable<ContentItem> enabledItems,
        IReadOnlyDictionary<string, string> sourceVariables,
        IReadOnlyDictionary<string, string> gameVariables)
    {
        var systemVariables = _systemVariableProvider.GetVariables();
        var results = new List<PathValidationResult>();

        foreach (var item in enabledItems)
        {
            foreach (var template in item.PathTemplates)
            {
                var resolved = _pathResolver.Resolve(template, systemVariables, sourceVariables, gameVariables);
                if (string.IsNullOrWhiteSpace(resolved) || Regex.IsMatch(resolved, @"%[A-Z0-9_]+%", RegexOptions.IgnoreCase))
                {
                    results.Add(new PathValidationResult(
                        item.ContentId,
                        template,
                        resolved,
                        PathValidationSeverity.Blocking,
                        "unresolved-template",
                        false,
                        false));
                    continue;
                }

                var exists = File.Exists(resolved) || Directory.Exists(resolved);
                var severity = exists || match.Origin == MatchOrigin.ManualDefinition
                    ? PathValidationSeverity.Ok
                    : PathValidationSeverity.Warning;
                var messageCode = exists
                    ? "path-exists"
                    : "path-missing";

                results.Add(new PathValidationResult(
                    item.ContentId,
                    template,
                    resolved,
                    severity,
                    messageCode,
                    exists,
                    true));
            }
        }

        if (results.Count > 0 && results.All(result => result.Severity == PathValidationSeverity.Blocking))
        {
            return results;
        }

        return results;
    }
}
