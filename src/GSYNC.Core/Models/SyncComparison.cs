namespace GSYNC.Core.Models;

public sealed class SyncComparison
{
    public IReadOnlyList<SyncDiffEntry> Entries { get; init; } = Array.Empty<SyncDiffEntry>();
}
