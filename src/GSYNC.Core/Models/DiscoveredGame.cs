namespace GSYNC.Core.Models;

public sealed class DiscoveredGame
{
    public required string GameId { get; init; }

    public required string DisplayName { get; init; }

    public required string SourceProviderId { get; init; }

    public string? InstallDirectory { get; init; }

    public string? PlatformGameId { get; init; }

    public IReadOnlyDictionary<string, string> Variables { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
