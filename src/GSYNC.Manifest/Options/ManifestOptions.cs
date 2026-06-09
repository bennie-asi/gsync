namespace GSYNC.Manifest.Options;

public sealed class ManifestOptions
{
    public string EmbeddedResourceName { get; set; } = "GSYNC.Manifest.Embedded.Ludusavi.manifest.yaml";

    public string? RemoteManifestUrl { get; set; }
}
