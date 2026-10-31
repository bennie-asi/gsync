namespace GSYNC.Core.Services.Sync;

/// <summary>Prevents update shutdown from racing a direct sync operation or queue submission.</summary>
public sealed class SyncActivityGate
{
    private readonly object _gate = new();
    private int _active;
    private bool _frozen;

    public IDisposable Enter()
    {
        lock (_gate)
        {
            if (_frozen) throw new InvalidOperationException("An application update is preparing to restart. Try again after it finishes.");
            _active++;
            return new Lease(this, false);
        }
    }

    public IDisposable? TryFreeze()
    {
        lock (_gate)
        {
            if (_frozen || _active != 0) return null;
            _frozen = true;
            return new Lease(this, true);
        }
    }

    private void Release(bool freeze)
    {
        lock (_gate) { if (freeze) _frozen = false; else _active--; }
    }

    private sealed class Lease(SyncActivityGate owner, bool freeze) : IDisposable
    {
        private SyncActivityGate? _owner = owner;
        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Release(freeze);
    }
}
