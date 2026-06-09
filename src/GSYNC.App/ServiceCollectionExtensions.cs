using GSYNC.Core.Abstractions;
using GSYNC.Core.Abstractions.Data;
using GSYNC.Core.Abstractions.Manifest;
using GSYNC.Core.Abstractions.Sync;
using GSYNC.Core.Services;
using GSYNC.Core.Services.Sync;
using GSYNC.Core.Utilities;
using GSYNC.Data.Options;
using GSYNC.Data.Repositories;
using GSYNC.Data.Services;
using GSYNC.Manifest.Ludusavi;
using GSYNC.Manifest.Options;
using GSYNC.Manifest.Services;
using GSYNC.Providers.Options;
using GSYNC.Providers.Services;
using GSYNC.Storage.Options;
using GSYNC.Storage.Services;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace GSYNC.App;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGsyncServices(this IServiceCollection services, Serilog.ILogger logger)
    {
        services.AddLogging(builder =>
        {
            builder.AddSerilog(logger, dispose: false);
        });

        services.AddHttpClient();
        services.AddHttpClient<IManifestUpdateSource, HttpManifestUpdateSource>();
        services.AddHttpClient<WebDavStorageProvider>();

        services.AddSingleton<IAppPathService, AppPathService>();
        services.AddSingleton(sp =>
        {
            var appPaths = sp.GetRequiredService<IAppPathService>();
            var connectionString = $"Data Source={appPaths.GetDatabasePath()}";
            return new SqliteConnectionFactory(connectionString);
        });
        services.AddSingleton<DatabaseInitializer>();

        services.AddSingleton<IGameInstanceRepository, GameInstanceRepository>();
        services.AddSingleton<IStorageBindingRepository, StorageBindingRepository>();
        services.AddSingleton<ISyncHistoryRepository, SyncHistoryRepository>();
        services.AddSingleton<ICommunityDefinitionStore, CommunityDefinitionStore>();
        services.AddSingleton<IDefinitionOverrideStore, DefinitionOverrideStore>();

        services.AddSingleton<PathResolver>();
        services.AddSingleton<SystemVariableProvider>();
        services.AddSingleton<EmbeddedManifestReader>();
        services.AddSingleton<ILudusaviManifestParser, LudusaviManifestParser>();
        services.AddSingleton<IUserDefinitionStore>(sp =>
        {
            var appPaths = sp.GetRequiredService<IAppPathService>();
            Directory.CreateDirectory(appPaths.GetDefinitionsDirectory());
            return new YamlUserDefinitionStore(appPaths.GetDefinitionsDirectory(), sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<YamlUserDefinitionStore>>());
        });
        services.AddSingleton<IManifestService, ManifestService>();

        services.AddSingleton<ISourceProvider, SteamSourceProvider>();
        services.AddSingleton<ISourceProvider, EpicSourceProvider>();
        services.AddSingleton<ISourceProvider, CustomSourceProvider>();

        services.AddSingleton<IStorageProvider>(sp =>
        {
            var appPaths = sp.GetRequiredService<IAppPathService>();
            return new LocalFolderStorageProvider(Microsoft.Extensions.Options.Options.Create(new LocalFolderOptions
            {
                RootPath = Path.Combine(appPaths.GetAppDataRoot(), "storage"),
            }));
        });
        services.AddTransient<IStorageProvider>(sp => sp.GetRequiredService<WebDavStorageProvider>());

        services.AddSingleton<SyncQueue>();
        services.AddSingleton<ISyncQueue>(sp => sp.GetRequiredService<SyncQueue>());
        services.AddSingleton<SyncEngine>();
        services.AddSingleton<ISyncEngine>(sp => sp.GetRequiredService<SyncEngine>());

        services.AddSingleton<MainWindow>();
        return services;
    }
}
