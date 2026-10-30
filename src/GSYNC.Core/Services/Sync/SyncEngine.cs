using System.Collections.Concurrent;
using GSYNC.Core.Abstractions;
using GSYNC.Core.Abstractions.Data;
using GSYNC.Core.Abstractions.Manifest;
using GSYNC.Core.Abstractions.Sync;
using GSYNC.Core.Models;
using GSYNC.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace GSYNC.Core.Services.Sync;

/// <summary>Coordinates job lifetime, per-game exclusion, and the focused sync services.</summary>
public sealed class SyncEngine : ISyncEngine
{
    private readonly ISyncQueue _queue;
    private readonly ISyncHistoryRepository _history;
    private readonly SyncContextFactory _contexts;
    private readonly SyncTransferService _transfers;
    private readonly SyncSnapshotService _snapshots;
    private readonly SyncOperationRunner _operations;
    private readonly ApplicationTaskHost? _applicationTasks;
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _gameLocks = new();

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
        ILogger<SyncEngine> logger,
        Func<bool>? autoSnapshotBeforeDownloadEnabled = null,
        Func<StorageBinding, IReadOnlyDictionary<string, string>>? storageConfigurationResolver = null,
        Func<IReadOnlyDictionary<string, string>>? userVariablesProvider = null,
        ApplicationTaskHost? applicationTasks = null)
    {
        _queue = syncQueue;
        _history = syncHistoryRepository;
        _applicationTasks = applicationTasks;
        var files = new SyncFileService(pathResolver, systemVariableProvider, userVariablesProvider);
        _contexts = new SyncContextFactory(gameInstanceRepository, storageBindingRepository, manifestService,
            sourceProviders, storageProviders, files, storageConfigurationResolver);
        _snapshots = new SyncSnapshotService(appPathService, syncHistoryRepository);
        _transfers = new SyncTransferService(syncQueue, syncHistoryRepository, _snapshots, autoSnapshotBeforeDownloadEnabled ?? (() => true), Path.Combine(appPathService.GetAppDataRoot(), "transactions"));
        _operations = new SyncOperationRunner(syncHistoryRepository, logger);
    }

    public Task QueueAsync(SyncJob syncJob, CancellationToken cancellationToken)
    {
        if (_applicationTasks?.IsStopping == true) throw new InvalidOperationException("The application is stopping.");
        return _queue.QueueAsync(syncJob, cancellationToken).AsTask();
    }

    public Task<SyncComparison> CompareAsync(SyncJob syncJob, CancellationToken cancellationToken) => TrackAsync(async token =>
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, syncJob.CancellationToken);
        using var lease = await LockGameAsync(syncJob.GameInstanceId, linked.Token);
        return await SyncFileService.CompareContextAsync(await _contexts.BuildAsync(syncJob.GameInstanceId, linked.Token), linked.Token);
    }, cancellationToken);

    public Task ResolveConflictsAsync(ConflictResolutionPlan plan, CancellationToken cancellationToken) => TrackAsync(token =>
        ExecuteAsync(new SyncJob { GameInstanceId = plan.GameInstanceId, Direction = SyncDirection.ResolveConflict, ConflictResolutionPlan = plan }, token), cancellationToken);

    public Task RestoreSnapshotAsync(Guid snapshotId, CancellationToken cancellationToken) => TrackAsync(async token =>
    {
        var snapshot = await _history.GetSnapshotAsync(snapshotId, token)
            ?? throw new InvalidOperationException($"Snapshot '{snapshotId}' was not found.");
        await _operations.RunAsync(snapshot.GameInstanceId, SyncDirection.Download, async state =>
        {
            using var lease = await LockGameAsync(snapshot.GameInstanceId, token);
            var context = await _contexts.BuildAsync(snapshot.GameInstanceId, token, configureStorage: false);
            await _snapshots.RestoreAsync(snapshot, context, state, token);
        });
    }, cancellationToken);

    public async Task ProcessQueuedJobsAsync(CancellationToken cancellationToken)
    {
        await foreach (var job in _queue.ReadAllAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_queue.TryBeginJob(job)) continue;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, job.CancellationToken, job.QueueCancellationToken);
            try
            {
                await ExecuteAsync(job, linked.Token);
                job.Status = SyncJobStatus.Completed;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                job.Status = SyncJobStatus.Cancelled;
                throw;
            }
            catch (OperationCanceledException) { job.Status = SyncJobStatus.Cancelled; }
            catch (Exception) { job.Status = SyncJobStatus.Failed; }
            finally { _queue.Complete(); }
        }
    }

    private Task ExecuteAsync(SyncJob job, CancellationToken token) => _operations.RunAsync(job.GameInstanceId, job.Direction, async state =>
    {
        using var lease = await LockGameAsync(job.GameInstanceId, token);
        var context = await _contexts.BuildAsync(job.GameInstanceId, token);
        switch (job.Direction)
        {
            case SyncDirection.Upload: await _transfers.UploadAsync(job, context, state, token); break;
            case SyncDirection.Download: await _transfers.DownloadAsync(job, context, state, token); break;
            case SyncDirection.ResolveConflict: await _transfers.ResolveConflictsAsync(job, context, state, token); break;
            case SyncDirection.Compare:
                var comparison = await SyncFileService.CompareContextAsync(context, token);
                state.Total = state.Processed = comparison.Entries.Count;
                state.Summary = $"Compared {state.Total} files.";
                break;
            default: throw new ArgumentOutOfRangeException(nameof(job.Direction));
        }
    });

    private Task TrackAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
        => TrackAsync(async token => { await operation(token); return true; }, cancellationToken);

    private Task<T> TrackAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        async Task<T> InvokeAsync(CancellationToken lifetimeToken)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetimeToken);
            return await Task.Run(() => operation(linked.Token), linked.Token);
        }
        return _applicationTasks is null ? InvokeAsync(CancellationToken.None) : _applicationTasks.RunAsync(InvokeAsync);
    }

    private async Task<IDisposable> LockGameAsync(Guid gameId, CancellationToken cancellationToken)
    {
        var gate = _gameLocks.GetOrAdd(gameId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        return new GameLease(gate);
    }
    private sealed class GameLease(SemaphoreSlim gate) : IDisposable { public void Dispose() => gate.Release(); }
}
