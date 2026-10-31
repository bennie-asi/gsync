namespace GSYNC.Core.Models;

public sealed class SyncComparison
{
    public IReadOnlyList<SyncDiffEntry> Entries { get; init; } = Array.Empty<SyncDiffEntry>();

    // Hash of the resolved paths and target identity used for this comparison.
    public string? ConfigurationFingerprint { get; init; }
}
