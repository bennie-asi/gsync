using GSYNC.App.Infrastructure.Configuration;
using GSYNC.App.Infrastructure;
using GSYNC.App.Infrastructure.Localization;
using GSYNC.App.Infrastructure.Wizard;
using GSYNC.App.ViewModels;
using GSYNC.Core.Abstractions;
using GSYNC.Core.Abstractions.Data;
using GSYNC.Core.Abstractions.Manifest;
using GSYNC.Core.Abstractions.Sync;
using GSYNC.Core.Models;
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
        services.AddHttpClient<IManifestUpdateSource, HttpManifestUpdateSource>()
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) });
        services.AddHttpClient<SteamCatalogSearchService>(client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 GSYNC/1.0");
        });
        services.AddHttpClient<WebDavStorageProvider>()
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5), AllowAutoRedirect = false });

        services.AddSingleton<IAppPathService, AppPathService>();
        services.AddSingleton<ICredentialStore, ProtectedFileCredentialStore>();
        services.AddSingleton<CredentialSettingsService>();
        services.AddSingleton<CredentialMigrationService>();
        services.AddSingleton<UiSettingsStore>();
        services.AddSingleton(sp => new AppUpdateService(sp.GetRequiredService<IAppPathService>().GetAppDataRoot(), AppUpdateService.CreateHttpClient(), DesktopFeatures.CurrentVersion));
        services.AddSingleton<SyncTargetStore>();
        services.AddSingleton<UserVariablesStore>();
        services.AddSingleton<Microsoft.Extensions.Options.IOptions<GSYNC.Manifest.Options.ManifestOptions>, StoreBackedManifestOptions>();
        services.AddSingleton<Microsoft.Extensions.Options.IOptions<GSYNC.Storage.Options.WebDavOptions>, StoreBackedWebDavOptions>();
        services.AddSingleton<Microsoft.Extensions.Options.IOptions<GSYNC.Storage.Options.LocalFolderOptions>, StoreBackedLocalFolderOptions>();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton(sp =>
        {
            var appPaths = sp.GetRequiredService<IAppPathService>();
            var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = appPaths.GetDatabasePath() }.ToString();
            return new SqliteConnectionFactory(connectionString);
        });
        services.AddSingleton<DatabaseInitializer>();

        services.AddSingleton<IGameInstanceRepository, GameInstanceRepository>();
        services.AddSingleton<IGameProfileRepository, GameProfileRepository>();
        services.AddSingleton<IStorageBindingRepository, StorageBindingRepository>();
        services.AddSingleton<ISyncHistoryRepository, SyncHistoryRepository>();
        services.AddSingleton<ICommunityDefinitionStore, CommunityDefinitionStore>();
        services.AddSingleton<IDefinitionOverrideStore, DefinitionOverrideStore>();

        services.AddSingleton<PathResolver>();
        services.AddSingleton<SystemVariableProvider>();
        services.AddSingleton<AddGameMatchService>();
        services.AddSingleton<RemoteGameDiscoveryService>();
        services.AddSingleton(sp => new AddGamePathValidationService(
            sp.GetRequiredService<PathResolver>(), sp.GetRequiredService<SystemVariableProvider>(),
            () => sp.GetRequiredService<UserVariablesStore>().AsDictionary()));
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
        services.AddSingleton<SyncActivityGate>();
        services.AddSingleton<AppUpdateInstaller>();
        services.AddSingleton(sp =>
        {
            var updates = sp.GetRequiredService<AppUpdateService>();
            var installer = sp.GetRequiredService<AppUpdateInstaller>();
            var paths = sp.GetRequiredService<IAppPathService>();
            return new AutoUpdateCoordinator(
                token => updates.CheckAsync(true, token), () => updates.Snapshot,
                async token => { await updates.DownloadAsync(null, token); }, () => updates.LastDownload,
                installer.InstallWhenIdleAsync, () => installer.Status,
                () => GSYNC.Core.Updates.UpdateResultStore.Read(paths.GetAppDataRoot()));
        });
        services.AddSingleton<ISyncQueue>(sp => sp.GetRequiredService<SyncQueue>());
        services.AddSingleton(sp =>
        {
            var uiSettingsStore = sp.GetRequiredService<UiSettingsStore>();
            return new SyncEngine(
                sp.GetRequiredService<ISyncQueue>(),
                sp.GetRequiredService<IGameInstanceRepository>(),
                sp.GetRequiredService<IStorageBindingRepository>(),
                sp.GetRequiredService<ISyncHistoryRepository>(),
                sp.GetRequiredService<IManifestService>(),
                sp.GetServices<ISourceProvider>(),
                sp.GetServices<IStorageProvider>(),
                sp.GetRequiredService<IAppPathService>(),
                sp.GetRequiredService<PathResolver>(),
                sp.GetRequiredService<SystemVariableProvider>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SyncEngine>>(),
                () => uiSettingsStore.Load().AutoSnapshotBeforeDownload,
                binding =>
                {
                    if (!binding.Settings.TryGetValue("targetId", out var targetId) || !Guid.TryParse(targetId, out var id))
                    {
                        return sp.GetRequiredService<CredentialSettingsService>().Resolve(binding.Settings);
                    }

                    var target = sp.GetRequiredService<SyncTargetStore>().Get(id)
                        ?? throw new InvalidOperationException($"Sync target '{id}' no longer exists.");
                    if (!string.Equals(target.ProviderId, binding.StorageProviderId, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("The sync target provider no longer matches the game's binding.");
                    }

                    return new Dictionary<string, string>(sp.GetRequiredService<SyncTargetStore>().ResolveSettings(target.Settings), StringComparer.OrdinalIgnoreCase)
                    {
                        ["targetId"] = id.ToString("D"),
                        ["__gsyncHistoryTargetName"] = target.Name,
                    };
                },
                () => sp.GetRequiredService<UserVariablesStore>().AsDictionary(),
                sp.GetRequiredService<GSYNC.Core.Services.ApplicationTaskHost>(),
                sp.GetRequiredService<SyncActivityGate>(),
                (binding, targetId) =>
                {
                    var target = sp.GetRequiredService<SyncTargetStore>().Get(targetId)
                        ?? throw new InvalidOperationException("The selected sync target no longer exists.");
                    var settings = new Dictionary<string, string>(binding.Settings, StringComparer.OrdinalIgnoreCase)
                    {
                        ["targetId"] = target.Id.ToString("D"), ["targetName"] = target.Name,
                        ["__gsyncTargetRevision"] = target.UpdatedAtUtc.ToUniversalTime().ToString("O"),
                    };
                    return new StorageBinding { Id = binding.Id, GameInstanceId = binding.GameInstanceId,
                        StorageProviderId = target.ProviderId, RemoteNamespace = binding.RemoteNamespace, Settings = settings,
                        ContentSelections = binding.ContentSelections, CreatedAtUtc = binding.CreatedAtUtc, UpdatedAtUtc = binding.UpdatedAtUtc };
                });
        });
        services.AddSingleton<ISyncEngine>(sp => sp.GetRequiredService<SyncEngine>());
        services.AddSingleton<ISyncBindingUpdater>(sp => sp.GetRequiredService<SyncEngine>());
        services.AddSingleton<AutoPullBaselineStore>();
        services.AddSingleton<AutoPullCoordinator>();

        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<LibraryPageViewModel>();
        services.AddTransient<HistoryPageViewModel>();
        services.AddTransient<SyncTargetsPageViewModel>();
        services.AddTransient<VariablesPageViewModel>();
        services.AddTransient<GameDetailsViewModel>();
        services.AddTransient<ConflictResolutionViewModel>();
        services.AddTransient<AddGameWizardViewModel>();
        services.AddTransient<SettingsPageViewModel>();
        services.AddTransient<QueuePageViewModel>();

        services.AddTransient<MainWindow>();
        return services;
    }
}
