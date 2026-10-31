using System.Text.RegularExpressions;
using System.Text;
using GSYNC.Core.Models;
using GSYNC.Core.Models.Manifest;
using GSYNC.Core.Utilities;

namespace GSYNC.App.Infrastructure.Wizard;

public sealed class AddGameMatchService
{
    public IReadOnlyList<GameCatalogCandidate> FindNameCandidates(string name, IReadOnlyList<GameContentDefinition> definitions)
    {
        var query = NormalizeName(name);
        if (query.Length == 0) return [];
        return definitions
            .Where(definition => definition.ContentItems.Count > 0)
            .Select(definition => (Definition: definition, Name: NormalizeName(definition.DisplayName)))
            .Where(item => item.Name == query || (query.Length >= 3 && item.Name.Contains(query, StringComparison.Ordinal)))
            .OrderByDescending(item => item.Name == query)
            .ThenBy(item => item.Definition.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(30)
            .Select(item => new GameCatalogCandidate(item.Definition, item.Name == query, item.Definition.DisplayName))
            .ToArray();
    }

    public GameDefinitionMatch Match(
        DiscoveredGame game,
        IReadOnlyList<GameContentDefinition> definitions,
        Func<DiscoveredGame, GameContentDefinition> fallbackFactory)
    {
        if (string.Equals(game.SourceProviderId, "steam", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(game.PlatformGameId))
        {
            var matches = definitions.Where(definition => definition.SourceHints.TryGetValue("steam.id", out var id) &&
                string.Equals(id, game.PlatformGameId, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length > 1)
                return new GameDefinitionMatch(game, fallbackFactory(game), MatchConfidence.Fallback,
                    MatchOrigin.FallbackDefinition, "ambiguous-steam-id", true);
            if (matches.Length == 1)
            {
                return new GameDefinitionMatch(
                    game,
                    matches[0],
                    MatchConfidence.SourceHint,
                    MatchOrigin.ExistingDefinition,
                    $"steam.id = {game.PlatformGameId}",
                    false);
            }
        }

        foreach (var definition in definitions)
        {
            if (string.Equals(definition.GameId, game.GameId, StringComparison.OrdinalIgnoreCase))
            {
                return new GameDefinitionMatch(
                    game,
                    definition,
                    MatchConfidence.PlatformId,
                    MatchOrigin.ExistingDefinition,
                    game.GameId,
                    false);
            }
        }

        var normalizedGameName = NormalizeName(game.DisplayName);
        var nameMatches = definitions.Where(definition =>
            definition.ContentItems.Count > 0 &&
            !(game.SourceProviderId == "steam" && !string.IsNullOrWhiteSpace(game.PlatformGameId) &&
              definition.SourceHints.TryGetValue("steam.id", out var hintedId) && !string.Equals(hintedId, game.PlatformGameId, StringComparison.OrdinalIgnoreCase)) &&
            NormalizeName(definition.DisplayName) == normalizedGameName).ToArray();
        if (nameMatches.Length == 1)
        {
            var nameMatch = nameMatches[0];
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

    public static string NormalizeName(string name)
    {
        return new string(name.Normalize(NormalizationForm.FormKC).ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
    }
}

public sealed record GameCatalogCandidate(GameContentDefinition Definition, bool IsExact, string MatchedName);

public sealed class AddGamePathValidationService
{
    private readonly PathResolver _pathResolver;
    private readonly SystemVariableProvider _systemVariableProvider;
    private readonly Func<IReadOnlyDictionary<string, string>>? _userVariablesProvider;

    public AddGamePathValidationService(PathResolver pathResolver, SystemVariableProvider systemVariableProvider,
        Func<IReadOnlyDictionary<string, string>>? userVariablesProvider = null)
    {
        _pathResolver = pathResolver;
        _systemVariableProvider = systemVariableProvider;
        _userVariablesProvider = userVariablesProvider;
    }

    public IReadOnlyList<PathValidationResult> Validate(
        GameDefinitionMatch match,
        IEnumerable<ContentItem> enabledItems,
        IReadOnlyDictionary<string, string> sourceVariables,
        IReadOnlyDictionary<string, string> gameVariables,
        IReadOnlyList<ContentSelection>? selections = null)
    {
        var systemVariables = _systemVariableProvider.GetVariables();
        var userVariables = _userVariablesProvider?.Invoke() ?? new Dictionary<string, string>();
        var results = new List<PathValidationResult>();

        foreach (var item in enabledItems)
        {
            var selection = selections?.FirstOrDefault(value =>
                string.Equals(value.ContentId, item.ContentId, StringComparison.OrdinalIgnoreCase));
            foreach (var template in item.PathTemplates)
            {
                var effectiveTemplate = selection?.EffectiveTemplate(template) ?? template;
                var resolved = _pathResolver.Resolve(effectiveTemplate, systemVariables, sourceVariables, gameVariables, userVariables);
                if (string.IsNullOrWhiteSpace(resolved) || resolved.IndexOfAny(['<', '>']) >= 0 || Regex.IsMatch(resolved, @"%[A-Z0-9_]+%", RegexOptions.IgnoreCase) || !Path.IsPathFullyQualified(resolved))
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

                var (searchRoot, pattern) = PathGlob.SplitRoot(resolved);
                var exists = pattern is null ? File.Exists(resolved) || Directory.Exists(resolved)
                    : Directory.Exists(searchRoot) && Directory.EnumerateFiles(searchRoot, "*", SearchOption.AllDirectories)
                        .Any(file => PathGlob.IsMatch(pattern, Path.GetRelativePath(searchRoot, file), includeDescendants: true));
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
