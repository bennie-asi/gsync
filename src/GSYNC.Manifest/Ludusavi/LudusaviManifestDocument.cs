using GSYNC.Core.Models.Manifest;

namespace GSYNC.Manifest.Ludusavi;

internal sealed class LudusaviManifestDocument : Dictionary<string, LudusaviGameEntry>
{
}

internal sealed class LudusaviGameEntry
{
    public Dictionary<string, object?> Files { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public string? InstallDir { get; init; }

    public Dictionary<string, string?> InstallDirRegistry { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public LudusaviSteamEntry? Steam { get; init; }
}

internal sealed class LudusaviSteamEntry
{
    public string? Id { get; init; }
}
