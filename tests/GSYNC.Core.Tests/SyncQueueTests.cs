using GSYNC.Core.Models;
using GSYNC.Core.Services.Sync;
using Xunit;

namespace GSYNC.Core.Tests;

public sealed class SyncQueueTests
{
    [Fact]
    public async Task QueueAsync_IncrementsQueueDepth_AndTracksActiveJob()
    {
        var queue = new SyncQueue();
        var job = new SyncJob
        {
            GameInstanceId = Guid.NewGuid(),
            Direction = SyncDirection.Upload,
        };

        await queue.QueueAsync(job, CancellationToken.None);

        Assert.Equal(1, queue.QueueDepth);

        queue.Start(job);
        Assert.Equal(0, queue.QueueDepth);
        Assert.Same(job, queue.ActiveJob);

        queue.Complete();
        Assert.Null(queue.ActiveJob);
    }
}
