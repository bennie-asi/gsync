using GSYNC.Core.Models;

namespace GSYNC.Core.Abstractions.Sync;

public interface ISyncQueue
{
    SyncJob? ActiveJob { get; }

    int QueueDepth { get; }

    /// <summary>
    /// Pending (not yet started) jobs in enqueue order.
    /// </summary>
    IReadOnlyList<SyncJob> PendingJobs { get; }

    /// <summary>
    /// Latest progress reported by the active job, if any.
    /// </summary>
    SyncProgress? ActiveProgress { get; }

    /// <summary>
    /// Raised whenever the queue contents, active job, or active progress change.
    /// </summary>
    event EventHandler? QueueChanged;

    ValueTask QueueAsync(SyncJob syncJob, CancellationToken cancellationToken);

    IAsyncEnumerable<SyncJob> ReadAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Marks the job as the active job and transitions it to Running.
    /// Returns <c>false</c> if the job was removed before it could start, in which case
    /// the caller should skip it without executing or writing a terminal record.
    /// </summary>
    bool TryBeginJob(SyncJob syncJob);

    void Complete();

    /// <summary>
    /// Records the latest progress for the active job and notifies observers.
    /// </summary>
    void ReportProgress(SyncJob syncJob, SyncProgress progress);

    /// <summary>
    /// Removes a pending (not yet started) job so it will never execute.
    /// Returns <c>false</c> if the job is the active job or is not pending.
    /// </summary>
    bool Remove(Guid jobId);

    /// <summary>
    /// Requests cancellation of the active job via its queue-owned token.
    /// Returns <c>false</c> if the given job is not currently active.
    /// </summary>
    bool CancelActive(Guid jobId);
}
