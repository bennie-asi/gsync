using System.Text.Json;
using System.Security.Cryptography;
using GSYNC.Core.Abstractions;
using GSYNC.Core.Abstractions.Data;
using GSYNC.Core.Abstractions.Manifest;
using GSYNC.Core.Models;
using GSYNC.Core.Utilities;
using static GSYNC.Core.Services.Sync.SyncFileService;

namespace GSYNC.Core.Services.Sync;

internal sealed class SyncContextFactory(
    IGameInstanceRepository games, IStorageBindingRepository bindings, IManifestService manifests,
    IEnumerable<ISourceProvider> sources, IEnumerable<IStorageProvider> storage, SyncFileService files,
    Func<StorageBinding, IReadOnlyDictionary<string, string>>? configuration,
    Func<StorageBinding, Guid, StorageBinding>? targetBinding = null)
{
    private readonly IGameInstanceRepository _gameInstanceRepository = games;
    private readonly IStorageBindingRepository _storageBindingRepository = bindings;
    private readonly IManifestService _manifestService = manifests;
    private readonly IEnumerable<ISourceProvider> _sourceProviders = sources;
    private readonly IEnumerable<IStorageProvider> _storageProviders = storage;
    private readonly SyncFileService _files = files;
    private readonly Func<StorageBinding, IReadOnlyDictionary<string, string>>? _storageConfigurationResolver = configuration;
    internal async Task<SyncExecutionContext> BuildAsync(Guid gameInstanceId, CancellationToken cancellationToken, bool configureStorage = true, Guid? targetId = null)
    {
        var gameInstance = await _gameInstanceRepository.GetAsync(gameInstanceId, cancellationToken)
            ?? throw new InvalidOperationException($"Game instance '{gameInstanceId}' was not found.");

        var binding = (await _storageBindingRepository.ListByGameInstanceAsync(gameInstanceId, cancellationToken)).FirstOrDefault()
            ?? throw new InvalidOperationException($"No storage binding exists for game instance '{gameInstanceId}'.");
        if (targetId is { } requestedTarget)
            binding = targetBinding?.Invoke(binding, requestedTarget) ?? throw new InvalidOperationException("Selecting another sync target is not supported by this installation.");

        var definition = await _manifestService.GetDefinitionAsync(gameInstance.GameId, cancellationToken)
            ?? throw new InvalidOperationException($"No manifest definition exists for game '{gameInstance.GameId}'.");

        if (string.Equals(gameInstance.SourceProviderId, "steam", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(gameInstance.PlatformInstanceId) && definition.SourceHints.TryGetValue("steam.id", out var steamId) &&
            !string.Equals(steamId, gameInstance.PlatformInstanceId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The Steam app ID does not match the save definition. Review this game's profile before syncing.");

        var sourceProvider = _sourceProviders.FirstOrDefault(provider => string.Equals(provider.ProviderId, gameInstance.SourceProviderId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Source provider '{gameInstance.SourceProviderId}' is not registered.");

        var storageProvider = _storageProviders.FirstOrDefault(provider => string.Equals(provider.ProviderId, binding.StorageProviderId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Storage provider '{binding.StorageProviderId}' is not registered.");
        var effectiveConfiguration = configureStorage ? _storageConfigurationResolver?.Invoke(binding) ?? binding.Settings : binding.Settings;
        if (configureStorage)
        {
            storageProvider = storageProvider.WithConfiguration(effectiveConfiguration);
            storageProvider = await RemoteSaveSession.OpenAsync(storageProvider, binding.RemoteNamespace, cancellationToken);
        }

        SafePath.ValidateRelative(binding.RemoteNamespace);
        var resolvedContentRoots = _files.ResolveContentRoots(definition, gameInstance, sourceProvider, binding);
        if (configureStorage)
        {
            foreach (var root in resolvedContentRoots)
            {
                try
                {
                    var descriptor = await ((RemoteSaveSession)storageProvider).ReadMetadataAsync<RootDescriptor>(RootMetadataPath(binding, root), cancellationToken);
                    if (descriptor?.Version != 1 || descriptor.IsDirectory is null) throw new InvalidDataException("Invalid remote content metadata.");
                    root.UseRemoteKind(descriptor.IsDirectory.Value);
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
                catch (HttpRequestException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound) { }

                if (storageProvider.LocalRootPath is { } storageRoot)
                {
                    var source = SafePath.GetPhysicalPath(root.LocalRootPath).TrimEnd(Path.DirectorySeparatorChar);
                    var remote = SafePath.GetPhysicalPath(storageRoot).TrimEnd(Path.DirectorySeparatorChar);
                    if (string.Equals(source, remote, StringComparison.OrdinalIgnoreCase) ||
                        source.StartsWith(remote + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                        (root.TreatAsDirectory && remote.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidOperationException("Local save paths and the storage folder must not overlap.");
                }
            }
        }
        // Do not persist credentials. These fields identify the destination and resolved local scope.
        var targetIdentity = effectiveConfiguration
            .Where(pair => pair.Key.Equals("targetId", StringComparison.OrdinalIgnoreCase) || pair.Key.Equals("rootPath", StringComparison.OrdinalIgnoreCase) ||
                           pair.Key.Equals("baseUrl", StringComparison.OrdinalIgnoreCase) || pair.Key.Equals("username", StringComparison.OrdinalIgnoreCase))
            .Select(pair => new { Key = pair.Key.ToUpperInvariant(), pair.Value }).ToList();
        if (binding.Settings.TryGetValue("__gsyncTargetRevision", out var targetRevision))
            targetIdentity.Add(new { Key = "__GSYNCTARGETREVISION", Value = targetRevision });
        var identity = new
        {
            gameInstance.GameId, gameInstance.SourceProviderId, binding.Id, binding.StorageProviderId, binding.RemoteNamespace,
            Target = targetIdentity.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).ToArray(),
            Roots = resolvedContentRoots.OrderBy(root => root.StorageKey, StringComparer.OrdinalIgnoreCase).Select(root => new
            {
                root.StorageKey, root.LocalRootPath, root.TreatAsDirectory, root.GlobPattern,
                root.ContentItem.IncludePatterns, root.ContentItem.ExcludePatterns,
            }).ToArray(),
        };
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(identity)));
        var resolvedTargetId = effectiveConfiguration.TryGetValue("targetId", out var rawTargetId) && Guid.TryParse(rawTargetId, out var parsedTargetId)
            ? parsedTargetId : (Guid?)null;
        effectiveConfiguration.TryGetValue("__gsyncHistoryTargetName", out var targetName);
        return new SyncExecutionContext(gameInstance, definition, binding, sourceProvider, storageProvider, resolvedContentRoots, fingerprint, resolvedTargetId, targetName);
    }

}
