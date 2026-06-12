using GSYNC.Storage.Options;
using Microsoft.Extensions.Options;

namespace GSYNC.App.Infrastructure.Configuration;

/// <summary>
/// Bridges the persisted WebDAV sync-target settings into the options instance the
/// storage provider reads on every request, so target edits take effect without restart.
/// </summary>
public sealed class StoreBackedWebDavOptions : IOptions<WebDavOptions>
{
    private readonly SyncTargetStore _store;

    public StoreBackedWebDavOptions(SyncTargetStore store)
    {
        _store = store;
    }

    public WebDavOptions Value
    {
        get
        {
            var target = _store.GetFirstByProvider("webdav");
            return new WebDavOptions
            {
                BaseUrl = target?.Settings.GetValueOrDefault("baseUrl"),
                Username = target?.Settings.GetValueOrDefault("username"),
                Password = target?.Settings.GetValueOrDefault("password"),
            };
        }
    }
}
