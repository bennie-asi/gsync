namespace GSYNC.Core.Models;

public sealed class SyncRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid GameInstanceId { get; init; }

    public Guid? SnapshotId { get; init; }

    public required SyncDirection Direction { get; init; }

    public required SyncJobStatus Status { get; init; }

    public DateTimeOffset StartedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAtUtc { get; init; }

    public string? Summary { get; init; }

    public string? ErrorMessage { get; init; }

    public int ProcessedFiles { get; init; }

    public int TotalFiles { get; init; }
}
