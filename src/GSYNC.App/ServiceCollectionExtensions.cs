using GSYNC.App.Infrastructure.Configuration;
using GSYNC.App.Infrastructure.Localization;
using GSYNC.App.Infrastructure.Wizard;
using GSYNC.App.ViewModels;
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
        services.AddSingleton<UiSettingsStore>();
        services.AddSingleton<SyncTargetStore>();
        services.AddSingleton<UserVariablesStore>();
        services.AddSingleton<Microsoft.Extensions.Options.IOptions<GSYNC.Storage.Options.WebDavOptions>, StoreBackedWebDavOptions>();
        services.AddSingleton<Microsoft.Extensions.Options.IOptions<GSYNC.Storage.Options.LocalFolderOptions>, StoreBackedLocalFolderOptions>();
        services.AddSingleton<ILocalizationService, LocalizationService>();
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
        services.AddSingleton<AddGameMatchService>();
        services.AddSingleton<AddGamePathValidationService>();
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
            new LocalFolderStorageProvider(sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<LocalFolderOptions>>()));
        services.AddTransient<IStorageProvider>(sp => sp.GetRequiredService<WebDavStorageProvider>());

        services.AddSingleton<SyncQueue>();
        services.AddSingleton<ISyncQueue>(sp => sp.GetRequiredService<SyncQueue>());
        services.AddSingleton<SyncEngine>();
        services.AddSingleton<ISyncEngine>(sp => sp.GetRequiredService<SyncEngine>());

        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<LibraryPageViewModel>();
        services.AddTransient<HistoryPageViewModel>();
        services.AddTransient<SyncTargetsPageViewModel>();
        services.AddTransient<VariablesPageViewModel>();
        services.AddTransient<GameDetailsViewModel>();
        services.AddTransient<ConflictResolutionViewModel>();
        services.AddTransient<AddGameWizardViewModel>();
        services.AddTransient<SettingsPageViewModel>();

        services.AddTransient<MainWindow>();
        return services;
    }
}
