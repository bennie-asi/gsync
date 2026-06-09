using System.Threading.Channels;
using GSYNC.Core.Abstractions.Sync;
using GSYNC.Core.Models;

namespace GSYNC.Core.Services.Sync;

public sealed class SyncQueue : ISyncQueue
{
    private readonly Channel<SyncJob> _channel = Channel.CreateUnbounded<SyncJob>();
    private int _queueDepth;

    public SyncJob? ActiveJob { get; private set; }

    public int QueueDepth => _queueDepth;

    public ValueTask QueueAsync(SyncJob syncJob, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _queueDepth);
        return _channel.Writer.WriteAsync(syncJob, cancellationToken);
    }

    public IAsyncEnumerable<SyncJob> ReadAllAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }

    public void Start(SyncJob syncJob)
    {
        ActiveJob = syncJob;
        Interlocked.Decrement(ref _queueDepth);
    }

    public void Complete()
    {
        ActiveJob = null;
    }
}
