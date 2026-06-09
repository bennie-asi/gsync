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
        try
        {
            var path = _appPathService.GetUiSettingsPath();
            if (!File.Exists(path))
            {
                return new AppUiSettings();
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppUiSettings>(json, JsonOptions) ?? new AppUiSettings();
        }
        catch
        {
            return new AppUiSettings();
        }
    }

    public void Save(AppUiSettings settings)
    {
        Directory.CreateDirectory(_appPathService.GetAppDataRoot());
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_appPathService.GetUiSettingsPath(), json);
    }

    public static string LoadStartupLanguageTag(string settingsPath)
    {
        try
        {
            if (!File.Exists(settingsPath))
            {
                return AppUiSettings.DefaultLanguageTag;
            }

            var json = File.ReadAllText(settingsPath);
            return JsonSerializer.Deserialize<AppUiSettings>(json, JsonOptions)?.LanguageTag ?? AppUiSettings.DefaultLanguageTag;
        }
        catch
        {
            return AppUiSettings.DefaultLanguageTag;
        }
    }
}

public sealed class AppUiSettings
{
    public const string DefaultLanguageTag = "zh-CN";

    public string LanguageTag { get; set; } = DefaultLanguageTag;
}
