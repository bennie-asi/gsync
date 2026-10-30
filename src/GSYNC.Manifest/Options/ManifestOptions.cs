namespace GSYNC.Manifest.Options;

public sealed class ManifestOptions
{
    public const string DefaultRemoteManifestUrl = "https://raw.githubusercontent.com/mtkennerly/ludusavi-manifest/master/data/manifest.yaml";
    public string EmbeddedResourceName { get; set; } = "GSYNC.Manifest.Embedded.Ludusavi.manifest.yaml";

    public string? RemoteManifestUrl { get; set; } = DefaultRemoteManifestUrl;
}
