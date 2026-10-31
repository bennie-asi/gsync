using GSYNC.Core.Models;
using GSYNC.Core.Models.Manifest;

namespace GSYNC.App.Infrastructure.Wizard;

public sealed record GameArtworkInfo(string? CoverUri, string? LocalArtworkPath, string? ProgramPath);

public static class GameArtworkResolver
{
    public static GameArtworkInfo Resolve(GameInstance instance, GameContentDefinition? definition,
        IReadOnlyList<GameContentDefinition>? catalog = null) =>
        ResolveWithIndex(instance, definition, catalog is null ? null : BuildCatalogIndex(catalog));

    public static IReadOnlyDictionary<string, string?> BuildCatalogIndex(IReadOnlyList<GameContentDefinition> catalog)
    {
        var index = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var candidate in catalog)
        {
            var id = SteamId(candidate);
            var name = AddGameMatchService.NormalizeName(candidate.DisplayName);
            if (id is null || name.Length == 0) continue;
            if (!index.TryGetValue(name, out var existing)) index[name] = id;
            else if (!string.Equals(existing, id, StringComparison.Ordinal)) index[name] = null;
        }
        return index;
    }

    public static GameArtworkInfo ResolveWithIndex(GameInstance instance, GameContentDefinition? definition,
        IReadOnlyDictionary<string, string?>? catalog)
    {
        var steamId = SteamId(definition);
        if (steamId is null && (instance.SourceProviderId.Equals("steam", StringComparison.OrdinalIgnoreCase) ||
                                instance.SourceProviderId.Equals("custom", StringComparison.OrdinalIgnoreCase)))
            steamId = ValidSteamId(instance.PlatformInstanceId);
        if (steamId is null && catalog is not null)
        {
            catalog.TryGetValue(AddGameMatchService.NormalizeName(instance.DisplayName), out steamId);
        }
        var cover = steamId is null ? null : $"https://cdn.cloudflare.steamstatic.com/steam/apps/{steamId}/library_600x900.jpg";
        instance.Variables.TryGetValue(GameProgramSelectionReader.ArtworkPathVariable, out var artwork);
        instance.Variables.TryGetValue(GameProgramSelectionReader.ProgramPathVariable, out var program);
        return new GameArtworkInfo(cover, artwork, program);
    }

    private static string? SteamId(GameContentDefinition? definition) =>
        definition is not null && definition.SourceHints.TryGetValue("steam.id", out var id) ? ValidSteamId(id) : null;

    private static string? ValidSteamId(string? id) =>
        !string.IsNullOrWhiteSpace(id) && id.Length <= 20 && id.All(char.IsAsciiDigit) && id.Any(character => character != '0') ? id : null;
}
