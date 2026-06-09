namespace GSYNC.Core.Models;

public sealed class Snapshot
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid GameInstanceId { get; init; }

    public required string ArchivePath { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public int FileCount { get; init; }

    public long TotalBytes { get; init; }
}
