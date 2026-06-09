namespace GSYNC.Core.Models;

public sealed class SyncDiffEntry
{
    public required string RelativePath { get; init; }

    public required SyncChangeKind ChangeKind { get; init; }

    public string? LocalHash { get; init; }

    public string? RemoteHash { get; init; }

    public DateTimeOffset? LocalModifiedAtUtc { get; init; }

    public DateTimeOffset? RemoteModifiedAtUtc { get; init; }
}
