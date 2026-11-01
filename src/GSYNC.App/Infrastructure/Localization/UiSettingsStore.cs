using System.Text.Json;
using GSYNC.Core.Abstractions.Data;
using GSYNC.Core.Utilities;

namespace GSYNC.App.Infrastructure.Localization;

public sealed class UiSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly IAppPathService _appPathService;
    public event Action<AppUiSettings>? Changed;

    public UiSettingsStore(IAppPathService appPathService)
    {
        _appPathService = appPathService;
    }

    public AppUiSettings Load()
    {
        return LoadSettings(_appPathService.GetUiSettingsPath());
    }

    public AppUiSettings LoadForStartup() => LoadStartupSettings(_appPathService.GetUiSettingsPath());

    public void Save(AppUiSettings settings)
    {
        // Do not silently replace a damaged settings file with defaults.
        if (File.Exists(_appPathService.GetUiSettingsPath())) _ = Load();
        Directory.CreateDirectory(_appPathService.GetAppDataRoot());
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        AtomicFile.WriteAllText(_appPathService.GetUiSettingsPath(), json);
        Changed?.Invoke(settings);
    }

    public static string LoadStartupLanguageTag(string settingsPath)
    {
        return LoadStartupSettings(settingsPath).LanguageTag;
    }

    public static AppUiSettings LoadStartupSettings(string settingsPath)
    {
        try { return LoadSettings(settingsPath); }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        { return new AppUiSettings { RefreshManifestOnStartup = false, AutoCheckUpdates = false, AutoInstallUpdates = false }; }
    }

    private static AppUiSettings LoadSettings(string settingsPath)
    {
        try
        {
            if (!File.Exists(settingsPath))
            {
                return new AppUiSettings();
            }

            if (new FileInfo(settingsPath).Length > 1024 * 1024) throw new InvalidDataException("The UI settings file is too large.");
            var json = File.ReadAllText(settingsPath);
            return JsonSerializer.Deserialize<AppUiSettings>(json, JsonOptions) ?? throw new InvalidDataException("The UI settings file is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Cannot read UI settings from '{settingsPath}'. The original file was preserved.", exception);
        }
    }
}

public sealed class AppUiSettings
{
    public const string DefaultLanguageTag = "zh-CN";
    public const string ThemeSystem = "system";
    public const string ThemeDark = "dark";
    public const string ThemeLight = "light";
    public const string DensityCompact = "compact";
    public const string DensityComfortable = "comfortable";
    public const string DefaultLogLevel = "Information";

    public string LanguageTag { get; set; } = DefaultLanguageTag;

    public string ThemeMode { get; set; } = ThemeSystem;

    public string DensityMode { get; set; } = DensityCompact;

    public string LogLevel { get; set; } = DefaultLogLevel;

    public bool AutoSnapshotBeforeDownload { get; set; } = true;

    public bool AutoPullEnabled { get; set; }

    public bool RefreshManifestOnStartup { get; set; } = true;

    public bool AutoCheckUpdates { get; set; } = true;

    public bool AutoInstallUpdates { get; set; } = true;

    public bool CloseToTray { get; set; } = true;

    public bool StartupInBackground { get; set; } = true;

    public string? ManifestSourceUrl { get; set; }
}
