using GSYNC.Core.Services;
using Xunit;

namespace GSYNC.Core.Tests;

public sealed class ApplicationTaskHostTests
{
    [Fact]
    public async Task ShutdownWaitsForCancellationCleanup_AndRejectsNewWork()
    {
        using var host = new ApplicationTaskHost();
        var cleaning = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleaned = false;
        var worker = host.RunAsync(async token =>
        {
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { cleaning.SetResult(); await release.Task; cleaned = true; }
        });
        var stopping = host.StopAsync();
        await cleaning.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(stopping.IsCompleted);
        Assert.Throws<InvalidOperationException>(() => { _ = host.RunAsync(_ => Task.CompletedTask); });
        release.SetResult();
        await stopping;
        Assert.True(cleaned);
        Assert.True(worker.IsCanceled);
        Assert.Same(stopping, host.StopAsync());
    }

    [Fact]
    public async Task ShutdownSurfacesWorkerFailure_AfterJoiningOtherWorkers()
    {
        using var host = new ApplicationTaskHost();
        _ = host.RunAsync(_ => Task.FromException(new IOException("worker failed")));
        var cleaned = false;
        _ = host.RunAsync(async token =>
        {
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { cleaned = true; }
        });
        await Assert.ThrowsAsync<IOException>(() => host.StopAsync());
        Assert.True(cleaned);
    }
}
