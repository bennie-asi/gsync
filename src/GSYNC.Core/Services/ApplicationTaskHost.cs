namespace GSYNC.Core.Services;

/// <summary>Owns application tasks so shutdown can cancel and join them before disposing their dependencies.</summary>
public sealed class ApplicationTaskHost : IDisposable
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _cancellation = new();
    private readonly List<Task> _tasks = new();
    private Task? _stopTask;
    private bool _stopping;
    private Exception? _firstFailure;

    public CancellationToken Token => _cancellation.Token;
    public bool IsStopping { get { lock (_gate) return _stopping; } }

    public Task RunAsync(Func<CancellationToken, Task> operation)
    {
        lock (_gate)
        {
            if (_stopping) throw new InvalidOperationException("The application is stopping and cannot accept new work.");
            // Invoke on the caller's context; UI initialization must retain its dispatcher.
            Task task;
            try { task = operation(Token); }
            catch (Exception exception) { task = Task.FromException(exception); }
            _tasks.Add(task);
            Observe(task);
            return task;
        }
    }

    public Task StopAsync()
    {
        lock (_gate)
        {
            if (_stopTask is not null) return _stopTask;
            _stopping = true;
            return _stopTask = StopCoreAsync(_tasks.ToArray());
        }
    }

    public Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> operation)
    {
        lock (_gate)
        {
            if (_stopping) throw new InvalidOperationException("The application is stopping and cannot accept new work.");
            Task<T> task;
            try { task = operation(Token); }
            catch (Exception exception) { task = Task.FromException<T>(exception); }
            _tasks.Add(task);
            Observe(task);
            return task;
        }
    }

    private async Task StopCoreAsync(Task[] tasks)
    {
        try { await _cancellation.CancelAsync(); }
        finally
        {
            try { await Task.WhenAll(tasks); }
            catch (OperationCanceledException) when (_cancellation.IsCancellationRequested) { }
        }
        if (_firstFailure is { } failure) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private void Observe(Task task)
    {
        _ = task.ContinueWith(completed =>
        {
            lock (_gate)
            {
                _tasks.Remove(completed);
                if (completed.IsFaulted) _firstFailure ??= completed.Exception!.InnerException ?? completed.Exception;
            }
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_stopTask is not { IsCompleted: true })
                throw new InvalidOperationException("Stop and await application tasks before disposing them.");
            _cancellation.Dispose();
        }
    }
}
