using GSYNC.Core.Models;

namespace GSYNC.Core.Abstractions.Sync;

public interface ISyncQueue
{
    SyncJob? ActiveJob { get; }

    int QueueDepth { get; }

    ValueTask QueueAsync(SyncJob syncJob, CancellationToken cancellationToken);

    IAsyncEnumerable<SyncJob> ReadAllAsync(CancellationToken cancellationToken);

    void Start(SyncJob syncJob);

    void Complete();
}
