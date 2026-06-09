namespace GSYNC.Core.Models;

public sealed class SyncProgress
{
    public string? CurrentFileName { get; init; }

    public int ProcessedFiles { get; init; }

    public int TotalFiles { get; init; }

    public string? Stage { get; init; }
}
