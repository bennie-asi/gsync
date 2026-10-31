using System.Diagnostics;
using GSYNC.Core.Abstractions.Data;
using GSYNC.Core.Services.Sync;
using GSYNC.Core.Updates;

namespace GSYNC.App.Infrastructure;

/// <summary>Waits for sync work to drain, freezes new work, and hands the signed package to the updater.</summary>
public sealed class AppUpdateInstaller(IAppPathService paths, AppUpdateService updates, SyncActivityGate activity, SyncQueue queue)
{
    private readonly object _gate = new();
    private CancellationTokenSource? _cancel;
    private IDisposable? _activityLease;
    private IDisposable? _queueLease;
    private InstallStatus _status = new("idle");
    public event EventHandler? Changed;
    public event Action? RestartRequested;
    public InstallStatus Status => Volatile.Read(ref _status);

    public async Task InstallWhenIdleAsync(CancellationToken lifetimeToken)
    {
        CancellationTokenSource cancellation;
        lock (_gate)
        {
            if (_cancel is not null) return;
            _cancel = cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken);
        }
        try
        {
            var download = updates.LastDownload;
            if (download?.Manifest is null || download.Signature is null)
                throw new InvalidOperationException("This release has no trusted signature. Install it manually instead.");
            Set(new("preparing"));
            var session = await PrepareSessionAsync(download, cancellation.Token);
            Set(new("waiting"));
            while (true)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                var activeLease = activity.TryFreeze();
                if (activeLease is not null)
                {
                    var queueLease = queue.TryFreezeWhenIdle();
                    if (queueLease is not null)
                    {
                        _activityLease = activeLease;
                        _queueLease = queueLease;
                        break;
                    }
                    activeLease.Dispose();
                }
                await Task.Delay(500, cancellation.Token);
            }
            cancellation.Token.ThrowIfCancellationRequested();
            Set(new("starting"));
            using var parent = Process.GetCurrentProcess();
            var installRoot = Path.GetFullPath(AppContext.BaseDirectory);
            var start = new ProcessStartInfo(Path.Combine(session, "GSYNC.Updater.exe")) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
            foreach (var arg in new[] { "--apply", session, parent.Id.ToString(), parent.StartTime.ToUniversalTime().Ticks.ToString(), installRoot, paths.GetAppDataRoot(), download.Version })
                start.ArgumentList.Add(arg);
            using var updater = Process.Start(start) ?? throw new IOException("The update helper could not start.");
            try
            {
                if (RestartRequested is null) throw new InvalidOperationException("The application cannot close for update.");
                RestartRequested.Invoke();
            }
            catch
            {
                try { updater.Kill(); } catch (InvalidOperationException) { }
                throw;
            }
            // Keep both leases until the application has stopped its background services.
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            ReleaseLeases();
            Set(new("cancelled"));
        }
        catch (Exception ex)
        {
            ReleaseLeases();
            Set(new("error", ex.Message));
        }
        finally
        {
            lock (_gate) { if (ReferenceEquals(_cancel, cancellation)) _cancel = null; }
            cancellation.Dispose();
        }
    }

    public void Cancel() { lock (_gate) _cancel?.Cancel(); }
    public void ReleaseLeases() { Interlocked.Exchange(ref _queueLease, null)?.Dispose(); Interlocked.Exchange(ref _activityLease, null)?.Dispose(); }

    private async Task<string> PrepareSessionAsync(DownloadedRelease download, CancellationToken token)
    {
        var dataRoot = paths.GetAppDataRoot();
        var installRoot = Path.GetFullPath(AppContext.BaseDirectory);
        var helper = Path.Combine(installRoot, "Updater", "GSYNC.Updater.exe");
        UpdateTransaction.ValidateInstalledHelper(installRoot);
        var session = Path.Combine(dataRoot, "updates", Guid.NewGuid().ToString("N"));
        UpdateTransaction.ValidateLocations(installRoot, dataRoot, session);
        var metadata = UpdateManifestVerifier.Verify(download.Manifest!, download.Signature!, download.Version);
        await UpdateManifestVerifier.VerifyArchiveAsync(download.Path, metadata, token);
        Directory.CreateDirectory(session);
        await File.WriteAllBytesAsync(Path.Combine(session, "manifest.json"), download.Manifest!, token);
        await File.WriteAllBytesAsync(Path.Combine(session, "manifest.sig"), download.Signature!, token);
        await using (var source = new FileStream(download.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
        await using (var target = new FileStream(Path.Combine(session, "package.zip"), FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
        {
            await source.CopyToAsync(target, token);
            await target.FlushAsync(token);
            target.Flush(true);
        }
        File.Copy(helper, Path.Combine(session, "GSYNC.Updater.exe"));
        return session;
    }

    private void Set(InstallStatus value)
    {
        Volatile.Write(ref _status, value);
        if (Changed is { } handlers)
            foreach (EventHandler handler in handlers.GetInvocationList()) { try { handler(this, EventArgs.Empty); } catch { } }
    }
}

public sealed record InstallStatus(string State, string? Message = null);
