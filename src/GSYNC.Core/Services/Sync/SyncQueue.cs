using System.Threading.Channels;
using System.Runtime.CompilerServices;
using GSYNC.Core.Abstractions.Sync;
using GSYNC.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GSYNC.Core.Services.Sync;

public sealed class SyncQueue : ISyncQueue
{
    private readonly Channel<SyncJob> _channel = Channel.CreateUnbounded<SyncJob>();
    private readonly object _gate = new();
    private readonly List<JobEntry> _pending = new();
    private readonly HashSet<Guid> _removed = new();
    private JobEntry? _active;
    private long _lastProgressRaiseTicks;
    private bool _readerActive;
    private readonly ILogger<SyncQueue> _logger;

    public SyncQueue(ILogger<SyncQueue>? logger = null) => _logger = logger ?? NullLogger<SyncQueue>.Instance;

    public SyncJob? ActiveJob
    {
        get
        {
            lock (_gate)
            {
                return _active?.Job;
            }
        }
    }

    public int QueueDepth
    {
        get
        {
            lock (_gate)
            {
                return _pending.Count;
            }
        }
    }

    public IReadOnlyList<SyncJob> PendingJobs
    {
        get
        {
            lock (_gate)
            {
                return _pending.Select(entry => entry.Job).ToArray();
            }
        }
    }

    public SyncProgress? ActiveProgress
    {
        get
        {
            lock (_gate)
            {
                return _active?.Job.LatestProgress;
            }
        }
    }

    public event EventHandler? QueueChanged;

    public ValueTask QueueAsync(SyncJob syncJob, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (syncJob.GameInstanceId == Guid.Empty || !Enum.IsDefined(syncJob.Direction)) throw new ArgumentException("The sync job has an invalid game or direction.");
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (syncJob.Status != SyncJobStatus.Queued || _pending.Any(entry => entry.Job.Id == syncJob.Id) || _active?.Job.Id == syncJob.Id)
                throw new InvalidOperationException("A sync job can only be queued once.");
            var cts = new CancellationTokenSource();
            syncJob.QueueCancellationToken = cts.Token;
            syncJob.EnqueuedAtUtc = DateTimeOffset.UtcNow;
            _pending.Add(new JobEntry(syncJob, cts));
            if (!_channel.Writer.TryWrite(syncJob))
            {
                _pending.RemoveAt(_pending.Count - 1);
                cts.Dispose();
                throw new ChannelClosedException();
            }
        }
        RaiseChanged();
        return ValueTask.CompletedTask;
    }

    public async IAsyncEnumerable<SyncJob> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_readerActive) throw new InvalidOperationException("Only one sync queue worker may run at a time.");
            _readerActive = true;
        }
        try
        {
            await foreach (var job in _channel.Reader.ReadAllAsync(cancellationToken)) yield return job;
        }
        finally { lock (_gate) _readerActive = false; }
    }

    public bool TryBeginJob(SyncJob syncJob)
    {
        lock (_gate)
        {
            if (_removed.Remove(syncJob.Id))
            {
                // The job was removed before it could start; skip it.
                return false;
            }

            var index = _pending.FindIndex(entry => entry.Job.Id == syncJob.Id);
            if (index < 0 || !ReferenceEquals(_pending[index].Job, syncJob)) return false;
            if (_active is not null) throw new InvalidOperationException("Another sync job is already running.");
            var entry = _pending[index];
            _pending.RemoveAt(index);
            _active = entry;
            syncJob.Status = SyncJobStatus.Running;
        }

        RaiseChanged();
        return true;
    }

    public void Complete()
    {
        CancellationTokenSource? cts;
        lock (_gate)
        {
            cts = _active?.Cts;
            _active = null;
        }

        cts?.Dispose();
        RaiseChanged();
    }

    public void ReportProgress(SyncJob syncJob, SyncProgress progress)
    {
        lock (_gate)
        {
            if (_active is { } active && active.Job.Id == syncJob.Id)
            {
                active.Job.LatestProgress = progress;
            }
        }

        // Throttle change notifications so per-file progress does not flood the UI.
        var now = Environment.TickCount64;
        var last = Interlocked.Read(ref _lastProgressRaiseTicks);
        if (now - last >= 120)
        {
            Interlocked.Exchange(ref _lastProgressRaiseTicks, now);
            RaiseChanged();
        }
    }

    public bool Remove(Guid jobId)
    {
        CancellationTokenSource? cts;
        lock (_gate)
        {
            if (_active is { } active && active.Job.Id == jobId)
            {
                // Active jobs must be cancelled, not removed.
                return false;
            }

            var index = _pending.FindIndex(entry => entry.Job.Id == jobId);
            if (index < 0)
            {
                return false;
            }

            var entry = _pending[index];
            _pending.RemoveAt(index);
            _removed.Add(jobId);
            entry.Job.Status = SyncJobStatus.Cancelled;
            cts = entry.Cts;
        }

        cts?.Cancel();
        cts?.Dispose();
        RaiseChanged();
        return true;
    }

    public bool CancelActive(Guid jobId)
    {
        CancellationTokenSource? cts = null;
        lock (_gate)
        {
            if (_active is { } active && active.Job.Id == jobId)
            {
                cts = active.Cts;
            }
        }

        if (cts is null)
        {
            return false;
        }

        try { cts.Cancel(); return true; }
        catch (ObjectDisposedException) { return false; }
    }

    private void RaiseChanged()
    {
        if (QueueChanged is not { } changed) return;
        foreach (EventHandler observer in changed.GetInvocationList())
        {
            try { observer(this, EventArgs.Empty); }
            catch (Exception exception) { _logger.LogWarning(exception, "A queue observer failed."); }
        }
    }

    private sealed class JobEntry
    {
        public JobEntry(SyncJob job, CancellationTokenSource cts)
        {
            Job = job;
            Cts = cts;
        }

        public SyncJob Job { get; }

        public CancellationTokenSource Cts { get; }
    }
}
