using GSYNC.App.Infrastructure.Localization;
using GSYNC.Manifest.Options;
using Microsoft.Extensions.Options;

namespace GSYNC.App.Infrastructure.Configuration;

public sealed class StoreBackedManifestOptions : IOptions<ManifestOptions>
{
    private readonly UiSettingsStore _store;

    public StoreBackedManifestOptions(UiSettingsStore store)
    {
        _store = store;
    }

    public ManifestOptions Value
    {
        get
        {
            var settings = _store.Load();
            return new ManifestOptions
            {
                RemoteManifestUrl = string.IsNullOrWhiteSpace(settings.ManifestSourceUrl)
                    ? null
                    : settings.ManifestSourceUrl.Trim(),
            };
        }
    }
}
