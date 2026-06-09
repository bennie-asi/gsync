namespace GSYNC.Core.Models.Manifest;

public sealed class GameContentDefinition
{
    public required string GameId { get; init; }

    public required string DisplayName { get; init; }

    public string? Description { get; init; }

    public IReadOnlyDictionary<string, string> SourceHints { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ContentItem> ContentItems { get; init; } = Array.Empty<ContentItem>();
}
