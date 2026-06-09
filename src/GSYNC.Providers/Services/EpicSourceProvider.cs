using System.Text.Json;
using GSYNC.Core.Abstractions;
using GSYNC.Core.Models;
using GSYNC.Providers.Options;
using Microsoft.Extensions.Options;

namespace GSYNC.Providers.Services;

public sealed class EpicSourceProvider : ISourceProvider
{
    private readonly EpicOptions _options;

    public EpicSourceProvider(IOptions<EpicOptions> options)
    {
        _options = options.Value;
    }

    public string ProviderId => "epic";

    public string DisplayName => "Epic Games";

    public async Task<IReadOnlyList<DiscoveredGame>> ScanAsync(CancellationToken cancellationToken)
    {
        var manifestsDirectory = ResolveManifestsDirectory();
        if (string.IsNullOrWhiteSpace(manifestsDirectory) || !Directory.Exists(manifestsDirectory))
        {
            return Array.Empty<DiscoveredGame>();
        }

        var results = new List<DiscoveredGame>();
        foreach (var manifestFile in Directory.EnumerateFiles(manifestsDirectory, "*.item", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var stream = File.OpenRead(manifestFile);
            var manifest = await JsonSerializer.DeserializeAsync<EpicItemManifest>(stream, cancellationToken: cancellationToken);
            if (manifest is null || string.IsNullOrWhiteSpace(manifest.DisplayName))
            {
                continue;
            }

            results.Add(new DiscoveredGame
            {
                GameId = $"epic-{manifest.CatalogItemId ?? manifest.AppName ?? manifest.DisplayName}".ToLowerInvariant(),
                DisplayName = manifest.DisplayName,
                SourceProviderId = ProviderId,
                InstallDirectory = manifest.InstallLocation,
                PlatformGameId = manifest.CatalogItemId ?? manifest.AppName,
                Variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["GAME_INSTALL_DIR"] = manifest.InstallLocation ?? string.Empty,
                    ["EPIC_APP_ID"] = manifest.CatalogItemId ?? manifest.AppName ?? string.Empty,
                },
            });
        }

        return results;
    }

    public IReadOnlyDictionary<string, string> ResolveVariables(GameInstance instance)
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(instance.InstallDirectory))
        {
            variables["GAME_INSTALL_DIR"] = instance.InstallDirectory;
        }

        if (!string.IsNullOrWhiteSpace(instance.PlatformInstanceId))
        {
            variables["EPIC_APP_ID"] = instance.PlatformInstanceId;
        }

        return variables;
    }

    private string ResolveManifestsDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_options.ManifestsDirectory))
        {
            return _options.ManifestsDirectory;
        }

        var commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return Path.Combine(commonAppData, "Epic", "EpicGamesLauncher", "Data", "Manifests");
    }

    private sealed class EpicItemManifest
    {
        public string? AppName { get; init; }

        public string? CatalogItemId { get; init; }

        public string? DisplayName { get; init; }

        public string? InstallLocation { get; init; }
    }
}
