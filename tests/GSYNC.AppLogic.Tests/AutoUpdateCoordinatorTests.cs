using GSYNC.App.Infrastructure;
using GSYNC.Core.Updates;
using Xunit;

namespace GSYNC.AppLogic.Tests;

public sealed class AutoUpdateCoordinatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "gsync-auto-update-tests", Guid.NewGuid().ToString("N"));
    private readonly TaskCompletionSource _installed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _checks;
    private int _downloads;
    private int _installs;
    private DownloadedRelease? _download;
    private UpdateResult? _result;
    private bool _signed = true;
    private bool _blockDownload;
    private InstallStatus _installStatus = new("starting");
    private readonly TaskCompletionSource _downloadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _downloadCancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public AutoUpdateCoordinatorTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task PeriodicallyChecksAndInstallsOnlyAfterSignedDownload()
    {
        using var cancellation = new CancellationTokenSource();
        var coordinator = CreateCoordinator();
        coordinator.Configure(true, true);
        var run = coordinator.RunAsync(cancellation.Token);
        await _installed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, _downloads);
        Assert.Equal(1, _installs);
        Assert.True(_download?.IsSigned);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    [Fact]
    public async Task CheckOnlyModeRunsPeriodicallyWithoutDownloading()
    {
        using var cancellation = new CancellationTokenSource();
        var coordinator = CreateCoordinator();
        coordinator.Configure(true, false);
        var run = coordinator.RunAsync(cancellation.Token);
        await WaitUntilAsync(() => Volatile.Read(ref _checks) >= 2);
        Assert.Equal(0, _downloads);
        Assert.Equal(0, _installs);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    [Fact]
    public async Task DisableCancelsAnInProgressAutomaticDownload()
    {
        _blockDownload = true;
        using var cancellation = new CancellationTokenSource();
        var coordinator = CreateCoordinator();
        coordinator.Configure(true, true);
        var run = coordinator.RunAsync(cancellation.Token);
        await _downloadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        coordinator.Configure(false, false);
        await _downloadCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, _installs);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    [Fact]
    public async Task WillNotAutomaticallyRetryAVersionThatWasRolledBack()
    {
        _result = new UpdateResult("rolled-back", "1.2.3", "failure", DateTimeOffset.UtcNow);
        using var cancellation = new CancellationTokenSource();
        var coordinator = CreateCoordinator();
        coordinator.Configure(true, true);
        var run = coordinator.RunAsync(cancellation.Token);
        await WaitUntilAsync(() => coordinator.Status.State == "deferred");
        Assert.Equal(0, _downloads);
        Assert.Equal(0, _installs);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    [Fact]
    public async Task UnsignedReleaseNeverStartsAutomaticDownloadOrInstallation()
    {
        _signed = false;
        using var cancellation = new CancellationTokenSource();
        var coordinator = CreateCoordinator();
        coordinator.Configure(true, true);
        var run = coordinator.RunAsync(cancellation.Token);
        await WaitUntilAsync(() => Volatile.Read(ref _checks) >= 2);
        Assert.Equal(0, _downloads);
        Assert.Equal(0, _installs);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    [Fact]
    public async Task UserCancellingWaitDoesNotRestartInstallationOnNextTick()
    {
        _installStatus = new("cancelled");
        using var cancellation = new CancellationTokenSource();
        var coordinator = CreateCoordinator();
        coordinator.Configure(true, true);
        var run = coordinator.RunAsync(cancellation.Token);
        await WaitUntilAsync(() => coordinator.Status.State == "deferred" && Volatile.Read(ref _checks) >= 2);
        Assert.Equal(1, _downloads);
        Assert.Equal(1, _installs);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    private AutoUpdateCoordinator CreateCoordinator() => new(
        token => { Interlocked.Increment(ref _checks); return Task.CompletedTask; },
        () => new UpdateSnapshot("available", null, null, "1.0.0", "1.2.3", ManifestUrl: _signed ? "manifest" : null, SignatureUrl: _signed ? "signature" : null),
        async token =>
        {
            Interlocked.Increment(ref _downloads);
            _downloadStarted.TrySetResult();
            if (_blockDownload)
            {
                try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
                catch (OperationCanceledException) { _downloadCancelled.TrySetResult(); throw; }
            }
            var path = Path.Combine(_root, "download.zip");
            await File.WriteAllTextAsync(path, "download", token);
            _download = new DownloadedRelease("1.2.3", path, [1], [2]);
        },
        () => _download,
        token => { Interlocked.Increment(ref _installs); _installed.TrySetResult(); return Task.CompletedTask; },
        () => _installStatus, () => _result, TimeSpan.FromMilliseconds(30));

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(10, timeout.Token);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
