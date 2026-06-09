namespace GSYNC.Core.Models;

public sealed class RemoteEntry
{
    public required string Path { get; init; }

    public bool IsDirectory { get; init; }

    public long Size { get; init; }

    public DateTimeOffset? LastModifiedUtc { get; init; }

    public string? Hash { get; init; }
}
