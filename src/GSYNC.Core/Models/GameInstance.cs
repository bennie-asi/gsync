namespace GSYNC.Core.Models;

public sealed class GameInstance
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string GameId { get; init; }

    public required string DisplayName { get; init; }

    public required string SourceProviderId { get; init; }

    public string? InstallDirectory { get; init; }

    public string? PlatformInstanceId { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public IReadOnlyDictionary<string, string> Variables { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
