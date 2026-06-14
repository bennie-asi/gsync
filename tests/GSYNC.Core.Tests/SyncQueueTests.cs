using GSYNC.Core.Models;
using GSYNC.Core.Services.Sync;
using Xunit;

namespace GSYNC.Core.Tests;

public sealed class SyncQueueTests
{
    [Fact]
    public async Task QueueAsync_AssignsIdentity_AndTracksPendingJob()
    {
        var queue = new SyncQueue();
        var changed = 0;
        queue.QueueChanged += (_, _) => changed++;

        var job = new SyncJob
        {
            GameInstanceId = Guid.NewGuid(),
            Direction = SyncDirection.Upload,
            DisplayName = "Game A",
        };

        await queue.QueueAsync(job, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, job.Id);
        Assert.NotEqual(default, job.EnqueuedAtUtc);
        Assert.Equal(SyncJobStatus.Queued, job.Status);
        Assert.Equal(1, queue.QueueDepth);
        Assert.Equal(job, Assert.Single(queue.PendingJobs));
        Assert.True(changed >= 1);
    }

    [Fact]
    public async Task TryBeginJob_PromotesPendingToActive()
    {
        var queue = new SyncQueue();
        var job = new SyncJob
        {
            GameInstanceId = Guid.NewGuid(),
            Direction = SyncDirection.Upload,
        };

        await queue.QueueAsync(job, CancellationToken.None);

        Assert.True(queue.TryBeginJob(job));
        Assert.Equal(0, queue.QueueDepth);
        Assert.Same(job, queue.ActiveJob);
        Assert.Equal(SyncJobStatus.Running, job.Status);

        queue.Complete();
        Assert.Null(queue.ActiveJob);
    }

    [Fact]
    public async Task Remove_PendingJob_IsSkippedByTryBeginJob()
    {
        var queue = new SyncQueue();
        var job = new SyncJob
        {
            GameInstanceId = Guid.NewGuid(),
            Direction = SyncDirection.Upload,
        };

        await queue.QueueAsync(job, CancellationToken.None);

        Assert.True(queue.Remove(job.Id));
        Assert.Equal(0, queue.QueueDepth);
        Assert.Empty(queue.PendingJobs);
        Assert.Equal(SyncJobStatus.Cancelled, job.Status);

        // The job is still sitting in the channel; the engine must skip it.
        Assert.False(queue.TryBeginJob(job));
        Assert.Null(queue.ActiveJob);
    }

    [Fact]
    public void Remove_UnknownJob_ReturnsFalse()
    {
        var queue = new SyncQueue();
        Assert.False(queue.Remove(Guid.NewGuid()));
    }

    [Fact]
    public async Task Remove_ActiveJob_ReturnsFalse()
    {
        var queue = new SyncQueue();
        var job = new SyncJob
        {
            GameInstanceId = Guid.NewGuid(),
            Direction = SyncDirection.Upload,
        };

        await queue.QueueAsync(job, CancellationToken.None);
        queue.TryBeginJob(job);

        Assert.False(queue.Remove(job.Id));
        Assert.Same(job, queue.ActiveJob);
    }

    [Fact]
    public async Task CancelActive_TriggersQueueOwnedToken()
    {
        var queue = new SyncQueue();
        var job = new SyncJob
        {
            GameInstanceId = Guid.NewGuid(),
            Direction = SyncDirection.Upload,
        };

        await queue.QueueAsync(job, CancellationToken.None);
        queue.TryBeginJob(job);

        Assert.False(job.QueueCancellationToken.IsCancellationRequested);
        Assert.True(queue.CancelActive(job.Id));
        Assert.True(job.QueueCancellationToken.IsCancellationRequested);
    }

    [Fact]
    public void CancelActive_WithNoActiveJob_ReturnsFalse()
    {
        var queue = new SyncQueue();
        Assert.False(queue.CancelActive(Guid.NewGuid()));
    }
}
