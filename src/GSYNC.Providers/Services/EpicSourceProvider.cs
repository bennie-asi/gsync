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
            EpicItemManifest? manifest;
            try
            {
                await using var stream = File.OpenRead(manifestFile);
                manifest = await JsonSerializer.DeserializeAsync<EpicItemManifest>(stream, cancellationToken: cancellationToken);
            }
            catch (JsonException) { continue; }
            if (manifest is null || string.IsNullOrWhiteSpace(manifest.DisplayName) || string.IsNullOrWhiteSpace(manifest.InstallLocation) || !Directory.Exists(manifest.InstallLocation))
            {
                continue;
            }

            var platformId = !string.IsNullOrWhiteSpace(manifest.CatalogItemId) ? manifest.CatalogItemId.Trim()
                : !string.IsNullOrWhiteSpace(manifest.AppName) ? manifest.AppName.Trim()
                : null;
            results.Add(new DiscoveredGame
            {
                GameId = $"epic-{platformId ?? manifest.DisplayName}".ToLowerInvariant(),
                DisplayName = manifest.DisplayName,
                SourceProviderId = ProviderId,
                InstallDirectory = manifest.InstallLocation,
                PlatformGameId = platformId,
                Variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["GAME_INSTALL_DIR"] = manifest.InstallLocation ?? string.Empty,
                    ["EPIC_APP_ID"] = platformId ?? string.Empty,
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
            variables["GAME_DIR_NAME"] = Path.GetFileName(instance.InstallDirectory.TrimEnd('/', '\\'));
            variables["STORE_ROOT"] = Path.GetDirectoryName(instance.InstallDirectory)!;
        }

        if (!string.IsNullOrWhiteSpace(instance.PlatformInstanceId))
        {
            variables["EPIC_APP_ID"] = instance.PlatformInstanceId;
            variables["STORE_GAME_ID"] = instance.PlatformInstanceId;
        }

        variables["STORE_USER_ID"] = "*";
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
