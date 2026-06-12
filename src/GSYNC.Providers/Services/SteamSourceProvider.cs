using GSYNC.Core.Abstractions;
using GSYNC.Core.Models;
using GSYNC.Providers.Options;
using Microsoft.Extensions.Options;

namespace GSYNC.Providers.Services;

public sealed class SteamSourceProvider : ISourceProvider
{
    private const string LibraryFoldersRelativePath = "steamapps/libraryfolders.vdf";
    private const string CommonRelativePath = "steamapps/common";
    private readonly SteamOptions _options;

    public SteamSourceProvider(IOptions<SteamOptions> options)
    {
        _options = options.Value;
    }

    public string ProviderId => "steam";

    public string DisplayName => "Steam";

    public Task<IReadOnlyList<DiscoveredGame>> ScanAsync(CancellationToken cancellationToken)
    {
        var libraries = GetSteamLibraries();
        var results = new List<DiscoveredGame>();

        foreach (var library in libraries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var commonDirectory = Path.Combine(library, CommonRelativePath);
            if (!Directory.Exists(commonDirectory))
            {
                continue;
            }

            foreach (var gameDirectory in Directory.EnumerateDirectories(commonDirectory))
            {
                var displayName = Path.GetFileName(gameDirectory);
                results.Add(new DiscoveredGame
                {
                    GameId = $"steam-{displayName}".ToLowerInvariant(),
                    DisplayName = displayName,
                    SourceProviderId = ProviderId,
                    InstallDirectory = gameDirectory,
                    PlatformGameId = TryReadSteamAppId(gameDirectory),
                    Variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["GAME_INSTALL_DIR"] = gameDirectory,
                        ["STEAM_LIBRARY"] = library,
                    },
                });
            }
        }

        return Task.FromResult<IReadOnlyList<DiscoveredGame>>(results);
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
            variables["STEAM_APP_ID"] = instance.PlatformInstanceId;
        }

        var libraries = GetSteamLibraries();
        if (libraries.Count > 0)
        {
            variables["STEAM_LIBRARY"] = libraries[0];
        }

        return variables;
    }

    private IReadOnlyList<string> GetSteamLibraries()
    {
        var root = ResolveSteamRoot();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return Array.Empty<string>();
        }

        var libraries = new List<string> { root };
        var libraryFoldersFile = Path.Combine(root, LibraryFoldersRelativePath);
        if (!File.Exists(libraryFoldersFile))
        {
            return libraries;
        }

        foreach (var line in File.ReadLines(libraryFoldersFile))
        {
            var trimmed = line.Trim();
            if (!trimmed.Contains("\"path\"", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = trimmed.Split('"', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 4)
            {
                continue;
            }

            var path = parts[3].Replace("\\\\", "\\", StringComparison.Ordinal);
            if (!libraries.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                libraries.Add(path);
            }
        }

        return libraries;
    }

    private string? ResolveSteamRoot()
    {
        if (!string.IsNullOrWhiteSpace(_options.SteamRootPath))
        {
            return _options.SteamRootPath;
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
    }

    private static string? TryReadSteamAppId(string gameDirectory)
    {
        var commonDirectory = Directory.GetParent(gameDirectory)?.FullName;
        var steamAppsDirectory = commonDirectory is null ? null : Directory.GetParent(commonDirectory)?.FullName;
        if (string.IsNullOrWhiteSpace(steamAppsDirectory) || !Directory.Exists(steamAppsDirectory))
        {
            return null;
        }

        foreach (var manifestPath in Directory.EnumerateFiles(steamAppsDirectory, "appmanifest_*.acf", SearchOption.TopDirectoryOnly))
        {
            foreach (var line in File.ReadLines(manifestPath))
            {
                var trimmed = line.Trim();
                if (!trimmed.Contains("\"installdir\"", StringComparison.OrdinalIgnoreCase) ||
                    !trimmed.Contains(Path.GetFileName(gameDirectory), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var name = Path.GetFileNameWithoutExtension(manifestPath);
                return name.StartsWith("appmanifest_", StringComparison.OrdinalIgnoreCase)
                    ? name["appmanifest_".Length..]
                    : null;
            }
        }

        return null;
    }
}
