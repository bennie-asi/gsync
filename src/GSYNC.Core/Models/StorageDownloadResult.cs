using System.IO;

namespace GSYNC.Core.Models;

public sealed class StorageDownloadResult : IDisposable
{
    public required Stream Content { get; init; }

    public string? ContentType { get; init; }

    public long? Length { get; init; }

    public void Dispose()
    {
        Content.Dispose();
    }
}
