using GSYNC.Core.Abstractions.Data;
using GSYNC.Storage.Options;
using Microsoft.Extensions.Options;

namespace GSYNC.App.Infrastructure.Configuration;

/// <summary>
/// Resolves the local-folder storage root from the configured sync target, falling back
/// to the app-data storage directory so the default target works without any setup.
/// </summary>
public sealed class StoreBackedLocalFolderOptions : IOptions<LocalFolderOptions>
{
    private readonly SyncTargetStore _store;
    private readonly IAppPathService _appPathService;

    public StoreBackedLocalFolderOptions(SyncTargetStore store, IAppPathService appPathService)
    {
        _store = store;
        _appPathService = appPathService;
    }

    public LocalFolderOptions Value
    {
        get
        {
            var target = _store.GetFirstByProvider("local-folder");
            var rootPath = target?.Settings.GetValueOrDefault("rootPath");
            return new LocalFolderOptions
            {
                RootPath = string.IsNullOrWhiteSpace(rootPath)
                    ? Path.Combine(_appPathService.GetAppDataRoot(), "storage")
                    : rootPath,
            };
        }
    }
}
