using System.Threading.Channels;
using GSYNC.Core.Abstractions.Sync;
using GSYNC.Core.Models;

namespace GSYNC.Core.Services.Sync;

public sealed class SyncQueue : ISyncQueue
{
    private readonly Channel<SyncJob> _channel = Channel.CreateUnbounded<SyncJob>();
    private readonly object _gate = new();
    private readonly List<JobEntry> _pending = new();
    private readonly HashSet<Guid> _removed = new();
    private JobEntry? _active;
    private long _lastProgressRaiseTicks;

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
        var cts = new CancellationTokenSource();
        syncJob.QueueCancellationToken = cts.Token;
        syncJob.EnqueuedAtUtc = DateTimeOffset.UtcNow;
        syncJob.Status = SyncJobStatus.Queued;

        lock (_gate)
        {
            _pending.Add(new JobEntry(syncJob, cts));
        }

        var result = _channel.Writer.WriteAsync(syncJob, cancellationToken);
        RaiseChanged();
        return result;
    }

    public IAsyncEnumerable<SyncJob> ReadAllAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
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
            JobEntry entry;
            if (index >= 0)
            {
                entry = _pending[index];
                _pending.RemoveAt(index);
            }
            else
            {
                // Job was not registered through QueueAsync; create a queue-owned token for it.
                var cts = new CancellationTokenSource();
                syncJob.QueueCancellationToken = cts.Token;
                entry = new JobEntry(syncJob, cts);
            }

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

        cts.Cancel();
        return true;
    }

    private void RaiseChanged() => QueueChanged?.Invoke(this, EventArgs.Empty);

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
