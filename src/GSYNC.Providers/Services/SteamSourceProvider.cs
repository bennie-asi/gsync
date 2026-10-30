using System.Text;
using GSYNC.Core.Abstractions;
using GSYNC.Core.Models;
using GSYNC.Core.Utilities;
using GSYNC.Providers.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GSYNC.Providers.Services;

public sealed class SteamSourceProvider : ISourceProvider
{
    private readonly SteamOptions _options;
    private readonly ILogger<SteamSourceProvider> _logger;
    public SteamSourceProvider(IOptions<SteamOptions> options, ILogger<SteamSourceProvider>? logger = null)
    {
        _options = options.Value;
        _logger = logger ?? NullLogger<SteamSourceProvider>.Instance;
    }
    public string ProviderId => "steam";
    public string DisplayName => "Steam";

    public Task<IReadOnlyList<DiscoveredGame>> ScanAsync(CancellationToken cancellationToken)
    {
        var results = new List<DiscoveredGame>();
        foreach (var library in GetSteamLibraries())
        {
            var apps = Path.Combine(library, "steamapps");
            if (!Directory.Exists(apps)) continue;
            foreach (var file in Directory.EnumerateFiles(apps, "appmanifest_*.acf"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var pairs = ReadPairs(file).Where(pair => pair.Depth == 1).ToArray();
                    string? Value(string key) => pairs.Where(pair => pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                        .Select(pair => pair.Value).Distinct().SingleOrDefault();
                    var id = Value("appid") ?? Path.GetFileNameWithoutExtension(file)["appmanifest_".Length..];
                    var install = Value("installdir");
                    if (string.IsNullOrWhiteSpace(id) || !id.All(char.IsAsciiDigit) || string.IsNullOrWhiteSpace(install)) continue;
                    var directory = SafePath.ResolveUnderRoot(Path.Combine(apps, "common"), install);
                    if (!Directory.Exists(directory)) continue;
                    var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["GAME_INSTALL_DIR"] = directory, ["STEAM_LIBRARY"] = library, ["STEAM_ROOT"] = ResolveSteamRoot(),
                    };
                    results.Add(new DiscoveredGame { GameId = "steam-" + id, PlatformGameId = id,
                        DisplayName = Value("name") ?? Path.GetFileName(directory), SourceProviderId = ProviderId,
                        InstallDirectory = directory, Variables = variables });
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
                {
                    _logger.LogWarning(exception, "Skipping invalid Steam manifest {Path}.", file);
                }
            }
        }
        var groups = results.GroupBy(game => game.GameId, StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var group in groups.Where(group => group.Select(game => game.InstallDirectory).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1))
            _logger.LogWarning("Steam app {AppId} has multiple installation locations; configure its save path manually.", group.Key);
        return Task.FromResult<IReadOnlyList<DiscoveredGame>>(groups
            .Where(group => group.Select(game => game.InstallDirectory).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1)
            .Select(group => group.First()).ToArray());
    }

    public IReadOnlyDictionary<string, string> ResolveVariables(GameInstance instance)
    {
        ValidateInstallationIdentity(instance);
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["STEAM_ROOT"] = ResolveSteamRoot(), ["STORE_ROOT"] = ResolveSteamRoot(), ["STORE_USER_ID"] = "*",
        };
        if (!string.IsNullOrWhiteSpace(instance.InstallDirectory))
        {
            variables["GAME_INSTALL_DIR"] = instance.InstallDirectory;
            variables["GAME_DIR_NAME"] = Path.GetFileName(instance.InstallDirectory.TrimEnd('/', '\\'));
        }
        if (!string.IsNullOrWhiteSpace(instance.PlatformInstanceId))
        {
            variables["STEAM_APP_ID"] = instance.PlatformInstanceId;
            variables["STORE_GAME_ID"] = instance.PlatformInstanceId;
        }
        if (instance.Variables.TryGetValue("STEAM_LIBRARY", out var library)) variables["STEAM_LIBRARY"] = library;
        else if (instance.InstallDirectory is { } install && Directory.GetParent(install)?.Parent?.Parent is { } parent)
            variables["STEAM_LIBRARY"] = parent.FullName;
        return variables;
    }

    private static void ValidateInstallationIdentity(GameInstance instance)
    {
        var id = instance.PlatformInstanceId;
        if (string.IsNullOrWhiteSpace(id)) return;
        if (!id.All(char.IsAsciiDigit)) throw new InvalidDataException("The Steam app ID must contain only digits.");
        if (instance.InstallDirectory is not { } install || Directory.GetParent(install) is not { } common ||
            !common.Name.Equals("common", StringComparison.OrdinalIgnoreCase) || common.Parent is not { } apps ||
            !apps.Name.Equals("steamapps", StringComparison.OrdinalIgnoreCase)) return;
        var manifest = Path.Combine(apps.FullName, $"appmanifest_{id}.acf");
        if (!File.Exists(manifest)) return; // Uninstalled games may still have saves in AppData.
        var pairs = ReadPairs(manifest).Where(pair => pair.Depth == 1).ToArray();
        var directory = pairs.Where(pair => pair.Key.Equals("installdir", StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair.Value).Distinct().SingleOrDefault();
        var appId = pairs.Where(pair => pair.Key.Equals("appid", StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair.Value).Distinct().SingleOrDefault();
        if (directory is null || (appId is not null && appId != id) ||
            !string.Equals(SafePath.ResolveUnderRoot(common.FullName, directory), Path.GetFullPath(install), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The Steam installation no longer matches this profile's app ID. Review its save paths before syncing.");
    }

    private IReadOnlyList<string> GetSteamLibraries()
    {
        var root = ResolveSteamRoot();
        if (!Directory.Exists(root)) return Array.Empty<string>();
        var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.GetFullPath(root) };
        var file = Path.Combine(root, "steamapps/libraryfolders.vdf");
        if (File.Exists(file))
        {
            foreach (var pair in ReadPairs(file))
                if ((pair.Key.Equals("path", StringComparison.OrdinalIgnoreCase) || pair.Key.All(char.IsAsciiDigit)) && Path.IsPathFullyQualified(pair.Value))
                    libraries.Add(Path.GetFullPath(pair.Value));
        }
        return libraries.ToArray();
    }

    private string ResolveSteamRoot()
    {
        if (!string.IsNullOrWhiteSpace(_options.SteamRootPath)) return _options.SteamRootPath;
        if (OperatingSystem.IsWindows())
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            if (key?.GetValue("SteamPath") is string path && Directory.Exists(path)) return path;
        }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
    }

    private static IEnumerable<(string Key, string Value, int Depth)> ReadPairs(string path)
    {
        var text = File.ReadAllText(path);
        string? key = null;
        var depth = 0;
        for (var i = 0; i < text.Length;)
        {
            if (char.IsWhiteSpace(text[i])) { i++; continue; }
            if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '/')
            { while (i < text.Length && text[i] != '\n') i++; continue; }
            if (text[i] is '{' or '}')
            {
                depth += text[i] == '{' ? 1 : -1;
                if (depth < 0) throw new InvalidDataException("Unbalanced Steam manifest blocks.");
                key = null; i++; continue;
            }
            var value = new StringBuilder();
            if (text[i] == '"')
            {
                i++;
                while (i < text.Length && text[i] != '"')
                {
                    if (text[i] == '\\' && i + 1 < text.Length && text[i + 1] is '\\' or '"') i++;
                    value.Append(text[i++]);
                }
                if (i == text.Length) throw new InvalidDataException("Unterminated Steam manifest string.");
                i++;
            }
            else while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] is not ('{' or '}')) value.Append(text[i++]);
            if (key is null) key = value.ToString();
            else { yield return (key, value.ToString(), depth); key = null; }
        }
        if (depth != 0) throw new InvalidDataException("Unbalanced Steam manifest blocks.");
    }
}
