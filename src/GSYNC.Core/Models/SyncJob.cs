namespace GSYNC.Core.Models;

public sealed class SyncJob
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required Guid GameInstanceId { get; init; }

    public required SyncDirection Direction { get; init; }

    /// <summary>
    /// Human-readable label (typically the game/instance title) for queue display.
    /// Supplied by the caller so the queue does not have to look it up.
    /// </summary>
    public string? DisplayName { get; init; }

    public ConflictResolutionPlan? ConflictResolutionPlan { get; init; }

    public CancellationToken CancellationToken { get; init; }

    public IProgress<SyncProgress>? Progress { get; init; }

    /// <summary>
    /// When the job entered the queue. Assigned by the queue on enqueue.
    /// </summary>
    public DateTimeOffset EnqueuedAtUtc { get; internal set; }

    /// <summary>
    /// Queue-maintained lifecycle status (Queued → Running → terminal).
    /// </summary>
    public SyncJobStatus Status { get; internal set; } = SyncJobStatus.Queued;

    /// <summary>
    /// Latest progress reported while the job is active. Maintained by the queue.
    /// </summary>
    public SyncProgress? LatestProgress { get; internal set; }

    /// <summary>
    /// Cancellation token owned by the queue for this job, allowing the active job to be
    /// cancelled after it has been handed off to the engine.
    /// </summary>
    internal CancellationToken QueueCancellationToken { get; set; }
}
