namespace GSYNC.Core.Models;

public sealed class SyncJob
{
    public required Guid GameInstanceId { get; init; }

    public required SyncDirection Direction { get; init; }

    public CancellationToken CancellationToken { get; init; }

    public IProgress<SyncProgress>? Progress { get; init; }
}
