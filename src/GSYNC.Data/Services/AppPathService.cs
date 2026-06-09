using GSYNC.Core.Abstractions.Data;

namespace GSYNC.Data.Services;

public sealed class AppPathService : IAppPathService
{
    private readonly string _appDataRoot;

    public AppPathService()
    {
        var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _appDataRoot = Path.Combine(roamingAppData, "GSYNC");
    }

    public string GetAppDataRoot() => _appDataRoot;

    public string GetDatabasePath() => Path.Combine(_appDataRoot, "data.db");

    public string GetDefinitionsDirectory() => Path.Combine(_appDataRoot, "definitions");

    public string GetLogsDirectory() => Path.Combine(_appDataRoot, "logs");

    public string GetManifestCachePath() => Path.Combine(_appDataRoot, "community-manifest.yaml");

    public string GetSnapshotsDirectory() => Path.Combine(_appDataRoot, "snapshots");

    public string GetUiSettingsPath() => Path.Combine(_appDataRoot, "ui-settings.json");
}
