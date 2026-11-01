using GSYNC.App.Infrastructure.Configuration;
using GSYNC.Core.Abstractions;
using GSYNC.Core.Models;
using GSYNC.Core.Models.Manifest;
using GSYNC.Core.Services.Sync;
using GSYNC.Core.Utilities;
using GSYNC.Storage.Services;

namespace GSYNC.App.Infrastructure.Wizard;

public sealed record RemoteGameCandidate(string Namespace, string Name, string Match, RemoteGameDescriptor? Descriptor);
public sealed record RemoteGameScan(IReadOnlyList<RemoteGameCandidate> Candidates, bool Incomplete);

/// <summary>Finds candidate games without opening or modifying their save files.</summary>
public sealed class RemoteGameDiscoveryService(SyncTargetStore targets, IEnumerable<IStorageProvider> providers)
{
    private const int MaximumDirectories = 200;
    private const int MaximumDepth = 3;

    public async Task<RemoteGameScan> ScanAsync(Guid targetId, GameInstance game, GameContentDefinition definition, CancellationToken cancellationToken)
    {
        var target = targets.Get(targetId) ?? throw new InvalidOperationException("The selected sync target no longer exists.");
        var settings = targets.ResolveSettings(target.Settings);
        var provider = providers.FirstOrDefault(value => string.Equals(value.ProviderId, target.ProviderId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The selected storage provider is unavailable.");
        var configured = provider.WithConfiguration(settings);
        var identities = RemoteGameCatalog.Identities(game, definition);
        var named = GameIdentity.NamedStorageNamespace(definition.DisplayName, definition.GameId);
        var legacy = GameIdentity.StorageNamespace(definition.GameId);
        var suffixes = new[] { named[(named.LastIndexOf('-') + 1)..], legacy[(legacy.LastIndexOf('-') + 1)..] };
        var pending = new Queue<(string Path, int Depth)>();
        pending.Enqueue((string.Empty, 0));
        var found = new List<RemoteGameCandidate>();
        var visited = 0;
        var incomplete = false;
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (path, depth) = pending.Dequeue();
            if (path.Length > 0)
            {
                if (++visited > MaximumDirectories) { incomplete = true; break; }
                try
                {
                    var descriptor = await RemoteGameCatalog.ReadAsync(configured, path, cancellationToken);
                    if (descriptor is not null)
                    {
                        if (RemoteGameCatalog.IsStrongMatch(identities, descriptor.Identities))
                            found.Add(new(path, descriptor.Name, "identity", descriptor));
                        else if (RemoteGameCatalog.IsNameMatch(identities, descriptor.Identities))
                            found.Add(new(path, descriptor.Name, "name", descriptor));
                        continue;
                    }
                    var leaf = path[(path.LastIndexOf('/') + 1)..];
                    if (suffixes.Any(suffix => leaf.EndsWith("-" + suffix, StringComparison.OrdinalIgnoreCase)) &&
                        (await RemoteContentLayout.ListRootsAsync(configured, path, cancellationToken)).Count > 0)
                    {
                        found.Add(new(path, leaf, "legacy", null));
                        continue;
                    }
                }
                catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or HttpRequestException or InvalidOperationException)
                {
                    incomplete = true;
                    continue;
                }
            }
            if (depth >= MaximumDepth) { incomplete = true; continue; }
            try
            {
                IReadOnlyList<string> folders;
                if (provider is WebDavStorageProvider webDav)
                    folders = (await webDav.BrowseDirectoriesAsync(settings, path, cancellationToken)).Select(value => value.Path).ToArray();
                else if (configured.LocalRootPath is { } root)
                {
                    var folder = SafePath.ResolveUnderRoot(root, path, allowRoot: true);
                    folders = !Directory.Exists(folder) ? [] : Directory.EnumerateDirectories(folder)
                        .Where(value => (File.GetAttributes(value) & FileAttributes.ReparsePoint) == 0).Select(Path.GetFileName).OfType<string>().ToArray();
                }
                else throw new NotSupportedException("This storage provider cannot browse game folders.");
                foreach (var name in folders.Where(value => !value.Equals("__gsync_meta", StringComparison.OrdinalIgnoreCase) &&
                                                            !value.Equals("__gsync_versions", StringComparison.OrdinalIgnoreCase)))
                {
                    var child = path.Length == 0 ? name : path + "/" + name;
                    SafePath.ValidateRelative(child);
                    if (pending.Count + visited >= MaximumDirectories) { incomplete = true; break; }
                    pending.Enqueue((child, depth + 1));
                }
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or HttpRequestException or InvalidOperationException)
            { incomplete = true; }
        }
        return new RemoteGameScan(found.OrderBy(value => value.Match == "identity" ? 0 : value.Match == "legacy" ? 1 : 2)
            .ThenBy(value => value.Namespace, StringComparer.OrdinalIgnoreCase).ToArray(), incomplete);
    }
}
