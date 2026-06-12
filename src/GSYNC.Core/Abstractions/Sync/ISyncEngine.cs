using GSYNC.Core.Models;

namespace GSYNC.Core.Abstractions.Sync;

public interface ISyncEngine
{
    Task QueueAsync(SyncJob syncJob, CancellationToken cancellationToken);

    Task<SyncComparison> CompareAsync(SyncJob syncJob, CancellationToken cancellationToken);

    Task RestoreSnapshotAsync(Guid snapshotId, CancellationToken cancellationToken);

    Task ResolveConflictsAsync(ConflictResolutionPlan plan, CancellationToken cancellationToken);
}
