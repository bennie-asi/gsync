using GSYNC.App.Infrastructure.Localization;
using GSYNC.App.Infrastructure;
using GSYNC.App.Infrastructure.Logging;
using GSYNC.Core.Abstractions.Data;
using GSYNC.Core.Abstractions.Manifest;
using GSYNC.Core.Services.Sync;
using GSYNC.Core.Services;
using GSYNC.Manifest.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.UI.Xaml;
using Serilog;
using System.Diagnostics;
using GSYNC.Core.Updates;

namespace GSYNC.App;

public partial class App : Application
{
    private readonly ApplicationTaskHost _tasks = new();
    private Window? _window;
    private ServiceProvider? _services;
    private Task? _startupTask;
    private bool _shutdownComplete;
    private bool _shutdownStarted;
    private FileStream? _instanceLease;

    public CancellationToken LifetimeToken => _tasks.Token;
    public bool IsReady { get; private set; }

    public Task RunApplicationTaskAsync(Func<CancellationToken, Task> operation)
    {
        if (_tasks.IsStopping) return Task.CompletedTask;
        return _tasks.RunAsync(operation);
    }

    public App()
    {
        InitializeComponent();

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
        var startupSettings = UiSettingsStore.LoadStartupSettings(appPaths.GetUiSettingsPath());
        ApplyStartupDensity(startupSettings.DensityMode);
        Log.Logger = SerilogBootstrap.CreateLogger(appPaths.GetLogsDirectory(), startupSettings.LogLevel);
        Log.Information("GSYNC launch requested.");

        try
        {
            _instanceLease = new FileStream(Path.Combine(appPaths.GetAppDataRoot(), "app.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var recovery = UpdateTransaction.FindPendingRecovery(AppContext.BaseDirectory, appPaths.GetAppDataRoot());
            if (recovery is not null)
            {
                var helper = Path.Combine(recovery, "GSYNC.Updater.exe");
                if (!File.Exists(helper) || (File.GetAttributes(helper) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("The interrupted update needs recovery, but its recovery helper is missing. Backups are at " + recovery);
                var start = new ProcessStartInfo(helper) { UseShellExecute = false, CreateNoWindow = true };
                start.ArgumentList.Add("--recover");
                start.ArgumentList.Add(recovery);
                using var process = Process.Start(start) ?? throw new IOException("Could not start update recovery. Backups are at " + recovery);
                // Keep the lock until this process exits so a recovery started at sign-in cannot
                // replace files while this executable is still loaded.
                Exit();
                return;
            }
            Log.Information("Building application service provider.");
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(_tasks);
            serviceCollection.AddGsyncServices(Log.Logger);
            _services = serviceCollection.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

            Log.Information("Creating main window shell.");
            _window = _services.GetRequiredService<MainWindow>();
            _services.GetRequiredService<AppUpdateInstaller>().RestartRequested += () =>
            {
                if (!_window.DispatcherQueue.TryEnqueue(() => _window.Close()))
                    throw new InvalidOperationException("The application window could not be closed for update.");
            };
            if (_window.Content is FrameworkElement root)
            {
                root.ActualThemeChanged += (_, _) => AppPalette.Apply(Resources, root.ActualTheme == ElementTheme.Light);
                ApplyTheme(startupSettings.ThemeMode);
            }
            _window.AppWindow.Closing += OnWindowClosing;
            _window.Closed += OnWindowClosed;
            _window.Activate();
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "GSYNC failed before the main window shell could be shown.");
            throw;
        }
    }

    public void ApplyTheme(string themeMode)
    {
        if (_window?.Content is not FrameworkElement root) return;
        root.RequestedTheme = themeMode switch
        {
            AppUiSettings.ThemeLight => ElementTheme.Light,
            AppUiSettings.ThemeDark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
        AppPalette.Apply(Resources, root.ActualTheme == ElementTheme.Light);
    }

    public Task RunDeferredStartupAsync() => _startupTask ??= _tasks.RunAsync(InitializeCoreAsync);

    private async Task InitializeCoreAsync(CancellationToken cancellationToken)
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
            GSYNC.Core.Utilities.RecoverableFileTransaction.RecoverAll(Path.Combine(_services.GetRequiredService<IAppPathService>().GetAppDataRoot(), "transactions"));
            await initializer.InitializeAsync(cancellationToken);
            // Migrate legacy plaintext settings before any page or worker can read them.
            _ = _services.GetRequiredService<GSYNC.App.Infrastructure.Configuration.SyncTargetStore>().List();
            await _services.GetRequiredService<GSYNC.Data.Services.CredentialMigrationService>().MigrateBindingsAsync(cancellationToken);

            Log.Information("Seeding embedded manifest if needed.");
            await SeedEmbeddedManifestIfNeededAsync(_services, cancellationToken);

            var probeToken = Environment.GetEnvironmentVariable("GSYNC_UPDATE_PROBE_TOKEN");
            if (Guid.TryParseExact(probeToken, "N", out _))
            {
                var probeRoot = _services.GetRequiredService<IAppPathService>().GetAppDataRoot();
                GSYNC.Core.Utilities.AtomicFile.WriteAllText(Path.Combine(probeRoot, "update-health-" + probeToken), DesktopFeatures.CurrentVersion);
                _window?.DispatcherQueue.TryEnqueue(() => _window.Close());
                return;
            }

            Log.Information("Starting background services.");
            cancellationToken.ThrowIfCancellationRequested();
            StartBackgroundServices(_services);

            Log.Information("Refreshing community manifest if configured.");
            var settingsStore = _services.GetRequiredService<UiSettingsStore>();
            var appSettings = settingsStore.Load();
            var autoUpdate = _services.GetRequiredService<AutoUpdateCoordinator>();
            autoUpdate.Configure(appSettings.AutoCheckUpdates, appSettings.AutoInstallUpdates);
            settingsStore.Changed += value => autoUpdate.Configure(value.AutoCheckUpdates, value.AutoInstallUpdates);
            _ = _tasks.RunAsync(token => Task.Run(() => autoUpdate.RunAsync(token), token));
            if (appSettings.RefreshManifestOnStartup)
            {
                _ = _tasks.RunAsync(token => Task.Run(() => RefreshCommunityManifestIfConfiguredAsync(_services, token), token));
            }
            else
            {
                Log.Information("Startup settings disabled remote manifest refresh.");
            }
            IsReady = true;
            Log.Information("GSYNC deferred startup completed successfully.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
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

    private void ApplyStartupDensity(string densityMode)
    {
        if (!string.Equals(densityMode, AppUiSettings.DensityComfortable, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Resources["AppPagePadding"] = new Thickness(28, 28, 28, 28);
        Resources["AppInlinePadding"] = new Thickness(12, 10, 12, 10);
        Resources["AppPanelPadding"] = new Thickness(18, 18, 18, 18);
        Resources["AppDenseRowHeight"] = 42d;
        Resources["AppSectionSpacing"] = 18d;
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
        _ = _tasks.RunAsync(token => Task.Run(() => syncEngine.ProcessQueuedJobsAsync(token), token));
        var autoPull = services.GetRequiredService<AutoPullCoordinator>();
        var settings = services.GetRequiredService<UiSettingsStore>();
        autoPull.SetEnabled(settings.LoadForStartup().AutoPullEnabled);
        settings.Changed += value => autoPull.SetEnabled(value.AutoPullEnabled);
        _ = _tasks.RunAsync(token => Task.Run(() => autoPull.RunAsync(token), token));
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
        try
        {
            await manifestService.RefreshCommunityDefinitionsAsync(cancellationToken);
            Log.Information("Community manifest refresh completed.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception) { Log.Warning(exception, "Community refresh failed; existing definitions remain available."); }
    }

    private async void OnWindowClosing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        if (_shutdownComplete) return;
        args.Cancel = true;
        if (_shutdownStarted) return;
        _shutdownStarted = true;
        IsReady = false;
        if (_window?.Content is UIElement content) content.IsHitTestVisible = false;
        try { await _tasks.StopAsync(); }
        catch (Exception exception) { Log.Error(exception, "Background services failed while shutting down."); }
        Log.Information("Background services stopped; closing the main window.");
        _shutdownComplete = true;
        _window?.Close();
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _services?.GetService<AppUpdateInstaller>()?.ReleaseLeases();
        _services?.Dispose();
        _tasks.Dispose();
        _instanceLease?.Dispose();
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
