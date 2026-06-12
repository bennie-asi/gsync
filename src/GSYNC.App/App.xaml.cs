using GSYNC.App.Infrastructure.Logging;
using GSYNC.Core.Abstractions.Data;
using GSYNC.Core.Abstractions.Manifest;
using GSYNC.Core.Services.Sync;
using GSYNC.Manifest.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.UI.Xaml;
using Serilog;

namespace GSYNC.App;

public partial class App : Application
{
    private readonly CancellationTokenSource _lifetimeCts = new();
    private Window? _window;
    private ServiceProvider? _services;
    private Task? _syncProcessingTask;

    public App()
    {
        InitializeComponent();
        RequestedTheme = ApplicationTheme.Dark;
        UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    public static new App Current => (App)Application.Current;

    public Window MainWindow => _window ?? throw new InvalidOperationException("Main window has not been created yet.");

    public IServiceProvider Services => _services ?? throw new InvalidOperationException("Services have not been initialized.");

    public static T GetService<T>() where T : notnull
    {
        return Current.Services.GetRequiredService<T>();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var appPaths = new GSYNC.Data.Services.AppPathService();
        EnsureAppDirectories(appPaths);
        Log.Logger = SerilogBootstrap.CreateLogger(appPaths.GetLogsDirectory());
        Log.Information("GSYNC launch requested.");

        try
        {
            Log.Information("Building application service provider.");
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddGsyncServices(Log.Logger);
            _services = serviceCollection.BuildServiceProvider();

            Log.Information("Creating main window shell.");
            _window = _services.GetRequiredService<MainWindow>();
            _window.Closed += OnWindowClosed;
            _window.Activate();
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "GSYNC failed before the main window shell could be shown.");
            throw;
        }
    }

    public async Task RunDeferredStartupAsync()
    {
        if (_services is null)
        {
            Log.Error("Deferred startup was requested before services were initialized.");
            return;
        }

        try
        {
            Log.Information("Deferred startup phase started.");
            var initializer = _services.GetRequiredService<GSYNC.Data.Services.DatabaseInitializer>();
            Log.Information("Initializing database.");
            await initializer.InitializeAsync(_lifetimeCts.Token);

            Log.Information("Seeding embedded manifest if needed.");
            await SeedEmbeddedManifestIfNeededAsync(_services, _lifetimeCts.Token);

            Log.Information("Starting background services.");
            StartBackgroundServices(_services);

            Log.Information("Refreshing community manifest if configured.");
            _ = RefreshCommunityManifestIfConfiguredAsync(_services, _lifetimeCts.Token);
            Log.Information("GSYNC deferred startup completed successfully.");
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Deferred startup failed after the main window shell was shown.");

            try
            {
                if (_services.GetService<GSYNC.App.ViewModels.MainWindowViewModel>() is { } shellViewModel)
                {
                    shellViewModel.ReportStartupDegraded("启动后初始化失败 · 壳层已保留");
                }
            }
            catch (Exception reportException)
            {
                Log.Error(reportException, "Failed to report deferred startup degradation to the shell view model.");
            }
        }
    }

    private static void EnsureAppDirectories(IAppPathService appPaths)
    {
        Directory.CreateDirectory(appPaths.GetAppDataRoot());
        Directory.CreateDirectory(appPaths.GetLogsDirectory());
        Directory.CreateDirectory(appPaths.GetDefinitionsDirectory());
        Directory.CreateDirectory(appPaths.GetSnapshotsDirectory());
    }

    private static async Task SeedEmbeddedManifestIfNeededAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var store = services.GetRequiredService<ICommunityDefinitionStore>();
        var existingDefinitions = await store.GetDefinitionsAsync(cancellationToken);
        if (existingDefinitions.Count > 0)
        {
            Log.Information("Community manifest definitions already exist; skipping embedded seed.");
            return;
        }

        var reader = services.GetRequiredService<EmbeddedManifestReader>();
        var parser = services.GetRequiredService<ILudusaviManifestParser>();
        var yaml = await reader.ReadAsync(cancellationToken);
        var definitions = parser.Parse(yaml);
        await store.SaveDefinitionsAsync(definitions, cancellationToken);
        Log.Information("Seeded community manifest definitions from embedded resource.");
    }

    private void StartBackgroundServices(IServiceProvider services)
    {
        var syncEngine = services.GetRequiredService<SyncEngine>();
        _syncProcessingTask = Task.Run(() => syncEngine.ProcessQueuedJobsAsync(_lifetimeCts.Token), _lifetimeCts.Token);
    }

    private static async Task RefreshCommunityManifestIfConfiguredAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var options = services.GetRequiredService<IOptions<GSYNC.Manifest.Options.ManifestOptions>>();
        if (string.IsNullOrWhiteSpace(options.Value.RemoteManifestUrl))
        {
            Log.Information("Remote manifest refresh is not configured; skipping refresh.");
            return;
        }

        var manifestService = services.GetRequiredService<IManifestService>();
        await manifestService.RefreshCommunityDefinitionsAsync(cancellationToken);
        Log.Information("Community manifest refresh completed.");
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _lifetimeCts.Cancel();
        _services?.Dispose();
        Log.CloseAndFlush();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.Exception, "Unhandled UI exception.");
    }

    private void OnCurrentDomainUnhandledException(object? sender, System.UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            Log.Fatal(exception, "Unhandled AppDomain exception.");
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unobserved task exception.");
        e.SetObserved();
    }
}
