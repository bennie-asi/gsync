namespace GSYNC.Core.Models;

public sealed class Conflict
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid GameInstanceId { get; init; }

    public string? ContentItemId { get; init; }

    public required string RelativePath { get; init; }

    public required ConflictKind Kind { get; init; }

    public string? LocalHash { get; init; }

    public string? RemoteHash { get; init; }

    public DateTimeOffset DetectedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
