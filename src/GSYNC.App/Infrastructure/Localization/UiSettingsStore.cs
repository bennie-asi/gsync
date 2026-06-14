using System.Text.Json;
using GSYNC.Core.Abstractions.Data;

namespace GSYNC.App.Infrastructure.Localization;

public sealed class UiSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly IAppPathService _appPathService;

    public UiSettingsStore(IAppPathService appPathService)
    {
        _appPathService = appPathService;
    }

    public AppUiSettings Load()
    {
        return LoadSettings(_appPathService.GetUiSettingsPath());
    }

    public void Save(AppUiSettings settings)
    {
        Directory.CreateDirectory(_appPathService.GetAppDataRoot());
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_appPathService.GetUiSettingsPath(), json);
    }

    public static string LoadStartupLanguageTag(string settingsPath)
    {
        return LoadSettings(settingsPath).LanguageTag;
    }

    public static AppUiSettings LoadStartupSettings(string settingsPath)
    {
        return LoadSettings(settingsPath);
    }

    private static AppUiSettings LoadSettings(string settingsPath)
    {
        try
        {
            if (!File.Exists(settingsPath))
            {
                return new AppUiSettings();
            }

            var json = File.ReadAllText(settingsPath);
            return JsonSerializer.Deserialize<AppUiSettings>(json, JsonOptions) ?? new AppUiSettings();
        }
        catch
        {
            return new AppUiSettings();
        }
    }
}

public sealed class AppUiSettings
{
    public const string DefaultLanguageTag = "zh-CN";
    public const string ThemeDark = "dark";
    public const string ThemeLight = "light";
    public const string DensityCompact = "compact";
    public const string DensityComfortable = "comfortable";
    public const string DefaultLogLevel = "Information";

    public string LanguageTag { get; set; } = DefaultLanguageTag;

    public string ThemeMode { get; set; } = ThemeDark;

    public string DensityMode { get; set; } = DensityCompact;

    public string LogLevel { get; set; } = DefaultLogLevel;

    public bool AutoSnapshotBeforeDownload { get; set; } = true;

    public bool RefreshManifestOnStartup { get; set; } = true;

    public string? ManifestSourceUrl { get; set; }
}
