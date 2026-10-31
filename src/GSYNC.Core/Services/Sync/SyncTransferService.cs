using System.Security.Cryptography;
using System.Text.Json;
using GSYNC.Core.Abstractions.Data;
using GSYNC.Core.Abstractions.Sync;
using GSYNC.Core.Models;
using GSYNC.Core.Utilities;
using static GSYNC.Core.Services.Sync.SyncFileService;

namespace GSYNC.Core.Services.Sync;

internal sealed class SyncTransferService(ISyncQueue queue, ISyncHistoryRepository history, SyncSnapshotService snapshots, Func<bool> autoSnapshot, string transactionDirectory,
    Func<Guid, CancellationToken, Task<SyncExecutionContext>> freshContext)
{
    private readonly ISyncQueue _syncQueue = queue;
    private readonly ISyncHistoryRepository _syncHistoryRepository = history;
    private readonly SyncSnapshotService _snapshots = snapshots;
    private readonly Func<bool> _autoSnapshotBeforeDownloadEnabled = autoSnapshot;
    internal async Task UploadAsync(SyncJob job, SyncExecutionContext context, SyncOperationState state, CancellationToken cancellationToken)
    {
        var uploadFiles = context.ContentRoots.SelectMany(EnumerateLocalFiles).ToArray();
        if (job.ProtectRemoteChanges)
        {
            var comparison = await CompareContextAsync(context, cancellationToken);
            if (comparison.Entries.Any(entry => entry.ChangeKind is SyncChangeKind.Conflict or SyncChangeKind.AddedRemotely or SyncChangeKind.UpdatedRemotely))
                throw new InvalidOperationException("Remote saves differ or contain additional files. Compare the versions and choose which to keep before uploading.");
        }
        if (uploadFiles.Length == 0)
        {
            throw new InvalidOperationException("No files matched the enabled content paths. Check the paths, variables and filters before uploading.");
        }
        var missingSave = context.ContentRoots.Select(root => root.ContentItem).DistinctBy(item => item.ContentId)
            .FirstOrDefault(item => item.Category == ContentCategory.Save && !uploadFiles.Any(file => file.ContentItem.ContentId == item.ContentId));
        if (missingSave is not null)
            throw new InvalidOperationException($"No save files matched enabled content '{missingSave.ContentId}'. Check its paths before uploading.");
        state.Total = uploadFiles.Length;
        if (uploadFiles.Select(file => BuildRemotePath(file.StorageKey, file.RelativePath)).Distinct(StringComparer.OrdinalIgnoreCase).Count() != state.Total)
            throw new InvalidDataException("Multiple local files map to the same remote path.");
        using var sources = await SyncUploadSourceSet.CaptureAsync(uploadFiles, null, cancellationToken);
        state.Snapshot = await _snapshots.BackupRemoteFilesAsync(context, uploadFiles, cancellationToken);
        await WriteRootDescriptorsAsync(context, uploadFiles, cancellationToken);

        var stagedUploads = 0;
        foreach (var file in uploadFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var stream = sources.OpenRead(file.AbsolutePath);
            var remotePath = BuildRemotePath(context.Binding.RemoteNamespace, file.StorageKey, file.RelativePath);
            await context.StorageProvider.UploadAsync(remotePath, stream, cancellationToken);
            stagedUploads++;
            ReportProgress(job, new SyncProgress
            {
                CurrentFileName = file.RelativePath,
                ProcessedFiles = stagedUploads,
                TotalFiles = state.Total,
                Stage = "Upload",
            });
        }

        await ((RemoteSaveSession)context.StorageProvider).CommitAsync(cancellationToken);
        state.Processed = stagedUploads;
        state.Summary = $"Uploaded {state.Processed} files.";
    }
    internal async Task DownloadAsync(SyncJob job, SyncExecutionContext context, SyncOperationState state, CancellationToken cancellationToken)
    {
        var preparedFiles = new List<AtomicFile.PreparedFile>();
        try
        {
            var remoteFileSets = new Dictionary<ResolvedContentRoot, IReadOnlyDictionary<string, RemoteFileEntry>>();
            foreach (var root in context.ContentRoots)
            {
                remoteFileSets[root] = await ListRemoteFilesAsync(context, root, cancellationToken);
            }

            state.Total = remoteFileSets.Values.Sum(set => set.Count);
            if (state.Total == 0)
                throw new InvalidOperationException("No remote files matched the enabled content. Check the target and paths before downloading.");
            var missingSave = context.ContentRoots.Select(root => root.ContentItem).DistinctBy(item => item.ContentId)
                .FirstOrDefault(item => item.Category == ContentCategory.Save &&
                    !remoteFileSets.Any(pair => pair.Key.ContentItem.ContentId == item.ContentId && pair.Value.Count > 0));
            if (missingSave is not null)
                throw new InvalidOperationException($"No remote save files matched enabled content '{missingSave.ContentId}'. Compare the target and paths before downloading.");
            ValidateDownloadTargets(remoteFileSets);
            var originals = remoteFileSets.SelectMany(pair => pair.Value.Values.Select(remote => ResolveTargetLocalPath(pair.Key, remote.RelativePath)))
                .ToDictionary(path => path, RecoverableFileTransaction.Hash, StringComparer.OrdinalIgnoreCase);
            if (_autoSnapshotBeforeDownloadEnabled())
            {
                var backup = _snapshots.CreateSnapshot(context, remoteFileSets);
                // Persist the recovery point before any file can be overwritten or cancelled.
                await _syncHistoryRepository.AddSnapshotAsync(backup, cancellationToken);
                state.Snapshot = backup;
            }

            foreach (var pair in remoteFileSets)
            {
                var root = pair.Key;
                foreach (var remoteFile in pair.Value.Values)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var targetPath = ResolveTargetLocalPath(root, remoteFile.RelativePath);
                    using var download = await context.StorageProvider.DownloadAsync(remoteFile.StoragePath, cancellationToken);
                    var prepared = await AtomicFile.PrepareAsync(download.Content, targetPath, cancellationToken);
                    prepared.VerifyOriginal = true;
                    prepared.ExpectedOriginalHash = originals[targetPath];
                    preparedFiles.Add(prepared);
                    await VerifyPreparedDownloadAsync(prepared, remoteFile, cancellationToken);
                    ReportProgress(job, new SyncProgress
                    {
                        CurrentFileName = remoteFile.RelativePath,
                        ProcessedFiles = preparedFiles.Count,
                        TotalFiles = state.Total,
                        Stage = "Download",
                    });
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            ValidateDownloadTargets(remoteFileSets);
            AtomicFile.Apply(preparedFiles, transactionDirectory);
            state.Processed = preparedFiles.Count;
            state.Summary = $"Downloaded {state.Processed} files.";
        }
        finally { foreach (var prepared in preparedFiles) prepared.Dispose(); }
    }
    internal async Task ResolveConflictsAsync(SyncJob job, SyncExecutionContext context, SyncOperationState state, CancellationToken cancellationToken)
    {
        if (job.ConflictResolutionPlan is null) throw new InvalidOperationException("A conflict plan is required.");
        if (job.ConflictResolutionPlan.ExpectedConfigurationFingerprint is { } expectedFingerprint &&
            !string.Equals(expectedFingerprint, context.ConfigurationFingerprint, StringComparison.Ordinal))
            throw new InvalidOperationException("The sync destination changed after comparison. Compare the latest versions before resolving conflicts.");
        var preparedFiles = new List<AtomicFile.PreparedFile>();
        try
        {
            var remoteFileSets = new Dictionary<ResolvedContentRoot, IReadOnlyDictionary<string, RemoteFileEntry>>();
            foreach (var root in context.ContentRoots)
            {
                remoteFileSets[root] = await ListRemoteFilesAsync(context, root, cancellationToken);
            }

            var decisions = job.ConflictResolutionPlan.Decisions
                .Where(decision => decision.Action != ConflictResolutionAction.Undecided && decision.Action != ConflictResolutionAction.Skip)
                .ToArray();
            state.Total = decisions.Length;
            if (job.ConflictResolutionPlan.GameInstanceId != job.GameInstanceId ||
                decisions.Any(decision => !Enum.IsDefined(decision.Action)) ||
                decisions.Select(decision => NormalizePlanPath(context.Binding.RemoteNamespace.Trim('/'), decision.RelativePath))
                    .Distinct(StringComparer.OrdinalIgnoreCase).Count() != decisions.Length)
                throw new InvalidDataException("The conflict plan has invalid or duplicate decisions.");

            if (job.ConflictResolutionPlan.ExpectedComparison is { } initialExpected)
            {
                // An automatic pull must not modify another logical save that maps to the same local file.
                ValidateDownloadTargets(remoteFileSets);
                await VerifyFullComparisonAsync(context, initialExpected, cancellationToken);
            }

            var localUploads = new List<ResolvedLocalFile>();
            var uploadHashes = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var originals = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var downloadTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var decision in decisions)
            {
                if (!TryResolvePlanPath(context, remoteFileSets, decision.RelativePath, out var root, out var relative, out var remote))
                    throw new InvalidDataException($"Conflict path '{decision.RelativePath}' is not part of the selected content.");
                if (decision.VerifyComparedVersions)
                {
                    var localPath = ResolveTargetLocalPath(root, relative);
                    var localHash = File.Exists(localPath) ? await ComputeFileHashAsync(localPath, cancellationToken) : null;
                    var remoteHash = remote?.Hash;
                    if (remote is not null && string.IsNullOrWhiteSpace(remoteHash))
                    {
                        using var download = await context.StorageProvider.DownloadAsync(remote.StoragePath, cancellationToken);
                        remoteHash = Convert.ToHexString(await SHA256.HashDataAsync(download.Content, cancellationToken));
                    }
                    if (!string.Equals(localHash, decision.ExpectedLocalHash, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(remoteHash, decision.ExpectedRemoteHash, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("A save changed after comparison. Compare the latest versions before resolving this conflict.");
                }
                if (decision.Action == ConflictResolutionAction.KeepLocal)
                {
                    var file = EnumerateLocalFiles(root).FirstOrDefault(file => string.Equals(file.RelativePath, relative, StringComparison.OrdinalIgnoreCase))
                        ?? throw new InvalidDataException($"Local conflict file '{relative}' no longer exists.");
                    localUploads.Add(file);
                    if (decision.VerifyComparedVersions) uploadHashes[file.AbsolutePath] = decision.ExpectedLocalHash;
                }
                else if (remote is null || !downloadTargets.Add(SafePath.GetPhysicalPath(ResolveTargetLocalPath(root, relative))))
                    throw new InvalidDataException($"Remote conflict file '{relative}' is missing or has a duplicate local target.");
                else
                {
                    var path = ResolveTargetLocalPath(root, relative);
                    originals[path] = decision.VerifyComparedVersions ? decision.ExpectedLocalHash : RecoverableFileTransaction.Hash(path);
                }
            }
            using var sources = await SyncUploadSourceSet.CaptureAsync(localUploads, uploadHashes, cancellationToken);
            state.Snapshot = await _snapshots.BackupRemoteFilesAsync(context, localUploads, cancellationToken);
            await WriteRootDescriptorsAsync(context, localUploads, cancellationToken);
            var remoteOverwriteTargets = decisions
                .Where(decision => decision.Action == ConflictResolutionAction.KeepRemote)
                .ToArray();

            if (remoteOverwriteTargets.Length > 0)
            {
                state.Snapshot = _snapshots.CreateSnapshotForRelativePaths(context, remoteFileSets, remoteOverwriteTargets.Select(decision => decision.RelativePath));
                await _syncHistoryRepository.AddSnapshotAsync(state.Snapshot, cancellationToken);
            }

            foreach (var decision in decisions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!TryResolvePlanPath(context, remoteFileSets, decision.RelativePath, out var root, out var relativePath, out var remoteFile))
                {
                    throw new InvalidDataException($"Conflict path '{decision.RelativePath}' is no longer available.");
                }

                switch (decision.Action)
                {
                    case ConflictResolutionAction.KeepLocal:
                    {
                        var localFile = EnumerateLocalFiles(root).FirstOrDefault(file => string.Equals(file.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase));
                        if (localFile is null)
                        {
                            throw new IOException($"Local conflict file '{relativePath}' no longer exists.");
                        }

                        await using var stream = sources.OpenRead(localFile.AbsolutePath);
                        var remotePath = BuildRemotePath(context.Binding.RemoteNamespace, root.StorageKey, relativePath);
                        await context.StorageProvider.UploadAsync(remotePath, stream, cancellationToken);
                        break;
                    }
                    case ConflictResolutionAction.KeepRemote when remoteFile is not null:
                    {
                        var localPath = ResolveTargetLocalPath(root, relativePath);
                        using var download = await context.StorageProvider.DownloadAsync(remoteFile.StoragePath, cancellationToken);
                        var prepared = await AtomicFile.PrepareAsync(download.Content, localPath, cancellationToken);
                        prepared.VerifyOriginal = true;
                        prepared.ExpectedOriginalHash = originals[localPath];
                        preparedFiles.Add(prepared);
                        await VerifyPreparedDownloadAsync(prepared, remoteFile, cancellationToken);
                        if (decision.VerifyComparedVersions && !string.Equals(decision.ExpectedRemoteHash,
                            await ComputeFileHashAsync(prepared.TemporaryPath!, cancellationToken), StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("The downloaded save changed after comparison.");
                        break;
                    }
                    default:
                        continue;
                }

                if (decision.Action == ConflictResolutionAction.KeepLocal) state.Processed++;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (job.ConflictResolutionPlan.ExpectedComparison is { } finalExpected)
            {
                var currentContext = await freshContext(job.GameInstanceId, cancellationToken);
                await VerifyFullComparisonAsync(currentContext, finalExpected, cancellationToken);
            }
            await ((RemoteSaveSession)context.StorageProvider).CommitAsync(cancellationToken);
            AtomicFile.Apply(preparedFiles, transactionDirectory);
            state.Processed += preparedFiles.Count;
            state.Summary = $"Resolved {state.Processed} conflict file decisions.";
        }
        finally { foreach (var prepared in preparedFiles) prepared.Dispose(); }
    }

    private static async Task VerifyFullComparisonAsync(SyncExecutionContext context, SyncComparison expected, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(expected.ConfigurationFingerprint) ||
            !string.Equals(expected.ConfigurationFingerprint, context.ConfigurationFingerprint, StringComparison.Ordinal) ||
            expected.Entries.Count == 0 || expected.Entries.Select(entry => entry.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count() != expected.Entries.Count)
            throw new InvalidOperationException("The save configuration changed after comparison. Review the game before syncing.");
        var current = await CompareContextAsync(context, cancellationToken);
        var expectedByPath = expected.Entries.ToDictionary(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase);
        if (current.Entries.Count != expectedByPath.Count ||
            current.Entries.Select(entry => entry.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count() != current.Entries.Count ||
            current.Entries.Any(entry =>
            !expectedByPath.TryGetValue(entry.RelativePath, out var previous) ||
            !string.Equals(entry.LocalHash, previous.LocalHash, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(entry.RemoteHash, previous.RemoteHash, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("The save changed after comparison. Review the latest versions before syncing.");
    }
    private static async Task WriteRootDescriptorsAsync(SyncExecutionContext context, IReadOnlyList<ResolvedLocalFile> files, CancellationToken cancellationToken)
    {
        foreach (var root in context.ContentRoots.Where(root => files.Any(file => file.StorageKey == root.StorageKey)))
        {
            using var descriptor = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(new RootDescriptor(1, root.TreatAsDirectory)));
            await context.StorageProvider.UploadAsync(RootMetadataPath(context.Binding, root), descriptor, cancellationToken);
        }
    }

    private void ReportProgress(SyncJob job, SyncProgress progress)
    {
        _syncQueue.ReportProgress(job, progress);
        job.Progress?.Report(progress);
    }

}
