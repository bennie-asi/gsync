using System.Security.Cryptography;
using System.Text.Json;
using System.IO.Compression;
using GSYNC.Core.Abstractions.Data;
using GSYNC.Core.Models;
using GSYNC.Core.Utilities;
using static GSYNC.Core.Services.Sync.SyncFileService;

namespace GSYNC.Core.Services.Sync;

internal sealed class SyncSnapshotService(IAppPathService paths, ISyncHistoryRepository history)
{
    private readonly IAppPathService _appPathService = paths;
    private readonly ISyncHistoryRepository _syncHistoryRepository = history;
    internal async Task<Snapshot?> BackupRemoteFilesAsync(SyncExecutionContext context, IReadOnlyList<ResolvedLocalFile> files, CancellationToken cancellationToken)
    {
        if (files.Count == 0) return null;
        var staged = new List<AtomicFile.PreparedFile>();
        var candidates = new List<SnapshotCandidate>();
        try
        {
            foreach (var root in context.ContentRoots)
            {
                var uploads = files.Where(file => file.StorageKey == root.StorageKey).ToArray();
                if (uploads.Length == 0) continue;
                var existing = await ListRemoteFilesAsync(context, root, cancellationToken, checkLegacyLayout: false);
                foreach (var file in uploads)
                {
                    if (!existing.TryGetValue(file.RelativePath, out var remote)) continue;
                    using var download = await context.StorageProvider.DownloadAsync(remote.StoragePath, cancellationToken);
                    var prepared = await AtomicFile.PrepareAsync(download.Content,
                        Path.Combine(_appPathService.GetSnapshotsDirectory(), Guid.NewGuid().ToString("N")), cancellationToken);
                    staged.Add(prepared);
                    await VerifyPreparedDownloadAsync(prepared, remote, cancellationToken);
                    candidates.Add(new SnapshotCandidate(prepared.TemporaryPath!, BuildRemotePath(root.StorageKey, file.RelativePath), root.TreatAsDirectory));
                }
            }
            if (candidates.Count == 0) return null;
            var snapshot = CreateSnapshotArchive(context.GameInstance.Id, candidates);
            await _syncHistoryRepository.AddSnapshotAsync(snapshot, cancellationToken);
            return snapshot;
        }
        finally
        {
            foreach (var file in staged) file.Dispose();
        }
    }

    internal Snapshot CreateSnapshot(
        SyncExecutionContext context,
        IReadOnlyDictionary<ResolvedContentRoot, IReadOnlyDictionary<string, RemoteFileEntry>> remoteFileSets)
    {
        var filesToSnapshot = remoteFileSets
            .SelectMany(pair => pair.Value.Values.Select(remote => new SnapshotCandidate(
                ResolveTargetLocalPath(pair.Key, remote.RelativePath),
                BuildRemotePath(pair.Key.StorageKey, remote.RelativePath), pair.Key.TreatAsDirectory)))
            .DistinctBy(candidate => candidate.LocalPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return CreateSnapshotArchive(context.GameInstance.Id, filesToSnapshot);
    }

    internal Snapshot CreateSnapshotForRelativePaths(
        SyncExecutionContext context,
        IReadOnlyDictionary<ResolvedContentRoot, IReadOnlyDictionary<string, RemoteFileEntry>> remoteFileSets,
        IEnumerable<string> relativePaths)
    {
        var namespacePrefix = context.Binding.RemoteNamespace.Trim('/');
        var requested = new HashSet<string>(
            relativePaths.Select(path => NormalizePlanPath(namespacePrefix, path)),
            StringComparer.OrdinalIgnoreCase);
        var filesToSnapshot = remoteFileSets
            .SelectMany(pair => pair.Value.Values
                .Where(remote => requested.Contains(BuildRemotePath(pair.Key.StorageKey, remote.RelativePath)))
                .Select(remote => new SnapshotCandidate(
                    ResolveTargetLocalPath(pair.Key, remote.RelativePath),
                    BuildRemotePath(pair.Key.StorageKey, remote.RelativePath), pair.Key.TreatAsDirectory)))
            .DistinctBy(candidate => candidate.LocalPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return CreateSnapshotArchive(context.GameInstance.Id, filesToSnapshot);
    }

    internal Snapshot CreateSnapshotArchive(Guid gameInstanceId, IReadOnlyList<SnapshotCandidate> filesToSnapshot)
    {
        var directory = _appPathService.GetSnapshotsDirectory();
        Directory.CreateDirectory(directory);
        var snapshotPath = Path.Combine(directory, $"{gameInstanceId:N}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.zip");
        using var prepared = new AtomicFile.PreparedFile(snapshotPath, Path.Combine(directory, AtomicFile.TemporaryName()));
        var files = new List<SnapshotFile>();
        long bytes = 0;
        using (var archive = ZipFile.Open(prepared.TemporaryPath!, ZipArchiveMode.Create))
        {
            foreach (var file in filesToSnapshot)
            {
                if (Directory.Exists(file.LocalPath)) throw new IOException($"Snapshot target '{file.LocalPath}' is a directory.");
                if (!File.Exists(file.LocalPath))
                {
                    files.Add(new SnapshotFile(file.SnapshotEntry, false, null, file.RootIsDirectory));
                    continue;
                }
                using var source = File.OpenRead(file.LocalPath);
                var hash = Convert.ToHexString(SHA256.HashData(source));
                source.Position = 0;
                using var target = archive.CreateEntry(file.SnapshotEntry.Replace('\\', '/')).Open();
                source.CopyTo(target);
                bytes += source.Length;
                files.Add(new SnapshotFile(file.SnapshotEntry, true, hash, file.RootIsDirectory));
            }
            using var metadata = archive.CreateEntry(Snapshot.MetadataEntryName).Open();
            JsonSerializer.Serialize(metadata, new SnapshotManifest(1, files.ToArray()));
        }
        AtomicFile.Apply([prepared]);
        return new Snapshot { GameInstanceId = gameInstanceId, ArchivePath = snapshotPath,
            FileCount = files.Count(file => file.Existed), TotalBytes = bytes };
    }
    internal async Task RestoreAsync(Snapshot snapshot, SyncExecutionContext context, SyncOperationState state, CancellationToken cancellationToken)
    {
        var preparedFiles = new List<AtomicFile.PreparedFile>();
        try
        {
            using var archive = ZipFile.OpenRead(snapshot.ArchivePath);
            var data = archive.Entries.Where(entry => !string.IsNullOrEmpty(entry.Name) && entry.FullName != Snapshot.MetadataEntryName)
                .ToDictionary(entry => entry.FullName.Replace('\\', '/'), StringComparer.OrdinalIgnoreCase);
            SnapshotFile[] files;
            if (archive.GetEntry(Snapshot.MetadataEntryName) is { } metadata)
            {
                using var stream = metadata.Open();
                var manifest = await JsonSerializer.DeserializeAsync<SnapshotManifest>(stream, cancellationToken: cancellationToken)
                    ?? throw new InvalidDataException("Snapshot metadata is empty.");
                if (manifest.Version != 1 || manifest.Files is null) throw new InvalidDataException("Unsupported snapshot format.");
                files = manifest.Files;
                if (files.Count(file => file.Existed) != data.Count || files.Select(file => file.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != files.Length ||
                    files.Any(file => data.ContainsKey(file.Path) != file.Existed))
                    throw new InvalidDataException("Snapshot files do not match its metadata.");
            }
            else files = data.Keys.Select(path => new SnapshotFile(path, true, null)).ToArray();

            state.Total = files.Length;
            var roots = context.ContentRoots.OrderByDescending(root => root.StorageKey.Length).ToArray();
            var candidates = new List<SnapshotCandidate>();
            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var root = roots.FirstOrDefault(candidate => file.Path.StartsWith(candidate.StorageKey + '/', StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidDataException($"Snapshot entry '{file.Path}' cannot be mapped to a current content path.");
                if (file.RootIsDirectory is { } isDirectory) root.UseRemoteKind(isDirectory);
                var target = ResolveTargetLocalPath(root, file.Path[(root.StorageKey.Length + 1)..]);
                if (!targets.Add(SafePath.GetPhysicalPath(target))) throw new InvalidDataException("Snapshot entries map to the same local file.");
                if (Directory.Exists(target)) throw new InvalidDataException($"Snapshot target '{target}' is a directory.");
                candidates.Add(new SnapshotCandidate(target, file.Path, root.TreatAsDirectory));
                var originalHash = RecoverableFileTransaction.Hash(target);
                if (file.Existed)
                {
                    using var stream = data[file.Path].Open();
                    var prepared = await AtomicFile.PrepareAsync(stream, target, cancellationToken);
                    prepared.VerifyOriginal = true;
                    prepared.ExpectedOriginalHash = originalHash;
                    preparedFiles.Add(prepared);
                    if (file.Hash is not null && !string.Equals(file.Hash, await ComputeFileHashAsync(prepared.TemporaryPath!, cancellationToken), StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException($"Snapshot file '{file.Path}' failed its integrity check.");
                }
                else preparedFiles.Add(new AtomicFile.PreparedFile(target, null) { VerifyOriginal = true, ExpectedOriginalHash = originalHash });
            }

            // All archive entries have been read and checked before changing any save.
            state.Snapshot = CreateSnapshotArchive(snapshot.GameInstanceId, candidates);
            await _syncHistoryRepository.AddSnapshotAsync(state.Snapshot, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var file in files)
            {
                var root = roots.First(candidate => file.Path.StartsWith(candidate.StorageKey + '/', StringComparison.OrdinalIgnoreCase));
                ResolveTargetLocalPath(root, file.Path[(root.StorageKey.Length + 1)..]);
            }
            AtomicFile.Apply(preparedFiles, Path.Combine(_appPathService.GetAppDataRoot(), "transactions"));
            state.Processed = state.Total;
            state.Summary = $"Restored {state.Processed} paths from snapshot {snapshot.Id}.";
        }
        finally { foreach (var prepared in preparedFiles) prepared.Dispose(); }
    }
}
