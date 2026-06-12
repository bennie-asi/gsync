namespace GSYNC.Core.Models;

public sealed class ConflictResolutionPlan
{
    public required Guid GameInstanceId { get; init; }

    public IReadOnlyList<ConflictResolutionDecision> Decisions { get; init; } = Array.Empty<ConflictResolutionDecision>();
}
