namespace GSYNC.Core.Models.Manifest;

public sealed class ContentItem
{
    public required string ContentId { get; init; }

    public required ContentCategory Category { get; init; }

    public IReadOnlyList<string> PathTemplates { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> IncludePatterns { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ExcludePatterns { get; init; } = Array.Empty<string>();

    public bool DefaultEnabled { get; init; } = true;

    public string? Description { get; init; }
}
