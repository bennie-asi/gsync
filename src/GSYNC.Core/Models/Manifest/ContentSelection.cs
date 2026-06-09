namespace GSYNC.Core.Models.Manifest;

public sealed class ContentSelection
{
    public required string ContentId { get; init; }

    public bool IsEnabled { get; init; } = true;
}
