namespace GSYNC.Core.Models;

public sealed class ConflictResolutionPlan
{
    public required Guid GameInstanceId { get; init; }

    public IReadOnlyList<ConflictResolutionDecision> Decisions { get; init; } = Array.Empty<ConflictResolutionDecision>();

    // When supplied, the entire comparison must still match immediately before writes.
    public SyncComparison? ExpectedComparison { get; init; }

    public string? ExpectedConfigurationFingerprint { get; init; }
}
