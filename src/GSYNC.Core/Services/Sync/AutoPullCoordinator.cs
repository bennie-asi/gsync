using GSYNC.Core.Abstractions.Data;
using GSYNC.Core.Abstractions.Sync;
using GSYNC.Core.Models;
using Microsoft.Extensions.Logging;

namespace GSYNC.Core.Services.Sync;

public sealed record AutoPullGameStatus(Guid GameInstanceId, string GameName, string State, string? Detail = null);

/// <summary>Polls only while enabled; each game requires an explicitly confirmed common baseline.</summary>
public sealed class AutoPullCoordinator(
    IGameInstanceRepository games, ISyncEngine engine, ISyncQueue queue, AutoPullBaselineStore baselines, ILogger<AutoPullCoordinator> logger)
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _scanLock = new(1, 1);
    private readonly SemaphoreSlim _wake = new(0, 1);
    private readonly Dictionary<Guid, SyncJob> _jobs = [];
    private readonly Dictionary<Guid, AutoPullGameStatus> _statuses = [];
    private CancellationTokenSource _policyCancellation = new();
    private bool _enabled;

    public event EventHandler? StatusChanged;
    public bool Enabled { get { lock (_gate) return _enabled; } }
    public IReadOnlyList<AutoPullGameStatus> Statuses { get { lock (_gate) return _statuses.Values.OrderBy(s => s.GameName).ToArray(); } }

    public void SetEnabled(bool enabled)
    {
        CancellationTokenSource? cancel = null;
        SyncJob[] jobs = [];
        lock (_gate)
        {
            if (_enabled == enabled) return;
            _enabled = enabled;
            if (enabled) _policyCancellation = new CancellationTokenSource();
            else { cancel = _policyCancellation; jobs = _jobs.Values.ToArray(); _jobs.Clear(); _statuses.Clear(); }
        }
        if (cancel is not null)
        {
            cancel.Cancel();
            foreach (var job in jobs) { if (!queue.Remove(job.Id)) queue.CancelActive(job.Id); }
        }
        Signal();
        RaiseStatusChanged();
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (Enabled)
                {
                    try { await ScanAsync(cancellationToken); }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
                    catch (Exception exception) { logger.LogWarning(exception, "Automatic pull scan failed."); SetStatus(new(Guid.Empty, "", "error", exception.Message)); }
                }
                await _wake.WaitAsync(TimeSpan.FromMinutes(1), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        finally { SetEnabled(false); }
    }

    public async Task ConfirmBaselineAsync(Guid gameId, CancellationToken cancellationToken)
    {
        await _scanLock.WaitAsync(cancellationToken);
        try
        {
            if (IsBusy(gameId)) throw new InvalidOperationException("Wait for this game's queued operations before confirming a baseline.");
            lock (_gate) _jobs.Remove(gameId);
            var game = await games.GetAsync(gameId, cancellationToken) ?? throw new InvalidOperationException("The game no longer exists.");
            var comparison = await engine.CompareAsync(new SyncJob { GameInstanceId = gameId, Direction = SyncDirection.Compare }, cancellationToken);
            baselines.SaveConfirmed(AutoPullPlanner.ConfirmBaseline(gameId, comparison));
            SetStatus(new(gameId, game.DisplayName, "ready"));
        }
        finally { _scanLock.Release(); }
        Signal();
    }

    public void NotifyDestinationChanged(Guid gameId, string gameName)
    {
        SetStatus(new(gameId, gameName, "review", "configuration-changed"));
        Signal();
    }

    public async Task ScanAsync(CancellationToken cancellationToken)
    {
        await _scanLock.WaitAsync(cancellationToken);
        try
        {
            CancellationToken policyToken;
            lock (_gate) { if (!_enabled) return; policyToken = _policyCancellation.Token; }
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, policyToken);
            var token = linked.Token;
            var instances = await games.ListAsync(token);
            lock (_gate) _statuses.Remove(Guid.Empty);
            if (instances.Count == 0)
            {
                SetStatus(new(Guid.Empty, "", "no-games"));
                return;
            }
            foreach (var game in instances)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    SyncJob? previous;
                    lock (_gate) _jobs.TryGetValue(game.Id, out previous);
                    if (previous is not null)
                    {
                        if (previous.Status is SyncJobStatus.Queued or SyncJobStatus.Running) { SetStatus(new(game.Id, game.DisplayName, "queued")); continue; }
                        lock (_gate) _jobs.Remove(game.Id);
                        if (previous.Status == SyncJobStatus.Completed)
                        {
                            // Successful atomic application produced exactly these remote versions locally.
                            var expected = previous.ConflictResolutionPlan!.ExpectedComparison!;
                            var aligned = new SyncComparison { ConfigurationFingerprint = expected.ConfigurationFingerprint, Entries = expected.Entries.Select(e => new SyncDiffEntry
                            {
                                RelativePath = e.RelativePath, ChangeKind = SyncChangeKind.Unchanged, LocalHash = e.RemoteHash, RemoteHash = e.RemoteHash,
                            }).ToArray() };
                            baselines.Save(AutoPullPlanner.ConfirmBaseline(game.Id, aligned));
                        }
                        else if (previous.Status == SyncJobStatus.Failed)
                        { SetStatus(new(game.Id, game.DisplayName, "error", "The automatic download failed; check sync history.")); continue; }
                    }
                    if (IsBusy(game.Id)) { SetStatus(new(game.Id, game.DisplayName, "busy")); continue; }
                    var baseline = baselines.Load(game.Id);
                    if (baseline is null) { SetStatus(new(game.Id, game.DisplayName, "baseline-required")); continue; }
                    var comparison = await engine.CompareAsync(new SyncJob { GameInstanceId = game.Id, Direction = SyncDirection.Compare }, token);
                    var evaluation = AutoPullPlanner.Evaluate(game.Id, comparison, baseline);
                    if (evaluation.Disposition == AutoPullDisposition.Review) { SetStatus(new(game.Id, game.DisplayName, "review", evaluation.Reason)); continue; }
                    if (evaluation.Disposition == AutoPullDisposition.UpToDate) { SetStatus(new(game.Id, game.DisplayName, "ready")); continue; }
                    if (evaluation.Plan is null) { SetStatus(new(game.Id, game.DisplayName, "review", evaluation.Reason)); continue; }
                    var job = new SyncJob { GameInstanceId = game.Id, Direction = SyncDirection.ResolveConflict, DisplayName = game.DisplayName,
                        ConflictResolutionPlan = evaluation.Plan, CancellationToken = policyToken };
                    lock (_gate) { if (!_enabled) return; _jobs[game.Id] = job; }
                    try { await engine.QueueAsync(job, token); }
                    catch { lock (_gate) _jobs.Remove(game.Id); throw; }
                    SetStatus(new(game.Id, game.DisplayName, "queued"));
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Automatic pull failed for game {GameId}.", game.Id);
                    SetStatus(new(game.Id, game.DisplayName, "error", exception.Message));
                }
            }
            lock (_gate)
            {
                var ids = instances.Select(game => game.Id).ToHashSet();
                foreach (var id in _statuses.Keys.Where(id => id != Guid.Empty && !ids.Contains(id)).ToArray()) _statuses.Remove(id);
            }
        }
        finally { _scanLock.Release(); }
    }

    private bool IsBusy(Guid gameId) => queue.ActiveJob?.GameInstanceId == gameId || queue.PendingJobs.Any(job => job.GameInstanceId == gameId);
    private void SetStatus(AutoPullGameStatus status)
    {
        lock (_gate) { if (!_enabled && status.State != "ready") return; _statuses[status.GameInstanceId] = status; }
        RaiseStatusChanged();
    }
    private void RaiseStatusChanged()
    {
        if (StatusChanged is not { } handlers) return;
        foreach (EventHandler handler in handlers.GetInvocationList())
        {
            try { handler(this, EventArgs.Empty); }
            catch (Exception exception) { logger.LogWarning(exception, "Automatic pull status observer failed."); }
        }
    }
    private void Signal() { try { _wake.Release(); } catch (SemaphoreFullException) { } }
}
