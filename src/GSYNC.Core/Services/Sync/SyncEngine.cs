using System.IO.Compression;
using System.IO.Enumeration;
using System.Security.Cryptography;
using GSYNC.Core.Abstractions;
using GSYNC.Core.Abstractions.Data;
using GSYNC.Core.Abstractions.Manifest;
using GSYNC.Core.Abstractions.Sync;
using GSYNC.Core.Models;
using GSYNC.Core.Models.Manifest;
using GSYNC.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace GSYNC.Core.Services.Sync;

public sealed class SyncEngine : ISyncEngine
{
    private readonly ISyncQueue _syncQueue;
    private readonly IGameInstanceRepository _gameInstanceRepository;
    private readonly IStorageBindingRepository _storageBindingRepository;
    private readonly ISyncHistoryRepository _syncHistoryRepository;
    private readonly IManifestService _manifestService;
    private readonly IEnumerable<ISourceProvider> _sourceProviders;
    private readonly IEnumerable<IStorageProvider> _storageProviders;
    private readonly IAppPathService _appPathService;
    private readonly PathResolver _pathResolver;
    private readonly SystemVariableProvider _systemVariableProvider;
    private readonly ILogger<SyncEngine> _logger;

    public SyncEngine(
        ISyncQueue syncQueue,
        IGameInstanceRepository gameInstanceRepository,
        IStorageBindingRepository storageBindingRepository,
        ISyncHistoryRepository syncHistoryRepository,
        IManifestService manifestService,
        IEnumerable<ISourceProvider> sourceProviders,
        IEnumerable<IStorageProvider> storageProviders,
        IAppPathService appPathService,
        PathResolver pathResolver,
        SystemVariableProvider systemVariableProvider,
        ILogger<SyncEngine> logger)
    {
        _syncQueue = syncQueue;
        _gameInstanceRepository = gameInstanceRepository;
        _storageBindingRepository = storageBindingRepository;
        _syncHistoryRepository = syncHistoryRepository;
        _manifestService = manifestService;
        _sourceProviders = sourceProviders;
        _storageProviders = storageProviders;
        _appPathService = appPathService;
        _pathResolver = pathResolver;
        _systemVariableProvider = systemVariableProvider;
        _logger = logger;
    }

    public Task QueueAsync(SyncJob syncJob, CancellationToken cancellationToken)
    {
        return _syncQueue.QueueAsync(syncJob, cancellationToken).AsTask();
    }

    public async Task<SyncComparison> CompareAsync(SyncJob syncJob, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, syncJob.CancellationToken);
        return await CompareInternalAsync(syncJob, linked.Token);
    }

    public async Task ProcessQueuedJobsAsync(CancellationToken cancellationToken)
    {
        await foreach (var job in _syncQueue.ReadAllAsync(cancellationToken))
        {
            _syncQueue.Start(job);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, job.CancellationToken);
            var jobToken = linked.Token;

            try
            {
                switch (job.Direction)
                {
                    case SyncDirection.Upload:
                        await ExecuteUploadAsync(job, jobToken);
                        break;
                    case SyncDirection.Download:
                        await ExecuteDownloadAsync(job, jobToken);
                        break;
                    case SyncDirection.Compare:
                        await ExecuteCompareAsync(job, jobToken);
                        break;
                }
            }
            finally
            {
                _syncQueue.Complete();
            }
        }
    }

    private async Task ExecuteCompareAsync(SyncJob job, CancellationToken cancellationToken)
    {
        try
        {
            var comparison = await CompareInternalAsync(job, cancellationToken);
            await WriteTerminalRecordAsync(
                job.GameInstanceId,
                SyncDirection.Compare,
                SyncJobStatus.Completed,
                cancellationToken,
                processedFiles: comparison.Entries.Count,
                totalFiles: comparison.Entries.Count,
                summary: $"Compared {comparison.Entries.Count} files.");
        }
        catch (OperationCanceledException)
        {
            await WriteTerminalRecordAsync(
                job.GameInstanceId,
                SyncDirection.Compare,
                SyncJobStatus.Cancelled,
                CancellationToken.None,
                summary: "Compare cancelled.");
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Compare job failed for game instance {GameInstanceId}.", job.GameInstanceId);
            await WriteTerminalRecordAsync(
                job.GameInstanceId,
                SyncDirection.Compare,
                SyncJobStatus.Failed,
                CancellationToken.None,
                errorMessage: exception.Message);
            throw;
        }
    }

    private async Task<SyncComparison> CompareInternalAsync(SyncJob syncJob, CancellationToken cancellationToken)
    {
        var context = await BuildContextAsync(syncJob.GameInstanceId, cancellationToken);
        var entries = new List<SyncDiffEntry>();

        foreach (var root in context.ContentRoots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var localFiles = EnumerateLocalFiles(root).ToDictionary(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase);
            var remoteFiles = await ListRemoteFilesAsync(context, root, cancellationToken);
            var allRelativePaths = localFiles.Keys.Union(remoteFiles.Keys, StringComparer.OrdinalIgnoreCase);

            foreach (var relativePath in allRelativePaths)
            {
                localFiles.TryGetValue(relativePath, out var localFile);
                remoteFiles.TryGetValue(relativePath, out var remoteFile);

                var localHash = localFile is not null ? await ComputeFileHashAsync(localFile.AbsolutePath, cancellationToken) : null;
                entries.Add(new SyncDiffEntry
                {
                    RelativePath = BuildRemotePath(context.Binding.RemoteNamespace, root.ContentItem.ContentId, relativePath),
                    ChangeKind = DetermineChangeKind(localFile, remoteFile, localHash),
                    LocalHash = localHash,
                    RemoteHash = remoteFile?.Hash,
                    LocalModifiedAtUtc = localFile?.LastModifiedUtc,
                    RemoteModifiedAtUtc = remoteFile?.LastModifiedUtc,
                });
            }
        }

        return new SyncComparison
        {
            Entries = entries,
        };
    }

    private async Task ExecuteUploadAsync(SyncJob job, CancellationToken cancellationToken)
    {
        try
        {
            var context = await BuildContextAsync(job.GameInstanceId, cancellationToken);
            var uploadFiles = context.ContentRoots.SelectMany(EnumerateLocalFiles).ToArray();
            var total = uploadFiles.Length;
            var processed = 0;

            foreach (var file in uploadFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await using var stream = File.OpenRead(file.AbsolutePath);
                var remotePath = BuildRemotePath(context.Binding.RemoteNamespace, file.ContentItem.ContentId, file.RelativePath);
                await context.StorageProvider.UploadAsync(remotePath, stream, cancellationToken);
                processed++;
                job.Progress?.Report(new SyncProgress
                {
                    CurrentFileName = file.RelativePath,
                    ProcessedFiles = processed,
                    TotalFiles = total,
                    Stage = "Upload",
                });
            }

            await WriteTerminalRecordAsync(
                job.GameInstanceId,
                SyncDirection.Upload,
                SyncJobStatus.Completed,
                cancellationToken,
                processedFiles: processed,
                totalFiles: total,
                summary: $"Uploaded {processed} files.");
        }
        catch (OperationCanceledException)
        {
            await WriteTerminalRecordAsync(
                job.GameInstanceId,
                SyncDirection.Upload,
                SyncJobStatus.Cancelled,
                CancellationToken.None,
                summary: "Upload cancelled.");
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Upload job failed for game instance {GameInstanceId}.", job.GameInstanceId);
            await WriteTerminalRecordAsync(
                job.GameInstanceId,
                SyncDirection.Upload,
                SyncJobStatus.Failed,
                CancellationToken.None,
                errorMessage: exception.Message);
            throw;
        }
    }

    private async Task ExecuteDownloadAsync(SyncJob job, CancellationToken cancellationToken)
    {
        var context = await BuildContextAsync(job.GameInstanceId, cancellationToken);
        var remoteFileSets = new Dictionary<ResolvedContentRoot, IReadOnlyDictionary<string, RemoteFileEntry>>();
        foreach (var root in context.ContentRoots)
        {
            remoteFileSets[root] = await ListRemoteFilesAsync(context, root, cancellationToken);
        }

        var snapshot = CreateSnapshot(context, remoteFileSets);
        var total = remoteFileSets.Values.Sum(set => set.Count);
        var processed = 0;
        var tempFiles = new List<string>();

        try
        {
            foreach (var pair in remoteFileSets)
            {
                var root = pair.Key;
                foreach (var remoteFile in pair.Value.Values)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var targetPath = ResolveTargetLocalPath(root, remoteFile.RelativePath);
                    using var download = await context.StorageProvider.DownloadAsync(remoteFile.StoragePath, cancellationToken);
                    var tempFile = Path.GetTempFileName();
                    tempFiles.Add(tempFile);

                    await using (var tempStream = File.Create(tempFile))
                    {
                        await download.Content.CopyToAsync(tempStream, cancellationToken);
                    }

                    var directory = Path.GetDirectoryName(targetPath);
                    if (!string.IsNullOrWhiteSpace(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.Copy(tempFile, targetPath, overwrite: true);
                    processed++;
                    job.Progress?.Report(new SyncProgress
                    {
                        CurrentFileName = remoteFile.RelativePath,
                        ProcessedFiles = processed,
                        TotalFiles = total,
                        Stage = "Download",
                    });
                }
            }

            await _syncHistoryRepository.AddSnapshotAsync(snapshot, cancellationToken);
            await WriteTerminalRecordAsync(
                job.GameInstanceId,
                SyncDirection.Download,
                SyncJobStatus.Completed,
                cancellationToken,
                snapshotId: snapshot.Id,
                processedFiles: processed,
                totalFiles: total,
                summary: $"Downloaded {processed} files.");
        }
        catch (OperationCanceledException)
        {
            await WriteTerminalRecordAsync(
                job.GameInstanceId,
                SyncDirection.Download,
                SyncJobStatus.Cancelled,
                CancellationToken.None,
                snapshotId: snapshot.Id,
                processedFiles: processed,
                totalFiles: total,
                summary: "Download cancelled.");
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Download job failed for game instance {GameInstanceId}.", job.GameInstanceId);
            await WriteTerminalRecordAsync(
                job.GameInstanceId,
                SyncDirection.Download,
                SyncJobStatus.Failed,
                CancellationToken.None,
                snapshotId: snapshot.Id,
                processedFiles: processed,
                totalFiles: total,
                errorMessage: exception.Message);
            throw;
        }
        finally
        {
            CleanupTempFiles(tempFiles);
        }
    }

    private Snapshot CreateSnapshot(
        SyncExecutionContext context,
        IReadOnlyDictionary<ResolvedContentRoot, IReadOnlyDictionary<string, RemoteFileEntry>> remoteFileSets)
    {
        var snapshotDirectory = _appPathService.GetSnapshotsDirectory();
        Directory.CreateDirectory(snapshotDirectory);

        var snapshotPath = Path.Combine(snapshotDirectory, $"{context.GameInstance.Id:N}-{DateTime.UtcNow:yyyyMMddHHmmss}.zip");
        var filesToSnapshot = remoteFileSets
            .SelectMany(pair => pair.Value.Values.Select(remote => new
            {
                LocalPath = ResolveTargetLocalPath(pair.Key, remote.RelativePath),
                SnapshotEntry = BuildRemotePath(pair.Key.ContentItem.ContentId, remote.RelativePath),
            }))
            .Where(item => File.Exists(item.LocalPath))
            .DistinctBy(item => item.LocalPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        using var archive = ZipFile.Open(snapshotPath, ZipArchiveMode.Create);
        foreach (var file in filesToSnapshot)
        {
            archive.CreateEntryFromFile(file.LocalPath, file.SnapshotEntry.Replace('\\', '/'));
        }

        return new Snapshot
        {
            GameInstanceId = context.GameInstance.Id,
            ArchivePath = snapshotPath,
            FileCount = filesToSnapshot.Length,
            TotalBytes = filesToSnapshot.Sum(file => new FileInfo(file.LocalPath).Length),
        };
    }

    private async Task<SyncExecutionContext> BuildContextAsync(Guid gameInstanceId, CancellationToken cancellationToken)
    {
        var gameInstance = await _gameInstanceRepository.GetAsync(gameInstanceId, cancellationToken)
            ?? throw new InvalidOperationException($"Game instance '{gameInstanceId}' was not found.");

        var binding = (await _storageBindingRepository.ListByGameInstanceAsync(gameInstanceId, cancellationToken)).FirstOrDefault()
            ?? throw new InvalidOperationException($"No storage binding exists for game instance '{gameInstanceId}'.");

        var definition = await _manifestService.GetDefinitionAsync(gameInstance.GameId, cancellationToken)
            ?? throw new InvalidOperationException($"No manifest definition exists for game '{gameInstance.GameId}'.");

        var sourceProvider = _sourceProviders.FirstOrDefault(provider => string.Equals(provider.ProviderId, gameInstance.SourceProviderId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Source provider '{gameInstance.SourceProviderId}' is not registered.");

        var storageProvider = _storageProviders.FirstOrDefault(provider => string.Equals(provider.ProviderId, binding.StorageProviderId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Storage provider '{binding.StorageProviderId}' is not registered.");

        var resolvedContentRoots = ResolveContentRoots(definition, gameInstance, sourceProvider, binding);
        return new SyncExecutionContext(gameInstance, binding, sourceProvider, storageProvider, resolvedContentRoots);
    }

    private IReadOnlyList<ResolvedContentRoot> ResolveContentRoots(
        GameContentDefinition definition,
        GameInstance instance,
        ISourceProvider sourceProvider,
        StorageBinding binding)
    {
        var systemVariables = _systemVariableProvider.GetVariables();
        var sourceVariables = sourceProvider.ResolveVariables(instance);
        var instanceVariables = instance.Variables;
        var userOverrides = binding.Settings;
        var enabledContent = binding.ContentSelections.Count == 0
            ? definition.ContentItems.Where(item => item.DefaultEnabled)
            : definition.ContentItems.Where(item => binding.ContentSelections.Any(selection =>
                string.Equals(selection.ContentId, item.ContentId, StringComparison.OrdinalIgnoreCase) && selection.IsEnabled));

        return enabledContent
            .SelectMany(item => item.PathTemplates.Select(template => new ResolvedContentRoot(
                item,
                _pathResolver.Resolve(template, systemVariables, sourceVariables, instanceVariables, userOverrides),
                ShouldTreatAsDirectory(item, template))))
            .Where(root => !string.IsNullOrWhiteSpace(root.LocalRootPath))
            .DistinctBy(root => (root.ContentItem.ContentId, root.LocalRootPath), EqualityComparer<(string, string)>.Default)
            .ToArray();
    }

    private static bool ShouldTreatAsDirectory(ContentItem item, string template)
    {
        if (item.IncludePatterns.Count > 0 || item.ExcludePatterns.Count > 0)
        {
            return true;
        }

        return template.EndsWith('/') || template.EndsWith('\\') || !Path.HasExtension(template);
    }

    private IEnumerable<ResolvedLocalFile> EnumerateLocalFiles(ResolvedContentRoot root)
    {
        if (File.Exists(root.LocalRootPath) && !root.TreatAsDirectory)
        {
            yield return new ResolvedLocalFile(
                root.ContentItem,
                root.LocalRootPath,
                Path.GetFileName(root.LocalRootPath),
                File.GetLastWriteTimeUtc(root.LocalRootPath));
            yield break;
        }

        if (!Directory.Exists(root.LocalRootPath))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(root.LocalRootPath, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(root.LocalRootPath, file).Replace('\\', '/');
            if (!MatchesPatterns(root.ContentItem, relativePath))
            {
                continue;
            }

            yield return new ResolvedLocalFile(
                root.ContentItem,
                file,
                relativePath,
                File.GetLastWriteTimeUtc(file));
        }
    }

    private static bool MatchesPatterns(ContentItem item, string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        var includes = item.IncludePatterns.Count == 0 || item.IncludePatterns.Any(pattern => MatchPattern(pattern, normalized));
        var excludes = item.ExcludePatterns.Any(pattern => MatchPattern(pattern, normalized));
        return includes && !excludes;
    }

    private static bool MatchPattern(string pattern, string relativePath)
    {
        var normalizedPattern = pattern.Replace('\\', '/');
        return FileSystemName.MatchesSimpleExpression(normalizedPattern, relativePath, ignoreCase: true);
    }

    private async Task<IReadOnlyDictionary<string, RemoteFileEntry>> ListRemoteFilesAsync(
        SyncExecutionContext context,
        ResolvedContentRoot root,
        CancellationToken cancellationToken)
    {
        var namespacePath = BuildRemotePath(context.Binding.RemoteNamespace, root.ContentItem.ContentId);
        var entries = await context.StorageProvider.ListAsync(namespacePath, cancellationToken);
        return entries
            .Where(entry => !entry.IsDirectory)
            .Select(entry => CreateRemoteFileEntry(namespacePath, entry))
            .Where(entry => entry is not null)
            .ToDictionary(entry => entry!.RelativePath, entry => entry!, StringComparer.OrdinalIgnoreCase);
    }

    private static RemoteFileEntry? CreateRemoteFileEntry(string namespacePath, RemoteEntry entry)
    {
        var normalizedNamespace = namespacePath.Trim('/');
        var normalizedPath = entry.Path.Trim('/');
        string relativePath;

        if (string.Equals(normalizedPath, normalizedNamespace, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (normalizedPath.StartsWith(normalizedNamespace + '/', StringComparison.OrdinalIgnoreCase))
        {
            relativePath = normalizedPath[(normalizedNamespace.Length + 1)..];
        }
        else
        {
            relativePath = normalizedPath;
        }

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        return new RemoteFileEntry(relativePath, normalizedPath, entry.Hash, entry.LastModifiedUtc, entry.Size);
    }

    private static string ResolveTargetLocalPath(ResolvedContentRoot root, string relativePath)
    {
        if (!root.TreatAsDirectory && Path.HasExtension(root.LocalRootPath))
        {
            return root.LocalRootPath;
        }

        return Path.Combine(root.LocalRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static SyncChangeKind DetermineChangeKind(ResolvedLocalFile? localFile, RemoteFileEntry? remoteFile, string? localHash)
    {
        if (localFile is not null && remoteFile is not null)
        {
            if (!string.IsNullOrWhiteSpace(remoteFile.Hash) && string.Equals(localHash, remoteFile.Hash, StringComparison.OrdinalIgnoreCase))
            {
                return SyncChangeKind.Unchanged;
            }

            if (remoteFile.Hash is null && remoteFile.LastModifiedUtc is not null)
            {
                var localTime = localFile.LastModifiedUtc;
                var remoteTime = remoteFile.LastModifiedUtc.Value.UtcDateTime;
                if (Math.Abs((localTime - remoteTime).TotalSeconds) < 1)
                {
                    return SyncChangeKind.Unchanged;
                }
            }

            return SyncChangeKind.Conflict;
        }

        if (localFile is not null)
        {
            return SyncChangeKind.AddedLocally;
        }

        if (remoteFile is not null)
        {
            return SyncChangeKind.AddedRemotely;
        }

        return SyncChangeKind.Unchanged;
    }

    private static async Task<string> ComputeFileHashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private static string BuildRemotePath(params string[] segments)
    {
        return string.Join('/', segments.Where(segment => !string.IsNullOrWhiteSpace(segment)).Select(segment => segment.Trim('/')));
    }

    private static void CleanupTempFiles(IEnumerable<string> tempFiles)
    {
        foreach (var tempFile in tempFiles)
        {
            try
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
            catch
            {
            }
        }
    }

    private Task WriteTerminalRecordAsync(
        Guid gameInstanceId,
        SyncDirection direction,
        SyncJobStatus status,
        CancellationToken cancellationToken,
        Guid? snapshotId = null,
        int processedFiles = 0,
        int totalFiles = 0,
        string? summary = null,
        string? errorMessage = null)
    {
        return _syncHistoryRepository.AddRecordAsync(new SyncRecord
        {
            GameInstanceId = gameInstanceId,
            SnapshotId = snapshotId,
            Direction = direction,
            Status = status,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            ProcessedFiles = processedFiles,
            TotalFiles = totalFiles,
            Summary = summary,
            ErrorMessage = errorMessage,
        }, cancellationToken);
    }

    private sealed record SyncExecutionContext(
        GameInstance GameInstance,
        StorageBinding Binding,
        ISourceProvider SourceProvider,
        IStorageProvider StorageProvider,
        IReadOnlyList<ResolvedContentRoot> ContentRoots);

    private sealed record ResolvedContentRoot(ContentItem ContentItem, string LocalRootPath, bool TreatAsDirectory);

    private sealed record ResolvedLocalFile(ContentItem ContentItem, string AbsolutePath, string RelativePath, DateTime LastModifiedUtc);

    private sealed record RemoteFileEntry(string RelativePath, string StoragePath, string? Hash, DateTimeOffset? LastModifiedUtc, long Size);
}
