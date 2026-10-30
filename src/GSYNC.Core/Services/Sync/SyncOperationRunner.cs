using System.Runtime.ExceptionServices;
using GSYNC.Core.Abstractions.Data;
using GSYNC.Core.Models;
using Microsoft.Extensions.Logging;

namespace GSYNC.Core.Services.Sync;

internal sealed class SyncOperationState
{
    internal Snapshot? Snapshot { get; set; }
    internal int Processed { get; set; }
    internal int Total { get; set; }
    internal string? Summary { get; set; }
}

/// <summary>One terminal record and one error policy for every executed operation.</summary>
internal sealed class SyncOperationRunner(ISyncHistoryRepository history, ILogger logger)
{
    internal async Task RunAsync(Guid gameId, SyncDirection direction, Func<SyncOperationState, Task> operation)
    {
        var started = DateTimeOffset.UtcNow;
        var state = new SyncOperationState();
        var status = SyncJobStatus.Completed;
        Exception? failure = null;
        try { await operation(state); }
        catch (OperationCanceledException exception) { failure = exception; status = SyncJobStatus.Cancelled; }
        catch (Exception exception)
        {
            failure = exception;
            status = SyncJobStatus.Failed;
            logger.LogError(exception, "{Direction} failed for game instance {GameInstanceId}.", direction, gameId);
        }

        try
        {
            await history.AddRecordAsync(new SyncRecord
            {
                GameInstanceId = gameId, Direction = direction, Status = status,
                StartedAtUtc = started, CompletedAtUtc = DateTimeOffset.UtcNow,
                SnapshotId = state.Snapshot?.Id, ProcessedFiles = state.Processed, TotalFiles = state.Total,
                Summary = state.Summary ?? (status == SyncJobStatus.Cancelled ? $"{direction} cancelled." : null),
                ErrorMessage = status == SyncJobStatus.Failed ? failure?.Message : null,
            }, CancellationToken.None);
        }
        catch (Exception historyError)
        {
            logger.LogError(historyError, "Could not persist the terminal record for game {GameInstanceId}; operation status was {Status}.", gameId, status);
            if (failure is null) throw new IOException("The operation finished, but its history could not be saved. Check the current save before retrying.", historyError);
        }
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
