namespace GSYNC.Core.Models;

public sealed class ConflictResolutionDecision
{
    public required string RelativePath { get; init; }

    public required ConflictResolutionAction Action { get; init; }

    public bool VerifyComparedVersions { get; init; }
    public string? ExpectedLocalHash { get; init; }
    public string? ExpectedRemoteHash { get; init; }
}
