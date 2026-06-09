using GSYNC.Core.Models.Manifest;

namespace GSYNC.Core.Models;

public sealed class StorageBinding
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid GameInstanceId { get; init; }

    public required string StorageProviderId { get; init; }

    public required string RemoteNamespace { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public IReadOnlyDictionary<string, string> Settings { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ContentSelection> ContentSelections { get; init; } = Array.Empty<ContentSelection>();
}
