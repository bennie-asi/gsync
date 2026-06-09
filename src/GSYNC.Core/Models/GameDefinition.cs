namespace GSYNC.Core.Models;

public sealed class GameDefinition
{
    public required string GameId { get; init; }

    public required string DisplayName { get; init; }

    public string? Description { get; init; }

    public IReadOnlyDictionary<string, string> SourceHints { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
