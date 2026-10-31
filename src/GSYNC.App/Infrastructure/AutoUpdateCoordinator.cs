using System.Threading.Channels;
using GSYNC.Core.Updates;

namespace GSYNC.App.Infrastructure;

/// <summary>Checks while the app is running and installs only a verified release after sync work drains.</summary>
public sealed class AutoUpdateCoordinator(
    Func<CancellationToken, Task> check,
    Func<UpdateSnapshot> snapshot,
    Func<CancellationToken, Task> download,
    Func<DownloadedRelease?> lastDownload,
    Func<CancellationToken, Task> install,
    Func<InstallStatus> installStatus,
    Func<UpdateResult?> lastResult,
    TimeSpan? interval = null)
{
    private readonly object _gate = new();
    private readonly Channel<bool> _changes = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });
    private CancellationTokenSource? _cycle;
    private bool _checkEnabled;
    private bool _installEnabled;
    private string? _cancelledVersion;
    private AutoUpdateStatus _status = new("idle");
    private readonly TimeSpan _interval = interval ?? TimeSpan.FromHours(1);

    public event EventHandler? Changed;
    public AutoUpdateStatus Status => Volatile.Read(ref _status);

    public void Configure(bool checkEnabled, bool installEnabled)
    {
        lock (_gate)
        {
            if (_checkEnabled == checkEnabled && _installEnabled == installEnabled) return;
            _checkEnabled = checkEnabled;
            _installEnabled = installEnabled;
            _cycle?.Cancel();
            if (!installEnabled) _cancelledVersion = null;
        }
        _changes.Writer.TryWrite(true);
    }

    public async Task RunAsync(CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            while (_changes.Reader.TryRead(out _)) { }
            using var cycle = CancellationTokenSource.CreateLinkedTokenSource(token);
            bool enabled, automaticInstall;
            lock (_gate)
            {
                _cycle = cycle;
                enabled = _checkEnabled;
                automaticInstall = _installEnabled;
            }
            try
            {
                if (enabled) await RunCycleAsync(automaticInstall, cycle.Token);
                else Set(new("idle"));
            }
            catch (OperationCanceledException) when (cycle.IsCancellationRequested) { }
            catch (Exception ex) { Set(new("error", ex.Message)); }
            finally { lock (_gate) { if (ReferenceEquals(_cycle, cycle)) _cycle = null; } }

            token.ThrowIfCancellationRequested();
            // Settings changes wake the loop immediately; network failures retry on the next tick.
            using var wait = CancellationTokenSource.CreateLinkedTokenSource(token);
            wait.CancelAfter(_interval);
            try { await _changes.Reader.ReadAsync(wait.Token); }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
            while (_changes.Reader.TryRead(out _)) { }
        }
    }

    private async Task RunCycleAsync(bool automaticInstall, CancellationToken token)
    {
        await check(token);
        token.ThrowIfCancellationRequested();
        var release = snapshot();
        if (!automaticInstall || release.State != "available" || release.Version is null || release.ManifestUrl is null || release.SignatureUrl is null)
        {
            Set(new("idle"));
            return;
        }
        var result = lastResult();
        if (Volatile.Read(ref _cancelledVersion) == release.Version || (result?.Version == release.Version && result.State is "rolled-back" or "recovery-required"))
        {
            Set(new("deferred", Version: release.Version));
            return;
        }
        var existing = lastDownload();
        if (existing?.Version != release.Version || !existing.IsSigned || !File.Exists(existing.Path))
        {
            Set(new("downloading", Version: release.Version));
            await download(token);
        }
        token.ThrowIfCancellationRequested();
        var ready = lastDownload();
        if (ready?.Version != release.Version || !ready.IsSigned || !File.Exists(ready.Path))
            throw new InvalidDataException("The automatic update has no verified signed download.");
        if (snapshot().Version != release.Version) return;
        Set(new("installing", Version: release.Version));
        await install(token);
        token.ThrowIfCancellationRequested();
        var outcome = installStatus();
        if (outcome.State == "cancelled")
        {
            Volatile.Write(ref _cancelledVersion, release.Version);
            Set(new("deferred", Version: release.Version));
        }
        else if (outcome.State == "error") Set(new("error", outcome.Message, release.Version));
    }

    private void Set(AutoUpdateStatus value)
    {
        Volatile.Write(ref _status, value);
        if (Changed is { } handlers)
            foreach (EventHandler handler in handlers.GetInvocationList()) { try { handler(this, EventArgs.Empty); } catch { } }
    }
}

public sealed record AutoUpdateStatus(string State, string? Message = null, string? Version = null);
