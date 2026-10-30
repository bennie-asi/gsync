using GSYNC.Core.Models.Manifest;

namespace GSYNC.Manifest.Ludusavi;

internal sealed class LudusaviManifestDocument : Dictionary<string, LudusaviGameEntry>
{
}

internal sealed class LudusaviGameEntry
{
    public Dictionary<string, LudusaviFileEntry?> Files { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public LudusaviSteamEntry? Steam { get; init; }
}

internal sealed class LudusaviFileEntry
{
    public List<string> Tags { get; init; } = [];
    public List<LudusaviConstraint> When { get; init; } = [];
}

internal sealed class LudusaviConstraint
{
    public string? Os { get; init; }
}

internal sealed class LudusaviSteamEntry
{
    public string? Id { get; init; }
}
